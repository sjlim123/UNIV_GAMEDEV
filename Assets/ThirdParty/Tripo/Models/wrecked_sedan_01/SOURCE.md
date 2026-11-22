# wrecked_sedan_01 — 버려진 소형 세단

- 만든 곳: Tripo AI API (Holymolly Ltd), 2026-11-21, 맵 모델링 세션
- 만든 방법: 글 → 3D (text_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 5000, 텍스처 standard (텍스처 모델 v3.5)
  - 작업 번호: 176e117c-7cbf-41c7-89e5-bf497aad9dca (생성, 30크레딧), f0f9c1d8-536c-411e-87cb-bbdb19b89f6f (FBX 변환, 5크레딧)
  - 글: "Abandoned compact four-door sedan car, game prop. Faded dusty grey-blue paint, rust patches, dented doors and crumpled hood, cracked and broken windows, flat tires, dirt and grime. Realistic proportions, generic design without any brand. Single object, no ground."
  - 빼는 글: "logo, brand badge, text, letters, license plate number, ground plane, base, people, fire"
- 파일
  - `wrecked_sedan_01.fbx`: Tripo FBX 변환 결과 (삼각형 6872개, 텍스처가 안에 들어 있지만 재질은 가져오지 않는다)
  - `wrecked_sedan_01_diff.jpg`: GLB에서 꺼낸 색 텍스처 원본 2048×2048. Unity에는 512로 들여온다 (Docs/텍스처_규칙.md 소품 규칙)
- 라이선스: Tripo API 약관(developers.tripo3d.ai/en/terms, 2025-07-11판) 5.2.2 — 유료 사용자는 만든 결과물의 권리를 가진다. 3.2 — 상업·비상업 용도로 쓸 수 있다. 무료 사용자는 5.2.1에 따라 Tripo가 권리를 가진다. 출처 표기 의무는 없다.
- 정한 재질 값 (2026-11-22 사용자 통과, 미리보기 Docs/맵모델링_미리보기/01_부서진차): URP Lit, _BaseMap = 이 폴더의 색 텍스처, _BaseColor (0.4, 0.4, 0.4), _Metallic 0.3, _Smoothness 0.3. 방향: 원본 FBX에 Y축 90° 더 돌려야 앞이 +Z (WreckedCarV2와 같은 쪽). 크기 배율 3.5 (1.79 × 1.60 × 3.48 m), 원점이 가운데라 바닥에 맞춰 올린다

## 매우 높음 판 (Q3, 2026-11-22 다시 만듦)

- 왜: 사용자 결정 (11-22) — 로우 폴리 계획을 빼고 모델 하나당 5만 폴리곤 이하. 품질 단계(낮음·중간·높음·매우 높음) 중 '매우 높음'에 쓸 판
- 만든 방법: 그림 → 3D (image_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 20000, 텍스처 standard. 그림: 처음 만든 세단(작업 176e117c)의 Tripo 그림
  - 작업 번호: f6128ec5-650a-4e8a-b996-cc114bff9f31 (생성, 40크레딧), f0f6bc82-bcb5-41cd-9c15-65113c2c6a62 (FBX 변환, 5크레딧)
- 파일 (`Q3_매우높음/`)
  - `wrecked_sedan_01_q3.fbx`: 삼각형 19,090개 (예전 판 6,872개). 재질은 가져오지 않는다
  - `wrecked_sedan_01_q3_diff.jpg`: 색 텍스처 원본 2048. Unity에는 1024로 들여오고 HighRes 묶음(텍스처 품질 '매우 높음'에서만 원본 크기)
- 낮음·중간·높음 판 (2026-11-23, 이 매우 높음 판에서 줄임, 크레딧 0): `Q0_낮음/wrecked_sedan_01_q0.*` 삼각형 636 · `Q1_중간/wrecked_sedan_01_q1.*` 2,617 · `Q2_높음/wrecked_sedan_01_q2.*` 9,999 (각 `.asset` 메시 · `_diff.png` 텍스처 256/512/512 · `.prefab`). 도구 `Assets/Editor/GameSceneBuilder.TripoTiers.cs`·`TripoSimplify.cs`. 높음은 UV 그대로(원본 텍스처 이음매를 지키며 면만 줄임), 나머지는 모양만 줄인 뒤 새로 펼쳐 이 판의 텍스처를 구워 옮김. 사진 Docs/맵모델링_미리보기/09_Tripo5_품질4단계. `wrecked_sedan_01_q3.prefab`은 FBX 메시를 같은 좌표(앞 +Z)로 돌려 둔 것. 아직 도시에서 쓰지 않는다
- 2026-11-23 사용자 통과 (미리보기 Docs/맵모델링_미리보기/07_Tripo_다시만들기_2만). 재질 값은 위 예전 판 값을 그대로 쓴다
- 권리 (11-23 사용자 확인): 모든 Tripo 작업은 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤에 했다 (API 크레딧은 구독과 별도 잔액이지만, 만든 때는 유료 구독 중). Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중이거나 유료 이용권으로 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 결제 영수증을 보관한다.
