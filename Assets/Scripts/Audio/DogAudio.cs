using System.Collections.Generic;
using UnityEngine;

// 들개(개좀비) 소리를 코드로 합성한다: 짖기·으르렁·깨갱·낑낑·물 때의 으르렁·킁킁.
// 성대 모델(Throat): 성문 파형에 주기마다 세기가 다른 거친 떨림(roughness)과 숨소리(breath)를 섞어 목·입의 포먼트로 거른다.
// 좀비 개라 보통 개보다 낮고 거칠고 꾸르륵거린다. 녹음 파일 없이 전부 코드로 만든다 (저작권 걱정 없음)
public static class DogAudio
{
    const int SR = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static readonly System.Random rng = new System.Random(9090);

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

    // 성대·목·입: f0로 성문이 열렸다 닫히고(톱니를 둥글린 파형), 주기마다 세기가 달라 거칠고(rough), 숨이 섞인다(breath).
    // 세 포먼트(F1 목구멍 / F2 혀 / F3 입술)와 가슴 울림(chest)으로 거른다. open(0~1)은 입을 벌린 정도: 높은 포먼트와 쉰 숨소리가 열린다
    class Throat
    {
        readonly Filter f1, f2, f3, chest, hiss;
        readonly float rough, breath;
        float phase, sub = 1f, gurglePhase;
        readonly float gurgleRate;

        public Throat(float F1, float F2, float F3, float rough, float breath, float chestHz)
        {
            f1 = Filter.BandPass(F1, 4.5f);
            f2 = Filter.BandPass(F2, 5f);
            f3 = Filter.BandPass(F3, 6f);
            chest = Filter.LowPass(chestHz, 1.2f);
            hiss = Filter.BandPass(F3 * 0.9f, 1.1f);
            this.rough = rough;
            this.breath = breath;
            gurgleRate = Rand(18f, 28f);
        }

