# 아라쿠스 효과음 생성기

`Assets/MyAssets/Arakus_SFX/` 의 WAV 16개를 만든 코드다.
다운로드한 음원이 아니라 **코드로 합성**한 것이라 라이선스가 깨끗하다.

## 다시 뽑기

```bash
pip install numpy scipy
cd Tools/SfxGen
python3 build.py ../../Assets/MyAssets/Arakus_SFX
```

## 파일 구성

| 파일 | 역할 |
|---|---|
| `core.py` | 기본 부품 — 노이즈, 사인파, 엔벨로프, 필터, 리버브 |
| `patterns.py` | 패턴 11종 사운드 (함수 1개 = 소리 1개) |
| `states.py` | 패링·그로기·페이즈 전환·사망·피격 5종 |
| `build.py` | 전부 만들어 WAV로 저장 |

소리를 바꾸고 싶으면 `patterns.py` / `states.py` 의 숫자만 만지면 된다.
예: 거검을 더 묵직하게 → `sword_swinging` 의 `_whoosh(dur, 300, 1750, ...)` 에서
앞 숫자(300)를 더 낮추면 저음이 강해진다.

## 교체

더 좋은 음원을 구하면 이 폴더를 쓸 필요 없이
보스 오브젝트의 **Boss Audio** 컴포넌트에서 클립만 갈아끼우면 된다.
