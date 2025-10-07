using Godot;

public partial class OlReliableModule : WeaponModule, IStatModifier
{
  public void Modify(ref WeaponStats stats)
  {
    stats.Damage = 40f;
    stats.Accuracy = 1f;
    stats.Ammo = 400;
    stats.ReloadSpeed = 1f;
    // Leave BulletSpeed/FireRate unchanged; Ol' Reliable focuses on consistent baseline.
  }
}
