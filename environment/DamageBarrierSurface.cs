#nullable enable

using Godot;
using Shared.Runtime;

public partial class DamageBarrierSurface : StaticBody3D, IDamageBarrierSurface
{
  [Export]
  public bool BlocksDirectProjectiles
  {
    get => _blocksDirectProjectiles;
    set
    {
      if (_blocksDirectProjectiles == value)
        return;
      _blocksDirectProjectiles = value;
      UpdateCollisionProfile();
    }
  }

  [Export]
  public bool BlocksIndirectDamage
  {
    get => _blocksIndirectDamage;
    set => _blocksIndirectDamage = value;
  }

  [Export]
  public DamageBarrierDirectionality Directionality
  {
    get => _directionality;
    set => _directionality = value;
  }

  private bool _blocksDirectProjectiles = true;
  private bool _blocksIndirectDamage = true;
  private DamageBarrierDirectionality _directionality = DamageBarrierDirectionality.Both;

  public Node3D BarrierNode => this;

  public override void _EnterTree()
  {
    base._EnterTree();
    DamageBarrierRegistry.Register(this);
  }

  public override void _ExitTree()
  {
    DamageBarrierRegistry.Unregister(this);
    base._ExitTree();
  }

  public override void _Ready()
  {
    base._Ready();
    if (!IsInGroup(DamageBarrierUtilities.GroupName))
      AddToGroup(DamageBarrierUtilities.GroupName);
    UpdateCollisionProfile();
  }

  public virtual bool TryGetIntersection(in DamageBarrierQuery query, out DamageBarrierHit hit)
  {
    hit = default;
    return false;
  }

  public virtual bool ShouldBlockDamage(in DamageBarrierQuery query, in DamageBarrierHit hit)
  {
    if (!DamageBarrierUtilities.BlocksKind(query.Kind, _blocksDirectProjectiles, _blocksIndirectDamage))
      return false;

    return DamageBarrierUtilities.PassesDirection(_directionality, query.OriginPosition, query.TargetPosition, hit.Normal);
  }

  protected void UpdateCollisionProfile()
  {
    if (_blocksDirectProjectiles)
      CollisionLayer = PhysicsLayers.Mask(PhysicsLayers.Layer.DamageBarrier);
    else
      CollisionLayer = 0;

    CollisionMask = 0;
  }
}
