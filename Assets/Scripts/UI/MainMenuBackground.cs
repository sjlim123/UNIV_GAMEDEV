using UnityEngine;

// 메인 메뉴 배경: 폐허 거리를 천천히 떠도는 카메라와 바람 소리. 메뉴 UI는 UIManager가 띄운다.
public class MainMenuBackground : MonoBehaviour
{
    public Transform cameraTransform;
    public float driftAmount = 1.2f;

    Vector3 startPos;
    Quaternion startRot;
    AudioSource wind;
    float groanTimer = 6f;

    void Start()
    {
        startPos = cameraTransform.position;
        startRot = cameraTransform.rotation;
        wind = gameObject.AddComponent<AudioSource>();
        wind.clip = ProceduralAudio.Wind();
        wind.loop = true;
        wind.volume = 0.35f;
        wind.Play();
    }

    void Update()
    {
        float t = Time.time;
        cameraTransform.position = startPos + new Vector3(Mathf.Sin(t * 0.05f) * driftAmount, Mathf.Sin(t * 0.09f) * 0.15f, Mathf.Sin(t * 0.035f) * driftAmount * 0.8f);
        cameraTransform.rotation = startRot * Quaternion.Euler(Mathf.Sin(t * 0.07f) * 1.2f, Mathf.Sin(t * 0.04f) * 3f, 0);

        // 멀리서 가끔 들려오는 신음
        groanTimer -= Time.deltaTime;
        if (groanTimer <= 0)
        {
            groanTimer = Random.Range(10f, 22f);
            var pos = cameraTransform.position + Quaternion.Euler(0, Random.Range(-60f, 60f), 0) * cameraTransform.forward * Random.Range(25f, 40f);
            ProceduralAudio.PlayAt(ProceduralAudio.Groan(), pos, 0.6f, Random.Range(0.6f, 0.8f), 1f, 60f);
        }
    }
}
