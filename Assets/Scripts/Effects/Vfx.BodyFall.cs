using UnityEngine;

// 쓰러진 몸이 바닥에 닿은 자리의 먼지 (BodyImpact가 부른다): 몸 아래에서 바닥을 따라 낮게 밀려 나가는 먼지 고리와, 천천히 떠오르는 옅은 먼지.
// 바닥 재질로 색을 고른다 (콘크리트 회색, 벽돌 붉은 가루, 흙, 모래, 나무 톱밥, 녹). 천·풀은 먼지가 거의 없다. 웅덩이는 PuddleFx가 맡는다
public static partial class Vfx
{
    // point: 몸이 닿은 바닥, normal: 바닥 면, radius: 몸 크기 (사람 몸통 0.45, 들개 0.3), strength: 떨어진 세기 0~1
    public static void BodyDust(Vfx.ImpactKind ground, Vector3 point, Vector3 normal, float radius, float strength)
    {
        if (ground == ImpactKind.Water || ground == ImpactKind.Flesh || ground == ImpactKind.Glass) return;
        Init();
        var n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        float s = Mathf.Clamp01(strength);
        Color tint = ground == ImpactKind.Brick ? BrickDust : ground == ImpactKind.Dirt ? EarthDust : ground == ImpactKind.Wood ? SawDust
            : ground == ImpactKind.Metal ? RustDust : ground == ImpactKind.Soft ? SandDust : ConcreteDust;
        float amount = ground == ImpactKind.Soft ? 0.35f : ground == ImpactKind.Metal || ground == ImpactKind.Wood ? 0.6f : 1f;
        // 바닥 위 두 방향 (몸 아래에서 사방으로 밀려 나간다)
        var a = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
        var b = Vector3.Cross(n, a);
        var p = point + n * 0.04f;

        int ring = Count(16 * amount * (0.5f + s));
        for (int i = 0; i < ring; i++)
        {
            float ang = Random.value * Mathf.PI * 2f;
            var dir = a * Mathf.Cos(ang) + b * Mathf.Sin(ang);
            Puff(p + dir * Random.Range(0.05f, radius * 0.7f), dir * Random.Range(0.6f, 1.8f) * (0.5f + s) + n * Random.Range(0.02f, 0.18f),
                Random.Range(0.9f, 1.8f), Random.Range(0.12f, 0.26f) * (0.8f + 0.4f * s), tint * ImpactDustDim, Random.Range(0.4f, 0.62f));
        }
        int rise = Count(6 * amount * (0.5f + s));
        for (int i = 0; i < rise; i++)
        {
            var off = (a * Random.Range(-1f, 1f) + b * Random.Range(-1f, 1f)) * radius * 0.5f;
            Puff(p + off, n * Random.Range(0.12f, 0.4f) * (0.5f + s) + Random.insideUnitSphere * 0.08f,
                Random.Range(1.6f, 2.8f), Random.Range(0.22f, 0.4f), tint * ImpactDustDim, Random.Range(0.2f, 0.34f));
        }
        // 흙·콘크리트·벽돌은 작은 알갱이가 몇 개 튄다
        if (ground == ImpactKind.Dirt || ground == ImpactKind.Concrete || ground == ImpactKind.Brick)
        {
            int grit = Count(8 * s);
            for (int i = 0; i < grit; i++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                var dir = a * Mathf.Cos(ang) + b * Mathf.Sin(ang);
                Chip(p + dir * Random.Range(0.05f, radius * 0.5f), dir * Random.Range(0.5f, 1.6f) + n * Random.Range(0.6f, 1.6f), Random.Range(0.006f, 0.014f));
            }
        }
    }
}
