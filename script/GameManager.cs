using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 唯一裁决点 + 关卡协调者:
/// - 持有 EnergySystem(旧版精力/成就规则:持续流失 + 站桩回能,耗尽判负);
/// - 相克裁决:同色武器(克制)命中烦恼造成伤害直至消散;异色武器无效(已移除分裂/debuff);
/// - 用不可摧毁的 WorrySpawner 按权重供给烦恼(玩家跑赢生成速度);
/// - 驱动 VisionOverlay 暗角与自绘 HUD;成就满 100 即判胜,3 秒后重开新一轮。
/// </summary>
public partial class GameManager : Node
{
	[ExportGroup("关卡参数")]
	[Export(PropertyHint.Range, "0,200,1")] public float StartEnergy { get; set; } = 100f;
	[Export(PropertyHint.Range, "50,200,5")] public float MaxEnergy { get; set; } = 100f;   // 精力上限(血条厚度)
	[Export(PropertyHint.Range, "50,500,10")] public float AchieveGoal { get; set; } = 200f; // 本关需要达成的成就值
	[Export] public string StageLabel { get; set; } = ""; // 左上角显示的阶段名称
	[Export(PropertyHint.Range, "1,10,0.5")] public float ResultDelay { get; set; } = 3f; // 结算后重开倒计时

	[ExportGroup("烦恼强度(本关统一覆盖)")]
	[Export(PropertyHint.Range, "5,30,1")] public float WorryInitialHp { get; set; } = 10f;
	[Export(PropertyHint.Range, "20,100,1")] public float WorryMaxHp { get; set; } = 40f;
	[Export(PropertyHint.Range, "1,8,0.1")] public float WorryHpGrowRate { get; set; } = 2.5f;
	[Export(PropertyHint.Range, "30,150,1")] public float WorryMaxSpeed { get; set; } = 65f;
	[Export(PropertyHint.Range, "100,500,10")] public float WorryAccel { get; set; } = 180f;
	[Export(PropertyHint.Range, "20,50,1")] public float WorryBodyRadius { get; set; } = 28f;

	[ExportGroup("烦恼场景(裁决/生成用)")]
	[Export] public PackedScene DoubtScene { get; set; }
	[Export] public PackedScene PressureScene { get; set; }
	[Export] public PackedScene LonelinessScene { get; set; }

	[ExportGroup("场地约束")]
	[Export(PropertyHint.Range, "0,200,1")] public float ClampInset { get; set; } = 46f;     // 实体限制在地面内的边距
	[Export(PropertyHint.Range, "100,1500,10")] public float NeighborRadius { get; set; } = 640f; // 生成器附近已有烦恼的判定半径

	// ---- 运行时 ----
	public EnergySystem EnergySystem { get; private set; }
	public IReadOnlyList<Worry> Worries => _worries;

	private Character _player;
	private readonly List<Worry> _worries = new();
	private readonly List<WorrySpawner> _spawners = new();
	private Ground _ground;
	private VisionOverlay _overlay;
	private Hud _hud;
	private AudioManager _audio;
	private readonly RandomNumberGenerator _rng = new();

	// 场景里与玩家"并列"的相机(已在各关卡 .tscn 中声明,Godot 会将其自动激活为当前相机)。
	// 这里只在物理帧平滑移动它,不再运行时 AddChild/MakeCurrent,规避激活时序问题。
	private Camera2D _camera;
	private const float CameraSmoothSpeed = 4f; // exp 平滑权重系数,越小惯性越强/尾随感越明显,越大跟得越紧


	private bool _finished;                 // 是否已进入结算态(胜/负)
	private int _seedLeft = 2;              // 开局先让两个生成器立刻产一只,避免空场发呆
	public bool EventOpen { get; private set; } // 特殊事件弹窗是否打开
	private EventDialog _eventDialog;
	private SpecialEventData _currentEventData;
	private bool _pauseOpen;                // 暂停菜单是否打开

