using System;
using Godot;

public static class UserSettings
{
  private const string ConfigPath = "user://settings.cfg";
  private const string VideoSection = "video";
  private const string MaxFpsKey = "max_fps";
  private const string VsyncKey = "vsync_enabled";
  private const string FullscreenKey = "fullscreen_enabled";

  private static bool _loaded = false;
  private static int _maxFps = 0;
  private static bool _vsyncEnabled = true;
  private static bool _fullscreenEnabled = false;

  public static event Action<int> MaxFpsChanged;
  public static event Action<bool> VsyncChanged;
  public static event Action<bool> FullscreenChanged;

  public static int MaxFps
  {
    get
    {
      EnsureLoaded();
      return _maxFps;
    }
  }

  public static bool VsyncEnabled
  {
    get
    {
      EnsureLoaded();
      return _vsyncEnabled;
    }
  }

  public static bool FullscreenEnabled
  {
    get
    {
      EnsureLoaded();
      return _fullscreenEnabled;
    }
  }

  public static void EnsureLoaded()
  {
    if (_loaded)
      return;
    Load();
  }

  private static void Load()
  {
    var config = new ConfigFile();
    Error error = config.Load(ConfigPath);
    if (error == Error.Ok)
    {
      object stored = config.GetValue(VideoSection, MaxFpsKey, 0);
      _maxFps = CoerceToInt(stored);
      object vsyncStored = config.GetValue(VideoSection, VsyncKey, true);
      _vsyncEnabled = CoerceToBool(vsyncStored);
      object fullscreenStored = config.GetValue(VideoSection, FullscreenKey, false);
      _fullscreenEnabled = CoerceToBool(fullscreenStored);
    }
    else
    {
      _maxFps = 0;
      _vsyncEnabled = true;
      _fullscreenEnabled = false;
    }

    ApplyVideoSettings();
    _loaded = true;
  }

  public static void SetMaxFps(int value)
  {
    EnsureLoaded();
    int clamped = value <= 0 ? 0 : Math.Clamp(value, 30, 1000);
    if (_maxFps == clamped)
      return;
    _maxFps = clamped;
    ApplyVideoSettings();
    Save();
    MaxFpsChanged?.Invoke(_maxFps);
  }

  public static void SetVsyncEnabled(bool enabled)
  {
    EnsureLoaded();
    if (_vsyncEnabled == enabled)
      return;
    _vsyncEnabled = enabled;
    ApplyVideoSettings();
    Save();
    VsyncChanged?.Invoke(_vsyncEnabled);
  }

  public static void SetFullscreenEnabled(bool enabled)
  {
    EnsureLoaded();
    if (_fullscreenEnabled == enabled)
      return;
    _fullscreenEnabled = enabled;
    ApplyVideoSettings();
    Save();
    FullscreenChanged?.Invoke(_fullscreenEnabled);
  }

  private static void ApplyVideoSettings()
  {
    // Godot treats 0 as unlimited.
    Engine.MaxFps = _maxFps <= 0 ? 0 : Math.Max(1, _maxFps);

    var mode = _vsyncEnabled ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled;
    DisplayServer.WindowSetVsyncMode(mode);

    var windowMode = _fullscreenEnabled ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Maximized;
    DisplayServer.WindowSetMode(windowMode);
  }

  private static void Save()
  {
    var config = new ConfigFile();
    config.Load(ConfigPath);
    config.SetValue(VideoSection, MaxFpsKey, _maxFps);
    config.SetValue(VideoSection, VsyncKey, _vsyncEnabled);
    config.SetValue(VideoSection, FullscreenKey, _fullscreenEnabled);
    config.Save(ConfigPath);
  }

  private static int CoerceToInt(object value)
  {
    return value switch
    {
      int i => i,
      long l => (int)Math.Clamp(l, int.MinValue, int.MaxValue),
      float f => (int)MathF.Round(f),
      double d => (int)Math.Round(d),
      _ => 0
    };
  }

  private static bool CoerceToBool(object value)
  {
    return value switch
    {
      bool b => b,
      int i => i != 0,
      long l => l != 0,
      float f => MathF.Abs(f) > float.Epsilon,
      double d => Math.Abs(d) > double.Epsilon,
      string s when bool.TryParse(s, out bool parsed) => parsed,
      _ => true
    };
  }
}
