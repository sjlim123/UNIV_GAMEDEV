using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 좀비 종류. 모두 같은 좀비 프리팹을 Zombie.Configure로 바꿔 쓴다
public enum ZombieKind { Normal, Leather, Ranged, Heavy, Raider }

// 좀비: 배회 → 소리를 들으면 확인하러 옴 → 보이면 추격·공격. 닫힌 문은 두드려 부순다.
// 종류에 따라 수치(GameBalance)·겉모습·공격 방식이 다르다. Configure를 부르지 않으면 일반 좀비.
// 죽으면 관절이 풀려 물리(Ragdoll)로 힘없이 무너지고, 쓰러진 자리에 피가 번지며, 잠시 뒤 어두워지면서 땅으로 가라앉아 사라진다(Gore)
[RequireComponent(typeof(NavMeshAgent))]
public class Zombie : MonoBehaviour, IDamageable
{
    // 속도·시야·피해 등은 GameBalance 에셋에서 조정한다
    public float attackRange = 1.6f;

    // 관절 (Configure가 몸을 지을 때 채운다): 몸통·머리·턱·어깨·팔꿈치·엉덩이·무릎
    public Transform body, head, armL, armR, legL, legR, foreL, foreR, shinL, shinR, jaw;
    // 눈이 빛나는 재질의 원본 (빌드에 발광 셰이더가 들어가게 프리팹이 들고 있는다)
    public Material eyeTemplate;

    // 원거리 좀비: 이보다 가까우면 뒷걸음질쳐 거리를 벌린다 (최대 사거리는 GameBalance.rangedAttackRange)
    public const float RangedKeepMin = 6f;
    // 원거리 좀비가 새총으로 쏜 돌이 날아가는 수평 속도(m/s)
    public const float ProjectileSpeed = 19f;
    // 공격 예비 동작 시간(초). 이 동안 때리면 공격이 끊긴다 (거대 좀비는 안 끊긴다)
    const float RangedWindup = 0.8f, HeavyWindup = 0.6f, RaiderWindup = 0.45f;
    // 새총 예비 동작 진행도: SlingGrab까지 돌을 메기며 겨누고, SlingFull까지 고무줄을 끝까지 당긴다
    const float SlingGrab = 0.3f, SlingFull = 0.85f;
    // 놓은 뒤 자세를 유지하는 시간과 팔을 내리는 시간(초)
    const float SlingHold = 0.2f, SlingLower = 0.4f;

    enum State { Wander, Investigate, Chase, Dead }

    State state;
    NavMeshAgent agent;
    AudioSource voice, feet;
    GameBalance balance;
    EnemyStats stats;
    PlayerFlashlight playerLight;
    float health, lostSightTimer, attackTimer, groanTimer, wanderTimer, lookTimer, staggerTimer, repathTimer, doorTimer, animPhase, lunge;
    float windup, windupLength, stepDistance, baseAttackRange, size = 1f;
    float hitReact, mouth; // 맞아서 움찔한 정도, 입을 벌린 정도 (0~1, 저절로 줄어든다)
    float hitStun, hitSlow; // 맞고 굳은 시간, 그 뒤 느려지는 시간 (GameBalance.enemyHit*)
    bool shooting, backpedal, guarding, configured;
    CharacterRig rig;
    HitFlash flash;
    int armorLost; // 가죽 좀비: 떨어져 나간 갑옷 조각 수
    Vector3 lastKnown, guardPoint;

    // 원거리 좀비의 새총 (다른 종류는 null)
    ZombieLooks.Slingshot sling;
    AudioSource stretchSound;
    Vector3 aimPoint;
    Quaternion slingHold = Quaternion.identity; // 마지막으로 당기던 오른팔 자세 (몸 기준)
    float slingSnap = -1;                       // 고무줄을 놓은 뒤 지난 시간 (-1 = 놓지 않음)
    float slingDraw, slingAim, slingHoldK, snapDraw, snapAim, snapHoldK;
    bool slingDrawn, slingPosed;

    public ZombieKind Kind { get; private set; }
    public bool IsDead => state == State.Dead;
    public float Health => health;                                    // 남은 체력 (HUD 체력바)
    public float MaxHealth => stats != null ? stats.health : 1f;
    public string DisplayName => stats != null ? stats.displayName : "좀비";
    public bool IsChasing => state == State.Chase;
    bool IsRaider => Kind == ZombieKind.Raider;

    // 다른 스크립트(방공호 침입 등)가 좀비를 곧장 그 위치로 달려오게 할 때 쓴다
    public void Alert(Vector3 position)
    {
        if (state == State.Dead) return;
        lastKnown = position;
        lostSightTimer = 0;
        state = State.Chase;
    }

    // 종류를 정한다. Instantiate 직후에 부른다
    public void Configure(ZombieKind kind)
    {
        if (state == State.Dead) return;
        configured = true;
        Kind = kind;
        stats = StatsFor(kind);
        health = stats.health;
        float scale = kind == ZombieKind.Heavy ? 1.35f : kind == ZombieKind.Leather ? 1.06f : 1f;
        transform.localScale = Vector3.one * scale * Random.Range(0.94f, 1.06f);
        size = transform.localScale.y;
        // 거대 좀비는 팔이 길고, 약탈자는 방망이만큼 더 닿는다
        attackRange = baseAttackRange * (kind == ZombieKind.Heavy ? 1.3f : kind == ZombieKind.Raider ? 1.2f : 1f);
        if (kind == ZombieKind.Raider) groanTimer = Random.Range(6f, 12f);
        rig = ZombieLooks.Apply(this, kind, out sling, out _);
        body = rig.Body; head = rig.Head; jaw = rig.Jaw;
        armL = rig.ArmL; armR = rig.ArmR; foreL = rig.ForeL; foreR = rig.ForeR;
        legL = rig.LegL; legR = rig.LegR; shinL = rig.ShinL; shinR = rig.ShinR;
        flash = HitFlash.For(gameObject, rig.Renderers);
        armorLost = 0;
    }

