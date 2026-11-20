using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using G29ControlCenter.Hardware;
using G29ControlCenter.Models;

namespace G29ControlCenter.Overlay;

/// <summary>
/// Eingebauter lokaler HTTP-Server für OBS Studio Browser Source (kein OBS-Plugin erforderlich).
/// Liefert unter /overlay eine transparente HTML/SVG-Darstellung ohne Grad- oder Rohwert-Texte.
/// </summary>
public sealed class OverlayHttpServer : IDisposable
{
    private readonly IWheelHardwareService _hardwareService;
    private readonly OverlaySettings _settings;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;
    private bool _disposed;

    public OverlayHttpServer(IWheelHardwareService hardwareService, OverlaySettings settings)
    {
        _hardwareService = hardwareService ?? throw new ArgumentNullException(nameof(hardwareService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool IsRunning => _listener?.IsListening == true;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning)
        {
            return;
        }

        string prefix = $"http://127.0.0.1:{_settings.BrowserServerPort}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        _listener = listener;
        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;
        _acceptTask = Task.Run(() => AcceptLoopAsync(listener, token), token);
    }

    public void Stop()
    {
        if (_listener is null)
        {
            return;
        }

        try
        {
            _cts?.Cancel();
            _listener.Stop();
            _listener.Close();
        }
        catch
        {
            // Listener-Abbruch beim Stoppen ignorieren
        }
        finally
        {
            _listener = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested && listener.IsListening)
        {
            try
            {
                HttpListenerContext ctx = await listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequest(ctx), token);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            string path = ctx.Request.Url?.AbsolutePath ?? "/";
            if (path.Equals("/api/state", StringComparison.OrdinalIgnoreCase))
            {
                WheelTelemetrySnapshot s = _hardwareService.LatestSnapshot;
                var payload = new
                {
                    angle = Math.Round(s.WheelAngleDegrees, 2),
                    gas = Math.Round(s.GasPercent, 1),
                    brake = Math.Round(s.BrakePercent, 1),
                    clutch = Math.Round(s.ClutchPercent, 1),
                    rpm = Math.Round(s.RpmLedNormalized, 3),
                    gear = s.ShifterGear,
                    leftPaddle = s.Buttons.Length > 5 && s.Buttons[5],
                    rightPaddle = s.Buttons.Length > 4 && s.Buttons[4],
                    showWheel = _settings.ShowWheel,
                    showPedals = _settings.ShowPedals,
                    showRpm = _settings.ShowRpmLeds,
                    showGear = _settings.ShowGear,
                    showLabels = _settings.ShowPedalLabels,
                    scale = _settings.ScaleFactor,
                    opacity = _settings.WindowOpacity
                };

                byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.Headers["Cache-Control"] = "no-store";
                ctx.Response.ContentLength64 = jsonBytes.Length;
                ctx.Response.OutputStream.Write(jsonBytes, 0, jsonBytes.Length);
            }
            else
            {
                byte[] htmlBytes = Encoding.UTF8.GetBytes(OverlayHtmlDocument);
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.Headers["Cache-Control"] = "no-store";
                ctx.Response.ContentLength64 = htmlBytes.Length;
                ctx.Response.OutputStream.Write(htmlBytes, 0, htmlBytes.Length);
            }
        }
        catch
        {
            // Verbindungsabbruch durch Client ignorieren
        }
        finally
        {
            try
            {
                ctx.Response.OutputStream.Close();
            }
            catch
            {
                // Ignorieren
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private const string OverlayHtmlDocument = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title>WheelCtrl Stream Overlay</title>
<style>
  html, body {
    margin: 0;
    padding: 0;
    background: transparent;
    overflow: hidden;
    font-family: 'Segoe UI', system-ui, sans-serif;
    user-select: none;
  }
  #root {
    display: inline-flex;
    flex-direction: column;
    align-items: center;
    gap: 10px;
    padding: 12px;
    transform-origin: top left;
  }
  .leds {
    display: flex;
    gap: 4px;
    align-items: center;
  }
  .led {
    width: 16px;
    height: 8px;
    border-radius: 2px;
    background: rgba(120,120,120,0.28);
    border: 1px solid rgba(255,255,255,0.16);
  }
  .row {
    display: flex;
    align-items: center;
    gap: 18px;
  }
  .pedals {
    display: flex;
    gap: 10px;
    align-items: flex-end;
    height: 150px;
  }
  .pedal-col {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 5px;
  }
  .pedal-track {
    width: 24px;
    height: 128px;
    border-radius: 4px;
    background: rgba(20, 22, 26, 0.45);
    border: 1px solid rgba(255, 255, 255, 0.22);
    position: relative;
    overflow: hidden;
  }
  .pedal-fill {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 0;
    height: 0%;
  }
  .pedal-label {
    font-size: 11px;
    font-weight: 700;
    color: rgba(255,255,255,0.88);
    letter-spacing: 0.5px;
    text-shadow: 0 1px 2px rgba(0,0,0,0.85);
  }
  .gear-box {
    min-width: 44px;
    height: 44px;
    border-radius: 6px;
    background: rgba(20, 22, 26, 0.55);
    border: 1px solid rgba(255, 255, 255, 0.25);
    display: flex;
    align-items: center;
    justify-content: center;
    color: #ffffff;
    font-size: 22px;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
  }
</style>
</head>
<body>
<div id="root">
  <div id="leds" class="leds">
    <div class="led" id="l1"></div><div class="led" id="l2"></div><div class="led" id="l3"></div><div class="led" id="l4"></div><div class="led" id="l5"></div>
    <div style="width:6px"></div>
    <div class="led" id="r5"></div><div class="led" id="r4"></div><div class="led" id="r3"></div><div class="led" id="r2"></div><div class="led" id="r1"></div>
  </div>
  <div class="row">
    <svg id="wheelSvg" width="180" height="180" viewBox="0 0 340 340">
      <g id="wheelGroup" transform="rotate(0 170 170)">
        <path id="lp" d="M 110,126 L 78,118 C 72,117 68,121 68,127 L 68,167 C 68,173 72,177 78,176 L 110,168 Z" fill="rgba(45,48,54,0.85)" stroke="rgba(255,255,255,0.22)" stroke-width="2" />
        <path id="rp" d="M 230,126 L 262,118 C 268,117 272,121 272,127 L 272,167 C 272,173 268,177 262,176 L 230,168 Z" fill="rgba(45,48,54,0.85)" stroke="rgba(255,255,255,0.22)" stroke-width="2" />
        <path d="M 55,155 L 146,158 L 146,184 L 55,181 Z" fill="#464A52" />
        <path d="M 194,158 L 285,155 L 285,181 L 194,184 Z" fill="#464A52" />
        <path d="M 154,186 L 186,186 L 181,284 L 159,284 Z" fill="#464A52" />
        <circle cx="170" cy="170" r="122" fill="none" stroke="#22252A" stroke-width="22" />
        <circle cx="170" cy="170" r="133" fill="none" stroke="rgba(255,255,255,0.22)" stroke-width="1.5" />
        <rect x="163" y="37" width="14" height="22" rx="2" fill="#0078D4" />
        <line x1="170" y1="37" x2="170" y2="59" stroke="#ffffff" stroke-width="1.5" />
        <circle cx="170" cy="170" r="46" fill="#1F2227" stroke="#464A52" stroke-width="3" />
        <circle cx="170" cy="170" r="30" fill="#292D34" stroke="#0078D4" stroke-width="2" />
        <text x="170" y="172" text-anchor="middle" fill="#F5F7FA" font-size="18" font-weight="700">WC</text>
        <text x="170" y="185" text-anchor="middle" fill="#9BA3B0" font-size="6.5" font-weight="600">WHEELCTRL</text>
      </g>
    </svg>
    <div id="pedals" class="pedals">
      <div class="pedal-col">
        <div class="pedal-track"><div id="clutchFill" class="pedal-fill" style="background:#4A86C8"></div></div>
        <div class="pedal-label">C</div>
      </div>
      <div class="pedal-col">
        <div class="pedal-track"><div id="brakeFill" class="pedal-fill" style="background:#D93829"></div></div>
        <div class="pedal-label">B</div>
      </div>
      <div class="pedal-col">
        <div class="pedal-track"><div id="gasFill" class="pedal-fill" style="background:#2EA043"></div></div>
        <div class="pedal-label">T</div>
      </div>
    </div>
    <div id="gearBox" class="gear-box">N</div>
  </div>
</div>
<script>
const root = document.getElementById('root');
const wheelSvg = document.getElementById('wheelSvg');
const wheelGroup = document.getElementById('wheelGroup');
const lp = document.getElementById('lp');
const rp = document.getElementById('rp');
const pedals = document.getElementById('pedals');
const leds = document.getElementById('leds');
const gearBox = document.getElementById('gearBox');
const gasFill = document.getElementById('gasFill');
const brakeFill = document.getElementById('brakeFill');
const clutchFill = document.getElementById('clutchFill');

const pairs = [
  ['l1','r1','#1B8A4B'],
  ['l2','r2','#1B8A4B'],
  ['l3','r3','#D97706'],
  ['l4','r4','#D97706'],
  ['l5','r5','#C42B1C']
];

async function poll() {
  try {
    const r = await fetch('/api/state', { cache: 'no-store' });
    const d = await r.json();
    root.style.transform = `scale(${d.scale})`;
    root.style.opacity = d.opacity;

    wheelSvg.style.display = d.showWheel ? 'block' : 'none';
    pedals.style.display = d.showPedals ? 'flex' : 'none';
    leds.style.display = d.showRpm ? 'flex' : 'none';
    gearBox.style.display = d.showGear ? 'flex' : 'none';

    document.querySelectorAll('.pedal-label').forEach(el => {
      el.style.display = d.showLabels ? 'block' : 'none';
    });

    wheelGroup.setAttribute('transform', `rotate(${d.angle} 170 170)`);
    lp.setAttribute('fill', d.leftPaddle ? '#0078D4' : 'rgba(45,48,54,0.85)');
    rp.setAttribute('fill', d.rightPaddle ? '#0078D4' : 'rgba(45,48,54,0.85)');

    gasFill.style.height = d.gas + '%';
    brakeFill.style.height = d.brake + '%';
    clutchFill.style.height = d.clutch + '%';
    gearBox.textContent = d.gear || 'N';

    const lit = d.rpm >= 0.90 ? 5 : d.rpm >= 0.75 ? 4 : d.rpm >= 0.55 ? 3 : d.rpm >= 0.35 ? 2 : d.rpm >= 0.15 ? 1 : 0;
    pairs.forEach((p, idx) => {
      const color = idx < lit ? p[2] : 'rgba(120,120,120,0.28)';
      document.getElementById(p[0]).style.background = color;
      document.getElementById(p[1]).style.background = color;
    });
  } catch {}
}
setInterval(poll, 16);
poll();
</script>
</body>
</html>
""";
}
