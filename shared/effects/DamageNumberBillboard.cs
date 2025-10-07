using Godot;

#nullable enable

public partial class DamageNumberBillboard : Node3D
{
  [Export] public int ViewportWidth = 256;
  [Export] public int ViewportHeight = 128;
  [Export] public float PixelSize = 0.0065f;
  [Export] public float PopInTime = 0.08f;
  [Export] public float HoldSeconds = 0.45f;
  [Export] public float FadeOutSeconds = 0.33f;
  [Export] public float BaseScale = 1.0f;
  [Export] public float PulseSpeed = 12.0f;
  [Export] public float PopOutRate = 4.0f;
  [Export(PropertyHint.Range, "0.0,1.0,0.01")] public float BaselineOffsetRatio = 0.12f;
  [Export] public float FloatUpSpeed = 0.9f;

  private SubViewport _viewport = null!;
  private Control _canvas = null!;
  private Label _label = null!;
  private LabelSettings _settings = null!;
  private MeshInstance3D _quad = null!;
  private StandardMaterial3D _material = null!;
  private Vector3 _baseQuadScale = Vector3.One;

  private float _age = 0f;
  private bool _pulseActive = false;
  private float _pulseStart = 0f;
  private float _pulseAmount = 0.2f;
  private bool _popOutActive = false;
  private float _popOutStart = 0f;
  private float _randomTilt = 0f;
  private RandomNumberGenerator _rng = new();
  private Vector3 _spawnPosition = Vector3.Zero;
  private bool _spawnInitialized = false;

  public override void _Ready()
  {
    _rng.Randomize();

    _viewport = new SubViewport
    {
      Size = new Vector2I(ViewportWidth, ViewportHeight),
      TransparentBg = true,
      RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
      RenderTargetClearMode = SubViewport.ClearMode.Always,
    };
    AddChild(_viewport);

    _canvas = new Control
    {
      Size = new Vector2(ViewportWidth, ViewportHeight),
    };
    _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    _viewport.AddChild(_canvas);

    _label = new Label
    {
      Size = new Vector2(ViewportWidth, ViewportHeight),
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
      AutowrapMode = TextServer.AutowrapMode.Off,
    };
    _label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    _settings = new LabelSettings();
    _label.LabelSettings = _settings;
    _canvas.AddChild(_label);

    _material = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
      BillboardKeepScale = true,
      CullMode = BaseMaterial3D.CullModeEnum.Disabled,
      AlbedoColor = Colors.White,
      AlbedoTexture = _viewport.GetTexture(),
      TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
    };

    _quad = new MeshInstance3D
    {
      Mesh = new QuadMesh { Size = Vector2.One },
      MaterialOverride = _material,
    };
    AddChild(_quad);

    _randomTilt = _rng.RandfRange(-0.1f, 0.1f);
    Rotation = Vector3.Zero;
  }

  public void SetSpawnPosition(Vector3 worldPosition)
  {
    _spawnPosition = worldPosition;
    _spawnInitialized = true;
    GlobalPosition = worldPosition;
  }

  public void Configure(string text, Color fill, Color outline, int fontSize, FontFile? font, int outlineSize = 6)
  {
    _settings.Font = font;
    _settings.FontSize = fontSize;
    _settings.FontColor = fill;
    _settings.OutlineColor = outline;
    _settings.OutlineSize = (outline.A > 0.01f && outlineSize > 0) ? outlineSize : 0;
    _label.Text = text;
    UpdateViewportSize(fontSize, text.Length);

    _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
  }

  private void UpdateViewportSize(int fontSize, int length)
  {
    int width = Mathf.Clamp(fontSize * (length + 2), 128, 512);
    int height = Mathf.Clamp(fontSize * 2, 96, 256);

    _viewport.Size = new Vector2I(width, height);
    _canvas.Size = new Vector2(width, height);
    _label.Size = _canvas.Size;

    _material.AlbedoTexture = _viewport.GetTexture();

    float worldWidth = width * PixelSize;
    float worldHeight = height * PixelSize;
    _baseQuadScale = new Vector3(worldWidth, worldHeight, 1f);
    _quad.Scale = _baseQuadScale;
    _quad.Position = new Vector3(0f, worldHeight * BaselineOffsetRatio, 0f);
  }

  public override void _Process(double delta)
  {
    _age += (float)delta;

    if (!_spawnInitialized)
    {
      _spawnPosition = GlobalPosition;
      _spawnInitialized = true;
    }

    if (FloatUpSpeed > 0f)
    {
      GlobalPosition = _spawnPosition + Vector3.Up * (FloatUpSpeed * _age);
    }

    float t = Mathf.Clamp(_age / Mathf.Max(0.001f, PopInTime), 0f, 1f);
    float ease = 1f - Mathf.Pow(1f - t, 3f);
    float scale = Mathf.Lerp(0.35f, 1f, ease);

    if (_pulseActive)
    {
      float pulseT = (_age - _pulseStart) * PulseSpeed;
      float pulse = Mathf.Sin(pulseT) * _pulseAmount;
      scale *= 1f + Mathf.Max(0f, pulse);
      if (pulseT > Mathf.Pi)
        _pulseActive = false;
    }

    float alpha = 1f;
    if (_age > HoldSeconds)
    {
      float fade = Mathf.Clamp((_age - HoldSeconds) / Mathf.Max(0.001f, FadeOutSeconds), 0f, 1f);
      alpha = 1f - fade;
    }

    if (!_popOutActive && _age > HoldSeconds)
    {
      _popOutActive = true;
      _popOutStart = _age;
    }

    float scaleFactor = BaseScale * scale;
    if (_popOutActive)
    {
      float popElapsed = _age - _popOutStart;
      float pop = Mathf.Clamp(1f - popElapsed * Mathf.Max(0.001f, PopOutRate), 0f, 1f);
      float easePop = pop * pop;
      scaleFactor *= easePop;
      alpha *= easePop;
      if (easePop <= 0.002f)
      {
        QueueFree();
        return;
      }
    }

    _quad.Scale = _baseQuadScale * scaleFactor;
    _material.AlbedoColor = new Color(1f, 1f, 1f, alpha);
    _quad.Position = new Vector3(0f, (_baseQuadScale.Y * scaleFactor) * BaselineOffsetRatio, 0f);

    Rotation = new Vector3(0f, 0f, _randomTilt + Mathf.Sin(_age * 4f) * 0.03f);

    if (!_popOutActive && _age > HoldSeconds + FadeOutSeconds)
      QueueFree();
  }

  public void TriggerPulse(float amount)
  {
    _pulseAmount = amount;
    _pulseStart = _age;
    _pulseActive = true;
  }
}
