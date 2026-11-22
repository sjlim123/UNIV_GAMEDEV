# pickup_truck_01 — 고장 난 픽업트럭

- 만든 곳: Tripo AI API (Holymolly Ltd), 2026-11-22, 맵 모델링 세션
- 만든 방법: 글 → 3D (text_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 5000, 텍스처 standard (텍스처 모델 v3.5-20260815)
  - 작업 번호: 488d2baf-1412-4790-9a30-778db4d78e0d (생성, 30크레딧), 1ccfaec3-9a13-46a8-bfd9-78c87cc1bd7f (FBX 변환, 5크레딧)
  - 글: "Abandoned old small pickup truck being repaired, game prop. Conventional cab with hood in front, hood propped wide open showing an empty engine bay, all four wheels removed with empty wheel arches, open cargo bed. Faded dark blue paint, heavy rust, dents, dirt. Realistic proportions, generic design without any brand. Single object, no ground."
  - 빼는 글: "logo, brand badge, text, letters, license plate number, wheels, tires, ground plane, wooden blocks, people, fire"
- 파일
  - `pickup_truck_01.fbx`: Tripo FBX 변환 결과 (삼각형 7511개, 텍스처가 안에 들어 있지만 재질은 가져오지 않는다)
  - `pickup_truck_01_diff.jpg`: GLB에서 꺼낸 색 텍스처 원본 2048×2048. Unity에는 512로 들여온다 (Docs/텍스처_규칙.md 소품 규칙)
- 라이선스: Tripo API 약관(developers.tripo3d.ai/en/terms, 2025-07-11판) 5.2.2 — 유료 사용자는 만든 결과물의 권리를 가진다. 3.2 — 상업·비상업 용도로 쓸 수 있다. 무료 사용자는 5.2.1에 따라 Tripo가 권리를 가진다. 출처 표기 의무는 없다.

## 다시 만든 판 — 바퀴·엔진 없는 수리용 트럭 (2026-11-23)

- 왜: 처음 판(위)은 바퀴와 엔진이 붙어 나와 고치는 목표물(TruckSite)로 쓸 수 없었다. 사용자 "가로등·트럭 먼저" → "네, 둘 다" → "트럭 1만·가로등 직접"
- 만든 방법
  1. 위 처음 판을 Unity에서 비스듬히 찍었다 (`Docs/맵모델링_미리보기/10_가로등_트럭/참고그림/트럭_예전Tripo.png`)
  2. Tripo 그림 편집 (seedream_v5, 작업 130e1dda-37b1-4bea-8a7a-7d0ac07e9702, 5크레딧): 같은 차·칠·각도로 바퀴 4개와 엔진을 지우고 보닛을 연 그림
  3. 그림 → 3D (v3.1-20260211, 게임용 저폴리, 면 제한 10000, 텍스처 standard, 작업 d31b76aa-90a7-4275-9d9f-058470246fc6, 40크레딧)
  4. FBX 변환 (작업 06d399c7-c84e-487d-b1be-e32141f2f6f4, 5크레딧)
  - 앞선 실패: 면 제한 15000 판(작업 33e81d48, FBX 5a4b61f1, 45크레딧)은 짐칸 옆벽이 사라졌다. 얇은 판은 면 1만이 안전
- 파일: `Q3_매우높음/pickup_truck_01_q3.fbx`(삼각형 12,970) · `_q3_diff.jpg`(2048 → 1024, HighRes) · `_q3.prefab`. 낮음·중간·높음 `Q0_낮음`·`Q1_중간`·`Q2_높음/pickup_truck_01_q{n}.*` 삼각형 1,192 · 3,255 · 7,000 (도구 GameSceneBuilder.TripoTiers.cs). 방향: yaw 90°로 앞이 +Z
- 2026-11-23 사용자 통과. 재질 값·도시 넣기는 아직 (사진 Docs/맵모델링_미리보기/10_가로등_트럭)
- 권리 (11-23 사용자 확인): 모든 Tripo 작업은 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤에 했다 (API 크레딧은 구독과 별도 잔액이지만, 만든 때는 유료 구독 중). Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중이거나 유료 이용권으로 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 결제 영수증을 보관한다.
