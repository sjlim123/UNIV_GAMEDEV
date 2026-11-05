using System.Collections.Generic;
using UnityEngine;

// 방공호 장치 이펙트 소리를 코드로 합성한다 (FurnaceAudio와 같은 방식):
// 무거운 것이 바닥에 쾅 놓이는 소리, 망치질, 불꽃이 튀는 치직 소리, 전기가 튀는 지직 소리, 고기가 지글거리는 소리,
// 미닫이 철문이 레일 위를 구르는 소리·걸쇠·끝에서 탁 멈추는 소리, 방폭문의 공압 치이익
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

    // 미닫이 철문이 레일 위를 굴러가는 소리 (반복): 낮은 우르릉 + 쇠가 긁히는 쉭 + 바퀴가 이음매를 넘는 덜컹
    public static AudioClip DoorRoll()
    {
        var rumble = Filter.LowPass(220, 0.9f);
        var grind = Filter.BandPass(900, 1.2f);
        var hp = Filter.HighPass(40);
        var bumps = new Pops(380, 2.2f, 6f);
        float brown = 0;
        return Make("fx_doorroll", 1.6f, t =>
        {
            brown = (brown + 0.05f * Noise()) / 1.02f;
            float body = rumble.Process(brown * 6f) * 1.4f + grind.Process(Noise()) * 0.18f * (0.7f + 0.3f * Mathf.Sin(2 * Mathf.PI * 7f * t));
            return hp.Process(body + bumps.Next(0.97f) * 0.9f);
        }, 0.7f, 0.3f);
    }

    // 문이 끝에 닿아 탁 멈추는 소리: 쇠가 부딪히는 쿵 + 짧은 울림 + 레일이 덜덜거리는 여운
    public static AudioClip DoorStop()
    {
        const float dur = 0.9f;
        var thud = Filter.LowPass(260, 0.9f);
        var ring = Filter.BandPass(1700, 6f);
        var rattle = Filter.BandPass(520, 2f);
        var hp = Filter.HighPass(45);
        float phase = 0;
        return Make("fx_doorstop", dur, t =>
        {
            float f = 95f * Mathf.Exp(-t * 14f) + 55f;
            phase += 2 * Mathf.PI * f / SR;
            float boom = Mathf.Sin(phase) * Mathf.Exp(-t * 9f) * 1.5f + thud.Process(Noise()) * Mathf.Exp(-t * 30f) * 3f;
            float clang = ring.Process(Noise()) * Mathf.Exp(-t * 12f) * 0.8f;
            float shake = rattle.Process(Noise()) * Mathf.Exp(-t * 6f) * 0.5f * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 23f * t));
            return hp.Process(boom + clang + shake);
        }, 0.95f);
    }

    // 방폭문의 공압 장치: 압축 공기가 치이익 빠지는 소리 (연구 시설 기밀문처럼). release: 잠금을 풀 때(길고 거세다) / 아니면 닫혀 밀봉될 때(짧고 끝에 걸쇠가 탁)
    public static AudioClip Pneumatic(bool release)
    {
        float dur = release ? 1.5f : 1.05f;
        var hiss = Filter.BandPass(release ? 3000f : 2500f, 0.6f);
        var hiss2 = Filter.BandPass(5400f, 0.8f);
        var body = Filter.LowPass(900f, 0.8f);
        var clack = Filter.BandPass(1900f, 4f);
        var hp = Filter.HighPass(120f);
        return Make(release ? "fx_pneu_release" : "fx_pneu_seal", dur, t =>
        {
            // 처음에 확 터졌다가 점점 가늘어진다 (높은 쉭 소리가 먼저 사그라져 음이 내려가는 느낌)
            float env = Mathf.Exp(-t * (release ? 2.4f : 3.2f)) * (1f - Mathf.Exp(-t * 90f));
            float wob = 0.85f + 0.15f * Mathf.Sin(2 * Mathf.PI * 9f * t);
            float h = hiss.Process(Noise()) * env * wob + hiss2.Process(Noise()) * env * env * 0.7f;
            float b = body.Process(Noise()) * env * 0.35f;
            float tc = dur - 0.22f;
            float c = t > tc ? clack.Process(Noise()) * Mathf.Exp(-(t - tc) * 70f) * 2.2f : 0f;
            return hp.Process(h * 2.5f + b + c);
        }, 0.8f);
    }

    // 걸쇠를 풀고 손잡이를 당기는 짧은 철컥
    public static AudioClip DoorUnlatch()
    {
        const float dur = 0.22f;
        var click = Filter.BandPass(2100, 2.5f);
        var tock = Filter.BandPass(420, 3f);
        return Make("fx_doorunlatch", dur, t =>
        {
            float a = click.Process(Noise()) * Mathf.Exp(-t * 90f) * 2f;
            float b = tock.Process(Noise()) * Mathf.Exp(-Mathf.Max(0, t - 0.06f) * 60f) * (t > 0.06f ? 2.5f : 0f);
            return a + b;
        }, 0.6f);
    }
}
