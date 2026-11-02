using UnityEngine;

// 소리 종류 (설정 → 소리에서 종류마다 음량을 따로 정한다. 전체 음량은 AudioListener가 한꺼번에 줄인다)
public enum SoundCategory { Effects, Ambient, Enemy, UI }

// 계속 켜 두는 AudioSource(바람·웅웅거림·좀비 목소리 등)에 붙인다:
// 코드가 정한 음량 × 종류 음량으로 매 프레임 맞춘다. 코드가 음량을 바꾸면 그 값을 새 기준으로 삼는다.
// (한 번 울리고 사라지는 소리는 ProceduralAudio.PlayAt이 만들 때 종류 음량을 곱한다)
// 한 오브젝트에 AudioSource가 여럿이면 소리마다 하나씩 붙는다
public class AudioCategory : MonoBehaviour
{
    public SoundCategory category;

    AudioSource source;
    float baseVolume, applied = -1f;

    // src에 종류를 붙인다 (이미 붙어 있으면 종류만 바꾼다). 같은 오브젝트에 소리가 여럿이면 각각 따로 붙는다
    public static AudioSource Tag(AudioSource src, SoundCategory category)
    {
        if (src == null) return null;
        foreach (var c in src.GetComponents<AudioCategory>())
            if (c.source == src)
            {
                c.category = category;
                return src;
            }
        var tag = src.gameObject.AddComponent<AudioCategory>();
        if (tag == null) return src;
        tag.source = src;
        tag.category = category;
        return src;
    }

    public static bool IsTagged(AudioSource src)
    {
        foreach (var c in src.GetComponents<AudioCategory>())
            if (c.source == src) return true;
        return false;
    }

    // 다른 스크립트의 Start가 원래 음량을 읽은 뒤(첫 LateUpdate)부터 맞춘다
    void LateUpdate()
    {
        if (source == null) return;
        if (applied < 0 || !Mathf.Approximately(source.volume, applied)) baseVolume = source.volume;
        float v = baseVolume * GameSettings.CategoryVolume(category);
        if (!Mathf.Approximately(source.volume, v)) source.volume = v;
        applied = source.volume;
    }
}
