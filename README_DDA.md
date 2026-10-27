# DDA 모듈 — LinUCB 구현 + 오프라인 검증 (2026-10-17)

간트 차트 **'DDA 코어: LinUCB 구현 + 오프라인 검증'**의 산출물입니다. DDA 설계서 5~12장을 그대로 코드로 옮겼습니다.

> ⚠️ 이 작업 환경에는 C# 컴파일러가 없어 Unity에서 직접 빌드해 보지 못했습니다.
> 대신 C# 코드를 한 줄씩 파이썬으로 옮겨 정답 파일과 맞춰 봤고(2,061개 항목 일치), 별도로 한 번 더 검토했습니다.
> Console에 빨간 오류가 뜨면 그대로 보내 주세요.

---

## 1. 해 보는 법 (5분)

1. Unity로 프로젝트를 열고 컴파일이 끝날 때까지 기다린다 (Console에 빨간 오류 0개).
2. 메뉴 **역전의 탑 ▸ DDA ▸ 오프라인 검증 실행**
   → "DDA 오프라인 검증 통과 — N개 항목 모두 맞습니다" 창이 뜨면 성공.
   보고서는 `Logs/dda_verification_report.txt`에도 남습니다 (발표 자료 근거).
3. 아라쿠스가 들어간 씬(`ArakusTestScene`, 게임 흐름에 넣었다면 `BossTestScene`)을 열고 메뉴 **역전의 탑 ▸ DDA ▸ 지금 씬에 DDA 붙이기** → 씬 저장(Ctrl+S).
4. Play → **F2**로 DDA 패널을 연다 (F1은 기존 디버그 패널).
5. 한 판 싸우고 (지든 이기든) 다시 Play. 두 번째 전투부터 단계가 움직입니다.

콘솔에 이런 줄이 뜨면 잘 돌아가는 것입니다.

```
[Collector] 기록 시작 B261017... — Arakus 2번째 도전, DDA 적용
[DDA] Arakus 2번째 전투 — 그룹 dda, 정책 linucb, 초기값 sim-0 (+0회 학습)
  일반 공격 1 (거검 횡베기): -1 (learned, 예측 0.66 ± 0.03)
  방패 돌진: 0 (noData)
  ...
[DDA] Arakus 전투 끝 (PlayerDead) — 모델 업데이트 4개 패턴, 끝낸 전투 2번, 연속 패배 2번. 저장: ...
```

---

## 2. 들어온 것

```
Assets/ReversalTower/DDA/
├─ Core/      계산 코어 (Unity 없이 돌아가는 순수 C#)
│   DdaModel      LinUCB 모델: 예측 ŷ · 불확실성 u · 업데이트 (설계서 8.1)
│   DdaPolicies   정책 4개 off · rule · random · linucb (같은 틀 IDdaPolicy)
│   DdaEngine     전투 직전 단계 고르기(안전장치 9장) · 전투 직후 학습 · 문맥 x0~x5(6장)
│   DdaState      보스별 상태 (모델 · 패턴별 단계 · 최근 3전 · 연속 패배)
│   DdaMath       18×18 행렬 계산 (촐레스키 분해)
├─ Runtime/   게임 연결
│   DDAManager    설정 + F2 패널. 수집기 오브젝트에 붙인다
│   DdaRuntime    전투 시작 → 단계 걸기 / 전투 끝 → 학습 · 저장
│   DDAArmTable   배율 표 에셋 (5장). 숫자만 바꿔 테스트
│   DdaStateStore 상태 저장 (persistentDataPath/dda/state_Arakus.json) · 초기값 파일 읽기
├─ Editor/    메뉴 (오프라인 검증 · 씬에 붙이기 · 초기값 만들기 · 상태 지우기 · 기록 폴더 열기)
│   Golden/dda_golden_v1.json   파이썬 시뮬레이션이 만든 정답 파일
├─ Priors/dda_prior_sim0.json   시험용 초기값 (가상 플레이테스트 30명 × 10전)
└─ Data/DDAArmTable.asset       '씬에 붙이기'를 누르면 생김

Tools/ddasim/   파이썬 시뮬레이션 + 정답 파일 만드는 코드 (Unity와 무관)
```

### 한 판의 흐름

```
수집기 기록 시작 ─▶ DDAManager ─▶ 패턴마다: 문맥 → 정책 → 안전장치 → 배율 표 → boss.SetTuning
                                  └▶ 수집기에 단계 · 문맥 · 예측 전달 (CSV에 같이 남음)
전투 끝 (클리어 · 사망 · 포기) ─▶ 유효 타 3 이상인 패턴만 모델 업데이트 → 연속 패배 → 저장
```

---

## 3. DDA 매니저 설정 (Inspector)

| 칸 | 기본 | 뜻 |
|---|---|---|
| Group Mode | 항상 DDA | 본 실험에서는 **PC마다 50:50** (한 번 정해지면 그 PC는 고정) |
| Policy | LinUcb | Rule = 규칙 기반 · **Random = 플레이테스트용** · Off = 끔 |
| Prior File | dda_prior_sim0 | 비우면 LinUCB 대신 규칙 기반으로 고름 (이유 `fallback`) |
| Alpha | 0.2 | 설계서 8.2 |
| Default Fail Rate | 0.4 | 기록이 모자랄 때 쓰는 실패율. 플레이테스트 평균으로 바꾼다 |
| Panel Key | F2 | 개발용 패널. 배포 빌드에서는 열리지 않는다 |

