using System.Collections.Generic;
using UnityEngine;

// 발전기 소리를 코드로 합성한다 (FurnaceAudio와 같은 방식): 디젤 엔진이 덜덜거리는 반복음, 시동이 걸리는 소리, 꺼지는 소리,
// 연료를 붓는 꿀렁임, 힘이 달릴 때의 쿨럭임. 전부 샘플 하나씩 계산해 AudioClip으로 만든다 (사운드 파일 없음)
public static class GeneratorAudio
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

    // 만들어 두고 다시 쓴다. loopFade > 0이면 끝을 앞에 겹쳐 자연스럽게 반복된다
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

    // 실린더가 터지는 박자: rate(Hz)마다 짧게 꺼지는 잡음 덩어리(pulse)와 밸브 덜걱임(clat)을 새로 당긴다
    class Engine
    {
        public readonly Filter exhaust = Filter.LowPass(150, 1.2f), body = Filter.BandPass(92, 3f), clatter = Filter.BandPass(2400, 1.6f), tin = Filter.BandPass(430, 4f);
        public float phase, pulse, clat, jitter = 1f;

        // 한 샘플: 박자를 돌리고 폭발음·밸브·쇠판 울림을 합친다. strength: 점화 세기 배율 (크랭킹 중엔 작게)
        public float Next(float rate, float strength)
        {
            phase += rate * jitter / SR;
            if (phase >= 1f)
            {
                phase -= 1f;
                pulse = Rand(0.75f, 1.2f) * strength;
                clat = Rand(0.25f, 1f);
                jitter = Rand(0.965f, 1.035f);
            }
            float fire = Noise() * pulse;
            float thump = exhaust.Process(fire) * 2.4f + body.Process(fire) * 1.5f;
            float valves = clatter.Process(Noise()) * clat * 0.22f;
            float sheet = tin.Process(fire) * 0.45f;
            pulse *= 0.9972f;
            clat *= 0.9975f;
            return thump + valves + sheet;
        }
    }

    // 엔진 반복음 (4초 루프): 초당 29번 터지는 실린더의 덜덜거림 + 밸브 덜걱임 + 쇠판 떨림 + 바닥의 웅웅거림. 천천히 세졌다 약해진다
    public static AudioClip Rumble()
    {
        var e = new Engine();
        var hp = Filter.HighPass(36);
        float brown = 0;
        return Make("gen_rumble", 4f, t =>
        {
            brown = (brown + 0.03f * Noise()) / 1.02f;
            float sway = 0.86f + 0.14f * Mathf.Sin(2 * Mathf.PI * t / 2.3f);
            return hp.Process(e.Next(29f, 1f) + brown) * sway;
        }, 0.75f, 0.6f);
    }

    // 시동 (1.8초): 스타터가 끼릭끼릭 느리게 돌리다 점점 빨라지며 걸린다. 걸리는 순간 크게 털털거리고 안정된다
    public static AudioClip StartUp()
    {
        const float dur = 1.8f;
        var e = new Engine();
        var whine = Filter.BandPass(340, 6f);
        var hp = Filter.HighPass(36);
        float wPhase = 0;
        return Make("gen_start", dur, t =>
        {
            float u = t / dur;
            bool cranking = u < 0.58f;
            float rate = cranking ? Mathf.Lerp(5f, 11f, u / 0.58f) : Mathf.Lerp(11f, 30f, (u - 0.58f) / 0.42f);
            float engine = e.Next(rate, cranking ? 0.4f : Mathf.Lerp(1.4f, 1f, (u - 0.58f) / 0.42f));
            // 스타터 모터의 윙 소리 (크랭킹 동안만)
            wPhase += (240f + 140f * u) / SR;
            float starterEnv = Mathf.Clamp01((0.62f - u) * 8f);
            float starter = whine.Process(Noise()) * 0.7f * starterEnv + Mathf.Sin(2 * Mathf.PI * wPhase) * 0.14f * starterEnv;
            float env = Mathf.Clamp01(t * 20f) * (u < 0.9f ? 1f : Mathf.Lerp(1f, 0.35f, (u - 0.9f) / 0.1f));
            return hp.Process(engine + starter) * env;
        }, 0.85f);
    }

    // 꺼짐 (1.7초): 박자가 느려지며 쿨럭 몇 번 하고 멈춘다. 마지막에 쇠가 쿵 내려앉는다
    public static AudioClip ShutDown()
    {
        const float dur = 1.7f;
        var e = new Engine();
        var thud = Filter.LowPass(180, 1f);
        var hp = Filter.HighPass(36);
        return Make("gen_stop", dur, t =>
        {
            float u = t / dur;
            float rate = Mathf.Lerp(29f, 3f, u * u);
            float strength = u < 0.7f ? 1f - 0.5f * u : 0.4f * (1f - (u - 0.7f) / 0.3f);
            float engine = e.Next(rate, strength);
            float clunk = thud.Process(Noise()) * Mathf.Exp(-Mathf.Max(0, t - 1.45f) * 40f) * (t > 1.45f ? 2.5f : 0f);
            return hp.Process(engine + clunk) * Mathf.Clamp01((1f - u) * 6f + 0.4f);
        }, 0.85f);
    }

    // 힘이 달려 쿨럭인다 (0.9초): 두어 박자를 거르고 쿨럭, 배기관에서 퍽 하고 터진다
    public static AudioClip Sputter()
    {
        const float dur = 0.9f;
        var e = new Engine();
        var pop = Filter.LowPass(260, 0.9f);
        var hp = Filter.HighPass(36);
        return Make("gen_sputter", dur, t =>
        {
            float u = t / dur;
            // 0.15~0.35초: 박자가 빠지고, 0.4초에 퍽 터진 뒤 더듬더듬 돌아온다
            bool gap = u > 0.15f && u < 0.38f;
            float rate = gap ? 8f : u < 0.15f ? 29f : Mathf.Lerp(14f, 29f, (u - 0.38f) / 0.62f);
            float engine = e.Next(rate, gap ? 0.15f : 1f);
            float bang = pop.Process(Noise()) * Mathf.Exp(-Mathf.Max(0, t - 0.36f) * 30f) * (t > 0.36f ? 3f : 0f);
            return hp.Process(engine + bang);
        }, 0.8f);
    }

    // 연료를 붓는다 (1.5초): 통 안에서 꿀렁꿀렁 올라오는 거품 + 쏟아지는 기름의 쉬익
    public static AudioClip Pour()
    {
        const float dur = 1.5f;
        var lp = Filter.LowPass(700, 1.0f);
        var hiss = Filter.BandPass(3000, 0.8f);
        float glugPhase = 0, glug = 0, blubF = 200f, bPhase = 0;
        return Make("gen_pour", dur, t =>
        {
            float u = t / dur;
            glugPhase += (4.5f + 1.5f * Mathf.Sin(t * 3f)) / SR;
            if (glugPhase >= 1f)
            {
                glugPhase -= 1f;
                glug = Rand(0.7f, 1.1f);
                blubF = Rand(160f, 260f);
            }
            float g = lp.Process(Noise()) * glug * 1.6f;
            // 꿀렁: 음이 쑥 올라가는 거품 소리
            bPhase += blubF * (1f + glug * 0.8f) / SR;
            float bubble = Mathf.Sin(2 * Mathf.PI * bPhase) * glug * 0.5f;
            glug *= 0.9993f;
            float pourHiss = hiss.Process(Noise()) * 0.12f;
            float env = Mathf.Clamp01(t * 12f) * Mathf.Clamp01((1f - u) * 5f);
            return (g + bubble + pourHiss) * env;
        }, 0.8f);
    }
}
