using Godot;

/// <summary>
/// 烦恼基类(Area2D,根节点自绘「外亮内暗」色块,零贴图)。
/// - 保留血条:HP 随时间自然成长,体型随 HP 增大 → 时间压力;
///   裁决方(GameManager)用正确武器放大伤害(≈快速解决),错误武器伤害为 0。
/// - 外观:由 Kind 决定身份色(Palette),攻击与旋转在 _Draw 中实时反映,无 Sprite2D。
/// - 移动 AI:子类覆写 Move(),疑原地打转不追人 / 焦缓慢逼近 / 孤保持距离并伺机贴近。
/// </summary>
public abstract partial class Worry : Area2D
{
	public abstract WorryType Kind { get; }

	[ExportGroup("血条(时间压力)")]
	[Export(PropertyHint.Range, "1,60,1")] public float InitialHp { get; set; } = 12f;
	[Export(PropertyHint.Range, "12,120,1")] public float MaxHp { get; set; } = ReactionTable.MaxWorryHp;
	// 每秒自然成长的 HP;最终 HP 越高体型越大 → 留给玩家的时间越短
	[Export(PropertyHint.Range, "0,10,0.1")] public float HpGrowRate { get; set; } = ReactionTable.WorryHpGrowPerSec;
	[Export(PropertyHint.Range, "0,2,0.05")] public float ScalePerLn { get; set; } = ReactionTable.WorryScalePerLn;
	[Export(PropertyHint.Range, "0.5,6,0.1")] public float BodyGrowSpeed { get; set; } = 2f; // 体型趋近速度

	[ExportGroup("外观")]
	[Export(PropertyHint.Range, "0,180,1")] public float RotationSpeed { get; set; } = 30f; // 自转(度/秒,0=不转)
	[Export(PropertyHint.Range, "16,60,1")] public float BodyRadius { get; set; } = 30f;   // 基准半宽(世界单位)
	[Export] public bool Ghostly { get; set; } = false; // 孤:幽灵半透明

	[ExportGroup("移动")]
	[Export(PropertyHint.Range, "0,240,1")] public float MaxSpeed { get; set; } = 80f;
	[Export(PropertyHint.Range, "0,800,10")] public float Accel { get; set; } = 240f;

	[ExportGroup("受击")]
	[Export(PropertyHint.Range, "0,0.5,0.01")] public float FlashTime { get; set; } = 0.15f;

	// ---- 运行时 ----
	public float Hp { get; private set; }
	public float Radius { get; private set; }           // 当前碰撞/视觉半径(世界单位)
	public bool IsDying { get; private set; }

	private CollisionShape2D _shape;
	private CircleShape2D _circle;  // 实例独占,半径随体型变
	private float _growth = 1f;     // 当前体型倍率(平滑趋近目标)
	private float _flash;           // 命中闪白剩余时间
	private float _dieT;            // 消散动画计时
	private float _wrongT;          // 误伤反馈动画剩余时间
	private const float DieDuration = 0.3f;     // 消散动画总时长(秒)
	private const float DieShrinkSpeed = 6f;    // 体型收缩速度(每秒倍率)
	private const float WrongHitFlash = 0.25f;   // 误伤反馈动画时长(秒)
	private static readonly Color WrongMarkColor = new Color(1f, 0.42f, 0.22f); // 误伤警告橙红色
	private Font _font;             // 方块内中文标识
	private Vector2 _velocity;
	private Node2D _target;

	public override void _Ready()
	{
		AddToGroup("worry");
		_shape = GetNode<CollisionShape2D>("CollisionShape2D");
		if (_shape.Shape is CircleShape2D src)
		{
			_circle = (CircleShape2D)src.Duplicate();
			_shape.Shape = _circle;
			_circle.Radius = BodyRadius;
		}
		else
		{
			_circle = new CircleShape2D { Radius = BodyRadius };
			_shape.Shape = _circle;
		}

		_font = new SystemFont
		{
			FontNames = new[] { "Noto Sans CJK SC", "Microsoft YaHei", "SimHei", "PingFang SC", "Source Han Sans SC" }
		};

		Hp = InitialHp;
		ApplyBody(0f); // 出生直接对齐目标体型
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;

		if (IsDying)
		{
			// 消散动画:闪白后收缩淡出,节点延后释放(便于看到击杀反馈)
			_dieT += d;
			_growth = Mathf.Max(_growth - DieShrinkSpeed * d, 0f);
			RotationDegrees += RotationSpeed * d;
			_flash = Mathf.Max(_flash - d, 0f);
			if (_dieT >= DieDuration || _growth <= 0.02f)
				QueueFree();
			else
				QueueRedraw();
			return;
		}

		Hp = Mathf.Min(Hp + HpGrowRate * d, Mathf.Max(MaxHp, InitialHp)); // HP 持续上涨
		ApplyBody(d);                                                     // 体型平滑跟随 HP
		RotationDegrees += RotationSpeed * d;
		_flash = Mathf.Max(_flash - d, 0f);
		_wrongT = Mathf.Max(_wrongT - d, 0f);
		Move(d);
		QueueRedraw(); // 自转会不断变化 → 每帧轻量重绘(矩形极廉价)
	}

	/// <summary>
	/// 扣除生命值。返回 true = 归零消散(进入消散动画,节点延后释放;结算由裁决方 GameManager 负责)。
	/// 只有裁决方调用它;错误武器伤害恒为 0,不会走到这里。
	/// </summary>
	public bool TakeDamage(float damage)
	{
		if (IsDying) return false;
		if (damage <= 0f) return false;

		Hp -= damage;
		if (Hp > 0f)
		{
			_flash = FlashTime;
			return false;
		}
		BeginDeath();
		return true;
	}

