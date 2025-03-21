using Godot;
using Godot.Collections;
using System;
using System.Threading.Tasks;

[Tool]
public partial class WeaponModule : Resource
{
  [Export] public virtual string ModuleName { get; set; } = "Base Module";
  [Export] public virtual string ModuleDescription { get; set; } = "Base Module";
  [Export] public virtual Texture2D CardTexture { get; set; } = GD.Load<Texture2D>("res://icons/explosive.png");

  // [Export] public float ReloadSpeedMultiplier { get; set; } = 1.0f;
  // [Export] public float FireRateMultiplier { get; set; } = 1.0f;
  // [Export] public float BulletSpeedMultiplier { get; set; } = 1.0f;
  // [Export] public int AmmoMultiplier { get; set; } = 1;
  // [Export] public float DamageMultiplier { get; set; } = 1.0f;
  // [Export] public float AccuracyMultiplier { get; set; } = 1.0f;
  [Export] public virtual Rarity Rarity { get; set; } = Rarity.Common;
  [Export] public virtual Array<BulletModifier> BulletModifiers { get; set; } = new Array<BulletModifier>();


  public Action OnReloadStart { get; private set; } = () => { };
  public Action OnReloadEnd { get; private set; } = () => { };

  public void AddReloadStartLogic(Action newLogic)
  {
    var prevLogic = OnReloadStart;
    OnReloadStart = () => { prevLogic.Invoke(); newLogic.Invoke(); };
  }

  public void AddReloadEndLogic(Action newLogic)
  {
    var prevLogic = OnReloadEnd;
    OnReloadEnd = () => { prevLogic.Invoke(); newLogic.Invoke(); };
  }

  // This base module can be extended to modify bullets or weapons.
  public virtual Bullet ModifyBullet(Bullet bullet)
  {
    return bullet;
  }

  public virtual float GetModifiedReloadSpeed(float reloadSpeed)
  {
    return reloadSpeed;
  }

  public virtual float GetModifiedFireRate(float fireRate)
  {
    return fireRate;
  }

  public virtual float GetModifiedBulletSpeed(float bulletSpeed)
  {
    return bulletSpeed;
  }

  public virtual int GetModifiedAmmo(int ammo)
  {
    return ammo;
  }

  public virtual float GetModifiedDamage(float damage)
  {
    return damage;
  }

  public virtual float GetModifiedAccuracy(float accuracy)
  {
    return accuracy;
  }

  public virtual async Task OnWeaponProcess(double delta)
  {
    await Task.CompletedTask;
  }

  public virtual async Task OnReload()
  {
    await Task.CompletedTask;
  }
}
