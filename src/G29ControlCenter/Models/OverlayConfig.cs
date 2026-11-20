using System.Windows.Media;
using G29ControlCenter.ViewModels;

namespace G29ControlCenter.Models;

public enum OverlayBackgroundMode
{
    Transparent = 0,
    ChromaGreen = 1,
    ChromaBlue = 2,
    DarkSolid = 3
}

public sealed class OverlayBackgroundOption : ObservableObject
{
    private readonly Func<string> _labelSelector;

    public OverlayBackgroundOption(OverlayBackgroundMode mode, Func<string> labelSelector)
    {
        Mode = mode;
        _labelSelector = labelSelector;
    }

    public OverlayBackgroundMode Mode { get; }
    public string DisplayName => _labelSelector();

    public void RefreshLabel() => OnPropertyChanged(nameof(DisplayName));
}

/// <summary>
/// Konfiguration des OBS-Stream-Overlays pro Gerät (ohne Gradangaben oder Rohwerte).
/// </summary>
public sealed class OverlaySettings : ObservableObject
{
    private bool _showWheel = true;
    private bool _showPedals = true;
    private bool _showRpmLeds = true;
    private bool _showGear = true;
    private bool _showPedalLabels = true;
    private int _scalePercent = 100;
    private int _opacityPercent = 95;
    private OverlayBackgroundMode _backgroundMode = OverlayBackgroundMode.Transparent;
    private bool _alwaysOnTop = true;
    private bool _clickThrough;
    private bool _browserServerEnabled;
    private int _browserServerPort = 49290;

    public bool ShowWheel
    {
        get => _showWheel;
        set => SetProperty(ref _showWheel, value);
    }

    public bool ShowPedals
    {
        get => _showPedals;
        set => SetProperty(ref _showPedals, value);
    }

    public bool ShowRpmLeds
    {
        get => _showRpmLeds;
        set => SetProperty(ref _showRpmLeds, value);
    }

    public bool ShowGear
    {
        get => _showGear;
        set => SetProperty(ref _showGear, value);
    }

    public bool ShowPedalLabels
    {
        get => _showPedalLabels;
        set => SetProperty(ref _showPedalLabels, value);
    }

    public int ScalePercent
    {
        get => _scalePercent;
        set
        {
            if (SetProperty(ref _scalePercent, Math.Clamp(value, 40, 250)))
            {
                OnPropertyChanged(nameof(ScaleFactor));
            }
        }
    }

    public double ScaleFactor => _scalePercent / 100.0;

    public int OpacityPercent
    {
        get => _opacityPercent;
        set
        {
            if (SetProperty(ref _opacityPercent, Math.Clamp(value, 20, 100)))
            {
                OnPropertyChanged(nameof(WindowOpacity));
            }
        }
    }

    public double WindowOpacity => _opacityPercent / 100.0;

    public OverlayBackgroundMode BackgroundMode
    {
        get => _backgroundMode;
        set
        {
            if (SetProperty(ref _backgroundMode, value))
            {
                OnPropertyChanged(nameof(WindowBackgroundBrush));
            }
        }
    }

    public Brush WindowBackgroundBrush => _backgroundMode switch
    {
        OverlayBackgroundMode.ChromaGreen => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00)),
        OverlayBackgroundMode.ChromaBlue => new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF)),
        OverlayBackgroundMode.DarkSolid => new SolidColorBrush(Color.FromRgb(0x18, 0x1A, 0x1F)),
        _ => Brushes.Transparent
    };

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => SetProperty(ref _alwaysOnTop, value);
    }

    public bool ClickThrough
    {
        get => _clickThrough;
        set => SetProperty(ref _clickThrough, value);
    }

    public bool BrowserServerEnabled
    {
        get => _browserServerEnabled;
        set => SetProperty(ref _browserServerEnabled, value);
    }

    public int BrowserServerPort
    {
        get => _browserServerPort;
        set
        {
            if (SetProperty(ref _browserServerPort, Math.Clamp(value, 1024, 65535)))
            {
                OnPropertyChanged(nameof(BrowserSourceUrl));
            }
        }
    }

    public string BrowserSourceUrl => $"http://127.0.0.1:{_browserServerPort}/overlay";
}
