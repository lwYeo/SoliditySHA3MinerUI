using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SoliditySHA3MinerUI.Helper
{
    public static class FileSystem
    {
        private static DirectoryInfo LocalAppParentDir => new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        public static DirectoryInfo LocalAppDirectory => new DirectoryInfo(Path.Combine(LocalAppParentDir.FullName, "lwyeo@github", "SoliditySHA3MinerUI"));
        public static DirectoryInfo AppDirectory => Directory.GetParent(Assembly.GetExecutingAssembly().Location);

        public static DirectoryInfo DownloadDirectory => new DirectoryInfo(Path.Combine(LocalAppDirectory.FullName, "Downloads"));
        public static DirectoryInfo MinerDirectory => new DirectoryInfo(Path.Combine(LocalAppDirectory.FullName, "SoliditySHA3Miner"));
        public static DirectoryInfo LogDirectory => new DirectoryInfo(Path.Combine(MinerDirectory.FullName, "Log"));
        public static FileInfo MinerSettingsPath => new FileInfo(Path.Combine(MinerDirectory.FullName, "SoliditySHA3Miner.conf"));

        public static string GetProcessOutput(string filePath, string args)
        {
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            })
            {
                process.Start();
                process.WaitForExit();

                return process.StandardOutput.ReadToEnd();
            }
        }

        public static JToken DeserializeFromFile(string filePath)
        {
            var retryCount = 0;
            string sJSON = string.Empty;

            while (string.IsNullOrEmpty(sJSON) && retryCount < 20)
            {
                try { sJSON = File.ReadAllText(filePath); }
                catch { Task.Delay(100); }
                retryCount++;
            }

            JToken jToken = null;
            try { jToken = JToken.Parse(sJSON); }
            catch { }
            return jToken;
        }

        public static bool SerializeToFile(object jObject, string filePath, JsonSerializerSettings settings = null)
        {
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);

                File.WriteAllText(filePath, (settings == null) ?
                    JsonConvert.SerializeObject(jObject, Formatting.Indented) :
                    JsonConvert.SerializeObject(jObject, Formatting.Indented, settings));
                return true;
            }
            catch { return false; }
        }

        public static bool UnzipMinerArchrive(string filePath, string extractPath)
        {
            if (!extractPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                extractPath += Path.DirectorySeparatorChar;
            try
            {
                if (MinerDirectory.Exists && (MinerDirectory.GetFiles().Any() || MinerDirectory.GetDirectories().Any()))
                {
                    var backupDirPath = new DirectoryInfo(MinerDirectory.FullName + "_backup" + DateTime.Now.ToFileTime());
                    if (backupDirPath.Exists) backupDirPath.Delete();

                    MinerDirectory.MoveTo(backupDirPath.FullName);
                }

                using (var archive = ZipFile.OpenRead(filePath))
                {
                    var pathToTruncate = "SoliditySHA3Miner/";
                    if (!archive.Entries.Any(e => e.FullName == pathToTruncate)) pathToTruncate = string.Empty;

                    if (!archive.Entries.Any(e => e.FullName.EndsWith("SoliditySHA3Miner.dll"))) return false;

                    UnzipArchive(archive, MinerDirectory.FullName, pathToTruncate);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Processor.ShowMessageBox("Error extracting archive", ex.Message);
                return false;
            }
        }

        private static void UnzipArchive(ZipArchive archive, string pathToExtract, string pathToTruncate)
        {
            archive.Entries.ToList().ForEach(entry =>
            {
                var archriveSubPath = entry.FullName;
                if (archriveSubPath.StartsWith(pathToTruncate))
                    archriveSubPath = archriveSubPath.Substring(pathToTruncate.Length);

                if (string.IsNullOrWhiteSpace(archriveSubPath)) return;

                var outputPath = Path.Combine(pathToExtract, archriveSubPath);
                var outputDir = Path.GetDirectoryName(outputPath);

                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
                entry.ExtractToFile(outputPath);
            });
        }
    }
}