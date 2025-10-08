using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;

public partial class ItemRenderer : Node3D
{
  public static ItemRenderer Instance { get; private set; }

  [Export] public int DefaultValue { get; set; } = 10;
  [Export] public float PixelSize { get; set; } = 0.025f; // smaller coins by default
  [Export] public float PickupRadius { get; set; } = 1.2f;
  [Export] public float AnimationFps { get; set; } = 12.0f;
  [Export] public bool FlipH { get; set; } = false; // default to not flipped
  [Export] public float PlayerTargetYOffset { get; set; } = 1.0f; // aim vacuum at player center
  // Separate magnet radii by item type
  [Export] public float CoinMagnetRadius { get; set; } = 20.0f;   // larger vacuum for gold
  [Export] public bool GlobalCoinVacuum { get; set; } = true;     // vacuum coins regardless of distance
  [Export] public float PotionMagnetRadius { get; set; } = 10.0f; // default vacuum for potions
  [Export] public float MagnetAccel { get; set; } = 200.0f;   // acceleration toward player
  [Export] public float MagnetMaxSpeed { get; set; } = 45.0f; // base clamp speed when vacuuming
  [Export] public float ArriveTime { get; set; } = 0.12f;     // seconds to converge to desired velocity
  [Export] public float TangentialDamping { get; set; } = 14.0f; // reduces sideways drift/orbiting
  [Export] public float SnapRadius { get; set; } = 1.5f;      // instant pickup when close
  [Export] public float SnapHeightTolerance { get; set; } = 1.6f; // vertical tolerance for snap
  [Export] public int AnimationBuckets { get; set; } = 8;     // number of phase buckets
  [Export] public float Gravity { get; set; } = 60.0f;        // world gravity
  [Export] public float Bounciness { get; set; } = 0.15f;     // bounce energy retention
  [Export] public float GroundFriction { get; set; } = 8.0f;  // slowdown on ground
  [Export] public float AirDrag { get; set; } = 0.2f;         // mild air drag
  [Export] public float FloorOffset { get; set; } = 0.02f;    // lift to avoid z-fighting
  [Export] public float HoverHeight { get; set; } = 0.12f;    // extra hover above ground
  [Export] public uint CoinCollisionMask { get; set; } = uint.MaxValue; // collide with world
  [Export] public float GroundProbeHeight { get; set; } = 2.5f; // ray up from coin center
  [Export] public float GroundProbeDepth { get; set; } = 5.0f;  // ray down below coin

  private MultiMesh[] _mms = System.Array.Empty<MultiMesh>();
  private MultiMeshInstance3D[] _mmis = System.Array.Empty<MultiMeshInstance3D>();
  private StandardMaterial3D[] _mats = System.Array.Empty<StandardMaterial3D>();
  private Mesh _quad;
  private Texture2D _sheet;
  private int _cols = 1;
  private int _rows = 1;
  private int _frameIndex = 0;
  private float _frameTimer = 0f;
  private int[] _phaseOffsets = System.Array.Empty<int>();
  private float _frameDuration => AnimationFps > 0 ? (1.0f / AnimationFps) : 0.1f;

  private struct Coin
  {
    public Vector3 Pos;
    public Vector3 Vel;
    public bool OnGround;
    public int Value;
    public int MmIndex;
  }
  private List<Coin>[] _coinsBuckets = System.Array.Empty<List<Coin>>();

  public override void _EnterTree()
  {
    Instance = this;
  }
  public override void _ExitTree()
  {
    if (Instance == this) Instance = null;
  }

