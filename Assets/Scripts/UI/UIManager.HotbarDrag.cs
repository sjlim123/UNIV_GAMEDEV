using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 소지품 창 가운데 아래의 핫바 줄: 아이템 카드를 끌어다 1~8번 칸에 놓는다.
// 칸끼리 끌면 맞바꾸고, 칸의 아이템을 줄 밖으로 끌어 놓으면 핫바에서 뺀다 (소지품에는 그대로 있다)
public partial class UIManager
{
    const float InvBarSlot = 56, InvBarGap = 8, InvBarHeight = 104;

    readonly List<ItemSlotView> invBarSlots = new List<ItemSlotView>();
    readonly List<UnityEngine.UI.Text> invBarCounts = new List<UnityEngine.UI.Text>(), invBarKeys = new List<UnityEngine.UI.Text>();
    readonly List<Image> invBarGlows = new List<Image>();
    RectTransform dragGhost;
    ItemSlotView dragView;
    string dragId;
    int dragFromSlot = -1, dragHover = -1;

    public bool IsDraggingItem => dragId != null;

    void BuildInventoryHotbar(RectTransform glass, float gridX)
    {
        var head = NewText(glass, "핫바", 20, TextAnchor.MiddleLeft, Accent, FontSemiBold);
        Place(head.rectTransform, new Vector2(0, 0), new Vector2(gridX, 36 + InvBarSlot + 8), new Vector2(80, 28));
        var tip = NewText(glass, "아이템을 끌어다 칸에 놓기 · 칸 밖으로 끌면 빠짐", 15, TextAnchor.MiddleRight, Dim);
        Place(tip.rectTransform, new Vector2(0, 0), new Vector2(gridX + InvGridWidth - 420, 36 + InvBarSlot + 8), new Vector2(420, 28));
        for (int i = 0; i < GameState.HotbarSlots; i++)
        {
            var v = NewItemSlot(glass, "BarSlot" + i, 6);
            Place(v.Rect, new Vector2(0, 0), new Vector2(gridX + i * (InvBarSlot + InvBarGap), 36), new Vector2(InvBarSlot, InvBarSlot));
            v.Rect.pivot = Vector2.zero;
            v.Rect.anchoredPosition = new Vector2(gridX + i * (InvBarSlot + InvBarGap), 36);
            // 끌어 놓을 자리: 칸 전체가 마우스를 받는다
            v.Fill.raycastTarget = true;
            var glow = NewRect("DropGlow", v.Rect).gameObject.AddComponent<Image>();
            Stretch(glow.rectTransform, -4, -4);
            glow.sprite = UIStyle.Frame;
            glow.type = Image.Type.Sliced;
            glow.color = Accent;
            glow.raycastTarget = false;
            glow.enabled = false;
            invBarGlows.Add(glow);
            var key = NewText(v.Rect, (i + 1).ToString(), 12, TextAnchor.UpperLeft, new Color(1, 1, 1, 0.5f), FontSemiBold);
            Stretch(key.rectTransform, 5, 2);
            invBarKeys.Add(key);
            var count = NewText(v.Rect, "", 14, TextAnchor.LowerRight, Text, FontSemiBold);
            Stretch(count.rectTransform, 5, 2);
            count.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            invBarCounts.Add(count);
            var target = v.Rect.gameObject.AddComponent<InvDropTarget>();
            target.Slot = i;
            target.UI = this;
            var source = v.Rect.gameObject.AddComponent<InvDragSource>();
            source.Slot = i;
            source.UI = this;
            invBarSlots.Add(v);
        }
    }

    void RefreshInventoryHotbar(GameState s)
    {
        var hb = PlayerHotbar.Instance;
        int held = hb != null ? hb.SelectedIndex : -1;
        for (int i = 0; i < invBarSlots.Count; i++)
        {
            string id = s.Hotbar[i];
            SetItemSlot(invBarSlots[i], id);
            invBarSlots[i].Fill.raycastTarget = true;
            int n = id != null ? s.Carried.Count(id) : 0;
            invBarCounts[i].text = n > 1 ? n.ToString() : "";
            invBarKeys[i].color = i == held ? Accent : new Color(1, 1, 1, 0.5f);
            invBarSlots[i].Rect.GetComponent<InvDragSource>().Id = id;
        }
    }

    // ---------- 끌기 ----------

