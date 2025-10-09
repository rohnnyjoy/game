#nullable enable

using Godot;

namespace Shared.Runtime
{
  public readonly struct DamageRequest
  {
    public DamageRequest(
      Node3D? source,
      Vector3? sourcePosition,
      Node3D target,
      float amount,
      DamageKind kind = DamageKind.Other,
      float knockbackStrength = 0f,
      Vector3? knockbackDirection = null,
      float knockbackScale = 1f,
      bool respectShields = true,
      float shieldPadding = 0f,
      bool emitGlobalEvent = true,
      Vector3? originPosition = null)
    {
      Source = source;
      SourcePosition = sourcePosition;
      OriginPosition = originPosition ?? sourcePosition;
      Target = target;
      Amount = amount;
      Kind = kind;
      KnockbackStrength = knockbackStrength;
      KnockbackDirection = knockbackDirection;
      KnockbackScale = knockbackScale;
      RespectShields = respectShields;
      ShieldPadding = shieldPadding;
      EmitGlobalEvent = emitGlobalEvent;
    }

    public Node3D? Source { get; }
    public Vector3? SourcePosition { get; }
    public Vector3? OriginPosition { get; }
    public Node3D Target { get; }
    public float Amount { get; }
    public DamageKind Kind { get; }
    public float KnockbackStrength { get; }
    public Vector3? KnockbackDirection { get; }
    public float KnockbackScale { get; }
    public bool RespectShields { get; }
    public float ShieldPadding { get; }
    public bool EmitGlobalEvent { get; }

    public DamageRequest WithAmount(float amount)
    {
      return new DamageRequest(Source, SourcePosition, Target, amount, Kind, KnockbackStrength, KnockbackDirection, KnockbackScale, RespectShields, ShieldPadding, EmitGlobalEvent, OriginPosition);
    }

    public DamageRequest WithSource(Node3D? source, Vector3? sourcePosition)
    {
      return new DamageRequest(source, sourcePosition, Target, Amount, Kind, KnockbackStrength, KnockbackDirection, KnockbackScale, RespectShields, ShieldPadding, EmitGlobalEvent, OriginPosition);
    }
  }
}
