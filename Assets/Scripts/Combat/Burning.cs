using UnityEngine;

// 화상: 라이터로 때린 적에게 불이 붙어 seconds초 동안 모두 total만큼 피해를 입는다 (기본 5초 · 10).
// 몸에서 불꽃·연기가 피어오르고 주황빛이 일렁이며 타닥타닥 소리가 난다. 다시 맞으면 시간이 처음부터 다시 간다
public class Burning : MonoBehaviour
{
    const float TickInterval = 0.5f;

    float duration, damagePerTick, timeLeft, tickTimer;
    ParticleSystem fire, smoke;
    Light glow;
    AudioSource crackle;
    HitFlash flash;
    IDamageable target;
    float height = 1.7f, localHeight = 1.7f, width = 1f; // 몸 높이(월드·로컬), 몸 배율

    public static void Apply(GameObject go, float seconds, float total)
    {
        var target = go.GetComponentInParent<IDamageable>();
        if (target == null) return;
        var root = ((Component)target).gameObject;
        var b = root.GetComponent<Burning>();
        if (b == null) b = root.AddComponent<Burning>();
        b.Begin(target, seconds, total);
    }

    public static bool IsBurning(GameObject go)
    {
        var b = go.GetComponentInParent<Burning>();
        return b != null && b.timeLeft > 0;
    }

    void Begin(IDamageable t, float seconds, float total)
    {
        target = t;
        duration = Mathf.Max(0.5f, seconds);
        damagePerTick = total / Mathf.Max(1, Mathf.RoundToInt(duration / TickInterval));
        timeLeft = duration;
        tickTimer = TickInterval;
        var col = GetComponent<Collider>();
        if (col != null)
        {
            height = col.bounds.size.y;
            localHeight = height / Mathf.Max(0.01f, transform.lossyScale.y);
            width = transform.lossyScale.x;
        }
        if (fire == null) BuildEffects();
        fire.Play();
        smoke.Play();
        if (!crackle.isPlaying) crackle.Play();
        flash = GetComponent<HitFlash>();
        ProceduralAudio.PlayAt3D(ProceduralAudio.Ignite(), transform.position + Vector3.up * height * 0.5f, 1f, Random.Range(0.9f, 1.1f));
        enabled = true;
    }

    void BuildEffects()
    {
        // 불꽃: 몸통 둘레에서 위로 솟는 가산 혼합 입자
        fire = MakeParticles("BurnFire", HitEffects.ParticleMaterial("Effects/M_BurnFire", new Color(1f, 0.55f, 0.2f, 1f), true));
        var main = fire.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f, 0.9f), new Color(1f, 0.3f, 0.06f, 0.8f));
        main.gravityModifier = -0.25f;
        var em = fire.emission;
        em.rateOverTime = 70;
        var shape = fire.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        // 입자 크기·모양은 부모 배율을 받지 않으므로 월드 크기로 정하고, 위치만 몸에 맞춘다
        shape.scale = new Vector3(0.45f * width, height * 0.75f, 0.35f * width);
        fire.transform.localPosition = new Vector3(0, localHeight * 0.5f, 0);
        var col = fire.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.5f), new GradientColorKey(new Color(0.4f, 0.05f, 0.02f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var size = fire.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0.2f));

        // 검은 연기
        smoke = MakeParticles("BurnSmoke", HitEffects.ParticleMaterial("Effects/M_BurnSmoke", new Color(0.12f, 0.1f, 0.09f, 0.6f), false));
        var sm = smoke.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.09f, 0.08f, 0.5f), new Color(0.2f, 0.18f, 0.16f, 0.35f));
        sm.gravityModifier = -0.15f;
        var sem = smoke.emission;
        sem.rateOverTime = 18;
        var ss = smoke.shape;
        ss.enabled = true;
        ss.shapeType = ParticleSystemShapeType.Sphere;
        ss.radius = 0.2f;
        smoke.transform.localPosition = new Vector3(0, localHeight * 0.85f, 0);
        var scol = smoke.colorOverLifetime;
        scol.enabled = true;
        var sg = new Gradient();
        sg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) });
        scol.color = sg;
        var ssize = smoke.sizeOverLifetime;
        ssize.enabled = true;
        ssize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.8f));

        // 주변을 비추는 일렁이는 불빛
        var lg = new GameObject("BurnLight");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0, localHeight * 0.6f, 0);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.5f, 0.15f);
        glow.range = 5f;
        glow.intensity = 0;
        glow.shadows = LightShadows.None;

        crackle = gameObject.AddComponent<AudioSource>();
        ProceduralAudio.Setup3D(crackle, 22f, 100);
        crackle.clip = ProceduralAudio.Crackle();
        crackle.loop = true;
        crackle.volume = 0.8f;
    }

    ParticleSystem MakeParticles(string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        bool dead = target == null || (target is Zombie z && z.IsDead) || (target is Animal a && a.IsDead);
        if (timeLeft > 0 && !dead)
        {
            timeLeft -= dt;
            tickTimer -= dt;
            if (tickTimer <= 0)
            {
                tickTimer += TickInterval;
                target.TakeDamage(damagePerTick, Vector3.zero);
            }
        }
        else if (timeLeft > 0) timeLeft = Mathf.Min(timeLeft, 0.8f) - dt; // 죽으면 불이 금방 사그라든다
        float k = Mathf.Clamp01(timeLeft / 0.6f);
        if (flash != null) flash.SetBurning(k);
        if (glow != null) glow.intensity = k * (2.2f + Mathf.PerlinNoise(Time.time * 12f, 0.7f) * 2.2f);
        if (crackle != null) crackle.volume = 0.8f * k;
        if (timeLeft <= 0)
        {
            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (crackle != null) crackle.Stop();
            if (flash != null) flash.SetBurning(0);
            if (glow != null) glow.intensity = 0;
            enabled = false;
        }
    }
}
