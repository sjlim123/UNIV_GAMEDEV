using UnityEngine;

// 코드로 합성하던 소리를 실제 녹음으로 바꾼 것 (Higgsfield Seed Audio로 만든 폴리·환경음): Resources/Audio/World/<이름>_<0~>.wav
//   wind_loop·rain_loop (반복해도 이음새가 들리지 않게 끝을 처음에 섞은 바람·비), creak·latch·door_slam·clang (문·걸쇠·쇠),
//   door_roll·door_stop·door_unlatch·pneumatic_in/out (미닫이문·방폭문), hurt·hit·jump·land·rustle (플레이어), bark·growl·snarl·yelp·whine·snuffle (들개)
// ProceduralAudio·EffectAudio·DogAudio의 해당 함수가 먼저 이것을 찾고, 없으면 예전 합성 소리를 낸다. 크기는 예전 합성 소리와 같게 맞춰 두었다.
// 같은 합성 소리를 다른 뜻으로도 쓰던 곳은 따로 둔다: 문 쾅 DoorSlam(처치 연출은 합성 Bang), 플레이어 착지 PlayerLand(떨어진 아이템은 합성 Land),
// 플레이어 목소리 PlayerHurt(약탈자는 합성 Hurt)
public static partial class EffectAudio
{
    const string WorldFolder = "Audio/World/";

    public static AudioClip World(string key) => Recorded(WorldFolder, key);
}
