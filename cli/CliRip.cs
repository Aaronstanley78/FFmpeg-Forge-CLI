using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FFmpegForge.CommandLine
{
    public static partial class Cli
    {
        static List<int> Numbers(string v, string opt)
        {
            var r = new List<int>();
            foreach (var p in v.Split(',', ';', ' ').Where(x => x.Trim().Length > 0))
            {
                int n;
                if (!int.TryParse(p.Trim(), out n)) throw new UsageException("--" + opt + ": list of numbers, e.g. 1,3");
                r.Add(n);
            }
            return r;
        }

        static int CmdRip(Args a)
        {
            Init(a);
            if (a.Positional.Count == 0) throw new UsageException("forge rip SOURCE   (drive like E:\\, a VIDEO_TS / BDMV folder or an .iso)");
            string srcPath = a.Positional[0];
            if (srcPath.Length == 2 && srcPath[1] == ':') srcPath += "\\";
            bool list = a.Has("list"), all = a.Has("all"), showShort = a.Has("show-short");
            string titles = a.Get("title");
            var o = new RipOptions();
            string fmt = a.Get("format", "mkv").ToLowerInvariant();
            if (!new[] { "mkv", "mp4", "mov", "webm", "flac", "mp3", "m4a", "wav", "mka" }.Contains(fmt)) throw new UsageException("--format: mkv mp4 mov webm flac mp3 m4a wav mka");
            o.Container = fmt;
            string vk = a.Get("video", "copy").ToLowerInvariant();
            var vmap = new Dictionary<string, string> { { "copy", "copy" }, { "x264", "x264" }, { "h264", "x264" }, { "x265", "x265" }, { "h265", "x265" }, { "hevc", "x265" }, { "av1", "av1" }, { "nv264", "nv264" }, { "nvh264", "nv264" }, { "nv265", "nv265" }, { "nvh265", "nv265" }, { "nvav1", "nvav1" } };
            if (!vmap.ContainsKey(vk)) throw new UsageException("--video: copy x264 x265 av1 nv264 nv265 nvav1");
            o.VCodec = vmap[vk];
            o.Quality = Math.Max(0, Math.Min(3, a.Int("quality", 1)));
            int sp = Pick(a.Get("speed"), "speed", "slow", "medium", "fast"); o.Speed = sp >= 0 ? sp : 1;
            o.Height = a.Int("max-height", 0);
            int di = Pick(a.Get("deinterlace"), "deinterlace", "auto", "off", "always"); o.Deint = di >= 0 ? di : 0;
            string ak = a.Get("audio", "copy").ToLowerInvariant();
            if (!new[] { "copy", "aac", "ac3", "eac3", "flac", "opus", "mp3" }.Contains(ak)) throw new UsageException("--audio: copy aac ac3 eac3 flac opus mp3");
            o.ACodec = ak; o.AKbps = a.Int("abitrate", 0); o.Downmix = a.Has("downmix");
            int sm = Pick(a.Get("subtitles"), "subtitles", "keep", "none", "burn"); o.Subs = sm >= 0 ? sm : 0;
            o.Chapters = !a.Has("no-chapters");
            o.OutDir = Path.GetFullPath(a.Get("output", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "FFmpeg Forge Rips")));
            string audioSel = a.Get("audio-tracks"), subSel = a.Get("subs");
            a.CheckUnused();

            string err;
            var s = DiscScanner.Detect(srcPath, out err);
            if (s == null) { Fail("forge rip: " + err); return 1; }
            if (s.Protected) { Fail(s.ProtectionNote); return 1; }
            Info("Reading " + (s.IsIso ? "ISO image " : s.Kind + " ") + s.Path + "…");
            DiscScanner.Scan(s, showShort ? 0 : 60, m => { if (!quiet && Interactive) { Console.Write("\r   " + m.PadRight(70)); } }, () => cancel, out err);
            if (!quiet && Interactive) Console.Write("\r" + new string(' ', 76) + "\r");
            if (s.Protected) { Fail(s.ProtectionNote); return 1; }
            if (err != null && s.Titles.Count == 0) { Fail(err); return 1; }
            if (err != null) Warn(err);
            Out(s.Kind + (s.IsIso ? " image" : "") + " \"" + s.Label + "\" — " + s.Titles.Count(t => t.Error == null) + " title(s)", ConsoleColor.White);
            foreach (var t in s.Titles)
            {
                var v = t.Video.FirstOrDefault();
                string name = t.Number < 0 ? "Main movie" : t.Name(s.IsBluray);
                Out((t.Selected ? " * " : "   ") + name.PadRight(16) + Dur(t.Duration).PadLeft(8) + "  " + (t.Chapters > 0 ? t.Chapters + " ch." : "").PadRight(7) +
                    (t.Error != null ? "unreadable: " + t.Error.Split('\n')[0] : v == null ? "" : DiscStream.CodecName(v.Codec) + " " + v.Width + "×" + v.Height), t.Error != null ? (ConsoleColor?)ConsoleColor.Red : null);
                if (list)
                {
                    int i = 1; foreach (var x in t.Audio) Dim("        audio " + i++ + ": " + x.Describe());
                    i = 1; foreach (var x in t.Subs) Dim("        subs  " + i++ + ": " + x.Describe());
                }
            }
            if (list) { Dim("(* = longest title, ripped by default)"); return 0; }

            // titles to rip
            var pick = new List<DiscTitle>();
            if (all) pick = s.Titles.Where(t => t.Error == null).ToList();
            else if (titles != null)
            {
                foreach (var n in Numbers(titles, "title"))
                {
                    var t = s.Titles.FirstOrDefault(x => x.Number == n);
                    if (t == null) throw new UsageException("Title " + n + " not found (forge rip " + srcPath + " --list" + (showShort ? "" : " --show-short") + ").");
                    pick.Add(t);
                }
            }
            else pick = s.Titles.Where(t => t.Selected && t.Error == null).ToList();
            if (pick.Count == 0) { Fail("Nothing to rip."); return 1; }
            foreach (var t in pick)
            {
                if (audioSel != null) { var al = Numbers(audioSel, "audio-tracks"); int i = 1; foreach (var x in t.Audio) x.Selected = al.Contains(i++); }
                if (subSel != null) { if (subSel.Equals("none", StringComparison.OrdinalIgnoreCase)) foreach (var x in t.Subs) x.Selected = false; else { var sl = Numbers(subSel, "subs"); int i = 1; foreach (var x in t.Subs) x.Selected = sl.Contains(i++); } }
            }
            Directory.CreateDirectory(o.OutDir);
            int ok = 0, fail = 0;
            var total = Stopwatch.StartNew();
            foreach (var t in pick)
            {
                if (cancel) break;
                string name = t.Number < 0 ? "Main movie" : t.Name(s.IsBluray);
                if (s.IsDvd)
                {
                    string why;
                    if (!DiscScanner.CheckDvdReadable(s, t, () => cancel, out why)) { Fail(name + ": " + why); fail++; continue; }
                }
                string outPath = RipBuilder.OutputName(s, t, o);
                string chap = null;
                if (s.IsBluray && t.ChapterTimes.Count > 1)
                {
                    chap = Path.Combine(Path.GetTempPath(), "FFmpegForge-chapters-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt");
                    try { RipBuilder.WriteChapters(t, chap); } catch { chap = null; }
                }
                var notes = new List<string>();
                string args = RipBuilder.Build(s, t, o, outPath, chap, notes, out err);
                if (args == null) { Fail(name + ": " + err); fail++; continue; }
                Out(name + " (" + Dur(t.Duration) + ") → " + outPath, ConsoleColor.White);
                foreach (var n in notes) Warn("   " + n);
                if (dryRun) { Out("   ffmpeg " + args, ConsoleColor.Cyan); continue; }
                if (verbose) Dim("   ffmpeg " + args);
                var bar = new Progress(name);
                var sw = Stopwatch.StartNew();
                string lastErr;
                int code = Runner.Run(Runner.FFmpeg, args, t.Duration, bar.Report, l => { bar.Break(); LogLine(l); }, () => cancel, out lastErr);
                bar.Done();
                if (chap != null) try { File.Delete(chap); } catch { }
                if (code == 0 && File.Exists(outPath)) { ok++; Good("   ✓ " + Dur(sw.Elapsed.TotalSeconds) + " — " + Size(new FileInfo(outPath).Length)); }
                else
                {
                    try { if (File.Exists(outPath)) File.Delete(outPath); } catch { }
                    if (cancel) { Warn("   cancelled"); break; }
                    fail++;
                    Fail("   ✗ failed (code " + code + ")");
                    if (!string.IsNullOrEmpty(lastErr)) foreach (var l in lastErr.Split('\n').Where(x => x.Trim().Length > 0 && !DiscScanner.Noise(x)).Reverse().Take(5).Reverse()) Fail("     " + l.Trim());
                }
            }
            if (pick.Count > 1 || fail > 0) Out((cancel ? "Cancelled. " : "Finished: ") + ok + " ripped" + (fail > 0 ? ", " + fail + " failed" : "") + " — " + Dur(total.Elapsed.TotalSeconds) + " → " + o.OutDir, fail > 0 ? ConsoleColor.Yellow : ConsoleColor.Green);
            return cancel ? 130 : fail > 0 ? 1 : 0;
        }
    }
}
