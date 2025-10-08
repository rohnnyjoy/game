using Godot;

// Thin compatibility wrapper that uses SpriteSheetFx under the hood.
public partial class ImpactSprite : Node3D
{
  private const int FrameW = 48;
  private const int FrameH = 48;
  private const float FramesPerSecond = 18f;
  private const string SheetPath = "res://assets/sprites/effects/impact/impact_48x48.png";

  public static void Prewarm()
  {
    SpriteSheetFx.Prewarm(SheetPath, FrameW, FrameH, FramesPerSecond, loop: false, animName: "impact");
  }

  public static void Spawn(Node context, Vector3 position, Vector3? surfaceNormal = null, float pixelSize = 0.045f)
  {
    SpriteSheetFx.Spawn(
      context,
      sheetPath: SheetPath,
      frameW: FrameW,
      frameH: FrameH,
      position: position,
      surfaceNormal: surfaceNormal,
      pixelSize: pixelSize,
      fps: FramesPerSecond,
      loop: false,
      billboard: false,
      normalOffset: 0.08f,
      randomRoll: true,
      doubleSided: true,
      depthTest: true,
      filter: BaseMaterial3D.TextureFilterEnum.Nearest,
      animName: "impact"
    );
  }
}
