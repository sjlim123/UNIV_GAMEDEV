using System.Collections.Generic;
using UnityEngine;

// 근접 공격이 벽·물건·바닥에 맞는 소리 (Vfx.Impact와 짝).
// 소리는 Resources/Audio/Impact/<재질>_<bat|hard|fist>_<0~2>.wav (Higgsfield Seed Audio로 만든 폴리 녹음, 재질·무기마다 3가지)를 번갈아 쓴다.
// 파일이 없을 때만 아래의 코드 합성 소리를 대신 쓴다.
// hardTool: 쇠붙이 무기(단검·쇠 방망이·못 방망이)는 돌에서 쇳소리가 살짝 섞이고 쇠에서는 더 날카롭게 울린다. 나무 방망이는 쇠를 둔하게 친다.
// fist: 맨손·라이터는 재질과 상관없이 주먹이 부딪는 둔한 소리에 재질의 울림만 조금 섞는다.
// 시체·웅덩이는 null을 돌려준다 (부르는 쪽이 살 타격음·물 튀는 소리를 쓴다).
public static partial class EffectAudio
{
    public static AudioClip Impact(Vfx.ImpactKind kind, bool hardTool, bool fist)
    {
        if (kind == Vfx.ImpactKind.Flesh || kind == Vfx.ImpactKind.Water) return null;
        // 흙·천은 쇠 무기 소리를 따로 두지 않았다 (방망이 소리와 같다)
        string mode = fist ? "fist" : hardTool && kind != Vfx.ImpactKind.Dirt && kind != Vfx.ImpactKind.Soft ? "hard" : "bat";
        var recorded = Recorded("Audio/Impact/", kind.ToString().ToLowerInvariant() + "_" + mode);
        if (recorded != null) return recorded;
        int v = rng.Next(3);
        string key = "imp_" + kind + (fist ? "_fist" : hardTool ? "_hard" : "") + v;
        if (cache.TryGetValue(key, out var clip) && clip != null) return clip;
        if (fist) return Make(key, 0.32f, Knuckle(kind, v), 0.85f);
        switch (kind)
        {
            case Vfx.ImpactKind.Brick: return Make(key, 0.6f, Brick(v, hardTool), 0.88f);
            case Vfx.ImpactKind.Wood: return Make(key, 0.4f, Wood(v, hardTool), 0.88f);
            case Vfx.ImpactKind.Metal: return Make(key, hardTool ? 1.1f : 0.9f, Metal(v, hardTool), hardTool ? 0.8f : 0.85f);
            case Vfx.ImpactKind.Glass: return Make(key, 0.75f, Glass(v, hardTool), 0.8f);
            case Vfx.ImpactKind.Dirt: return Make(key, 0.45f, Dirt(v), 0.82f);
            case Vfx.ImpactKind.Soft: return Make(key, 0.32f, Soft(v), 0.75f);
            default: return Make(key, 0.55f, Concrete(v, hardTool), 0.9f);
        }
    }

    static readonly Dictionary<string, AudioClip[]> recordedClips = new Dictionary<string, AudioClip[]>();
    static readonly Dictionary<string, int> lastRecorded = new Dictionary<string, int>();

    // 녹음 묶음(folder + key + "_0", "_1", ...)에서 하나 (바로 앞에 쓴 것은 피한다). 하나도 없으면 null
    static AudioClip Recorded(string folder, string key)
    {
        key = folder + key;
        if (!recordedClips.TryGetValue(key, out var clips))
        {
            var list = new List<AudioClip>();
            for (int i = 0; i < 6; i++)
            {
                var c = Resources.Load<AudioClip>(key + "_" + i);
                if (c != null) list.Add(c);
            }
            clips = list.ToArray();
            recordedClips[key] = clips;
        }
        if (clips.Length == 0) return null;
        if (!lastRecorded.TryGetValue(key, out int last)) last = -1;
        int pick = rng.Next(clips.Length);
        if (clips.Length > 1 && pick == last) pick = (pick + 1 + rng.Next(clips.Length - 1)) % clips.Length;
        lastRecorded[key] = pick;
        return clips[pick];
    }

    // 쇠붙이가 돌을 칠 때 섞이는 짧은 쇳소리 (무기 쪽이 울린다)
    static float ToolRing(float t, int v) =>
        (Mathf.Sin(2 * Mathf.PI * (1720 + v * 90) * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * (3950 + v * 140) * t + 0.4f)) * Mathf.Exp(-t * 22f) * 0.22f;

