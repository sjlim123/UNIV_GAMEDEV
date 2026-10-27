using UnityEngine;

// 바닥에 떨어진 아이템. F로 줍는다.
public class PickupItem : Interactable
{
    public string itemId = "cloth";
    public int amount = 1;

    public override string Prompt => $"{ItemDatabase.Name(itemId)} x{amount} 줍기";

    public override void Interact(PlayerInteractor player)
    {
        var gm = GameManager.Instance;
        var s = gm.State;
        int take = amount;
        while (take > 0 && !s.CanCarry(itemId, take)) take--;
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

    public void BuildVisual()
    {
        ItemVisuals.Build(itemId, transform);
        // 줍기용 판정 상자 (조금 크게 잡아 찾기 쉽게)
        var col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, 0.2f, 0);
        col.size = new Vector3(0.7f, 0.5f, 0.7f);
    }
}
