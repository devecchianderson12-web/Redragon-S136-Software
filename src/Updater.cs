using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace S136Battery
{
    // Busca la última release en GitHub y reemplaza el .exe al instalar.
    static class Updater
    {
        public const string Repo = "devecchianderson12-web/Redragon-S136-Software";
        const string AssetName = "S136Battery.exe";

        public class Release
        {
            public Version Version;
            public string Tag;
            public string Url;
        }

        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "S136Battery/" + Program.Version;
            return wc;
        }

        // null si no hay nada más nuevo o no hay red.
        public static Release Check()
        {
            try
            {
                string json;
                using (var wc = Client())
                {
                    wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    wc.Encoding = Encoding.UTF8;
                    json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
                }
                var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"(v?([0-9]+(\\.[0-9]+){1,3}))\"");
                var url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/" + Regex.Escape(AssetName) + ")\"");
                if (!tag.Success || !url.Success) return null;
                var latest = new Version(tag.Groups[2].Value);
                if (latest <= new Version(Program.Version)) return null;
                return new Release { Version = latest, Tag = tag.Groups[1].Value, Url = url.Groups[1].Value };
            }
            catch { return null; }
        }

        // Baja el .exe nuevo y deja un script que lo cambia cuando esta app se cierre.
        // Devuelve null si todo bien, o el error.
        public static string Prepare(Release r, string exePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(exePath);
                string tmp = Path.Combine(dir, Path.GetFileNameWithoutExtension(exePath) + ".new");
                using (var wc = Client()) wc.DownloadFile(r.Url, tmp);
                var head = new byte[2];
                using (var f = File.OpenRead(tmp))
                    if (f.Length < 10000 || f.Read(head, 0, 2) != 2 || head[0] != 'M' || head[1] != 'Z')
                    {
                        f.Close();
                        File.Delete(tmp);
                        return "La descarga no es un .exe válido.";
                    }

                int pid = Process.GetCurrentProcess().Id;
                string script = Path.Combine(Path.GetTempPath(), "s136-update-" + pid + ".cmd");
                File.WriteAllText(script,
                    "@echo off\r\n" +
                    ":wait\r\n" +
                    "tasklist /fi \"PID eq " + pid + "\" | find \"" + pid + "\" >nul && (ping -n 2 127.0.0.1 >nul & goto wait)\r\n" +
                    "move /y \"" + tmp + "\" \"" + exePath + "\" >nul\r\n" +
                    "start \"\" \"" + exePath + "\"\r\n" +
                    "del \"%~f0\"\r\n", Encoding.Default);
                Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return "Sin permiso para escribir en la carpeta de la app.";
            }
            catch (Exception e)
            {
                return "No se pudo descargar: " + e.Message;
            }
        }
    }
}
