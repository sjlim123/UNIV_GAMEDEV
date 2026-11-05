using UnityEngine;

// 생성기(GameSceneBuilder.BeginMeshes)가 만드는 메시·텍스처 묶음 파일(Assets/Generated/*Meshes.asset)의 메인 에셋.
// [PreferBinarySerialization]: 프로젝트가 텍스트 직렬화(Force Text)여도 이 파일은 서브에셋까지 바이너리로 저장된다.
// 정점 수십만 개짜리 메시를 YAML 16진수 문자열로 쓰면 원본의 두 배가 넘게 커져(도시 261MB) GitHub 100MB 제한에 걸리기 때문.
// 씬은 이 객체가 아니라 서브에셋 메시만 참조하므로, 빌드에는 메시만 들어간다.
[PreferBinarySerialization]
public class MeshHolder : ScriptableObject { }
