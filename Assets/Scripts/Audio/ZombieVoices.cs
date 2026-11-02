using System.Collections.Generic;
using UnityEngine;

// 좀비 목소리: OpenGameArt의 CC0 녹음을 다듬은 클립 (Resources/Audio/Zombie, 편집기 메뉴 ZombieSoundSlicer가 만든다).
// 종류마다 여러 개 중 하나를 고르되 같은 소리가 연달아 나오지 않게 한다.
// 클립이 없으면 null을 돌려주고, ProceduralAudio가 예전 합성음으로 대신한다.
public static class ZombieVoices
{
    static Dictionary<string, List<AudioClip>> clips;
    static readonly Dictionary<string, int> last = new Dictionary<string, int>();

    public static AudioClip Groan() => Pick("groan");   // 평소 신음
    public static AudioClip Alert() => Pick("alert");   // 플레이어를 발견했을 때
    public static AudioClip Attack() => Pick("attack"); // 덤벼들 때
    public static AudioClip Hurt() => Pick("hurt");     // 맞았을 때

    static AudioClip Pick(string kind)
    {
        if (clips == null)
        {
            clips = new Dictionary<string, List<AudioClip>>();
            foreach (var c in Resources.LoadAll<AudioClip>("Audio/Zombie"))
            {
                int cut = c.name.IndexOf('_');
                if (cut <= 0) continue;
                string k = c.name.Substring(0, cut);
                if (!clips.TryGetValue(k, out var list)) clips[k] = list = new List<AudioClip>();
                list.Add(c);
            }
        }
        if (!clips.TryGetValue(kind, out var options) || options.Count == 0) return null;
        int i = Random.Range(0, options.Count);
        if (options.Count > 1 && last.TryGetValue(kind, out int prev) && i == prev) i = (i + 1) % options.Count;
        last[kind] = i;
        return options[i];
    }
}
