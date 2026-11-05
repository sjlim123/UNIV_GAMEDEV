using UnityEngine;

// 켜진 간판의 고장 난 전기: 글자 빛이 불규칙하게 들어왔다 나가고, 꺼지는 순간 가끔 노란 스파크가 튀며 파지직 소리가 난다
// (라디오의 Spark와 같은 Vfx.Zap·EffectAudio.Zap). 도시 씬 빌더가 켜진 간판마다 붙이고 글자 면·등·스파크 자리를 넣어 준다
public class SignFlicker : MonoBehaviour
{
    public Renderer face;                      // 글자 면 (emission 재질)
    public Light lamp;                         // 간판 등
    public Vector3 sparkLocal;                 // 스파크 자리 (이 물체 기준 로컬)
    public Vector3 sparkNormal = Vector3.back; // 스파크가 튀는 방향 (로컬)
    [Range(0f, 1f)] public float sparkChance = 0.35f; // 꺼질 때 스파크가 튈 확률

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    Material mat;
    Color baseEmission;
    float timer;
    bool on = true;

    void Start()
    {
        if (face != null)
        {
            mat = face.material; // 간판마다 따로 깜빡이게 사본을 쓴다
            baseEmission = mat.GetColor(EmissionId);
        }
        timer = Random.Range(0.3f, 3f);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        on = !on;
        if (on)
        {
            timer = Random.Range(0.35f, 2.8f);
            Set(1f);
        }
        else
        {
            timer = Random.Range(0.04f, 0.45f);
            Set(Random.value < 0.5f ? 0f : 0.12f);
            if (Random.value < sparkChance) Spark();
        }
    }

    void Set(float k)
    {
        if (mat != null) mat.SetColor(EmissionId, baseEmission * k);
        if (lamp != null) lamp.enabled = k > 0.05f;
    }

    void Spark()
    {
        var pos = transform.TransformPoint(sparkLocal);
        var n = transform.TransformDirection(sparkNormal);
        Vfx.Zap(pos, n, 0.6f, 2);
        ProceduralAudio.PlayAt(EffectAudio.Zap(Random.Range(0, 3)), pos, 0.45f, Random.Range(0.9f, 1.1f), 1f, 16f, SoundCategory.Ambient);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
