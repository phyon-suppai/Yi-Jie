using Godot;

/// <summary>
/// 视线遮罩:CanvasLayer(layer 100)上的全屏径向渐变暗角。
/// - radiusScale(0~1):可视「透亮圈」半径(精力低 / 倦怠 / 透支 / 疏离 debuff 都会缩小它)
/// - darkness(0~1):四周压暗强度(精力越低越暗)
/// 全部由代码生成渐变纹理,零贴图。仅在数值跨档变化时重建纹理,不每帧写。
/// </summary>
public partial class VisionOverlay : CanvasLayer
{
	private TextureRect _rect;
	private float _lastRadiusScale = -1f;
	private float _lastDarkness = -1f;

	public override void _Ready()
	{
		Layer = 100;

		_rect = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_rect.OffsetLeft = 0; _rect.OffsetTop = 0; _rect.OffsetRight = 0; _rect.OffsetBottom = 0;
		AddChild(_rect);
	}

	/// <summary>刷新遮罩。带缓冲:只有数值跨过阶梯才重建纹理。</summary>
	public void Refresh(float radiusScale, float darkness)
	{
		float rs = Mathf.Clamp(radiusScale, 0f, 1f);
		float dk = Mathf.Clamp(darkness, 0f, 1f);

		// 阶梯化避免每帧重建:半径每 0.01、暗度每 0.02 一档
		rs = Mathf.Round(rs * 100f) / 100f;
		dk = Mathf.Round(dk * 50f) / 50f;
		if (Mathf.IsEqualApprox(rs, _lastRadiusScale) && Mathf.IsEqualApprox(dk, _lastDarkness))
			return;

		_lastRadiusScale = rs;
		_lastDarkness = dk;

		if (dk <= 0.02f) // 精力接近满:不开遮罩
		{
			_rect.Texture = null;
			return;
		}

		// 透亮内圈半径(相对对角线),外圈渐黑
		float innerR = Mathf.Max(rs * 0.55f, 0.06f);
		float fadeEnd = Mathf.Min(innerR + 0.30f, 0.97f);
		var transparent = new Color(0f, 0f, 0f, 0f);
		var black = new Color(0f, 0f, 0f, dk);

		var grad = new Gradient
		{
			Colors = new[] { transparent, transparent, black, black },
			Offsets = new[] { 0f, innerR, fadeEnd, 1f }
		};

		var tex = new GradientTexture2D
		{
			Gradient = grad,
			Width = 64,
			Height = 64,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 1f)
		};
		_rect.Texture = tex;
	}
}
