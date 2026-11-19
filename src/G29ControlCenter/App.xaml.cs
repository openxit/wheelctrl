using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace G29ControlCenter;

public partial class App : Application
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplySystemSurfacePalette();
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
        base.OnExit(e);
    }

    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
        {
            Dispatcher.BeginInvoke(ApplySystemSurfacePalette);
        }
    }

    private void ApplySystemSurfacePalette()
    {
        bool isLight = IsWindowsLightTheme();

        if (isLight)
        {
            SetBrush("SurfaceBackgroundBrush", "#F3F3F3");
            SetBrush("SurfaceCardBrush", "#FFFFFF");
            SetBrush("SurfaceSubtleBrush", "#EBEBEB");
            SetBrush("CardBorderBrush", "#E0E0E0");
            SetBrush("PrimaryTextBrush", "#1A1A1A");
            SetBrush("SecondaryTextBrush", "#5C5C5C");
            SetBrush("WheelRimBrush", "#242424");
            SetBrush("WheelSpokeBrush", "#4E4E4E");
            SetBrush("WheelTrackBrush", "#DCDCDC");
            SetBrush("LedOffBrush", "#D4D4D4");

            SetBrush("BadgeOkBackgroundBrush", "#EAF5EC");
            SetBrush("BadgeOkBorderBrush", "#A7D7B2");
            SetBrush("BadgeOkDotBrush", "#107C10");

            SetBrush("BadgeWarnBackgroundBrush", "#FFF7E6");
            SetBrush("BadgeWarnBorderBrush", "#E5C07B");
            SetBrush("BadgeWarnDotBrush", "#C87E00");

            SetBrush("BadgeErrorBackgroundBrush", "#FDECEA");
            SetBrush("BadgeErrorBorderBrush", "#E5A29B");
            SetBrush("BadgeErrorDotBrush", "#C42B1C");
        }
        else
        {
            SetBrush("SurfaceBackgroundBrush", "#202020");
            SetBrush("SurfaceCardBrush", "#2B2B2B");
            SetBrush("SurfaceSubtleBrush", "#333333");
            SetBrush("CardBorderBrush", "#383838");
            SetBrush("PrimaryTextBrush", "#F3F3F3");
            SetBrush("SecondaryTextBrush", "#A8A8A8");
            SetBrush("WheelRimBrush", "#E2E2E2");
            SetBrush("WheelSpokeBrush", "#9A9A9A");
            SetBrush("WheelTrackBrush", "#424242");
            SetBrush("LedOffBrush", "#3E3E3E");

            SetBrush("BadgeOkBackgroundBrush", "#1D3322");
            SetBrush("BadgeOkBorderBrush", "#2E5A38");
            SetBrush("BadgeOkDotBrush", "#4EC969");

            SetBrush("BadgeWarnBackgroundBrush", "#3A2E17");
            SetBrush("BadgeWarnBorderBrush", "#6B5121");
            SetBrush("BadgeWarnDotBrush", "#F2B134");

            SetBrush("BadgeErrorBackgroundBrush", "#3B1E1C");
            SetBrush("BadgeErrorBorderBrush", "#6E312B");
            SetBrush("BadgeErrorDotBrush", "#F17066");
        }
    }

    private void SetBrush(string key, string hexColor)
    {
        var color = (Color)ColorConverter.ConvertFromString(hexColor);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Resources[key] = brush;
    }

    private static bool IsWindowsLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            object? value = key?.GetValue("AppsUseLightTheme");
            if (value is int intVal)
            {
                return intVal != 0;
            }
        }
        catch
        {
            // Fallback auf helles Windows-11-Standardtheme
        }

        return true;
    }
}
