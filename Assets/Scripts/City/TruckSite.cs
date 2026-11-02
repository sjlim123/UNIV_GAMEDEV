using UnityEngine;

// 도시의 트럭 정비소: 트럭 재료를 넣으면 그 저장 슬롯에 계속 남는다 (방공호로 돌아가 저장될 때 함께 저장된다).
// 다 모이면 트럭을 타고 탈출한다(엔딩). 넣은 재료는 트럭 위에 보인다 (바퀴·엔진·모터와 배선·네비게이션·연료통).
// 창은 UIManager.Truck.cs, 모양은 GameSceneBuilder.City.cs(BuildTruckYard)가 만든다
public class TruckSite : Interactable
{
    // 재료 하나에 딸린 모양들. 넣은 개수에 맞춰 앞에서부터 켠다
    [System.Serializable]
    public class PartPieces
    {
        public string id;
        public GameObject[] pieces;
    }

    public PartPieces[] parts = new PartPieces[0];
    public GameObject[] whenComplete = new GameObject[0]; // 다 고치면 켜진다 (전조등)
    public Transform soundPoint;                          // 넣는 소리가 나는 곳 (엔진 칸)

    // 도시 시작 안내(5초)가 지나간 뒤, 다른 안내가 없을 때 한 번 알려 준다
    const float HintDelay = 6.5f, HintGiveUp = 40f;
    bool hintPending;
    float hintClock;

    public override string Prompt => GameManager.Instance.State.TruckComplete ? "트럭 - 탈출하기" : "트럭 정비소 - 재료 넣기";

    public override void Interact(PlayerInteractor player) => GameManager.Instance.UI.OpenTruck(this);

    void Start()
    {
        Refresh();
        hintPending = GameManager.Instance.State.Truck.IsEmpty;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        // 창이 열려 있는 동안 뒤 배경 블러·알림을 갱신한다
        if (gm.UI.IsTruckOpen) gm.UI.UpdateTruck();
        if (hintPending) TickHint(gm);
    }

    // 넣은 개수만큼 트럭 위 모양을 켠다 (모양 수와 필요 개수가 달라도 비율대로, 하나라도 넣으면 보인다)
    public void Refresh()
    {
        var s = GameManager.Instance.State;
        foreach (var part in parts)
        {
            if (part == null || part.pieces == null) continue;
            int len = part.pieces.Length;
            int need = ItemDatabase.TruckParts.TryGetValue(part.id, out var n) ? n : len;
            int have = Mathf.Min(s.Truck.Count(part.id), need);
            int show = need <= 0 ? len : Mathf.CeilToInt(len * have / (float)need - 0.001f);
            for (int i = 0; i < len; i++)
                if (part.pieces[i] != null) part.pieces[i].SetActive(i < show);
        }
        bool complete = s.TruckComplete;
        foreach (var go in whenComplete)
            if (go != null) go.SetActive(complete);
    }

    // 소지품의 재료 하나를 필요한 만큼 넣는다. 넣은 개수
    public int Deliver(string id)
    {
        int n = GameManager.Instance.State.DeliverToTruck(id);
        if (n > 0) Delivered($"{UIManager.ColoredName(id)} x{n} 넣었다.");
        return n;
    }

    // 가진 트럭 재료를 모두 넣는다. 넣은 개수
    public int DeliverAll()
    {
        var s = GameManager.Instance.State;
        int total = 0, kinds = 0;
        string last = null;
        foreach (var kv in ItemDatabase.TruckParts)
        {
            int n = s.DeliverToTruck(kv.Key);
            if (n <= 0) continue;
            total += n;
            kinds++;
            last = $"{UIManager.ColoredName(kv.Key)} x{n}";
        }
        if (total > 0) Delivered(kinds == 1 ? last + " 넣었다." : $"트럭 재료 {kinds}종류 {total}개를 넣었다.");
        return total;
    }

    void Delivered(string message)
    {
        var gm = GameManager.Instance;
        Refresh();
        var at = soundPoint != null ? soundPoint.position : transform.position;
        ProceduralAudio.PlayAt(TruckAudio.Ratchet(), at, 0.85f, Random.Range(0.95f, 1.05f));
        Progress(gm.State, out int have, out int need);
        if (gm.State.TruckComplete)
            gm.UI.ShowMessage("트럭을 다 고쳤다! 이제 시동을 걸고 이 도시를 빠져나갈 수 있다.", 5f);
        else
            gm.UI.ShowMessage($"{message}  <color=#a8a294>(수리 {have} / {need})</color>", 3f);
    }

    // 전체 수리 진행: 넣은 부품 수 / 필요한 부품 수 (필요한 만큼만 센다)
    public static void Progress(GameState s, out int have, out int need)
    {
        have = need = 0;
        foreach (var kv in ItemDatabase.TruckParts)
        {
            need += kv.Value;
            have += Mathf.Min(s.Truck.Count(kv.Key), kv.Value);
        }
    }

    void TickHint(GameManager gm)
    {
        hintClock += Time.deltaTime;
        if (hintClock > HintGiveUp || !gm.State.Truck.IsEmpty) { hintPending = false; return; }
        if (hintClock < HintDelay || gm.IsGameOver || gm.IsTransitioning || gm.UI.IsBlocking || gm.UI.IsShowingMessage) return;
        hintPending = false;
        gm.UI.ShowMessage(HintText(), 6f);
    }

    // 예: 해치 근처에 트럭 정비소가 있다 (오른쪽 앞 20m). 부품을 모아 고치면 탈출할 수 있다.
    string HintText()
    {
        string where = "";
        var p = PlayerController.Instance;
        if (p != null)
        {
            var to = transform.position - p.transform.position;
            to.y = 0;
            var fwd = p.transform.forward;
            fwd.y = 0;
            if (to.magnitude > 3f && fwd.sqrMagnitude > 0.001f)
                where = $" ({Direction(fwd, to)} {Mathf.RoundToInt(to.magnitude)}m)";
        }
        return $"해치 근처에 트럭 정비소가 있다{where}. 부품을 모아 고치면 탈출할 수 있다.";
    }

    // 바라보는 방향 기준 8방향 (오른쪽이 +)
    static string Direction(Vector3 forward, Vector3 to)
    {
        switch (Mathf.RoundToInt(Vector3.SignedAngle(forward, to, Vector3.up) / 45f))
        {
            case 0: return "앞쪽";
            case 1: return "오른쪽 앞";
            case 2: return "오른쪽";
            case 3: return "오른쪽 뒤";
            case -1: return "왼쪽 앞";
            case -2: return "왼쪽";
            case -3: return "왼쪽 뒤";
            default: return "뒤쪽";
        }
    }
}
