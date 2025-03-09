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
    UniqueModule = new OlReliableModule();
    Modules = new Array<WeaponModule> {
      // new ExplosiveModule(),
      new PenetratingModule(),
      // new HomingModule(),
      new AimbotModule()
    };
    // Uncomment one of the following module sets if needed:
    // Modules = new Array<WeaponModule> { new PenetratingModule(), new ExplosiveModule() };
    // Modules = new Array<WeaponModule> { new BouncingModule(), new TrackingModule(), new SlowModule() };
    // Modules = new Array<WeaponModule> { new BouncingModule(), new ExplosiveModule() };
  }
}