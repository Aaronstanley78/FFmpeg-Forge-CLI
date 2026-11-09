@echo off
REM Rip the longest title of the disc in drive E: to MKV (lossless). Change E: to your drive.
cd /d "%~dp0.."
forge rip E: --list
forge rip E: --format mkv
pause
