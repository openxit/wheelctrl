using G29ControlCenter.ViewModels;

namespace G29ControlCenter.Localization;

public enum AppLanguage
{
    De = 0,
    En = 1
}

/// <summary>
/// Zentrale, zur Laufzeit umschaltbare Lokalisierung für Deutsch und Englisch.
/// </summary>
public sealed class LocalizationStrings : ObservableObject
{
    private AppLanguage _language = AppLanguage.En;

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (SetProperty(ref _language, value))
            {
                OnPropertyChanged(nameof(IsGerman));
                OnPropertyChanged(nameof(IsEnglish));
                OnPropertyChanged(string.Empty);
            }
        }
    }

    public bool IsGerman
    {
        get => _language == AppLanguage.De;
        set
        {
            if (value)
            {
                Language = AppLanguage.De;
            }
        }
    }

    public bool IsEnglish
    {
        get => _language == AppLanguage.En;
        set
        {
            if (value)
            {
                Language = AppLanguage.En;
            }
        }
    }

    private bool En => _language == AppLanguage.En;

    public string WindowTitle => "WheelCtrl";
    public string AppTitle => "WheelCtrl";
    public string AppSubtitle => En
        ? "Open-Source Racing Hardware Hub & Telemetry"
        : "Open-Source Racing Hardware Hub & Telemetrie";

    // Top Navigation & Home View
    public string NavHomeTitle => "Home";
    public string BackToHomeButton => En ? "← Home" : "← Startseite";
    public string NavDevicesTitle => En ? "Home" : "Startseite";
    public string NavControlsTitle => En ? "Controls" : "Steuerung";
    public string NavGamepadTitle => En ? "Virtual Gamepad" : "Virtuelles Gamepad";
    public string NavOverlayTitle => En ? "Stream Overlay" : "Stream-Overlay";
    public string HomeEmptyTitle => En ? "No Logitech devices connected" : "Keine Logitech-Geräte verbunden";
    public string HomeEmptyDesc => En
        ? "Connect your Logitech G29 via USB and 24V power supply (PS4 switch position), then click Reconnect."
        : "Schließe dein Logitech G29 per USB und 24V-Netzteil an (Schalter auf PS4) und klicke auf Neu verbinden.";
    public string HomeCardClickHint => En ? "Click card to configure wheel, virtual gamepad & overlay" : "Klicken für Lenkrad, virtuelles Gamepad & Overlay";
    public string HomeOpenControlsButton => En ? "Configure" : "Konfigurieren";
    public string ProfilePrefix => En ? "Profile:" : "Profil:";
    public string ProfileStandardName => En ? "Standard (900°)" : "Standard (900°)";
    public string ProfileDriftName => En ? "Drift / Rally (540°)" : "Drift / Rallye (540°)";
    public string ProfileFormulaName => En ? "GT / Formula (360°)" : "GT / Formel (360°)";

    // Hub Sidebar & Navigation
    public string HubDevicesHeader => En ? "DEVICES" : "GERÄTE";
    public string HubPrimaryDeviceName => "G29 Driving Force";
    public string HubPrimaryDeviceSubConnected => En ? "Connected • FFB Ready" : "Verbunden • FFB bereit";
    public string HubPrimaryDeviceSubWaiting => En ? "Waiting for USB" : "Warte auf USB";
    public string HubExtensibleTitle => En ? "More Devices" : "Weitere Geräte";
    public string HubExtensibleSub => En ? "Modular SDK architecture" : "Modulare SDK-Architektur";
    public string TabControlsTitle => En ? "Controls & Telemetry" : "Steuerung & Telemetrie";
    public string TabOverlayTitle => En ? "Stream Overlay (OBS)" : "Stream-Overlay (OBS)";

    // Stream Overlay Page
    public string OverlayHeaderTitle => En ? "Stream Overlay for OBS Studio (No Plugin Required)" : "Stream-Overlay für OBS Studio (ohne Plugin)";
    public string OverlayHeaderDesc => En
        ? "Show live steering wheel, pedals, RPM LEDs, and gear in OBS Studio without degree numbers or raw values."
        : "Zeigt Lenkrad, Pedale, RPM-LEDs und Gang live in OBS Studio – ohne Gradangaben oder Rohwerte.";
    public string OverlayWindowCardTitle => En ? "Desktop Overlay Window (OBS Window Capture)" : "Desktop-Overlay-Fenster (OBS Fensteraufnahme)";
    public string OverlayOpenButton => En ? "Open Desktop Overlay" : "Desktop-Overlay öffnen";
    public string OverlayCloseButton => En ? "Close Desktop Overlay" : "Desktop-Overlay schließen";
    public string OverlayDragHint => En
        ? "Drag to move • Hold Ctrl + Drag (or Ctrl + Scroll) to resize (40%–250%). Enable 'Click-Through' to lock while gaming."
        : "Zum Verschieben ziehen • Strg + Ziehen (oder Strg + Mausrad) für Größe (40%–250%). 'Klick-Durchlass' sperrt im Spiel.";
    public string OverlayBrowserCardTitle => En ? "OBS Browser Source (True Alpha Transparency)" : "OBS Browser-Quelle (echte Alpha-Transparenz)";
    public string OverlayBrowserEnable => En ? "Enable local Browser Source server" : "Lokalen Browser-Source-Server aktivieren";
    public string OverlayCopyUrlButton => En ? "Copy URL" : "URL kopieren";
    public string OverlayUrlCopiedHint => En ? "Copied! Add as 'Browser' source in OBS Studio." : "Kopiert! In OBS Studio als 'Browser'-Quelle einfügen.";
    public string OverlayAppearanceTitle => En ? "Overlay Elements & Appearance" : "Overlay-Elemente & Darstellung";
    public string OverlayShowWheel => En ? "Steering wheel (no degree scale)" : "Lenkrad (ohne Gradskala)";
    public string OverlayShowPedals => En ? "Transparent pedals (Throttle / Brake / Clutch)" : "Transparente Pedale (Gas / Bremse / Kupplung)";
    public string OverlayShowPedalLabels => En ? "Short pedal letters (T / B / C)" : "Kurze Pedal-Kürzel (T / B / C)";
    public string OverlayShowRpmLeds => En ? "RPM shift LEDs" : "RPM-Schalt-LEDs";
    public string OverlayShowGear => En ? "Shifter gear indicator" : "Shifter-Ganganzeige";
    public string OverlayBackgroundLabel => En ? "Window background:" : "Fenster-Hintergrund:";
    public string OverlayScaleLabel => En ? "Scale" : "Größe";
    public string OverlayOpacityLabel => En ? "Opacity" : "Deckkraft";
    public string OverlayAlwaysOnTop => En ? "Always on top" : "Immer im Vordergrund";
    public string OverlayClickThrough => En ? "Lock position (Click-through)" : "Position sperren (Klick-Durchlass)";
    public string OverlayPreviewTitle => En ? "Live Overlay Preview" : "Live-Overlay-Vorschau";
    public string OverlayBgTransparent => En ? "Transparent (Alpha)" : "Transparent (Alpha)";
    public string OverlayBgChromaGreen => En ? "Chroma Key Green (#00FF00)" : "Chroma-Key Grün (#00FF00)";
    public string OverlayBgChromaBlue => En ? "Chroma Key Blue (#0000FF)" : "Chroma-Key Blau (#0000FF)";
    public string OverlayBgDark => En ? "Dark Solid (#181A1F)" : "Dunkel (#181A1F)";

    public string ReconnectButton => En ? "Reconnect" : "Neu verbinden";
    public string StopAllMotorsButton => En ? "Stop All Motors" : "Alle Motoren stoppen";
    public string CombinedPedalsBanner => En
        ? "Note: 'Combined Pedals' is enabled in Logitech G HUB. Disable it for independent throttle and brake axes."
        : "Hinweis: In Logitech G HUB ist 'Kombinierte Pedale' aktiviert. Deaktivieren Sie diese Option für unabhängige Gas- und Bremsachsen.";

    // Status Badges
    public string DeviceConnectedPrefix => En ? "Device" : "Gerät";
    public string DeviceWaiting => En
        ? "Device: Not detected — check USB / PS4 switch / G HUB"
        : "Gerät: Nicht erkannt — USB / PS4-Schalter / G HUB prüfen";
    public string DeviceDriverMissing => En
        ? "Device: Logitech G HUB / SDK driver missing"
        : "Gerät: Logitech G HUB / SDK-Treiber fehlt";
    public string DeviceInitializing => En ? "Device: Initializing..." : "Gerät: Initialisiere...";

    public string FocusActive => En ? "Focus: Active (FFB ready)" : "Fokus: Aktiv (FFB bereit)";
    public string FocusPaused => En ? "Focus: Window in background (FFB paused)" : "Fokus: Fenster im Hintergrund (FFB pausiert)";

    public string PowerVerified => En ? "24V Motors: Response OK" : "24V-Motoren: Reaktion OK";
    public string PowerNoResponse => En ? "24V Motors: No response!" : "24V-Motoren: Keine Reaktion!";
    public string PowerReady => En ? "24V Motors: Ready" : "24V-Motoren: Bereit";
    public string PowerDisconnected => En ? "24V Motors: Disconnected" : "24V-Motoren: Getrennt";

    // Column 1: Steering
    public string SteeringSectionTitle => En ? "Steering Control & Angle Telemetry" : "Lenkrad-Steuerung & Winkel-Telemetrie";
    public string OperatingRangePrefix => En ? "Range:" : "Arbeitsbereich:";
    public string ModeFreewheel => En ? "Read (Freewheel / Telemetry)" : "Lesen (Freilauf / Telemetrie)";
    public string ModeMotorPositioning => En ? "Motor Positioning (Target Angle)" : "Motor-Positionierung (Zielwinkel)";
    public string WheelDragTooltip => En
        ? "Draggable with the mouse in 'Motor Positioning' mode to steer the physical G29."
        : "Im Modus 'Motor-Positionierung' per Maus drehbar, um das echte G29 anzufahren.";

    public string ActualAngleLabel => En ? "ACTUAL ANGLE" : "IST-WINKEL";
    public string DeflectionLabel => En ? "DEFLECTION" : "AUSLENKUNG";
    public string RawValueLabel => En ? "16-BIT RAW (lX)" : "16-BIT ROHWERT (lX)";
    public string GentleDampingLabel => En ? "Gentle freewheel damping" : "Sanfte Freilauf-Dämpfung";
    public string RangeLabel => En ? "Operating range:" : "Arbeitsbereich:";
    public string TargetAngleTitle => En ? "Target Angle" : "Zielwinkel-Vorgabe";
    public string ApplyTargetButton => En ? "Move" : "Anfahren";
    public string CenterPresetButton => En ? "0° Center" : "0° Mitte";

    // Column 2: FFB Lab & Controller
    public string FfbLabTitle => En ? "Force Feedback Effect Lab" : "Force-Feedback-Effekt-Labor";
    public string SpringTitle => En ? "Spring Force (Centering Spring)" : "Spring Force (Rückstellfeder)";
    public string SpringOffsetLabel => En ? "Center" : "Mittelpunkt";
    public string SpringSaturationLabel => En ? "Saturation" : "Sättigung";
    public string SpringCoefficientLabel => En ? "Stiffness" : "Federhärte";
    public string DamperTitle => En ? "Damper Force (Viscosity / Resistance)" : "Damper Force (Zähigkeit / Lenkwiderstand)";
    public string DamperResistanceLabel => En ? "Resistance" : "Widerstand";
    public string BumpyTitle => En ? "Bumpy Road (Cobblestone Vibration)" : "Bumpy Road (Kopfsteinpflaster-Rütteln)";
    public string DirtTitle => En ? "Dirt Road (Gravel Vibration)" : "Dirt Road (Schotter-Vibration)";
    public string IntensityLabel => En ? "Intensity" : "Intensität";
    public string CollisionLabel => En ? "Collision" : "Kollision";
    public string CollisionButton => En ? "Trigger impulse" : "Impuls auslösen";

    public string ControllerSectionTitle => En ? "Position Controller & Force Limit" : "Positionsregler & Kraft-Begrenzung";
    public string ResetDefaultsButton => En ? "Defaults" : "Standardwerte";
    public string MaxForceLabel => En ? "Max Motor Force (Limit)" : "Max. Motorkraft (Limit)";
    public string KpLabel => En ? "Proportional (Kp)" : "Proportional (Kp)";
    public string KdLabel => En ? "Damping (Kd)" : "Dämpfung (Kd)";
    public string DeadzoneLabel => En ? "Deadzone" : "Totzone";

    // Column 3: Pedals, LEDs, Shifter, Buttons
    public string PedalsSectionTitle => En ? "Pedals & Calibration" : "Pedale & Kalibrierung";
    public string AutoMinMaxLabel => "Auto-Min/Max";
    public string ResetButton => "Reset";
    public string GasLabel => En ? "Throttle" : "Gas";
    public string BrakeLabel => En ? "Brake" : "Bremse";
    public string ClutchLabel => En ? "Clutch" : "Kupplung";
    public string PedalCouplingLabel => En ? "Pedal coupling:" : "Pedal-Kopplung:";
    public string CouplingNone => En ? "None" : "Keine Kopplung";
    public string CouplingGasToAngle => En ? "Throttle → Target Angle" : "Gas → Zielwinkel";
    public string CouplingGasToBumpy => En ? "Throttle → Bumpy Vibration" : "Gas → Bumpy-Vibration";

    public string RpmSectionTitle => En ? "RPM Shift LEDs (LogiPlayLeds)" : "RPM-Shift-LEDs (LogiPlayLeds)";
    public string RpmManual => En ? "Manual" : "Manuell";
    public string RpmGasCoupled => En ? "Throttle Coupled" : "Gaspedal-Kopplung";

    public string ButtonsSectionTitle => En ? "Buttons, D-Pad & H-Shifter (25 Inputs)" : "Tasten, D-Pad & H-Shifter (25 Tasten)";
    public string DPadPrefix => "D-Pad (POV):";
    public string ShifterPrefix => En ? "Shifter Gear:" : "Shifter-Gang:";

    // Virtual Gamepad (Xbox 360 Translator)
    public string GamepadHeaderTitle => En ? "Virtual Xbox 360 Gamepad (Live Translator)" : "Virtuelles Xbox-360-Gamepad (Live-Übersetzer)";
    public string GamepadHeaderDesc => En
        ? "Emulates a native Windows Xbox 360 controller from your G29 so games without steering wheel support recognize it as a real gamepad."
        : "Emuliert ein echtes Windows-Xbox-360-Gamepad aus deinem G29 – ideal für Spiele ohne Lenkrad-Unterstützung.";
    public string GamepadEnableToggle => En ? "Enable Virtual Xbox 360 Controller" : "Virtuellen Xbox-360-Controller aktivieren";
    public string GamepadInstallDriverButton => En ? "Install ViGEmBus Driver (1-Click)" : "ViGEmBus-Treiber installieren (1-Klick)";
    public string GamepadInstallingDriver => En ? "Installing ViGEmBus..." : "Installiere ViGEmBus...";
    public string GamepadRecheckDriverButton => En ? "Re-check Driver" : "Treiber prüfen";
    public string GamepadDriverReady => En ? "Driver: ViGEmBus Ready (Native XInput)" : "Treiber: ViGEmBus bereit (Natives XInput)";
    public string GamepadDriverMissing => En ? "Driver: ViGEmBus missing (required once for Windows virtual gamepad)" : "Treiber: ViGEmBus fehlt (einmalig für virtuelles Windows-Gamepad nötig)";
    public string GamepadLatencyTitle => En ? "Live Translation Latency" : "Live-Übersetzungslatenz";
    public string GamepadAxisMappingTitle => En ? "Steering & Pedal Axis Translation" : "Lenkrad- & Pedal-Achsenübersetzung";
    public string GamepadSteeringLockLabel => En ? "Steering Lock (Full Stick):" : "Lenkeinschlag (Vollausschlag):";
    public string GamepadSteeringDeadzoneLabel => En ? "Center Deadzone:" : "Mittel-Totzone:";
    public string GamepadSteeringLinearityLabel => En ? "Steering Curve:" : "Lenkkurve (Linearität):";
    public string GamepadInvertSteeringLabel => En ? "Invert Steering (Left/Right)" : "Lenkung invertieren (Links/Rechts)";
    public string GamepadGasTargetLabel => En ? "Throttle Pedal →" : "Gaspedal →";
    public string GamepadBrakeTargetLabel => En ? "Brake Pedal →" : "Bremspedal →";
    public string GamepadClutchTargetLabel => En ? "Clutch Pedal →" : "Kupplungspedal →";
    public string GamepadMapDPadLabel => En ? "Map Wheel D-Pad → Xbox D-Pad" : "Lenkrad-D-Pad → Xbox-D-Pad";
    public string GamepadPreviewTitle => En ? "Live Virtual Gamepad Output" : "Live-Ausgabe Virtuelles Gamepad";
    public string GamepadButtonMappingTitle => En ? "Custom Button Mapping (25 Wheel Inputs → Xbox 360)" : "Freie Tastenbelegung (25 G29-Tasten → Xbox 360)";
    public string GamepadPresetDefaultButton => En ? "Racing Preset" : "Racing-Standard";
    public string GamepadClearAllButton => En ? "Clear All" : "Alle leeren";
}
