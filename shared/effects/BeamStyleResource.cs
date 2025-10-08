using Godot;
#nullable enable

public sealed partial class BeamStyleResource : Resource
{
  [Export] public Shader? Shader { get; set; }
  [Export] public Texture2D? Texture { get; set; }

  [Export(PropertyHint.Range, "1.0,256.0,1.0")] public float FramePixelWidth { get; set; } = 32.0f;
  [Export] public float TopPixels { get; set; } = 48.0f;
  [Export] public float MidPixels { get; set; } = 48.0f;
  [Export] public float BottomPixels { get; set; } = 48.0f;

  [Export(PropertyHint.Range, "1.0,120.0,0.5")] public float AnimationFps { get; set; } = 22.0f;
  [Export] public bool AnimateOnce { get; set; } = true;
  [Export(PropertyHint.Range, "-8.0,8.0,0.5")] public float FrameOffset { get; set; } = 0.0f;
}

