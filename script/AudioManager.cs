using Godot;

/// <summary>
/// 音频管理:生成四状态循环氛围音效,并播放一次性事件音效。
/// </summary>
public partial class AudioManager : AudioStreamPlayer
{
	private enum State
	{
		HighEnergyHighAchieve,
		HighEnergyLowAchieve,
		LowEnergyHighAchieve,
		LowEnergyLowAchieve
	}

	private AudioStreamGeneratorPlayback _playback;
	private GameManager _gm;

	private double _phase1;
	private double _phase2;
	private double _phase3;

	private float _freq1, _freq2, _freq3;
	private float _amp;
	private State _currentState;

	// 节奏：每拍 4 个跳音（短促音符），做氛围和弦的脉动
	private double _patternTime;
	private const double StepDuration = 0.25; // 每个八分音符时长（秒）
	private const double NoteDuty = 0.75;     // 音符占每个步长的比例
	private const double NoteFade = 0.02;     // 音符起止淡入淡出（秒）

	private AudioStreamPlayer _sfxShoot;
	private AudioStreamPlayer _sfxDissolve;
	private AudioStreamPlayer _sfxHit;
	private AudioStreamPlayer _sfxHurt;
	private AudioStreamPlayer _sfxEventCorrect;
	private AudioStreamPlayer _sfxEventPenalty;
	private AudioStreamPlayer _sfxWin;
	private AudioStreamPlayer _sfxLose;
	private AudioStreamPlayer _sfxHeart;
	private AudioStreamPlayer _sfxLevelStart;

	public override void _Ready()
	{
		_gm = GetParent<GameManager>();

		// 氛围循环生成器
		Stream = new AudioStreamGenerator { MixRate = 44100, BufferLength = 0.1f };
		Play();
		_playback = GetStreamPlayback() as AudioStreamGeneratorPlayback;

		// 一次性音效播放器
		_sfxShoot = AddSfx("Shoot", "res://assets/sfx/shoot.wav");
		_sfxDissolve = AddSfx("Dissolve", "res://assets/sfx/resolve.wav");
		_sfxHit = AddSfx("Hit", "res://assets/sfx/hit.wav");
		_sfxHurt = AddSfx("Hurt", "res://assets/sfx/hurt.wav");
		_sfxEventCorrect = AddSfx("EventCorrect", "res://assets/sfx/encourage.wav");
		_sfxEventPenalty = AddSfx("EventPenalty", "res://assets/sfx/bomb.wav");
		_sfxWin = AddSfx("Win", "res://assets/sfx/win.wav");
		_sfxLose = AddSfx("Lose", "res://assets/sfx/lose.wav");
		_sfxHeart = AddSfx("Heart", "res://assets/sfx/heart_thump.wav");
		_sfxHeart.VolumeDb = -14f; // 心跳小声一点

		_sfxLevelStart = AddSfx("LevelStart", "res://assets/sfx/level_start.wav");
		// 如果没有准备专门的关卡进入音效，先用拾取音代替
		if (_sfxLevelStart.Stream == null)
			_sfxLevelStart.Stream = GD.Load<AudioStream>("res://assets/sfx/pickup.wav");
	}

	private AudioStreamPlayer AddSfx(string nodeName, string path)
	{
		var player = new AudioStreamPlayer { Name = nodeName };
		var stream = GD.Load<AudioStream>(path);
		if (stream != null)
			player.Stream = stream;
		AddChild(player);
		return player;
	}

