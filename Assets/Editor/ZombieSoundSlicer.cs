using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// OpenGameArt에서 받은 CC0 좀비 소리(Assets/ThirdParty/OpenGameArt)를 게임용 클립으로 만든다.
// 여러 소리가 이어진 파일은 조용한 구간에서 자르고, 모노로 합쳐 크기를 맞춘 뒤(최고점 -1dB)
// 앞뒤를 짧게 페이드해서 Assets/Resources/Audio/Zombie/{종류}_{번호}.wav로 저장한다.
// 종류: groan(평소 신음) / alert(플레이어를 발견) / attack(공격) / hurt(맞음). 쓰는 곳은 ZombieVoices.cs
public static class ZombieSoundSlicer
{
    const string Src = "Assets/ThirdParty/OpenGameArt";
    const string Dst = "Assets/Resources/Audio/Zombie";

    // (파일, 시작초, 끝초(0이면 끝까지), 종류)
    static readonly (string file, float from, float to, string kind)[] Cuts =
    {
        ("darsycho_zombie_moans/darsycho__zombie-moans.ogg", 0.20f, 2.70f, "groan"),
        ("darsycho_zombie_moans/darsycho__zombie-moans.ogg", 3.60f, 6.24f, "groan"),
        ("darsycho_zombie_moans/darsycho__zombie-moans.ogg", 6.58f, 8.92f, "groan"),
        ("darsycho_zombie_moans/darsycho__zombie-moans.ogg", 9.26f, 10.22f, "attack"),
        ("darsycho_zombie_moans/darsycho__zombie-moans.ogg", 10.36f, 11.30f, "attack"),
        ("darsycho_zombie_moans/darsycho__zombie-moans.ogg", 11.54f, 14.32f, "groan"),
        ("darsycho_zombie_moans/darsycho__zombie-moans.ogg", 14.74f, 0f, "groan"),
        ("ianzazz_zombie_noises/zombienoise1.ogg", 0f, 0f, "groan"),
        ("ianzazz_zombie_noises/zombienoise2.ogg", 0.28f, 1.24f, "hurt"),
        ("ianzazz_zombie_noises/zombienoise3.ogg", 0f, 0f, "hurt"),
        ("ianzazz_zombie_noises/fastzombie1.ogg", 0f, 0f, "attack"),
        ("saturn91_zomby_sfx/01_zombey.mp3", 0f, 0f, "alert"),
        ("saturn91_zomby_sfx/02_zombey.mp3", 0f, 0f, "alert"),
        ("saturn91_zomby_sfx/03_zombey.mp3", 0f, 0f, "alert"),
        ("saturn91_zomby_sfx/06_zombey.mp3", 0f, 0f, "alert"),
        ("saturn91_zomby_sfx/04_zombey.mp3", 0f, 0f, "groan"),
        ("saturn91_zomby_sfx/07_zombey.mp3", 0f, 0f, "groan"),
    };

    [MenuItem("Tools/핵폭발 생존/좀비 소리 만들기 (CC0 원본에서)")]
    public static void Build()
    {
        Directory.CreateDirectory(Dst);
        foreach (var old in Directory.GetFiles(Dst, "*.wav")) AssetDatabase.DeleteAsset(old.Replace('\\', '/'));

        var counts = new Dictionary<string, int>();
        foreach (var cut in Cuts)
        {
            var path = $"{Src}/{cut.file}";
            var samples = ReadMono(path, out int rate);
            if (samples == null) { Debug.LogWarning("[ZombieSoundSlicer] 읽지 못함: " + path); continue; }
            int a = Mathf.Clamp(Mathf.RoundToInt(cut.from * rate), 0, samples.Length);
            int b = cut.to > 0 ? Mathf.Clamp(Mathf.RoundToInt(cut.to * rate), a, samples.Length) : samples.Length;
            var seg = new float[b - a];
            System.Array.Copy(samples, a, seg, 0, seg.Length);
            Normalize(seg, 0.89f);
            Fade(seg, rate / 100);
            counts.TryGetValue(cut.kind, out int n);
            counts[cut.kind] = ++n;
            WriteWav($"{Dst}/{cut.kind}_{n:00}.wav", seg, rate);
        }
        AssetDatabase.Refresh();
        // 3D 소리로 쓰므로 모노, 짧은 소리라 불러올 때 풀어 둔다
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Dst }))
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            imp.forceToMono = true;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
        Debug.Log("[ZombieSoundSlicer] 좀비 소리 " + Cuts.Length + "개 생성: " + string.Join(", ", new List<string>(Keys(counts))));
    }

    static IEnumerable<string> Keys(Dictionary<string, int> d)
    {
        foreach (var kv in d) yield return kv.Key + " " + kv.Value;
    }

    // 원본을 풀어서(DecompressOnLoad) 채널을 평균 낸 모노 샘플로 읽는다
    static float[] ReadMono(string path, out int rate)
    {
        rate = 44100;
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp == null) return null;
        var st = imp.defaultSampleSettings;
        if (st.loadType != AudioClipLoadType.DecompressOnLoad)
        {
            st.loadType = AudioClipLoadType.DecompressOnLoad;
            imp.defaultSampleSettings = st;
            imp.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null || !clip.LoadAudioData()) return null;
        rate = clip.frequency;
        int ch = clip.channels;
        var data = new float[clip.samples * ch];
        clip.GetData(data, 0);
        var mono = new float[clip.samples];
        for (int i = 0; i < mono.Length; i++)
        {
            float s = 0;
            for (int c = 0; c < ch; c++) s += data[i * ch + c];
            mono[i] = s / ch;
        }
        return mono;
    }

    static void Normalize(float[] s, float target)
    {
        float peak = 0;
        foreach (var v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak < 1e-5f) return;
        float k = target / peak;
        for (int i = 0; i < s.Length; i++) s[i] *= k;
    }

    // 잘린 곳에서 딸깍 소리가 나지 않게 앞뒤를 len 샘플 동안 키우고 줄인다
    static void Fade(float[] s, int len)
    {
        len = Mathf.Min(len, s.Length / 2);
        for (int i = 0; i < len; i++)
        {
            float k = i / (float)len;
            s[i] *= k;
            s[s.Length - 1 - i] *= k;
        }
    }

    static void WriteWav(string path, float[] s, int rate)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = s.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(bytes);
            foreach (var v in s) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }
}
