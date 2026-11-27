using UnityEngine;

// 새총으로 쏘는 돌멩이 (원거리 좀비와 플레이어가 함께 쓴다). 낮고 빠른 포물선으로 날아가
// 좀비가 쏜 돌은 플레이어에, 플레이어가 쏜 돌은 좀비·들개에 맞으면 피해를 주고,
// 벽·바닥에 맞으면 맞은 재질의 가루·부스러기를 튀기며 딱 소리와 함께 튕겨 떨어졌다가 잠시 뒤 사라진다.
public class EnemyProjectile : MonoBehaviour
{
    // 중력 배율 (작을수록 곧게 날아간다)
    public const float GravityScale = 0.45f;
    // 이 시간(초)이 지나면 저절로 사라진다
    public const float Lifetime = 4f;
    public const float Radius = 0.06f;
    // 떨어진 돌이 바닥에 남아 있는 시간(초)
    const float RestTime = 1.5f;

    static readonly RaycastHit[] hits = new RaycastHit[16];
    static Mesh sphereMesh;

    Vector3 velocity, spin;
    float damage, age;
    string deathReason;
    Transform owner, stone;
    bool spent, resting; // spent: 한 번 부딪힌 돌 (더는 피해를 주지 않는다)
    bool fromPlayer;     // 플레이어가 쏜 돌 (좀비·들개에게 피해를 준다)
    int bounces;

    // from에서 target을 향해 포물선으로 쏜다. speed = 수평 속도(m/s), mat = 돌 재질 (null이면 회색 돌)
    public static EnemyProjectile Launch(Vector3 from, Vector3 target, float speed, float damage, string deathReason, Transform owner, Material mat)
    {
        var p = Create(from, damage, owner, mat);
        p.deathReason = deathReason;
        p.velocity = BallisticVelocity(from, target, speed);
        return p;
    }

    // 플레이어의 새총: from에서 velocity로 쏜다 (조준한 방향으로 곧게 날아가다 조금씩 떨어진다)
    public static EnemyProjectile LaunchFromPlayer(Vector3 from, Vector3 velocity, float damage, Transform player)
    {
        var p = Create(from, damage, player, null);
        p.fromPlayer = true;
        p.velocity = velocity;
        return p;
    }

    static EnemyProjectile Create(Vector3 from, float damage, Transform owner, Material mat)
    {
        var go = new GameObject("SlingStone");
        go.transform.position = from;
        var col = go.AddComponent<SphereCollider>();
        col.radius = Radius;
        col.isTrigger = true;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        if (mat == null) mat = RuntimeVisuals.Mat(new Color(0.42f, 0.41f, 0.38f), 0.15f);
        // 울퉁불퉁한 돌 (찌그러진 공)
        var stone = Visual("Stone", go.transform, mat, new Vector3(Random.Range(0.085f, 0.1f), Random.Range(0.065f, 0.08f), Random.Range(0.075f, 0.09f)));
        stone.localRotation = Random.rotation;

        var p = go.AddComponent<EnemyProjectile>();
        p.damage = damage;
        p.owner = owner;
        p.stone = stone;
        p.spin = Random.onUnitSphere * Random.Range(500f, 900f); // 날아가며 핑글핑글 돈다
        return p;
    }

