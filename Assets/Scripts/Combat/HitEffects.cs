using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 타격감: 맞은 적이 빨갛게 번쩍이고(HitFlash), 맞은 자리에서 피가 튀고(Blood),
// 아주 잠깐 화면이 멈칫하며(HitStop) 조준점에 빨간 X가 뜬다 (UIManager.ShowHitMarker)
public static class HitEffects
{
    static ParticleSystem blood;
    static Material bloodMat;

    // 플레이어가 적을 때렸을 때 한 번에 부른다
    public static void PlayerHit(Component target, Vector3 point, Vector3 direction, bool killed)
    {
        Blood(point, -direction, killed ? 44 : 14);
        var ui = GameManager.Instance.UI;
        ui.ShowHitMarker(killed);
        HitStop(killed ? 0.09f : 0.05f);
        if (killed) GameManager.Instance.StartCoroutine(KillSlow());
    }

    // 처치: 멈칫한 뒤 잠깐 느려졌다가 돌아온다 + 둔탁한 울림 (마지막 타격을 느끼게)
    const float KillScale = 0.35f;

    static IEnumerator KillSlow()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        if (Time.timeScale > 0.99f || Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = KillScale;
        ProceduralAudio.Play2D(ProceduralAudio.Bang(), 0.35f, 0.45f);
        yield return new WaitForSecondsRealtime(0.18f);
        if (Mathf.Approximately(Time.timeScale, KillScale)) Time.timeScale = 1f;
    }

    // 맞은 자리에서 핏방울이 튄다 (normal 쪽으로)
    public static void Blood(Vector3 point, Vector3 normal, int count)
    {
        if (blood == null) blood = MakeBlood();
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            var dir = (normal.normalized + Random.insideUnitSphere * 0.75f + Vector3.up * 0.35f).normalized;
            p.position = point + Random.insideUnitSphere * 0.05f;
            p.velocity = dir * Random.Range(1.5f, 4.2f);
            p.startSize = Random.Range(0.025f, 0.07f);
            p.startLifetime = Random.Range(0.35f, 0.7f);
            p.startColor = Color.Lerp(new Color(0.42f, 0.03f, 0.02f), new Color(0.25f, 0.01f, 0.01f), Random.value);
            blood.Emit(p, 1);
        }
        // 뿌연 피 안개
        p.velocity = normal.normalized * 0.6f;
        p.startSize = 0.28f;
        p.startLifetime = 0.25f;
        p.startColor = new Color(0.45f, 0.02f, 0.02f, 0.45f);
        p.position = point;
        blood.Emit(p, 2);
    }

    static ParticleSystem MakeBlood()
    {
        var go = new GameObject("HitBlood");
        Object.DontDestroyOnLoad(go);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.6f;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.4f));
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
        col.color = grad;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = BloodMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // 피·불꽃 입자 재질 (Resources/Effects에 만들어 둔다. 없으면 같은 셰이더로 새로 만든다)
    public static Material BloodMaterial => bloodMat != null ? bloodMat : bloodMat = ParticleMaterial("Effects/M_HitBlood", new Color(1, 1, 1, 1), false);

    public static Material ParticleMaterial(string resource, Color color, bool additive)
    {
        var m = Resources.Load<Material>(resource);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", additive ? 2 : 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    // 아주 잠깐 시간을 느리게 해 "퍽" 하는 손맛을 준다 (일시정지 등 다른 곳에서 시간을 바꿨으면 건드리지 않는다)
    const float StopScale = 0.06f;

    public static void HitStop(float seconds)
    {
        if (Time.timeScale != 1f) return;
        GameManager.Instance.StartCoroutine(HitStopRoutine(seconds));
    }

    static IEnumerator HitStopRoutine(float seconds)
    {
        Time.timeScale = StopScale;
        yield return new WaitForSecondsRealtime(seconds);
        if (Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;
    }

    // 몸에서 떨어져 나간 조각 (가죽 갑옷): 맞은 방향으로 튕겨 바닥에 떨어지고 잠시 뒤 사라진다
    public static void DropPiece(Transform piece, Vector3 direction)
    {
        if (piece == null) return;
        piece.SetParent(null, true);
        var bounds = new Bounds(piece.position, Vector3.one * 0.05f);
        foreach (var r in piece.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        var holder = new GameObject(piece.name + "_Dropped");
        holder.transform.position = bounds.center;
        piece.SetParent(holder.transform, true);
        holder.layer = 2; // Ignore Raycast: 공격·시야를 막지 않는다
        var box = holder.AddComponent<BoxCollider>();
        box.size = Vector3.Max(bounds.size, Vector3.one * 0.04f);
        var rb = holder.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.linearVelocity = new Vector3(direction.x, 0, direction.z).normalized * 2.2f + Vector3.up * 2.4f + Random.insideUnitSphere * 0.6f;
        rb.angularVelocity = Random.insideUnitSphere * 8f;
        Object.Destroy(holder, 14f);
    }
}

// 맞으면 몸 전체가 잠깐 빨갛게 번쩍인다 (불에 타면 주황색으로 일렁인다). 재질은 그대로 두고 MaterialPropertyBlock만 바꾼다
public class HitFlash : MonoBehaviour
{
    public static readonly Color HitColor = new Color(1f, 0.12f, 0.08f);
    public static readonly Color BurnColor = new Color(1f, 0.45f, 0.08f);
    const float FlashTime = 0.18f;

    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock block;
    Color color;
    float t = 1f, burn;
    bool dirty;

    public static HitFlash For(GameObject go, Renderer[] renderers)
    {
        var f = go.GetComponent<HitFlash>();
        if (f == null) f = go.AddComponent<HitFlash>();
        f.Bind(renderers);
        return f;
    }

    void Bind(Renderer[] list)
    {
        if (renderers != null) Clear();
        renderers = list ?? new Renderer[0];
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i] != null ? renderers[i].sharedMaterial : null;
            baseColors[i] = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
        }
        block ??= new MaterialPropertyBlock();
    }

    public void Flash(Color c)
    {
        color = c;
        t = 0;
        enabled = true;
    }

    // 불에 타는 동안 계속 주황빛으로 일렁인다 (0이면 끈다)
    public void SetBurning(float amount)
    {
        burn = amount;
        enabled = true;
    }

    void Update()
    {
        if (renderers == null) return;
        t = Mathf.Min(1f, t + Time.deltaTime / FlashTime);
        float hit = (1 - t) * (1 - t);
        float b = burn > 0 ? burn * (0.25f + 0.2f * Mathf.PerlinNoise(Time.time * 9f, 0.3f)) : 0;
        if (hit <= 0.001f && b <= 0.001f)
        {
            if (dirty) Clear();
            if (burn <= 0) enabled = false;
            return;
        }
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var c = Color.Lerp(baseColors[i], BurnColor, b);
            c = Color.Lerp(c, color, hit * 0.85f);
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
        dirty = true;
    }

    void Clear()
    {
        foreach (var r in renderers)
            if (r != null) r.SetPropertyBlock(null);
        dirty = false;
    }
}
