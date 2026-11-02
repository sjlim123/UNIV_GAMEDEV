using System.Collections.Generic;
using UnityEngine;

// 사운드 에셋 없이 코드로 효과음을 합성한다 (발소리, 바람, 좀비 신음 등)
public static class ProceduralAudio
{
    const int SR = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static System.Random rng = new System.Random(1234);

    static float Noise() => (float)(rng.NextDouble() * 2 - 1);
    static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    class Biquad
    {
        float b0, b1, b2, a1, a2, x1, x2, y1, y2;

        public static Biquad BandPass(float f, float q)
        {
            float w = 2 * Mathf.PI * f / SR, alpha = Mathf.Sin(w) / (2 * q), a0 = 1 + alpha;
            return new Biquad { b0 = alpha / a0, b1 = 0, b2 = -alpha / a0, a1 = -2 * Mathf.Cos(w) / a0, a2 = (1 - alpha) / a0 };
        }

        public static Biquad LowPass(float f, float q = 0.707f)
        {
            float w = 2 * Mathf.PI * f / SR, alpha = Mathf.Sin(w) / (2 * q), cos = Mathf.Cos(w), a0 = 1 + alpha;
            return new Biquad { b0 = (1 - cos) / 2 / a0, b1 = (1 - cos) / a0, b2 = (1 - cos) / 2 / a0, a1 = -2 * cos / a0, a2 = (1 - alpha) / a0 };
        }

        public float Process(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    static AudioClip Make(string name, float seconds, System.Func<float, float> sample, float peak = 0.9f, float loopFade = 0f)
    {
        if (cache.TryGetValue(name, out var clip) && clip != null) return clip;
        int n = (int)(seconds * SR);
        int fade = (int)(loopFade * SR);
        var data = new float[n + fade];
        for (int i = 0; i < data.Length; i++) data[i] = sample(i / (float)SR);
        // 루프 사운드는 끝부분을 앞부분에 겹쳐 끊김을 없앤다
        if (fade > 0)
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = data[i] * k + data[n + i] * (1 - k);
            }
        float max = 0.0001f;
        for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
        var outData = new float[n];
        for (int i = 0; i < n; i++) outData[i] = data[i] / max * peak;
        clip = AudioClip.Create(name, n, 1, SR, false);
        clip.SetData(outData, 0);
        cache[name] = clip;
        return clip;
    }

    public static AudioClip Footstep()
    {
        int v = rng.Next(4);
        return Make("step" + v, 0.2f, MakeStep(v));
    }

    static System.Func<float, float> MakeStep(int v)
    {
        var lp = Biquad.LowPass(1400 + v * 250);
        var bp = Biquad.BandPass(260 + v * 40, 1.2f);
        float thumpF = 60 + v * 8;
        return t =>
        {
            float thump = Mathf.Sin(2 * Mathf.PI * thumpF * t) * Mathf.Exp(-t * 45);
            float grit = lp.Process(Noise()) * Mathf.Exp(-t * 38) + bp.Process(Noise()) * 0.6f * Mathf.Exp(-t * 60);
            return thump * 0.7f + grit;
        };
    }

    // 빗소리 (반복): 쏴아 하는 고른 소음 + 바닥·물웅덩이에 떨어지는 빗방울 톡톡 소리
    public static AudioClip Rain()
    {
        var hiss = Biquad.BandPass(2600, 0.5f);
        var body = Biquad.LowPass(1100);
        var drop = Biquad.BandPass(3400, 3f);
        float dropEnv = 0;
        return Make("rain", 10f, t =>
        {
            if (rng.NextDouble() < 0.0018) dropEnv = 0.35f + (float)rng.NextDouble() * 0.65f;
            dropEnv *= 0.993f;
            float swell = 0.85f + 0.15f * Mathf.Sin(2 * Mathf.PI * t / 5f);
            return (hiss.Process(Noise()) * 0.55f + body.Process(Noise()) * 0.35f) * swell + drop.Process(Noise()) * dropEnv * 1.4f;
        }, 0.8f, 1.2f);
    }

    public static AudioClip Wind()
    {
        float brown = 0;
        var bp = Biquad.BandPass(380, 0.6f);
        var lp = Biquad.LowPass(900);
        return Make("wind", 12f, t =>
        {
            brown = (brown + 0.02f * Noise()) / 1.02f;
            float gust = 0.55f + 0.3f * Mathf.Sin(2 * Mathf.PI * t / 6f) + 0.15f * Mathf.Sin(2 * Mathf.PI * t / 2.4f + 1);
            return (lp.Process(brown * 6) + bp.Process(Noise()) * 0.12f) * gust;
        }, 0.9f, 1.5f);
    }

    // 멀리서 들리는 신음 (녹음한 좀비 신음이 있으면 그것을 쓴다)
    public static AudioClip Groan()
    {
        var real = ZombieVoices.Groan();
        if (real != null) return real;
        int v = rng.Next(3);
        return Make("groan" + v, 1.9f, MakeVoice(70 + v * 14, 1.9f, 0.25f, new[] { 420f, 820f, 2400f }, 3f));
    }

    public static AudioClip Scream()
    {
        return Make("scream", 1.1f, MakeVoice(300, 1.1f, 0.6f, new[] { 900f, 1500f, 2900f }, 6f));
    }

    public static AudioClip Hurt()
    {
        return Make("hurt", 0.6f, MakeVoice(140, 0.6f, -0.3f, new[] { 600f, 1100f, 2600f }, 4f));
    }

    // 성대 진동(톱니파) + 포먼트 필터로 신음 소리를 만든다
    static System.Func<float, float> MakeVoice(float f0, float dur, float bend, float[] formants, float drive)
    {
        var f1 = Biquad.BandPass(formants[0], 4);
        var f2 = Biquad.BandPass(formants[1], 5);
        var f3 = Biquad.BandPass(formants[2], 6);
        float phase = 0;
        float vibRate = Rand(4, 7);
        return t =>
        {
            float u = t / dur;
            float f = f0 * (1 + bend * Mathf.Sin(Mathf.PI * u)) * (1 + 0.05f * Mathf.Sin(2 * Mathf.PI * vibRate * t) + 0.08f * Mathf.Sin(2 * Mathf.PI * 0.8f * t));
            phase += f / SR;
            float saw = 2 * (phase - Mathf.Floor(phase + 0.5f));
            float src = saw + Noise() * 0.35f;
            float env = Mathf.Clamp01(u * 8) * Mathf.Clamp01((1 - u) * 3);
            float y = f1.Process(src) + f2.Process(src) * 0.7f + f3.Process(src) * 0.3f;
            return (float)System.Math.Tanh(y * drive) * env;
        };
    }

    public static AudioClip Hit()
    {
        var lp = Biquad.LowPass(1200);
        return Make("hit", 0.3f, t => Mathf.Sin(2 * Mathf.PI * 85 * t) * Mathf.Exp(-t * 22) + lp.Process(Noise()) * Mathf.Exp(-t * 28));
    }

    public static AudioClip Swoosh()
    {
        float f = 600;
        Biquad bp = Biquad.BandPass(f, 2);
        int i = 0;
        return Make("swoosh", 0.28f, t =>
        {
            if (i++ % 64 == 0) bp = Biquad.BandPass(600 + 1600 * (t / 0.28f), 2);
            return bp.Process(Noise()) * Mathf.Sin(Mathf.PI * t / 0.28f);
        }, 0.6f);
    }

    public static AudioClip Creak()
    {
        var r1 = Biquad.BandPass(650, 12);
        var r2 = Biquad.BandPass(1350, 10);
        float phase = 0;
        return Make("creak", 0.9f, t =>
        {
            float rate = 28 + 22 * Mathf.Sin(2 * Mathf.PI * 1.3f * t) + Noise() * 6;
            phase += rate / SR;
            float imp = 0;
            if (phase >= 1) { phase -= 1; imp = 1; }
            float env = Mathf.Sin(Mathf.PI * t / 0.9f);
            return (r1.Process(imp) + r2.Process(imp) * 0.6f) * env;
        });
    }

    public static AudioClip Bang()
    {
        var lp = Biquad.LowPass(500);
        return Make("bang", 0.5f, t => (lp.Process(Noise()) * 2 + Mathf.Sin(2 * Mathf.PI * 55 * t)) * Mathf.Exp(-t * 14));
    }

    public static AudioClip Siren()
    {
        var lp = Biquad.LowPass(1500);
        float phase = 0;
        return Make("siren", 4f, t =>
        {
            float f = 480 + 260 * Mathf.Sin(2 * Mathf.PI * 0.5f * t - Mathf.PI / 2);
            phase += f / SR;
            float s = Mathf.Sin(2 * Mathf.PI * phase) + 0.3f * Mathf.Sin(4 * Mathf.PI * phase);
            return lp.Process(s) * Mathf.Clamp01(t * 3) * Mathf.Clamp01((4 - t) * 2);
        }, 0.7f);
    }

    public static AudioClip Hum()
    {
        var lp = Biquad.LowPass(300);
        return Make("hum", 2f, t =>
            Mathf.Sin(2 * Mathf.PI * 50 * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 100 * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 150 * t)
            + lp.Process(Noise()) * 0.8f * (0.8f + 0.2f * Mathf.Sin(2 * Mathf.PI * 12 * t)), 0.8f, 0.3f);
    }

    public static AudioClip Heartbeat()
    {
        return Make("heart", 1f, t =>
        {
            float lub = Mathf.Sin(2 * Mathf.PI * 55 * t) * Mathf.Exp(-t * 18);
            float t2 = t - 0.28f;
            float dub = t2 > 0 ? Mathf.Sin(2 * Mathf.PI * 48 * t2) * Mathf.Exp(-t2 * 22) * 0.7f : 0;
            return lub + dub;
        });
    }

    public static AudioClip Rustle()
    {
        var bp = Biquad.BandPass(2400, 1.5f);
        return Make("rustle", 0.25f, t => bp.Process(Noise()) * Mathf.Sin(Mathf.PI * t / 0.25f) * (0.6f + 0.4f * Mathf.Sin(t * 180)), 0.5f);
    }

    public static AudioClip Click()
    {
        var bp = Biquad.BandPass(3200, 2);
        return Make("click", 0.06f, t => bp.Process(Noise()) * Mathf.Exp(-t * 250), 0.5f);
    }

    public static AudioClip Clang()
    {
        return Make("clang", 0.8f, t =>
            (Mathf.Sin(2 * Mathf.PI * 523 * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * 1347 * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 2210 * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 3530 * t))
            * Mathf.Exp(-t * 7) + Noise() * Mathf.Exp(-t * 90), 0.6f);
    }

    public static AudioClip Bark()
    {
        return Make("bark", 0.35f, MakeVoice(380, 0.35f, -0.35f, new[] { 800f, 1600f, 3000f }, 7f));
    }

    // ---------- 적 소리 ----------
    // 낮은 웅웅거림만 있으면 방향을 알 수 없어서 300~3000Hz(쉰 소리·긁는 소리)를 일부러 두껍게 넣는다

    // 좀비 목소리는 녹음(ZombieVoices)을 먼저 쓰고, 없을 때만 합성한다
    public static AudioClip ZombieGroan()
    {
        var real = ZombieVoices.Groan();
        if (real != null) return real;
        int v = rng.Next(4);
        return Make("zgroan" + v, 1.7f, MakeRasp(105 + v * 18, 1.7f, 0.2f - v * 0.08f, new[] { 520f + v * 60, 1150f + v * 90, 2500f }, 0.9f, 5f), 0.95f);
    }

    public static AudioClip ZombieHurt() => ZombieVoices.Hurt() ?? Hurt();

    // 덤벼들 때 내는 짧은 소리 (녹음이 없으면 소리 없음)
    public static AudioClip ZombieAttack() => ZombieVoices.Attack();

    public static AudioClip ZombieScream()
    {
        var real = ZombieVoices.Alert();
        if (real != null) return real;
        int v = rng.Next(2);
        return Make("zscream" + v, 1.2f, MakeRasp(330 + v * 45, 1.2f, 0.45f, new[] { 950f, 1700f + v * 150, 3100f }, 1.2f, 6f), 0.95f);
    }

    // 구역질하며 꾸르륵거리는 소리
    public static AudioClip Retch()
    {
        return Make("retch", 0.75f, MakeRasp(170, 0.75f, 0.5f, new[] { 700f, 1400f, 2800f }, 1.6f, 5f), 0.95f);
    }

    // 들개 으르렁 (공격 전 경고)
    public static AudioClip Growl()
    {
        return Make("growl", 1.2f, MakeRasp(88, 1.2f, 0.06f, new[] { 520f, 1250f, 2700f }, 1.0f, 6f), 0.95f);
    }

    // 약탈자 고함 ("야!")
    public static AudioClip RaiderShout()
    {
        int v = rng.Next(2);
        return Make("rshout" + v, 0.7f, MakeVoice(150 + v * 25, 0.7f, 0.25f, new[] { 730f, 1150f + v * 100, 2500f }, 3.5f), 0.95f);
    }

    // 약탈자 기합·신음 (짧은 사람 목소리)
    public static AudioClip Grunt()
    {
        return Make("grunt", 0.3f, MakeVoice(125, 0.3f, -0.2f, new[] { 600f, 1050f, 2500f }, 4f), 0.9f);
    }

    // 좀비 발소리: 발을 질질 끄는 소리
    public static AudioClip Shuffle()
    {
        int v = rng.Next(3);
        float dur = 0.42f + v * 0.06f;
        var scrape = Biquad.BandPass(1300 + v * 300, 1.4f);
        var grit = Biquad.BandPass(3200, 2f);
        var lp = Biquad.LowPass(900);
        return Make("shuffle" + v, dur, t =>
        {
            float u = t / dur;
            float thump = lp.Process(Noise()) * Mathf.Exp(-t * 40) * 1.4f + Mathf.Sin(2 * Mathf.PI * 75 * t) * Mathf.Exp(-t * 35) * 0.5f;
            float drag = scrape.Process(Noise()) * Mathf.Sin(Mathf.PI * u) * (0.7f + 0.3f * Mathf.Sin(t * 90));
            float crackle = rng.NextDouble() < 0.004 ? 4f : 0f;
            return thump + drag * 1.2f + grit.Process(crackle + Noise() * 0.1f) * Mathf.Sin(Mathf.PI * u);
        }, 0.85f);
    }

    // 거대 좀비 발소리: 쿵 + 부서지는 소리
    public static AudioClip Stomp()
    {
        var lp = Biquad.LowPass(700);
        var bp = Biquad.BandPass(900, 1.2f);
        return Make("stomp", 0.4f, t =>
            Mathf.Sin(2 * Mathf.PI * 52 * t) * Mathf.Exp(-t * 16) + lp.Process(Noise()) * 1.5f * Mathf.Exp(-t * 22) + bp.Process(Noise()) * Mathf.Exp(-t * 30));
    }

    // 무언가를 퉤 뱉는 소리
    public static AudioClip Spit()
    {
        var bp = Biquad.BandPass(1500, 1.5f);
        return Make("spit", 0.25f, t => bp.Process(Noise()) * Mathf.Exp(-t * 24) + Mathf.Sin(2 * Mathf.PI * 140 * t) * Mathf.Exp(-t * 40) * 0.4f, 0.85f);
    }

    // 질척한 것이 터지는 소리
    public static AudioClip Splat()
    {
        var lp = Biquad.LowPass(2000);
        var bubble = Biquad.BandPass(1800, 3f);
        return Make("splat", 0.4f, t =>
        {
            float pop = rng.NextDouble() < 0.006 ? 3f : 0f;
            return lp.Process(Noise()) * Mathf.Exp(-t * 18) + bubble.Process(pop + Noise() * 0.2f) * Mathf.Exp(-t * 6);
        }, 0.85f);
    }

    // 새총 고무줄을 당기는 소리: 고무가 늘어나며 끼이익 하고 가죽 주머니가 사각거린다 (원거리 좀비가 겨눌 때)
    public static AudioClip SlingStretch()
    {
        const float dur = 0.6f;
        var r1 = Biquad.BandPass(950, 10);
        var r2 = Biquad.BandPass(2100, 8);
        var leather = Biquad.BandPass(3000, 1.5f);
        float phase = 0;
        return Make("slingstretch", dur, t =>
        {
            float u = t / dur;
            float rate = 35 + 120 * u + Noise() * 8; // 당길수록 빨라지는 마찰 떨림
            phase += rate / SR;
            float imp = 0;
            if (phase >= 1) { phase -= 1; imp = 1; }
            float env = Mathf.Clamp01((u - 0.12f) * 5) * Mathf.Clamp01((1 - u) * 8);
            float creak = (r1.Process(imp) + r2.Process(imp) * 0.5f) * 30;
            return (creak + leather.Process(Noise()) * 0.3f) * env;
        }, 0.6f);
    }

    // 새총 고무줄을 놓는 소리: 탁! 튕기며 짧게 부르르 떨린다
    public static AudioClip SlingSnap()
    {
        var click = Biquad.BandPass(2800, 1.5f);
        var air = Biquad.BandPass(900, 1.2f);
        float phase = 0;
        return Make("slingsnap", 0.3f, t =>
        {
            float f = 110 + 320 * Mathf.Exp(-t * 45); // 튕기며 뚝 떨어지는 음
            phase += f / SR;
            float wobble = 1 + 0.35f * Mathf.Sin(2 * Mathf.PI * 34 * t) * Mathf.Exp(-t * 10);
            float thwap = Mathf.Sin(2 * Mathf.PI * phase) * Mathf.Exp(-t * 22) * wobble;
            float snap = click.Process(Noise()) * Mathf.Exp(-t * 140) * 2.5f + (t < 0.0015f ? 1f : 0f);
            float whoosh = air.Process(Noise()) * Mathf.Exp(-t * 30) * 0.6f;
            return thwap + snap + whoosh;
        }, 0.9f);
    }

    // 돌멩이가 벽·바닥에 부딪히는 소리: 딱 + 둔탁한 쿵, 작게 한 번 더 튄다
    public static AudioClip StoneHit()
    {
        int v = rng.Next(3);
        return Make("stonehit" + v, 0.3f, MakeStoneHit(v), 0.9f);
    }

    static System.Func<float, float> MakeStoneHit(int v)
    {
        var r1 = Biquad.BandPass(1900 + v * 380, 9);
        var r2 = Biquad.BandPass(3400 + v * 450, 7);
        var crack = Biquad.BandPass(4500, 1f);
        var lp = Biquad.LowPass(1000);
        float bounce = 0.055f + v * 0.02f;
        float thudF = 90 + v * 14;
        return t =>
        {
            float t2 = t - bounce;
            float burst = Noise() * (Mathf.Exp(-t * 500) + (t2 > 0 ? 0.35f * Mathf.Exp(-t2 * 600) : 0));
            float tock = r1.Process(burst) + r2.Process(burst) * 0.7f;
            float thud = Mathf.Sin(2 * Mathf.PI * thudF * t) * Mathf.Exp(-t * 38);
            float grit = lp.Process(Noise()) * Mathf.Exp(-t * 32);
            return tock * 10 + crack.Process(burst) * 1.5f + thud * 0.55f + grit * 0.8f;
        };
    }

    // 개가 무는 소리 (이빨 딱)
    public static AudioClip Snap()
    {
        var bp = Biquad.BandPass(2200, 2f);
        return Make("snap", 0.15f, t => bp.Process(Noise()) * Mathf.Exp(-t * 60) + (t < 0.004f ? 1f : 0f), 0.8f);
    }

    // 거친 목소리: 톱니파 + 숨소리를 포먼트로 거르고, 고음 쉰 소리와 그르렁거리는 떨림을 더한다
    static System.Func<float, float> MakeRasp(float f0, float dur, float bend, float[] formants, float breath, float drive)
    {
        var f1 = Biquad.BandPass(formants[0], 3);
        var f2 = Biquad.BandPass(formants[1], 4);
        var f3 = Biquad.BandPass(formants[2], 5);
        var hiss = Biquad.BandPass(formants[2] * 0.8f, 1.2f);
        float phase = 0, gurglePhase = 0, jitter = 0;
        float gurgleRate = Rand(14, 26);
        return t =>
        {
            float u = t / dur;
            if (rng.NextDouble() < 0.0005) jitter = Rand(-0.1f, 0.1f);
            float f = f0 * (1 + bend * Mathf.Sin(Mathf.PI * u) + jitter) * (1 + 0.06f * Mathf.Sin(2 * Mathf.PI * 5.5f * t));
            phase += f / SR;
            float saw = 2 * (phase - Mathf.Floor(phase + 0.5f));
            gurglePhase += gurgleRate / SR;
            float gurgle = 0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * gurglePhase);
            float src = saw + Noise() * breath;
            float y = f1.Process(src) + f2.Process(src) * 0.9f + f3.Process(src) * 0.6f;
            float rasp = hiss.Process(Noise()) * (0.5f + 0.5f * Mathf.Abs(saw)) * breath;
            float env = Mathf.Clamp01(u * 6) * Mathf.Clamp01((1 - u) * 2.5f);
            return (float)System.Math.Tanh((y + rasp * 0.8f) * drive) * env * gurgle;
        };
    }

