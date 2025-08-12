using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using System.Linq;

namespace BrickRigsModManager
{
    public static class GamePlatformManager
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public class UserProfile
        {
            public string Platform { get; set; } = "Unknown";
            public string UserId { get; set; } = "";
            public string Username { get; set; } = "Unknown User";
            public BitmapImage Avatar { get; set; }
            public bool IsLoggedIn { get; set; } = false;
        }

        public static async Task<UserProfile> GetCurrentUserProfileAsync()
        {
            // Try Steam first, then Epic
            var steamProfile = await GetSteamUserProfileAsync();
            if (steamProfile.IsLoggedIn)
                return steamProfile;

            var epicProfile = await GetEpicUserProfileAsync();
            if (epicProfile.IsLoggedIn)
                return epicProfile;

            // Return default profile if no user is logged in
            return new UserProfile();
        }

        private static async Task<UserProfile> GetSteamUserProfileAsync()
        {
            var profile = new UserProfile { Platform = "Steam" };

            try
            {
                // Check if Steam is installed
                string steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath))
                    return profile;

                // Check if Steam is running
                if (!Process.GetProcessesByName("steam").Any())
                    return profile;

                // Get Steam user ID from registry or config files
                string steamId = GetSteamUserId(steamPath);
                if (string.IsNullOrEmpty(steamId))
                    return profile;

                profile.UserId = steamId;

                // Get user info from Steam local config
                string username = GetSteamUsername(steamPath, steamId);
                profile.Username = string.IsNullOrEmpty(username) ? $"Steam User {steamId}" : username;

                // Try to get avatar from Steam cache
                string avatarPath = Path.Combine(steamPath, "config", "avatarcache", $"{steamId}.jpg");
                if (File.Exists(avatarPath))
                {
                    profile.Avatar = new BitmapImage();
                    profile.Avatar.BeginInit();
                    profile.Avatar.UriSource = new Uri(avatarPath);
                    profile.Avatar.CacheOption = BitmapCacheOption.OnLoad;
                    profile.Avatar.EndInit();
                    profile.Avatar.Freeze(); // Important for cross-thread access
                }

