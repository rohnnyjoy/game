using System.Collections.Generic;

public enum DamagePreStepKind
{
  SpeedScale,
  Metronome,
  CritChance,
}

public enum DamagePostStepKind
{
  OverkillTransfer,
}

public readonly struct DamagePreStepConfig
{
  public DamagePreStepConfig(DamagePreStepKind kind, int priority, float paramA, float paramB, float paramC, bool flag)
  {
    Kind = kind;
    Priority = priority;
    ParamA = paramA;
    ParamB = paramB;
    ParamC = paramC;
    Flag = flag;
  }

  public DamagePreStepKind Kind { get; }
  public int Priority { get; }
  public float ParamA { get; }
  public float ParamB { get; }
  public float ParamC { get; }
  public bool Flag { get; }
}

public readonly struct DamagePostStepConfig
{
  public DamagePostStepConfig(DamagePostStepKind kind, int priority, float paramA, float paramB, float paramC)
  {
    Kind = kind;
    Priority = priority;
    ParamA = paramA;
    ParamB = paramB;
    ParamC = paramC;
  }

  public DamagePostStepKind Kind { get; }
  public int Priority { get; }
  public float ParamA { get; }
  public float ParamB { get; }
  public float ParamC { get; }
}

public interface IDamagePreStepProvider
{
  IEnumerable<DamagePreStepConfig> GetDamagePreSteps();
}

public interface IDamagePostStepProvider
{
  IEnumerable<DamagePostStepConfig> GetDamagePostSteps();
}
