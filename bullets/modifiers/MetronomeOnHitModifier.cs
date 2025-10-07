using Godot;
using System.Threading.Tasks;

public partial class MetronomeOnHitModifier : BulletModifier
{
  public MetronomeModule Owner { get; set; }

  public override Task OnCollision(Bullet bullet, Bullet.CollisionData collision)
  {
    if (Owner == null)
      return Task.CompletedTask;

    if (collision.Collider != null && collision.Collider.IsInGroup("enemies"))
    {
      Owner.NotifyHit();
    }
    return Task.CompletedTask;
  }
}

