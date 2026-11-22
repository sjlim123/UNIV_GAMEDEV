# steel_drum_01 — 녹슨 200리터 드럼통

- 만든 곳: Tripo AI API (Holymolly Ltd), 2026-11-22, 맵 모델링 세션
- 만든 방법: 글 → 3D (text_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 1500, 텍스처 standard (텍스처 모델 v3.5-20260815)
  - 작업 번호: 094e3fe9-85c9-4359-8d2b-55f34b8db6d2 (생성, 30크레딧), 3dae0078-d832-4619-9c36-59d102c0f819 (FBX 변환, 5크레딧)
  - 글: "Old 200-liter steel oil drum standing upright, game prop. Faded dark blue paint, heavy rust, dents, ribbed rings around the body, closed lid with two bung caps, dirt and grime. Realistic, no text or labels. Single object, no ground."
  - 빼는 글: "logo, text, letters, label, sticker, ground plane, base, fire, people"
- 파일
  - `steel_drum_01.fbx`: Tripo FBX 변환 결과 (삼각형 2048개, 텍스처가 안에 들어 있지만 재질은 가져오지 않는다)
  - `steel_drum_01_diff.jpg`: GLB에서 꺼낸 색 텍스처 원본 2048×2048. Unity에는 512로 들여온다 (Docs/텍스처_규칙.md 소품 규칙)
- 라이선스: Tripo API 약관(developers.tripo3d.ai/en/terms, 2025-07-11판) 5.2.2 — 유료 사용자는 만든 결과물의 권리를 가진다. 3.2 — 상업·비상업 용도로 쓸 수 있다. 무료 사용자는 5.2.1에 따라 Tripo가 권리를 가진다. 출처 표기 의무는 없다.
- 정한 재질 값 (2026-11-22 사용자 통과, 미리보기 Docs/맵모델링_미리보기/02_드럼통_실외기_소화전): URP Lit, _BaseMap = 이 폴더의 색 텍스처, _BaseColor (0.6, 0.6, 0.6), _Metallic 0.3, _Smoothness 0.3. 방향: 원본 FBX에 Y축 0° 더 돌려야 코드 소품과 같은 쪽을 본다

## 매우 높음 판 (Q3, 2026-11-22 다시 만듦)

- 왜: 사용자 결정 (11-22) — 로우 폴리 계획을 빼고 모델 하나당 5만 폴리곤 이하. 품질 단계(낮음·중간·높음·매우 높음) 중 '매우 높음'에 쓸 판
- 만든 방법: 그림 → 3D (image_to_model), 모델 v3.1-20260211, 게임용 저폴리(smart_low_poly), 면 제한 10000, 텍스처 standard. 그림: 처음 만든 드럼통(작업 094e3fe9)의 Tripo 그림
  - 작업 번호: b4d97c3d-e28c-46bd-8815-432870d7346f (생성, 40크레딧), 88c14ee4-d170-4790-9d05-f57156ec6c41 (FBX 변환, 5크레딧)
  - 앞선 실패: 면 제한 20000으로 먼저 만든 판(작업 bf6a8c76·b5e9f51f, 45크레딧)은 윗부분이 잘려 뚜껑이 없어 버렸다. 얇거나 속이 빈 부분은 면 수를 너무 올리면 날아간다
- 파일 (`Q3_매우높음/`)
  - `steel_drum_01_q3.fbx`: 삼각형 14,860개 (예전 판 2,048개). 재질은 가져오지 않는다
  - `steel_drum_01_q3_diff.jpg`: 색 텍스처 원본 2048. Unity에는 1024로 들여오고 HighRes 묶음(텍스처 품질 '매우 높음'에서만 원본 크기)
- 낮음·중간·높음 판 (2026-11-23, 이 매우 높음 판에서 줄임, 크레딧 0): `Q0_낮음/steel_drum_01_q0.*` 삼각형 312 · `Q1_중간/steel_drum_01_q1.*` 1,249 · `Q2_높음/steel_drum_01_q2.*` 4,999 (각 `.asset` 메시 · `_diff.png` 텍스처 256/512/512 · `.prefab`). 도구 `Assets/Editor/GameSceneBuilder.TripoTiers.cs`·`TripoSimplify.cs`. 높음은 UV 그대로(원본 텍스처 이음매를 지키며 면만 줄임), 나머지는 모양만 줄인 뒤 새로 펼쳐 이 판의 텍스처를 구워 옮김. 사진 Docs/맵모델링_미리보기/09_Tripo5_품질4단계. `steel_drum_01_q3.prefab`은 FBX 메시를 같은 좌표(앞 +Z)로 돌려 둔 것. 아직 도시에서 쓰지 않는다
- 2026-11-23 사용자 통과 (미리보기 Docs/맵모델링_미리보기/07_Tripo_다시만들기_2만). 재질 값은 위 예전 판 값을 그대로 쓴다
- 권리 (11-23 사용자 확인): 모든 Tripo 작업은 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤에 했다 (API 크레딧은 구독과 별도 잔액이지만, 만든 때는 유료 구독 중). Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중이거나 유료 이용권으로 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 결제 영수증을 보관한다.
