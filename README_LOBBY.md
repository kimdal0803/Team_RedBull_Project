# 메뉴형 로비 "맹세의 회랑" + 강화 카드 + 영구 저장

걸어 다니던 팀 로비를 지우고, GPT 시안(0.4)의 메뉴형 로비를 팀 프로젝트에 옮겼습니다.
씬 이름은 그대로 `LobbyScene`이라 타이틀·사망·클리어 후 이동 코드는 고치지 않았습니다.

> Unity 6000.6.5f1에서 컴파일 오류 0. 타이틀 → 로비 → 카드 구매 → 아라쿠스 입장 → 사망 정산 → 로비(기록·카드 다시 열림) → ESC로 타이틀까지 직접 돌려 확인했습니다.

## 화면

| 위치 | 내용 |
|---|---|
| 위 | 제목, 보유 골드, `기록` · `설정` · `도움말` |
| 왼쪽 | 도전할 보스 5개. **앞 보스를 처치하면 다음 문이 열림** |
| 가운데 | 고른 보스 설명 + 입장 버튼 (씬이 없는 보스는 "준비 중") |
| 오른쪽 | 탑의 기록 — 정산한 전투 수, 처치한 보스 수, 고른 강화 수, 최근 전투 3개 |
| 아래 | 영구 강화 합계 + 강화 카드 열기 / 조작 요약 |

키: `↑↓` 보스 선택 · `C` 강화 카드 · `ENTER` 입장 · `H` 도움말 · `ESC` 타이틀 (창이 열려 있으면 닫기) · 카드 창에서 `1/2/3`

화면은 IMGUI로 1600×900 기준 좌표에 그리고 창 크기에 맞춰 늘립니다. 씬에는 카메라와 `LobbyMenu` 오브젝트(+ `InGameDebugPanel`)만 있습니다.

## 보스 목록

`LobbyMenu` 인스펙터의 `Bosses`에서 바꿉니다 (위에서부터 01~05).

| id (BossController.bossId) | 이름 | 씬 |
|---|---|---|
| Arakus | 아라쿠스 | BossTestScene |
| Phanes | 파네스 | (비어 있음 — 준비 중) |
| Adicus | 아디쿠스 | (준비 중) |
| Aethelos | 에텔로스 | (준비 중) |
| Kharos | 카로스 | (준비 중) |

파네스 씬이 들어오면 `Scene Name`만 채우고 Build Settings에 씬을 넣으면 됩니다. `id`는 그 씬 보스의 `bossId`와 같아야 처치 기록이 이어집니다.

## 강화 카드 (맹세의 인장)

- **3장 중 1장**. 스탯이 겹치지 않게 뽑고 등급(하급~전설·혼돈)은 예전 가챠 확률 그대로
- 카드를 **먼저 보고, 고를 때만 골드를 냄**. `선택하지 않고 돌아가기`(ESC)면 골드 그대로
- 놓인 3장은 저장됨 → 창을 닫았다 열거나 게임을 다시 켜도 같은 3장
- **전투 1번에 1장**. 정산(사망·클리어) 때 다시 열리고 새 3장
- 비용 = 100G + 25G × 고른 장수 (`BuffGachaManager`의 `Gacha Cost`, `Cost Step`)
- 고른 강화는 저장되어 **게임을 꺼도 유지**되고, 전투 씬 플레이어가 자기 기본값 위에 다시 쌓음
- 확률·수치는 예전처럼 인스펙터와 디버그 패널(F1, 로비에서)에서 조절

카드 이름: 공격력 = 검의 단련(검) · 체력 = 생명의 인장(심) · 이동 속도 = 질풍의 인장(풍) · 점프력 = 도약의 인장(익)

## 저장

