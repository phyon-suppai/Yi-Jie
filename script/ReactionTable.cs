using System;

/// <summary>
/// 相克(克制)映射与平衡数值常量。
/// 纯数据,不做成节点;GameManager 是唯一裁决点。
/// 视觉语言:武器色 = 它唯一能「消散」的烦恼色(同色即克制)。
///   行动(绿) 克 疑(绿)    表达(蓝) 克 孤(蓝)    接受(红) 克 焦(红)
/// </summary>
public static class ReactionTable
{
	// 克制映射 [武器, 烦恼] = 是否可消散(正确武器)
	//              疑 Doubt       焦 Pressure     孤 Loneliness
	private static readonly bool[,] CounterMap =
	{
		/* 行动 Act     */ { true,  false, false },
		/* 表达 Express */ { false, false, true  },
		/* 接受 Accept */ { false, true,  false },
	};

	/// <summary>武器是否克制该烦恼(同色即克制)。</summary>
	public static bool IsCounter(WorryType worry, WeaponType weapon)
		=> CounterMap[(int)weapon, (int)worry];

	// ---------------------------------------------------------------- 结算数值
	public const float DissolveAchieveBonus = 10f;  // 每解决一个烦恼:成就 +10
	public const float MaxAchieve = 300f;           // 成就满 → 判胜(需要更多击杀)

	// 精力规则:没事慢慢掉;附近烦恼越多掉越快;击杀恢复;站桩回复不能超过时间掉血
	public const float TimeDrainRate = 2.5f;           // 时间比例:每秒固定流失(站桩也回不上来)
	public const float ContactRange = 180f;            // 多近算「附近」(玩家中心到烦恼中心的判定距离)
	public const float RestoreRate = 0.3f;             // 站桩回能(不移动且不施放)/秒;必须 < TimeDrainRate
	public const float KillEnergyRestore = 18f;        // 每解决一个烦恼恢复多少精力

	// 误伤惩罚 & 缠身惩罚:用错颜色武器命中,或一个烦恼贴身缠着玩家,两者每秒/每次惩罚同样严重
	public const float WrongColorEnergyPenalty = 12f;
	public const float ContactDrainRate = WrongColorEnergyPenalty; // 每个「缠身」烦恼每秒额外流失 = 一次误伤惩罚

	// 相克伤害:正确武器单次伤害,与烦恼随时间成长的 HP 对抗(初期 1 发、满成长 2 发)
	public const float CounterDamage = 30f;
	public const float MaxWorryHp = 60f;            // 烦恼 HP 成长上限(时间压力上限)
	public const float WorryHpGrowPerSec = 4.0f;     // 烦恼每秒自然成长 HP(越高,血量增厚越快)
	public const float WorryScalePerLn = 0.85f;      // 体型随 HP 放大的幅度(越大,方块越明显变大)

	// 武器基础手感(冷却)
	public const float ActCooldown = 0f;            // 行动:直线单体,取消冷却可连发
	public const float ExpressCooldown = 1.6f;      // 表达:回旋往返(往返期间不能再发)
	public const float AcceptCooldown = 1.1f;       // 接受:范围瞬发,中冷却
}
