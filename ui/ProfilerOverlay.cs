using Godot;
using System;
using System.Diagnostics;

#nullable enable

public partial class ProfilerOverlay : Control
{
  private Label _label = new();
  private Process _proc = Process.GetCurrentProcess();
  private Stopwatch _wall = new();
  private TimeSpan _lastCpu;
  private TimeSpan _lastWall;
  private int _cores = Math.Max(1, System.Environment.ProcessorCount);
  private Viewport? _viewport;
  private bool _viewportConnected;
  private double _lastCpuPercent;
  private bool _hasCpuSample;
  private const ulong FpsSampleWindowUsec = 250_000;
  private static readonly TimeSpan CpuSampleInterval = TimeSpan.FromSeconds(0.5);
  private ulong _fpsLastSampleUsec;
  private int _fpsSampleFrameCount;
  private double _displayFps;
  private double _displayFrameMs;
  private bool _hasFpsSample;

  public override void _Ready()
  {
    // Draw above most UI but below tooltips, which use 4096
    ZAsRelative = false;
    ZIndex = 4095;

    MouseFilter = MouseFilterEnum.Ignore;

    // Ensure this Control covers the viewport so child anchors work correctly.
    AnchorLeft = 0;
    AnchorTop = 0;
    AnchorRight = 1;
    AnchorBottom = 1;
    OffsetLeft = 0;
    OffsetTop = 0;
    OffsetRight = 0;
    OffsetBottom = 0;
    UpdateViewportSize();
    var viewport = GetViewport();
    if (viewport != null)
    {
      _viewport = viewport;
      viewport.Connect(Viewport.SignalName.SizeChanged, new Callable(this, nameof(OnViewportResized)));
      _viewportConnected = true;
    }

    // Container panel anchored to top-right
    var panel = new PanelContainer();
    panel.MouseFilter = MouseFilterEnum.Ignore;
    panel.AnchorLeft = 1; panel.AnchorTop = 0; panel.AnchorRight = 1; panel.AnchorBottom = 0;
    // Fixed width so it anchors cleanly to the right edge
    const int width = 240;
    panel.CustomMinimumSize = new Vector2(width, 0);
    panel.OffsetRight = -8;       // 8px from right edge
    panel.OffsetTop = 8;          // 8px from top edge
    panel.OffsetLeft = -8 - width; // place left edge width pixels from the right
    panel.SizeFlagsVertical = SizeFlags.ShrinkBegin;

    var style = new StyleBoxFlat();
    style.BgColor = new Color(0, 0, 0, 0.65f);
    style.BorderColor = new Color(0.5f, 0.9f, 0.5f, 0.8f);
    style.BorderWidthTop = 1;
    style.BorderWidthBottom = 1;
    style.BorderWidthLeft = 1;
    style.BorderWidthRight = 1;
    panel.AddThemeStyleboxOverride("panel", style);
    AddChild(panel);

    _label.HorizontalAlignment = HorizontalAlignment.Right;
    _label.VerticalAlignment = VerticalAlignment.Top;
    _label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    _label.SizeFlagsVertical = SizeFlags.ExpandFill;
    _label.MouseFilter = MouseFilterEnum.Ignore;
    _label.AddThemeColorOverride("font_color", Colors.Lime);
    _label.AddThemeFontSizeOverride("font_size", 14);
    panel.AddChild(_label);

    _wall.Start();
    _lastCpu = _proc.TotalProcessorTime;
    _lastWall = _wall.Elapsed;
    _fpsLastSampleUsec = Time.GetTicksUsec();
    _displayFps = Engine.GetFramesPerSecond();
    if (_displayFps > 0)
    {
      _displayFrameMs = 1000.0 / _displayFps;
      _hasFpsSample = true;
    }
    else
    {
      _displayFrameMs = 0.0;
      _hasFpsSample = false;
    }

    SetProcess(true);
  }

  public override void _ExitTree()
  {
    if (_viewportConnected && _viewport != null && GodotObject.IsInstanceValid(_viewport))
    {
      _viewport.Disconnect(Viewport.SignalName.SizeChanged, new Callable(this, nameof(OnViewportResized)));
      _viewportConnected = false;
      _viewport = null;
    }
  }

  public override void _Process(double delta)
  {
    UpdateFpsSample();
    SampleCpuIfDue();
    UpdateText(_hasCpuSample ? _lastCpuPercent : null);
  }

  private void OnViewportResized()
  {
    UpdateViewportSize();
  }

  private void UpdateViewportSize()
  {
    var viewport = GetViewport();
    if (viewport != null)
      Size = viewport.GetVisibleRect().Size;
  }

  private void UpdateFpsSample()
  {
    _fpsSampleFrameCount++;
    var now = Time.GetTicksUsec();
    var elapsedUsec = now - _fpsLastSampleUsec;
    if (elapsedUsec < FpsSampleWindowUsec || _fpsSampleFrameCount <= 0)
      return;

    var elapsedSeconds = elapsedUsec / 1_000_000.0;
    if (elapsedSeconds <= 0)
      return;

    _displayFps = _fpsSampleFrameCount / elapsedSeconds;
    _displayFrameMs = (_displayFps > 0) ? (1000.0 / _displayFps) : 0.0;
    _fpsSampleFrameCount = 0;
    _fpsLastSampleUsec = now;
    _hasFpsSample = _displayFps > 0;
  }

  private void SampleCpuIfDue()
  {
    var wallNow = _wall.Elapsed;
    var wallDelta = wallNow - _lastWall;
    if (wallDelta < CpuSampleInterval)
      return;

    var cpuNow = _proc.TotalProcessorTime;
    var cpuDeltaMs = (cpuNow - _lastCpu).TotalMilliseconds;
    var wallDeltaMs = wallDelta.TotalMilliseconds;

    double cpuPct = 0.0;
    if (wallDeltaMs > 0)
      cpuPct = Math.Clamp((cpuDeltaMs / (wallDeltaMs * _cores)) * 100.0, 0.0, 100.0);

    _lastCpu = cpuNow;
    _lastWall = wallNow;
    _lastCpuPercent = cpuPct;
    _hasCpuSample = true;
  }

  private void UpdateText(double? cpuPct)
  {
    double fps;
    double frameMs;
    if (_hasFpsSample && _displayFps > 0)
    {
      fps = _displayFps;
      frameMs = _displayFrameMs > 0 ? _displayFrameMs : (1000.0 / _displayFps);
    }
    else
    {
      fps = Engine.GetFramesPerSecond();
      frameMs = fps > 0 ? (1000.0 / fps) : 0.0;
    }

    if (double.IsNaN(fps) || double.IsInfinity(fps) || fps < 0)
    {
      fps = 0.0;
    }
    if (double.IsNaN(frameMs) || double.IsInfinity(frameMs) || frameMs < 0)
    {
      frameMs = fps > 0 ? (1000.0 / fps) : 0.0;
    }

    string cpuLine = cpuPct.HasValue ? $"CPU: {cpuPct.Value:0.0}%" : "CPU: --";
    _label.Text = $"FPS: {fps:0.0}\nFrame: {frameMs:0.0} ms\n{cpuLine}";
  }
}
