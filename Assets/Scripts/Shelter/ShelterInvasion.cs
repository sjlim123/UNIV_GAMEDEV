using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 발전기 디버프: 전력이 0%가 되면(정전) 방폭문이 열리고, 문 너머 어둠에서 좀비가 일정 간격으로 들어온다.
// 침입한 좀비가 살아 있으면 출발·휴식을 할 수 없다. 연료를 넣으면 더 들어오지 않고 문이 다시 닫힌다.
public class ShelterInvasion : MonoBehaviour
{
    public static ShelterInvasion Instance { get; private set; }

    public GameObject zombiePrefab;
    public BlastDoor door;
    public Transform[] spawnPoints;
    [Tooltip("동시에 살아 있을 수 있는 침입 좀비 수")]
    public int maxAlive = 6;
    [Tooltip("정전 후 첫 좀비가 들어오기까지(초). 그다음부터는 GameBalance.outageZombieInterval")]
    public float firstDelay = 5f;

    const string ZombieWarning = "방공호 안에 감염자가 있다. 먼저 처치해야 출발할 수 있다.";

    readonly List<Zombie> invaders = new List<Zombie>();
    float spawnTimer, alertTimer, groanTimer, lockoutTimer;
    bool outage, warnedNoNavMesh;
    int pendingInvaders; // 저장 당시 안에 있던 좀비 (불러온 뒤 곧바로 다시 들어온다)

    public bool IsOutage => outage;

    public int AliveCount
    {
        get
        {
            int n = 0;
            foreach (var z in invaders) if (z != null && !z.IsDead) n++;
            return n;
        }
    }

    // 살아 있거나 곧 다시 들어올 침입 좀비 수 (저장에 쓴다)
    public int InvaderCount => AliveCount + pendingInvaders;

    // 살아 있는 침입 좀비가 있는가 (방공호 씬이 아니면 false)
    public static bool ZombiesInside => Instance != null && Instance.InvaderCount > 0;

    // 좀비가 남아 있으면 안내를 띄우고 true. 출발·휴식을 막을 때 쓴다
    public static bool BlockedByZombies(string message = ZombieWarning)
    {
        if (!ZombiesInside) return false;
        GameManager.Instance.UI.ShowMessage(message, 3f);
        return true;
    }

    void Awake()
    {
        Instance = this;
        // 씬을 연 직후의 자동 저장(GameManager.OnSceneLoaded)보다 먼저 읽어 둔다
        var s = GameManager.Instance.State;
        pendingInvaders = s.ShelterInvaders;
        s.ShelterInvaders = 0;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (zombiePrefab == null) Debug.LogWarning("[ShelterInvasion] zombiePrefab이 비어 있다. 씬을 다시 생성하세요.");
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.IsGameOver || gm.IsTransitioning) return;
        float dt = Time.deltaTime;
        invaders.RemoveAll(z => z == null || z.IsDead);

        bool powered = gm.State.Power > 0;
        if (!powered && !outage) BeginOutage();
        else if (powered && outage) EndOutage();

        var player = PlayerController.Instance;
        // 저장 당시 안에 있던 좀비는 바로 다시 들여보낸다 (보이는 곳이어도)
        if (pendingInvaders > 0 && TrySpawn(player, true)) pendingInvaders--;
        if (outage) UpdateOutage(dt, player);
        else UpdateClosing(dt, player);

