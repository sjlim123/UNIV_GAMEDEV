using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 타격감: 맞은 적이 붉게 달아오르고(HitFlash), 맞은 자리에서 피가 튀며 붉은 빛이 잠깐 번지고(Blood·Vfx.Flash·Vfx.Pulse),
// 아주 잠깐 화면이 멈칫하며(HitStop) 조준점에 빨간 X가 뜬다 (UIManager.ShowHitMarker)
public static class HitEffects
{
    static ParticleSystem blood;
    static Material bloodMat;

    // 플레이어가 적을 때렸을 때 한 번에 부른다
    public static void PlayerHit(Component target, Vector3 point, Vector3 direction, bool killed)
    {
        Blood(point, -direction, killed ? 44 : 14);
        // 맞은 자리에 붉은 빛이 잠깐 번진다 (몸의 붉은 달아오름과 함께, 어두운 밤에도 맞았다는 것이 보인다. 참고 영상보다 훨씬 은은하게)
        var back = -direction.normalized;
        Vfx.Flash(point + back * 0.06f, killed ? 0.5f : 0.32f, 0.14f, new Color(1f, 0.12f, 0.08f, killed ? 0.5f : 0.32f));
        Vfx.Pulse(point + back * 0.3f, new Color(1f, 0.2f, 0.1f), killed ? 1.6f : 0.9f, 2.2f, 0.16f, false);
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

// 맞으면 몸 전체가 잠깐 붉게 달아오른다 (불에 타면 주황색으로 일렁인다).
// 어두운 밤에는 색만 바꿔서는 보이지 않아, 발광(_EMISSION)을 켠 쌍둥이 재질로 잠깐 바꿔 끼우고 MaterialPropertyBlock으로 발광색을 준다.
// 쌍둥이는 재질마다 하나만 만들어 모든 적이 함께 쓰고, 번쩍임이 끝나면 원래 재질로 되돌린다 (평소 그리기 비용은 그대로).
// 빌드에 이 셰이더 변형(_NORMALMAP + _EMISSION)이 들어가도록 Resources/Effects/M_HitGlow 재질을 둔다
public class HitFlash : MonoBehaviour
{
    public static readonly Color HitColor = new Color(1f, 0.1f, 0.06f);
    public static readonly Color BurnColor = new Color(1f, 0.42f, 0.08f);
    const float FlashTime = 0.45f; // 참고 영상보다 훨씬 은은하게: 잠깐 달아올랐다 식는다
    const float HitGlow = 0.5f;    // 발광 세기 (몸의 음영이 남을 만큼만. 블룸은 거의 번지지 않는다)

    static readonly Dictionary<Material, Material> twins = new Dictionary<Material, Material>();
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionId = Shader.PropertyToID("_EmissionColor");

    Renderer[] renderers;
    Material[] originals, glowing;
    Color[] baseColors;
    MaterialPropertyBlock block;
    Color color;
    float t = 1f, burn;
    bool swapped;

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
        // 눈처럼 스스로 빛나는 부품은 뺀다
        var use = new List<Renderer>();
        foreach (var r in list ?? new Renderer[0])
            if (r != null && r.sharedMaterial != null && !r.sharedMaterial.IsKeywordEnabled("_EMISSION")) use.Add(r);
        renderers = use.ToArray();
        originals = new Material[renderers.Length];
        glowing = new Material[renderers.Length];
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            originals[i] = m;
            glowing[i] = Twin(m);
            baseColors[i] = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
        }
        block ??= new MaterialPropertyBlock();
    }

    // 발광을 켠 쌍둥이 재질 (재질마다 하나). 노멀맵 키워드도 켜서 변형이 M_HitGlow와 같게 한다 (노멀맵이 없으면 평평한 기본 노멀)
    static Material Twin(Material src)
    {
        if (twins.TryGetValue(src, out var tw) && tw != null) return tw;
        tw = new Material(src) { name = src.name + "_glow" };
        tw.EnableKeyword("_EMISSION");
        tw.EnableKeyword("_NORMALMAP");
        tw.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        tw.SetColor(EmissionId, Color.black);
        twins[src] = tw;
        return tw;
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
        float hit = (1 - t) * (1 - t); // 처음에 달아올랐다 식는다
        float b = burn > 0 ? burn * (0.3f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, 0.3f)) : 0;
        if (hit <= 0.002f && b <= 0.002f)
        {
            if (swapped) Clear();
            if (burn <= 0) enabled = false;
            return;
        }
        if (!swapped) Swap(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            var c = Color.Lerp(baseColors[i], BurnColor, b * 0.5f);
            c = Color.Lerp(c, color, hit * 0.3f);
            var glow = BurnColor * (b * 1.2f) + color * (hit * HitGlow);
            block.SetColor(BaseColorId, c);
            block.SetColor(EmissionId, glow);
            r.SetPropertyBlock(block);
        }
    }

    void Swap(bool on)
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sharedMaterial = on ? glowing[i] : originals[i];
        swapped = on;
    }

    void Clear()
    {
        foreach (var r in renderers)
            if (r != null) r.SetPropertyBlock(null);
        if (swapped) Swap(false);
    }

    // 죽어서 꺼지면 원래 재질로 되돌린다 (죽음 연출이 재질을 다루기 전에)
    void OnDisable()
    {
        if (renderers != null && swapped) Clear();
    }
}
