using UnityEngine;

// 메인 메뉴 배경: 방공호 입구 터널 안에서 천천히 흔들리는 카메라와 바람·빗소리. 메뉴 UI는 UIManager가 띄운다.
// 소리는 메뉴가 뜰 때 3.5초 동안 서서히 커진다 (갑자기 '쏴' 하지 않게). 빗소리는 메인 화면 음량에서 10% 더 작다 (2026-11-05, 사용자 요청).
// 어떤 까닭으로든 멈춘 바람·비 루프는 다시 튼다 (메뉴에 오래 있으면 신음만 남던 문제)
public class MainMenuBackground : MonoBehaviour
{
    public Transform cameraTransform;
    public float driftAmount = 1.2f;
    // 고개를 흔드는 정도 (1이면 위아래 ±1.2도, 좌우 ±3도)
    public float lookDrift = 1f;
    // 빗소리를 낸다 (메뉴 씬에 비가 내릴 때)
    public bool rain = true;

    // 메인 화면 소리는 전체보다 10% 더 작게(바람 0.2). 빗소리는 거기서 10% 더(0.38 → 0.34)
    const float WindVolume = 0.2f, RainVolume = 0.34f, FadeInSeconds = 3.5f;

    Vector3 startPos;
    Quaternion startRot;
    AudioSource wind, rainSource;
    float groanTimer = 6f, fadeT;
    static bool silenced;

    // 새 게임 컷씬이 시작되면 바람·비·신음을 멈춘다 (다음에 메뉴 씬이 열리면 다시 난다)
    public static void Silence() => silenced = true;

    void Start()
    {
        silenced = false;
        startPos = cameraTransform.position;
        startRot = cameraTransform.rotation;
        wind = Loop(ProceduralAudio.Wind());
        if (rain) rainSource = Loop(ProceduralAudio.Rain());
    }

    // 음량 0으로 시작하고 Update에서 서서히 올린다
    AudioSource Loop(AudioClip clip)
    {
        var src = gameObject.AddComponent<AudioSource>();
        AudioCategory.Tag(src, SoundCategory.Ambient);
        src.clip = clip;
        src.loop = true;
        src.volume = 0f;
        src.time = Random.Range(0f, clip.length * 0.9f);
        src.Play();
        return src;
    }

    void Update()
    {
        float t = Time.time;
        cameraTransform.position = startPos + new Vector3(Mathf.Sin(t * 0.05f) * driftAmount, Mathf.Sin(t * 0.09f) * 0.12f * driftAmount, Mathf.Sin(t * 0.035f) * driftAmount * 0.8f);
        cameraTransform.rotation = startRot * Quaternion.Euler(Mathf.Sin(t * 0.07f) * 1.2f * lookDrift, Mathf.Sin(t * 0.04f) * 3f * lookDrift, 0);

        if (silenced)
        {
            if (wind != null && wind.isPlaying) wind.Stop();
            if (rainSource != null && rainSource.isPlaying) rainSource.Stop();
            return;
        }

        // 서서히 커지는 바람·비 (AudioCategory가 매 프레임 종류 음량을 곱한다)
        fadeT = Mathf.Min(1f, fadeT + Time.deltaTime / FadeInSeconds);
        float k = fadeT * fadeT;
        Keep(wind, WindVolume * k);
        Keep(rainSource, RainVolume * k);

        // 멀리서 가끔 들려오는 신음
        groanTimer -= Time.deltaTime;
        if (groanTimer <= 0)
        {
            groanTimer = Random.Range(10f, 22f);
            var pos = cameraTransform.position + Quaternion.Euler(0, Random.Range(-60f, 60f), 0) * cameraTransform.forward * Random.Range(25f, 40f);
            ProceduralAudio.PlayAt(ProceduralAudio.ZombieGroan(), pos, 0.6f, Random.Range(0.8f, 0.95f), 1f, 60f, SoundCategory.Ambient);
        }
    }

    // 음량을 맞추고, 멈춰 있으면 다시 튼다
    static void Keep(AudioSource src, float volume)
    {
        if (src == null) return;
        src.volume = volume;
        if (!src.isPlaying) src.Play();
    }
}
