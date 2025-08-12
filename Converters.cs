using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BrickRigsModManager
{
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EnabledToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "Enabled" : "Disabled";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EnabledToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Orange);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EnabledToToggleTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "Disable" : "Enable";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EnabledToButtonStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var app = Application.Current;
                if (app != null)
                {
                    if ((bool)value)
                    {
                        // For enabled mods, use the WarningButton style for the Disable button
                        if (app.Resources.Contains("WarningButton"))
                            return app.Resources["WarningButton"];
                    }
                    else
                    {
                        // For disabled mods, use the SuccessButton style for the Enable button
                        if (app.Resources.Contains("SuccessButton"))
                            return app.Resources["SuccessButton"];
                    }
                }

                // Default fallback to ModernButton
                return Application.Current.Resources["ModernButton"];
            }
            catch
            {
                // Ultimate fallback - return null if anything goes wrong
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public class RatingToColorConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is int rating && parameter is string paramStr && int.TryParse(paramStr, out int starPosition))
                {
                    // If the rating is equal to or higher than the star position, show gold star
                    if (rating >= starPosition)
                        return new SolidColorBrush(Colors.Gold);

                    // Otherwise show gray star
                    return new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
                }

                return new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
        public class ProgressBarWidthConverter : IMultiValueConverter
        {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                if (values.Length < 3 ||
                    !double.TryParse(values[0].ToString(), out double value) ||
                    !double.TryParse(values[1].ToString(), out double maximum) ||
                    !double.TryParse(values[2].ToString(), out double actualWidth))
                {
                    return 0;
                }

                if (maximum <= 0)
                    return 0;

                return (value / maximum) * actualWidth;
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }


    }
}
