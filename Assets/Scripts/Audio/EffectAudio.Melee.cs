using UnityEngine;

// 근접 무기마다 다른 소리: 적을 맞히는 소리(hit), 쓰러뜨리는 마무리 소리(kill), 가죽 갑옷을 친 소리(armor), 휘두르는 소리(swing).
// Resources/Audio/Melee/<종류>_<무기>_<0~2>.wav (Higgsfield Seed Audio로 만든 폴리 녹음)를 번갈아 쓰고, 없으면 예전 합성 소리(ProceduralAudio)를 쓴다.
// 무기: fist(맨손·라이터) · woodbat(야구 방망이) · spiked(못 방망이) · alloy(쇠 방망이) · dagger(단검). 휘두르는 소리는 나무·못 방망이가 같다(bat).
public static partial class EffectAudio
{
    const string MeleeFolder = "Audio/Melee/";

    static string WeaponKey(ItemDef weapon)
    {
        if (weapon == null) return "fist";
        switch (weapon.Id)
        {
            case "baseball_bat": return "woodbat";
            case "spiked_bat": return "spiked";
            case "alloy_bat": return "alloy";
            case "dagger": return "dagger";
        }
        // 나중에 생길 무기: 찌르는 것은 단검, 휘두르는 것은 나무 방망이 소리
        return weapon.Weapon == WeaponKind.Stab ? "dagger" : weapon.Weapon == WeaponKind.Swing ? "woodbat" : "fist";
    }

    // 적을 맞혔다. killed면 더 무거운 마무리 소리
    public static AudioClip MeleeHit(ItemDef weapon, bool killed)
    {
        var clip = Recorded(MeleeFolder, (killed ? "kill_" : "hit_") + WeaponKey(weapon));
        return clip != null ? clip : killed ? ProceduralAudio.KillHit() : ProceduralAudio.FleshHit();
    }

    // 가죽 갑옷을 쳤다 (단검은 베는 소리, 나머지는 둔탁한 소리). 녹음이 없으면 null
    public static AudioClip ArmorHit(ItemDef weapon) =>
        Recorded(MeleeFolder, WeaponKey(weapon) == "dagger" ? "armor_blade" : "armor_blunt");

    // 휘두르는 소리
    public static AudioClip MeleeSwing(ItemDef weapon)
    {
        string w = WeaponKey(weapon);
        var clip = Recorded(MeleeFolder, "swing_" + (w == "woodbat" || w == "spiked" ? "bat" : w));
        return clip != null ? clip : ProceduralAudio.Swoosh();
    }
}
