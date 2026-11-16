using System.Collections.Generic;
using UnityEngine;

// GameSceneBuilder의 삼일아파트 102동 탑 안쪽 (Apt102 씬): 방 칸막이·불길·유리 조각·바리케이드·야영지, 303호 시체와 쪽지, 4호 줄 꾸밈,
// 잠긴 현관문과 판자로 막은 창, 옥상 중계기함·안테나·전선·잘린 면 꾸밈, 지킴이 자리·아이템 자리·위험 구역.
// 좌표는 모두 탑 로컬 (원점 = 건물 바닥 가운데, +z = 발코니 쪽, +x = 부서진 끝). 벽·바닥판·계단·옥상 벽은 GameSceneBuilder.AptTower.cs가 먼저 짓는다.
// 꾸밈에는 콜라이더를 달지 않는다 (콜라이더는 내비메시와 바닥 구멍 아래 내려설 자리를 바꾼다).
// 콜라이더가 있는 것: 칸막이, 바리케이드, 모닥불 통, 야영지 상자, 잠긴 문, 중계기함, 시체, 303호 탁자와 쪽지, 403호 조리대
public static partial class GameSceneBuilder
{
    // 아이템 자리에서 나올 것 (같은 것을 여러 번 적으면 그만큼 잘 나온다). 임시 수치
    static readonly string[] AptRmLootHome = { "water", "water", "canned_food", "canned_food", "bandage", "bandage", "cloth", "cloth", "battery", "copper_wire", "rope", "stick" };
    static readonly string[] AptRmLootTools = { "copper_wire", "copper_wire", "copper_wire", "metal", "metal", "battery", "battery", "rubber", "rope", "screw" };
    static readonly string[] AptRmLootStash = { "canned_food", "canned_food", "water", "water", "bandage", "battery", "metal", "leather", "rubber", "fuel" };

    // 4호 줄 바닥 구멍 (x, z, 폭, 깊이). 구조 쪽(AptTower.cs)과 같은 값이다: 꾸밈이 구멍 위 허공에 뜨지 않게 피한다
    static readonly Rect AptRmH2 = new Rect(9.6f, 2.6f, 2f, 2f), AptRmH3 = new Rect(9.6f, -3.8f, 2f, 2f), AptRmH4 = new Rect(9.6f, -3.8f, 2f, 3.4f),
        AptRmH5 = new Rect(9.6f, 2.6f, 2f, 2f), AptRmH6 = new Rect(9.6f, -2.4f, 2f, 2f), AptRmH7 = new Rect(9.6f, 2.6f, 2f, 2f);

    const string AptRmFallHint = "바닥 구멍으로 한 층은 뛰어내려도 된다. 두 층을 한꺼번에 떨어지면 크게 다친다. 내려다보고 뛰자.";

    // 실내 아이템 자리 하나 (탑 로컬, 바닥에서 0.2m 위: PickupItem.Spawn이 아래 바닥으로 붙인다). list = 여기서 하나가 나오는 목록, fixedId = 반드시 놓이는 것
    struct AptRmSpot
    {
        public string name;
        public float x, y, z;
        public string[] list;
        public float chance;
        public string fixedId;
        public int amount;

        public AptRmSpot(string name, float x, float y, float z, string[] list, float chance = 0f)
        {
            this.name = name; this.x = x; this.y = y; this.z = z;
            this.list = list; this.chance = chance; fixedId = null; amount = 0;
        }

        public AptRmSpot(string name, float x, float y, float z, string fixedId, int amount)
        {
            this.name = name; this.x = x; this.y = y; this.z = z;
            list = null; chance = 0f; this.fixedId = fixedId; this.amount = amount;
        }
    }

    const float AptRmSome = 0.7f, AptRmMost = 0.8f; // 임시 수치: 나올 확률 (적지 않은 자리는 GameBalance.itemSpawnChance)

    // 이름은 Item_<방>_<번호>, 반드시 놓이는 자리는 ItemFixed_<방>_<번호> (점검이 이 이름으로 센다). 꾸밈은 이 자리들을 피해서 놓는다.
    // 자리마다의 목록·확률, 고정 품목과 개수는 임시 수치
    static readonly AptRmSpot[] AptRmSpots =
    {
        new AptRmSpot("Item_204_1", 9.0f, 3.0f, 1.2f, AptRmLootHome),
        new AptRmSpot("Item_204_2", 12.2f, 3.0f, -1.0f, AptRmLootHome),
        new AptRmSpot("ItemFixed_204_1", 8.9f, 3.0f, -4.8f, "battery", 1),

        new AptRmSpot("Item_303_1", 0.8f, 5.8f, 4.6f, AptRmLootHome, AptRmSome),
        new AptRmSpot("Item_303_2", 2.4f, 5.8f, 0.7f, AptRmLootHome, AptRmSome),
        new AptRmSpot("Item_303_3", 4.0f, 5.8f, 4.7f, AptRmLootHome, AptRmSome),
        new AptRmSpot("ItemFixed_303_1", 0.7f, 5.8f, 1.2f, "bandage", 2),

        new AptRmSpot("Item_304_1", 12.2f, 5.8f, -4.4f, AptRmLootTools, AptRmMost),
        new AptRmSpot("Item_304_2", 9.0f, 5.8f, 0.5f, AptRmLootTools, AptRmMost),
        new AptRmSpot("ItemFixed_304_1", 12.2f, 5.8f, 1.5f, "bandage", 1),

        new AptRmSpot("Item_403_1", 0.8f, 8.6f, -4.6f, AptRmLootHome),
        new AptRmSpot("Item_403_2", 0.6f, 8.6f, -1.0f, AptRmLootHome),
        new AptRmSpot("Item_403_3", 4.1f, 8.6f, 1.2f, AptRmLootHome),
        new AptRmSpot("Item_403_4", 3.9f, 8.6f, 4.7f, AptRmLootHome),

        new AptRmSpot("Item_402_1", -3.9f, 8.6f, 4.6f, AptRmLootHome),
        new AptRmSpot("Item_402_2", -0.7f, 8.6f, -1.0f, AptRmLootHome),
        new AptRmSpot("Item_402_3", -0.7f, 8.6f, -4.7f, AptRmLootTools),

        new AptRmSpot("Item_404_1", 12.2f, 8.6f, 1.2f, AptRmLootTools),

        new AptRmSpot("ItemFixed_504_1", 9.0f, 11.4f, 4.8f, "bandage", 1),
        new AptRmSpot("Item_504_1", 12.2f, 11.4f, 1.5f, AptRmLootTools, AptRmMost),

        new AptRmSpot("Item_Hall_1", 4.1f, 14.2f, 3.4f, AptRmLootStash, AptRmMost),
        new AptRmSpot("Item_Hall_2", 4.1f, 14.2f, 4.5f, AptRmLootStash, AptRmMost),
        new AptRmSpot("Item_Hall_3", 3.2f, 14.2f, 4.7f, AptRmLootStash, AptRmMost),
        new AptRmSpot("Item_Hall_4", 0.4f, 14.2f, 4.7f, AptRmLootStash, AptRmMost),
        new AptRmSpot("ItemFixed_Hall_1", 2.4f, 14.2f, 4.7f, "canned_food", 1),

        new AptRmSpot("Item_604_1", 12.2f, 14.2f, -4.6f, AptRmLootTools),
        new AptRmSpot("Item_604_2", 9.0f, 14.2f, 0.8f, AptRmLootTools),

        new AptRmSpot("ItemFixed_704_1", 12.2f, 17.0f, 4.6f, "copper_wire", 2),
        new AptRmSpot("Item_704_1", 9.0f, 17.0f, 2.0f, AptRmLootStash, AptRmMost),
        new AptRmSpot("Item_704_2", 12.2f, 17.0f, -4.6f, AptRmLootStash, AptRmMost),

        new AptRmSpot("Item_804_1", 12.2f, 19.8f, 0.5f, AptRmLootTools),
    };

    // 방 안 꾸밈·지킴이·아이템·함정. tower = "Tower" 그룹 (로컬 = 설계 좌표)
    static void BuildAptTowerRooms(Transform tower, Transform points, Transform dyn, System.Random r)
    {
        // 방마다 고정 씨앗으로 난수를 따로 쓴다. 탑 구조의 난수(r)를 건드리지 않으니 한쪽을 고쳐도 다른 쪽 모양이 바뀌지 않는다
        AptRm204(tower, dyn);
        AptRm303(tower);
        AptRm304(tower);
        AptRm403(tower, dyn);
        AptRm402(tower);
        AptRm404(tower, dyn);
        AptRm504(tower, dyn);
        AptRmHall(tower, dyn);
        AptRm604(tower);
        AptRm704(tower);
        AptRm804(tower);
        AptRmSealedDoors(tower);
        AptRmRoof(tower, dyn);
        AptRmGuards(tower, points);
        AptRmItems(tower, points);
    }