    public void BeginItemDrag(string id, int fromSlot, PointerEventData e)
    {
        if (id == null) return;
        dragId = id;
        dragFromSlot = fromSlot;
        if (dragGhost == null)
        {
            dragView = NewItemSlot(root, "DragGhost", 6);
            dragGhost = dragView.Rect;
            dragGhost.anchorMin = dragGhost.anchorMax = dragGhost.pivot = new Vector2(0.5f, 0.5f);
            dragGhost.sizeDelta = new Vector2(70, 70);
            var g = dragGhost.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.alpha = 0.9f;
        }
        SetItemSlot(dragView, id);
        foreach (var gr in dragGhost.GetComponentsInChildren<Graphic>()) gr.raycastTarget = false;
        dragGhost.SetAsLastSibling();
        dragGhost.gameObject.SetActive(true);
        dragGhost.localScale = Vector3.one * 1.15f;
        MoveDragGhost(e);
        ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.3f, 1.2f, SoundCategory.UI);
    }

    public void DragItem(PointerEventData e)
    {
        if (dragId == null) return;
        MoveDragGhost(e);
        dragGhost.localScale = Vector3.Lerp(dragGhost.localScale, Vector3.one, Time.unscaledDeltaTime * 12f);
    }

    void MoveDragGhost(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, e.position, null, out var local);
        dragGhost.localPosition = local;
    }

    public void HoverDropSlot(int slot, bool on)
    {
        if (dragId == null) return;
        dragHover = on ? slot : dragHover == slot ? -1 : dragHover;
        for (int i = 0; i < invBarGlows.Count; i++) invBarGlows[i].enabled = i == dragHover;
    }

    public void EndItemDrag(PointerEventData e)
    {
        if (dragId == null) return;
        var s = GameManager.Instance.State;
        // 놓은 자리: 핫바 칸이면 그 칸에, 핫바 칸에서 끌어 줄 밖에 놓으면 뺀다
        InvDropTarget target = null;
        var over = e.pointerCurrentRaycast.gameObject;
        if (over != null) target = over.GetComponentInParent<InvDropTarget>();
        if (target != null)
        {
            if (dragFromSlot >= 0) s.SwapHotbar(dragFromSlot, target.Slot);
            else s.SetHotbar(target.Slot, dragId);
            ProceduralAudio.Play2D(ProceduralAudio.Click(), 0.45f, 0.9f, SoundCategory.UI);
            invBarSlots[target.Slot].Rect.localScale = Vector3.one * 1.2f;
            ShowMessage($"{ColoredName(dragId)}{PlayerHotbar.Eul(ItemDatabase.Name(dragId))} 핫바 {target.Slot + 1}번 칸에 놓았다.", 1.6f);
        }
        else if (dragFromSlot >= 0)
        {
            s.Hotbar[dragFromSlot] = null;
            ProceduralAudio.Play2D(ProceduralAudio.Rustle(), 0.35f, 1f, SoundCategory.UI);
            ShowMessage($"{ColoredName(dragId)}{PlayerHotbar.Eul(ItemDatabase.Name(dragId))} 핫바에서 뺐다.", 1.6f);
        }
        dragId = null;
        dragFromSlot = -1;
        dragHover = -1;
        foreach (var g in invBarGlows) g.enabled = false;
        if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        RefreshInventory();
    }

    // 창이 닫히면 끌던 것을 취소한다
    void CancelItemDrag()
    {
        dragId = null;
        dragFromSlot = -1;
        dragHover = -1;
        foreach (var g in invBarGlows) g.enabled = false;
        if (dragGhost != null) dragGhost.gameObject.SetActive(false);
    }
}

// 끌 수 있는 아이템 (소지품 카드 또는 핫바 칸). Slot이 0 이상이면 핫바 칸
public class InvDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string Id;
    public int Slot = -1;
    public UIManager UI;

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && Id != null) UI.BeginItemDrag(Id, Slot, e);
    }

    public void OnDrag(PointerEventData e) => UI.DragItem(e);

    public void OnEndDrag(PointerEventData e) => UI.EndItemDrag(e);
}

// 아이템을 놓을 수 있는 핫바 칸 (끄는 동안 마우스를 올리면 금색 테두리)
public class InvDropTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public int Slot;
    public UIManager UI;

    public void OnPointerEnter(PointerEventData e) => UI.HoverDropSlot(Slot, true);

    public void OnPointerExit(PointerEventData e) => UI.HoverDropSlot(Slot, false);
}
