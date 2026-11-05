using UnityEngine;

// 건물 벽에 기대 앉은 채 죽은 사람. 실행되면 몸(CharacterModels.Corpse)을 짓고 앉은 자세를 잡는다 (숨 쉬지 않는다).
// 상호작용(F)하면 이미 죽었다는 짧은 글이 뜬다 (다섯 가지 중 하나, 바로 앞 것과는 다르게). 도시 씬 빌더가 벽 앞에 놓는다
public class DeadBody : Interactable
{
    public int variant;
    public int poseSeed;

    static readonly string[] Lines =
    {
        "이미 죽은 것 같다.",
        "숨을 쉬지 않는다. 죽은 지 오래됐다.",
        "차갑게 식어 있다. 해 줄 수 있는 게 없다.",
        "벽에 기댄 채 그대로 굳어 있다. 너무 늦었다.",
        "눈을 감은 채 조용히 앉아 있다. 고통 없이 갔기를.",
    };
    static int lastLine = -1;

    public override string Prompt => "시체 - 살펴보기";

    void Awake() => Build();

    // 몸을 짓고 앉은 자세를 잡는다 (편집기 미리보기에서도 부른다)
    public void Build()
    {
        var rig = CharacterModels.Build(transform, FigureKind.Corpse, variant);
        var r = new System.Random(poseSeed);
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
        int side = r.NextDouble() < 0.5 ? -1 : 1;
        // 엉덩이를 바닥에 (사람 뼈대는 발밑이 원점이라 골반 높이만큼 내린다)
        rig.Model.localPosition = new Vector3(0, -0.8f, 0);
        // 등을 벽에 기대고 한쪽으로 기운 몸통, 떨군 고개
        rig.Body.localRotation = Quaternion.Euler(F(-16f, -8f), side * F(0f, 8f), side * F(4f, 12f));
        rig.Head.localRotation = Quaternion.Euler(F(32f, 50f), side * F(6f, 24f), side * F(12f, 30f));
        // 다리: 한쪽은 쭉 뻗고, 한쪽은 무릎을 세우거나 둘 다 뻗는다
        bool kneeUp = r.NextDouble() < 0.6;
        int upSide = r.NextDouble() < 0.5 ? -1 : 1;
        SetLeg(rig.LegL, rig.ShinL, -1, kneeUp && upSide < 0, r);
        SetLeg(rig.LegR, rig.ShinR, 1, kneeUp && upSide > 0, r);
        // 팔: 양옆으로 힘없이 늘어지고 아래팔은 허벅지·바닥에 얹힌다. 한쪽 손은 배 위에 올라가기도 한다
        bool lap = r.NextDouble() < 0.4;
        rig.ArmL.localRotation = Quaternion.Euler(F(-4f, 8f), 0, -F(8f, 20f));
        rig.ForeL.localRotation = Quaternion.Euler(-F(22f, 48f), 0, F(0f, 10f));
        if (lap)
        {
            rig.ArmR.localRotation = Quaternion.Euler(-18f, 0, F(18f, 30f));
            rig.ForeR.localRotation = Quaternion.Euler(-F(70f, 85f), 0, -F(15f, 25f));
        }
        else
        {
            rig.ArmR.localRotation = Quaternion.Euler(F(-4f, 8f), 0, F(8f, 20f));
            rig.ForeR.localRotation = Quaternion.Euler(-F(22f, 48f), 0, -F(0f, 10f));
        }
        rig.HandL.localRotation = Quaternion.Euler(F(10f, 25f), 0, F(-10f, 10f));
        rig.HandR.localRotation = Quaternion.Euler(F(10f, 25f), 0, F(-10f, 10f));
    }

    // 다리 하나: 뻗으면 허벅지가 바닥에 눕고 정강이가 앞으로 이어지며(발끝이 위를 본다), 무릎을 세우면 허벅지가 비스듬히 오르고 정강이가 내려온다
    static void SetLeg(Transform leg, Transform shin, int side, bool kneeUp, System.Random r)
    {
        if (leg == null || shin == null) return;
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
        if (kneeUp)
        {
            leg.localRotation = Quaternion.Euler(-F(112f, 124f), side * F(0f, 6f), side * -F(3f, 9f));
            shin.localRotation = Quaternion.Euler(F(86f, 100f), 0, 0);
        }
        else
        {
            leg.localRotation = Quaternion.Euler(-F(84f, 90f), side * F(0f, 10f), side * -F(4f, 14f));
            shin.localRotation = Quaternion.Euler(F(4f, 16f), 0, 0);
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        int i;
        do i = Random.Range(0, Lines.Length); while (i == lastLine && Lines.Length > 1);
        lastLine = i;
        GameManager.Instance.UI.ShowMessage(Lines[i], 3f);
        ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.3f, 0.8f);
    }
}
