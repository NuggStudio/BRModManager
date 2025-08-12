using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace BrickRigsModManager
{
    public class Category
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("iconClass")]
        public string IconClass { get; set; }

        [JsonIgnore]
        public ObservableCollection<FeaturedModInfo> Mods { get; set; } = new ObservableCollection<FeaturedModInfo>();
    }

    public class FeaturedModsWrapper
    {
        [JsonProperty("categories")]
        public List<Category> Categories { get; set; }

        [JsonProperty("featuredMods")]
        public List<FeaturedModInfo> FeaturedMods { get; set; }
    }

    public class ModCategory
    {
        public string CategoryName { get; set; }
        public string CategoryDescription { get; set; }
        public ObservableCollection<ModInfo> Mods { get; set; }
    }


    public class FeaturedModInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("shortDescription")]
        public string ShortDescription { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("logoUrl")]
        public string LogoUrl { get; set; }

        [JsonProperty("bannerUrl")]
        public string BannerUrl { get; set; }

        [JsonProperty("downloadUrl")]
        public string DownloadUrl { get; set; }

        [JsonProperty("features")]
        public string[] Features { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("downloads")]
        public int Downloads { get; set; }

        [JsonProperty("rating")]
        public int Rating { get; set; }
    }
}