    static Vector3 BallisticVelocity(Vector3 from, Vector3 to, float speed)
    {
        float g = -Physics.gravity.y * GravityScale;
        Vector3 d = to - from;
        Vector3 flat = new Vector3(d.x, 0, d.z);
        float t = Mathf.Max(0.15f, flat.magnitude / Mathf.Max(1f, speed));
        Vector3 v = flat / t;
        v.y = d.y / t + 0.5f * g * t;
        return v;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        age += dt;
        if (resting) return;
        if (age > Lifetime) { Destroy(gameObject); return; }

        // 날아가는 동안은 중력을 약하게 (낮고 곧은 궤적), 한 번 부딪힌 뒤에는 그냥 떨어진다
        velocity += Physics.gravity * (spent ? 1f : GravityScale) * dt;
        if (stone != null) stone.Rotate(spin * dt, Space.Self);
        Vector3 step = velocity * dt;
        float len = step.magnitude;
        if (len < 1e-5f) return;

        // 빨리 날아가도 얇은 벽을 뚫지 않도록 이번 프레임에 지나갈 길을 미리 훑는다
        Vector3 dir = step / len;
        int n = Physics.SphereCastNonAlloc(transform.position, Radius, dir, hits, len, ~0, QueryTriggerInteraction.Ignore);
        int best = -1;
        for (int i = 0; i < n; i++)
        {
            if (Ignore(hits[i].collider)) continue;
            if (best < 0 || hits[i].distance < hits[best].distance) best = i;
        }
        if (best >= 0)
        {
            var h = hits[best];
            bool overlapped = h.distance <= 0f; // 처음부터 겹쳐 있던 물체는 point가 비어 있다
            Vector3 normal = overlapped ? -dir : h.normal;
            Vector3 point = overlapped ? transform.position : h.point;
            // 부딪힌 자리에 붙인 뒤 살짝 띄워 다음 프레임에 다시 겹치지 않게 한다
            transform.position += dir * h.distance + normal * 0.01f;
            if (spent) Bounce(h.collider, point, normal);
            else Impact(h.collider, point, normal);
            return;
        }
        transform.position += step;
    }

    // 플레이어가 날아오는 돌에 직접 걸어 들어온 경우
    void OnTriggerEnter(Collider other)
    {
        if (spent || Ignore(other)) return;
        Impact(other, transform.position, velocity.sqrMagnitude > 1e-6f ? -velocity.normalized : Vector3.up);
    }

    // 한 번 부딪힌 돌은 플레이어·좀비 몸을 그냥 지나 떨어진다 (몸에 끼어 앞뒤로 튕기다 공중에 멈추지 않게)
    bool Ignore(Collider c) =>
        c == null || c.isTrigger || c.transform == transform || (owner != null && c.transform.IsChildOf(owner))
        || (spent && (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<IDamageable>() != null));

    void Impact(Collider c, Vector3 point, Vector3 normal)
    {
        spent = true;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var target = fromPlayer ? c.GetComponentInParent<IDamageable>() : null;
        if ((target is Zombie deadZombie && deadZombie.IsDead) || (target is Animal deadAnimal && deadAnimal.IsDead))
        {
            // 쓰러진 몸: 피해·맞힘 소리(화면 전체에 들리는 2D) 없이 살에 맞은 둔한 소리만 내고 툭 떨어진다
            ProceduralAudio.PlayAt3D(ProceduralAudio.FleshHit(), point, 0.5f, Random.Range(0.95f, 1.1f));
            velocity *= -0.12f;
            return;
        }
        if (target != null)
        {
            target.TakeDamage(damage, velocity.sqrMagnitude > 1e-6f ? velocity.normalized : -normal);
            ProceduralAudio.Play2D(ProceduralAudio.Hit(), 0.6f, 1.3f);
            ProceduralAudio.PlayAt3D(ProceduralAudio.StoneHit(), point, 0.6f, 0.8f);
            velocity *= -0.12f;
            return;
        }
        if (!fromPlayer && c.GetComponentInParent<PlayerController>() != null)
        {
            GameManager.Instance.Damage(damage, deathReason);
            ProceduralAudio.Play2D(ProceduralAudio.Hit(), 0.85f, 1.25f);
            ProceduralAudio.Play2D(ProceduralAudio.StoneHit(), 0.35f, 0.7f);
            // 몸에 맞고 툭 떨어진다
            velocity *= -0.12f;
            return;
        }
        // 웅덩이에 떨어지면 물결·물방울과 첨벙 소리, 아니면 딱 소리와 맞은 재질의 가루
        bool splashed = c.GetComponentInParent<IDamageable>() == null && SurfaceDust(c, point, normal, false);
        if (!splashed) ProceduralAudio.PlayAt3D(ProceduralAudio.StoneHit(), point, 1f, Random.Range(0.9f, 1.15f));
        Bounce(null, point, normal);
    }

