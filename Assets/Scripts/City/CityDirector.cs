using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 도시 탐험 진행: 제한시간, 아이템 배치, 좀비 등장(시간이 갈수록 늘어남), 공포 사운드.
// 제한시간(기본 5분)이 지나면 붉은 밤: 하늘·안개·빛이 붉게 물들고 redNightInterval초마다 체력이 redNightDamage씩 준다 (해치로 돌아갈 때까지).
// 턴이 지날수록 새 종류의 좀비가 섞이고, 약탈자·잡상인은 탐험마다 확률로 나타난다.
public class CityDirector : MonoBehaviour
{
    // 제한시간·좀비 수·아이템 확률은 GameBalance 에셋에서 조정한다 (기획서: 약 5분)
    public static float DefaultTimeLimit => GameBalance.Data.expeditionSeconds;

    public static CityDirector Instance { get; private set; }

    public GameObject zombiePrefab;
    public GameObject dogPrefab;

    [Header("좀비 종류 가중치 (등장 턴이 지난 종류끼리만 고른다)")]
    public float normalWeight = 55f;
    public float leatherWeight = 22f;
    public float rangedWeight = 15f;
    public float heavyWeight = 8f;
    [Tooltip("2층 좀비 자리 중 탐험 시작 때 좀비를 세워 둘 비율")]
    [Range(0, 1)] public float upstairsFill = 0.25f;

    GameBalance balance;
    float timeLimit;

    public float TimeLeft { get; private set; }
    public float Danger => Mathf.Clamp01(1 - TimeLeft / timeLimit); // 0 → 1로 점점 위험해짐
    public bool IsRedNight { get; private set; }
    public float RedNightElapsed { get; private set; }

    // 붉은 밤으로 바뀌기 전 장면 색 (서서히 붉게 섞는다)
    Color baseFog, baseAmbient, baseSky;
    Camera skyCam;
    readonly List<Light> sunLights = new List<Light>();
    readonly List<Color> sunColors = new List<Color>();
    float redNightBlend, redNightTimer, heartbeatVolume;

    readonly List<Zombie> zombies = new List<Zombie>(); // 거리를 돌아다니는 좀비 (최대 수에 포함)
    readonly List<Zombie> others = new List<Zombie>();  // 2층 지킴이·약탈자 (최대 수에 포함하지 않음)
    ZombieSpawnPoint[] zombiePoints;
    AudioSource wind, heartbeat;
    float spawnTimer = 30f, distantSoundTimer = 8f, traderHintTimer = -1f, raiderHintTimer = -1f;
    bool warned, ended;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        balance = GameBalance.Data;
        Weather.Begin(); // 이번 탐험의 날씨 (붉은 밤보다 먼저 적용된다)
        timeLimit = balance.expeditionSeconds;
        TimeLeft = timeLimit;
        zombiePoints = FindObjectsByType<ZombieSpawnPoint>(FindObjectsSortMode.None);
        int turn = GameManager.Instance.State.Turn;

        foreach (var p in FindObjectsByType<ItemSpawnPoint>(FindObjectsSortMode.None))
        {
            if (Random.value > balance.itemSpawnChance) continue;
            string id = ItemDatabase.RollLoot(p.outdoor);
            int n = (id == "cloth" || id == "stick") && Random.value < 0.4f ? 2 : 1;
            PickupItem.Spawn(id, n, p.transform.position);
        }

        for (int i = 0; i < balance.startZombies; i++) SpawnZombie();
        SpawnUpstairsZombies(turn);
        for (int i = 0; i < balance.dogCount; i++) Spawn(dogPrefab);

        // 약탈자: raider.fromTurn 턴부터, 탐험마다 raiderChance 확률로 1~2명
        if (turn >= balance.raider.fromTurn && Random.value < balance.raiderChance)
        {
            int n = Random.Range(1, 3), spawned = 0;
            for (int i = 0; i < n; i++)
            {
                var r = SpawnZombie(ZombieKind.Raider, false);
                if (r != null) { others.Add(r); spawned++; }
            }
            if (spawned > 0) raiderHintTimer = 12f;
        }

        // 잡상인: traderChance 확률로만 나타난다. 없으면 노점(인물·상자·등불)째로 끈다
        bool traderHere = Random.value < balance.traderChance;
        var traders = FindObjectsByType<Trader>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (!traderHere)
            foreach (var t in traders) t.gameObject.SetActive(false);
        else if (traders.Length > 0) traderHintTimer = 6f;

