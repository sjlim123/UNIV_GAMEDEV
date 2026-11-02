using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 좀비 종류. 모두 같은 좀비 프리팹을 Zombie.Configure로 바꿔 쓴다
public enum ZombieKind { Normal, Leather, Ranged, Heavy, Raider }

// 좀비: 배회 → 소리를 들으면 확인하러 옴 → 보이면 추격·공격. 닫힌 문은 두드려 부순다.
// 종류에 따라 수치(GameBalance)·겉모습·공격 방식이 다르다. Configure를 부르지 않으면 일반 좀비.
[RequireComponent(typeof(NavMeshAgent))]
public class Zombie : MonoBehaviour, IDamageable
{
    // 속도·시야·피해 등은 GameBalance 에셋에서 조정한다
    public float attackRange = 1.6f;

    public Transform body, head, armL, armR, legL, legR;

    // 원거리 좀비: 이보다 가까우면 뒷걸음질쳐 거리를 벌린다 (최대 사거리는 GameBalance.rangedAttackRange)
    public const float RangedKeepMin = 6f;
    // 원거리 좀비 독액이 날아가는 수평 속도(m/s)
    public const float ProjectileSpeed = 13f;
    // 공격 예비 동작 시간(초). 이 동안 때리면 공격이 끊긴다 (거대 좀비는 안 끊긴다)
    const float RangedWindup = 0.7f, HeavyWindup = 0.6f, RaiderWindup = 0.45f;

    enum State { Wander, Investigate, Chase, Dead }

    State state;
    NavMeshAgent agent;
    AudioSource voice, feet;
    GameBalance balance;
    EnemyStats stats;
    PlayerFlashlight playerLight;
    float health, lostSightTimer, attackTimer, groanTimer, wanderTimer, lookTimer, staggerTimer, repathTimer, doorTimer, animPhase, lunge;
    float windup, windupLength, stepDistance, baseAttackRange, size = 1f;
    bool throwing, backpedal, guarding;
    Vector3 lastKnown, guardPoint;

    public ZombieKind Kind { get; private set; }
    public bool IsDead => state == State.Dead;
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
        Kind = kind;
        stats = StatsFor(kind);
        health = stats.health;
        float scale = kind == ZombieKind.Heavy ? 1.35f : kind == ZombieKind.Leather ? 1.06f : 1f;
        transform.localScale = Vector3.one * scale * Random.Range(0.94f, 1.06f);
        size = transform.localScale.y;
        // 거대 좀비는 팔이 길고, 약탈자는 방망이만큼 더 닿는다
        attackRange = baseAttackRange * (kind == ZombieKind.Heavy ? 1.3f : kind == ZombieKind.Raider ? 1.2f : 1f);
        if (kind == ZombieKind.Raider) groanTimer = Random.Range(6f, 12f);
        ZombieLooks.Apply(this, kind);
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
        agent = GetComponent<NavMeshAgent>();
        balance = GameBalance.Data;
        stats = balance.zombie;
        health = stats.health;
        baseAttackRange = attackRange;
        // 목소리·발소리: 완전 3D, 방향이 또렷하고 15~20m에서도 잘 들린다
        voice = gameObject.AddComponent<AudioSource>();
        ProceduralAudio.Setup3D(voice, 36f, 64);
        feet = gameObject.AddComponent<AudioSource>();
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
            if (state != State.Chase) Scream();
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

