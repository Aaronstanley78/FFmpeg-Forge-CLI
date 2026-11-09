# Command reference

Output of `forge help`, `forge help convert` and `forge help rip`.

```text
FFmpeg Forge CLI 1.0 — the FFmpeg Forge 2.3.0 engine on the command line

USAGE
  forge <command> [options]
  forge FILE|FOLDER… [convert options]        (same as forge convert)

COMMANDS
  convert FILE|FOLDER…   convert videos, audio and pictures      forge help convert
  rip SOURCE             rip a DVD / Blu-ray (drive, folder, ISO)  forge help rip
  info FILE…             show what a file contains
  formats [video|audio|image] [--all]     output formats
  codecs [SEARCH]        video / audio codecs and every encoder of the build
  filters [SEARCH]       every FFmpeg filter of the build
  scale-types            --scale types (FSR, NIS, SGSR, Anime4K, xBR…)
  denoise-presets        --denoise-preset values
  layouts                --channels values (5.1, 7.1.4 Atmos beds, spatial headphones…)
  status                 FFmpeg, GPU features, AI engines installed
  install ENGINE         razornext | visionnext | seedvr2 | qtgmc | ai   (Windows)
  version                version of the CLI and of FFmpeg

GLOBAL OPTIONS
  --ffmpeg PATH          FFmpeg to use (file or folder). Default: bin\ next to forge.exe,
                         then C:\FFmpeg\bin, then PATH
  --forge-dir FOLDER     use the AI engines / models / settings of a FFmpeg Forge folder
                         (e.g. D:\FFmpeg). Environment: FFMPEG_FORGE_DIR
  --dry-run              print the commands, convert nothing
  -q, --quiet            only results and errors       -v, --verbose   every FFmpeg line
  --no-color             plain text output (also NO_COLOR=1)

EXIT CODES  0 ok · 1 at least one file failed · 2 usage error · 130 cancelled (Ctrl+C)

EXAMPLES
  forge movie.mkv -f mp4
  forge convert "D:\Videos" -r -f mp4 --vcodec h265 --quality high -o "D:\Out"
  forge clip.mp4 --scale fsr --scale-factor 2 --denoise
  forge old.avi --deinterlace double --upscale razornext --upscale-factor 2 -f mkv
  forge song.flac -f mp3 --abitrate 320
  forge concert.mkv -f mkv --channels 7.1.4 --upmix spatial
  forge rip E:\ --format mkv

```

