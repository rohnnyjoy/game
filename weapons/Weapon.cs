using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Weapon : Node3D
{
  [Export] public Array<WeaponModule> ImmutableModules { get; set; }
  [Export] public Array<WeaponModule> Modules { get; set; } = new();
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
  }

  public virtual void OnPress()
  {
    GD.Print("OnPress not implemented");
  }

  public virtual void OnRelease()
  {
    GD.Print("OnRelease not implemented");
  }

  public float GetReloadSpeed() => ImmutableModules.Concat(Modules)
        .Aggregate(ReloadSpeed, (speed, module) => module.GetModifiedReloadSpeed(speed));

  public float GetFireRate() => ImmutableModules.Concat(Modules)
      .Aggregate(FireRate, (rate, module) => module.GetModifiedFireRate(rate));

  public float GetDamage() => ImmutableModules.Concat(Modules)
      .Aggregate(Damage, (damage, module) => module.GetModifiedDamage(damage));

  public int GetAmmo() => ImmutableModules.Concat(Modules)
      .Aggregate(Ammo, (ammo, module) => module.GetModifiedAmmo(ammo));

  public float GetAccuracy() => ImmutableModules.Concat(Modules)
      .Aggregate(Accuracy, (accuracy, module) => module.GetModifiedAccuracy(accuracy));

  public float GetBulletSpeed() => ImmutableModules.Concat(Modules)
      .Aggregate(BulletSpeed, (speed, module) => module.GetModifiedBulletSpeed(speed));
}