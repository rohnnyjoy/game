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
  private DynaTextControl _modulesHeader;
  private DynaTextControl _modulesValue;

  private Weapon _currentWeapon;

  private readonly FontFile _pixelFont = GD.Load<FontFile>("res://assets/fonts/Born2bSportyV2.ttf");

  public override void _Ready()
  {
    BuildUiIfNeeded();

    if (Player.Instance?.Inventory != null)
      Player.Instance.Inventory.InventoryChanged += OnInventoryChanged;

    HookWeaponEvents();
    UpdateInfo();
  }

  public override void _ExitTree()
  {
    UnhookWeaponEvents();
    if (Player.Instance?.Inventory != null)
      Player.Instance.Inventory.InventoryChanged -= OnInventoryChanged;
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

    // Modules (header as DynaText)
    _modulesHeader = new DynaTextControl { Name = "ModulesHeader", FontPx = 36, AmbientRotate = false, AmbientFloat = true, AmbientBump = false, CenterInRect = false };
    _modulesHeader.ShadowAlpha = 0.55f;
    _modulesHeader.SetColours(new List<Color> { Colors.White });
    _modulesHeader.SetText("Modules:");
    _modulesHeader.CustomMinimumSize = new Vector2(0, 42);
    _vbox.AddChild(_modulesHeader);
    // Spacer for visual separation
    var spacer = new Control { Name = "Spacer" };
    spacer.CustomMinimumSize = new Vector2(0, 6);
    _vbox.AddChild(spacer);
    _modulesValue = new DynaTextControl { Name = "ModulesValue", FontPx = 40, AmbientRotate = true, AmbientFloat = true, AmbientBump = false };
    _modulesValue.ShadowAlpha = 0.55f;
    _modulesValue.CustomMinimumSize = new Vector2(0, 46);
    _vbox.AddChild(_modulesValue);
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

  private void OnInventoryChanged()
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
      w.ModulesChanged += OnWeaponModulesChanged;
  }

  private void UnhookWeaponEvents()
  {
    if (_currentWeapon != null)
    {
      _currentWeapon.ModulesChanged -= OnWeaponModulesChanged;
      _currentWeapon = null;
    }
  }

  private void OnWeaponModulesChanged()
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
    // Name: colorful palette and Balatro-style transient quiver + pulse
    _nameText.SetColours(GetNameColours());
    _nameText.SetText(GetWeaponDisplayName(w));
    _nameText.Pulse(0.3f);
    _nameText.Quiver(0.03f, 0.5f, 0.4f);

    float damage = w.GetDamage();
    int mag = w.GetAmmo();
    float reload = w.GetReloadSpeed();
    float fireInterval = MathF.Max(w.GetFireRate(), 0.0001f);
    float sps = 1f / fireInterval;
    float acc = Mathf.Clamp(w.GetAccuracy(), 0f, 1f) * 100f;
    float bs = w.GetBulletSpeed();

    _damageVal.SetColours(new List<Color> { new Color(1.0f, 0.35f, 0.0f) });
    _damageVal.SetText($"{damage:0.##}");
    ApplyJuice(_damageVal, damage);

    _accVal.SetColours(new List<Color> { new Color(0.1f, 1.0f, 0.35f) });
    _accVal.SetText($"{acc:0}%");
    ApplyJuice(_accVal, acc);

    _bulletVal.SetColours(new List<Color> { new Color(0.3f, 0.6f, 1.0f) });
    _bulletVal.SetText($"{bs:0.##}");
    ApplyJuice(_bulletVal, bs);

    _fireVal.SetColours(new List<Color> { new Color(1.0f, 0.92f, 0.0f) });
    _fireVal.SetText($"{sps:0.##}/s");
    ApplyJuice(_fireVal, sps);

    _reloadVal.SetColours(new List<Color> { new Color(0.1f, 1.0f, 1.0f) });
    _reloadVal.SetText($"{reload:0.##}s");
    ApplyJuice(_reloadVal, reload);

    _magVal.SetColours(new List<Color> { Colors.White });
    _magVal.SetText($"{mag}");
    ApplyJuice(_magVal, mag);

    // Modules list
    List<string> mods = new List<string>();
    if (w.ImmutableModules != null)
    {
      foreach (var m in w.ImmutableModules) mods.Add(GetModuleDisplayName(m));
    }
    if (w.Modules != null)
    {
      foreach (var m in w.Modules) mods.Add(GetModuleDisplayName(m));
    }
    _modulesValue.SetColours(new List<Color> { Colors.White });
    _modulesValue.SetText(mods.Count > 0 ? string.Join("  •  ", mods) : "None");
    // Small consistent juice when modules list updates
    _modulesValue.Pulse(0.3f);
    _modulesValue.Quiver(0.03f, 0.5f, 0.4f);
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

  private static string GetModuleDisplayName(WeaponModule m)
  {
    if (m == null) return "";
    if (!string.IsNullOrEmpty(m.ModuleName) && m.ModuleName != "Base Module")
      return m.ModuleName;
    return m.GetType().Name.Replace("Module", " Module");
  }

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

  private static List<Color> GetModulesColours()
  {
    // Softer accent for modules text
    return new List<Color>
    {
      new Color(0.9f, 0.9f, 0.9f),
      new Color(0.85f, 0.95f, 1.0f),
      new Color(0.95f, 0.9f, 1.0f),
    };
  }

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
