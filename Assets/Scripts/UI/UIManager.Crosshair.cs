using UnityEngine;
using UnityEngine.UI;

// 화면 가운데 조준점. 설정(조작 탭)에서 모양(점·십자·십자+점·원·원+점·없음)·색·크기를 고른다 (GameSettings.Crosshair*).
// 움직이거나 휘두르면 살짝 벌어졌다 모이고, 적을 맞히면 빨간 X(쓰러뜨리면 더 크고 진하게)가 번쩍인다
public partial class UIManager
{
    static readonly float[] CrosshairScales = { 0.8f, 1f, 1.35f };

    RectTransform crosshair, hitMarker;
    Image crossDot, crossRing;
    readonly Image[] crossArms = new Image[4];
    readonly Image[] hitBars = new Image[4];
    float crossLen, crossGap, crossSpread, crossKick, hitT = 1f;
    bool hitKill;

    void BuildCrosshair(Transform hud)
    {
        crosshair = NewRect("Crosshair", hud);
        Place(crosshair, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
        crossRing = CrosshairPart("Ring", UIStyle.Ring);
        for (int i = 0; i < 4; i++) crossArms[i] = CrosshairPart("Arm", null);
        crossDot = CrosshairPart("Dot", UIStyle.Dot);

        // 맞혔을 때의 X 표시 (조준점 둘레 대각선 네 막대)
        hitMarker = NewRect("HitMarker", hud);
        Place(hitMarker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
        for (int i = 0; i < 4; i++)
        {
            var rt = NewRect("Bar", hitMarker);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float a = 45 + i * 90;
            rt.localRotation = Quaternion.Euler(0, 0, a);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.5f);
            hitBars[i] = img;
        }
        hitMarker.gameObject.SetActive(false);
        ApplyCrosshair();
    }

    // 적을 맞혔다 (killed면 쓰러뜨림)
    public void ShowHitMarker(bool killed)
    {
        hitT = 0;
        hitKill = killed;
        if (hitMarker != null) hitMarker.gameObject.SetActive(true);
    }

    // 공격할 때 조준점이 잠깐 벌어진다
    public void KickCrosshair(float amount = 1f) => crossKick = Mathf.Max(crossKick, amount);

    // 매 프레임: 벌어짐(이동·공격)과 X 표시
    void UpdateCrosshair()
    {
        if (crosshair == null) return;
        float dt = Time.unscaledDeltaTime;
        var pc = PlayerController.Instance;
        float move = pc != null ? Mathf.Clamp01(pc.HorizontalSpeed / 4f) * (pc.IsCrouching ? 0.4f : 1f) + (pc.IsGrounded ? 0 : 0.8f) : 0;
        crossKick = Mathf.MoveTowards(crossKick, 0, dt * 5f);
        crossSpread = Mathf.Lerp(crossSpread, move * 4f + crossKick * 7f, dt * 14f);
        float k = CrosshairScales[Mathf.Clamp(GameSettings.CrosshairSize, 0, CrosshairScales.Length - 1)];
        var dirs = Dirs;
        for (int i = 0; i < 4; i++)
            if (crossArms[i].gameObject.activeSelf)
                crossArms[i].rectTransform.anchoredPosition = dirs[i] * (crossGap + crossSpread * k + crossLen / 2);
        if (crossRing.gameObject.activeSelf) crossRing.rectTransform.localScale = Vector3.one * (1 + crossSpread * 0.06f);

        if (hitT < 1f)
        {
            hitT = Mathf.Min(1f, hitT + dt / (hitKill ? 0.45f : 0.28f));
            float pop = 1 + (1 - hitT) * (hitKill ? 0.6f : 0.35f);
            float alpha = 1 - hitT * hitT;
            var c = hitKill ? new Color(1f, 0.12f, 0.08f, alpha) : new Color(1f, 0.32f, 0.25f, alpha * 0.95f);
            float len = (hitKill ? 15f : 11f) * k, thick = (hitKill ? 3.4f : 2.4f) * k, gap = (hitKill ? 11f : 9f) * k;
            for (int i = 0; i < 4; i++)
            {
                var rt = hitBars[i].rectTransform;
                rt.sizeDelta = new Vector2(thick, len);
                rt.anchoredPosition = (Vector2)(Quaternion.Euler(0, 0, 45 + i * 90) * Vector2.up) * (gap + len / 2) * pop;
                hitBars[i].color = c;
            }
            if (hitT >= 1f) hitMarker.gameObject.SetActive(false);
        }
    }

    static readonly Vector2[] Dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

    Image CrosshairPart(string name, Sprite sprite)
    {
        var rt = NewRect(name, crosshair);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        // 밝은 바닥 위에서도 보이게 검은 테두리를 두른다
        img.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.55f);
        return img;
    }

    // 설정이 바뀌면 다시 그린다 (설정 창이 부른다)
    public void ApplyCrosshair()
    {
        if (crosshair == null) return;
        int style = GameSettings.CrosshairStyle;
        float k = CrosshairScales[Mathf.Clamp(GameSettings.CrosshairSize, 0, CrosshairScales.Length - 1)];
        var color = GameSettings.CrosshairColors[Mathf.Clamp(GameSettings.CrosshairColor, 0, GameSettings.CrosshairColors.Length - 1)];
        color.a = 0.85f;

        bool dot = style == 0 || style == 2 || style == 4;
        bool cross = style == 1 || style == 2;
        bool ring = style == 3 || style == 4;

        crossDot.gameObject.SetActive(dot);
        crossDot.rectTransform.sizeDelta = Vector2.one * (style == 0 ? 6f : 4f) * k;
        crossDot.color = color;

        crossRing.gameObject.SetActive(ring);
        crossRing.rectTransform.sizeDelta = Vector2.one * 22f * k;
        crossRing.color = color;

        // 십자: 가운데를 비운 네 막대 (위·아래·왼쪽·오른쪽)
        float len = 8f * k, thick = 2f * k, gap = (style == 2 ? 5f : 3f) * k;
        crossLen = len;
        crossGap = gap;
        for (int i = 0; i < 4; i++)
        {
            var arm = crossArms[i];
            arm.gameObject.SetActive(cross);
            bool vertical = i < 2;
            arm.rectTransform.sizeDelta = vertical ? new Vector2(thick, len) : new Vector2(len, thick);
            arm.rectTransform.anchoredPosition = Dirs[i] * (gap + len / 2);
            arm.color = color;
        }
    }
}
