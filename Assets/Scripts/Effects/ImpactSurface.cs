using UnityEngine;

// 근접 공격이 벽·물건·바닥에 맞았을 때 무엇을 때렸는지 가린다 (Vfx.Impact와 EffectAudio.Impact가 재질마다 다른 효과·소리를 낸다).
// 이 프로젝트에는 물리 재질·태그가 없어서, 맞은 콜라이더의 렌더러 재질 이름(M_...)을 먼저 보고, 렌더러가 없으면 오브젝트 이름을 본다.
// 재질 이름은 앞부분만 비교한다 (" (Instance)"나 Tripo 모델의 _q0~_q3 꼬리가 붙어도 된다). 모르는 것은 콘크리트로 친다 (도시·방공호 대부분이 콘크리트).
public static class ImpactSurface
{
    // 앞에 있는 것부터 맞춰 본다. 처음 맞는 것으로 정한다
    static readonly (string prefix, Vfx.ImpactKind kind)[] Materials =
    {
        // 유리 (창 유리, 굴뚝 들여다보는 창, 시계 유리, 애자)
        ("M_DarkWindow", Vfx.ImpactKind.Glass), ("M_ShopGlass", Vfx.ImpactKind.Glass), ("M_ChimneyGlass", Vfx.ImpactKind.Glass),
        ("M_ClockGlass", Vfx.ImpactKind.Glass), ("M_Insulator", Vfx.ImpactKind.Glass),
        // 천·모래주머니·고무·쓰레기봉투 (푹 꺼지는 것)
        ("M_Cloth", Vfx.ImpactKind.Soft), ("M_Blanket", Vfx.ImpactKind.Soft), ("M_Sandbag", Vfx.ImpactKind.Soft), ("M_TR_city_sandbag", Vfx.ImpactKind.Soft),
        ("M_TR_shelter_bed", Vfx.ImpactKind.Soft), ("M_Awning", Vfx.ImpactKind.Soft), ("M_Banner", Vfx.ImpactKind.Soft),
        ("M_PH_old_tyre", Vfx.ImpactKind.Soft), ("M_TR_city_mattress", Vfx.ImpactKind.Soft), ("M_PH_trashbag", Vfx.ImpactKind.Soft), ("M_PH_old_gas_mask", Vfx.ImpactKind.Soft),
        // 흙·풀·재 더미
        ("M_Earth", Vfx.ImpactKind.Dirt), ("M_GrassDry", Vfx.ImpactKind.Dirt), ("M_AshPile", Vfx.ImpactKind.Dirt), ("M_Vine", Vfx.ImpactKind.Dirt),
        // 나무 (상자, 판자, 탁자·의자, 죽은 나무)
        ("M_WoodPlanks", Vfx.ImpactKind.Wood), ("M_Bark", Vfx.ImpactKind.Wood), ("M_ClockWood", Vfx.ImpactKind.Wood), ("M_TR_dead_tree", Vfx.ImpactKind.Wood),
        ("M_TR_city_crate", Vfx.ImpactKind.Wood), ("M_PH_old_military_crate", Vfx.ImpactKind.Wood),
        ("M_TR_shelter_table", Vfx.ImpactKind.Wood), ("M_TR_shelter_chair", Vfx.ImpactKind.Wood),
        ("M_TR_city_counter", Vfx.ImpactKind.Wood), ("M_TR_city_stump", Vfx.ImpactKind.Wood),
        // 벽돌
        ("M_Brick", Vfx.ImpactKind.Brick), ("M_TR_city_rubble_brick", Vfx.ImpactKind.Brick),
        // 쇠 (칠한 것·녹슨 것 모두. 차, 드럼통, 간판, 철문, 셔터, 난간, 방공호 쇠가구·발전기·화로)
        ("M_TR_wrecked_sedan", Vfx.ImpactKind.Metal), ("M_TR_pickup_truck", Vfx.ImpactKind.Metal), ("M_TR_steel_drum", Vfx.ImpactKind.Metal),
        ("M_TR_lamp_post", Vfx.ImpactKind.Metal), ("M_TR_ac_unit", Vfx.ImpactKind.Metal), ("M_TR_bus_stop", Vfx.ImpactKind.Metal),
        ("M_TR_fire_hydrant", Vfx.ImpactKind.Metal), ("M_TR_city_bin", Vfx.ImpactKind.Metal), ("M_TR_city_bollard", Vfx.ImpactKind.Metal), ("M_TR_city_water_tank", Vfx.ImpactKind.Metal), ("M_TR_shelter_cabinet", Vfx.ImpactKind.Metal), ("M_TR_shelter_shelf", Vfx.ImpactKind.Metal),
        ("M_TR_shelter_furnace", Vfx.ImpactKind.Metal), ("M_TR_shelter_generator", Vfx.ImpactKind.Metal), ("M_TR_shelter_workbench", Vfx.ImpactKind.Metal),
        ("M_PH_metal", Vfx.ImpactKind.Metal), ("M_RustyMetal", Vfx.ImpactKind.Metal), ("M_Steel", Vfx.ImpactKind.Metal), ("M_DarkRustDoor", Vfx.ImpactKind.Metal),
        ("M_Rebar", Vfx.ImpactKind.Metal), ("M_Generator", Vfx.ImpactKind.Metal), ("M_StreetGrate", Vfx.ImpactKind.Metal), ("M_StreetManhole", Vfx.ImpactKind.Metal), ("M_StreetFence", Vfx.ImpactKind.Metal), ("M_TreeGrate", Vfx.ImpactKind.Metal),
        ("M_AptRail", Vfx.ImpactKind.Metal), ("M_AptGate", Vfx.ImpactKind.Metal), ("M_FrameDark", Vfx.ImpactKind.Metal), ("M_Shutter", Vfx.ImpactKind.Metal),
        ("M_LampHead", Vfx.ImpactKind.Metal), ("M_ClockBrass", Vfx.ImpactKind.Metal), ("M_PaintRed", Vfx.ImpactKind.Metal), ("M_PaintBlue", Vfx.ImpactKind.Metal),
        ("M_BlastDoor", Vfx.ImpactKind.Metal), ("M_ShelterCabinet", Vfx.ImpactKind.Metal), ("M_GoldPaint", Vfx.ImpactKind.Metal), ("M_Sign", Vfx.ImpactKind.Metal),
        ("M_SideSign", Vfx.ImpactKind.Metal), ("M_BusStopSign", Vfx.ImpactKind.Metal), ("M_AcUnit", Vfx.ImpactKind.Metal),
        ("M_Furnace", Vfx.ImpactKind.Metal), ("M_Radio", Vfx.ImpactKind.Metal), ("M_GaugeFace", Vfx.ImpactKind.Glass),
    };

