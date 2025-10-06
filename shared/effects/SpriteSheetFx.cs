using Godot;
using System;
using System.Collections.Generic;

// Generic helper to render a 2D sprite sheet as an AnimatedSprite3D in 3D.
// Use Spawn(...) with a sheet path and frame size instead of adding a new class per sheet.
public partial class SpriteSheetFx : Node3D
{
  private AnimatedSprite3D _sprite;

  // Pending configuration so we can safely apply transforms and children
  // after the node has entered the tree (avoids early add_child/transform errors).
  private bool _hasPendingConfig = false;
  private string _pSheetPath = string.Empty;
  private int _pFrameW;
  private int _pFrameH;
  private float _pPixelSize;
  private float _pFps;
  private bool _pLoop;
  private bool _pBillboard;
  private bool _pRandomRoll;
  private bool _pDoubleSided;
  private bool _pDepthTest;
  private BaseMaterial3D.TextureFilterEnum _pFilter;
  private Vector3 _pNormal = Vector3.Up;
  private string _pAnimName = "fx";
  private Vector3 _pGlobalPosition;

  private struct FramesKey
  {
    public string Path;
    public int W;
    public int H;
    public float Fps;
    public bool Loop;
    public string Anim;
  }

  private static readonly Dictionary<FramesKey, SpriteFrames> Cache = new();

  private static SpriteFrames GetOrBuild(string sheetPath, int frameW, int frameH, float fps, bool loop, string anim)
  {
    var key = new FramesKey { Path = sheetPath, W = frameW, H = frameH, Fps = fps, Loop = loop, Anim = anim };
    if (Cache.TryGetValue(key, out var existing)) return existing;

    var frames = new SpriteFrames();
    frames.AddAnimation(anim);
    frames.SetAnimationLoop(anim, loop);
    frames.SetAnimationSpeed(anim, fps);

    var sheet = GD.Load<Texture2D>(sheetPath);
    if (sheet == null)
      return frames;

    int cols = Math.Max(1, sheet.GetWidth() / frameW);
    int rows = Math.Max(1, sheet.GetHeight() / frameH);
    for (int y = 0; y < rows; y++)
    {
      for (int x = 0; x < cols; x++)
      {
        var region = new Rect2I(x * frameW, y * frameH, frameW, frameH);
        if (region.Position.X + region.Size.X <= sheet.GetWidth() && region.Position.Y + region.Size.Y <= sheet.GetHeight())
        {
          var atlas = new AtlasTexture { Atlas = sheet, Region = new Rect2(region.Position, region.Size) };
          frames.AddFrame(anim, atlas);
        }
      }
    }

    Cache[key] = frames;
    return frames;
  }

  // Optional: warm the cache for a sheet without spawning anything
  public static void Prewarm(string sheetPath, int frameW, int frameH, float fps, bool loop = false, string animName = "fx")
  {
    _ = GetOrBuild(sheetPath, frameW, frameH, fps, loop, animName);
  }

  public static void Spawn(
    Node context,
    string sheetPath,
    int frameW,
    int frameH,
    Vector3 position,
    Vector3? surfaceNormal = null,
    float pixelSize = 0.045f,
    float fps = 18f,
    bool loop = false,
    bool billboard = false,
    float normalOffset = 0.08f,
    bool randomRoll = true,
    bool doubleSided = true,
    bool depthTest = true,
    BaseMaterial3D.TextureFilterEnum filter = BaseMaterial3D.TextureFilterEnum.Nearest,
    string animName = "fx"
  )
  {
    if (context == null || !GodotObject.IsInstanceValid(context)) return;
    var tree = context.GetTree();
    if (tree?.CurrentScene == null) return;

    var node = new SpriteSheetFx();

    // Compute a robust normal and the desired spawn position.
    var n = surfaceNormal ?? Vector3.Up;
    if (n.LengthSquared() < 1e-6f) n = Vector3.Up;
    n = n.Normalized();

    // Bias toward facing camera to reduce self-occlusion when aligning to normals
    var cam = context.GetViewport()?.GetCamera3D();
    if (!billboard && cam != null)
    {
      Vector3 toCam = (cam.GlobalPosition - position).Normalized();
      if (n.Dot(toCam) < 0) n = -n;
    }

    // Stash pending configuration; we'll apply it in _Ready.
    node._hasPendingConfig = true;
    node._pSheetPath = sheetPath;
    node._pFrameW = frameW;
    node._pFrameH = frameH;
    node._pPixelSize = pixelSize;
    node._pFps = fps;
    node._pLoop = loop;
    node._pBillboard = billboard;
    node._pRandomRoll = randomRoll;
    node._pDoubleSided = doubleSided;
    node._pDepthTest = depthTest;
    node._pFilter = filter;
    node._pNormal = n;
    node._pAnimName = animName;
    node._pGlobalPosition = position + n * normalOffset;

    // Defer adding to the scene tree to avoid "parent busy setting up children" errors.
    tree.CurrentScene.CallDeferred(Node.MethodName.AddChild, node);
  }

  public override void _Ready()
  {
    // Apply any pending configuration once inside the tree.
    if (_hasPendingConfig)
    {
      _hasPendingConfig = false;
      GlobalPosition = _pGlobalPosition;
      Init(_pSheetPath, _pFrameW, _pFrameH, _pPixelSize, _pFps, _pLoop, _pBillboard, _pRandomRoll, _pDoubleSided, _pDepthTest, _pFilter, _pNormal, _pAnimName);
    }
  }

  private void Init(
    string sheetPath,
    int frameW,
    int frameH,
    float pixelSize,
    float fps,
    bool loop,
    bool billboard,
    bool randomRoll,
    bool doubleSided,
    bool depthTest,
    BaseMaterial3D.TextureFilterEnum filter,
    Vector3 normal,
    string animName
  )
  {
    var frames = GetOrBuild(sheetPath, frameW, frameH, fps, loop, animName);

    _sprite = new AnimatedSprite3D
    {
      SpriteFrames = frames,
      Animation = animName,
      TextureFilter = filter,
      PixelSize = pixelSize,
      Shaded = false,
      Billboard = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
      DoubleSided = doubleSided,
      NoDepthTest = !depthTest,
      Visible = true,
    };
    AddChild(_sprite);

    if (!billboard)
    {
      var nrm = normal.LengthSquared() < 1e-6f ? Vector3.Up : normal.Normalized();
      // Pick a non-colinear up vector to avoid unwanted rotation around local Z.
      Vector3 upCandidate = Vector3.Up;
      if (Mathf.Abs(nrm.Dot(upCandidate)) > 0.999f)
      {
        upCandidate = Vector3.Right;
      }
      LookAt(GlobalPosition + nrm, upCandidate);
      if (randomRoll)
      {
        float angle = (float)GD.RandRange(0.0, Math.PI * 2.0);
        RotateObjectLocal(Vector3.Forward, angle);
      }
    }

    _sprite.Play();

    int frameCount = frames.GetFrameCount(animName);
    float lifetime = Math.Max(1, frameCount) / Math.Max(1e-3f, fps);
    var timer = GetTree().CreateTimer(lifetime + 0.02f);
    timer.Connect("timeout", new Callable(this, nameof(OnTimeout)));
  }

  private void OnTimeout()
  {
    QueueFree();
  }
}
