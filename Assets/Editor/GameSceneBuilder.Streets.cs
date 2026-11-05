using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// GameSceneBuilder의 거리 부분: 진짜 차처럼 다시 지은 버려진 차(세단·해치백·밴, 탄 차, 뒤집힌 차)와 땅에 붙었는지 검사,
// 길가에 더 흩어진 잔해, 건물 벽에 기대 앉아 죽은 사람들, 잡상인 건물의 황금 문과 반딧불 빛
public static partial class GameSceneBuilder
{
    // ---------- 버려진 차 ----------

    // pos: 바닥 자리 (바퀴 밑이 바닥에 닿는다). burnt: 불탄 차 (그을린 몸체, 유리 없음). 뒤집힌 차는 지붕이 바닥에 닿는다
    static void WreckedCarV2(Transform parent, Vector3 pos, float yaw, bool flipped, bool burnt, System.Random r)
    {
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
        bool C(double p) => r.NextDouble() < p;
        int variant = r.Next(3); // 0 세단, 1 해치백, 2 밴
        var g = Group("WreckedCar", parent, pos, yaw);
        var body = Group("Body", g, Vector3.zero);
        var paints = new[] { M["paintRed"], M["paintBlue"], M["metal"], M["paintBlue"], M["dirty"] };
        var paint = burnt ? M["scorch"] : paints[r.Next(paints.Length)];
        var dark = M["tire"];
        var chrome = burnt ? M["soot"] : M["metal"];
        bool glass = !burnt;

        float length = variant == 2 ? 4.7f : variant == 1 ? 3.95f : 4.45f;
        float width = variant == 2 ? 1.9f : 1.78f;
        float wheelR = variant == 2 ? 0.36f : 0.33f;
        float wheelZ = variant == 2 ? 1.5f : variant == 1 ? 1.25f : 1.38f;
        float lowerY = wheelR + 0.06f + 0.22f; // 아래 몸체 가운데 높이 (바닥은 바퀴 축보다 조금 위)
        float roofTop;

        // 아래 몸체와 문턱
        Box("Lower", body, new Vector3(0, lowerY, 0), new Vector3(width, 0.44f, length - 0.3f), paint, 1.5f);
        foreach (var s in new[] { -1f, 1f })
            Box("Sill", body, new Vector3(s * (width / 2 + 0.02f), lowerY - 0.17f, 0), new Vector3(0.05f, 0.1f, length * 0.55f), dark, 1f, false);
        if (variant == 2)
        {
            // 밴: 앞이 짧은 운전석과 긴 짐칸이 한 덩어리, 지붕이 높다
            float cabZ = length / 2 - 0.9f;
            Box("Cab", body, new Vector3(0, lowerY + 0.62f, cabZ - 0.1f), new Vector3(width - 0.04f, 0.8f, 1.6f), paint, 1.5f);
            Box("Cargo", body, new Vector3(0, lowerY + 0.95f, cabZ - 2.5f), new Vector3(width, 1.46f, 3.2f), paint, 1.5f);
            roofTop = lowerY + 0.95f + 0.73f + 0.07f; // 지붕 선반까지 (뒤집히면 선반이 바닥에 닿는다)
            if (glass)
            {
                Box("Windshield", body, new Vector3(0, lowerY + 0.78f, cabZ + 0.68f), new Vector3(width - 0.3f, 0.7f, 0.02f), M["glass"], 1f, false, new Vector3(-18, 0, 0));
                foreach (var s in new[] { -1f, 1f })
                    Box("SideWindow", body, new Vector3(s * (width / 2 - 0.01f), lowerY + 0.8f, cabZ - 0.15f), new Vector3(0.02f, 0.5f, 1.0f), M["glass"], 1f, false);
            }
            foreach (var s in new[] { -1f, 1f })
            {
                Box("DoorSeam", body, new Vector3(s * (width / 2 + 0.003f), lowerY + 0.3f, cabZ - 0.95f), new Vector3(0.004f, 1.2f, 0.02f), dark, 1f, false);
                Box("Handle", body, new Vector3(s * (width / 2 + 0.012f), lowerY + 0.42f, cabZ - 0.5f), new Vector3(0.02f, 0.03f, 0.14f), chrome, 1f, false);
                Box("Mirror", body, new Vector3(s * (width / 2 + 0.11f), lowerY + 0.85f, cabZ + 0.5f), new Vector3(0.16f, 0.12f, 0.08f), paint, 1f, false);
            }
            // 뒷문 이음선과 지붕 선반
            Box("RearSeam", body, new Vector3(0, lowerY + 0.95f, -length / 2 + 0.2f - 0.006f), new Vector3(0.006f, 1.3f, 0.004f), dark, 1f, false);
            Box("Rack", body, new Vector3(0, roofTop + 0.08f, cabZ - 2.4f), new Vector3(width - 0.3f, 0.04f, 2.6f), chrome, 1f, false);
            foreach (var z in new[] { -1.1f, 0f, 1.1f })
                Box("RackBar", body, new Vector3(0, roofTop + 0.04f, cabZ - 2.4f + z), new Vector3(width - 0.3f, 0.06f, 0.04f), chrome, 1f, false);
            Box("Nose", body, new Vector3(0, lowerY + 0.1f, length / 2 - 0.17f), new Vector3(width - 0.06f, 0.5f, 0.26f), paint, 1f);
        }
        else
        {
            bool hatch = variant == 1;
            float cabFront = hatch ? 0.75f : 0.85f, cabBack = hatch ? -1.45f : -1.1f;
            float hoodZ = (cabFront + length / 2) / 2, hoodL = length / 2 - cabFront;
            // 보닛 (살짝 찌그러져 기운 것도 있다), 코, 그릴, 전조등, 범퍼
            Box("Hood", body, new Vector3(0, lowerY + 0.3f, hoodZ), new Vector3(width - 0.08f, 0.12f, hoodL), paint, 1.5f, true, new Vector3(C(0.4) ? F(4, 9) : 3, 0, C(0.3) ? F(-4, 4) : 0));
            Box("Nose", body, new Vector3(0, lowerY + 0.1f, length / 2 - 0.13f), new Vector3(width - 0.06f, 0.36f, 0.22f), paint, 1f);
            Box("Grille", body, new Vector3(0, lowerY + 0.08f, length / 2 - 0.012f), new Vector3(0.95f, 0.18f, 0.02f), dark, 1f, false);
            foreach (var s in new[] { -1f, 1f })
                Box("Headlight", body, new Vector3(s * (width / 2 - 0.32f), lowerY + 0.16f, length / 2 - 0.01f), new Vector3(0.3f, 0.13f, 0.02f), glass ? M["glass"] : dark, 1f, false);
            Box("Bumper", body, new Vector3(0, lowerY - 0.12f, length / 2 - 0.05f), new Vector3(width + 0.02f, 0.16f, 0.14f), dark, 1f);
            Box("Plate", body, new Vector3(0, lowerY - 0.12f, length / 2 + 0.025f), new Vector3(0.34f, 0.1f, 0.01f), M["line"], 1f, false);
            // 트렁크·뒷면
            float trunkZ = (cabBack - length / 2) / 2, trunkL = cabBack + length / 2;
            if (!hatch) Box("Trunk", body, new Vector3(0, lowerY + 0.34f, trunkZ), new Vector3(width - 0.08f, 0.14f, trunkL), paint, 1.5f);
            Box("Tail", body, new Vector3(0, lowerY + 0.1f, -length / 2 + 0.11f), new Vector3(width - 0.06f, 0.36f, 0.18f), paint, 1f);
            foreach (var s in new[] { -1f, 1f })
                Box("Taillight", body, new Vector3(s * (width / 2 - 0.3f), lowerY + 0.2f, -length / 2 + 0.012f), new Vector3(0.34f, 0.12f, 0.02f), M["paintRed"], 1f, false);
            Box("RearBumper", body, new Vector3(0, lowerY - 0.12f, -length / 2 + 0.05f), new Vector3(width + 0.02f, 0.16f, 0.14f), dark, 1f);
            // 객실: 지붕과 기둥, 유리
            float roofY = lowerY + 0.22f + 0.6f;
            float roofLen = cabFront - cabBack - (hatch ? 0.35f : 0.5f);
            float roofZ = (cabFront + cabBack) / 2 - (hatch ? -0.05f : 0.05f);
            Box("Roof", body, new Vector3(0, roofY, roofZ), new Vector3(width - 0.2f, 0.06f, roofLen), paint, 1.5f);
            roofTop = roofY + 0.03f;
            foreach (var s in new[] { -1f, 1f })
            {
                float px = s * (width / 2 - 0.08f);
                Box("PillarA", body, new Vector3(px, roofY - 0.3f, cabFront - 0.05f), new Vector3(0.06f, 0.66f, 0.07f), paint, 1f, false, new Vector3(-34, 0, 0));
                Box("PillarB", body, new Vector3(px, roofY - 0.3f, (cabFront + cabBack) / 2 - 0.1f), new Vector3(0.06f, 0.6f, 0.08f), paint, 1f, false);
                Box("PillarC", body, new Vector3(px, roofY - 0.3f, cabBack + 0.08f), new Vector3(0.06f, 0.66f, 0.07f), paint, 1f, false, new Vector3(hatch ? 14 : 30, 0, 0));
                if (glass)
                {
                    bool broken = C(0.25);
                    if (!broken) Box("SideWindowF", body, new Vector3(s * (width / 2 - 0.085f), roofY - 0.3f, (cabFront + (cabFront + cabBack) / 2) / 2 - 0.05f), new Vector3(0.02f, 0.46f, (cabFront - cabBack) / 2 - 0.3f), M["glass"], 1f, false);
                    Box("SideWindowR", body, new Vector3(s * (width / 2 - 0.085f), roofY - 0.3f, ((cabFront + cabBack) / 2 + cabBack) / 2 + 0.05f), new Vector3(0.02f, 0.46f, (cabFront - cabBack) / 2 - 0.3f), M["glass"], 1f, false);
                }
                // 문 이음선, 손잡이, 거울
                Box("DoorSeam", body, new Vector3(s * (width / 2 + 0.003f), lowerY + 0.02f, (cabFront + cabBack) / 2 - 0.1f), new Vector3(0.004f, 0.74f, 0.02f), dark, 1f, false);
                Box("DoorSeam", body, new Vector3(s * (width / 2 + 0.003f), lowerY + 0.02f, cabBack - 0.05f), new Vector3(0.004f, 0.74f, 0.02f), dark, 1f, false);
                Box("Handle", body, new Vector3(s * (width / 2 + 0.012f), lowerY + 0.18f, (cabFront + cabBack) / 2 + 0.25f), new Vector3(0.02f, 0.03f, 0.14f), chrome, 1f, false);
                Box("Handle", body, new Vector3(s * (width / 2 + 0.012f), lowerY + 0.18f, cabBack + 0.35f), new Vector3(0.02f, 0.03f, 0.14f), chrome, 1f, false);
                Box("Mirror", body, new Vector3(s * (width / 2 + 0.1f), roofY - 0.42f, cabFront - 0.1f), new Vector3(0.14f, 0.1f, 0.08f), paint, 1f, false);
            }
            if (glass)
            {
                Box("Windshield", body, new Vector3(0, roofY - 0.3f, cabFront - 0.06f), new Vector3(width - 0.3f, 0.66f, 0.02f), M["glass"], 1f, false, new Vector3(-34, 0, 0));
                Box("RearGlass", body, new Vector3(0, roofY - 0.3f, cabBack + 0.08f), new Vector3(width - 0.32f, 0.6f, 0.02f), M["glass"], 1f, false, new Vector3(hatch ? 14 : 30, 0, 0));
            }
            // 열린 문 하나 (가끔): 앞문이 경첩에서 60도 벌어져 있다
            if (!burnt && C(0.3))
            {
                float s = C(0.5) ? -1f : 1f;
                var hinge = Group("OpenDoor", body, new Vector3(s * (width / 2 - 0.02f), lowerY + 0.12f, (cabFront + cabBack) / 2 + 0.6f), s * -60f);
                Box("Leaf", hinge, new Vector3(0, 0, -0.5f), new Vector3(0.05f, 0.8f, 1.0f), paint, 1f);
                Box("LeafWindow", hinge, new Vector3(0, 0.62f, -0.5f), new Vector3(0.03f, 0.42f, 0.9f), glass ? M["glass"] : dark, 1f, false);
            }
        }

        // 바퀴: 타이어·림·허브. 하나가 빠져 받침목으로 괸 차, 바람 빠진 타이어
        int missing = C(0.2) ? r.Next(4) : -1;
        int flat = missing < 0 && C(0.35) ? r.Next(4) : -1;
        int wi = 0;
        foreach (var z in new[] { wheelZ, -wheelZ })
            foreach (var s in new[] { -1f, 1f })
            {
                float wx = s * (width / 2 - 0.05f);
                if (wi == missing)
                {
                    Prim(PrimitiveType.Cylinder, "Drum", body, new Vector3(wx, wheelR, z), new Vector3(0.3f, 0.07f, 0.3f), chrome, new Vector3(0, 0, 90));
                    Box("Crib", body, new Vector3(wx, 0.08f, z + 0.1f), new Vector3(0.24f, 0.16f, 0.5f), M["wood"], 1f, true);
                    Box("Crib", body, new Vector3(wx, 0.2f, z + 0.1f), new Vector3(0.5f, 0.08f, 0.22f), M["wood"], 1f, false);
                }
                else if (wi == flat)
                {
                    // 바람 빠진 타이어: 납작하고 바닥에 눌린다
                    Prim(PrimitiveType.Cylinder, "Tire", body, new Vector3(wx, wheelR - 0.05f, z), new Vector3((wheelR - 0.05f) * 2f, 0.11f, wheelR * 2.05f), M["tire"], new Vector3(0, 0, 90)); // z축 90도 회전 뒤 로컬 x가 세로(높이)가 된다: 납작한 쪽이 세로, 늘어난 쪽이 앞뒤 (예전엔 뒤바뀌어 타이어가 바닥에 6cm 묻혀 CheckGrounded가 차를 띄웠다)
                    Prim(PrimitiveType.Cylinder, "Rim", body, new Vector3(wx + s * 0.005f, wheelR - 0.05f, z), new Vector3(wheelR * 1.15f, 0.113f, wheelR * 1.15f), chrome, new Vector3(0, 0, 90));
                }
                else
                {
                    Prim(PrimitiveType.Cylinder, "Tire", body, new Vector3(wx, wheelR, z), new Vector3(wheelR * 2f, 0.11f, wheelR * 2f), M["tire"], new Vector3(0, 0, 90));
                    Prim(PrimitiveType.Cylinder, "Rim", body, new Vector3(wx + s * 0.005f, wheelR, z), new Vector3(wheelR * 1.15f, 0.113f, wheelR * 1.15f), chrome, new Vector3(0, 0, 90));
                    Prim(PrimitiveType.Cylinder, "Hub", body, new Vector3(wx + s * 0.012f, wheelR, z), new Vector3(0.12f, 0.115f, 0.12f), dark, new Vector3(0, 0, 90));
                }
                wi++;
            }

        // 녹 자국 (탄 차는 온통 그을렸으니 없음)
        if (!burnt)
            for (int i = 0, n = 1 + r.Next(3); i < n; i++)
            {
                float s = C(0.5) ? -1f : 1f;
                Stain("Rust", body, new Vector3(s * (width / 2 + 0.004f), lowerY + F(-0.1f, 0.15f), F(-length * 0.4f, length * 0.4f)), F(0.18f, 0.4f), F(0.12f, 0.26f), M["metal"], 800 + r.Next(100), new Vector3(0, 0, s * -90f));
            }
        else
        {
            // 탄 차: 둘레 바닥의 그을음
            Stain("Scorch", g, new Vector3(0, 0.011f, 0), length * 0.62f, width * 0.95f, M["scorch"], 900 + r.Next(100)); // 차선 페인트보다 위 (z-fighting)
        }

        if (flipped)
        {
            // 통째로 뒤집어 지붕이 바닥에 닿게 내려놓는다
            body.localRotation = Quaternion.Euler(0, 0, 180);
            body.localPosition = Vector3.up * (roofTop + 0.005f);
        }
    }

