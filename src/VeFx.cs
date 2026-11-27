using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FFmpegForge
{
    /// 3.0.1 — Visual Enhancer models run through the user's own Visual Enhancer installation (VE_CLI.exe, Merserk).
    /// FFmpeg Forge does not contain, copy or redistribute any Visual Enhancer file or model: it calls VE_CLI.exe,
    /// which uses the models downloaded in Visual Enhancer (Models tab).
    ///   AI Upscale: OptiPulsar (documented flags, --engine optipulsar; OptiPulsar also has a built-in engine)
    ///   Deblurring: FocusPulsar (no command-line flag: FFmpeg Forge writes a Visual Enhancer preset; a preset exported
    ///   from the Visual Enhancer app can replace it: VE-presets\focuspulsar.json next to FFmpegForge.exe)
    public static class VeFx
    {
        public static string PresetDir { get { return Path.Combine(AppPaths.AppDir, "VE-presets"); } }

        /// The user's exported preset (VE-presets\<key>.json), if any.
        public static string UserPreset(string key)
        {
            try { string p = Path.Combine(PresetDir, key + ".json"); return File.Exists(p) ? p : null; } catch { return null; }
        }

        static string Js(object v)
        {
            if (v is string) return "\"" + ((string)v).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is double) return ((double)v).ToString("0.###", CultureInfo.InvariantCulture);
            if (v is int) return ((int)v).ToString(CultureInfo.InvariantCulture);
            var arr = v as string[];
            if (arr != null) return "[" + string.Join(", ", arr.Select(a => Js(a)).ToArray()) + "]";
            return "null";
        }

        /// Preset written for VE_CLI --preset: the same settings at the top level and under "settings".
        static string WritePreset(string dir, string key, List<KeyValuePair<string, object>> kv)
        {
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.Append("{\n  \"schema_version\": 1,\n  \"generator\": \"FFmpeg Forge 3.0\",\n  \"settings\": {\n");
            sb.Append(string.Join(",\n", kv.Select(p => "    \"" + p.Key + "\": " + Js(p.Value)).ToArray()));
            sb.Append("\n  },\n");
            sb.Append(string.Join(",\n", kv.Select(p => "  \"" + p.Key + "\": " + Js(p.Value)).ToArray()));
            sb.Append("\n}\n");
            string json = sb.ToString();
            string path = Path.Combine(dir, "forge_" + key + ".json");
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return path;
        }

        static readonly string[] Order = { "denoising", "deblurring", "upscale" };

        public static string PresetFor(string dir, string key, double strength, int scale)
        {
            string user = UserPreset(key);
            if (user != null) return user;
            var kv = new List<KeyValuePair<string, object>>();
            Action<string, object> add = (k, v) => kv.Add(new KeyValuePair<string, object>(k, v));
            add("image_stage_order", Order); add("video_stage_order", Order);
            // FocusPulsar (Deblurring)
            add("image_enabled_stages", new[] { "deblurring" }); add("video_enabled_stages", new[] { "deblurring" });
            add("standard_image_deblur_mode", "AI Advanced"); add("standard_video_deblur_mode", "AI Advanced");
            add("standard_image_deblur_intensity", strength); add("standard_video_deblur_intensity", strength);
            return WritePreset(dir, key, kv);
        }

        /// VE_CLI arguments for OptiPulsar run by Visual Enhancer.
        public static string UpscaleArgs(string model, int scale, string tempDir)
        {
            return "--engine optipulsar --model optipulsar --scale " + scale + " --format png";
        }

        public static string ModelName(string model)
        {
            switch (model) { case "optipulsar": return "OptiPulsar"; case "razornext": return "RazorNext"; default: return "VisionNext"; }
        }

        public static string Status(string what)
        {
            string exe = VisionEngine.ExePath;
            if (exe == null)
                return what + " runs through your Visual Enhancer installation: choose its folder (the one with VE_CLI.exe) with \"Visual Enhancer folder…\".";
            return "Ready: " + exe + "\nThe model must be downloaded once in Visual Enhancer (Models tab); Visual Enhancer's own licence applies.";
        }

        // ---------------- stage: Deblurring FocusPulsar ----------------
        static string Q(string x) { return "\"" + x.Replace("\"", "\\\"") + "\""; }

        public class Stage
        {
            public VisionEngine.Plan Plan;
            public string Intermediate, TempDir, Key, Name;
            public bool Picture;
            public double Seconds;
        }

        /// source → VE_CLI (FocusPulsar, batches of frames) → lossless intermediate (trim applied, audio copied).
        public static Stage Build(Job job, ConvSettings s, bool pictureOutput, string key, double strength, out string error)
        {
            error = null;
            string exe = VisionEngine.ExePath;
            string name = "FocusPulsar";
            if (exe == null) { error = name + " runs through Visual Enhancer: choose the folder that contains VE_CLI.exe (\"Visual Enhancer folder…\")."; return null; }
            int w = job.Width, h = job.Height;
            if (w < 16 || h < 16) { error = "Could not read the picture size (ffprobe)."; return null; }
            bool srcVideo = job.Kind == MediaKind.Video || CommandBuilder.IsAnimatedImageSource(job);
            bool picture = pictureOutput || !srcVideo;
            var st = new Stage { Picture = picture, Key = key, Name = name };
            st.TempDir = Path.Combine(Path.GetTempPath(), "FFmpegForge-" + name + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            st.Intermediate = Path.Combine(st.TempDir, picture ? key + ".png" : key + ".mkv");

            int fn = job.FpsNum > 0 ? job.FpsNum : 30, fd = job.FpsDen > 0 ? job.FpsDen : 1;
            if (s.Fps > 0 && !picture) { fn = (int)Math.Round(s.Fps * 1000); fd = 1000; }
            string rate = fn + "/" + fd;
            double ss = s.TrimStart > 0 ? s.TrimStart : 0, to = s.TrimEnd > 0 ? s.TrimEnd : -1;
            string trim = picture ? "" : (ss > 0 ? "-ss " + Formats.Inv(ss) + " " : "") + (to > ss && to > 0 ? "-to " + Formats.Inv(to) + " " : "");
            double dur = job.Duration;
            if (to > 0 && (dur <= 0 || to < dur)) dur = to;
            dur = Math.Max(0, dur - ss);
            st.Seconds = picture ? 0 : dur;
            string m709 = h >= 720 ? "bt709" : "smpte170m";
            string preset = PresetFor(st.TempDir, key, Math.Max(0, Math.Min(2, strength)), 1);
            string veArgs = "--preset " + Q(preset) + " --format png";

            // the plan works in a sub-folder: VisionEngine.Run deletes its own temporary folder, not the intermediate
            var p = new VisionEngine.Plan { Exe = exe, Builtin = false, Name = name, WorkDir = Path.GetDirectoryName(exe), Output = st.Intermediate, InW = w, InH = h, OutW = w, OutH = h, Video = !picture, TempDir = Path.Combine(st.TempDir, "work"), Expected = st.Seconds };
            if (picture)
            {
                p.VeIn = Path.Combine(p.TempDir, "input.png");
                p.VeOut = Path.Combine(p.TempDir, key + "_ve.png");
                p.PreArgs = "-hide_banner -nostdin -y -loglevel error -i " + Q(job.Path) + " -map 0:v:0 -frames:v 1 -vf format=rgb24 " + Q(p.VeIn);
                p.VeArgs = "upscale " + Q(p.VeIn) + " -o " + Q(p.VeOut) + " " + veArgs;
                p.EncArgs = "-hide_banner -nostdin -y -loglevel warning -i " + Q(p.VeOut) + " -frames:v 1 -update 1 " + Q(st.Intermediate);
            }
            else
            {
                p.Streamed = true;
                p.Batch = (int)Math.Max(8, Math.Min(96, 450e6 / ((double)w * h * 3)));
                p.VeTemplate = "upscale \"%IN%\" -o \"%OUT%\" " + veArgs;
                p.PreArgs = "-hide_banner -nostdin -loglevel error " + trim + "-i " + Q(job.Path) + " -map 0:v:0 -an -sn -dn -vf format=rgb24 -r " + rate + " -f image2pipe -c:v png -compression_level 1 -";
                p.EncArgs = "-hide_banner -nostdin -y -loglevel warning -progress pipe:1 -nostats " +
                            "-f image2pipe -framerate " + rate + " -thread_queue_size 64 -c:v png -i - " + trim + "-i " + Q(job.Path) +
                            " -map 0:v:0 -map 1:a? -vf " + Q("scale=" + w + ":" + h + ":out_color_matrix=" + m709 + ":out_range=tv,format=yuv444p") +
                            " -c:v utvideo -pred median -color_primaries " + m709.Replace("smpte170m", "smpte170m") + " -color_trc " + m709 + " -colorspace " + m709 +
                            (job.IsDsdAudio ? " -af " + Q("aresample=176400,lowpass=f=40000,volume=2.0") + " -c:a flac" : " -c:a copy") + " -shortest " + Q(st.Intermediate);
            }
            st.Plan = p;
            return st;
        }

        public static void Cleanup(Stage st)
        {
            try { if (st != null && st.TempDir != null && Directory.Exists(st.TempDir)) Directory.Delete(st.TempDir, true); } catch { }
        }
    }
}
