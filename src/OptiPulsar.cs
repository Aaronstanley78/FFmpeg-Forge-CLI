using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FFmpegForge
{
    /// 3.0.1 — Built-in OptiPulsar engine. OptiPulsar is Visual Enhancer's name for NERVE (Philip Hofmann,
    /// huggingface.co/Phips/NERVE, Apache-2.0), a small norm-free convolutional super-resolution network (x2 / x4).
    /// FFmpeg Forge installs Python / PyTorch and the official NERVE weights in its own folder and runs its own
    /// implementation of the architecture (forge_optipulsar.py) — no Visual Enhancer needed. Videos and pictures.
    public static class OptiBuiltin
    {
        public static string Dir = "";
        public const string HfBase = "https://huggingface.co/Phips/NERVE/resolve/main/models/";
        public static readonly string[] HfFiles = { "2x_NERVE_release.safetensors", "4x_NERVE_release.safetensors" };

        public static string DefaultInstallDir
        {
            get
            {
                string app = Path.Combine(AppPaths.AppDir, "OptiPulsar");
                try
                {
                    string probeDir = Directory.Exists(app) ? app : AppPaths.AppDir;
                    string t = Path.Combine(probeDir, ".ffmpegforge-write-test"); File.WriteAllText(t, "x"); File.Delete(t);
                    return app;
                }
                catch { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FFmpegForge", "OptiPulsar"); }
            }
        }

        public static string Root { get { return string.IsNullOrEmpty(Dir) ? DefaultInstallDir : Dir; } }
        public static string Weights { get { return Path.Combine(Root, "models"); } }
        public static string Model2x { get { return Path.Combine(Weights, HfFiles[0]); } }
        public static string Model4x { get { return Path.Combine(Weights, HfFiles[1]); } }
        public static string Helper { get { return Path.Combine(Root, "forge_optipulsar.py"); } }

        public static string Python
        {
            get
            {
                foreach (var p in new[] { Path.Combine(Root, "venv", "Scripts", "python.exe"), Path.Combine(Root, "venv", "bin", "python") })
                    try { if (File.Exists(p)) return p; } catch { }
                return null;
            }
        }

        static bool Big(string f) { try { var i = new FileInfo(f); return i.Exists && i.Length > 1000000; } catch { return false; } }

        public static bool Ready { get { return Python != null && Big(Model2x) && Big(Model4x); } }

        public static string Status()
        {
            if (Ready) return "Ready: OptiPulsar model (NERVE) installed in " + Root;
            if (Python != null) return "OptiPulsar is partly installed in " + Root + ": click \"Install OptiPulsar model\" again to finish.";
            return "OptiPulsar model not installed yet: click \"Install OptiPulsar model\" (about 3 GB with PyTorch). No Visual Enhancer needed.";
        }

        /// Writes the streaming helper (embedded in FFmpegForge.exe) into the install folder.
        public static void SyncHelper()
        {
            try
            {
                Directory.CreateDirectory(Root);
                string want;
                using (var st = typeof(OptiBuiltin).Assembly.GetManifestResourceStream("forge_optipulsar.py"))
                using (var r = new StreamReader(st, Encoding.UTF8)) want = r.ReadToEnd().Replace("\r\n", "\n");
                if (!File.Exists(Helper) || File.ReadAllText(Helper) != want) File.WriteAllText(Helper, want, new UTF8Encoding(false));
            }
            catch { }
        }

        public static string WriteInstaller(string root)
        {
            Directory.CreateDirectory(root);
            string bat = Path.Combine(root, "install_optipulsar.bat");
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("chcp 65001 >nul");
            sb.AppendLine("title FFmpeg Forge - OptiPulsar (NERVE) installation");
            sb.AppendLine("setlocal");
            sb.AppendLine("cd /d \"%~dp0\"");
            sb.AppendLine("echo ============================================================");
            sb.AppendLine("echo  OptiPulsar for FFmpeg Forge = NERVE by Philip Hofmann (Apache-2.0)");
            sb.AppendLine("echo  Folder: %CD%");
            sb.AppendLine("echo  Downloads: uv + Python 3.12, PyTorch CUDA 12.8 (about 3 GB),");
            sb.AppendLine("echo  NERVE x2 and x4 weights (Hugging Face Phips/NERVE, a few MB).");
            sb.AppendLine("echo ============================================================");
            sb.AppendLine("echo.");
            sb.AppendLine("if not exist uv\\uv.exe (");
            sb.AppendLine("  echo [1/4] Downloading uv...");
            sb.AppendLine("  powershell -NoProfile -ExecutionPolicy Bypass -Command \"$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing '" + SeedEngine.UvZip + "' -OutFile uv.zip; Expand-Archive -Force uv.zip uv; Remove-Item uv.zip\" || goto :fail");
            sb.AppendLine(")");
            sb.AppendLine("echo [2/4] Python 3.12 environment...");
            sb.AppendLine("set UV_PYTHON_INSTALL_DIR=%CD%\\python");
            sb.AppendLine("set UV_CACHE_DIR=%CD%\\cache");
            sb.AppendLine("set UV_LINK_MODE=copy");
            sb.AppendLine("if not exist venv\\Scripts\\python.exe uv\\uv.exe venv --python 3.12 --seed venv || goto :fail");
            sb.AppendLine("echo [3/4] PyTorch with CUDA 12.8 (large download)...");
            sb.AppendLine("uv\\uv.exe pip install --python venv\\Scripts\\python.exe torch --index-url " + SeedEngine.TorchIndex + " || goto :fail");
            sb.AppendLine("uv\\uv.exe pip install --python venv\\Scripts\\python.exe numpy \"safetensors>=0.4\" || goto :fail");
            sb.AppendLine("echo [4/4] NERVE weights (huggingface.co/Phips/NERVE)...");
            sb.AppendLine("if not exist models mkdir models");
            foreach (var f in HfFiles)
                sb.AppendLine("if not exist models\\" + f + " powershell -NoProfile -ExecutionPolicy Bypass -Command \"$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -UseBasicParsing '" + HfBase + f + "' -OutFile 'models\\" + f + ".part'; Move-Item -Force 'models\\" + f + ".part' 'models\\" + f + "'\" || goto :fail");
            sb.AppendLine("venv\\Scripts\\python.exe -c \"import torch;print('PyTorch',torch.__version__,'CUDA',torch.cuda.is_available(), torch.cuda.get_device_name(0) if torch.cuda.is_available() else '(CPU only: slow)')\"");
            sb.AppendLine("echo.");
            sb.AppendLine("echo OptiPulsar is installed. You can close this window and go back to FFmpeg Forge.");
            sb.AppendLine("pause");
            sb.AppendLine("exit /b 0");
            sb.AppendLine(":fail");
            sb.AppendLine("echo.");
            sb.AppendLine("echo Installation failed. Check your internet connection and run install_optipulsar.bat again.");
            sb.AppendLine("pause");
            sb.AppendLine("exit /b 1");
            File.WriteAllText(bat, sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
            return bat;
        }
    }
}
