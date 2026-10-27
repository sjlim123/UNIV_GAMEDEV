using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 좀비: 배회 → 소리를 들으면 확인하러 옴 → 보이면 추격·공격. 닫힌 문은 두드려 부순다.
[RequireComponent(typeof(NavMeshAgent))]
public class Zombie : MonoBehaviour, IDamageable
{
    // 속도·시야·피해 등은 GameBalance 에셋에서 조정한다
    public float attackRange = 1.6f;

    public Transform body, head, armL, armR, legL, legR;

    enum State { Wander, Investigate, Chase, Dead }

    State state;
    NavMeshAgent agent;
    AudioSource voice;
    GameBalance balance;
    PlayerFlashlight playerLight;
    float health, lostSightTimer, attackTimer, groanTimer, wanderTimer, lookTimer, staggerTimer, repathTimer, doorTimer, animPhase, lunge;
    Vector3 lastKnown;

    public bool IsDead => state == State.Dead;
    public bool IsChasing => state == State.Chase;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        balance = GameBalance.Data;
        health = balance.zombieHealth;
        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 1;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = 2;
        voice.maxDistance = 32;
        groanTimer = Random.Range(2f, 8f);
        animPhase = Random.value * 10;
    }

    void Update()
    {
        if (state == State.Dead) return;
        var player = PlayerController.Instance;
        if (player == null || GameManager.Instance.IsGameOver) { Animate(); return; }

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
        else if (state != State.Chase && player.Noise > 0 && dist < balance.zombieHearingRange * player.Noise)
        {
            state = State.Investigate;
            lastKnown = playerPos;
            lookTimer = 3f;
        }

        staggerTimer -= dt;
        attackTimer -= dt;
        repathTimer -= dt;
        agent.isStopped = staggerTimer > 0;

        switch (state)
        {
            case State.Chase:
                agent.speed = balance.zombieChaseSpeed * (1 + 0.3f * danger);
                if (!sees)
                {
                    lostSightTimer += dt;
                    if (lostSightTimer > 5f) { state = State.Investigate; lookTimer = 4f; }
                }
                if (repathTimer <= 0) { agent.SetDestination(sees ? playerPos : lastKnown); repathTimer = 0.2f; }
                if (dist < attackRange + 0.6f) FaceTowards(playerPos);
                if (dist < attackRange && attackTimer <= 0 && staggerTimer <= 0)
                {
                    attackTimer = balance.zombieAttackCooldown;
                    lunge = 1;
                    ProceduralAudio.PlayAt(ProceduralAudio.Hit(), playerPos, 0.8f, 0.8f);
                    GameManager.Instance.Damage(balance.zombieDamage * (1 + 0.3f * danger), "좀비에게 물어뜯겼다.");
                }
                TryBreakDoor(dt);
                break;

            case State.Investigate:
                agent.speed = balance.zombieWanderSpeed * 1.8f;
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

        groanTimer -= dt;
        if (groanTimer <= 0)
        {
            groanTimer = state == State.Chase ? Random.Range(1.5f, 3.5f) : Random.Range(5f, 12f);
            voice.pitch = Random.Range(0.8f, 1.15f);
            voice.PlayOneShot(ProceduralAudio.Groan(), state == State.Chase ? 1f : 0.7f);
        }

        Animate();
    }

    void Wander(float dt)
    {
        agent.speed = balance.zombieWanderSpeed;
        wanderTimer -= dt;
        if (wanderTimer <= 0 || (!agent.pathPending && agent.remainingDistance < 0.5f))
        {
            wanderTimer = Random.Range(5f, 12f);
            var target = transform.position + Random.insideUnitSphere * 10f;
            if (NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }
    }

    bool CanSee(PlayerController p, float dist, float danger)
    {
        float range = balance.zombieSightRange * (1 + 0.5f * danger);
        if (p.IsCrouching) range *= 0.55f;
        if (playerLight == null) playerLight = p.GetComponent<PlayerFlashlight>();
        if (playerLight != null && playerLight.IsOn) range *= balance.flashlightVisibility; // 손전등 불빛은 멀리서도 눈에 띈다
        if (dist > range) return false;

        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 target = p.cameraRoot.position;
        if (dist > 2.5f && Vector3.Angle(transform.forward, target - eye) > 65f) return false;
        if (Physics.Linecast(eye, target, out var hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == p.transform || hit.transform.IsChildOf(p.transform);
        return true;
    }

    // 닫힌 문 앞에서 막히면 쾅쾅 두드리다 부수고 들어온다
    void TryBreakDoor(float dt)
    {
        if (agent.velocity.magnitude > 0.4f) { doorTimer = 0; return; }
        foreach (var door in Door.All)
        {
            if (door.IsOpen || Vector3.Distance(door.transform.position, transform.position) > 2.2f) continue;
            float before = doorTimer;
            doorTimer += dt;
            if (Mathf.Floor(before / 0.8f) != Mathf.Floor(doorTimer / 0.8f))
                ProceduralAudio.PlayAt(ProceduralAudio.Bang(), door.transform.position, 0.9f, Random.Range(0.9f, 1.1f));
            if (doorTimer > 3.5f) { door.ForceOpen(transform.position); doorTimer = 0; }
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

    void Scream()
    {
        voice.pitch = Random.Range(0.85f, 1.1f);
        voice.PlayOneShot(ProceduralAudio.Scream(), 1f);
        groanTimer = 2f;
    }

    public void TakeDamage(float amount, Vector3 direction)
    {
        if (state == State.Dead) return;
        health -= amount;
        staggerTimer = 0.5f;
        if (agent.isOnNavMesh) agent.Move(new Vector3(direction.x, 0, direction.z).normalized * 0.35f);
        voice.pitch = Random.Range(0.9f, 1.2f);
        voice.PlayOneShot(ProceduralAudio.Hurt(), 0.9f);
        if (PlayerController.Instance != null) lastKnown = PlayerController.Instance.transform.position;
        state = State.Chase;
        if (health <= 0) StartCoroutine(Die(direction));
    }

    IEnumerator Die(Vector3 direction)
    {
        state = State.Dead;
        agent.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        voice.pitch = 0.7f;
        voice.PlayOneShot(ProceduralAudio.Groan(), 1f);

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
        ProceduralAudio.PlayAt(ProceduralAudio.Bang(), transform.position, 0.5f, 0.6f);
        Destroy(gameObject, 40f);
    }

    // 다리를 질질 끌고 팔을 앞으로 뻗은 걸음걸이
    void Animate()
    {
        float speed = agent.enabled ? agent.velocity.magnitude : 0;
        animPhase += Time.deltaTime * (1.2f + speed * 2.6f);
        float swing = Mathf.Clamp01(speed / 2f);
        lunge = Mathf.MoveTowards(lunge, 0, Time.deltaTime * 3);

        if (legL != null) legL.localRotation = Quaternion.Euler(Mathf.Sin(animPhase) * 28 * swing, 0, 0);
        if (legR != null) legR.localRotation = Quaternion.Euler(-Mathf.Sin(animPhase) * 28 * swing, 0, 0);
        float armBase = state == State.Chase ? -85f : -60f;
        if (armL != null) armL.localRotation = Quaternion.Euler(armBase - lunge * 25 + Mathf.Sin(animPhase * 0.7f) * 8, 0, -6);
        if (armR != null) armR.localRotation = Quaternion.Euler(armBase - lunge * 25 + Mathf.Sin(animPhase * 0.7f + 1.5f) * 8, 0, 6);
        if (body != null) body.localRotation = Quaternion.Euler(12 + lunge * 15 + Mathf.Sin(animPhase * 2) * 2, Mathf.Sin(animPhase) * 6, Mathf.Sin(animPhase * 0.5f) * 5);
        if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(animPhase * 0.31f) * 12, Mathf.Sin(animPhase * 0.23f) * 20, 20 + Mathf.Sin(animPhase * 0.4f) * 10);
    }
}