    // Configure를 부르지 않고 놓인 좀비는 일반 좀비로 짓는다
    void Start()
    {
        if (!configured) Configure(ZombieKind.Normal);
    }

    // 이 자리 근처만 맴돈다 (2층 전리품을 지키는 좀비)
    public void SetGuardPoint(Vector3 point)
    {
        guarding = true;
        guardPoint = point;
    }

    public static EnemyStats StatsFor(ZombieKind kind)
    {
        var b = GameBalance.Data;
        switch (kind)
        {
            case ZombieKind.Leather: return b.leatherZombie;
            case ZombieKind.Ranged: return b.rangedZombie;
            case ZombieKind.Heavy: return b.heavyZombie;
            case ZombieKind.Raider: return b.raider;
            default: return b.zombie;
        }
    }

    void Awake()
    {
        if (eyeTemplate != null) ProcTex.GlowTemplate = eyeTemplate;
        agent = GetComponent<NavMeshAgent>();
        balance = GameBalance.Data;
        stats = balance.zombie;
        health = stats.health;
        baseAttackRange = attackRange;
        // 목소리·발소리: 완전 3D, 방향이 또렷하고 15~20m에서도 잘 들린다
        // (두 AudioSource를 먼저 붙이고 나서 Setup3D를 부른다: Setup3D가 붙이는 저음 필터가 있는 오브젝트에
        //  AudioSource를 더 붙이면 유니티가 빈 소리를 재생하려다 "Only custom filters can be played" 경고를 낸다)
        voice = gameObject.AddComponent<AudioSource>();
        feet = gameObject.AddComponent<AudioSource>();
        ProceduralAudio.Setup3D(voice, 36f, 64);
        ProceduralAudio.Setup3D(feet, 26f, 150);
        groanTimer = Random.Range(2f, 8f);
        animPhase = Random.value * 10;
    }

    void Update()
    {
        if (state == State.Dead) return;
        var player = PlayerController.Instance;
        if (player == null || GameManager.Instance.IsGameOver || !agent.isOnNavMesh) { Animate(); return; }

        float dt = Time.deltaTime;
        // 맞고 굳은 동안은 움직임·공격 준비·공격 간격·몸짓이 모두 멈춘다 (맞은 자세 그대로)
        if (hitStun > 0)
        {
            hitStun -= dt;
            agent.isStopped = true;
            return;
        }
        float danger = CityDirector.Instance != null ? CityDirector.Instance.Danger : 0;
        Vector3 playerPos = player.transform.position;
        float dist = Vector3.Distance(transform.position, playerPos);

        // 최적화: 멀리서 배회 중인 좀비는 시야·청각 검사를 건너뛴다
        if (dist > 45f && state == State.Wander)
        {
            Wander(dt);
            Animate();
            return;
        }

        bool sees = CanSee(player, dist, danger);

        if (sees)
        {
            if (state != State.Chase)
            {
                Scream();
                Tutorial.Hint(Tutorial.HintZombie, "놈이 알아챘다. 달리면 소리가 커지고, 앉으면 조용하다. 무기를 들고 벽을 등지자.");
            }
            state = State.Chase;
            lastKnown = playerPos;
            lostSightTimer = 0;
        }
        else if (state != State.Chase && player.Noise > 0 && dist < balance.zombieHearingRange * player.Noise * (IsRaider ? 1.3f : 1f))
        {
            state = State.Investigate;
            lastKnown = playerPos;
            lookTimer = 3f;
        }

        staggerTimer -= dt;
        attackTimer -= dt;
        repathTimer -= dt;
        backpedal = false;

        // 공격 예비 동작이 끝나면 휘두르기·내려찍기·새총 쏘기
        if (windup > 0)
        {
            windup -= dt;
            FaceTowards(playerPos);
            if (shooting)
            {
                aimPoint = ChestOf(player);
                // 돌을 메기고 나면 고무줄을 끼이익 당긴다
                if (!slingDrawn && windup < windupLength * (1 - SlingGrab))
                {
                    slingDrawn = true;
                    stretchSound = ProceduralAudio.PlayAt3D(ProceduralAudio.SlingStretch(), SlingPoint(), 0.85f, Random.Range(0.95f, 1.08f));
                }
            }
            if (windup <= 0) ReleaseAttack(player, playerPos, dist, danger);
        }
        agent.isStopped = staggerTimer > 0 || windup > 0;

        switch (state)
        {
            case State.Chase:
                agent.speed = balance.ToMetersPerSecond(stats.speed) * (1 + 0.3f * danger);
                if (!sees)
                {
                    lostSightTimer += dt;
                    if (lostSightTimer > 5f) { state = State.Investigate; lookTimer = 4f; }
                }
                if (Kind == ZombieKind.Ranged) RangedChase(sees, playerPos, dist, danger);
                else
                {
                    if (repathTimer <= 0) { agent.SetDestination(sees ? playerPos : lastKnown); repathTimer = 0.2f; }
                    if (dist < attackRange + 0.6f) FaceTowards(playerPos);
                }
                // 벽 너머로는 휘두르지 않는다 (문을 두드리는 쪽으로 간다)
                if (dist < attackRange && attackTimer <= 0 && staggerTimer <= 0 && windup <= 0 && ReachesPlayer(PlayerController.Instance)) StartAttack(playerPos, dist, danger);
                TryBreakDoor(dt);
                break;

            case State.Investigate:
                agent.speed = balance.zombieWanderSpeed * (IsRaider ? 2.6f : 1.8f);
                if (repathTimer <= 0) { agent.SetDestination(lastKnown); repathTimer = 0.5f; }
                if (!agent.pathPending && agent.remainingDistance < 1.5f)
                {
                    lookTimer -= dt;
                    transform.Rotate(0, 40 * dt, 0);
                    if (lookTimer <= 0) state = State.Wander;
                }
                TryBreakDoor(dt);
                break;

            default:
                Wander(dt);
                break;
        }

        // 굳었다 풀린 뒤 다시 움직일 때 잠깐 느리다
        if (hitSlow > 0 && staggerTimer <= 0)
        {
            hitSlow -= dt;
            agent.speed *= balance.enemyHitSlow;
        }

        // 원거리 좀비가 뒷걸음질칠 때는 플레이어를 노려본 채 물러난다
        agent.updateRotation = !backpedal;
        if (backpedal) FaceTowards(playerPos);

        Footsteps(dt, dist);
        groanTimer -= dt;
        if (groanTimer <= 0) Vocalize(dist);

        Animate();
    }

