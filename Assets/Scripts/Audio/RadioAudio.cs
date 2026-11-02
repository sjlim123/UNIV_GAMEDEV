using System.Collections.Generic;
using UnityEngine;

// 방공호 라디오(잡음·튜닝·끊긴 목소리)와 방폭문(모터·쿵) 소리를 코드로 합성한다
public static class RadioAudio
{
    const int SR = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static readonly System.Random rng = new System.Random(4242);

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

    // ---------- 라디오 ----------

    // 반복 재생하는 잡음: 쉬익 하는 소리 + 탁탁 튀는 소리, 신호가 약해졌다 강해졌다 한다
    public static AudioClip Static()
    {
        var hp = Filter.HighPass(300);
        var lp = Filter.LowPass(4500);
        var band = Filter.BandPass(1700, 0.5f);
        var popFilter = Filter.BandPass(2600, 1.2f);
        float fade = 0.8f, fadeTarget = 0.8f, pop = 0;
        int k = 0;
        return Make("radio_static", 4f, t =>
        {
            if (k++ % 2205 == 0) fadeTarget = Rand(0.45f, 1f);
            fade += (fadeTarget - fade) * 0.0008f;
            float hiss = lp.Process(hp.Process(Noise())) * 0.5f + band.Process(Noise()) * 0.9f;
            if (rng.NextDouble() < 0.0004) pop = Rand(0.8f, 2f);
            float crackle = popFilter.Process(pop * Noise());
            pop *= 0.985f;
            // 다른 방송이 섞여 드는 듯한 희미한 휘파람
            float whine = 0.05f * Mathf.Sin(2 * Mathf.PI * 1180 * t) * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 0.5f * t));
            return hiss * fade + crackle * 1.5f + whine;
        }, 0.55f, 0.4f);
    }

    // 켤 때 다이얼을 돌리는 소리: 높은 휘파람이 내려오며 주파수가 맞는다
    public static AudioClip Tune()
    {
        const float dur = 1.4f;
        var band = Filter.BandPass(1500, 0.7f);
        float phase = 0;
        return Make("radio_tune", dur, t =>
        {
            float u = t / dur;
            float f = 2600 * Mathf.Exp(-u * 3.2f) + 300 + 180 * Mathf.Sin(2 * Mathf.PI * 2.3f * t);
            phase += f / SR;
            float whistle = Mathf.Sin(2 * Mathf.PI * phase) * Mathf.Clamp01((0.95f - u) * 4) * 0.5f;
            float hiss = band.Process(Noise()) * (0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * 7 * t));
            return (whistle + hiss) * Mathf.Clamp01(t * 20) * Mathf.Clamp01((1 - u) * 6);
        }, 0.6f);
    }

    // 알아들을 수 없게 뭉개진 사람 목소리 (변형 3가지)
    public static AudioClip Garble()
    {
        int v = rng.Next(3);
        return Make("radio_voice" + v, 2.4f, MakeGarble(v, 2.4f), 0.75f);
    }

    static System.Func<float, float> MakeGarble(int v, float dur)
    {
        var f1 = Filter.BandPass(600, 3);
        var f2 = Filter.BandPass(1400, 4);
        var f3 = Filter.BandPass(2500, 5);
        var hp = Filter.HighPass(350);
        var lp = Filter.LowPass(2800);
        var hissBand = Filter.BandPass(1800, 0.6f);
        float f0 = 115 + v * 18, phase = 0, syll = 0, syllRate = Rand(3.6f, 5f), drop = 1, dropSmooth = 1;
        int lastSyll = -1, k = 0;
        return t =>
        {
            float u = t / dur;
            // 음절마다 모음(포먼트)을 바꿔 말하는 것처럼 들리게 한다
            syll += syllRate / SR;
            int s = (int)syll;
            if (s != lastSyll)
            {
                lastSyll = s;
                f1 = Filter.BandPass(Rand(380, 850), 3);
                f2 = Filter.BandPass(Rand(950, 2100), 4);
            }
            float f = f0 * (1 + 0.12f * Mathf.Sin(2 * Mathf.PI * 0.7f * t + v) + 0.05f * Mathf.Sin(2 * Mathf.PI * 3.1f * t));
            phase += f / SR;
            float saw = 2 * (phase - Mathf.Floor(phase + 0.5f));
            float env = Mathf.Sin(Mathf.PI * (syll - s));
            env *= env;
            // 신호가 가끔 뚝 끊긴다
            if (k++ % 4410 == 0) drop = rng.NextDouble() < 0.2 ? 0.08f : 1f;
            dropSmooth += (drop - dropSmooth) * 0.004f;
            float voiced = f1.Process(saw) + f2.Process(saw) * 0.6f + f3.Process(saw) * 0.25f;
            float y = lp.Process(hp.Process(voiced * env * dropSmooth));
            y = (float)System.Math.Tanh(y * 3f);
            float hiss = hissBand.Process(Noise()) * 0.2f;
            return (y + hiss) * Mathf.Clamp01(u * 15) * Mathf.Clamp01((1 - u) * 8);
        };
    }

    // ---------- 방폭문 ----------

    // 문이 움직이는 동안 반복: 낮은 모터 웅웅거림 + 쇠 긁히는 소리 + 톱니 덜컹거림
    public static AudioClip DoorGrind()
    {
        var rumble = Filter.LowPass(200);
        var scrape = Filter.BandPass(850, 2.5f);
        var squeal = Filter.BandPass(2100, 6f);
        float phase = 0, chatter = 0.6f, jitter = 0.6f;
        int k = 0;
        return Make("blastdoor_grind", 2f, t =>
        {
            phase += (48 + 3 * Mathf.Sin(2 * Mathf.PI * 1.5f * t)) / SR;
            float saw = 2 * (phase - Mathf.Floor(phase + 0.5f));
            float motor = rumble.Process(saw) * 1.3f;
            if (k++ % 441 == 0) jitter = Rand(0.3f, 1.2f);
            chatter += (jitter - chatter) * 0.002f;
            float grind = scrape.Process(Noise()) * chatter + squeal.Process(Noise()) * 0.35f * chatter * chatter;
            float teeth = Mathf.Pow(Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * 11 * t)), 40) * Noise() * 0.8f;
            return motor + grind * 0.8f + teeth;
        }, 0.8f, 0.25f);
    }

    // 문이 끝까지 열리거나 닫힐 때 나는 묵직한 쿵 소리
    public static AudioClip DoorBoom()
    {
        var lp = Filter.LowPass(260);
        float phase = 0;
        return Make("blastdoor_boom", 1.6f, t =>
        {
            phase += (38 + 20 * Mathf.Exp(-t * 8)) / SR;
            float thud = Mathf.Sin(2 * Mathf.PI * phase) * Mathf.Exp(-t * 3.5f);
            float body = lp.Process(Noise()) * 2.5f * Mathf.Exp(-t * 6);
            float ring = (Mathf.Sin(2 * Mathf.PI * 187 * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 431 * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 977 * t)) * 0.35f * Mathf.Exp(-t * 2.2f);
            return thud + body + ring;
        }, 0.95f);
    }
}
