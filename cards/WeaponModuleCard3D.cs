using Godot;

public partial class WeaponModuleCard3D : Card3D
{
  [Export]
  public WeaponModule Module { get; set; }

  /// <summary>
  /// Initializes the card with an optional module.
  /// </summary>
  /// <param name="module">A WeaponModule instance (optional).</param>
  public void Initialize(WeaponModule module = null)
  {
    Module = module;
    // Ensure CardCore is valid.
    if (CardCore == null)
    {
      CardCore = new CardCore();
    }
    if (Module != null)
    {
      // Associate the module's properties with the card.
      CardCore.CardTexture = Module.CardTexture;
      CardCore.CardDescription = Module.ModuleDescription;
      // Optionally set other module-specific data here.
    }
  }

  public override void _Ready()
  {
    base._Ready();
    // Additional setup can be done here if needed.
  }
}
