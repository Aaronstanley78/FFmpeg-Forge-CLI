using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FFmpegForge.CommandLine
{
    public static partial class Cli
    {
        /// Engine folders saved by FFmpeg Forge (window) in its settings.ini: the CLI uses the same installations.
        static void LoadForgeSettings()
        {
            try
            {
                string ini = Path.Combine(AppPaths.DataDir, "settings.ini");
                if (!File.Exists(ini)) return;
                foreach (var l in File.ReadAllLines(ini, Encoding.UTF8))
                {
                    int i = l.IndexOf('='); if (i <= 0) continue;
                    string k = l.Substring(0, i), v = l.Substring(i + 1).Trim();
                    if (v.Length == 0) continue;
                    if (k == "vn_dir" && VisionEngine.Resolve(VisionEngine.Dir) == null) VisionEngine.Dir = v;
                    else if (k == "vn_builtin_dir") VisionBuiltin.Dir = v;
                    else if (k == "seed_dir" && string.IsNullOrEmpty(SeedEngine.Dir)) SeedEngine.Dir = v;
                    else if (k == "ai_dn_dir") AiDenoise.Dir = v;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(SeedEngine.Dir) && SeedEngine.ScriptDir(SeedEngine.DefaultInstallDir) != null) SeedEngine.Dir = SeedEngine.DefaultInstallDir;
        }

        // ------------------------------------------------------------------ info
        static int CmdInfo(Args a)
        {
            Init(a);
            a.CheckUnused();
            if (a.Positional.Count == 0) throw new UsageException("forge info <file> [file…]");
            foreach (var f in ExpandInputs(a.Positional, false))
            {
                var j = new Job { Path = f }; Runner.Probe(j);
                Out(Path.GetFileName(f), ConsoleColor.White);
                Out("   " + Describe(j));
                if (j.HasVideo) Out("   video: " + j.VideoCodec + (string.IsNullOrEmpty(j.VideoProfile) ? "" : " (" + j.VideoProfile + ")") + "  " + j.PixFmt + (j.IsHdr ? "  HDR (" + j.ColorTransfer + ")" : "") + (j.IsInterlaced ? "  interlaced (" + j.FieldOrder + ")" : "") + (j.VideoBitrate > 0 ? "  " + j.VideoBitrate / 1000 + " kb/s" : ""));
                if (j.HasAudio) Out("   audio: " + j.AudioStreams + " track(s), first " + j.AudioCodec + " " + j.AudioChannels + " ch " + (j.AudioLayout ?? "") + (j.AudioBitrate > 0 ? "  " + j.AudioBitrate / 1000 + " kb/s" : ""));
                if (j.SubtitleStreams > 0) Out("   subtitles: " + j.SubtitleStreams);
            }
            return 0;
        }

        // ------------------------------------------------------------------ lists
        static int CmdFormats(Args a)
        {
            Init(a);
            bool all = a.Has("all");
            a.CheckUnused();
            try { FormatCatalog.Load(); } catch { }
            MediaKind? only = a.Positional.Count > 0 ? (MediaKind?)ParseKind(a.Positional[0]) : null;
            foreach (var k in new[] { MediaKind.Video, MediaKind.Audio, MediaKind.Image })
            {
                if (only != null && only.Value != k) continue;
                Out(k.ToString().ToUpperInvariant() + " — tuned presets  (forge convert FILE -f EXT" + (k != MediaKind.Video ? " -t " + k.ToString().ToLowerInvariant() : "") + ")", ConsoleColor.Yellow);
                foreach (var f in Formats.Of(k)) Out("  " + f.Ext.PadRight(8) + f.Label.PadRight(46) + Codecs(f));
                if (all)
                {
                    Out(k.ToString().ToUpperInvariant() + " — every other format of your FFmpeg build", ConsoleColor.Yellow);
                    foreach (var f in FormatCatalog.Dynamic.Where(x => x.Kind == k)) Out("  " + f.Ext.PadRight(8) + f.Label);
                }
            }
            if (!all) Dim("(add --all for every muxer of your FFmpeg build)");
            return 0;
        }

        static string Codecs(OutFormat f)
        {
            var p = new List<string>();
            if (!string.IsNullOrEmpty(f.VCodec) && f.Kind == MediaKind.Video) p.Add("--vcodec " + f.VCodec);
            if (!string.IsNullOrEmpty(f.ACodec) && f.ACodec != "none") p.Add("--acodec " + f.ACodec);
            return p.Count == 0 ? "" : "[" + string.Join(" ", p.ToArray()) + "]";
        }

        static int CmdCodecs(Args a)
        {
            Init(a);
            a.CheckUnused();
            try { FormatCatalog.Load(); } catch { }
            Out("VIDEO (--vcodec)", ConsoleColor.Yellow);
            foreach (var c in Formats.VideoCodecs) Out("  " + c.Key.PadRight(18) + c.ToString());
            Out("AUDIO (--acodec)", ConsoleColor.Yellow);
            foreach (var c in Formats.AudioCodecsForVideo) Out("  " + c.Key.PadRight(18) + c.ToString());
            Out("Every encoder of your FFmpeg build can also be used as  enc:NAME  (e.g. --vcodec enc:libvvenc):", ConsoleColor.Yellow);
            foreach (var e in FormatCatalog.Encoders.Where(e => e.Type == 'V' || e.Type == 'A'))
                if (a.Positional.Count == 0 || e.Name.IndexOf(a.Positional[0], StringComparison.OrdinalIgnoreCase) >= 0) Out("  " + e.Type + "  enc:" + e.Name.PadRight(22) + e.Desc);
            return 0;
        }

        static int CmdFilters(Args a)
        {
            Init(a);
            a.CheckUnused();
            FilterCatalog.Load();
            string q = a.Positional.Count > 0 ? a.Positional[0] : null;
            var list = FilterCatalog.All.Where(f => q == null || f.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || (f.Desc ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (var f in list) Out("  " + f.Name.PadRight(22) + (f.In + "->" + f.Out).PadRight(9) + f.Desc);
            Dim(list.Count + " filter(s).  Use them with --vf \"name=opt=value\" / --af \"…\"  (or --filter-complex)");
            return 0;
        }

        static int CmdList(Args a, string what)
        {
            a.CheckUnused();
            if (what == "scale")
            {
                Out("--scale TYPE   (GPU types need Vulkan / libplacebo)", ConsoleColor.Yellow);
                foreach (var t in Scaling.Types) Out("  " + t.Key.PadRight(15) + t.Label + (t.GpuOnly ? "  [GPU]" : ""));
            }
            else if (what == "denoise")
            {
                Out("--denoise-preset", ConsoleColor.Yellow);
                string[] keys = { "auto", "light", "medium", "strong", "very-strong", "film-grain", "low-light", "animation", "vhs" };
                for (int i = 0; i < Denoise.Presets.Count && i < keys.Length; i++) Out("  " + keys[i].PadRight(13) + Denoise.Presets[i].Name);
                Out("--denoise-engine:  balanced | hq | fast", ConsoleColor.Yellow);
            }
            else
            {
                Out("--channels LAYOUT", ConsoleColor.Yellow);
                foreach (var l in ChannelLayouts.All.Where(x => x.Key != "")) Out("  " + l.Key.PadRight(20) + l.Label);
            }
            return 0;
        }

        // ------------------------------------------------------------------ status / install
        static int CmdStatus(Args a)
        {
            Init(a, false);
            LoadForgeSettings();
            a.CheckUnused();
            Out("FFmpeg Forge CLI " + Version + "  (engine " + EngineVersion + ")", ConsoleColor.White);
            Line("FFmpeg", !string.IsNullOrEmpty(Runner.FFmpeg), string.IsNullOrEmpty(Runner.FFmpeg) ? "not found" : ShortVersion() + "  —  " + Runner.FFmpeg);
            Line("FFprobe", !string.IsNullOrEmpty(Runner.FFprobe), string.IsNullOrEmpty(Runner.FFprobe) ? "not found (durations / progress unavailable)" : Runner.FFprobe);
            Line("Forge folder", true, AppPaths.AppDir + (AppPaths.Portable ? "  (portable, settings in " + AppPaths.DataDir + ")" : ""));
            Line("dovi_tool", Hdr.DoviTool != null, Hdr.DoviTool ?? "missing (Dolby Vision 8.1 unavailable)");
            Line("ForgeRTX", Rtx.HasDriver, Rtx.HasDriver ? Rtx.DriverPath : "missing (NVIDIA RTX Video driver engine unavailable)");
            Line("RazorNext", RazorBuiltin.Ready, RazorBuiltin.Status());
            Line("VisionNext", VisionBuiltin.Ready, VisionBuiltin.Status());
            Line("Visual Enhancer", VisionEngine.Root != null, VisionEngine.Root != null ? VisionEngine.ExePath : "not found (optional, --ve-dir)");
            Line("SeedVR2", SeedEngine.Script != null && SeedEngine.Python(SeedEngine.Script) != null, SeedEngine.Status().Split('\n')[0]);
            Line("AI denoiser", AiDenoise.Ready, AiDenoise.Ready ? "ready (" + AiDenoise.Root + ")" : "not installed (forge install ai)");
            Line("QTGMC", Qtgmc.Ready, Qtgmc.Ready ? "ready (" + Qtgmc.Root + ")" : "not installed — bwdif is used (forge install qtgmc)");
            if (!string.IsNullOrEmpty(Runner.FFmpeg))
            {
                try { FormatCatalog.Load(); Line("Formats", true, FormatCatalog.Muxers.Count + " muxers, " + FormatCatalog.Encoders.Count + " encoders"); } catch { }
                bool dvd = false, bd = false, placebo = false;
                try
                {
                    string o = DiscScanner.Capture(Runner.FFmpeg, "-hide_banner -demuxers", 15000, null) + DiscScanner.Capture(Runner.FFmpeg, "-hide_banner -protocols", 15000, null) + DiscScanner.Capture(Runner.FFmpeg, "-hide_banner -filters", 15000, null);
                    dvd = o.Contains("dvdvideo"); bd = o.Split('\n').Any(x => x.Trim() == "bluray"); placebo = o.Contains("libplacebo");
                }
                catch { }
                Line("DVD / Blu-ray", dvd && bd, (dvd ? "dvdvideo ✓" : "dvdvideo ✗") + "  " + (bd ? "libbluray ✓" : "libbluray ✗"));
                Line("GPU scaling", placebo, placebo ? "libplacebo (Vulkan) ✓" : "libplacebo missing: FSR / NIS / OptiScaler unavailable");
            }
            return 0;
        }

        static void Line(string k, bool ok, string v)
        {
            v = System.Text.RegularExpressions.Regex.Replace(v ?? "", "click \"Install ([A-Za-z0-9]+)( model)?…?\"( again)?", m => "run  forge install " + m.Groups[1].Value.ToLowerInvariant());
            if (noColor) { Console.WriteLine("  " + (ok ? "[ok] " : "[--] ") + k.PadRight(16) + v); return; }
            var o = Console.ForegroundColor;
            Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.DarkYellow; Console.Write("  " + (ok ? "● " : "○ "));
            Console.ForegroundColor = ConsoleColor.Gray; Console.Write(k.PadRight(16));
            Console.ForegroundColor = o; Console.WriteLine(v);
        }

        static int CmdInstall(Args a)
        {
            Init(a, false);
            LoadForgeSettings();
            a.CheckUnused();
            if (!AppPaths.IsWindows) { Fail("forge install uses Windows scripts: not available on this system."); return 1; }
            string what = a.Positional.Count > 0 ? a.Positional[0].ToLowerInvariant() : "";
            string root, bat;
            switch (what)
            {
                case "razornext": root = RazorBuiltin.Root; bat = RazorBuiltin.WriteInstaller(root); break;
                case "visionnext": root = VisionBuiltin.Root; bat = VisionBuiltin.WriteInstaller(root); break;
                case "seedvr2": root = !string.IsNullOrEmpty(SeedEngine.Dir) ? SeedEngine.Dir : SeedEngine.DefaultInstallDir; bat = SeedEngine.WriteInstaller(root); break;
                case "qtgmc": root = Qtgmc.Root; bat = Qtgmc.WriteInstaller(root); break;
                case "ai": case "denoiser": root = AiDenoise.Root; bat = AiDenoise.WriteInstaller(root); break;
                default: throw new UsageException("forge install razornext | visionnext | seedvr2 | qtgmc | ai");
            }
            Out("Installing " + what + " in " + root + " (several GB may be downloaded)…", ConsoleColor.White);
            // the batch files end with "pause": feed it a key so the command returns by itself
            var psi = new ProcessStartInfo("cmd.exe", "/c \"\"" + bat + "\" < nul\"") { UseShellExecute = false, WorkingDirectory = root };
            using (var p = Process.Start(psi)) { p.WaitForExit(); return p.ExitCode; }
        }

        // ------------------------------------------------------------------ hints (same as the window)
        static string Hint(string err)
        {
            if (string.IsNullOrEmpty(err)) return null;
            string e = err.ToLowerInvariant();
            if (e.Contains("forgern") || e.Contains("forge_razornext") || e.Contains("relayvsr"))
                return e.Contains("out of memory") ? "RazorNext ran out of GPU memory: use a smaller source (720p / 1080p) or --upscale-engine ve --memory-budget 8." :
                       e.Contains("no module named") || e.Contains("checkpoint not found") ? "The RazorNext installation is incomplete: forge install razornext" : null;
            if (e.Contains("visionnext model is not installed")) return "forge install visionnext";
            if (e.Contains("razornext model is not installed")) return "forge install razornext";
            if (e.Contains("out of memory") || e.Contains("outofmemory")) return "The GPU ran out of memory: lower the resolution / model size (SeedVR2: --seed-model with fp8 or GGUF, --blockswap, --vae-tiled).";
            if (e.Contains("no module named") || e.Contains("modulenotfounderror")) return "A Python environment is incomplete: run the matching  forge install …  again.";
            if (e.Contains("cuda") && (e.Contains("not available") || e.Contains("no cuda gpus") || e.Contains("no kernel image"))) return "PyTorch cannot use your NVIDIA GPU: update the NVIDIA driver.";
            if (e.Contains("vulkan") || e.Contains("libplacebo") || e.Contains("failed creating vulkan") || e.Contains("device creation failed"))
                return "The GPU path (Vulkan / libplacebo) could not start: update the graphics driver, or add --no-gpu-scale / pick a CPU scale type (xbr, integer, lanczos…).";
            if (e.Contains("no such filter")) return "This filter is not in your FFmpeg build: forge filters NAME";
            if (e.Contains("error parsing") || e.Contains("error initializing filter") || e.Contains("option not found") || e.Contains("error applying option")) return "A filter parameter is invalid: check --vf / --af (forge filters).";
            if (e.Contains("unknown encoder") || e.Contains("encoder not found")) return "This encoder is not in your FFmpeg build: forge codecs";
            if (e.Contains("nvenc") || e.Contains("no capable devices found") || e.Contains("cannot load nvcuda")) return "NVIDIA NVENC is not available on this PC: use a CPU codec (--vcodec h264 / h265 / av1).";
            if (e.Contains("permission denied")) return "The output folder is not writable: use -o FOLDER.";
            if (e.Contains("no space left")) return "The disk is full.";
            if (e.Contains("does not contain any stream") || e.Contains("matches no streams")) return "The file does not contain the requested stream type.";
            return null;
        }

        // ------------------------------------------------------------------ help
        static void Help(string topic)
        {
            var t = (topic ?? "").ToLowerInvariant();
            if (t == "convert" || t == "c") { Out(HelpConvert); return; }
            if (t == "rip") { Out(HelpRip); return; }
            Out(HelpMain);
        }

        const string HelpMain =
@"FFmpeg Forge CLI 1.0 — the FFmpeg Forge 2.3.0 engine on the command line

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
  forge convert ""D:\Videos"" -r -f mp4 --vcodec h265 --quality high -o ""D:\Out""
  forge clip.mp4 --scale fsr --scale-factor 2 --denoise
  forge old.avi --deinterlace double --upscale razornext --upscale-factor 2 -f mkv
  forge song.flac -f mp3 --abitrate 320
  forge concert.mkv -f mkv --channels 7.1.4 --upmix spatial
  forge rip E:\ --format mkv
";

        const string HelpConvert =
@"forge convert FILE|FOLDER|PATTERN… [options]

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
  --custom ""ARGS""        extra FFmpeg output arguments
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
";

        const string HelpRip =
@"forge rip SOURCE [options]          SOURCE = drive (E:\), VIDEO_TS / BDMV folder, or .iso

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
";
    }
}
