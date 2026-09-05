using Godot;
using System;

/// <summary>
/// 玩家:黄色自绘方块(亮黄框 + 暗黄芯) + 随 8 向朝向转的亮黄小三角。
/// WASD 决定移动与朝向;
/// J=行动·瞬发(无冷却连发), K=表达·按住飞出越远越慢、松开收回, L=接受·按住扩张越来越慢、松开消散。
/// 精力/成就规则由 GameManager 持有的 EnergySystem 结算;本类只暴露站桩状态供其回能。
/// </summary>
public partial class Character : CharacterBody2D
{
	[ExportGroup("移动")]
	[Export(PropertyHint.Range, "40,800,5")] public float MoveSpeed { get; set; } = 380f;
	[Export(PropertyHint.Range, "10,48,1")] public float BodyHalf { get; set; } = 26f; // 视觉方块半宽

	[ExportGroup("武器场景(1/2/3)")]
	[Export] public PackedScene ActScene { get; set; }
	[Export] public PackedScene ExpressScene { get; set; }
	[Export] public PackedScene AcceptScene { get; set; }

	[ExportGroup("软锁定")]
	[Export(PropertyHint.Range, "0,900,10")] public float LockRange { get; set; } = 520f;
	[Export] public float LockStrength { get; set; } = 0.85f;

	/// <summary>当前朝向(单位向量)。静止时保持最后朝向。</summary>
	public Vector2 Facing { get; private set; } = Vector2.Down;

	public Vector2 AimDirection => Facing;

	// 槽位 0=行动(J) 1=表达(K) 2=接受(L),与项目 Input Map 的 action 1/2/3 一致
	private static readonly string[] SlotAction = { "action 1", "action 2", "action 3" };
	private readonly float[] _cdLeft = new float[3];  // 剩余冷却
	private readonly float[] _cdFull = new float[3];  // 本次冷却全长
	private readonly IWeapon[] _heldWeapon = new IWeapon[3]; // 当前在场武器实例(Express/Accept 需要持续 Hold/Release)

	private GameManager _gm;
	public bool Resting { get; private set; } // 站桩回能:不移动且不施放

	public override void _Ready()
	{
		AddToGroup("player");
		_gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
		if (_gm == null)
			_gm = GetNodeOrNull<GameManager>("../GameManager");
	}

	public float CdRemain(int slot) => _cdLeft[slot];
	public float CdFull(int slot) => _cdFull[slot];

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;
		UpdateCooldowns(d);
		if (_gm == null || !_gm.EventOpen)
			HandleWeapons();

		Vector2 input = Input.GetVector("left", "right", "up", "down");
		if (input != Vector2.Zero)
			Facing = input.Normalized(); // 8 方向;静止保持朝向

		Velocity = input * MoveSpeed;
		MoveAndSlide();

		// 站桩回能判定:不移动且未按住任何武器键
		bool casting = Input.IsActionPressed(SlotAction[0])
			|| Input.IsActionPressed(SlotAction[1])
			|| Input.IsActionPressed(SlotAction[2]);
		Resting = input == Vector2.Zero && !casting;

