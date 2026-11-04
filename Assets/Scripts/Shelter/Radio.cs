using System.Collections.Generic;
using UnityEngine;

// 배터리 라디오. 전력이 없어도 켜진다 (정전 중 유일한 위안).
// 켜면 잡음 속에서 끊어진 구조 방송이 한 줄씩 들리고, 다시 누르면 꺼진다. 방송이 끝나면 저절로 꺼진다.
public class Radio : Interactable
{
    public AudioSource speaker;   // 반복 잡음
    public Light glow;            // 켜져 있을 때 다이얼 불빛
    public Transform needle;      // 주파수 바늘
    [Tooltip("이 거리 안에 있을 때만 방송 자막을 띄운다")]
    public float subtitleRange = 9f;

    const float NeedleTravel = 0.09f; // 바늘이 움직이는 폭(로컬 X)
    const float TuneSeconds = 1.4f;
    const float TailSeconds = 1.8f;   // 마지막 줄 뒤에 잡음만 남는 시간

    AudioSource voice;            // 튜닝 휘파람·뭉개진 목소리
    readonly List<string> lines = new List<string>();
    readonly List<float> durations = new List<float>();
    Vector3 needleHome;
    float timer, tuneTimer, staticVolume, glowBase;
    int index;
    bool playing;

    public bool IsPlaying => playing;

    public override string Prompt => playing ? "라디오 끄기" : "라디오 듣기";

    // 스피커 그릴 자리와 앞면 방향 (앞면이 -Z)
    Vector3 GrillePos => transform.TransformPoint(new Vector3(-0.085f, 0.09f, -0.1f));
    Vector3 Front => -transform.forward;

    // 낡은 라디오의 치지직: 스피커 그릴에서 전기가 튄다
    void Spark(float scale, int bursts, float volume, int variant)
    {
        Vfx.Zap(GrillePos, Front, scale, bursts);
        ProceduralAudio.PlayAt(EffectAudio.Zap(variant), GrillePos, volume, Random.Range(0.9f, 1.15f), 1f, 12f, SoundCategory.Ambient);
    }

    void Awake()
    {
        voice = gameObject.AddComponent<AudioSource>();
        AudioCategory.Tag(voice, SoundCategory.Ambient);
        voice.playOnAwake = false;
        voice.spatialBlend = 1;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = speaker != null ? speaker.minDistance : 1f;
        voice.maxDistance = speaker != null ? speaker.maxDistance : 14f;
        voice.volume = 0.6f;
        if (speaker != null) staticVolume = speaker.volume;
        if (needle != null) needleHome = needle.localPosition;
        if (glow != null)
        {
            glowBase = glow.intensity;
            glow.enabled = false;
        }
    }

    void Start()
    {
        // 처음 켤 때 끊기지 않게 소리를 미리 만든다
        if (speaker != null && speaker.clip == null) speaker.clip = RadioAudio.Static();
        RadioAudio.Tune();
    }

    public override void Interact(PlayerInteractor player)
    {
        ProceduralAudio.PlayAt(ProceduralAudio.Click(), transform.position, 0.7f, 0.8f);
        if (playing) TurnOff();
        else TurnOn();
    }

    void TurnOn()
    {
        Tutorial.Notify("radio");
        BuildTransmission();
        playing = true;
        index = -1;
        timer = 0.8f; // 첫 줄 전에 잠깐 잡음만
        tuneTimer = TuneSeconds;
        if (speaker != null)
        {
            speaker.volume = staticVolume;
            speaker.Play();
        }
        voice.clip = RadioAudio.Tune();
        voice.pitch = 1f;
        voice.Play();
        if (glow != null) glow.enabled = true;
        Spark(0.08f, 3, 0.45f, 0);
    }

    void TurnOff()
    {
        playing = false;
        if (speaker != null) speaker.Stop();
        voice.Stop();
        if (glow != null) glow.enabled = false;
        if (needle != null) needle.localPosition = needleHome;
        Spark(0.06f, 1, 0.25f, 2);
    }

