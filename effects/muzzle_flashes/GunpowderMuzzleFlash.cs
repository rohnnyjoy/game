using Godot;

public partial class GunpowderMuzzleFlash : MuzzleFlash
{

  protected static PackedScene _muzzleFlashScene = GD.Load<PackedScene>("res://effects/muzzle_flashes/GunpowderMuzzleFlash.tscn");

  public override void _Ready()
  {
    GD.Print("Instantiated GunpowderMuzzleFlash");
  }

  public static GunpowderMuzzleFlash CreateInstance()
  {
    return _muzzleFlashScene.Instantiate<GunpowderMuzzleFlash>();
  }

  public override void Play()
  {
    Restart();
  }
}