using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;

namespace SoliditySHA3MinerUI.Helper
{
    public static class Network
    {
        public static string MinerReleasesAPI_Path => "https://api.github.com/repos/lwYeo/SoliditySHA3Miner/releases";
        public static string UiReleasesAPI_Path => "https://api.github.com/repos/lwYeo/SoliditySHA3MinerUI/releases";

        public static bool IsNetworkConnected()
        {
            var pingSuccess = false;
            string[] pingAddresses = { "1.1.1.1", "1.0.0.1", "8.8.8.8", "8.8.4.4" };

            foreach (var address in pingAddresses)
                using (var pinger = new Ping())
                {
                    try
                    {
                        var reply = pinger.Send(address, 1000);
                        pingSuccess = reply.Status == IPStatus.Success;
                    }
                    catch { }
                    if (pingSuccess) break;
                }

            return pingSuccess;
        }

        public static bool GetLatestMinerInfo(out Version version, out string downloadUrl)
        {
            return GetLatestGithubReleaseInfo(MinerReleasesAPI_Path, ".zip", out version, out downloadUrl);
        }

        public static bool GetLatestUiInfo(out Version version, out string downloadUrl)
        {
            return GetLatestGithubReleaseInfo(UiReleasesAPI_Path, ".msi", out version, out downloadUrl);
        }

        private static bool GetLatestGithubReleaseInfo(string apiPath, string fileExtension, out Version version, out string downloadUrl)
        {
            version = new Version();
            downloadUrl = string.Empty;
            try
            {
                var apiResult = DeserializeFromURL<List<API.GithubRelease>>(apiPath, "application/vnd.github.v3+json");
                if (apiResult == null) return false;

                var latestRelease = apiResult.Where(r =>
                    {
                        if (r.IsDraft || r.IsPreRelease) return false;

                        var hasZipFile = r.AssetsList.Any(a => ((a.FileName ?? string.Empty).EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                                                               && (a.FileState ?? string.Empty).Equals("uploaded", StringComparison.OrdinalIgnoreCase));
                        return hasZipFile;
                    })
                    .OrderByDescending(r => r.PublishedDateTime)
                    .FirstOrDefault();

                if (latestRelease == null) return false;

                downloadUrl = latestRelease.AssetsList.
                                            Single(a => ((a.FileName ?? string.Empty).EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                                                        && (a.FileState ?? string.Empty).Equals("uploaded", StringComparison.OrdinalIgnoreCase)).
                                            DownloadURL;

                var sLatestVersion = string.Empty;
                foreach (var c in latestRelease.TagName.TrimStart())
                {
                    if (!char.IsDigit(c) && !c.Equals('.')) break;
                    sLatestVersion += c;
                }
                if (!Version.TryParse(sLatestVersion, out Version latestVersion)) return false;

                version = latestVersion;
                return true;
            }
            catch (Exception ex)
            {
                Processor.ShowMessageBox("Error searching latest GUI information", ex.Message);
                return false;
            }
        }

        public static string GetHttpResponse(string url, string mediaType = "")
        {
            using (var client = new HttpClient())
            {
                if (!string.IsNullOrWhiteSpace(mediaType))
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));

                // You must set a user agent so that the CRLF requirement on the header parsing is met.
                // Otherwise you may get an excpetion message with "The server committed a protocol violation. Section=ResponseStatusLine"
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));

                return client.GetStringAsync(url).Result;
            }
        }

        public static WebClient DownloadFromURL(string fileURL, string filePath,
                                                DownloadProgressChangedEventHandler progressHandler,
                                                AsyncCompletedEventHandler completedHandler)
        {
            var client = new WebClient();
            client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(progressHandler);
            client.DownloadFileCompleted += new AsyncCompletedEventHandler(completedHandler);
            client.DownloadFileAsync(new Uri(fileURL), filePath);
            return client;
        }

        public static T DeserializeFromURL<T>(string url, string mediaType = "")
        {
            string sJSON = string.Empty;
            var jObject = (T)Activator.CreateInstance(typeof(T));
            try
            {
                sJSON = GetHttpResponse(url, mediaType);
                jObject = JsonConvert.DeserializeObject<T>(sJSON);
            }
            catch { }
            return jObject;
        }
    }
}