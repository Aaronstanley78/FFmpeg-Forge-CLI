@echo off
REM LTX 2.5 Detail Refiner x1.5 through WanGP (first time:  forge install wangp , tens of GB on the first run).
cd /d "%~dp0.."
forge "%~1" --ltx refiner --ltx-factor 1.5 --ltx-profile 4 -f mp4
pause
