using System.Collections.Generic;
using UnityEngine;

// 방공호 장치 이펙트 소리를 코드로 합성한다 (FurnaceAudio와 같은 방식):
// 무거운 것이 바닥에 쾅 놓이는 소리, 망치질, 불꽃이 튀는 치직 소리, 전기가 튀는 지직 소리, 고기가 지글거리는 소리
public static class EffectAudio
{
    const int SR = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static readonly System.Random rng = new System.Random(7071);

    static float Noise() => (float)(rng.NextDouble() * 2 - 1);
    static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    class Filter
    {
        float b0, b1, b2, a1, a2, x1, x2, y1, y2;

        public static Filter BandPass(float f, float q)
        {
            float w = 2 * Mathf.PI * f / SR, alpha = Mathf.Sin(w) / (2 * q), a0 = 1 + alpha;
            return new Filter { b0 = alpha / a0, b1 = 0, b2 = -alpha / a0, a1 = -2 * Mathf.Cos(w) / a0, a2 = (1 - alpha) / a0 };
        }

        public static Filter LowPass(float f, float q = 0.707f)
        {
            float w = 2 * Mathf.PI * f / SR, alpha = Mathf.Sin(w) / (2 * q), cos = Mathf.Cos(w), a0 = 1 + alpha;
            return new Filter { b0 = (1 - cos) / 2 / a0, b1 = (1 - cos) / a0, b2 = (1 - cos) / 2 / a0, a1 = -2 * cos / a0, a2 = (1 - alpha) / a0 };
        }

        public static Filter HighPass(float f, float q = 0.707f)
        {
            float w = 2 * Mathf.PI * f / SR, alpha = Mathf.Sin(w) / (2 * q), cos = Mathf.Cos(w), a0 = 1 + alpha;
            return new Filter { b0 = (1 + cos) / 2 / a0, b1 = -(1 + cos) / a0, b2 = (1 + cos) / 2 / a0, a1 = -2 * cos / a0, a2 = (1 - alpha) / a0 };
        }

        public float Process(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    // 짧게 꺼지는 잡음 알갱이가 초당 perSecond번 터진다 (불꽃·기름이 튀는 소리)
    class Pops
    {
        readonly Filter tone;
        readonly float rate;
        float pop;

        public Pops(float freq, float q, float perSecond)
        {
            tone = Filter.BandPass(freq, q);
            rate = perSecond / SR;
        }

        public float Next(float decay)
        {
            if (rng.NextDouble() < rate) pop = Rand(0.4f, 1.6f);
            float y = tone.Process(pop * Noise());
            pop *= decay;
            return y;
        }
    }

    static AudioClip Make(string name, float seconds, System.Func<float, float> sample, float peak)
    {
        if (cache.TryGetValue(name, out var clip) && clip != null) return clip;
        int n = (int)(seconds * SR);
        var data = new float[n];
        float max = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            data[i] = sample(i / (float)SR);
            max = Mathf.Max(max, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < n; i++) data[i] = data[i] / max * peak;
        clip = AudioClip.Create(name, n, 1, SR, false);
        clip.SetData(data, 0);
        cache[name] = clip;
        return clip;
    }

    // 쾅: 무거운 쇠붙이가 바닥에 놓인다. 음이 떨어지는 낮은 쿵 + 둔탁한 잡음 + 쇠가 울리는 여운 + 흩날리는 먼지의 쉬익
    public static AudioClip Slam()
    {
        const float dur = 1.8f;
        var thud = Filter.LowPass(170, 0.8f);
        var rattle = Filter.BandPass(430, 2.5f);
        var dust = Filter.BandPass(2300, 0.8f);
        var hp = Filter.HighPass(35);
        float phase = 0;
        return Make("fx_slam", dur, t =>
        {
            float f = 64f * Mathf.Exp(-t * 6f) + 38f;
            phase += 2 * Mathf.PI * f / SR;
            float boom = Mathf.Sin(phase) * Mathf.Exp(-t * 5.5f) * 1.6f;
            float crack = thud.Process(Noise()) * Mathf.Exp(-t * 28f) * 3f;
            float ring = (Mathf.Sin(2 * Mathf.PI * 183 * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 412 * t + 0.3f) + 0.35f * Mathf.Sin(2 * Mathf.PI * 731 * t))
                         * Mathf.Exp(-t * 2.6f) * 0.25f * Mathf.Clamp01(t * 60);
            float hiss = dust.Process(Noise()) * Mathf.Exp(-t * 2.2f) * 0.35f * Mathf.Clamp01(t * 8);
            float body = rattle.Process(Noise()) * Mathf.Exp(-t * 9f) * 0.5f;
            return hp.Process(boom + crack + ring + hiss + body);
        }, 0.95f);
    }

    // 망치질: 쇠 머리가 쇠판을 때리는 날카로운 땅 + 상 전체가 울리는 둔탁한 쿵
    public static AudioClip HammerHit()
    {
        const float dur = 0.6f;
        var wood = Filter.LowPass(240, 1.1f);
        var click = Filter.BandPass(3200, 1.5f);
        var hp = Filter.HighPass(50);
        float phase = 0;
        return Make("fx_hammer", dur, t =>
        {
            float f = 140f * Mathf.Exp(-t * 18f) + 70f;
            phase += 2 * Mathf.PI * f / SR;
            float thud = Mathf.Sin(phase) * Mathf.Exp(-t * 16f) * 1.3f + wood.Process(Noise()) * Mathf.Exp(-t * 40f) * 2.5f;
            float ring = (Mathf.Sin(2 * Mathf.PI * 1180 * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 2470 * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 3900 * t))
                         * Mathf.Exp(-t * 14f) * 0.35f;
            float snap = click.Process(Noise()) * Mathf.Exp(-t * 90f) * 1.5f;
            return hp.Process(thud + ring + snap);
        }, 0.9f);
    }

    // 불꽃이 튄다: 잘게 터지는 치직 소리 + 잦아드는 쉬익
    public static AudioClip Sparks()
    {
        const float dur = 1.1f;
        var hiss = Filter.BandPass(5200, 0.7f);
        var fine = new Pops(3800, 1.8f, 90f);
        var coarse = new Pops(1900, 1.4f, 25f);
        return Make("fx_sparks", dur, t =>
        {
            float env = Mathf.Exp(-t * 3.2f);
            return hiss.Process(Noise()) * 0.35f * env + fine.Next(0.93f) * 1.6f * env + coarse.Next(0.96f) * env;
        }, 0.7f);
    }

    // 전기가 튄다: 4ms마다 끊겼다 이어지는 지직 + 전원 주파수의 윙 + 높은 삐 (variant 0~2로 조금씩 다르다)
    public static AudioClip Zap(int variant = 0)
    {
        variant = Mathf.Clamp(variant, 0, 2);
        float dur = 0.36f + variant * 0.06f;
        float toneFreq = 2600f + variant * 700f;
        var hp = Filter.HighPass(900);
        var tone = Filter.BandPass(toneFreq, 3f);
        float gate = 0;
        int k = 0;
        return Make("fx_zap" + variant, dur, t =>
        {
            if (k++ % 180 == 0) gate = rng.NextDouble() < 0.55 ? Rand(0.3f, 1f) : 0f;
            float buzz = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 60 * t)) * 0.5f + Mathf.Sin(2 * Mathf.PI * 120 * t) * 0.3f;
            float crackle = hp.Process(Noise()) * gate;
            float whine = tone.Process(Noise()) * 1.5f;
            float env = Mathf.Exp(-t * 7f) * Mathf.Clamp01(t * 200);
            return (crackle * (1f + buzz) + whine * gate) * env + Noise() * Mathf.Exp(-t * 300) * 2f;
        }, 0.8f);
    }

    // 고기가 굽힌다: 지글거리는 쉬익 + 가끔 튀는 기름
    public static AudioClip Sizzle()
    {
        const float dur = 2.4f;
        var hiss = Filter.BandPass(4200, 0.5f);
        var pops = new Pops(2600, 1.2f, 30f);
        float sway = 1;
        int k = 0;
        return Make("fx_sizzle", dur, t =>
        {
            if (k++ % 2205 == 0) sway = Rand(0.5f, 1f);
            float env = Mathf.Clamp01(t * 6) * Mathf.Clamp01((dur - t) * 1.5f);
            return (hiss.Process(Noise()) * 0.5f * sway + pops.Next(0.95f)) * env;
        }, 0.55f);
    }
}
