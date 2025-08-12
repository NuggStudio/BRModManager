using System;
using System.IO;
using Newtonsoft.Json;

namespace BrickRigsModManagerWPF
{
    public class AppSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BrickRigsModManager",
            "settings.json");

        public string Theme { get; set; } = "Dark";

        // Other settings...

        public static AppSettings Current { get; private set; } = Load();

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                        return settings;
                }
            }
            catch (Exception)
            {
                // Ignore errors and return default settings
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json);
                Current = this;
            }
            catch (Exception)
            {
                // Ignore errors
            }
        }
    }
}
