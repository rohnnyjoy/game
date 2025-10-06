using Godot;
using System;

/// <summary>
/// SubViewport-based compositor that renders the entire scene (3D + UI)
/// into an offscreen SubViewport and then presents it via a TextureRect.
/// The presentation quad can be shifted/rotated in screen space to achieve
/// a true full-frame shake without relying on SCREEN_TEXTURE, which is
/// problematic on some drivers (e.g., Metal).
///
/// Usage:
/// - Add this node as a direct child of the main scene root.
/// - On ready, it will move all siblings under an internal SubViewport.
/// - Call SetShake(offsetPx, rotationRad) each frame to animate the frame.
/// </summary>
public partial class SubViewportCompositor : Node
{
  public static SubViewportCompositor Instance { get; private set; }

  // SubViewport that owns the world and UI we reparent under it
  private SubViewport _subViewport;
  private Node _stageRoot;

  // Presentation tree drawing the SubViewport texture to the main viewport
  private Control _presentRoot;   // Root filling screen
  private SubViewportContainer _presentContainer; // Rotates/translates around center

  // Cached screen size
  private Vector2I _screenSize;

  // Overscan in pixels around all sides to avoid black edges during shake/rotate
  [Export(PropertyHint.Range, "0,128,1")] public int OverscanPixels { get; set; } = 24;
  [Export] public bool DebugLogs { get; set; } = false;
  private bool _cameraBound = false;

public override void _EnterTree()
  {
    // Create SubViewport and stage root as early as possible so we can
    // move siblings before their _Ready runs.
    _subViewport = new SubViewport
    {
      Name = "SubVP",
      RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
      Disable3D = false,
      TransparentBg = false,
    };

    _stageRoot = new Node { Name = "StageRoot" };
    _subViewport.AddChild(_stageRoot);

    // Presentation quad in the main viewport
    _presentRoot = new Control
    {
      Name = "PresentRoot",
      MouseFilter = Control.MouseFilterEnum.Ignore,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ExpandFill,
    };
    // Fill the window; pivot will be set on size changes
    _presentRoot.AnchorLeft = 0; _presentRoot.AnchorTop = 0; _presentRoot.AnchorRight = 1; _presentRoot.AnchorBottom = 1;
    _presentRoot.OffsetLeft = 0; _presentRoot.OffsetTop = 0; _presentRoot.OffsetRight = 0; _presentRoot.OffsetBottom = 0;

    _presentContainer = new SubViewportContainer
    {
      Name = "Present",
      MouseFilter = Control.MouseFilterEnum.Stop, // capture + forward to SubViewport
      Stretch = true,
      SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
      SizeFlagsVertical = Control.SizeFlags.ExpandFill,
    };
    // Fill the root, actual sizing handled by anchors
    _presentContainer.AnchorLeft = 0; _presentContainer.AnchorTop = 0; _presentContainer.AnchorRight = 1; _presentContainer.AnchorBottom = 1;
    _presentContainer.OffsetLeft = 0; _presentContainer.OffsetTop = 0; _presentContainer.OffsetRight = 0; _presentContainer.OffsetBottom = 0;
    _presentRoot.AddChild(_presentContainer);

    // Add SubViewport under the container so it auto-displays and forwards input
    _presentContainer.AddChild(_subViewport);
    // Add presentation into our node
    AddChild(_presentRoot);

    // Defer moving siblings until after the scene has fully entered the tree
  }

public override void _Ready()
  {
    Instance = this;
    {
      Vector2 s = GetViewport().GetVisibleRect().Size;
      _screenSize = new Vector2I((int)MathF.Round(s.X), (int)MathF.Round(s.Y));
    }
    ApplySizes(_screenSize);
    // React to window size changes
    GetTree().Root.SizeChanged += OnRootSizeChanged;
    // Now that we're in the tree, reparent siblings under SubViewport on the next idle frame
    CallDeferred(nameof(MoveSiblingsIntoStage));
    SetProcess(true);
  }

  public override void _ExitTree()
  {
    if (GetTree()?.Root != null)
      GetTree().Root.SizeChanged -= OnRootSizeChanged;
    if (Instance == this) Instance = null;
  }

  private void OnRootSizeChanged()
  {
    Vector2 s = GetViewport().GetVisibleRect().Size;
    Vector2I newSize = new Vector2I((int)MathF.Round(s.X), (int)MathF.Round(s.Y));
    if (newSize != _screenSize)
    {
      _screenSize = newSize;
      ApplySizes(newSize);
      if (DebugLogs) GD.Print($"[Compositor] resized -> screen={newSize}, subvp={_subViewport.Size}");
    }
  }

