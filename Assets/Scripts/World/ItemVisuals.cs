using System.Collections.Generic;
using UnityEngine;

// 아이템마다 다른 모양을 기본 도형으로 만든다 (바닥 아이템, 손에 든 물건, 아이콘이 함께 쓴다)
public static partial class ItemVisuals
{
    static readonly Color Steel = new Color(0.6f, 0.6f, 0.63f);
    static readonly Color DarkSteel = new Color(0.3f, 0.3f, 0.32f);
    static readonly Color Black = new Color(0.07f, 0.07f, 0.07f);
    static readonly Color Wood = new Color(0.62f, 0.46f, 0.28f);

    public static void Build(string itemId, Transform t)
    {
        switch (itemId)
        {
            case "water":
                PetBottle(t);
                break;
            // 사실적인 소품(통조림·붕대·구급상자·배터리·손전등·연료통)은 ItemVisuals.Models.cs
            case "canned_food":
                CannedFood(t);
                break;
            case "bandage":
                Bandage(t);
                break;
            case "medkit":
                Medkit(t);
                break;
            case "cloth":
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.025f, 0), new Vector3(0.4f, 0.05f, 0.3f), new Color(0.55f, 0.48f, 0.38f), 0.05f, new Vector3(0, 0, 3));
                break;
            case "stick":
                // 막대기: 껍질이 붙은 나무 막대 + 잘린 단면 + 잔가지
                var bark = new Color(0.36f, 0.25f, 0.14f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.025f, 0), new Vector3(0.05f, 0.3f, 0.05f), bark, 0.1f, new Vector3(0, 0, 90));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.3f, 0.025f, 0), new Vector3(0.046f, 0.004f, 0.046f), new Color(0.72f, 0.58f, 0.38f), 0.1f, new Vector3(0, 0, 90));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.12f, 0.041f, 0.058f), new Vector3(0.022f, 0.06f, 0.022f), bark, 0.1f, new Vector3(75, 0, 0));
                break;
            case "rope":
                // 밧줄: 둥글게 감은 타래 + 감긴 줄무늬 + 풀린 끝
                var hemp = new Color(0.62f, 0.5f, 0.32f);
                var hempDark = new Color(0.5f, 0.39f, 0.24f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.04f, 0), new Vector3(0.24f, 0.04f, 0.24f), hemp, 0.05f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.025f, 0), new Vector3(0.245f, 0.01f, 0.245f), hempDark, 0.05f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.055f, 0), new Vector3(0.245f, 0.01f, 0.245f), hempDark, 0.05f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.04f, 0), new Vector3(0.1f, 0.042f, 0.1f), new Color(0.12f, 0.1f, 0.08f), 0.05f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.145f, 0.012f, -0.078f), new Vector3(0.024f, 0.09f, 0.024f), hemp, 0.05f, new Vector3(90, 150, 0));
                break;
            case "copper_wire":
                // 구리선: 검은 실패에 감긴 구리선 + 삐져나온 끝
                var copper = new Color(0.8f, 0.43f, 0.2f);
                var spool = new Color(0.16f, 0.16f, 0.17f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.008f, 0), new Vector3(0.2f, 0.008f, 0.2f), spool, 0.3f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.051f, 0), new Vector3(0.16f, 0.035f, 0.16f), copper, 0.8f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.094f, 0), new Vector3(0.2f, 0.008f, 0.2f), spool, 0.3f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.095f, 0), new Vector3(0.05f, 0.008f, 0.05f), new Color(0.04f, 0.04f, 0.04f));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.14f, 0.05f, 0.035f), new Vector3(0.012f, 0.07f, 0.012f), copper, 0.8f, new Vector3(90, 60, 0));
                break;
            case "rubber":
                // 고무: 타이어에서 잘라낸 검은 판 + 비스듬한 홈(트레드)
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0), new Vector3(0.32f, 0.04f, 0.16f), new Color(0.09f, 0.09f, 0.09f), 0.15f);
                for (int i = 0; i < 4; i++)
                    RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.11f + i * 0.072f, 0.048f, 0), new Vector3(0.04f, 0.018f, 0.15f), new Color(0.15f, 0.15f, 0.15f), 0.2f, new Vector3(0, 20, 0));
                break;
            case "leather":
                // 가죽: 울퉁불퉁한 갈색 가죽 두 겹 + 접힌 끝자락
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.01f, 0), new Vector3(0.36f, 0.02f, 0.26f), new Color(0.45f, 0.27f, 0.15f), 0.35f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.03f, 0.012f, 0.02f), new Vector3(0.3f, 0.021f, 0.22f), new Color(0.52f, 0.33f, 0.19f), 0.35f, new Vector3(0, 28, 0));
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0.15f, 0.03f, -0.06f), new Vector3(0.1f, 0.02f, 0.12f), new Color(0.4f, 0.24f, 0.13f), 0.35f, new Vector3(0, -15, 20));
                break;
            case "metal":
                // 고철: 꺾인 철판 브래킷 + 녹슨 파이프 + 너트 (SurvivalInventory 아이콘과 같은 모양)
                var rust = new Color(0.55f, 0.28f, 0.12f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.05f, 0.0125f, 0.05f), new Vector3(0.3f, 0.025f, 0.09f), Steel, 0.45f, null, 0.5f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0.19f, 0.075f, 0.05f), new Vector3(0.22f, 0.025f, 0.09f), Steel, 0.45f, new Vector3(0, 0, 35), 0.5f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.11f, 0.014f, 0.04f), new Vector3(0.05f, 0.026f, 0.04f), rust, 0.1f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0.17f, 0.064f, 0.06f), new Vector3(0.04f, 0.026f, 0.035f), rust, 0.1f, new Vector3(0, 0, 35));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.04f, -0.09f), new Vector3(0.08f, 0.15f, 0.08f), new Color(0.5f, 0.5f, 0.53f), 0.5f, new Vector3(0, 0, 90));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.06f, 0.04f, -0.09f), new Vector3(0.083f, 0.03f, 0.083f), rust, 0.1f, new Vector3(0, 0, 90));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.21f, 0.012f, -0.12f), new Vector3(0.05f, 0.012f, 0.05f), DarkSteel, 0.5f);
                break;
            case "stone":
                // 돌: 찌그러진 회색 돌 두 개
                RuntimeVisuals.Part(PrimitiveType.Sphere, t, new Vector3(0, 0.045f, 0), new Vector3(0.13f, 0.09f, 0.11f), new Color(0.45f, 0.44f, 0.41f), 0.15f, new Vector3(0, 30, 8));
                RuntimeVisuals.Part(PrimitiveType.Sphere, t, new Vector3(0.09f, 0.03f, 0.05f), new Vector3(0.08f, 0.06f, 0.07f), new Color(0.37f, 0.36f, 0.34f), 0.15f, new Vector3(10, -20, 0));
                break;
            case "battery":
                Battery(t);
                break;
            case "fuel":
                JerryCan(t);
                break;
            case "flashlight":
                Flashlight(t);
                break;
            // 고기(닭다리)는 ItemVisuals.Meat.cs
            case "raw_meat":
                Meat(t, false);
                break;
            case "cooked_meat":
                Meat(t, true);
                break;

            // 무기 (방망이류는 +z로 길게: 손에 들 때 PlayerHands가 세운다)
            case "baseball_bat":
                Bat(t, Wood);
                break;
            case "spiked_bat":
                Bat(t, Wood);
                // 몸통에 박힌 나사들
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 140f, z = 0.14f + (i % 5) * 0.06f;
                    var dir = Quaternion.Euler(0, 0, a) * Vector3.up;
                    RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.034f, z) + dir * 0.045f, new Vector3(0.012f, 0.02f, 0.012f), Steel, 0.6f, new Vector3(0, 0, a));
                }
                break;
            case "alloy_bat":
                Bat(t, new Color(0.5f, 0.55f, 0.6f));
                // 몸통을 감싼 합금판 띠와 나사
                foreach (var z in new[] { 0.16f, 0.27f, 0.38f })
                    RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.034f, z), new Vector3(0.076f, 0.03f, 0.076f), new Color(0.72f, 0.76f, 0.82f), 0.8f, new Vector3(90, 0, 0), 0.7f);
                break;
            case "dagger":
                // 단검: 밧줄 감은 손잡이 + 가드 + 날 (+z가 칼끝)
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.02f, 0), new Vector3(0.032f, 0.06f, 0.032f), new Color(0.55f, 0.43f, 0.27f), 0.1f, new Vector3(90, 0, 0));
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0.07f), new Vector3(0.09f, 0.022f, 0.016f), DarkSteel, 0.5f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0.16f), new Vector3(0.036f, 0.008f, 0.17f), new Color(0.75f, 0.77f, 0.8f), 0.85f, null, 0.85f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0.255f), new Vector3(0.025f, 0.008f, 0.025f), new Color(0.75f, 0.77f, 0.8f), 0.85f, new Vector3(0, 45, 0), 0.85f);
                break;
            case "slingshot":
                Slingshot(t);
                break;
            case "hammer":
                // 망치: 나무 자루 + 쇠머리
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.025f, 0), new Vector3(0.035f, 0.17f, 0.035f), Wood, 0.2f, new Vector3(90, 0, 0));
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.045f, 0.18f), new Vector3(0.17f, 0.07f, 0.07f), DarkSteel, 0.55f, null, 0.55f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.1f, 0.045f, 0.18f), new Vector3(0.03f, 0.076f, 0.076f), Steel, 0.7f, null, 0.7f);
                break;

            // 장비 칸 (가방·방어구)
            case "old_backpack":
                Backpack(t, new Color(0.42f, 0.36f, 0.27f), 0.9f, false);
                break;
            case "backpack":
                Backpack(t, new Color(0.28f, 0.3f, 0.2f), 1f, false);
                break;
            case "large_backpack":
                Backpack(t, new Color(0.22f, 0.26f, 0.3f), 1.15f, true);
                break;
            case "leather_armor":
                Armor(t, new Color(0.45f, 0.28f, 0.16f), null, false);
                break;
            case "iron_armor":
                Armor(t, new Color(0.27f, 0.29f, 0.21f), new Color(0.52f, 0.54f, 0.57f), false);
                break;
            case "alloy_armor":
                Armor(t, new Color(0.2f, 0.22f, 0.25f), new Color(0.68f, 0.73f, 0.8f), true);
                break;

            // 도구·재료
            case "flint_lighter":
                // 라이터(빨간 몸통 + 쇠 덮개) + 길쭉한 부싯돌
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.045f, 0), new Vector3(0.05f, 0.09f, 0.025f), new Color(0.65f, 0.12f, 0.1f), 0.6f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.1f, 0), new Vector3(0.05f, 0.022f, 0.026f), Steel, 0.8f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.012f, 0.118f, 0), new Vector3(0.016f, 0.004f, 0.016f), DarkSteel, 0.5f, new Vector3(90, 0, 0));
                RuntimeVisuals.Part(PrimitiveType.Sphere, t, new Vector3(0.09f, 0.02f, 0.01f), new Vector3(0.1f, 0.04f, 0.05f), new Color(0.2f, 0.2f, 0.22f), 0.4f, new Vector3(0, 25, 0));
                break;
            case "iron_ingot":
                Ingot(t, new Color(0.38f, 0.38f, 0.4f), new Color(0.5f, 0.5f, 0.53f));
                break;
            case "screw":
                for (int i = 0; i < 3; i++)
                {
                    var p = new Vector3(-0.05f + i * 0.05f, 0.012f, (i - 1) * 0.03f);
                    var rot = new Vector3(0, i * 50 - 40, 90);
                    RuntimeVisuals.Part(PrimitiveType.Cylinder, t, p, new Vector3(0.016f, 0.05f, 0.016f), Steel, 0.7f, rot, 0.7f);
                    RuntimeVisuals.Part(PrimitiveType.Cylinder, t, p + Quaternion.Euler(rot) * Vector3.up * 0.05f, new Vector3(0.034f, 0.008f, 0.034f), Steel, 0.7f, rot, 0.7f);
                }
                break;
            case "circuit_board":
                // 전자기판: 초록 판 + 검은 칩 + 금색 줄
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.006f, 0), new Vector3(0.24f, 0.012f, 0.16f), new Color(0.1f, 0.38f, 0.18f), 0.5f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.05f, 0.018f, 0.02f), new Vector3(0.06f, 0.012f, 0.06f), Black, 0.4f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0.06f, 0.016f, -0.03f), new Vector3(0.04f, 0.008f, 0.03f), Black, 0.4f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.07f, 0.03f, 0.045f), new Vector3(0.025f, 0.02f, 0.025f), new Color(0.15f, 0.25f, 0.55f), 0.5f);
                for (int i = 0; i < 3; i++)
                    RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.0125f, -0.06f + i * 0.025f), new Vector3(0.2f, 0.002f, 0.006f), new Color(0.85f, 0.68f, 0.25f), 0.8f);
                break;
            case "semiconductor":
                // 반도체: 다리가 달린 검은 칩
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0), new Vector3(0.12f, 0.022f, 0.08f), Black, 0.5f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(-0.04f, 0.032f, 0), new Vector3(0.012f, 0.002f, 0.012f), new Color(0.25f, 0.25f, 0.25f));
                for (int i = 0; i < 6; i++)
                    foreach (var side in new[] { -1f, 1f })
                        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.05f + i * 0.02f, 0.01f, side * 0.048f), new Vector3(0.006f, 0.02f, 0.016f), Steel, 0.8f);
                break;
            case "stone_pouch":
                // 돌주머니: 밧줄로 묶은 천 자루 + 입구로 보이는 돌
                RuntimeVisuals.Part(PrimitiveType.Sphere, t, new Vector3(0, 0.09f, 0), new Vector3(0.2f, 0.18f, 0.18f), new Color(0.5f, 0.43f, 0.33f), 0.05f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.175f, 0), new Vector3(0.09f, 0.02f, 0.09f), new Color(0.62f, 0.5f, 0.32f), 0.05f);
                RuntimeVisuals.Part(PrimitiveType.Sphere, t, new Vector3(0.01f, 0.2f, 0), new Vector3(0.07f, 0.05f, 0.06f), new Color(0.45f, 0.44f, 0.41f), 0.15f);
                break;
            case "furnace":
                // 화로(설치): 벽돌 몸통 + 불빛이 보이는 화구 + 연통
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.12f, 0), new Vector3(0.26f, 0.24f, 0.2f), new Color(0.48f, 0.2f, 0.14f), 0.1f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.1f, -0.101f), new Vector3(0.12f, 0.08f, 0.004f), new Color(1f, 0.5f, 0.15f), 0.2f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.245f, 0), new Vector3(0.28f, 0.012f, 0.21f), DarkSteel, 0.5f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.05f, 0.33f, 0.03f), new Vector3(0.05f, 0.08f, 0.05f), DarkSteel, 0.5f);
                break;

            // 트럭 부품
            case "wheel":
                Tire(t);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.2025f, 0), new Vector3(0.24f, 0.07f, 0.24f), Steel, 0.6f, new Vector3(90, 0, 0));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.2025f, -0.07f), new Vector3(0.08f, 0.01f, 0.08f), DarkSteel, 0.5f, new Vector3(90, 0, 0));
                break;
            case "alloy_plate":
                Plate(t, new Color(0.5f, 0.55f, 0.62f), new Color(0.68f, 0.73f, 0.8f));
                break;
            case "motor":
                Motor(t);
                break;
            case "engine":
                EnginePart(t);
                break;
            case "navigation":
                // 네비게이션: 검은 본체 + 밝은 화면(지도) + 거치대
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.09f, 0), new Vector3(0.2f, 0.13f, 0.03f), new Color(0.1f, 0.1f, 0.11f), 0.6f, new Vector3(-10, 0, 0));
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.093f, -0.0175f), new Vector3(0.17f, 0.1f, 0.004f), new Color(0.3f, 0.75f, 0.65f), 0.9f, new Vector3(-10, 0, 0));
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.03f, 0.1f, -0.021f), new Vector3(0.08f, 0.008f, 0.002f), new Color(0.95f, 0.85f, 0.3f), 0.9f, new Vector3(-10, 0, 20));
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.012f, 0.02f), new Vector3(0.09f, 0.012f, 0.09f), Black, 0.4f);
                RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.03f, 0.02f), new Vector3(0.02f, 0.02f, 0.02f), Black, 0.4f);
                break;
            default:
                RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.1f, 0), Vector3.one * 0.2f, Color.gray);
                break;
        }
    }

    // ---------- 플라스틱 페트병 (물) ----------
    // 둥근 바닥, 홈 세 줄, 잘록한 허리, 둥근 어깨, 목 받침 고리, 남색 뚜껑. 투명한 껍질 안에 물이 2/3쯤 차 있고 허리 바깥에 미색 라벨이 감겨 있다
    // (HUD 갈증 계기 아이콘과 같은 색). 바닥이 y=0, 위가 +y. 손에 들 때는 허리(y≈0.13, 반지름 0.045)를 감아쥔다 (HeldItems)
    static readonly float[] BottleProfile =
    {
        // y, 반지름 — 아래에서 위로
        0.000f, 0.030f, 0.006f, 0.040f, 0.016f, 0.0445f, 0.030f, 0.045f,
        0.040f, 0.045f, 0.043f, 0.0425f, 0.048f, 0.0425f, 0.051f, 0.045f,   // 홈 1
        0.062f, 0.045f, 0.065f, 0.0425f, 0.070f, 0.0425f, 0.073f, 0.045f,   // 홈 2
        0.084f, 0.045f, 0.095f, 0.0445f, 0.120f, 0.0435f, 0.145f, 0.0445f, 0.156f, 0.045f, // 허리 (라벨 자리)
        0.166f, 0.045f, 0.169f, 0.0425f, 0.174f, 0.0425f, 0.177f, 0.045f,   // 홈 3
        0.190f, 0.045f, 0.200f, 0.0445f, 0.210f, 0.042f, 0.219f, 0.037f, 0.226f, 0.030f, 0.232f, 0.022f, 0.236f, 0.016f, // 어깨
        0.238f, 0.014f, 0.250f, 0.014f, 0.252f, 0.0185f, 0.256f, 0.0185f, 0.258f, 0.014f, 0.264f, 0.014f,  // 목과 받침 고리
    };
    const float BottleLabelBottom = 0.086f, BottleLabelTop = 0.150f, BottleLabelRadius = 0.0458f;

    static Material plasticTemplate, labelMat;
    static Texture2D labelTex;
    static readonly Dictionary<string, Material> plastics = new Dictionary<string, Material>();

    // 아이콘 카메라가 찍는 동안 true (ItemIcons가 켠다). 투명 배경 위에서는 맑은 플라스틱이 사라져 보이므로 아이콘에서는 더 진하게 칠한다
    public static bool IconMode;

    static void PetBottle(Transform t)
    {
        const int Radial = 28;
        float shellAlpha = IconMode ? 0.85f : 0.32f, waterAlpha = IconMode ? 0.95f : 0.8f;
        // 물: 껍질보다 조금 작게, 2/3 높이까지. 관의 윗뚜껑이 평평한 수면이 된다. 먼저 그리고(3000) 그 위에 껍질을 섞는다(3001)
        MeshPart(t, "Water", ProcMesh.Tube("pet_water", 2, Radial, BottleRings(0.94f, 0.004f, 0.162f)), Plastic(new Color(0.3f, 0.58f, 0.9f, waterAlpha), 0.9f, 3000));
        // 라벨: 허리 바깥을 감은 미색 띠 (껍질보다 조금 커서 껍질 밖에 붙어 보인다)
        MeshPart(t, "Label", ProcMesh.Tube("pet_label", 2, Radial, BottleLabelBottom, BottleLabelRadius, BottleLabelRadius, 0, BottleLabelTop, BottleLabelRadius, BottleLabelRadius, 0), LabelMaterial());
        // 투명한 껍질 (그림자는 안의 물이 드리운다)
        var shell = MeshPart(t, "Shell", ProcMesh.Tube("pet_shell", 2, Radial, BottleRings(1f, 0f, 0.264f)), Plastic(new Color(0.72f, 0.84f, 0.95f, shellAlpha), 0.95f, 3001));
        shell.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // 뚜껑: 아래 봉인 띠에 홈이 한 줄 파여 있고 위가 살짝 둥글다
        MeshPart(t, "Cap", ProcMesh.Tube("pet_cap", 2, Radial,
            0.258f, 0.0165f, 0.0165f, 0, 0.2605f, 0.0165f, 0.0165f, 0, 0.261f, 0.0152f, 0.0152f, 0, 0.2625f, 0.0152f, 0.0152f, 0,
            0.263f, 0.0165f, 0.0165f, 0, 0.292f, 0.0165f, 0.0165f, 0, 0.296f, 0.015f, 0.015f, 0, 0.299f, 0.010f, 0.010f, 0),
            RuntimeVisuals.Mat(new Color(0.16f, 0.32f, 0.62f), 0.5f));
    }

    // 병 프로필의 높이 y에서의 반지름 (점 사이는 직선으로 잇는다)
    static float BottleRadiusAt(float y)
    {
        for (int i = 2; i < BottleProfile.Length; i += 2)
            if (y <= BottleProfile[i])
                return Mathf.Lerp(BottleProfile[i - 1], BottleProfile[i + 1], Mathf.InverseLerp(BottleProfile[i - 2], BottleProfile[i], y));
        return BottleProfile[BottleProfile.Length - 1];
    }

    // yMin~yMax 사이의 프로필을 ProcMesh.Tube 고리(y, rx, rz, 치우침)로 만든다. 반지름은 rScale배, 양 끝은 보간한 고리로 닫는다
    static float[] BottleRings(float rScale, float yMin, float yMax)
    {
        var rings = new List<float>();
        System.Action<float, float> add = (y, r) => { rings.Add(y); rings.Add(r * rScale); rings.Add(r * rScale); rings.Add(0); };
        add(yMin, BottleRadiusAt(yMin));
        for (int i = 0; i < BottleProfile.Length; i += 2)
            if (BottleProfile[i] > yMin && BottleProfile[i] < yMax) add(BottleProfile[i], BottleProfile[i + 1]);
        add(yMax, BottleRadiusAt(yMax));
        return rings.ToArray();
    }

    static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go;
    }

    // 반투명 플라스틱·물 재질 (URP Lit 투명, 알파 미리 곱함, 반사광 유지). Resources/Items/M_Plastic을 본떠 색만 바꾼다
    // (에셋이 있어야 빌드에 투명 셰이더 변형이 들어간다. 없으면 같은 설정으로 새로 만든다)
    static Material Plastic(Color color, float smoothness, int queue)
    {
        string key = color + "/" + smoothness + "/" + queue;
        if (plastics.TryGetValue(key, out var m) && m != null) return m;
        if (plasticTemplate == null) plasticTemplate = Resources.Load<Material>("Items/M_Plastic");
        if (plasticTemplate != null) m = new Material(plasticTemplate);
        else
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_BlendModePreserveSpecular", 1);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        m.name = "Plastic " + key;
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.renderQueue = queue;
        plastics[key] = m;
        return m;
    }

    // 라벨 재질: 코드로 그린 라벨 그림을 띠 둘레에 한 바퀴 맞춰 입힌다 (ProcMesh.Tube의 UV: u = 둘레 길이 m, v = 높이 m)
    static Material LabelMaterial()
    {
        if (labelMat != null) return labelMat;
        labelMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PetLabel" };
        labelMat.SetTexture("_BaseMap", LabelTexture());
        labelMat.SetColor("_BaseColor", Color.white);
        labelMat.SetFloat("_Smoothness", 0.45f);
        float around = 2 * Mathf.PI * BottleLabelRadius, height = BottleLabelTop - BottleLabelBottom;
        labelMat.SetTextureScale("_BaseMap", new Vector2(1f / around, 1f / height));
        labelMat.SetTextureOffset("_BaseMap", new Vector2(0, -BottleLabelBottom / height));
        return labelMat;
    }

    // 라벨 그림 (256×64, 가로가 병 둘레): 미색 종이, 아래쪽 파란 물결 띠와 남색 선, 물방울 로고와 글씨처럼 보이는 획, 바코드.
    // 반 바퀴마다 같은 무늬라 어느 쪽에서 봐도 로고가 보인다
    static Texture2D LabelTexture()
    {
        if (labelTex != null) return labelTex;
        const int W = 256, H = 64, Half = W / 2;
        var paper = new Color(0.93f, 0.92f, 0.88f);
        var blue = new Color(0.3f, 0.58f, 0.9f);
        var navy = new Color(0.16f, 0.32f, 0.62f);
        var ink = new Color(0.25f, 0.27f, 0.3f);
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var c = paper;
                float wave = 20f + Mathf.Sin((x + 0.5f) / W * Mathf.PI * 6f) * 3f; // 물결 띠의 윗선 (px)
                int lx = x % Half;
                if (y < wave)
                {
                    c = blue;
                    if (y >= 8 && y < 12 && lx >= 56 && lx < 96 && (lx - 56) % 8 < 5) c = Color.Lerp(blue, Color.white, 0.85f); // 띠 안의 흰 잔글씨 (용량 표시처럼)
                }
                else if (y < wave + 3f) c = navy;
                else
                {
                    float dx = lx - 40f, dy = y - 38f;
                    if (dx * dx + dy * dy < 64f || (dy >= 0 && dy < 16f && Mathf.Abs(dx) < 8f * (1f - dy / 16f))) c = navy; // 물방울 로고
                    else if (y >= 40 && y < 47 && lx >= 56 && lx < 110 && (lx - 56) % 11 < 8) c = navy;                  // 상품명처럼 굵은 획
                    else if (y >= 31 && y < 34 && lx >= 56 && lx < 104 && (lx - 56) % 7 < 5) c = ink;                    // 잔글씨 획
                    else if (x >= 150 && x < 178 && y >= 30 && y < 44 && x * 7 % 11 < 4) c = ink;                         // 바코드 (한쪽에만)
                }
                px[y * W + x] = c;
            }
        labelTex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "PetLabel", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
        labelTex.SetPixels32(px);
        labelTex.Apply(true, true);
        return labelTex;
    }

    // 방망이: 손잡이 끝(knob)부터 +z로 손잡이 → 굵은 몸통. 바닥에 눕혀 놓을 수 있게 반지름만큼 띄운다
    static void Bat(Transform t, Color body)
    {
        const float y = 0.034f;
        var grip = new Color(0.12f, 0.11f, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, y, -0.33f), new Vector3(0.056f, 0.012f, 0.056f), body, 0.3f, new Vector3(90, 0, 0));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, y, -0.2f), new Vector3(0.038f, 0.12f, 0.038f), grip, 0.2f, new Vector3(90, 0, 0));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, y, -0.02f), new Vector3(0.042f, 0.08f, 0.042f), body, 0.3f, new Vector3(90, 0, 0));
        // 굵은 몸통은 손잡이 쪽 토막 끝(0.06)에 겹쳐 이어 붙인다 (예전에는 1cm 틈이 벌어져 끊겨 보였다)
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, y, 0.2625f), new Vector3(0.068f, 0.2075f, 0.068f), body, 0.3f, new Vector3(90, 0, 0));
        RuntimeVisuals.Part(PrimitiveType.Sphere, t, new Vector3(0, y, 0.47f), new Vector3(0.068f, 0.068f, 0.03f), body, 0.3f);
    }

    // 새총: Y자 나뭇가지 + 고무줄 + 가죽 주머니 (세워 둔다)
    static void Slingshot(Transform t)
    {
        var band = new Color(0.55f, 0.15f, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.07f, 0), new Vector3(0.03f, 0.07f, 0.03f), Wood, 0.2f);
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(-0.035f, 0.18f, 0), new Vector3(0.026f, 0.06f, 0.026f), Wood, 0.2f, new Vector3(0, 0, 30));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.035f, 0.18f, 0), new Vector3(0.026f, 0.06f, 0.026f), Wood, 0.2f, new Vector3(0, 0, -30));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(-0.065f, 0.22f, 0.04f), new Vector3(0.01f, 0.045f, 0.01f), band, 0.3f, new Vector3(-60, 0, 0));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.065f, 0.22f, 0.04f), new Vector3(0.01f, 0.045f, 0.01f), band, 0.3f, new Vector3(-60, 0, 0));
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.2f, 0.08f), new Vector3(0.05f, 0.03f, 0.012f), new Color(0.4f, 0.25f, 0.14f), 0.3f);
    }

    // 가방: 몸통 + 앞주머니 + 덮개. big이면 옆주머니와 쇠 프레임을 더한다
    static void Backpack(Transform t, Color cloth, float size, bool big)
    {
        var dark = cloth * 0.8f;
        dark.a = 1;
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.2f, 0) * size, new Vector3(0.35f, 0.4f, 0.2f) * size, cloth, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.12f, -0.11f) * size, new Vector3(0.25f, 0.15f, 0.06f) * size, dark, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.37f, -0.02f) * size, new Vector3(0.36f, 0.08f, 0.22f) * size, dark, 0.1f);
        // 위쪽 손잡이 고리 (손에 들 때 손가락을 건다)
        var strap = new Color(0.13f, 0.13f, 0.12f);
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.445f, -0.01f) * size, new Vector3(0.014f, 0.05f, 0.014f) * size, strap, 0.2f, new Vector3(0, 0, 90));
        foreach (var x in new[] { -0.05f, 0.05f })
            RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(x, 0.425f, -0.01f) * size, new Vector3(0.012f, 0.02f, 0.012f) * size, strap, 0.2f);
        if (!big) return;
        foreach (var x in new[] { -1f, 1f })
            RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(x * 0.2f, 0.16f, 0) * size, new Vector3(0.06f, 0.2f, 0.14f) * size, dark, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.44f, 0.08f) * size, new Vector3(0.3f, 0.02f, 0.02f) * size, Steel, 0.6f);
    }

    // 철주괴: 위가 좁은 쇳덩이 두 단
    static void Ingot(Transform t, Color body, Color top)
    {
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0), new Vector3(0.24f, 0.04f, 0.1f), body, 0.55f, null, 0.5f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.05f, 0), new Vector3(0.2f, 0.022f, 0.075f), top, 0.65f, null, 0.55f);
    }

    // 판: 겹쳐 놓은 두 장 + 모서리 구멍 + 표면 줄무늬
    static void Plate(Transform t, Color under, Color plate)
    {
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.011f, 0), new Vector3(0.4f, 0.022f, 0.28f), under, 0.55f, null, 0.5f);
        var top = RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0.015f, 0.033f, -0.01f), new Vector3(0.38f, 0.022f, 0.26f), plate, 0.75f, new Vector3(0, 9, 0), 0.6f).transform;
        // 아래 장식은 위 판 기준으로 붙인다 (크기 비율을 되돌려 원래 치수로 만든다)
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0 ? -1 : 1) * 0.16f, z = (i < 2 ? -1 : 1) * 0.1f;
            PlateDetail(top, PrimitiveType.Cylinder, new Vector3(x, 0.012f, z), new Vector3(0.024f, 0.002f, 0.024f), new Color(0.1f, 0.1f, 0.11f), 0.3f);
        }
        PlateDetail(top, PrimitiveType.Cube, new Vector3(0.0f, 0.0115f, -0.075f), new Vector3(0.3f, 0.002f, 0.008f), Color.Lerp(plate, Color.white, 0.4f), 0.8f);
        PlateDetail(top, PrimitiveType.Cube, new Vector3(0.0f, 0.0115f, 0.05f), new Vector3(0.3f, 0.002f, 0.008f), Color.Lerp(plate, Color.white, 0.4f), 0.8f);
    }

    // 판 위에 붙는 얇은 장식. 부모(판)의 크기 비율을 되돌려서 원래 치수(m)로 붙인다
    static void PlateDetail(Transform plate, PrimitiveType type, Vector3 pos, Vector3 size, Color color, float smoothness)
    {
        var s = plate.localScale;
        var p = RuntimeVisuals.Part(type, plate, Vector3.zero, Vector3.one, color, smoothness).transform;
        p.localPosition = new Vector3(pos.x / s.x, pos.y / s.y, pos.z / s.z);
        p.localScale = new Vector3(size.x / s.x, size.y / s.y, size.z / s.z);
    }

    // 방어구: 조끼에 판을 덧댄 모양. 살짝 뒤로 기대 세운다 (앞면이 -z 쪽, 아이콘 카메라를 본다).
    // plate가 null이면 가죽 덧댐, shoulders면 어깨 보호대를 더한다
    static void Armor(Transform t, Color fabric, Color? plate, bool shoulders)
    {
        var vest = new GameObject("Vest").transform;
        vest.SetParent(t, false);
        vest.localPosition = new Vector3(0, 0.015f, 0);
        vest.localEulerAngles = new Vector3(10, 0, 0);

        var fabricDark = fabric * 0.75f;
        fabricDark.a = 1;
        var strap = new Color(0.13f, 0.13f, 0.12f);

        // 몸통(조끼)과 어깨끈, 목·팔 구멍
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0, 0.21f, 0), new Vector3(0.36f, 0.4f, 0.14f), fabric, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(-0.11f, 0.43f, 0), new Vector3(0.075f, 0.05f, 0.15f), fabric, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0.11f, 0.43f, 0), new Vector3(0.075f, 0.05f, 0.15f), fabric, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0, 0.4105f, 0), new Vector3(0.13f, 0.002f, 0.1f), new Color(0.04f, 0.04f, 0.04f));
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(-0.181f, 0.33f, 0), new Vector3(0.002f, 0.12f, 0.1f), new Color(0.04f, 0.04f, 0.04f));
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0.181f, 0.33f, 0), new Vector3(0.002f, 0.12f, 0.1f), new Color(0.04f, 0.04f, 0.04f));

        if (plate.HasValue)
        {
            // 앞판 + 리벳
            RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0, 0.26f, -0.078f), new Vector3(0.25f, 0.21f, 0.02f), plate.Value, 0.6f);
            RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0, 0.26f, -0.0885f), new Vector3(0.18f, 0.004f, 0.002f), Color.Lerp(plate.Value, Color.white, 0.3f), 0.8f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 0.105f, y = i < 2 ? 0.17f : 0.35f;
                RuntimeVisuals.Part(PrimitiveType.Sphere, vest, new Vector3(x, y, -0.088f), new Vector3(0.016f, 0.016f, 0.01f), DarkSteel, 0.5f);
            }
        }
        else
        {
            // 가죽을 겹겹이 덧대 꿰맨 자국
            for (int i = 0; i < 3; i++)
                RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0, 0.18f + i * 0.08f, -0.074f), new Vector3(0.3f, 0.06f, 0.012f), fabricDark, 0.3f);
        }

        if (shoulders)
            foreach (var x in new[] { -1f, 1f })
                RuntimeVisuals.Part(PrimitiveType.Sphere, vest, new Vector3(x * 0.15f, 0.43f, 0), new Vector3(0.13f, 0.07f, 0.17f), plate ?? fabric, 0.6f);

        // 가로 고정끈과 아래 주머니 두 개
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0, 0.135f, -0.074f), new Vector3(0.37f, 0.03f, 0.02f), strap, 0.15f);
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(-0.09f, 0.07f, -0.085f), new Vector3(0.1f, 0.08f, 0.04f), fabricDark, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0.09f, 0.07f, -0.085f), new Vector3(0.1f, 0.08f, 0.04f), fabricDark, 0.1f);
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(-0.09f, 0.105f, -0.1f), new Vector3(0.104f, 0.018f, 0.012f), strap, 0.15f);
        RuntimeVisuals.Part(PrimitiveType.Cube, vest, new Vector3(0.09f, 0.105f, -0.1f), new Vector3(0.104f, 0.018f, 0.012f), strap, 0.15f);
    }

    // 타이어: 원통 토막을 둥글게 이어 붙인 고무 고리 (세워 둔다, 축은 z). 토막마다 색을 번갈아 트레드처럼 보이게 한다
    static void Tire(Transform t)
    {
        const int segments = 18;
        const float ring = 0.16f, tube = 0.085f, width = 0.13f;
        var rubber = new Color(0.08f, 0.08f, 0.08f);
        var tread = new Color(0.13f, 0.13f, 0.13f);
        float center = ring + tube / 2;
        float segLen = 2 * Mathf.PI * ring / segments * 1.3f; // 바깥쪽에 틈이 보이지 않게 겹친다
        for (int i = 0; i < segments; i++)
        {
            float a = i * 360f / segments;
            float rad = a * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(rad) * ring, center + Mathf.Cos(rad) * ring, 0);
            // 원통의 축(y)을 고리의 접선 방향으로 눕힌다
            RuntimeVisuals.Part(PrimitiveType.Cylinder, t, pos, new Vector3(tube, segLen / 2, width), i % 2 == 0 ? rubber : tread, 0.15f, new Vector3(0, 0, -a - 90));
        }
        // 안쪽 테두리(비드)와 옆면 글자 띠
        for (int i = 0; i < segments; i++)
        {
            float a = (i + 0.5f) * 360f / segments;
            float rad = a * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(rad) * (ring - tube * 0.45f), center + Mathf.Cos(rad) * (ring - tube * 0.45f), 0);
            RuntimeVisuals.Part(PrimitiveType.Cube, t, pos, new Vector3(segLen * 0.85f, 0.012f, width * 0.8f), new Color(0.05f, 0.05f, 0.05f), 0.1f, new Vector3(0, 0, -a));
            if (i % 3 == 0)
            {
                var side = new Vector3(Mathf.Sin(rad) * ring, center + Mathf.Cos(rad) * ring, -width / 2 - 0.002f);
                RuntimeVisuals.Part(PrimitiveType.Cube, t, side, new Vector3(segLen * 0.6f, 0.012f, 0.004f), new Color(0.32f, 0.32f, 0.31f), 0.1f, new Vector3(0, 0, -a));
            }
        }
    }

    // 모터: 원통 몸통 + 냉각 홈 + 양 끝 덮개 + 축 + 단자함 + 받침
    static void Motor(Transform t)
    {
        var body = new Color(0.22f, 0.32f, 0.42f);
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.11f, 0), new Vector3(0.18f, 0.12f, 0.18f), body, 0.5f, new Vector3(0, 0, 90));
        for (int i = 0; i < 5; i++)
            RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(-0.08f + i * 0.04f, 0.11f, 0), new Vector3(0.186f, 0.006f, 0.186f), body * 0.8f, 0.5f, new Vector3(0, 0, 90));
        foreach (var x in new[] { -0.125f, 0.125f })
            RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(x, 0.11f, 0), new Vector3(0.16f, 0.008f, 0.16f), DarkSteel, 0.6f, new Vector3(0, 0, 90));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.17f, 0.11f, 0), new Vector3(0.03f, 0.04f, 0.03f), Steel, 0.8f, new Vector3(0, 0, 90));
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.21f, 0), new Vector3(0.07f, 0.04f, 0.06f), Black, 0.4f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.012f, 0), new Vector3(0.2f, 0.024f, 0.16f), DarkSteel, 0.4f);
    }

    // 엔진: 엔진 블록 + 붉은 헤드 커버 + 배기관 + 풀리 + 고무 호스
    static void EnginePart(Transform t)
    {
        var block = new Color(0.32f, 0.33f, 0.34f);
        var head = new Color(0.44f, 0.45f, 0.47f);
        var rust = new Color(0.47f, 0.27f, 0.15f);

        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.025f, 0), new Vector3(0.26f, 0.05f, 0.17f), new Color(0.22f, 0.22f, 0.23f), 0.4f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.14f, 0), new Vector3(0.3f, 0.18f, 0.2f), block, 0.45f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.25f, 0), new Vector3(0.29f, 0.04f, 0.17f), head, 0.5f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0, 0.285f, 0.015f), new Vector3(0.27f, 0.03f, 0.11f), new Color(0.55f, 0.16f, 0.11f), 0.55f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(-0.06f, 0.302f, 0.015f), new Vector3(0.02f, 0.008f, 0.1f), new Color(0.65f, 0.2f, 0.14f), 0.55f);
        RuntimeVisuals.Part(PrimitiveType.Cube, t, new Vector3(0.06f, 0.302f, 0.015f), new Vector3(0.02f, 0.008f, 0.1f), new Color(0.65f, 0.2f, 0.14f), 0.55f);

        // 점화 플러그 캡 (헤드 앞쪽 위)
        for (int i = -1; i <= 1; i++)
            RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(i * 0.08f, 0.283f, -0.065f), new Vector3(0.024f, 0.013f, 0.024f), Black, 0.3f);

        // 배기관: 앞면에서 나온 짧은 관 셋 + 가로 모음관 + 아래로 꺾인 출구
        for (int i = -1; i <= 1; i++)
            RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(i * 0.08f, 0.16f, -0.12f), new Vector3(0.036f, 0.022f, 0.036f), rust, 0.25f, new Vector3(90, 0, 0));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.16f, -0.15f), new Vector3(0.042f, 0.16f, 0.042f), rust, 0.25f, new Vector3(0, 0, 90));
        RuntimeVisuals.Part(PrimitiveType.Sphere, t, new Vector3(0.16f, 0.16f, -0.15f), new Vector3(0.046f, 0.046f, 0.046f), rust, 0.25f);
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.16f, 0.09f, -0.15f), new Vector3(0.042f, 0.07f, 0.042f), rust, 0.25f);

        // 옆면 풀리(벨트 바퀴)
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.163f, 0.13f, 0.02f), new Vector3(0.14f, 0.013f, 0.14f), new Color(0.12f, 0.12f, 0.12f), 0.4f, new Vector3(0, 0, 90));
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(0.178f, 0.13f, 0.02f), new Vector3(0.05f, 0.01f, 0.05f), new Color(0.6f, 0.6f, 0.62f), 0.7f, new Vector3(0, 0, 90));

        // 반대쪽 고무 호스
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(-0.1725f, 0.215f, 0.04f), new Vector3(0.03f, 0.05f, 0.03f), Black, 0.3f, new Vector3(0, 0, -38));
        RuntimeVisuals.Part(PrimitiveType.Sphere, t, new Vector3(-0.2f, 0.18f, 0.04f), new Vector3(0.03f, 0.03f, 0.03f), Black, 0.3f);
        RuntimeVisuals.Part(PrimitiveType.Cylinder, t, new Vector3(-0.2f, 0.12f, 0.04f), new Vector3(0.03f, 0.06f, 0.03f), Black, 0.3f);
    }
}
