@echo off
REM Deinterlace an old video and upscale it x2 with RazorNext (run "forge install razornext" once).
cd /d "%~dp0.."
forge "%~1" -f mkv --deinterlace double --upscale razornext --upscale-factor 2 --denoise
pause