    // 콘크리트: 음이 떨어지는 둔한 쿵 + 돌이 깨지는 딱 (두 공명) + 거친 잡음 + 부스러기 알갱이 + 흩날리는 가루의 쉬익
    static System.Func<float, float> Concrete(int v, bool hard)
    {
        var r1 = Filter.BandPass(1500 + v * 250, 5f);
        var r2 = Filter.BandPass(3000 + v * 300, 4f);
        var grit = Filter.LowPass(900);
        var dust = Filter.BandPass(2400, 0.8f);
        var chips = new Pops(1800 + v * 200, 2f, 45f);
        var hp = Filter.HighPass(40);
        float phase = 0;
        return t =>
        {
            float f = 75 + v * 8 + 45 * Mathf.Exp(-t * 40f);
            phase += 2 * Mathf.PI * f / SR;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 30f) * 0.9f;
            float burst = Noise() * Mathf.Exp(-t * 450f);
            float crack = (r1.Process(burst) + 0.6f * r2.Process(burst)) * 8f;
            float g = grit.Process(Noise()) * Mathf.Exp(-t * 25f) * 0.9f;
            float d = dust.Process(Noise()) * Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 30f) * 0.22f;
            float c = chips.Next(0.94f) * 0.8f * Mathf.Exp(-t * 6f) * Mathf.Clamp01((0.3f - t) * 20f);
            return hp.Process(thump + crack + g + d + c + (hard ? ToolRing(t, v) : 0f));
        };
    }

    // 벽돌: 콘크리트보다 낮고 무르게 깨지며, 부스러기가 더 오래 바스러진다
    static System.Func<float, float> Brick(int v, bool hard)
    {
        var r1 = Filter.BandPass(1100 + v * 200, 4f);
        var r2 = Filter.BandPass(2300 + v * 150, 3f);
        var crumble = new Pops(1400 + v * 120, 1.6f, 60f);
        var trickle = Filter.LowPass(1200);
        var hp = Filter.HighPass(40);
        float phase = 0;
        return t =>
        {
            float f = 82 + v * 7 + 35 * Mathf.Exp(-t * 35f);
            phase += 2 * Mathf.PI * f / SR;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 28f);
            float burst = Noise() * Mathf.Exp(-t * 380f);
            float crack = (r1.Process(burst) + 0.55f * r2.Process(burst)) * 7f;
            float c = crumble.Next(0.95f) * Mathf.Exp(-t * 7f) * 0.9f;
            float tr = trickle.Process(Noise()) * Mathf.Exp(-t * 12f) * 0.45f * Mathf.Clamp01(t * 25f);
            return hp.Process(thump + crack + c + tr + (hard ? ToolRing(t, v) * 0.8f : 0f));
        };
    }

    // 나무: 속이 빈 상자처럼 통 울리는 노크 + 낮은 쿵 + 갈라지는 딱, 조금 뒤 가시가 튀는 작은 딱
    static System.Func<float, float> Wood(int v, bool hard)
    {
        var box1 = Filter.BandPass(210 + v * 30, 8f);
        var box2 = Filter.BandPass(520 + v * 60, 6f);
        var crack = Filter.BandPass(hard ? 2600 : 2000, 1.2f);
        var tick = Filter.BandPass(3400, 3f);
        var hp = Filter.HighPass(45);
        float delay = 0.028f + v * 0.008f;
        return t =>
        {
            float burst = Noise() * Mathf.Exp(-t * 350f);
            float knock = (box1.Process(burst) + 0.7f * box2.Process(burst)) * 6f;
            float thump = Mathf.Sin(2 * Mathf.PI * 110 * t) * Mathf.Exp(-t * 35f) * 0.6f;
            float c = crack.Process(Noise()) * Mathf.Exp(-t * (hard ? 70f : 90f)) * (hard ? 1.5f : 1.2f);
            float t2 = t - delay;
            float splinter = t2 > 0 ? tick.Process(Noise()) * Mathf.Exp(-t2 * 160f) * 0.5f : tick.Process(0f);
            return hp.Process(knock + thump + c + splinter);
        };
    }

    // 쇠: 철판이 울리는 여러 겹의 울림(서로 다르게 줄어든다) + 몸통의 붕 + 때리는 순간의 딱.
    // 쇠 무기면 높게 쨍 울리고 긁히는 쉬익이 붙는다. 나무 방망이면 울림이 작고 둔탁하다
    static System.Func<float, float> Metal(int v, bool hard)
    {
        float k = hard ? 1.18f : 1f;
        float f0 = (300 + v * 25) * k, f1 = 790 * k, f2 = (1430 + v * 40) * k, f3 = 2270 * k;
        float ringGain = hard ? 0.55f : 0.28f;
        var click = Filter.BandPass(hard ? 4200 : 2600, 1.5f);
        var dull = Filter.LowPass(500);
        var scrape = Filter.BandPass(5600, 2f);
        var hp = Filter.HighPass(50);
        return t =>
        {
            float wobble = 0.85f + 0.15f * Mathf.Sin(2 * Mathf.PI * 6f * t);
            float ring = (Mathf.Sin(2 * Mathf.PI * f0 * t) * Mathf.Exp(-t * 5f) * wobble
                          + 0.8f * Mathf.Sin(2 * Mathf.PI * f1 * t + 0.4f) * Mathf.Exp(-t * 7f)
                          + 0.5f * Mathf.Sin(2 * Mathf.PI * f2 * t + 1.1f) * Mathf.Exp(-t * 10f)
                          + 0.3f * Mathf.Sin(2 * Mathf.PI * f3 * t + 2f) * Mathf.Exp(-t * 14f)) * ringGain * Mathf.Clamp01(t * 400f);
            float boom = Mathf.Sin(2 * Mathf.PI * 120 * t) * Mathf.Exp(-t * 12f) * (hard ? 0.5f : 0.8f);
            float c = click.Process(Noise()) * Mathf.Exp(-t * 120f) * 1.5f;
            float thud = hard ? 0f : dull.Process(Noise()) * Mathf.Exp(-t * 35f) * 2.2f;
            float s = hard ? scrape.Process(Noise()) * Mathf.Exp(-t * 18f) * 0.25f * Mathf.Clamp01(t * 60f) : 0f;
            return hp.Process(ring + boom + c + thud + s);
        };
    }

    // 유리: 둔한 텅 + 판이 맑게 울리는 높은 소리 몇 개(서로 다르게 줄어든다) + 금이 가는 날카로운 틱
    static System.Func<float, float> Glass(int v, bool hard)
    {
        var tick = Filter.BandPass(5200, 2f);
        var hp = Filter.HighPass(80);
        float f1 = 2900 + v * 180, f2 = 4300 + v * 220, f3 = 6100 + v * 260;
        return t =>
        {
            float thunk = Mathf.Sin(2 * Mathf.PI * 180 * t) * Mathf.Exp(-t * 30f) * 0.7f;
            float ring = (Mathf.Sin(2 * Mathf.PI * f1 * t) * Mathf.Exp(-t * 9f)
                          + 0.7f * Mathf.Sin(2 * Mathf.PI * f2 * t + 0.7f) * Mathf.Exp(-t * 13f)
                          + 0.45f * Mathf.Sin(2 * Mathf.PI * f3 * t + 1.9f) * Mathf.Exp(-t * 18f)) * (hard ? 0.35f : 0.25f) * Mathf.Clamp01(t * 500f);
            float tk = tick.Process(Noise()) * Mathf.Exp(-t * 200f) * 1.2f;
            return hp.Process(thunk + ring + tk);
        };
    }

    // 흙: 울림 없는 퍽 + 낮은 쿵 + 흙알갱이 + 쏟아지는 모래의 쉬익
    static System.Func<float, float> Dirt(int v)
    {
        var lp = Filter.LowPass(400 + v * 40);
        var grains = new Pops(2800, 0.9f, 30f);
        var hiss = Filter.BandPass(3500, 0.6f);
        var hp = Filter.HighPass(35);
        return t =>
        {
            float body = lp.Process(Noise()) * Mathf.Exp(-t * 30f) * 2f + Mathf.Sin(2 * Mathf.PI * (60 + v * 6) * t) * Mathf.Exp(-t * 25f);
            float g = grains.Next(0.93f) * Mathf.Exp(-t * 6f) * 0.8f;
            float h = hiss.Process(Noise()) * Mathf.Exp(-t * 8f) * Mathf.Clamp01(t * 20f) * 0.3f;
            return hp.Process(body + g + h);
        };
    }

    // 천·모래주머니·고무: 높은 소리 없는 푹 + 천이 스치는 바스락
    static System.Func<float, float> Soft(int v)
    {
        var lp = Filter.LowPass(300 + v * 30);
        var rustle = Filter.BandPass(1800, 1f);
        var hp = Filter.HighPass(35);
        return t =>
        {
            float body = lp.Process(Noise()) * Mathf.Exp(-t * 25f) * 2f + Mathf.Sin(2 * Mathf.PI * (65 + v * 5) * t) * Mathf.Exp(-t * 28f);
            float r = rustle.Process(Noise()) * Mathf.Exp(-t * 20f) * 0.4f;
            return hp.Process(body + r);
        };
    }

    // 맨손: 주먹이 부딪는 둔한 툭 + 살갗 소리, 재질의 울림을 조금 (쇠는 웅, 유리는 틱, 나무는 통)
    static System.Func<float, float> Knuckle(Vfx.ImpactKind kind, int v)
    {
        var lp = Filter.LowPass(800);
        var slap = Filter.BandPass(1600 + v * 120, 1.2f);
        var wood = Filter.BandPass(250 + v * 25, 6f);
        var glass = Filter.BandPass(4000 + v * 200, 20f);
        var hp = Filter.HighPass(45);
        return t =>
        {
            float thump = Mathf.Sin(2 * Mathf.PI * (90 + v * 8 + 30 * Mathf.Exp(-t * 40f)) * t) * Mathf.Exp(-t * 28f);
            float body = lp.Process(Noise()) * Mathf.Exp(-t * 60f) * 1.2f;
            float skin = slap.Process(Noise()) * Mathf.Exp(-t * 90f) * 0.5f;
            float burst = Noise() * Mathf.Exp(-t * 400f);
            float extra = 0f;
            if (kind == Vfx.ImpactKind.Metal)
                extra = (Mathf.Sin(2 * Mathf.PI * (330 + v * 20) * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 870 * t)) * Mathf.Exp(-t * 14f) * 0.14f;
            else if (kind == Vfx.ImpactKind.Glass) extra = glass.Process(burst) * 4f;
            else if (kind == Vfx.ImpactKind.Wood) extra = wood.Process(burst) * 3f;
            return hp.Process(thump + body + skin + extra);
        };
    }
}