- `Application.persistentDataPath/progress.json` (윈도우: `C:\Users\이름\AppData\LocalLow\<회사명>\<제품명>\`)
- 놓인 카드 3장, 이번 방문에 골랐는지, 고른 카드 목록, 정산한 전투 수, **처치한 보스**, **최근 전투 30개**(보스·처치 여부·시간·피해·패링·골드·날짜)
- 임시 파일에 다 쓴 뒤 한 번에 바꿔 끼우고 직전 파일은 `.bak`으로 남김. 본 파일이 깨지면 `.bak`에서 읽음
- 카드를 고를 때: 기록 저장 → 성공하면 골드 차감. 저장이 실패하면 아무것도 바뀌지 않음
- 골드는 예전 그대로 PlayerPrefs `PlayerGold`. 음량은 PlayerPrefs `MasterVolume`
- 타이틀의 데이터 초기화가 progress.json도 지움

## 지운 것 (팀 로비)

| 파일 | 이유 |
|---|---|
| `Scenes/LobbyScene.unity` 내용 | 같은 경로·같은 GUID의 새 메뉴형 로비로 교체 (Build Settings 그대로) |
| `Scripts/AltarTrigger.cs`, `Scripts/BuffCardUI.cs` | 제단·가챠 카드 UI |
| `Prefabs/ConfirmPanel.prefab`, `Prefabs/GachaPanel.prefab` | 가챠 확인창·카드창 |
| `MyAssets/Altar_Lobby.png`, `Altar_Idle.anim`, `Lobby_Altar_0.controller` | 제단 그림·애니메이션 |
| `MyAssets/Bgrd/DinV/` (Dynamic Space Background) | 로비 우주 배경 (로비에서만 씀) |

로비에서 쓰던 것이지만 다른 곳에서도 쓸 수 있어서 **남긴 것**: `Prefabs/Canvas.prefab`(체력바·보스바·일시정지·게임오버·클리어 패널이 든 HUD 캔버스), `Prefabs/InteractPrompt.prefab`, `Scripts/ScenePortal.cs`, `Scripts/KeyGuideTrigger.cs`.

## 고친 팀 파일 (팀원 확인 부탁)

| 파일 | 바뀐 것 |
|---|---|
| `Scripts/BuffGachaManager.cs` | 화면 코드를 빼고 **규칙만** 남김 (등급 뽑기·확률·수치·비용·구매). 디버그 패널이 쓰는 이름(`Instance`, `dropRates`, `*Values`, `UpdateLobbyGoldUI`)은 그대로 |
| `Scripts/PlayerMovement.cs` | `Start`에 한 줄(`ApplyPermanentCards()`) + 맨 아래 메서드 1개. 기존 코드 수정 없음 |
| `Scripts/BattleStatsManager.cs` | `SettleAndSaveGold` 끝에 정산 기록(보스 id·처치 여부·피해·패링·골드·시간) 저장 + 카드 다시 열기 |
| `Scripts/MenuManager.cs` | 데이터 초기화 때 progress.json도 지움 (한 줄) |

새 파일: `ReversalTower/Lobby/LobbyMenu.cs`, `ReversalTower/Progress/ProgressData.cs`, `ReversalTower/Progress/ProgressStore.cs`

## 테스트하는 법

- `LobbyMenu` 오브젝트의 `Buff Gacha Manager` ⋮ 메뉴
  - `⭐ [테스트] 이번 방문 카드 다시 열기` — 전투 없이 다시 고르기
  - `⭐ [테스트] 진행 기록 전부 지우기` — progress.json 삭제 (골드는 그대로)
- 골드는 디버그 패널(F1)의 골드 관리

## 기획 확인이 필요한 것

| 내용 | 지금 구현 |
|---|---|
| 보스 2~5의 부제·설명 | GPT 시안 문구를 팀 조작키에 맞게 다듬은 임시 문구 |
| 해금 순서 | 앞 보스를 처치해야 다음 문이 열림 (5번 카로스도 4번 다음) |
| 비용 증가 폭 | 장당 +25G. `Cost Step`을 0으로 두면 고정 |
| GPT 시안의 장착 궁극기 칸 | 팀 게임에 궁극기가 아직 없어 조작 요약으로 대신함 |
| 로비 배경 그림 | 지금은 어두운 단색. `LobbyMenu`의 `Backdrop`에 그림을 넣으면 어둡게 깔림 (다음 단계: 배경) |

## 알려진 한계

- 보스를 처치해 다음 문이 열리는 흐름은 코드로만 확인했습니다 (테스트 씬에서 아라쿠스가 공중에 떠 있어 처치까지 가지 못함). 정산 기록의 `clear`는 `BossClearManager`가 이미 쓰는 `isBossKilled` 그대로입니다.
