# 합본 프로젝트 — 열고 메뉴 한 번 누르면 끝 (2026-10-15)

원본: `kimdal0803/Team_RedBull_Project` (Unity **6000.6.5f1**)
여기에 **FSM 보스 + 아라쿠스 패턴 11종 + 돌진 기믹 + 전투 데이터 수집기**를 합쳐 둔 것입니다.

---

## 할 일 (3분)

### 1. 압축 풀기

`Team_RedBull_Project_merged` 폴더를 원하는 곳에 풉니다.
(원래 프로젝트와 **다른 폴더**에 두세요. 원본은 그대로 남겨 둡니다.)

### 2. Unity Hub로 열기

1. **Unity Hub ▸ Add ▸ Add project from disk**
2. 압축 푼 `Team_RedBull_Project_merged` 폴더를 고른다
3. 버전이 **6000.6.5f1** 인지 확인하고 연다
4. 처음 열 때 몇 분 걸립니다 (에셋을 다시 읽습니다)
5. **Console에 빨간 에러가 없는지 확인**

### 3. 메뉴 한 번

상단 메뉴 **역전의 탑 ▸ ★ 아라쿠스 테스트 씬 만들기 (전자동)**

이것만 누르면 아래가 전부 자동으로 됩니다:

- `BossTestScene`을 복제해 **`ArakusTestScene`** 생성 (원본은 안 건드림)
- `Player` · `Wall` · `GimmickObject` 레이어 생성
- 아라쿠스 패턴 에셋 11개 생성
- 기존 `DummyBoss`를 **끄기만** 함 (지우지 않음)
- 그 자리에 아라쿠스 보스 세우기 + 콜라이더 + 값 맞추기
- 패턴 9개와 페이즈 전환 3개 연결
- 플레이어에 피격 어댑터 · 이벤트 릴레이 붙이기
- 전투 데이터 수집기 놓고 보스 연결
- 돌진 기믹 기둥을 보스와 플레이어 사이에 놓기
- 씬 저장

끝나면 "세팅 완료" 창이 뜹니다. **다시 눌러도 안전합니다.**

### 4. Play

**Play** → **Game 창을 한 번 클릭** → 플레이어로 보스에게 접근

---

## 잘 됐는지 보는 법

콘솔에 이 순서로 뜨면 성공입니다.

```
[Collector] 기록 시작 B261015... — Arakus 1번째 도전, DDA 미적용
[Arakus] 패턴 시작: 일반 공격 1 (거검 횡베기) (Arakus_NormalAttack_R)
[Arakus] Idle → Telegraph
[피격 어댑터] Arakus_NormalAttack_R 1타 → Hit (보스 20 → 플레이어 1)
[Player] 피격! 남은 체력: 4
```

