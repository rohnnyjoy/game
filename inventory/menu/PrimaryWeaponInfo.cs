using Godot;
using System;
using System.Collections.Generic;

public partial class PrimaryWeaponInfo : PanelContainer
{
  private const float LabelColumnWidth = 240f;
  private MarginContainer _root;
  private VBoxContainer _vbox;
  private DynaTextControl _nameText;
  private GridContainer _statsGrid;
  // Module list removed from display

  private Weapon _currentWeapon;
  private InventoryStore _store;

  // Track previous values to animate only the changed stat
  private bool _hasLastStats = false;
  private float _lastDamage, _lastAccPct, _lastBulletSpeed, _lastShotsPerSecond, _lastReloadSec;
  private int _lastMag;
  private Weapon _lastWeaponRef;
  // Also track last displayed strings to key animation to visible changes
  private string _lastDamageText = "";
  private string _lastAccText = "";
  private string _lastBulletText = "";
  private string _lastFireText = "";
  private string _lastReloadText = "";
  private string _lastMagText = "";

  private readonly FontFile _pixelFont = GD.Load<FontFile>("res://assets/fonts/Born2bSportyV2.ttf");

  public override void _Ready()
  {
    BuildUiIfNeeded();

    _store = InventoryStore.Instance;
    if (_store != null)
      _store.StateChanged += OnStoreStateChanged;

    HookWeaponEvents();
    UpdateInfo();
  }

  public override void _ExitTree()
  {
    UnhookWeaponEvents();
    if (_store != null)
      _store.StateChanged -= OnStoreStateChanged;
    base._ExitTree();
  }

  private void BuildUiIfNeeded()
  {
    _root = GetNodeOrNull<MarginContainer>("Root");
    if (_root != null)
      return;

    _root = new MarginContainer { Name = "Root" };
    AddChild(_root);
    _root.SetAnchorsPreset(LayoutPreset.FullRect);
    // Inner padding so text doesn’t touch the frame
    _root.AddThemeConstantOverride("margin_left", 32);
    _root.AddThemeConstantOverride("margin_top", 20);
    _root.AddThemeConstantOverride("margin_right", 32);
    _root.AddThemeConstantOverride("margin_bottom", 20);

    _vbox = new VBoxContainer { Name = "VBox" };
    _root.AddChild(_vbox);
    _vbox.SetAnchorsPreset(LayoutPreset.FullRect);
    _vbox.AddThemeConstantOverride("separation", 16);

    // Name header (DynaText)
    _nameText = new DynaTextControl { Name = "Name", FontPx = 88, AmbientRotate = true, AmbientFloat = true, AmbientBump = false };
    _nameText.CustomMinimumSize = new Vector2(0, 96);
    _vbox.AddChild(_nameText);

    // Stats grid: Key,Value | Key,Value per row
    _statsGrid = new GridContainer { Name = "Stats", Columns = 2 };
    _statsGrid.AddThemeConstantOverride("h_separation", 28);
    _statsGrid.AddThemeConstantOverride("v_separation", 12);
    _vbox.AddChild(_statsGrid);

    // Pre-create stat cells to keep layout stable
    AddStatRow("Damage:", out _damageVal);
    AddStatRow("Accuracy:", out _accVal);
    AddStatRow("Bullet Spd:", out _bulletVal);
    AddStatRow("Fire Rate:", out _fireVal);
    AddStatRow("Reload:", out _reloadVal);
    AddStatRow("Magazine:", out _magVal);

    // Name gradient set once
    _nameText.SetColours(GetNameColours());

    // Module list intentionally removed from UI
  }

  private void ApplyFont(Control c, int size)
  {
    if (c is Label l)
    {
      if (_pixelFont != null) l.AddThemeFontOverride("font", _pixelFont);
      l.AddThemeFontSizeOverride("font_size", size);
      l.AddThemeColorOverride("font_color", Colors.Black);
    }
  }

