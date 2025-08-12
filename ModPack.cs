using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace BrickRigsModManager
{
    public class ModPack
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<ModPackEntry> Mods { get; set; } = new List<ModPackEntry>();

        [JsonIgnore]
        public string ThumbnailPath { get; set; }

        public class ModPackEntry
        {
            public string Name { get; set; }
            public string Type { get; set; } // "Folder", "Pak", "PakBundle"
            public string RelativePath { get; set; }
            public string Version { get; set; }
            public string Author { get; set; }

            [JsonIgnore] // Don't include this in the JSON serialization
            public string Path { get; set; } // The actual file path
        }

        public static async Task<ModPack> CreateFromPathsAsync(string name, string author, string version, string description, List<string> modPaths, string thumbnailPath = null)
        {
            var modPack = new ModPack
            {
                Name = name,
                Author = author,
                Version = version,
                Description = description,
                CreatedDate = DateTime.Now,
                ThumbnailPath = thumbnailPath
            };

            System.Diagnostics.Debug.WriteLine($"Creating mod pack: {name} with {modPaths.Count} mods");

            foreach (var path in modPaths)
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Path does not exist: {path}");
                    continue;
                }

                string modName = Path.GetFileName(path);
                string modType = DetermineModType(path);
                string modAuthor = "Unknown";
                string modVersion = "1.0";

                System.Diagnostics.Debug.WriteLine($"Adding mod: {modName} (Type: {modType}, Path: {path})");

                // Try to extract mod info if available
                try
                {
                    if (modType == "Folder")
                    {
                        // Look for mod.json in folder
                        string modInfoPath = Path.Combine(path, "mod.json");
                        if (File.Exists(modInfoPath))
                        {
                            var modInfo = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(modInfoPath));
                            modName = modInfo.Name ?? modName;
                            modAuthor = modInfo.Author ?? modAuthor;
                            modVersion = modInfo.Version ?? modVersion;
                        }
                    }
                    else if (modType == "Pak")
                    {
                        // Look for .pakmeta file
                        string metaPath = path + "meta";
                        if (File.Exists(metaPath))
                        {
                            try
                            {
                                string metaContent = File.ReadAllText(metaPath);
                                var metaLines = metaContent.Split('\n');

                                foreach (var line in metaLines)
                                {
                                    if (line.StartsWith("Author="))
                                        modAuthor = line.Substring("Author=".Length).Trim();
                                    else if (line.StartsWith("Version="))
                                        modVersion = line.Substring("Version=".Length).Trim();
                                    else if (line.StartsWith("Name="))
                                        modName = line.Substring("Name=".Length).Trim();
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error reading pakmeta: {ex.Message}");
                            }
                        }

                        // Also check for .pakmeta file (alternative format)
                        string altMetaPath = Path.ChangeExtension(path, ".pakmeta");
                        if (File.Exists(altMetaPath))
                        {
                            try
                            {
                                var metaInfo = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(altMetaPath));
                                if (metaInfo != null)
                                {
                                    if (metaInfo.Name != null)
                                        modName = metaInfo.Name.ToString();
                                    if (metaInfo.Author != null)
                                        modAuthor = metaInfo.Author.ToString();
                                    if (metaInfo.Version != null)
                                        modVersion = metaInfo.Version.ToString();
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error reading pakmeta JSON: {ex.Message}");
                            }
                        }
                    }
                    else if (modType == "PakBundle")
                    {
                        // Extract info from .pakbundleid
                        string bundleIdPath = Path.ChangeExtension(path, ".pakbundleid");
                        if (File.Exists(bundleIdPath))
                        {
                            var bundleInfo = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(bundleIdPath));
                            modName = bundleInfo.Name ?? modName;
                            modAuthor = bundleInfo.Author ?? modAuthor;
                            modVersion = bundleInfo.Version ?? modVersion;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Just use default values if we can't extract info
                    System.Diagnostics.Debug.WriteLine($"Error extracting mod info: {ex.Message}");
                }

                modPack.Mods.Add(new ModPackEntry
                {
                    Name = modName,
                    Type = modType,
                    RelativePath = modName,
                    Author = modAuthor,
                    Version = modVersion,
                    Path = path // Store the actual path for later use
                });

                System.Diagnostics.Debug.WriteLine($"Added mod to pack: {modName} (Type: {modType}, Author: {modAuthor}, Version: {modVersion})");
            }

            return modPack;
        }


        private static string DetermineModType(string path)
        {
            if (Directory.Exists(path))
                return "Folder";

            string extension = Path.GetExtension(path).ToLower();

            if (extension == ".pak")
                return "Pak";

            if (extension == ".pakbundle")
                return "PakBundle";

            return "Unknown";
        }

        public async Task<string> SaveToFileAsync(string outputPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                System.Diagnostics.Debug.WriteLine($"Creating mod pack in temporary directory: {tempDir}");

                // Create manifest file
                var manifest = new
                {
                    Name = this.Name,
                    Author = this.Author,
                    Version = this.Version,
                    Description = this.Description,
                    CreatedDate = this.CreatedDate,
                    Mods = this.Mods.Select(m => new
                    {
                        m.Name,
                        m.Type,
                        m.RelativePath,
                        m.Author,
                        m.Version
                    }).ToList() // Don't include the Path property in the manifest
                };

                string manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                string manifestPath = Path.Combine(tempDir, "packbundle.json");
                await File.WriteAllTextAsync(manifestPath, manifestJson);

                System.Diagnostics.Debug.WriteLine($"Created manifest file: {manifestPath}");

                // Create a directory for mods
                string modsDir = Path.Combine(tempDir, "Mods");
                Directory.CreateDirectory(modsDir);

                // Copy all mods to temp directory
                foreach (var mod in this.Mods)
                {
                    string sourcePath = mod.Path; // Use the actual path stored in ModPackEntry
                    string modFileName = Path.GetFileName(sourcePath);
                    string destPath = Path.Combine(modsDir, modFileName);

                    System.Diagnostics.Debug.WriteLine($"Copying mod from {sourcePath} to {destPath}");

                    if (Directory.Exists(sourcePath))
                    {
                        // Copy folder
                        CopyDirectory(sourcePath, destPath);
                        System.Diagnostics.Debug.WriteLine($"Copied folder mod: {modFileName}");
                    }
                    else if (File.Exists(sourcePath))
                    {
                        // Create directory if needed
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                        // Copy file
                        File.Copy(sourcePath, destPath, true);
                        System.Diagnostics.Debug.WriteLine($"Copied file mod: {modFileName}");

                        // Copy metadata files if they exist
                        if (mod.Type == "Pak")
                        {
                            // Check for .pakmeta format
                            string metaPath = Path.ChangeExtension(sourcePath, ".pakmeta");
                            if (File.Exists(metaPath))
                            {
                                string destMetaPath = Path.ChangeExtension(destPath, ".pakmeta");
                                File.Copy(metaPath, destMetaPath, true);
                                System.Diagnostics.Debug.WriteLine($"Copied metadata file: {Path.GetFileName(metaPath)}");
                            }

                            // Check for .pakmeta format (alternate)
                            string altMetaPath = sourcePath + "meta";
                            if (File.Exists(altMetaPath))
                            {
                                string destMetaPath = destPath + "meta";
                                File.Copy(altMetaPath, destMetaPath, true);
                                System.Diagnostics.Debug.WriteLine($"Copied alternate metadata file: {Path.GetFileName(altMetaPath)}");
                            }
                        }
                        else if (mod.Type == "PakBundle")
                        {
                            string bundleIdPath = Path.ChangeExtension(sourcePath, ".pakbundleid");
                            if (File.Exists(bundleIdPath))
                            {
                                string destBundleIdPath = Path.ChangeExtension(destPath, ".pakbundleid");
                                File.Copy(bundleIdPath, destBundleIdPath, true);
                                System.Diagnostics.Debug.WriteLine($"Copied bundle ID file: {Path.GetFileName(bundleIdPath)}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Source path does not exist: {sourcePath}");
                    }
                }

                // Copy thumbnail if exists
                if (!string.IsNullOrEmpty(ThumbnailPath) && File.Exists(ThumbnailPath))
                {
                    string thumbnailExt = Path.GetExtension(ThumbnailPath);
                    string destThumbnail = Path.Combine(tempDir, "thumbnail" + thumbnailExt);
                    File.Copy(ThumbnailPath, destThumbnail, true);
                    System.Diagnostics.Debug.WriteLine($"Copied thumbnail: {Path.GetFileName(ThumbnailPath)}");
                }

                // List all files in the temp directory for debugging
                System.Diagnostics.Debug.WriteLine("Files in temp directory:");
                ListFilesInDirectory(tempDir);

                // Create zip file
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    System.Diagnostics.Debug.WriteLine($"Deleted existing output file: {outputPath}");
                }

                System.Diagnostics.Debug.WriteLine($"Creating zip file: {outputPath}");
                ZipFile.CreateFromDirectory(tempDir, outputPath);

                System.Diagnostics.Debug.WriteLine($"Mod pack created successfully: {outputPath}");
                return outputPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating mod pack: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
            finally
            {
                // Clean up temp directory
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                        System.Diagnostics.Debug.WriteLine($"Cleaned up temp directory: {tempDir}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error cleaning up temp directory: {ex.Message}");
                    }
                }
            }
        }

        private void ListFilesInDirectory(string directory, string indent = "")
        {
            try
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    System.Diagnostics.Debug.WriteLine($"{indent}- {Path.GetFileName(file)} ({new FileInfo(file).Length} bytes)");
                }

                foreach (var dir in Directory.GetDirectories(directory))
                {
                    System.Diagnostics.Debug.WriteLine($"{indent}+ {Path.GetFileName(dir)}/");
                    ListFilesInDirectory(dir, indent + "  ");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error listing files: {ex.Message}");
            }
        }

        


        public static async Task<ModPack> LoadFromFileAsync(string filePath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                System.Diagnostics.Debug.WriteLine($"Extracting mod pack to: {tempDir}");

                // Extract zip file
                ZipFile.ExtractToDirectory(filePath, tempDir);

                // Read manifest
                string manifestPath = Path.Combine(tempDir, "packbundle.json");
                if (!File.Exists(manifestPath))
                {
                    throw new FileNotFoundException("Invalid mod pack: missing packbundle.json");
                }

                string manifestJson = await File.ReadAllTextAsync(manifestPath);
                var modPack = JsonConvert.DeserializeObject<ModPack>(manifestJson);

                if (modPack == null)
                {
                    throw new InvalidOperationException("Failed to parse mod pack manifest");
                }

                // Update paths to point to the extracted files
                string modsDir = Path.Combine(tempDir, "Mods");
                if (Directory.Exists(modsDir))
                {
                    // Update paths for mods
                    foreach (var mod in modPack.Mods)
                    {
                        string modPath = Path.Combine(modsDir, Path.GetFileName(mod.RelativePath));
                        mod.RelativePath = modPath;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Mods directory not found in mod pack");
                }

                // Look for thumbnail
                string[] thumbnailFiles = Directory.GetFiles(tempDir, "thumbnail.*");
                if (thumbnailFiles.Length > 0)
                {
                    modPack.ThumbnailPath = thumbnailFiles[0];
                }

                return modPack;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading mod pack: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Clean up temp directory on error
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch { }
                }

                throw;
            }
        }


        public async Task InstallAsync(string brickRigsModsPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Extract all mods to temp directory
                foreach (var mod in this.Mods)
                {
                    string sourcePath = Path.Combine(tempDir, mod.RelativePath);
                    string destPath = Path.Combine(brickRigsModsPath, mod.Name);

                    if (mod.Type == "Folder")
                    {
                        // Copy folder
                        if (Directory.Exists(destPath))
                            Directory.Delete(destPath, true);

                        CopyDirectory(sourcePath, destPath);
                    }
                    else
                    {
                        // Copy file
                        if (File.Exists(destPath))
                            File.Delete(destPath);

                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                        File.Copy(sourcePath, destPath, true);

                        // Copy metadata files
                        if (mod.Type == "Pak")
                        {
                            string sourceMetaPath = sourcePath + "meta";
                            string destMetaPath = destPath + "meta";

                            if (File.Exists(sourceMetaPath))
                            {
                                if (File.Exists(destMetaPath))
                                    File.Delete(destMetaPath);

                                File.Copy(sourceMetaPath, destMetaPath, true);
                            }
                        }
                        else if (mod.Type == "PakBundle")
                        {
                            string sourceBundleIdPath = Path.ChangeExtension(sourcePath, ".pakbundleid");
                            string destBundleIdPath = Path.ChangeExtension(destPath, ".pakbundleid");

                            if (File.Exists(sourceBundleIdPath))
                            {
                                if (File.Exists(destBundleIdPath))
                                    File.Delete(destBundleIdPath);

                                File.Copy(sourceBundleIdPath, destBundleIdPath, true);
                            }
                        }
                    }
                }
            }
            finally
            {
                // Clean up temp directory
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch { }
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            // Create destination directory
            Directory.CreateDirectory(destDir);

            // Copy files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy subdirectories
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }
    }
}
