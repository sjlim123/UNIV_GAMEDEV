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

    public static AudioClip Groan()
    {
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

    public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        PlayAt(clip, Vector3.zero, volume, pitch, 0f);
    }

    public static AudioSource PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, float spatial = 1f, float maxDistance = 35f)
    {
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