	public override void _Ready()
	{
		AddToGroup("game_manager");
		_rng.Randomize();
		EnergySystem = new EnergySystem(StartEnergy, MaxEnergy, AchieveGoal);

		_player = GetTree().GetFirstNodeInGroup("player") as Character;
		if (_player == null)
			_player = GetNodeOrNull<Character>("../Player");

		// 引用关卡场景(.tscn)中声明的、与玩家并列的 Camera2D。
		// 它随父场景进入树,此刻已在树内,直接激活为当前相机是安全的(不再运行时创建)。
		_camera = GetNodeOrNull<Camera2D>("../Camera2D");
		if (_camera != null)
		{
			_camera.MakeCurrent();
			// 初始直接对齐玩家,避免从原点慢慢"飘"向玩家造成开局画面错位
			if (_player != null)
				_camera.GlobalPosition = _player.GlobalPosition;
		}
		else
			GD.PushWarning("[GM] 未在关卡场景找到并列 Camera2D(../Camera2D),请确认 .tscn 中已添加。");


		// HUD(自绘)与暗角在运行时创建,零 UI 场景依赖
		var hudLayer = new CanvasLayer { Layer = 40 };
		AddChild(hudLayer);
		_hud = new Hud();
		hudLayer.AddChild(_hud);

		_overlay = new VisionOverlay();
		AddChild(_overlay);

		_audio = new AudioManager();
		AddChild(_audio);
		CallDeferred(nameof(PlayLevelStartSound));

		CallDeferred(nameof(CollectSpawnersAndPortal));
	}

	private void PlayLevelStartSound() => _audio?.PlayLevelStart();

	private void CollectSpawnersAndPortal()
	{
		foreach (Node n in GetTree().GetNodesInGroup("spawner"))
			if (n is WorrySpawner s) _spawners.Add(s);
		_ground = GetTree().GetFirstNodeInGroup("ground") as Ground;
	}

	/// <summary>把玩家与烦恼都限制在「纸面」内,避免追逐战打出场外。</summary>
	private void ClampEntities()
	{
		if (_ground == null) return;
		Rect2 r = _ground.ArenaRect.Grow(-ClampInset);
		_player.GlobalPosition = ClampToRect(_player.GlobalPosition, r);
		foreach (Worry w in _worries)
		{
			if (GodotObject.IsInstanceValid(w))
				w.GlobalPosition = ClampToRect(w.GlobalPosition, r);
		}
	}

	private static Vector2 ClampToRect(Vector2 v, Rect2 r)
	{
		return new Vector2(Mathf.Clamp(v.X, r.Position.X, r.End.X), Mathf.Clamp(v.Y, r.Position.Y, r.End.Y));
	}

	// 在物理帧平滑跟随玩家:与 CharacterBody2D 玩家同频,减少错位。
	// 权重 weight = 1 - exp(-speed*delta) 为帧率无关的指数平滑,避免 lerp 系数过小"卡住",
	// 也避免过高导致相机生硬。speed 取 12 既能跟住角色又不过度拖影。
	public override void _PhysicsProcess(double delta)
	{
		FollowPlayer((float)delta);
	}

