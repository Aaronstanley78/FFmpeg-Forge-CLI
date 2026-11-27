# Changelog

## 2.0.0 — 2026-11-27

Built on the engine of FFmpeg Forge 3.0.1 (was 2.3.0).

- New `--upscale optipulsar`: NERVE upscaler (Philip Hofmann, Apache-2.0) with a built-in engine,
  x1 / x2 / x4 / x8, GPU or `--cpu`; `forge install optipulsar`. `--upscale-engine ve` also works.
- New `--deblur` / `--deblur-strength 0-200`: FocusPulsar deblurring through an existing Visual
  Enhancer installation (`--ve-dir`); optional `VE-presets\focuspulsar.json`.
- New `--ltx upscaler|refiner`, `--ltx-factor`, `--ltx-prompt`, `--ltx-profile`, `--ltx-attention`,
  `--ltx-seed`: LTX 2.5 Upscale through WanGP's Video Postprocessing; `forge install wangp` or
  `--wangp-dir` / `FFMPEG_FORGE_WANGP`.
- Pipeline: deinterlacing → AI denoiser → deblurring → Python / ONNX model → final engine
  (… OptiPulsar, LTX 2.5 …) → image sequence → Dolby Vision → cover.
- `forge status`: OptiPulsar, LTX 2.5 (WanGP) and Visual Enhancer lines; new error hints.
- Windows installer `FFmpegForge-CLI-2.0.0-Setup.exe` (NSIS, per-user, no administrator rights):
  user PATH, Start menu shortcuts, uninstaller; source in `installer/forge-cli.nsi`.
- New examples: `upscale-optipulsar.cmd`, `deblur-focuspulsar.cmd`, `ltx25-refine.cmd`.

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
