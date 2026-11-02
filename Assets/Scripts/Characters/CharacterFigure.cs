using UnityEngine;

// 움직이지 않는 인물 (메인 화면 배경의 좀비, 도시의 잡상인): 실행하면 디자인 시트 모양으로 몸을 짓고
// 정해 둔 자세를 잡은 채 숨을 쉬듯 살짝 흔들린다. 잡상인이면 등불에 불을 켜고 Trader에 머리를 넘겨 준다
public class CharacterFigure : MonoBehaviour
{
    public FigureKind kind = FigureKind.Zombie;
    public int variant;
    // 자세 (관절별 회전, 도)
    public Vector3 bodyPose, headPose, armLPose, armRPose, legLPose, legRPose;
    // 눈·등불이 빛나는 재질의 원본 (빌드에 발광 셰이더가 들어가게 들고 있는다)
    public Material glowTemplate;

    CharacterRig rig;
    Quaternion bodyBase, headBase, armLBase, armRBase;
    float phase;

    public CharacterRig Rig => rig;

    void Awake()
    {
        if (glowTemplate != null) ProcTex.GlowTemplate = glowTemplate;
        rig = CharacterModels.Build(transform, kind, variant);
        if (rig.Body != null && bodyPose != Vector3.zero) rig.Body.localRotation = Quaternion.Euler(bodyPose);
        if (rig.Head != null && headPose != Vector3.zero) rig.Head.localRotation = Quaternion.Euler(headPose);
        if (rig.ArmL != null && armLPose != Vector3.zero) rig.ArmL.localRotation = Quaternion.Euler(armLPose);
        if (rig.ArmR != null && armRPose != Vector3.zero) rig.ArmR.localRotation = Quaternion.Euler(armRPose);
        if (rig.LegL != null && legLPose != Vector3.zero) rig.LegL.localRotation = Quaternion.Euler(legLPose);
        if (rig.LegR != null && legRPose != Vector3.zero) rig.LegR.localRotation = Quaternion.Euler(legRPose);
        if (rig.ForeL != null && kind == FigureKind.Zombie) rig.ForeL.localRotation = Quaternion.Euler(-15, 0, 0);
        if (rig.ForeR != null && kind == FigureKind.Zombie) rig.ForeR.localRotation = Quaternion.Euler(-22, 0, 0);
        if (rig.Jaw != null && kind == FigureKind.Zombie) rig.Jaw.localRotation = Quaternion.Euler(16, 0, 0);
        bodyBase = rig.Body != null ? rig.Body.localRotation : Quaternion.identity;
        headBase = rig.Head != null ? rig.Head.localRotation : Quaternion.identity;
        armLBase = rig.ArmL != null ? rig.ArmL.localRotation : Quaternion.identity;
        armRBase = rig.ArmR != null ? rig.ArmR.localRotation : Quaternion.identity;
        phase = Random.value * 10f;

        // 잡상인: 등짐에 매단 등불을 켠다 (그림자 없음)
        if (rig.Lantern != null)
        {
            var lg = new GameObject("LanternGlow");
            lg.transform.SetParent(rig.Lantern, false);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.68f, 0.35f);
            l.intensity = 1.6f;
            l.range = 4.5f;
            l.shadows = LightShadows.None;
            var flicker = lg.AddComponent<FlickerLight>();
            flicker.baseIntensity = 1.6f;
            flicker.flickerAmount = 0.15f;
            flicker.speed = 6f;
        }
        var trader = GetComponent<Trader>();
        if (trader != null) trader.head = rig.Head;
    }

    void Update()
    {
        if (rig == null) return;
        phase += Time.deltaTime;
        // 숨쉬기: 몸통이 아주 조금 오르내리고 팔이 따라 흔들린다. 좀비는 머리를 천천히 까딱인다
        float breath = Mathf.Sin(phase * 1.6f);
        if (rig.Body != null) rig.Body.localRotation = bodyBase * Quaternion.Euler(breath * 1.2f, Mathf.Sin(phase * 0.4f) * 2f, 0);
        if (rig.ArmL != null) rig.ArmL.localRotation = armLBase * Quaternion.Euler(breath * 2f, 0, 0);
        if (rig.ArmR != null && kind != FigureKind.Merchant) rig.ArmR.localRotation = armRBase * Quaternion.Euler(-breath * 2f, 0, 0);
        if (rig.Head != null && kind == FigureKind.Zombie)
            rig.Head.localRotation = headBase * Quaternion.Euler(Mathf.Sin(phase * 0.7f) * 4f, Mathf.Sin(phase * 0.31f) * 8f, Mathf.Sin(phase * 0.5f) * 5f);
    }
}
