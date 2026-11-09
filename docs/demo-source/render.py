#!/usr/bin/env python3
"""Renders the FFmpeg Forge CLI demo GIF (Windows Terminal look) from real captured outputs."""
from PIL import Image, ImageDraw, ImageFont
import math

FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"
FS = 15
font = ImageFont.truetype(FONT, FS)
fontb = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf", FS)
ui = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
CW = font.getlength("M"); LH = 20
COLS, ROWS = 116, 22
PADX, PADY, TITLE = 18, 12, 38
W = int(PADX * 2 + CW * COLS); H = TITLE + PADY * 2 + LH * ROWS
SCALE = 1

BG = (12, 12, 12); FG = (204, 204, 204)
C = {"fg": FG, "white": (242, 242, 242), "dim": (118, 118, 118), "green": (22, 198, 12), "dyellow": (193, 156, 0),
     "yellow": (249, 241, 165), "cyan": (97, 214, 214), "gold": (245, 184, 61), "blue": (59, 120, 255), "prompt": (204, 204, 204)}

PROMPT = "PS D:\\Videos> "

class Term:
    def __init__(self):
        self.lines = []          # list of list of (text, color)
    def clear(self): self.lines = []
    def add(self, segs):
        # wrap at COLS like a real terminal
        line, n = [], 0
        for t, c in segs:
            while t:
                room = COLS - n
                part, t = t[:room], t[room:]
                line.append((part, c)); n += len(part)
                if n >= COLS and t:
                    self.lines.append(line); line, n = [], 0
        self.lines.append(line)
    def set_last(self, segs):
        if self.lines: self.lines.pop()
        self.add(segs)

def frame(term, cursor=None):
    im = Image.new("RGB", (W, H), (32, 32, 32))
    d = ImageDraw.Draw(im)
    # title bar (Windows Terminal): one tab + window controls
    d.rectangle([0, 0, W, TITLE], fill=(32, 32, 32))
    d.rounded_rectangle([10, 7, 250, TITLE + 6], radius=7, fill=BG)
    # tab icon: small terminal glyph
    d.rounded_rectangle([22, 15, 38, 29], radius=2, outline=(0, 120, 215), width=2)
    d.line([25, 19, 29, 22], fill=(220, 220, 220), width=2); d.line([29, 22, 25, 25], fill=(220, 220, 220), width=2)
    d.text((48, 13), "FFmpeg Forge CLI", font=ui, fill=(235, 235, 235))
    d.text((226, 12), "×", font=ui, fill=(170, 170, 170))
    d.text((266, 11), "+", font=ui, fill=(170, 170, 170))
    for i, g in enumerate(["—", "□", "✕"]):
        d.text((W - 130 + i * 46, 11), g, font=ui, fill=(200, 200, 200))
    d.rectangle([0, TITLE, W, H], fill=BG)
    vis = term.lines[-ROWS:]
    y = TITLE + PADY
    for li, line in enumerate(vis):
        x = PADX
        for t, c in line:
            f = fontb if c in ("white",) else font
            d.text((x, y), t, font=f, fill=C[c])
            x += CW * len(t)
        if cursor is not None and li == len(vis) - 1:
            n = sum(len(t) for t, _ in line)
            cx = PADX + CW * n
            if cursor: d.rectangle([cx, y + 2, cx + CW - 1, y + LH - 3], fill=FG)
        y += LH
    return im

frames, durs = [], []
def snap(term, ms, cursor=None):
    frames.append(frame(term, cursor)); durs.append(ms)

def blink(term, total_ms):
    on = True
    t = 0
    while t < total_ms:
        snap(term, 500, on); on = not on; t += 500

def typecmd(term, cmd):
    last = "".join(t for t, _ in term.lines[-1]) if term.lines else None
    if last != PROMPT: term.add([(PROMPT, "prompt")]); blink(term, 1000)
    else: blink(term, 500)
    typed = ""
    for i, ch in enumerate(cmd):
        typed += ch
        segs = [(PROMPT, "prompt")]
        # colour like PSReadLine: command yellow, options dim grey, rest white
        words = typed.split(" ")
        out = []
        for wi, w in enumerate(words):
            col = "yellow" if wi == 0 else ("dim" if w.startswith("-") else "white" if False else "fg")
            out.append((w, col))
            if wi < len(words) - 1: out.append((" ", "fg"))
        term.set_last(segs + out)
        snap(term, 45 if ch != " " else 90, True)
    snap(term, 350, True)

def out(term, lines, ms=55):
    for segs in lines:
        term.add(segs)
        snap(term, ms)

def bar(p, elapsed, eta, label):
    width = 28; fill = int(round(p * width))
    s = "   [" + "#" * fill + "-" * (width - fill) + "] " + ("%.1f" % (p * 100)).rjust(5) + " %  " + elapsed + (("  ETA " + eta) if eta else "") + "  " + label
    return [(s, "fg")]