    void Wander(float dt)
    {
        agent.speed = balance.zombieWanderSpeed * (IsRaider ? 1.7f : 1f);
        wanderTimer -= dt;
        if (wanderTimer <= 0 || (!agent.pathPending && agent.remainingDistance < 0.5f))
        {
            wanderTimer = Random.Range(5f, 12f);
            if (guarding)
            {
                // 지키는 자리에서 멀리 가지 않는다 (작은 반경으로 찾아야 아래층으로 새지 않는다)
                var r = Random.insideUnitCircle * 3f;
                if (NavMesh.SamplePosition(guardPoint + new Vector3(r.x, 0, r.y), out var near, 1.5f, NavMesh.AllAreas)) agent.SetDestination(near.position);
                return;
            }
            var target = transform.position + Random.insideUnitSphere * 10f;
            if (NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }
    }

    // 원거리 좀비: 6m~사거리를 유지하며 새총으로 돌을 쏜다
    void RangedChase(bool sees, Vector3 playerPos, float dist, float danger)
    {
        if (sees && dist <= balance.rangedAttackRange)
        {
            if (dist < RangedKeepMin)
            {
                // 너무 가까우면 뒷걸음질쳐 거리를 벌린다
                backpedal = true;
                agent.speed *= 0.6f;
                if (repathTimer <= 0)
                {
                    repathTimer = 0.4f;
                    var away = transform.position - playerPos;
                    away.y = 0;
                    if (NavMesh.SamplePosition(transform.position + away.normalized * 4f, out var hit, 2f, NavMesh.AllAreas))
                        agent.SetDestination(hit.position);
                }
            }
            else
            {
                if (agent.hasPath) agent.ResetPath();
                FaceTowards(playerPos);
            }
            if (dist > attackRange + 0.3f && attackTimer <= 0 && staggerTimer <= 0 && windup <= 0) StartAttack(playerPos, dist, danger);
        }
        else if (repathTimer <= 0)
        {
            agent.SetDestination(sees ? playerPos : lastKnown);
            repathTimer = 0.25f;
        }
    }

    void StartAttack(Vector3 playerPos, float dist, float danger)
    {
        attackTimer = stats.attackDelay;
        shooting = false;
        switch (Kind)
        {
            case ZombieKind.Ranged when dist > attackRange + 0.3f:
                // 허리 주머니에서 돌을 꺼내 새총에 메기며 겨눈다 (고무줄은 예비 동작 중에 당긴다)
                shooting = true;
                slingDrawn = false;
                slingSnap = -1;
                aimPoint = playerPos + Vector3.up * 1.2f;
                BeginWindup(RangedWindup);
                ProceduralAudio.PlayAt3D(ProceduralAudio.Rustle(), transform.position + Vector3.up * size, 0.7f, Random.Range(0.85f, 1.05f));
                break;
            case ZombieKind.Heavy:
                // 두 팔을 머리 위로 치켜든다
                BeginWindup(HeavyWindup);
                voice.pitch = Random.Range(0.55f, 0.65f);
                voice.PlayOneShot(ProceduralAudio.ZombieScream(), 0.9f);
                break;
            case ZombieKind.Raider:
                // 방망이를 머리 뒤로 젖힌다
                BeginWindup(RaiderWindup);
                voice.pitch = Random.Range(0.95f, 1.1f);
                voice.PlayOneShot(ProceduralAudio.Grunt(), 0.9f);
                break;
            default:
                // 일반·가죽 좀비(와 바짝 붙은 원거리 좀비)는 바로 물어뜯는다
                lunge = 1;
                var attackCry = ProceduralAudio.ZombieAttack();
                if (attackCry != null)
                {
                    voice.pitch = VoicePitch();
                    voice.PlayOneShot(attackCry, 0.9f);
                }
                ProceduralAudio.PlayAt(ProceduralAudio.Hit(), playerPos, 0.8f, 0.8f);
                UIManager.LastHitFrom = transform.position;
                GameManager.Instance.Damage(stats.damage * (1 + 0.3f * danger), DeathReason());
                break;
        }
    }

    void BeginWindup(float seconds)
    {
        windup = seconds;
        windupLength = seconds;
    }

    void ReleaseAttack(PlayerController player, Vector3 playerPos, float dist, float danger)
    {
        lunge = 1;
        float damage = stats.damage * (1 + 0.3f * danger);
        if (shooting)
        {
            Shoot(player, damage);
            return;
        }

        // 휘두르는 사이 피했으면 빗나간다
        var to = playerPos - transform.position;
        float height = Mathf.Abs(to.y);
        to.y = 0;
        // 벽·문이 사이에 있으면 닿지 않는다
        bool hit = dist < attackRange + 0.5f && height < 1.6f && Vector3.Angle(transform.forward, to) < 75f && ReachesPlayer(player);
        if (Kind == ZombieKind.Heavy)
        {
            // 내려찍기: 빗나가도 땅이 울린다
            ProceduralAudio.PlayAt3D(ProceduralAudio.Stomp(), transform.position + transform.forward * attackRange * 0.7f, 1f, 0.7f);
            if (dist < 7f) player.Shake(hit ? 0.6f : 0.25f);
        }
        else ProceduralAudio.PlayAt3D(ProceduralAudio.Swoosh(), transform.position + Vector3.up * 1.3f * size, 0.9f, 0.8f);
        if (!hit) return;
        ProceduralAudio.PlayAt(ProceduralAudio.Hit(), playerPos, 0.9f, Kind == ZombieKind.Heavy ? 0.6f : 0.9f);
        UIManager.LastHitFrom = transform.position;
        GameManager.Instance.Damage(damage, DeathReason());
    }

    // 새총 고무줄을 놓아 돌을 쏜다
    void Shoot(PlayerController player, float damage)
    {
        Vector3 from = SlingPoint();
        EnemyProjectile.Launch(from, ChestOf(player), ProjectileSpeed, damage, "새총으로 쏜 돌에 맞았다.", transform, ZombieLooks.Stone);
        ProceduralAudio.PlayAt3D(ProceduralAudio.SlingSnap(), from, 1f, Random.Range(0.92f, 1.08f));
        ReleaseSling();
    }

    // 고무줄을 놓는다: 쏘았거나, 당기다 맞아서 놓쳤을 때. 주머니가 튕겨 돌아간다
    void ReleaseSling()
    {
        if (stretchSound != null) stretchSound.Stop();
        stretchSound = null;
        slingDrawn = false;
        if (sling == null || !slingPosed) return;
        slingSnap = 0;
        snapDraw = slingDraw;
        snapAim = slingAim;
        snapHoldK = slingHoldK;
    }

    static Vector3 ChestOf(PlayerController player) => player.cameraRoot.position + Vector3.down * 0.4f; // 가슴 높이

    // 돌이 나가는 곳 (새총 두 갈래 사이)
    Vector3 SlingPoint() =>
        sling != null && sling.Fork != null ? sling.Muzzle : transform.position + Vector3.up * 1.55f * size + transform.forward * 0.5f;

    string DeathReason()
    {
        switch (Kind)
        {
            case ZombieKind.Leather: return "가죽 좀비에게 물어뜯겼다.";
            case ZombieKind.Ranged: return "원거리 좀비에게 할퀴어졌다.";
            case ZombieKind.Heavy: return "거대 좀비의 주먹에 짓눌렸다.";
            case ZombieKind.Raider: return "약탈자의 야구방망이에 맞았다.";
            default: return "좀비에게 물어뜯겼다.";
        }
    }

    bool CanSee(PlayerController p, float dist, float danger)
    {
        float range = balance.zombieSightRange * (1 + 0.5f * danger);
        if (IsRaider) range *= 1.5f;                       // 사람은 좀비보다 눈이 밝다
        else if (Kind == ZombieKind.Ranged) range *= 1.2f; // 멀리서 노린다
        if (p.IsCrouching) range *= IsRaider ? 0.75f : 0.55f;
        if (playerLight == null) playerLight = p.GetComponent<PlayerFlashlight>();
        if (playerLight != null && playerLight.IsOn) range *= balance.flashlightVisibility; // 손전등 불빛은 멀리서도 눈에 띈다
        if (dist > range) return false;

        Vector3 eye = transform.position + Vector3.up * 1.6f * size;
        Vector3 target = p.cameraRoot.position;
        // 이미 쫓는 중이면 시야를 넓게 본다 (옆으로 돌아도 놓치지 않게)
        float fov = state == State.Chase ? 100f : IsRaider ? 80f : 65f;
        if (dist > 2.5f && Vector3.Angle(transform.forward, target - eye) > fov) return false;
        if (Physics.Linecast(eye, target, out var hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == p.transform || hit.transform.IsChildOf(p.transform);
        return true;
    }

    // 가슴 높이에서 플레이어까지 곧게 뻗었을 때 벽·문이 가로막지 않는가 (공격이 벽을 뚫지 않게)
    bool ReachesPlayer(PlayerController p)
    {
        if (p == null) return false;
        Vector3 from = transform.position + Vector3.up * 1.2f * size;
        Vector3 to = p.cameraRoot.position;
        if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == p.transform || hit.transform.IsChildOf(p.transform);
        return true;
    }

    // 닫힌 문 앞에서 막히면 쾅쾅 두드리다 부수고 들어온다
    void TryBreakDoor(float dt)
    {
        // 가려는 길이 문에 막혀 멈춰 있을 때만 두드린다 (새총을 겨누느라 멈춘 원거리 좀비 등은 제외)
        bool stuck = agent.hasPath && windup <= 0 && agent.velocity.magnitude <= 0.4f
            && (agent.pathStatus != NavMeshPathStatus.PathComplete || agent.remainingDistance > 1.5f);
        if (!stuck) { doorTimer = 0; return; }
        float breakTime = IsRaider ? 1.6f : Kind == ZombieKind.Heavy ? 2f : 3.5f;
        foreach (var door in Door.All)
        {
            if (door.IsOpen || Vector3.Distance(door.transform.position, transform.position) > 2.2f * size) continue;
            float before = doorTimer;
            doorTimer += dt;
            if (Mathf.Floor(before / 0.8f) != Mathf.Floor(doorTimer / 0.8f))
                ProceduralAudio.PlayAt(ProceduralAudio.Bang(), door.transform.position, 0.9f, Random.Range(0.9f, 1.1f));
            if (doorTimer > breakTime) { door.ForceOpen(transform.position); doorTimer = 0; }
            return;
        }
    }

    void FaceTowards(Vector3 pos)
    {
        var dir = pos - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 360 * Time.deltaTime);
    }

    // ---------- 소리 ----------

    float VoicePitch()
    {
        switch (Kind)
        {
            case ZombieKind.Leather: return Random.Range(0.8f, 0.95f);
            case ZombieKind.Ranged: return Random.Range(1.1f, 1.3f);
            case ZombieKind.Heavy: return Random.Range(0.6f, 0.72f);
            default: return Random.Range(0.88f, 1.12f);
        }
    }

    // 신음. 가까울수록 자주 낸다. 약탈자는 신음 대신 쫓을 때만 가끔 고함친다
    void Vocalize(float dist)
    {
        bool chasing = state == State.Chase;
        if (IsRaider)
        {
            groanTimer = chasing ? Random.Range(3.5f, 7f) : Random.Range(8f, 15f);
            if (!chasing) return;
            voice.pitch = Random.Range(0.92f, 1.08f);
            voice.PlayOneShot(ProceduralAudio.RaiderShout(), 1f);
            return;
        }
        float near = 1 - Mathf.Clamp01((dist - 4f) / 20f);
        groanTimer = chasing ? Random.Range(1.2f, 2.6f) : Mathf.Lerp(Random.Range(5f, 11f), Random.Range(2.2f, 4.5f), near);
        voice.pitch = VoicePitch();
        voice.PlayOneShot(ProceduralAudio.ZombieGroan(), chasing ? 1f : 0.85f);
        mouth = 1;
    }

    void Scream()
    {
        if (IsRaider)
        {
            voice.pitch = Random.Range(0.95f, 1.08f);
            voice.PlayOneShot(ProceduralAudio.RaiderShout(), 1f);
            groanTimer = Random.Range(4f, 7f);
            return;
        }
        voice.pitch = VoicePitch();
        voice.PlayOneShot(ProceduralAudio.ZombieScream(), 1f);
        groanTimer = 2f;
        mouth = 1.3f;
    }

    // 발소리: 배회할 땐 작게, 쫓아올 땐 크게
    void Footsteps(float dt, float dist)
    {
        float speed = agent.velocity.magnitude;
        if (speed < 0.15f || dist > 30f) return;
        bool chasing = state == State.Chase;
        stepDistance += speed * dt;
        float stride = IsRaider ? (chasing ? 1.3f : 0.75f) : Kind == ZombieKind.Heavy ? 1f : chasing ? 1.1f : 0.65f;
        if (stepDistance < stride * size) return;
        stepDistance = 0;
        // 웅덩이를 밟으면 첨벙 (물결·물방울은 PuddleFx)
        if (PuddleFx.Step(transform.position, chasing ? 0.8f : 0.5f, false))
        {
            feet.pitch = Random.Range(0.9f, 1.1f);
            feet.PlayOneShot(ProceduralAudio.Splash(), chasing ? 0.9f : 0.5f);
            return;
        }
        if (IsRaider)
        {
            feet.pitch = Random.Range(0.8f, 0.95f);
            feet.PlayOneShot(ProceduralAudio.Footstep(), chasing ? 1f : 0.55f);
        }
        else if (Kind == ZombieKind.Heavy)
        {
            feet.pitch = Random.Range(0.75f, 0.9f);
            feet.PlayOneShot(ProceduralAudio.Stomp(), chasing ? 1f : 0.7f);
            if (dist < 8f) PlayerController.Instance.Shake(0.05f);
        }
        else
        {
            feet.pitch = Random.Range(0.85f, 1.15f);
            feet.PlayOneShot(ProceduralAudio.Shuffle(), chasing ? 0.9f : 0.45f);
        }
    }

    // ---------- 피해·죽음 ----------

    // direction이 0이면 불에 타는 것처럼 계속 받는 피해: 움찔하거나 밀리지 않는다
    public void TakeDamage(float amount, Vector3 direction)
    {
        if (state == State.Dead) return;
        health -= amount;
        bool dot = direction == Vector3.zero;
        if (!dot)
        {
            bool heavy = Kind == ZombieKind.Heavy;
            staggerTimer = heavy ? 0.2f : 0.5f;
            hitReact = heavy ? 0.5f : 1f;
            hitStun = balance.enemyHitStun;
            hitSlow = balance.enemyHitSlowSeconds;
            if (agent.isOnNavMesh) agent.velocity = Vector3.zero;
            if (!heavy)
            {
                // 맞으면 공격 준비가 끊긴다 (거대 좀비는 버틴다). 당기던 새총 고무줄은 놓쳐서 튕겨 돌아간다
                if (windup > 0 && shooting)
                {
                    if (slingDrawn) ProceduralAudio.PlayAt3D(ProceduralAudio.SlingSnap(), SlingPoint(), 0.5f, Random.Range(1.1f, 1.25f));
                    ReleaseSling();
                }
                windup = 0;
            }
            if (agent.isOnNavMesh) agent.Move(new Vector3(direction.x, 0, direction.z).normalized * (heavy ? 0.1f : 0.35f));
            if (flash != null) flash.Flash(HitFlash.HitColor);
            if (Kind == ZombieKind.Leather) LoseArmor(direction);
        }
        if (!dot || Random.value < 0.35f)
        {
            voice.pitch = IsRaider ? Random.Range(1f, 1.15f) : VoicePitch() * 1.1f;
            voice.PlayOneShot(IsRaider ? ProceduralAudio.Hurt() : ProceduralAudio.ZombieHurt(), dot ? 0.6f : 0.9f);
            mouth = 1;
        }
        if (PlayerController.Instance != null) lastKnown = PlayerController.Instance.transform.position;
        state = State.Chase;
        if (health <= 0) StartCoroutine(Die(dot ? -transform.forward : direction));
    }

    // 가죽 좀비: 체력이 줄 때마다 갑옷 조각(가슴·어깨·무릎·모자)이 하나씩 떨어져 나간다
    void LoseArmor(Vector3 direction)
    {
        if (rig == null || rig.Armor.Count == 0) return;
        float lost = 1 - Mathf.Clamp01(health / Mathf.Max(1, stats.health));
        int shouldLose = Mathf.Min(rig.Armor.Count, Mathf.FloorToInt(lost * (rig.Armor.Count + 1)));
        while (armorLost < shouldLose)
        {
            // 아직 붙어 있는 조각 중 아무거나
            var left = rig.Armor.FindAll(t => t != null && t.IsChildOf(transform));
            if (left.Count == 0) break;
            var piece = left[Random.Range(0, left.Count)];
            armorLost++;
            HitEffects.DropPiece(piece, direction);
            ProceduralAudio.PlayAt3D(ProceduralAudio.Rustle(), piece.position, 0.9f, 0.6f);
        }
    }

    IEnumerator Die(Vector3 direction)
    {
        state = State.Dead;
        GameManager.Instance.NoteKill(false);
        windup = 0;
        if (stretchSound != null) stretchSound.Stop();
        if (sling != null) sling.Relax(); // 당기던 고무줄을 놓고 쓰러진다
        agent.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        if (flash != null) flash.enabled = false;
        // 남은 신음·발소리를 끊고 마지막 소리만 낸다
        feet.Stop();
        voice.Stop();
        voice.pitch = IsRaider ? 0.85f : VoicePitch() * 0.75f;
        voice.PlayOneShot(IsRaider ? ProceduralAudio.Hurt() : ProceduralAudio.ZombieGroan(), 1f);
        if (jaw != null) jaw.localRotation = Quaternion.Euler(22, 0, 0);

        // 관절이 풀려 중력에 힘없이 무너진다 (맞은 방향으로 밀린다)
        var flat = new Vector3(direction.x, 0, direction.z).normalized;
        var impulse = (flat * 90f + Vector3.up * 12f) * Mathf.Max(0.6f, size);
        var parts = Ragdoll.Human(rig, impulse);
        DropLoot();

        // 몸이 바닥에 늘어진 뒤 그 자리에 피가 번진다
        float settle = 0;
        while (settle < 4f && !(settle > 0.9f && Ragdoll.Settled(parts)))
        {
            settle += Time.deltaTime;
            yield return null;
        }
        var torso = body != null ? body.position : transform.position;
        ProceduralAudio.PlayAt3D(ProceduralAudio.Bang(), torso, 0.35f, 0.6f);
        Gore.BloodPool(torso, Random.Range(0.55f, 0.8f) * Mathf.Max(0.7f, size));

        // 잠시 뒤 색이 어두워지며 땅으로 가라앉아 사라진다
        yield return Gore.FadeAway(gameObject, rig != null ? rig.Model : transform, rig != null ? rig.Renderers : new Renderer[0], parts, 7f, 5f, 3.5f);
    }

    // 일반·가죽·원거리 좀비는 쓰러진 자리에 밧줄·천·고철 중 하나를 떨어뜨린다
    static readonly string[] ZombieLoot = { "rope", "cloth", "metal" };

    void DropLoot()
    {
        if (Kind != ZombieKind.Normal && Kind != ZombieKind.Leather && Kind != ZombieKind.Ranged) return;
        var side = transform.right * Random.Range(-0.4f, 0.4f) + Vector3.up * 0.5f;
        PickupItem.Spawn(ZombieLoot[Random.Range(0, ZombieLoot.Length)], 1, transform.position + side);
    }

    // ---------- 움직임 ----------

    void Animate()
    {
        float dt = Time.deltaTime;
        float speed = agent.enabled ? agent.velocity.magnitude : 0;
        float wind = windup > 0 && windupLength > 0 ? 1 - windup / windupLength : 0; // 예비 동작 진행도 0→1
        lunge = Mathf.MoveTowards(lunge, 0, dt * 3);
        hitReact = Mathf.MoveTowards(hitReact, 0, dt * 5);
        mouth = Mathf.MoveTowards(mouth, 0, dt * 1.4f);
        if (Kind == ZombieKind.Raider) AnimateRaider(dt, speed, wind);
        else if (Kind == ZombieKind.Heavy) AnimateHeavy(dt, speed, wind);
        else AnimateZombie(dt, speed, wind);
        // 맞으면 몸통이 뒤로 젖혀지고 머리가 꺾인다
        if (hitReact > 0)
        {
            float k = Mathf.Sin(hitReact * Mathf.PI * 0.5f);
            if (body != null) body.localRotation *= Quaternion.Euler(-22 * k, 0, 8 * k);
            if (head != null) head.localRotation *= Quaternion.Euler(-25 * k, 0, -10 * k);
        }
        // 입: 신음·비명 때 크게 벌리고, 평소에도 반쯤 벌리고 있다 (약탈자는 다문다)
        if (jaw != null)
        {
            float open = IsRaider ? 0 : 7 + 22 * Mathf.Clamp01(mouth) + 18 * lunge + Mathf.Sin(animPhase * 1.7f) * 3;
            jaw.localRotation = Quaternion.Euler(open, 0, 0);
        }
    }

    // 무릎: 다리가 앞으로 넘어오는 동안 굽힌다 (limp가 크면 왼다리를 덜 굽혀 질질 끈다)
    void SetKnees(float amplitude, float limp = 0f)
    {
        float l = Mathf.Max(0, -Mathf.Cos(animPhase)), r = Mathf.Max(0, Mathf.Cos(animPhase));
        if (shinL != null) shinL.localRotation = Quaternion.Euler(4 + l * amplitude * (1 - limp), 0, 0);
        if (shinR != null) shinR.localRotation = Quaternion.Euler(4 + r * amplitude, 0, 0);
    }

    void SetElbows(float left, float right)
    {
        if (foreL != null) foreL.localRotation = Quaternion.Euler(left, 0, 0);
        if (foreR != null) foreR.localRotation = Quaternion.Euler(right, 0, 0);
    }

    void SetLegs(float amplitude)
    {
        if (legL != null) legL.localRotation = Quaternion.Euler(Mathf.Sin(animPhase) * amplitude, 0, 0);
        if (legR != null) legR.localRotation = Quaternion.Euler(-Mathf.Sin(animPhase) * amplitude, 0, 0);
    }

    // 다리를 질질 끌고 팔을 앞으로 뻗은 걸음걸이
    void AnimateZombie(float dt, float speed, float wind)
    {
        animPhase += dt * (1.2f + speed * 2.6f);
        float walk = Mathf.Clamp01(speed / 2f);
        SetLegs(28 * walk);
        SetKnees(40 * walk + 3, Kind == ZombieKind.Normal ? 0.6f : 0.2f);
        // 팔꿈치를 살짝 굽혀 할퀴려는 손
        SetElbows(-14 - lunge * 20 + Mathf.Sin(animPhase * 0.9f) * 6, -10 - lunge * 20 + Mathf.Sin(animPhase * 0.9f + 1.2f) * 6);
        float armBase = state == State.Chase ? -85f : -60f;
        float aL = armBase - lunge * 25 + Mathf.Sin(animPhase * 0.7f) * 8;
        float aR = armBase - lunge * 25 + Mathf.Sin(animPhase * 0.7f + 1.5f) * 8;
        float lean = 12 + lunge * 15;
        if (armL != null) armL.localRotation = Quaternion.Euler(aL, 0, -6);
        if (armR != null) armR.localRotation = Quaternion.Euler(aR, 0, 6);
        if (body != null) body.localRotation = Quaternion.Euler(lean + Mathf.Sin(animPhase * 2) * 2, Mathf.Sin(animPhase) * 6, Mathf.Sin(animPhase * 0.5f) * 5);
        if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(animPhase * 0.31f) * 12, Mathf.Sin(animPhase * 0.23f) * 20, 20 + Mathf.Sin(animPhase * 0.4f) * 10);
        if (sling != null) AnimateSling(dt, wind);
    }

    // 원거리 좀비의 새총: 몸을 틀어 왼손 새총으로 겨누고, 오른손으로 주머니를 집어 귀 옆까지 당겼다가 놓는다.
    // 놓으면 주머니가 튕겨 Y자 앞으로 넘어갔다 돌아오며 떨리고, 잠시 뒤 팔을 내린다
    void AnimateSling(float dt, float wind)
    {
        bool drawing = shooting && windup > 0;
        if (slingSnap >= 0)
        {
            slingSnap += dt;
            if (slingSnap > SlingHold + SlingLower) slingSnap = -1;
        }
        if (!drawing && slingSnap < 0)
        {
            if (slingPosed) sling.Relax();
            slingPosed = false;
            slingDraw = slingAim = slingHoldK = 0;
            return;
        }
        slingPosed = true;

        float holdK, draw;
        bool loaded;
        if (drawing)
        {
            slingAim = Smooth(wind / SlingGrab);
            holdK = slingAim;
            draw = Mathf.Clamp01((wind - SlingGrab * 0.85f) / (SlingGrab * 0.5f)); // 손이 주머니에 닿으면 주머니가 손을 따라간다
            loaded = wind > SlingGrab * 0.6f;
        }
        else
        {
            // 놓은 뒤: 주머니는 감쇠 진동, 팔은 잠깐 버티다 내린다
            float t = slingSnap;
            float lower = 1 - Smooth((t - SlingHold) / SlingLower);
            slingAim = snapAim * lower;
            holdK = snapHoldK * lower;
            draw = snapDraw * Mathf.Exp(-t * 18f) * Mathf.Cos(t * 38f);
            if (draw < 0) draw *= 0.5f;
            loaded = false;
        }
        float aim = slingAim;
        // 겨누는 동안 팔꿈치를 편다 (새총·주머니 위치가 곧은 팔 기준이다)
        float straight = Mathf.Max(aim, holdK);
        if (foreL != null) foreL.localRotation = Quaternion.Slerp(foreL.localRotation, Quaternion.identity, straight);
        if (foreR != null) foreR.localRotation = Quaternion.Slerp(foreR.localRotation, Quaternion.identity, straight);

        // 몸을 꼿꼿이 세우고 왼어깨를 앞으로 튼다. 머리는 튼 만큼 되돌려 과녁을 본다
        if (body != null) body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(3, 22, 0), aim);
        if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(4, -20, 6), aim);

