using UnityEngine;

// 원거리 좀비가 던지는 독액 덩어리. 포물선으로 날아가 플레이어에 맞으면 피해를 주고, 벽·바닥에 맞으면 터진다.
public class EnemyProjectile : MonoBehaviour
{
    // 중력 배율 (작을수록 곧게 날아간다)
    public const float GravityScale = 0.6f;
    // 이 시간(초)이 지나면 저절로 사라진다
    public const float Lifetime = 4f;
    public const float Radius = 0.11f;

    static readonly RaycastHit[] hits = new RaycastHit[16];

    Vector3 velocity;
    float damage, age;
    string deathReason;
    Transform owner;
    Material material;
    bool done;

    // from에서 target을 향해 포물선으로 던진다. speed = 수평 속도(m/s)
    public static EnemyProjectile Launch(Vector3 from, Vector3 target, float speed, float damage, string deathReason, Transform owner, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EnemyProjectile";
        go.transform.position = from;
        go.transform.localScale = Vector3.one * Radius * 2;
        go.GetComponent<Collider>().isTrigger = true;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        if (mat == null) mat = RuntimeVisuals.Mat(new Color(0.5f, 1f, 0.3f), 0.8f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(go.transform, false);
        glow.type = LightType.Point;
        glow.color = new Color(0.5f, 1f, 0.3f);
        glow.range = 3f;
        glow.intensity = 2f;
        glow.shadows = LightShadows.None;

        var p = go.AddComponent<EnemyProjectile>();
        p.damage = damage;
        p.deathReason = deathReason;
        p.owner = owner;
        p.material = mat;
        p.velocity = BallisticVelocity(from, target, speed);
        return p;
    }

    static Vector3 BallisticVelocity(Vector3 from, Vector3 to, float speed)
    {
        float g = -Physics.gravity.y * GravityScale;
        Vector3 d = to - from;
        Vector3 flat = new Vector3(d.x, 0, d.z);
        float t = Mathf.Max(0.2f, flat.magnitude / Mathf.Max(1f, speed));
        Vector3 v = flat / t;
        v.y = d.y / t + 0.5f * g * t;
        return v;
    }

    void Update()
    {
        if (done) return;
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        age += dt;
        if (age > Lifetime) { Destroy(gameObject); return; }

        velocity += Physics.gravity * GravityScale * dt;
        Vector3 step = velocity * dt;
        float len = step.magnitude;
        if (len < 1e-5f) return;

        // 빨리 날아가도 얇은 벽을 뚫지 않도록 이번 프레임에 지나갈 길을 미리 훑는다
        int n = Physics.SphereCastNonAlloc(transform.position, Radius, step / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
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
            Impact(h.collider, overlapped ? transform.position : h.point, overlapped ? -step / len : h.normal);
            return;
        }
        transform.position += step;
    }

    // 플레이어가 날아오는 독액에 직접 걸어 들어온 경우
    void OnTriggerEnter(Collider other)
    {
        if (done || Ignore(other)) return;
        Impact(other, transform.position, -velocity.normalized);
    }

    bool Ignore(Collider c) =>
        c == null || c.isTrigger || c.transform == transform || (owner != null && c.transform.IsChildOf(owner));

    void Impact(Collider c, Vector3 point, Vector3 normal)
    {
        done = true;
        if (c.GetComponentInParent<PlayerController>() != null)
        {
            GameManager.Instance.Damage(damage, deathReason);
            ProceduralAudio.Play2D(ProceduralAudio.Splat(), 0.9f);
        }
        else
        {
            ProceduralAudio.PlayAt3D(ProceduralAudio.Splat(), point, 1f, Random.Range(0.9f, 1.1f));
            if (c.GetComponentInParent<IDamageable>() == null) LeaveSplat(point, normal);
        }
        Destroy(gameObject);
    }

    // 벽·바닥에 남는 독액 자국 (잠시 뒤 사라진다)
    void LeaveSplat(Vector3 point, Vector3 normal)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var col = go.GetComponent<Collider>();
        col.enabled = false;
        Destroy(col);
        go.name = "AcidSplat";
        go.transform.position = point + normal * 0.01f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        go.transform.localScale = new Vector3(0.45f, 0.03f, 0.45f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go, 6f);
    }
}
