using UnityEngine;

// 발전기 (디젤): F로 연료통을 넣는다 (창고·소지품 어디에 있든). 전력이 있으면 엔진이 덜덜거리며 돈다 — 몸체가 떨리고, 냉각 팬이 돌고,
// 계기 바늘이 올라가고 초록 표시등이 켜지며, 옆구리 철창 안의 배기관이 벌겋게 달아올라 불티가 날리고, 배기관 끝에서 검회색 연기가 피어오른다.
// 연료를 부으면 꿀렁 소리와 함께 엔진이 울컥 빨라지고 불티·검은 연기가 확 늘었다가 가라앉는다. 전력이 20% 아래면 쿨럭이며 불안정해진다.
// 소리(GeneratorAudio)는 발전기실 미닫이 문(SlidingDoor)이 닫혀 있고 밖에 있으면 들리지 않고, 문이 열리는 만큼 서서히 커진다 (먹먹함도 함께 풀린다).
// 모양은 GameSceneBuilder.Shelter.cs의 BuildGenerator가 만들어 아래 참조를 채운다
public class Generator : Interactable
{
    public AudioSource hum;
    public Transform vibe;             // 돌 때 떨리는 몸체
    public Transform fan;              // 냉각 팬 (로컬 X 축으로 돈다)
    public Transform needleA, needleB; // 계기 바늘 (전력 / 회전수). 로컬 Z 축으로 돈다
    public Transform smokeTip;         // 배기관 끝 (굴뚝이 천장까지 이어진 지금은 비어 있다: 방에 연기를 풀지 않는다)
    public ParticleSystem chimney;     // 굴뚝 안에서 올라가는 연기 (유리 구간에서 보인다)
    public Renderer glow;              // 철창 안 벌건 뒷판
    public Renderer lamp;              // 계기판 초록 표시등
    public Renderer[] manifold;        // 달아오르는 배기관 조각
    public ParticleSystem embers;      // 철창 안에서 날리는 불티
    public Vector3 smokeDrift = new Vector3(0.1f, 0.5f, 0);

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock block;
    Color glowBase, lampBase, manifoldBase;
    AudioLowPassFilter lowPass;
    float surge, fanSpeed, smokeAcc, sputterTimer, misfire, startBlend, gate = 1f;
    bool wasPowered;

    public override string Prompt => $"발전기 (전력 {GameManager.Instance.State.Power:0}%) - 연료 넣기";

