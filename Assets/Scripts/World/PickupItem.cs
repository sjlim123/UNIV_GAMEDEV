using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 바닥에 떨어진 아이템. F로 줍는다.
// Spawn: 그 자리 바닥에 바로 놓는다 (도시 생성·들개 드랍). Throw: 손에서 떨어뜨리거나 던진다 — 잠시 물리(중력·튕김·굴러감)로 움직이다
// 멈추면 보통 아이템이 된다. 둥근 면이 있는 것(통조림·물병·배터리·돌·고기…)은 캡슐 콜라이더라 조금 굴러가고, 나머지는 상자라 툭 떨어져 눕는다
public class PickupItem : Interactable
{
    public string itemId = "cloth";
    public int amount = 1;

    // 둥글어서 굴러가는 것
    static readonly HashSet<string> Rolling = new HashSet<string> { "canned_food", "water", "battery", "flashlight", "stone", "raw_meat", "cooked_meat", "rope", "copper_wire" };
    // 떨어질 때 쇳소리가 나는 것
    static readonly HashSet<string> Metallic = new HashSet<string> { "metal", "iron_ingot", "alloy_plate", "screw", "hammer", "canned_food", "fuel", "wheel", "motor", "engine", "battery", "flashlight", "dagger" };

    Rigidbody body;
    bool landed;

