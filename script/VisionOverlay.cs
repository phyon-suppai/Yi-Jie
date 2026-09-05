using Godot;

/// <summary>
/// 视线遮罩:CanvasLayer(layer 100)上的全屏径向渐变暗角。
/// - 精力 >= 60%:不遮罩
/// - 精力 60%~30%:以玩家为中心出现椭圆透亮区,外部比内部暗得更多
/// - 精力 <= 30%:达到最暗(内部约60%可见,外部约30%可见),并随降低让外部越来越模糊
/// 全部由代码生成渐变纹理,零贴图。
/// </summary>
public partial class VisionOverlay : CanvasLayer
{
	private TextureRect _rect;
	private float _lastEnergy = -1f;
	private Vector2 _lastPos;

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
		AddChild(_rect);
	}

	/// <summary>
	/// 刷新遮罩。
	/// energyFrac:精力比例(0~1); playerScreen:玩家在屏幕上的位置; viewportSize:视口大小。
	/// </summary>
	public void Refresh(float energyFrac, Vector2 playerScreen, Vector2 viewportSize)
	{
		float ef = Mathf.Clamp(energyFrac, 0f, 1f);

		// 阶梯化减少重建
		float stepped = Mathf.Round(ef * 80f) / 80f;
		Vector2 posStep = new Vector2(Mathf.Round(playerScreen.X / 8f) * 8f, Mathf.Round(playerScreen.Y / 8f) * 8f);
		if (stepped >= 0.6f)
		{
			if (_rect.Texture != null)
			{
				_rect.Texture = null;
				_lastEnergy = stepped;
			}
			return;
		}
		if (Mathf.IsEqualApprox(stepped, _lastEnergy) && posStep == _lastPos)
			return;

		_lastEnergy = stepped;
		_lastPos = posStep;

		// t:0 在 60% 精力,1 在 30% 精力
		float t = Mathf.Clamp((0.6f - ef) / 0.3f, 0f, 1f);

		// 可见度:内部从 100% -> 60%,外部从 100% -> 30%
		float innerVis = Mathf.Lerp(1f, 0.6f, t);
		float outerVis = Mathf.Lerp(1f, 0.3f, t);
		float innerAlpha = 1f - innerVis;
		float outerAlpha = 1f - outerVis;

		// 低于 30% 后,外部进一步变暗,但保持高分辨率渐变平滑
		if (ef < 0.3f)
		{
			float b = Mathf.Clamp((0.3f - ef) / 0.3f, 0f, 1f);
			outerAlpha = Mathf.Lerp(outerAlpha, 0.96f, b);
		}

		const int resolution = 128;

		// 椭圆中心跟随玩家(UV 坐标)
		Vector2 fillFrom = posStep / viewportSize;
		fillFrom = new Vector2(Mathf.Clamp(fillFrom.X, 0.01f, 0.99f), Mathf.Clamp(fillFrom.Y, 0.01f, 0.99f));
		Vector2 fillTo = new Vector2(Mathf.Clamp(fillFrom.X + 0.5f, 0.01f, 0.99f), Mathf.Clamp(fillFrom.Y + 0.5f, 0.01f, 0.99f));

		// 内圈半径随精力降低而轻微缩小
		float innerR = 0.38f - t * 0.08f;
		float fadeEnd = innerR + 0.46f;

		var transparent = new Color(0f, 0f, 0f, 0f);
		var innerColor = new Color(0f, 0f, 0f, innerAlpha);
		var outerColor = new Color(0f, 0f, 0f, outerAlpha);

		var grad = new Gradient
		{
			Colors = new[] { transparent, innerColor, outerColor, outerColor },
			Offsets = new[] { 0f, innerR, fadeEnd, 1f }
		};

		var tex = new GradientTexture2D
		{
			Gradient = grad,
			Width = resolution,
			Height = resolution,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = fillFrom,
			FillTo = fillTo
		};
		_rect.Texture = tex;
	}
}
