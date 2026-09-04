using Godot;
using System;

/// <summary>疑：自我内耗，原地打转，不主动追人。随时间成长：HP 无上限增长，精灵按 log(HP) 放大。</summary>
public partial class Doubt : Area2D
{
	[ExportGroup("成长")]
	/// <summary>初始生命值（可调）</summary>
	[Export] public float InitialHp { get; set; } = 10f;

	/// <summary>生命值随时间增长的速度（HP/秒，无上限）</summary>
	[Export] public float HpGrowPerSec { get; set; } = 2f;

	[ExportGroup("旋转")]
	/// <summary>旋转动画速度（度/秒，负值反向）</summary>
	[Export(PropertyHint.Range, "-360,360,1")]
	public float RotationSpeed { get; set; } = 90f;

	[ExportGroup("体型")]
	/// <summary>半径-血量比例系数（可调）：scale = 1 + ScalePerLn × ln(Hp / InitialHp)。数值越大，同样血量下精灵越大</summary>
	[Export(PropertyHint.Range, "0,5,0.01")]
	public float ScalePerLn { get; set; } = 0.15f;

	/// <summary>当前生命值。随 HpGrowPerSec 每帧上涨，无上限。</summary>
	public float Hp { get; private set; }

	private Sprite2D _sprite;

	public override void _Ready()
	{
		// 加入 worry 组：武器的命中上报依赖此组识别烦恼
		AddToGroup("worry");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		Hp = InitialHp;
		ApplyScale();
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;

		// 生命值随时间的无上限增长
		Hp += HpGrowPerSec * d;
		RotationDegrees += RotationSpeed * d; // 旋转动画
		ApplyScale();
	}

	/// <summary>精灵半径与生命值的对数成正比：scale = 1 + ScalePerLn × ln(Hp / InitialHp)</summary>
	private void ApplyScale()
	{
		if (_sprite == null) return;
		float init = Mathf.Max(InitialHp, 0.001f);
		float hp = Mathf.Max(Hp, init);
		float s = 1f + ScalePerLn * Mathf.Log(hp / init);
		_sprite.Scale = new Vector2(s, s);
	}
}
