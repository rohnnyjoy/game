using Godot;
using Godot.Collections;

public partial class Microgun : BulletWeapon
{
  public override void _Ready()
  {
    GD.Print("Instantiated MicrogunBulletWeapon");
    base._Ready();
  }

  public Microgun()
  {
    UniqueModule = new MicrogunModule();
    Modules = new Array<WeaponModule> {
      // new ExplosiveModule(),
      new BouncingModule(),
      // new HomingModule(),
      new StickyModule()
    };
    // Uncomment one of the following module sets if needed:
    // Modules = new Array<WeaponModule> { new PenetratingModule(), new ExplosiveModule() };
    // Modules = new Array<WeaponModule> { new BouncingModule(), new TrackingModule(), new SlowModule() };
    // Modules = new Array<WeaponModule> { new BouncingModule(), new ExplosiveModule() };
  }
}