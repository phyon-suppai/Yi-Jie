using Godot;

/// <summary>
/// 特殊事件方块生成器:按间隔从本关事件列表中轮询生成,
/// 事件进行期间暂停计时,同一时刻只存在一个特殊方块。
/// </summary>
public partial class SpecialEventSpawner : Node2D
{
	[Export] public PackedScene BlockScene { get; set; }
	[Export] public string[] EventIds { get; set; }
	[Export] public float Interval { get; set; } = 14f;
	[Export] public float SpawnDistance { get; set; } = 1200f;
	[Export(PropertyHint.Range, "0,300,1")] public float FirstSpawnAtAchieve { get; set; } = 25f;
	[Export(PropertyHint.Range, "0,300,1")] public float SecondSpawnAtAchieve { get; set; } = 140f;
	[Export(PropertyHint.Range, "0,300,1")] public float ThirdSpawnAtAchieve { get; set; } = 240f;

	private GameManager _gm;
	private Node2D _player;
	private int _index;
	private float _timer;
	private int _spawnedCount;
	private readonly float[] _achieveThresholds;
	private readonly RandomNumberGenerator _rng = new();

	public SpecialEventSpawner()
	{
		_achieveThresholds = new[] { FirstSpawnAtAchieve, SecondSpawnAtAchieve, ThirdSpawnAtAchieve };
	}

	public override void _Ready()
	{
		_rng.Randomize();
		_timer = Interval * 0.25f;
	}

	public override void _Process(double delta)
	{
		// 延迟获取:保证 GameManager/Player 的 _Ready 已执行完毕
		if (_gm == null)
			_gm = GetNodeOrNull<GameManager>("../GameManager") ?? GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
		if (_player == null)
			_player = GetNodeOrNull<Node2D>("../Player") ?? GetTree().GetFirstNodeInGroup("player") as Node2D;

		if (_gm == null || _player == null || !GodotObject.IsInstanceValid(_player)) return;
		if (EventIds == null || EventIds.Length == 0) return;

		// 已有弹窗打开或场上仍有未触发的特殊方块时,暂停计时并禁止生成
		if (_gm.EventOpen || HasAliveSpecialBlock())
		{
			_timer = 0f;
			return;
		}

		float achieve = _gm.EnergySystem.AchieveFraction * ReactionTable.MaxAchieve;
		if (_spawnedCount < _achieveThresholds.Length && achieve >= _achieveThresholds[_spawnedCount])
		{
			SpawnNext();
			return;
		}

		_timer += (float)delta;
		if (_timer >= Interval)
		{
			_timer = 0f;
			SpawnNext();
		}
	}

	private bool HasAliveSpecialBlock()
	{
		foreach (Node n in GetTree().GetNodesInGroup("special_event_block"))
		{
			if (GodotObject.IsInstanceValid(n) && !n.IsQueuedForDeletion())
				return true;
		}
		return false;
	}

	private void SpawnNext()
	{
		if (BlockScene == null) return;
		string id = EventIds[_index % EventIds.Length];
		_index++;
		_spawnedCount++;

		Node inst = BlockScene.Instantiate();
		if (inst is not SpecialEventBlock block) { inst.QueueFree(); return; }
		block.EventId = id;
		GetTree().CurrentScene.AddChild(block);

		float ang = _rng.RandfRange(0f, Mathf.Tau);
		block.GlobalPosition = _player.GlobalPosition
			+ new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * SpawnDistance;

		GD.Print($"[特殊事件] 生成 {id} 于 {block.GlobalPosition} (成就 {_gm.EnergySystem.AchieveFraction * ReactionTable.MaxAchieve:F0})");
	}
}
