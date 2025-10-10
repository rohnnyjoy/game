using Godot;

/// <summary>
/// Custom tooltip overlay: 9-patch background with DynaTextControl for rich text + parallax shadow.
/// Follows the mouse with no delay and clamps to viewport. Attach as a child of GameUI.
/// </summary>
public partial class TooltipUi : Control
{
  private NinePatchRect _frame;
  private MarginContainer _pad;
  private VBoxContainer _vbox;
  private const int LineSeparation = 5;
  private Control _anchor; // control we anchor to while visible
  private Font _measureFont;
  private readonly System.Collections.Generic.List<LineSpec> _activeLines = new System.Collections.Generic.List<LineSpec>();

  private struct LineSpec
  {
    public string Text;
    public Color? Colour;
  }

  [Export] public Texture2D FrameTexture { get; set; } = GD.Load<Texture2D>("res://assets/ui/3x/ninepatch.png");
  [Export] public int PatchMargin { get; set; } = 16;
  [Export] public float ContentPadding { get; set; } = 7f;
  [Export] public int FontPx { get; set; } = 31; // slightly larger than default for readability
  [Export] public Color TextColor { get; set; } = Colors.White;
  [Export] public float ShadowAlpha { get; set; } = 0.35f;
  [Export] public float MaxWidth { get; set; } = 468f; // total tooltip width cap (frame included)
  [Export] public float AnchorSpacing { get; set; } = 7f; // gap from anchor rect
  [Export] public string FontPath { get; set; } = "res://assets/fonts/Born2bSportyV2.ttf";

  public override void _Ready()
  {
    MouseFilter = MouseFilterEnum.Ignore;
    Visible = false;

    _frame = new NinePatchRect
    {
      Name = "Frame",
      Texture = FrameTexture,
      DrawCenter = true,
      MouseFilter = MouseFilterEnum.Ignore
    };
    _frame.PatchMarginLeft = PatchMargin;
    _frame.PatchMarginRight = PatchMargin;
    _frame.PatchMarginTop = PatchMargin;
    _frame.PatchMarginBottom = PatchMargin;
    _frame.SetAnchorsPreset(LayoutPreset.TopLeft);
    AddChild(_frame);

    _pad = new MarginContainer
    {
      Name = "Pad",
      MouseFilter = MouseFilterEnum.Ignore
    };
    _pad.SetAnchorsPreset(LayoutPreset.FullRect);
    _pad.AddThemeConstantOverride("margin_left", (int)(ContentPadding + PatchMargin));
    _pad.AddThemeConstantOverride("margin_right", (int)(ContentPadding + PatchMargin));
    _pad.AddThemeConstantOverride("margin_top", (int)(ContentPadding + PatchMargin));
    _pad.AddThemeConstantOverride("margin_bottom", (int)(ContentPadding + PatchMargin));
    AddChild(_pad);

    _vbox = new VBoxContainer
    {
      Name = "Lines",
      MouseFilter = MouseFilterEnum.Ignore
    };
    _vbox.AddThemeConstantOverride("separation", LineSeparation);
    _vbox.SetAnchorsPreset(LayoutPreset.FullRect);
    _pad.AddChild(_vbox);

    // Size to content, start hidden
    CustomMinimumSize = new Vector2(16, 16);

    _measureFont = GD.Load<FontFile>(FontPath);
  }

  public override void _Process(double delta)
  {
    if (!Visible) return;
    UpdatePlacement();
  }

  public void ShowTooltip(string text)
  {
    ShowTooltip(null, text);
  }

  public void ShowTooltip(Control anchor, string text)
  {
    if (_vbox == null) return;
    foreach (Node c in _vbox.GetChildren())
      c.QueueFree();
    if (string.IsNullOrEmpty(text))
    {
      Visible = false;
      return;
    }
    _anchor = anchor;
    float contentMax = Mathf.Max(64f, MaxWidth - 2f * (ContentPadding + PatchMargin));
    _activeLines.Clear();
    var paragraphs = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    foreach (var para in paragraphs)
    {
      ExtractLineStyle(para, out string cleanText, out Color? overrideColour);
      var wrapped = WrapLine(cleanText, contentMax);
      if (wrapped.Count == 0)
      {
        _activeLines.Add(new LineSpec { Text = string.Empty, Colour = overrideColour });
        continue;
      }
      foreach (var line in wrapped)
      {
        _activeLines.Add(new LineSpec { Text = line ?? string.Empty, Colour = overrideColour });
      }
    }

    // Compute placement and frame size before showing
    UpdatePlacement();

    // Populate visuals after placement is set
    foreach (var line in _activeLines)
    {
      var dt = new DynaTextControl
      {
        FontPx = FontPx,
        Shadow = true,
        ShadowAlpha = ShadowAlpha,
        UseShadowParallax = true,
        ParallaxPixelScale = 0f,
        CenterInRect = false,
        LetterSpacingExtraPx = 0f
      };
      var colour = line.Colour ?? TextColor;
      dt.SetColours(new System.Collections.Generic.List<Color> { colour });
      dt.SetText(line.Text ?? string.Empty);
      _vbox.AddChild(dt);
    }
    Visible = true;
  }

  public void HideTooltip()
  {
    Visible = false;
    _anchor = null;
  }

