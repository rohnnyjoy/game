using Godot;
using Godot.Collections;

public partial class OlReliable : BulletWeapon
{
  public override void _Ready()
  {
    GD.Print("Instantiated OlReliableBulletWeapon");
    base._Ready();
  }

  public OlReliable()
  {
    ImmutableModules = new Array<WeaponModule> { new OlReliableModule() };
    Modules = new Array<WeaponModule> {
      // new ExplosiveModule(),
      new PiercingModule(),
      // new HomingModule(),
      new AimbotModule()
    };
    // MuzzleFlash = new GunpowderMuzzleFlash();
    // Uncomment one of the following module sets if needed:
    // Modules = new Array<WeaponModule> { new PenetratingModule(), new ExplosiveModule() };
    // Modules = new Array<WeaponModule> { new BouncingModule(), new TrackingModule(), new SlowModule() };
    // Modules = new Array<WeaponModule> { new BouncingModule(), new ExplosiveModule() };
  }
}