using Godot;

/// <summary>
/// 烦恼基类：体型成长、旋转动画、追击玩家的运动。
/// 三种烦恼（疑 / 压 / 孤）只有数据差异（初始血量、旋转速度、贴图、缩放），
/// 行为骨架全部在此，具体数值由各自场景（doubt / pressure / loneliness.tscn）覆盖。
/// </summary>
public partial class Worry : Area2D
{
	[ExportGroup("成长")]
	// 初始生命值（可调）
	[Export] public float InitialHp { get; set; } = 10f;

	// HP 增长系数（可调）：每秒增长 = |RotationSpeed| × HpGrowFactor，转得越快涨得越快
	[Export(PropertyHint.Range, "0,0.5,0.001")]
	public float HpGrowFactor { get; set; } = 0.03f;

	// 血量上限：涨到这里就不再长。没有上限的话，拖久了烦恼会硬到打不动（这是手感崩掉的根源）
	[Export(PropertyHint.Range, "1,500,1")]
	public float MaxHp { get; set; } = 40f;

	[ExportGroup("旋转")]
	// 旋转动画速度（度/秒，负值反向）
	[Export(PropertyHint.Range, "-360,360,1")]
	public float RotationSpeed { get; set; } = 90f;

	[ExportGroup("体型")]
	// 血量高于初始时的放大幅度：倍率 = 1 + ScalePerLn × ln(Hp / InitialHp)
	[Export(PropertyHint.Range, "0,5,0.01")]
	public float ScalePerLn { get; set; } = 0.15f;

	// 半径变化速度（每秒变化的体型倍率）。受伤后半径不是瞬变，而是按此速度平滑趋近目标。
	// 参考：3 表示约 0.3 秒完成从 1.8 倍缩到 0.9 倍
	[Export(PropertyHint.Range, "0.1,20,0.1")]
	public float RadiusChangeSpeed { get; set; } = 3f;

	[ExportGroup("追击")]
	// 最大速度比例系数：最大速度 = MaxSpeedFactor × √半径
	// 参考：半径 ≈80 时，18 → 约 160 px/s（玩家移动速度为 300，追得上但甩得掉）
	[Export(PropertyHint.Range, "0,100,0.5")]
	public float MaxSpeedFactor { get; set; } = 18f;

	// 加速度比例系数：加速度 = AccelFactor / 半径²
	// 参考：半径 ≈80 时，1500000 → 约 230 px/s²
	// 半径越大越笨重：最终速度更高（√r），但提速更慢（1/r²）
	[Export(PropertyHint.Range, "0,10000000,1000")]
	public float AccelFactor { get; set; } = 1500000f;

	[ExportGroup("受击")]
	// 受击闪光时长（秒）：命中但没打死时闪一下，让玩家知道打中了
	[Export(PropertyHint.Range, "0,0.5,0.01")]
	public float FlashTime { get; set; } = 0.12f;

	// 受击闪光颜色（由白渐变回此色再恢复）
	[Export] public Color FlashColor { get; set; } = new Color(1f, 0.55f, 0.55f);

	// 当前生命值。随 |旋转速度| × HpGrowFactor 每帧上涨；被子弹命中则扣减
	public float Hp { get; private set; }

	// 当前体型半径（世界单位）= 碰撞半径 × 节点缩放 × 成长系数
	public float Radius { get; private set; }

	private Sprite2D _sprite;
	private CollisionShape2D _shape;
	private CircleShape2D _circle;   // 实例独占的碰撞圆，半径随体型同步变化
	private float _baseRadius = 40f; // 场景里配置的原始碰撞半径
	private float _growth = 1f;      // 当前体型倍率（平滑趋近目标倍率）
	private Node2D _target;
	private Vector2 _velocity;
	private float _flash;

