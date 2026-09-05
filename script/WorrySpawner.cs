using Godot;

/// <summary>
/// 烦恼生成器(不可摧毁):靠近(进入 ActivationRange)才计时产怪。
/// 产怪类型按权重决定 → 三关的「比例差异」= 各生成器权重组合。
/// 生成动作由 GameManager 统一执行(它持有场景引用与场上计数)。
/// </summary>
public partial class WorrySpawner : Node2D
{
	[ExportGroup("生成节奏")]
	[Export(PropertyHint.Range, "0.1,10,0.1")] public float Interval { get; set; } = 1.2f;
	[Export(PropertyHint.Range, "60,900,10")] public float ActivationRange { get; set; } = 620f;
	[Export(PropertyHint.Range, "1,20,1")] public int MaxAlive { get; set; } = 8;

	[ExportGroup("类型权重(比例即关卡身份)")]
	[Export(PropertyHint.Range, "0,20,1")] public int DoubtWeight { get; set; } = 5;
	[Export(PropertyHint.Range, "0,20,1")] public int PressureWeight { get; set; } = 1;
	[Export(PropertyHint.Range, "0,20,1")] public int LonelinessWeight { get; set; } = 1;

	public float Timer { get; set; }

	public override void _Ready()
	{
		AddToGroup("spawner");
		Timer = Interval * 0.5f; // 进入范围后稍作停顿即开始产
	}

	/// <summary>按权重抽一种烦恼类型;权重全 0 返回 null(该生成器停摆)。</summary>
	public WorryType? PickWeighted(RandomNumberGenerator rng)
	{
		int doubt = DoubtWeight;
		int pressure = PressureWeight;
		int lonely = LonelinessWeight;
		int total = doubt + pressure + lonely;
		if (total <= 0) return null;

		int roll = rng.RandiRange(1, total);
		if (roll <= doubt) return WorryType.Doubt;
		roll -= doubt;
		if (roll <= pressure) return WorryType.Pressure;
		return WorryType.Loneliness;
	}

	public override void _Draw()
	{
		// 生成器本体:暗紫小方块 + 亮紫细框,「门/生成」共用紫色系
		float half = 22f;
		DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), Palette.PurpleCore.Darkened(0.25f));
		DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), Palette.PurpleFrame, false, 3f);
		// 中心小圆点:待命微光
		DrawCircle(Vector2.Zero, 5f, Palette.PurpleFrame);
	}
}
