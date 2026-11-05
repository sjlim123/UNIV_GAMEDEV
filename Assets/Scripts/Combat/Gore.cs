using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 죽은 적의 뒷처리: 쓰러진 자리 바닥의 핏자국(젖은 피처럼 빛을 반쯤 반사한다), 몸이 어두워지며 땅으로 가라앉아 사라지기.
// 핏자국은 가장자리가 들쭉날쭉한 얼룩 메시(투명도 없이 불투명한 URP Lit 재질)라 어떤 빌드에서도 같은 셰이더로 그려진다
public static class Gore
{
    static Material bloodMat;
    static readonly Dictionary<int, Mesh> blobs = new Dictionary<int, Mesh>();

    // 젖은 피: 검붉고 매끈함 0.5 (빛 반사 50%)
    public static Material BloodMaterial => bloodMat != null ? bloodMat : bloodMat = RuntimeVisuals.Mat(new Color(0.27f, 0.02f, 0.015f), 0.5f, 0f);

    // 가장자리가 들쭉날쭉한 납작한 얼룩 (반지름 1, xz 평면, 위를 본다). seed마다 모양이 다르다
    public static Mesh Blob(int seed)
    {
        if (blobs.TryGetValue(seed, out var cached) && cached != null) return cached;
        var rng = new System.Random(seed);
        const int N = 40;
        var verts = new Vector3[N + 1];
        var uvs = new Vector2[N + 1];
        var norms = new Vector3[N + 1];
        var tris = new int[N * 3];
        verts[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);
        norms[0] = Vector3.up;
        float o1 = (float)rng.NextDouble() * 10f, o2 = (float)rng.NextDouble() * 10f;
        for (int i = 0; i < N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f;
            // 큰 굴곡 몇 개 + 잔 굴곡 (주기가 맞아떨어지게 각도의 정수배로 잰다)
            float r = 0.72f + 0.17f * Mathf.Sin(a * 3f + o1) + 0.08f * Mathf.Sin(a * 7f + o2) + 0.06f * Mathf.Sin(a * 11f + o1 * 2f);
            r += 0.05f * ((float)rng.NextDouble() - 0.5f);
            verts[i + 1] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            uvs[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * r * 0.5f, 0.5f + Mathf.Sin(a) * r * 0.5f);
            norms[i + 1] = Vector3.up;
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 1 == N ? 1 : i + 2;
            tris[i * 3 + 2] = i + 1;
        }
        var m = new Mesh { name = "blob" + seed };
        m.vertices = verts;
        m.uv = uvs;
        m.normals = norms;
        m.triangles = tris;
        m.RecalculateBounds();
        blobs[seed] = m;
        return m;
    }

    // 바닥의 핏자국: center 아래 바닥을 찾아 놓고 seconds초에 걸쳐 radius까지 번진다. 주위에 작은 방울 몇 개
    public static void BloodPool(Vector3 center, float radius, float seconds = 2.5f)
    {
        if (!Physics.Raycast(center + Vector3.up * 0.6f, Vector3.down, out var hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
        var root = new GameObject("BloodPool");
        root.transform.position = hit.point + hit.normal * 0.006f;
        root.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        root.layer = Ragdoll.Layer;
        int seed = Random.Range(0, 100000);
        var main = Splat(root.transform, Vector3.zero, seed, radius, 0.08f, 0f, seconds);
        main.name = "Main";
        int drops = Random.Range(3, 7);
        for (int i = 0; i < drops; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), d = radius * Random.Range(0.75f, 1.45f);
            Splat(root.transform, new Vector3(Mathf.Cos(a) * d, 0.0005f * (i + 1), Mathf.Sin(a) * d), seed + 1 + i, radius * Random.Range(0.12f, 0.32f), 0.02f, Random.Range(0.3f, 1.4f), Random.Range(0.6f, 1.2f));
        }
    }

    static Transform Splat(Transform parent, Vector3 localPos, int seed, float radius, float startScale, float delay, float seconds)
    {
        var go = new GameObject("Splat");
        go.layer = Ragdoll.Layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        go.AddComponent<MeshFilter>().sharedMesh = Blob(seed);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = BloodMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = true;
        var grow = go.AddComponent<BloodGrow>();
        grow.target = radius;
        grow.start = startScale;
        grow.delay = delay;
        grow.seconds = seconds;
        go.transform.localScale = Vector3.one * startScale;
        return go.transform;
    }

    // 죽은 몸을 서서히 어둡게 한 뒤 땅속으로 가라앉혀 지운다 (darkenAfter초 뒤 darkenSeconds 동안 어두워지고, sinkSeconds 동안 가라앉는다).
    // parts: 굳힐 강체들, model: 가라앉힐 뿌리, renderers: 어둡게 할 렌더러. 끝나면 root를 지운다
    public static IEnumerator FadeAway(GameObject root, Transform model, Renderer[] renderers, List<Rigidbody> parts, float darkenAfter, float darkenSeconds, float sinkSeconds)
    {
        yield return new WaitForSeconds(darkenAfter);
        var baseColors = new Color[renderers.Length];
        var baseEmission = new Color[renderers.Length];
        var hasEmission = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i] != null ? renderers[i].sharedMaterial : null;
            baseColors[i] = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            hasEmission[i] = m != null && m.IsKeywordEnabled("_EMISSION");
            baseEmission[i] = hasEmission[i] ? m.GetColor("_EmissionColor") : Color.black;
        }
        var block = new MaterialPropertyBlock();
        for (float t = 0; t < darkenSeconds && root != null; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / darkenSeconds);
            Darken(renderers, baseColors, baseEmission, hasEmission, block, k);
            yield return null;
        }
        if (root == null) yield break;
        Darken(renderers, baseColors, baseEmission, hasEmission, block, 1f);
        // 가라앉는다 (물리는 굳힌다)
        if (parts != null) Ragdoll.Freeze(parts);
        var start = model != null ? model.position : root.transform.position;
        for (float t = 0; t < sinkSeconds && root != null; t += Time.deltaTime)
        {
            float k = t / sinkSeconds;
            if (model != null) model.position = start + Vector3.down * (k * k * 1.3f);
            yield return null;
        }
        if (root != null) Object.Destroy(root);
    }

    static void Darken(Renderer[] renderers, Color[] baseColors, Color[] baseEmission, bool[] hasEmission, MaterialPropertyBlock block, float k)
    {
        var dark = new Color(0.08f, 0.07f, 0.07f);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            block.Clear();
            block.SetColor("_BaseColor", Color.Lerp(baseColors[i], dark, k * 0.92f));
            if (hasEmission[i]) block.SetColor("_EmissionColor", Color.Lerp(baseEmission[i], Color.black, Mathf.Clamp01(k * 2f)));
            r.SetPropertyBlock(block);
        }
    }
}

// 핏자국이 번진다: delay초 뒤 seconds초에 걸쳐 start → target 반지름으로 (처음엔 빨리, 끝엔 천천히)
public class BloodGrow : MonoBehaviour
{
    public float target = 0.5f, start = 0.08f, delay, seconds = 2.5f;
    float t;

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01((t - delay) / Mathf.Max(0.01f, seconds));
        float e = 1 - (1 - u) * (1 - u) * (1 - u);
        transform.localScale = Vector3.one * Mathf.Lerp(start, target, e);
        if (u >= 1f) enabled = false;
    }
}
