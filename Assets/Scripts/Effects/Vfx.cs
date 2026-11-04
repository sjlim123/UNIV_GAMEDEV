using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// 방공호 장치의 이펙트: 바닥에 쾅 놓일 때 바닥을 따라 퍼지는 먼지와 튀는 파편, 망치질 먼지, 바닥에 튕기는 불꽃, 가지처럼 뻗는 전기 아크,
// 피어오르는 김, 섬광, 잠깐 켜지는 불빛.
// 텍스처·재질·입자 시스템은 처음 쓸 때 코드로 만들고 씬이 바뀌어도 그대로 둔다 (HitEffects의 피와 같은 방식).
// 입자는 모두 EmitParams로 하나씩 내보내므로 위치·속도·색을 그 자리의 상황(벽 방향, 조명)에 맞춘다.
// 먼지는 빛을 받지 않는 재질이라 그 자리의 조명을 어림해 색에 미리 곱한다 (전구 옆은 따뜻하게, 구석은 어둡게).
public static class Vfx
{
    static ParticleSystem dust, chips, sparks, arcs, flash;
    static Material dustMat, sparkMat, arcMat, flashMat, chipMat;
    static VfxRunner runner;
    static readonly List<Light> sceneLights = new List<Light>();
    static Scene lightsScene;
    static int pulsesAlive;

    static readonly Color DustTint = new Color(0.8f, 0.72f, 0.6f);
    static readonly Color SmokeTint = new Color(0.4f, 0.4f, 0.42f);
    static readonly Color SteamTint = new Color(0.88f, 0.9f, 0.95f);

    // ---------- 공개 효과 ----------

    // 무거운 것이 바닥에 쾅 놓였다: 바닥을 따라 퍼지는 먼지 고리 + 피어오르는 먼지 기둥 + 튀는 파편.
    // open: 먼지가 퍼질 수 있는 쪽 (벽 반대쪽). zero면 사방
    public static void DustSlam(Vector3 ground, float radius, Vector3 open, float strength = 1f)
    {
        Init();
        var openDir = open.sqrMagnitude > 0.001f ? open.normalized : Vector3.zero;
        int ring = Count(60 * strength), plume = Count(26 * strength), bits = Count(36 * strength);
        for (int i = 0; i < ring; i++)
        {
            var dir = Spread(openDir, 0.3f);
            float speed = Random.Range(1.4f, 3.6f) * strength;
            Puff(ground + dir * Random.Range(0.05f, radius * 0.6f) + Vector3.up * Random.Range(0.05f, 0.25f),
                dir * speed + Vector3.up * Random.Range(0.05f, 0.6f),
                Random.Range(2.5f, 5f), Random.Range(0.4f, 0.8f) * (0.7f + 0.5f * strength), DustTint, Random.Range(0.7f, 1f));
        }
        for (int i = 0; i < plume; i++)
        {
            var dir = Spread(openDir, 0.3f);
            Puff(ground + dir * Random.Range(0f, radius * 0.5f) + Vector3.up * Random.Range(0.15f, 0.6f),
                dir * Random.Range(0.2f, 0.9f) + Vector3.up * Random.Range(0.5f, 1.6f) * strength,
                Random.Range(2f, 4f), Random.Range(0.35f, 0.65f), DustTint, Random.Range(0.55f, 0.85f));
        }
        for (int i = 0; i < bits; i++)
        {
            var dir = Spread(openDir, 0.5f);
            Chip(ground + dir * Random.Range(0.1f, radius * 0.6f) + Vector3.up * 0.05f,
                dir * Random.Range(0.8f, 3f) + Vector3.up * Random.Range(1f, 3.2f) * strength, Random.Range(0.012f, 0.035f));
        }
    }

    // 망치질: 윗면에서 옆으로 퍼지는 먼지 + 작은 부스러기 + 쇠 부스러기 불꽃 몇 개 + 짧은 섬광
    public static void BenchHit(Vector3 top)
    {
        Init();
        int n = Count(28), bits = Count(14);
        for (int i = 0; i < n; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Puff(top + dir * Random.Range(0.02f, 0.15f) + Vector3.up * Random.Range(0.04f, 0.12f),
                dir * Random.Range(0.5f, 1.6f) + Vector3.up * Random.Range(0.15f, 0.6f),
                Random.Range(1.2f, 2.4f), Random.Range(0.14f, 0.28f), DustTint, Random.Range(0.6f, 0.9f));
        }
        for (int i = 0; i < bits; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Chip(top + dir * Random.Range(0.02f, 0.1f) + Vector3.up * 0.02f, dir * Random.Range(0.5f, 1.8f) + Vector3.up * Random.Range(0.8f, 2.2f), Random.Range(0.006f, 0.016f));
        }
        SparkBurst(top + Vector3.up * 0.02f, Vector3.up, 7, 1.2f, 2.8f, 0.9f, 0.8f);
        Flash(top + Vector3.up * 0.03f, 0.2f, 0.08f, new Color(1f, 0.8f, 0.5f));
        Pulse(top + Vector3.up * 0.15f, new Color(1f, 0.75f, 0.45f), 2.5f, 1.8f, 0.12f, false);
    }