    // 연료 주입구 (연료통 위 뚜껑)
    Vector3 FillerPos => transform.TransformPoint(new Vector3(0.25f, 1.06f, 0.1f));

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        if (s.Power >= 100f)
        {
            gm.UI.ShowMessage("전력이 이미 가득 차 있다.");
            return;
        }
        if (!s.ConsumeFromAnywhere("fuel", 1))
        {
            gm.UI.ShowMessage("연료가 없다. 도시에서 연료통을 찾아야 한다.");
            return;
        }
        s.Power = Mathf.Min(100, s.Power + GameBalance.Data.fuelPower);
        gm.UI.ShowMessage($"연료를 부었다. 전력 {s.Power:0}%");
        var cap = FillerPos;
        ProceduralAudio.PlayAt(ProceduralAudio.Clang(), cap, 0.3f, 0.7f);
        ProceduralAudio.PlayAt(GeneratorAudio.Pour(), cap, 0.8f, Random.Range(0.95f, 1.05f), 1f, 14f);
        surge = 0.45f;
        // 철창 안 불티가 확 날리고 배기관에서 검은 연기가 뭉게뭉게 솟는다
        if (embers != null) embers.Emit(26);
        if (chimney != null) chimney.Emit(18);
        if (smokeTip != null)
            for (int i = 0; i < 10; i++)
                Vfx.Smoke(smokeTip.position + Random.insideUnitSphere * 0.04f,
                    smokeDrift * Random.Range(0.8f, 1.5f) + Random.insideUnitSphere * 0.25f,
                    Random.Range(2.2f, 3.8f), Random.Range(0.26f, 0.46f), 0.75f, 0.95f);
        if (PlayerController.Instance != null) PlayerController.Instance.Shake(0.12f);
    }

    void Start()
    {
        block = new MaterialPropertyBlock();
        if (hum != null)
        {
            hum.clip = GeneratorAudio.Rumble();
            hum.loop = true;
            hum.minDistance = 2f;
            hum.maxDistance = 24f;
            AudioCategory.Tag(hum, SoundCategory.Effects); // 설정의 효과음 음량을 따른다 (코드가 정한 음량 × 종류 음량)
            lowPass = hum.GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = hum.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
        }
        // 발광 키워드가 꺼진 채 불러와진 재질(에디터 재임포트 뒤 가끔)을 켠다. 없으면 철창 안이 그냥 어두운 붉은 판으로 보인다
        EmissionFix.Ensure(transform);
        glowBase = BaseEmission(glow, new Color(2.4f, 0.55f, 0.1f));
        lampBase = BaseEmission(lamp, new Color(0.3f, 2.2f, 0.5f));
        manifoldBase = manifold != null && manifold.Length > 0 ? BaseEmission(manifold[0], new Color(1.5f, 0.22f, 0.04f)) : Color.black;
        wasPowered = GameManager.Instance.State.Power > 0;
        startBlend = wasPowered ? 1f : 0f;
        if (hum != null && wasPowered) hum.Play();
    }

    static Color BaseEmission(Renderer r, Color fallback) =>
        r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(EmissionId) ? r.sharedMaterial.GetColor(EmissionId) : fallback;

    void Update()
    {
        float dt = Time.deltaTime;
        var s = GameManager.Instance.State;
        bool powered = s.Power > 0;
        var at = transform.position + Vector3.up;
        if (powered != wasPowered)
        {
            wasPowered = powered;
            if (powered)
            {
                ProceduralAudio.PlayAt(GeneratorAudio.StartUp(), at, 0.8f, 1f, 1f, 20f);
                if (hum != null) hum.Play();
                startBlend = 0f;
            }
            else
            {
                ProceduralAudio.PlayAt(GeneratorAudio.ShutDown(), at, 0.8f, 1f, 1f, 20f);
                if (hum != null) hum.Stop();
            }
        }
        startBlend = Mathf.MoveTowards(startBlend, powered ? 1f : 0f, dt / 1.5f);
        surge = Mathf.MoveTowards(surge, 0, dt * 0.25f);
        float low = powered ? 1f - Mathf.Clamp01(s.Power / 20f) : 0f; // 힘이 달리는 정도
        // 쿨럭임: 힘이 달릴수록 자주
        sputterTimer -= dt;
        misfire = Mathf.MoveTowards(misfire, 0, dt * 2.2f);
        if (powered && low > 0 && sputterTimer <= 0)
        {
            sputterTimer = Random.Range(2.5f, 6f) / (0.3f + low);
            misfire = 1f;
            ProceduralAudio.PlayAt(GeneratorAudio.Sputter(), at, 0.65f, Random.Range(0.9f, 1.1f), 1f, 18f);
            if (chimney != null) chimney.Emit(7);
            if (smokeTip != null)
                for (int i = 0; i < 3; i++)
                    Vfx.Smoke(smokeTip.position, smokeDrift * Random.Range(0.8f, 1.3f) + Random.insideUnitSphere * 0.2f, Random.Range(2f, 3f), Random.Range(0.2f, 0.34f), 0.65f, 0.95f);
        }
        float run = startBlend * (1f - 0.6f * misfire); // 엔진이 도는 정도 (0~1)

        // 소리: 회전수에 따라 높낮이. 미닫이 문이 닫혀 있고 밖에 있으면 들리지 않고, 문이 열리는 만큼 커진다 (먹먹함도 함께 풀린다)
        if (hum != null)
        {
            hum.pitch = 0.78f + 0.27f * startBlend + surge * 0.6f - 0.3f * misfire + 0.04f * Mathf.PerlinNoise(Time.time * 0.7f, 0);
            float want = 1f;
            var door = SlidingDoor.Instance;
            var cam = Camera.main;
            if (door != null && cam != null && !door.IsInside(cam.transform.position)) want = Mathf.Pow(door.Openness, 0.7f);
            gate = Mathf.MoveTowards(gate, want, dt * 2f);
            hum.volume = 0.45f * gate * (0.8f + 0.2f * startBlend);
            if (lowPass != null) lowPass.cutoffFrequency = Mathf.Lerp(420f, 22000f, Mathf.Sqrt(gate));
        }

        // 몸체 떨림·팬·바늘·표시등·발광
        if (vibe != null)
            vibe.localPosition = run > 0.01f
                ? new Vector3(Mathf.Sin(Time.time * 173f), Mathf.Sin(Time.time * 141f + 1f), Mathf.Sin(Time.time * 157f + 2f)) * (0.0016f * run * (1f + surge * 1.5f))
                : Vector3.zero;
        fanSpeed = Mathf.MoveTowards(fanSpeed, run * (700f + 500f * surge), dt * 500f);
        if (fan != null) fan.Rotate(fanSpeed * dt, 0, 0, Space.Self);
        float power01 = s.Power / 100f;
        if (needleA != null) needleA.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(62f, -62f, power01) + Mathf.Sin(Time.time * 31f) * 1.2f * run);
        float rpm = hum != null ? Mathf.Clamp01((hum.pitch - 0.78f) / 0.7f) : run;
        if (needleB != null) needleB.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(62f, -62f, rpm * run) + Mathf.Sin(Time.time * 23f) * 2f * run);
        float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 7f, 0.5f);
        SetEmission(glow, glowBase * ((0.12f + 0.88f * run) * flicker * (1f + 1.3f * surge)));
        if (manifold != null)
            foreach (var m in manifold) SetEmission(m, manifoldBase * ((0.08f + 0.92f * run) * (1f + 0.9f * surge)));
        bool lampOn = powered && (low <= 0.01f || Mathf.PerlinNoise(Time.time * 6f, 3f) > 0.35f);
        SetEmission(lamp, lampOn ? lampBase : lampBase * 0.04f);
        // 불티·연기
        if (embers != null)
        {
            var em = embers.emission;
            em.rateOverTime = run * (1.6f + 7f * surge);
        }
        if (chimney != null)
        {
            var ce = chimney.emission;
            ce.rateOverTime = run * (11f + 28f * surge + 8f * low);
        }
        if (smokeTip != null && run > 0.01f)
        {
            smokeAcc += dt * run * (3f + 12f * surge + 3f * low);
            while (smokeAcc >= 1f)
            {
                smokeAcc -= 1f;
                Vfx.Smoke(smokeTip.position + Random.insideUnitSphere * 0.03f,
                    smokeDrift * Random.Range(0.6f, 1f) + Random.insideUnitSphere * 0.12f,
                    Random.Range(2.2f, 3.6f), Random.Range(0.22f, 0.34f) * (1f + surge), 0.5f + 0.3f * surge, 0.4f + 0.5f * surge + 0.3f * low);
            }
        }
    }

    void SetEmission(Renderer r, Color c)
    {
        if (r == null) return;
        if (block == null) block = new MaterialPropertyBlock(); // Start가 끝나기 전(부트스트랩 전에 씬을 직접 플레이)에도 안전하게
        block.SetColor(EmissionId, c);
        r.SetPropertyBlock(block);
    }
}
