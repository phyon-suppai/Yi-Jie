using System;
using Godot;

/// <summary>
/// 精力/成就纯规则:
/// - 没事时精力按时间慢慢掉;
/// - 被烦恼缠身(附近烦恼)时按烦恼数大量掉;
/// - 站桩不动时回复;
/// - 解决(消散)一个烦恼时恢复精力。
/// </summary>
public sealed class EnergySystem
{
	private float _energy;
	private float _achieve;

	public EnergySystem(float startEnergy)
	{
		MaxEnergy = 100f;
		_energy = Math.Clamp(startEnergy, 0f, MaxEnergy);
		_achieve = 0f;
	}

	public float MaxEnergy { get; }
	public float Energy => _energy;
	public float Achieve => _achieve;
	public float AchieveFraction => _achieve / ReactionTable.MaxAchieve;

	/// <summary>
	/// 每帧推进:
	/// 消耗 = (时间基础 + 缠身烦恼数 × 单个缠身流失) × delta;
	/// 恢复 = 站桩回复 × delta(仅当 resting)。
	/// 掉到 0 由持有者(GameManager)判负。
	/// </summary>
	public void Tick(float delta, int contactCount, bool resting)
	{
		float drain = (ReactionTable.TimeDrainRate + ReactionTable.ContactDrainRate * contactCount) * delta;
		float restore = resting ? ReactionTable.RestoreRate * delta : 0f;
		_energy = Mathf.Clamp(_energy + restore - drain, 0f, MaxEnergy);
	}

	/// <summary>消散:每解决一个烦恼,成就 +15,并恢复精力。</summary>
	public void ApplyDissolve()
	{
		_achieve = Math.Min(_achieve + ReactionTable.DissolveAchieveBonus, ReactionTable.MaxAchieve);
		_energy = Math.Min(_energy + ReactionTable.KillEnergyRestore, MaxEnergy);
	}

	/// <summary>误伤:用错颜色武器命中非克制烦恼,精力扣减(下限 0)。</summary>
	public void ApplyWrongHit()
	{
		_energy = Math.Max(_energy - ReactionTable.WrongColorEnergyPenalty, 0f);
	}

	/// <summary>特殊事件等直接增减精力(可正可负,限制在 0~Max)。</summary>
	public void AddEnergy(float amount)
	{
		_energy = Mathf.Clamp(_energy + amount, 0f, MaxEnergy);
	}
}
