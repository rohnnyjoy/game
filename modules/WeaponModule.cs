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

  public event Action<ModuleBadge?> BadgeChanged;


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

  // This legacy hook is deprecated in favor of provider patterns and WeaponStats snapshots.
  [Obsolete("ModifyBullet is deprecated. Prefer provider interfaces and manager pipelines.")]
  public virtual Bullet ModifyBullet(Bullet bullet)
  {
    return bullet;
  }

  [Obsolete("GetModifiedReloadSpeed is deprecated. Implement IStatModifier.Modify instead.")]
  public virtual float GetModifiedReloadSpeed(float reloadSpeed)
  {
    return reloadSpeed;
  }

  [Obsolete("GetModifiedFireRate is deprecated. Implement IStatModifier.Modify instead.")]
  public virtual float GetModifiedFireRate(float fireRate)
  {
    return fireRate;
  }

  [Obsolete("GetModifiedBulletSpeed is deprecated. Implement IStatModifier.Modify instead.")]
  public virtual float GetModifiedBulletSpeed(float bulletSpeed)
  {
    return bulletSpeed;
  }

  [Obsolete("GetModifiedAmmo is deprecated. Implement IStatModifier.Modify instead.")]
  public virtual int GetModifiedAmmo(int ammo)
  {
    return ammo;
  }

  [Obsolete("GetModifiedDamage is deprecated. Implement IStatModifier.Modify instead.")]
  public virtual float GetModifiedDamage(float damage)
  {
    return damage;
  }

  [Obsolete("GetModifiedAccuracy is deprecated. Implement IStatModifier.Modify instead.")]
  public virtual float GetModifiedAccuracy(float accuracy)
  {
    return accuracy;
  }

  protected void SetBadge(string text, Color? textColor = null, Color? backgroundColor = null)
  {
    BadgeChanged?.Invoke(new ModuleBadge(text, textColor, backgroundColor));
  }

  protected void ClearBadge()
  {
    BadgeChanged?.Invoke(null);
  }

  public virtual ModuleBadge? GetInitialBadge() => null;

  public virtual async Task OnWeaponProcess(double delta)
  {
    await Task.CompletedTask;
  }

  public virtual async Task OnReload()
  {
    await Task.CompletedTask;
  }
}