        // 왼팔: 새총을 가슴 앞으로 모아 과녁을 겨눈다
        if (armL != null && body != null)
        {
            var to = aimPoint - armL.position;
            var dir = (to.sqrMagnitude > 0.01f ? to.normalized : transform.forward) * 0.92f + transform.right * 0.36f;
            armL.localRotation = Quaternion.Slerp(armL.localRotation, ArmPose(dir), aim);
        }

        // 오른팔: 주머니를 집으러 새총 쪽으로 뻗었다가(잡기) 귀 옆까지 당긴다
        Vector3 hand = sling.Rest;
        if (armR != null && body != null)
        {
            if (drawing)
            {
                float pull = Smooth((wind - SlingGrab) / (SlingFull - SlingGrab));
                var grab = sling.Rest - armR.position;
                // 오른쪽 귀 뒤 바깥 (고무줄이 머리를 뚫지 않고 옆을 지난다. 아래층을 겨눌 때도)
                var anchor = body.TransformDirection(new Vector3(0.55f, 0.35f, -0.75f));
                slingHold = ArmPose(Vector3.Slerp(grab.normalized, anchor.normalized, pull));
                slingHoldK = holdK;
            }
            armR.localRotation = Quaternion.Slerp(armR.localRotation, slingHold, holdK);
            hand = armR.TransformPoint(new Vector3(0, -0.7f, 0));
        }
        slingDraw = draw;
        sling.Pose(hand, draw, loaded);
    }

    // 팔(아래쪽 -y)이 worldDir을 가리키게 하는 몸 기준 회전. 팔 앞면(+z, 새총 갈래 쪽)은 되도록 위를 향한다
    Quaternion ArmPose(Vector3 worldDir)
    {
        var d = body.InverseTransformDirection(worldDir);
        if (d.sqrMagnitude < 1e-6f) return Quaternion.identity;
        d.Normalize();
        var up = Vector3.ProjectOnPlane(Vector3.up, d);
        if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.forward, d);
        return Quaternion.LookRotation(up.normalized, -d);
    }

    static float Smooth(float x) => Mathf.SmoothStep(0, 1, x);

    // 거대 좀비: 느릿하게 몸을 좌우로 흔들며 쿵쿵 걷고, 두 팔을 치켜들었다 내려찍는다
    void AnimateHeavy(float dt, float speed, float wind)
    {
        animPhase += dt * (0.9f + speed * 1.5f);
        float swing = Mathf.Clamp01(speed / 2.5f);
        SetLegs(24 * swing);
        SetKnees(30 * swing + 8);
        SetElbows(-20 - wind * 30 + lunge * 15, -25 - wind * 40 + lunge * 20);
        float sway = Mathf.Sin(animPhase);
        float armBase = state == State.Chase ? -40f : -20f;
        float arm = wind > 0 ? Mathf.Lerp(armBase, -170f, wind) : Mathf.Lerp(armBase, -60f, lunge);
        if (armL != null) armL.localRotation = Quaternion.Euler(arm + sway * 10 * swing, 0, -12);
        if (armR != null) armR.localRotation = Quaternion.Euler(arm - sway * 10 * swing, 0, 12);
        if (body != null) body.localRotation = Quaternion.Euler(20 - wind * 25 + lunge * 25 + Mathf.Abs(sway) * 4, sway * 8, sway * 10 * (0.4f + swing));
        if (head != null) head.localRotation = Quaternion.Euler(10 + Mathf.Sin(animPhase * 0.5f) * 6, Mathf.Sin(animPhase * 0.3f) * 10, Mathf.Sin(animPhase * 0.4f) * 8);
    }

    // 약탈자: 똑바로 서서 걷고, 오른손의 야구방망이를 머리 뒤로 젖혔다가 내려친다
    void AnimateRaider(float dt, float speed, float wind)
    {
        animPhase += dt * (1.5f + speed * 2.8f);
        float swing = Mathf.Clamp01(speed / 2f);
        float s = Mathf.Sin(animPhase);
        bool chasing = state == State.Chase;
        SetLegs(32 * swing);
        SetKnees(45 * swing + 3);
        SetElbows(-18 - s * 12 * swing, -15 - wind * 35 + lunge * 25);
        if (armL != null) armL.localRotation = Quaternion.Euler(-s * 28 * swing - 6, 0, -5);
        float rest = (chasing ? -35f : -12f) + s * 10 * swing;
        float bat;
        if (wind > 0) bat = Mathf.Lerp(rest, -205f, Mathf.Sin(wind * Mathf.PI * 0.5f));
        else if (lunge > 0.7f) bat = Mathf.Lerp(-50f, -205f, (lunge - 0.7f) / 0.3f);
        else bat = Mathf.Lerp(rest, -50f, lunge / 0.7f);
        if (armR != null) armR.localRotation = Quaternion.Euler(bat, 0, 8);
        if (body != null) body.localRotation = Quaternion.Euler(3 + (chasing ? 6 : 0) + lunge * 12 - wind * 6, -wind * 18 + lunge * 10 + s * 3 * swing, 0);
        if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(animPhase * 0.3f) * 3 - (chasing ? 6 : 0), Mathf.Sin(animPhase * 0.17f) * (chasing ? 4 : 25), 0);
    }
}
