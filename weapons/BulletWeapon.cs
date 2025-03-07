using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BulletWeapon : Weapon
{
  private Node3D BulletOrigin;
  private Node MuzzleFlash;
  private bool Firing = false;

  public override void _Ready()
  {
    base._Ready();
    BulletOrigin = GetNode<Node3D>("BulletOrigin");
    MuzzleFlash = GetNode<Node>("MuzzleFlash");
  }

  public override void OnPress()
  {
    if (Firing)
      return;
    Firing = true;
    _ = FireLoop();
  }

  public override void OnRelease()
  {
    Firing = false;
  }

  private async Task FireLoop()
  {
    while (Firing)
    {
      if (CurrentAmmo <= 0)
      {
        await Reload();
      }
      else
      {
        FireBullet();
        CurrentAmmo--;
        await Task.Delay((int)(GetFireRate() * 1000));
      }
    }
  }

  private void FireBullet()
  {
    Bullet bullet = new Bullet();
    Vector3 baseDirection = -BulletOrigin.GlobalTransform.Basis.Z;

    bullet.Direction = baseDirection;
    bullet.Speed = GetBulletSpeed();
    bullet.Radius = 0.05f;
    bullet.Damage = GetDamage();
    bullet.GlobalTransform = BulletOrigin.GlobalTransform;

    foreach (var module in Modules)
    {
      bullet = module.ModifyBullet(bullet);
    }

    bullet.Modules = Modules;
    GetTree().CurrentScene.AddChild(bullet);

    if (MuzzleFlash != null && MuzzleFlash.HasMethod("trigger_flash"))
    {
      MuzzleFlash.Call("trigger_flash");
    }
  }

  private async Task Reload()
  {
    Reloading = true;
    await Task.Delay((int)(GetReloadSpeed() * 1000));
    CurrentAmmo = GetAmmo();
    Reloading = false;
  }
}
