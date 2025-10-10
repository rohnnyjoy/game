using Godot;
using Shared.Runtime;

public partial class GlobalEvents : Node
{
  public static GlobalEvents Instance { get; private set; }
  // Shared scheduling for money drain/fill start so UI elements sync to the same frame
  public float NextMoneyDrainStartAt { get; private set; } = 0f;
  public bool MenuOpen { get; private set; } = false;

  [Signal]
  public delegate void EnemyDiedEventHandler();

  [Signal]
  public delegate void MoneyUpdatedEventHandler(int oldAmount, int newAmount);

  // Emitted whenever damage is applied to a target. Carries a suggested impulse for knockback.
  [Signal]
  public delegate void DamageDealtEventHandler(Node3D target, float amount, Vector3 impulse);

  // Emitted when a projectile/attack impacts a surface or target (for FX).
  [Signal]
  public delegate void ImpactOccurredEventHandler(Vector3 position, Vector3 normal, Vector3 direction);

  [Signal]
  public delegate void ExplosionOccurredEventHandler(Vector3 position, float radius);

  // Emitted whenever a weapon successfully fires a shot.
  [Signal]
  public delegate void WeaponFiredEventHandler(Weapon weapon);

  // Emitted when an enemy is killed with leftover damage; amount is the overkill.
  [Signal]
  public delegate void OverkillOccurredEventHandler(Node3D victim, float overkillAmount);

  // (Reverted) No generic screen shake signal here

  // Helper method to emit the enemy death event.
  public void EmitEnemyDied()
  {
    EmitSignal(nameof(EnemyDied));
  }


  public void EmitMoneyUpdated(int oldAmount, int newAmount)
  {
    EmitSignal(nameof(MoneyUpdated), oldAmount, newAmount);
  }

  public void SetMenuOpen(bool open)
  {
    MenuOpen = open;
    // Toggle global pause so gameplay stops while menus are open.
    var tree = GetTree();
    if (tree != null)
    {
      tree.Paused = open;
    }
    if (open)
    {
      // Ensure any ongoing weapon fire is stopped when opening menus/UI
      var weapons = GetTree()?.GetNodesInGroup("weapons");
      if (weapons != null)
      {
        foreach (var node in weapons)
        {
          if (node is Weapon w)
          {
            try { w.OnRelease(); } catch { }
          }
        }
      }
    }
  }

  public float ClaimMoneyDrainStartAt(float delaySeconds)
  {
    float now = (float)Time.GetTicksMsec() / 1000f;
    // Push-out semantics: schedule at max(existing, now + delay). Newer increments can only delay, never pull earlier.
    float proposed = now + MathF.Max(0f, delaySeconds);
    if (proposed > NextMoneyDrainStartAt)
      NextMoneyDrainStartAt = proposed;
    return NextMoneyDrainStartAt;
  }

  public override void _Ready()
  {
    Instance = this;
    AssetWarmup.Run(this);
    // Prewarm runtime-generated resources to avoid first-use hitches
    Coin.Prewarm();
    HealthPotion.Prewarm();
    ImpactSprite.Prewarm();
    CallDeferred(nameof(WarmupCoinDraw));
    CallDeferred(nameof(WarmupPotionDraw));
    CallDeferred(nameof(WarmupImpactDraw));
    // Ensure an ItemRenderer exists for batched pickup rendering
    if (ItemRenderer.Instance == null)
    {
      var ir = new ItemRenderer();
      AddChild(ir);
    }
    // Ensure an ExplosionVfxManager exists for batched explosion rendering
    if (ExplosionVfxManager.Instance == null)
    {
      var ex = new ExplosionVfxManager();
      AddChild(ex);
    }
    // Ensure a ModuleBadgeRegistry exists for dynamic HUD badges
    if (ModuleBadgeRegistry.Instance == null)
    {
      var badges = new ModuleBadgeRegistry();
      AddChild(badges);
    }
    // Ensure a beam manager exists for batched transfer rendering
    if (BeamVfxManager.Instance == null)
    {
      var beamMgr = new BeamVfxManager();
      AddChild(beamMgr);
    }

    // Ensure a pause menu overlay exists so players can exit safely mid-run
    if (PauseMenu.Instance == null)
    {
      var pauseMenu = new PauseMenu();
      AddChild(pauseMenu);
    }

    // Wire global listeners
    Connect(nameof(DamageDealt), new Callable(this, nameof(OnDamageDealt)));
    Connect(nameof(ImpactOccurred), new Callable(this, nameof(OnImpactOccurred)));
    Connect(nameof(ExplosionOccurred), new Callable(this, nameof(OnExplosionOccurred)));
    Connect(nameof(WeaponFired), new Callable(this, nameof(OnWeaponFired)));
    // (Reverted) No UI shake hookup on money updates

    // Ensure an Overkill handler exists for global chain effects (e.g., Cursed Skull)
    if (CursedSkullOverkillHandler.Instance == null)
    {
      var handler = new CursedSkullOverkillHandler();
      AddChild(handler);
    }
  }

