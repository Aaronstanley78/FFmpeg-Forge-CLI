using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FFmpegForge.CommandLine
{
    public static partial class Cli
    {
        // ------------------------------------------------------------------ inputs
        static List<string> ExpandInputs(IEnumerable<string> items, bool recursive)
        {
            var res = new List<string>();
            foreach (var it in items)
            {
                if (File.Exists(it)) { res.Add(Path.GetFullPath(it)); continue; }
                if (Directory.Exists(it))
                {
                    foreach (var f in Directory.GetFiles(it, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                        if (Formats.KindFromExt(f) != MediaKind.Unknown) res.Add(Path.GetFullPath(f));
                    continue;
                }
                if (it.Contains("*") || it.Contains("?"))
                {
                    string dir = Path.GetDirectoryName(it); if (string.IsNullOrEmpty(dir)) dir = ".";
                    if (Directory.Exists(dir))
                        foreach (var f in Directory.GetFiles(dir, Path.GetFileName(it), recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            res.Add(Path.GetFullPath(f));
                    continue;
                }
                throw new UsageException("Input not found: " + it);
            }
            return res.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        static MediaKind ParseKind(string t)
        {
            switch ((t ?? "").ToLowerInvariant())
            {
                case "video": case "v": return MediaKind.Video;
                case "audio": case "a": return MediaKind.Audio;
                case "image": case "picture": case "i": return MediaKind.Image;
            }
            throw new UsageException("--type must be video, audio or image.");
        }

        static OutFormat FindFormat(string ext, MediaKind? want, MediaKind src, string vcodecHint, string acodecHint = null)
        {
            ext = ext.TrimStart('.').ToLowerInvariant();
            var all = Formats.All.Concat(FormatCatalog.Dynamic).Where(f => f.Ext.Equals(ext, StringComparison.OrdinalIgnoreCase)).ToList();
            if (want != null) all = all.Where(f => f.Kind == want.Value).ToList();
            if (all.Count == 0) return null;
            var pref = all.Where(f => f.Kind == src).ToList();
            if (pref.Count == 0) pref = all.Where(f => f.Kind == MediaKind.Video).ToList();
            if (pref.Count == 0) pref = all;
            if (vcodecHint != null) { var m = pref.FirstOrDefault(f => f.VCodec == vcodecHint); if (m != null) return m; }
            if (acodecHint != null) { var m = pref.FirstOrDefault(f => f.ACodec == acodecHint); if (m != null) return m; }
            return pref.FirstOrDefault(f => !f.Dynamic) ?? pref[0];
        }

        static double ParseTime(string t)
        {
            if (t == null) return -1;
            t = t.Trim().Replace(',', '.');
            double total = 0;
            foreach (var part in t.Split(':'))
            {
                double v;
                if (!double.TryParse(part, NumberStyles.Float, Inv, out v)) throw new UsageException("Invalid time \"" + t + "\" (use seconds or hh:mm:ss).");
                total = total * 60 + v;
            }
            return total;
        }

        static int Pick(string v, string opt, params string[] names)
        {
            if (v == null) return -1;
            int n;
            if (int.TryParse(v, out n)) return n;
            for (int i = 0; i < names.Length; i++) if (names[i].Split('|').Any(x => x.Equals(v, StringComparison.OrdinalIgnoreCase))) return i;
            throw new UsageException("--" + opt + ": unknown value \"" + v + "\". Choices: " + string.Join(", ", names.Select(x => x.Split('|')[0]).ToArray()));
        }

        static readonly Dictionary<string, int[]> ResPresets = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase) {
            {"8k", new[]{7680,4320}}, {"4320p", new[]{7680,4320}}, {"5k", new[]{5120,2880}}, {"dci4k", new[]{4096,2160}}, {"4k", new[]{3840,2160}}, {"2160p", new[]{3840,2160}},
            {"1440p", new[]{2560,1440}}, {"2k", new[]{2560,1440}}, {"1080p", new[]{1920,1080}}, {"720p", new[]{1280,720}}, {"576p", new[]{1024,576}}, {"480p", new[]{854,480}},
            {"360p", new[]{640,360}}, {"vertical1080", new[]{1080,1920}}, {"square1080", new[]{1080,1080}} };

        // ------------------------------------------------------------------ settings from the command line
        static ConvSettings BuildSettings(Args a, MediaKind srcKind, out bool aiDenoiseOff)
        {
            var s = new ConvSettings();
            MediaKind? want = a.Has("type") ? (MediaKind?)ParseKind(a.Get("type")) : null;
            string fmt = a.Get("format");
            string vcHint = a.Get("vcodec");
            if (fmt == null)
            {
                // default: MP4 for videos, same type as the source otherwise
                var k = want ?? srcKind;
                fmt = k == MediaKind.Audio ? "mp3" : k == MediaKind.Image ? "png" : "mp4";
            }
            s.Format = FindFormat(fmt, want, want ?? srcKind, vcHint, a.Get("acodec"));
            if (s.Format == null) throw new UsageException("Unknown output format \"" + fmt + "\"" + (want != null ? " for " + want.Value.ToString().ToLowerInvariant() : "") + ". See  forge formats");
            var kind = s.Format.Kind;

            // ---------- video ----------
            if (vcHint != null) s.VCodec = vcHint;
            int q = Pick(a.Get("quality"), "quality", "max|lossless", "very-high|veryhigh", "high", "medium", "small|smallest"); if (q >= 0) s.QualityIdx = Math.Max(0, Math.Min(4, q));
            int p = Pick(a.Get("preset"), "preset", "ultrafast", "veryfast", "fast", "medium", "slow", "veryslow"); if (p >= 0) s.PresetIdx = Math.Max(0, Math.Min(5, p));
            s.VBitrate = a.Int("vbitrate", 0);
            s.NvCrf = a.Int("nvcq", 0);
            string res = a.Get("resolution") ?? a.Get("size");
            if (res != null)
            {
                int[] wh;
                if (ResPresets.TryGetValue(res, out wh)) { s.ResW = wh[0]; s.ResH = wh[1]; }
                else
                {
                    var parts = res.ToLowerInvariant().Split('x', ':');
                    int w, h;
                    if (parts.Length != 2 || !int.TryParse(parts[0], out w) || !int.TryParse(parts[1], out h)) throw new UsageException("--resolution: use WIDTHxHEIGHT (0 = automatic), or 4k / 1080p / 720p…");
                    s.ResW = w; s.ResH = h;
                }
            }
            int fit = Pick(a.Get("fit"), "fit", "stretch", "fit|pad", "fill|crop"); s.ScaleMode = fit >= 0 ? fit : 1;
            s.Fps = a.Dbl("fps", 0);
            int rot = Pick(a.Get("rotate"), "rotate", "none|0", "90", "180", "270", "hflip|mirror", "vflip"); if (rot >= 0) s.Rotate = rot;
            s.KeepSubs = a.Has("keep-subs");
            s.StillDuration = a.Dbl("still-duration", 5);
            int deint = Pick(a.Get("deinterlace"), "deinterlace", "off", "double|qtgmc|on|50p", "same|qtgmc-same|25p"); if (deint >= 0 && kind == MediaKind.Video) s.Deint = deint;
            int order = Pick(a.Get("field-order"), "field-order", "auto", "tff", "bff"); if (order >= 0) s.DeintOrder = order;
            int seq = Pick(a.Get("sequence"), "sequence", "off", "png", "jpg|jpeg"); if (seq >= 0 && kind == MediaKind.Video) s.SeqExport = seq;

            // ---------- audio ----------
            s.ACodec = a.Get("acodec", "auto");
            s.AKbps = a.Int("abitrate", 0);
            s.SampleRate = a.Int("rate", 0);
            s.ChLayout = a.Get("channels", "");
            if (s.ChLayout != "" && !ChannelLayouts.All.Any(l => l.Key == s.ChLayout)) throw new UsageException("--channels: unknown layout \"" + s.ChLayout + "\". See  forge layouts");
            int up = Pick(a.Get("upmix"), "upmix", "auto", "spatial|spatial-music", "music", "cinema", "live", "off");
            if (up >= 0) s.UpmixStyle = new[] { 0, ChannelLayouts.StyleSpatial, 1, 2, 3, 4 }[Math.Min(5, up)];
            s.Normalize = a.Has("normalize");
            s.VolumePct = a.Int("volume", 100);
            s.KeepVideo = a.Has("keep-video") && kind == MediaKind.Video;
            int ex = Pick(a.Get("existing-audio"), "existing-audio", "convert", "copy|keep", "remove|drop"); if (ex >= 0) s.ExistingAudio = ex;
            foreach (var t in a.All("add-audio"))
            {
                // file[:lang[:title[:delayMs]]]  (Windows drive letters are kept)
                var parts = new List<string>(t.Split(':'));
                if (parts.Count > 1 && parts[0].Length == 1 && (parts[1].StartsWith("\\") || parts[1].StartsWith("/"))) { parts[1] = parts[0] + ":" + parts[1]; parts.RemoveAt(0); }
                var tr = new ExtraTrack { Path = Path.GetFullPath(parts[0]) };
                if (!File.Exists(tr.Path)) throw new UsageException("--add-audio: file not found: " + tr.Path);
                if (parts.Count > 1) tr.Lang = parts[1];
                if (parts.Count > 2) tr.Title = parts[2];
                int dl; if (parts.Count > 3 && int.TryParse(parts[3], out dl)) tr.DelayMs = dl;
                s.ExtraAudio.Add(tr);
            }

            // ---------- image ----------
            s.ImgQuality = a.Int("img-quality", 90);
            string exm = a.Get("extract");
            if (exm != null)
            {
                if (exm == "single") s.ExtractMode = 0;
                else if (exm == "all") s.ExtractMode = 1;
                else if (exm.StartsWith("every:")) { s.ExtractMode = 2; s.ExtractEvery = double.Parse(exm.Substring(6).Replace(',', '.'), Inv); }
                else throw new UsageException("--extract: single, all or every:SECONDS");
            }
            s.ExtractAt = a.Dbl("at", 1);

            // ---------- common ----------
            s.TrimStart = a.Has("start") ? ParseTime(a.Get("start")) : -1;
            s.TrimEnd = a.Has("end") ? ParseTime(a.Get("end")) : -1;
            s.CustomArgs = a.Get("custom", "");
            s.OutDir = a.Get("output");
            s.Overwrite = a.Has("overwrite");
            foreach (var f in a.All("vf")) s.Filters.Add(new FilterEntry { Kind = 'V', Expr = f });
            foreach (var f in a.All("af")) s.Filters.Add(new FilterEntry { Kind = 'A', Expr = f });
            s.FilterComplex = a.Get("filter-complex", "");
            s.ExtraInputs = a.All("extra-input").Select(Path.GetFullPath).ToList();

            // ---------- HDR ----------
            int hdr = Pick(a.Get("hdr"), "hdr", "off", "hdr10", "dv|dolby-vision|dv81");
            if (hdr > 0) s.SdrHdr = kind == MediaKind.Image ? Hdr.Hdr10 : hdr;
            s.HdrPeak = a.Int("hdr-peak", 1000);

            // ---------- Scale / OptiScaler ----------
            string sc = a.Get("scale");
            if (sc != null && kind != MediaKind.Audio)
            {
                if (!Scaling.Types.Any(t => t.Key == sc)) throw new UsageException("--scale: unknown type \"" + sc + "\". See  forge scale-types");
                s.ScaleEnabled = true; s.ScaleType = sc;
                string sf = a.Get("scale-factor", "2");
                s.ScaleFactor = sf.Equals("res", StringComparison.OrdinalIgnoreCase) || sf.Equals("resolution", StringComparison.OrdinalIgnoreCase) ? 0 : double.Parse(sf.Replace(',', '.'), Inv);
                if (s.ScaleFactor == 0 && s.ResW <= 0 && s.ResH <= 0) s.ScaleFactor = 2;
                s.ScaleSharpness = a.Int("sharpness", 100);
                s.ScaleOptimized = !a.Has("no-gpu-scale");
                s.ScaleFidelity = !a.Has("no-fidelity");
                s.ScaleFullChroma = !a.Has("no-444");
            }
            if (a.Has("optiscaler") && kind != MediaKind.Audio)
            {
                s.OptiEnabled = true;
                s.OptiMult = a.Dbl("opti-mult", 1.5);
                s.OptiKernel = Math.Max(1, Math.Min(7, a.Int("opti-kernel", 3)));
                s.OptiSharpness = a.Int("opti-sharpness", 40);
                s.OptiContrast = a.Has("opti-contrast");
            }

            // ---------- Denoiser ----------
            aiDenoiseOff = a.Has("denoise-classic");
            if ((a.Has("denoise") || a.Has("denoise-preset") || aiDenoiseOff || a.Has("denoise-ai")) && kind != MediaKind.Audio)
            {
                s.DnEnabled = true;
                int dp = Pick(a.Get("denoise-preset"), "denoise-preset", "auto", "light", "medium", "strong", "very-strong", "film-grain|grain", "low-light|smartphone", "animation|cartoon", "vhs|analog");
                if (dp >= 0) s.DnPreset = Math.Min(Denoise.Presets.Count - 1, dp);
                int de = Pick(a.Get("denoise-engine"), "denoise-engine", "balanced", "hq|high-quality", "fast"); if (de >= 0) s.DnEngine = de;
                s.DnStrength = a.Int("denoise-strength", 100);
                s.DnSharpen = a.Int("denoise-sharpen", 20);
                s.DnTemporal = !a.Has("no-temporal");
                s.DnAutoProfile = !a.Has("no-auto-profile");
                s.DnModel = a.Get("denoise-ai", "scunet");
            }

            // ---------- AI upscale (VisionNext / RazorNext) ----------
            string upm = a.Get("upscale");
            if (upm != null && kind != MediaKind.Audio)
            {
                upm = upm.ToLowerInvariant();
                if (upm != "razornext" && upm != "visionnext") throw new UsageException("--upscale: razornext or visionnext");
                var v = s.Vision; v.Enabled = true; v.Model = upm;
                v.Scale = a.Int("upscale-factor", upm == "razornext" ? 4 : 2);
                int eng = Pick(a.Get("upscale-engine"), "upscale-engine", "builtin|built-in", "ve|visual-enhancer"); v.Engine = Math.Max(0, eng);
                v.UseGpu = !a.Has("cpu");
                v.Tile = a.Int("tile", 512);
                v.MemBudget = a.Get("memory-budget", "auto");
            }

            // ---------- SeedVR2 ----------
            if (a.Has("seedvr2") && kind != MediaKind.Audio)
            {
                s.Seed.Enabled = true;
                string m = a.Get("seed-model");
                if (m != null)
                {
                    var mm = SeedEngine.Models.FirstOrDefault(x => x.File.Equals(m, StringComparison.OrdinalIgnoreCase) || x.File.StartsWith(m, StringComparison.OrdinalIgnoreCase));
                    if (mm == null) throw new UsageException("--seed-model: unknown model. Known: " + string.Join(", ", SeedEngine.Models.Select(x => x.File).ToArray()));
                    s.Seed.Model = mm.File;
                }
                s.Seed.SizeIdx = Math.Max(0, Math.Min(SeedEngine.Sizes.Length - 1, a.Int("seed-size", 1)));
                s.Seed.BatchSize = a.Int("seed-batch", s.Seed.BatchSize);
                s.Seed.VaeTiled = a.Has("vae-tiled"); s.Seed.Offload = a.Has("offload");
                s.Seed.BlocksToSwap = a.Int("blockswap", 0);
            }

            // ---------- NVIDIA RTX ----------
            if (a.Has("rtx-vsr"))
            {
                s.RtxVsr = true;
                s.VsrQuality = Math.Max(1, Math.Min(4, a.Int("vsr-quality", 4)));
                s.VsrScale = Math.Max(1, Math.Min(4, a.Int("vsr-scale", 2)));
            }
            if (a.Has("rtx-hdr") && kind == MediaKind.Video) s.RtxHdr = true;
            if (a.Has("dlss") && kind == MediaKind.Video) s.Dlss = true;
            int re = Pick(a.Get("rtx-engine"), "rtx-engine", "auto", "native", "driver", "processor"); if (re >= 0) s.RtxEnginePref = new[] { "auto", "native", "driver", "processor" }[re];
            if (kind == MediaKind.Image && s.RtxVsr) { s.RtxHdr = false; s.Dlss = false; }

            // ---------- Python / ONNX model ----------
            string am = a.Get("ai-model");
            if (am != null && kind != MediaKind.Audio)
            {
                s.CaiEnabled = true;
                if (File.Exists(am))
                {
                    string dst = Path.Combine(CustomAi.LibraryDir, Path.GetFileName(am));
                    if (!File.Exists(dst)) File.Copy(am, dst);
                    am = Path.GetFileName(am);
                }
                s.CaiModel = am;
                s.CaiKeep = a.Has("ai-keep-size"); s.CaiGpu = !a.Has("ai-cpu"); s.CaiFp32 = a.Has("ai-fp32");
                s.CaiTile = a.Int("ai-tile", 0);
            }

            // ---------- cover ----------
            string cov = a.Get("cover");
            if (cov != null && kind == MediaKind.Video)
            {
                if (cov.StartsWith("frame:")) { s.CoverMode = 1; s.CoverAt = ParseTime(cov.Substring(6)); }
                else { s.CoverMode = 2; s.CoverPath = Path.GetFullPath(cov.StartsWith("file:") ? cov.Substring(5) : cov); if (!File.Exists(s.CoverPath)) throw new UsageException("--cover: picture not found: " + s.CoverPath); }
            }
            return s;
        }

        // ------------------------------------------------------------------ output name (same rules as the window)
        static string MakeOutPath(Job job, ConvSettings s, bool sequence, string outFile)
        {
            if (outFile != null) return Path.GetFullPath(outFile);
            string dir = string.IsNullOrEmpty(s.OutDir) ? Path.GetDirectoryName(job.Path) : Path.GetFullPath(s.OutDir);
            string name = Path.GetFileNameWithoutExtension(job.Path);
            string ext = "." + s.Format.Ext;
            { string ak = s.ACodec == "auto" ? s.Format.ACodec : s.ACodec; if (s.Format.Kind == MediaKind.Audio && DsdEngine.IsDsd(ak)) ext = "." + DsdEngine.Ext(DsdEngine.ResolveKey(job, s, ak)); }
            Func<string, string> build = b => Path.Combine(dir, sequence ? b + "_%05d" + ext : b + ext);
            Func<string, bool> taken = b =>
            {
                string p = sequence ? Path.Combine(dir, b + "_00001" + ext) : Path.Combine(dir, b + ext);
                if (string.Equals(Path.GetFullPath(p), Path.GetFullPath(job.Path), StringComparison.OrdinalIgnoreCase)) return true;
                return !s.Overwrite && File.Exists(p);
            };
            string bn = name; int n = 1;
            while (taken(bn)) { n++; bn = name + " (" + n + ")"; }
            return build(bn);
        }

        static bool IsSequence(Job j, ConvSettings s)
        {
            bool srcVideo = j.Kind == MediaKind.Video || CommandBuilder.IsAnimatedImageSource(j);
            if (s.Seed != null && s.Seed.Enabled) return false;
            if (s.Vision != null && s.Vision.Enabled) return false;
            return s.Format.Kind == MediaKind.Image && srcVideo && s.ExtractMode != 0;
        }

        /// Same engine choice as the window (DSD, audio tracks, VisionNext / RazorNext, SeedVR2, RTX, plain FFmpeg).
        static bool BuildJobCommand(Job job, ConvSettings s, string outPath, out string exe, out string args, out double expected, out string err, out Rtx.DriverPlan plan, List<string> notes)
        {
            exe = Runner.FFmpeg; plan = null; string why;
            string ak = s.ACodec == "auto" ? s.Format.ACodec : s.ACodec;
            if (DsdEngine.IsDsd(ak))
            {
                expected = 0;
                if (s.Format.Kind != MediaKind.Audio) { err = "DSD (.dsf / .dff) is an audio format: use --type audio."; args = null; return false; }
                var dp = DsdEngine.Build(job, s, ak, outPath, out err);
                expected = dp != null ? dp.Expected : 0; args = dp != null ? dp.Text() : null; return dp != null;
            }
            if (s.SdrHdr > 0 && s.Format.Kind != MediaKind.Audio) { string herr = Hdr.Check(job, s); if (herr != null) { err = herr; args = null; expected = 0; return false; } }
            if (AudioTracks.Active(s)) { args = AudioTracks.Build(job, s, outPath, out expected, out err, notes); return args != null; }
            if (s.Vision != null && s.Vision.Enabled && s.Format.Kind != MediaKind.Audio)
            { var vp = VisionEngine.Build(job, s, outPath, out err); expected = vp != null ? vp.Expected : 0; args = vp != null ? vp.Text() : null; return vp != null; }
            if (s.Seed != null && s.Seed.Enabled && s.Format.Kind != MediaKind.Audio)
            { var sp = SeedEngine.Build(job, s, outPath, out err); expected = sp != null ? sp.Expected : 0; args = sp != null ? sp.Text() : null; return sp != null; }
            if (s.Format.Kind == MediaKind.Image && s.RtxVsr)
            {
                if (!Rtx.HasDriver) { err = "NVIDIA RTX Video for pictures needs ForgeRTX.exe next to forge.exe."; args = null; expected = 0; return false; }
                plan = Rtx.PlanDriver(job, s, outPath, out err); expected = 0; args = plan != null ? plan.Text() : null; exe = Rtx.DriverPath; return plan != null;
            }
            string engine = s.Format.Kind == MediaKind.Video && s.RtxActive ? Rtx.Engine(s, out why) : null;
            if (engine == "driver") { plan = Rtx.PlanDriver(job, s, outPath, out err); expected = plan != null ? plan.Expected : 0; args = plan != null ? plan.Text() : null; exe = Rtx.DriverPath; return plan != null; }
            if (engine == "processor") { exe = Rtx.ProcessorPath; args = Rtx.ProcessorArgs(job, s, outPath, out expected, out err); }
            else args = CommandBuilder.Build(job, s, outPath, out expected, out err);
            return args != null;
        }

        // ------------------------------------------------------------------ convert
        static int CmdConvert(Args a)
        {
            Init(a);
            var inputs = ExpandInputs(a.Positional, a.Has("recursive"));
            if (inputs.Count == 0) throw new UsageException("No input file. Example:  forge convert video.mkv -f mp4");
            string outFile = a.Get("output-file");
            if (outFile != null && inputs.Count > 1) throw new UsageException("--output-file works with a single input (use -o FOLDER for several).");
            Info("FFmpeg Forge CLI " + Version + "  ·  FFmpeg " + ShortVersion());
            // every format of the FFmpeg build (cached in formats.cache, same as the window)
            try { FormatCatalog.Load(); } catch { }

            var jobs = inputs.Select(p => new Job { Path = p }).ToList();
            foreach (var j in jobs) Runner.Probe(j);
            bool aiOff;
            var first = jobs[0];
            var sUser = BuildSettings(a, first.Kind == MediaKind.Unknown ? MediaKind.Video : first.Kind, out aiOff);
            a.CheckUnused();
            Info("Output: " + sUser.Format.Label + (sUser.OutDir != null ? "  →  " + Path.GetFullPath(sUser.OutDir) : "  (next to the sources)") + "   ·   " + jobs.Count + " file" + (jobs.Count > 1 ? "s" : ""));
            if (sUser.OutDir != null) try { Directory.CreateDirectory(sUser.OutDir); } catch (Exception ex) { throw new Exception("Cannot create " + sUser.OutDir + ": " + ex.Message); }

            // Image sequence export: lossless 4:4:4 FFV1 render, then every frame written as PNG / JPEG (same as the window)
            var s0 = sUser;
            int seqKind = s0.Format.Kind == MediaKind.Video ? s0.SeqExport : 0;
            bool seqHi = false;
            if (seqKind > 0)
            {
                var mkv = Formats.All.FirstOrDefault(f => f.Kind == MediaKind.Video && f.Ext == "mkv" && f.VCodec == "ffv1");
                if (mkv != null)
                {
                    s0 = s0.Clone();
                    s0.Format = mkv; s0.VCodec = "ffv1"; s0.ACodec = "none"; s0.VBitrate = 0; s0.NvCrf = 0; s0.KeepSubs = false;
                    seqHi = (s0.ScaleActive && s0.ScaleFidelity) || s0.RtxHdr;
                    s0.CustomArgs = ((s0.CustomArgs ?? "") + " -pix_fmt " + (seqHi ? "yuv444p16le" : "yuv444p")).Trim();
                }
                else seqKind = 0;
            }

            int ok = 0, fail = 0;
            var total = Stopwatch.StartNew();
            for (int i = 0; i < jobs.Count && !cancel; i++)
            {
                var job = jobs[i];
                var s = s0;
                if (jobs.Count > 1) Out("[" + (i + 1) + "/" + jobs.Count + "] " + Path.GetFileName(job.Path), ConsoleColor.White);
                else Out(Path.GetFileName(job.Path), ConsoleColor.White);
                if (job.Kind == MediaKind.Unknown) { Fail("   ✗ not a video, audio or image file"); fail++; continue; }
                Dim("   " + Describe(job));
                string lastErr;
                string outPath;
                int code = ConvertOne(job, s, sUser, seqKind, seqHi, outFile, aiOff, out outPath, out lastErr);
                if (code == 0)
                {
                    ok++;
                    string size = ""; try { if (File.Exists(outPath)) size = "  (" + Size(new FileInfo(outPath).Length) + ")"; } catch { }
                    Good("   ✓ " + outPath + size);
                }
                else if (code == -1 || cancel) { Warn("   cancelled"); break; }
                else
                {
                    fail++;
                    Fail("   ✗ failed (code " + code + ")");
                    if (!string.IsNullOrEmpty(lastErr)) foreach (var l in lastErr.Split('\n').Where(x => x.Trim().Length > 0).Reverse().Take(6).Reverse()) Fail("     " + l.TrimEnd());
                    string hint = Hint(lastErr);
                    if (hint != null) Warn("     → " + hint);
                }
            }
            if (jobs.Count > 1 || fail > 0) Out((cancel ? "Cancelled. " : "Finished: ") + ok + " ok" + (fail > 0 ? ", " + fail + " failed" : "") + " — " + Dur(total.Elapsed.TotalSeconds), fail > 0 ? ConsoleColor.Yellow : ConsoleColor.Green);
            return cancel ? 130 : fail > 0 ? 1 : 0;
        }

        static string ShortVersion()
        {
            try
            {
                string o = DiscScanner.Capture(Runner.FFmpeg, "-hide_banner -version", 10000, null).Split('\n')[0];
                o = o.Replace("ffmpeg version", "").Trim();
                int i = o.IndexOf("Copyright", StringComparison.OrdinalIgnoreCase); if (i > 0) o = o.Substring(0, i).Trim();
                return o;
            }
            catch { return "?"; }
        }

        static string Describe(Job j)
        {
            var p = new List<string> { j.Kind.ToString().ToLowerInvariant() };
            if (j.Width > 0) p.Add(j.Width + "×" + j.Height + (string.IsNullOrEmpty(j.VideoCodec) ? "" : " " + j.VideoCodec));
            if (j.FpsNum > 0) p.Add((j.FpsNum / (double)Math.Max(1, j.FpsDen)).ToString("0.###", Inv) + " fps");
            if (j.HasAudio && !string.IsNullOrEmpty(j.AudioCodec)) p.Add(j.AudioCodec + " " + j.AudioChannels + " ch");
            if (j.Duration > 0) p.Add(Dur(j.Duration));
            if (j.Size > 0) p.Add(Size(j.Size));
            return string.Join("  ·  ", p.ToArray());
        }

        static void LogLine(string l)
        {
            if (string.IsNullOrWhiteSpace(l) || DiscScanner.Noise(l)) return;
            if (l.Contains("Guessed Channel Layout") || l.Contains("deprecated pixel format") || l.Contains("Last message repeated")) return;
            if (!verbose && !(l.StartsWith("Forge") || l.StartsWith("SeedVR2") || l.StartsWith("VisionNext") || l.StartsWith("RazorNext") || l.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || l.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0)) return;
            Dim("     " + l.Trim());
        }

        /// The conversion pipeline of the window (FFmpeg Forge 2.3.0): deinterlacing → AI denoiser → Python / ONNX model →
        /// final engine (DSD, VisionNext / RazorNext, SeedVR2, RTX, FFmpeg) → image sequence → Dolby Vision → cover.
        static int ConvertOne(Job job, ConvSettings s, ConvSettings sUser, int seqKind, bool seqHi, string outFile, bool aiOff, out string outPath, out string lastErr)
        {
            lastErr = null;
            bool seq = IsSequence(job, s);
            outPath = MakeOutPath(job, s, seq, seqKind > 0 ? null : outFile);
            string seqDir = null, seqPattern = null, seqTemp = null;
            if (seqKind > 0)
            {
                string bn = Path.GetFileNameWithoutExtension(job.Path);
                string d0 = outFile != null ? Path.GetDirectoryName(Path.GetFullPath(outFile)) : string.IsNullOrEmpty(sUser.OutDir) ? Path.GetDirectoryName(job.Path) : Path.GetFullPath(sUser.OutDir);
                string tag = seqKind == 1 ? "_PNG" : "_JPG";
                seqDir = Path.Combine(d0, bn + tag);
                for (int k = 2; !sUser.Overwrite && Directory.Exists(seqDir) && Directory.GetFileSystemEntries(seqDir).Length > 0; k++) seqDir = Path.Combine(d0, bn + tag + " (" + k + ")");
                seqPattern = Path.Combine(seqDir, bn + "_%06d." + (seqKind == 1 ? "png" : "jpg"));
                seqTemp = Path.Combine(Path.GetTempPath(), "FFmpegForge-sequence-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".mkv");
                outPath = seqTemp;
            }
            try { string od = Path.GetDirectoryName(outPath); if (!string.IsNullOrEmpty(od)) Directory.CreateDirectory(od); } catch { }
            var notes = new List<string>();
            double expected; string err, exe, args; Rtx.DriverPlan plan;
            BuildJobCommand(job, s, outPath, out exe, out args, out expected, out err, out plan, notes);
            foreach (var n in notes) Warn("   " + n);
            if (s.DlssSkipped) Warn("   DLSS skipped: NVIDIA offers no DLSS for video files. RTX Video Super Resolution is applied instead.");
            if (args == null) { lastErr = err; return 3; }
            if (dryRun)
            {
                Out("   " + (exe == Runner.FFmpeg ? "ffmpeg " : "\"" + exe + "\" ") + args, ConsoleColor.Cyan);
                outPath = "(dry run)";
                return 0;
            }
            int code = 0;
            var rjob = job; var rs = s;
            double pBase = 0, pScale = 1;
            var bar = new Progress(Path.GetFileName(job.Path));
            Action<double, double, double> prog = (b, sc, p) => bar.Report(b + sc * p);
            AiDenoise.Stage diStage = null, aiStage = null, caiStage = null;
            bool srcHasPicture = job.Kind == MediaKind.Video || job.Kind == MediaKind.Image || CommandBuilder.IsAnimatedImageSource(job);
            Action<string> log = l => { bar.Break(); LogLine(l); };
            try
            {
                // ---- deinterlacing (QTGMC, or FFmpeg bwdif when QTGMC is not installed)
                if (s.Deint > 0 && s.Format.Kind == MediaKind.Video && job.Kind == MediaKind.Video && job.HasVideo && !Qtgmc.Wanted(job, s))
                    Dim("   Deinterlacing: the video is progressive (" + (string.IsNullOrEmpty(job.FieldOrder) ? "no field order" : job.FieldOrder) + ") — left as it is (use --field-order tff|bff to force).");
                if (Qtgmc.Wanted(job, s) && !cancel)
                {
                    bool q = Qtgmc.Ready;
                    string ordTxt = Qtgmc.OrderText(job, s);
                    bool plainPath = !q && !(s.Vision != null && s.Vision.Enabled) && !(s.Seed != null && s.Seed.Enabled) && !s.RtxActive && !s.DnEnabled && !s.CaiEnabled && s.Rotate == 0 && string.IsNullOrWhiteSpace(s.FilterComplex);
                    Info("   Deinterlacing " + (q ? "QTGMC" : "bwdif (QTGMC not installed: forge install qtgmc)") + " — " + (s.Deint == 2 ? "same rate" : "double rate") + ", " + ordTxt);
                    if (plainPath)
                    {
                        rs = s.Clone(); rs.Filters = new List<FilterEntry>(s.Filters);
                        rs.Filters.Insert(0, new FilterEntry { Kind = 'V', Expr = Qtgmc.Bwdif(job, s) });
                        if (!BuildJobCommand(rjob, rs, outPath, out exe, out args, out expected, out err, out plan, notes)) { lastErr = err; return 3; }
                    }
                    else
                    {
                        string derr; diStage = Qtgmc.Build(job, s, q, out derr);
                        if (diStage == null) { lastErr = derr; return 3; }
                        try { Directory.CreateDirectory(diStage.TempDir); } catch { }
                        bool more = s.DnEnabled || s.CaiEnabled || s.RtxActive || (s.Vision != null && s.Vision.Enabled) || (s.Seed != null && s.Seed.Enabled);
                        double share = more ? 0.25 : 0.6;
                        code = Rtx.RunDriver(diStage.Plan, p => prog(0, share, p), log, () => cancel, out lastErr);
                        if (code != 0) return code;
                        if (!File.Exists(diStage.Intermediate)) { lastErr = "Deinterlacing did not write its result."; return 3; }
                        rjob = new Job { Path = diStage.Intermediate }; Runner.Probe(rjob);
                        rs = s.Clone(); rs.TrimStart = -1; rs.TrimEnd = -1;
                        if (rs.Fps <= 0) rs.Fps = Qtgmc.ExactRate(rjob);
                        if (!BuildJobCommand(rjob, rs, outPath, out exe, out args, out expected, out err, out plan, notes)) { lastErr = err; return 3; }
                        pBase = share; pScale = 1 - share;
                    }
                }
                // ---- AI denoiser (SCUNet / HAT / DAT / SPAN), else the classic denoiser stays in the FFmpeg chain
                bool aiWanted = s.DnEnabled && !aiOff && srcHasPicture && s.Format.Kind != MediaKind.Audio && job.Width > 0;
                if (aiWanted && !AiDenoise.ReadyFor(s)) Dim("   Denoiser: " + AiDenoise.StatusFor(s.DnModel) + " — classic denoiser used.");
                if (aiWanted && AiDenoise.ReadyFor(s) && !cancel)
                {
                    string aerr; aiStage = AiDenoise.Build(rjob, rs, s.Format.Kind == MediaKind.Image, out aerr);
                    if (aiStage != null)
                    {
                        try { Directory.CreateDirectory(aiStage.TempDir); } catch { }
                        var dnm = AiDenoise.FindModel(s.DnModel);
                        Info("   Denoiser IA (" + (dnm.Key == "scunet" ? "SCUNet" : dnm.Arch + " · " + dnm.Key) + " + temporal stabilisation)");
                        double st = pBase, sh = pScale * (s.Format.Kind == MediaKind.Image ? 0.8 : 0.7);
                        code = Rtx.RunDriver(aiStage.Plan, p => prog(st, sh, p), log, () => cancel, out lastErr);
                        if (code != 0) return code;
                        if (!File.Exists(aiStage.Intermediate)) { lastErr = "The AI Denoiser did not write its result."; return 3; }
                        rjob = new Job { Path = aiStage.Intermediate }; Runner.Probe(rjob);
                        rs = rs.Clone(); rs.DnEnabled = false; rs.TrimStart = -1; rs.TrimEnd = -1;
                        if (!BuildJobCommand(rjob, rs, outPath, out exe, out args, out expected, out err, out plan, notes)) { lastErr = err; return 3; }
                        pBase = st + sh; pScale = 1 - pBase;
                    }
                    else Warn("   Denoiser IA: " + aerr + " — classic denoiser used.");
                }
                // ---- Python / ONNX model
                if (s.CaiEnabled && !string.IsNullOrEmpty(s.CaiModel) && srcHasPicture && s.Format.Kind != MediaKind.Audio && job.Width > 0 && !cancel)
                {
                    string cerr; Info("   Python / ONNX AI: " + s.CaiModel + " (" + CustomAi.Kind(s.CaiModel) + ")");
                    caiStage = CustomAi.Build(rjob, rs, s.Format.Kind == MediaKind.Image, out cerr);
                    if (caiStage == null) { lastErr = cerr; return 3; }
                    try { Directory.CreateDirectory(caiStage.TempDir); } catch { }
                    double st = pBase, sh = pScale * (s.Format.Kind == MediaKind.Image ? 0.8 : 0.7);
                    code = Rtx.RunDriver(caiStage.Plan, p => prog(st, sh, p), log, () => cancel, out lastErr);
                    if (code != 0) return code;
                    if (!File.Exists(caiStage.Intermediate)) { lastErr = "The Python / ONNX model did not write its result."; return 3; }
                    rjob = new Job { Path = caiStage.Intermediate }; Runner.Probe(rjob);
                    rs = rs.Clone(); rs.DnEnabled = false; rs.CaiEnabled = false; rs.TrimStart = -1; rs.TrimEnd = -1;
                    if (!BuildJobCommand(rjob, rs, outPath, out exe, out args, out expected, out err, out plan, notes)) { lastErr = err; return 3; }
                    pBase = st + sh; pScale = 1 - pBase;
                }
                if (cancel) return -1;

                // ---- final engine
                double b0 = pBase, s1 = pScale;
                Action<double> fp = p => prog(b0, s1, p);
                string dsdKey = rs.ACodec == "auto" ? rs.Format.ACodec : rs.ACodec;
                if (s.Format.Kind == MediaKind.Audio && DsdEngine.IsDsd(dsdKey))
                {
                    string derr; var dp = DsdEngine.Build(rjob, rs, dsdKey, outPath, out derr);
                    if (dp == null) { lastErr = derr; return 3; }
                    Info("   DSD " + (dp.Rate == 5644800 ? "DSD128" : "DSD64") + " · " + dp.Channels + " ch");
                    code = DsdEngine.Run(dp, fp, log, () => cancel, out lastErr);
                }
                else if (s.Vision != null && s.Vision.Enabled && s.Format.Kind != MediaKind.Audio)
                {
                    string verr; var vp = VisionEngine.Build(rjob, rs, outPath, out verr);
                    if (vp == null) { lastErr = verr; return 3; }
                    Info("   Upscale " + s.Vision.Name + " ×" + s.Vision.Scale + (vp.Builtin ? " (built-in, " + (s.Vision.UseGpu ? "GPU" : "CPU") + ")" : " (Visual Enhancer)") + "  ·  " + vp.InW + "×" + vp.InH + " → " + vp.OutW + "×" + vp.OutH);
                    code = vp.Driver != null ? Rtx.RunDriver(vp.Driver, fp, log, () => cancel, out lastErr) : VisionEngine.Run(vp, fp, log, () => cancel, out lastErr);
                }
                else if (s.Seed != null && s.Seed.Enabled && s.Format.Kind != MediaKind.Audio)
                {
                    string serr; var sp = SeedEngine.Build(rjob, rs, outPath, out serr);
                    if (sp == null) { lastErr = serr; return 3; }
                    Info("   SeedVR2: " + SeedEngine.Short(sp.Script == null ? "" : s.Seed.Model) + "  ·  " + sp.InW + "×" + sp.InH + " → short side " + sp.OutShort);
                    code = SeedEngine.Run(sp, fp, log, () => cancel, out lastErr);
                }
                else if (plan != null)
                {
                    Info("   NVIDIA RTX driver engine: " + plan.W + "×" + plan.H + " → " + plan.OW + "×" + plan.OH + (s.RtxHdr ? " · HDR10" : ""));
                    code = Rtx.RunDriver(plan, fp, log, () => cancel, out lastErr);
                }
                else
                {
                    if (verbose) Dim("   " + (exe == Runner.FFmpeg ? "ffmpeg " : "\"" + exe + "\" ") + args);
                    code = Runner.Run(exe, args, expected, fp, log, () => cancel, out lastErr);
                }
                bar.Done();
                if (code != 0) { CleanupOut(outPath, seq); return cancel ? -1 : code; }

                // ---- image sequence
                if (seqKind > 0)
                {
                    Directory.CreateDirectory(seqDir);
                    var tj = new Job { Path = seqTemp }; Runner.Probe(tj);
                    string vfx = "scale=in_color_matrix=auto:in_range=auto:out_range=pc:flags=bicubic+accurate_rnd+full_chroma_int,format=" + (seqKind == 1 ? (seqHi ? "rgb48be" : "rgb24") : "yuvj444p");
                    string sargs = "-hide_banner -nostdin -y -loglevel warning -progress pipe:1 -nostats -i \"" + seqTemp + "\" -map 0:v:0 -an -sn -dn -vf \"" + vfx + "\" " +
                                   (seqKind == 1 ? "-c:v png -pred mixed" : "-c:v mjpeg -q:v 2 -qmin 1 -huffman optimal") + " -start_number 1 -f image2 \"" + seqPattern + "\"";
                    Info("   Image sequence (" + (seqKind == 1 ? "PNG" : "JPG") + ") → " + seqDir);
                    var bar2 = new Progress("frames");
                    code = Runner.Run(Runner.FFmpeg, sargs, tj.Duration, bar2.Report, log, () => cancel, out lastErr);
                    bar2.Done();
                    if (code != 0) return code;
                    var files = Directory.GetFiles(seqDir).Where(f => f.EndsWith(seqKind == 1 ? ".png" : ".jpg", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
                    if (files.Length == 0) { lastErr = "No frame was written."; return 3; }
                    Info("   " + files.Length + " frames written (" + Size(files.Sum(f => new FileInfo(f).Length)) + ")");
                    outPath = files[0];
                }
                // ---- Dolby Vision 8.1
                if (seqKind == 0 && s.SdrHdr == Hdr.DolbyVision81 && s.Format.Kind == MediaKind.Video && File.Exists(outPath))
                {
                    string dverr;
                    if (!Hdr.MakeDolbyVision(outPath, s.HdrPeak, m => Info("   " + m), p => { }, () => cancel, out dverr))
                    { lastErr = cancel ? "Cancelled" : "Dolby Vision: " + dverr + " (the HDR10 file is kept)"; return cancel ? -1 : 3; }
                    Info("   Dolby Vision profile 8.1 written.");
                }
                // ---- cover / thumbnail
                if (seqKind == 0 && sUser.CoverMode > 0 && sUser.Format.Kind == MediaKind.Video && !seq)
                {
                    string cerr2, cext = Path.GetExtension(outPath).TrimStart('.').ToLowerInvariant();
                    if (!Cover.Supported(cext)) Warn("   Cover: not supported for ." + cext + " (MP4, M4V, MKV) — skipped.");
                    else if (Cover.Apply(outPath, sUser, out cerr2)) Info("   Cover / thumbnail added.");
                    else Warn("   Cover could not be added: " + cerr2);
                }
                if (seq) outPath = outPath.Replace("%05d", "00001");
                return 0;
            }
            catch (Exception ex) { bar.Break(); lastErr = ex.Message; return -2; }
            finally
            {
                if (seqTemp != null) try { if (File.Exists(seqTemp)) File.Delete(seqTemp); } catch { }
                AiDenoise.Cleanup(aiStage); AiDenoise.Cleanup(caiStage); AiDenoise.Cleanup(diStage);
            }
        }

        static void CleanupOut(string outPath, bool seq)
        {
            try { if (!seq && File.Exists(outPath) && (cancel || new FileInfo(outPath).Length == 0)) File.Delete(outPath); } catch { }
        }
    }
}
