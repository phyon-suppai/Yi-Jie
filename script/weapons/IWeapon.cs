using Godot;

/// <summary>
/// 武器通用契约（接口，不是基类）。
/// <para>
/// 去基类后三把武器各自继承 Area2D、各自独立实现本接口，
/// Character 只需面向接口驱动「发射 / 冷却 / 按住状态」，
/// 而不必关心具体是哪一把武器、也不需要公共基类。
/// </para>
/// </summary>
public interface IWeapon
{
	/// <summary>冷却时间（秒）。由持有者（Character）读取，用于控制发射节奏。</summary>
	float Cooldown { get; }

	/// <summary>
	/// 伤害系数。实际伤害 = 本系数 × 武器随命中上报的「量（amount）」。
	/// amount 的含义由各武器自行定义：笔=1（一次命中，伤害恒定）；
	/// 纸=划过烦恼的线段长度；橡皮=接触时长（秒）。
	/// </summary>
	float Damage { get; }

	/// <summary>
	/// 玩家是否正按住对应按键（纸与橡皮据此决定伸长 / 扩张）。
	/// 由持有者每帧写入；笔不使用（按下即发射，与按住无关）。
	/// </summary>
	bool IsHeld { get; set; }

	/// <summary>发射。持有者实例化后立即调用。</summary>
	/// <param name="owner">发射者（通常是玩家），纸与橡皮需要跟随其位置。</param>
	/// <param name="direction">发射方向（笔与纸使用；橡皮无朝向概念）。</param>
	void Launch(Node2D owner, Vector2 direction);
}
