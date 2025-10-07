using Godot;
using System;
using System.Threading.Tasks;

public partial class MetronomeModule : WeaponModule
{
  [Export] public float StackIncrement { get; set; } = 0.08f; // +8% damage per hit
  [Export] public float MaxMultiplier { get; set; } = 2.0f;   // up to 2x damage
  [Export] public float ResetDelaySeconds { get; set; } = 2.0f; // reset if no hit within delay
  [Export] public bool ResetOnReload { get; set; } = true;

  private int _hitCount = 0;
  private float _lastHitAt = 0f;

  public MetronomeModule()
  {
    CardTexture = IconAtlas.MakeItemsIcon(10); // metronome sprite (shifted left by 1)
    ModuleName = "Metronome";
    ModuleDescription = "Each successful hit powers the next shot.";
    Rarity = Rarity.Rare;
    BulletModifiers.Add(new MetronomeOnHitModifier { Owner = this });
  }

  public void NotifyHit()
  {
    _hitCount = Math.Max(0, _hitCount) + 1;
    _lastHitAt = GetNow();
  }

  // Expose current multiplier without mutating state, for manager-side application.
  public float GetCurrentMultiplier()
  {
    MaybeReset();
    float mult = 1.0f + StackIncrement * Math.Max(0, _hitCount);
    if (MaxMultiplier > 0.0f)
      mult = MathF.Min(mult, MaxMultiplier);
    return MathF.Max(0.0f, mult);
  }

  private float GetNow() => (float)Time.GetTicksMsec() / 1000f;

  private void MaybeReset()
  {
    float now = GetNow();
    if (ResetDelaySeconds > 0 && (now - _lastHitAt) > ResetDelaySeconds)
    {
      _hitCount = 0;
    }
  }

  public override float GetModifiedDamage(float damage)
  {
    MaybeReset();
    float mult = 1.0f + StackIncrement * Math.Max(0, _hitCount);
    if (MaxMultiplier > 0.0f)
      mult = MathF.Min(mult, MaxMultiplier);
    return damage * MathF.Max(0.0f, mult);
  }

  public override Task OnReload()
  {
    if (ResetOnReload)
    {
      _hitCount = 0;
      _lastHitAt = GetNow();
    }
    return Task.CompletedTask;
  }
}
