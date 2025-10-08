using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class MetronomeModule : WeaponModule, IDamagePreStepProvider
{
  [Export] public float StackIncrement { get; set; } = 0.08f; // +8% damage per hit
  [Export] public float MaxMultiplier { get; set; } = 2.0f;   // up to 2x damage
  [Export] public float ResetDelaySeconds { get; set; } = 2.0f; // reset if no hit within delay
  [Export] public bool ResetOnReload { get; set; } = true;

  private int _streak = 0;
  private float _lastHitAt = 0f;
  private ulong _lastEnemyId = 0;
  private bool _hasLastEnemy = false;

  public MetronomeModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(10); // metronome sprite (shifted left by 1)
    ModuleName = "Metronome";
    ModuleDescription = "Each successful hit powers the next shot.";
    Rarity = Rarity.Rare;
  }

  public IEnumerable<DamagePreStepConfig> GetDamagePreSteps()
  {
    yield return new DamagePreStepConfig(
      DamagePreStepKind.Metronome,
      priority: 5,
      paramA: StackIncrement,
      paramB: MaxMultiplier,
      paramC: ResetDelaySeconds,
      flag: ResetOnReload
    );
  }

  public float GetCurrentMultiplier()
  {
    MaybeTimeoutReset();
    return ComputeMultiplier(_streak);
  }

  public float AdjustDamageForEnemy(ulong enemyId, float damage)
  {
    MaybeTimeoutReset();
    if (!_hasLastEnemy || _lastEnemyId != enemyId)
    {
      _streak = 0;
      _lastEnemyId = enemyId;
      _hasLastEnemy = true;
    }

    float multiplier = ComputeMultiplier(_streak);
    float adjusted = damage * multiplier;

    IncrementStreak();
    _lastHitAt = GetNow();

    return adjusted;
  }

  public void RegisterMiss()
  {
    ResetChain();
    _lastHitAt = GetNow();
    PublishMultiplier(1f);
  }

  private float GetNow() => (float)Time.GetTicksMsec() / 1000f;

  private void MaybeTimeoutReset()
  {
    float now = GetNow();
    if (ResetDelaySeconds > 0 && (now - _lastHitAt) > ResetDelaySeconds)
    {
      ResetChain();
    }
  }

  private float ComputeMultiplier(int streak)
  {
    float mult = 1.0f + StackIncrement * Math.Max(0, streak);
    if (MaxMultiplier > 0.0f)
      mult = MathF.Min(mult, MaxMultiplier);
    return MathF.Max(0.0f, mult);
  }

  private void IncrementStreak()
  {
    if (StackIncrement <= 0.0f)
    {
      _streak = 0;
      return;
    }

    int next = _streak + 1;
    if (MaxMultiplier > 0.0f && MaxMultiplier > 1.0f)
    {
      float maxStacks = MathF.Max(0.0f, (MaxMultiplier - 1.0f) / StackIncrement);
      next = Math.Min(next, (int)MathF.Ceiling(maxStacks));
    }
    else if (MaxMultiplier > 0.0f && MaxMultiplier <= 1.0f)
    {
      next = 0;
    }
    _streak = Math.Max(0, next);
  }

  private void ResetChain()
  {
    _streak = 0;
    _hasLastEnemy = false;
    _lastEnemyId = 0;
    PublishMultiplier(1f);
  }

  // No per-stat overrides; damage multipliers apply at hit time via pipeline.

  public override Task OnReload()
  {
    if (ResetOnReload)
    {
      ResetChain();
      _lastHitAt = GetNow();
      PublishMultiplier(1f);
    }
    return Task.CompletedTask;
  }

  public void PublishMultiplier(float multiplier)
  {
    string text = $"x{multiplier:0.0#}";
    SetBadge(text);
  }

  public override ModuleBadge? GetInitialBadge() => new ModuleBadge("x1.0");
}
