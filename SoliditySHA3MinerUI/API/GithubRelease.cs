using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SoliditySHA3MinerUI.API
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class GithubRelease
    {
        [JsonProperty(PropertyName = "html_url")]
        public string PageURL { get; set; }

        [JsonProperty(PropertyName = "tag_name")]
        public string TagName { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Title { get; set; }

        [JsonProperty(PropertyName = "draft")]
        public bool IsDraft { get; set; }

        [JsonProperty(PropertyName = "prerelease")]
        public bool IsPreRelease { get; set; }

        [JsonProperty(PropertyName = "published_at")]
        public DateTime PublishedDateTime { get; set; }

        [JsonProperty(PropertyName = "assets")]
        public List<Assets> AssetsList { get; set; }

        public GithubRelease()
        {
            AssetsList = new List<Assets>();
        }

        public class Assets
        {
            [JsonProperty(PropertyName = "name")]
            public string FileName { get; set; }

            [JsonProperty(PropertyName = "content_type")]
            public string ContentType { get; set; }

            [JsonProperty(PropertyName = "state")]
            public string FileState { get; set; }

            [JsonProperty(PropertyName = "size")]
            public ulong FileSizeByte { get; set; }

            [JsonProperty(PropertyName = "updated_at")]
            public DateTime UpdatedDateTime { get; set; }

            [JsonProperty(PropertyName = "browser_download_url")]
            public string DownloadURL { get; set; }

            public Assets()
            {
            }
        }
    }
}