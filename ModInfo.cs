using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace BrickRigsModManager
{
    public class ModInfo : INotifyPropertyChanged
    {
        private bool _isEnabled;

        [JsonProperty("FileVersion")]
        public int FileVersion { get; set; }

        [JsonProperty("Version")]
        public int Version { get; set; }

        [JsonProperty("VersionName")]
        public string VersionName { get; set; }

        [JsonProperty("FriendlyName")]
        public string FriendlyName { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("Category")]
        public string Category { get; set; }

        [JsonProperty("CreatedBy")]
        public string CreatedBy { get; set; }

        [JsonProperty("CreatedByURL")]
        public string CreatedByURL { get; set; }

        [JsonProperty("DocsURL")]
        public string DocsURL { get; set; }

        [JsonProperty("MarketplaceURL")]
        public string MarketplaceURL { get; set; }

        [JsonProperty("SupportURL")]
        public string SupportURL { get; set; }

        [JsonProperty("CanContainContent")]
        public bool CanContainContent { get; set; }

        [JsonProperty("IsBetaVersion")]
        public bool IsBetaVersion { get; set; }

        [JsonProperty("IsExperimentalVersion")]
        public bool IsExperimentalVersion { get; set; }

        [JsonProperty("Installed")]
        public bool Installed { get; set; }

        // Additional properties for mod management
        [JsonIgnore]
        public string ModName { get; set; }

        [JsonIgnore]
        public string ModDirectory { get; set; }

        [JsonIgnore]
        public List<string> PaksDirectories { get; set; } = new List<string>();

        [JsonIgnore]
        public List<string> DisabledPaksDirectories { get; set; } = new List<string>();

        [JsonIgnore]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        [JsonIgnore]
        public bool IsPakMod { get; set; }

        [JsonIgnore]
        public string DisabledPakPath { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
