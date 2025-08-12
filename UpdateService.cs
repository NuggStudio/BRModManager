// UpdateService.cs
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BrickRigsModManager
{
    public class UpdateService
    {
        private readonly string _updateCheckUrl;
        private readonly string _currentVersion;
        private readonly Action<string> _logAction;

        public UpdateService(string updateCheckUrl, string currentVersion, Action<string> logAction = null)
        {
            _updateCheckUrl = updateCheckUrl;
            _currentVersion = currentVersion;
            _logAction = logAction;
        }

        private void LogDebug(string message)
        {
            _logAction?.Invoke(message);
        }

        public async Task<ModManagerVersionInfo> CheckForUpdates()
        {
            try
            {
                LogDebug($"Checking for updates at: {_updateCheckUrl}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "BrickRigsModManager");
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetStringAsync(_updateCheckUrl);
                var versionInfo = JsonConvert.DeserializeObject<ModManagerVersionInfo>(response);

                if (versionInfo == null)
                {
                    LogDebug("Failed to parse version info JSON");
                    return null;
                }

                LogDebug($"Latest version: {versionInfo.Version}, Current version: {_currentVersion}");

                // Compare versions
                if (IsNewerVersion(versionInfo.Version, _currentVersion))
                {
                    LogDebug("Update available");
                    return versionInfo;
                }
                else
                {
                    LogDebug("No update available");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        private bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                // Remove any "V" prefix
                if (latestVersion.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                    latestVersion = latestVersion.Substring(1);

                if (currentVersion.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                    currentVersion = currentVersion.Substring(1);

                // Remove any "AT" suffix
                if (currentVersion.EndsWith("AT", StringComparison.OrdinalIgnoreCase))
                    currentVersion = currentVersion.Substring(0, currentVersion.Length - 2);

                // Parse versions
                var latest = Version.Parse(latestVersion);
                var current = Version.Parse(currentVersion);

                return latest > current;
            }
            catch (Exception ex)
            {
                LogDebug($"Error comparing versions: {ex.Message}");
                return false;
            }
        }

        public async Task<string> DownloadUpdate(ModManagerVersionInfo versionInfo, string destinationPath, IProgress<(int percentage, string status)> progress = null)
        {
            try
            {
                // Use direct download URL if available, otherwise use regular download URL
                var downloadUrl = !string.IsNullOrEmpty(versionInfo.DirectDownloadUrl)
                    ? versionInfo.DirectDownloadUrl
                    : versionInfo.DownloadUrl;

                LogDebug($"Downloading update from: {downloadUrl}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "BrickRigsModManager");

                // Create a temporary file path
                var tempFilePath = Path.Combine(
                    Path.GetTempPath(),
                    $"BrickRigsModManager_Update_{versionInfo.Version}_{Guid.NewGuid()}.zip");

                LogDebug($"Downloading to temporary file: {tempFilePath}");

                // Download the file with progress reporting
                progress?.Report((0, "Connecting to server..."));

                var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                progress?.Report((10, "Starting download..."));

                using (var fileStream = File.Create(tempFilePath))
                using (var downloadStream = await response.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[8192];
                    var bytesRead = 0;
                    var totalBytesRead = 0L;

                    while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);

                        totalBytesRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progressPercentage = (int)((totalBytesRead * 90) / totalBytes);
                            var downloadedMB = totalBytesRead / (1024.0 * 1024.0);
                            var totalMB = totalBytes / (1024.0 * 1024.0);

                            progress?.Report((10 + progressPercentage,
                                $"Downloading update: {downloadedMB:F1} MB / {totalMB:F1} MB ({progressPercentage}%)"));
                        }
                        else
                        {
                            var downloadedMB = totalBytesRead / (1024.0 * 1024.0);
                            progress?.Report((50, $"Downloading update: {downloadedMB:F1} MB"));
                        }
                    }
                }

                progress?.Report((100, "Download complete"));

                return tempFilePath;
            }
            catch (Exception ex)
            {
                LogDebug($"Error downloading update: {ex.Message}");
                throw new Exception($"Failed to download update: {ex.Message}", ex);
            }
        }

        public async Task InstallUpdate(string updateFilePath, string destinationPath, IProgress<(int percentage, string status)> progress = null)
        {
            try
            {
                LogDebug($"Installing update from {updateFilePath} to {destinationPath}");

                progress?.Report((0, "Preparing to install update..."));

                // Create a temporary directory to extract the update
                var tempDir = Path.Combine(Path.GetTempPath(), $"BrickRigsModManager_Update_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Extract the update
                    progress?.Report((20, "Extracting update files..."));
                    LogDebug($"Extracting update to {tempDir}");

                    ZipFile.ExtractToDirectory(updateFilePath, tempDir);

                    // Get the current executable path
                    var currentExePath = Process.GetCurrentProcess().MainModule.FileName;
                    var currentDir = Path.GetDirectoryName(currentExePath);
                    var exeName = Path.GetFileName(currentExePath);

                    LogDebug($"Current executable: {currentExePath}");
                    LogDebug($"Current directory: {currentDir}");

                    // Define Program Files installation path
                    var programFilesPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        "BrickRigsModManager");

                    // Define shortcut locations
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    var startMenuPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                        "Programs");

                    // Create a batch file to copy files and restart the application
                    progress?.Report((50, "Creating installation script..."));

                    var batchFilePath = Path.Combine(Path.GetTempPath(), $"BrickRigsModManager_Update_{Guid.NewGuid()}.bat");

                    // Create a VBS script to create shortcuts
                    var vbsScriptPath = Path.Combine(Path.GetTempPath(), $"CreateShortcuts_{Guid.NewGuid()}.vbs");
                    var vbsContent = new StringBuilder();
                    vbsContent.AppendLine("Set WshShell = WScript.CreateObject(\"WScript.Shell\")");

                    // Desktop shortcut
                    

                    // Start Menu shortcut
                    

                    // Write the VBS script
                    await File.WriteAllTextAsync(vbsScriptPath, vbsContent.ToString());

                    // Create the batch file content
                    var batchContent = new StringBuilder();
                    batchContent.AppendLine("@echo off");
                    batchContent.AppendLine("echo Installing Brick Rigs Mod Manager...");
                    batchContent.AppendLine("timeout /t 2 /nobreak > nul");

                    // Create Program Files directory if it doesn't exist
                    batchContent.AppendLine($"if not exist \"{programFilesPath}\" mkdir \"{programFilesPath}\"");

                    // Copy all files from the temp directory to Program Files
                    batchContent.AppendLine($"xcopy \"{tempDir}\\*\" \"{programFilesPath}\" /E /Y /Q");

                    // Create shortcuts using the VBS script
                    batchContent.AppendLine("echo Creating shortcuts...");
                    batchContent.AppendLine($"cscript //nologo \"{vbsScriptPath}\"");

                    // Delete the VBS script
                    batchContent.AppendLine($"del \"{vbsScriptPath}\"");

                    // Delete the temporary directory
                    batchContent.AppendLine($"rmdir /S /Q \"{tempDir}\"");

                    // Delete the update file
                    batchContent.AppendLine($"del \"{updateFilePath}\"");

                    // Start the application from Program Files
                    batchContent.AppendLine($"start \"\" \"{Path.Combine(programFilesPath, exeName)}\"");

                    // Delete the batch file itself
                    batchContent.AppendLine("del \"%~f0\"");

                    // Write the batch file
                    await File.WriteAllTextAsync(batchFilePath, batchContent.ToString());

                    LogDebug($"Created installation script: {batchFilePath}");

                    // Execute the batch file with admin privileges
                    progress?.Report((90, "Launching installation process..."));

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = batchFilePath,
                        CreateNoWindow = false,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal,
                        Verb = "runas" // Request admin privileges
                    };

                    Process.Start(startInfo);

                    // Exit the application
                    progress?.Report((100, "Update ready. Restarting application..."));
                    LogDebug("Installation process started. Exiting application.");

                    // Give the user a moment to see the message
                    await Task.Delay(2000);

                    // Exit the application
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    LogDebug($"Error during update installation: {ex.Message}");

                    // Clean up
                    if (Directory.Exists(tempDir))
                    {
                        try { Directory.Delete(tempDir, true); } catch { }
                    }

                    throw;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error installing update: {ex.Message}");
                throw new Exception($"Failed to install update: {ex.Message}", ex);
            }
        }


    }
}
