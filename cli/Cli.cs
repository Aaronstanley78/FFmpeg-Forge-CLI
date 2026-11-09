using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

[assembly: System.Reflection.AssemblyTitle("FFmpeg Forge CLI")]
[assembly: System.Reflection.AssemblyDescription("Command-line edition of FFmpeg Forge (engine 2.3.0)")]
[assembly: System.Reflection.AssemblyProduct("FFmpeg Forge CLI")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.0.0")]

namespace FFmpegForge.CommandLine
{
    /// FFmpeg Forge CLI 1.0 — the conversion engine of FFmpeg Forge 2.3.0 without the window.
    public static partial class Cli
    {
        public const string Version = "1.0.0";
        public const string EngineVersion = "2.3.0";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static bool quiet, noColor, dryRun, verbose;
        static volatile bool cancel;

        // ------------------------------------------------------------------ console helpers
        public static void Out(string s, ConsoleColor? c = null)
        {
            if (c != null && !noColor) { var o = Console.ForegroundColor; Console.ForegroundColor = c.Value; Console.WriteLine(s); Console.ForegroundColor = o; }
            else Console.WriteLine(s);
        }
        public static void Info(string s) { if (!quiet) Out(s); }
        public static void Dim(string s) { if (!quiet) Out(s, ConsoleColor.DarkGray); }
        public static void Good(string s) { Out(s, ConsoleColor.Green); }
        public static void Warn(string s) { Out(s, ConsoleColor.Yellow); }
        public static void Fail(string s)
        {
            if (!noColor) { var o = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Red; Console.Error.WriteLine(s); Console.ForegroundColor = o; }
            else Console.Error.WriteLine(s);
        }

        static bool Interactive { get { try { return !Console.IsOutputRedirected; } catch { return false; } } }

        /// One progress line, redrawn in place (or a line every 10 % when the output is redirected).
        public class Progress
        {
            readonly string label; readonly Stopwatch sw = Stopwatch.StartNew(); long last = -1000; int lastDecile = -1; bool drawn;
            public Progress(string label) { this.label = label; }
            public void Report(double p)
            {
                if (quiet) return;
                p = Math.Max(0, Math.Min(1, p));
                if (!Interactive)
                {
                    int d = (int)(p * 10);
                    if (d > lastDecile) { lastDecile = d; Console.WriteLine("   " + (d * 10) + " %"); }
                    return;
                }
                long now = sw.ElapsedMilliseconds;
                if (now - last < 200 && p < 1) return;
                last = now;
                int width = 28, fill = (int)Math.Round(p * width);
                string eta = p > 0.01 && p < 1 ? "  ETA " + Dur(sw.Elapsed.TotalSeconds * (1 - p) / p) : "";
                string line = "   [" + new string('#', fill) + new string('-', width - fill) + "] " + (p * 100).ToString("0.0", Inv).PadLeft(5) + " %  " + Dur(sw.Elapsed.TotalSeconds) + eta + "  " + label;
                int max = 0; try { max = Console.WindowWidth - 1; } catch { max = 119; }
                if (max > 20 && line.Length > max) line = line.Substring(0, max);
                lock (typeof(Progress)) { Console.Write("\r" + line.PadRight(Math.Max(max, line.Length))); drawn = true; }
            }
            public void Done() { if (drawn && Interactive && !quiet) { Console.WriteLine(); drawn = false; } }
            public void Break() { if (drawn && Interactive && !quiet) { Console.WriteLine(); drawn = false; } }
        }

        public static string Dur(double s)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, s));
            return t.TotalHours >= 1 ? string.Format("{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds) : string.Format("{0}:{1:00}", t.Minutes, t.Seconds);
        }

        public static string Size(long b)
        {
            if (b >= 1L << 30) return (b / 1073741824.0).ToString("0.00", Inv) + " GB";
            if (b >= 1L << 20) return (b / 1048576.0).ToString("0.0", Inv) + " MB";
            return (b / 1024.0).ToString("0", Inv) + " KB";
        }

        // ------------------------------------------------------------------ arguments
        public class Args
        {
            public readonly List<string> Positional = new List<string>();
            public readonly Dictionary<string, List<string>> Opts = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> Used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            static readonly HashSet<string> Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "overwrite", "y", "keep-subs", "normalize", "dry-run", "quiet", "q", "no-color", "verbose", "v", "recursive", "r",
                "no-gpu-scale", "no-fidelity", "no-444", "optiscaler", "opti-contrast", "denoise-classic", "no-temporal", "no-auto-profile",
                "cpu", "rtx-hdr", "dlss", "ai-keep-size", "ai-cpu", "ai-fp32", "keep-video", "downmix", "no-chapters", "all", "show-short",
                "help", "h", "json", "denoise", "rtx-vsr", "seedvr2", "vae-tiled", "offload", "list" };

            public static Args Parse(IEnumerable<string> raw)
            {
                var a = new Args();
                var list = raw.ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    string t = list[i];
                    if (t == "--") { a.Positional.AddRange(list.Skip(i + 1)); break; }
                    if (t.StartsWith("--") || (t.StartsWith("-") && t.Length == 2 && !char.IsDigit(t[1])))
                    {
                        string k = t.TrimStart('-'), v = null;
                        int eq = k.IndexOf('=');
                        if (eq > 0) { v = k.Substring(eq + 1); k = k.Substring(0, eq); }
                        else if (!Flags.Contains(k) && i + 1 < list.Count) v = list[++i];
                        else if (!Flags.Contains(k)) throw new UsageException("Option --" + k + " needs a value.");
                        if (k == "o") k = "output"; if (k == "f") k = "format"; if (k == "y") k = "overwrite"; if (k == "q") k = "quiet"; if (k == "r") k = "recursive"; if (k == "v") k = "verbose"; if (k == "h") k = "help"; if (k == "t") k = "type";
                        List<string> l;
                        if (!a.Opts.TryGetValue(k, out l)) a.Opts[k] = l = new List<string>();
                        l.Add(v ?? "1");
                    }
                    else a.Positional.Add(t);
                }
                return a;
            }

            public bool Has(string k) { Used.Add(k); return Opts.ContainsKey(k); }
            public string Get(string k, string def = null) { Used.Add(k); List<string> l; return Opts.TryGetValue(k, out l) ? l[l.Count - 1] : def; }
            public List<string> All(string k) { Used.Add(k); List<string> l; return Opts.TryGetValue(k, out l) ? l : new List<string>(); }
            public int Int(string k, int def)
            {
                string v = Get(k); if (v == null) return def;
                int r; if (int.TryParse(v, NumberStyles.Integer, Inv, out r)) return r;
                throw new UsageException("--" + k + " expects a whole number (got \"" + v + "\").");
            }
            public double Dbl(string k, double def)
            {
                string v = Get(k); if (v == null) return def;
                double r; if (double.TryParse(v.Replace(',', '.'), NumberStyles.Float, Inv, out r)) return r;
                throw new UsageException("--" + k + " expects a number (got \"" + v + "\").");
            }
            public void CheckUnused()
            {
                var unknown = Opts.Keys.Where(k => !Used.Contains(k)).ToList();
                if (unknown.Count > 0) throw new UsageException("Unknown option" + (unknown.Count > 1 ? "s" : "") + ": " + string.Join(", ", unknown.Select(k => "--" + k).ToArray()) + "   (forge help)");
            }
        }

        public class UsageException : Exception { public UsageException(string m) : base(m) { } }

        // ------------------------------------------------------------------ entry
        public static int Main(string[] argv)
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
            Console.CancelKeyPress += (s, e) => { if (!cancel) { cancel = true; e.Cancel = true; Console.WriteLine(); Warn("Cancelling… (Ctrl+C again to quit at once)"); } };
            Args a;
            try { a = Args.Parse(argv); }
            catch (UsageException ex) { Fail("forge: " + ex.Message); return 2; }
            quiet = a.Has("quiet"); noColor = a.Has("no-color") || Environment.GetEnvironmentVariable("NO_COLOR") != null; dryRun = a.Has("dry-run"); verbose = a.Has("verbose");
            string cmd = a.Positional.Count > 0 ? a.Positional[0].ToLowerInvariant() : "help";
            if (a.Has("help") && a.Positional.Count <= 1) { Help(a.Positional.Count == 1 ? cmd : null); return 0; }
            // a file / folder as first argument = convert
            var known = new HashSet<string> { "help", "-h", "/?", "version", "--version", "convert", "c", "info", "probe", "formats", "codecs", "encoders", "filters", "scale-types", "scalers", "denoise-presets", "layouts", "channels", "rip", "status", "doctor", "install" };
            if (cmd != "help" && (File.Exists(a.Positional[0]) || Directory.Exists(a.Positional[0]) || a.Positional[0].Contains("*") ||
                (!known.Contains(cmd) && (Path.HasExtension(a.Positional[0]) || a.Positional[0].Contains("\\") || a.Positional[0].Contains("/")))))
            { a.Positional.Insert(0, "convert"); cmd = "convert"; }
            else if (a.Positional.Count > 0) a.Positional.RemoveAt(0);
            try
            {
                switch (cmd)
                {
                    case "help": case "-h": case "/?": Help(a.Positional.FirstOrDefault()); return 0;
                    case "version": case "--version": return CmdVersion(a);
                    case "convert": case "c": if (a.Positional.Count > 0 && a.Positional[0] == "convert") a.Positional.RemoveAt(0); return CmdConvert(a);
                    case "info": case "probe": return CmdInfo(a);
                    case "formats": return CmdFormats(a);
                    case "codecs": case "encoders": return CmdCodecs(a);
                    case "filters": return CmdFilters(a);
                    case "scale-types": case "scalers": return CmdList(a, "scale");
                    case "denoise-presets": return CmdList(a, "denoise");
                    case "layouts": case "channels": return CmdList(a, "layouts");
                    case "rip": return CmdRip(a);
                    case "status": case "doctor": return CmdStatus(a);
                    case "install": return CmdInstall(a);
                    default: Fail("forge: unknown command \"" + cmd + "\".  Run  forge help"); return 2;
                }
            }
            catch (UsageException ex) { Fail("forge: " + ex.Message); return 2; }
            catch (Exception ex)
            {
                if (ex is IOException && (ex.Message.Contains("Write fault") || ex.Message.Contains("pipe"))) return 0;   // output closed (| more, | head)
                Fail("forge: " + ex.Message); if (verbose) Fail(ex.ToString()); return 1;
            }
        }

        /// FFmpeg (bundled bin\, --ffmpeg, C:\FFmpeg\bin, PATH) and the FFmpeg Forge folder used for the AI engines.
        static void Init(Args a, bool needFfmpeg = true)
        {
            string ff = a.Get("ffmpeg") ?? Environment.GetEnvironmentVariable("FFMPEG_FORGE_FFMPEG");
            if (ff != null && Directory.Exists(ff)) ff = Path.Combine(ff, File.Exists(Path.Combine(ff, "bin", AppPaths.ExeName("ffmpeg"))) ? Path.Combine("bin", AppPaths.ExeName("ffmpeg")) : AppPaths.ExeName("ffmpeg"));
            bool ok = ff != null ? Runner.SetFFmpeg(ff) : Runner.AutoDetect(null);
            if (!ok && needFfmpeg) throw new Exception("FFmpeg not found. Put ffmpeg in the bin folder next to forge" + (AppPaths.IsWindows ? ".exe" : "") + ", install it in C:\\FFmpeg\\bin, or use --ffmpeg <path>.");
            string fd = a.Get("forge-dir") ?? Environment.GetEnvironmentVariable("FFMPEG_FORGE_DIR");
            if (!string.IsNullOrEmpty(fd))
            {
                if (!Directory.Exists(fd)) throw new UsageException("--forge-dir: folder not found: " + fd);
                AppPaths.AppDir = Path.GetFullPath(fd).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;   // AI engines, models, settings of that FFmpeg Forge
            }
            LoadForgeSettings();
            string ve = a.Get("ve-dir"); if (ve != null) VisionEngine.Dir = ve;
            if (VisionEngine.Resolve(VisionEngine.Dir) == null)
                foreach (var c in new[] { Path.Combine(AppPaths.AppDir, "Visual Enhancer") }.Concat(VisionEngine.DefaultDirs))
                    if (VisionEngine.Resolve(c) != null) { VisionEngine.Dir = c; break; }
        }

        static int CmdVersion(Args a)
        {
            a.CheckUnused();
            Out("FFmpeg Forge CLI " + Version + "  (engine of FFmpeg Forge " + EngineVersion + ")");
            try { Init(a, false); Out("FFmpeg: " + (string.IsNullOrEmpty(Runner.FFmpeg) ? "not found" : ShortVersion() + "  —  " + Runner.FFmpeg)); } catch { }
            return 0;
        }
    }
}