def mmss(s): s = int(round(s)); return "%d:%02d" % (s // 60, s % 60)

def progress(term, total_real, label, steps=34, ms=90):
    term.add(bar(0, "0:00", None, label)); snap(term, 200)
    for i in range(1, steps + 1):
        p = i / steps
        el = total_real * p
        eta = mmss(total_real - el) if p < 1 else None
        term.set_last(bar(p, mmss(el), eta, label)); snap(term, ms)

t = Term()

# ---------------- scene 1: status
typecmd(t, "forge status")
st = [l.rstrip("\n") for l in open("/home/claude/demo/s1.txt", encoding="utf-8") if l.strip()]
lines = [[(st[0], "white")]]
for l in st[1:]:
    ok = l.strip().startswith("[ok]")
    body = l.strip()[5:]
    key, val = body[:16], body[16:]
    lines.append([("  ", "fg"), ("● " if ok else "○ ", "green" if ok else "dyellow"), (key, "fg"), (val, "fg" if ok else "dim")])
out(t, lines, 70)
t.add([(PROMPT, "prompt")]); blink(t, 2500)

# ---------------- scene 2: convert
t.clear()
typecmd(t, "forge trailer.mp4 -f mkv --vcodec h265 --quality high")
out(t, [[("FFmpeg Forge CLI 1.0.0  ·  FFmpeg 9.0.2-full_build-www.gyan.dev", "fg")],
        [("Output: MKV — Matroska  (next to the sources)   ·   1 file", "fg")],
        [("trailer.mp4", "white")],
        [("   video  ·  1920×1080 h264  ·  23.976 fps  ·  aac 2 ch  ·  0:20  ·  31.6 MB", "dim")]], 120)
progress(t, 14, "trailer.mp4")
t.lines.pop(); t.add(bar(1, "0:14", None, "trailer.mp4"))
out(t, [[("   ✓ D:\\Videos\\trailer.mkv  (13.5 MB)", "green")]], 200)
t.add([(PROMPT, "prompt")]); blink(t, 2200)

# ---------------- scene 3: rip --list
t.clear()
typecmd(t, "forge rip D:\\Discs\\HOLIDAYS_2025 --list")
s3 = [l.rstrip("\n") for l in open("/home/claude/demo/s3.txt", encoding="utf-8") if l.strip()]
lines = []
for l in s3:
    if l.startswith("Reading"): lines.append([(l, "fg")])
    elif l.startswith("DVD"): lines.append([(l, "white")])
    elif l.startswith(" *"): lines.append([(" * ", "gold"), (l[3:], "fg")])
    else: lines.append([(l, "dim")])
out(t, lines, 160)
t.add([]); t.add([(PROMPT, "prompt")]); blink(t, 2200)

# ---------------- scene 4: rip to MP4 H.265
typecmd(t, "forge rip D:\\Discs\\HOLIDAYS_2025 -f mp4 --video x265 --audio aac")
s4 = [l.rstrip("\n") for l in open("/home/claude/demo/s4.txt", encoding="utf-8") if l.strip() and "%" not in l]
lines = []
for l in s4:
    if l.startswith("   ✓"): continue
    if l.startswith("Reading"): lines.append([(l, "fg")])
    elif l.startswith("DVD") or l.startswith("Title"): lines.append([(l, "white")])
    elif l.startswith(" *"): lines.append([(" * ", "gold"), (l[3:], "fg")])
    elif "cannot store" in l: lines.append([(l, "yellow")])
    else: lines.append([(l, "fg")])
out(t, lines, 160)
progress(t, 125, "Title 01", steps=40, ms=85)
t.lines.pop(); t.add(bar(1, "2:05", None, "Title 01"))
out(t, [[("   ✓ 2:05 — 37.6 MB", "green")]], 200)
t.add([(PROMPT, "prompt")]); blink(t, 4000)

# ---------------- write the GIF (shared palette: few colours, small file)
pal_src = Image.new("RGB", (len(C) + 4, 1))
for i, col in enumerate(list(C.values()) + [BG, (32, 32, 32), (0, 120, 215), (170, 170, 170)]):
    pal_src.putpixel((i, 0), col)
sample = frames[::max(1, len(frames) // 16)] + [frames[-1]]
comp = Image.new("RGB", (W, H * len(sample) + 40))
for i, f in enumerate(sample): comp.paste(f, (0, H * i))
cols = list(C.values()) + [BG, (32, 32, 32), (0, 120, 215)]
for i, col in enumerate(cols):                       # every text colour, in solid blocks, so the palette keeps them exact
    ImageDraw.Draw(comp).rectangle([i * 40, H * len(sample), i * 40 + 39, H * len(sample) + 39], fill=col)
base = comp.quantize(colors=96, method=Image.Quantize.MEDIANCUT)
q = [f.quantize(palette=base, dither=Image.Dither.NONE) for f in frames]
q[0].save("/home/claude/demo/forge-cli-demo.gif", save_all=True, append_images=q[1:], duration=durs, loop=0, optimize=True, disposal=1)
print(len(frames), "frames", sum(durs) / 1000.0, "s", W, "x", H)
