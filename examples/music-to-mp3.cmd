@echo off
REM FLAC / WAV / any audio of a folder to MP3 320 kb/s, loudness normalised.
cd /d "%~dp0.."
forge convert "%~1" -r -f mp3 -t audio --abitrate 320 --normalize -o "%~1\MP3"
pause
