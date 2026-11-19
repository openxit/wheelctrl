using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace G29ControlCenter.Converters;

public enum BadgeSeverity
{
    Neutral = 0,
    Ok = 1,
    Warning = 2,
    Error = 3
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        if (invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool visible = value is Visibility v && v == Visibility.Visible;
        bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        return invert ? !visible : visible;
    }
}

public sealed class EnumEqualsBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        string? valueStr = value.ToString();
        string? paramStr = parameter.ToString();
        return string.Equals(valueStr, paramStr, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is not null)
        {
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, parameter.ToString()!, ignoreCase: true);
            }

            if (targetType == typeof(int) && int.TryParse(parameter.ToString(), out int intVal))
            {
                return intVal;
            }
        }

        return Binding.DoNothing;
    }
}

public sealed class BadgeSeverityToBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        BadgeSeverity severity = value is BadgeSeverity s ? s : BadgeSeverity.Neutral;
        string resourceKey = severity switch
        {
            BadgeSeverity.Ok => "BadgeOkBackgroundBrush",
            BadgeSeverity.Warning => "BadgeWarnBackgroundBrush",
            BadgeSeverity.Error => "BadgeErrorBackgroundBrush",
            _ => "SurfaceSubtleBrush"
        };

        return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BadgeSeverityToBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        BadgeSeverity severity = value is BadgeSeverity s ? s : BadgeSeverity.Neutral;
        string resourceKey = severity switch
        {
            BadgeSeverity.Ok => "BadgeOkBorderBrush",
            BadgeSeverity.Warning => "BadgeWarnBorderBrush",
            BadgeSeverity.Error => "BadgeErrorBorderBrush",
            _ => "CardBorderBrush"
        };

        return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BadgeSeverityToDotConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        BadgeSeverity severity = value is BadgeSeverity s ? s : BadgeSeverity.Neutral;
        string resourceKey = severity switch
        {
            BadgeSeverity.Ok => "BadgeOkDotBrush",
            BadgeSeverity.Warning => "BadgeWarnDotBrush",
            BadgeSeverity.Error => "BadgeErrorDotBrush",
            _ => "SecondaryTextBrush"
        };

        return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BipolarForceWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int forcePercent)
        {
            return 0.0;
        }

        double maxHalfWidth = 70.0;
        if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            maxHalfWidth = parsed;
        }

        return Math.Clamp(Math.Abs(forcePercent) / 100.0, 0.0, 1.0) * maxHalfWidth;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
