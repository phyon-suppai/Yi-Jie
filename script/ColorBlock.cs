using Godot;

/// <summary>色块形状模式</summary>
public enum ColorBlockShape
{
	Square,  // 方体(疑/压/孤/玩家/生成器)
	Bar,     // 长条(行动的直线弹道)
	Diamond, // 菱形(表达的回旋镖)
	Ring,    // 圆环(接受的范围特效)
	Triangle // 小三角(朝向指示,可作内部标记)
}

/// <summary>
/// 通用「外亮内暗」自绘色块组件(Node2D)。
/// 世界内所有实体外观都由它承担,零贴图。父脚本改属性后调 QueueRedraw()。
/// 画法:先用亮色铺满外形,再用同色相暗芯按边框厚度向内收缩覆盖中心 → 天然「亮框暗芯」。
/// </summary>
public partial class ColorBlock : Node2D
{
	[Export] public ColorBlockShape Shape { get; set; } = ColorBlockShape.Square;
	[Export] public Color Frame { get; set; } = Palette.PurpleFrame;
	[Export] public Color Core { get; set; } = Palette.PurpleCore;
	[Export] public float Width { get; set; } = 64f;
	[Export] public float Height { get; set; } = 64f;
	[Export] public float Thickness { get; set; } = 8f; // 亮框厚度(暗芯收缩量)

	/// <summary>批量配置一次(改色块外观唯一入口),随后立即重绘。</summary>
	public void SetVisual(ColorBlockShape shape, Color frame, Color core,
		float w, float h, float thickness)
	{
		Shape = shape;
		Frame = frame;
		Core = core;
		Width = w;
		Height = h;
		Thickness = thickness;
		QueueRedraw();
	}

	/// <summary>仅改形状(弹道变化/朝向等)。</summary>
	public void SetShape(ColorBlockShape shape)
	{
		Shape = shape;
		QueueRedraw();
	}

	/// <summary>仅改尺寸(烦恼体型成长、呼吸动画等)。</summary>
	public void SetSize(float w, float h)
	{
		Width = w;
		Height = h;
		QueueRedraw();
	}

	/// <summary>仅改配色(命中闪光、冷却熄灭等)。</summary>
	public void SetColors(Color frame, Color core)
	{
		Frame = frame;
		Core = core;
		QueueRedraw();
	}

	public override void _Draw()
	{
		switch (Shape)
		{
			case ColorBlockShape.Square:
			case ColorBlockShape.Bar:
				DrawQuad(Width, Height);
				break;
			case ColorBlockShape.Diamond:
				DrawDiamond();
				break;
			case ColorBlockShape.Ring:
				DrawRing();
				break;
			case ColorBlockShape.Triangle:
				DrawTriangle();
				break;
		}
	}

	// 先亮色满铺,再暗芯收缩 → 边框宽度 = Thickness
	private void DrawQuad(float w, float h)
	{
		DrawRect(new Rect2(-w / 2f, -h / 2f, w, h), Frame);
		float inset = Thickness;
		float iw = Mathf.Max(w - inset * 2f, 0f);
		float ih = Mathf.Max(h - inset * 2f, 0f);
		if (iw > 0f && ih > 0f)
			DrawRect(new Rect2(-iw / 2f, -ih / 2f, iw, ih), Core);
	}

	private void DrawDiamond()
	{
		float t = Mathf.Min(Thickness, Mathf.Min(Width, Height) * 0.5f);
		DrawFilledDiamond(Width, Height, Frame);
		float innerW = Mathf.Max(Width - t * 2f, 0f);
		float innerH = Mathf.Max(Height - t * 2f, 0f);
		if (innerW > 0f && innerH > 0f)
			DrawFilledDiamond(innerW, innerH, Core);
	}

	private void DrawFilledDiamond(float w, float h, Color c)
	{
		Vector2[] pts =
		{
			new Vector2(0f, -h / 2f),
			new Vector2(w / 2f, 0f),
			new Vector2(0f, h / 2f),
			new Vector2(-w / 2f, 0f)
		};
		DrawColoredPolygon(pts, c);
	}

	private void DrawRing()
	{
		float outer = Mathf.Max(Width, Height) / 2f;
		DrawArc(Vector2.Zero, outer, 0f, Mathf.Tau, 48, Frame, Thickness, true);
		float inner = Mathf.Max(outer - Thickness * 1.5f, 1f);
		// 内沿一条细芯线,维持「同色相」:红环+暗芯,否则空心会只剩亮框没有暗芯
		DrawArc(Vector2.Zero, inner, 0f, Mathf.Tau, 48, Core, Mathf.Max(Thickness * 0.4f, 2f), true);
	}

	private void DrawTriangle()
	{
		float half = Width / 2f;
		float h = Height;
		Vector2[] pts =
		{
			new Vector2(half, 0f),
			new Vector2(-half, -half),
			new Vector2(-half, half)
		};
		DrawColoredPolygon(pts, Frame);
	}
}
