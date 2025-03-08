using Godot;

public partial class GlobalEvents : Node
{
  public static GlobalEvents Instance { get; private set; }

  [Signal]
  public delegate void EnemyDiedEventHandler();

  [Signal]
  public delegate void MoneyUpdatedEventHandler(int oldAmount, int newAmount);

  // Helper method to emit the enemy death event.
  public void EmitEnemyDied()
  {
    GD.Print("Emitting EnemyDied signal from GlobalEvents.");
    EmitSignal(nameof(EnemyDied));
  }


  public void EmitMoneyUpdated(int oldAmount, int newAmount)
  {
    EmitSignal(nameof(MoneyUpdated), oldAmount, newAmount);
  }

  public override void _Ready()
  {
    Instance = this;
    GD.Print("GlobalEvents singleton is ready.");
  }
}
