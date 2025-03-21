using Godot;

public partial class TrackingModule : WeaponModule
{
  public TrackingModule()
  {
    CardTexture = GD.Load<Texture2D>("res://icons/tracking.png");
    Rarity = Rarity.Rare;
    ModuleDescription = "Bullets track the mouse cursor, adjusting their trajectory to hit it.";
    BulletModifiers.Add(new TrackingBulletModifier());
  }
}
