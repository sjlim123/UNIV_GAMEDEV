# city_rubble_concrete_01 — 철근 콘크리트 덩어리

- 만든 곳: Tripo AI 웹 스튜디오 (Holymolly Ltd), 2026-11-24, 맵 모델링 세션 (사용자 '1차: 재사용+상위 4종', 승인 440크레딧)
- 만든 방법: 글 → 그림 (Nano Banana Pro, 1:1, 2장, 구독 무료 그림 횟수에서 0크레딧. 그림 글: 깨진 회색 철근 콘크리트 덩어리, 휜 녹슨 철근 세 가닥, 핵폭발 뒤 한국 도시, 연회색 배경) → 고정밀 모델 H3.1 (텍스처 4K, 폴리곤 6만, 울트라 메시, 조명 제거, PBR, 삼각형) 55크레딧
  - 작업 번호: ec2ebb5e-7e7f-469c-9660-d14020e50283
  - 원본 삼각형 58,464개. 첫 생성 작업이 실패해 환불된 뒤 같은 그림으로 다시 만듦
- 받은 것: FBX(Blender 설정, 4K) zip에서 FBX와 색 텍스처(basecolor)만. 색 텍스처는 4096 → 2048로 줄여 `Q3_매우높음/city_rubble_concrete_01_q3_diff.jpg`. 노멀·금속성·거칠기 텍스처는 쓰지 않는다. 재질은 가져오지 않고 판마다 `M_TR_city_rubble_concrete_01_q0..q3` (URP Lit)
- 품질 4단계: 낮음 150 · 중간 600 · 높음 4,000 · 매우 높음 58,464 (텍스처 256 · 512 · 512 · 2048). 도구 `Assets/Editor/GameSceneBuilder.TripoTiers.cs` 메뉴 'Tripo 품질별 판 만들기 (도시 남은 소품 1차…)'
- 도시 넣기: `Assets/Editor/GameSceneBuilder.TripoCity2.cs` TripoCityRound1 — 도시 잔해 덩어리(금 간 콘크리트 중 두꺼운 것, 더러운 콘크리트 반). 도시를 다 만든 뒤 길 찾기 지도를 굽기 직전에 겉모습만 바꾼다 (콜라이더·자리·난수 그대로). 사진 Docs/맵모델링_미리보기/17_도시_남은소품
- 2026-11-24 사용자 통과 (모델·품질 4단계)
- 권리: 사용자가 Tripo 유료 구독(Max 플랜)을 결제한 뒤 웹 스튜디오(studio.tripo3d.ai)에서 그 구독 크레딧으로 만들었다. Tripo 약관(www.tripo3d.ai/terms, 2026-11-13판) 5.2.2 — 유료 구독 중에 만든 결과물(Paid Content)은 사용자 것이고 구독이 끝나도 그대로다. 출처 표기 의무 없음.
