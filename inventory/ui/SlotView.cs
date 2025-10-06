using Godot;

/// <summary>
/// Inventory slot UI with a 9-patch frame, padded inner content area,
/// optional icon, and precise drag preview anchored to the grabbed pixel.
/// </summary>
public partial class SlotView : Control
{
  private NinePatchRect _frame;
  private MarginContainer _content;
  private TextureRect _icon;
  private ModuleVm _module;
  private Vector2 _cardSize = new Vector2(100, 100);
  private ColorRect _placeholderOverlay;

  // Grab offset measured in the inner-content local space at drag start.
  private Vector2 _grabOffsetInInner = Vector2.Zero;

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
      // In-slot display remains KeepAspectCentered; drag preview compensates for inset.
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
      MouseFilter = MouseFilterEnum.Ignore,
      ZIndex = 100
    };
    _placeholderOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
    AddChild(_placeholderOverlay);

    UpdateVisuals();
  }

  /// <summary>
  /// Configure frame texture, margins, padding, and intended inner card size.
  /// </summary>
  public void ConfigureVisuals(Texture2D frameTexture, int ninePatchMargin, float slotPadding, Vector2 cardSize)
  {
    FrameTexture = frameTexture;
    NinePatchMargin = ninePatchMargin;
    SlotPadding = slotPadding;
    _cardSize = cardSize;
    UpdateVisuals();
  }

  /// <summary>
  /// Apply margins and min size so the inner content area matches _cardSize.
  /// </summary>
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
      // Theme margins define the inner content rect used for icon & math.
      int inner = (int)Mathf.Round(SlotPadding + NinePatchMargin);
      _content.AddThemeConstantOverride("margin_left", inner);
      _content.AddThemeConstantOverride("margin_right", inner);
      _content.AddThemeConstantOverride("margin_top", inner);
      _content.AddThemeConstantOverride("margin_bottom", inner);
    }

    // Overall slot minimum size = inner card size + left/right & top/bottom padding/margins.
    float width = _cardSize.X + 2f * (SlotPadding + NinePatchMargin);
    float height = _cardSize.Y + 2f * (SlotPadding + NinePatchMargin);
    CustomMinimumSize = new Vector2(width, height);
  }

  /// <summary>
  /// Assigns or clears the module. Icon uses KeepAspectCentered inside the inner content.
  /// </summary>
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

  public void Clear() => SetContent(null);

  /// <summary>
  /// Shows a translucent overlay to indicate a placeholder drop target.
  /// </summary>
  public void SetPlaceholderHighlight(bool enabled)
  {
    if (_placeholderOverlay != null)
      _placeholderOverlay.Visible = enabled;
  }

  /// <summary>
  /// Inner content rect used for icon drawing (local coordinates).
  /// Uses the TextureRect's arranged rect provided by the MarginContainer,
  /// so our math matches the visual area the icon actually occupies.
  /// </summary>
  private Rect2 GetInnerRectLocal()
  {
    if (_icon == null)
      return new Rect2(Vector2.Zero, Vector2.Zero);

    Rect2 innerGlobal = _icon.GetGlobalRect();
    Vector2 selfGlobal = GetGlobalRect().Position;
    return new Rect2(innerGlobal.Position - selfGlobal, innerGlobal.Size);
  }

  /// <summary>
  /// Calculates the actual draw rect of the icon (size and inset) inside the given inner rect,
  /// respecting KeepAspectCentered. If no texture, returns zero size and zero inset.
  /// </summary>
  private void GetIconDrawRect(in Vector2 innerSize, out Vector2 drawSize, out Vector2 inset, out float scale)
  {
    drawSize = Vector2.Zero;
    inset = Vector2.Zero;
    scale = 0f;

    if (_module?.Icon == null)
      return;

    Vector2 texSize = _module.Icon.GetSize();
    if (texSize.X <= 0 || texSize.Y <= 0)
      return;

    scale = Mathf.Min(innerSize.X / texSize.X, innerSize.Y / texSize.Y);
    drawSize = texSize * scale;
    inset = (innerSize - drawSize) * 0.5f; // centered letterbox inset within inner rect
  }

  public override void _GuiInput(InputEvent @event)
  {
    // Do NOT record grab offset here. Godot starts a drag after a threshold of movement;
    // recording here would bake in a stale offset and cause a visible jump.
    base._GuiInput(@event);
  }

  public override Variant _GetDragData(Vector2 atPosition)
  {
    if (_module?.Icon == null)
      return new Variant();

    // 1) Compute grab offset at true drag start in inner-local space.
    Rect2 inner = GetInnerRectLocal();
    Vector2 innerTopLeft = inner.Position;
    Vector2 innerSize = inner.Size;

    _grabOffsetInInner = atPosition - innerTopLeft;
    _grabOffsetInInner.X = Mathf.Clamp(_grabOffsetInInner.X, 0, innerSize.X);
    _grabOffsetInInner.Y = Mathf.Clamp(_grabOffsetInInner.Y, 0, innerSize.Y);

    // 2) Compute the actual drawn region of the icon inside the inner rect.
    GetIconDrawRect(innerSize, out Vector2 drawSize, out Vector2 inset, out float scale);

    // If for any reason drawSize is zero (e.g., bad texture), bail out gracefully.
    if (drawSize.X <= 0 || drawSize.Y <= 0)
      return new Variant();

    // 3) Build a preview that is EXACTLY the draw region size (no extra scaling),
    // while using Nearest filtering to keep pixels crisp.
    var preview = new TextureRect
    {
      Texture = _module.Icon,
      StretchMode = TextureRect.StretchModeEnum.Scale,
      MouseFilter = MouseFilterEnum.Ignore,
      TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
      CustomMinimumSize = drawSize,
      Size = drawSize
    };

    // 4) Wrapper has the same origin/size as the draw region, so its (0,0)
    // matches the top-left of the on-screen drawn pixels.
    var wrapper = new Control
    {
      MouseFilter = MouseFilterEnum.Ignore,
      CustomMinimumSize = drawSize,
      Size = drawSize
    };

    // 5) Position preview so the exact pixel grabbed stays under the cursor:
    // - _grabOffsetInInner is measured from inner top-left.
    // - 'inset' shifts from inner top-left to the draw region top-left.
    // So the grab offset within the draw region = (_grabOffsetInInner - inset).
    // We negate to move the image under the cursor.
    Vector2 grabWithinDraw = _grabOffsetInInner - inset;

    // Clamp to draw bounds for safety (e.g., clicking letterbox area).
    grabWithinDraw.X = Mathf.Clamp(grabWithinDraw.X, 0, drawSize.X);
    grabWithinDraw.Y = Mathf.Clamp(grabWithinDraw.Y, 0, drawSize.Y);

    preview.Position = -grabWithinDraw;

    wrapper.AddChild(preview);
    SetDragPreview(wrapper);

    // 6) Payload for the drop target.
    var data = new Godot.Collections.Dictionary
    {
      { "module_id", _module.ModuleId },
      { "source_stack", (int)Kind },
      // Provide precise visual geometry so targets can align placeholder with the
      // actual sprite instead of the raw mouse pointer.
      { "grab_within_draw_x", grabWithinDraw.X },
      { "draw_width", drawSize.X }
    };

    return data;
  }
}
