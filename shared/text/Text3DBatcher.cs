using Godot;
using System;
using System.Collections.Generic;

#nullable enable

// Centralized per-letter 3D text batching using MultiMesh.
// Groups instances by glyph char for a given font configuration; each glyph char has a TextMesh-based Mesh
// and a MultiMeshInstance3D to render all its occurrences in one draw call.
public partial class Text3DBatcher : Node3D
{
  public static Text3DBatcher? Instance { get; private set; }

  public static Text3DBatcher EnsureInScene(Node context)
  {
    if (Instance != null && GodotObject.IsInstanceValid(Instance)) return Instance;
    var tree = context.GetTree();
    if (tree?.CurrentScene == null) throw new InvalidOperationException("No current scene to attach Text3DBatcher.");
    var node = new Text3DBatcher();
    tree.CurrentScene.AddChild(node);
    Instance = node;
    return node;
  }

  public struct FontConfig : IEquatable<FontConfig>
  {
    public FontFile Font;
    public int FontSize;
    public float PixelSize;
    public bool Shaded;
    public int OutlineSize;
    public Color OutlineColor;

    public bool Equals(FontConfig other)
    {
      // Font resource equality by RID is robust; fallback to reference equality
      return Font == other.Font && FontSize == other.FontSize && Mathf.IsEqualApprox(PixelSize, other.PixelSize)
        && Shaded == other.Shaded && OutlineSize == other.OutlineSize && OutlineColor == other.OutlineColor;
    }
    public override bool Equals(object? obj) => obj is FontConfig o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(Font, FontSize, PixelSize, Shaded, OutlineSize, OutlineColor);
  }

  private class GlyphBucket
  {
    public char Char;
    public Mesh Mesh = null!; // TextMesh generated per char
    public MultiMesh Multi = null!;
    public MultiMeshInstance3D Instance = null!;
    // Transient list of instance transforms/colors to flush per frame
    public List<Transform3D> Transforms = new();
    public List<Color> Colors = new();
  }

  private class FontGroup
  {
    public FontConfig Config;
    public Dictionary<char, GlyphBucket> Buckets = new();
  }

  private readonly List<Text3DString> _strings = new();
  private readonly Dictionary<FontConfig, FontGroup> _groups = new();

  public void Register(Text3DString s)
  {
    if (!_strings.Contains(s)) _strings.Add(s);
  }
  public void Unregister(Text3DString s)
  {
    _strings.Remove(s);
  }

  public override void _Process(double delta)
  {
    // Clear buckets for reuse this frame
    foreach (var (_, group) in _groups)
    {
      foreach (var (_, bucket) in group.Buckets)
      {
        bucket.Transforms.Clear();
        bucket.Colors.Clear();
      }
    }

    // Collect instances
    foreach (var s in _strings)
    {
      if (!s.IsInsideTree()) continue;
      var cfg = s.GetFontConfig();
      var group = GetOrCreateGroup(cfg);
      s.EmitLetters((ch, xform, color) =>
      {
        var bucket = GetOrCreateBucket(group, ch);
        bucket.Transforms.Add(xform);
        bucket.Colors.Add(color);
      });
    }

    // Flush to MultiMeshes
    foreach (var (_, group) in _groups)
    {
      foreach (var (_, bucket) in group.Buckets)
      {
        int count = bucket.Transforms.Count;
        bucket.Multi.InstanceCount = count;
        for (int i = 0; i < count; i++)
        {
          bucket.Multi.SetInstanceTransform(i, bucket.Transforms[i]);
          bucket.Multi.SetInstanceColor(i, bucket.Colors[i]);
        }
      }
    }
  }

  private FontGroup GetOrCreateGroup(FontConfig cfg)
  {
    if (_groups.TryGetValue(cfg, out var g)) return g;
    g = new FontGroup { Config = cfg };
    _groups[cfg] = g;
    return g;
  }

  private GlyphBucket GetOrCreateBucket(FontGroup group, char ch)
  {
    if (group.Buckets.TryGetValue(ch, out var b)) return b;
    // Create TextMesh for this character
    var tm = new TextMesh
    {
      Font = group.Config.Font,
      Text = ch.ToString(),
      FontSize = group.Config.FontSize,
      PixelSize = group.Config.PixelSize,
      // Center pivots
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
    };
    // Outline support requires Font outline settings; not all engines expose direct outline color per mesh.
    // We approximate by duplicating geometry via outline size if available. If not, rely on batcher's material.

    var mesh = tm; // TextMesh inherits Mesh in Godot 4

    var mm = new MultiMesh
    {
      TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
      Mesh = mesh,
      UseColors = true,
      UseCustomData = false,
    };

    var mmi = new MultiMeshInstance3D
    {
      Multimesh = mm,
      Visible = true,
    };
    // Ensure per-instance color is visible: use unshaded material that takes vertex/instance color as albedo
    var mat = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      AlbedoColor = Colors.White,
      VertexColorUseAsAlbedo = true,
      CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };
    mmi.MaterialOverride = mat;
    AddChild(mmi);

    b = new GlyphBucket { Char = ch, Mesh = mesh, Multi = mm, Instance = mmi };
    group.Buckets[ch] = b;
    return b;
  }
}

// Text3DString: describes a single string instance with per-letter transforms/colors
public abstract partial class Text3DString : Node3D
{
  public abstract Text3DBatcher.FontConfig GetFontConfig();
  public abstract void EmitLetters(Action<char, Transform3D, Color> emit);

  public override void _Ready()
  {
    Text3DBatcher.EnsureInScene(this).Register(this);
  }
  public override void _ExitTree()
  {
    Text3DBatcher.Instance?.Unregister(this);
  }
}