    // 버려진 차들이 땅에 붙었는지 검사하고(렌더러 경계 아랫면이 바닥에서 5cm 넘게 뜨거나 묻히면) 바로잡는다. 결과를 콘솔에 적는다
    static void CheckGrounded(Transform root)
    {
        Physics.SyncTransforms();
        int fixedCount = 0, checkedCount = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>())
        {
            if (t.name != "WreckedCar") continue;
            checkedCount++;
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            float minY = float.MaxValue;
            foreach (var rd in rs) minY = Mathf.Min(minY, rd.bounds.min.y);
            // 바닥 높이: 차 가운데에서 아래로 쏜다 (차 자신은 뺀다)
            float ground = t.position.y;
            var hits = Physics.RaycastAll(t.position + Vector3.up * 3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (var h in hits)
                if (!h.collider.transform.IsChildOf(t) && h.point.y > best && h.point.y <= t.position.y + 0.3f) best = h.point.y;
            if (best > float.MinValue) ground = best;
            float gap = minY - ground;
            if (Mathf.Abs(gap) > 0.05f)
            {
                t.position -= Vector3.up * gap;
                fixedCount++;
                Debug.LogWarning($"[GameSceneBuilder] 떠 있거나 묻힌 차를 바로잡음: {t.parent.name}/{t.name} @ {t.position} (틈 {gap:0.00}m)");
            }
        }
        Debug.Log($"[GameSceneBuilder] 차 {checkedCount}대 바닥 검사, {fixedCount}대 바로잡음");
    }

