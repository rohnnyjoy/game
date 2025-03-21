using Godot;
using System.Threading.Tasks;

[Tool]
public partial class BulletModifier : Resource
{

  public virtual async Task OnFire(Bullet bullet)
  {
    await Task.CompletedTask;
  }

  public virtual async Task OnCollision(Bullet bullet, Bullet.CollisionData collisionData)
  {
    await Task.CompletedTask;
  }

  public virtual async Task OnUpdate(Bullet bullet, float delta)
  {
    await Task.CompletedTask;
  }
}
