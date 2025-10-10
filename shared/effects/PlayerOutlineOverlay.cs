using Godot;

// Draws an edges-only outline for objects on a specific visibility layer using a
// SubViewport mask + canvas_item edge shader. Intended for local player X-ray.
public partial class PlayerOutlineOverlay : CanvasLayer
{
    [Export(PropertyHint.Range, "1,20,1")] public int OverlayLayerBit { get; set; } = 20;
    [Export] public Color OutlineColor { get; set; } = new Color(0.258824f, 0.839216f, 1f, 1f);
    [Export(PropertyHint.Range, "0,8,0.5")] public float PixelThickness { get; set; } = 2.0f;

    private SubViewport _vp = null!;
    private Camera3D _cam = null!;
    private ColorRect _rect = null!;
    private ShaderMaterial _mat = null!;

    public override void _Ready()
    {
        // SubViewport for 3D mask
        _vp = new SubViewport
        {
            TransparentBg = true,
            DebugDraw = SubViewport.DebugDrawEnum.Disabled,
        };
        _vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _vp.World3D = GetViewport().World3D;
        AddChild(_vp);

        // Sync size
        var sz = GetViewport().GetVisibleRect().Size;
        _vp.Size = new Vector2I((int)sz.X, (int)sz.Y);

        // Camera that renders only the overlay layer
        _cam = new Camera3D
        {
            Current = true
        };
        _vp.AddChild(_cam);
        uint mask = 1u << (OverlayLayerBit - 1);
        _cam.CullMask = mask;

        // 2D overlay rect with edge shader
        _rect = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = new Color(1, 1, 1, 0)
        };
        _rect.AnchorLeft = 0; _rect.AnchorTop = 0; _rect.AnchorRight = 1; _rect.AnchorBottom = 1;
        _rect.OffsetLeft = 0; _rect.OffsetTop = 0; _rect.OffsetRight = 0; _rect.OffsetBottom = 0;
        _rect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _rect.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        AddChild(_rect);

        var shader = GD.Load<Shader>("res://shared/shaders/edge_outline_post.gdshader");
        _mat = new ShaderMaterial { Shader = shader };
        _rect.Material = _mat;
        _mat.SetShaderParameter("mask_tex", _vp.GetTexture());
        _mat.SetShaderParameter("outline_color", OutlineColor);
        _mat.SetShaderParameter("pixel_thickness", PixelThickness);
        _mat.SetShaderParameter("viewport_size", sz);

        // Resize hook
        GetViewport().SizeChanged += OnRootSizeChanged;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (GetViewport() != null)
            GetViewport().SizeChanged -= OnRootSizeChanged;
    }

    private void OnRootSizeChanged()
    {
        var sz = GetViewport().GetVisibleRect().Size;
        _vp.Size = new Vector2I((int)sz.X, (int)sz.Y);
        if (_mat != null)
        {
            _mat.SetShaderParameter("viewport_size", sz);
        }
    }

    public override void _Process(double delta)
    {
        // Follow the main camera
        var mainCam = GetTree().Root.GetCamera3D();
        if (mainCam == null)
            return;
        _cam.GlobalTransform = mainCam.GlobalTransform;
        _cam.Fov = mainCam.Fov;
        _cam.Near = mainCam.Near;
        _cam.Far = mainCam.Far;
        _cam.Projection = mainCam.Projection;
        _cam.Size = mainCam.Size;

        // Keep parameters in sync
        if (_mat != null)
        {
            _mat.SetShaderParameter("outline_color", OutlineColor);
            _mat.SetShaderParameter("pixel_thickness", PixelThickness);
        }
    }
}
