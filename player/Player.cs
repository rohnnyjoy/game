using Godot;
using System;

public partial class Player : CharacterBody3D
{
  public static Player Instance { get; private set; }
  public Inventory Inventory { get; private set; }
  public WeaponHolder WeaponHolder { get; private set; }
  public CameraShake CameraShake { get; private set; }
  // public AnimationPlayer AnimPlayer { get; private set; }
  public Weapon CurrentWeapon { get; set; }
  public Vector3 PreSlideHorizontalVelocity { get; set; }
  public AirLurchManager AirLurchManager { get; set; }

  public float JumpBufferTimer { get; set; }
  public int JumpsRemaining { get; set; }
  public bool IsSliding { get; set; }
  public new Vector3 Velocity
  {
    get => base.Velocity;
    set => base.Velocity = value;
  }

  [Export] public NodePath WeaponHolderPath;
  [Export] public NodePath CameraShakePath;
  // [Export] public NodePath AnimPlayerPath;

  private PlayerInput playerInput;
  private Node3D characterNode;
  private AnimationPlayer characterAnim;
  private string idleAnimName;
  private string walkAnimName;
  private const float IdleAnimSpeed = 1.0f;
  private const float WalkAnimSpeed = 1.6f;
  private Transform3D characterBaseTransform;
  private bool characterBaseCaptured = false;
  private float currentCharacterAngle = 0f;
  private Vector2 lastInputVector = new Vector2(0, 1);
  private float characterFacingOffset = 0f;

  public override void _Ready()
  {
    Instance = this;
    WeaponHolder = GetNode<WeaponHolder>(WeaponHolderPath);
    CameraShake = GetNode<CameraShake>(CameraShakePath);
    // AnimPlayer = GetNode<AnimationPlayer>(AnimPlayerPath);
    AddToGroup("players");
    Inventory = new Inventory();
    AddChild(Inventory);
    playerInput = new PlayerInput(this);
    Input.MouseMode = Input.MouseModeEnum.Captured;
    EquipDefaultWeapon();

    // Try to find the character model and its AnimationPlayer.
    characterNode = GetNodeOrNull<Node3D>("Character");
    if (characterNode != null)
    {
      characterAnim = FindFirstOfType<AnimationPlayer>(characterNode);
      if (characterAnim != null)
      {
        characterAnim.PlaybackDefaultBlendTime = 0.0f;
        // Heuristic: pick animations by common names.
        var list = characterAnim.GetAnimationList();
        string firstAnim = null;
        foreach (var name in list)
        {
          string nameStr = name.ToString();
          firstAnim ??= nameStr;
          var s = nameStr.ToLowerInvariant();
          if (string.IsNullOrEmpty(walkAnimName) && (s.Contains("walk") || s.Contains("run")))
            walkAnimName = nameStr;
          if (string.IsNullOrEmpty(idleAnimName) && s.Contains("idle"))
            idleAnimName = nameStr;
        }
        // Fallbacks if not found.
        if (string.IsNullOrEmpty(idleAnimName))
          idleAnimName = firstAnim;
        if (string.IsNullOrEmpty(walkAnimName))
          walkAnimName = firstAnim;

        if (!string.IsNullOrEmpty(idleAnimName))
          characterAnim.Play(idleAnimName, customBlend: 0.0f);
        if (!Mathf.IsEqualApprox(characterAnim.SpeedScale, IdleAnimSpeed))
          characterAnim.SpeedScale = IdleAnimSpeed;
      }

      CaptureCharacterBaseTransform();

    }

    // Attach shared damage feedback helper under the player for consistent visuals
    _damageFeedback = new Combat.DamageFeedback { VisualRoot = this };
    AddChild(_damageFeedback);

    // Initialize health UI
    GameUI.Instance?.SetHealth(Health, MaxHealth);
  }

  public override void _UnhandledInput(InputEvent @event)
  {
    playerInput.HandleInput(@event);
  }

  public Vector3 GetInputDirection()
  {
    Vector2 rawInput = Input.GetVector("left", "right", "up", "down");
    return (Transform.Basis * new Vector3(rawInput.X, 0, rawInput.Y)).Normalized();
  }

  private void EquipDefaultWeapon()
  {
    if (Inventory?.PrimaryWeapon != null)
    {
      CurrentWeapon = Inventory.PrimaryWeapon;
      WeaponHolder.AddChild(CurrentWeapon);
    }
  }

