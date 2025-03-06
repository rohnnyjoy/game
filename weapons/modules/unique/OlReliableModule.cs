using Godot;

public partial class OlReliableModule : WeaponModule
{
  public override Bullet ModifyBullet(Bullet bullet)
  {
    bullet.Gravity = 0;
    bullet.Radius = 5;
    return bullet;
  }

  public override float GetModifiedDamage(float damage)
  {
    return 2;
  }

  public override float GetModifiedFireRate(float fireRate)
  {
    return 0.1f;
  }

  public override float GetModifiedBulletSpeed(float bulletSpeed)
  {
    return 200;
  }

  public override float GetModifiedAccuracy(float accuracy)
  {
    return 1;
  }

  public override int GetModifiedAmmo(int ammo)
  {
    return 400;
  }

  public override float GetModifiedReloadSpeed(float reloadSpeed)
  {
    return 1;
  }
}