  private void ExtractLineStyle(string paragraph, out string text, out Color? colour)
  {
    colour = null;
    text = paragraph ?? string.Empty;
    if (string.IsNullOrEmpty(paragraph)) return;

    const string rarityPrefix = "[rarity=";
    if (paragraph.StartsWith(rarityPrefix, System.StringComparison.OrdinalIgnoreCase))
    {
      int closeIndex = paragraph.IndexOf(']');
      if (closeIndex > rarityPrefix.Length)
      {
        string rarityToken = paragraph.Substring(rarityPrefix.Length, closeIndex - rarityPrefix.Length);
        if (System.Enum.TryParse(rarityToken, true, out Rarity rarityValue))
        {
          colour = RarityExtensions.GetColor(rarityValue);
          text = paragraph.Substring(closeIndex + 1).TrimStart();
          return;
        }
      }
    }
  }

  private Vector2 ComputeContentMinSizeFromLines()
  {
    float w = 0f;
    float h = 0f;
    float lh = GetLineHeight();
    int count = _activeLines != null ? _activeLines.Count : 0;
    if (count == 0) return Vector2.Zero;
    foreach (var line in _activeLines)
    {
      w = Mathf.Max(w, MeasureWidth(line.Text ?? string.Empty));
      h += lh;
    }
    if (count > 1) h += (count - 1) * LineSeparation;
    return new Vector2(w, h);
  }

  private System.Collections.Generic.List<string> WrapLine(string text, float maxWidth)
  {
    var result = new System.Collections.Generic.List<string>();
    if (string.IsNullOrEmpty(text))
    {
      result.Add("");
      return result;
    }
    // Simple word wrap (fallback to character wrap for long tokens)
    var words = text.Split(' ');
    string current = "";
    foreach (var word in words)
    {
      if (string.IsNullOrEmpty(current))
      {
        if (MeasureWidth(word) <= maxWidth)
        {
          current = word;
        }
        else
        {
          // Break very long single token
          var chunks = BreakLongWord(word, maxWidth);
          if (chunks.Count > 0)
          {
            // First chunk starts the current line; rest are standalone lines
            current = chunks[0];
            for (int i = 1; i < chunks.Count; i++) result.Add(chunks[i]);
          }
        }
      }
      else
      {
        string candidate = current + " " + word;
        if (MeasureWidth(candidate) <= maxWidth)
        {
          current = candidate;
        }
        else
        {
          result.Add(current);
          if (MeasureWidth(word) <= maxWidth)
          {
            current = word;
          }
          else
          {
            var chunks = BreakLongWord(word, maxWidth);
            if (chunks.Count > 0)
            {
              current = chunks[0];
              for (int i = 1; i < chunks.Count; i++) result.Add(chunks[i]);
            }
            else
            {
              current = word;
            }
          }
        }
      }
    }
    if (!string.IsNullOrEmpty(current)) result.Add(current);
    return result;
  }

  private System.Collections.Generic.List<string> BreakLongWord(string word, float maxWidth)
  {
    var chunks = new System.Collections.Generic.List<string>();
    int start = 0;
    while (start < word.Length)
    {
      int end = start + 1;
      int lastGood = start;
      while (end <= word.Length)
      {
        string sub = word.Substring(start, end - start);
        if (MeasureWidth(sub) <= maxWidth)
        {
          lastGood = end;
          end++;
        }
        else
        {
          break;
        }
      }
      if (lastGood == start)
      {
        // Ensure forward progress even if a single character exceeds max (extreme case)
        lastGood = Math.Min(start + 1, word.Length);
      }
      chunks.Add(word.Substring(start, lastGood - start));
      start = lastGood;
    }
    return chunks;
  }

  private float MeasureWidth(string s)
  {
    if (_measureFont == null) return s.Length * FontPx * 0.5f;
    Vector2 sz = _measureFont.GetStringSize(s, HorizontalAlignment.Left, -1, FontPx);
    return sz.X;
  }

  private float GetLineHeight()
  {
    if (_measureFont == null) return FontPx;
    return _measureFont.GetHeight(FontPx);
  }

  private void UpdatePlacement()
  {
    var vp = GetViewport();
    if (vp == null) return;
    Rect2 vr = vp.GetVisibleRect();

    // Compute total size including frame/padding from active lines
    Vector2 contentMin = ComputeContentMinSizeFromLines();
    float w = contentMin.X + 2f * (ContentPadding + PatchMargin);
    float h = contentMin.Y + 2f * (ContentPadding + PatchMargin);

    float minX = vr.Position.X + 2f;
    float maxX = vr.Position.X + vr.Size.X - w - 2f;
    float topClamp = vr.Position.Y + 2f;

    Vector2 desired = Position;
    if (IsInstanceValid(_anchor))
    {
      Rect2 ar = _anchor.GetGlobalRect();
      float centerX = ar.Position.X + ar.Size.X * 0.5f;
      float x = centerX - w * 0.5f;
      float yAbove = ar.Position.Y - h - AnchorSpacing;
      float y = yAbove;
      if (y < topClamp)
      {
        // If clipping vertically above, place centered below anchor.
        y = ar.Position.Y + ar.Size.Y + AnchorSpacing;
      }
      // Bump horizontally in-bounds only; keep centered if within bounds.
      x = Mathf.Clamp(x, minX, maxX);
      desired = new Vector2(x, y);
    }

    Position = desired;
    _frame.Position = Vector2.Zero;
    _frame.Size = new Vector2(w, h);
    Size = _frame.Size;
  }
}
