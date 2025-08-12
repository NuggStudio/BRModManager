using BrickRigsModManager;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BrickRigsModManagerWPF
{
    public class PakModInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("website")]
        public string Website { get; set; }

        // Additional properties for mod management
        [JsonIgnore]
        public string PakFilePath { get; set; }

        [JsonIgnore]
        public string MetaFilePath { get; set; }

        [JsonIgnore]
        public bool IsEnabled { get; set; }

        [JsonIgnore]
        public string DisabledPakPath { get; set; }

        // Create a ModInfo from PakModInfo
        public ModInfo ToModInfo()
        {
            return new ModInfo
            {
                FriendlyName = Name ?? Path.GetFileNameWithoutExtension(PakFilePath),
                CreatedBy = Author ?? "Unknown",
                VersionName = Version ?? "1.0",
                Description = Description ?? "No description available",
                Category = Category ?? "Pak Mod",
                ModName = Path.GetFileNameWithoutExtension(PakFilePath),
                ModDirectory = Path.GetDirectoryName(PakFilePath),
                IsEnabled = IsEnabled,
                IsPakMod = true  // Mark as pak mod
            };
        }
    }
}
