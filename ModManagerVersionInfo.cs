// ModManagerVersionInfo.cs
using System;
using System.Collections.Generic;

namespace BrickRigsModManager
{
    public class ModManagerVersionInfo
    {
        public string Version { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string DownloadUrl { get; set; }
        public string DirectDownloadUrl { get; set; }
        public List<string> WhatsNew { get; set; }
        public string MinRequiredVersion { get; set; }
        public bool IsCriticalUpdate { get; set; }
    }
}
