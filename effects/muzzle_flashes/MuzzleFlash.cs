using Godot;

public abstract partial class MuzzleFlash : GpuParticles3D
{
  public abstract void Play();
  public virtual void Initialize() { }
}