  // Overload that derives the impulse magnitude from a snapshot + base knockback.
  public void EmitDamageDealt(Node3D target, BulletManager.ImpactSnapshot snapshot, Vector3 direction, float baseKnockback, float extraScale = 1.0f)
  {
    if (target == null || !IsInstanceValid(target)) return;
    Vector3 dir = direction.LengthSquared() > 0.000001f ? direction.Normalized() : Vector3.Forward;
    float scale = MathF.Max(0.0f, snapshot.KnockbackScale) * MathF.Max(0.0f, extraScale);
    Vector3 impulse = dir * MathF.Max(0.0f, baseKnockback) * scale;
    EmitSignal(nameof(DamageDealt), target, snapshot.Damage, impulse);
  }

  private void OnDamageDealt(Node3D target, float amount, Vector3 impulse)
  {
    if (!IsInstanceValid(target)) return;
    if (target is Enemy enemy)
    {
      enemy.ApplyKnockback(impulse);
    }
    else if (target is Player player)
    {
      player.ApplyKnockback(impulse);
    }
    // (Reverted) No global screen shake trigger here
  }

  public void EmitImpactOccurred(Vector3 position, Vector3 normal, Vector3 direction)
  {
    EmitSignal(nameof(ImpactOccurred), position, normal, direction);
  }

  public void EmitExplosionOccurred(Vector3 position, float radius)
  {
    EmitSignal(nameof(ExplosionOccurred), position, radius);
  }

  public void EmitWeaponFired(Weapon weapon)
  {
    if (weapon == null || !IsInstanceValid(weapon)) return;
    EmitSignal(nameof(WeaponFired), weapon);
  }

  public void EmitOverkillOccurred(Node3D victim, float overkillAmount)
  {
    if (victim == null || !IsInstanceValid(victim)) return;
    if (overkillAmount <= 0.0f) return;
    EmitSignal(nameof(OverkillOccurred), victim, overkillAmount);
  }

  private void OnImpactOccurred(Vector3 position, Vector3 normal, Vector3 direction)
  {
    // Default FX responders
    ImpactSprite.Spawn(this, position, normal);
    ImpactSound.Play(this, position);
    // Re-enable a subtle camera shake on any impact.
    TryCameraShake(0.07f, 0.10f);
  }

  private void OnExplosionOccurred(Vector3 position, float radius)
  {
    ExplosionVfxManager.Instance?.Spawn(position, radius);
    // Scale shake slightly by explosion radius.
    float dur = Mathf.Clamp(0.08f + 0.02f * radius, 0.08f, 0.22f);
    float amp = Mathf.Clamp(0.12f + 0.05f * radius, 0.12f, 0.30f);
    TryCameraShake(dur, amp);
  }

  private void OnWeaponFired(Weapon weapon)
  {
    // Subtle kick on firing; lighter than impact/explosion.
    TryCameraShake(0.05f, 0.06f);
  }

  // (Reverted) No money-based or generic screen shake methods

  private void TryCameraShake(float duration, float intensity)
  {
    // Prefer world camera shake; UI shake remains reserved for money flow.
    var player = Player.Instance;
    if (player != null && IsInstanceValid(player))
    {
      player.CameraShake?.TriggerShake(duration, intensity);
    }
  }

  private async void WarmupCoinDraw()
  {
    try
    {
      var s = new AnimatedSprite3D();
      s.SpriteFrames = Coin.GetSharedFrames();
      s.PixelSize = 0.0005f; // effectively invisible
      s.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
      s.Animation = "spin";
      s.Play();
      AddChild(s);
      var cam = GetViewport()?.GetCamera3D();
      if (cam != null)
      {
        s.GlobalPosition = cam.GlobalPosition + cam.GlobalTransform.Basis.Z * -1.0f; // 1m in front
        s.LookAt(cam.GlobalTransform.Origin, Vector3.Up);
      }
      await ToSignal(GetTree().CreateTimer(0.05), "timeout");
      s.QueueFree();
    }
    catch { }
  }

  private async void WarmupImpactDraw()
  {
    try
    {
      // Use code path that doesn't require a .tscn file to avoid load errors in headless/offline builds
      var cam = GetViewport()?.GetCamera3D();
      Vector3 pos = cam != null ? cam.GlobalPosition + cam.GlobalTransform.Basis.Z * 1.0f : Vector3.Zero;
      ImpactSprite.Spawn(this, pos, Vector3.Forward, pixelSize: 0.0005f);
      await ToSignal(GetTree().CreateTimer(0.06), "timeout");
    }
    catch { }
  }

  private async void WarmupPotionDraw()
  {
    try
    {
      var s = new AnimatedSprite3D();
      s.SpriteFrames = HealthPotion.GetSharedFrames();
      s.PixelSize = 0.0005f; // effectively invisible
      s.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
      s.Animation = "idle";
      s.Play();
      AddChild(s);
      var cam = GetViewport()?.GetCamera3D();
      if (cam != null)
      {
        s.GlobalPosition = cam.GlobalPosition + cam.GlobalTransform.Basis.Z * -1.0f;
        s.LookAt(cam.GlobalTransform.Origin, Vector3.Up);
      }
      await ToSignal(GetTree().CreateTimer(0.05), "timeout");
      s.QueueFree();
    }
    catch { }
  }
}
