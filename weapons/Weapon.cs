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
  private bool _statsDirty = true;
  private WeaponStats _statsCache;

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
      _statsDirty = true;
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
      _statsDirty = true;
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
      _statsDirty = true;
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
      _statsDirty = true;
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
      _statsDirty = true;
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
      _statsDirty = true;
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
      _statsDirty = true;
    }
  }

  protected int CurrentAmmo;
  protected bool Reloading = false;

  public override void _Ready()
  {
    CurrentAmmo = GetAmmo();
    // Allow managers to discover weapons generically
    AddToGroup("weapons");
    RecomputeStats();
  }

  public virtual void OnPress()
  {
    GD.Print("OnPress not implemented");
  }

  public virtual void OnRelease()
  {
    GD.Print("OnRelease not implemented");
  }

  private void RecomputeStats()
  {
    _statsCache = new WeaponStats
    {
      Damage = _damage,
      FireRate = _fireRate,
      BulletSpeed = _bulletSpeed,
      Accuracy = _accuracy,
      ReloadSpeed = _reloadSpeed,
      Ammo = _ammo,
    };

    void ApplyMods(Array<WeaponModule> list)
    {
      if (list == null) return;
      foreach (var m in list)
      {
        if (m is IStatModifier sm)
        {
          sm.Modify(ref _statsCache);
        }
      }
    }

    ApplyMods(ImmutableModules);
    ApplyMods(Modules);
    _statsDirty = false;
  }

  private void EnsureStats()
  {
    if (_statsDirty) RecomputeStats();
  }

  public float GetReloadSpeed() { EnsureStats(); return _statsCache.ReloadSpeed; }
  public float GetFireRate() { EnsureStats(); return _statsCache.FireRate; }
  public float GetDamage() { EnsureStats(); return _statsCache.Damage; }
  public int GetAmmo() { EnsureStats(); return _statsCache.Ammo; }
  public float GetAccuracy() { EnsureStats(); return _statsCache.Accuracy; }
  public float GetBulletSpeed() { EnsureStats(); return _statsCache.BulletSpeed; }
}
