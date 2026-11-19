# WheelCtrl

**WheelCtrl** is an open-source Windows 11 hardware hub (**C# / .NET 9 WPF, x64**) for real-time telemetry, bidirectional force-feedback control, and plugin-free **OBS Studio stream overlays** for racing wheels (currently supporting **Logitech G29 Driving Force**).

---

## Key Features

- **Hardware Hub Architecture:**
  - Clean native Windows 11 interface adapting automatically to System Light/Dark mode and Windows accent color, with a live **EN / DE** language switcher (English by default).
  - Extensible device sidebar designed to manage multiple hardware modules from one place.
- **OBS Studio Stream Overlay (No OBS Plugin Required):**
  - **OBS Browser Source (True Alpha Transparency):** Built-in local HTTP server (`http://127.0.0.1:49290/overlay`) for direct use as an OBS Browser Source with zero background.
  - **Desktop Overlay Window (Window Capture):** Frameless, draggable window supporting *Transparent (Alpha)*, *Chroma Key Green (`#00FF00`)*, *Chroma Key Blue (`#0000FF`)*, *Always on Top*, and *Click-Through* lock.
  - Displays a clean steering wheel (no degree scale or numbers), transparent Throttle/Brake/Clutch bars, RPM shift LEDs, and shifter gear.
- **Bidirectional Steering & Force Feedback Lab:**
  - **Read (Freewheel / Telemetry):** Live steering angle, deflection percentage, 16-bit raw `lX`, and configurable operating range (`180°`, `360°`, `540°`, `900°`).
  - **Motor Positioning:** Drag the wheel with the mouse, use the slider, type an exact angle, or click quick presets (`-450°` to `+450°`) driven by a 100 Hz anti-windup PI-D controller.
  - **FFB Effect Lab:** Test *Spring Force*, *Damper Force*, *Bumpy Road*, *Dirt Road*, and *Frontal Collision* impulse.
- **Pedals, H-Shifter, Buttons & RPM LEDs:**
  - Independent Throttle, Brake, and Clutch bars (with optional *Auto-Min/Max* calibration and throttle-to-wheel/vibration coupling).
  - Live 25-button matrix, 8-way D-Pad (`POV`), H-Pattern Shifter (`R`, `N`, `1–6`), and `LogiPlayLeds` RPM bar.
  - Instant **Stop All Motors** button (also bound to `Space` and `Escape`).

---

## Download & Run (Single `.exe`)

Download the standalone **`WheelCtrl.exe`** from [GitHub Releases](https://github.com/openxit/wheelctrl/releases) — no .NET runtime installation required.

**Hardware Requirements:**
- Windows 11 (x64)
- **Logitech G HUB** installed (WheelCtrl automatically resolves `logi_steering_wheel_x64.dll` via the Windows registry and starts G HUB in the background if needed)
- 24V power supply connected to the wheel base; top mode switch set to **PS4**

---

## Build from Source

```powershell
dotnet restore G29ControlCenter.sln -p:Platform=x64
dotnet test tests/G29ControlCenter.Tests/G29ControlCenter.Tests.csproj -c Release -p:Platform=x64
dotnet publish src/G29ControlCenter/G29ControlCenter.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

---

## Legal & Trademarks

WheelCtrl is an independent open-source project and is not affiliated with, endorsed by, or sponsored by Logitech. All product names are used strictly to identify hardware compatibility.

## License

[MIT](LICENSE)