	public override void _Ready()
	{
		// 加入 worry 组：武器的命中上报依赖此组识别烦恼
		AddToGroup("worry");
		_sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		_shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

		// 场景里的形状是共享资源，必须先复制一份，否则改半径会影响同类型的所有实例
		if (_shape?.Shape is CircleShape2D src)
		{
			_baseRadius = src.Radius;
			_circle = (CircleShape2D)src.Duplicate();
			_shape.Shape = _circle;
		}

		Hp = InitialHp;
		ApplyScale(0f); // d=0：出生即等于目标体型，不需要过渡
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;

		// 生命值持续上涨（封顶于 MaxHp），速率 = |旋转速度| × HpGrowFactor
		float hpCap = Mathf.Max(MaxHp, InitialHp); // 上限不低于初始血量，避免一出生就被削
		Hp = Mathf.Min(Hp + Mathf.Abs(RotationSpeed) * HpGrowFactor * d, hpCap);
		RotationDegrees += RotationSpeed * d; // 旋转动画
		ApplyScale(d);
		UpdateFlash(d);

		Chase(d);
	}

	/// <summary>
	/// 扣除生命值。返回 true 表示血量归零、已消散（成就由裁决方结算）。
	/// 只有裁决方（GameManager）应调用它。
	/// </summary>
	public bool TakeDamage(float damage)
	{
		if (damage <= 0f)
			return false;

		Hp -= damage;
		if (Hp > 0f)
		{
			_flash = FlashTime; // 未致死：闪一下，让玩家知道打中了
			return false;
		}

		QueueFree(); // 血量归零：消散
		return true;
	}

	/// <summary>受击闪光随时间衰减回原色；无闪光时不动，避免每帧写属性。</summary>
	private void UpdateFlash(float d)
	{
		if (_sprite == null || _flash <= 0f)
			return;

		_flash = Mathf.Max(_flash - d, 0f);
		float t = FlashTime > 0f ? _flash / FlashTime : 0f;
		_sprite.Modulate = _flash > 0f ? Colors.White.Lerp(FlashColor, t) : Colors.White;
	}

	/// <summary>朝玩家加速追击：速度上限 ∝ √半径，加速度 ∝ 1/半径²。</summary>
	private void Chase(float d)
	{
		if (_target == null || !GodotObject.IsInstanceValid(_target))
			_target = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (_target == null)
			return;

		Vector2 to = _target.GlobalPosition - GlobalPosition;
		float dist = to.Length();
		if (dist <= 0.001f)
			return;

		// 半径兜底，避免除零 / 开方得到病态值
		float r = Mathf.Max(Radius, 1f);
		float maxSpeed = MaxSpeedFactor * Mathf.Sqrt(r);
		float accel = AccelFactor / (r * r);

		_velocity = _velocity.MoveToward(to / dist * maxSpeed, accel * d);
		GlobalPosition += _velocity * d;
	}

	/// <summary>
	/// 同步体型：先按血量算出目标倍率，再以 RadiusChangeSpeed 平滑趋近。
	///   Hp ≥ 初始：目标 = 1 + ScalePerLn × ln(Hp / 初始)  血量越高按对数放大
	///   Hp < 初始：目标 = Hp / 初始                      血量越低线性缩小，Hp=0 时倍率恰为 0
	/// 受伤骤降的是血量，体型跟随有一个可调速度的过程 → 「被打就缓缓缩一圈」的反馈。
	/// </summary>
	private void ApplyScale(float d)
	{
		float init = Mathf.Max(InitialHp, 0.001f);
		float ratio = Hp / init;
		float target = ratio >= 1f
			? 1f + ScalePerLn * Mathf.Log(ratio)
			: ratio;

		// d>0 时按速度平滑趋近目标；d=0（出生瞬间）直接对齐，不播过渡动画
		_growth = d > 0f
			? Mathf.MoveToward(_growth, target, RadiusChangeSpeed * d)
			: target;
		_growth = Mathf.Max(_growth, 0f); // 防除零/负倍率

		if (_sprite != null)
			_sprite.Scale = new Vector2(_growth, _growth);

		// 碰撞圆与视觉同步缩放（形状在 _Ready 已 Duplicate，本实例独占，改半径不影响同类型其他个体）
		if (_circle != null)
			_circle.Radius = _baseRadius * _growth;

		Radius = _baseRadius * _growth * Scale.X;
	}
}