        // 들어온 좀비는 플레이어 냄새를 맡고 계속 쫓아온다 (구석에서 헤매다 출발이 막히지 않게)
        alertTimer -= dt;
        if (alertTimer <= 0 && player != null)
        {
            alertTimer = 2f;
            invaders.RemoveAll(z =>
            {
                if (z.transform.position.y > -5f) return false;
                Destroy(z.gameObject); // 바닥 밑으로 빠진 좀비는 정리한다
                return true;
            });
            foreach (var z in invaders) z.Alert(player.transform.position);
        }
    }

    void BeginOutage()
    {
        outage = true;
        spawnTimer = firstDelay;
        groanTimer = 8f;
        if (door != null)
        {
            door.Open();
            ProceduralAudio.PlayAt(ProceduralAudio.Siren(), door.transform.position + Vector3.up * 2f, 0.6f, 0.75f, 1f, 50f);
        }
        GameManager.Instance.UI.ShowMessage("전력이 끊겼다! 방공호 문이 열렸다… 발전기에 연료를 넣어야 한다.", 5f);
    }

    void EndOutage()
    {
        outage = false;
        int n = AliveCount;
        GameManager.Instance.UI.ShowMessage(n > 0
            ? $"전력이 돌아왔다. 방폭문이 닫힌다… 안에 들어온 감염자 {n}마리를 처치해야 한다."
            : "전력이 돌아왔다. 방폭문이 닫힌다.", 4f);
    }

    void UpdateOutage(float dt, PlayerController player)
    {
        // 문 너머 어둠에서 들려오는 신음
        groanTimer -= dt;
        if (groanTimer <= 0 && spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[0] != null)
        {
            groanTimer = Random.Range(9f, 18f);
            ProceduralAudio.PlayAt(ProceduralAudio.ZombieGroan(), spawnPoints[0].position + Vector3.up * 1.5f, 0.7f, Random.Range(0.78f, 0.9f), 1f, 30f, SoundCategory.Enemy);
        }

        spawnTimer -= dt;
        if (spawnTimer > 0) return;
        if (door != null && !door.IsOpen) { spawnTimer = 1f; return; }
        if (AliveCount >= maxAlive)
        {
            spawnTimer = GameBalance.Data.outageZombieInterval; // 이번 차례는 건너뛴다
            return;
        }
        // 플레이어가 등장 지점을 보고 있으면 잠시 미룬다 (눈앞에서 갑자기 생기지 않게)
        spawnTimer = TrySpawn(player) ? GameBalance.Data.outageZombieInterval : 1.5f;
    }

    bool TrySpawn(PlayerController player, bool ignoreSight = false)
    {
        if (zombiePrefab == null || spawnPoints == null) return false;
        foreach (var p in spawnPoints)
        {
            if (p == null || (!ignoreSight && CanPlayerSee(p.position, player))) continue;
            if (!NavMesh.SamplePosition(p.position, out var hit, 1.5f, NavMesh.AllAreas))
            {
                if (!warnedNoNavMesh) Debug.LogWarning("[ShelterInvasion] 좀비 등장 지점 근처에 내비메시가 없다. 씬을 다시 생성하세요.");
                warnedNoNavMesh = true;
                continue;
            }
            var go = Instantiate(zombiePrefab, hit.position, p.rotation);
            var z = go.GetComponent<Zombie>();
            if (z == null)
            {
                Destroy(go);
                return false;
            }
            invaders.Add(z);
            if (player != null) z.Alert(player.transform.position);
            ProceduralAudio.PlayAt(ProceduralAudio.ZombieGroan(), hit.position + Vector3.up * 1.5f, 1f, Random.Range(0.85f, 1f), 1f, 30f, SoundCategory.Enemy);
            return true;
        }
        return false;
    }

    static bool CanPlayerSee(Vector3 point, PlayerController player)
    {
        if (player == null) return false;
        Vector3 eye = player.cameraRoot != null ? player.cameraRoot.position : player.transform.position + Vector3.up * 1.6f;
        Vector3 target = point + Vector3.up * 1.2f;
        if (Vector3.Distance(eye, target) < 3f) return true;
        return !Physics.Linecast(eye, target, ~0, QueryTriggerInteraction.Ignore);
    }

    // 전력이 돌아온 뒤: 문간·문 바깥에 좀비나 플레이어가 없을 때 문을 닫는다
    void UpdateClosing(float dt, PlayerController player)
    {
        if (door == null || door.IsClosed)
        {
            lockoutTimer = 0;
            return;
        }

        bool zombieInDoorway = false, zombieBeyond = false;
        foreach (var z in invaders)
        {
            var p = z.transform.position;
            if (door.InDoorway(p)) zombieInDoorway = true;
            else if (door.IsBeyond(p)) zombieBeyond = true;
        }
        bool playerNear = player != null && (door.InDoorway(player.transform.position) || door.IsBeyond(player.transform.position));

        if (!zombieInDoorway && !zombieBeyond && !playerNear)
        {
            lockoutTimer = 0;
            door.Close();
            return;
        }

        // 닫히는 도중에 누가 문간·바깥에 있으면 다시 연다 (끼이거나 갇히지 않게)
        if (!door.IsOpen) door.Open();

        // 바깥 통로에서 끝내 들어오지 않는 좀비는 어둠 속으로 사라진 것으로 친다 (출발이 영영 막히지 않게)
        if (zombieBeyond && !zombieInDoorway && !playerNear)
        {
            lockoutTimer += dt;
            if (lockoutTimer > 20f)
            {
                lockoutTimer = 0;
                invaders.RemoveAll(z =>
                {
                    if (!door.IsBeyond(z.transform.position) || door.InDoorway(z.transform.position)) return false;
                    Destroy(z.gameObject);
                    return true;
                });
            }
        }
        else lockoutTimer = 0;
    }
}