```text
forge convert FILE|FOLDER|PATTERN… [options]

OUTPUT
  -f, --format EXT       mp4 mkv mov webm avi … | mp3 flac wav aac opus … | png jpg webp avif …
  -t, --type KIND        video | audio | image (when the extension is ambiguous)
  -o, --output FOLDER    output folder (default: next to each source)
  --output-file PATH     exact output file (one input only)
  -y, --overwrite        replace existing files (default: name (2).ext)
  -r, --recursive        include sub-folders
VIDEO
  --vcodec KEY           h264 h265 av1 vp9 prores_hq dnxhr_hq nvh264 nvh265 … or enc:NAME  (forge codecs)
  --quality Q            max | very-high | high | medium | small    (0-4)
  --preset P             ultrafast veryfast fast medium slow veryslow (0-5)
  --vbitrate KBPS        target bitrate (0 = quality mode)      --nvcq N   NVENC constant quality
  --resolution WxH       or 8k 4k dci4k 1440p 1080p 720p 576p 480p   (0 = automatic side)
  --fit MODE             stretch | fit (black bars) | fill (crop)
  --fps N                frame rate          --rotate 90|180|270|hflip|vflip
  --deinterlace MODE     off | double (50/60p) | same (25/30p)  — QTGMC, else bwdif
  --field-order O        auto | tff | bff
  --keep-subs            keep subtitle tracks       --still-duration S   picture → video length
  --sequence png|jpg     export every frame as pictures
AUDIO
  --acodec KEY           aac mp3 opus flac ac3 eac3 pcm24 … copy none     --abitrate KBPS
  --rate HZ              sample rate        --channels LAYOUT  (forge layouts)
  --upmix STYLE          auto | spatial | music | cinema | live | off
  --normalize            EBU R128 loudness     --volume PCT
  --keep-video           change only the audio (video copied)
  --existing-audio M     convert | copy | remove
  --add-audio FILE[:lang[:title[:delayMs]]]   add an audio track (repeatable)
IMAGE
  --img-quality 1-100    --extract single|all|every:SECONDS    --at SECONDS (frame to extract)
TRIM / FILTERS
  --start T  --end T     seconds or hh:mm:ss
  --vf FILTER  --af FILTER   (repeatable, in order)     --filter-complex GRAPH  --extra-input FILE
  --custom "ARGS"        extra FFmpeg output arguments
SCALE / OPTISCALER
  --scale TYPE           fsr fsr2 fsr3 nis sgsr anime4k xbr hqx integer ewa lanczos … (forge scale-types)
  --scale-factor X       1.5 2 2.5 3 4, or res (= --resolution)     --sharpness 0-100
  --no-gpu-scale  --no-fidelity  --no-444
  --optiscaler  --opti-mult 1.25-3  --opti-kernel 1-7  --opti-sharpness 0-100  --opti-contrast
DENOISER
  --denoise              AI denoiser when installed (SCUNet…), else the classic Neat Video-style one
  --denoise-classic      always the classic denoiser       --denoise-ai MODEL  (scunet or models\*.pth)
  --denoise-preset P     (forge denoise-presets)   --denoise-engine balanced|hq|fast
  --denoise-strength 0-200   --denoise-sharpen 0-100   --no-temporal  --no-auto-profile
HDR
  --hdr hdr10|dv         SDR → HDR10 / Dolby Vision 8.1        --hdr-peak 600|1000|1500|2000|4000
AI UPSCALE
  --upscale razornext|visionnext   --upscale-factor N   --upscale-engine builtin|ve
  --cpu  --tile 256|512  --memory-budget auto|unlimited|12|8|6  --ve-dir FOLDER
  --seedvr2  --seed-model FILE  --seed-size 0-8  --seed-batch N  --blockswap N  --vae-tiled  --offload
  --ai-model FILE        your Python / ONNX model    --ai-keep-size --ai-cpu --ai-fp32 --ai-tile N
NVIDIA RTX
  --rtx-vsr  --vsr-quality 1-4  --vsr-scale 1-4  --rtx-hdr  --dlss  --rtx-engine auto|native|driver|processor
COVER
  --cover frame:SECONDS | file:PICTURE    (MP4 / M4V / MKV)

```

```text
forge rip SOURCE [options]          SOURCE = drive (E:\), VIDEO_TS / BDMV folder, or .iso

  forge rip E:\ --list               list the titles / playlists (languages, chapters…)
  forge rip E:\                      rip the longest title to MKV (lossless copy)
  forge rip E:\ --title 1,3 --format mp4 --video h265 --audio aac

  --list                 show the titles and their tracks, rip nothing
  --title N[,N…]         titles (DVD) / playlist numbers (Blu-ray) to rip   --all   every title
  --show-short           include titles shorter than 1 minute
  --audio-tracks N[,N…]  audio tracks to keep (1 = first; default all)
  --subs N[,N…]|none     subtitle tracks to keep (default all)
  --format F             mkv mp4 mov webm | audio only: flac mp3 m4a wav mka
  --video V              copy x264 x265 av1 nv264 nv265 nvav1          (default copy)
  --quality 0-3          very high … small file      --speed slow|medium|fast
  --max-height H         1080 720 576 480            --deinterlace auto|off|always
  --audio A              copy aac ac3 eac3 flac opus mp3     --abitrate KBPS   --downmix
  --subtitles M          keep | none | burn (first selected track)
  --no-chapters          -o, --output FOLDER

Copy-protected discs (CSS, AACS, BD+) are not supported: FFmpeg Forge rips your own
videos, home-made discs and discs without copy protection.

```