    // 켤 때마다 두 방송이 번갈아 잡힌다 (○○ 지명은 나중에 정한다):
    // 1) 긴급 재난 방송 — 핵폭발 뒤 퍼진 바이러스와 감염자(좀비)를 알리고 서울시 ○○으로 대피하라는 안내
    // 2) 생존자의 호출 — 트럭을 만들어 ○○으로 오라는 끊어진 목소리 (남은 턴에 따라 한 줄이 바뀐다)
    static int broadcastTurn;

    void BuildTransmission()
    {
        lines.Clear();
        durations.Clear();
        var s = GameManager.Instance.State;
        int left = Mathf.Max(0, GameState.MaxTurns - s.Turn);
        bool emergency = broadcastTurn++ % 2 == 0;
        Add("치지직…", 2f);
        if (emergency)
        {
            Add("…긴급 재난 방송입니다… 치직…", 2.5f);
            Add("…핵폭발 이후… 정체불명의 바이러스가… 수도권에 퍼지고 있습니다…", 3.5f);
            Add("…감염자는… 사람을 공격합니다… 절대 접근하지 마십시오…", 3f);
            Add("…서울 전역이… 위험 지대로… 지정되었습니다…", 3f);
            Add("…생존자 여러분은… 서울시 ○○으로… 대피하시기 바랍니다…", 3.5f);
            Add("…이 방송은… 반복됩니다… 치지직…", 2.5f);
            return;
        }
        Add("…들리는 사람… 있나…", 2.5f);
        Add($"…{GameState.MaxTurns}턴 안에… 트럭을… 만들어서…", 3f);
        Add("…서울시 ○○으로… 와라… 치직…", 3f);
        if (s.Turn > 0)
            Add(left > 20 ? $"…남은 시간… {left}턴… 치지직…" : $"…{left}턴… 치직… 더는… 기다릴 수 없다…", 2.5f);
        Add("…반복한다… 치지직…", 2.5f);
    }

    void Add(string line, float seconds)
    {
        lines.Add(line);
        durations.Add(seconds);
    }

    void Update()
    {
        if (!playing) return;
        float dt = Time.deltaTime;

        // 신호가 들쭉날쭉하게 세졌다 약해진다
        if (speaker != null) speaker.volume = staticVolume * (0.65f + 0.35f * Mathf.PerlinNoise(Time.time * 1.7f, 3.1f));
        if (glow != null) glow.intensity = glowBase * (0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 9f, 0.5f));

        // 켤 때 바늘이 주파수를 찾아 흔들리다 멈춘다
        if (needle != null && tuneTimer > 0)
        {
            tuneTimer -= dt;
            float u = 1 - Mathf.Clamp01(tuneTimer / TuneSeconds);
            float x = Mathf.Lerp(0, NeedleTravel, u) + Mathf.Sin(u * 18f) * 0.012f * (1 - u);
            needle.localPosition = needleHome + new Vector3(x, 0, 0);
        }

        timer -= dt;
        if (timer > 0) return;
        index++;
        if (index < lines.Count) ShowLine(index);
        else if (index == lines.Count) timer = TailSeconds;
        else TurnOff();
    }

    void ShowLine(int i)
    {
        timer = durations[i];
        string line = lines[i];
        // 잡음만 있는 줄이면 전기가 튀고, 아니면 뭉개진 목소리를 섞는다
        if (line.StartsWith("치지직", System.StringComparison.Ordinal)) Spark(0.06f, 2, 0.3f, 1);
        else
        {
            voice.clip = RadioAudio.Garble();
            voice.pitch = Random.Range(0.92f, 1.08f);
            voice.Play();
        }
        var player = PlayerController.Instance;
        if (player != null && Vector3.Distance(player.transform.position, transform.position) <= subtitleRange)
            GameManager.Instance.UI.ShowMessage("<color=#8f9a86>[라디오]</color>  " + line, timer + 0.3f);
    }
}
