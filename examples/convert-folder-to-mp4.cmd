@echo off
REM Every video of a folder (and its sub-folders) to MP4 H.265, high quality, into a "Converted" folder.
cd /d "%~dp0.."
forge convert "%~1" -r -f mp4 --vcodec h265 --quality high -o "%~1\Converted"
pause
