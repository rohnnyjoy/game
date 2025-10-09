#nullable enable

namespace Shared.Runtime
{
  public readonly struct DamageResult
  {
    public static readonly DamageResult Blocked = new DamageResult(false, 0f, 0f, 0f);

    public DamageResult(bool applied, float damageDealt, float overkill, float remainingHealth)
    {
      Applied = applied;
      DamageDealt = damageDealt;
      Overkill = overkill;
      RemainingHealth = remainingHealth;
    }

    public bool Applied { get; }
    public float DamageDealt { get; }
    public float Overkill { get; }
    public float RemainingHealth { get; }
  }
}
