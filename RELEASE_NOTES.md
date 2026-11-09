# FFmpeg Forge CLI 1.0.0

First release of the command-line edition of FFmpeg Forge. `forge.exe` runs the conversion
engine of FFmpeg Forge 2.3.0 without the window: same presets, filters, upscalers, HDR,
spatial audio, AI engines and DVD / Blu-ray ripper, scriptable and batchable.

## Highlights

- `forge convert`: every format and codec of the FFmpeg build, quality / speed presets,
  resolution presets, trimming, filters, image sequences, extra audio tracks, covers.
- Full pipeline: deinterlacing (QTGMC / bwdif), AI or classic denoiser, Python / ONNX models,
  Scale (FSR 1/2/3, NIS, SGSR, Anime4K…), OptiScaler, SDR to HDR10 / Dolby Vision 8.1,
  RazorNext, VisionNext, SeedVR2, NVIDIA RTX Video.
- `forge rip`: DVD / Blu-ray ripping (drive, VIDEO_TS / BDMV folder, ISO) with title, track,
  format and subtitle choices. Discs without copy protection only.
- `forge info`, `formats`, `codecs`, `filters`, `scale-types`, `denoise-presets`, `layouts`,
  `status`, `install`.
- Folders, wildcards, recursion, progress bar with ETA, `--dry-run`, `--quiet`, exit codes.
- Portable package with FFmpeg 9.0.2 full build; falls back to `C:\FFmpeg\bin` and the PATH.
- Reuses the AI engines of an FFmpeg Forge installation with `--forge-dir`.

## Download

`FFmpegForge-CLI-1.0.0-Windows.rar` — extract, open `Forge console.cmd`, type `forge help`.

Requirements: Windows 10 / 11 64-bit, .NET Framework 4.x. GPU scaling needs Vulkan;
NVENC, RTX and the AI engines need an NVIDIA GPU.