        wind = MakeLoop(ProceduralAudio.Wind(), 0.35f);
        heartbeat = MakeLoop(ProceduralAudio.Heartbeat(), 0f);
        GameManager.Instance.UI.ShowMessage("폐허가 된 도시. 붉은 등이 켜진 해치가 돌아갈 곳이다.", 5f);
    }

    void StartRedNight(GameManager gm)
    {
        IsRedNight = true;
        TimeLeft = 0;
        baseFog = RenderSettings.fogColor;
        baseAmbient = RenderSettings.ambientLight;
        skyCam = Camera.main;
        if (skyCam != null) baseSky = skyCam.backgroundColor;
        sunLights.Clear();
        sunColors.Clear();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sunLights.Add(l); sunColors.Add(l.color); }
        ProceduralAudio.Play2D(ProceduralAudio.Siren(), 0.5f, 0.8f);
        gm.UI.ShowMessage("붉은 밤이 왔다! 체력이 계속 줄어든다. 어서 해치로 돌아가자!", 5f);
        heartbeatVolume = 0.7f;
    }

    // 붉은 밤: 4초에 걸쳐 장면이 붉게 물들고, 정해진 간격마다 체력이 준다. 좀비는 계속 가장 많이 나온다
    void UpdateRedNight(GameManager gm, float dt)
    {
        RedNightElapsed += dt;
        redNightBlend = Mathf.Min(1f, redNightBlend + dt / 4f);
        float k = Mathf.SmoothStep(0, 1, redNightBlend);
        var red = new Color(0.32f, 0.03f, 0.02f);
        RenderSettings.fogColor = Color.Lerp(baseFog, red, k);
        RenderSettings.ambientLight = Color.Lerp(baseAmbient, new Color(0.35f, 0.05f, 0.04f) * (0.8f + 0.2f * Mathf.Sin(Time.time * 1.3f)), k);
        if (skyCam != null) skyCam.backgroundColor = Color.Lerp(baseSky, new Color(0.16f, 0.01f, 0.01f), k);
        for (int i = 0; i < sunLights.Count; i++)
            if (sunLights[i] != null) sunLights[i].color = Color.Lerp(sunColors[i], new Color(1f, 0.18f, 0.1f), k);
        gm.UI.SetRedNight(k);

        var b = GameBalance.Data;
        redNightTimer += dt;
        if (redNightTimer >= b.redNightInterval)
        {
            redNightTimer -= b.redNightInterval;
            gm.Damage(b.redNightDamage, "붉은 밤을 끝내 버티지 못했다.");
            ProceduralAudio.Play2D(ProceduralAudio.Heartbeat(), 0.6f, 0.9f);
        }
    }

    void OnDisable()
    {
        if (GameManager.Instance != null && GameManager.Instance.UI != null) GameManager.Instance.UI.SetRedNight(0);
    }

    AudioSource MakeLoop(AudioClip clip, float volume)
    {
        var src = gameObject.AddComponent<AudioSource>();
        AudioCategory.Tag(src, SoundCategory.Ambient);
        src.clip = clip;
        src.loop = true;
        src.volume = volume;
        src.spatialBlend = 0;
        src.Play();
        return src;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.IsGameOver || gm.IsTransitioning) return;
        float dt = Time.deltaTime;

        TimeLeft -= dt;
        if (!warned && TimeLeft <= 60)
        {
            warned = true;
            ProceduralAudio.Play2D(ProceduralAudio.Siren(), 0.35f);
            gm.UI.ShowMessage("1분 남았다. 서둘러 해치로 돌아가야 한다!", 4f);
        }
        if (TimeLeft <= 0 && !ended)
        {
            ended = true;
            StartRedNight(gm);
        }
        if (IsRedNight) UpdateRedNight(gm, dt);

        // 잡상인·약탈자가 있다는 힌트 (시작 안내문이 지나간 뒤)
        if (traderHintTimer > 0)
        {
            traderHintTimer -= dt;
            if (traderHintTimer <= 0) gm.UI.ShowMessage("어딘가에서 잡상인의 목소리가 들린다.", 4f);
        }
        if (raiderHintTimer > 0)
        {
            raiderHintTimer -= dt;
            if (raiderHintTimer <= 0) RaiderHint(gm);
        }

        // 시간이 갈수록 좀비가 더 자주, 더 많이 나타난다
        spawnTimer -= dt;
        if (spawnTimer <= 0)
        {
            spawnTimer = Mathf.Lerp(balance.zombieSpawnIntervalStart, balance.zombieSpawnIntervalEnd, Danger);
            zombies.RemoveAll(z => z == null || z.IsDead);
            others.RemoveAll(z => z == null || z.IsDead);
            if (zombies.Count < Mathf.RoundToInt(Mathf.Lerp(balance.startZombies, balance.maxZombies, Danger))) SpawnZombie();
        }

        // 쫓아오는 적이 가까울수록 심장 소리가 커진다
        var player = PlayerController.Instance;
        float threat = 0;
        if (player != null)
        {
            Vector3 pp = player.transform.position;
            threat = Mathf.Max(Threat(zombies, pp), Threat(others, pp));
            foreach (var a in Animal.All)
                if (a != null && a.IsChasing)
                    threat = Mathf.Max(threat, 1 - Vector3.Distance(a.transform.position, pp) / 20f);
        }
        heartbeatVolume = Mathf.MoveTowards(heartbeatVolume, threat * 0.9f, dt);
        heartbeat.volume = heartbeatVolume;
        heartbeat.pitch = 1 + threat * 0.5f;

        // 멀리서 들려오는 신음 소리
        distantSoundTimer -= dt;
        if (distantSoundTimer <= 0 && player != null)
        {
            distantSoundTimer = Random.Range(10f, 25f) * Mathf.Lerp(1f, 0.5f, Danger);
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(25f, 45f);
            var pos = player.transform.position + new Vector3(r.x, 1, r.y);
            // 녹음한 좀비 목소리 (예전 합성음 Groan/Scream은 더 쓰지 않는다)
            var clip = Random.value < 0.25f ? ProceduralAudio.ZombieScream() : ProceduralAudio.ZombieGroan();
            ProceduralAudio.PlayAt(clip, pos, 0.8f, Random.Range(0.8f, 0.98f), 1f, 60f, SoundCategory.Ambient);
        }
    }

    static float Threat(List<Zombie> list, Vector3 playerPos)
    {
        float threat = 0;
        foreach (var z in list)
            if (z != null && z.IsChasing)
                threat = Mathf.Max(threat, 1 - Vector3.Distance(z.transform.position, playerPos) / 20f);
        return threat;
    }

    // 약탈자 쪽에서 고함이 들리고 안내문이 뜬다
    void RaiderHint(GameManager gm)
    {
        var player = PlayerController.Instance;
        var raider = others.Find(z => z != null && !z.IsDead && z.Kind == ZombieKind.Raider);
        if (player == null || raider == null) return;
        gm.UI.ShowMessage("멀리서 사람의 고함 소리가 들린다. 약탈자가 있다.", 4f);
        var dir = raider.transform.position - player.transform.position;
        var pos = player.transform.position + Vector3.ClampMagnitude(dir, 30f) + Vector3.up;
        ProceduralAudio.PlayAt(ProceduralAudio.RaiderShout(), pos, 0.7f, Random.Range(0.95f, 1.05f), 1f, 60f, SoundCategory.Enemy);
    }

    // 지금 턴에 나올 수 있는 종류 중에서 가중치로 고른다
    ZombieKind PickKind(int turn)
    {
        float wN = normalWeight;
        float wL = turn >= balance.leatherZombie.fromTurn ? leatherWeight : 0;
        float wR = turn >= balance.rangedZombie.fromTurn ? rangedWeight : 0;
        float wH = turn >= balance.heavyZombie.fromTurn ? heavyWeight : 0;
        float r = Random.value * (wN + wL + wR + wH);
        if ((r -= wL) < 0) return ZombieKind.Leather;
        if ((r -= wR) < 0) return ZombieKind.Ranged;
        if ((r -= wH) < 0) return ZombieKind.Heavy;
        return ZombieKind.Normal;
    }

    void SpawnZombie() => SpawnZombie(PickKind(GameManager.Instance.State.Turn), true);

    Zombie SpawnZombie(ZombieKind kind, bool counted)
    {
        var go = Spawn(zombiePrefab);
        if (go == null) return null;
        var z = go.GetComponent<Zombie>();
        if (z == null) return null;
        z.Configure(kind);
        if (counted) zombies.Add(z);
        return z;
    }

    // 2층 좀비 자리마다 upstairsFill 확률로 좀비를 미리 세워 둔다 (반드시 나오지는 않는다. 기본 시작 좀비와 별개로, 전리품을 지킨다)
    void SpawnUpstairsZombies(int turn)
    {
        if (zombiePrefab == null) return;
        var points = new List<ZombieSpawnPoint>();
        foreach (var p in zombiePoints)
            if (p != null && p.upstairs) points.Add(p);
        var player = PlayerController.Instance;
        foreach (var point in points)
        {
            if (Random.value >= upstairsFill) continue;
            var pos = point.transform.position;
            if (player != null && Vector3.Distance(pos, player.transform.position) < balance.safeRadius * 0.5f) continue;
            // 반경을 작게 잡아야 아래층 바닥에 붙지 않는다
            if (!NavMesh.SamplePosition(pos, out var hit, 1f, NavMesh.AllAreas)) continue;
            var go = Instantiate(zombiePrefab, hit.position, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            var z = go.GetComponent<Zombie>();
            if (z == null) continue;
            z.Configure(PickKind(turn));
            z.SetGuardPoint(hit.position);
            others.Add(z);
        }
    }

    GameObject Spawn(GameObject prefab)
    {
        if (prefab == null || zombiePoints.Length == 0) return null;
        var player = PlayerController.Instance;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var point = zombiePoints[Random.Range(0, zombiePoints.Length)];
            // 2층 자리는 지킴이 좀비 몫이다 (거리 좀비·들개는 바깥에서 나온다)
            if (point.upstairs) continue;
            var p = point.transform.position;
            if (player != null && Vector3.Distance(p, player.transform.position) < balance.safeRadius) continue;
            if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) continue;
            return Instantiate(prefab, hit.position, Quaternion.Euler(0, Random.Range(0, 360f), 0));
        }
        return null;
    }
}