**F2 패널**에서 패턴마다 단계 · 이유 · 예측을 보고, **−/+로 지금 전투에 바로 단계를 걸 수 있습니다**
(설계서 12.3의 '동작 확인': 배율이 실제로 걸리는지 보기). 강제로 바꾼 것은 기록에 `forced`로 남습니다.

배율은 `Data/DDAArmTable.asset`에서 바꿉니다. 선딜은 배율을 곱해도 0.4초 아래로 내려가지 않고,
연타 수 ±1은 방패 돌진처럼 모든 판정이 2타 이상인 패턴에만 걸립니다.

### DDA가 건드리지 않는 패턴

퀵스텝 · 분노(타격 없음), 돌진 기믹(강제 패턴, 한 판에 2타뿐). 나머지 아라쿠스 패턴 8개가 대상입니다.
다른 패턴을 빼고 싶으면 패턴 에셋의 **DDA ▸ Dda Excluded**를 켭니다.

---

## 4. 초기값: 시험용 → 진짜 (10월 말 플레이테스트)

`dda_prior_sim0`은 가상 플레이어로 만든 **시험용**입니다. LinUCB가 도는지 보는 데만 쓰고 실험 · 발표 결과에는 쓰지 않습니다.

1. 플레이테스트 빌드: DDA 매니저 **Policy = Random**, Group Mode = 항상 DDA.
2. 참가자마다 10전. 각 PC의 `persistentDataPath/logs/patterns.csv`를 한 폴더에 모은다 (메뉴 **전투 기록 폴더 열기**).
3. 메뉴 **역전의 탑 ▸ DDA ▸ 초기값 만들기** → 그 폴더 선택 → `Priors/dda_prior_Arakus_날짜.json` 생성.
4. DDA 매니저의 Prior File에 새 파일을 넣고 Policy = LinUcb. 모델은 다음 전투부터 새 초기값으로 다시 시작합니다.

---

## 5. 기존 파일 수정 (5개)

| 파일 | 바뀐 것 |
|---|---|
| `Combat/CombatDataCollector.cs` | `BattleBegan` 이벤트, `SetDdaLog`, 전투가 끝날 때 DDA 칸 · 대응 성공률 채우기 |
| `Combat/BattleModels.cs` | 요약에 experimentGroup · ddaPolicy · priorVersion · protectionApplied, 패턴 집계에 ddaStep · ddaReason · ctx1~5 · ddaPredicted · ddaUncertainty · responseRate · ddaReward (설계서 10장) |
| `Combat/CombatLogWriter.cs` | CSV에 위 열 추가. 열이 바뀌면 예전 파일을 `patterns_old_날짜.csv`로 옮기고 새로 시작 |
| `Integration/PlayerBossHitAdapter.cs` | **대시 무적으로 흘린 타를 Ignored → Missed(회피)로 센다** (아래 6번) |
| `Boss/Core/BossPatternData.cs` | `ddaExcluded` 칸 하나 |

정훈의 `PlayerMovement`와 보스 FSM 본체는 건드리지 않았습니다.

---

## 6. 알아 둘 것

- **대시 회피 기록이 바뀝니다.** PlayerMovement에는 피격 후 무적이 없고 대시 무적만 있어서, 체력이 안 줄고 패링도 아니면 대시로 피한 것입니다.
  예전처럼 Ignored로 두면 대응 성공률의 분모 · 분자에서 같이 빠져서 대시를 잘 쓰는 사람일수록 성공률이 낮게 잡힙니다.
  `dodgeCount`가 늘어나는 것이 정상입니다. 어댑터의 `Count Dash As Dodge`를 끄면 예전과 같아집니다.
- **데미지 배율(±20%)은 지금 거의 안 걸립니다.** 플레이어 체력이 정수 5칸이라 보스 피해 20 × 0.05 = 1이 0.8배든 1.2배든 반올림하면 1입니다.
  선딜 · 쿨타임 · 연타는 그대로 걸리고, DDA가 배우는 대응 성공률에도 영향이 없습니다. 체력 단위를 키울지 정훈과 정해야 합니다.
- **선딜 하한 0.4초는 속도 1배 기준입니다.** 분노(속도 1.5배) 중에는 원래 기획대로 더 짧아질 수 있습니다.
- **게임을 끄거나 Play를 멈추면 그 전투는 기록 · 학습되지 않습니다** (수집기의 원래 동작). 로비로 나가는 '포기'는 기록 · 학습됩니다.
- **DDA 상태 지우기**: F2 패널 아래 버튼(이 보스) 또는 메뉴 **저장된 DDA 상태 지우기 (이 PC)**. 전투 기록 CSV는 지우지 않습니다.

---

## 7. 정답 파일 다시 만들기 (규칙을 바꿨을 때만)

C# 규칙(`DdaEngine`)을 바꾸면 `Tools/ddasim/ref_engine.py`도 똑같이 바꾸고:

```
cd Tools/ddasim
python3 export_golden.py out
```

`out/dda_golden_v1.json` → `Assets/ReversalTower/DDA/Editor/Golden/`, `out/dda_prior_sim0.json` → `Assets/ReversalTower/DDA/Priors/`에 덮어쓴 뒤 검증 메뉴를 다시 돌립니다.
