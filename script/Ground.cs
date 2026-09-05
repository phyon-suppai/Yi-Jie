using Godot;

/// <summary>
/// 色块地面:整块活动区 = 中紫底色,四边一圈亮紫细框,
/// 画面其余部分由背景清屏色(深紫近黑)兜底。
/// </summary>
public partial class Ground : Node2D
{
	[Export] public float Width { get; set; } = 2600f;
	[Export] public float Height { get; set; } = 1700f;

	public Rect2 ArenaRect => new Rect2(-Width / 2f, -Height / 2f, Width, Height);

	public override void _Ready()
	{
		AddToGroup("ground");
	}

	public override void _Draw()
	{
		Rect2 r = ArenaRect;
		// 中紫活动地面
		DrawRect(r, Palette.Ground);
		// 亮紫四周边框(世界边界 = 「暗紫纸上的亮框」)
		DrawRect(r, Palette.PurpleFrame, false, 8f);
	}
}