    // ---------- 전투·이동·출입 소리 ----------

    // 불이 확 붙는 소리 (훅 하는 바람 + 낮은 울림)
    public static AudioClip Ignite()
    {
        var bp = Biquad.BandPass(900, 0.8f);
        var lp = Biquad.LowPass(300);
        return Make("ignite", 0.7f, t =>
        {
            float env = Mathf.Clamp01(t * 14) * Mathf.Exp(-t * 4.5f);
            return bp.Process(Noise()) * env * 1.2f + lp.Process(Noise()) * env * 2.5f;
        }, 0.8f);
    }

    // 타닥타닥 타는 소리 (반복 재생). 낮은 화르르 소리 위에 불규칙한 탁 소리
    public static AudioClip Crackle()
    {
        var roar = Biquad.LowPass(500);
        var pop = Biquad.BandPass(2600, 1.5f);
        float popEnv = 0;
        return Make("crackle", 2f, t =>
        {
            if (rng.NextDouble() < 0.0016) popEnv = Rand(0.4f, 1f);
            popEnv *= 0.993f;
            return roar.Process(Noise()) * (1.4f + 0.5f * Mathf.Sin(2 * Mathf.PI * 1.7f * t)) + pop.Process(Noise()) * popEnv * 3f;
        }, 0.6f, 0.25f);
    }

