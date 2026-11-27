# FFmpeg Forge 2.4.1 - LTX 2.5 Upscale through WanGP's Video Postprocessing.
# Runs WanGP (github.com/deepbeepmeep/WanGP, WanGP Community License 2.0) headless with its Python API:
#   shared.api.init(...) -> session.submit_media_postprocessing(video, spatial_upsampling="ltx25*2" | "ltx25_refine*<x>")
# WanGP downloads the LTX-2.5 model files (Lightricks, LTX-2 Community License) itself on the first run.
# Progress lines for FFmpeg Forge:  FORGE_PROGRESS <0..1> <phase>
import argparse
import os
import shutil
import sys
import tempfile
import time


def main():
    ap = argparse.ArgumentParser(description="FFmpeg Forge - LTX 2.5 Upscale (WanGP)")
    ap.add_argument("--wangp", required=True, help="WanGP folder (contains wgp.py)")
    ap.add_argument("--in", dest="src", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--method", default="ltx25", choices=("ltx25", "ltx25_refine"))
    ap.add_argument("--scale", default="2")
    ap.add_argument("--prompt", default="")
    ap.add_argument("--profile", default="4")
    ap.add_argument("--attention", default="sdpa")
    ap.add_argument("--seed", type=int, default=-1)
    a = ap.parse_args()

    root = os.path.abspath(a.wangp)
    src = os.path.abspath(a.src)
    out = os.path.abspath(a.out)
    if not os.path.isfile(os.path.join(root, "wgp.py")):
        print("FORGE_ERROR WanGP not found in " + root, flush=True)
        return 2
    os.chdir(root)
    if root not in sys.path:
        sys.path.insert(0, root)

    scale = float(a.scale)
    scale_txt = ("%g" % scale)
    spatial = a.method + "*" + scale_txt
    params = {}
    if a.method == "ltx25_refine" and a.prompt.strip():
        params["refine_prompt"] = a.prompt.strip()

    print("FORGE_PROGRESS 0 starting WanGP (" + spatial + ", profile " + a.profile + ")", flush=True)
    from shared.api import init

    class Callbacks:
        last = -1.0

        def on_progress(self, p):
            try:
                v = float(p.progress or 0) / 100.0      # ProgressUpdate.progress is a percentage (int)
                phase = (p.phase or p.status or "").strip()
                if p.window_index and p.window_count:
                    phase += " (window %d/%d)" % (p.window_index, p.window_count)
                if abs(v - self.last) >= 0.005 or v >= 1.0:
                    self.last = v
                    print("FORGE_PROGRESS %.4f %s" % (min(1.0, max(0.0, v)), phase), flush=True)
            except Exception:
                pass

        def on_stream(self, line):
            try:
                t = (line.text or "").rstrip()
                if t:
                    print(t, flush=True)
            except Exception:
                pass

        def on_error(self, error):
            print("FORGE_ERROR " + str(getattr(error, "message", error)), flush=True)

    work = tempfile.mkdtemp(prefix="forge_ltx25_")
    try:
        cli = ["--attention", a.attention, "--profile", str(a.profile)]
        session = init(root=root, output_dir=work, cli_args=cli, console_output=True, console_isatty=False)
        t0 = time.time()
        job = session.submit_media_postprocessing(src, spatial_upsampling=spatial, spatial_postprocessing_parameters=params,
                                                  seed=a.seed, callbacks=Callbacks())
        res = job.result()
        if not res.success:
            for e in res.errors:
                print("FORGE_ERROR " + str(e), flush=True)
            return 4 if res.cancelled else 3
        files = [f for f in res.generated_files if os.path.splitext(f)[1].lower() in (".mp4", ".mkv", ".mov", ".webm")]
        if not files:
            files = [os.path.join(work, f) for f in sorted(os.listdir(work)) if f.lower().endswith((".mp4", ".mkv", ".mov"))]
        if not files:
            print("FORGE_ERROR WanGP did not write a video (generated: %r)" % (res.generated_files,), flush=True)
            return 3
        os.makedirs(os.path.dirname(out), exist_ok=True)
        shutil.copyfile(files[0], out)
        print("FORGE_PROGRESS 1 done in %.0f s" % (time.time() - t0), flush=True)
        return 0
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as ex:
        import traceback
        traceback.print_exc()
        print("FORGE_ERROR " + str(ex), flush=True)
        sys.exit(1)
