# dead_tree_01 — 불탄 가로수

- 만든 곳: Tripo AI API (Holymolly Ltd), 2026-11-22, 맵 모델링 세션
- 만든 방법: 글 → 3D (text_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 3000, 텍스처 standard (텍스처 모델 v3.5-20260815)
  - 작업 번호: 7f16f36d-61bf-4d81-b547-18646528cdb1 (생성, 30크레딧), 12f13270-add5-4f5f-ad86-ae8b0a7a258d (FBX 변환, 5크레딧)
  - 글: "Dead burnt street tree, game prop. Charred bare ginkgo tree about 5 meters tall, straight blackened trunk with peeling scorched bark, a few broken thick branches and thin bare twigs, no leaves at all. Realistic. Single object, no ground."
  - 빼는 글: "leaves, foliage, grass, ground plane, pot, base, roots spreading on ground, fire, people"
- 파일
  - `dead_tree_01.fbx`: Tripo FBX 변환 결과 (삼각형 5098개, 텍스처가 안에 들어 있지만 재질은 가져오지 않는다)
  - `dead_tree_01_diff.jpg`: GLB에서 꺼낸 색 텍스처 원본 2048×2048. Unity에는 512로 들여온다 (Docs/텍스처_규칙.md 소품 규칙)
- 라이선스: Tripo API 약관(developers.tripo3d.ai/en/terms, 2025-07-11판) 5.2.2 — 유료 사용자는 만든 결과물의 권리를 가진다. 3.2 — 상업·비상업 용도로 쓸 수 있다. 무료 사용자는 5.2.1에 따라 Tripo가 권리를 가진다. 출처 표기 의무는 없다.
- 정한 재질 값 (2026-11-22 사용자 통과, 미리보기 Docs/맵모델링_미리보기/03_트럭_가로등_정류장_나무): URP Lit, _BaseMap = 이 폴더의 색 텍스처, _BaseColor (0.5, 0.5, 0.5), _Metallic 0, _Smoothness 0.1 (사진으로 본 값. 손전등 아래에서 아직 회색이라 0.25가 더 맞을 수 있다 — 넣을 때 다시 사진으로 확인). 방향 0°. 높이를 코드 나무 높이에 맞춘다(사진은 6.0 m, 배율 6.0, 가지 폭 약 6 m). 코드의 격자 덮개·흙·캡슐 콜라이더는 그대로 둔다
- 2026-11-23 도시 넣기: 기본색 0.5 → 0.3 (도시에서 가까이 손전등을 받으면 밝은 회색이라, 사용자 "둘 다 손보기"). 품질 4단계 LOD로 가로수 99그루에 들어감