    // 불꽃 count개가 dir 쪽으로 튄다 (speedMin~speedMax m/s, spread 0 = 한 방향, 1 = 사방). 바닥에 닿으면 튕긴다
    public static void SparkBurst(Vector3 pos, Vector3 dir, int count, float speedMin = 2f, float speedMax = 5f, float spread = 0.55f, float size = 1f)
    {
        Init();
        count = Count(count);
        dir = dir.normalized;
        for (int i = 0; i < count; i++)
        {
            var v = (dir + Random.insideUnitSphere * spread).normalized * Random.Range(speedMin, speedMax);
            var p = new ParticleSystem.EmitParams
            {
                position = pos + Random.insideUnitSphere * 0.04f,
                velocity = v,
                startLifetime = Random.Range(0.4f, 1.2f),
                startSize = Random.Range(0.016f, 0.034f) * size,
                startColor = Color.Lerp(new Color(1f, 0.8f, 0.45f), new Color(1f, 0.5f, 0.12f), Random.value)
            };
            sparks.Emit(p, 1);
        }
    }

    // 불꽃이 확 튄다: 불꽃 + 섬광 + 잠깐 환해지는 불빛
    public static void SparkFlash(Vector3 pos, Vector3 dir, int count, float scale = 1f)
    {
        SparkBurst(pos, dir, count, 2f * scale, 5.5f * scale, 0.6f, scale);
        Flash(pos, 0.45f * scale, 0.12f, new Color(1f, 0.75f, 0.4f));
        Pulse(pos + dir.normalized * 0.15f, new Color(1f, 0.65f, 0.3f), 6f * scale, 3f * scale, 0.28f, true);
    }

    // seconds초 동안 불꽃이 계속 튄다 (초당 perSecond개, 가끔은 한꺼번에 왈칵)
    public static void SparkShower(Vector3 pos, Vector3 dir, float seconds, float perSecond) => Run(SparkShowerRoutine(pos, dir, seconds, perSecond));

    static IEnumerator SparkShowerRoutine(Vector3 pos, Vector3 dir, float seconds, float perSecond)
    {
        var scene = SceneManager.GetActiveScene();
        float t = 0, acc = 0, burst = Random.Range(0.3f, 0.7f);
        while (t < seconds && SceneManager.GetActiveScene() == scene)
        {
            float dt = Time.deltaTime;
            t += dt;
            acc += dt * perSecond;
            int n = Mathf.FloorToInt(acc);
            acc -= n;
            if (n > 0) SparkBurst(pos, dir, n, 1.2f, 3.5f, 0.75f, 0.9f);
            burst -= dt;
            if (burst <= 0)
            {
                burst = Random.Range(0.25f, 0.6f);
                SparkBurst(pos, dir, Random.Range(10, 20), 2.5f, 5f, 0.5f);
                Pulse(pos, new Color(1f, 0.6f, 0.25f), 3.5f, 2.5f, 0.15f, true);
            }
            yield return null;
        }
    }

    // 전기가 튄다: 가지처럼 뻗는 푸른 아크 몇 가닥 + 섬광 + 파르르 떠는 불빛 + 작은 연기. scale: 아크가 뻗는 길이(m)쯤. bursts: 몇 번 연달아
    public static void Zap(Vector3 pos, Vector3 normal, float scale, int bursts = 3) => Run(ZapRoutine(pos, normal.normalized, scale, bursts));

