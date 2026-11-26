using UnityEngine;

// 아이템을 쓸 때(우클릭) 나는 소리: 쓰는 몸짓(PlayerHands.UseMove)의 박자에 맞춰 소리 조각을 하나씩 낸다.
// Resources/Audio/Use/<소리>_<0~2>.wav (Higgsfield Seed Audio로 만든 폴리 녹음)를 번갈아 쓴다. 녹음이 없으면 그 조각은 조용히 건너뛴다.
// 박자(At)는 몸짓 전체 길이에 대한 비율이다. 몸짓은 0~0.25 들어 올리고, 0.25~0.8 동안 아이템마다 다른 동작을 세 번(구급상자는 흔들기 네 번), 0.8~1 내린다.
//   물: 뚜껑 따기 → 꿀꺽 세 번 → 내리며 "하아"     통조림: 캔 따기 → 씹기 세 번     구운 고기: 베어 물기 세 번
//   붕대: 찢기 → 감기 세 번     구급상자: 걸쇠 열기 → 안의 물건 달그락 두 번
public static partial class EffectAudio
{
    const string UseFolder = "Audio/Use/";

    public struct UseCue
    {
        public readonly float At;      // 몸짓 비율 (0~1)
        public readonly string Sound;  // Resources/Audio/Use/ 안의 이름 (_0~2 앞까지)
        public readonly float Volume;
        public UseCue(float at, string sound, float volume) { At = at; Sound = sound; Volume = volume; }
    }

    // 몸짓 비율 → 가운데 동작 구간(hold, 0~1) 안의 자리
    static float Hold(float h) => 0.25f + 0.55f * h;

    static readonly UseCue[] DrinkCues =
    {
        new UseCue(0f, "drink_cap", 0.5f),
        // 꿀꺽: 병을 뒤로 기울이는 세 번 (gulp = sin(hold·6π))의 앞쪽
        new UseCue(Hold(1f / 12), "drink_gulp", 0.6f),
        new UseCue(Hold(5f / 12), "drink_gulp", 0.6f),
        new UseCue(Hold(9f / 12), "drink_gulp", 0.6f),
        new UseCue(0.82f, "drink_breath", 0.3f),
    };
    static readonly UseCue[] CanCues =
    {
        new UseCue(0f, "can_open", 0.5f),
        // 씹기: 세 번 베어 무는 꼭대기 (|sin(hold·3π)|)
        new UseCue(Hold(1f / 6), "eat_chew", 0.55f),
        new UseCue(Hold(3f / 6), "eat_chew", 0.55f),
        new UseCue(Hold(5f / 6), "eat_chew", 0.55f),
    };
    static readonly UseCue[] MeatCues =
    {
        new UseCue(Hold(1f / 6), "meat_bite", 0.6f),
        new UseCue(Hold(3f / 6), "meat_bite", 0.55f),
        new UseCue(Hold(5f / 6), "meat_bite", 0.6f),
    };
    static readonly UseCue[] BandageCues =
    {
        new UseCue(0f, "bandage_tear", 0.45f),
        // 감기: 팔뚝 둘레를 도는 세 바퀴 (a = hold·6π)
        new UseCue(Hold(0.04f), "bandage_wrap", 0.35f),
        new UseCue(Hold(0.37f), "bandage_wrap", 0.35f),
        new UseCue(Hold(0.70f), "bandage_wrap", 0.4f),
    };
    static readonly UseCue[] MedkitCues =
    {
        new UseCue(0f, "medkit_open", 0.5f),
        // 흔들기 네 번 (sin(hold·8π)) 동안 안의 물건이 달그락
        new UseCue(Hold(0.02f), "medkit_rattle", 0.4f),
        new UseCue(Hold(0.5f), "medkit_rattle", 0.3f),
    };
    static readonly UseCue[] NoCues = new UseCue[0];

    // 이 아이템을 쓸 때의 소리 박자. 녹음이 하나도 없으면 빈 배열 (그때는 예전 바스락 소리를 쓴다)
    public static UseCue[] UseCues(string itemId, UseGesture gesture)
    {
        UseCue[] cues;
        switch (itemId)
        {
            case "water": cues = DrinkCues; break;
            case "canned_food": cues = CanCues; break;
            case "cooked_meat": cues = MeatCues; break;
            case "bandage": cues = BandageCues; break;
            case "medkit": cues = MedkitCues; break;
            default:
                // 나중에 생길 아이템: 몸짓에 맞는 소리 (먹기는 통조림처럼 씹기만)
                cues = gesture == UseGesture.Drink ? DrinkCues : gesture == UseGesture.Eat ? MeatCues
                    : gesture == UseGesture.Bandage ? BandageCues : gesture == UseGesture.Medkit ? MedkitCues : NoCues;
                break;
        }
        return cues.Length > 0 && UseClip(cues[cues.Length - 1].Sound) != null ? cues : NoCues;
    }

    public static AudioClip UseClip(string sound) => Recorded(UseFolder, sound);

    // 몸짓 없이 썼을 때 (연출 중이거나 손이 감춰졌거나, 몸짓을 시작하기 전에 다음 사용에 밀렸을 때): 첫 소리 하나만 바로 낸다.
    // 녹음이 없는 아이템은 GameManager.UseItem이 이미 바스락 소리를 냈으니 아무것도 하지 않는다
    public static void PlayUseNow(string itemId, UseGesture gesture)
    {
        var cues = UseCues(itemId, gesture);
        if (cues.Length > 0) PlayUseCue(cues[0]);
    }

    // 박자 하나를 낸다 (같은 소리가 똑같이 들리지 않게 음높이를 조금씩 바꾼다)
    public static void PlayUseCue(UseCue cue)
    {
        var clip = UseClip(cue.Sound);
        if (clip != null) ProceduralAudio.Play2D(clip, cue.Volume, Random.Range(0.96f, 1.04f));
    }
}
