using Godot;

using System.Collections.Generic;

public partial class OlReliableModule : WeaponModule, IStatModifier, IDamagePreStepProvider
{
  public void Modify(ref WeaponStats stats)
  {
    stats.Damage = 40f;
    stats.Accuracy = 1f;
    stats.Ammo = 400;
    stats.ReloadSpeed = 1f;
    // Leave BulletSpeed/FireRate unchanged; Ol' Reliable focuses on consistent baseline.
  }

  public IEnumerable<DamagePreStepConfig> GetDamagePreSteps()
  {
    // 20% crit chance, 2.0x multiplier
    yield return new DamagePreStepConfig(
      DamagePreStepKind.CritChance,
      priority: 0,
      paramA: 0.20f,   // chance
      paramB: 2.0f,    // multiplier
      paramC: 0f,
      flag: false
    );
  }
}
