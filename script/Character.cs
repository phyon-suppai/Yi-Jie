using Godot;

public partial class Character : CharacterBody2D
{
	[Export] public float HorizontalSpeed;
	[Export] public float VerticalSpeed;
	[Export] public float BleedRate;

	[Export] public float Hp;

	[ExportGroup("武器")]
	/// <summary>笔·行动（按下即发射，单体直线）</summary>
	[Export] public PackedScene ActWeaponScene { get; set; }

	/// <summary>纸·表达（按住伸出，松手返航，群体）</summary>
	[Export] public PackedScene ExpressWeaponScene { get; set; }

	/// <summary>橡皮·接纳（按住扩张，松手收缩，群体）</summary>
	[Export] public PackedScene AcceptWeaponScene { get; set; }

	/// <summary>当前朝向（单位向量）。静止时保持最后一次移动方向。</summary>
	public Vector2 Facing { get; private set; } = Vector2.Down;

	private Timer _timer;

	private AnimatedSprite2D _player;
	private Heart _heart;

	// 每个槽位各自在用的按住型武器实例（0=笔·瞬时；1=纸；2=橡皮）。各管各的键，互不打断。
	private readonly IWeapon[] _heldWeapon = new IWeapon[3];
	private static readonly string[] SlotAction = { "action 1", "action 2", "action 3" };
	private readonly float[] _cooldowns = new float[3];

	public override void _Ready()
	{
		_player = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_timer = new Timer();
		_timer.WaitTime = 1.0;
		_timer.Timeout += Bleed;
		AddChild(_timer);
		_timer.Start();
		_heart = GetNode<Heart>("Heart");
		_heart.Total = Hp;
		_heart.Current = Hp;
	}

	private void Bleed()
	{
		Hp -= BleedRate;
		_heart.Current = Hp;
		if (Hp <= 0)
		{
			Hp = 0;
			_heart.Current = 0;
			GD.Print("精力耗尽");
		}
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

		_player.Play(input == Vector2.Zero ? "idle" : "run");

		MoveAndSlide();
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

	/// <summary>按槽位推进按住型武器：按住续力，松开开始回收；实例回收后清空槽位以便再发。</summary>
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

	/// <summary>实例化并发射一件武器</summary>
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
