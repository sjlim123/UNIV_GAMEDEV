using System.Collections.Generic;
using UnityEngine;

// 손에 드는 모양 (아이템마다 다르다)
public enum GripKind
{
    Power, // 손잡이를 주먹으로 감아쥔다 (방망이·병·통조림·배터리·손전등)
    Palm,  // 손바닥을 위로 펴고 올려 든다 (천·판·부품·큰 물건)
    Hook,  // 손잡이를 쥐고 물건을 아래로 매단다 (가방 손잡이·연료통·바퀴·방어구 어깨끈)
}

// 팔을 드는 자세 (화면 속 손 위치와 방향)
public enum HoldPose
{
    Ready,      // 빈손: 힘을 빼고 오른쪽 아래에
    Upright,    // 손잡이를 세워 끝을 어깨 쪽으로 살짝 젖혀 든다 (방망이·망치·새총)
    Hold,       // 똑바로 세워 든다 (병·통조림·고기·라이터)
    Forward,    // 손잡이를 앞으로 겨눠 든다 (단검 날·손전등 빛이 앞)
    Palm,       // 손바닥을 위로 펴 내민다
    Hook,       // 가로로 누운 손잡이를 쥐고 앞으로 내민다 (물건은 주먹 아래로 매달린다)
}

// 쓰는(우클릭) 몸짓
public enum UseGesture { Generic, Drink, Eat, Bandage, Medkit }

// 아이템 하나를 손에 드는 법. 좌표는 모두 ItemVisuals가 만든 아이템 좌표 (손에 들 때 Scale을 곱한다)
public class HeldSpec
{
    public GripKind Grip = GripKind.Palm;
    public HoldPose Pose = HoldPose.Palm;
    // Power·Hook: 쥐는 손잡이 가운데와 그 축 (축의 + 쪽이 엄지 쪽으로 간다), 손잡이 반지름
    // Palm: Point를 비우면(AutoPoint) 아이템 바닥의 가까운 가장자리를 손바닥에 올리고 엄지로 위를 누른다 (아이템은 손가락 너머로 뻗는다).
    //       Axis = 손바닥에서 멀어지는 쪽(아이템 위)
    public Vector3 Point, Axis = Vector3.up;
    // 둘째 방향: 손가락 쪽(손 +z)으로 가는 아이템 방향. Hook은 아이템 위쪽 (손가락 반대쪽으로 매달린다)
    public Vector3 Second = Vector3.forward;
    public float Radius = 0.02f;
    public float Scale = 1f;
    public bool AutoPoint;
    // 아이템마다 자세를 조금 더 옮기고 돌린다 (화면 좌표, m·도)
    public Vector3 Offset, Euler;
    public UseGesture Use = UseGesture.Generic;
    // 손에 들 때 감출 부품 (부싯돌과 라이터의 부싯돌 등). ItemVisuals가 만든 순서
    public int[] Hide;
}

public static class HeldItems
{
    static readonly Dictionary<string, HeldSpec> specs = new Dictionary<string, HeldSpec>();

    static HeldSpec Power(HoldPose pose, Vector3 point, Vector3 axis, Vector3 second, float radius, float scale = 1f) =>
        new HeldSpec { Grip = GripKind.Power, Pose = pose, Point = point, Axis = axis, Second = second, Radius = radius, Scale = scale };

    static HeldSpec Palm(float scale, Vector3? up = null, Vector3? forward = null) =>
        new HeldSpec { Grip = GripKind.Palm, Pose = HoldPose.Palm, AutoPoint = true, Axis = up ?? Vector3.up, Second = forward ?? Vector3.forward, Scale = scale };

    static HeldSpec Hook(Vector3 point, Vector3 axis, Vector3 up, float radius, float scale = 1f) =>
        new HeldSpec { Grip = GripKind.Hook, Pose = HoldPose.Hook, Point = point, Axis = axis, Second = up, Radius = radius, Scale = scale };

