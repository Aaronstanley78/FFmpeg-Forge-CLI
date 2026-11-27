using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FFmpegForge
{
    /// 2.4.1 — LTX 2.5 Upscale through WanGP's Video Postprocessing (deepbeepmeep/WanGP, WanGP Community License 2.0).
    /// WanGP runs Lightricks' LTX-2.5 22B distilled model with one of its IC-LoRAs:
    ///   ltx25         LTX 2.5 Pixel Spatial Upscaler  — generative ×2 upscale
    ///   ltx25_refine  LTX 2.5 Detail Refiner          — Refine Details IC-LoRA, ×1 (detail only) / ×1.5 / ×2, tiled up to 8K
    /// FFmpeg Forge installs WanGP (official code from GitHub, Python 3.11, PyTorch 2.10 CUDA 13.0) next to FFmpegForge.exe,
    /// or uses an existing WanGP folder, and drives it with its Python API (shared.api.init → submit_media_postprocessing).
    /// The model files are downloaded by WanGP itself on the first run; they are not redistributed.
    /// Pictures are turned into a short still clip (LTX works on videos), processed, and the middle frame is kept.
    public class LtxSettings
    {
        public bool Enabled;
        public string Method = "ltx25";        // ltx25 | ltx25_refine
        public double Scale = 2;               // ltx25: 2 ; refine: 1 / 1.5 / 2
        public string Prompt = "";             // Detail Refiner style prompt ("" = WanGP default)
        public string Profile = "4";           // WanGP memory profile
        public string Attention = "sdpa";
        public int Seed = -1;
    }

    public static class LtxEngine
    {
        public static readonly string[] MethodLabels = { "LTX 2.5 Pixel Spatial Upscaler (×2)", "LTX 2.5 Detail Refiner (×1 / ×1.5 / ×2)" };
        public static readonly string[] Methods = { "ltx25", "ltx25_refine" };
        public static readonly string[] ScaleLabels = { "×1 (detail only, same size)", "×1.5", "×2" };
        public static readonly double[] Scales = { 1, 1.5, 2 };
        public static readonly string[] ProfileLabels = {
            "4 — recommended (models sent to the GPU part by part)",
            "4.5 — like 4, about 1 GB less VRAM",
            "5 — fail safe (little RAM and VRAM, slower)",
            "3 — fast (whole model in VRAM, 24 GB+)",
            "2 — lots of RAM (64 GB+), less VRAM",
            "1 — fastest (most RAM and VRAM)" };
        public static readonly string[] Profiles = { "4", "4.5", "5", "3", "2", "1" };
        public static readonly string[] AttentionLabels = { "PyTorch SDPA (always works)", "SageAttention (if installed in WanGP)", "SageAttention 2 (if installed)", "Flash Attention (if installed)" };
        public static readonly string[] Attentions = { "sdpa", "sage", "sage2", "flash" };
        public const string DefaultRefinePrompt = "sharp photographic detail, crisp natural texture, fine surface detail, clean edges, natural film grain, high resolution footage";

        public const string RepoZip = "https://github.com/deepbeepmeep/WanGP/archive/refs/heads/main.zip";
        public const string TorchCmd = "torch==2.10.0 torchvision==0.25.0 torchaudio==2.10.0 --index-url https://download.pytorch.org/whl/cu130";
        public const int MaxOutLong = 7680;       // 8K
        const int StillFrames = 17;               // picture → 17-frame still clip (LTX 8n+1 cadence)
        const int StillFps = 24;

        public static string Dir = "";

        public static string DefaultInstallDir
        {
            get
            {
                string app = Path.Combine(AppPaths.AppDir, "WanGP");
                try
                {
                    string probeDir = Directory.Exists(app) ? app : AppPaths.AppDir;
                    string t = Path.Combine(probeDir, ".ffmpegforge-write-test"); File.WriteAllText(t, "x"); File.Delete(t);
                    return app;
                }
                catch { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FFmpegForge", "WanGP"); }
            }
        }

        /// The WanGP code folder (the one that contains wgp.py): the folder itself, its "app" subfolder or one level below.
        public static string AppFolder(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return null;
            try
            {
                if (File.Exists(Path.Combine(dir, "wgp.py"))) return dir;
                if (File.Exists(Path.Combine(dir, "app", "wgp.py"))) return Path.Combine(dir, "app");
                if (Directory.Exists(dir))
                    foreach (var sub in Directory.GetDirectories(dir))
                        if (File.Exists(Path.Combine(sub, "wgp.py"))) return sub;
            }
            catch { }
            return null;
        }

        public static string Root { get { return string.IsNullOrEmpty(Dir) ? DefaultInstallDir : Dir; } }
        public static string App { get { return AppFolder(Root); } }

        static string Parent(string d) { try { return d == null ? null : Path.GetDirectoryName(d.TrimEnd('\\', '/')); } catch { return null; } }

        static string PyIn(string envDir, bool conda)
        {
            if (string.IsNullOrEmpty(envDir)) return null;
            var c = conda ? new[] { Path.Combine(envDir, "python.exe"), Path.Combine(envDir, "bin", "python") }
                          : new[] { Path.Combine(envDir, "Scripts", "python.exe"), Path.Combine(envDir, "bin", "python") };
            foreach (var p in c) try { if (File.Exists(p)) return p; } catch { }
            return null;
        }

        /// The Python of the WanGP environment: FFmpeg Forge's own venv, WanGP's envs.json (installer), or a venv next to it.
        public static string Python(string app)
        {
            if (app == null) return null;
            string up = Parent(app);
            foreach (var d in new[] { up, app })
                if (d != null)
                    foreach (var n in new[] { "venv", ".venv", "env", "wan2gp" })
                    {
                        string p = PyIn(Path.Combine(d, n), false);
                        if (p != null) return p;
                    }
            // WanGP's own installer: envs.json {"active": "name", "envs": {"name": {"type": "venv", "path": "..."}}}
            try
            {
                string ej = Path.Combine(app, "envs.json");
                if (File.Exists(ej))
                {
                    string js = File.ReadAllText(ej);
                    var act = Regex.Match(js, "\"active\"\\s*:\\s*\"([^\"]+)\"");
                    if (act.Success)
                    {
                        var env = Regex.Match(js, "\"" + Regex.Escape(act.Groups[1].Value) + "\"\\s*:\\s*\\{([^}]*)\\}");
                        if (env.Success)
                        {
                            var ty = Regex.Match(env.Groups[1].Value, "\"type\"\\s*:\\s*\"([^\"]+)\"");
                            var pa = Regex.Match(env.Groups[1].Value, "\"path\"\\s*:\\s*\"([^\"]+)\"");
                            if (pa.Success)
                            {
                                string path = pa.Groups[1].Value.Replace("\\\\", "\\");
                                if (!Path.IsPathRooted(path)) path = Path.Combine(app, path);
                                string p = PyIn(path, ty.Success && ty.Groups[1].Value == "conda");
                                if (p != null) return p;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static bool Ready { get { string a = App; return a != null && Python(a) != null && File.Exists(Path.Combine(a, "shared", "api.py")); } }

        public static string Status()
        {
            string a = App;
            if (a == null)
                return string.IsNullOrEmpty(Dir) || Dir == DefaultInstallDir
                    ? "WanGP is not installed: click \"Install WanGP (LTX 2.5)\" or choose an existing WanGP folder."
                    : "wgp.py not found in " + Dir + ": choose the WanGP folder.";
            if (!File.Exists(Path.Combine(a, "shared", "api.py")))
                return "This WanGP version is too old (no Python API, shared\\api.py): update WanGP or click \"Install WanGP (LTX 2.5)\".";
            if (Python(a) == null)
                return "WanGP found (" + a + ") but no Python environment: click \"Install WanGP (LTX 2.5)\" to create it.";
            return "Ready: WanGP in " + a + "\nThe LTX 2.5 model files (tens of GB) download automatically on the first conversion.";
        }

        /// Writes the WanGP helper (embedded in FFmpegForge.exe) into the install root.
        public static string SyncHelper(string root)
        {
            string path = Path.Combine(root, "forge_ltx25.py");
            try
            {
                string want;
                using (var st = typeof(LtxEngine).Assembly.GetManifestResourceStream("forge_ltx25.py"))
                using (var r = new StreamReader(st, Encoding.UTF8)) want = r.ReadToEnd().Replace("\r\n", "\n");
                if (!File.Exists(path) || File.ReadAllText(path) != want) File.WriteAllText(path, want, new UTF8Encoding(false));
            }
            catch { }
            return path;
        }

        // ---------------- installer ----------------
        public static string WriteInstaller(string root)
        {
            Directory.CreateDirectory(root);
            string bat = Path.Combine(root, "install_wangp.bat");
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("chcp 65001 >nul");
            sb.AppendLine("title FFmpeg Forge - WanGP (LTX 2.5 Upscale) installation");
            sb.AppendLine("setlocal");
            sb.AppendLine("cd /d \"%~dp0\"");
            sb.AppendLine("echo ============================================================");
            sb.AppendLine("echo  LTX 2.5 Upscale for FFmpeg Forge = WanGP Video Postprocessing");
            sb.AppendLine("echo  WanGP by DeepBeepMeep (WanGP Community License 2.0)");
            sb.AppendLine("echo  Folder: %CD%");
            sb.AppendLine("echo  Downloads: uv + Python 3.11, PyTorch 2.10 CUDA 13.0 (about 3 GB),");
            sb.AppendLine("echo  WanGP code (GitHub) and its Python packages (several GB).");
            sb.AppendLine("echo  The LTX 2.5 model files download on the first conversion.");
            sb.AppendLine("echo ============================================================");
            sb.AppendLine("echo.");
            sb.AppendLine("if not exist uv\\uv.exe (");
            sb.AppendLine("  echo [1/6] Downloading uv...");
            sb.AppendLine("  powershell -NoProfile -ExecutionPolicy Bypass -Command \"$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing '" + SeedEngine.UvZip + "' -OutFile uv.zip; Expand-Archive -Force uv.zip uv; Remove-Item uv.zip\" || goto :fail");
            sb.AppendLine(")");
            sb.AppendLine("echo [2/6] WanGP code (github.com/deepbeepmeep/WanGP)...");
            sb.AppendLine("if not exist app\\wgp.py powershell -NoProfile -ExecutionPolicy Bypass -Command \"$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing '" + RepoZip + "' -OutFile repo.zip; if (Test-Path repo_tmp) { Remove-Item -Recurse -Force repo_tmp }; Expand-Archive -Force repo.zip repo_tmp; Remove-Item repo.zip; if (Test-Path app) { Remove-Item -Recurse -Force app }; Move-Item (Get-ChildItem repo_tmp)[0].FullName app; Remove-Item -Recurse -Force repo_tmp\" || goto :fail");
            sb.AppendLine("echo [3/6] Python 3.11 environment...");
            sb.AppendLine("set UV_PYTHON_INSTALL_DIR=%CD%\\python");
            sb.AppendLine("set UV_CACHE_DIR=%CD%\\cache");
            sb.AppendLine("set UV_LINK_MODE=copy");
            sb.AppendLine("if not exist venv\\Scripts\\python.exe uv\\uv.exe venv --python 3.11 --seed venv || goto :fail");
            sb.AppendLine("echo [4/6] PyTorch 2.10 with CUDA 13.0 (large download)...");
            sb.AppendLine("uv\\uv.exe pip install --python venv\\Scripts\\python.exe " + TorchCmd + " || goto :fail");
            sb.AppendLine("echo [5/6] WanGP requirements (large download)...");
            sb.AppendLine("uv\\uv.exe pip install --python venv\\Scripts\\python.exe -r app\\requirements.txt --index-strategy unsafe-best-match || goto :fail");
            sb.AppendLine("echo [6/6] Triton for Windows (optional: faster kernels)...");
            sb.AppendLine("uv\\uv.exe pip install --python venv\\Scripts\\python.exe \"triton-windows>=3.6,<3.7\" || echo     Triton not installed: WanGP works without it (slower).");
            sb.AppendLine("venv\\Scripts\\python.exe -c \"import torch;print('PyTorch',torch.__version__,'CUDA',torch.cuda.is_available(), torch.cuda.get_device_name(0) if torch.cuda.is_available() else '(no NVIDIA GPU found)')\"");
            sb.AppendLine("echo.");
            sb.AppendLine("echo WanGP is installed. You can close this window and go back to FFmpeg Forge.");
            sb.AppendLine("echo The LTX 2.5 model files download automatically on the first conversion.");
            sb.AppendLine("pause");
            sb.AppendLine("exit /b 0");
            sb.AppendLine(":fail");
            sb.AppendLine("echo.");
            sb.AppendLine("echo Installation failed. Check your internet connection and run install_wangp.bat again.");
            sb.AppendLine("pause");
            sb.AppendLine("exit /b 1");
            File.WriteAllText(bat, sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
            return bat;
        }

        // ---------------- plan ----------------
        public class Plan
        {
            public string TempDir, PreArgs, Python, App, Helper, LtxArgs, EncArgs, LtxIn, LtxOut, Output;
            public double Expected;
            public int InW, InH, OutW, OutH;
            public bool Image;
            public string Text()
            {
                return "ffmpeg " + PreArgs + "  &&  \"" + Python + "\" " + LtxArgs + "  &&  ffmpeg " + EncArgs;
            }
        }

        static string Q(string x) { return "\"" + x.Replace("\"", "\\\"") + "\""; }
        static string F(double d) { return d.ToString("0.###", CultureInfo.InvariantCulture); }

        public static string Label(LtxSettings v)
        {
            return (v.Method == "ltx25_refine" ? "LTX 2.5 Detail Refiner" : "LTX 2.5 Pixel Spatial Upscaler") + " ×" + F(v.Method == "ltx25_refine" ? v.Scale : 2);
        }

        public static Plan Build(Job job, ConvSettings s, string outPath, out string error)
        {
            error = null;
            var v = s.Ltx;
            var fmt = s.Format;
            string app = App;
            if (app == null) { error = "WanGP is not installed: in VIDEO / IMAGE SETTINGS (LTX 2.5 Upscale) click \"Install WanGP (LTX 2.5)\" (or choose your WanGP folder)."; return null; }
            if (!File.Exists(Path.Combine(app, "shared", "api.py"))) { error = "This WanGP version has no Python API (shared\\api.py): update WanGP or click \"Install WanGP (LTX 2.5)\"."; return null; }
            app = Path.GetFullPath(app);
            string py = Python(app);
            if (py == null) { error = "No Python environment found for WanGP (" + app + "). Click \"Install WanGP (LTX 2.5)\" to create it."; return null; }
            if (job.Kind == MediaKind.Audio || fmt.Kind == MediaKind.Audio) { error = "LTX 2.5 Upscale needs a video or a picture."; return null; }
            bool image = fmt.Kind == MediaKind.Image;
            bool srcVideo = job.Kind == MediaKind.Video || CommandBuilder.IsAnimatedImageSource(job);
            if (image && srcVideo) { error = "LTX 2.5 Upscale + Image output works on pictures. For a video, choose the Video category."; return null; }
            if (s.Vision != null && s.Vision.Enabled) { error = "Tick only one AI upscaler: LTX 2.5 Upscale or AI Upscale " + s.Vision.Name + "."; return null; }
            if (s.Seed != null && s.Seed.Enabled) { error = "Tick only one AI upscaler: LTX 2.5 Upscale or SeedVR2."; return null; }
            if (s.ScaleActive) { error = "LTX 2.5 Upscale sets the size itself: untick \"Scale\" / OptiScaler."; return null; }
            if (s.RtxActive) { error = "Untick the NVIDIA RTX AI options (VSR / TrueHDR / DLSS) to use LTX 2.5 Upscale."; return null; }
            if (!string.IsNullOrWhiteSpace(s.FilterComplex)) { error = "The advanced filtergraph cannot be combined with LTX 2.5 Upscale. Use the filter chain instead."; return null; }
            string vkey = fmt.VCodec;
            if (!image)
            {
                if (s.VCodec != "auto") vkey = s.VCodec;
                if (vkey == "copy") { error = "LTX 2.5 Upscale needs a real video codec (not stream copy)."; return null; }
            }
            int w = job.Width, h = job.Height;
            if (w < 16 || h < 16) { error = "Could not read the picture size (ffprobe). Re-add the file."; return null; }
            if (s.Rotate == 1 || s.Rotate == 3) { int t = w; w = h; h = t; }
            string method = v.Method == "ltx25_refine" ? "ltx25_refine" : "ltx25";
            double scale = method == "ltx25" ? 2 : (Scales.Contains(v.Scale) ? v.Scale : 2);
            int ow = (int)Math.Round(w * scale / 2) * 2, oh = (int)Math.Round(h * scale / 2) * 2;
            if (Math.Max(ow, oh) > MaxOutLong || Math.Min(ow, oh) > 4320)
            { error = "LTX 2.5 Upscale outputs at most 8K: " + w + "×" + h + " ×" + F(scale) + " would be " + ow + "×" + oh + ". Pick a smaller factor or a smaller source."; return null; }

            var p = new Plan { Python = py, App = app, Output = outPath, InW = w, InH = h, OutW = ow, OutH = oh, Image = image };
            p.TempDir = Path.Combine(Path.GetTempPath(), "FFmpegForge-LTX25-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            p.LtxIn = Path.Combine(p.TempDir, "input.mp4");
            p.LtxOut = Path.Combine(p.TempDir, "ltx25.mp4");
            p.Helper = Path.GetFullPath(Path.Combine(Root, "forge_ltx25.py"));

            // 1) pre-pass: trim, rotation, denoiser, user filters -> near-lossless H.264 file WanGP can read
            int fn = job.FpsNum > 0 ? job.FpsNum : 30, fd = job.FpsDen > 0 ? job.FpsDen : 1;
            if (s.Fps > 0) { fn = (int)Math.Round(s.Fps * 1000); fd = 1000; }
            if (image) { fn = StillFps; fd = 1; }
            string rate = fn + "/" + fd;
            double ss = s.TrimStart > 0 ? s.TrimStart : 0, to = s.TrimEnd > 0 ? s.TrimEnd : -1;
            string trim = image ? "" : (ss > 0 ? "-ss " + Formats.Inv(ss) + " " : "") + (to > ss && to > 0 ? "-to " + Formats.Inv(to) + " " : "");
            double dur = job.Duration;
            if (to > 0 && (dur <= 0 || to < dur)) dur = to;
            dur = Math.Max(0, dur - ss);
            bool still = !image && !srcVideo;      // picture -> video clip
            if (still) { dur = s.StillDuration > 0 ? s.StillDuration : 5; trim = "-loop 1 -t " + Formats.Inv(dur) + " "; }
            p.Expected = image ? 0 : dur;
            var pre = new List<string>();
            CommandBuilder.AddRotate(pre, s.Rotate);
            string dn = Denoise.Filter(s, job, image);
            if (dn != null) pre.Add(dn);
            string userV = s.UserVideoChain();
            if (userV.Length > 0) pre.Add(userV);
            pre.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2");
            pre.Add("format=yuv420p");
            if (image)
                p.PreArgs = "-hide_banner -nostdin -y -loglevel error -loop 1 -framerate " + StillFps + " -i " + Q(job.Path) + " -map 0:v:0 -frames:v " + StillFrames +
                            " -vf " + Q(string.Join(",", pre.ToArray())) + " -c:v libx264 -preset veryfast -crf 8 -pix_fmt yuv420p " + Q(p.LtxIn);
            else
                p.PreArgs = "-hide_banner -nostdin -y -loglevel error -progress pipe:1 -nostats " + trim + "-i " + Q(job.Path) + " -map 0:v:0 -an -sn -dn -vf " + Q(string.Join(",", pre.ToArray())) +
                            " -r " + rate + " -c:v libx264 -preset veryfast -crf 10 -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 " + Q(p.LtxIn);

            // 2) WanGP
            var a = new StringBuilder();
            a.Append("-u " + Q(p.Helper));
            a.Append(" --wangp " + Q(app));
            a.Append(" --in " + Q(p.LtxIn));
            a.Append(" --out " + Q(p.LtxOut));
            a.Append(" --method " + method);
            a.Append(" --scale " + F(scale));
            a.Append(" --profile " + (Profiles.Contains(v.Profile) ? v.Profile : "4"));
            a.Append(" --attention " + (Attentions.Contains(v.Attention) ? v.Attention : "sdpa"));
            a.Append(" --seed " + v.Seed);
            if (method == "ltx25_refine" && !string.IsNullOrWhiteSpace(v.Prompt)) a.Append(" --prompt " + Q(v.Prompt.Trim().Replace("\r", " ").Replace("\n", " ")));
            p.LtxArgs = a.ToString();

            // 3) final encode with FFmpeg Forge's format / codec / audio settings (exact output size)
            var vf = new List<string>();
            vf.Add("scale=" + ow + ":" + oh + ":flags=lanczos");
            bool even = !image && CommandBuilder.NeedsEven(vkey);
            string sc = CommandBuilder.ScaleFilter(s, even);
            if (sc != null) vf.Add(sc);
            if (image)
            {
                string ia = CommandBuilder.ImageArgs(vkey ?? "auto", s.ImgQuality) ?? "";
                // middle frame of the still clip (the ends of an LTX window carry less detail)
                p.EncArgs = "-hide_banner -nostdin -y -loglevel warning -i " + Q(p.LtxOut) + " -vf " + Q("select=eq(n\\," + (StillFrames / 2) + ")," + string.Join(",", vf.ToArray())) +
                            " -frames:v 1 -update 1 " + ia + (string.IsNullOrWhiteSpace(s.CustomArgs) ? "" : " " + s.CustomArgs.Trim()) + " -strict -2 " + Q(outPath);
                return p;
            }
            if (even) vf.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2");
            string va = CommandBuilder.VideoArgs(vkey, s, fmt.Ext) + (oh >= 720 ? " -color_primaries bt709 -color_trc bt709 -colorspace bt709" : "");
            string akey = s.ACodec == "auto" ? fmt.ACodec : s.ACodec;
            if (s.ChLayout == "copy" && akey != "none") akey = "copy";
            string audio;
            if (akey == "none" || (job.Probed && !job.HasAudio) || still) audio = "-an";
            else
            {
                string userA = s.UserAudioChain();
                if (akey == "copy" && userA.Length > 0) { error = "Audio filters cannot be used with audio stream copy."; return null; }
                s.SourceChannels = job.AudioChannels; s.SourceIsVideo = true; s.SourceDuration = dur; s.SourceDsd = job.IsDsdAudio; s.DsdTargetRate = 48000;
                string af = akey != "copy" ? CommandBuilder.AudioChain(s, userA) : null;
                if (akey != "copy")
                {
                    string lerr = ChannelLayouts.Check(s.ChLayout, akey);
                    if (lerr != null && s.ACodec == "auto") { string alt = ChannelLayouts.AutoCodec(fmt.Ext); if (alt != null) { akey = alt; lerr = null; } }
                    if (lerr != null) { error = lerr; return null; }
                }
                audio = "-map 1:a:0? " + (af != null ? "-af " + Q(af) + " " : "") + CommandBuilder.AudioFormatArgs(s, akey != "copy") + " " +
                        CommandBuilder.AudioArgs(akey, s.AKbps) + (akey != "copy" ? " " + ChannelLayouts.ExtraArgs(s.ChLayout, akey) : "");
            }
            p.EncArgs = "-hide_banner -nostdin -y -loglevel warning -progress pipe:1 -nostats " +
                        "-i " + Q(p.LtxOut) + " " + (still ? "" : trim + "-i " + Q(job.Path) + " ") + "-map 0:v:0 " + audio +
                        " -vf " + Q(string.Join(",", vf.ToArray())) + " -r " + rate + " " + va +
                        " -sn -shortest " + (fmt.Extra ?? "") + (fmt.Dynamic && fmt.Muxer != null ? " -f " + fmt.Muxer : "") +
                        (string.IsNullOrWhiteSpace(s.CustomArgs) ? "" : " " + s.CustomArgs.Trim()) + " -strict -2 " + Q(outPath);
            return p;
        }

        // ---------------- run ----------------
        static readonly Regex Prog = new Regex(@"^FORGE_PROGRESS\s+([\d.]+)\s*(.*)$", RegexOptions.Compiled);
        static readonly Regex Tqdm = new Regex(@"\d+%\s*\|", RegexOptions.Compiled);
        static readonly Regex Ansi = new Regex(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

        /// Pre-pass 5 %, WanGP 85 %, final encode 10 %.
        public static int Run(Plan plan, Action<double> onProgress, Action<string> onLog, Func<bool> cancelled, out string lastErrors)
        {
            try
            {
                Directory.CreateDirectory(plan.TempDir);
                SyncHelper(Parent(plan.Helper));
                try { string od = Path.GetDirectoryName(plan.Output); if (!string.IsNullOrEmpty(od)) Directory.CreateDirectory(od); } catch { }

                onLog("LTX 2.5 1/3 — preparing the source (FFmpeg)");
                int code = Runner.Run(Runner.FFmpeg, plan.PreArgs, plan.Expected, x => onProgress(0.05 * x), onLog, cancelled, out lastErrors);
                if (code != 0) { Cleanup(plan); return code; }

                onLog("LTX 2.5 2/3 — WanGP Video Postprocessing " + plan.InW + "×" + plan.InH + " → " + plan.OutW + "×" + plan.OutH + " (first run: model download, tens of GB)");
                code = RunWanGP(plan, x => onProgress(0.05 + 0.85 * x), onLog, cancelled, out lastErrors);
                if (code != 0) { Cleanup(plan); return code; }
                if (!File.Exists(plan.LtxOut)) { lastErrors = "WanGP finished but did not write " + plan.LtxOut; Cleanup(plan); return 3; }

                onLog("LTX 2.5 3/3 — encoding the final file (FFmpeg)");
                code = Runner.Run(Runner.FFmpeg, plan.EncArgs, plan.Expected, x => onProgress(0.90 + 0.10 * x), onLog, cancelled, out lastErrors);
                Cleanup(plan);
                if (code == 0) onProgress(1.0);
                return code;
            }
            catch (Exception ex) { lastErrors = ex.Message; Cleanup(plan); return -2; }
        }

        static int RunWanGP(Plan plan, Action<double> onProgress, Action<string> onLog, Func<bool> cancelled, out string lastErrors)
        {
            var errs = new List<string>();
            var psi = new ProcessStartInfo(plan.Python, plan.LtxArgs)
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = plan.App,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            // WanGP calls "ffmpeg" / "ffprobe": use FFmpeg Forge's
            string ffdir = string.IsNullOrEmpty(Runner.FFmpeg) ? null : Path.GetDirectoryName(Runner.FFmpeg);
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!string.IsNullOrEmpty(ffdir)) psi.EnvironmentVariables["PATH"] = ffdir + Path.PathSeparator + path;
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            psi.EnvironmentVariables["HF_HUB_DISABLE_PROGRESS_BARS"] = "1";
            long lastLog = 0, lastBar = -10000;
            var sw = Stopwatch.StartNew();
            Action<string> line = l =>
            {
                if (string.IsNullOrWhiteSpace(l)) return;
                l = Ansi.Replace(l, "").TrimEnd();
                var m = Prog.Match(l);
                if (m.Success)
                {
                    double f;
                    if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) onProgress(Math.Max(0, Math.Min(1, f)));
                    if (sw.ElapsedMilliseconds - lastLog < 3000) return;
                    lastLog = sw.ElapsedMilliseconds;
                    l = "LTX 2.5 " + (m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : "") + " " + (f * 100).ToString("0", CultureInfo.InvariantCulture) + " %";
                }
                else if (Tqdm.IsMatch(l))
                {
                    if (sw.ElapsedMilliseconds - lastBar < 3000 && !l.Contains("100%")) return;
                    lastBar = sw.ElapsedMilliseconds;
                }
                lock (errs) { errs.Add(l); if (errs.Count > 40) errs.RemoveAt(0); }
                onLog(l);
            };
            using (var p = Process.Start(psi))
            {
                p.OutputDataReceived += (o, e) => line(e.Data);
                p.ErrorDataReceived += (o, e) => line(e.Data);
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                while (!p.WaitForExit(250))
                {
                    if (cancelled()) { KillTree(p); lastErrors = "Cancelled"; return -1; }
                }
                p.WaitForExit();
                lock (errs) lastErrors = string.Join(Environment.NewLine, errs.ToArray());
                return p.ExitCode == -1 ? 255 : p.ExitCode;
            }
        }

        static void KillTree(Process p)
        {
            try
            {
                if (Path.DirectorySeparatorChar == '\\')
                {
                    var k = Process.Start(new ProcessStartInfo("taskkill", "/PID " + p.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true });
                    if (k != null) k.WaitForExit(5000);
                }
            }
            catch { }
            try { if (!p.HasExited) p.Kill(); } catch { }
            try { p.WaitForExit(5000); } catch { }
        }

        static void Cleanup(Plan plan)
        {
            try { if (plan.TempDir != null && Directory.Exists(plan.TempDir)) Directory.Delete(plan.TempDir, true); } catch { }
        }
    }
}