                profile.IsLoggedIn = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Steam profile: {ex.Message}");
            }

            return profile;
        }

        private static async Task<UserProfile> GetEpicUserProfileAsync()
        {
            var profile = new UserProfile { Platform = "Epic" };

            try
            {
                // Check if Epic Games Launcher is installed
                string epicPath = GetEpicInstallPath();
                if (string.IsNullOrEmpty(epicPath))
                    return profile;

                // Check if Epic Games Launcher is running
                if (!Process.GetProcessesByName("EpicGamesLauncher").Any())
                    return profile;

                // Get Epic user ID from registry or config files
                string userId = GetEpicUserId(epicPath);
                if (string.IsNullOrEmpty(userId))
                    return profile;

                profile.UserId = userId;

                // Get username from Epic config
                string username = GetEpicUsername(epicPath, userId);
                profile.Username = string.IsNullOrEmpty(username) ? $"Epic User {userId}" : username;

                // Try to get avatar from Epic cache
                string avatarPath = Path.Combine(epicPath, "Users", userId, "avatar.png");
                if (File.Exists(avatarPath))
                {
                    profile.Avatar = new BitmapImage();
                    profile.Avatar.BeginInit();
                    profile.Avatar.UriSource = new Uri(avatarPath);
                    profile.Avatar.CacheOption = BitmapCacheOption.OnLoad;
                    profile.Avatar.EndInit();
                    profile.Avatar.Freeze(); // Important for cross-thread access
                }

                profile.IsLoggedIn = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Epic profile: {ex.Message}");
            }

            return profile;
        }



        private static string GetSteamInstallPath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
                {
                    if (key != null)
                    {
                        return key.GetValue("SteamPath") as string;
                    }
                }

                // Alternative locations
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string steamPath = Path.Combine(programFiles, "Steam");

                if (Directory.Exists(steamPath))
                    return steamPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Steam path: {ex.Message}");
            }

            return null;
        }

        public static string GetEpicInstallPath()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Epic Games\\EpicGamesLauncher"))
                {
                    if (key != null)
                    {
                        return key.GetValue("AppDataPath") as string;
                    }
                }

                // Alternative location
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string epicPath = Path.Combine(appData, "EpicGamesLauncher");

                if (Directory.Exists(epicPath))
                    return epicPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Epic path: {ex.Message}");
            }

            return null;
        }

        private static string GetSteamUserId(string steamPath)
        {
            try
            {
                // Try to get the active user from Steam's loginusers.vdf file
                string loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");

                if (File.Exists(loginUsersPath))
                {
                    string content = File.ReadAllText(loginUsersPath);

                    // Simple parsing to find the most recent login
                    // This is a very basic approach - a proper VDF parser would be better
                    string mostRecentUser = null;
                    DateTime mostRecentTime = DateTime.MinValue;

                    // Find user blocks in the VDF file
                    int startIndex = 0;
                    while ((startIndex = content.IndexOf("\"", startIndex)) != -1)
                    {
                        int endIndex = content.IndexOf("\"", startIndex + 1);
                        if (endIndex == -1) break;

                        string userId = content.Substring(startIndex + 1, endIndex - startIndex - 1);

                        // Check if this looks like a Steam ID (digits only)
                        if (userId.All(char.IsDigit))
                        {
                            // Look for the "Timestamp" field
                            string timestampMarker = "\"Timestamp\"";
                            int timestampIndex = content.IndexOf(timestampMarker, endIndex);

                            if (timestampIndex != -1 && timestampIndex < content.IndexOf("}", endIndex))
                            {
                                int valueStart = content.IndexOf("\"", timestampIndex + timestampMarker.Length);
                                int valueEnd = content.IndexOf("\"", valueStart + 1);

                                if (valueStart != -1 && valueEnd != -1)
                                {
                                    string timestampStr = content.Substring(valueStart + 1, valueEnd - valueStart - 1);

                                    if (long.TryParse(timestampStr, out long timestamp))
                                    {
                                        DateTime loginTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;

                                        if (loginTime > mostRecentTime)
                                        {
                                            mostRecentTime = loginTime;
                                            mostRecentUser = userId;
                                        }
                                    }
                                }
                            }
                        }

                        startIndex = endIndex + 1;
                    }

                    return mostRecentUser;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Steam user ID: {ex.Message}");
            }

            return null;
        }

        private static string GetEpicUserId(string epicPath)
        {
            try
            {
                // Try to find the user ID from Epic's saved data
                string savedDataPath = Path.Combine(epicPath, "Saved", "Config", "Windows");

                if (Directory.Exists(savedDataPath))
                {
                    // Look for the user profile file
                    foreach (var file in Directory.GetFiles(savedDataPath, "*.ini"))
                    {
                        string content = File.ReadAllText(file);

                        // Look for a line containing "AccountId="
                        foreach (var line in content.Split('\n'))
                        {
                            if (line.Contains("AccountId="))
                            {
                                return line.Split('=')[1].Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Epic user ID: {ex.Message}");
            }

            return null;
        }

        private static string GetSteamUsername(string steamPath, string steamId)
        {
            try
            {
                // Try to get the username from Steam's config
                string configPath = Path.Combine(steamPath, "config", "config.vdf");

                if (File.Exists(configPath))
                {
                    string content = File.ReadAllText(configPath);

                    // Look for the PersonaName field
                    string personaNameMarker = "\"PersonaName\"";
                    int personaNameIndex = content.IndexOf(personaNameMarker);

                    if (personaNameIndex != -1)
                    {
                        int valueStart = content.IndexOf("\"", personaNameIndex + personaNameMarker.Length);
                        int valueEnd = content.IndexOf("\"", valueStart + 1);

                        if (valueStart != -1 && valueEnd != -1)
                        {
                            return content.Substring(valueStart + 1, valueEnd - valueStart - 1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Steam username: {ex.Message}");
            }

            return null;
        }

        private static string GetEpicUsername(string epicPath, string userId)
        {
            try
            {
                // Try to find the username from Epic's saved data
                string savedDataPath = Path.Combine(epicPath, "Saved", "Config", "Windows");

                if (Directory.Exists(savedDataPath))
                {
                    // Look for the user profile file
                    foreach (var file in Directory.GetFiles(savedDataPath, "*.ini"))
                    {
                        string content = File.ReadAllText(file);

                        // Look for a line containing "DisplayName="
                        foreach (var line in content.Split('\n'))
                        {
                            if (line.Contains("DisplayName="))
                            {
                                return line.Split('=')[1].Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting Epic username: {ex.Message}");
            }

            return null;
        }

        public static bool IsBrickRigsInstalled()
        {
            try
            {
                // Check Steam installation
                string steamPath = GetSteamInstallPath();
                if (!string.IsNullOrEmpty(steamPath))
                {
                    string steamAppsPath = Path.Combine(steamPath, "steamapps", "common", "Brick Rigs");
                    if (Directory.Exists(steamAppsPath))
                        return true;

                    // Check for library folders
                    string libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(libraryFoldersPath))
                    {
                        string content = File.ReadAllText(libraryFoldersPath);

                        // Simple parsing to find library paths
                        // This is a very basic approach - a proper VDF parser would be better
                        int pathIndex = 0;
                        while ((pathIndex = content.IndexOf("\"path\"", pathIndex)) != -1)
                        {
                            int valueStart = content.IndexOf("\"", pathIndex + 6);
                            int valueEnd = content.IndexOf("\"", valueStart + 1);

                            if (valueStart != -1 && valueEnd != -1)
                            {
                                string libraryPath = content.Substring(valueStart + 1, valueEnd - valueStart - 1);
                                string brickRigsPath = Path.Combine(libraryPath, "steamapps", "common", "Brick Rigs");

                                if (Directory.Exists(brickRigsPath))
                                    return true;
                            }

                            pathIndex = valueEnd + 1;
                        }
                    }
                }

                // Check Epic installation
                string epicPath = GetEpicInstallPath();
                if (!string.IsNullOrEmpty(epicPath))
                {
                    string manifestsPath = Path.Combine(epicPath, "Manifests");

                    if (Directory.Exists(manifestsPath))
                    {
                        foreach (var file in Directory.GetFiles(manifestsPath, "*.item"))
                        {
                            string content = File.ReadAllText(file);

                            // Check if this is the Brick Rigs manifest
                            if (content.Contains("\"DisplayName\": \"Brick Rigs\""))
                            {
                                // Extract the install location
                                int installLocationIndex = content.IndexOf("\"InstallLocation\"");
                                if (installLocationIndex != -1)
                                {
                                    int valueStart = content.IndexOf("\"", installLocationIndex + 18);
                                    int valueEnd = content.IndexOf("\"", valueStart + 1);

                                    if (valueStart != -1 && valueEnd != -1)
                                    {
                                        string installPath = content.Substring(valueStart + 1, valueEnd - valueStart - 1);

                                        if (Directory.Exists(installPath))
                                            return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking Brick Rigs installation: {ex.Message}");
            }

            return false;
        }
    }
}
