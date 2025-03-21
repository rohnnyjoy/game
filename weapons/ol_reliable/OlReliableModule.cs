using Godot;

public partial class OlReliableModule : WeaponModule
{
  public override Bullet ModifyBullet(Bullet bullet)
  {
    bullet.Gravity = 0;
    bullet.Radius = 0.1f;
    return bullet;
  }

  public override float GetModifiedDamage(float damage)
  {
    return 40;
  }

  public override float GetModifiedFireRate(float fireRate)
  {
    return 0.2f;
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
