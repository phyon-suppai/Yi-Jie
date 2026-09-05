using Godot;

/// <summary>
/// 武器通用契约(接口,不是基类)。
/// 三把武器各自实现本接口;Character 面向接口做「按下即发 / 按住发散 / 松开收回」。
/// 命中裁决一律上报 GameManager(唯一裁决点),武器不知道相克表。
/// </summary>
public interface IWeapon
{
	/// <summary>武器类型(裁决查表与 HUD 取色用)。</summary>
	WeaponType Kind { get; }

	/// <summary>
	/// 冷却时间(秒),由持有者 Character 读取。
	/// 对按住型武器(Express/Accept)应设为 0,实际再发限制由 IsReleased 控制。
	/// </summary>
	float Cooldown { get; }

	/// <summary>该武器实例是否已经收回/消散,可以再次发射。</summary>
	bool IsReleased { get; }

	/// <summary>
	/// 发射(按下按键即调用一次)。
	/// 行动=直线飞行;表达=生成后进入可按住发散状态;接受=生成后进入可按住扩张状态。
	/// </summary>
	void Launch(Node2D owner, Vector2 direction);

	/// <summary>按住期间每帧调用(仅 Express/Accept 需要实现)。</summary>
	void Hold(float delta);

	/// <summary>松开按键时调用(仅 Express/Accept 需要实现)。</summary>
	void Release();
}
