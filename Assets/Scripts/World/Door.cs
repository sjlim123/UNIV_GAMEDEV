using System.Collections.Generic;
using UnityEngine;

// 경첩 문. 이 오브젝트가 경첩 위치이고, 자식에 문짝이 있다. 닫힌 문은 좀비 길을 막는다.
// 열고 닫을 때 문틀 위에서 문 종류(나무·철·황금)에 맞는 부스러기가 떨어진다 (Vfx.DoorDebris)
public class Door : Interactable
{
    public static readonly List<Door> All = new List<Door>();

    public float openAngle = 100f;
    public float speed = 4f;

    Quaternion closedRotation;
    float angle, targetAngle;
    Transform leaf;
    Vfx.DoorKind kind = Vfx.DoorKind.Wood;
    float leafWidth = 1.3f, leafHeight = 2.25f;

    public bool IsOpen => Mathf.Abs(targetAngle) > 1f;

    public override string Prompt => IsOpen ? "문 닫기" : "문 열기";

    void Awake()
    {
        closedRotation = transform.localRotation;
        leaf = transform.Find("Leaf");
        if (leaf != null)
        {
            leafWidth = leaf.localPosition.x * 2f;
            leafHeight = leaf.localPosition.y * 2f;
            var r = leaf.GetComponent<Renderer>();
            string mat = r != null && r.sharedMaterial != null ? r.sharedMaterial.name : "";
            kind = mat.Contains("Gold") ? Vfx.DoorKind.Gold : mat.Contains("Wood") ? Vfx.DoorKind.Wood : Vfx.DoorKind.Steel;
        }
    }

    // 문틀 위 모서리에서 부스러기가 떨어진다
    void Debris(float strength)
    {
        var a = transform.TransformPoint(new Vector3(0, leafHeight, 0));
        var b = transform.TransformPoint(new Vector3(leafWidth, leafHeight, 0));
        Vfx.DoorDebris(kind, a, b, transform.forward, strength);
    }
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public override void Interact(PlayerInteractor player)
    {
        if (IsOpen) targetAngle = 0;
        else OpenAwayFrom(player.transform.position);
        ProceduralAudio.PlayAt(ProceduralAudio.Creak(), transform.position, 0.6f, Random.Range(0.85f, 1.15f));
        PlayerController.Instance.MakeNoise(0.6f);
        Debris(1f);
    }

    // 문짝은 +X 방향으로 뻗어 있다. 상대가 있는 쪽의 반대로 연다
    public void OpenAwayFrom(Vector3 position)
    {
        float side = Vector3.Dot(position - transform.position, transform.parent != null ? transform.parent.forward : Vector3.forward);
        targetAngle = side > 0 ? openAngle : -openAngle;
    }

    // 좀비가 문을 부수고 들어올 때
    public void ForceOpen(Vector3 from)
    {
        OpenAwayFrom(from);
        speed = 8f;
        ProceduralAudio.PlayAt(ProceduralAudio.Bang(), transform.position, 1f, 0.8f);
        Debris(2.2f);
    }

    void Update()
    {
        angle = Mathf.Lerp(angle, targetAngle, Time.deltaTime * speed);
        transform.localRotation = closedRotation * Quaternion.Euler(0, angle, 0);
    }
}
