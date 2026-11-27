@echo off
REM Upscale every video of a folder x2 with OptiPulsar (NERVE, built-in engine, no Visual Enhancer needed).
REM First time only:  forge install optipulsar
cd /d "%~dp0.."
forge convert "%~1" -r --upscale optipulsar --upscale-factor 2 -f mp4 --vcodec h265 --quality high
pause