    public override string Prompt => $"{ItemDatabase.Name(itemId)} x{amount} 줍기";

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        int take = amount;
        while (take > 0 && !s.CanCarry(itemId, take)) take--;
        if (take > 0) Tutorial.Notify("pickup");
        if (take == 0)
        {
            gm.UI.ShowMessage("너무 무겁다. 더는 들 수 없다.");
            return;
        }
        s.Carried.Add(itemId, take);
        amount -= take;
        gm.UI.ShowMessage($"{ItemDatabase.Name(itemId)} x{take} 획득");
        ProceduralAudio.PlayAt(ProceduralAudio.Rustle(), transform.position, 0.7f);
        PlayerController.Instance.MakeNoise(0.4f);
        if (amount <= 0) Destroy(gameObject);
    }

    public static PickupItem Spawn(string id, int amount, Vector3 position)
    {
        // 바닥에 떨어지도록 아래로 붙인다
        if (Physics.Raycast(position + Vector3.up * 0.2f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point;
        var go = new GameObject("Pickup_" + id);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
        var p = go.AddComponent<PickupItem>();
        p.itemId = id;
        p.amount = amount;
        p.BuildVisual();
        return p;
    }

    // 손에서 던진다: from 자리에서 velocity로 날아가 떨어지고(굴러가고) 멈추면 보통 아이템이 된다. ignore: 던진 사람의 콜라이더 (부딪히지 않게)
    public static PickupItem Throw(string id, int amount, Vector3 from, Vector3 velocity, Collider ignore)
    {
        var go = new GameObject("Pickup_" + id);
        go.transform.position = from; // 돌리기 전에 지어야 경계 상자를 아이템 좌표로 잴 수 있다
        var p = go.AddComponent<PickupItem>();
        p.itemId = id;
        p.amount = amount;
        p.BuildVisual();
        p.StartCoroutine(p.Fly(velocity, ignore));
        return p;
    }

    public void BuildVisual()
    {
        ItemVisuals.Build(itemId, transform);
        // 줍기용 판정 상자 (조금 크게 잡아 찾기 쉽게)
        var col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, 0.2f, 0);
        col.size = new Vector3(0.7f, 0.5f, 0.7f);
    }

    // 모양에 맞는 물리 콜라이더와 강체를 달아 날려 보내고, 멈추면 떼어 낸다
    IEnumerator Fly(Vector3 velocity, Collider ignore)
    {
        var b = VisualBounds();
        bool round = Rolling.Contains(itemId);
        Collider phys;
        var mat = new PhysicsMaterial("Pickup") { dynamicFriction = round ? 0.35f : 0.7f, staticFriction = round ? 0.35f : 0.7f, bounciness = round ? 0.15f : 0.05f };
        if (round)
        {
            // 가장 긴 축을 따라 눕는 캡슐: 그 축 둘레로 굴러간다
            int axis = b.extents.y >= b.extents.x && b.extents.y >= b.extents.z ? 1 : b.extents.x >= b.extents.z ? 0 : 2;
            var cap = gameObject.AddComponent<CapsuleCollider>();
            cap.center = b.center;
            cap.direction = axis;
            cap.height = Mathf.Max(0.04f, b.size[axis]);
            cap.radius = Mathf.Max(0.015f, Mathf.Min(axis == 0 ? b.extents.y : b.extents.x, axis == 2 ? b.extents.y : b.extents.z));
            phys = cap;
        }
        else
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = Vector3.Max(b.size, Vector3.one * 0.03f);
            phys = box;
        }
        phys.material = mat;
        if (ignore != null) Physics.IgnoreCollision(phys, ignore);
        transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0) * Quaternion.Euler(Random.Range(-8f, 8f), 0, Random.Range(-8f, 8f));

        body = gameObject.AddComponent<Rigidbody>();
        body.mass = Mathf.Clamp(ItemDatabase.Get(itemId).Weight * 2f, 0.3f, 5f);
        body.linearDamping = 0.15f;
        body.angularDamping = round ? 0.6f : 2.5f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = velocity;
        // 둥근 것은 날아가는 쪽으로 구르기 시작하고, 각진 것은 조금만 비틀린다
        body.angularVelocity = round ? Vector3.Cross(Vector3.up, velocity).normalized * 6f : Random.insideUnitSphere * 1.5f;

        float t = 0, still = 0;
        while (t < 8f)
        {
            yield return null;
            if (body == null) yield break;
            t += Time.deltaTime;
            bool quiet = body.linearVelocity.sqrMagnitude < 0.004f && body.angularVelocity.sqrMagnitude < 0.02f;
            still = quiet ? still + Time.deltaTime : 0f;
            if (t > 0.4f && (body.IsSleeping() || still > 0.35f)) break;
            if (transform.position.y < -40f) break; // 세상 밖으로 떨어졌다
        }
        Settle();
    }

    // 물리를 떼고 보통 아이템이 된다 (세상 밖으로 떨어졌으면 던진 자리 근처 바닥으로 되돌린다)
    void Settle()
    {
        if (body == null) return;
        Vector3 pos = transform.position;
        Destroy(body);
        body = null;
        foreach (var c in GetComponents<Collider>())
            if (!c.isTrigger) Destroy(c);
        if (pos.y < -40f)
        {
            transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var player = PlayerController.Instance;
            var at = player != null ? player.transform.position + player.transform.forward * 0.8f : Vector3.zero;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
            transform.position = at;
        }
    }

    void OnCollisionEnter(Collision c)
    {
        if (body == null || landed || c.relativeVelocity.magnitude < 1.2f) return;
        landed = true;
        float v = Mathf.Clamp01(c.relativeVelocity.magnitude / 5f);
        if (Metallic.Contains(itemId)) ProceduralAudio.PlayAt(ProceduralAudio.Clang(), transform.position, 0.12f + 0.2f * v, Random.Range(0.9f, 1.15f));
        else ProceduralAudio.PlayAt(ProceduralAudio.Land(), transform.position, 0.2f + 0.3f * v, Random.Range(0.85f, 1.05f));
        PlayerController.Instance?.MakeNoise(0.3f + 0.3f * v);
    }

    // 아이템 좌표의 경계 상자 (돌리기 전, 그림 부품들 기준)
    Bounds VisualBounds()
    {
        var b = new Bounds(Vector3.zero, Vector3.one * 0.05f);
        bool first = true;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var wb = r.bounds;
            var lb = new Bounds(wb.center - transform.position, wb.size);
            if (first) { b = lb; first = false; }
            else b.Encapsulate(lb);
        }
        return b;
    }
}
