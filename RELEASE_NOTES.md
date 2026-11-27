# FFmpeg Forge CLI 2.0.0

The command-line edition of FFmpeg Forge moves to the engine of **FFmpeg Forge 3.0.1**. Version 1.0 used the 2.3.0 engine.

## New

- **OptiPulsar upscaler**, built in: `--upscale optipulsar --upscale-factor 1|2|4|8`.
  - The model is NERVE by Philip Hofmann (Apache-2.0).
  - Install it once with `forge install optipulsar` (Python, PyTorch CUDA and the official weights, about 3 GB).
  - No Visual Enhancer needed. Runs on the GPU, or on the processor with `--cpu`.
- **Deblurring** with FocusPulsar: `--deblur --deblur-strength 0-200`.
  - Runs through an existing Visual Enhancer installation (`--ve-dir`).
- **LTX 2.5 Upscale** through WanGP's Video Postprocessing:
  - `--ltx upscaler`: Pixel Spatial Upscaler, x2.
  - `--ltx refiner --ltx-factor 1|1.5|2`: Detail Refiner.
  - Settings: `--ltx-prompt`, `--ltx-profile`, `--ltx-attention`, `--ltx-seed`.
  - Install with `forge install wangp`, or use an existing WanGP folder with `--wangp-dir`.
- **Windows installer** `FFmpegForge-CLI-2.0.0-Setup.exe`:
  - Installs forge for your Windows account, with no administrator rights.
  - Adds forge to the PATH and creates Start menu shortcuts (Forge console, command reference).
  - Can be removed from Settings > Apps.
- `forge status` shows OptiPulsar, LTX 2.5 (WanGP) and Visual Enhancer. Error hints are added for the new engines.
- New example scripts: `upscale-optipulsar.cmd`, `deblur-focuspulsar.cmd`, `ltx25-refine.cmd`.

## Examples

```text
forge clip.mp4 --upscale optipulsar --upscale-factor 4 -f mp4 --vcodec h265
forge soft.mp4 --deblur --deblur-strength 120 --ve-dir "C:\Program Files\Visual Enhancer" --upscale optipulsar
forge draft.mp4 --ltx refiner --ltx-factor 1.5 --ltx-prompt "sharp detail, natural film grain"
```

## Downloads

| File | Content |
| --- | --- |
| `FFmpegForge-CLI-2.0.0-Setup.exe` | Windows installer (per user, adds forge to the PATH), with FFmpeg 9.0.2 full build |
| `FFmpegForge-CLI-2.0.0-Windows.rar` | portable edition: extract anywhere, open `Forge console.cmd` |

## Requirements

- Windows 10 / 11 64-bit with .NET Framework 4.x.
- AI engines: an NVIDIA GPU is recommended.
- `--deblur`: an installed Visual Enhancer with the FocusPulsar model downloaded.

## Licence

GNU GPL v3.

- FFmpeg, dovi_tool, NERVE, WanGP, the LTX-2.5 models and the other AI models keep their own licences. Their models are downloaded from the official sources at installation time and are not redistributed.
- FFmpeg Forge CLI does not contain any Visual Enhancer file and is not affiliated with Merserk.
- Copy-protected discs (CSS, AACS, BD+) are not supported.