        public float Next(float f0, float open)
        {
            phase += f0 / SR;
            if (phase >= 1f)
            {
                phase -= 1f;
                sub = 1f - rough * (float)rng.NextDouble(); // 주기마다 세기가 다르다 (거친 소리)
            }
            float saw = 2f * phase - 1f;
            float glottal = (saw * saw * saw * 0.6f + saw * 0.4f) * sub; // 닫히는 순간이 날카로운 성문 파형
            gurglePhase += gurgleRate / SR;
            float gurgle = 1f - 0.25f * rough * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * gurglePhase)); // 좀비 개의 꾸르륵거림
            float src = glottal * gurgle + Noise() * breath * 0.6f;
            float y = f1.Process(src) + f2.Process(src) * (0.45f + 0.55f * open) + f3.Process(src) * (0.2f + 0.5f * open);
            y += chest.Process(glottal) * 0.5f;
            y += hiss.Process(Noise()) * breath * 0.4f * open;
            return (float)System.Math.Tanh(y * 2.2f);
        }
    }

    // 짖기 "웝!": 숨을 터뜨리는 짧고 큰 소리. 음이 확 올라갔다 떨어지고 입이 벌어지며 밝아진다. v 0~2 변형 (높이·길이가 조금씩 다르다)
    public static AudioClip Bark(int v)
    {
        v = Mathf.Clamp(v, 0, 2);
        float dur = 0.3f + v * 0.04f;
        float baseF = 230f + v * 35f;
        var th = new Throat(640f + v * 50f, 1350f + v * 120f, 2500f, 0.45f, 0.5f, 240f);
        return Make("dog_bark" + v, dur, t =>
        {
            float u = t / dur;
            float f0 = baseF * (0.75f + 0.7f * Mathf.Exp(-Mathf.Pow((u - 0.18f) / 0.16f, 2f)) - 0.25f * u);
            float env = Mathf.Clamp01(t * 90f) * (u < 0.55f ? 1f : Mathf.Exp(-(u - 0.55f) * 9f));
            float open = Mathf.Clamp01(u * 5f) * (1f - 0.5f * u);
            return th.Next(f0, open) * env;
        }, 0.95f);
    }

    // 으르렁 (경고): 낮게 떨리는 목소리가 서서히 거세진다. 입술이 부르르 떨린다. v 0~1
    public static AudioClip Growl(int v)
    {
        v = Mathf.Clamp(v, 0, 1);
        float dur = 1.25f + v * 0.35f;
        var th = new Throat(430f, 1050f, 2300f, 0.75f, 0.35f, 170f);
        float trillPhase = 0, trillRate = Rand(22f, 30f);
        return Make("dog_growl" + v, dur, t =>
        {
            float u = t / dur;
            float f0 = (68f + v * 8f) * (1f + 0.12f * Mathf.Sin(2 * Mathf.PI * 0.9f * t) + 0.22f * u);
            float env = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1f - u) * 4f) * (0.72f + 0.28f * Mathf.Sin(2 * Mathf.PI * 1.3f * t + v));
            trillPhase += trillRate / SR;
            float trill = 0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * trillPhase);
            return th.Next(f0, 0.25f + 0.25f * u) * env * trill;
        }, 0.9f);
    }

    // 물 때: 짧고 사나운 으르렁 (Snap과 함께 낸다)
    public static AudioClip Snarl()
    {
        const float dur = 0.42f;
        var th = new Throat(520f, 1250f, 2600f, 0.7f, 0.45f, 180f);
        return Make("dog_snarl", dur, t =>
        {
            float u = t / dur;
            float f0 = 110f * (1f + 0.5f * Mathf.Sin(Mathf.PI * u));
            float env = Mathf.Clamp01(t * 40f) * Mathf.Clamp01((1f - u) * 3f);
            return th.Next(f0, 0.6f + 0.3f * u) * env;
        }, 0.95f);
    }

    // 깨갱 (맞았을 때): 높은 소리가 치솟았다 꺾인다
    public static AudioClip Yelp()
    {
        const float dur = 0.36f;
        var th = new Throat(1100f, 2200f, 3400f, 0.3f, 0.45f, 300f);
        return Make("dog_yelp", dur, t =>
        {
            float u = t / dur;
            float f0 = 620f + 520f * Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f)) - 150f * u;
            float env = Mathf.Clamp01(t * 60f) * (u < 0.6f ? 1f : Mathf.Exp(-(u - 0.6f) * 10f));
            return th.Next(f0, 0.8f) * env;
        }, 0.95f);
    }

    // 낑낑 (죽어 가며): 약하게 떨며 내려가는 소리
    public static AudioClip Whine()
    {
        const float dur = 0.95f;
        var th = new Throat(950f, 1900f, 3100f, 0.35f, 0.6f, 260f);
        return Make("dog_whine", dur, t =>
        {
            float u = t / dur;
            float f0 = (820f - 330f * u) * (1f + 0.05f * Mathf.Sin(2 * Mathf.PI * 6.5f * t));
            float env = Mathf.Clamp01(t * 10f) * Mathf.Clamp01((1f - u) * 2.5f) * (0.65f + 0.35f * Mathf.Sin(2 * Mathf.PI * 2.2f * t));
            return th.Next(f0, 0.35f) * env;
        }, 0.7f);
    }

    // 킁킁 (배회할 때): 코로 들이쉬는 짧은 숨 두세 번
    public static AudioClip Snuffle()
    {
        const float dur = 0.55f;
        var nose = Filter.BandPass(1300f, 1.4f);
        var lp = Filter.LowPass(2400f);
        return Make("dog_snuffle", dur, t =>
        {
            float env = 0;
            foreach (var at in new[] { 0.03f, 0.2f, 0.38f })
            {
                float k = (t - at) / 0.09f;
                if (k > 0 && k < 1) env += Mathf.Sin(k * Mathf.PI);
            }
            return lp.Process(nose.Process(Noise())) * env;
        }, 0.6f);
    }
}
