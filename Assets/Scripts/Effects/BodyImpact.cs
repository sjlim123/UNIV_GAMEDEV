using System.Collections.Generic;
using UnityEngine;

// 쓰러진 몸(Ragdoll)의 마디가 바닥·벽에 닿는 순간: 몸통이면 묵직한 쿵 소리와 바닥 재질의 먼지(웅덩이면 물보라와 물결), 팔·다리·머리면 작은 툭 소리.
// Zombie.Die·Animal.Die가 Ragdoll을 만든 뒤 Attach로 마디마다 붙인다. 한 몸에서 몸통 소리는 두 번, 팔다리 소리는 네 번까지 (튕기며 여러 번 닿아도 시끄럽지 않게),
// 쓰러지고 5초가 지나면(시체가 가라앉기 전) 더 내지 않는다
public class BodyImpact : MonoBehaviour
{
    const float ListenSeconds = 5f;

    class Fall
    {
        public readonly HashSet<Collider> own = new HashSet<Collider>();
        public float start, lastTorso = -9f, lastLimb = -9f, size = 1f;
        public int torsoHits, limbHits;
        public bool dog;
    }

    Fall fall;
    bool torso;
    Rigidbody body;

    // parts: Ragdoll.Human/Dog가 돌려준 강체 (첫 번째가 몸통). size: 몸 크기 배율 (덩치 큰 좀비는 소리가 낮고 먼지가 넓다)
    public static void Attach(List<Rigidbody> parts, bool dog, float size)
    {
        if (parts == null || parts.Count == 0) return;
        var f = new Fall { start = Time.time, dog = dog, size = Mathf.Max(0.5f, size) };
        foreach (var rb in parts)
            if (rb != null)
                foreach (var c in rb.GetComponents<Collider>()) f.own.Add(c);
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] == null) continue;
            var bi = parts[i].gameObject.AddComponent<BodyImpact>();
            bi.fall = f;
            bi.torso = i == 0;
            bi.body = parts[i];
        }
    }

    static bool IsDead(IDamageable d) => (d is Zombie z && z.IsDead) || (d is Animal a && a.IsDead);

    // 몸이 다 멈췄을 때 (Zombie.Die·Animal.Die): 몸통이 한 번도 소리를 내지 못했으면 (천천히 미끄러져 눕거나 처음부터 바닥에 붙어 있었으면)
    // 몸통 아래 바닥을 보고 작게 한 번 낸다. 쓰러지는 소리가 아예 빠지지 않게
    public static void Settled(List<Rigidbody> parts)
    {
        if (parts == null || parts.Count == 0 || parts[0] == null) return;
        var bi = parts[0].GetComponent<BodyImpact>();
        if (bi == null || bi.fall == null || bi.fall.torsoHits > 0) return;
        bi.fall.torsoHits = 2; // 이 뒤로는 더 내지 않는다
        var pos = parts[0].worldCenterOfMass;
        var ground = Vfx.ImpactKind.Concrete;
        if (Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out var h, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !bi.fall.own.Contains(h.collider))
            ground = ImpactSurface.Classify(h.collider, h.point, h.normal, h.triangleIndex);
        ProceduralAudio.PlayAt3D(EffectAudio.BodyFall(ground, bi.fall.dog), pos, 0.4f, Random.Range(0.95f, 1.05f) * (bi.fall.size > 1.2f ? 0.88f : 1f));
    }

    void OnCollisionEnter(Collision hit)
    {
        if (fall == null) return;
        float now = Time.time;
        if (now - fall.start > ListenSeconds) { enabled = false; return; }
        var other = hit.collider;
        if (other == null || other.isTrigger || fall.own.Contains(other)) return;
        // 산 적·플레이어에 걸친 것과, 함께 떨어지는 가벼운 물건(가죽 좀비가 떨군 갑옷 조각, 떨어뜨린 아이템)은 바닥이 아니다.
        // 먼저 쓰러진 시체 위로 넘어지면 둔한 털썩 소리만 낸다 (먼지는 없다)
        var victim = other.GetComponentInParent<IDamageable>();
        bool onCorpse = victim != null && IsDead(victim);
        if ((victim != null && !onCorpse) || other.GetComponentInParent<PlayerController>() != null) return;
        var otherBody = other.attachedRigidbody;
        if (!onCorpse && otherBody != null && !otherBody.isKinematic && otherBody.mass < 5f) return;
        float speed = hit.relativeVelocity.magnitude;
        if (hit.contactCount == 0) return;
        var contact = hit.GetContact(0);
        var point = contact.point;
        var normal = contact.normal;
        if (body != null && Vector3.Dot(normal, body.worldCenterOfMass - point) < 0) normal = -normal; // 면이 몸 쪽을 보게

        if (torso)
        {
            if (speed < 1f || fall.torsoHits >= 2 || now - fall.lastTorso < 0.3f) return;
            bool first = fall.torsoHits == 0;
            fall.torsoHits++;
            fall.lastTorso = now;
            var ground = onCorpse ? Vfx.ImpactKind.Soft : ImpactSurface.Classify(other, point, normal);
            // 들개는 다리가 짧아 몸통이 거의 바로 낮게 닿는다 (빠르기가 작아도 털썩 소리는 들리게)
            float strength = Mathf.Clamp(speed / 3.5f, fall.dog ? 0.6f : 0.35f, 1f) * (first ? 1f : 0.55f);
            float pitch = Random.Range(0.95f, 1.05f) * (fall.size > 1.2f ? 0.88f : 1f);
            ProceduralAudio.PlayAt3D(EffectAudio.BodyFall(ground, fall.dog), point, strength, pitch);
            if (onCorpse || !ImpactSurface.Visible(other, point)) return;
            float radius = (fall.dog ? 0.3f : 0.45f) * fall.size;
            if (ground == Vfx.ImpactKind.Water) PuddleFx.Step(point, first ? 0.7f : 0.35f, false);
            else if (normal.y > 0.5f) Vfx.BodyDust(ground, point, normal, radius, strength);
            return;
        }
        if (speed < 1.6f || fall.limbHits >= 4 || now - fall.lastLimb < 0.1f) return;
        fall.limbHits++;
        fall.lastLimb = now;
        var clip = EffectAudio.LimbThud();
        if (clip != null) ProceduralAudio.PlayAt3D(clip, point, Mathf.Clamp(speed / 5f, 0.2f, 0.55f) * (fall.dog ? 0.7f : 1f), Random.Range(0.95f, 1.1f));
    }
}
