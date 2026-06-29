using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace DigitalCoreAnalyser.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool flag = (bool)value;

            if (parameter?.ToString() == "invert")
                flag = !flag;

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return null;
        }
    }
}