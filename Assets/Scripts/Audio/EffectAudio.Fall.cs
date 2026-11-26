using UnityEngine;

// 쓰러진 몸이 바닥에 닿는 소리 (BodyImpact가 낸다): Resources/Audio/Fall/<소리>_<0~2>.wav (Higgsfield Seed Audio로 만든 폴리 녹음)를 번갈아 쓴다.
// 사람 몸통: 딱딱한 바닥(콘크리트·아스팔트·나무·쇠)이면 fall_hard, 흙·풀·천이면 fall_soft, 웅덩이면 fall_water. 들개는 dog_fall. 팔다리는 limb.
// 녹음이 없으면 예전 합성 쿵 소리(ProceduralAudio.Bang)를 쓴다
public static partial class EffectAudio
{
    const string FallFolder = "Audio/Fall/";

    public static AudioClip BodyFall(Vfx.ImpactKind ground, bool dog)
    {
        string key = ground == Vfx.ImpactKind.Water ? "fall_water"
            : dog ? "dog_fall"
            : ground == Vfx.ImpactKind.Dirt || ground == Vfx.ImpactKind.Soft ? "fall_soft" : "fall_hard";
        var clip = Recorded(FallFolder, key);
        return clip != null ? clip : ProceduralAudio.Bang();
    }

    // 팔·다리·머리가 툭 떨어지는 소리 (녹음이 없으면 null: 소리 없이 넘어간다)
    public static AudioClip LimbThud() => Recorded(FallFolder, "limb");
}