	/// <summary>进入消散动画(裁决方确认击杀后调用);节点延后释放。</summary>
	public void BeginDeath()
	{
		if (IsDying) return;
		IsDying = true;
		_dieT = 0f;
		_flash = FlashTime;
	}

	/// <summary>被非克制武器命中时的反馈(不扣血,但触发视觉警告与精力惩罚)。</summary>
	public void OnWrongHit()
	{
		if (IsDying) return;
		_wrongT = WrongHitFlash;
		QueueRedraw();
	}

	/// <summary>分裂体复制继承:继承母体的当前 HP 与体型(不重置时间压力)。</summary>
	public void AdoptSplitState(float hpLeft)
	{
		Hp = Mathf.Clamp(hpLeft, 1f, Mathf.Max(MaxHp, InitialHp));
		ApplyBody(0f);
	}

	/// <summary>烦恼方块中央显示的单字。</summary>
	private string KindLabel => Kind switch
	{
		WorryType.Doubt => "疑",
		WorryType.Pressure => "焦",
		WorryType.Loneliness => "孤",
		_ => ""
	};

	public override void _Draw()
	{
		(Color frame, Color core) = Palette.ForWorry(Kind);
		float alpha = 1f;
		if (Ghostly) // 孤:幽灵质感——半透暗芯 + 半透亮框
		{
			core.A = 0.38f;
			frame.A = 0.72f;
		}
		if (_flash > 0f) // 命中闪白:亮框往白推,暗芯往亮推
		{
			frame = frame.Lerp(Colors.White, 0.6f);
			core = core.Lerp(Palette.DissolveFlash, 0.35f);
		}
		if (IsDying) // 消散时整体淡出
		{
			alpha = 1f - Mathf.Clamp(_dieT / DieDuration, 0f, 1f);
			frame.A *= alpha;
			core.A *= alpha;
		}
		if (_wrongT > 0f) // 误伤反馈:边框闪警告橙红色,并画白色 "×"
		{
			float a = _wrongT / WrongHitFlash;
			frame = frame.Lerp(WrongMarkColor, 0.6f * a);
			core = core.Lerp(WrongMarkColor.Darkened(0.3f), 0.45f * a);
		}

		float half = BodyRadius * _growth;
		// 亮框满铺
		DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), frame);
		// 暗芯收缩(亮框视觉厚度随体型比例走)
		float inset = Mathf.Max(half * 0.18f, 3f);
		float inner = half - inset;
		if (inner > 0f)
			DrawRect(new Rect2(-inner, -inner, inner * 2f, inner * 2f), core);

		// 身份字(居中)
		string label = KindLabel;
		if (!string.IsNullOrEmpty(label) && inner > 5f && _font != null)
		{
			int fontSize = Mathf.RoundToInt(Mathf.Clamp(inner * 1.3f, 8f, 28f));
			Vector2 ts = _font.GetStringSize(label, fontSize: fontSize);
			Color textColor = Colors.White;
			textColor.A = alpha;
			DrawString(_font, new Vector2(-ts.X * 0.5f, ts.Y * 0.35f), label,
				HorizontalAlignment.Left, -1, fontSize, textColor);
		}

		// 误伤 "×" 标记(居中,随反馈时间淡出)
		if (_wrongT > 0f && inner > 3f)
		{
			float a = _wrongT / WrongHitFlash;
			Color mark = Colors.White;
			mark.A = 0.85f * a * alpha;
			float s = inner * 0.55f;
			float w = Mathf.Max(inner * 0.12f, 2f);
			DrawLine(new Vector2(-s, -s), new Vector2(s, s), mark, w, true);
			DrawLine(new Vector2(s, -s), new Vector2(-s, s), mark, w, true);
		}
	}

	/// <summary>
	/// 移动 AI(子类覆写)。默认向玩家匀速加速逼近(焦的表现由较低 MaxSpeed 体现)。
	/// </summary>
	protected virtual void Move(float d)
	{
		Node2D target = PlayerNode();
		if (target == null) return;
		Vector2 to = target.GlobalPosition - GlobalPosition;
		Seek(to, MaxSpeed, Accel, d);
	}

	/// <summary>朝 to 方向加减速移动;到目标附近减速(松软追尾,不硬停)。</summary>
	protected void Seek(Vector2 to, float maxSpeed, float accel, float d)
	{
		float dist = to.Length();
		if (dist <= 0.001f) return;
		Vector2 dir = to / dist;
		float desired = maxSpeed;
		if (dist < 90f) desired = maxSpeed * (dist / 90f); // 近距缓行防撞挤
		_velocity = _velocity.MoveToward(dir * desired, accel * d);
		GlobalPosition += _velocity * d;
	}

	protected void AddDrift(Vector2 accelV, float d)
	{
		_velocity += accelV * d;
		GlobalPosition += _velocity * d;
	}

	protected Node2D PlayerNode()
	{
		if (_target == null || !GodotObject.IsInstanceValid(_target))
			_target = GetTree().GetFirstNodeInGroup("player") as Node2D;
		return _target;
	}

	/// <summary>体型平滑趋近目标:d=0 直接对齐(出生/分裂瞬间)。</summary>
	private void ApplyBody(float d)
	{
		float init = Mathf.Max(InitialHp, 0.01f);
		float ratio = Hp / init;
		float target = ratio >= 1f ? 1f + ScalePerLn * Mathf.Log(ratio) : ratio;
		_growth = d > 0f ? Mathf.MoveToward(_growth, target, BodyGrowSpeed * d) : target;
		_growth = Mathf.Max(_growth, 0f);

		if (_circle != null) _circle.Radius = BodyRadius * _growth;
		Radius = BodyRadius * _growth;
	}
}
