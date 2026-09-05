using Godot;

/// <summary>
/// 唯一裁决点：
/// - 武器命中烦恼 → 烦恼消散、成就 +AchievePerWorry；
/// - 场上烦恼按需补充（麻烦越多精力掉得越快由此持续存在）；
/// - 精力归零 → 判负；成就满 → 通关结算。两种结局都在短暂展示后自动重开新一轮（单关验证）。
/// </summary>
public partial class GameManager : Node
{
	[ExportGroup("裁决数值")]
	/// <summary>解决一个烦恼增加的成就</summary>
	[Export(PropertyHint.Range, "1,50,1")]
	public int AchievePerWorry { get; set; } = 15;

	/// <summary>通关所需的成就目标</summary>
	[Export(PropertyHint.Range, "1,200,1")]
	public int AchieveGoal { get; set; } = 100;

	[ExportGroup("烦恼补充")]
	/// <summary>场上烦恼目标上限（含初始在场的那几只）</summary>
	[Export(PropertyHint.Range, "1,16,1")]
	public int MaxWorries { get; set; } = 6;

	/// <summary>每隔多少秒补一次怪（场上不足上限时才补）</summary>
	[Export(PropertyHint.Range, "0.5,10,0.5")]
	public float SpawnInterval { get; set; } = 3f;

	/// <summary>新烦恼离玩家的最小 / 最大距离</summary>
	[Export] public float SpawnDistanceMin { get; set; } = 450f;
	[Export] public float SpawnDistanceMax { get; set; } = 900f;

	/// <summary>结算展示多少秒后自动重开新一轮</summary>
	[Export] public float ResultDelay { get; set; } = 3f;

	[ExportGroup("HUD 配色")]
	/// <summary>精力条填充色（蓝）</summary>
	[Export] public Color EnergyColor { get; set; } = new Color(0.24f, 0.62f, 1f);

	/// <summary>成就条填充色（黄）</summary>
	[Export] public Color AchieveColor { get; set; } = new Color(1f, 0.79f, 0.22f);

	/// <summary>竖条槽底色</summary>
	[Export] public Color SlotColor { get; set; } = new Color(0.05f, 0.07f, 0.11f, 0.72f);

	/// <summary>可生成的烦恼场景（未绑定对应项则跳过该类型）</summary>
	[Export] public PackedScene DoubtScene { get; set; }
	[Export] public PackedScene PressureScene { get; set; }
	[Export] public PackedScene LonelinessScene { get; set; }

	/// <summary>当前成就（0 ~ AchieveGoal）</summary>
	public int Achieve { get; private set; }

	/// <summary>是否已进入结算态（胜利或失败），此后不再补怪 / 裁决 / 计分</summary>
	public bool Finished { get; private set; }

	// HUD：右侧两根竖条（左蓝=精力，右黄=成就）+ 顶部结算文案
	private ProgressBar _energyBar;
	private ProgressBar _achieveBar;
	private Label _message;

	private Character _player;
	private float _spawnTimer;
	private RandomNumberGenerator _rng = new();
	private PackedScene[] _pool;

	public override void _Ready()
	{
		AddToGroup("game_manager");
		_player = GetNode<Character>("../Player");

		// 用 OrNull：没有 HUD 的场景（如后续关卡）也不会崩
		_energyBar = GetNodeOrNull<ProgressBar>("../Hud/Bars/EnergyBar");
		_achieveBar = GetNodeOrNull<ProgressBar>("../Hud/Bars/AchieveBar");
		_message = GetNodeOrNull<Label>("../Hud/Message");

		if (_energyBar != null)
		{
			_energyBar.MaxValue = _player.EnergyMax;
			ApplyBarStyle(_energyBar, EnergyColor);
		}
		if (_achieveBar != null)
		{
			_achieveBar.MaxValue = AchieveGoal;
			ApplyBarStyle(_achieveBar, AchieveColor);
		}

		_rng.Randomize();
		_pool = new PackedScene[] { DoubtScene, PressureScene, LonelinessScene };
	}