        // 공격 예비 동작이 끝나면 휘두르기·내려찍기·던지기
        if (windup > 0)
        {
            windup -= dt;
            FaceTowards(playerPos);
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
                if (dist < attackRange && attackTimer <= 0 && staggerTimer <= 0 && windup <= 0) StartAttack(playerPos, dist, danger);
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

    // 원거리 좀비: 6m~사거리를 유지하며 독액을 던진다
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
        throwing = false;
        switch (Kind)
        {
            case ZombieKind.Ranged when dist > attackRange + 0.3f:
                // 독액을 끌어올리며 팔을 뒤로 젖힌다
                throwing = true;
                BeginWindup(RangedWindup);
                voice.pitch = Random.Range(0.9f, 1.1f);
                voice.PlayOneShot(ProceduralAudio.Retch(), 1f);
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
                ProceduralAudio.PlayAt(ProceduralAudio.Hit(), playerPos, 0.8f, 0.8f);
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
        if (throwing)
        {
            Throw(player, damage);
            return;
        }

        // 휘두르는 사이 피했으면 빗나간다
        var to = playerPos - transform.position;
        float height = Mathf.Abs(to.y);
        to.y = 0;
        bool hit = dist < attackRange + 0.5f && height < 1.6f && Vector3.Angle(transform.forward, to) < 75f;
        if (Kind == ZombieKind.Heavy)
        {
            // 내려찍기: 빗나가도 땅이 울린다
            ProceduralAudio.PlayAt3D(ProceduralAudio.Stomp(), transform.position + transform.forward * attackRange * 0.7f, 1f, 0.7f);
            if (dist < 7f) player.Shake(hit ? 0.6f : 0.25f);
        }
        else ProceduralAudio.PlayAt3D(ProceduralAudio.Swoosh(), transform.position + Vector3.up * 1.3f * size, 0.9f, 0.8f);
        if (!hit) return;
        ProceduralAudio.PlayAt(ProceduralAudio.Hit(), playerPos, 0.9f, Kind == ZombieKind.Heavy ? 0.6f : 0.9f);
        GameManager.Instance.Damage(damage, DeathReason());
    }

    void Throw(PlayerController player, float damage)
    {
        Vector3 from = transform.position + Vector3.up * 1.55f * size + transform.forward * 0.45f;
        Vector3 target = player.cameraRoot.position + Vector3.down * 0.4f; // 가슴 높이
        EnemyProjectile.Launch(from, target, ProjectileSpeed, damage, "원거리 좀비가 뱉은 독액에 맞았다.", transform, ZombieLooks.Spit);
        voice.pitch = Random.Range(0.9f, 1.1f);
        voice.PlayOneShot(ProceduralAudio.Spit(), 1f);
    }

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

    // 닫힌 문 앞에서 막히면 쾅쾅 두드리다 부수고 들어온다
    void TryBreakDoor(float dt)
    {
        // 가려는 길이 문에 막혀 멈춰 있을 때만 두드린다 (던지려고 멈춘 원거리 좀비 등은 제외)
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

    public void TakeDamage(float amount, Vector3 direction)
    {
        if (state == State.Dead) return;
        health -= amount;
        bool heavy = Kind == ZombieKind.Heavy;
        staggerTimer = heavy ? 0.2f : 0.5f;
        if (!heavy) windup = 0; // 맞으면 공격 준비가 끊긴다 (거대 좀비는 버틴다)
        if (agent.isOnNavMesh) agent.Move(new Vector3(direction.x, 0, direction.z).normalized * (heavy ? 0.1f : 0.35f));
        voice.pitch = IsRaider ? Random.Range(1f, 1.15f) : VoicePitch() * 1.1f;
        voice.PlayOneShot(ProceduralAudio.Hurt(), 0.9f);
        if (PlayerController.Instance != null) lastKnown = PlayerController.Instance.transform.position;
        state = State.Chase;
        if (health <= 0) StartCoroutine(Die(direction));
    }

    IEnumerator Die(Vector3 direction)
    {
        state = State.Dead;
        windup = 0;
        agent.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        // 남은 신음·발소리를 끊고 마지막 소리만 낸다
        feet.Stop();
        voice.Stop();
        voice.pitch = IsRaider ? 0.85f : VoicePitch() * 0.75f;
        voice.PlayOneShot(IsRaider ? ProceduralAudio.Hurt() : ProceduralAudio.ZombieGroan(), 1f);

        // 맞은 방향으로 쓰러진다
        var flat = new Vector3(direction.x, 0, direction.z).normalized;
        var axis = Vector3.Cross(Vector3.up, flat);
        if (axis.sqrMagnitude < 0.01f) axis = transform.right;
        var startRot = transform.rotation;
        var endRot = Quaternion.AngleAxis(88, axis) * startRot;
        for (float t = 0; t < 1; t += Time.deltaTime / 0.6f)
        {
            transform.rotation = Quaternion.Slerp(startRot, endRot, t * t);
            yield return null;
        }
        transform.rotation = endRot;
        ProceduralAudio.PlayAt3D(ProceduralAudio.Bang(), transform.position, 0.5f, 0.6f);
        Destroy(gameObject, 40f);
    }

    // ---------- 움직임 ----------

    void Animate()
    {
        float dt = Time.deltaTime;
        float speed = agent.enabled ? agent.velocity.magnitude : 0;
        float wind = windup > 0 && windupLength > 0 ? 1 - windup / windupLength : 0; // 예비 동작 진행도 0→1
        lunge = Mathf.MoveTowards(lunge, 0, dt * 3);
        if (Kind == ZombieKind.Raider) AnimateRaider(dt, speed, wind);
        else if (Kind == ZombieKind.Heavy) AnimateHeavy(dt, speed, wind);
        else AnimateZombie(dt, speed, wind);
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
        SetLegs(28 * Mathf.Clamp01(speed / 2f));
        float armBase = state == State.Chase ? -85f : -60f;
        float aL = armBase - lunge * 25 + Mathf.Sin(animPhase * 0.7f) * 8;
        float aR = armBase - lunge * 25 + Mathf.Sin(animPhase * 0.7f + 1.5f) * 8;
        float lean = 12 + lunge * 15;
        if (Kind == ZombieKind.Ranged && throwing && (wind > 0 || lunge > 0))
        {
            // 던지기: 오른팔을 뒤로 젖혔다가 앞으로 내던진다
            aR = wind > 0 ? Mathf.Lerp(armBase, -215f, wind) : Mathf.Lerp(armBase, -100f, lunge);
            lean = 12 - wind * 18 + lunge * 15;
        }
        if (armL != null) armL.localRotation = Quaternion.Euler(aL, 0, -6);
        if (armR != null) armR.localRotation = Quaternion.Euler(aR, 0, 6);
        if (body != null) body.localRotation = Quaternion.Euler(lean + Mathf.Sin(animPhase * 2) * 2, Mathf.Sin(animPhase) * 6, Mathf.Sin(animPhase * 0.5f) * 5);
        if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(animPhase * 0.31f) * 12, Mathf.Sin(animPhase * 0.23f) * 20, 20 + Mathf.Sin(animPhase * 0.4f) * 10);
    }

    // 거대 좀비: 느릿하게 몸을 좌우로 흔들며 쿵쿵 걷고, 두 팔을 치켜들었다 내려찍는다
    void AnimateHeavy(float dt, float speed, float wind)
    {
        animPhase += dt * (0.9f + speed * 1.5f);
        float swing = Mathf.Clamp01(speed / 2.5f);
        SetLegs(24 * swing);
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