    // ---------- 도우미 ----------

    // 방 하나의 콜라이더 없는 꾸밈 조각을 재질별로 모았다가 재질마다 한 덩어리로 합친다 (물체 수를 줄인다)
    sealed class AptRmParts
    {
        readonly List<string> keys = new List<string>();
        readonly Dictionary<string, List<CombineInstance>> lists = new Dictionary<string, List<CombineInstance>>();

        public List<CombineInstance> this[string key]
        {
            get
            {
                if (!lists.TryGetValue(key, out var list))
                {
                    lists[key] = list = new List<CombineInstance>();
                    keys.Add(key);
                }
                return list;
            }
        }

        public void Flush(Transform room)
        {
            foreach (var key in keys) CombineInto(room, "Decor_" + key, lists[key], M[key]);
        }
    }

    // 꾸밈 상자 하나를 합칠 목록에 넣는다 (가운데·크기·회전. 방 그룹이 탑 원점에 있어 탑 로컬 좌표 그대로다)
    static void AptRmPart(List<CombineInstance> parts, float x, float y, float z, float sx, float sy, float sz, float yaw = 0f, float pitch = 0f, float roll = 0f)
        => KPart(parts, new Vector3(sx, sy, sz), 1f, new Vector3(x, y, z), Quaternion.Euler(pitch, yaw, roll));

    // 이 층 바닥(y)의 아이템 자리 바로 위인가: 그런 데에는 잔해를 흩지 않는다 (작은 아이템이 조각 속에 묻혀 안 보인다)
    static bool AptRmOnSpot(float x, float y, float z)
    {
        foreach (var s in AptRmSpots)
            if (Mathf.Abs(s.y - 0.2f - y) < 0.3f && Mathf.Abs(s.x - x) < 0.45f && Mathf.Abs(s.z - z) < 0.45f) return true;
        return false;
    }

    // 납작한 잔해 조각을 흩는다. keepOut(x, z, 폭, 깊이) = 바닥 구멍·불 자리처럼 비워 둘 곳 (조각 크기만큼 0.3m 여유를 둔다)
    static void AptRmScatter(List<CombineInstance> parts, System.Random rr, float x0, float x1, float z0, float z1, float y, int count, params Rect[] keepOut)
    {
        for (int placed = 0, tries = 0; placed < count && tries < count * 10; tries++)
        {
            float x = KF(rr, x0, x1), z = KF(rr, z0, z1);
            bool blocked = AptRmOnSpot(x, y, z);
            foreach (var k in keepOut)
                if (x > k.xMin - 0.3f && x < k.xMax + 0.3f && z > k.yMin - 0.3f && z < k.yMax + 0.3f) blocked = true;
            if (blocked) continue;
            // 크기는 몇 가지로만 고른다 (상자 메시 종류가 늘지 않게)
            var size = new Vector3(0.12f + 0.12f * rr.Next(4), 0.06f, 0.1f + 0.1f * rr.Next(4));
            KPart(parts, size, 1f, new Vector3(x, y + 0.03f, z), Quaternion.Euler(KF(rr, -8f, 8f), KF(rr, 0f, 180f), 0));
            placed++;
        }
    }

    // 유리 조각밭: 얇은 조각을 한 덩어리로 (콜라이더 없음. 밟는 소리는 HazardZone이 낸다). 겹친 조각끼리 면이 떨리지 않게 높이를 조금씩 달리한다
    static void AptRmGlass(Transform room, System.Random rr, float x0, float x1, float z0, float z1, float y, int count)
    {
        var parts = new List<CombineInstance>();
        for (int i = 0; i < count; i++)
        {
            var size = new Vector3(0.1f + 0.05f * rr.Next(4), 0.01f, 0.1f + 0.05f * rr.Next(3));
            KPart(parts, size, 1f, new Vector3(KF(rr, x0, x1), y + 0.006f + i * 0.0004f, KF(rr, z0, z1)), Quaternion.Euler(0, KF(rr, 0f, 180f), 0));
        }
        CombineInto(room, "GlassField", parts, M["glass"]);
    }

    // 바닥에 누운 문틀 (막대 넷): 유리 조각이 어디서 나왔는지 보여 준다
    static void AptRmFrame(List<CombineInstance> parts, float cx, float y, float cz, float w, float d, float yaw)
    {
        var q = Quaternion.Euler(0, yaw, 0);
        var c = new Vector3(cx, y + 0.025f, cz);
        KPart(parts, new Vector3(w, 0.05f, 0.05f), 1f, c + q * new Vector3(0, 0, -d / 2f), q);
        KPart(parts, new Vector3(w, 0.05f, 0.05f), 1f, c + q * new Vector3(0, 0, d / 2f), q);
        KPart(parts, new Vector3(0.05f, 0.05f, d - 0.1f), 1f, c + q * new Vector3(-w / 2f + 0.025f, 0, 0), q);
        KPart(parts, new Vector3(0.05f, 0.05f, d - 0.1f), 1f, c + q * new Vector3(w / 2f - 0.025f, 0, 0), q);
    }

    // 판자로 막은 창 하나 (앞·뒤 안쪽 벽). 방 안에서 바깥이 보이지 않는 까닭이다. inward = 방 안쪽 방향 (앞벽 -1, 뒷벽 +1). 벽에서 3cm쯤 떠 있다
    static void AptRmWindow(AptRmParts bag, System.Random rr, float x, float floorY, float wallZ, float inward)
    {
        float cy = floorY + 1.45f;
        KPart(bag["glass"], new Vector3(1.6f, 1.4f, 0.02f), 1f, new Vector3(x, cy, wallZ + inward * 0.01f), Quaternion.identity);
        KPart(bag["wood"], new Vector3(1.86f, 0.16f, 0.03f), 1f, new Vector3(x + KF(rr, -0.05f, 0.05f), cy + 0.38f, wallZ + inward * 0.03f), Quaternion.Euler(0, 0, KF(rr, -4f, 4f)));
        KPart(bag["wood"], new Vector3(1.86f, 0.16f, 0.03f), 1f, new Vector3(x + KF(rr, -0.05f, 0.05f), cy - 0.36f, wallZ + inward * 0.03f), Quaternion.Euler(0, 0, KF(rr, -4f, 4f)));
        float lean = (rr.NextDouble() < 0.5 ? 1f : -1f) * KF(rr, 26f, 34f);
        KPart(bag["wood"], new Vector3(2.0f, 0.14f, 0.03f), 1f, new Vector3(x, cy, wallZ + inward * 0.036f), Quaternion.Euler(0, 0, lean));
    }

