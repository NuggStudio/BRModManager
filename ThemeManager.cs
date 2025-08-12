using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace BrickRigsModManagerWPF
{
    public static class ThemeManager
    {
        // Available themes
        public static readonly Dictionary<string, ThemeColors> Themes = new Dictionary<string, ThemeColors>
        {
            { "Dark", new ThemeColors
                {
                    Name = "Dark",
                    Background = Color.FromRgb(30, 30, 30),
                    BackgroundSecondary = Color.FromRgb(37, 37, 38),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(170, 170, 170),
                    Border = Color.FromRgb(63, 63, 70),
                    Accent = Color.FromRgb(0, 122, 204)
                }
            },
            { "Light (why)", new ThemeColors
                {
                    Name = "Light",
                    Background = Color.FromRgb(240, 240, 240),
                    BackgroundSecondary = Color.FromRgb(230, 230, 230),
                    Foreground = Colors.Black,
                    ForegroundSecondary = Color.FromRgb(80, 80, 80),
                    Border = Color.FromRgb(180, 180, 180),
                    Accent = Color.FromRgb(0, 120, 215)
                }
            },
            { "Blue", new ThemeColors
                {
                    Name = "Blue",
                    Background = Color.FromRgb(20, 30, 40),
                    BackgroundSecondary = Color.FromRgb(30, 40, 55),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(180, 200, 220),
                    Border = Color.FromRgb(50, 70, 90),
                    Accent = Color.FromRgb(65, 150, 255)
                }
            },
            // New themes
            { "Green", new ThemeColors
                {
                    Name = "Green",
                    Background = Color.FromRgb(25, 35, 25),
                    BackgroundSecondary = Color.FromRgb(35, 45, 35),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(180, 220, 180),
                    Border = Color.FromRgb(50, 80, 50),
                    Accent = Color.FromRgb(80, 180, 80)
                }
            },
            { "Purple", new ThemeColors
                {
                    Name = "Purple",
                    Background = Color.FromRgb(35, 25, 40),
                    BackgroundSecondary = Color.FromRgb(45, 35, 50),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(200, 180, 220),
                    Border = Color.FromRgb(70, 50, 80),
                    Accent = Color.FromRgb(150, 80, 200)
                }
            },
            { "Red", new ThemeColors
                {
                    Name = "Red",
                    Background = Color.FromRgb(40, 25, 25),
                    BackgroundSecondary = Color.FromRgb(50, 35, 35),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(220, 180, 180),
                    Border = Color.FromRgb(80, 50, 50),
                    Accent = Color.FromRgb(200, 80, 80)
                }
            },
            { "Orange", new ThemeColors
                {
                    Name = "Orange",
                    Background = Color.FromRgb(40, 30, 20),
                    BackgroundSecondary = Color.FromRgb(50, 40, 30),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(220, 200, 180),
                    Border = Color.FromRgb(80, 60, 40),
                    Accent = Color.FromRgb(240, 140, 0)
                }
            },
            { "Teal", new ThemeColors
                {
                    Name = "Teal",
                    Background = Color.FromRgb(20, 40, 40),
                    BackgroundSecondary = Color.FromRgb(30, 50, 50),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(180, 220, 220),
                    Border = Color.FromRgb(40, 80, 80),
                    Accent = Color.FromRgb(0, 180, 180)
                }
            },
            { "Pink", new ThemeColors
                {
                    Name = "Pink",
                    Background = Color.FromRgb(40, 25, 35),
                    BackgroundSecondary = Color.FromRgb(50, 35, 45),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(220, 180, 200),
                    Border = Color.FromRgb(80, 50, 70),
                    Accent = Color.FromRgb(240, 100, 170)
                }
            },
            { "Yellow", new ThemeColors
                {
                    Name = "Yellow",
                    Background = Color.FromRgb(40, 40, 20),
                    BackgroundSecondary = Color.FromRgb(50, 50, 30),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(220, 220, 180),
                    Border = Color.FromRgb(80, 80, 40),
                    Accent = Color.FromRgb(240, 240, 0)
                }
            },
            { "High Contrast", new ThemeColors
                {
                    Name = "High Contrast",
                    Background = Colors.Black,
                    BackgroundSecondary = Color.FromRgb(20, 20, 20),
                    Foreground = Colors.White,
                    ForegroundSecondary = Color.FromRgb(200, 200, 200),
                    Border = Color.FromRgb(100, 100, 100),
                    Accent = Color.FromRgb(255, 255, 0)
                }
            }
        };

        // Current theme name
        public static string CurrentTheme { get; private set; } = "Dark";

        // Apply theme
        public static void ApplyTheme(string themeName)
        {
            if (!Themes.ContainsKey(themeName))
                themeName = "Dark";

            CurrentTheme = themeName;
            var theme = Themes[themeName];

            // Update resource dictionary
            var resources = Application.Current.Resources;

            // Update brushes
            resources["BackgroundBrush"] = new SolidColorBrush(theme.Background);
            resources["BackgroundSecondaryBrush"] = new SolidColorBrush(theme.BackgroundSecondary);
            resources["ForegroundBrush"] = new SolidColorBrush(theme.Foreground);
            resources["ForegroundSecondaryBrush"] = new SolidColorBrush(theme.ForegroundSecondary);
            resources["BorderBrush"] = new SolidColorBrush(theme.Border);
            resources["AccentBrush"] = new SolidColorBrush(theme.Accent);

            // Save theme to settings
            AppSettings.Current.Theme = themeName;
            AppSettings.Current.Save();
        }

        // Initialize
        public static void Initialize()
        {
            // Apply saved theme
            ApplyTheme(AppSettings.Current.Theme);
        }
    }

    // Theme colors class
    public class ThemeColors
    {
        public string Name { get; set; }
        public Color Background { get; set; }
        public Color BackgroundSecondary { get; set; }
        public Color Foreground { get; set; }
        public Color ForegroundSecondary { get; set; }
        public Color Border { get; set; }
        public Color Accent { get; set; }
    }
}
