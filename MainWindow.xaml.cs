using BrickRigsModManagerWPF;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BrickRigsModManager
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ModManager _modManager;
        private readonly HttpClient _httpClient;
        private ObservableCollection<ModInfo> _installedMods;
        private ObservableCollection<FeaturedModInfo> _featuredMods;
        private FeaturedModInfo _selectedFeaturedMod;
        private string _gamePath;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ModInfo> InstalledMods
        {
            get => _installedMods;
            set
            {
                _installedMods = value;
                OnPropertyChanged(nameof(InstalledMods));
                UpdateModCount();
            }
        }

        public ObservableCollection<FeaturedModInfo> FeaturedMods
        {
            get => _featuredMods;
            set
            {
                _featuredMods = value;
                OnPropertyChanged(nameof(FeaturedMods));
            }
        }

        public string GamePath
        {
            get => _gamePath;
            set
            {
                _gamePath = value;
                OnPropertyChanged(nameof(GamePath));
                GamePathTextBox.Text = value;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            InitializeThemes();
            _modManager = new ModManager();
            _httpClient = new HttpClient();
            _installedMods = new ObservableCollection<ModInfo>();
            _featuredMods = new ObservableCollection<FeaturedModInfo>();
            _updateService = new UpdateService(
        "enter your own things",
        _currentVersion);
            this.PreviewMouseRightButtonUp += (s, e) => ReleaseMouseCapture();
            this.PreviewMouseMove += ScrollViewer_PreviewMouseMove;
            this.MouseLeave += (s, e) => ReleaseMouseCapture();

            

            FeaturedModsListBox = this.FindName("FeaturedModsListBox") as ListBox;

            // Now, the following line will work:
            SizeChanged += MainWindow_SizeChanged;

            this.Loaded += (s, e) =>
            {
                // We need to wait for the UI to be fully loaded and populated
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    InitializeHorizontalScrolling();
                }), System.Windows.Threading.DispatcherPriority.Loaded);

            };

            // Add window-level event handlers for when the mouse leaves the ScrollViewer
            this.PreviewMouseUp += (s, e) =>
            {
                if (_isHorizontalDragging)
                {
                    _isHorizontalDragging = false;
                    _horizontalScrollViewer = null;
                    Mouse.Capture(null);
                }
            };


            this.MouseLeave += (s, e) =>
            {
                if (_isHorizontalDragging)
                {
                    _isHorizontalDragging = false;
                    _horizontalScrollViewer = null;
                    Mouse.Capture(null);
                }
            };
            this.Loaded += (s, e) => InitializeHorizontalScrolling();

            InitializeAsync();
        }


        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Update the layout when the window size changes
            AdjustFeaturedModsLayout();
        }

        private void AdjustFeaturedModsLayout()
        {
            if (FeaturedModsScrollViewer != null)
            {
                // Get the available width of the ScrollViewer
                double availableWidth = FeaturedModsScrollViewer.ViewportWidth;

                // Calculate how many tiles can fit in a row
                // Each tile is 220px wide with a 10px right margin
                int tilesPerRow = Math.Max(1, (int)((availableWidth - 10) / 230));

                // Find the WrapPanel in the visual tree
                var itemsPresenter = FindVisualChild<ItemsPresenter>(FeaturedModsItemsControl);
                if (itemsPresenter != null)
                {
                    var wrapPanel = FindVisualChild<WrapPanel>(itemsPresenter);
                    if (wrapPanel != null)
                    {
                        // Set the width to ensure proper wrapping
                        wrapPanel.Width = availableWidth - 10;

                        // Ensure the ScrollViewer updates its scroll info
                        FeaturedModsScrollViewer.UpdateLayout();
                    }
                }
            }
        }


        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void AttachScrollingEvents(ScrollViewer scrollViewer)
        {
            if (scrollViewer != null)
            {
                // Attach direct event handlers
                scrollViewer.PreviewMouseRightButtonDown += (s, e) =>
                {
                    _lastMousePosition = e.GetPosition(scrollViewer);
                    _isRightMouseDown = true;
                    _activeScrollViewer = scrollViewer;
                    _isHorizontalScrolling = false; // Will be determined on first move
                    Mouse.Capture(scrollViewer);
                    e.Handled = true;
                };

                // Make sure the ScrollViewer can receive focus
                scrollViewer.Focusable = true;

                // Disable vertical scrolling completely for these ScrollViewers
                scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

                // Explicitly set to horizontal only
                scrollViewer.PanningMode = PanningMode.HorizontalOnly;

                // Attach mouse wheel handler
                scrollViewer.PreviewMouseWheel += HorizontalScrollViewer_PreviewMouseWheel;
            }
        }





        private void CreateModPack_Click(object sender, RoutedEventArgs e)
        {
            // Get the appropriate mods path
            string modsPath = GetBrickRigsModsPath();

            var createModPackWindow = new CreateModPackWindow(modsPath);
            createModPackWindow.Owner = this;

            if (createModPackWindow.ShowDialog() == true)
            {
                // Refresh mod list if needed
                RefreshMods_Click(this, new RoutedEventArgs());
            }
        }


        private string GetBrickRigsModsPath()
        {
            // First, check the standard mods path
            string standardPath = Path.Combine(GamePathTextBox.Text, "BrickRigs", "Mods");
            if (Directory.Exists(standardPath))
            {
                return standardPath;
            }

            // Then check the alternate path for pak mods
            string alternatePath = Path.Combine(GamePathTextBox.Text, "Brick Rigs", "Content", "~mods");
            if (Directory.Exists(alternatePath))
            {
                return alternatePath;
            }

            // Also check other possible paths
            string[] possiblePaths = new[]
            {
        Path.Combine(GamePathTextBox.Text, "Content", "Paks", "~mods"),
        Path.Combine(GamePathTextBox.Text, "BrickRigs", "Content", "Paks", "~mods"),
        Path.Combine(GamePathTextBox.Text, "Content", "~mods"),
        Path.Combine(GamePathTextBox.Text, "Mods")
    };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            // If neither exists, create and return the standard path
            Directory.CreateDirectory(standardPath);
            return standardPath;
        }






        private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private double _featuredModsPosition = 0;
        private double _popularModsPosition = 0;

        // Add these methods to your MainWindow class

        private void ScrollHorizontally(ItemsControl itemsControl, Canvas canvas, double offset, ref double position)
        {
            if (itemsControl == null || canvas == null) return;

            // Calculate the total width of the items
            double totalWidth = 0;

            if (itemsControl.ItemsSource is IEnumerable<object> items)
            {
                totalWidth = items.Count() * 315; // 300 width + 15 margin
            }

            // Calculate the new position
            double newPosition = position + offset;

            // Ensure we don't scroll beyond the bounds
            newPosition = Math.Max(0, Math.Min(newPosition, Math.Max(0, totalWidth - canvas.ActualWidth)));

            // Update the position
            position = newPosition;

            // Apply the transform to the ItemsControl
            Canvas.SetLeft(itemsControl, -position);

            // Update button states
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {

        }

        private void AddRightClickDragScrolling(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null) return;

            bool isDragging = false;
            Point lastPosition = new Point();

            scrollViewer.MouseRightButtonDown += (s, e) =>
            {
                isDragging = true;
                lastPosition = e.GetPosition(scrollViewer);
                scrollViewer.CaptureMouse();
                e.Handled = true;
            };

            scrollViewer.MouseRightButtonUp += (s, e) =>
            {
                isDragging = false;
                scrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            };

            scrollViewer.MouseMove += (s, e) =>
            {
                if (isDragging)
                {
                    Point position = e.GetPosition(scrollViewer);
                    double delta = lastPosition.X - position.X;
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + delta);
                    lastPosition = position;
                    e.Handled = true;
                }
            };
        }

        private void AttachDragScrolling(ScrollViewer scrollViewer)
        {
            // Make sure the ScrollViewer is set up for horizontal scrolling
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

            // Add a visual indicator that this is scrollable
            scrollViewer.Cursor = Cursors.Hand;

            // Right mouse button events for drag scrolling
            scrollViewer.PreviewMouseRightButtonDown += (s, e) =>
            {
                Debug.WriteLine("Right mouse button down");
                _isDragging = true;
                _lastDragPoint = e.GetPosition(scrollViewer);
                _currentScrollViewer = scrollViewer;
                Mouse.Capture(scrollViewer);
                e.Handled = true;
            };

            scrollViewer.PreviewMouseRightButtonUp += (s, e) =>
            {
                Debug.WriteLine("Right mouse button up");
                _isDragging = false;
                _currentScrollViewer = null;
                Mouse.Capture(null);
                e.Handled = true;
            };

            scrollViewer.PreviewMouseMove += (s, e) =>
            {
                if (_isDragging && _currentScrollViewer == scrollViewer)
                {
                    Point currentPosition = e.GetPosition(scrollViewer);
                    double deltaX = _lastDragPoint.X - currentPosition.X;

                    Debug.WriteLine($"Scrolling by {deltaX}");
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + deltaX);

                    _lastDragPoint = currentPosition;
                    e.Handled = true;
                }
            };

            // Also enable left-click drag scrolling for better usability
            scrollViewer.PreviewMouseLeftButtonDown += (s, e) =>
            {
                // Only if not clicking on a clickable element
                if (e.OriginalSource is FrameworkElement element &&
                    (element is Button || element.FindAncestor<Button>() != null))
                {
                    return;
                }

                Debug.WriteLine("Left mouse button down");
                _isDragging = true;
                _lastDragPoint = e.GetPosition(scrollViewer);
                _currentScrollViewer = scrollViewer;
                Mouse.Capture(scrollViewer);
                e.Handled = true;
            };

            scrollViewer.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (_isDragging)
                {
                    Debug.WriteLine("Left mouse button up");
                    _isDragging = false;
                    _currentScrollViewer = null;
                    Mouse.Capture(null);
                    e.Handled = true;
                }
            };

            // Mouse wheel for horizontal scrolling
            scrollViewer.PreviewMouseWheel += (s, e) =>
            {
                Debug.WriteLine($"Mouse wheel delta: {e.Delta}");
                scrollViewer.ScrollToHorizontalOffset(
                    scrollViewer.HorizontalOffset + (e.Delta > 0 ? -50 : 50));
                e.Handled = true;
            };
        }
        private void AttachHorizontalScrolling(ScrollViewer scrollViewer)
        {
            // Make sure it's set up for horizontal scrolling
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

            // Add a simple mouse wheel handler
            scrollViewer.PreviewMouseWheel += (s, e) =>
            {
                // Scroll horizontally with the mouse wheel
                scrollViewer.ScrollToHorizontalOffset(
                    scrollViewer.HorizontalOffset + (e.Delta > 0 ? -50 : 50));
                e.Handled = true;
            };

            // Add right-click drag scrolling
            bool isDragging = false;
            Point lastPosition = new Point();

            scrollViewer.MouseRightButtonDown += (s, e) =>
            {
                isDragging = true;
                lastPosition = e.GetPosition(scrollViewer);
                scrollViewer.CaptureMouse();
                e.Handled = true;
            };

            scrollViewer.MouseRightButtonUp += (s, e) =>
            {
                isDragging = false;
                scrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            };

            scrollViewer.MouseMove += (s, e) =>
            {
                if (isDragging)
                {
                    Point position = e.GetPosition(scrollViewer);
                    double delta = lastPosition.X - position.X;
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + delta);
                    lastPosition = position;
                    e.Handled = true;
                }
            };
        }
        private void InitializeHorizontalScrolling()
        {
            // Find the horizontal ScrollViewers by name
            var featuredScrollViewer = FindName("FeaturedModsScrollViewer") as ScrollViewer;
            var popularScrollViewer = FindName("PopularModsScrollViewer") as ScrollViewer;

            // Attach events to each ScrollViewer
            if (featuredScrollViewer != null)
            {
                AttachHorizontalScrolling(featuredScrollViewer);
            }

            if (popularScrollViewer != null)
            {
                AttachHorizontalScrolling(popularScrollViewer);
            }
        }
        private bool _isHorizontalDragging = false;
        private ScrollViewer _horizontalScrollViewer = null;
        private void AttachRightClickDragToElement(UIElement element, ScrollViewer parentScrollViewer)
        {
            element.PreviewMouseRightButtonDown += (s, e) =>
            {
                _isHorizontalDragging = true;
                _lastDragPoint = e.GetPosition(parentScrollViewer);
                _horizontalScrollViewer = parentScrollViewer;
                element.CaptureMouse();
                e.Handled = true;
            };

            element.PreviewMouseRightButtonUp += (s, e) =>
            {
                if (_isHorizontalDragging)
                {
                    _isHorizontalDragging = false;
                    _horizontalScrollViewer = null;
                    element.ReleaseMouseCapture();
                    e.Handled = true;
                }
            };

            element.PreviewMouseMove += (s, e) =>
            {
                if (_isHorizontalDragging && _horizontalScrollViewer != null)
                {
                    Point currentPosition = e.GetPosition(_horizontalScrollViewer);
                    double deltaX = _lastDragPoint.X - currentPosition.X;

                    _horizontalScrollViewer.ScrollToHorizontalOffset(
                        _horizontalScrollViewer.HorizontalOffset + deltaX);

                    _lastDragPoint = currentPosition;
                    e.Handled = true;
                }
            };
        }

        private bool _isDragging = false;
        private Point _lastDragPoint;
        private ScrollViewer _currentScrollViewer = null;


        private void Window_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
            e.Handled = _isRightMouseDown; // Only handle if we were dragging
        }
        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isRightMouseDown && _activeScrollViewer != null)
            {
                Point currentPosition = e.GetPosition(_activeScrollViewer);
                double deltaX = _lastMousePosition.X - currentPosition.X;
                double deltaY = _lastMousePosition.Y - currentPosition.Y;

                // Determine if this is a horizontal drag (only on first significant movement)
                if (!_isHorizontalScrolling)
                {
                    if (Math.Abs(deltaX) > _horizontalDragThreshold)
                    {
                        _isHorizontalScrolling = true;
                        Mouse.OverrideCursor = Cursors.Hand; // Visual feedback
                    }
                    else if (Math.Abs(deltaY) > _horizontalDragThreshold)
                    {
                        // This is a vertical drag, release the horizontal ScrollViewer
                        ReleaseMouseCapture();
                        return;
                    }
                }

                // If we've determined this is a horizontal drag, apply scrolling
                if (_isHorizontalScrolling)
                {
                    _activeScrollViewer.ScrollToHorizontalOffset(_activeScrollViewer.HorizontalOffset + deltaX);
                    _lastMousePosition = currentPosition;
                    e.Handled = true;
                }
            }
        }
        private async void InitializeAsync()
        {
            await DetectGamePath();
            await LoadInstalledMods();
            await LoadFeaturedMods();
            await CheckForUpdates();
        }

        private async Task DetectGamePath()
        {
            var defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\Brick Rigs";

            if (Directory.Exists(defaultPath))
            {
                GamePath = defaultPath;
                _modManager.SetGamePath(defaultPath);
            }
            else
            {
                // Check other common Steam locations
                var steamPaths = new[]
                {
                    @"C:\Program Files\Steam\steamapps\common\Brick Rigs",
                    @"D:\Steam\steamapps\common\Brick Rigs",
                    @"E:\Steam\steamapps\common\Brick Rigs"
                };

                foreach (var path in steamPaths)
                {
                    if (Directory.Exists(path))
                    {
                        GamePath = path;
                        _modManager.SetGamePath(path);
                        return;
                    }
                }

                MessageBox.Show("Brick Rigs installation not found. Please browse for your game directory.",
                    "Game Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private bool _isDraggingHorizontally = false;
        private async Task LoadInstalledMods()
        {
            try
            {
                Console.WriteLine("Loading installed mods...");
                var mods = await _modManager.GetInstalledMods();

                Console.WriteLine($"Found {mods.Count} mods in total");
                foreach (var mod in mods)
                {
                    Console.WriteLine($"- {mod.FriendlyName} ({(mod.IsPakMod ? "PAK" : "Folder")} mod, {(mod.IsEnabled ? "Enabled" : "Disabled")})");
                }

                InstalledMods.Clear();

                foreach (var mod in mods)
                {
                    InstalledMods.Add(mod);
                }

                InstalledModsList.ItemsSource = InstalledMods;
                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading installed mods: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private CancellationTokenSource _downloadCancellationTokenSource;


        private Point _lastMousePosition;
        private bool _isRightMouseDown = false;
        private ScrollViewer _activeScrollViewer = null;

        private void ScrollViewer_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                _lastMousePosition = e.GetPosition(scrollViewer);
                _isRightMouseDown = true;
                _activeScrollViewer = scrollViewer;
                _isDraggingHorizontally = scrollViewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;

                scrollViewer.CaptureMouse();
                e.Handled = true;

                // Change cursor to indicate drag mode
                Mouse.OverrideCursor = Cursors.Hand;
            }
        }

        private void ReleaseMouseCapture()
        {
            if (_isRightMouseDown)
            {
                _isRightMouseDown = false;
                _isHorizontalScrolling = false;

                if (_activeScrollViewer != null)
                {
                    Mouse.Capture(null);
                    _activeScrollViewer = null;
                }

                // Restore cursor
                Mouse.OverrideCursor = null;
            }
        }

        private void ScrollViewer_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
            e.Handled = true;
        }

        private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isRightMouseDown && _activeScrollViewer != null)
            {
                Point currentPosition = e.GetPosition(_activeScrollViewer);
                double deltaX = _lastMousePosition.X - currentPosition.X;
                double deltaY = _lastMousePosition.Y - currentPosition.Y;

                // Determine if we're dragging horizontally or vertically based on the initial movement
                if (!_isDraggingHorizontally && Math.Abs(deltaY) > Math.Abs(deltaX) * 2)
                {
                    _isDraggingHorizontally = false;
                }
                else if (Math.Abs(deltaX) > 5) // Add a small threshold to prevent accidental scrolling
                {
                    _isDraggingHorizontally = true;
                }

                // Apply scrolling based on drag direction
                if (_isDraggingHorizontally)
                {
                    _activeScrollViewer.ScrollToHorizontalOffset(_activeScrollViewer.HorizontalOffset + deltaX);
                }
                else
                {
                    // Find the parent ScrollViewer for vertical scrolling
                    var parentScrollViewer = FindParentScrollViewer(_activeScrollViewer);
                    if (parentScrollViewer != null)
                    {
                        parentScrollViewer.ScrollToVerticalOffset(parentScrollViewer.VerticalOffset + deltaY);
                    }
                }

                // Update last position
                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
        }
        private double _horizontalDragThreshold = 5.0;
        private bool _isHorizontalScrolling = false;
        private ScrollViewer FindParentScrollViewer(DependencyObject child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is ScrollViewer))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as ScrollViewer;
        }

        // Also handle mouse wheel for horizontal scrolling
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // Scroll horizontally with the mouse wheel
                scrollViewer.ScrollToHorizontalOffset(
                    scrollViewer.HorizontalOffset + (e.Delta > 0 ? -50 : 50));
                e.Handled = true;
            }
        }

        private async void DetailDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFeaturedMod != null)
            {
                try
                {
                    // Log the download URL for debugging


                    DetailDownloadButton.Content = "Downloading...";
                    DetailDownloadButton.IsEnabled = false;

                    if (string.IsNullOrEmpty(_selectedFeaturedMod.DownloadUrl))
                    {
                        MessageBox.Show("This mod doesn't have a download URL specified.", "Download Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Check if it's a Dropbox link and show guidance if needed
                    if (_selectedFeaturedMod.DownloadUrl.Contains("dropbox.com"))
                    {
                        var result = MessageBox.Show(
                            $"You're trying to download from Dropbox URL:\n{_selectedFeaturedMod.DownloadUrl}\n\n" +
                            "For best results, the link should be a direct download link.\n\n" +
                            "If the download fails, please ask the mod creator to:\n" +
                            "1. Right-click the file in Dropbox\n" +
                            "2. Select 'Copy link'\n" +
                            "3. Make sure 'No expiry' is selected\n" +
                            "4. Click 'Copy link'\n\n" +
                            "Do you want to continue with the current link?",
                            "Dropbox Download",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.No)
                        {
                            DetailDownloadButton.Content = "Download & Install";
                            DetailDownloadButton.IsEnabled = true;
                            return;
                        }
                    }

                    // Show progress bar
                    DownloadProgressBar.Value = 0;
                    DownloadProgressBar.Visibility = Visibility.Visible;
                    DownloadStatusText.Visibility = Visibility.Visible;
                    CancelDownloadButton.Visibility = Visibility.Visible;

                    // Create progress and cancellation objects
                    var progress = new Progress<(int percentage, string status)>(ReportProgress);
                    _downloadCancellationTokenSource = new CancellationTokenSource();

                    await _modManager.InstallModFromUrl(
                        _selectedFeaturedMod.DownloadUrl,
                        progress,
                        _downloadCancellationTokenSource.Token);

                    await LoadInstalledMods();

                    MessageBox.Show("Mod downloaded and installed successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    ShowModManagerView();
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("Download was canceled.", "Download Canceled",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Error downloading mod: {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMessage += $"\n\nDetails: {ex.InnerException.Message}";
                    }

                    // For 403 errors, provide more specific guidance
                    if (ex.Message.Contains("403") || (ex.InnerException != null && ex.InnerException.Message.Contains("403")))
                    {
                        errorMessage += "\n\nThis is likely due to access restrictions on the file. Please ask the mod creator to check their sharing settings and provide a direct download link.";
                    }

                    MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                }
                finally
                {
                    // Clean up
                    _downloadCancellationTokenSource?.Dispose();
                    _downloadCancellationTokenSource = null;

                    // Hide progress indicators
                    DownloadProgressBar.Visibility = Visibility.Collapsed;
                    DownloadStatusText.Visibility = Visibility.Collapsed;
                    CancelDownloadButton.Visibility = Visibility.Collapsed;

                    DetailDownloadButton.Content = "Download & Install";
                    DetailDownloadButton.IsEnabled = true;
                }
            }
        }


        private void ReportProgress((int percentage, string status) progress)
        {
            // Update UI on the UI thread
            Dispatcher.Invoke(() =>
            {
                DownloadProgressBar.Value = progress.percentage;
                DownloadStatusText.Text = progress.status;
            });
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            _downloadCancellationTokenSource?.Cancel();
        }





        private ObservableCollection<FeaturedModInfo> _allFeaturedMods;
        private ObservableCollection<FeaturedModInfo> _filteredFeaturedMods;
        private Dictionary<string, List<FeaturedModInfo>> _categorizedMods;

        private string _selectedCategoryId;
        public ObservableCollection<Category> Categories
        {
            get => _categories;
            set
            {
                _categories = value;
                OnPropertyChanged(nameof(Categories));
            }
        }

        private ObservableCollection<Category> _categories;

        public string SelectedCategoryId
        {
            get => _selectedCategoryId;
            set
            {
                _selectedCategoryId = value;
                OnPropertyChanged(nameof(SelectedCategoryId));
                FilterModsByCategory();
            }
        }

        private void FilterModsByCategory()
        {
            if (_allFeaturedMods == null)
                return;

            if (string.IsNullOrEmpty(SelectedCategoryId) || SelectedCategoryId == "all")
            {
                // Show all mods
                // Show all mods
                _filteredFeaturedMods = new ObservableCollection<FeaturedModInfo>(_allFeaturedMods);

            }
            else
            {
                _filteredFeaturedMods = new ObservableCollection<FeaturedModInfo>(
            _allFeaturedMods.Where(m => m.Category == SelectedCategoryId));

            }

        }
        private async Task LoadFeaturedMods()
        {
            try
            {
                var json = await _httpClient.GetStringAsync("enter your own things");

                // Create a wrapper class to match the JSON structure
                var wrapper = JsonConvert.DeserializeObject<FeaturedModsWrapper>(json);

                _allFeaturedMods = new ObservableCollection<FeaturedModInfo>();
                _categorizedMods = new Dictionary<string, List<FeaturedModInfo>>();

                if (wrapper?.FeaturedMods != null)
                {
                    foreach (var mod in wrapper.FeaturedMods)
                    {
                        _allFeaturedMods.Add(mod);

                        // Add to category dictionary
                        if (!string.IsNullOrEmpty(mod.Category))
                        {
                            if (!_categorizedMods.ContainsKey(mod.Category))
                            {
                                _categorizedMods[mod.Category] = new List<FeaturedModInfo>();
                            }

                            _categorizedMods[mod.Category].Add(mod);
                        }
                    }
                }

                // Populate category combo box
                if (CategoryComboBox != null)
                {
                    CategoryComboBox.Items.Clear();
                    CategoryComboBox.Items.Add("All Categories");

                    if (_categorizedMods != null)
                    {
                        foreach (var category in _categorizedMods.Keys)
                        {
                            CategoryComboBox.Items.Add(category);
                        }
                    }

                    // Select "All Categories" by default
                    CategoryComboBox.SelectedIndex = 0;
                }

                // Set the featured mods section
                if (FeaturedModsItemsControl != null)
                {
                    FeaturedModsItemsControl.ItemsSource = _allFeaturedMods;

                    // Adjust the layout after loading the mods
                    // Use dispatcher to ensure the UI has updated
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AdjustFeaturedModsLayout();

                        // Ensure the ScrollViewer can scroll to show all content
                        FeaturedModsScrollViewer.UpdateLayout();

                        // Reset scroll position to top
                        FeaturedModsScrollViewer.ScrollToTop();
                    }), DispatcherPriority.Loaded);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading featured mods: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }






        private void AddInvisibleScrollViewerStyle()
        {
            // Add the invisible ScrollViewer style to resources
            var style = new Style(typeof(ScrollViewer));
            style.Setters.Add(new Setter(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden));
            style.Setters.Add(new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden));
            style.Setters.Add(new Setter(ScrollViewer.PanningModeProperty, PanningMode.Both));

            this.Resources.Add("InvisibleScrollViewer", style);
        }

        private void ShowModDetails(FeaturedModInfo mod)
        {
            // Set detail view properties
            DetailNameText.Text = mod.Name;
            DetailAuthorText.Text = $"By {mod.Author}";
            DetailVersionText.Text = $"Version {mod.Version}";
            DetailDescriptionText.Text = mod.Description; // Correct casing

            // Set images
            try
            {
                if (!string.IsNullOrEmpty(mod.BannerUrl)) // Correct casing
                {
                    DetailBannerImage.Source = new BitmapImage(new Uri(mod.BannerUrl));
                }

                if (!string.IsNullOrEmpty(mod.LogoUrl)) // Correct casing
                {
                    DetailLogoImage.Source = new BitmapImage(new Uri(mod.LogoUrl));
                }
            }
            catch (Exception ex)
            {
                // Handle image loading error
                Debug.WriteLine($"Error loading images: {ex.Message}");
            }

            // Store the download URL for later use
            DetailDownloadButton.Tag = mod;

            // Show the detail view
            ModManagerView.Visibility = Visibility.Collapsed;
            FeaturedModsView.Visibility = Visibility.Collapsed;
            ModDetailView.Visibility = Visibility.Visible;
        }



        private void UpdateFilteredMods(List<FeaturedModInfo> allMods)
        {
            var selectedCategory = CategoryComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedCategory) || selectedCategory == "All Categories")
            {
                // Show all mods
                _filteredFeaturedMods = new ObservableCollection<FeaturedModInfo>(allMods);

            }
            else
            {
                // Filter by category
                var filteredMods = allMods.Where(m => m.Category == selectedCategory).ToList();
                _filteredFeaturedMods = new ObservableCollection<FeaturedModInfo>(filteredMods);

            }

            // Update the filtered mods list with the correct control name

        }


        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryComboBox == null || CategoryComboBox.SelectedItem == null || _allFeaturedMods == null)
                return;

            string selectedCategory = CategoryComboBox.SelectedItem.ToString();
            ObservableCollection<FeaturedModInfo> filteredMods;

            if (selectedCategory == "All Categories")
            {
                // Show all mods
                filteredMods = _allFeaturedMods;
            }
            else
            {
                // Filter by selected category
                filteredMods = new ObservableCollection<FeaturedModInfo>(
                    _allFeaturedMods.Where(m => m.Category == selectedCategory)
                );
            }

            // Update the ItemsSource
            FeaturedModsItemsControl.ItemsSource = filteredMods;

            // Adjust layout and reset scroll position
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AdjustFeaturedModsLayout();
                FeaturedModsScrollViewer.ScrollToTop();
            }), DispatcherPriority.Loaded);
        }



        private void HorizontalScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // Check if Shift key is pressed - explicit horizontal scroll
                bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                if (isShiftPressed)
                {
                    // Always scroll horizontally with Shift+wheel
                    double scrollAmount = 50 * (e.Delta > 0 ? -1 : 1);
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollAmount);
                    e.Handled = true;
                }
                else
                {
                    // Let the parent handle vertical scrolling
                    e.Handled = false;
                }
            }
        }





        // Replace the LoadFeaturedModImages method with this simpler version
        private void LoadFeaturedModImages()
        {
            // We'll handle image loading directly in XAML instead
        }


        private void UpdateModCount()
        {
            var enabledCount = InstalledMods?.Count(m => m.IsEnabled) ?? 0;
            var totalCount = InstalledMods?.Count ?? 0;
            ModCountTextBlock.Text = $"{enabledCount}/{totalCount} mods enabled";
        }

        private void UpdateEmptyState()
        {
            EmptyStatePanel.Visibility = InstalledMods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ModsScrollViewer.Visibility = InstalledMods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Navigation Events
        private void ModManagerButton_Click(object sender, RoutedEventArgs e)
        {
            HideAllViews();
            ModManagerView.Visibility = Visibility.Visible;
            UpdateActiveButton(ModManagerButton);
        }


        private void FeaturedModsButton_Click(object sender, RoutedEventArgs e)
        {
            HideAllViews();
            FeaturedModsView.Visibility = Visibility.Visible;
            UpdateActiveButton(FeaturedModsButton);
            RefreshFeatured_Click(this, new RoutedEventArgs());
        }
       

        

        



        


        private void ShowModManagerView()
        {
            ModManagerView.Visibility = Visibility.Visible;
            FeaturedModsView.Visibility = Visibility.Collapsed;
            ModDetailView.Visibility = Visibility.Collapsed;
        }

        private void ShowFeaturedModsView()
        {
            ModManagerView.Visibility = Visibility.Collapsed;
            FeaturedModsView.Visibility = Visibility.Visible;
            ModDetailView.Visibility = Visibility.Collapsed;
        }

        private void ShowModDetailView(FeaturedModInfo mod)
        {
            _selectedFeaturedMod = mod;

            ModManagerView.Visibility = Visibility.Collapsed;
            FeaturedModsView.Visibility = Visibility.Collapsed;
            ModDetailView.Visibility = Visibility.Visible;

            // Populate detail view
            DetailNameText.Text = mod.Name;
            DetailAuthorText.Text = $"By {mod.Author}";
            DetailVersionText.Text = $"Version {mod.Version}";
            DetailDescriptionText.Text = mod.Description;

            // Load images if available
            if (!string.IsNullOrEmpty(mod.BannerUrl))
            {
                DetailBannerImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(mod.BannerUrl));
            }

            if (!string.IsNullOrEmpty(mod.LogoUrl))
            {
                DetailLogoImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(mod.LogoUrl));
            }
        }

        // Game Path Events
        private void BrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Brick Rigs Installation Directory"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var selectedPath = dialog.FileName;

                // Validate that this is a Brick Rigs directory
                if (File.Exists(Path.Combine(selectedPath, "BrickRigs.exe")) ||
                    Directory.Exists(Path.Combine(selectedPath, "BrickRigs")))
                {
                    GamePath = selectedPath;
                    _modManager.SetGamePath(selectedPath);
                    LoadInstalledMods();
                }
                else
                {
                    MessageBox.Show("Invalid Brick Rigs directory. Please select the correct folder.",
                        "Invalid Directory", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // Mod Management Events
        private async void RefreshMods_Click(object sender, RoutedEventArgs e)
        {
            await LoadInstalledMods();
        }

        private async void ImportMod_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Mod File",
                Filter = "Mod Files (*.zip;*.pak;*.pakbundle;*.brmodpack)|*.zip;*.pak;*.pakbundle;*.brmodpack|Zip Files (*.zip)|*.zip|Pak Files (*.pak)|*.pak|PakBundle Files (*.pakbundle)|*.pakbundle|Mod Pack Files (*.brmodpack)|*.brmodpack|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Show progress bar
                    DownloadProgressBar.Value = 0;
                    DownloadProgressBar.Visibility = Visibility.Visible;
                    DownloadStatusText.Text = "Installing mod...";
                    DownloadStatusText.Visibility = Visibility.Visible;

                    var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();

                    if (extension == ".brmodpack")
                    {
                        // Handle mod pack installation
                        await InstallModPack(dialog.FileName);
                    }
                    else if (extension == ".pak")
                    {
                        // Show warning for pak files without meta
                        var metaPath = Path.ChangeExtension(dialog.FileName, ".pakmeta");
                        var altMetaPath = dialog.FileName + "meta";

                        if (!File.Exists(metaPath) && !File.Exists(altMetaPath))
                        {
                            var result = MessageBox.Show(
                                "This PAK file doesn't have a metadata file (.pakmeta). " +
                                "Basic information will be created automatically.\n\n" +
                                "Would you like to continue?",
                                "No Metadata Found",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (result == MessageBoxResult.No)
                            {
                                DownloadProgressBar.Visibility = Visibility.Collapsed;
                                DownloadStatusText.Visibility = Visibility.Collapsed;
                                return;
                            }
                        }

                        // Update progress
                        DownloadProgressBar.Value = 50;
                        await _modManager.InstallModFromFile(dialog.FileName);
                        DownloadProgressBar.Value = 100;
                    }
                    else
                    {
                        // Handle regular mod files
                        DownloadProgressBar.Value = 50;
                        await _modManager.InstallModFromFile(dialog.FileName);
                        DownloadProgressBar.Value = 100;
                    }

                    // Force a refresh of the mod list
                    await Task.Delay(500); // Small delay to ensure file operations are complete
                    await LoadInstalledMods();

                    MessageBox.Show("Mod installed successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error installing mod: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // Explicitly reset and hide progress indicators
                    DownloadProgressBar.Value = 0;
                    DownloadProgressBar.Visibility = Visibility.Collapsed;
                    DownloadStatusText.Text = "";
                    DownloadStatusText.Visibility = Visibility.Collapsed;
                }
            }
        }


        private async Task InstallModPack(string modPackPath)
        {
            string tempDir = null;

            try
            {
                // Show loading indicator
                DownloadProgressBar.Value = 25;
                DownloadStatusText.Text = "Loading mod pack...";

                // Load the mod pack
                var modPack = await ModPack.LoadFromFileAsync(modPackPath);

                // Update progress
                DownloadProgressBar.Value = 50;
                DownloadStatusText.Text = $"Installing {modPack.Mods.Count} mods...";

                // Get the appropriate mods path for folder mods
                string folderModsPath = GetBrickRigsModsPath();

                // Get the appropriate path for pak mods
                string pakModsPath = Path.Combine(GamePathTextBox.Text, "Brick Rigs", "Content", "~mods");
                if (!Directory.Exists(pakModsPath))
                {
                    // Try alternative paths
                    string[] possiblePakPaths = new[]
                    {
                Path.Combine(GamePathTextBox.Text, "BrickRigs", "Content", "Paks", "~mods"),
                Path.Combine(GamePathTextBox.Text, "Content", "Paks", "~mods"),
                Path.Combine(GamePathTextBox.Text, "BrickRigs", "Content", "~mods"),
                Path.Combine(GamePathTextBox.Text, "Content", "~mods")
            };

                    foreach (var path in possiblePakPaths)
                    {
                        if (Directory.Exists(path))
                        {
                            pakModsPath = path;
                            break;
                        }
                    }

                    // If no pak mods directory exists, create one
                    if (!Directory.Exists(pakModsPath))
                    {
                        Directory.CreateDirectory(pakModsPath);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Installing folder mods to: {folderModsPath}");
                System.Diagnostics.Debug.WriteLine($"Installing pak mods to: {pakModsPath}");

                // Install each mod based on its type
                foreach (var mod in modPack.Mods)
                {
                    System.Diagnostics.Debug.WriteLine($"Installing mod: {mod.Name} (Type: {mod.Type})");

                    if (mod.Type == "Folder")
                    {
                        // Install folder mod to the folder mods path
                        string sourcePath = mod.RelativePath;
                        string destPath = Path.Combine(folderModsPath, mod.Name);

                        System.Diagnostics.Debug.WriteLine($"Copying folder mod from {sourcePath} to {destPath}");

                        if (Directory.Exists(destPath))
                            Directory.Delete(destPath, true);

                        Directory.CreateDirectory(destPath);
                        CopyDirectory(sourcePath, destPath);
                    }
                    else if (mod.Type == "Pak" || mod.Type == "PakBundle")
                    {
                        // Install pak mod to the pak mods path
                        string sourcePath = mod.RelativePath;
                        string destPath = Path.Combine(pakModsPath, mod.Name);

                        System.Diagnostics.Debug.WriteLine($"Copying pak mod from {sourcePath} to {destPath}");

                        if (File.Exists(destPath))
                            File.Delete(destPath);

                        File.Copy(sourcePath, destPath, true);

                        // Copy metadata files if they exist
                        if (mod.Type == "Pak")
                        {
                            // Check for .pakmeta format
                            string metaPath = Path.ChangeExtension(sourcePath, ".pakmeta");
                            if (File.Exists(metaPath))
                            {
                                string destMetaPath = Path.ChangeExtension(destPath, ".pakmeta");
                                if (File.Exists(destMetaPath))
                                    File.Delete(destMetaPath);

                                File.Copy(metaPath, destMetaPath, true);
                            }

                            // Check for .pakmeta format (alternate)
                            string altMetaPath = sourcePath + "meta";
                            if (File.Exists(altMetaPath))
                            {
                                string destMetaPath = destPath + "meta";
                                if (File.Exists(destMetaPath))
                                    File.Delete(destMetaPath);

                                File.Copy(altMetaPath, destMetaPath, true);
                            }
                        }
                        else if (mod.Type == "PakBundle")
                        {
                            string bundleIdPath = Path.ChangeExtension(sourcePath, ".pakbundleid");
                            if (File.Exists(bundleIdPath))
                            {
                                string destBundleIdPath = Path.ChangeExtension(destPath, ".pakbundleid");
                                if (File.Exists(destBundleIdPath))
                                    File.Delete(destBundleIdPath);

                                File.Copy(bundleIdPath, destBundleIdPath, true);
                            }
                        }
                    }
                }

                // Update progress
                DownloadProgressBar.Value = 100;
                DownloadStatusText.Text = "Installation complete!";

                // Refresh mod list
                await LoadInstalledMods();

                MessageBox.Show($"Mod pack '{modPack.Name}' installed successfully!\n\nContains {modPack.Mods.Count} mods.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error installing mod pack: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error installing mod pack: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Reset progress bar
                DownloadProgressBar.Value = 0;
                DownloadProgressBar.Visibility = Visibility.Collapsed;
                DownloadStatusText.Text = "";
                DownloadStatusText.Visibility = Visibility.Collapsed;
            }
        }

        // Helper method to copy a directory
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



        


        private async void CreatePakBundle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModInfo mod && mod.IsPakMod)
            {
                var pakFile = Directory.GetFiles(mod.ModDirectory, "*.pak")
                    .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == mod.ModName);

                if (pakFile != null && File.Exists(pakFile))
                {
                    var metaFile = Path.Combine(mod.ModDirectory, $"{mod.ModName}.pakmeta");

                    // If no metadata exists, offer to create it
                    if (!File.Exists(metaFile))
                    {
                        var result = MessageBox.Show(
                            "This PAK mod doesn't have metadata. Would you like to create it before bundling?",
                            "Create Metadata",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            var metaDialog = new PakMetaDialog(mod.ModName);
                            if (metaDialog.ShowDialog() == true)
                            {
                                var pakModInfo = new PakModInfo
                                {
                                    Name = metaDialog.ModName,
                                    Author = metaDialog.ModAuthor,
                                    Version = metaDialog.ModVersion,
                                    Description = metaDialog.ModDescription,
                                    Category = metaDialog.ModCategory
                                };

                                var metaJson = JsonConvert.SerializeObject(pakModInfo, Formatting.Indented);
                                await File.WriteAllTextAsync(metaFile, metaJson);
                            }
                            else
                            {
                                // User canceled metadata creation
                                return;
                            }
                        }
                    }

                    // Ask where to save the bundle
                    var saveDialog = new SaveFileDialog
                    {
                        Title = "Save PakBundle",
                        Filter = "PakBundle Files (*.pakbundle)|*.pakbundle",
                        FileName = $"{mod.ModName}.pakbundle"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        try
                        {
                            // Create a temporary directory
                            var tempDir = Path.Combine(Path.GetTempPath(), $"PakBundle_{Guid.NewGuid()}");
                            Directory.CreateDirectory(tempDir);

                            try
                            {
                                // Copy the pak file
                                var tempPakPath = Path.Combine(tempDir, Path.GetFileName(pakFile));
                                File.Copy(pakFile, tempPakPath);

                                // Copy the meta file if it exists
                                if (File.Exists(metaFile))
                                {
                                    var tempMetaPath = Path.Combine(tempDir, Path.GetFileName(metaFile));
                                    File.Copy(metaFile, tempMetaPath);
                                }

                                // Create the bundle (zip file with .pakbundle extension)
                                if (File.Exists(saveDialog.FileName))
                                {
                                    File.Delete(saveDialog.FileName);
                                }

                                ZipFile.CreateFromDirectory(tempDir, saveDialog.FileName);

                                MessageBox.Show($"PakBundle created successfully at:\n{saveDialog.FileName}",
                                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            finally
                            {
                                // Clean up
                                if (Directory.Exists(tempDir))
                                {
                                    Directory.Delete(tempDir, true);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error creating PakBundle: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private async void RefreshPakMods_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show a loading indicator
                DownloadProgressBar.Value = 50;
                DownloadProgressBar.Visibility = Visibility.Visible;
                DownloadStatusText.Text = "Refreshing pak mods...";
                DownloadStatusText.Visibility = Visibility.Visible;

                // Force a refresh of the mod list
                await LoadInstalledMods();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing pak mods: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                DownloadProgressBar.Visibility = Visibility.Collapsed;
                DownloadStatusText.Visibility = Visibility.Collapsed;
            }
        }
        private async void CreatePakMeta_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModInfo mod)
            {
                // Check if this is a pak mod without metadata
                if (mod.Description == "Pak mod without metadata")
                {
                    var pakFilePath = Directory.GetFiles(mod.ModDirectory, "*.pak")
                        .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == mod.ModName);

                    if (pakFilePath != null)
                    {
                        // Show dialog to create metadata
                        var metaDialog = new PakMetaDialog(mod.ModName);
                        if (metaDialog.ShowDialog() == true)
                        {
                            try
                            {
                                var pakModInfo = new PakModInfo
                                {
                                    Name = metaDialog.ModName,
                                    Author = metaDialog.ModAuthor,
                                    Version = metaDialog.ModVersion,
                                    Description = metaDialog.ModDescription,
                                    Category = metaDialog.ModCategory
                                };

                                var metaJson = JsonConvert.SerializeObject(pakModInfo, Formatting.Indented);
                                var metaPath = Path.Combine(mod.ModDirectory, $"{mod.ModName}.pakmeta");
                                await File.WriteAllTextAsync(metaPath, metaJson);

                                MessageBox.Show("Metadata created successfully!", "Success",
                                    MessageBoxButton.OK, MessageBoxImage.Information);

                                await LoadInstalledMods();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error creating metadata: {ex.Message}", "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Could not find the PAK file for this mod.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }





        private async void DownloadMod_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DownloadModDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _modManager.InstallModFromUrl(dialog.ModUrl);
                    await LoadInstalledMods();
                    MessageBox.Show("Mod downloaded and installed successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error downloading mod: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ToggleMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModInfo mod)
            {
                try
                {
                    // Disable the button during the operation
                    button.IsEnabled = false;

                    // Show a loading indicator or change button text
                    var originalContent = button.Content;
                    button.Content = mod.IsEnabled ? "Disabling..." : "Enabling...";

                    await _modManager.ToggleModEnabled(mod);

                    // Refresh the mod list to show updated status
                    await LoadInstalledMods();

                    // Restore button state
                    button.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error toggling mod: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);

                    // Restore button state
                    button.IsEnabled = true;
                }
            }
        }

        // Add this field to your MainWindow class
        private ListBox FeaturedModsListBox;

        // In your constructor, after InitializeComponent(), add:


        // Add this field to your MainWindow class
        private readonly UpdateService _updateService;
        private readonly string _currentVersion = "V1.0.18.0";

        // Add this to your constructor


        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Other loading code...

            // Check for updates
            await CheckForUpdates();
        }

        // Add this method to check for updates
        private async Task CheckForUpdates(bool showNoUpdateMessage = false)
        {
            try
            {
                var updateInfo = await _updateService.CheckForUpdates();

                if (updateInfo != null)
                {
                    // Show update dialog
                    var updateDialog = new UpdateDialog(updateInfo, _currentVersion, _updateService);
                    updateDialog.Owner = this;
                    updateDialog.ShowDialog();
                }
                else if (showNoUpdateMessage)
                {
                    MessageBox.Show(
                        "You are using the latest version of Brick Rigs Mod Manager.",
                        "No Updates Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {


                if (showNoUpdateMessage)
                {
                    MessageBox.Show(
                        $"Failed to check for updates: {ex.Message}",
                        "Update Check Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        // Add a menu item for manually checking updates


        private void FeaturedModsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Prevent selection in the ListBox
            FeaturedModsListBox.SelectedIndex = -1;
        }

        private async void DeleteMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ModInfo mod)
            {
                var result = MessageBox.Show($"Are you sure you want to delete '{mod.FriendlyName}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _modManager.DeleteMod(mod);
                        await LoadInstalledMods();
                        MessageBox.Show("Mod deleted successfully!", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting mod: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Featured Mods Events
        private async void RefreshFeatured_Click(object sender, RoutedEventArgs e)
        {
            await LoadFeaturedMods();
        }

        private void FeaturedMod_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is FeaturedModInfo mod)
            {
                ShowModDetailView(mod);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            HideAllViews();
            FeaturedModsView.Visibility = Visibility.Visible;
            UpdateActiveButton(FeaturedModsButton);
        }

        private void ApplyTransparencyToUI(bool enableTransparency)
        {
            // Get opacity value based on whether transparency is enabled
            double opacity = enableTransparency ? 0.85 : 1.0;

            // Apply to main containers
            var mainBackground = new SolidColorBrush(Color.FromRgb(37, 37, 38));
            mainBackground.Opacity = opacity;

            // Apply to sidebar
            var sidebarBorder = FindVisualChild<Border>(this, b => b.Name == "" && Grid.GetColumn(b) == 0);
            if (sidebarBorder != null)
            {
                sidebarBorder.Background = mainBackground;
            }

            // Apply to content areas
            ApplyTransparencyToContainer(ModManagerView, opacity);
            ApplyTransparencyToContainer(FeaturedModsView, opacity);
            ApplyTransparencyToContainer(SettingsView, opacity);
            ApplyTransparencyToContainer(ModDetailView, opacity);
        }

        private void ApplyTransparencyToContainer(DependencyObject container, double opacity)
        {
            if (container == null) return;

            // Find all Borders and apply transparency
            foreach (var border in FindVisualChildren<Border>(container))
            {
                if (border.Background is SolidColorBrush brush)
                {
                    Color color = brush.Color;
                    border.Background = new SolidColorBrush(Color.FromArgb(
                        (byte)(opacity * 255),
                        color.R,
                        color.G,
                        color.B));
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate = null) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t && (predicate == null || predicate(t)))
                    return t;

                var result = FindVisualChild<T>(child, predicate);
                if (result != null)
                    return result;
            }

            return null;
        }

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as T;
        }



        protected override void OnClosed(EventArgs e)
        {
            _httpClient?.Dispose();
            base.OnClosed(e);
        }

        

        private void ForceApplyMica()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                // Try both methods
                try
                {
                    // Older API
                    int micaValue = 1;
                    DwmSetWindowAttribute(hwnd, 1029, ref micaValue, sizeof(int));
                }
                catch { }

                try
                {
                    // Newer API
                    int backdropType = 2; // Mica
                    DwmSetWindowAttribute(hwnd, 38, ref backdropType, sizeof(int));
                }
                catch { }

                MessageBox.Show("Mica effect applied using direct method.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying Mica effect: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Add this DllImport at the top of your class



        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            HideAllViews();
            SettingsView.Visibility = Visibility.Visible;
            UpdateActiveButton(SettingsButton);
        }

        private void UpdateActiveButton(Button activeButton)
        {
            // Reset all navigation buttons to default style
            ModManagerButton.Background = Brushes.Transparent;
            FeaturedModsButton.Background = Brushes.Transparent;
            SettingsButton.Background = Brushes.Transparent;

            // Highlight the active button
            if (activeButton != null)
            {
                activeButton.Background = (Brush)new BrushConverter().ConvertFrom("#FF3E3E42");
            }
        }


        private void InitializeSettings()
        {
            // Set the selected theme in the ComboBox
            foreach (ComboBoxItem item in ThemeSelector.Items)
            {
                if (item.Content.ToString() == ThemeManager.CurrentTheme)
                {
                    ThemeSelector.SelectedItem = item;
                    break;
                }
            }
        }

        private void InitializeThemes()
        {
            // Initialize theme manager
            ThemeManager.Initialize();

            // Populate theme combo box
            
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                string themeName = selectedItem.Content.ToString();
                ThemeManager.ApplyTheme(themeName);
            }
        }






        private Color BackgroundColor { get; set; } = Colors.Black;
        // Add properties for other colors



        


        private void ApplyCustomTheme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                
                MessageBox.Show("Custom theme applied and saved.", "Theme", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying custom theme: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        

        private void HideAllViews()
        {
            // Hide all views
            ModManagerView.Visibility = Visibility.Collapsed;
            FeaturedModsView.Visibility = Visibility.Collapsed;
            ModDetailView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
        }

        private void TestMicaButton_Click(object sender, RoutedEventArgs e)
        {
            ForceApplyMica();
        }



        private void ApplyMicaToUI(bool enable)
        {
            // Get all resources
            var resources = this.Resources;

            if (enable)
            {
                // Set semi-transparent brushes
                resources["MicaBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(64, 37, 37, 38)); // #40252526
                resources["MicaBorderBrush"] = new SolidColorBrush(Color.FromArgb(64, 63, 63, 70)); // #403F3F46
                resources["MicaButtonBrush"] = new SolidColorBrush(Color.FromArgb(64, 45, 45, 48)); // #402D2D30
                resources["MicaHeaderBrush"] = new SolidColorBrush(Color.FromArgb(64, 45, 45, 48)); // #402D2D30
                resources["MicaHoverBrush"] = new SolidColorBrush(Color.FromArgb(64, 62, 62, 66)); // #403E3E42
                resources["MicaAccentBrush"] = new SolidColorBrush(Color.FromArgb(64, 0, 122, 204)); // #40007ACC
            }
            else
            {
                // Set solid brushes
                resources["MicaBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(37, 37, 38)); // #FF252526
                resources["MicaBorderBrush"] = new SolidColorBrush(Color.FromRgb(63, 63, 70)); // #FF3F3F46
                resources["MicaButtonBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 48)); // #FF2D2D30
                resources["MicaHeaderBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 48)); // #FF2D2D30
                resources["MicaHoverBrush"] = new SolidColorBrush(Color.FromRgb(62, 62, 66)); // #FF3E3E42
                resources["MicaAccentBrush"] = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // #FF007ACC
            }
        }

        private void ApplySimpleMica()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;

                // Enable dark mode
                int darkMode = 1;
                DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));

                // Try both Mica APIs
                int micaValue = 1;
                DwmSetWindowAttribute(hwnd, 1029, ref micaValue, sizeof(int));

                int backdropType = 2;
                DwmSetWindowAttribute(hwnd, 38, ref backdropType, sizeof(int));

                MessageBox.Show("Simple Mica applied.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {


            await CheckForUpdates(true);
        }


    }
    public static class VisualTreeHelperExtensions
    {
        public static T FindAncestor<T>(this DependencyObject current) where T : DependencyObject
        {
            current = VisualTreeHelper.GetParent(current);

            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }

            return current as T;
        }
    }
    
} 


