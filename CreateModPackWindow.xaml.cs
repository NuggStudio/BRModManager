using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace BrickRigsModManager
{
    public partial class CreateModPackWindow : Window, INotifyPropertyChanged
    {
        public class ModItem : INotifyPropertyChanged
        {
            private bool _isSelected;

            public string Name { get; set; }
            public string Path { get; set; }
            public string Type { get; set; }
            public string Size { get; set; }
            public string Author { get; set; } = "Unknown";
            public string Version { get; set; } = "1.0";

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public string TypeDisplay
            {
                get
                {
                    switch (Type)
                    {
                        case "Folder": return "Folder Mod";
                        case "Pak": return "Pak Mod";
                        case "PakBundle": return "Pak Bundle";
                        default: return "Unknown";
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private ObservableCollection<ModItem> _mods = new ObservableCollection<ModItem>();
        private string _brickRigsModsPath;
        private string _thumbnailPath;

        public event PropertyChangedEventHandler PropertyChanged;

        private string _altModsPath;
        public CreateModPackWindow(string brickRigsModsPath)
        {
            InitializeComponent();

            // Store the provided path
            _brickRigsModsPath = brickRigsModsPath;

            // Also check the alternate path
            string altModsPath = Path.Combine(Path.GetDirectoryName(brickRigsModsPath), "Brick Rigs", "Content", "~mods");
            if (Directory.Exists(altModsPath))
            {
                _altModsPath = altModsPath;
                System.Diagnostics.Debug.WriteLine($"Found alternate mods path: {_altModsPath}");
            }

            ModsListView.ItemsSource = _mods;

            // Set default author from system username
            AuthorTextBox.Text = Environment.UserName;

            // Load available mods
            LoadAvailableMods();
        }


        private void LoadAvailableMods()
        {
            _mods.Clear();

            try
            {
                // Debug output
                System.Diagnostics.Debug.WriteLine($"Scanning for mods in primary path: {_brickRigsModsPath}");

                // First, check the standard mods directory
                if (Directory.Exists(_brickRigsModsPath))
                {
                    ScanModsDirectory(_brickRigsModsPath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Primary mods directory does not exist!");
                }

                // Now check all possible pak mod locations based on the ModManager code
                var possiblePakPaths = new[]
                {
            Path.Combine(Path.GetDirectoryName(_brickRigsModsPath), "BrickRigs", "Content", "Paks", "~mods"),
            Path.Combine(Path.GetDirectoryName(_brickRigsModsPath), "Content", "Paks", "~mods"),
            Path.Combine(Path.GetDirectoryName(_brickRigsModsPath), "BrickRigs", "Content", "~mods"),
            Path.Combine(Path.GetDirectoryName(_brickRigsModsPath), "Content", "~mods"),
            Path.Combine(Path.GetDirectoryName(_brickRigsModsPath), "Brick Rigs", "Content", "~mods"),
            Path.Combine(Path.GetDirectoryName(_brickRigsModsPath), "Brick Rigs", "Content", "Paks", "~mods")
        };

                foreach (var pakPath in possiblePakPaths)
                {
                    if (Directory.Exists(pakPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Found pak mods directory: {pakPath}");
                        ScanPakModsDirectory(pakPath);
                    }
                }

                // Final debug output
                System.Diagnostics.Debug.WriteLine($"Total mods found: {_mods.Count}");
                foreach (var mod in _mods)
                {
                    System.Diagnostics.Debug.WriteLine($"- {mod.Name} ({mod.Type}, {mod.Size}, Author: {mod.Author}, Version: {mod.Version})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading mods: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error loading mods: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScanModsDirectory(string directory)
        {
            try
            {
                // Get all files and directories in the mods folder
                var directories = Directory.GetDirectories(directory);
                var files = Directory.GetFiles(directory);

                System.Diagnostics.Debug.WriteLine($"Found {directories.Length} directories and {files.Length} files in {directory}");

                // Process directories (folder mods)
                foreach (var dir in directories)
                {
                    string name = Path.GetFileName(dir);

                    // Skip system folders or hidden folders
                    if (name.StartsWith(".") || name.Equals("Temp", StringComparison.OrdinalIgnoreCase))
                        continue;

                    long size = CalculateFolderSize(dir);

                    System.Diagnostics.Debug.WriteLine($"Adding folder mod: {name} ({FormatFileSize(size)})");

                    _mods.Add(new ModItem
                    {
                        Name = name,
                        Path = dir,
                        Type = "Folder",
                        Size = FormatFileSize(size),
                        IsSelected = false
                    });
                }

                // Process all files
                foreach (var file in files)
                {
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    string name = Path.GetFileName(file);

                    // Skip metadata files and other non-mod files
                    if (extension == ".pakmeta" || extension == ".pakbundleid" ||
                        extension == ".meta" || extension == ".json" ||
                        extension == ".txt" || extension == ".log" ||
                        name.EndsWith("meta") || name.StartsWith("."))
                    {
                        continue;
                    }

                    // Process pak files
                    if (extension == ".pak")
                    {
                        long size = new FileInfo(file).Length;
                        string author = "Unknown";
                        string version = "1.0";

                        System.Diagnostics.Debug.WriteLine($"Found pak file: {name} ({FormatFileSize(size)})");

                        // Check for metadata (both formats)
                        string metaPath = file + "meta";
                        string altMetaPath = Path.ChangeExtension(file, ".pakmeta");

                        if (File.Exists(metaPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"  Found metadata file: {Path.GetFileName(metaPath)}");

                            try
                            {
                                // Try to read metadata
                                string metaContent = File.ReadAllText(metaPath);
                                var metaLines = metaContent.Split('\n');

                                foreach (var line in metaLines)
                                {
                                    if (line.StartsWith("Author="))
                                        author = line.Substring("Author=".Length).Trim();
                                    else if (line.StartsWith("Version="))
                                        version = line.Substring("Version=".Length).Trim();
                                }

                                System.Diagnostics.Debug.WriteLine($"  Extracted metadata: Author={author}, Version={version}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"  Error reading metadata: {ex.Message}");
                            }
                        }
                        else if (File.Exists(altMetaPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"  Found alternate metadata file: {Path.GetFileName(altMetaPath)}");

                            try
                            {
                                // Try to read JSON metadata
                                string metaContent = File.ReadAllText(altMetaPath);
                                var metaInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(metaContent);

                                if (metaInfo != null)
                                {
                                    if (metaInfo.Author != null)
                                        author = metaInfo.Author.ToString();
                                    if (metaInfo.Version != null)
                                        version = metaInfo.Version.ToString();
                                }

                                System.Diagnostics.Debug.WriteLine($"  Extracted metadata: Author={author}, Version={version}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"  Error reading JSON metadata: {ex.Message}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("  No metadata file found");
                        }

                        System.Diagnostics.Debug.WriteLine($"Adding pak mod: {name} ({FormatFileSize(size)})");

                        _mods.Add(new ModItem
                        {
                            Name = name,
                            Path = file,
                            Type = "Pak",
                            Size = FormatFileSize(size),
                            IsSelected = false,
                            Author = author,
                            Version = version
                        });
                    }
                    // Process pakbundle files
                    else if (extension == ".pakbundle")
                    {
                        long size = new FileInfo(file).Length;
                        string author = "Unknown";
                        string version = "1.0";

                        System.Diagnostics.Debug.WriteLine($"Found pakbundle file: {name} ({FormatFileSize(size)})");

                        // Check for bundle ID file
                        string bundleIdPath = Path.ChangeExtension(file, ".pakbundleid");

                        if (File.Exists(bundleIdPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"  Found bundle ID file: {Path.GetFileName(bundleIdPath)}");

                            try
                            {
                                // Try to read bundle ID
                                string bundleContent = File.ReadAllText(bundleIdPath);
                                var bundleInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(bundleContent);

                                if (bundleInfo != null)
                                {
                                    if (bundleInfo.Author != null)
                                        author = bundleInfo.Author.ToString();
                                    if (bundleInfo.Version != null)
                                        version = bundleInfo.Version.ToString();
                                }

                                System.Diagnostics.Debug.WriteLine($"  Extracted metadata: Author={author}, Version={version}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"  Error reading bundle ID: {ex.Message}");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("  No bundle ID file found");
                        }

                        System.Diagnostics.Debug.WriteLine($"Adding pakbundle mod: {name} ({FormatFileSize(size)})");

                        _mods.Add(new ModItem
                        {
                            Name = name,
                            Path = file,
                            Type = "PakBundle",
                            Size = FormatFileSize(size),
                            IsSelected = false,
                            Author = author,
                            Version = version
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning directory {directory}: {ex.Message}");
            }
        }

        private void ScanPakModsDirectory(string pakPath)
        {
            try
            {
                // Check for enabled pak files (*.pak)
                var pakFiles = Directory.GetFiles(pakPath, "*.pak");
                System.Diagnostics.Debug.WriteLine($"Found {pakFiles.Length} pak files in {pakPath}");

                // Also check for disabled pak files (*.pak.disabled)
                var disabledPakFiles = Directory.GetFiles(pakPath, "*.pak.disabled");
                System.Diagnostics.Debug.WriteLine($"Found {disabledPakFiles.Length} disabled pak files in {pakPath}");

                // Process enabled pak files
                foreach (var pakFile in pakFiles)
                {
                    ProcessPakFile(pakFile, true);
                }

                // Process disabled pak files
                foreach (var disabledPakFile in disabledPakFiles)
                {
                    ProcessPakFile(disabledPakFile, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning pak mods directory {pakPath}: {ex.Message}");
            }
        }

        private void ProcessPakFile(string pakFile, bool isEnabled)
        {
            try
            {
                var pakPath = Path.GetDirectoryName(pakFile);
                var pakFileName = Path.GetFileName(pakFile);
                var pakName = Path.GetFileNameWithoutExtension(pakFile);

                // If it's a disabled file, remove the .disabled extension for the name
                if (!isEnabled)
                {
                    pakName = Path.GetFileNameWithoutExtension(pakName);
                }

                System.Diagnostics.Debug.WriteLine($"Processing {(isEnabled ? "enabled" : "disabled")} pak file: {pakName}");

                long size = new FileInfo(pakFile).Length;
                string author = "Unknown";
                string version = "1.0";

                // Check for metadata (both formats)
                string metaPath = Path.Combine(pakPath, $"{pakName}.pakmeta");
                string altMetaPath = pakFile + "meta";

                if (File.Exists(metaPath))
                {
                    System.Diagnostics.Debug.WriteLine($"  Found metadata file: {Path.GetFileName(metaPath)}");

                    try
                    {
                        // Try to read JSON metadata
                        string metaContent = File.ReadAllText(metaPath);
                        var metaInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(metaContent);

                        if (metaInfo != null)
                        {
                            if (metaInfo.Author != null)
                                author = metaInfo.Author.ToString();
                            if (metaInfo.Version != null)
                                version = metaInfo.Version.ToString();
                        }

                        System.Diagnostics.Debug.WriteLine($"  Extracted metadata: Author={author}, Version={version}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Error reading JSON metadata: {ex.Message}");
                    }
                }
                else if (File.Exists(altMetaPath))
                {
                    System.Diagnostics.Debug.WriteLine($"  Found alternate metadata file: {Path.GetFileName(altMetaPath)}");

                    try
                    {
                        // Try to read metadata
                        string metaContent = File.ReadAllText(altMetaPath);
                        var metaLines = metaContent.Split('\n');

                        foreach (var line in metaLines)
                        {
                            if (line.StartsWith("Author="))
                                author = line.Substring("Author=".Length).Trim();
                            else if (line.StartsWith("Version="))
                                version = line.Substring("Version=".Length).Trim();
                        }

                        System.Diagnostics.Debug.WriteLine($"  Extracted metadata: Author={author}, Version={version}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Error reading metadata: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("  No metadata file found");
                }

                System.Diagnostics.Debug.WriteLine($"Adding pak mod: {pakFileName} ({FormatFileSize(size)})");

                _mods.Add(new ModItem
                {
                    Name = pakFileName,
                    Path = pakFile,
                    Type = "Pak",
                    Size = FormatFileSize(size),
                    IsSelected = false,
                    Author = author,
                    Version = version
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing pak file {pakFile}: {ex.Message}");
            }
        }

        private long CalculateFolderSize(string folder)
        {
            try
            {
                return Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
            }
            catch
            {
                return 0;
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var mod in _mods)
            {
                mod.IsSelected = true;
            }
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var mod in _mods)
            {
                mod.IsSelected = false;
            }
        }

        private void AddExternalMod_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Mod Files|*.pak;*.pakbundle|Folders|*.*|All Files|*.*",
                Title = "Select Mod File or Folder",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    string name = Path.GetFileName(file);
                    string type;
                    long size;

                    if (Directory.Exists(file))
                    {
                        type = "Folder";
                        size = CalculateFolderSize(file);
                    }
                    else
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        type = ext == ".pak" ? "Pak" : (ext == ".pakbundle" ? "PakBundle" : "Unknown");
                        size = new FileInfo(file).Length;
                    }

                    _mods.Add(new ModItem
                    {
                        Name = name,
                        Path = file,
                        Type = type,
                        Size = FormatFileSize(size),
                        IsSelected = true
                    });
                }
            }
        }

        private void BrowseThumbnail_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp",
                Title = "Select Thumbnail Image"
            };

            if (dialog.ShowDialog() == true)
            {
                _thumbnailPath = dialog.FileName;
                

                // Show preview
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_thumbnailPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    ThumbnailPreview.Source = bitmap;
                    ThumbnailPreviewBorder.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading thumbnail: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ThumbnailPreviewBorder.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void CreateModPack_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(PackNameTextBox.Text))
            {
                MessageBox.Show("Please enter a name for the mod pack.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                PackNameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(AuthorTextBox.Text))
            {
                MessageBox.Show("Please enter an author name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                AuthorTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(VersionTextBox.Text))
            {
                MessageBox.Show("Please enter a version number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                VersionTextBox.Focus();
                return;
            }

            // Get selected mods
            var selectedMods = _mods.Where(m => m.IsSelected).ToList();
            if (selectedMods.Count == 0)
            {
                MessageBox.Show("Please select at least one mod to include in the pack.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Debug output
            System.Diagnostics.Debug.WriteLine($"Creating mod pack with {selectedMods.Count} mods:");
            foreach (var mod in selectedMods)
            {
                System.Diagnostics.Debug.WriteLine($"- {mod.Name} ({mod.Type}, Path: {mod.Path})");
            }

            // Ask for save location
            var saveDialog = new SaveFileDialog
            {
                Filter = "BrickRigs Mod Pack|*.brmodpack",
                Title = "Save Mod Pack",
                FileName = $"{PackNameTextBox.Text}.brmodpack"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            try
            {
                // Show progress
                StatusText.Text = "Creating mod pack...";
                IsEnabled = false;

                // Create mod pack
                var modPack = await ModPack.CreateFromPathsAsync(
                    PackNameTextBox.Text,
                    AuthorTextBox.Text,
                    VersionTextBox.Text,
                    DescriptionTextBox.Text,
                    selectedMods.Select(m => m.Path).ToList(),
                    _thumbnailPath
                );

                // Save to file
                await modPack.SaveToFileAsync(saveDialog.FileName);

                // Verify the file was created
                if (File.Exists(saveDialog.FileName))
                {
                    var fileInfo = new FileInfo(saveDialog.FileName);
                    System.Diagnostics.Debug.WriteLine($"Mod pack file created: {saveDialog.FileName} ({fileInfo.Length} bytes)");

                    MessageBox.Show($"Mod pack created successfully at:\n{saveDialog.FileName}\n\nSize: {FormatFileSize(fileInfo.Length)}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to create mod pack file. The file was not found after creation.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating mod pack: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error creating mod pack: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error creating mod pack.";
            }
            finally
            {
                IsEnabled = true;
            }
        }

    }
}