    // 벽·바닥에 맞은 자리: 맞은 재질에 맞는 가루·부스러기 (콘크리트 먼지와 돌 부스러기, 나무 톱밥, 쇠의 녹 가루, 흙 — Vfx.Impact). 웅덩이면 물결과 낮은 물방울을 내고 true.
    // 세기는 빠르기로 정한다: 플레이어 새총(26 m/s)은 부스러기까지, 좀비가 쏜 돌과 다시 튕길 때는 가루만 조금 (좀비 여럿이 쏘아도 화면이 어지럽지 않게).
    // 자국은 남기지 않는다 (돌이 많이 날아다녀서)
    bool SurfaceDust(Collider c, Vector3 point, Vector3 normal, bool rebound)
    {
        if (c == null) return false;
        float speed = velocity.magnitude;
        var dir = speed > 0.01f ? velocity / speed : -normal;
        // 맞은 면을 그 물체에 다시 쏴서 얻는다: 벽에 바짝 붙어 쏘면 돌이 처음부터 벽 안에 있어 point가 벽 속이고 normal이 카메라를 향한다.
        // 면을 못 찾으면 (벽 속에서 시작했고 뒤에서도 안 닿으면) 가루를 내지 않는다
        if (!c.Raycast(new Ray(point - dir * 0.4f, dir), out var face, 0.8f)) return false;
        point = face.point;
        normal = face.normal;
        if (!ImpactSurface.Visible(c, point)) return false; // 렌더러 없는 투명 벽이면 허공에 먼지가 생기지 않게
        var kind = ImpactSurface.Classify(c, point, normal, face.triangleIndex);
        if (kind == Vfx.ImpactKind.Water)
            return PuddleFx.Step(point, rebound ? 0.15f : 0.35f, true); // 발로 디딘 것보다 작게, 낮게 튀는 물방울
        float strength = rebound ? Mathf.Clamp(speed / 26f, 0.15f, 0.3f)
            : fromPlayer ? Mathf.Clamp(speed / 26f * 0.85f, 0.3f, 0.9f) : 0.45f;
        // 쇠·나무는 가루가 적어 작은 돌로는 거의 안 보이므로 조금 세게 (차·판자에 앉은 먼지와 녹·톱밥이 튄다)
        if (!rebound && fromPlayer && (kind == Vfx.ImpactKind.Metal || kind == Vfx.ImpactKind.Wood)) strength *= 1.3f;
        var along = Vector3.ProjectOnPlane(dir, normal);
        if (along.sqrMagnitude < 1e-4f) along = Vector3.Cross(normal, Vector3.right);
        if (along.sqrMagnitude < 1e-4f) along = Vector3.forward;
        Vfx.Impact(kind, point, normal, dir, along.normalized, strength, false, false);
        return false;
    }

    // 튕긴다 (탄성 0.3, 마찰 0.5). 바닥에서 느려지면 멈췄다가 잠시 뒤 사라진다
    void Bounce(Collider c, Vector3 point, Vector3 normal)
    {
        bounces++;
        float into = Vector3.Dot(velocity, normal);
        if (into < 0)
        {
            // 두 번째부터 튕길 때는 작게 달각거린다 (웅덩이 안이면 달각 소리 대신 첨벙)
            bool wet = normal.y > 0.6f && PuddleFx.Inside(point);
            if (bounces > 1 && -into > 1.5f && !wet)
                ProceduralAudio.PlayAt3D(ProceduralAudio.StoneHit(), transform.position, Mathf.Clamp01(-into / 8f) * 0.6f, Random.Range(1.2f, 1.4f));
            // 세게 다시 떨어지면 그 자리에서도 가루(웅덩이면 작은 물방울)가 조금 인다
            if (bounces > 1 && -into > 2.5f) SurfaceDust(c, point, normal, true);
            velocity = (velocity - into * normal) * 0.5f - into * normal * 0.3f;
        }
        if (normal.y > 0.6f && velocity.magnitude < 1.2f)
        {
            resting = true;
            if (stone != null) stone.position = transform.position - normal * (Radius - 0.035f); // 바닥에 닿게 내려놓는다
            Destroy(gameObject, RestTime);
        }
        else if (bounces > 5) Destroy(gameObject);
    }

    // 콜라이더 없는 공 모양 (돌). CreatePrimitive는 콜라이더가 프레임 끝까지 남아서 메시만 빌려 쓴다
    static Transform Visual(string name, Transform parent, Material mat, Vector3 scale)
    {
        if (sphereMesh == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tmp.SetActive(false);
            sphereMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }
}
