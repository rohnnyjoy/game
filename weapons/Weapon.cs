using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Weapon : Node3D
{
  [Export] public WeaponModule UniqueModule { get; set; }
  [Export] public Godot.Collections.Array<WeaponModule> Modules { get; set; } = new();
  [Export] public float FireRate { get; set; } = 0.5f;
  [Export] public float ReloadSpeed { get; set; } = 2f;
  [Export] public int Ammo { get; set; } = 10;
  [Export] public float Damage { get; set; } = 10f;
  [Export] public float Accuracy { get; set; } = 1.0f;
  [Export] public float BulletSpeed { get; set; } = 100f;

  protected int CurrentAmmo;
  protected bool Reloading = false;

  public override void _Ready()
  {
    CurrentAmmo = GetAmmo();

    foreach (var module in [UniqueModule] + Modules)
    {
      AddChild(module);
    }
  }

  public virtual void OnPress()
  {
    GD.Print("OnPress not implemented");
  }

  public virtual void OnRelease()
  {
    GD.Print("OnRelease not implemented");
  }

  public float GetReloadSpeed() => new List<WeaponModule> { UniqueModule }.Concat(Modules)
      .Aggregate(ReloadSpeed, (speed, module) => module.GetModifiedReloadSpeed(speed));

  public float GetFireRate() => new List<WeaponModule> { UniqueModule }.Concat(Modules)
      .Aggregate(FireRate, (rate, module) => module.GetModifiedFireRate(rate));

  public float GetDamage() => new List<WeaponModule> { UniqueModule }.Concat(Modules)
      .Aggregate(Damage, (damage, module) => module.GetModifiedDamage(damage));

  public int GetAmmo() => new List<WeaponModule> { UniqueModule }.Concat(Modules)
      .Aggregate(Ammo, (ammo, module) => module.GetModifiedAmmo(ammo));

  public float GetAccuracy() => new List<WeaponModule> { UniqueModule }.Concat(Modules)
      .Aggregate(Accuracy, (accuracy, module) => module.GetModifiedAccuracy(accuracy));

  public float GetBulletSpeed() => new List<WeaponModule> { UniqueModule }.Concat(Modules)
      .Aggregate(BulletSpeed, (speed, module) => module.GetModifiedBulletSpeed(speed));
}