  private DynaTextControl _damageVal, _accVal, _bulletVal, _fireVal, _reloadVal, _magVal;

  private void AddStatRow(string key, out DynaTextControl valueLabel)
  {
    // Simple left-aligned key as DynaText (avoid force-expanding wrappers that skew grid layout)
    var k = new DynaTextControl { FontPx = 36, AmbientRotate = true, AmbientFloat = true, AmbientBump = false, CenterInRect = false };
    k.CustomMinimumSize = new Vector2(LabelColumnWidth, 52);
    k.ShadowAlpha = 0.55f;
    k.SetColours(new List<Color> { Colors.White });
    k.SetText(key);
    var v = new DynaTextControl { FontPx = 44, AmbientRotate = true, AmbientFloat = true, AmbientBump = false };
    v.ShadowAlpha = 0.55f;
    v.CustomMinimumSize = new Vector2(0, 52);
    v.CenterInRect = false; // left-align value in its cell
    _statsGrid.AddChild(k);
    _statsGrid.AddChild(v);

    // Fill the second pair in the row only when called twice per row; but we keep simple sequential add.
    valueLabel = v;
  }

  private void OnStoreStateChanged(InventoryState state, ChangeOrigin origin)
  {
    HookWeaponEvents();
    UpdateInfo();
  }

  private void HookWeaponEvents()
  {
    UnhookWeaponEvents();
    var w = Player.Instance?.Inventory?.PrimaryWeapon;
    _currentWeapon = w;
    if (w != null)
    {
      w.ModulesChanged += OnWeaponModulesChanged;
      w.StatsUpdated += OnWeaponStatsUpdated;
    }
  }

  private void UnhookWeaponEvents()
  {
    if (_currentWeapon != null)
    {
      _currentWeapon.ModulesChanged -= OnWeaponModulesChanged;
      _currentWeapon.StatsUpdated -= OnWeaponStatsUpdated;
      _currentWeapon = null;
    }
  }

  private void OnWeaponModulesChanged()
  {
    UpdateInfo();
  }

  private void OnWeaponStatsUpdated()
  {
    UpdateInfo();
  }

  private void UpdateInfo()
  {
    var w = Player.Instance?.Inventory?.PrimaryWeapon;
    if (w == null)
    {
      if (_nameText != null) _nameText.SetText("No primary weapon");
      return;
    }
    // Name: animate only when weapon changes
    string name = GetWeaponDisplayName(w);
    _nameText.SetText(name);
    bool weaponChanged = _lastWeaponRef != w;
    if (weaponChanged)
    {
      _nameText.Pulse(0.3f);
      _nameText.Quiver(0.03f, 0.5f, 0.4f);
    }

    float damage = w.GetDamage();
    int mag = w.GetAmmo();
    float reload = w.GetReloadSpeed();
    float fireInterval = MathF.Max(w.GetFireRate(), 0.0001f);
    float sps = 1f / fireInterval;
    float acc = Mathf.Clamp(w.GetAccuracy(), 0f, 1f) * 100f;
    float bs = w.GetBulletSpeed();

    string damageText = $"{damage:0.##}";
    Color damageCol = WeaponStatColorConfig.GetColour(WeaponStatKind.Damage, damage);
    SetStatVisual(_damageVal, damageText, damageCol, ref _lastDamageText, ref _lastDamageColor, damage);

    string accText = $"{acc:0}%";
    Color accCol = WeaponStatColorConfig.GetColour(WeaponStatKind.AccuracyPct, acc);
    SetStatVisual(_accVal, accText, accCol, ref _lastAccText, ref _lastAccColor, acc);

    string bulletText = $"{bs:0.##}";
    Color bsCol = WeaponStatColorConfig.GetColour(WeaponStatKind.BulletSpeed, bs);
    SetStatVisual(_bulletVal, bulletText, bsCol, ref _lastBulletText, ref _lastBulletColor, bs);

    string fireText = $"{sps:0.##}/s";
    Color spsCol = WeaponStatColorConfig.GetColour(WeaponStatKind.ShotsPerSecond, sps);
    SetStatVisual(_fireVal, fireText, spsCol, ref _lastFireText, ref _lastFireColor, sps);

    string reloadText = $"{reload:0.##}s";
    Color reloadCol = WeaponStatColorConfig.GetColour(WeaponStatKind.ReloadSeconds, reload);
    SetStatVisual(_reloadVal, reloadText, reloadCol, ref _lastReloadText, ref _lastReloadColor, reload);

    string magText = $"{mag}";
    Color magCol = WeaponStatColorConfig.GetColour(WeaponStatKind.Magazine, mag);
    SetStatVisual(_magVal, magText, magCol, ref _lastMagText, ref _lastMagColor, mag);

    // Snapshot current values for change detection next time
    _lastWeaponRef = w;
    _lastDamage = damage; _lastDamageText = damageText;
    _lastAccPct = acc; _lastAccText = accText;
    _lastBulletSpeed = bs; _lastBulletText = bulletText;
    _lastShotsPerSecond = sps; _lastFireText = fireText;
    _lastReloadSec = reload; _lastReloadText = reloadText;
    _lastMag = mag; _lastMagText = magText;
    _hasLastStats = true;

    // Module list removed; no additional UI updates required here
  }