    // 매트리스와 베개 (높이 0.2)
    static void AptRmMattress(AptRmParts bag, float x, float y, float z, float yaw)
    {
        AptRmPart(bag["blanket"], x, y + 0.1f, z, 0.9f, 0.2f, 1.9f, yaw);
        var head = Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 0.72f);
        AptRmPart(bag["cloth"], x + head.x, y + 0.25f, z + head.z, 0.5f, 0.1f, 0.3f, yaw + 6f);
    }

    // 바닥에 편 이부자리와 베개 (높이 0.12)
    static void AptRmBedroll(AptRmParts bag, float x, float y, float z, float yaw)
    {
        AptRmPart(bag["blanket"], x, y + 0.06f, z, 0.9f, 0.12f, 1.9f, yaw);
        var head = Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 0.68f);
        AptRmPart(bag["cloth"], x + head.x, y + 0.17f, z + head.z, 0.5f, 0.1f, 0.3f, yaw - 5f);
    }

    // 낮은 탁자 (높이 0.38): 상판과 다리 넷
    static void AptRmLowTable(AptRmParts bag, float x, float y, float z, float w, float d, float yaw)
    {
        var q = Quaternion.Euler(0, yaw, 0);
        AptRmPart(bag["wood"], x, y + 0.355f, z, w, 0.05f, d, yaw);
        foreach (float lx in new[] { -w / 2f + 0.06f, w / 2f - 0.06f })
            foreach (float lz in new[] { -d / 2f + 0.06f, d / 2f - 0.06f })
            {
                var o = q * new Vector3(lx, 0, lz);
                AptRmPart(bag["wood"], x + o.x, y + 0.165f, z + o.z, 0.05f, 0.33f, 0.05f, yaw);
            }
    }

    // 4호 줄 방의 공통 꾸밈: 바닥 잔해(바닥 구멍은 피한다)와 앞뒤 벽의 막힌 창 넷
    static void AptRm04Base(AptRmParts bag, System.Random rr, float y, int cracked, int wood, params Rect[] keepOut)
    {
        AptRmScatter(bag["cracked"], rr, 8.75f, 12.45f, -4.95f, 4.95f, y, cracked, keepOut);
        AptRmScatter(bag["wood"], rr, 8.75f, 12.45f, -4.95f, 4.95f, y, wood, keepOut);
        foreach (float x in new[] { 9.55f, 11.65f })
        {
            AptRmWindow(bag, rr, x, y, 5.25f, -1f);
            AptRmWindow(bag, rr, x, y, -5.25f, 1f);
        }
    }

    // 위험 구역 (콜라이더 없이 HazardZone이 플레이어 위치만 본다). local = 바닥 가운데 (탑 로컬), 상자는 탑과 같은 방향
    static HazardZone AptRmHazard(Transform tower, Transform dyn, string name, HazardZone.Kind kind, Vector3 local, Vector3 half)
    {
        var zone = Spawn<HazardZone>(dyn, tower.TransformPoint(local));
        zone.gameObject.name = name;
        zone.transform.rotation = tower.rotation;
        zone.kind = kind;
        zone.halfSize = half;
        return zone;
    }

    // 지킴이 자리: 반드시 서고 (비율·안전 거리 무시) yaw 방향을 본다
    static void AptRmGuard(Transform tower, Transform points, string name, SpawnKind kind, int tier, Vector3 local, float yaw)
    {
        var p = Spawn<ZombieSpawnPoint>(points, tower.TransformPoint(local));
        p.gameObject.name = name;
        p.transform.rotation = Quaternion.Euler(0, tower.eulerAngles.y + yaw, 0);
        p.upstairs = true;
        p.guaranteed = true;
        p.kind = kind;
        p.tier = tier;
    }

    // ---------- 4호 줄 (L1~L7): 칸막이 없는 빈 껍데기 방. 바닥 구멍이 굴뚝처럼 이어진다 ----------

    // 204호 (L1): 동쪽 벽이 뚫린 들머리 방. 폭발에 안으로 쏟아진 덩어리, 그을음, 낮은 가구. 울림 힌트가 여기서 뜬다
    static void AptRm204(Transform tower, Transform dyn)
    {
        float y = AptY(1);
        var room = Group("Room204", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(20401);
        AptRm04Base(bag, rr, y, 10, 4);
        // 뚫린 벽 남쪽 옆과 북동 구석의 덩어리 (들어오는 길 z 2.2~5.0과 위층 구멍 아래 x 9.6~11.6, z 2.6~4.6에는 두지 않는다)
        AptRmPart(bag["dirty"], 12.42f, y + 0.11f, 1.75f, 0.5f, 0.22f, 0.4f, 25f, 4f, -6f);
        AptRmPart(bag["dirty"], 12.5f, y + 0.08f, 1.2f, 0.3f, 0.16f, 0.3f, 60f, 0f, 5f);
        AptRmPart(bag["dirty"], 11.9f, y + 0.07f, 1.5f, 0.35f, 0.14f, 0.25f, -30f, 3f, 0f);
        AptRmPart(bag["dirty"], 12.35f, y + 0.1f, 5.08f, 0.5f, 0.2f, 0.3f, 10f, -5f, 4f);
        // 뚫린 자리 위턱·옆턱에서 삐져나온 철근 (머리 위)
        Rebar(room, new Vector3(12.86f, y + 2.4f, 2.9f), new Vector3(-0.5f, -1f, 0.2f), 0.45f, rr);
        Rebar(room, new Vector3(12.9f, y + 2.4f, 4.2f), new Vector3(-0.4f, -1f, -0.3f), 0.4f, rr);
        Rebar(room, new Vector3(12.88f, y + 2.05f, 2.2f), new Vector3(-0.6f, -0.4f, 0.5f), 0.3f, rr);
        AptRmMattress(bag, 9.0f, y, -0.95f, 3f);
        AptRmPart(bag["wood"], 12.47f, y + 0.2f, -2.9f, 0.45f, 0.4f, 1.3f, -4f); // 넘어진 낮은 장
        Stain("Scorch", room, new Vector3(11.8f, y + 0.012f, 3.6f), 1.2f, 1.5f, M["scorch"], 4711, Vector3.zero);
        bag.Flush(room);

        var echo = AptRmHazard(tower, dyn, "Hint_Echo", HazardZone.Kind.Hint, new Vector3(12.4f, y, 3.6f), new Vector3(0.9f, 1.2f, 1.4f));
        echo.hintBit = Tutorial.HintEcho;
        echo.hintText = "이 건물은 소리가 울린다. 걸으면 위아래 층이 다 듣는다. 앉아서 걸으면 조용하다.";
    }

    // 304호 (L2): 위에서 떨어져야만 들어오는 갇힌 방. 두 층을 떨어지는 자리(H3 아래)에 마른 핏자국
    static void AptRm304(Transform tower)
    {
        float y = AptY(2);
        var room = Group("Room304", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(30401);
        AptRm04Base(bag, rr, y, 9, 4, AptRmH2);
        Stain("BloodOld", room, new Vector3(10.55f, y + 0.012f, -2.75f), 0.6f, 0.45f, M["bloodOld"], 4721);
        Stain("BloodOld", room, new Vector3(11.15f, y + 0.013f, -2.2f), 0.18f, 0.14f, M["bloodOld"], 4722);
        AptRmMattress(bag, 9.0f, y, -0.75f, -2f);                                  // 떨어져 내려서는 자리(z -3.8~-1.8) 옆은 비운다
        AptRmPart(bag["wood"], 11.35f, y + 0.21f, -4.92f, 0.9f, 0.42f, 0.5f, 5f);  // 공구 상자
        AptRmPart(bag["metal"], 12.5f, y + 0.22f, -0.6f, 0.4f, 0.44f, 1.1f, 2f);   // 쓰러진 철제 선반
        bag.Flush(room);
    }

    // 404호 (L3): 4층 층계참에서 굴뚝으로 빠지는 방. 북쪽 끝이 불타고 있다 (F2)
    static void AptRm404(Transform tower, Transform dyn)
    {
        float y = AptY(3);
        var room = Group("Room404", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(40401);
        AptRm04Base(bag, rr, y, 8, 3, AptRmH3, new Rect(9.2f, 3.4f, 2.8f, 1.6f));
        AptTFire(room, new Vector3(10.6f, y + 0.1f, 4.2f), 5f, 5f, false);
        AptRmHazard(tower, dyn, "Fire_F2", HazardZone.Kind.Fire, new Vector3(10.6f, y, 4.2f), new Vector3(1.4f, 1.0f, 0.8f));
        // 타고 있는 가구 더미 (낮게 둔다: 불꽃이 그 위로 올라온다)
        AptRmPart(bag["soot"], 10.6f, y + 0.09f, 4.2f, 1.6f, 0.18f, 0.7f, 6f);
        AptRmPart(bag["soot"], 10.05f, y + 0.14f, 4.5f, 0.9f, 0.28f, 0.5f, -20f, 0f, 5f);
        AptRmPart(bag["soot"], 11.3f, y + 0.06f, 4.0f, 0.5f, 0.12f, 0.9f, 35f);
        Stain("Scorch", room, new Vector3(10.6f, y + 0.012f, 4.1f), 1.5f, 0.95f, M["scorch"], 4731, Vector3.zero);
        Stain("Soot", room, new Vector3(10.6f, y + 1.45f, 5.238f), 1.4f, 1.0f, M["soot"], 4732, new Vector3(-90f, 0, 0)); // 앞벽에 번진 그을음 (방 안쪽을 본다)
        AptRmLowTable(bag, 8.8f, y, 1.5f, 0.55f, 1.0f, -3f);
        bag.Flush(room);
    }

    // 504호 (L4): 갇힌 방. 긴 바닥 구멍(H4, z -3.8~-0.4)의 남쪽 2m 아래는 404호 바닥도 뚫려 있어(H3) 304호까지 두 층을 떨어진다. 추락 힌트가 여기서 뜬다
    static void AptRm504(Transform tower, Transform dyn)
    {
        float y = AptY(4);
        var room = Group("Room504", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(50401);
        AptRm04Base(bag, rr, y, 9, 4, AptRmH4);
        AptRmMattress(bag, 9.0f, y, 1.3f, 2f);
        AptRmLowTable(bag, 12.42f, y, -4.45f, 0.55f, 0.9f, 4f);
        Stain("Scorch", room, new Vector3(10.6f, y + 0.012f, 4.1f), 1.3f, 0.9f, M["scorch"], 4771, Vector3.zero); // 바로 아래층(404호)이 타고 있다: 그 위 바닥이 그을었다
        bag.Flush(room);

        var fall = AptRmHazard(tower, dyn, "Hint_Fall_504", HazardZone.Kind.Hint, new Vector3(10.6f, y, 1.2f), new Vector3(1.5f, 1.2f, 1.0f));
        fall.hintBit = Tutorial.HintFall;
        fall.hintText = AptRmFallHint;
    }

    // 604호 (L5): 6층 층계참에서 굴뚝으로 빠지는 방. 약탈자가 쓰던 이부자리와 상자
    static void AptRm604(Transform tower)
    {
        float y = AptY(5);
        var room = Group("Room604", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(60401);
        AptRm04Base(bag, rr, y, 9, 4, AptRmH5);
        AptRmBedroll(bag, 12.25f, y, 1.2f, 0f);
        AptRmPart(bag["wood"], 12.4f, y + 0.2f, 4.9f, 0.5f, 0.4f, 0.5f, 7f); // 낮은 상자 (북동 구석)
        Stain("Soot", room, new Vector3(12.738f, y + 1.4f, -2.0f), 0.9f, 1.1f, M["soot"], 4772, new Vector3(0, 0, 90f)); // 동쪽 벽의 그을음 (-x, 방 안쪽을 본다)
        bag.Flush(room);
    }

    // 704호 (L6): 갇힌 방. 전선 타래와 낮은 작업대 (구리선이 반드시 놓이는 방)
    static void AptRm704(Transform tower)
    {
        float y = AptY(6);
        var room = Group("Room704", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(70401);
        AptRm04Base(bag, rr, y, 9, 4, AptRmH6);
        // 전선 타래 (나무 원판 둘 사이에 검은 전선)
        KCylAt(bag["wood"], new Vector3(12.3f, y + 0.02f, 3.6f), 0.3f, 0.04f, Quaternion.identity);
        KCylAt(bag["tire"], new Vector3(12.3f, y + 0.19f, 3.6f), 0.15f, 0.3f, Quaternion.identity);
        KCylAt(bag["wood"], new Vector3(12.3f, y + 0.36f, 3.6f), 0.3f, 0.04f, Quaternion.identity);
        // 바닥에 풀어 놓은 전선 고리
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2f / 10f, b = (i + 1) * Mathf.PI * 2f / 10f;
            KRod(bag["tire"], new Vector3(9.1f + Mathf.Cos(a) * 0.25f, y + 0.02f, 3.5f + Mathf.Sin(a) * 0.25f), new Vector3(9.1f + Mathf.Cos(b) * 0.25f, y + 0.02f, 3.5f + Mathf.Sin(b) * 0.25f), 0.015f);
        }
        AptRmLowTable(bag, 8.8f, y, -3.9f, 0.6f, 1.4f, 2f);
        Stain("Scorch", room, new Vector3(10.6f, y + 0.012f, 3.5f), 1.0f, 0.8f, M["scorch"], 4773, Vector3.zero); // 위층 구멍(H7) 아래 바닥의 그을음
        bag.Flush(room);
    }

    // 804호 (L7): 옥상 구덩이(H8) 바로 아래. 지붕에서 떨어진 덩어리와 그을음, 구덩이 둘레에 늘어진 철근
    static void AptRm804(Transform tower)
    {
        float y = AptY(7);
        var room = Group("Room804", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(80401);
        AptRm04Base(bag, rr, y, 10, 4, AptRmH7);
        // 내려서는 자리(x 10.6~12.6, z -4.9~-2.9) 둘레의 덩어리
        AptRmPart(bag["dirty"], 12.45f, y + 0.12f, -1.9f, 0.45f, 0.24f, 0.5f, 15f, 5f, 0f);
        AptRmPart(bag["dirty"], 12.5f, y + 0.08f, -1.2f, 0.3f, 0.16f, 0.35f, -40f, 0f, 6f);
        AptRmPart(bag["dirty"], 10.15f, y + 0.1f, -5.0f, 0.5f, 0.2f, 0.35f, 8f, -4f, 0f);
        Stain("Scorch", room, new Vector3(11.6f, y + 0.012f, -3.9f), 1.1f, 1.1f, M["scorch"], 4741, Vector3.zero);
        // 구덩이 가장자리(옥상 바닥판 밑면 y 22.2)에서 늘어진 철근
        Rebar(room, new Vector3(10.6f, 22.2f, -3.4f), new Vector3(0.3f, -1f, 0.1f), 0.4f, rr);
        Rebar(room, new Vector3(11.7f, 22.2f, -2.9f), new Vector3(0f, -1f, -0.4f), 0.45f, rr);
        Rebar(room, new Vector3(12.6f, 22.2f, -4.2f), new Vector3(-0.3f, -1f, 0.2f), 0.35f, rr);
        bag.Flush(room);
    }

    // ---------- 3호·2호 ----------

    // 303호 (L2 북쪽 반): 판자 틈으로만 들어오는 안전한 방. 벽에 기댄 시체, 탁자 위 쪽지와 촛불
    static void AptRm303(Transform tower)
    {
        float y = AptY(2);
        var room = Group("Room303", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(30301);
        AptRmScatter(bag["cracked"], rr, 0.45f, 4.25f, 0.35f, 4.4f, y, 5);
        AptRmScatter(bag["wood"], rr, 0.45f, 4.25f, 0.35f, 4.4f, y, 3);
        AptRmWindow(bag, rr, 1.25f, y, 5.25f, -1f);
        AptRmWindow(bag, rr, 3.4f, y, 5.25f, -1f);

        // 서쪽 벽에 기대 앉은 시체: 도시(PlaceDeadBodies)와 같은 콜라이더와 핏자국. 이 방은 내비메시에서 빠져 있어 굽기에 영향이 없다
        var go = new GameObject("DeadBody");
        go.transform.SetParent(room, false);
        go.transform.localPosition = new Vector3(0.45f, y, 2.6f);
        go.transform.localRotation = Quaternion.Euler(0, 90f, 0);
        var body = go.AddComponent<DeadBody>();
        body.variant = 0;
        body.poseSeed = 303;
        var col = go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.5f, 0.18f);
        col.radius = 0.4f;
        col.height = 1.1f;
        Stain("BloodFloor", go.transform, new Vector3(0.1f, 0.008f, 0.25f), 0.5f, 0.4f, M["bloodOld"], 1303);
        Stain("BloodWall", go.transform, new Vector3(-0.06f, 0.8f, -0.35f + 0.012f), 0.25f, 0.35f, M["bloodOld"], 1304, new Vector3(90, 0, 0));

        // 앞벽 앞 탁자 위의 쪽지와 촛불. 상판에만 콜라이더를 둔다 (쪽지만 허공에서 몸에 걸리지 않게)
        Box("Table", room, new Vector3(2.2f, y + 0.72f, 4.9f), new Vector3(1.1f, 0.04f, 0.5f), M["wood"], 1.2f);
        foreach (float lx in new[] { 1.72f, 2.68f })
            foreach (float lz in new[] { 4.7f, 5.1f })
                AptRmPart(bag["wood"], lx, y + 0.35f, lz, 0.05f, 0.7f, 0.05f);
        var note = Box("Note", room, new Vector3(2.2f, y + 0.75f, 4.9f), new Vector3(0.3f, 0.02f, 0.2f), M["cloth"], 1f, true, new Vector3(0, 12f, 0));
        var noteCol = note.GetComponent<BoxCollider>(); // 얇은 종이라 조준하기 쉽게 판정을 키운다
        noteCol.center = new Vector3(0, 0.04f, 0);
        noteCol.size = new Vector3(0.34f, 0.1f, 0.24f);
        var memo = note.AddComponent<InspectNote>();
        memo.prompt = "쪽지 - 읽어 본다";
        memo.sound = InspectNote.NoteSound.Paper;
        memo.inOrder = true;
        memo.lines = new[]
        {
            "'계단이 막혔으면 옆집으로. 벽을 뚫어 놨다.' 급히 쓴 글씨다.",
            "'불 피운 놈들은 감염자가 아니다. 사람이다. 사람이 더 무섭다.'",
        };
        Box("Candle", room, new Vector3(2.6f, y + 0.8f, 5.0f), new Vector3(0.05f, 0.12f, 0.05f), M["bulb"], 1f, false);
        var candle = PointLight("CandleLight", room, new Vector3(2.6f, y + 0.95f, 4.98f), new Color(1f, 0.75f, 0.45f), 1.2f, 3.5f);
        Flicker(candle, 0.2f, 6f, false);

        // 버티던 흔적: 이부자리, 빈 깡통 (탁자 위 둘, 바닥에 하나)
        AptRmBedroll(bag, 3.9f, y, 1.3f, 4f);
        KCylAt(bag["metal"], new Vector3(1.8f, y + 0.795f, 4.82f), 0.04f, 0.11f, Quaternion.identity);
        KCylAt(bag["metal"], new Vector3(1.92f, y + 0.795f, 4.98f), 0.04f, 0.11f, Quaternion.identity);
        KCylAt(bag["metal"], new Vector3(3.2f, y + 0.04f, 1.9f), 0.04f, 0.11f, Quaternion.Euler(90f, 40f, 0));
        bag.Flush(room);
    }

    // 403호 (L3): 칸막이 둘로 나뉜 세 방. 가운데 부엌의 불길(F1)이 두 문 사이 지름길을 막고, 북쪽 방 바닥에는 유리 조각(N1)이 깔려 있다
    static void AptRm403(Transform tower, Transform dyn)
    {
        float y = AptY(3);
        var room = Group("Room403", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(40301);

        // 칸막이 (문짝 없는 문: 남쪽 x 2.7~4.3, 북쪽 x 0.4~2.0)
        var south = Wall(room, "Partition", new Vector3(0.1f, y, -1.65f), 0f, 4.45f, 2.6f, 0.1f, M["wall"], new[] { new Op(3.4f, 1.6f, 0, 2.2f) });
        DoorCasing(south, 3.4f, 1.6f, 2.2f, 0.1f, M["wood"]);
        var north = Wall(room, "Partition", new Vector3(0.1f, y, 1.65f), 0f, 4.45f, 2.6f, 0.1f, M["wall"], new[] { new Op(1.1f, 1.6f, 0, 2.2f) });
        DoorCasing(north, 1.1f, 1.6f, 2.2f, 0.1f, M["wood"]);

        // 부엌 한가운데 불길 F1: 두 칸막이 문을 잇는 직선 위에 있다 (쫓아오는 놈은 불을 가로지르고, 플레이어는 돌아간다)
        AptTFire(room, new Vector3(2.3f, y + 0.1f, 0f), 6f, 5f, true);
        AptRmHazard(tower, dyn, "Fire_F1", HazardZone.Kind.Fire, new Vector3(2.3f, y, 0f), new Vector3(0.7f, 1.0f, 0.7f));
        AptRmPart(bag["soot"], 2.3f, y + 0.1f, 0f, 1.0f, 0.2f, 0.7f, 15f);
        AptRmPart(bag["soot"], 2.55f, y + 0.07f, -0.15f, 0.6f, 0.14f, 0.9f, -30f, 0f, 4f);
        AptRmPart(bag["soot"], 1.95f, y + 0.05f, 0.3f, 0.5f, 0.1f, 0.3f, 50f);
        Stain("Scorch", room, new Vector3(2.3f, y + 0.012f, 0f), 1.25f, 1.15f, M["scorch"], 4751, Vector3.zero);
        Stain("Soot", room, new Vector3(2.3f, y + 2.588f, 0f), 1.3f, 1.2f, M["soot"], 4752, new Vector3(180f, 0, 0)); // 천장 그을음 (아래를 본다)
        // 서쪽 벽의 탄 조리대 (높이 0.9 이하). 북쪽 문 앞(z 0.9 위)과 남서 구석 아이템 자리는 비운다
        AptRmPart(bag["soot"], 0.375f, y + 0.425f, -0.1f, 0.55f, 0.85f, 0.8f);
        AptRmPart(bag["soot"], 0.4f, y + 0.87f, -0.1f, 0.6f, 0.04f, 0.84f);
        AptRmPart(bag["soot"], 0.375f, y + 0.25f, 0.6f, 0.55f, 0.5f, 0.6f);
        // 허리 높이의 조리대를 걸어서 통과하면 어색하다. 몸통 둘에는 판정을 둔다 (설계도 콜라이더 없는 꾸밈은 따로 밝혀 적는데 조리대는 그냥 상자다).
        // 불길 서쪽으로 돌아가는 길은 조리대(x 0.65)와 불길 구역(x 1.6) 사이로 0.95가 남는다
        ColliderBox("Counter", room, new Vector3(0.375f, y + 0.425f, -0.1f), new Vector3(0.55f, 0.85f, 0.8f), Vector3.zero);
        ColliderBox("Counter", room, new Vector3(0.375f, y + 0.25f, 0.6f), new Vector3(0.55f, 0.5f, 0.6f), Vector3.zero);
        Stain("Soot", room, new Vector3(0.112f, y + 1.55f, 0.1f), 0.85f, 1.0f, M["soot"], 4753, new Vector3(0, 0, -90f)); // 조리대 위 벽 그을음 (+x를 본다)
        AptRmScatter(bag["cracked"], rr, 0.9f, 4.25f, -1.3f, 1.3f, y, 3, new Rect(1.6f, -0.7f, 1.4f, 1.4f));

        // 남쪽 방: 칸막이에 기대 넘어진 옷장 (칸막이 문 앞은 비운다)
        AptRmPart(bag["wood"], 1.5f, y + 0.25f, -2.05f, 1.6f, 0.5f, 0.55f, 4f);
        AptRmScatter(bag["cracked"], rr, 0.45f, 4.25f, -4.95f, -2.0f, y, 4);
        AptRmScatter(bag["wood"], rr, 0.45f, 4.25f, -4.95f, -2.0f, y, 2);
        AptRmWindow(bag, rr, 1.25f, y, -5.25f, 1f);
        AptRmWindow(bag, rr, 3.4f, y, -5.25f, 1f);

        // 북쪽 방: 유리 조각 N1 (칸막이 문에서 옆집 벽 구멍으로 가는 길목). 깨진 유리문 틀이 바닥에 누워 있다
        AptRmGlass(room, rr, 0.3f, 3.1f, 1.9f, 5.1f, y, 30);
        AptRmHazard(tower, dyn, "Glass_N1", HazardZone.Kind.Glass, new Vector3(1.6f, y, 3.5f), new Vector3(1.5f, 1.0f, 1.6f));
        AptRmFrame(bag["frameDark"], 2.15f, y, 4.33f, 1.5f, 0.9f, 12f);
        AptRmScatter(bag["wood"], rr, 3.3f, 4.25f, 2.0f, 4.9f, y, 2);
        AptRmWindow(bag, rr, 1.25f, y, 5.25f, -1f);
        AptRmWindow(bag, rr, 3.4f, y, 5.25f, -1f);
        bag.Flush(room);
    }

    // 402호 (L3): 옆집 벽 구멍으로 넘어와 세 방을 지그재그로 지나 A 계단실로 나간다
    static void AptRm402(Transform tower)
    {
        float y = AptY(3);
        var room = Group("Room402", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(40201);

        // 칸막이 (북쪽 문 x -3.9~-2.3, 남쪽 문 x -1.9~-0.3)
        var north = Wall(room, "Partition", new Vector3(-4.55f, y, 1.65f), 0f, 4.45f, 2.6f, 0.1f, M["wall"], new[] { new Op(1.45f, 1.6f, 0, 2.2f) });
        DoorCasing(north, 1.45f, 1.6f, 2.2f, 0.1f, M["wood"]);
        var south = Wall(room, "Partition", new Vector3(-4.55f, y, -1.65f), 0f, 4.45f, 2.6f, 0.1f, M["wall"], new[] { new Op(3.45f, 1.6f, 0, 2.2f) });
        DoorCasing(south, 3.45f, 1.6f, 2.2f, 0.1f, M["wood"]);

        // 뚫은 벽 구멍(x 0, z 2.4~4.2) 가장자리: 깨져 나온 덩어리와 철근. 지나는 자리(구멍 안)는 비우고 철근은 머리 위에만 둔다
        AptRmPart(bag["cracked"], -0.35f, y + 0.1f, 4.45f, 0.4f, 0.2f, 0.3f, -15f, 0f, 5f);
        AptRmPart(bag["cracked"], -0.3f, y + 0.07f, 2.15f, 0.28f, 0.14f, 0.26f, 30f);
        AptRmPart(bag["cracked"], 0.32f, y + 0.09f, 2.18f, 0.35f, 0.18f, 0.3f, 20f, 0f, 6f);
        AptRmPart(bag["cracked"], 0.3f, y + 0.06f, 4.42f, 0.25f, 0.12f, 0.3f, -40f);
        Rebar(room, new Vector3(0.02f, y + 2.4f, 2.75f), new Vector3(0.4f, -1f, 0.2f), 0.3f, rr);
        Rebar(room, new Vector3(-0.03f, y + 2.4f, 3.85f), new Vector3(-0.3f, -1f, -0.1f), 0.35f, rr);
        Rebar(room, new Vector3(0f, y + 2.15f, 2.4f), new Vector3(0.2f, -0.5f, 1f), 0.22f, rr);

        // 북쪽 방: 앞벽 아래 주저앉은 소파
        AptRmPart(bag["blanket"], -2.0f, y + 0.21f, 4.88f, 1.8f, 0.42f, 0.6f);
        AptRmPart(bag["blanket"], -2.0f, y + 0.57f, 5.12f, 1.8f, 0.3f, 0.14f);
        AptRmScatter(bag["cracked"], rr, -4.25f, -0.45f, 2.0f, 4.4f, y, 4);
        AptRmScatter(bag["wood"], rr, -4.25f, -0.45f, 2.0f, 4.4f, y, 2);
        AptRmWindow(bag, rr, -1.25f, y, 5.25f, -1f);
        AptRmWindow(bag, rr, -3.4f, y, 5.25f, -1f);
        // 가운데 방: 낮은 탁자 (지킴이 자리와 두 문을 잇는 길에서 벗어난 서쪽 벽 앞)
        AptRmLowTable(bag, -3.85f, y, -0.3f, 0.6f, 1.1f, 3f);
        AptRmScatter(bag["cracked"], rr, -4.25f, -0.45f, -1.3f, 1.3f, y, 3);
        // 남쪽 방: 뒷벽 아래 매트리스
        AptRmMattress(bag, -2.2f, y, -4.72f, 90f);
        AptRmScatter(bag["cracked"], rr, -4.25f, -0.45f, -4.95f, -2.0f, y, 3);
        AptRmScatter(bag["wood"], rr, -4.25f, -0.45f, -4.95f, -2.0f, y, 2);
        AptRmWindow(bag, rr, -1.25f, y, -5.25f, 1f);
        AptRmWindow(bag, rr, -3.4f, y, -5.25f, 1f);
        bag.Flush(room);
    }

    // 6층 홀 (L5, 602호+603호): 약탈자 야영지. 남쪽 벽을 따라 가구 바리케이드 뒤로 앉아서 지나는 길이 있고, 그 밖 열린 바닥에는 유리 조각(N2)
    static void AptRmHall(Transform tower, Transform dyn)
    {
        float y = AptY(5);
        var hall = Group("Hall", tower, Vector3.zero);
        var bag = new AptRmParts();
        var rr = new System.Random(60201);

        // 바리케이드: 높이 1.4 (서면 머리가 보이고 앉으면 가려진다). x ±2.9 바깥 1.65m씩은 드나드는 틈이라 비운다
        AptTBox("Barricade", hall, -2.9f, -1.45f, y, y + 1.4f, -2.9f, -2.5f, M["wood"], 2f);
        AptTBox("Barricade", hall, -1.45f, 0f, y, y + 1.4f, -2.9f, -2.5f, M["wood"], 2f);
        AptTBox("Barricade", hall, 0f, 1.45f, y, y + 1.4f, -2.9f, -2.5f, M["wood"], 2f);
        AptTBox("Barricade", hall, 1.45f, 2.9f, y, y + 1.4f, -2.9f, -2.5f, M["wood"], 2f);
        // 겉모습 (콜라이더는 위 상자 넷뿐): 홀 쪽은 옷장 문짝·철제 사물함·옆으로 세운 탁자 다리·기대 세운 매트리스, 통로 쪽은 덧댄 판자, 위에는 뒤집힌 의자
        const float zn = -2.5f + 0.012f, zs = -2.9f - 0.012f;
        AptRmPart(bag["frameDark"], -2.55f, y + 0.7f, zn, 0.62f, 1.25f, 0.024f);
        AptRmPart(bag["frameDark"], -1.8f, y + 0.7f, zn, 0.62f, 1.25f, 0.024f);
        AptRmPart(bag["steelGrey"], -0.725f, y + 0.7f, zn, 1.3f, 1.3f, 0.024f);
        AptRmPart(bag["tire"], -0.725f, y + 1.15f, zn + 0.013f, 0.9f, 0.03f, 0.004f);
        AptRmPart(bag["tire"], -0.725f, y + 1.05f, zn + 0.013f, 0.9f, 0.03f, 0.004f);
        foreach (float lx in new[] { 0.2f, 1.25f })
            foreach (float ly in new[] { 0.18f, 1.22f })
                AptRmPart(bag["wood"], lx, y + ly, -2.33f, 0.07f, 0.07f, 0.35f);
        AptRmPart(bag["blanket"], 2.17f, y + 0.68f, -2.42f, 1.3f, 1.25f, 0.14f, 0f, -6f);
        AptRmPart(bag["wood"], 0f, y + 0.45f, zs, 5.7f, 0.14f, 0.024f, 0f, 0f, 0.6f);
        AptRmPart(bag["wood"], 0.05f, y + 1.02f, zs, 5.7f, 0.14f, 0.024f, 0f, 0f, -0.5f);
        AptRmPart(bag["wood"], -0.8f, y + 1.425f, -2.7f, 0.42f, 0.05f, 0.4f, 20f);
        foreach (float lx in new[] { -0.17f, 0.17f })
            foreach (float lz in new[] { -0.16f, 0.16f })
            {
                var o = Quaternion.Euler(0, 20f, 0) * new Vector3(lx, 0, lz);
                AptRmPart(bag["wood"], -0.8f + o.x, y + 1.65f, -2.7f + o.z, 0.04f, 0.4f, 0.04f, 20f);
            }

        // 모닥불: 잘라 낸 드럼통 바닥에서 탄다. 불꽃 속을 걸어 지나가지 못하게(내비메시도 돌아가게) 판정은 불꽃 높이 0.9까지 세운다
        var pan = Prim(PrimitiveType.Cylinder, "Barrel", hall, new Vector3(2.6f, y + 0.11f, 2.4f), new Vector3(0.7f, 0.11f, 0.7f), M["metal"], null, true);
        var cap = pan.GetComponent<CapsuleCollider>();
        if (cap != null)
        {
            cap.height = 0.9f / 0.11f;
            cap.center = new Vector3(0, (0.45f - 0.11f) / 0.11f, 0);
        }
        AptTFire(hall, new Vector3(2.6f, y + 0.1f, 2.4f), 9f, 7f, true);
        Stain("Scorch", hall, new Vector3(2.6f, y + 0.012f, 2.4f), 0.9f, 0.9f, M["scorch"], 4761);
        Stain("Soot", hall, new Vector3(2.6f, y + 2.588f, 2.4f), 1.1f, 1.1f, M["soot"], 4762, new Vector3(180f, 0, 0));

        // 북동 구석 야영지: 이부자리 둘 (콜라이더 없음), 상자 둘. 아이템 자리(북쪽·동쪽 벽 앞)와 지킴이 자리는 비운다
        AptRmBedroll(bag, 2.75f, y, 3.75f, 86f);
        AptRmBedroll(bag, 0.75f, y, 2.9f, -8f);
        Box("Crate", hall, new Vector3(4.14f, y + 0.25f, 3.95f), new Vector3(0.7f, 0.5f, 0.7f), M["wood"], 1f, true, new Vector3(0, 8f, 0));
        Box("Crate", hall, new Vector3(1.4f, y + 0.25f, 4.83f), new Vector3(0.7f, 0.5f, 0.7f), M["wood"], 1f, true, new Vector3(0, -10f, 0));
        // 불가의 냄비와 빈 깡통
        KCylAt(bag["metal"], new Vector3(3.3f, y + 0.08f, 2.05f), 0.13f, 0.16f, Quaternion.identity);
        KCylAt(bag["metal"], new Vector3(1.75f, y + 0.055f, 2.75f), 0.04f, 0.11f, Quaternion.identity);
        KCylAt(bag["metal"], new Vector3(1.9f, y + 0.04f, 2.6f), 0.04f, 0.11f, Quaternion.Euler(90f, 70f, 0));

        // 유리 조각 N2: 앉아서 지나는 길 밖의 열린 바닥 (서쪽 절반)
        AptRmGlass(hall, rr, -4.2f, -1.0f, -2.2f, 1.4f, y, 34);
        AptRmHazard(tower, dyn, "Glass_N2", HazardZone.Kind.Glass, new Vector3(-2.6f, y, -0.4f), new Vector3(1.7f, 1.0f, 1.9f));
        AptRmFrame(bag["frameDark"], -2.4f, y, 0.3f, 1.6f, 0.9f, -20f);

        var fire = new Rect(2.1f, 1.9f, 1.0f, 1.0f);
        AptRmScatter(bag["cracked"], rr, -4.25f, 4.25f, -2.2f, 4.9f, y, 8, fire);
        AptRmScatter(bag["wood"], rr, -4.25f, 4.25f, -2.2f, 4.9f, y, 4, fire);
        foreach (float x in new[] { -3.4f, -1.25f, 1.25f, 3.4f })
        {
            AptRmWindow(bag, rr, x, y, 5.25f, -1f);
            AptRmWindow(bag, rr, x, y, -5.25f, 1f);
        }
        bag.Flush(hall);
    }

    // ---------- 층계참의 잠긴 문 ----------

    // 잠긴 현관문 (층계참마다 들어갈 수 없는 집): 살펴보면 짧은 글이 뜬다. 벽에서 6cm 나온 얇은 판이라 층계참 길을 막지 않는다
    static void AptRmSealedDoors(Transform tower)
    {
        var g = Group("SealedDoors", tower, Vector3.zero);
        var frames = new List<CombineInstance>();
        AptRmSealedDoor(g, frames, 4.75f, 1f, AptY(1), -3.7f);   // 203호: B 계단실 L1 남쪽 층계참의 서쪽 벽
        AptRmSealedDoor(g, frames, 8.25f, -1f, AptY(2), 3.7f);   // 304호: L2 북쪽 층계참의 동쪽 벽
        AptRmSealedDoor(g, frames, -8.25f, 1f, AptY(3), -3.7f);  // 401호: A 계단실 L3 남쪽
        AptRmSealedDoor(g, frames, -8.25f, 1f, AptY(4), 3.7f);   // 501호: L4 북쪽
        AptRmSealedDoor(g, frames, -4.75f, -1f, AptY(4), 3.7f);  // 502호
        AptRmSealedDoor(g, frames, -8.25f, 1f, AptY(5), -3.7f);  // 601호: L5 남쪽
        AptRmSealedDoor(g, frames, 4.75f, 1f, AptY(6), 3.7f);    // 703호: B 계단실 L6 북쪽
        AptRmSealedDoor(g, frames, 8.25f, -1f, AptY(6), 3.7f);   // 704호
        AptRmSealedDoor(g, frames, 4.75f, 1f, AptY(7), -3.7f);   // 803호: L7 남쪽
        CombineInto(g, "DoorFrames", frames, M["frameDark"]);
    }

    // face = 문이 붙는 벽면의 x, side = 층계참이 있는 쪽 (+1이면 벽면의 +x 쪽)
    static void AptRmSealedDoor(Transform g, List<CombineInstance> frames, float face, float side, float y, float z)
    {
        var door = Box("SealedDoor", g, new Vector3(face + side * 0.03f, y + 1.05f, z), new Vector3(0.06f, 2.1f, 1.0f), M["doorRust"], 1.2f);
        Box("Handle", door.transform, new Vector3(side * 0.05f, -0.05f, 0.36f), new Vector3(0.04f, 0.04f, 0.14f), M["steelGrey"], 1f, false);
        var note = door.AddComponent<InspectNote>();
        note.prompt = "현관문 - 열어 본다";
        note.lines = new[]
        {
            "잠겨 있다. 안에서 아무 소리도 나지 않는다.",
            "문틈에 테이프가 발려 있다. 누군가 안에서 버텼던 모양이다.",
            "손잡이가 돌아가지 않는다.",
            "문이 뒤틀려 열리지 않는다.",
        };
        // 문틀 (벽에서 2cm)
        AptRmPart(frames, face + side * 0.01f, y + 1.07f, z - 0.54f, 0.02f, 2.14f, 0.08f);
        AptRmPart(frames, face + side * 0.01f, y + 1.07f, z + 0.54f, 0.02f, 2.14f, 0.08f);
        AptRmPart(frames, face + side * 0.01f, y + 2.14f, z, 0.02f, 0.08f, 1.16f);
    }

    // ---------- 옥상 ----------

    // 옥상 싸움터: 통신 중계기함(보상), 깜빡이는 목표 불빛이 달린 기운 안테나, 둘을 잇는 전선, 잘린 면 꾸밈, 그을음, 큰 연기
    static void AptRmRoof(Transform tower, Transform dyn)
    {
        const float y = 22.4f;
        var g = Group("RoofDressing", tower, Vector3.zero);

        // 안테나 기둥 (북동 구석에서 바깥으로 기울었다)과 꼭대기 불빛. 마당에서도 보이는 목표다. 불빛은 LootCache가 깜빡이다 열리면 끈다
        var mast = new List<CombineInstance>();
        Vector3 foot = new Vector3(12.3f, y, 4.7f), top = new Vector3(12.6f, y + 4.5f, 4.9f);
        KRod(mast, foot, top, 0.05f);
        KPart(mast, new Vector3(0.3f, 0.04f, 0.3f), 1f, foot + Vector3.up * 0.02f, Quaternion.identity);
        KPart(mast, new Vector3(0.7f, 0.03f, 0.03f), 1f, Vector3.Lerp(foot, top, 0.86f), Quaternion.Euler(0, 30f, 4f));
        KPart(mast, new Vector3(0.5f, 0.03f, 0.03f), 1f, Vector3.Lerp(foot, top, 0.93f), Quaternion.Euler(0, 120f, -3f));
        CombineInto(g, "Mast", mast, M["steelGrey"]);
        var bulb = Box("StrobeBulb", g, new Vector3(12.6f, y + 4.6f, 4.9f), new Vector3(0.18f, 0.18f, 0.18f), M["bulb"], 1f, false, null, false);
        var strobe = PointLight("Strobe", g, new Vector3(12.6f, y + 4.7f, 4.9f), new Color(0.8f, 0.9f, 1f), 5f, 12f);

        // 전선: 기둥 밑에서 동쪽·남쪽 난간 밑을 따라 중계기함 옆구리로 (서쪽 길 x 8.45~10.2와 구덩이 안은 비운다)
        var cable = new List<CombineInstance>();
        var path = new[]
        {
            new Vector3(12.3f, y + 0.02f, 4.7f), new Vector3(12.62f, y + 0.02f, 4.3f), new Vector3(12.66f, y + 0.02f, 1.8f), new Vector3(12.68f, y + 0.02f, -2.7f),
            new Vector3(12.68f, y + 0.02f, -5.08f), new Vector3(10.0f, y + 0.02f, -5.1f), new Vector3(9.95f, y + 0.3f, -5.0f),
        };
        for (int i = 1; i < path.Length; i++) KRod(cable, path[i - 1], path[i], 0.02f);
        CombineInto(g, "Cable", cable, M["tire"]);

        // 통신 중계기함: 남쪽 난간에 등을 대고 북쪽(+z)을 본다. 뜯어 열면 문짝이 로컬 Y로 -100도 돌아 앞으로 열리고, 앞의 세 자리에 보상이 떨어진다
        var cab = Group("RelayCabinet", tower, new Vector3(9.5f, y, -5.0f));
        var body = Box("Body", cab, new Vector3(0, 0.75f, 0), new Vector3(0.9f, 1.5f, 0.5f), M["steelGrey"], 1.5f);
        Box("Cap", cab, new Vector3(0, 1.52f, 0.02f), new Vector3(0.96f, 0.04f, 0.54f), M["steelGrey"], 1f, false);
        var hinge = Group("Door", cab, new Vector3(-0.43f, 0.05f, 0.26f));
        Box("Leaf", hinge, new Vector3(0.43f, 0.7f, 0.015f), new Vector3(0.86f, 1.4f, 0.03f), M["metal"], 1.2f, false, null, false);
        Box("Handle", hinge, new Vector3(0.78f, 0.75f, 0.045f), new Vector3(0.03f, 0.16f, 0.03f), M["tire"], 1f, false, null, false);
        Box("Plate", hinge, new Vector3(0.43f, 1.12f, 0.033f), new Vector3(0.4f, 0.14f, 0.006f), M["blastStripe"], 1f, false, null, false);
        // 첫 자리(첫 보상의 반도체가 놓인다)는 설계(z 0.8)보다 0.6 앞에 둔다: 열린 문짝이 경첩에서 (x -0.58, z 1.11)까지 뻗어 설계 자리를 7cm 옆에서 가린다
        // (중계기함 앞에 서면 물건이 문짝에 묻혀 보이지 않는다). 옮긴 자리는 문짝 끝에서 0.29 앞이고 싸움터 서쪽 벽에서 0.45, 구덩이에서 1.7 떨어져 있다
        var drops = new[]
        {
            Group("Drop", cab, new Vector3(-0.6f, 0.05f, 1.4f)),
            Group("Drop", cab, new Vector3(0f, 0.05f, 1.0f)),
            Group("Drop", cab, new Vector3(0.6f, 0.05f, 0.8f)),
        };
        var cache = body.AddComponent<LootCache>();
        cache.destinationId = Destinations.Apt102Id;
        cache.dropPoints = drops;
        cache.door = hinge;
        cache.led = strobe;
        cache.ledBulb = bulb.GetComponent<MeshRenderer>();

        AptRmCutFace(tower);
        // 싸움터 바닥의 그을음. 구덩이(z -2.9 아래)를 덮지 않게 북쪽으로 조금 밀고 줄였다
        Stain("Scorch", g, new Vector3(10.6f, y + 0.012f, 0.7f), 2.0f, 3.3f, M["scorch"], 4702, Vector3.zero);
        BigSmoke(g, new Vector3(6.6f, 29.5f, -2f), 1.0f);

        var fall = AptRmHazard(tower, dyn, "Hint_Fall_Roof", HazardZone.Kind.Hint, new Vector3(10.0f, y, -3.9f), new Vector3(1.0f, 1.2f, 1.0f));
        fall.hintBit = Tutorial.HintFall;
        fall.hintText = AptRmFallHint;
    }

    // 잘려 나간 면 (x = 6, 옥상 위 y 22.4~39.2): 층마다 삐져나온 바닥판 토막·휜 철근·매달린 벽 조각, 그을음.
    // KAptDamage(KoreaApartment.cs)의 같은 줄을 콜라이더 없이 옮겼다 (옥상 싸움터와 층계참 머리 위라 판정이 생기면 안 된다)
    static void AptRmCutFace(Transform tower)
    {
        const float cutX = 6f, depth = 11f, endH = 22.4f, h = 39.2f;
        var dg = Group("CutFace", tower, Vector3.zero);
        var rr = new System.Random(4700);
        for (int f = 9; f <= 14; f++)
        {
            float y = f * AptFloorH + 0.12f;
            int pieces = 1 + rr.Next(3);
            for (int k = 0; k < pieces; k++)
            {
                float len = KF(rr, 0.5f, 2.4f), dz = KF(rr, 1.2f, depth * 0.5f), zc = KF(rr, -depth / 2f + dz / 2f, depth / 2f - dz / 2f);
                Box("BrokenSlab", dg, new Vector3(cutX + len / 2f, y, zc), new Vector3(len, 0.24f, dz), M["dirty"], 2f, false, new Vector3(0, 0, KF(rr, -9f, 3f)));
                for (int q = 0; q < 2; q++)
                    Rebar(dg, new Vector3(cutX + len, y + KF(rr, -0.08f, 0.08f), zc + KF(rr, -dz * 0.4f, dz * 0.4f)), new Vector3(1f, KF(rr, -0.6f, 0.3f), KF(rr, -0.4f, 0.4f)), KF(rr, 0.4f, 1.1f), rr);
            }
            if (rr.NextDouble() < 0.6)
            {
                float s = rr.NextDouble() < 0.5 ? 1f : -1f;
                Box("HangingWall", dg, new Vector3(cutX + KF(rr, 0.3f, 1.1f), y + KF(rr, 0.6f, 1.6f), s * (depth / 2f - 0.15f)), new Vector3(KF(rr, 0.6f, 1.6f), KF(rr, 0.8f, 1.8f), 0.28f), M["aptWall"], 2f, false, new Vector3(KF(rr, -8f, 8f), 0, KF(rr, -20f, 20f)));
            }
        }
        // 드러난 끝면의 그을음 둘 (둘이 같은 평면이 아니게 x를 달리한다)
        AptRmFaceStain(dg, cutX + 0.03f, endH + KF(rr, 3f, 8f), KF(rr, -3f, 3f), KF(rr, 3f, 6f), KF(rr, 2.5f, 4f), 4700, endH + 0.2f, h, depth / 2f);
        AptRmFaceStain(dg, cutX + 0.045f, h - KF(rr, 2f, 5f), KF(rr, -3f, 3f), KF(rr, 2.5f, 5f), KF(rr, 2f, 3.5f), 4701, endH + 0.2f, h, depth / 2f);
    }

    // 끝면(+x를 보는 면)의 그을음. 도시와 달리 이 건물은 속이 비어 있어, 얼룩이 면 밖(옥상 아래 계단실·건물 꼭대기 위·앞뒤 벽 너머)으로 나가지 않게 크기를 줄인다
    static void AptRmFaceStain(Transform parent, float x, float y, float z, float ry, float rz, int seed, float yMin, float yMax, float zHalf)
    {
        const float reach = 1.06f; // 얼룩 메시(Gore.Blob)의 가장 먼 가장자리 반지름
        ry = Mathf.Min(ry, (y - yMin) / reach, (yMax - y) / reach);
        rz = Mathf.Min(rz, (zHalf - Mathf.Abs(z)) / reach);
        Stain("Scorch", parent, new Vector3(x, y, z), ry, rz, M["soot"], seed, new Vector3(0, 0, -90f));
    }

    // ---------- 지킴이 자리, 아이템 자리 ----------

    // 지킴이 자리 (반드시 서고 적힌 쪽을 본다). tier 1은 목적지의 LateTierTurn 날부터 선다. Heavy는 천장이 없는 옥상에만 있다
    static void AptRmGuards(Transform tower, Transform points)
    {
        AptRmGuard(tower, points, "G1", SpawnKind.Normal, 0, new Vector3(10.2f, 2.8f, -3.4f), 180f);
        AptRmGuard(tower, points, "G2", SpawnKind.Normal, 0, new Vector3(6.5f, 5.6f, 3.9f), 180f);
        AptRmGuard(tower, points, "G3a", SpawnKind.Normal, 0, new Vector3(2.6f, 8.4f, -3.6f), 90f);
        AptRmGuard(tower, points, "G3b", SpawnKind.Leather, 0, new Vector3(-2.3f, 8.4f, 0f), 0f);
        AptRmGuard(tower, points, "G5a", SpawnKind.Raider, 0, new Vector3(1.6f, 14.0f, 3.6f), 110f);
        AptRmGuard(tower, points, "G5b", SpawnKind.Raider, 0, new Vector3(3.8f, 14.0f, 1.2f), 300f);
        AptRmGuard(tower, points, "G6", SpawnKind.Leather, 0, new Vector3(6.5f, 16.8f, 3.9f), 180f);
        AptRmGuard(tower, points, "G7", SpawnKind.Normal, 0, new Vector3(9.4f, 19.6f, -3.6f), 270f);
        AptRmGuard(tower, points, "G8", SpawnKind.Heavy, 0, new Vector3(9.4f, 22.4f, -1.8f), 0f);
        AptRmGuard(tower, points, "G9", SpawnKind.Ranged, 0, new Vector3(11.4f, 22.4f, -2.0f), 0f);
        AptRmGuard(tower, points, "G4", SpawnKind.Leather, 1, new Vector3(-6.5f, 11.2f, 3.9f), 180f);
        AptRmGuard(tower, points, "G5c", SpawnKind.Ranged, 1, new Vector3(-3.2f, 14.0f, 4.0f), 150f);
        AptRmGuard(tower, points, "G8b", SpawnKind.Leather, 1, new Vector3(10.6f, 22.4f, 3.6f), 180f);
    }

    // 실내 아이템 자리 (AptRmSpots 표 그대로). 목록 자리는 chance 0이면 GameBalance.itemSpawnChance로 굴리고, 고정 자리는 반드시 놓인다
    static void AptRmItems(Transform tower, Transform points)
    {
        foreach (var s in AptRmSpots)
        {
            var p = Spawn<ItemSpawnPoint>(points, tower.TransformPoint(new Vector3(s.x, s.y, s.z)));
            p.gameObject.name = s.name;
            if (s.fixedId != null)
            {
                p.guaranteed = true;
                p.items = new[] { s.fixedId };
                p.amount = s.amount;
            }
            else
            {
                p.items = (string[])s.list.Clone();
                p.chance = s.chance;
            }
        }
    }
}