    // 살을 때리는 묵직한 타격음 (퍽): 낮은 쿵 + 짧은 찰싹 + 젖은 소리
    public static AudioClip FleshHit()
    {
        var slap = Biquad.BandPass(1400, 1.2f);
        var wet = Biquad.BandPass(500, 2f);
        return Make("fleshhit", 0.28f, t =>
            Mathf.Sin(2 * Mathf.PI * (70 + 40 * Mathf.Exp(-t * 30)) * t) * Mathf.Exp(-t * 20) * 1.4f
            + slap.Process(Noise()) * Mathf.Exp(-t * 70) * 2f
            + wet.Process(Noise()) * Mathf.Exp(-t * 14) * 0.6f, 0.95f);
    }

    // 쓰러뜨렸을 때 더 무거운 마무리 타격음
    public static AudioClip KillHit()
    {
        var crunch = Biquad.BandPass(800, 1.4f);
        return Make("killhit", 0.45f, t =>
            Mathf.Sin(2 * Mathf.PI * (55 + 50 * Mathf.Exp(-t * 18)) * t) * Mathf.Exp(-t * 9) * 1.5f
            + crunch.Process(Noise()) * Mathf.Exp(-t * 22) * 1.8f, 0.95f);
    }

    // 점프할 때 옷이 스치는 소리와 짧은 숨
    public static AudioClip JumpEffort()
    {
        var bp = Biquad.BandPass(1100, 1.2f);
        return Make("jump", 0.22f, t => bp.Process(Noise()) * Mathf.Sin(Mathf.PI * t / 0.22f) * (0.7f + 0.3f * Mathf.Sin(t * 90)), 0.5f);
    }