    // 렌더러 없는 충돌 상자의 이름 (도시 생성기의 ColliderBox 등). 이름 앞부분을 비교한다
    static readonly (string prefix, Vfx.ImpactKind kind)[] Names =
    {
        ("Pole", Vfx.ImpactKind.Metal), ("Lamp", Vfx.ImpactKind.Metal), ("Hydrant", Vfx.ImpactKind.Metal), ("Fence", Vfx.ImpactKind.Metal),
        ("YardFence", Vfx.ImpactKind.Metal), ("Bollard", Vfx.ImpactKind.Metal), ("Sign", Vfx.ImpactKind.Metal), ("Rail", Vfx.ImpactKind.Metal),
        ("BarrierArm", Vfx.ImpactKind.Metal), ("BarrierPost", Vfx.ImpactKind.Metal), ("Cart", Vfx.ImpactKind.Metal), ("Wire", Vfx.ImpactKind.Metal),
        ("Generator", Vfx.ImpactKind.Metal), ("Furnace", Vfx.ImpactKind.Metal), ("Radio", Vfx.ImpactKind.Metal), ("Storage", Vfx.ImpactKind.Metal),
        ("Workbench", Vfx.ImpactKind.Metal), ("WindowGuard", Vfx.ImpactKind.Glass), ("ShardGuard", Vfx.ImpactKind.Glass),
        ("Crate", Vfx.ImpactKind.Wood), ("Top", Vfx.ImpactKind.Wood), ("Seat", Vfx.ImpactKind.Wood), ("Board", Vfx.ImpactKind.Wood),
        ("Pallet", Vfx.ImpactKind.Wood), ("Bench", Vfx.ImpactKind.Wood), ("Counter", Vfx.ImpactKind.Wood),
        ("Bed", Vfx.ImpactKind.Soft), ("Tire", Vfx.ImpactKind.Soft), ("Wheel", Vfx.ImpactKind.Soft), ("Sandbag", Vfx.ImpactKind.Soft), ("Dirt", Vfx.ImpactKind.Dirt), ("Soil", Vfx.ImpactKind.Dirt), ("Sand", Vfx.ImpactKind.Dirt),
        ("Mud", Vfx.ImpactKind.Dirt), ("Mound", Vfx.ImpactKind.Dirt),
    };