    static HeldItems()
    {
        var bat = Power(HoldPose.Upright, new Vector3(0, 0.034f, -0.24f), Vector3.forward, Vector3.up, 0.019f, 0.9f);
        specs["baseball_bat"] = bat;
        specs["spiked_bat"] = Power(HoldPose.Upright, bat.Point, bat.Axis, bat.Second, bat.Radius, bat.Scale);
        specs["alloy_bat"] = Power(HoldPose.Upright, bat.Point, bat.Axis, bat.Second, bat.Radius, bat.Scale);
        // 망치: 자루 아래쪽을 쥐고 때리는 면(-x)이 앞으로
        specs["hammer"] = Power(HoldPose.Upright, new Vector3(0, 0.025f, -0.09f), Vector3.forward, Vector3.left, 0.0175f);
        // 단검: 손잡이를 쥐고 날을 앞으로 겨눈다 (날 끝이 엄지 쪽, 칼날이 위아래)
        specs["dagger"] = Power(HoldPose.Forward, new Vector3(0, 0.02f, -0.005f), Vector3.forward, Vector3.right, 0.016f);
        // 새총: 손잡이를 쥐고 갈래를 세운다. 가죽 주머니(+z)는 내 쪽으로
        specs["slingshot"] = Power(HoldPose.Upright, new Vector3(0, 0.06f, 0), Vector3.up, Vector3.back, 0.015f);
        specs["stick"] = Power(HoldPose.Upright, new Vector3(0.02f, 0.025f, 0), Vector3.right, Vector3.up, 0.025f);

        // 마시고 먹는 것: 몸통을 감아쥔다
        specs["water"] = Power(HoldPose.Hold, new Vector3(0, 0.13f, 0), Vector3.up, Vector3.forward, 0.045f, 0.78f);
        specs["water"].Use = UseGesture.Drink;
        specs["canned_food"] = Power(HoldPose.Hold, new Vector3(0, 0.06f, 0), Vector3.up, Vector3.forward, 0.0465f);
        specs["canned_food"].Use = UseGesture.Eat;
        // 고기(닭다리): 뼈 자루를 쥐고 살이 엄지 쪽(위)으로. 자루 자리·축은 ItemVisuals.Meat.cs가 정한다. 생고기는 먹는 몸짓이 없다 (화로에서 구워야 한다)
        specs["raw_meat"] = Power(HoldPose.Hold, ItemVisuals.MeatGripPoint, ItemVisuals.MeatAxis, ItemVisuals.MeatUp, 0.0125f, 0.85f);
        specs["cooked_meat"] = Power(HoldPose.Hold, ItemVisuals.MeatGripPoint, ItemVisuals.MeatAxis, ItemVisuals.MeatUp, 0.0125f, 0.85f);
        specs["cooked_meat"].Use = UseGesture.Eat;
        specs["bandage"] = Power(HoldPose.Hold, new Vector3(0, 0.03f, 0), Vector3.up, Vector3.forward, 0.05f, 0.75f);
        specs["bandage"].Use = UseGesture.Bandage;
        specs["medkit"] = Palm(0.6f);
        specs["medkit"].Use = UseGesture.Medkit;

        // 작은 것: 주먹에 감아쥔다
        specs["battery"] = Power(HoldPose.Hold, new Vector3(0, 0.025f, 0), Vector3.right, Vector3.up, 0.025f, 0.5f);
        // 부싯돌과 라이터: 라이터 몸통을 쥐고 뚜껑이 위로 (부싯돌은 감춘다). 넓은 면이 손바닥에 닿는다
        specs[ItemDatabase.LighterId] = Power(HoldPose.Hold, new Vector3(0, 0.045f, 0), Vector3.up, Vector3.right, 0.0125f);
        specs[ItemDatabase.LighterId].Hide = new[] { 3 };
        // 돌주머니: 묶은 목을 쥐고 자루가 아래로
        specs[ItemDatabase.StonePouchId] = Power(HoldPose.Hold, new Vector3(0, 0.175f, 0), Vector3.up, Vector3.forward, 0.045f, 0.85f);

        // 손바닥에 올려 드는 것
        specs["screw"] = Palm(1f);
        specs["semiconductor"] = Palm(0.6f);
        specs["circuit_board"] = Palm(0.8f);
        specs["stone"] = Palm(0.8f);
        specs["rope"] = Palm(0.75f);
        specs["copper_wire"] = Palm(0.8f);
        specs["rubber"] = Palm(0.8f);
        specs["leather"] = Palm(0.7f);
        specs["cloth"] = Palm(0.6f);
        specs["metal"] = Palm(0.7f);
        specs["iron_ingot"] = Palm(0.8f);
        specs["alloy_plate"] = Palm(0.6f);
        specs["motor"] = Palm(0.5f);
        specs["engine"] = Palm(0.42f);
        specs[ItemDatabase.FurnaceId] = Palm(0.42f);
        // 네비게이션: 화면(-z)이 나를 보게 손바닥에 세워 든다
        specs["navigation"] = Palm(0.8f, Vector3.back, Vector3.up);
        specs["navigation"].Euler = new Vector3(-50, 0, 0);

        // 매달아 드는 것
        // 가방: 위쪽 손잡이 고리 (앞주머니가 나를 보게)
        specs["old_backpack"] = Hook(new Vector3(0, 0.445f, -0.01f) * 0.9f, Vector3.left, Vector3.up, 0.007f, 0.72f);
        specs["backpack"] = Hook(new Vector3(0, 0.445f, -0.01f), Vector3.left, Vector3.up, 0.007f, 0.7f);
        specs["large_backpack"] = Hook(new Vector3(0, 0.445f, -0.01f) * 1.15f, Vector3.left, Vector3.up, 0.007f, 0.66f);
        // 방어구: 오른쪽 어깨끈 (조끼가 10° 뒤로 기대 있다)
        var strap = new Vector3(0.11f, 0.4385f, 0.0747f);
        var strapAxis = new Vector3(0, -0.174f, 0.985f);
        specs["leather_armor"] = Hook(strap, strapAxis, Vector3.up, 0.025f, 0.72f);
        specs["iron_armor"] = Hook(strap, strapAxis, Vector3.up, 0.025f, 0.72f);
        specs["alloy_armor"] = Hook(strap, strapAxis, Vector3.up, 0.025f, 0.72f);
        specs["fuel"] = Hook(new Vector3(0, 0.49f, -0.02f), Vector3.forward, Vector3.up, 0.01f, 0.7f);
        // 바퀴: 위쪽 타이어를 손가락으로 걸어 든다
        specs["wheel"] = Hook(new Vector3(0, 0.3625f, 0), Vector3.forward, Vector3.up, 0.043f, 0.72f);

        // 손전등 (오른손으로 들 때): 몸통을 쥐고 빛이 앞으로
        specs["flashlight"] = Power(HoldPose.Forward, new Vector3(0, 0.028f, -0.03f), Vector3.forward, Vector3.up, 0.02f);
    }

    // 왼손 손전등: 손등이 위로 오게 위에서 감아쥔다. 렌즈(+z)가 새끼손가락 쪽으로 나오도록 손잡이 축을 뒤집고, 버튼(+y)은 손가락 쪽
    public static readonly HeldSpec LeftFlashlight = Power(HoldPose.Forward, new Vector3(0, 0.028f, -0.01f), Vector3.back, Vector3.up, 0.02f);

    public static HeldSpec Get(string id)
    {
        if (id != null && specs.TryGetValue(id, out var s)) return s;
        // 모르는 아이템은 손바닥에 올린다
        var spec = Palm(0.7f);
        if (id != null) specs[id] = spec;
        return spec;
    }
}
