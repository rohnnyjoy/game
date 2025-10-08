using Godot;
using System;
using System.Collections.Generic;

public partial class HudModuleBar : HBoxContainer
{
  [Export] public float IconSize { get; set; } = ModuleUiConfig.IconSize;
  [Export] public float IconSpacing { get; set; } = ModuleUiConfig.IconSpacing;
  [Export] public CanvasItem.TextureFilterEnum IconFilter { get; set; } = CanvasItem.TextureFilterEnum.Nearest;

  private readonly List<Control> _containers = new();
  private readonly List<TextureRect> _iconSlots = new();
  private readonly List<DynaTextControl> _badgeSlots = new();
  private readonly Dictionary<string, string> _lastBadgeText = new();

  public override void _Ready()
  {
    AddThemeConstantOverride("separation", (int)Mathf.Round(IconSpacing));
    var store = InventoryStore.Instance;
    if (store != null)
      store.StateChanged += OnInventoryStateChanged;
    var reg = ModuleBadgeRegistry.Instance;
    if (reg != null)
      reg.BadgeChanged += OnBadgeChanged;
    RenderModules();
  }

  public override void _ExitTree()
  {
    var store = InventoryStore.Instance;
    if (store != null)
      store.StateChanged -= OnInventoryStateChanged;
    var reg = ModuleBadgeRegistry.Instance;
    if (reg != null)
      reg.BadgeChanged -= OnBadgeChanged;
    base._ExitTree();
  }

  private void OnInventoryStateChanged(InventoryState state, ChangeOrigin origin)
  {
    RenderModules();
  }

  private void RenderModules()
  {
    ModuleVm[] modules = InventoryStore.Instance != null
      ? InventoryStore.Instance.GetModules(StackKind.PrimaryWeapon)
      : Array.Empty<ModuleVm>();

    EnsureSlotCount(modules.Length);

    var currentModuleIds = new HashSet<string>();

    for (int i = 0; i < _iconSlots.Count; i++)
    {
      TextureRect slot = _iconSlots[i];
      DynaTextControl badge = _badgeSlots[i];
      Control container = _containers[i];
      if (i < modules.Length)
      {
        ModuleVm vm = modules[i];
        if (!string.IsNullOrEmpty(vm.ModuleId))
          currentModuleIds.Add(vm.ModuleId);
        slot.Texture = vm.Icon;
        slot.TooltipText = vm.Tooltip;
        bool hasIcon = vm.Icon != null;
        slot.Visible = hasIcon;
        container.Visible = hasIcon;

        // Apply badge if present
        if (vm != null && ModuleBadgeRegistry.Instance != null && ModuleBadgeRegistry.Instance.TryGetBadge(vm.ModuleId, out var b))
        {
          string text = b.Text ?? string.Empty;
          badge.SetText(text);
          bool hasText = !string.IsNullOrEmpty(text);
          if (hasText)
          {
            badge.SetColours(new List<Color> { b.TextColor });
            if (_lastBadgeText.TryGetValue(vm.ModuleId, out var previousText))
            {
              if (!string.Equals(previousText, text, StringComparison.Ordinal))
                badge.Pulse();
            }
            else
            {
              // Do not pulse on first assignment; just record state.
            }
            _lastBadgeText[vm.ModuleId] = text;
          }
          else
          {
            _lastBadgeText.Remove(vm.ModuleId);
          }
          badge.Visible = hasText;
        }
        else
        {
          badge.SetText(string.Empty);
          badge.Visible = false;
          if (!string.IsNullOrEmpty(vm.ModuleId))
            _lastBadgeText.Remove(vm.ModuleId);
        }
      }
      else
      {
        slot.Texture = null;
        slot.TooltipText = string.Empty;
        slot.Visible = false;
        _containers[i].Visible = false;
        badge.SetText(string.Empty);
        badge.Visible = false;
      }
    }

    if (_lastBadgeText.Count > 0)
    {
      var stale = new List<string>();
      foreach (var key in _lastBadgeText.Keys)
      {
        if (!currentModuleIds.Contains(key))
          stale.Add(key);
      }

      foreach (var key in stale)
        _lastBadgeText.Remove(key);
    }
  }

  private void OnBadgeChanged(string moduleId)
  {
    RenderModules();
  }

  private void EnsureSlotCount(int desired)
  {
    while (_iconSlots.Count < desired)
    {
      var (container, icon, badge) = CreateIconSlot();
      AddChild(container);
      _containers.Add(container);
      _iconSlots.Add(icon);
      _badgeSlots.Add(badge);
    }

    for (int i = desired; i < _iconSlots.Count; i++)
    {
      // No removal to avoid churn; just hide extras in RenderModules
      _iconSlots[i].Visible = false;
      _iconSlots[i].Texture = null;
      _iconSlots[i].TooltipText = string.Empty;
      _containers[i].Visible = false;
    }
  }

  private (Control, TextureRect, DynaTextControl) CreateIconSlot()
  {
    var container = new Control
    {
      CustomMinimumSize = new Vector2(IconSize, IconSize),
      Size = new Vector2(IconSize, IconSize),
      MouseFilter = MouseFilterEnum.Ignore
    };

    var icon = new TextureRect
    {
      StretchMode = TextureRect.StretchModeEnum.Scale,
      TextureFilter = IconFilter,
      MouseFilter = MouseFilterEnum.Ignore,
      Name = "Icon"
    };
    icon.SetAnchorsPreset(LayoutPreset.FullRect);
    container.AddChild(icon);

    var badge = new DynaTextControl
    {
      MouseFilter = MouseFilterEnum.Ignore,
      Name = "Badge",
      Visible = false,
      FontPx = 34,
      Shadow = true,
      UseShadowParallax = true,
      AmbientFloat = false,
      AmbientRotate = false,
      AmbientBump = false,
      CenterInRect = false,
      AlignX = 1f,
      AlignY = 1f,
      LetterSpacingExtraPx = 0f,
      OffsetYExtraPx = 0f,
      TextHeightScale = 1f
    };
    badge.SetAnchorsPreset(LayoutPreset.FullRect);
    int pad = 2;
    badge.AddThemeConstantOverride("margin_left", pad);
    badge.AddThemeConstantOverride("margin_top", pad);
    badge.AddThemeConstantOverride("margin_right", pad);
    badge.AddThemeConstantOverride("margin_bottom", pad);
    container.AddChild(badge);

    return (container, icon, badge);
  }
}