		QueueRedraw(); // 朝向/受击都会改变画面(廉价矩形)
	}

	private void UpdateCooldowns(float d)
	{
		for (int i = 0; i < _cdLeft.Length; i++)
			_cdLeft[i] = Mathf.Max(_cdLeft[i] - d, 0f);
	}

	private void HandleWeapons()
	{
		float d = (float)GetPhysicsProcessDeltaTime();

		// 行动(J):瞬发,取消冷却限制
		if (Input.IsActionJustPressed(SlotAction[0]))
			Fire(ActScene, 0);

		// 表达(K)/接受(L):按住型,无冷却,但上一个收回前不能再发
		for (int slot = 1; slot <= 2; slot++)
			HandleHeldWeapon(slot);
	}

	private void HandleHeldWeapon(int slot)
	{
		PackedScene scene = slot == 1 ? ExpressScene : AcceptScene;
		bool justPressed = Input.IsActionJustPressed(SlotAction[slot]);
		bool pressed = Input.IsActionPressed(SlotAction[slot]);
		bool justReleased = Input.IsActionJustReleased(SlotAction[slot]);

		if (IsHeldWeaponReleased(slot))
		{
			if (justPressed)
				Fire(scene, slot);
		}
		else if (_heldWeapon[slot] != null)
		{
			if (pressed)
				_heldWeapon[slot].Hold((float)GetPhysicsProcessDeltaTime());
			if (justReleased)
				_heldWeapon[slot].Release();
		}
	}

	/// <summary>当前在场武器是否已经收回/消散( true 表示可以再发)。</summary>
	private bool IsHeldWeaponReleased(int slot)
	{
		var w = _heldWeapon[slot];
		if (w == null) return true;
		if (w is GodotObject go && !GodotObject.IsInstanceValid(go)) return true;
		return w.IsReleased;
	}

	private void Fire(PackedScene scene, int slot)
	{
		if (scene == null || _cdLeft[slot] > 0f || !IsHeldWeaponReleased(slot)) return;

		Node inst = scene.Instantiate();
		if (inst is not IWeapon weapon)
		{
			GD.PushWarning($"武器场景 {scene.ResourcePath} 未实现 IWeapon");
			return;
		}
		var body = (Node2D)inst;
		body.GlobalPosition = GlobalPosition;
		GetTree().CurrentScene.AddChild(body);

		// 必须先订阅再发射:Accept Launch() 内部立即结算并上报 WorryHit,
		// 若订阅晚于发射,本次(唯一一次)信号会被 GameManager 错过,导致命中没有伤害。
		if (_gm != null)
			_gm.AttachWeapon(body);

		Vector2 dir = AimDirection;
		if (slot != 2) // 接受是范围,无朝向
			dir = SoftLock(dir);
		weapon.Launch(this, dir);
		_heldWeapon[slot] = weapon;
		_cdLeft[slot] = weapon.Cooldown;
		_cdFull[slot] = Mathf.Max(_cdLeft[slot], 0.001f);
		_gm?.PlayShoot();
	}

	/// <summary>软锁定:±30° 锥内修正(仅改变方向,不把玩家拖去没指的目标)。</summary>
	private Vector2 SoftLock(Vector2 raw)
	{
		if (raw == Vector2.Zero) raw = Vector2.Down;
		raw = raw.Normalized();
		if (_gm == null) return raw;

		float cone = 0.866f; // cos30°
		float bestDot = -1f;
		Worry best = null;
		foreach (Worry w in _gm.Worries)
		{
			if (!GodotObject.IsInstanceValid(w)) continue;
			Vector2 to = w.GlobalPosition - GlobalPosition;
			float dist = to.Length();
			if (dist > LockRange || dist < 1f) continue;
			float dot = raw.Dot(to / dist);
			if (dot >= cone && dot > bestDot)
			{
				bestDot = dot;
				best = w;
			}
		}
		if (best == null) return raw;
		return raw.Lerp((best.GlobalPosition - GlobalPosition).Normalized(), LockStrength).Normalized();
	}

	public override void _Draw()
	{
		float half = BodyHalf;

		Color frame = Palette.PlayerFrame;
		Color core = Palette.PlayerCore;

		// 亮黄框 + 暗黄芯
		DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), frame);
		float inset = Mathf.Max(half * 0.2f, 3f);
		DrawRect(new Rect2(-half + inset, -half + inset, (half - inset) * 2f, (half - inset) * 2f), core);

		// 朝向小三角(亮黄,8 向可见)
		Vector2 f = Facing.Normalized();
		float ang = f.Angle();
		Vector2 tip = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * half;
		Vector2 perp = new Vector2(-f.Y, f.X) * half * 0.5f;
		DrawColoredPolygon(new[] { tip, tip - f * half * 0.7f + perp, tip - f * half * 0.7f - perp }, Palette.PlayerFrame);
	}
}
