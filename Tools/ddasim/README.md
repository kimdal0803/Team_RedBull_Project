# ddasim — DDA 가상 시뮬레이션 (Unity와 무관)

Python 3 + numpy (+ power.py는 scipy).

| 파일 | 내용 |
|---|---|
| `sim.py` | 가상 플레이어 · 보스 · 정책 (DDA 설계서 12장) |
| `final.py` | 설계서 12.2 그래프 숫자 (`python3 final.py 100`, 약 4분) |
| `power.py` | 설계서 12.3 검정력 표 (약 3분) |
| `ref_engine.py` | 게임용 DDA 규칙의 파이썬 기준 구현. C# `DdaEngine`과 한 줄씩 같다 |
| `export_golden.py` | Unity 오프라인 검증용 정답 파일 + 시험용 초기값 sim-0 (`python3 export_golden.py out`) |

가정(한 단계의 효과 0.55, 학습 곡선 등)은 `sim.py` 맨 위 상수에 있습니다.
플레이테스트에서 실제 값을 재면 그 상수만 바꿔 다시 돌립니다.