    static IEnumerator ZapRoutine(Vector3 pos, Vector3 normal, float scale, int bursts)
    {
        Init();
        var scene = SceneManager.GetActiveScene();
        for (int b = 0; b < bursts; b++)
        {
            if (SceneManager.GetActiveScene() != scene) yield break;
            // 아크 길이에 맞춰 지그재그의 폭과 간격을 정한다
            var noise = arcs.noise;
            noise.strength = scale * 5f;
            noise.frequency = 1.2f / Mathf.Max(0.03f, scale);
            var trails = arcs.trails;
            trails.minVertexDistance = Mathf.Max(0.002f, scale * 0.03f);
            int n = Count(Random.Range(10, 18));
            for (int i = 0; i < n; i++)
            {
                var dir = (normal * 0.6f + Random.insideUnitSphere).normalized;
                float life = Random.Range(0.1f, 0.22f);
                var p = new ParticleSystem.EmitParams
                {
                    position = pos + Random.insideUnitSphere * 0.02f * scale,
                    velocity = dir * (scale / life) * Random.Range(0.6f, 1.3f), // 수명 동안 scale만큼 뻗는다
                    startLifetime = life,
                    startSize = Random.Range(0.011f, 0.02f) * Mathf.Max(0.6f, scale * 3f),
                    startColor = Color.Lerp(new Color(0.6f, 0.78f, 1f), new Color(0.3f, 0.5f, 1f), Random.value)
                };
                arcs.Emit(p, 1);
            }
            // 섬광은 작고 푸르게 (아크가 주인공이다). 불빛은 면에서 조금 떨어뜨려야 그 자리가 하얗게 타지 않는다
            Flash(pos, 0.3f * scale + 0.05f, 0.06f, new Color(0.5f, 0.65f, 1f, 0.5f));
            Pulse(pos + normal * (0.22f + scale), new Color(0.55f, 0.7f, 1f), 1.1f * (0.5f + scale * 2f), 1.2f + scale * 4f, 0.12f, true);
            // 탄 냄새 나는 작은 연기
            for (int i = 0; i < 2; i++)
                Puff(pos + normal * 0.02f, normal * 0.15f + Vector3.up * Random.Range(0.15f, 0.35f) + Random.insideUnitSphere * 0.1f,
                    Random.Range(1f, 1.8f), Random.Range(0.05f, 0.1f) + scale * 0.3f, SmokeTint, 0.35f);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.22f));
        }
    }

    // seconds초 동안 김이 피어오른다 (고기를 구울 때). drift: 위로 오르면서 함께 흘러가는 방향 (화구 밖으로)
    public static void Steam(Vector3 pos, Vector3 drift, float seconds, float perSecond = 10f) => Run(SteamRoutine(pos, drift, seconds, perSecond));

    static IEnumerator SteamRoutine(Vector3 pos, Vector3 drift, float seconds, float perSecond)
    {
        Init();
        var scene = SceneManager.GetActiveScene();
        float t = 0, acc = 0;
        while (t < seconds && SceneManager.GetActiveScene() == scene)
        {
            float dt = Time.deltaTime;
            t += dt;
            acc += dt * perSecond;
            while (acc >= 1f)
            {
                acc -= 1f;
                SteamPuff(pos, drift);
            }
            yield return null;
        }
    }

    // 김 한 뭉치: 빨리 솟아 불빛 밖으로 벗어난 뒤 느려지며 퍼진다 (불구멍 앞에서는 밝은 불빛에 묻혀 안 보인다)
    public static void SteamPuff(Vector3 pos, Vector3 drift)
    {
        Init();
        Puff(pos + Random.insideUnitSphere * 0.05f, Vector3.up * Random.Range(0.9f, 1.4f) + drift + Random.insideUnitSphere * 0.15f,
            Random.Range(1.4f, 2.4f), Random.Range(0.16f, 0.28f), SteamTint, Random.Range(0.5f, 0.7f));
    }

    // 가산 혼합 섬광 한 장 (size m, life초)
    public static void Flash(Vector3 pos, float size, float life, Color color)
    {
        Init();
        var p = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = Vector3.zero,
            startLifetime = life,
            startSize = size,
            startColor = color,
            rotation = Random.Range(0f, 360f)
        };
        flash.Emit(p, 1);
    }

    // 잠깐 켜졌다 꺼지는 점광원 (flicker면 파르르 떤다). 한꺼번에 너무 많이 켜지면 건너뛴다
    public static void Pulse(Vector3 pos, Color color, float intensity, float range, float seconds, bool flicker)
    {
        if (pulsesAlive >= 6) return;
        var go = new GameObject("VfxLight");
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        var pulse = go.AddComponent<LightPulse>();
        pulse.peak = intensity;
        pulse.seconds = Mathf.Max(0.02f, seconds);
        pulse.flicker = flicker;
        pulsesAlive++;
    }

    internal static void PulseEnded() => pulsesAlive = Mathf.Max(0, pulsesAlive - 1);

    // 그 자리의 빛을 어림한다 (씬의 점광원 + 주변광). 밝은 곳은 눈이 뭉개지듯 부드럽게 누른다
    public static Color LightAt(Vector3 p)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene != lightsScene || sceneLights.Count == 0)
        {
            sceneLights.Clear();
            sceneLights.AddRange(Object.FindObjectsByType<Light>(FindObjectsSortMode.None));
            lightsScene = scene;
        }
        Color c = RenderSettings.ambientLight * 1.5f;
        foreach (var l in sceneLights)
        {
            if (l == null || !l.enabled || !l.gameObject.activeInHierarchy || l.type == LightType.Directional) continue;
            float d = Vector3.Distance(p, l.transform.position);
            if (d >= l.range) continue;
            float win = 1f - d / l.range;
            win *= win;
            float k = l.intensity * win / (1f + d * d) * (l.type == LightType.Spot ? 0.5f : 1f);
            c += l.color * k;
        }
        return new Color(Soft(c.r), Soft(c.g), Soft(c.b), 1f);
    }

    static float Soft(float x) => x / (1f + x) * 1.6f;

    // ---------- 입자 하나 ----------

    static void Puff(Vector3 pos, Vector3 vel, float life, float size, Color tint, float alpha)
    {
        var c = LightAt(pos) * tint;
        c.a = alpha;
        var p = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = vel,
            startLifetime = life,
            startSize = size,
            startColor = c,
            rotation = Random.Range(0f, 360f),
            angularVelocity = Random.Range(-30f, 30f)
        };
        dust.Emit(p, 1);
    }

    static void Chip(Vector3 pos, Vector3 vel, float size)
    {
        var p = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = vel,
            startLifetime = Random.Range(2.5f, 4f),
            startSize3D = new Vector3(size * Random.Range(0.6f, 1.6f), size * Random.Range(0.4f, 1f), size * Random.Range(0.6f, 1.6f)),
            rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
            angularVelocity3D = Random.insideUnitSphere * 540f,
            startColor = Color.white
        };
        chips.Emit(p, 1);
    }

    // 바닥 방향 하나: open이 있으면 그쪽 반구(벽 반대쪽)에서만 뽑는다
    static Vector3 Spread(Vector3 open, float minDot)
    {
        for (int k = 0; k < 8; k++)
        {
            float a = Random.value * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            if (open == Vector3.zero || Vector3.Dot(d, open) > -minDot) return d;
        }
        return open;
    }

    // 그래픽 품질에 따라 입자 수를 줄인다
    static int Count(float n) => Mathf.Max(1, Mathf.RoundToInt(n * (GameSettings.Quality == 0 ? 0.45f : GameSettings.Quality == 1 ? 0.75f : 1f)));

    static void Run(IEnumerator routine)
    {
        if (runner == null)
        {
            var go = new GameObject("VfxRunner");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<VfxRunner>();
        }
        runner.StartCoroutine(routine);
    }

    // ---------- 만들기 ----------

    static void Init()
    {
        if (dust != null) return;
        var puff = PuffTexture();
        var hot = HotDotTexture();
        var soft = Resources.Load<Texture2D>("Effects/T_SoftCircle");
        if (soft == null) soft = hot;
        // 소프트 파티클 거리는 짧게: 길면 상판·화구처럼 면 바로 위에 뜨는 먼지가 통째로 지워진다
        dustMat = Make("Effects/M_VfxDust", false, puff, Color.white, 0f, 0.1f, 0.12f, 0.55f);
        sparkMat = Make("Effects/M_VfxSpark", true, hot, new Color(3f, 2.3f, 1.4f, 1f), 0f, 0f, 0.05f, 0.2f);
        arcMat = Make("Effects/M_VfxSpark", true, hot, new Color(1.2f, 1.6f, 2.6f, 1f), 0f, 0f, 0.03f, 0.15f);
        flashMat = Make("Effects/M_VfxSpark", true, soft, new Color(2.2f, 2.2f, 2.2f, 1f), 0f, 0.12f, 0.08f, 0.3f);
        chipMat = RuntimeVisuals.Mat(new Color(0.55f, 0.5f, 0.44f), 0.2f);

        // 먼지·연기·김: 천천히 퍼지며 커지고, 바람에 흔들리듯 조금씩 흐른다
        dust = System("VfxDust", dustMat, 2000);
        {
            var main = dust.main;
            main.gravityModifier = 0.02f;
            var lim = dust.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.drag = 1.4f;
            lim.multiplyDragByParticleVelocity = true;
            var noise = dust.noise;
            noise.enabled = true;
            noise.strength = 0.22f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.4f;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.damping = true;
            var col = dust.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.55f, 1, 1.35f));
            var sheet = dust.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
            var r = dust.GetComponent<ParticleSystemRenderer>();
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = 2f;
        }

        // 파편: 작은 쇠·돌 조각이 구르며 튕긴다. 사라지기 전에 작아진다
        chips = System("VfxChips", chipMat, 500);
        {
            var main = chips.main;
            main.gravityModifier = 1f;
            main.startRotation3D = true;
            var col = chips.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.bounce = 0.3f;
            col.dampen = 0.4f;
            col.lifetimeLoss = 0f;
            col.radiusScale = 0.5f;
            col.quality = ParticleSystemCollisionQuality.Medium;
            var size = chips.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.8f, 1), new Keyframe(1, 0)));
            var r = chips.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            r.alignment = ParticleSystemRenderSpace.World;
            r.enableGPUInstancing = true;
        }

        // 불꽃: 속도 방향으로 길게 늘어난 빛 알갱이 + 짧은 꼬리. 중력에 떨어져 바닥에 튕기며 식어 간다
        sparks = System("VfxSparks", sparkMat, 1500);
        {
            var main = sparks.main;
            main.gravityModifier = 1.1f;
            var lim = sparks.limitVelocityOverLifetime;
            lim.enabled = true;
            lim.drag = 0.35f;
            var col = sparks.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.35f), new GradientColorKey(new Color(0.7f, 0.12f, 0.02f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.3f));
            var collision = sparks.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.bounce = 0.45f;
            collision.dampen = 0.3f;
            collision.lifetimeLoss = 0.15f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            Trails(sparks, 0.09f, 0.01f);
            var r = sparks.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 1.3f;
            r.velocityScale = 0.02f;
            r.trailMaterial = sparkMat;
            r.minParticleSize = 0f;
            r.maxParticleSize = 0.5f;
        }

        // 전기 아크: 아주 짧게 사는 알갱이가 노이즈로 지그재그 뻗고, 꼬리가 그 길을 그린다
        arcs = System("VfxArcs", arcMat, 400);
        {
            var noise = arcs.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 10f;
            noise.scrollSpeed = 8f;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.damping = false;
            var col = arcs.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            Trails(arcs, 0.7f, 0.004f);
            var r = arcs.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.trailMaterial = arcMat;
            r.minParticleSize = 0f;
            r.maxParticleSize = 0.3f;
        }

        // 섬광: 확 커졌다가 줄며 사라지는 둥근 빛
        flash = System("VfxFlash", flashMat, 200);
        {
            var size = flash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.15f, 1f), new Keyframe(1, 0.1f)));
            var col = flash.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = flash.GetComponent<ParticleSystemRenderer>();
            r.maxParticleSize = 1f;
        }
    }

    static void Trails(ParticleSystem ps, float lifetimeRatio, float minVertexDistance)
    {
        var t = ps.trails;
        t.enabled = true;
        t.mode = ParticleSystemTrailMode.PerParticle;
        t.ratio = 1f;
        t.lifetime = new ParticleSystem.MinMaxCurve(lifetimeRatio);
        t.minVertexDistance = minVertexDistance;
        t.worldSpace = true;
        t.dieWithParticles = true;
        t.sizeAffectsWidth = true;
        t.inheritParticleColor = true;
        t.generateLightingData = false;
        t.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        t.colorOverTrail = new ParticleSystem.MinMaxGradient(g);
    }

    static ParticleSystem System(string name, Material mat, int max)
    {
        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        var em = ps.emission;
        em.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        ps.Play();
        return ps;
    }

    // Resources의 원본(셰이더·키워드를 빌드에 넣기 위한 것)을 복사해 쓴다. 없으면 같은 셰이더로 새로 만든다
    static Material Make(string resource, bool additive, Texture2D tex, Color tint, float softNear, float softFar, float fadeNear, float fadeFar)
    {
        var template = Resources.Load<Material>(resource);
        var m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.name = resource.Substring(resource.LastIndexOf('/') + 1) + "_inst";
        Configure(m, additive, softFar > softNear, fadeFar > fadeNear);
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        m.SetColor("_BaseColor", tint);
        if (softFar > softNear) m.SetVector("_SoftParticleFadeParams", new Vector4(softNear, 1f / (softFar - softNear), 0, 0));
        if (fadeFar > fadeNear) m.SetVector("_CameraFadeParams", new Vector4(fadeNear, 1f / (fadeFar - fadeNear), 0, 0));
        return m;
    }

    // 입자 재질 설정. 편집기에서 Resources 원본을 만들 때도 이것을 써서 같은 셰이더 변형이 빌드에 들어가게 한다.
    // soft: 바닥·벽에 닿는 가장자리가 부드럽게 사라진다 (깊이 텍스처 필요). fade: 카메라에 너무 가까우면 투명해진다
    public static void Configure(Material m, bool additive, bool soft, bool fade)
    {
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", additive ? 2 : 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetFloat("_SoftParticlesEnabled", soft ? 1 : 0);
        if (soft) m.EnableKeyword("_SOFTPARTICLES_ON"); else m.DisableKeyword("_SOFTPARTICLES_ON");
        m.SetFloat("_CameraFadingEnabled", fade ? 1 : 0);
        if (fade) m.EnableKeyword("_FADING_ON"); else m.DisableKeyword("_FADING_ON");
        m.renderQueue = (int)RenderQueue.Transparent + (additive ? 2 : 0); // 빛나는 것은 먼지 위에 그린다
    }

    // ---------- 텍스처 ----------

    // 먼지 뭉치 2×2장: 흐린 원 안에 노이즈 덩어리 (장마다 다른 씨앗이라 같은 먼지가 반복되지 않는다)
    static Texture2D PuffTexture()
    {
        const int S = 128;
        var tex = new Texture2D(S * 2, S * 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "VfxPuff" };
        var px = new Color[S * S * 4];
        for (int tile = 0; tile < 4; tile++)
        {
            float ox = 17.3f + tile * 41.7f, oy = 5.1f + tile * 23.9f;
            int tx = (tile % 2) * S, ty = (tile / 2) * S;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float mask = Mathf.Clamp01(1f - Mathf.InverseLerp(0.3f, 1f, r));
                    mask = mask * mask * (3 - 2 * mask);
                    float n = 0, amp = 0.5f, freq = 2.2f;
                    for (int o = 0; o < 4; o++)
                    {
                        n += Mathf.PerlinNoise(ox + (u + 1) * freq, oy + (v + 1) * freq) * amp;
                        amp *= 0.5f;
                        freq *= 2.1f;
                    }
                    n /= 0.9375f;
                    // 노이즈 덩어리 + 가운데를 채우는 부드러운 심 (가운데가 비어 고리처럼 보이지 않게)
                    float a = Mathf.Clamp01((n - 0.22f) * 2.2f + 0.5f * Mathf.Exp(-r * r * 6f)) * mask;
                    float b = 0.8f + 0.2f * mask; // 가운데가 살짝 밝다 (두께감)
                    px[(ty + y) * S * 2 + tx + x] = new Color(b, b, b, a);
                }
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    // 뜨거운 빛 알갱이: 흰 심 + 따뜻한 빛무리
    static Texture2D HotDotTexture()
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "VfxHotDot" };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1;
                float r = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                float core = 1f - Mathf.SmoothStep(0.1f, 0.4f, r);
                float glow = Mathf.Pow(1f - r, 2.2f);
                float a = Mathf.Clamp01(core + glow * 0.6f);
                var c = Color.Lerp(new Color(1f, 0.85f, 0.6f), Color.white, core);
                px[y * S + x] = new Color(c.r, c.g, c.b, a);
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }
}

// Vfx의 코루틴 주인 (씬이 바뀌어도 남는다)
class VfxRunner : MonoBehaviour { }

// 잠깐 켜졌다 꺼지는 불빛
class LightPulse : MonoBehaviour
{
    public float peak = 1f, seconds = 0.2f;
    public bool flicker;
    Light lamp;
    float t, seed;

    void Awake()
    {
        lamp = GetComponent<Light>();
        seed = Random.value * 10f;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = 1f - Mathf.Clamp01(t / seconds);
        k *= k;
        if (flicker) k *= 0.55f + 0.45f * Mathf.PerlinNoise(Time.time * 40f, seed);
        if (lamp != null) lamp.intensity = peak * k;
        if (t >= seconds) Destroy(gameObject);
    }

    void OnDestroy() => Vfx.PulseEnded();
}
