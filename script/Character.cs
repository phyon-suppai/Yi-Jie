using Godot;

public partial class Character : CharacterBody2D
{
	[Export] public float HorizontalSpeed;
	[Export] public float VerticalSpeed;

	[ExportGroup("精力")]
	// 当前精力（也是精力上限）。归零即败。
	[Export] public float Hp;

	[ExportGroup("精力 · 消耗（始终生效）")]
	// 时间比例：每秒固定流失，哪怕站着不动也在扣。
	[Export(PropertyHint.Range, "0,20,0.1")]
	public float TimeDrainRate { get; set; } = 2f;

	// 麻烦比例：场上每 1 个烦恼带来的额外流失（每秒）。
	// 总消耗 = (时间比例 + 麻烦比例 × 场上烦恼数) × 时间
	[Export(PropertyHint.Range, "0,8,0.1")]
	public float WorryDrainRate { get; set; } = 3f;

	[ExportGroup("精力 · 恢复（站桩时生效）")]
	// 站桩回能（每秒）。判定：不移动且当前没有投掷任何技能。
	// 与消耗并行结算，净变化 = 恢复 − 消耗。
	// 默认 14 意味着场上 ≤4 个烦恼时站桩能净回复；烦恼再多就回不上了，必须清怪。
	[Export(PropertyHint.Range, "0,40,0.1")]
	public float RestoreRate { get; set; } = 14f;

	[ExportGroup("武器")]
	// 笔·行动（按下即发射，单体直线）
	[Export] public PackedScene ActWeaponScene { get; set; }

	// 纸·表达（按住伸出，松手返航，群体）
	[Export] public PackedScene ExpressWeaponScene { get; set; }

	// 橡皮·接纳（按住扩张，松手收缩，群体）
	[Export] public PackedScene AcceptWeaponScene { get; set; }

	// 当前朝向（单位向量）。静止时保持最后一次移动方向。
	public Vector2 Facing { get; private set; } = Vector2.Down;

	// 精力上限 = 初始精力（站桩回能不越过它）。
	public float EnergyMax { get; private set; }

	// 当前是否处于站桩回能状态（供 HUD 提示）。
	public bool Resting { get; private set; }

	private AnimatedSprite2D _player;
	private Heart _heart;

	// 每个槽位各自在用的按住型武器实例（0=笔·瞬时；1=纸；2=橡皮）。各管各的键，互不打断。
	private readonly IWeapon[] _heldWeapon = new IWeapon[3];
	private static readonly string[] SlotAction = { "action 1", "action 2", "action 3" };
	private readonly float[] _cooldowns = new float[3];

	public override void _Ready()
	{
		// 加入 player 组：烦恼的追击逻辑依赖此组找到玩家
		AddToGroup("player");
		_player = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_heart = GetNode<Heart>("Heart");
		EnergyMax = Mathf.Max(Hp, 1f);
		_heart.Total = EnergyMax;
		_heart.Current = Hp;
	}

	public override void _PhysicsProcess(double delta)
	{
		UpdateCooldowns(delta);
		HandleWeapons();

		// GetVector 天然支持斜向，得到 8 方向输入
		Vector2 input = Input.GetVector("left", "right", "up", "down");

		if (input != Vector2.Zero)
			Facing = input.Normalized(); // 静止时保持上一次朝向

		Velocity = new Vector2(input.X * HorizontalSpeed, input.Y * VerticalSpeed);

		if (input != Vector2.Zero)
			_player.FlipH = input.X < 0;

		string anim = input == Vector2.Zero ? "idle" : "run";
		if (_player.Animation != anim)
			_player.Play(anim);

		MoveAndSlide();

		TickEnergy((float)delta, input);
	}

	// 精力按帧结算：消耗与恢复是两条独立的账，同时结算、互不相抵。
	//   消耗：始终生效，= (时间比例 + 麻烦比例 × 场上烦恼数) × delta
	//   恢复：仅站桩生效 = RestoreRate × delta
	//   净变化 = 恢复 − 消耗。掉到 0 由 GameManager 判负，这里只做钳制。
	private void TickEnergy(float delta, Vector2 input)
	{
		bool casting = Input.IsActionPressed(SlotAction[0])
			|| Input.IsActionPressed(SlotAction[1])
			|| Input.IsActionPressed(SlotAction[2]);

		Resting = input == Vector2.Zero && !casting;

		int worryCount = GetTree().GetNodesInGroup("worry").Count;
		float drain = (TimeDrainRate + WorryDrainRate * worryCount) * delta;
		float restore = Resting ? RestoreRate * delta : 0f;

		Hp = Mathf.Clamp(Hp + restore - drain, 0f, EnergyMax);
		_heart.Current = Hp;
	}

	private void UpdateCooldowns(double delta)
	{
		for (int i = 0; i < _cooldowns.Length; i++)
		{
			if (_cooldowns[i] > 0f)
				_cooldowns[i] = Mathf.Max(_cooldowns[i] - (float)delta, 0f);
		}
	}

	private void HandleWeapons()
	{
		// 笔：按下即发射，与按住无关
		if (Input.IsActionJustPressed(SlotAction[0]))
			Fire(ActWeaponScene, 0, held: false);

		// 纸 / 橡皮：每个按键各自独立。按住=续力，松开=自动回收；两把可同时在场，互不打断
		for (int slot = 1; slot <= 2; slot++)
		{
			if (Input.IsActionJustPressed(SlotAction[slot]))
				Fire(slot == 1 ? ExpressWeaponScene : AcceptWeaponScene, slot, held: true);
			PumpHeld(slot);
		}
	}

	// 按槽位推进按住型武器：按住续力，松开开始回收；实例回收后清空槽位以便再发。
	private void PumpHeld(int slot)
	{
		IWeapon w = _heldWeapon[slot];
		if (w == null)
			return;

		// 实例已回收 / 销毁：清空该槽
		if (!GodotObject.IsInstanceValid((GodotObject)w) || (w as Node)?.IsQueuedForDeletion() == true)
		{
			_heldWeapon[slot] = null;
			return;
		}

		w.IsHeld = Input.IsActionPressed(SlotAction[slot]);
	}

	// 实例化并发射一件武器，并让 GameManager 订阅它的命中信号（裁决唯一入口）。
	private void Fire(PackedScene scene, int slot, bool held)
	{
		if (scene == null || _cooldowns[slot] > 0f) return;

		Node inst = scene.Instantiate();
		if (inst is not IWeapon weapon)
		{
			GD.PushWarning($"武器场景 {scene.ResourcePath} 未实现 IWeapon 接口");
			return;
		}
		var body = (Node2D)inst;
		GetTree().CurrentScene.AddChild(inst);

		// 无角度自动纠正：发射即沿当前朝向直线打出，瞄准容差由场景里的碰撞箱半径提供
		weapon.Launch(this, Facing);
		body.GlobalPosition = GlobalPosition;
		_cooldowns[slot] = weapon.Cooldown;

		// 命中烦恼后统一交给裁决者
		if (GetTree().GetFirstNodeInGroup("game_manager") is GameManager gm)
			gm.AttachWeapon(body);

		if (held)
		{
			// 同键重按：先让同槽旧实例开始回收，再出新实例（不影响另一把按住型武器）
			IWeapon prev = _heldWeapon[slot];
			if (prev != null && GodotObject.IsInstanceValid((GodotObject)prev))
				prev.IsHeld = false;
			_heldWeapon[slot] = weapon;
		}
	}
}