    // 착지: 발이 바닥을 쿵 디디는 소리 + 자갈 긁힘
    public static AudioClip Land()
    {
        var lp = Biquad.LowPass(900);
        var grit = Biquad.BandPass(3000, 1.5f);
        return Make("land", 0.3f, t =>
            Mathf.Sin(2 * Mathf.PI * 60 * t) * Mathf.Exp(-t * 26) * 1.2f + lp.Process(Noise()) * Mathf.Exp(-t * 24) * 1.4f
            + grit.Process(Noise()) * Mathf.Exp(-t * 40) * 0.6f, 0.85f);
    }

    // 무거운 철문·해치가 덜컥 걸리는 소리: 빗장이 철컥 → 묵직한 쿵 → 쇠가 우는 짧은 울림
    public static AudioClip Latch()
    {
        var click = Biquad.BandPass(2800, 3f);
        var body = Biquad.LowPass(240);
        var ring1 = Biquad.BandPass(410, 30f);
        var ring2 = Biquad.BandPass(1130, 25f);
        return Make("latch", 1.1f, t =>
        {
            float c1 = t < 0.012f ? Noise() : 0;
            float t2 = t - 0.09f;
            float c2 = t2 > 0 && t2 < 0.01f ? Noise() : 0;
            float t3 = t - 0.16f;
            float thud = t3 > 0 ? (Mathf.Sin(2 * Mathf.PI * 52 * t3) * 1.6f + body.Process(Noise()) * 2.2f) * Mathf.Exp(-t3 * 11) : 0;
            float imp = t3 > 0 && t3 < 0.004f ? 1 : 0;
            float ring = (ring1.Process(imp + c2) * 18 + ring2.Process(imp) * 12) * (t3 > 0 ? Mathf.Exp(-t3 * 3.5f) : 1);
            return click.Process(c1 + c2) * 2.2f + thud + ring;
        }, 0.95f);
    }

