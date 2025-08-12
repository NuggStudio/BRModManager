using BrickRigsModManagerWPF;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BrickRigsModManager
{
    public class ModManager
    {
        private string _gamePath;
        private string _modsPath;
        private readonly HttpClient _httpClient;

        public ModManager()
        {
            _httpClient = new HttpClient();
        }

        public void SetGamePath(string gamePath)
        {
            _gamePath = gamePath;

            // Check for different possible mod paths
            var possibleModPaths = new[]
            {
                Path.Combine(gamePath, "BrickRigs", "Mods"),
                Path.Combine(gamePath, "Mods")
            };

            foreach (var path in possibleModPaths)
            {
                if (Directory.Exists(path))
                {
                    _modsPath = path;
                    return;
                }
            }

            // If no mod directory exists, create one
            _modsPath = Path.Combine(gamePath, "BrickRigs", "Mods");
            if (!Directory.Exists(_modsPath))
            {
                try
                {
                    Directory.CreateDirectory(_modsPath);
                }
                catch
                {
                    // If we can't create the directory in BrickRigs, try the root
                    _modsPath = Path.Combine(gamePath, "Mods");
                    if (!Directory.Exists(_modsPath))
                    {
                        Directory.CreateDirectory(_modsPath);
                    }
                }
            }
        }

        public async Task InstallModFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(_modsPath))
                throw new InvalidOperationException("Game path not set");

            LogDebug($"Installing mod from file: {filePath}");

            // Check file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension == ".zip")
            {
                // Handle ZIP file
                await InstallModFromZip(filePath);
            }
            else if (extension == ".pak")
            {
                // Handle PAK file
                await InstallPakMod(filePath);
            }
            else if (extension == ".pakbundle")
            {
                // Handle PAKBUNDLE file
                await InstallPakBundleMod(filePath);
            }
            else
            {
                throw new InvalidOperationException("Unsupported file type. Only .zip, .pak, and .pakbundle files are supported.");
            }
        }

        private async Task InstallPakMod(string pakFilePath)
        {
            var pakFileName = Path.GetFileName(pakFilePath);
            var pakName = Path.GetFileNameWithoutExtension(pakFilePath);

            LogDebug($"Installing pak mod: {pakFileName}");

            // Check for pakmeta file next to the pak file
            var metaFilePath = Path.ChangeExtension(pakFilePath, ".pakmeta");
            var hasMetaFile = File.Exists(metaFilePath);

            // Create Content/Paks/~mods directory if it doesn't exist
            var contentPath = Path.Combine(_gamePath, "BrickRigs", "Content");
            if (!Directory.Exists(contentPath))
            {
                contentPath = Path.Combine(_gamePath, "Content");
                if (!Directory.Exists(contentPath))
                {
                    LogDebug($"Creating Content directory: {contentPath}");
                    Directory.CreateDirectory(contentPath);
                }
            }

            var paksPath = Path.Combine(contentPath, "Paks");
            if (!Directory.Exists(paksPath))
            {
                LogDebug($"Creating Paks directory: {paksPath}");
                Directory.CreateDirectory(paksPath);
            }

            var modsPath = Path.Combine(paksPath, "~mods");
            if (!Directory.Exists(modsPath))
            {
                LogDebug($"Creating ~mods directory: {modsPath}");
                Directory.CreateDirectory(modsPath);
            }

            // Copy the pak file to the ~mods directory
            var destPakPath = Path.Combine(modsPath, pakFileName);
            LogDebug($"Copying pak file to: {destPakPath}");
            File.Copy(pakFilePath, destPakPath, true);

            // Copy the meta file if it exists
            if (hasMetaFile)
            {
                var destMetaPath = Path.Combine(modsPath, Path.GetFileName(metaFilePath));
                LogDebug($"Copying meta file to: {destMetaPath}");
                File.Copy(metaFilePath, destMetaPath, true);
            }
            else
            {
                // Create a basic meta file
                var pakModInfo = new PakModInfo
                {
                    Name = pakName,
                    Author = "Unknown",
                    Version = "1.0",
                    Description = "No description available",
                    Category = "Pak Mod"
                };

                var metaJson = JsonConvert.SerializeObject(pakModInfo, Formatting.Indented);
                var destMetaPath = Path.Combine(modsPath, $"{pakName}.pakmeta");
                LogDebug($"Creating basic meta file at: {destMetaPath}");
                await File.WriteAllTextAsync(destMetaPath, metaJson);
            }

            // Verify the installation
            if (await VerifyPakModInstalled(pakName))
            {
                LogDebug($"Pak mod {pakName} installed successfully");
            }
            else
            {
                LogDebug($"Failed to verify pak mod installation: {pakName}");
                throw new Exception($"Failed to install pak mod: {pakName}. The file was not found after installation.");
            }
        }





        private async Task InstallPakBundleMod(string bundleFilePath)
        {
            LogDebug($"Installing pakbundle mod: {bundleFilePath}");

            // Create a temporary directory to extract the bundle
            var tempDir = Path.Combine(Path.GetTempPath(), $"PakBundle_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Extract the pakbundle (it's just a zip file with a different extension)
                ZipFile.ExtractToDirectory(bundleFilePath, tempDir);

                // Check if this is actually a folder mod (has .uplugin file)
                var upluginFiles = Directory.GetFiles(tempDir, "*.uplugin", SearchOption.AllDirectories);
                if (upluginFiles.Length > 0)
                {
                    LogDebug("Found .uplugin files in the bundle, treating as a folder mod");

                    // Create a new zip file with the extracted contents
                    var tempZipPath = Path.Combine(Path.GetTempPath(), $"FolderMod_{Guid.NewGuid()}.zip");
                    ZipFile.CreateFromDirectory(tempDir, tempZipPath);

                    // Install as a folder mod
                    await InstallModFromZip(tempZipPath);

                    // Clean up
                    if (File.Exists(tempZipPath))
                    {
                        File.Delete(tempZipPath);
                    }

                    return;
                }

                // Look for .pak files (including in subdirectories)
                var pakFiles = Directory.GetFiles(tempDir, "*.pak", SearchOption.AllDirectories);
                LogDebug($"Found {pakFiles.Length} pak files in the pakbundle");

                if (pakFiles.Length == 0)
                {
                    // Log the contents of the extracted directory for debugging
                    var allFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);
                    LogDebug($"Contents of extracted pakbundle ({allFiles.Length} files):");
                    foreach (var file in allFiles)
                    {
                        LogDebug($"  - {Path.GetFileName(file)} ({new FileInfo(file).Length / 1024} KB)");
                    }

                    throw new InvalidOperationException("No .pak file found in the pakbundle.");
                }

                // Install each pak file found
                foreach (var pakFile in pakFiles)
                {
                    var pakFileName = Path.GetFileName(pakFile);
                    var pakName = Path.GetFileNameWithoutExtension(pakFile);
                    LogDebug($"Processing pak file: {pakFileName}");

                    // Look for a matching .pakmeta file in the same directory as the .pak file
                    var pakDir = Path.GetDirectoryName(pakFile);
                    var metaFile = Path.Combine(pakDir, $"{pakName}.pakmeta");
                    var hasMetaFile = File.Exists(metaFile);

                    // Create Content/Paks/~mods directory if it doesn't exist
                    var contentPath = Path.Combine(_gamePath, "BrickRigs", "Content");
                    if (!Directory.Exists(contentPath))
                    {
                        contentPath = Path.Combine(_gamePath, "Content");
                        if (!Directory.Exists(contentPath))
                        {
                            Directory.CreateDirectory(contentPath);
                        }
                    }

                    var paksPath = Path.Combine(contentPath, "Paks");
                    if (!Directory.Exists(paksPath))
                    {
                        Directory.CreateDirectory(paksPath);
                    }

                    var modsPath = Path.Combine(paksPath, "~mods");
                    if (!Directory.Exists(modsPath))
                    {
                        Directory.CreateDirectory(modsPath);
                    }

                    // Copy the pak file to the ~mods directory
                    var destPakPath = Path.Combine(modsPath, pakFileName);
                    LogDebug($"Copying pak file to: {destPakPath}");
                    File.Copy(pakFile, destPakPath, true);

                    // Copy the meta file if it exists
                    if (hasMetaFile)
                    {
                        var destMetaPath = Path.Combine(modsPath, Path.GetFileName(metaFile));
                        LogDebug($"Copying meta file to: {destMetaPath}");
                        File.Copy(metaFile, destMetaPath, true);
                    }

                    LogDebug($"Pak mod {pakName} installed successfully from bundle");
                }
            }
            finally
            {
                // Clean up the temporary directory
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Error cleaning up temporary directory: {ex.Message}");
                }
            }
        }




        public async Task<List<ModInfo>> GetInstalledMods()
        {
            var mods = new List<ModInfo>();

            if (string.IsNullOrEmpty(_modsPath) || !Directory.Exists(_modsPath))
                return mods;

            // Log for debugging
            LogDebug($"Searching for folder mods in: {_modsPath}");

            var modDirectories = Directory.GetDirectories(_modsPath);
            LogDebug($"Found {modDirectories.Length} potential mod directories");

            foreach (var modDir in modDirectories)
            {
                var modName = Path.GetFileName(modDir);
                LogDebug($"Examining mod directory: {modName}");

                // Try to find both regular and disabled uplugin files
                var upluginFiles = Directory.GetFiles(modDir, "*.uplugin", SearchOption.AllDirectories);
                var disabledUpluginFiles = Directory.GetFiles(modDir, "*.uplugin.disabled", SearchOption.AllDirectories);

                LogDebug($"Found {upluginFiles.Length} uplugin files and {disabledUpluginFiles.Length} disabled uplugin files in {modName}");

                // Check if we have any uplugin files (enabled or disabled)
                if (upluginFiles.Length > 0 || disabledUpluginFiles.Length > 0)
                {
                    // Determine if the mod is disabled based on uplugin files
                    bool hasDisabledUplugin = disabledUpluginFiles.Length > 0;

                    // Choose which uplugin file to use for metadata
                    string upluginContent = null;
                    string effectiveUpluginPath = null;

                    // First try to use an enabled uplugin file
                    if (upluginFiles.Length > 0)
                    {
                        effectiveUpluginPath = upluginFiles[0];
                        try
                        {
                            upluginContent = await File.ReadAllTextAsync(effectiveUpluginPath);
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"Error reading uplugin file {effectiveUpluginPath}: {ex.Message}");
                        }
                    }

                    // If that failed, try to use a disabled uplugin file
                    if (upluginContent == null && disabledUpluginFiles.Length > 0)
                    {
                        effectiveUpluginPath = disabledUpluginFiles[0];
                        try
                        {
                            upluginContent = await File.ReadAllTextAsync(effectiveUpluginPath);
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"Error reading disabled uplugin file {effectiveUpluginPath}: {ex.Message}");
                        }
                    }

                    // If we have uplugin content, try to parse it
                    if (upluginContent != null)
                    {
                        LogDebug($"Using uplugin file: {effectiveUpluginPath}");
                        try
                        {
                            var modInfo = JsonConvert.DeserializeObject<ModInfo>(upluginContent);

                            if (modInfo != null)
                            {
                                modInfo.ModDirectory = modDir;
                                modInfo.ModName = modName;

                                // Find all possible Paks directories
                                var paksDirs = FindAllPaksDirs(modDir);
                                LogDebug($"Found {paksDirs.Count} Paks directories in {modName}");

                                // Check if any Paks directory has .pak files
                                var hasPakFiles = paksDirs.Any(dir =>
                                    Directory.Exists(dir) &&
                                    Directory.GetFiles(dir, "*.pak").Length > 0);

                                // Check if any Paks directory has .pak.disabled files
                                var hasDisabledPakFiles = paksDirs.Any(dir =>
                                    Directory.Exists(dir) &&
                                    Directory.GetFiles(dir, "*.pak.disabled").Length > 0);

                                // Check if any Paks_Disabled directory exists
                                var disabledPaksDirs = FindAllDisabledPaksDirs(modDir);
                                var hasDisabledPaks = disabledPaksDirs.Any(dir =>
                                    Directory.Exists(dir) &&
                                    Directory.GetFiles(dir, "*.pak").Length > 0);

                                // Store the paths for later use
                                modInfo.PaksDirectories = paksDirs;
                                modInfo.DisabledPaksDirectories = disabledPaksDirs;

                                // Determine if the mod is enabled:
                                // 1. No disabled uplugin files
                                // 2. Has enabled pak files
                                // 3. No disabled pak files
                                modInfo.IsEnabled = !hasDisabledUplugin && hasPakFiles &&
                                                   !hasDisabledPakFiles && !hasDisabledPaks;

                                LogDebug($"Mod {modName} is {(modInfo.IsEnabled ? "enabled" : "disabled")}");

                                mods.Add(modInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"Error parsing uplugin for {modName}: {ex.Message}");

                            // If we can't parse the uplugin file, create a basic mod info
                            var basicModInfo = new ModInfo
                            {
                                FriendlyName = modName,
                                ModName = modName,
                                ModDirectory = modDir,
                                Description = "Unable to read mod information",
                                CreatedBy = "Unknown",
                                VersionName = "Unknown"
                            };

                            // Find all possible Paks directories
                            var paksDirs = FindAllPaksDirs(modDir);
                            var disabledPaksDirs = FindAllDisabledPaksDirs(modDir);

                            // Store the paths for later use
                            basicModInfo.PaksDirectories = paksDirs;
                            basicModInfo.DisabledPaksDirectories = disabledPaksDirs;

                            // Check if any Paks directory has .pak files
                            var hasPakFiles = paksDirs.Any(dir =>
                                Directory.Exists(dir) &&
                                Directory.GetFiles(dir, "*.pak").Length > 0);

                            // Check if any Paks directory has .pak.disabled files
                            var hasDisabledPakFiles = paksDirs.Any(dir =>
                                Directory.Exists(dir) &&
                                Directory.GetFiles(dir, "*.pak.disabled").Length > 0);

                            // Check if any Paks_Disabled directory has .pak files
                            var hasDisabledPaks = disabledPaksDirs.Any(dir =>
                                Directory.Exists(dir) &&
                                Directory.GetFiles(dir, "*.pak").Length > 0);

                            basicModInfo.IsEnabled = !hasDisabledUplugin && hasPakFiles &&
                                                    !hasDisabledPakFiles && !hasDisabledPaks;

                            mods.Add(basicModInfo);
                        }
                    }
                    else
                    {
                        // If we couldn't read any uplugin file, create a basic mod info
                        var basicModInfo = new ModInfo
                        {
                            FriendlyName = modName,
                            ModName = modName,
                            ModDirectory = modDir,
                            Description = "Unable to read mod information",
                            CreatedBy = "Unknown",
                            VersionName = "Unknown"
                        };

                        // Find all possible Paks directories
                        var paksDirs = FindAllPaksDirs(modDir);
                        var disabledPaksDirs = FindAllDisabledPaksDirs(modDir);

                        // Store the paths for later use
                        basicModInfo.PaksDirectories = paksDirs;
                        basicModInfo.DisabledPaksDirectories = disabledPaksDirs;

                        // Check if any Paks directory has .pak files
                        var hasPakFiles = paksDirs.Any(dir =>
                            Directory.Exists(dir) &&
                            Directory.GetFiles(dir, "*.pak").Length > 0);

                        // Check if any Paks directory has .pak.disabled files
                        var hasDisabledPakFiles = paksDirs.Any(dir =>
                            Directory.Exists(dir) &&
                            Directory.GetFiles(dir, "*.pak.disabled").Length > 0);

                        // Check if any Paks_Disabled directory has .pak files
                        var hasDisabledPaks = disabledPaksDirs.Any(dir =>
                            Directory.Exists(dir) &&
                            Directory.GetFiles(dir, "*.pak").Length > 0);

                        basicModInfo.IsEnabled = !hasDisabledUplugin && hasPakFiles &&
                                                !hasDisabledPakFiles && !hasDisabledPaks;

                        mods.Add(basicModInfo);
                    }
                }
                else
                {
                    // Handle mods without uplugin files (just folder with Paks)
                    LogDebug($"No uplugin file found for {modName}, checking for Paks directories");

                    var paksDirs = FindAllPaksDirs(modDir);
                    var disabledPaksDirs = FindAllDisabledPaksDirs(modDir);

                    // Check if any Paks directory has .pak files
                    var hasPakFiles = paksDirs.Any(dir =>
                        Directory.Exists(dir) &&
                        Directory.GetFiles(dir, "*.pak").Length > 0);

                    // Check if any Paks directory has .pak.disabled files
                    var hasDisabledPakFiles = paksDirs.Any(dir =>
                        Directory.Exists(dir) &&
                        Directory.GetFiles(dir, "*.pak.disabled").Length > 0);

                    // Check if any Paks_Disabled directory has .pak files
                    var hasDisabledPaks = disabledPaksDirs.Any(dir =>
                        Directory.Exists(dir) &&
                        Directory.GetFiles(dir, "*.pak").Length > 0);

                    if (hasPakFiles || hasDisabledPakFiles || hasDisabledPaks)
                    {
                        var basicModInfo = new ModInfo
                        {
                            FriendlyName = modName,
                            ModName = modName,
                            ModDirectory = modDir,
                            Description = "Pak mod without uplugin file",
                            CreatedBy = "Unknown",
                            VersionName = "Unknown",
                            PaksDirectories = paksDirs,
                            DisabledPaksDirectories = disabledPaksDirs,
                            IsEnabled = hasPakFiles && !hasDisabledPakFiles && !hasDisabledPaks
                        };

                        mods.Add(basicModInfo);
                    }
                }
            }

            // Get pak mods
            await GetPakMods(mods);

            return mods;
        }





        private List<string> FindAllPaksDirs(string modDir)
        {
            var result = new List<string>();

            // Common Paks directory patterns
            var paksPatterns = new[]
            {
                "Paks",
                "Content\\Paks",
                "Content\\Paks\\WindowsNoEditor"
            };

            foreach (var pattern in paksPatterns)
            {
                var path = Path.Combine(modDir, pattern);
                if (Directory.Exists(path))
                {
                    result.Add(path);
                }
            }

            // Also search for any directory named "Paks" recursively
            try
            {
                var allDirs = Directory.GetDirectories(modDir, "Paks", SearchOption.AllDirectories);
                foreach (var dir in allDirs)
                {
                    if (!result.Contains(dir))
                    {
                        result.Add(dir);
                    }
                }
            }
            catch
            {
                // Ignore errors in recursive search
            }

            return result;
        }

        private List<string> FindAllDisabledPaksDirs(string modDir)
        {
            var result = new List<string>();

            // Common disabled Paks directory patterns
            var disabledPatterns = new[]
            {
                "Paks_Disabled",
                "Content\\Paks_Disabled",
                "Content\\Paks_Disabled\\WindowsNoEditor"
            };

            foreach (var pattern in disabledPatterns)
            {
                var path = Path.Combine(modDir, pattern);
                if (Directory.Exists(path))
                {
                    result.Add(path);
                }
            }

            // Also search for any directory named "Paks_Disabled" recursively
            try
            {
                var allDirs = Directory.GetDirectories(modDir, "Paks_Disabled", SearchOption.AllDirectories);
                foreach (var dir in allDirs)
                {
                    if (!result.Contains(dir))
                    {
                        result.Add(dir);
                    }
                }
            }
            catch
            {
                // Ignore errors in recursive search
            }

            return result;
        }



        public async Task InstallModFromZip(string zipFilePath)
        {
            if (string.IsNullOrEmpty(_modsPath))
                throw new InvalidOperationException("Game path not set");

            LogDebug($"Installing mod from ZIP: {zipFilePath}");

            // Create a temporary directory to extract the ZIP
            var tempDir = Path.Combine(Path.GetTempPath(), $"BrickRigsMod_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Extract the ZIP file
                ZipFile.ExtractToDirectory(zipFilePath, tempDir);

                // Look for .uplugin files
                var upluginFiles = Directory.GetFiles(tempDir, "*.uplugin", SearchOption.AllDirectories);

                if (upluginFiles.Length == 0)
                {
                    // No .uplugin files found, check if it's a pakbundle
                    var pakFiles = Directory.GetFiles(tempDir, "*.pak", SearchOption.AllDirectories);

                    if (pakFiles.Length > 0)
                    {
                        // It's a pakbundle, install each pak file
                        LogDebug($"No .uplugin files found, but found {pakFiles.Length} .pak files. Installing as pakbundle.");
                        await InstallPakBundleMod(zipFilePath);
                        return;
                    }

                    // Not a recognized mod format
                    throw new InvalidOperationException("The ZIP file does not contain a recognized mod structure (no .uplugin file or standalone .pak files found).");
                }

                // Process each .uplugin file (there might be multiple mods in one ZIP)
                foreach (var upluginPath in upluginFiles)
                {
                    LogDebug($"Found .uplugin file: {upluginPath}");

                    try
                    {
                        // Read the .uplugin file to get mod info
                        var upluginContent = await File.ReadAllTextAsync(upluginPath);
                        var modInfo = JsonConvert.DeserializeObject<ModInfo>(upluginContent);

                        if (modInfo == null || string.IsNullOrEmpty(modInfo.FriendlyName))
                        {
                            throw new InvalidOperationException("Invalid .uplugin file: Could not parse mod information.");
                        }

                        // Use the mod name from the .uplugin file
                        var modName = modInfo.FriendlyName;
                        LogDebug($"Installing mod: {modName}");

                        // Get the directory containing the .uplugin file
                        var modSourceDir = Path.GetDirectoryName(upluginPath);

                        // Create the destination directory
                        var modDestDir = Path.Combine(_modsPath, modName);

                        // If the mod already exists, delete it first
                        if (Directory.Exists(modDestDir))
                        {
                            LogDebug($"Mod already exists, deleting: {modDestDir}");
                            Directory.Delete(modDestDir, true);
                        }

                        // Create the destination directory
                        Directory.CreateDirectory(modDestDir);

                        // Copy all files from the source directory to the destination
                        CopyDirectory(modSourceDir, modDestDir);

                        LogDebug($"Mod installed successfully: {modName}");
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Error installing mod from .uplugin file: {ex.Message}");
                        throw new InvalidOperationException($"Error installing mod: {ex.Message}", ex);
                    }
                }
            }
            finally
            {
                // Clean up the temporary directory
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Error cleaning up temporary directory: {ex.Message}");
                }
            }
        }

        // Helper method to copy a directory and its contents
        private void CopyDirectory(string sourceDir, string destDir)
        {
            // Create the destination directory if it doesn't exist
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Copy all files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
            }

            // Copy all subdirectories
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir);
            }
        }




        private void LogDebug(string message)
         {
             try
             {
                 var logPath = Path.Combine(Path.GetTempPath(), "BrickRigsModManager.log");
                 File.AppendAllText(logPath, $"[{DateTime.Now}] {message}\n");
             }
             catch
            {
                 // Ignore logging errors
             }
         }


        public async Task InstallModFromUrl(string url, IProgress<(int percentage, string status)> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_gamePath))
                throw new InvalidOperationException("Game path not set");

            LogDebug($"Downloading mod from URL: {url}");
            progress?.Report((0, "Preparing download..."));

            // Convert to direct download URL if needed
            string actualDownloadUrl = GetDirectDownloadUrl(url);

            LogDebug($"Using download URL: {actualDownloadUrl}");

            // Create a unique temporary file path
            string tempPath = Path.Combine(Path.GetTempPath(), $"BrickRigsMod_{Guid.NewGuid()}.tmp");

            try
            {
                // Set up HttpClient with appropriate headers to mimic a browser
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");

                // First attempt - direct download
                LogDebug("Sending HTTP request to download file");
                progress?.Report((10, "Connecting to server..."));

                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();

                HttpResponseMessage response;

                try
                {
                    response = await client.GetAsync(actualDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    // If we got a redirect, follow it manually (some services use JavaScript redirects)
                    if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                    {
                        var redirectUrl = response.Headers.Location?.ToString();
                        if (!string.IsNullOrEmpty(redirectUrl))
                        {
                            LogDebug($"Following redirect to: {redirectUrl}");
                            progress?.Report((15, "Following redirect..."));
                            response = await client.GetAsync(redirectUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        }
                    }
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("403"))
                {
                    LogDebug("Got 403 Forbidden, trying alternative approach...");
                    progress?.Report((15, "Access denied, trying alternative approach..."));

                    // For Dropbox, try a different approach
                    if (url.Contains("dropbox.com"))
                    {
                        LogDebug("Trying alternative Dropbox URL format...");
                        var altUrl = url.Replace("www.dropbox.com", "dl.dropboxusercontent.com");
                        altUrl = altUrl.Replace("?dl=0", "");
                        altUrl = altUrl.Replace("?dl=1", "");
                        altUrl += "?raw=1";

                        LogDebug($"Trying alternative URL: {altUrl}");
                        response = await client.GetAsync(altUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                    else if (url.Contains("drive.google.com"))
                    {
                        LogDebug("Trying alternative Google Drive approach...");
                        // For Google Drive, we need to handle the confirmation page
                        if (url.Contains("file/d/"))
                        {
                            int startIndex = url.IndexOf("file/d/") + 7;
                            int endIndex = url.IndexOf("/", startIndex);
                            if (endIndex == -1) endIndex = url.Length;
                            string fileId = url.Substring(startIndex, endIndex - startIndex);

                            // Try the direct link with confirm=t parameter
                            var altUrl = $"https://drive.google.com/uc?export=download&confirm=t&id={fileId}";
                            LogDebug($"Trying alternative URL: {altUrl}");
                            response = await client.GetAsync(altUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }

                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();

                // Check if we got a successful response
                if (!response.IsSuccessStatusCode)
                {
                    LogDebug($"HTTP error: {(int)response.StatusCode} {response.StatusCode}");

                    // Try to get more details about the error
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var preview = errorContent.Length > 200 ? errorContent.Substring(0, 200) : errorContent;
                    LogDebug($"Error response preview: {preview}");

                    throw new HttpRequestException($"Server returned error: {(int)response.StatusCode} {response.StatusCode}");
                }

                // Get the file name from the response or URL
                var fileName = GetFileNameFromResponse(response);
                if (string.IsNullOrEmpty(fileName))
                {
                    // If we couldn't get a filename, generate one based on the URL
                    fileName = "mod" + Path.GetExtension(url);
                    if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
                    {
                        // If there's no extension, use .bin as a temporary extension
                        fileName = "mod.bin";
                    }
                }
                LogDebug($"Using filename: {fileName}");

                // Check content type to ensure we're getting a valid file
                var contentType = response.Content.Headers.ContentType?.MediaType;
                LogDebug($"Received content type: {contentType}, filename: {fileName}");

                // Check if we got HTML instead of a binary file
                if (contentType != null && (contentType.Contains("text/html") || contentType.Contains("text/plain")))
                {
                    // Read a bit of the content to see what we got
                    var previewContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var preview = previewContent.Length > 100 ? previewContent.Substring(0, 100) : previewContent;
                    LogDebug($"Received HTML/text content instead of binary file. Preview: {preview}");

                    // Check if it's a Dropbox preview page
                    if (previewContent.Contains("Dropbox") && previewContent.Contains("download"))
                    {
                        throw new InvalidOperationException("The Dropbox link is pointing to a preview page instead of the file. Please use the 'Copy Link' option in Dropbox and ensure you're sharing a direct file link.");
                    }

                    throw new InvalidOperationException("The URL returned a web page instead of a file. Please check that your download link is correct and points directly to the file.");
                }

                // Get the file size if available
                var fileSize = response.Content.Headers.ContentLength ?? -1L;

                progress?.Report((20, $"Downloading {fileName}..."));

                LogDebug($"Saving downloaded file to: {tempPath}");

                // Download with progress reporting
                await using (var fileStream = File.Create(tempPath))
                await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    // Create a buffer for downloading
                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    var isMoreToRead = true;

                    // Download the file in chunks and report progress
                    while (isMoreToRead)
                    {
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();

                        var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (bytesRead == 0)
                        {
                            isMoreToRead = false;
                            continue;
                        }

                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);

                        totalBytesRead += bytesRead;

                        // Report progress if we know the file size
                        if (fileSize > 0)
                        {
                            var progressPercentage = (int)((totalBytesRead * 100) / fileSize);
                            var downloadedMB = totalBytesRead / (1024.0 * 1024.0);
                            var totalMB = fileSize / (1024.0 * 1024.0);

                            progress?.Report((20 + (progressPercentage * 60) / 100,
                                $"Downloading {fileName}: {downloadedMB:F1} MB / {totalMB:F1} MB ({progressPercentage}%)"));
                        }
                        else
                        {
                            // If we don't know the file size, just show how much we've downloaded
                            var downloadedMB = totalBytesRead / (1024.0 * 1024.0);
                            progress?.Report((50, $"Downloading {fileName}: {downloadedMB:F1} MB"));
                        }
                    }
                }

                // Determine file type by examining the file header
                progress?.Report((85, "Verifying download..."));

                // Read the first few bytes to determine file type
                var fileType = DetermineFileType(tempPath);
                LogDebug($"Detected file type: {fileType}");

                try
                {
                    switch (fileType)
                    {
                        case FileType.Zip:
                            // It's a ZIP file or pakbundle
                            try
                            {
                                LogDebug("Attempting to open as ZIP file");

                                using var testArchive = ZipFile.OpenRead(tempPath);
                                var entryCount = testArchive.Entries.Count;
                                LogDebug($"ZIP file is valid. Contains {entryCount} entries.");

                                // Check if it contains a .uplugin file (folder mod)
                                bool hasFolderModStructure = testArchive.Entries.Any(e => e.FullName.EndsWith(".uplugin", StringComparison.OrdinalIgnoreCase));

                                // Check if it contains .pak files directly (pakbundle)
                                bool isPakBundle = testArchive.Entries.Any(e => e.FullName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)) &&
                                                  !hasFolderModStructure;

                                LogDebug($"Is Folder Mod: {hasFolderModStructure}, Is PakBundle: {isPakBundle}");

                                if (hasFolderModStructure)
                                {
                                    // It's a folder mod
                                    progress?.Report((90, "Installing Folder mod..."));

                                    // Make sure we've closed all file handles to the temp file
                                    // Force garbage collection to release any lingering file handles
                                    testArchive.Dispose();
                                    GC.Collect();
                                    GC.WaitForPendingFinalizers();

                                    try
                                    {
                                        // Rename to .zip extension for installation
                                        var zipPath = Path.ChangeExtension(tempPath, ".zip");

                                        // Use File.Copy instead of File.Move to avoid file lock issues
                                        File.Copy(tempPath, zipPath, true);

                                        // Now we can safely delete the original file
                                        try { File.Delete(tempPath); } catch { /* Ignore errors here */ }

                                        await InstallModFromZip(zipPath);

                                        // Clean up
                                        try { File.Delete(zipPath); } catch { /* Ignore errors here */ }
                                    }
                                    catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                                    {
                                        // If we still get the file in use error, try an alternative approach
                                        LogDebug($"File in use error: {ex.Message}. Trying alternative approach.");

                                        // Create a new temporary file with a different name
                                        var altZipPath = Path.Combine(Path.GetTempPath(), $"BrickRigsMod_Alt_{Guid.NewGuid()}.zip");

                                        // Use File.Copy which might work even if the original file handle is still open
                                        File.Copy(tempPath, altZipPath, true);

                                        await InstallModFromZip(altZipPath);

                                        // Clean up
                                        try { File.Delete(altZipPath); } catch { /* Ignore errors here */ }
                                    }
                                }
                                else if (isPakBundle)
                                {
                                    // It's a pakbundle
                                    progress?.Report((90, "Installing PakBundle mod..."));
                                    await InstallPakBundleMod(tempPath);
                                }
                                else
                                {
                                    // It's a regular ZIP file, but not a recognized mod format
                                    throw new InvalidOperationException("The ZIP file does not contain a recognized mod structure (no .uplugin file or standalone .pak files found).");
                                }
                            }
                            catch (InvalidDataException ex)
                            {
                                LogDebug($"Error verifying ZIP file: {ex.Message}");
                                throw new InvalidOperationException("The downloaded file is not a valid ZIP archive. The download may have been corrupted.");
                            }
                            break;

                        case FileType.Pak:
                            // It's a PAK file
                            progress?.Report((90, "Installing PAK mod..."));

                            // Rename to .pak extension for installation
                            var pakPath = Path.ChangeExtension(tempPath, ".pak");
                            File.Move(tempPath, pakPath, true);

                            await InstallPakMod(pakPath);

                            // Clean up
                            if (File.Exists(pakPath))
                            {
                                File.Delete(pakPath);
                            }
                            break;

                        default:
                            // If we can't determine the file type, try to guess based on content
                            LogDebug("Unknown file type, attempting to determine by content");

                            // Try to open as ZIP
                            try
                            {
                                using var testArchive = ZipFile.OpenRead(tempPath);
                                // If we get here, it's a ZIP file
                                LogDebug("File opened successfully as ZIP");

                                // Check if it contains a .uplugin file (folder mod)
                                bool hasFolderModStructure = testArchive.Entries.Any(e => e.FullName.EndsWith(".uplugin", StringComparison.OrdinalIgnoreCase));

                                // Check if it contains .pak files directly (pakbundle)
                                bool isPakBundle = testArchive.Entries.Any(e => e.FullName.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)) &&
                                                  !hasFolderModStructure;

                                LogDebug($"Is Folder Mod: {hasFolderModStructure}, Is PakBundle: {isPakBundle}");

                                if (hasFolderModStructure)
                                {
                                    // It's a folder mod
                                    progress?.Report((90, "Installing Folder mod..."));

                                    // Make sure we've closed all file handles to the temp file
                                    // Force garbage collection to release any lingering file handles
                                    testArchive.Dispose();
                                    GC.Collect();
                                    GC.WaitForPendingFinalizers();

                                    try
                                    {
                                        // Rename to .zip extension for installation
                                        var zipPath = Path.ChangeExtension(tempPath, ".zip");

                                        // Use File.Copy instead of File.Move to avoid file lock issues
                                        File.Copy(tempPath, zipPath, true);

                                        // Now we can safely delete the original file
                                        try { File.Delete(tempPath); } catch { /* Ignore errors here */ }

                                        await InstallModFromZip(zipPath);

                                        // Clean up
                                        try { File.Delete(zipPath); } catch { /* Ignore errors here */ }
                                    }
                                    catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                                    {
                                        // If we still get the file in use error, try an alternative approach
                                        LogDebug($"File in use error: {ex.Message}. Trying alternative approach.");

                                        // Create a new temporary file with a different name
                                        var altZipPath = Path.Combine(Path.GetTempPath(), $"BrickRigsMod_Alt_{Guid.NewGuid()}.zip");

                                        // Use File.Copy which might work even if the original file handle is still open
                                        File.Copy(tempPath, altZipPath, true);

                                        await InstallModFromZip(altZipPath);

                                        // Clean up
                                        try { File.Delete(altZipPath); } catch { /* Ignore errors here */ }
                                    }
                                }
                                else if (isPakBundle)
                                {
                                    // It's a pakbundle
                                    progress?.Report((90, "Installing PakBundle mod..."));
                                    await InstallPakBundleMod(tempPath);
                                }
                                else
                                {
                                    // It's a regular ZIP file, but not a recognized mod format
                                    throw new InvalidOperationException("The ZIP file does not contain a recognized mod structure (no .uplugin file or standalone .pak files found).");
                                }
                            }
                            catch
                            {
                                // Not a ZIP file, try as PAK
                                try
                                {
                                    // Assume it's a PAK file
                                    progress?.Report((90, "Attempting to install as PAK mod..."));

                                    // Rename to .pak extension for installation
                                    var fallbackPakPath = Path.ChangeExtension(tempPath, ".pak");
                                    File.Move(tempPath, fallbackPakPath, true);

                                    await InstallPakMod(fallbackPakPath);

                                    // Clean up
                                    if (File.Exists(fallbackPakPath))
                                    {
                                        File.Delete(fallbackPakPath);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogDebug($"Failed to install as PAK: {ex.Message}");
                                    throw new InvalidOperationException("Unsupported file type. Only .zip, .pakbundle, and .pak files are supported.");
                                }
                            }
                            break;
                    }

                    progress?.Report((100, "Installation complete!"));
                }
                finally
                {
                    // Clean up the temporary file if it still exists
                    if (File.Exists(tempPath))
                    {
                        try
                        {
                            File.Delete(tempPath);
                            LogDebug("Temporary file deleted");
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"Failed to delete temporary file: {ex.Message}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogDebug("Download was canceled by the user");
                progress?.Report((0, "Download canceled"));

                // Clean up the temporary file if it exists
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                        LogDebug("Temporary file deleted after cancellation");
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"Failed to delete temporary file after cancellation: {ex.Message}");
                    }
                }

                throw;
            }
            catch (HttpRequestException ex)
            {
                LogDebug($"HTTP error downloading mod: {ex.Message}");
                if (ex.InnerException != null)
                {
                    LogDebug($"Inner exception: {ex.InnerException.Message}");
                }
                progress?.Report((0, "Download failed"));

                // Clean up the temporary file if it exists
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                        LogDebug("Temporary file deleted after error");
                    }
                    catch (Exception cleanupEx)
                    {
                        LogDebug($"Failed to delete temporary file after error: {cleanupEx.Message}");
                    }
                }

                throw new Exception($"Failed to download mod: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                LogDebug($"Error downloading mod: {ex.Message}");
                if (ex.InnerException != null)
                {
                    LogDebug($"Inner exception: {ex.InnerException.Message}");
                }
                progress?.Report((0, "Download failed"));

                // Clean up the temporary file if it exists
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                        LogDebug("Temporary file deleted after error");
                    }
                    catch (Exception cleanupEx)
                    {
                        LogDebug($"Failed to delete temporary file after error: {cleanupEx.Message}");
                    }
                }

                throw;
            }
        }



        private string GetFileNameFromResponse(HttpResponseMessage response)
        {
            // Try to get filename from Content-Disposition header
            if (response.Content.Headers.ContentDisposition != null)
            {
                var fileName = response.Content.Headers.ContentDisposition.FileName;
                if (!string.IsNullOrEmpty(fileName))
                {
                    // Remove quotes if present
                    fileName = fileName.Trim('"', '\'');
                    LogDebug($"Got filename from Content-Disposition: {fileName}");
                    return fileName;
                }
            }

            // Try to get from the URL
            var uri = response.RequestMessage?.RequestUri;
            if (uri != null)
            {
                var path = uri.AbsolutePath;
                var fileName = Path.GetFileName(path);

                // Remove query parameters if present
                int queryIndex = fileName.IndexOf('?');
                if (queryIndex > 0)
                {
                    fileName = fileName.Substring(0, queryIndex);
                }

                if (!string.IsNullOrEmpty(fileName) && fileName != "/")
                {
                    LogDebug($"Got filename from URL: {fileName}");
                    return fileName;
                }
            }

            // For Google Drive API links, try to extract the file ID and use that
            var url = response.RequestMessage?.RequestUri?.ToString();
            if (url != null && url.Contains("googleapis.com/drive/v3/files/"))
            {
                // Extract file ID
                int startIndex = url.IndexOf("/files/") + 7;
                int endIndex = url.IndexOf("?", startIndex);
                if (endIndex == -1) endIndex = url.Length;

                string fileId = url.Substring(startIndex, endIndex - startIndex);
                LogDebug($"Extracted Google Drive file ID: {fileId}");

                // Try to get a content type hint
                var contentType = response.Content.Headers.ContentType?.MediaType;
                string extension = ".bin";

                if (!string.IsNullOrEmpty(contentType))
                {
                    switch (contentType.ToLowerInvariant())
                    {
                        case "application/zip":
                            extension = ".zip";
                            break;
                        case "application/x-zip-compressed":
                            extension = ".zip";
                            break;
                        case "application/octet-stream":
                            // Try to determine from the first few bytes
                            break;
                    }
                }

                return $"gdrive_{fileId}{extension}";
            }

            // If all else fails, return null and let the caller decide
            return null;
        }

        private string GetDirectDownloadUrl(string url)
        {
            // Initialize the actual download URL
            string actualDownloadUrl = url;

            // If it's already a Google API direct download link, leave it as is
            if (url.Contains("googleapis.com/drive/v3/files/") && url.Contains("alt=media"))
            {
                LogDebug("Detected Google Drive API direct download link, using as is");
                return url;
            }

            // Special handling for battlemod.net links
            if (url.Contains("battlemod.net"))
            {
                LogDebug("Detected battlemod.net URL, using as is");
                // For now, we'll try to use the URL as-is
            }
            // Handle Dropbox links
            else if (url.Contains("dropbox.com"))
            {
                LogDebug("Detected Dropbox URL, converting to direct download link");

                // For Dropbox, we need to handle several URL formats

                // First, check if it's already a direct download link
                if (url.Contains("dl.dropboxusercontent.com"))
                {
                    // Already a direct link, just ensure it has the raw=1 parameter
                    var uri = new Uri(url);
                    var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
                    actualDownloadUrl = baseUrl + "?raw=1";
                }
                // Handle dropbox.com/s/ links (individual file shares)
                else if (url.Contains("dropbox.com/s/"))
                {
                    // Convert to dl.dropboxusercontent.com format
                    actualDownloadUrl = url.Replace("www.dropbox.com/s/", "dl.dropboxusercontent.com/s/");

                    // Remove any existing parameters
                    int queryIndex = actualDownloadUrl.IndexOf('?');
                    if (queryIndex > 0)
                    {
                        actualDownloadUrl = actualDownloadUrl.Substring(0, queryIndex);
                    }

                    // Add raw=1 parameter
                    actualDownloadUrl += "?raw=1";
                }
                // Handle standard dropbox.com/... links
                else
                {
                    // Try to extract the path and convert to direct download
                    var uri = new Uri(url);
                    var path = uri.AbsolutePath;

                    // Check if it's a preview link
                    if (path.Contains("/view/"))
                    {
                        path = path.Replace("/view/", "/raw/");
                    }

                    // Build direct download URL
                    actualDownloadUrl = $"https://dl.dropboxusercontent.com{path}?raw=1";
                }

                LogDebug($"Converted Dropbox URL to: {actualDownloadUrl}");
            }
            // Handle regular Google Drive links (not API links)
            else if (url.Contains("drive.google.com") && !url.Contains("googleapis.com"))
            {
                LogDebug("Detected Google Drive URL, converting to direct download link");

                if (url.Contains("file/d/"))
                {
                    // Extract file ID
                    int startIndex = url.IndexOf("file/d/") + 7;
                    int endIndex = url.IndexOf("/", startIndex);
                    if (endIndex == -1) endIndex = url.Length;
                    string fileId = url.Substring(startIndex, endIndex - startIndex);

                    // Create direct download link
                    actualDownloadUrl = $"https://drive.google.com/uc?export=download&id={fileId}";
                }
                else if (url.Contains("id="))
                {
                    // Already in correct format or close to it
                    if (!url.Contains("export=download"))
                    {
                        actualDownloadUrl = url + (url.Contains("?") ? "&" : "?") + "export=download";
                    }
                }

                LogDebug($"Converted Google Drive URL to: {actualDownloadUrl}");
            }

            return actualDownloadUrl;
        }

        // Add this enum to help with file type detection
        private enum FileType
        {
            Unknown,
            Zip,
            Pak
        }

        // Add this method to detect file types based on file headers
        private FileType DetermineFileType(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var header = new byte[4];
                if (fs.Read(header, 0, 4) < 4)
                {
                    return FileType.Unknown;
                }

                // Check for ZIP header (PK..)
                if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                {
                    LogDebug("File has ZIP header signature");
                    return FileType.Zip;
                }

                // Log the header bytes for debugging
                var headerHex = BitConverter.ToString(header);
                LogDebug($"File header: {headerHex}");

                // For PAK files, we'll assume any binary file that's not a ZIP is a PAK
                // This is a simplification, but should work for most cases
                return FileType.Pak;
            }
            catch (Exception ex)
            {
                LogDebug($"Error determining file type: {ex.Message}");
                return FileType.Unknown;
            }
        }


        // Add this enum to help with file type detection













        public async Task ToggleModEnabled(ModInfo mod)
        {
            if (string.IsNullOrEmpty(mod.ModDirectory) || !Directory.Exists(mod.ModDirectory))
            {
                throw new DirectoryNotFoundException($"Mod directory not found: {mod.ModDirectory}");
            }

            LogDebug($"Toggling mod {mod.FriendlyName} from {(mod.IsEnabled ? "enabled" : "disabled")} to {(mod.IsEnabled ? "disabled" : "enabled")}");

            try
            {
                if (mod.IsPakMod)
                {
                    // Handle pak mod enabling/disabling (existing code)
                    if (mod.IsEnabled)
                    {
                        // Disable pak mod: Rename .pak to .pak.disabled
                        var pakFile = Directory.GetFiles(mod.ModDirectory, $"{mod.ModName}.pak").FirstOrDefault();
                        if (pakFile != null && File.Exists(pakFile))
                        {
                            var disabledPath = pakFile + ".disabled";
                            LogDebug($"Disabling pak mod: Renaming {pakFile} to {disabledPath}");

                            // Delete destination file if it exists
                            if (File.Exists(disabledPath))
                            {
                                File.Delete(disabledPath);
                            }

                            File.Move(pakFile, disabledPath);
                        }
                        else
                        {
                            throw new FileNotFoundException($"PAK file not found for mod: {mod.ModName}");
                        }
                    }
                    else
                    {
                        // Enable pak mod: Rename .pak.disabled to .pak
                        var disabledPakFile = Directory.GetFiles(mod.ModDirectory, $"{mod.ModName}.pak.disabled").FirstOrDefault();
                        if (disabledPakFile != null && File.Exists(disabledPakFile))
                        {
                            var enabledPath = disabledPakFile.Substring(0, disabledPakFile.Length - ".disabled".Length);
                            LogDebug($"Enabling pak mod: Renaming {disabledPakFile} to {enabledPath}");

                            // Delete destination file if it exists
                            if (File.Exists(enabledPath))
                            {
                                File.Delete(enabledPath);
                            }

                            File.Move(disabledPakFile, enabledPath);
                        }
                        else
                        {
                            throw new FileNotFoundException($"Disabled PAK file not found for mod: {mod.ModName}");
                        }
                    }
                }
                else
                {
                    // Handle folder mod enabling/disabling
                    if (mod.IsEnabled)
                    {
                        // Disable mod: Rename .pak files to .pak.disabled
                        foreach (var paksDir in mod.PaksDirectories)
                        {
                            if (Directory.Exists(paksDir))
                            {
                                var pakFiles = Directory.GetFiles(paksDir, "*.pak");
                                LogDebug($"Found {pakFiles.Length} pak files to disable in {paksDir}");

                                foreach (var pakFile in pakFiles)
                                {
                                    var disabledPath = pakFile + ".disabled";
                                    LogDebug($"Disabling: Renaming {pakFile} to {disabledPath}");

                                    // Delete destination file if it exists
                                    if (File.Exists(disabledPath))
                                    {
                                        File.Delete(disabledPath);
                                    }

                                    File.Move(pakFile, disabledPath);
                                }
                            }
                        }

                        // Also disable the .uplugin file by renaming it to .uplugin.disabled
                        var upluginFiles = Directory.GetFiles(mod.ModDirectory, "*.uplugin", SearchOption.AllDirectories);
                        foreach (var upluginFile in upluginFiles)
                        {
                            var disabledPath = upluginFile + ".disabled";
                            LogDebug($"Disabling: Renaming {upluginFile} to {disabledPath}");

                            // Delete destination file if it exists
                            if (File.Exists(disabledPath))
                            {
                                File.Delete(disabledPath);
                            }

                            File.Move(upluginFile, disabledPath);
                        }
                    }
                    else
                    {
                        // Enable mod: First, look for and enable .uplugin.disabled files
                        var disabledUpluginFiles = Directory.GetFiles(mod.ModDirectory, "*.uplugin.disabled", SearchOption.AllDirectories);
                        foreach (var disabledUpluginFile in disabledUpluginFiles)
                        {
                            var enabledPath = disabledUpluginFile.Substring(0, disabledUpluginFile.Length - ".disabled".Length);
                            LogDebug($"Enabling: Renaming {disabledUpluginFile} to {enabledPath}");

                            // Delete destination file if it exists
                            if (File.Exists(enabledPath))
                            {
                                File.Delete(enabledPath);
                            }

                            File.Move(disabledUpluginFile, enabledPath);
                        }

                        // Then enable .pak.disabled files
                        bool foundDisabledFiles = false;

                        // First check in Paks directories for .pak.disabled files
                        foreach (var paksDir in mod.PaksDirectories)
                        {
                            if (Directory.Exists(paksDir))
                            {
                                var disabledPakFiles = Directory.GetFiles(paksDir, "*.pak.disabled");
                                LogDebug($"Found {disabledPakFiles.Length} disabled pak files in {paksDir}");

                                foreach (var disabledPakFile in disabledPakFiles)
                                {
                                    var enabledPath = disabledPakFile.Substring(0, disabledPakFile.Length - ".disabled".Length);
                                    LogDebug($"Enabling: Renaming {disabledPakFile} to {enabledPath}");

                                    // Delete destination file if it exists
                                    if (File.Exists(enabledPath))
                                    {
                                        File.Delete(enabledPath);
                                    }

                                    File.Move(disabledPakFile, enabledPath);
                                    foundDisabledFiles = true;
                                }
                            }
                        }

                        // If we didn't find any .pak.disabled files in Paks directories,
                        // check in Paks_Disabled directories for .pak files and move them to Paks
                        if (!foundDisabledFiles)
                        {
                            foreach (var disabledDir in mod.DisabledPaksDirectories)
                            {
                                if (Directory.Exists(disabledDir))
                                {
                                    var pakFiles = Directory.GetFiles(disabledDir, "*.pak");
                                    LogDebug($"Found {pakFiles.Length} pak files in {disabledDir}");

                                    if (pakFiles.Length > 0)
                                    {
                                        // Create corresponding enabled directory
                                        var enabledDir = disabledDir.Replace("Paks_Disabled", "Paks");

                                        if (!Directory.Exists(enabledDir))
                                        {
                                            Directory.CreateDirectory(enabledDir);
                                        }

                                        foreach (var pakFile in pakFiles)
                                        {
                                            var fileName = Path.GetFileName(pakFile);
                                            var destFile = Path.Combine(enabledDir, fileName);
                                            LogDebug($"Moving {pakFile} to {destFile}");

                                            // Delete destination file if it exists
                                            if (File.Exists(destFile))
                                            {
                                                File.Delete(destFile);
                                            }

                                            File.Move(pakFile, destFile);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Update the mod's enabled status
                mod.IsEnabled = !mod.IsEnabled;
            }
            catch (Exception ex)
            {
                LogDebug($"Error toggling mod: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Failed to {(mod.IsEnabled ? "disable" : "enable")} mod: {ex.Message}", ex);
            }
        }







        public async Task DeleteMod(ModInfo mod)
        {
            if (string.IsNullOrEmpty(mod.ModDirectory) || !Directory.Exists(mod.ModDirectory))
            {
                throw new DirectoryNotFoundException($"Mod directory not found: {mod.ModDirectory}");
            }

            LogDebug($"Deleting mod: {mod.FriendlyName}");

            try
            {
                if (mod.IsPakMod)
                {
                    // Handle pak mod deletion
                    // First, look for the .pak file
                    var pakFile = Directory.GetFiles(mod.ModDirectory, $"{mod.ModName}.pak").FirstOrDefault();
                    var disabledPakFile = Directory.GetFiles(mod.ModDirectory, $"{mod.ModName}.pak.disabled").FirstOrDefault();
                    var metaFile = Path.Combine(mod.ModDirectory, $"{mod.ModName}.pakmeta");

                    // Delete the pak file if it exists
                    if (pakFile != null && File.Exists(pakFile))
                    {
                        LogDebug($"Deleting pak file: {pakFile}");
                        File.Delete(pakFile);
                    }

                    // Delete the disabled pak file if it exists
                    if (disabledPakFile != null && File.Exists(disabledPakFile))
                    {
                        LogDebug($"Deleting disabled pak file: {disabledPakFile}");
                        File.Delete(disabledPakFile);
                    }

                    // Delete the meta file if it exists
                    if (File.Exists(metaFile))
                    {
                        LogDebug($"Deleting meta file: {metaFile}");
                        File.Delete(metaFile);
                    }

                    LogDebug($"Pak mod {mod.ModName} deleted successfully");
                }
                else
                {
                    // Handle folder mod deletion
                    LogDebug($"Deleting folder mod directory: {mod.ModDirectory}");
                    Directory.Delete(mod.ModDirectory, true);
                    LogDebug($"Folder mod {mod.ModName} deleted successfully");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error deleting mod: {ex.Message}\n{ex.StackTrace}");
                throw new Exception($"Failed to delete mod: {ex.Message}", ex);
            }
        }


        public async Task<bool> VerifyPakModInstalled(string pakName)
        {
            var possiblePakPaths = new[]
            {
        Path.Combine(_gamePath, "BrickRigs", "Content", "Paks", "~mods"),
        Path.Combine(_gamePath, "Content", "Paks", "~mods"),
        Path.Combine(_gamePath, "BrickRigs", "Content", "~mods"),
        Path.Combine(_gamePath, "Content", "~mods")
    };

            foreach (var pakPath in possiblePakPaths)
            {
                if (!Directory.Exists(pakPath))
                    continue;

                var pakFile = Path.Combine(pakPath, $"{pakName}.pak");
                if (File.Exists(pakFile))
                {
                    LogDebug($"Verified pak mod exists: {pakFile}");
                    return true;
                }
            }

            LogDebug($"Could not find pak mod: {pakName}");
            return false;
        }


        private async Task GetPakMods(List<ModInfo> mods)
        {
            // Check for pak mods in the Content/Paks/~mods directory
            var possiblePakPaths = new[]
            {
        Path.Combine(_gamePath, "BrickRigs", "Content", "Paks", "~mods"),
        Path.Combine(_gamePath, "Content", "Paks", "~mods")
    };

            foreach (var pakPath in possiblePakPaths)
            {
                LogDebug($"Checking for pak mods in: {pakPath}");

                if (!Directory.Exists(pakPath))
                {
                    LogDebug($"Directory does not exist: {pakPath}");
                    continue;
                }

                // Check for enabled pak files (*.pak)
                var pakFiles = Directory.GetFiles(pakPath, "*.pak");
                LogDebug($"Found {pakFiles.Length} pak files in {pakPath}");

                // Also check for disabled pak files (*.pak.disabled)
                var disabledPakFiles = Directory.GetFiles(pakPath, "*.pak.disabled");
                LogDebug($"Found {disabledPakFiles.Length} disabled pak files in {pakPath}");

                // Process enabled pak files
                foreach (var pakFile in pakFiles)
                {
                    await ProcessPakFile(pakFile, true, mods);
                }

                // Process disabled pak files
                foreach (var disabledPakFile in disabledPakFiles)
                {
                    await ProcessPakFile(disabledPakFile, false, mods);
                }
            }
        }

        private async Task ProcessPakFile(string pakFile, bool isEnabled, List<ModInfo> mods)
        {
            var pakPath = Path.GetDirectoryName(pakFile);
            var pakFileName = Path.GetFileName(pakFile);
            var pakName = Path.GetFileNameWithoutExtension(pakFile);

            // If it's a disabled file, remove the .disabled extension for the name
            if (!isEnabled)
            {
                pakName = Path.GetFileNameWithoutExtension(pakName);
            }

            LogDebug($"Processing {(isEnabled ? "enabled" : "disabled")} pak file: {pakName}");

            var metaFile = Path.Combine(pakPath, $"{pakName}.pakmeta");

            PakModInfo pakModInfo = null;

            // Check if there's a metadata file
            if (File.Exists(metaFile))
            {
                LogDebug($"Found metadata file for {pakName}");
                try
                {
                    var metaContent = await File.ReadAllTextAsync(metaFile);
                    pakModInfo = JsonConvert.DeserializeObject<PakModInfo>(metaContent);

                    if (pakModInfo != null)
                    {
                        pakModInfo.PakFilePath = pakFile;
                        pakModInfo.MetaFilePath = metaFile;
                        pakModInfo.IsEnabled = isEnabled;

                        // Convert to ModInfo and add to the list
                        var modInfo = new ModInfo
                        {
                            FriendlyName = pakModInfo.Name ?? pakName,
                            ModName = pakName,
                            ModDirectory = pakPath,
                            Description = pakModInfo.Description ?? "No description available",
                            CreatedBy = pakModInfo.Author ?? "Unknown",
                            VersionName = pakModInfo.Version ?? "1.0",
                            IsEnabled = isEnabled,
                            IsPakMod = true,
                            DisabledPakPath = isEnabled ? pakFile + ".disabled" : pakFile
                        };

                        mods.Add(modInfo);
                        LogDebug($"Added pak mod with metadata: {modInfo.FriendlyName} (Enabled: {isEnabled})");
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Error parsing pakmeta for {pakName}: {ex.Message}");
                }
            }

            // If we couldn't get info from the metadata file, create basic info
            if (pakModInfo == null)
            {
                LogDebug($"Creating basic info for pak mod: {pakName}");
                var basicModInfo = new ModInfo
                {
                    FriendlyName = pakName,
                    ModName = pakName,
                    ModDirectory = pakPath,
                    Description = "Pak mod without metadata",
                    CreatedBy = "Unknown",
                    VersionName = "Unknown",
                    IsEnabled = isEnabled,
                    IsPakMod = true,
                    DisabledPakPath = isEnabled ? pakFile + ".disabled" : pakFile
                };

                mods.Add(basicModInfo);
                LogDebug($"Added pak mod without metadata: {basicModInfo.FriendlyName} (Enabled: {isEnabled})");
            }
        }


        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
