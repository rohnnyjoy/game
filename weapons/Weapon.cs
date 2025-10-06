using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Weapon : Node3D
{
  [Export] public Array<WeaponModule> ImmutableModules { get; set; }

  private Array<WeaponModule> _modules = new();
  public event Action ModulesChanged;
  public event Action StatsUpdated;

  [Export]
  public Array<WeaponModule> Modules
  {
    get => _modules;
    set
    {
      _modules = value ?? new Array<WeaponModule>();
      DebugTrace.Log($"Weapon.Modules set count={_modules.Count}");
      ModulesChanged?.Invoke();
      StatsUpdated?.Invoke();
    }
  }
  private float _fireRate = 0.5f;
  private float _reloadSpeed = 2f;
  private int _ammo = 10;
  private float _damage = 10f;
  private float _accuracy = 1.0f;
  private float _bulletSpeed = 100f;

  [Export]
  public float FireRate
  {
    get => _fireRate;
    set
    {
      if (_fireRate == value) return;
      _fireRate = value;
      StatsUpdated?.Invoke();
    }
  }

  [Export]
  public float ReloadSpeed
  {
    get => _reloadSpeed;
    set
    {
      if (_reloadSpeed == value) return;
      _reloadSpeed = value;
      StatsUpdated?.Invoke();
    }
  }

  [Export]
  public int Ammo
  {
    get => _ammo;
    set
    {
      if (_ammo == value) return;
      _ammo = value;
      StatsUpdated?.Invoke();
    }
  }

  [Export]
  public float Damage
  {
    get => _damage;
    set
    {
      if (_damage == value) return;
      _damage = value;
      StatsUpdated?.Invoke();
    }
  }

  [Export]
  public float Accuracy
  {
    get => _accuracy;
    set
    {
      if (_accuracy == value) return;
      _accuracy = value;
      StatsUpdated?.Invoke();
    }
  }

  [Export]
  public float BulletSpeed
  {
    get => _bulletSpeed;
    set
    {
      if (_bulletSpeed == value) return;
      _bulletSpeed = value;
      StatsUpdated?.Invoke();
    }
  }

  protected int CurrentAmmo;
  protected bool Reloading = false;

  public override void _Ready()
  {
    CurrentAmmo = GetAmmo();
    // Allow managers to discover weapons generically
    AddToGroup("weapons");
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