	private void FollowPlayer(float delta)
	{
		if (_camera == null || !GodotObject.IsInstanceValid(_camera)) return;
		if (_player == null || !GodotObject.IsInstanceValid(_player)) return;

		float weight = 1f - Mathf.Exp(-CameraSmoothSpeed * delta);
		_camera.GlobalPosition = _camera.GlobalPosition.Lerp(_player.GlobalPosition, weight);
	}

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("ui_cancel") && !_pauseOpen)
		{
			OpenPauseMenu();
			return;
		}

		float d = (float)delta;

		if (!_finished)
		{
			// 精力:没事慢慢掉;被缠身烦恼大量掉;站桩回能
			int contactCount = CountContactWorries();
			EnergySystem.Tick(d, contactCount, _player != null && _player.Resting);
			SpawnWorries(d);
			UpdateVisionAndHud();
			CheckEnd();
		}

		ClampEntities(); // 结算期间也把实体限制在纸面内
	}

	private void OpenPauseMenu()
	{
		if (_pauseOpen || _finished) return;
		_pauseOpen = true;
		GetTree().Paused = true;

		var pause = GD.Load<PackedScene>("res://scenes/ui/pause_overlay.tscn");
		if (pause != null)
		{
			var overlay = pause.Instantiate<PauseOverlay>();
			overlay.TreeExited += () => _pauseOpen = false;
			GetTree().CurrentScene.AddChild(overlay);
		}
		else
		{
			_pauseOpen = false;
			GetTree().Paused = false;
		}
	}

	/// <summary>统计正在「缠身」玩家的烦恼数量(距离足够近才计入,太远只算普通压力不大量掉精力)。</summary>
	private int CountContactWorries()
	{
		if (_player == null) return 0;
		Vector2 p = _player.GlobalPosition;
		int count = 0;
		foreach (Worry w in _worries)
		{
			if (!GodotObject.IsInstanceValid(w) || w.IsQueuedForDeletion()) continue;
			float dist = w.GlobalPosition.DistanceTo(p) - w.Radius;
			if (dist <= ReactionTable.ContactRange)
				count++;
		}
		return count;
	}

	private void CheckEnd()
	{
		if (_finished) return;
		if (EnergySystem.Energy <= 0f)
			Finish("精力耗尽，闯关失败…即将重新开始");
		else if (EnergySystem.Achieve >= EnergySystem.MaxAchieve)
			Finish("成就圆满，闯关成功！即将开始新一轮…");
	}

	[Export] public string NextLevelPath { get; set; } = ""; // 胜利后进入的下一关路径

	public void PlayShoot() => _audio?.PlayShoot();
	public void PlayDissolve() => _audio?.PlayDissolve();
	public void PlayWrongHit() => _audio?.PlayHit();
	public void PlayHurt() => _audio?.PlayHurt();
	public void PlayEventCorrect() => _audio?.PlayEventCorrect();
	public void PlayEventPenalty() => _audio?.PlayEventPenalty();

	private void Finish(string msg)
	{
		_finished = true;
		GD.Print(msg);

		bool victory = EnergySystem.Achieve >= EnergySystem.MaxAchieve;
		if (victory) _audio?.PlayWin();
		else _audio?.PlayLose();

		GetTree().CreateTimer(0.6).Timeout += () =>
		{
			if (GetTree() == null) return;
			if (victory)
			{
				if (!string.IsNullOrEmpty(NextLevelPath))
				{
					GetTree().ChangeSceneToFile(NextLevelPath);
				}
				else
				{
					GetTree().ReloadCurrentScene();
				}
				return;
			}

			var defeat = GD.Load<PackedScene>("res://scenes/ui/defeat_panel.tscn");
			if (defeat != null)
				GetTree().CurrentScene.AddChild(defeat.Instantiate());
			else
				GetTree().ReloadCurrentScene();
		};
	}

	// ------------------------------------------------------------------ 特殊事件
	public void OpenEvent(string eventId)
	{
		if (EventOpen || _finished) return;
		if (!SpecialEventLibrary.All.TryGetValue(eventId, out _currentEventData)) return;

		EventOpen = true;
		_eventDialog = new EventDialog();
		AddChild(_eventDialog);
		// 传入"关闭回调":仅当弹窗真正消失(闪烁播放完毕)时才复位 EventOpen,
		// 避免此前用固定 2.5s 定时器导致弹窗已关、角色却仍被禁止读 J/K/L 的问题。
		_eventDialog.ShowEvent(_currentEventData, OnEventChoice, OnEventDialogClosed);
	}

	private void OnEventChoice(int index)
	{
		if (_currentEventData == null || index < 0 || index >= _currentEventData.Options.Length) return;
		var opt = _currentEventData.Options[index];

		EnergySystem.AddEnergy(opt.EnergyDelta);
		if (opt.SpawnPressureCount > 0)
			SpawnPenaltyWorries(opt.SpawnPressureCount);

		if (opt.IsCorrect) PlayEventCorrect();
		else PlayEventPenalty();
	}

	private void OnEventDialogClosed()
	{
		EventOpen = false;
		_currentEventData = null;
		_eventDialog = null;
	}

	private void SpawnPenaltyWorries(int count)
	{
		if (PressureScene == null || _player == null || _ground == null) return;

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		Rect2 r = _ground.ArenaRect.Grow(-ClampInset);

		for (int i = 0; i < count; i++)
		{
			Node inst = PressureScene.Instantiate();
			if (inst is not Worry worry) { inst.QueueFree(); continue; }
			ApplyWorryProfile(worry);

			GetTree().CurrentScene.AddChild(worry);
			float ang = rng.RandfRange(0f, Mathf.Tau);
			float dist = rng.RandfRange(220f, 460f);
			Vector2 pos = _player.GlobalPosition + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
			worry.GlobalPosition = ClampToRect(pos, r);
			RegisterWorry(worry);
		}
	}

	// ------------------------------------------------------------------ 生成
	private void SpawnWorries(float d)
	{
		foreach (WorrySpawner s in _spawners)
		{
			if (s == null || !GodotObject.IsInstanceValid(s)) continue;
			if (s.GlobalPosition.DistanceTo(_player.GlobalPosition) > s.ActivationRange)
				continue; // 未激活

			if (_seedLeft > 0)
			{
				_seedLeft--;
				SpawnFrom(s);
			}

			s.Timer += d;
			if (s.Timer < s.Interval) continue;

			int near = _worries.Count(w => w.GlobalPosition.DistanceTo(s.GlobalPosition) <= NeighborRadius);
			if (near >= s.MaxAlive) continue;
			s.Timer = 0f;
			SpawnFrom(s);
		}
	}

	private void SpawnFrom(WorrySpawner s)
	{
		WorryType? picked = s.PickWeighted(_rng);
		if (picked == null) return;
		PackedScene scene = SceneFor(picked.Value);
		if (scene == null) return;

		Node inst = scene.Instantiate();
		if (inst is not Worry worry) { inst.QueueFree(); return; }
		ApplyWorryProfile(worry);

		// 生成器附近随机落点
		float ang = _rng.RandfRange(0f, Mathf.Tau);
		Vector2 offset = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * _rng.RandfRange(60f, 190f);
		GetTree().CurrentScene.AddChild(worry);
		worry.GlobalPosition = s.GlobalPosition + offset;
		RegisterWorry(worry);
	}

	private void ApplyWorryProfile(Worry w)
	{
		w.InitialHp = WorryInitialHp;
		w.MaxHp = WorryMaxHp;
		w.HpGrowRate = WorryHpGrowRate;
		w.MaxSpeed = WorryMaxSpeed;
		w.Accel = WorryAccel;
		w.BodyRadius = WorryBodyRadius;
	}

	private PackedScene SceneFor(WorryType t)
	{
		return t switch
		{
			WorryType.Doubt => DoubtScene,
			WorryType.Pressure => PressureScene,
			WorryType.Loneliness => LonelinessScene,
			_ => null
		};
	}

	private void RegisterWorry(Worry w)
	{
		if (_worries.Contains(w)) return;
		_worries.Add(w);
		w.TreeExited += () => _worries.Remove(w);
	}

	/// <summary>击杀确认后立即移出计数列表(节点仍在播放消散动画,稍后自行释放)。</summary>
	private void RemoveWorry(Worry w) => _worries.Remove(w);

	// ------------------------------------------------------------------ 裁决
	/// <summary>由 Character 发射时调用:订阅这件武器实例的命中上报。</summary>
	public void AttachWeapon(Node2D weapon)
	{
		switch (weapon)
		{
			case Act a: a.WorryHit += (w) => OnWorryHit(w, WeaponType.Act); break;
			case Express ex: ex.WorryHit += (w) => OnWorryHit(w, WeaponType.Express); break;
			case Accept ac: ac.WorryHit += (w) => OnWorryHit(w, WeaponType.Accept); break;
		}
	}

	/// <summary>唯一裁决点:同色武器(克制)命中烦恼造成伤害直至消散;异色武器无效(已移除分裂/debuff)。</summary>
	private void OnWorryHit(Node2D worry, WeaponType weaponType)
	{
		if (_finished) return;
		if (worry == null || !GodotObject.IsInstanceValid(worry) || worry.IsQueuedForDeletion())
			return;
		if (worry is not Worry target)
			return;
		if (!_worries.Contains(target)) return;

		if (!ReactionTable.IsCounter(target.Kind, weaponType))
		{
			// 误伤:用错颜色的武器命中没有伤害,但扣精力并触发视觉警告
			EnergySystem.ApplyWrongHit();
			target.OnWrongHit();
			PlayWrongHit();
			GD.Print($"误伤 {target.Kind} (用 {weaponType}) → 精力 -{ReactionTable.WrongColorEnergyPenalty:F0}");
			return;
		}

		bool dead = target.TakeDamage(ReactionTable.CounterDamage);
		if (dead)
		{
			EnergySystem.ApplyDissolve(); // 成就 +N
			PlayDissolve();
			RemoveWorry(target);          // 立即移出计数,消散动画期间不再算作场上烦恼
			GD.Print($"消散 {target.Kind} → 成就+{ReactionTable.DissolveAchieveBonus:F0} (当前 {EnergySystem.Achieve:F0}/{EnergySystem.MaxAchieve:F0})");
		}
	}

	// ------------------------------------------------------------------ 视觉
	private void UpdateVisionAndHud()
	{
		if (_overlay == null || _hud == null) return;

		// 视线:精力低于60%开始压暗,30%达到最暗,并以玩家为中心做椭圆透亮区
		float frac = EnergySystem.Energy / EnergySystem.MaxEnergy;
		Vector2 vp = GetViewport().GetVisibleRect().Size;
		Vector2 playerScreen = _player != null
			? _player.GetGlobalTransformWithCanvas().Origin
			: vp * 0.5f;
		_overlay.Refresh(frac, playerScreen, vp);

		_hud.Refresh(
			frac,
			EnergySystem.AchieveFraction,
			_player.CdRemain(0), _player.CdRemain(1), _player.CdRemain(2),
			StageLabel);
	}
}