	public override void _Process(double delta)
	{
		if (_playback == null) return;

		int available = _playback.GetFramesAvailable();
		if (available <= 0) return;

		float energy = _gm?.EnergySystem != null ? _gm.EnergySystem.Energy / _gm.EnergySystem.MaxEnergy : 1f;
		float achieve = _gm?.EnergySystem != null ? _gm.EnergySystem.AchieveFraction : 0f;

		State state;
		if (energy >= 0.4f && achieve >= 0.6f) state = State.HighEnergyHighAchieve;
		else if (energy >= 0.4f) state = State.HighEnergyLowAchieve;
		else if (achieve >= 0.6f) state = State.LowEnergyHighAchieve;
		else state = State.LowEnergyLowAchieve;

		SetTarget(state);

		double sampleRate = ((AudioStreamGenerator)Stream).MixRate;
		for (int i = 0; i < available; i++)
		{
			_patternTime += 1.0 / sampleRate;
			float env = CalcEnvelope(_patternTime % StepDuration);

			float s = (Sine(_freq1, ref _phase1, sampleRate)
				+ Sine(_freq2, ref _phase2, sampleRate)
				+ Sine(_freq3, ref _phase3, sampleRate)) * _amp * env * 0.85f;
			_playback.PushFrame(new Vector2(s, s));
		}

		// 低精力心跳
		if (energy < 0.25f)
		{
			if (!_sfxHeart.Playing)
				_sfxHeart.Play();
		}
		else if (_sfxHeart.Playing)
		{
			_sfxHeart.Stop();
		}
	}

	private void SetTarget(State state)
	{
		if (state == _currentState) return;
		_currentState = state;

		switch (state)
		{
			case State.HighEnergyHighAchieve:
				// 明亮大三和弦 C4 - E4 - G4
				_freq1 = 261.63f;
				_freq2 = 329.63f;
				_freq3 = 392.00f;
				_amp = 0.13f;
				break;
			case State.HighEnergyLowAchieve:
				// 略带悬疑的 C 小三和弦 C4 - Eb4 - G4
				_freq1 = 261.63f;
				_freq2 = 311.13f;
				_freq3 = 392.00f;
				_amp = 0.12f;
				break;
			case State.LowEnergyHighAchieve:
				// 悬置感 C 挂二和弦 C4 - D4 - G4
				_freq1 = 261.63f;
				_freq2 = 293.66f;
				_freq3 = 392.00f;
				_amp = 0.13f;
				break;
			case State.LowEnergyLowAchieve:
				// 压抑的 C 减三和弦 C4 - Eb4 - Gb4
				_freq1 = 261.63f;
				_freq2 = 311.13f;
				_freq3 = 369.99f;
				_amp = 0.14f;
				break;
		}
	}

	/// <summary>
	/// 跳音包络：每个八分音符前段发声、后段休止，边缘做短淡入淡出避免爆音。
	/// </summary>
	private static float CalcEnvelope(double posInStep)
	{
		double noteLen = StepDuration * NoteDuty;
		if (posInStep >= noteLen) return 0f;

		if (posInStep < NoteFade)
			return (float)(posInStep / NoteFade);
		if (posInStep > noteLen - NoteFade)
			return (float)((noteLen - posInStep) / NoteFade);
		return 1f;
	}

	private static float Sine(float freq, ref double phase, double sampleRate)
	{
		phase += freq / sampleRate;
		while (phase >= 1.0) phase -= 1.0;
		return Mathf.Sin((float)(phase * Mathf.Tau));
	}

	public void PlayShoot() => PlayOneShot(_sfxShoot);
	public void PlayDissolve() => PlayOneShot(_sfxDissolve);
	public void PlayHit() => PlayOneShot(_sfxHit);
	public void PlayHurt() => PlayOneShot(_sfxHurt);
	public void PlayEventCorrect() => PlayOneShot(_sfxEventCorrect);
	public void PlayEventPenalty() => PlayOneShot(_sfxEventPenalty);
	public void PlayWin() => PlayOneShot(_sfxWin);
	public void PlayLose() => PlayOneShot(_sfxLose);
	public void PlayLevelStart() => PlayOneShot(_sfxLevelStart);

	private static void PlayOneShot(AudioStreamPlayer player)
	{
		if (player != null && player.Stream != null)
		{
			player.Stop();
			player.Play();
		}
	}
}
