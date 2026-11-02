using UnityEngine;

// 트럭 정비소 효과음 (사운드 에셋 없이 코드로 합성)
// 라쳇 렌치를 돌리는 딸깍 소리 몇 번 + 부품을 조여 붙이는 쇳소리
public static class TruckAudio
{
    const int SR = 44100;
    static AudioClip ratchet;
    static readonly System.Random rng = new System.Random(4321);

    static float Noise() => (float)(rng.NextDouble() * 2 - 1);

    public static AudioClip Ratchet()
    {
        if (ratchet != null) return ratchet;
        const float dur = 1.15f;
        int n = (int)(dur * SR);
        var data = new float[n];

        // 딸깍: 높은 쇳소리 + 짧은 잡음. 점점 빨라지다가 마지막에 꽉 조인다
        float[] ticks = { 0f, 0.1f, 0.19f, 0.27f, 0.34f, 0.405f, 0.465f, 0.52f };
        for (int k = 0; k < ticks.Length; k++)
        {
            int start = (int)(ticks[k] * SR), len = (int)(0.045f * SR);
            float pitch = 1f + 0.04f * (k % 2); // 톱니가 번갈아 걸리는 느낌
            for (int i = 0; i < len && start + i < n; i++)
            {
                float t = i / (float)SR;
                float ring = Mathf.Sin(2 * Mathf.PI * 3150 * pitch * t) * 0.55f + Mathf.Sin(2 * Mathf.PI * 5230 * pitch * t) * 0.35f;
                float body = Mathf.Sin(2 * Mathf.PI * 880 * pitch * t) * 0.45f * Mathf.Exp(-t * 90);
                data[start + i] += (ring + Noise() * 0.8f) * Mathf.Exp(-t * 170) + body;
            }
        }

        // 쾅: 부품이 맞물리는 둔탁한 쿵 + 여운이 남는 쇳소리
        int clank = (int)(0.66f * SR);
        for (int i = 0; clank + i < n; i++)
        {
            float t = i / (float)SR;
            float metal = Mathf.Sin(2 * Mathf.PI * 312 * t) + 0.7f * Mathf.Sin(2 * Mathf.PI * 787 * t)
                        + 0.45f * Mathf.Sin(2 * Mathf.PI * 1373 * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 2241 * t);
            float thump = Mathf.Sin(2 * Mathf.PI * 68 * t) * Mathf.Exp(-t * 24) * 1.4f;
            data[clank + i] += metal * Mathf.Exp(-t * 6.5f) * 0.8f + thump + Noise() * Mathf.Exp(-t * 70) * 0.9f;
        }

        float max = 0.0001f;
        for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / max * 0.85f;
        ratchet = AudioClip.Create("truck_ratchet", n, 1, SR, false);
        ratchet.SetData(data, 0);
        return ratchet;
    }
}