	public override void _Process(double delta)
	{
		if (Finished)
			return;

		UpdateHud();
		SpawnWorries((float)delta);

		if (_player.Hp <= 0f)
			Finish("精力耗尽，闯关失败…即将重新开始");
		else if (Achieve >= AchieveGoal)
			Finish("成就圆满，闯关成功！即将开启新一轮…");
	}

	// 用代码上色而非场景里的 theme_override：Godot 编辑器保存场景时会清掉手工写的 StyleBoxFlat 资源
	private void ApplyBarStyle(ProgressBar bar, Color fillColor)
	{
		var slot = new StyleBoxFlat();
		slot.BgColor = SlotColor;
		slot.CornerRadiusTopLeft = 6;
		slot.CornerRadiusTopRight = 6;
		slot.CornerRadiusBottomRight = 6;
		slot.CornerRadiusBottomLeft = 6;

		var fill = new StyleBoxFlat();
		fill.BgColor = fillColor;
		fill.CornerRadiusTopLeft = 6;
		fill.CornerRadiusTopRight = 6;
		fill.CornerRadiusBottomRight = 6;
		fill.CornerRadiusBottomLeft = 6;

		bar.AddThemeStyleboxOverride("background", slot);
		bar.AddThemeStyleboxOverride("fill", fill);
	}

	private void UpdateHud()
	{
		if (_energyBar != null)
			_energyBar.Value = _player.Hp;
		if (_achieveBar != null)
			_achieveBar.Value = Achieve;
	}

	private void SpawnWorries(float delta)
	{
		_spawnTimer += delta;
		if (_spawnTimer < SpawnInterval)
			return;
		_spawnTimer = 0f;

		int count = GetTree().GetNodesInGroup("worry").Count;
		if (count >= MaxWorries)
			return;

		PackedScene pick = _pool[_rng.RandiRange(0, _pool.Length - 1)];
		if (pick == null)
			return;

		Node inst = pick.Instantiate();
		if (inst is not Area2D worry)
		{
			inst.QueueFree();
			return;
		}

		// 在玩家周围随机一圈落点，避免当面刷怪
		float angle = _rng.RandfRange(0f, Mathf.Tau);
		float dist = _rng.RandfRange(SpawnDistanceMin, SpawnDistanceMax);
		Vector2 pos = _player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

		GetTree().CurrentScene.AddChild(worry);
		worry.GlobalPosition = pos;
		worry.Scale = Vector2.One * _rng.RandfRange(0.25f, 0.45f);
	}

	/// <summary>由 Character 在发射时调用：订阅这件武器的命中上报。</summary>
	public void AttachWeapon(Node2D weapon)
	{
		switch (weapon)
		{
			case Act act:   act.WorryHit   += OnWorryHit; break;
			case Express ex: ex.WorryHit   += OnWorryHit; break;
			case Accept acc: acc.WorryHit  += OnWorryHit; break;
		}
	}

	/// <summary>裁决：命中烦恼即消散（达成移除一个「麻烦」），累计成就。</summary>
	private void OnWorryHit(Node2D worry)
	{
		if (Finished || worry == null || !GodotObject.IsInstanceValid(worry))
			return;
		if (!worry.IsInGroup("worry") || worry.IsQueuedForDeletion())
			return;

		worry.QueueFree();
		Achieve = Mathf.Min(Achieve + AchievePerWorry, AchieveGoal);
		GD.Print($"烦恼消散 +{AchievePerWorry} 成就，当前 {Achieve}/{AchieveGoal}");
	}

	private void Finish(string message)
	{
		if (Finished)
			return;
		Finished = true;
		if (_message != null)
			_message.Text = message;
		_player.SetPhysicsProcess(false); // 结算时冻结玩家行动，便于看清结局

		GetTree().CreateTimer(ResultDelay).Timeout += () =>
		{
			if (GetTree() != null)
				GetTree().ReloadCurrentScene();
		};
	}
}
