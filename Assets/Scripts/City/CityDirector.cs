using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 도시 탐험 진행: 제한시간, 아이템 배치, 좀비 등장(시간이 갈수록 늘어남), 공포 사운드
public class CityDirector : MonoBehaviour
{
    // 제한시간·좀비 수·아이템 확률은 GameBalance 에셋에서 조정한다 (기획서: 약 5분)
    public static float DefaultTimeLimit => GameBalance.Data.expeditionSeconds;

    public static CityDirector Instance { get; private set; }

    public GameObject zombiePrefab;
    public GameObject dogPrefab;

    GameBalance balance;
    float timeLimit;

    public float TimeLeft { get; private set; }
    public float Danger => Mathf.Clamp01(1 - TimeLeft / timeLimit); // 0 → 1로 점점 위험해짐

    readonly List<Zombie> zombies = new List<Zombie>();
    ZombieSpawnPoint[] zombiePoints;
    AudioSource wind, heartbeat;
    float spawnTimer = 30f, distantSoundTimer = 8f;
    bool warned, ended;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        balance = GameBalance.Data;
        timeLimit = balance.expeditionSeconds;
        TimeLeft = timeLimit;
        zombiePoints = FindObjectsByType<ZombieSpawnPoint>(FindObjectsSortMode.None);

        foreach (var p in FindObjectsByType<ItemSpawnPoint>(FindObjectsSortMode.None))
        {
            if (Random.value > balance.itemSpawnChance) continue;
            string id = ItemDatabase.RollLoot();
            int n = (id == "cloth" || id == "wood") && Random.value < 0.4f ? 2 : 1;
            PickupItem.Spawn(id, n, p.transform.position);
        }

        for (int i = 0; i < balance.startZombies; i++) SpawnZombie();
        for (int i = 0; i < balance.dogCount; i++) Spawn(dogPrefab);

        wind = MakeLoop(ProceduralAudio.Wind(), 0.35f);
        heartbeat = MakeLoop(ProceduralAudio.Heartbeat(), 0f);
        GameManager.Instance.UI.ShowMessage("폐허가 된 도시. 붉은 등이 켜진 해치가 돌아갈 곳이다.", 5f);
    }

    AudioSource MakeLoop(AudioClip clip, float volume)
    {
        var src = gameObject.AddComponent<AudioSource>();
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
            gm.ExpeditionTimeUp();
            return;
        }

        // 시간이 갈수록 좀비가 더 자주, 더 많이 나타난다
        spawnTimer -= dt;
        if (spawnTimer <= 0)
        {
            spawnTimer = Mathf.Lerp(balance.zombieSpawnIntervalStart, balance.zombieSpawnIntervalEnd, Danger);
            zombies.RemoveAll(z => z == null || z.IsDead);
            if (zombies.Count < Mathf.RoundToInt(Mathf.Lerp(balance.startZombies, balance.maxZombies, Danger))) SpawnZombie();
        }

        // 쫓아오는 좀비가 가까울수록 심장 소리가 커진다
        var player = PlayerController.Instance;
        float threat = 0;
        if (player != null)
            foreach (var z in zombies)
                if (z != null && z.IsChasing)
                    threat = Mathf.Max(threat, 1 - Vector3.Distance(z.transform.position, player.transform.position) / 20f);
        heartbeat.volume = Mathf.MoveTowards(heartbeat.volume, threat * 0.9f, dt);
        heartbeat.pitch = 1 + threat * 0.5f;

        // 멀리서 들려오는 신음 소리
        distantSoundTimer -= dt;
        if (distantSoundTimer <= 0 && player != null)
        {
            distantSoundTimer = Random.Range(10f, 25f) * Mathf.Lerp(1f, 0.5f, Danger);
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(25f, 45f);
            var pos = player.transform.position + new Vector3(r.x, 1, r.y);
            var clip = Random.value < 0.25f ? ProceduralAudio.Scream() : ProceduralAudio.Groan();
            ProceduralAudio.PlayAt(clip, pos, 0.8f, Random.Range(0.6f, 0.85f), 1f, 60f);
        }
    }

    void SpawnZombie()
    {
        var go = Spawn(zombiePrefab);
        if (go == null) return;
        go.transform.localScale = Vector3.one * Random.Range(0.92f, 1.08f);
        zombies.Add(go.GetComponent<Zombie>());
    }

    GameObject Spawn(GameObject prefab)
    {
        if (prefab == null || zombiePoints.Length == 0) return null;
        var player = PlayerController.Instance;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var p = zombiePoints[Random.Range(0, zombiePoints.Length)].transform.position;
            if (player != null && Vector3.Distance(p, player.transform.position) < balance.safeRadius) continue;
            if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) continue;
            return Instantiate(prefab, hit.position, Quaternion.Euler(0, Random.Range(0, 360f), 0));
        }
        return null;
    }
}