  private void ApplySizes(Vector2I screen)
  {
    // SubViewport is larger than screen to provide overscan
    Vector2I subSize = new Vector2I(screen.X + OverscanPixels * 2, screen.Y + OverscanPixels * 2);
    _subViewport.Size = subSize;
    // Expand the presentation container by overscan so moving/rotating never reveals the background
    _presentContainer.AnchorLeft = 0; _presentContainer.AnchorTop = 0; _presentContainer.AnchorRight = 1; _presentContainer.AnchorBottom = 1;
    _presentContainer.OffsetLeft = -OverscanPixels;
    _presentContainer.OffsetTop = -OverscanPixels;
    _presentContainer.OffsetRight = OverscanPixels;
    _presentContainer.OffsetBottom = OverscanPixels;
    // Root stays identity; rotate around the on-screen center within the expanded container
    _presentRoot.PivotOffset = Vector2.Zero;
    _presentContainer.PivotOffset = new Vector2(OverscanPixels + screen.X * 0.5f, OverscanPixels + screen.Y * 0.5f);
  }

  private void MoveSiblingsIntoStage()
  {
    Node parent = GetParent();
    if (parent == null)
      return;

    // We need a stable copy of children as we'll be modifying the list
    Godot.Collections.Array<Node> toMove = new Godot.Collections.Array<Node>();
    foreach (Node child in parent.GetChildren())
    {
      // Skip self and the temporary presentation nodes we created (they are our children)
      if (child == this) continue;
      toMove.Add(child);
    }

    foreach (Node n in toMove)
    {
      parent.RemoveChild(n);
      _stageRoot.AddChild(n);
    }

    // After reparenting, ensure a camera is active for the SubViewport so it renders content.
    // Do it deferred to allow any pending _Ready on moved nodes to run first.
    CallDeferred(nameof(EnsureSubViewportCamera));
  }

  private void EnsureSubViewportCamera()
  {
    // Find first Camera3D in the stage and make it current for this SubViewport.
    Camera3D cam = FindFirstCamera(_stageRoot);
    if (cam != null)
    {
      // Explicitly make it current for this SubViewport
      try { cam.MakeCurrent(); } catch { cam.Current = true; }
      if (DebugLogs) GD.Print("[Compositor] Set Camera3D.Current = true for SubViewport");
      _cameraBound = true;
    }
    else if (DebugLogs)
    {
      GD.Print("[Compositor] No Camera3D found under StageRoot yet");
    }
  }

  public override void _Process(double delta)
  {
    if (!_cameraBound)
    {
      var active = _subViewport.GetCamera3D();
      if (active == null)
      {
        EnsureSubViewportCamera();
      }
      else
      {
        _cameraBound = true;
      }
    }
  }

  private static Camera3D FindFirstCamera(Node root)
  {
    foreach (Node child in root.GetChildren())
    {
      if (child is Camera3D camera)
        return camera;
      Camera3D nested = FindFirstCamera(child);
      if (nested != null)
        return nested;
    }
    return null;
  }

  /// <summary>
  /// Sets the current shake transform in pixels/radians. Offset applies in screen pixels.
  /// </summary>
  public void SetShake(Vector2 offsetPixels, float rotationRad)
  {
    // Rotate/translate the presentation container around the screen center
    _presentContainer.Rotation = rotationRad;
    // Clamp translation so the expanded container always covers the screen,
    // accounting for extra AABB growth due to rotation.
    int rotMarginX = (int)MathF.Ceiling(0.5f * (MathF.Abs(_screenSize.X * MathF.Cos(rotationRad)) + MathF.Abs(_screenSize.Y * MathF.Sin(rotationRad)) - _screenSize.X));
    int rotMarginY = (int)MathF.Ceiling(0.5f * (MathF.Abs(_screenSize.X * MathF.Sin(rotationRad)) + MathF.Abs(_screenSize.Y * MathF.Cos(rotationRad)) - _screenSize.Y));
    int limX = Math.Max(0, OverscanPixels - rotMarginX - 1);
    int limY = Math.Max(0, OverscanPixels - rotMarginY - 1);
    float px = Mathf.Clamp(offsetPixels.X, -limX, limX);
    float py = Mathf.Clamp(offsetPixels.Y, -limY, limY);
    _presentContainer.Position = new Vector2(px, py);
  }
}