  public override void _Ready()
  {
    SetProcess(true);
    SetPhysicsProcess(true);
    // Build material with billboarded quad and animated atlas
    _quad = new QuadMesh
    {
      Size = new Vector2(1, 1),
    };

    _sheet = GD.Load<Texture2D>("res://assets/sprites/items/coin_20x20.png");
    if (_sheet != null)
    {
      _cols = Math.Max(1, _sheet.GetWidth() / 20);
      _rows = Math.Max(1, _sheet.GetHeight() / 20);
    }

    int buckets = Math.Max(1, AnimationBuckets);
    _mms = new MultiMesh[buckets];
    _mmis = new MultiMeshInstance3D[buckets];
    _mats = new StandardMaterial3D[buckets];
    _phaseOffsets = new int[buckets];
    _coinsBuckets = new List<Coin>[buckets];

    var rng = new RandomNumberGenerator();
    rng.Randomize();
    int totalFrames = Math.Max(1, _cols * _rows);

    for (int i = 0; i < buckets; i++)
    {
      var mat = new StandardMaterial3D
      {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoTexture = _sheet,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY,
        BillboardKeepScale = true,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        VertexColorUseAsAlbedo = false,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
      };
      _mats[i] = mat;

      var mm = new MultiMesh
      {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        Mesh = _quad,
        UseCustomData = false,
        InstanceCount = 0,
      };
      _mms[i] = mm;
      var mmi = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat };
      _mmis[i] = mmi;
      AddChild(mmi);

      // Random phase offset per bucket
      _phaseOffsets[i] = rng.RandiRange(0, totalFrames - 1);
      _coinsBuckets[i] = new List<Coin>(64);

      // Prewarm one invisible instance per bucket
      mm.InstanceCount = 1;
      mm.SetInstanceTransform(0, Transform3D.Identity);
      mm.InstanceCount = 0;
    }