    // ---------- 길가 잔해 ----------

    // 인도와 길가에 잔해를 더 흩어 놓는다: 벽돌 더미, 판자, 타이어, 드럼통, 부서진 의자, 종이, 콘크리트 조각, 여행 가방.
    // 따로 둔 난수를 쓰고, 출발 지점·해치·터널 입구·건물 안은 피한다
    static void ScatterDebris(Transform props, Transform points)
    {
        Physics.SyncTransforms();
        var r = new System.Random(404);
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var g = Group("Debris", props, Vector3.zero);
        int placed = 0;
        for (int i = 0; i < 160 && placed < 70; i++)
        {
            Vector3 p;
            if (r.NextDouble() < 0.6)
            {
                // 블록 가장자리 인도
                float bx = new[] { -46f, 0f, 46f }[r.Next(3)], bz = new[] { -46f, 0f, 46f }[r.Next(3)];
                float along = F(-16f, 16f), edge = F(14.0f, 16.5f) * (r.NextDouble() < 0.5 ? -1 : 1);
                p = new Vector3(bx, 0.15f, bz) + (r.NextDouble() < 0.5 ? new Vector3(along, 0, edge) : new Vector3(edge, 0, along));
            }
            else
            {
                // 길가 (차선 가운데는 비운다)
                float rc = RoadCenters[r.Next(RoadCenters.Length)] + (r.NextDouble() < 0.5 ? -1 : 1) * F(3.2f, 5.6f);
                float along = F(-74f, 74f);
                p = r.NextDouble() < 0.5 ? new Vector3(rc, 0, along) : new Vector3(along, 0, rc);
            }
            if (Vector3.Distance(p, SpawnPos) < 8f || Vector3.Distance(p, CityHatchPos) < 10f || Vector3.Distance(p, new Vector3(20, 0, 78.5f)) < 11f) continue;
            if (Physics.CheckSphere(p + Vector3.up * 0.5f, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
            p = hit.point;
            float yaw = F(0, 360);
            switch (r.Next(9))
            {
                case 0:
                {
                    var pile = Group("Bricks", g, p, yaw);
                    for (int k = 0, n = 3 + r.Next(5); k < n; k++)
                        Box("Brick", pile, new Vector3(F(-0.35f, 0.35f), 0.035f + (k % 2) * 0.07f, F(-0.3f, 0.3f)), new Vector3(0.2f, 0.07f, 0.095f), M["brick"], 1f, false, new Vector3(0, F(0, 360), 0));
                    break;
                }
                case 1:
                {
                    var pile = Group("Planks", g, p, yaw);
                    for (int k = 0, n = 2 + r.Next(2); k < n; k++)
                        Box("Plank", pile, new Vector3(F(-0.2f, 0.2f), 0.025f + k * 0.045f, 0), new Vector3(F(1.2f, 2f), 0.04f, 0.16f), M["wood"], 1f, false, new Vector3(0, F(-15, 15), 0));
                    break;
                }
                case 2:
                    PolyProp("old_tyre", g, p + Vector3.up * 0.085f, yaw, 0.35f, 0, true, new Vector3(90, 0, 0));
                    break;
                case 3:
                    if (r.NextDouble() < 0.5) Prim(PrimitiveType.Cylinder, "Barrel", g, p + Vector3.up * 0.45f, new Vector3(0.6f, 0.45f, 0.6f), r.NextDouble() < 0.5 ? M["metal"] : M["paintBlue"], new Vector3(0, yaw, 0), true);
                    else Prim(PrimitiveType.Cylinder, "BarrelTipped", g, p + Vector3.up * 0.3f, new Vector3(0.6f, 0.45f, 0.6f), M["metal"], new Vector3(90, yaw, 0), true);
                    break;
                case 4:
                {
                    // 부서진 의자: 쓰러진 등받이와 자리, 떨어져 나간 다리
                    var chair = Group("BrokenChair", g, p, yaw);
                    Box("Seat", chair, new Vector3(0, 0.12f, 0), new Vector3(0.45f, 0.05f, 0.45f), M["wood"], 1f, false, new Vector3(0, 0, 32));
                    Box("Back", chair, new Vector3(0.1f, 0.03f, -0.3f), new Vector3(0.45f, 0.5f, 0.04f), M["wood"], 1f, false, new Vector3(88, F(-20, 20), 0));
                    for (int k = 0; k < 3; k++)
                        Box("Leg", chair, new Vector3(F(-0.5f, 0.5f), 0.02f, F(-0.5f, 0.5f)), new Vector3(0.04f, 0.04f, 0.45f), M["wood"], 1f, false, new Vector3(0, F(0, 360), 0));
                    break;
                }
                case 5:
                {
                    var papers = Group("Papers", g, p, yaw);
                    for (int k = 0, n = 4 + r.Next(4); k < n; k++)
                        Box("Paper", papers, new Vector3(F(-0.8f, 0.8f), 0.003f, F(-0.8f, 0.8f)), new Vector3(F(0.2f, 0.3f), 0.003f, F(0.25f, 0.4f)), M["cloth"], 1f, false, new Vector3(0, F(0, 360), 0));
                    break;
                }
                case 6:
                    RubblePile(g, p, 3 + r.Next(3), F(0.5f, 0.9f));
                    break;
                case 7:
                {
                    // 여행 가방 (벌어진 뚜껑)
                    var bag = Group("Suitcase", g, p, yaw);
                    Box("Case", bag, new Vector3(0, 0.1f, 0), new Vector3(0.6f, 0.2f, 0.42f), M["blanket"], 1f, true);
                    Box("Lid", bag, new Vector3(0, 0.26f, -0.16f), new Vector3(0.6f, 0.03f, 0.42f), M["blanket"], 1f, false, new Vector3(-55, 0, 0));
                    Box("Latch", bag, new Vector3(0.15f, 0.1f, 0.215f), new Vector3(0.06f, 0.04f, 0.01f), M["metal"], 1f, false);
                    Box("Cloth", bag, new Vector3(0.05f, 0.22f, 0.05f), new Vector3(0.35f, 0.04f, 0.25f), M["cloth"], 1f, false, new Vector3(0, F(0, 60), 0));
                    break;
                }
                default:
                    PolyProp("trashbag", g, p, yaw, 0.55f, 0f);
                    break;
            }
            placed++;
            if (r.NextDouble() < 0.15) OutdoorItemPoint(points, p + Quaternion.Euler(0, yaw, 0) * new Vector3(0.9f, 0.1f, 0.3f));
        }
        Debug.Log($"[GameSceneBuilder] 길가 잔해 {placed}곳 추가");
    }

    // ---------- 벽에 기대 앉아 죽은 사람들 ----------

    // 건물 바깥벽 앞 인도에 시체를 놓는다 (블록마다 1~2구, 문 앞은 피한다). 바닥엔 마른 핏자국, 벽엔 핏자국이 번진다
    static void PlaceDeadBodies(Transform root)
    {
        Physics.SyncTransforms();
        var r = new System.Random(505);
        float F(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var group = Group("DeadBodies", root, Vector3.zero);
        var doors = Object.FindObjectsByType<Door>(FindObjectsSortMode.None);
        var placed = new List<Vector3>();
        int variant = 0;
        foreach (var bx in new[] { -46f, 0f, 46f })
            foreach (var bz in new[] { -46f, 0f, 46f })
            {
                int want = 1 + (r.NextDouble() < 0.4 ? 1 : 0), tries = 0;
                while (want > 0 && tries++ < 40)
                {
                    // 인도 위 한 점에서 블록 가운데 쪽(건물 벽)으로 쏜다
                    float along = F(-13.5f, 13.5f);
                    int side = r.Next(4);
                    Vector3 from = side == 0 ? new Vector3(bx + along, 0.9f, bz - 15.8f) : side == 1 ? new Vector3(bx + along, 0.9f, bz + 15.8f)
                               : side == 2 ? new Vector3(bx - 15.8f, 0.9f, bz + along) : new Vector3(bx + 15.8f, 0.9f, bz + along);
                    Vector3 dir = side == 0 ? Vector3.forward : side == 1 ? Vector3.back : side == 2 ? Vector3.right : Vector3.left;
                    if (!Physics.Raycast(from, dir, out var hit, 2.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    // 벽인가: 면이 수직이고 그 콜라이더가 2.5m보다 높다
                    if (Mathf.Abs(hit.normal.y) > 0.2f || hit.collider.bounds.size.y < 2.5f) continue;
                    if (Vector3.Dot(hit.normal, -dir) < 0.8f) continue;
                    var pos = hit.point + hit.normal * 0.34f;
                    // 바닥 (인도)
                    if (!Physics.Raycast(pos + Vector3.up * 1f, Vector3.down, out var floor, 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    pos.y = floor.point.y;
                    bool bad = false;
                    foreach (var d in doors) if (d != null && Vector3.Distance(d.transform.position, pos) < 1.8f) { bad = true; break; }
                    foreach (var o in placed) if (Vector3.Distance(o, pos) < 7f) { bad = true; break; }
                    if (bad || Vector3.Distance(pos, SpawnPos) < 7f) continue;
                    // 벽 말고 다른 것에 묻히지 않는가 (벽에서 0.34m 떨어져 있으니 반지름 0.26으로 본다)
                    if (Physics.CheckSphere(pos + Vector3.up * 0.55f, 0.26f, ~0, QueryTriggerInteraction.Ignore)) continue;

                    var go = new GameObject("DeadBody");
                    go.transform.SetParent(group, false);
                    go.transform.position = pos;
                    go.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up) * Quaternion.Euler(0, F(-12f, 12f), 0);
                    var body = go.AddComponent<DeadBody>();
                    body.variant = variant++ % CharacterModels.CorpseVariants;
                    body.poseSeed = r.Next(100000);
                    var col = go.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0, 0.5f, 0.18f);
                    col.radius = 0.4f;
                    col.height = 1.1f;
                    go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                    // 마른 핏자국: 바닥과 벽
                    Stain("BloodFloor", go.transform, new Vector3(F(-0.25f, 0.25f), 0.008f, F(0.1f, 0.35f)), F(0.35f, 0.6f), F(0.3f, 0.5f), M["bloodOld"], 1000 + r.Next(200));
                    if (r.NextDouble() < 0.7)
                        Stain("BloodWall", go.transform, new Vector3(F(-0.15f, 0.15f), F(0.6f, 0.95f), -0.34f + 0.012f), F(0.18f, 0.32f), F(0.25f, 0.45f), M["bloodOld"], 1200 + r.Next(200), new Vector3(90, 0, 0));
                    placed.Add(pos);
                    want--;
                }
            }
        Debug.Log($"[GameSceneBuilder] 벽에 기댄 시체 {placed.Count}구 배치");
    }

    // ---------- 잡상인 건물: 황금 문과 반딧불 ----------

    // 황금색 철문: 금빛 문짝·틀과 가로 띠, 검은 리벗, 작은 창, 고리 손잡이 (AddDoor의 철문 꼴을 금빛으로). Door 부품은 그대로
    static void AddGoldenDoor(Transform wall, float c, float width, float height, float wallThick = 0.3f)
    {
        var gold = M["gold"];
        DoorCasing(wall, c, width, height, wallThick, gold);
        var pivot = Group("Door", wall, new Vector3(c - width / 2 + 0.03f, 0, 0));
        pivot.gameObject.AddComponent<Door>();
        pivot.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        float lw = width - 0.08f, lh = height - 0.04f, lt = 0.07f;
        var leafGo = Box("Leaf", pivot, new Vector3(width / 2 - 0.03f, height / 2, 0), new Vector3(lw, lh, lt), gold, 1.2f, true, null, false);
        var leaf = leafGo.transform;
        foreach (int s in new[] { -1, 1 })
        {
            float z = s * (lt / 2);
            foreach (float y in new[] { -lh * 0.32f, 0f, lh * 0.32f })
            {
                Box("Band", leaf, new Vector3(0, y, z + s * 0.006f), new Vector3(lw - 0.06f, 0.08f, 0.012f), gold, 1f, false, null, false);
                for (int i = 0; i < 5; i++)
                    Prim(PrimitiveType.Sphere, "Rivet", leaf, new Vector3(-lw / 2 + 0.08f + i * (lw - 0.16f) / 4f, y, z + s * 0.014f), new Vector3(0.02f, 0.02f, 0.012f), M["tire"]);
            }
            Box("KickPlate", leaf, new Vector3(0, -lh / 2 + 0.13f, z + s * 0.005f), new Vector3(lw - 0.04f, 0.22f, 0.01f), M["metal"], 1f, false, null, false);
            // 고리 손잡이 (두드리는 고리)와 받침
            Prim(PrimitiveType.Cylinder, "KnockerBase", leaf, new Vector3(lw / 2 - 0.16f, -0.02f, z + s * 0.008f), new Vector3(0.07f, 0.006f, 0.07f), gold, new Vector3(90, 0, 0));
            var ring = Prim(PrimitiveType.Cylinder, "Knocker", leaf, new Vector3(lw / 2 - 0.16f, -0.08f, z + s * 0.03f), new Vector3(0.1f, 0.012f, 0.1f), gold, new Vector3(90, 0, 0));
            Prim(PrimitiveType.Cylinder, "KnockerHole", ring.transform, Vector3.zero, new Vector3(0.6f, 1.2f, 0.6f), M["tire"]);
            Box("Nameplate", leaf, new Vector3(0, lh * 0.33f + 0.3f, z + s * 0.006f), new Vector3(0.3f, 0.08f, 0.01f), M["line"], 1f, false, null, false);
        }
        float wy = lh * 0.33f + 0.12f;
        Box("Window", leaf, new Vector3(0, wy, 0), new Vector3(0.3f, 0.22f, lt + 0.004f), M["glass"], 1f, false, null, false);
        foreach (int s in new[] { -1, 1 })
        {
            float fz = s * (lt / 2 + 0.008f);
            Box("WindowFrame", leaf, new Vector3(0, wy + 0.125f, fz), new Vector3(0.36f, 0.03f, 0.012f), gold, 1f, false, null, false);
            Box("WindowFrame", leaf, new Vector3(0, wy - 0.125f, fz), new Vector3(0.36f, 0.03f, 0.012f), gold, 1f, false, null, false);
            Box("WindowFrame", leaf, new Vector3(-0.165f, wy, fz), new Vector3(0.03f, 0.22f, 0.012f), gold, 1f, false, null, false);
            Box("WindowFrame", leaf, new Vector3(0.165f, wy, fz), new Vector3(0.03f, 0.22f, 0.012f), gold, 1f, false, null, false);
        }
        var obstacle = leafGo.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(lw, height, 0.3f);
        obstacle.carving = true;
    }

    // 잡상인 둘레의 반딧불(노랗게 깜빡이며 떠도는 알갱이, 몇 개는 작은 불빛을 낸다)과 은은히 고동치는 금빛 불,
    // 문 밖에도 반딧불 몇 마리가 떠돌아 밖에서도 잡상인이 있음을 안다. 모두 Trader 그룹의 자식이라 잡상인이 없는 탐험에는 함께 꺼진다
    static void TraderGlow(Transform building, Transform doorWall, float doorC, int doorSide)
    {
        var trader = building.Find("Trader");
        if (trader == null) return;
        var glow = PointLight("FireflyGlow", trader, new Vector3(0, 1.5f, 0), new Color(1f, 0.86f, 0.45f), 1.4f, 6.5f);
        Flicker(glow, 0.35f, 1.1f, false);
        Fireflies(trader, Vector3.zero, 2.4f, 7f);
        // 문 밖: 벽의 바깥쪽으로 0.9m
        var doorWorld = doorWall.TransformPoint(new Vector3(doorC, 0, 0));
        var outward = (doorSide == 1 || doorSide == 2) ? doorWall.forward : -doorWall.forward;
        var outside = new GameObject("FirefliesOutside").transform;
        outside.SetParent(trader, true);
        outside.position = doorWorld + outward * 0.9f;
        Fireflies(outside, Vector3.zero, 1.8f, 6f);
        var doorGlow = PointLight("DoorGlow", outside, new Vector3(0, 1.4f, 0), new Color(1f, 0.85f, 0.45f), 0.9f, 4.5f);
        Flicker(doorGlow, 0.4f, 1.3f, false);
    }

    static void Fireflies(Transform parent, Vector3 localPos, float radius, float rate)
    {
        var go = new GameObject("Fireflies");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos + Vector3.up * 1.2f;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.075f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.8f, 1.5f, 0.6f), new Color(1.8f, 1.2f, 0.45f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.prewarm = true;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.28f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.3f;
        noise.octaveCount = 2;
        noise.damping = true;
        // 깜빡임: 수명 동안 몇 번 밝아졌다 어두워진다 (수명이 제각각이라 서로 어긋난다)
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.12f, 0.25f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0.08f, 0.55f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0.15f, 0.85f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = M["fire"];
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.maxParticleSize = 0.02f;
        // 몇 마리는 작은 불빛을 낸다 (눈부시지 않게 약하게)
        var template = new GameObject("FireflyLight");
        template.transform.SetParent(go.transform, false);
        var l = template.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.85f, 0.4f);
        l.intensity = 0.35f;
        l.range = 1.6f;
        l.shadows = LightShadows.None;
        template.SetActive(false);
        var lights = ps.lights;
        lights.enabled = true;
        lights.light = l;
        lights.ratio = 0.2f;
        lights.maxLights = 4;
        lights.useParticleColor = true;
        lights.sizeAffectsRange = false;
        lights.alphaAffectsIntensity = true;
        lights.intensityMultiplier = 1f;
        lights.rangeMultiplier = 1f;
    }
}
