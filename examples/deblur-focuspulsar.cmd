@echo off
REM Deblur a video with FocusPulsar (runs through your Visual Enhancer installation).
REM Change the folder below to the one that contains VE_CLI.exe.
cd /d "%~dp0.."
forge "%~1" --deblur --deblur-strength 120 --ve-dir "C:\Program Files\Visual Enhancer" -f mp4
pause