    // Prepare potion batches/materials up-front to avoid first-use hitch
    EnsurePotionBatches();
  }

  public override void _Process(double delta)
  {
    int total = Math.Max(1, _cols * _rows);
    _frameTimer += (float)delta;
    if (_frameTimer >= _frameDuration)
    {
      _frameTimer -= _frameDuration;
      _frameIndex = (_frameIndex + 1) % total;
      // Update UV scale/offset per bucket with unique phase
      float sx = 1.0f / Math.Max(1, _cols);
      float sy = 1.0f / Math.Max(1, _rows);
      for (int i = 0; i < _mats.Length; i++)
      {
        int idx = (_frameIndex + _phaseOffsets[i]) % total;
        int cx = idx % Math.Max(1, _cols);
        int cy = idx / Math.Max(1, _cols);
        float ox = cx * sx;
        float oy = cy * sy;
        float scaleX = FlipH ? -sx : sx;
        float offsetX = FlipH ? (ox + sx) : ox;
        _mats[i].Uv1Scale = new Vector3(scaleX, sy, 1.0f);
        _mats[i].Uv1Offset = new Vector3(offsetX, oy, 0.0f);
      }
    }

    // Advance potion frames
    if (_matsPotion.Length > 0)
    {
      int totalP = Math.Max(1, _potionCols * _potionRows);
      _potionFrameTimer += (float)delta;
      if (_potionFrameTimer >= _potionFrameDuration)
      {
        _potionFrameTimer -= _potionFrameDuration;
        _potionFrameIndex = (_potionFrameIndex + 1) % totalP;
        float sxp = 1.0f / Math.Max(1, _potionCols);
        float syp = 1.0f / Math.Max(1, _potionRows);
        for (int i = 0; i < _matsPotion.Length; i++)
        {
          int idx = (_potionFrameIndex + _potionPhaseOffsets[i]) % totalP;
          int cx = idx % Math.Max(1, _potionCols);
          int cy = idx / Math.Max(1, _potionCols);
          float ox = cx * sxp;
          float oy = cy * syp;
          _matsPotion[i].Uv1Scale = new Vector3(sxp, syp, 1.0f);
          _matsPotion[i].Uv1Offset = new Vector3(ox, oy, 0.0f);
        }
      }
    }
  }

  public void SpawnCoinsAt(Vector3 origin, int count)
  {
    var rng = new RandomNumberGenerator();
    rng.Randomize();
    int buckets = Math.Max(1, _coinsBuckets.Length);
    for (int i = 0; i < count; i++)
    {
      Vector3 offset = new Vector3(
        rng.RandfRange(-0.6f, 0.6f),
        rng.RandfRange(0.2f, 0.6f),
        rng.RandfRange(-0.6f, 0.6f)
      );
      int b = rng.RandiRange(0, buckets - 1);
      // Outward-only initial velocity (horizontal), no upward kick
      Vector3 dirXZ = new Vector3(rng.RandfRange(-1.0f, 1.0f), 0, rng.RandfRange(-1.0f, 1.0f));
      if (dirXZ.LengthSquared() < 0.0001f)
        dirXZ = new Vector3(1, 0, 0);
      dirXZ = dirXZ.Normalized();
      float speed = rng.RandfRange(2.0f, 4.0f);
      Vector3 initialVel = dirXZ * speed;
      // Append to bucket and allocate a persistent MultiMesh instance index
      var mm = _mms[b];
      int idx = (int)mm.InstanceCount;
      mm.InstanceCount = idx + 1;
      var c = new Coin { Pos = origin + offset, Vel = initialVel, OnGround = false, Value = DefaultValue, MmIndex = idx };
      _coinsBuckets[b].Add(c);
      // Write initial transform immediately
      float sxInit = 20 * PixelSize;
      float syInit = 20 * PixelSize;
      var scaleInit = new Vector3(sxInit, syInit, 1.0f);
      var xformInit = new Transform3D(Basis.Identity.Scaled(scaleInit), c.Pos);
      mm.SetInstanceTransform(idx, xformInit);
    }
    // No full rebuild required here
  }

  public override void _PhysicsProcess(double delta)
  {
    // Simple proximity pickup around the player
    var p = Player.Instance;
    if (p == null) return;
    Vector3 ppos = p.GlobalTransform.Origin + new Vector3(0, PlayerTargetYOffset, 0);
    Vector3 pvel = p.Velocity;
    float r2 = PickupRadius * PickupRadius;
    float m2Coin = CoinMagnetRadius * CoinMagnetRadius;
    float s2 = SnapRadius * SnapRadius;

    var space = GetWorld3D().DirectSpaceState;
    // Precompute world scale for coin quads
    float sxWorld = 20 * PixelSize;
    float syWorld = 20 * PixelSize;
    var scaleWorld = new Vector3(sxWorld, syWorld, 1.0f);
    float halfHeight = 0.5f * 20.0f * PixelSize;
    float restHeight = halfHeight + FloorOffset + HoverHeight;
    for (int b = 0; b < _coinsBuckets.Length; b++)
    {
      var list = _coinsBuckets[b];
      var mm = _mms[b];
      int i = 0;
      while (i < list.Count)
      {
        Coin c = list[i];
        float d2 = c.Pos.DistanceSquaredTo(ppos);
        Vector3 diff = c.Pos - ppos;
        float dy = MathF.Abs(diff.Y);
        float d2xz = diff.X * diff.X + diff.Z * diff.Z;
        if (d2 <= s2 || (d2xz <= s2 && dy <= SnapHeightTolerance))
        {
          var inv = p.Inventory;
          if (inv != null && GlobalEvents.Instance != null)
          {
            int oldAmount = inv.Money;
            int newAmount = oldAmount + c.Value;
            GlobalEvents.Instance.EmitMoneyUpdated(oldAmount, newAmount);
            inv.Money = newAmount;
          }
          int last = list.Count - 1;
          if (i != last)
          {
            Coin moved = list[last];
            list[i] = moved;
            moved.MmIndex = c.MmIndex;
            list[i] = moved;
            var movedXform = new Transform3D(Basis.Identity.Scaled(scaleWorld), moved.Pos);
            mm.SetInstanceTransform(moved.MmIndex, movedXform);
          }
          mm.InstanceCount = Math.Max(0, (int)mm.InstanceCount - 1);
          list.RemoveAt(last);
        }
        else
        {
          float dt = (float)delta;
          bool coinVacuum = GlobalCoinVacuum || (d2 <= m2Coin);
          if (coinVacuum)
          {
            Vector3 toPlayer = (ppos - c.Pos);
            float dist = Mathf.Sqrt(Mathf.Max(d2, 0.000001f));
            Vector3 dir = toPlayer / MathF.Max(dist, 0.000001f);
            float desiredSpeed = dist / MathF.Max(ArriveTime, 0.01f);
            Vector3 desiredVel = dir * desiredSpeed + pvel;
            Vector3 accel = (desiredVel - c.Vel) / MathF.Max(ArriveTime, 0.01f);
            float aLen = accel.Length();
            if (aLen > MagnetAccel)
              accel = accel / aLen * MagnetAccel;
            c.Vel += accel * dt;
            Vector3 forward = dir * c.Vel.Dot(dir);
            Vector3 lateral = c.Vel - forward;
            float damp = MathF.Min(1.0f, TangentialDamping * dt);
            c.Vel = forward + lateral * (1.0f - damp);
            float speed = c.Vel.Length();
            float maxS = MagnetMaxSpeed + pvel.Length() * 1.25f;
            if (speed > maxS)
              c.Vel = c.Vel / speed * maxS;
          }
          if (!c.OnGround)
          {
            c.Vel.Y -= Gravity * dt;
            c.Vel *= (1.0f - Mathf.Clamp(AirDrag * dt, 0, 0.99f));
          }
          else
          {
            Vector3 horiz = new Vector3(c.Vel.X, 0, c.Vel.Z);
            float hspeed = horiz.Length();
            if (hspeed > 0.0001f)
            {
              float newH = MathF.Max(0, hspeed - GroundFriction * dt);
              horiz = horiz / hspeed * newH;
            }
            c.Vel = new Vector3(horiz.X, 0, horiz.Z);
          }
          Vector3 next = c.Pos + c.Vel * dt;
          Vector3 probeFrom = new Vector3(next.X, next.Y + GroundProbeHeight, next.Z);
          Vector3 probeTo = new Vector3(next.X, next.Y - GroundProbeDepth, next.Z);
          var ghit = IntersectRayIgnoringCharacters(space, probeFrom, probeTo, CoinCollisionMask);
          if (ghit.Count > 0)
          {
            Vector3 gpos = ghit.ContainsKey("position") ? (Vector3)ghit["position"] : next;
            float groundY = gpos.Y + restHeight;
            if (next.Y <= groundY)
            {
              next.Y = groundY;
              c.Vel.Y = 0;
              c.OnGround = true;
            }
            else
            {
              c.OnGround = false;
            }
          }
          else
          {
            c.OnGround = false;
          }

          c.Pos = next;
          var xform = new Transform3D(Basis.Identity.Scaled(scaleWorld), c.Pos);
          mm.SetInstanceTransform(c.MmIndex, xform);
          list[i] = c;
          i++;
        }
      }
    }

    // Potion physics/pickup
    if (_potionsBuckets.Length > 0)
    {
      float sxP = 24 * PotionPixelSize;
      float syP = 24 * PotionPixelSize;
      var scalePotion = new Vector3(sxP, syP, 1.0f);
      float halfHeightP = 0.5f * 24.0f * PotionPixelSize;
      float restHeightP = halfHeightP + FloorOffset + HoverHeight;
      for (int b = 0; b < _potionsBuckets.Length; b++)
      {
        var list = _potionsBuckets[b];
        var mm = _mmsPotion[b];
        int i = 0;
        while (i < list.Count)
        {
          Potion po = list[i];
          float d2 = po.Pos.DistanceSquaredTo(ppos);
          Vector3 diff = po.Pos - ppos;
          float dy = MathF.Abs(diff.Y);
          float d2xz = diff.X * diff.X + diff.Z * diff.Z;
          if (d2 <= s2 || (d2xz <= s2 && dy <= SnapHeightTolerance))
          {
            if (p != null)
              p.Heal(po.Heal);
            int last = list.Count - 1;
            if (i != last)
            {
              Potion moved = list[last];
              list[i] = moved;
              moved.MmIndex = po.MmIndex;
              list[i] = moved;
              var movedXform = new Transform3D(Basis.Identity.Scaled(scalePotion), moved.Pos);
              mm.SetInstanceTransform(moved.MmIndex, movedXform);
            }
            mm.InstanceCount = Math.Max(0, (int)mm.InstanceCount - 1);
            list.RemoveAt(last);
          }
          else
          {
            float dt = (float)delta;
            // Only vacuum potions if the player is missing health
            bool missingHealth = p != null && (p.Health < p.MaxHealth - 0.001f);
            float m2Potion = PotionMagnetRadius * PotionMagnetRadius;
            if (missingHealth && d2 <= m2Potion)
            {
              Vector3 toPlayer = (ppos - po.Pos);
              float dist = Mathf.Sqrt(Mathf.Max(d2, 0.000001f));
              Vector3 dir = toPlayer / MathF.Max(dist, 0.000001f);
              float desiredSpeed = dist / MathF.Max(ArriveTime, 0.01f);
              Vector3 desiredVel = dir * desiredSpeed + pvel;
              Vector3 accel = (desiredVel - po.Vel) / MathF.Max(ArriveTime, 0.01f);
              float aLen = accel.Length();
              if (aLen > MagnetAccel)
                accel = accel / aLen * MagnetAccel;
              po.Vel += accel * dt;
              Vector3 forward = dir * po.Vel.Dot(dir);
              Vector3 lateral = po.Vel - forward;
              float damp = MathF.Min(1.0f, TangentialDamping * dt);
              po.Vel = forward + lateral * (1.0f - damp);
              float speed = po.Vel.Length();
              float maxS = MagnetMaxSpeed + pvel.Length() * 1.25f;
              if (speed > maxS)
                po.Vel = po.Vel / speed * maxS;
            }
            if (!po.OnGround)
            {
              po.Vel.Y -= Gravity * dt;
              po.Vel *= (1.0f - Mathf.Clamp(AirDrag * dt, 0, 0.99f));
            }
            else
            {
              Vector3 horiz = new Vector3(po.Vel.X, 0, po.Vel.Z);
              float hspeed = horiz.Length();
              if (hspeed > 0.0001f)
              {
                float newH = MathF.Max(0, hspeed - GroundFriction * dt);
                horiz = horiz / hspeed * newH;
              }
              po.Vel = new Vector3(horiz.X, 0, horiz.Z);
            }
            Vector3 next = po.Pos + po.Vel * dt;
            Vector3 probeFrom = new Vector3(next.X, next.Y + GroundProbeHeight, next.Z);
            Vector3 probeTo = new Vector3(next.X, next.Y - GroundProbeDepth, next.Z);
            var ghit = IntersectRayIgnoringCharacters(space, probeFrom, probeTo, CoinCollisionMask);
            if (ghit.Count > 0)
            {
              Vector3 gpos = ghit.ContainsKey("position") ? (Vector3)ghit["position"] : next;
              float groundY = gpos.Y + restHeightP;
              if (next.Y <= groundY)
              {
                next.Y = groundY;
                po.Vel.Y = 0;
                po.OnGround = true;
              }
              else
              {
                po.OnGround = false;
              }
            }
            else
            {
              po.OnGround = false;
            }

            po.Pos = next;
            var xform = new Transform3D(Basis.Identity.Scaled(scalePotion), po.Pos);
            mm.SetInstanceTransform(po.MmIndex, xform);
            list[i] = po;
            i++;
          }
        }
      }
    }
  }

  private Dictionary IntersectRayIgnoringCharacters(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 to, uint mask)
  {
    var exclude = new Array<Rid>();
    for (int tries = 0; tries < 3; tries++)
    {
      var query = new PhysicsRayQueryParameters3D
      {
        From = from,
        To = to,
        CollisionMask = mask,
        Exclude = exclude,
      };
      var hit = space.IntersectRay(query);
      if (hit.Count == 0)
        return hit;
      Node3D collider = hit.ContainsKey("collider") ? hit["collider"].As<Node3D>() : null;
      if (collider != null && (collider.IsInGroup("players") || collider.IsInGroup("enemies")))
      {
        if (collider is CollisionObject3D co)
          exclude.Add(co.GetRid());
        else if (hit.ContainsKey("rid"))
          exclude.Add((Rid)hit["rid"]);
        continue;
      }
      return hit;
    }
    // Fallback after max tries
    return space.IntersectRay(new PhysicsRayQueryParameters3D { From = from, To = to, CollisionMask = mask, Exclude = exclude });
  }

  // Removed: full per-frame rebuild of MultiMesh transforms
  // ===================== POTIONS =====================
  [Export] public float PotionPixelSize { get; set; } = 0.035f;
  [Export] public float PotionAnimationFps { get; set; } = 10.0f;
  [Export] public int PotionHealAmount { get; set; } = 25;

  private MultiMesh[] _mmsPotion = System.Array.Empty<MultiMesh>();
  private MultiMeshInstance3D[] _mmisPotion = System.Array.Empty<MultiMeshInstance3D>();
  private StandardMaterial3D[] _matsPotion = System.Array.Empty<StandardMaterial3D>();
  private Texture2D _potionSheet;
  private int _potionCols = 1;
  private int _potionRows = 1;
  private int _potionFrameIndex = 0;
  private float _potionFrameTimer = 0f;
  private int[] _potionPhaseOffsets = System.Array.Empty<int>();
  private float _potionFrameDuration => PotionAnimationFps > 0 ? (1.0f / PotionAnimationFps) : 0.1f;

  private struct Potion
  {
    public Vector3 Pos;
    public Vector3 Vel;
    public bool OnGround;
    public int Heal;
    public int MmIndex;
  }
  private System.Collections.Generic.List<Potion>[] _potionsBuckets = System.Array.Empty<System.Collections.Generic.List<Potion>>();

  private void EnsurePotionBatches()
  {
    if (_potionSheet == null)
      _potionSheet = GD.Load<Texture2D>("res://assets/sprites/items/health_potion_24x24.png");
    if (_potionSheet != null)
    {
      _potionCols = Math.Max(1, _potionSheet.GetWidth() / 24);
      _potionRows = Math.Max(1, _potionSheet.GetHeight() / 24);
    }

    if (_mmsPotion.Length > 0) return;
    int buckets = Math.Max(1, AnimationBuckets);
    _mmsPotion = new MultiMesh[buckets];
    _mmisPotion = new MultiMeshInstance3D[buckets];
    _matsPotion = new StandardMaterial3D[buckets];
    _potionPhaseOffsets = new int[buckets];
    _potionsBuckets = new System.Collections.Generic.List<Potion>[buckets];

    var rng = new RandomNumberGenerator();
    rng.Randomize();
    int totalFrames = Math.Max(1, _potionCols * _potionRows);

    for (int i = 0; i < buckets; i++)
    {
      var mat = new StandardMaterial3D
      {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoTexture = _potionSheet,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY,
        BillboardKeepScale = true,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        VertexColorUseAsAlbedo = false,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
      };
      _matsPotion[i] = mat;

      var mm = new MultiMesh
      {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        Mesh = _quad,
        UseCustomData = false,
        InstanceCount = 0,
      };
      _mmsPotion[i] = mm;
      var mmi = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat };
      _mmisPotion[i] = mmi;
      AddChild(mmi);

      _potionPhaseOffsets[i] = rng.RandiRange(0, totalFrames - 1);
      _potionsBuckets[i] = new System.Collections.Generic.List<Potion>(32);

      // Prewarm one invisible instance per bucket
      mm.InstanceCount = 1;
      mm.SetInstanceTransform(0, Transform3D.Identity);
      mm.InstanceCount = 0;
    }
  }

  public void SpawnPotionsAt(Vector3 origin, int count, int? healOverride = null)
  {
    EnsurePotionBatches();

    var rng = new RandomNumberGenerator();
    rng.Randomize();
    int buckets = Math.Max(1, _potionsBuckets.Length);
    for (int i = 0; i < count; i++)
    {
      Vector3 offset = new Vector3(
        rng.RandfRange(-0.5f, 0.5f),
        rng.RandfRange(0.15f, 0.4f),
        rng.RandfRange(-0.5f, 0.5f)
      );
      int b = rng.RandiRange(0, buckets - 1);
      Vector3 dirXZ = new Vector3(rng.RandfRange(-1.0f, 1.0f), 0, rng.RandfRange(-1.0f, 1.0f));
      if (dirXZ.LengthSquared() < 0.0001f)
        dirXZ = new Vector3(1, 0, 0);
      dirXZ = dirXZ.Normalized();
      float speed = rng.RandfRange(1.5f, 3.0f);
      Vector3 initialVel = dirXZ * speed;

      var mm = _mmsPotion[b];
      int idx = (int)mm.InstanceCount;
      mm.InstanceCount = idx + 1;
      var p = new Potion { Pos = origin + offset, Vel = initialVel, OnGround = false, Heal = healOverride ?? PotionHealAmount, MmIndex = idx };
      _potionsBuckets[b].Add(p);

      float sxInit = 24 * PotionPixelSize;
      float syInit = 24 * PotionPixelSize;
      var scaleInit = new Vector3(sxInit, syInit, 1.0f);
      var xformInit = new Transform3D(Basis.Identity.Scaled(scaleInit), p.Pos);
      mm.SetInstanceTransform(idx, xformInit);
    }
  }

  
}