| 확인할 것 | 어떻게 |
|---|---|
| 보스 체력바 | 화면 위 바가 때릴 때 줄어듦 |
| 패링 | "패링 신호!" 뜰 때 패링 → `→ Parried` + 보스 경직 |
| 페이즈 | 체력 60%에서 2페이즈 + 돌진 기믹 |
| 기믹 | 기둥을 보스와 나 사이에 두면 `돌진 멈춤 (기믹 오브젝트)` |
| 클리어 | 보스 처치 → 기존 클리어 연출 + 골드 정산 |
| 기록 | `%USERPROFILE%\AppData\LocalLow\<회사>\<게임>\logs\` 에 CSV 3개 |

---

## 무엇이 바뀌었나

### 기존 파일 수정 — 딱 1개, 8줄

```
Assets/Scripts/PlayerAttackTrigger.cs    FSM 보스 타격 경로 추가
```

`PlayerMovement`(2614줄), UI, 씬 흐름, 버프 가챠, 골드 정산은 **한 줄도 안 건드렸습니다.**
`DummyBoss`도 그대로 있고, 원래 `BossTestScene`도 그대로 돌아갑니다.

### 새로 들어온 것

```
Assets/ReversalTower/
├─ Boss/          보스 FSM + 아라쿠스 패턴 11종 + 투사체 + 돌진 기믹
├─ Combat/        전투 데이터 수집기 (CSV 저장)
└─ Integration/   정훈 코드와 이어 주는 다리 3개 + 씬 자동 세팅
```

플레이어 쪽은 **공개 프로퍼티만 읽어서** 연결했습니다 (수정 0줄):

| 다리 | 하는 일 |
|---|---|
| `PlayerBossHitAdapter` | 보스 공격 → `TakeDamage`. 체력이 실제로 줄었는지로 Hit/Parried/Ignored 판정 |
| `BossUIBridge` | 보스 → `BossUI` 체력바 · `BossClearManager` 클리어 · `BattleStatsManager` 골드 |
| `PlayerCombatEventRelay` | 체력·포션·쿨다운을 지켜보다 수집기에 알림 |

### 서버(UGS)는 뺐습니다

이 프로젝트에 `Authentication`·`CloudSave`·`Analytics` 패키지가 없어서,
수집기를 그대로 넣으면 컴파일이 깨집니다.
**의존성을 끊고 CSV만 남기는 버전**으로 바꿨습니다.
나중에 서버를 붙일 때는 `BattleContextFactory`와 `CombatLogBridge` 두 파일만 고치면 됩니다.

---

## 알려진 한계

플레이어 코드를 고치지 않은 대가로 두 가지가 기획서와 다릅니다.
**콘솔에 `※` 표시로 알려 주니** 실제로 문제가 되는지 보고 판단하세요.

| 문제 | 지금 | 기획서 |
|---|---|---|
| 회피불가가 안 먹힘 | 일대 휩쓸기도 대시 무적으로 피해짐 | 구르기 무적을 뚫어야 함 |
| 패링이 모든 공격을 막음 | 패링 불가 패턴도 막힘 | 일부 패턴만 패링 가능 |

둘 다 `PlayerMovement.TakeDamage`에 조건 두 줄이면 풀립니다.
정훈의 파일이라 **합의 먼저**가 맞다고 보고 손대지 않았습니다.

그 밖에:

- 궁극기 기록은 비어 있습니다 (정훈 코드에 아직 궁극기가 없음)
- 스킬 사용은 쿨다운 감시 방식이라, 같은 프레임에 두 번 쓰면 한 번으로 셉니다
- 보스 애니메이션·이펙트는 연결돼 있지 않습니다 (아트가 나온 뒤)
- 내려찍기에 점프 연출이 없습니다 (지면을 미끄러지듯 이동)

---

## 수치를 손보고 싶으면

| 어디 | 칸 | 기본값 | 뜻 |
|---|---|---|---|
| `Arakus` ▸ Boss Controller | Max Hp | 60 | 보스 체력. 플레이어 공격력(1~3)에 맞춘 값 |
| `Arakus` ▸ Boss UI Bridge | Reward Hp Override | 50 | 클리어 골드 보상 기준 |
| 플레이어 ▸ Player Boss Hit Adapter | Damage Scale | 0.05 | 보스 피해 20 → 플레이어 1. 올리면 더 아픔 |
| 패턴 에셋 (`Assets/Data/Boss/Arakus`) | Telegraph Time 등 | 기획서 값 | 각 에셋 `Design Note`에 근거가 적혀 있음 |

---

## 팀에 알릴 것

1. `PlayerAttackTrigger.cs` 를 고쳤습니다 — 정훈 파일입니다
2. 보스 구현이 셋(`DummyBoss` / `ArakusBoss` / FSM)이었고, **FSM으로 가는 방향**으로 작업했습니다
3. 수집기가 둘(`BattleStatsManager` / `CombatDataCollector`)이고 지금은 **공존**합니다
   — 골드 정산은 기존 것이, 연구용 기록은 새 것이 합니다
4. 서버 연동은 아직 안 들어갔습니다

**원본 저장소에 바로 올리지 말고, 돌아가는 걸 확인한 뒤 팀에 보여주세요.**

> ⚠️ 유니티에서 컴파일해 보지 못했습니다. 문법·참조 검사는 통과했지만
> Unity API 수준의 오류가 남아 있을 수 있습니다. 콘솔 에러는 그대로 보내 주세요.