  private static string GetWeaponDisplayName(Weapon w)
  {
    return w switch
    {
      OlReliable => "Ol' Reliable",
      Microgun => "Microgun",
      _ => w.GetType().Name
    };
  }

  // Module list removed; helper no longer required

  private static List<Color> GetNameColours()
  {
    // A bright, playful gradient cycling through warm to cool hues
    return new List<Color>
    {
      new Color(1.0f, 0.95f, 0.4f),
      new Color(1.0f, 0.75f, 0.3f),
      new Color(1.0f, 0.45f, 0.35f),
      new Color(0.95f, 0.3f, 0.6f),
      new Color(0.6f, 0.4f, 1.0f),
      new Color(0.4f, 0.8f, 1.0f)
    };
  }

  private static bool NearEqual(float a, float b, float epsilon = 0.0001f)
  {
    return MathF.Abs(a - b) <= epsilon;
  }

  // Track last displayed colours to avoid unnecessary SetColours (which re-inits DynaText)
  private Color _lastDamageColor, _lastAccColor, _lastBulletColor, _lastFireColor, _lastReloadColor, _lastMagColor;

  private static bool ColorsEqual(in Color a, in Color b, float eps = 0.002f)
  {
    return MathF.Abs(a.R - b.R) <= eps && MathF.Abs(a.G - b.G) <= eps && MathF.Abs(a.B - b.B) <= eps && MathF.Abs(a.A - b.A) <= eps;
  }

  private void SetStatVisual(DynaTextControl label, string text, Color colour, ref string lastText, ref Color lastColour, float numericForJuice)
  {
    bool textChanged = (text != lastText) || !_hasLastStats;
    bool colourChanged = (!ColorsEqual(colour, lastColour)) || !_hasLastStats;

    if (colourChanged)
      label.SetColours(new List<Color> { colour });

    label.SetText(text);
    if (textChanged)
      ApplyJuice(label, numericForJuice);

    lastText = text;
    lastColour = colour;
  }

  // Module list removed; color palette helper not needed

  private static void ApplyJuice(DynaTextControl dt, float value)
  {
    float absVal = MathF.Abs(value);
    int power = (int)MathF.Max(0f, MathF.Floor(MathF.Log10(MathF.Max(1e-6f, absVal))));
    float quiverAmt = 0.03f * power;
    float pulseAmt = 0.3f + 0.08f * power;
    if (quiverAmt > 0f) dt.Quiver(quiverAmt, 0.5f, 0.4f);
    dt.Pulse(pulseAmt);
  }
}
