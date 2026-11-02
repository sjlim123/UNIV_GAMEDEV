using UnityEngine;

// 메인 메뉴 배경: 방공호 입구 터널 안에서 천천히 흔들리는 카메라와 바람·빗소리. 메뉴 UI는 UIManager가 띄운다.
public class MainMenuBackground : MonoBehaviour
{
    public Transform cameraTransform;
    public float driftAmount = 1.2f;
    // 고개를 흔드는 정도 (1이면 위아래 ±1.2도, 좌우 ±3도)
    public float lookDrift = 1f;
    // 빗소리를 낸다 (메뉴 씬에 비가 내릴 때)
    public bool rain = true;

    Vector3 startPos;
    Quaternion startRot;
    AudioSource wind, rainSource;
    float groanTimer = 6f;
    static bool silenced;

    // 새 게임 컷씬이 시작되면 바람·비·신음을 멈춘다 (다음에 메뉴 씬이 열리면 다시 난다)
    public static void Silence() => silenced = true;

    void Start()
    {
        silenced = false;
        startPos = cameraTransform.position;
        startRot = cameraTransform.rotation;
        wind = Loop(ProceduralAudio.Wind(), 0.22f);
        if (rain) rainSource = Loop(ProceduralAudio.Rain(), 0.42f);
    }

    AudioSource Loop(AudioClip clip, float volume)
    {
        var src = gameObject.AddComponent<AudioSource>();
        AudioCategory.Tag(src, SoundCategory.Ambient);
        src.clip = clip;
        src.loop = true;
        src.volume = volume;
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

        // 멀리서 가끔 들려오는 신음
        groanTimer -= Time.deltaTime;
        if (groanTimer <= 0)
        {
            groanTimer = Random.Range(10f, 22f);
            var pos = cameraTransform.position + Quaternion.Euler(0, Random.Range(-60f, 60f), 0) * cameraTransform.forward * Random.Range(25f, 40f);
            ProceduralAudio.PlayAt(ProceduralAudio.Groan(), pos, 0.6f, Random.Range(0.8f, 0.95f), 1f, 60f, SoundCategory.Ambient);
        }
    }
}