    // 수첩·양피지를 펼치는 소리: 종이가 바스락 구겨지며 펴지는 짧은 소리 여러 번
    public static AudioClip PaperUnfold()
    {
        var hi = Biquad.BandPass(3800, 0.9f);
        var lo = Biquad.BandPass(1200, 1.2f);
        float crinkle = 0;
        return Make("paperunfold", 0.75f, t =>
        {
            if (rng.NextDouble() < 0.004) crinkle = Rand(0.5f, 1f);
            crinkle *= 0.996f;
            float env = Mathf.Clamp01(t * 12) * Mathf.Clamp01((0.75f - t) * 3f);
            float flap = Mathf.Exp(-Mathf.Pow((t - 0.45f) / 0.06f, 2)) * 0.8f; // 종이가 펄럭 펴지는 순간
            return (hi.Process(Noise()) * crinkle * 1.6f + lo.Process(Noise()) * (0.25f + flap)) * env;
        }, 0.7f);
    }

    // 펜이 종이를 긁는 소리 (사각): 거친 고음 잡음이 빠르게 떨린다. 여러 번 이어 울려 글 쓰는 소리를 낸다
    public static AudioClip PenScratch()
    {
        var bp = Biquad.BandPass(5200, 1.4f);
        var body = Biquad.BandPass(2400, 2f);
        float grain = 0;
        return Make("penscratch", 0.11f, t =>
        {
            if (rng.NextDouble() < 0.02) grain = Rand(0.3f, 1f);
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.11f));
            float wobble = 0.6f + 0.4f * Mathf.Sin(t * 2 * Mathf.PI * 70);
            return (bp.Process(Noise()) * (0.6f + grain) + body.Process(Noise()) * 0.35f) * env * wobble;
        }, 0.55f);
    }

    // 잠잘 때 느린 숨소리 (들숨·날숨 한 번)
    public static AudioClip SleepBreath()
    {
        var bp = Biquad.BandPass(700, 0.9f);
        return Make("sleepbreath", 3.2f, t =>
        {
            float inhale = t < 1.3f ? Mathf.Sin(Mathf.PI * t / 1.3f) : 0;
            float t2 = t - 1.5f;
            float exhale = t2 > 0 && t2 < 1.6f ? Mathf.Sin(Mathf.PI * t2 / 1.6f) * 0.8f : 0;
            return bp.Process(Noise()) * (inhale * 0.7f + exhale);
        }, 0.5f);
    }

    // 적 소리용 감쇠 곡선 (가로축 = 거리 / maxDistance). 15~20m에서도 또렷하고 maxDistance에서 사라진다
    static AnimationCurve enemyRolloff;

    static AnimationCurve EnemyRolloff()
    {
        if (enemyRolloff != null) return enemyRolloff;
        enemyRolloff = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.08f, 1f), new Keyframe(0.3f, 0.72f),
            new Keyframe(0.5f, 0.45f), new Keyframe(0.75f, 0.16f), new Keyframe(1f, 0f));
        for (int i = 1; i < enemyRolloff.length - 1; i++) enemyRolloff.SmoothTangents(i, 0);
        return enemyRolloff;
    }

    // 좀비·들개 같은 적 소리용 AudioSource 설정: 완전 3D, 방향이 또렷하게(spread 0), 도플러 없음.
    // tag면 계속 켜 두는 소리로 보고 '적 소리' 음량을 따른다
    public static void Setup3D(AudioSource src, float maxDistance = 36f, int priority = 64, bool tag = true)
    {
        if (tag) AudioCategory.Tag(src, SoundCategory.Enemy);
        src.playOnAwake = false;
        src.spatialBlend = 1;
        src.spread = 0;
        src.dopplerLevel = 0;
        src.priority = priority;
        src.minDistance = 1f;
        src.maxDistance = maxDistance;
        src.rolloffMode = AudioRolloffMode.Custom;
        src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, EnemyRolloff());
    }

    // PlayAt과 같지만 적 소리용 3D 설정을 쓴다
    public static AudioSource PlayAt3D(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDistance = 36f)
    {
        var src = PlayAt(clip, pos, volume, pitch, 1f, maxDistance, SoundCategory.Enemy);
        Setup3D(src, maxDistance, 64, false);
        return src;
    }

    public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f, SoundCategory category = SoundCategory.Effects)
    {
        PlayAt(clip, Vector3.zero, volume, pitch, 0f, 35f, category);
    }

    // 한 번 울리고 사라지는 소리. 음량에 소리 종류 음량(설정)을 곱한다
    public static AudioSource PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, float spatial = 1f, float maxDistance = 35f, SoundCategory category = SoundCategory.Effects)
    {
        volume *= GameSettings.CategoryVolume(category);
        var go = new GameObject("SFX_" + clip.name);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = spatial;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1.5f;
        src.maxDistance = maxDistance;
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        return src;
    }
}
