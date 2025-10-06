using Godot;

public partial class SlotView : Control
{
  private NinePatchRect _frame;
  private MarginContainer _content;
  private TextureRect _icon;
  private ModuleVm _module;
  private Vector2 _cardSize = new Vector2(100, 100);
  private ColorRect _placeholderOverlay;

  public StackKind Kind { get; set; } = StackKind.Inventory;
  public Texture2D FrameTexture { get; private set; }
  public int NinePatchMargin { get; private set; } = 18;
  public float SlotPadding { get; private set; } = 6f;

  public bool HasModule => _module != null;
  public string ModuleId => _module?.ModuleId;

  public override void _Ready()
  {
    FocusMode = FocusModeEnum.None;
    MouseFilter = MouseFilterEnum.Pass;

    _frame = new NinePatchRect
    {
      Name = "Frame",
      DrawCenter = true,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _frame.SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(_frame);

    _content = new MarginContainer
    {
      Name = "Content",
      MouseFilter = MouseFilterEnum.Ignore
    };
    _content.SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(_content);

    _icon = new TextureRect
    {
      Name = "Icon",
      StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
      MouseFilter = MouseFilterEnum.Ignore,
      Visible = false
    };
    _icon.SetAnchorsPreset(LayoutPreset.FullRect);
    _content.AddChild(_icon);

    _placeholderOverlay = new ColorRect
    {
      Name = "PlaceholderOverlay",
      Color = new Color(1f, 1f, 1f, 0.25f),
      Visible = false,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _placeholderOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
    _placeholderOverlay.ZIndex = 100;
    AddChild(_placeholderOverlay);

    UpdateVisuals();
  }

  public void ConfigureVisuals(Texture2D frameTexture, int ninePatchMargin, float slotPadding, Vector2 cardSize)
  {
    FrameTexture = frameTexture;
    NinePatchMargin = ninePatchMargin;
    SlotPadding = slotPadding;
    _cardSize = cardSize;
    UpdateVisuals();
  }

  private void UpdateVisuals()
  {
    if (_frame != null)
    {
      _frame.Texture = FrameTexture;
      _frame.PatchMarginLeft = NinePatchMargin;
      _frame.PatchMarginRight = NinePatchMargin;
      _frame.PatchMarginTop = NinePatchMargin;
      _frame.PatchMarginBottom = NinePatchMargin;
    }

    if (_content != null)
    {
      int inner = (int)Mathf.Round(SlotPadding + NinePatchMargin);
      _content.AddThemeConstantOverride("margin_left", inner);
      _content.AddThemeConstantOverride("margin_right", inner);
      _content.AddThemeConstantOverride("margin_top", inner);
      _content.AddThemeConstantOverride("margin_bottom", inner);
    }

    float width = _cardSize.X + 2f * (SlotPadding + NinePatchMargin);
    float height = _cardSize.Y + 2f * (SlotPadding + NinePatchMargin);
    CustomMinimumSize = new Vector2(width, height);
  }

  public void SetContent(ModuleVm module)
  {
    _module = module;
    if (_module != null)
    {
      _icon.Texture = _module.Icon;
      _icon.Visible = _module.Icon != null;
      TooltipText = _module.Tooltip ?? string.Empty;
    }
    else
    {
      _icon.Texture = null;
      _icon.Visible = false;
      TooltipText = string.Empty;
    }
  }

  public void Clear()
  {
    SetContent(null);
  }

  public void SetPlaceholderHighlight(bool enabled)
  {
    if (_placeholderOverlay != null)
      _placeholderOverlay.Visible = enabled;
  }

  public override Variant _GetDragData(Vector2 atPosition)
  {
    if (_module == null)
      return new Variant();

    var data = new Godot.Collections.Dictionary
    {
      { "module_id", _module.ModuleId },
      { "source_stack", (int)Kind }
    };

    TextureRect preview = new TextureRect
    {
      Texture = _module.Icon,
      StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
      CustomMinimumSize = _cardSize
    };
    preview.SetAnchorsPreset(LayoutPreset.FullRect);
    Control wrapper = new Control
    {
      CustomMinimumSize = _cardSize
    };
    wrapper.AddChild(preview);
    SetDragPreview(wrapper);

    return data;
  }
}
