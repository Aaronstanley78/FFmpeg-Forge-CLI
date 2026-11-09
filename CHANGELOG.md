# Changelog

## 1.0.0 — 2026-11-09

First release of FFmpeg Forge CLI, built on the engine of FFmpeg Forge 2.3.0.

- Commands: `convert`, `rip`, `info`, `formats`, `codecs`, `filters`, `scale-types`,
  `denoise-presets`, `layouts`, `status`, `install`, `version`, `help`.
- Full FFmpeg Forge pipeline: deinterlacing (QTGMC / bwdif) → AI denoiser → Python / ONNX
  model → final engine (FFmpeg, DSD, RazorNext / VisionNext, SeedVR2, NVIDIA RTX) → image
  sequence → Dolby Vision 8.1 → cover.
- DVD / Blu-ray ripper (dvdvideo, libbluray) with title, track, format and subtitle choices.
- Folders, wildcards and recursion; progress bar with ETA; `--dry-run`, `--quiet`,
  `--verbose`, `--no-color`; exit codes.
- Portable package with FFmpeg 9.0.2 full build; falls back to `C:\FFmpeg\bin` and the PATH.
- `--forge-dir` / `FFMPEG_FORGE_DIR` to reuse the AI engines of a FFmpeg Forge installation.
