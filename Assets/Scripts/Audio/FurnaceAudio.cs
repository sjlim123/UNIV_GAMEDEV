using System.Collections.Generic;
using UnityEngine;

// 방공호 화로 소리를 코드로 합성한다: 평소의 작은 탁탁 소리, 녹이는 동안의 불길 소리, 불을 키울 때 확 붙는 소리
public static class FurnaceAudio
{
    const int SR = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static readonly System.Random rng = new System.Random(5150);

    static float Noise() => (float)(rng.NextDouble() * 2 - 1);
    static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    class Filter
    {
        float b0, b1, b2, a1, a2, x1, x2, y1, y2;

        public static Filter BandPass(float f, float q)
        {
            var filter = new Filter();
            filter.SetBandPass(f, q);
            return filter;
        }

        // 주파수만 바꾼다. 지난 샘플 상태는 그대로 두어 소리가 끊기지(딸깍거리지) 않는다
        public void SetBandPass(float f, float q)
        {
            float w = 2 * Mathf.PI * f / SR, alpha = Mathf.Sin(w) / (2 * q), a0 = 1 + alpha;
            b0 = alpha / a0; b1 = 0; b2 = -alpha / a0; a1 = -2 * Mathf.Cos(w) / a0; a2 = (1 - alpha) / a0;
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

    // ProceduralAudio.Make와 같은 방식: 만들어 두고 다시 쓴다. loopFade > 0이면 끝을 앞에 겹쳐 자연스럽게 반복된다
    static AudioClip Make(string name, float seconds, System.Func<float, float> sample, float peak, float loopFade = 0f)
    {
        if (cache.TryGetValue(name, out var clip) && clip != null) return clip;
        int n = (int)(seconds * SR);
        int fade = (int)(loopFade * SR);
        var data = new float[n + fade];
        for (int i = 0; i < data.Length; i++) data[i] = sample(i / (float)SR);
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

    // 장작이 타며 튀는 소리 한 번: 짧게 꺼지는 잡음을 대역 필터로 거른다
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

    // 평소(반복): 숯불이 낮게 웅웅거리고 가끔 탁탁 튄다
    public static AudioClip Crackle()
    {
        var bed = Filter.LowPass(260);
        var air = Filter.BandPass(900, 0.6f);
        var hp = Filter.HighPass(70);
        var small = new Pops(2400, 1.4f, 7f);
        var big = new Pops(1100, 1.1f, 1.2f);
        float brown = 0, sway = 0.7f, swayTarget = 0.7f;
        int k = 0;
        return Make("furnace_crackle", 5f, t =>
        {
            brown = (brown + 0.03f * Noise()) / 1.02f;
            if (k++ % 4410 == 0) swayTarget = Rand(0.45f, 1f);
            sway += (swayTarget - sway) * 0.0004f;
            float glow = hp.Process(bed.Process(brown * 5f)) * sway + air.Process(Noise()) * 0.05f * sway;
            return glow + small.Next(0.965f) * 1.3f + big.Next(0.985f) * 1.1f;
        }, 0.6f, 0.4f);
    }

    // 녹이는 동안(반복): 불길이 세게 일렁이며 쏴아 하고 빨려 올라가는 소리 + 잦은 탁탁 소리
    public static AudioClip Roar()
    {
        var body = Filter.LowPass(480, 0.9f);
        var rumble = Filter.BandPass(110, 1.2f);
        var draft = Filter.BandPass(1300, 0.7f);
        var hp = Filter.HighPass(45);
        var pops = new Pops(2000, 1.2f, 16f);
        float brown = 0;
        return Make("furnace_roar", 3f, t =>
        {
            brown = (brown + 0.04f * Noise()) / 1.015f;
            // 불길이 숨 쉬듯 커졌다 작아졌다 한다
            float surge = 0.7f + 0.2f * Mathf.Sin(2 * Mathf.PI * t / 1.5f) + 0.1f * Mathf.Sin(2 * Mathf.PI * t / 0.6f + 1.3f);
            float roar = hp.Process(body.Process(Noise()) * 1.4f + rumble.Process(brown * 6f) * 2f);
            float whoosh = draft.Process(Noise()) * 0.35f;
            return (roar + whoosh) * surge + pops.Next(0.97f) * 1.2f;
        }, 0.8f, 0.4f);
    }

    // 녹이기를 시작할 때: 땔감에 불이 확 붙는 소리 (쿵 + 높아지는 쏴아)
    public static AudioClip Ignite()
    {
        const float dur = 1.1f;
        var lp = Filter.LowPass(380);
        var pops = new Pops(1800, 1.3f, 22f);
        var sweep = Filter.BandPass(300, 1.1f);
        int k = 0;
        return Make("furnace_ignite", dur, t =>
        {
            float u = t / dur;
            if (k++ % 64 == 0) sweep.SetBandPass(300 + 1100 * Mathf.Sqrt(u), 1.1f);
            float thump = (Mathf.Sin(2 * Mathf.PI * 62 * t) * 0.8f + lp.Process(Noise()) * 1.6f) * Mathf.Exp(-t * 9);
            float whoosh = sweep.Process(Noise()) * Mathf.Clamp01(u * 12) * Mathf.Clamp01((1 - u) * 2.2f);
            return thump + whoosh * 1.4f + pops.Next(0.97f) * Mathf.Clamp01((1 - u) * 2);
        }, 0.85f);
    }
}
