using Godot;
using System;
using System.Threading.Tasks;

public partial class WeaponModule : Node
{
  [Export] public string ModuleName { get; set; } = "Base Module";
  [Export] public string ModuleDescription { get; set; } = "Base Module";
  [Export] public Texture2D CardTexture { get; set; } = GD.Load<Texture2D>("res://icons/explosive.png");

  [Export] public float ReloadSpeed { get; set; } = 0.5f;
  [Export] public float FireRate { get; set; } = 3f;
  [Export] public float BulletSpeed { get; set; } = 100f;
  [Export] public int Ammo { get; set; } = 10;
  [Export] public float Damage { get; set; } = 10;
  [Export] public float Accuracy { get; set; } = 1.0f;

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

  public virtual async Task OnFire(Bullet bullet)
  {
    await Task.CompletedTask;
  }
  // Change this to async Task if you plan on having asynchronous code inside.
  public virtual async Task OnCollision(Bullet.CollisionData collisionData, Bullet bullet)
  {
    await Task.CompletedTask;
  }

  public virtual async Task OnBulletPhysicsProcess(float delta, Bullet bullet)
  {
    await Task.CompletedTask;
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
