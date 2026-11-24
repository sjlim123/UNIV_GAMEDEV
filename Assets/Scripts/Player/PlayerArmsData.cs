using UnityEngine;

// 1인칭 팔 모델 자료 (2026-11-24, 플레이어 모델 업그레이드 2단계). 편집기 메뉴 '플레이어 모델: 1인칭 팔 만들기'가 만든다
// (GameSceneBuilder.PlayerArms). Tripo 플레이어 모델의 아래팔·장갑 손을 HandRig 뼈대(팔꿈치·손목·손가락 15마디)에 맞춰 묶은 스킨 메시와,
// HandRig가 같은 자리에 관절을 세우는 데 쓰는 치수. 없으면 HandRig는 예전처럼 코드로 지은 팔을 쓴다
public class PlayerArmsData : ScriptableObject
{
    [System.Serializable]
    public class Arm
    {
        public Mesh hi, lo;                                  // 뼈 순서: 0 팔꿈치, 1 손목, 2 + 손가락×3 + 마디 (0 엄지 … 4 새끼)
        public Vector3[] rootPos = new Vector3[5];           // 손가락 뿌리 마디 자리 (손목 좌표)
        public Quaternion[] rootRest = new Quaternion[5];    // 손가락을 곧게 편 자세의 뿌리 마디 방향 (HandRig 좌표 규칙)
        public float[] lengths = new float[15];              // 손가락 마디 길이
        public float[] radii = new float[5];                 // 손가락 굵기 (물건에 닿는지 잴 때)
        public float palm;                                   // 손목 줄기에서 손바닥 겉면까지 (손바닥 쪽, 손가락 뿌리 앞)
    }

    public Arm left = new Arm(), right = new Arm();
    public Material hiMat, loMat;
}
