using System.Collections.Generic;
using UnityEngine;

// 102동 방 안 소품을 Tripo 모델(품질 4단계 LOD)로 (2026-11-26). 계획: Docs/맵모델링_미리보기/19_102동_소품/계획.md
// 방 꾸밈은 방마다 재질별 한 덩어리(Decor_<재질>)로 합쳐져 있어 '만든 뒤 겉만 바꾸기'가 안 된다. 그래서 방을 짓는 코드(GameSceneBuilder.AptTowerRooms.cs)가
// kTripoApt일 때 그 조각을 덩어리에 넣지 않고, 같은 자리·크기·방향의 모델을 방 그룹 아래에 따로 놓는다 (받침 이름 Tripo_<id>).
// 난수는 쓰지 않는다: 방 난수를 뽑는 순서가 그대로라 잔해·판자·유리 조각 자리가 바뀌지 않는다.
// 품질별 판이 없어 모델을 못 놓으면 false·null을 돌려주고, 부르는 쪽이 예전 상자를 그대로 넣는다.
// 콜라이더는 여기서 만들지 않는다. 소파·옷장의 새 콜라이더(사용자 결정)는 부르는 쪽이 예전 상자 크기로 단다
public static partial class GameSceneBuilder
{
    // 모델을 방 기준 상자(가운데 center, 방향 rot)에 축마다 맞춰 놓는다. size는 돌린 뒤의 축(모델의 x·y·z) 기준 크기다
    static Transform TaRmFit(Transform room, string id, Vector3 center, Vector3 size, Quaternion rot)
    {
        var holder = TcBoxFit(room, id, null, new Bounds(center, size), 0f, Vector3.one);
        if (holder != null) holder.localRotation = rot; // 모델 가운데가 받침 원점에 맞춰져 있어, 받침만 돌리면 상자째 돈다
        return holder;
    }

    // 매트리스·이부자리: 요와 베개를 합친 상자(0.9 × height × 1.9. 베개는 요 위에 얹혀 옆으로 나오지 않는다)에 매트리스 모델.
    // 모델 베개가 -Z 쪽이라 180° 돌려 코드 베개(+Z 머리맡)와 같은 끝에 둔다 (도시 TripoCityRound2와 같다). 이부자리는 높이가 낮아 납작하게 눌린다
    static bool TaRmMattress(Transform room, float x, float y, float z, float yaw, float height)
        => TaRmFit(room, "city_mattress_01", new Vector3(x, y + height / 2f, z), new Vector3(0.9f, height, 1.9f), Quaternion.Euler(0, yaw + 180f, 0)) != null;

    // 낮은 탁자: 상판과 다리를 합친 상자(w × 0.38 × d)에 낮은 밥상 모델. 모델은 긴 쪽이 X라, 탁자의 긴 쪽이 앞뒤(d)면 90° 돌려 놓는다.
    // 콜라이더는 없다 (지금처럼 지나간다, 사용자 결정)
    static bool TaRmLowTable(Transform room, float x, float y, float z, float w, float d, float yaw)
    {
        bool turn = d > w;
        return TaRmFit(room, "apt_low_table_01", new Vector3(x, y + 0.19f, z), turn ? new Vector3(d, 0.38f, w) : new Vector3(w, 0.38f, d), Quaternion.Euler(0, yaw + (turn ? 90f : 0f), 0)) != null;
    }

    // 넘어진 옷장·장: 예전 상자(가운데 c, 크기 size, yaw) 하나에 옷장 모델을 등으로 눕혀 맞춘다. 모델 키(Y)가 상자의 긴 쪽(x 또는 z), 문(+Z)이 위를 본다
    static bool TaRmWardrobeDown(Transform room, Vector3 c, Vector3 size, float yaw)
    {
        bool alongX = size.x >= size.z;
        // LookRotation(앞, 위): 모델 +Z(문) → 위, 모델 +Y(머리) → 상자의 긴 쪽. 남는 모델 X(폭)는 상자의 나머지 수평 쪽에 놓인다
        var lie = Quaternion.LookRotation(Vector3.up, alongX ? Vector3.right : Vector3.forward);
        var fit = alongX ? new Vector3(size.z, size.x, size.y) : new Vector3(size.x, size.z, size.y);
        return TaRmFit(room, "apt_wardrobe_01", c, fit, Quaternion.Euler(0, yaw, 0) * lie) != null;
    }

    // 벽이 터진 곳의 콘크리트 덩어리: 도시 잔해(TripoCityRound1)와 같은 규칙으로 모델을 고른다. key = 예전 재질.
    // 금 간 콘크리트 → 납작하면 판 조각 아니면 철근 콘크리트, 더러운 콘크리트 → 납작하면 판 조각 아니면 자리로 정해 칠한 벽 콘크리트나 철근 콘크리트.
    // 기울기(rot)는 그대로 두고, 앞뒤(z)로 길쭉하면 모델을 90° 돌려 긴 쪽(x)을 맞춘다
    static bool TaRmLump(Transform room, string key, Vector3 c, Vector3 size, Quaternion rot)
    {
        var w = room.TransformPoint(c);
        int h = Mathf.Abs(Mathf.RoundToInt(w.x * 7.31f) * 31 + Mathf.RoundToInt(w.z * 5.17f) * 17 + Mathf.RoundToInt(w.y * 3.3f)) % 2;
        float flat = size.y / Mathf.Max(1e-3f, Mathf.Min(size.x, size.z));
        string id;
        if (key == "cracked") id = flat < 0.35f ? "city_rubble_slab_01" : "city_rubble_concrete_01";
        else if (key == "dirty") id = flat < 0.3f ? "city_rubble_slab_01" : h == 0 ? "city_rubble_wall_01" : "city_rubble_concrete_01";
        else return false;
        bool turn = size.z > size.x * 1.3f;
        var holder = TaRmFit(room, id, c, turn ? new Vector3(size.z, size.y, size.x) : size, turn ? rot * Quaternion.Euler(0f, 90f, 0f) : rot);
        if (holder == null) return false;
        if (id == "city_rubble_concrete_01") TcFillRebarBody(holder); // 몸통이 예전 상자를 채우고 철근 가닥만 밖으로 나온다 (도시와 같다)
        return true;
    }

    // 303호 탁자: 상판 상자(top. 콜라이더는 그대로 둔다)의 자식으로 방공호 탁자 모델을 다리까지의 전체 크기(whole, top 기준)에 맞춰 놓고
    // 상판 겉모습만 지운다 (도시 2층 책상 TripoCityDeskChair와 같은 방법). 축마다 맞추므로 윗면 높이가 그대로라 쪽지·촛불·깡통이 그대로 얹힌다.
    // 받침을 콜라이더 물체의 자식으로 두는 까닭: 타격 효과가 콜라이더 아래 렌더러의 재질로 소리를 고른다
    static bool TaRmTable(Transform top, Bounds whole)
    {
        if (TcBoxFit(top, "shelter_table_01", null, whole, 0f, Vector3.one) == null) return false;
        TcStrip(new List<Renderer>(top.GetComponents<MeshRenderer>()));
        return true;
    }
}