    // point·normal: 맞은 자리와 그 면의 바깥쪽 (웅덩이 바닥이면 물보라로 친다). triangleIndex: 메시 콜라이더면 맞은 삼각형 (RaycastHit.triangleIndex)
    public static Vfx.ImpactKind Classify(Collider c, Vector3 point, Vector3 normal, int triangleIndex = -1)
    {
        if (c == null) return Vfx.ImpactKind.Concrete;
        if (normal.y > 0.6f && PuddleFx.Inside(point)) return Vfx.ImpactKind.Water;
        if (c.GetComponentInParent<DeadBody>() != null) return Vfx.ImpactKind.Flesh;
        if (c.GetComponentInParent<CharacterFigure>() != null) return Vfx.ImpactKind.Soft; // 상인 같은 산 사람 (옷을 친 둔한 소리, 피는 없다)

        var r = RendererFor(c, point);
        if (r != null)
        {
            var m = MaterialAt(r, c, triangleIndex);
            if (m != null)
                foreach (var e in Materials)
                    if (m.name.StartsWith(e.prefix, System.StringComparison.Ordinal)) return e.kind;
        }
        string n = c.name;
        foreach (var e in Names)
            if (n.StartsWith(e.prefix, System.StringComparison.Ordinal)) return e.kind;
        return Vfx.ImpactKind.Concrete;
    }

    // 맞은 자리에 눈에 보이는 면이 있는가. 렌더러 없는 투명 충돌 상자(울타리 밖 벽, 그네 둘레 상자)를 치면 허공에 먼지·자국이 생기지 않게 효과를 건너뛴다
    public static bool Visible(Collider c, Vector3 point) => c != null && RendererFor(c, point) != null;

    static readonly System.Collections.Generic.List<Renderer> found = new System.Collections.Generic.List<Renderer>();

    // 콜라이더와 같은 오브젝트의 렌더러 → 자식 중 맞은 자리에 가까운 것 (Tripo 모델은 콜라이더 아래 Tripo_*/Model/LOD0에 있다)
    // → 부모 아래(형제와 그 자식) 중 맞은 자리를 감싸는 것 (크기를 바꾼 원통 옆의 Tripo 드럼통, SandbagFit 아래 모래주머니, 충돌 상자 옆의 기둥)
    static Renderer RendererFor(Collider c, Vector3 point)
    {
        var own = c.GetComponent<Renderer>();
        if (Usable(own)) return own;
        Renderer best = null;
        float bestD = float.MaxValue;
        c.GetComponentsInChildren(found);
        foreach (var r in found)
        {
            if (!Usable(r)) continue;
            float d = r.bounds.SqrDistance(point);
            if (d < bestD) { bestD = d; best = r; }
        }
        if (best != null && bestD < 0.04f) return best;
        best = null;
        var parent = c.transform.parent;
        if (parent == null) return null;
        // 부모가 씬 전체를 묶는 큰 묶음이면 훑지 않는다 (한 번에 수천 개를 보게 된다)
        if (parent.childCount > 200) return null;
        parent.GetComponentsInChildren(found);
        float bestVol = float.MaxValue;
        foreach (var r in found)
        {
            if (!Usable(r)) continue;
            var b = r.bounds;
            b.Expand(0.1f);
            if (!b.Contains(point)) continue;
            // 여럿이 감싸면 가장 작은 것 (건물 전체보다 그 앞의 물건)
            float vol = b.size.x * b.size.y * b.size.z;
            if (vol < bestVol) { bestVol = vol; best = r; }
        }
        return best;
    }

    static bool Usable(Renderer r) => r != null && r.enabled && !(r is ParticleSystemRenderer) && r.sharedMaterial != null;

    // 재질이 여러 개면 메시 콜라이더가 알려 주는 삼각형의 부분 메시 재질을 쓴다 (상자 콜라이더는 첫 재질)
    static Material MaterialAt(Renderer r, Collider c, int triangleIndex)
    {
        var mats = r.sharedMaterials;
        if (mats.Length <= 1 || triangleIndex < 0) return r.sharedMaterial;
        var filter = r.GetComponent<MeshFilter>();
        if (c is MeshCollider mc && mc.sharedMesh != null && filter != null && filter.sharedMesh == mc.sharedMesh)
        {
            // 삼각형 번호는 부분 메시를 차례로 이어 센 것이다
            var mesh = mc.sharedMesh;
            int index = triangleIndex * 3, start = 0;
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                int count = (int)mesh.GetIndexCount(s);
                if (index >= start && index < start + count) return mats[s] != null ? mats[s] : r.sharedMaterial;
                start += count;
            }
        }
        return r.sharedMaterial;
    }
}