  public override void _Process(double delta)
  {
    base._Process(delta);
    if (characterAnim == null)
      return;

    // Choose walk/idle strictly from player input while grounded.
    // Avoid flicker when coasting to a stop by ignoring residual velocity.
    Vector2 rawInput = Input.GetVector("left", "right", "up", "down");
    bool hasInput = rawInput.LengthSquared() > 0.001f;
    bool moving = IsOnFloor() && hasInput && !IsSliding;
    string target = moving ? walkAnimName : idleAnimName;
    if (!string.IsNullOrEmpty(target))
    {
      var current = characterAnim.CurrentAnimation.ToString();
      float desiredSpeed = moving ? WalkAnimSpeed : IdleAnimSpeed;
      if (!Mathf.IsEqualApprox(characterAnim.SpeedScale, desiredSpeed))
        characterAnim.SpeedScale = desiredSpeed;
      if (current != target)
        characterAnim.Play(target, customBlend: 0.0f);
    }

    UpdateCharacterFacing();
  }

  // --- Damage/Health/Knockback ---
  [Export] public float MaxHealth { get; set; } = 100f;
  [Export] public float KnockbackDamping { get; set; } = 16.0f;
  [Export] public float MaxKnockbackSpeed { get; set; } = 10.0f;
  public float Health { get; private set; } = 100f;
  private Vector3 _knockbackVelocity = Vector3.Zero;
  private Combat.DamageFeedback _damageFeedback;

  public Vector3 KnockbackVelocity => _knockbackVelocity;

  public void ApplyKnockback(Vector3 impulse)
  {
    _knockbackVelocity += impulse;
    float len = _knockbackVelocity.Length();
    if (len > MaxKnockbackSpeed && len > 0.0001f)
      _knockbackVelocity = _knockbackVelocity / len * MaxKnockbackSpeed;
  }

  public void StepKnockback(float delta)
  {
    _knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, KnockbackDamping * delta);
  }

  public void TakeDamage(float amount)
  {
    Health = Mathf.Clamp(Health - amount, 0, MaxHealth);
    _damageFeedback?.Trigger(amount);
    CameraShake?.TriggerShake(0.18f, 0.18f);
    if (amount > 0.01f)
      FloatingNumber3D.Spawn(this, this, amount, new Color(1f, 0.4f, 0.4f));
    if (Health <= 0)
    {
      // TODO: death handling (respawn, UI, etc.). For now, clamp at 0.
    }
    GameUI.Instance?.SetHealth(Health, MaxHealth);
  }

  public void Heal(float amount)
  {
    if (amount <= 0.0f) return;
    float old = Health;
    Health = Mathf.Clamp(Health + amount, 0, MaxHealth);
    float delta = Health - old;
    if (delta > 0.01f)
    {
      FloatingNumber3D.Spawn(this, this, delta, new Color(0.4f, 1f, 0.4f));
      GameUI.Instance?.SetHealth(Health, MaxHealth);
    }
  }

  private T FindFirstOfType<T>(Node root) where T : class
  {
    if (root is T asT)
      return asT;
    for (int i = 0; i < root.GetChildCount(); i++)
    {
      var child = root.GetChild(i);
      var found = FindFirstOfType<T>(child);
      if (found != null)
        return found;
    }
    return null;
  }

  private void UpdateCharacterFacing()
  {
    if (characterNode == null)
      return;

    if (!characterBaseCaptured)
      CaptureCharacterBaseTransform();

    Vector2 rawInput = Input.GetVector("left", "right", "up", "down");
    if (rawInput.LengthSquared() > 0.001f)
      lastInputVector = rawInput.Normalized();

    float localAngle = Mathf.Atan2(lastInputVector.X, lastInputVector.Y);
    float step = Mathf.Pi / 4.0f; // 8 directions
    float snappedLocalAngle = Mathf.Round(localAngle / step) * step;

    float appliedAngle = Mathf.PosMod(snappedLocalAngle + characterFacingOffset + Mathf.Pi, Mathf.Tau) - Mathf.Pi;

    if (Mathf.IsEqualApprox(appliedAngle, currentCharacterAngle))
      return;

    Basis rotation = new Basis(Vector3.Up, appliedAngle);
    Transform3D newTransform = characterBaseTransform;
    newTransform.Basis = rotation * characterBaseTransform.Basis;
    characterNode.Transform = newTransform;
    currentCharacterAngle = appliedAngle;
  }

  private void CaptureCharacterBaseTransform()
  {
    if (characterNode == null)
      return;

    characterBaseTransform = characterNode.Transform;
    characterBaseCaptured = true;

    Vector3 baseForward = -characterBaseTransform.Basis.Z;
    float baseYaw = Mathf.Atan2(baseForward.X, baseForward.Z);

    Vector3 desiredForward = Vector3.Forward;
    float desiredYaw = Mathf.Atan2(desiredForward.X, desiredForward.Z);

    float offset = desiredYaw - baseYaw;
    characterFacingOffset = Mathf.PosMod(offset + Mathf.Pi, Mathf.Tau) - Mathf.Pi;
  }

  public override void _ExitTree()
  {
    base._ExitTree();
  }
}
