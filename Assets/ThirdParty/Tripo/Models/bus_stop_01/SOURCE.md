# bus_stop_01 — 버스정류장

- 만든 곳: Tripo AI API (Holymolly Ltd), 2026-11-22, 맵 모델링 세션
- 만든 방법: 글 → 3D (text_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 3000, 텍스처 standard (텍스처 모델 v3.5-20260815)
  - 작업 번호: c14b7f84-084f-4930-a2a8-8ae991213e0e (생성, 30크레딧), dc3c245f-3438-4cd3-b910-237a9cc74c09 (FBX 변환, 5크레딧)
  - 글: "Abandoned Korean city bus stop shelter, game prop. Grey steel frame about 4 meters wide, slightly sloped flat roof, long metal bench inside, back wall frame with the glass panels completely smashed out and missing, empty frames. Weathered, rust, dirt, grime. Realistic, no text or advertisement. Single object, no ground."
  - 빼는 글: "logo, text, letters, advertisement poster, glass panels, sign, ground plane, base slab, people, bus"
- 파일
  - `bus_stop_01.fbx`: Tripo FBX 변환 결과 (삼각형 3700개, 텍스처가 안에 들어 있지만 재질은 가져오지 않는다)
  - `bus_stop_01_diff.jpg`: GLB에서 꺼낸 색 텍스처 원본 2048×2048. Unity에는 512로 들여온다 (Docs/텍스처_규칙.md 소품 규칙)
- 라이선스: Tripo API 약관(developers.tripo3d.ai/en/terms, 2025-07-11판) 5.2.2 — 유료 사용자는 만든 결과물의 권리를 가진다. 3.2 — 상업·비상업 용도로 쓸 수 있다. 무료 사용자는 5.2.1에 따라 Tripo가 권리를 가진다. 출처 표기 의무는 없다.
- 정한 재질 값 (2026-11-22 사용자 통과, 미리보기 Docs/맵모델링_미리보기/03_트럭_가로등_정류장_나무): URP Lit, _BaseMap = 이 폴더의 색 텍스처, _BaseColor (0.35, 0.35, 0.35), _Metallic 0.4, _Smoothness 0.35. 방향: 원본 FBX에 Y축 180° 더 돌려야 벤치 쪽(열린 쪽)이 길(+Z)을 본다. 폭 4.2 m에 맞춘다(배율 4.2). 뒤 유리는 불투명한 판이라 비치지 않는다. 코드의 표지판 기둥·광고판·쓰레기통·콜라이더는 그대로 둔다

## 매우 높음 판 (Q3, 2026-11-22 다시 만듦)

- 왜: 사용자 결정 (11-22) — 로우 폴리 계획을 빼고 모델 하나당 5만 폴리곤 이하. 품질 단계(낮음·중간·높음·매우 높음) 중 '매우 높음'에 쓸 판
- 만든 방법: 그림 → 3D (image_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 10000, 텍스처 standard. 그림: 처음 만든 버스정류장(작업 c14b7f84)의 Tripo 그림
  - 작업 번호: 57dca359-0128-4891-9781-6d3ecfa0cb90 (생성, 40크레딧), 98d7f36b-3013-4c0f-a613-f223702f5580 (FBX 변환, 5크레딧)
  - 앞선 실패: 면 제한 20000으로 먼저 만든 판(작업 d3c1fdac·0c3bb88e, 45크레딧)은 지붕이 없어 버렸다. 얇거나 속이 빈 부분은 면 수를 너무 올리면 날아간다
- 파일 (`Q3_매우높음/`)
  - `bus_stop_01_q3.fbx`: 삼각형 13,093개 (예전 판 3,700개). 재질은 가져오지 않는다
  - `bus_stop_01_q3_diff.jpg`: 색 텍스처 원본 2048. Unity에는 1024로 들여오고 HighRes 묶음(텍스처 품질 '매우 높음'에서만 원본 크기)
- 낮음·중간·높음 판 (2026-11-23, 이 매우 높음 판에서 줄임, 크레딧 0): `Q0_낮음/bus_stop_01_q0.*` 삼각형 691 · `Q1_중간/bus_stop_01_q1.*` 1,375 · `Q2_높음/bus_stop_01_q2.*` 6,999 (각 `.asset` 메시 · `_diff.png` 텍스처 256/512/512 · `.prefab`). 도구 `Assets/Editor/GameSceneBuilder.TripoTiers.cs`·`TripoSimplify.cs`. 높음은 UV 그대로(원본 텍스처 이음매를 지키며 면만 줄임), 나머지는 모양만 줄인 뒤 새로 펼쳐 이 판의 텍스처를 구워 옮김. 사진 Docs/맵모델링_미리보기/09_Tripo5_품질4단계. `bus_stop_01_q3.prefab`은 FBX 메시를 같은 좌표(앞 +Z)로 돌려 둔 것. 아직 도시에서 쓰지 않는다
- 2026-11-23 사용자 통과 (미리보기 Docs/맵모델링_미리보기/07_Tripo_다시만들기_2만). 재질 값은 위 예전 판 값을 그대로 쓴다
- 권리 (11-23 사용자 확인): 모든 Tripo 작업은 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤에 했다 (API 크레딧은 구독과 별도 잔액이지만, 만든 때는 유료 구독 중). Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중이거나 유료 이용권으로 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 결제 영수증을 보관한다.
