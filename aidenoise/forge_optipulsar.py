# FFmpeg Forge 3.0.1 - OptiPulsar built-in streaming helper.
# OptiPulsar is Visual Enhancer's name for NERVE ("Norm-free Efficient Restoration for Various Edge devices") by
# Philip Hofmann (huggingface.co/Phips/NERVE, Apache-2.0): 5x5 conv stem -> 24 x (conv-ReLU-conv + residual)
# -> conv + PixelShuffle, added to a bicubic upscale of the input. FFmpeg Forge's own implementation (GPL-3.0) of that
# published architecture; the official weights are downloaded by the installer, not redistributed.
# The layer sizes are read from the checkpoint itself, so any NERVE release (2x / 4x) loads whatever its key names.
# Reads raw RGB24 frames on stdin and writes the upscaled RGB24 frames on stdout (no picture files, one single encode).
#   python -u forge_optipulsar.py -w W -h H -u 1|2|4|8 -device cuda|cpu -m2 2x.safetensors -m4 4x.safetensors
import argparse, os, re, sys, time


def log(msg):
    sys.stderr.write("ForgeOP " + msg + "\n"); sys.stderr.flush()


def natural(k):
    return [int(t) if t.isdigit() else t for t in re.split(r"(\d+)", k)]


def build(path, torch, nn, F):
    from safetensors.torch import load_file
    sd = load_file(path)
    for pre in ("params_ema.", "params.", "module.", "model."):
        if all(k.startswith(pre) for k in sd): sd = {k[len(pre):]: v for k, v in sd.items()}
    keys = sorted(sd.keys(), key=natural)
    convs = [k for k in keys if sd[k].dim() == 4]
    if len(convs) < 3: raise RuntimeError("not a NERVE checkpoint (%d conv layers) in %s" % (len(convs), path))
    biases = {}
    for k in convs:
        b = k[:-len("weight")] + "bias" if k.endswith("weight") else None
        if b and b in sd: biases[k] = sd[b]
    # stem: the conv that reads the RGB image; head: the conv that writes 3 x scale^2 channels; body: the rest in order
    stems = [k for k in convs if sd[k].shape[1] == 3]
    if not stems: raise RuntimeError("no RGB input layer in %s" % path)
    stem_k = stems[0]
    dim0 = sd[stem_k].shape[0]
    heads = [k for k in convs if k != stem_k and sd[k].shape[1] == dim0 and sd[k].shape[0] != dim0 and sd[k].shape[0] % 3 == 0]
    if not heads: raise RuntimeError("no upsampling layer in %s" % path)
    up_k = heads[-1]
    body = [k for k in convs if k not in (stem_k, up_k)]
    if len(body) % 2: raise RuntimeError("unexpected NERVE layout (%d body convs)" % len(body))
    dim, cin = sd[stem_k].shape[0], sd[stem_k].shape[1]
    cout_up = sd[up_k].shape[0]
    scale = int(round((cout_up / cin) ** 0.5))

    def conv(k):
        w = sd[k]
        c = nn.Conv2d(w.shape[1], w.shape[0], w.shape[2], padding=w.shape[2] // 2, bias=k in biases)
        c.weight.data.copy_(w.float())
        if k in biases: c.bias.data.copy_(biases[k].float())
        return c

    class Block(nn.Module):
        def __init__(self, a, b):
            super().__init__(); self.c1 = conv(a); self.c2 = conv(b)
        def forward(self, x):
            return x + self.c2(F.relu(self.c1(x)))

    class Nerve(nn.Module):
        def __init__(self):
            super().__init__()
            self.stem = conv(stem_k)
            self.blocks = nn.Sequential(*[Block(body[i], body[i + 1]) for i in range(0, len(body), 2)])
            self.up = conv(up_k)
            self.ps = nn.PixelShuffle(scale)
            self.scale = scale
        def forward(self, x):
            f = self.stem(x)
            out = self.ps(self.up(f + self.blocks(f)))
            return out + F.interpolate(x, scale_factor=self.scale, mode="bicubic", align_corners=False)

    m = Nerve().eval()
    log("loaded %s: %d blocks, %d channels, x%d" % (os.path.basename(path), len(body) // 2, dim, scale))
    return m


def main():
    ap = argparse.ArgumentParser(add_help=False)
    ap.add_argument("-w", type=int, required=True)
    ap.add_argument("-h", type=int, required=True)
    ap.add_argument("-u", type=int, default=2)
    ap.add_argument("-device", default="cuda")
    ap.add_argument("-m2", required=True)
    ap.add_argument("-m4", required=True)
    ap.add_argument("-tile", type=int, default=768)
    a = ap.parse_args()
    if a.u not in (1, 2, 4, 8): log("unsupported upscale factor %d (OptiPulsar: x1, x2, x4 or x8)" % a.u); sys.exit(2)
    real_out = sys.stdout.buffer
    sys.stdout = open(os.devnull, "w")
    import numpy as np, torch
    import torch.nn as nn, torch.nn.functional as F
    dev = torch.device(a.device if (a.device != "cuda" or torch.cuda.is_available()) else "cpu")
    if a.device == "cuda" and dev.type != "cuda": log("CUDA is not available: running on the CPU (slow)")
    half = dev.type == "cuda"
    plan = {1: [a.m2], 2: [a.m2], 4: [a.m4], 8: [a.m4, a.m2]}[a.u]
    models = []
    for p in plan:
        m = build(p, torch, nn, F).to(dev)
        if half: m = m.half()
        models.append(m)
    W, H = a.w, a.h
    outW, outH = W * a.u, H * a.u
    PAD = 48                                    # receptive field of 24 blocks + stem (~50 px): tiles overlap by this

    def run(m, x):
        _, _, h, w = x.shape
        s = m.scale
        if h * w <= a.tile * a.tile * 4:
            return m(x)
        out = torch.empty((1, 3, h * s, w * s), device=x.device, dtype=x.dtype)
        t = a.tile
        for y0 in range(0, h, t):
            for x0 in range(0, w, t):
                y1, x1 = min(h, y0 + t), min(w, x0 + t)
                ya, xa = max(0, y0 - PAD), max(0, x0 - PAD)
                yb, xb = min(h, y1 + PAD), min(w, x1 + PAD)
                r = m(x[:, :, ya:yb, xa:xb])
                out[:, :, y0 * s:y1 * s, x0 * s:x1 * s] = r[:, :, (y0 - ya) * s:(y0 - ya + y1 - y0) * s, (x0 - xa) * s:(x0 - xa + x1 - x0) * s]
        return out

    fsize = W * H * 3
    stdin = sys.stdin.buffer
    n, t0 = 0, time.time()
    log("OptiPulsar x%d on %s%s: %dx%d -> %dx%d" % (a.u, dev.type.upper(), " (fp16)" if half else "", W, H, outW, outH))
    with torch.inference_mode():
        while True:
            buf = stdin.read(fsize)
            while buf is not None and 0 < len(buf) < fsize:
                more = stdin.read(fsize - len(buf))
                if not more: break
                buf += more
            if not buf or len(buf) < fsize: break
            x = torch.from_numpy(np.frombuffer(buf, dtype=np.uint8).copy()).to(dev).view(H, W, 3).permute(2, 0, 1).unsqueeze(0)
            x = x.half() / 255.0 if half else x.float() / 255.0
            for m in models: x = run(m, x)
            if a.u == 1: x = F.interpolate(x.float(), size=(H, W), mode="area")
            y = (x.float().clamp(0, 1) * 255.0 + 0.5).byte().squeeze(0).permute(1, 2, 0).contiguous().cpu().numpy()
            real_out.write(y.tobytes())
            n += 1
            if n % 24 == 0: log("%d frames, %.2f fps" % (n, n / max(1e-6, time.time() - t0)))
    real_out.flush()
    log("done: %d frames in %.1f s" % (n, time.time() - t0))


if __name__ == "__main__":
    try:
        main()
    except Exception as ex:
        import traceback; traceback.print_exc()
        log("error: %s" % ex); sys.exit(1)
