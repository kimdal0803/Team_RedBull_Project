"""
아라쿠스 패턴 11종의 전용 사운드.

설계 원칙 — 플레이어가 화면을 안 봐도 '무슨 패턴인지' 귀로 알아야 한다.
  가볍고 빠른 공격  → 높고 짧고 바람 소리 위주
  묵직한 공격       → 낮고 길고 울림 위주
  방패 계열         → 금속 질감
  이동 계열         → 발소리 + 긁히는 소리
"""
import numpy as np

from core import (SR, t, noise, sine, perc, swell, bandpass, lowpass, highpass,
                  sweep_bandpass, soft_clip, reverb, extend, fit)


# ── 재료 ───────────────────────────────────────────────────────────────
def _metal(dur, partials, amp=1.0):
    """어긋난 배음으로 만드는 금속 울림. (주파수, 세기, 감쇠) 목록을 받는다."""
    n = int(SR * dur)
    out = np.zeros(n)
    for f, g, d in partials:
        out += sine(dur, f) * perc(n, 0.0006, d) * g
    return out * amp


def _whoosh(dur, lo, hi, peak=0.5, seed=0, width=2.0):
    n = int(SR * dur)
    return sweep_bandpass(noise(dur, seed), lo, hi, width=width) * swell(n, peak)


def _footstep(dur, freq, seed, amp=1.0):
    n = int(SR * dur)
    thud = sine(dur, freq, freq * 0.45) * perc(n, 0.001, 28)
    grit = lowpass(noise(dur, seed), 2600) * perc(n, 0.0008, 42) * 0.55
    return (thud + grit) * amp


# ── 일반 공격 3종 — 짧고 서로 또렷이 달라야 한다 ────────────────────────
def normal_attack_sword(dur=0.4, seed=11):
    """일반 공격 1 (검) — 날카롭게 베는 소리."""
    n = int(SR * dur)
    air = _whoosh(dur, 700, 3400, peak=0.46, seed=seed)
    edge = _metal(dur, [(1950, 0.5, 26), (3120, 0.3, 32)])
    return reverb(soft_clip(extend(air * 0.95 + edge * 0.5, 0.35), 1.3), wet=0.22)


def normal_attack_shield(dur=0.45, seed=12):
    """일반 공격 2 (방패) — 둔탁한 금속 후려치기."""
    n = int(SR * dur)
    metal = _metal(dur, [(380, 1.0, 13), (627, 0.62, 17), (1045, 0.38, 22),
                         (1690, 0.2, 28)])
    thud = sine(dur, 125, 52) * perc(n, 0.001, 24) * 1.05
    air = _whoosh(dur, 400, 1500, peak=0.4, seed=seed) * 0.5
    return reverb(soft_clip(extend(metal * 0.6 + thud + air, 0.4), 1.5), wet=0.26)


def normal_attack_kick(dur=0.33, seed=13):
    """일반 공격 3 (발차기) — 금속기 없이 천·가죽의 퍽."""
    n = int(SR * dur)
    thud = sine(dur, 170, 62) * perc(n, 0.0012, 34) * 1.1
    cloth = bandpass(noise(dur, seed), 900, 4200) * perc(n, 0.001, 60) * 0.45
    air = _whoosh(dur, 500, 1800, peak=0.38, seed=seed + 1) * 0.4
    return reverb(soft_clip(extend(thud + cloth + air, 0.3), 1.4), wet=0.2)


# ── 큰 공격 2종 ────────────────────────────────────────────────────────
def sword_swinging(dur=0.62, seed=14):
    """거검 휘두르기 — 묵직하게 공기를 가른다. 패링 타이밍을 귀로 재는 소리."""
    n = int(SR * dur)
    air = _whoosh(dur, 300, 1750, peak=0.55, seed=seed, width=2.1)
    body = sine(dur, 92, 54) * swell(n, 0.6) * 0.6
    ring = _metal(dur, [(840, 0.35, 9), (1420, 0.2, 12)]) * 0.5
    return reverb(soft_clip(extend(air * 0.95 + body + ring, 0.55), 1.45), wet=0.3)


def sweeping_strike(dur=0.85, seed=15):
    """쓸어베기 — 더 길고 낮게, 바닥을 긁으며 돈다."""
    n = int(SR * dur)
    air = _whoosh(dur, 220, 1250, peak=0.58, seed=seed, width=2.4)
    scrape = bandpass(noise(dur, seed + 1), 1200, 5200) * swell(n, 0.62) * 0.38
    body = sine(dur, 74, 42) * swell(n, 0.64) * 0.7
    return reverb(soft_clip(extend(air * 0.9 + scrape + body, 0.6), 1.6), wet=0.34)


# ── 돌진 2종 — 지속음이라 다른 패턴과 확실히 구분된다 ────────────────────
def shield_dash(dur=1.4, seed=16):
    """방패 돌진 — 발소리가 연속으로 울리며 밀고 들어온다."""
    n = int(SR * dur)
    out = np.zeros(n)

    for i, tm in enumerate(np.arange(0.05, dur - 0.1, 0.17)):
        k = int(SR * tm)
        seg = fit(_footstep(dur - tm, 115 + (i % 2) * 18, seed + i, amp=0.75), n - k)
        out[k:] += seg

    rumble = lowpass(noise(dur, seed + 40), 240) * swell(n, 0.5) * 0.5
    clank = _metal(dur, [(520, 0.3, 4), (880, 0.18, 5)]) * 0.35
    return reverb(soft_clip(extend(out + rumble + clank, 0.5), 1.5), wet=0.3)


def charge(dur=1.6, seed=17):
    """돌진 (2페이즈 기믹) — 선딜 1.1초 동안 차오르다 터진다."""
    n = int(SR * dur)

    # 차오르는 구간
    build = sine(dur, 55, 150) * np.linspace(0.1, 1.0, n) ** 2.2 * 0.75
    hiss = sweep_bandpass(noise(dur, seed), 300, 3000, width=2.8)
    hiss *= np.linspace(0.0, 1.0, n) ** 3.5 * 0.5

    # 1.1초에 발사
    out = build + hiss
    k = int(SR * 1.1)
    if k < n:
        m = n - k
        burst = fit(lowpass(noise(dur, seed + 1), 1800), m) * perc(m, 0.0008, 9) * 1.1
        boom = fit(sine(dur, 90, 38), m) * perc(m, 0.001, 7) * 1.0
        out[k:] += burst + boom

    return reverb(soft_clip(extend(out, 0.6), 1.8), wet=0.36, decay=5.5)


# ── 방패 투척 2종 — 회전하는 도플러로 구분 ─────────────────────────────
def _spin(dur, base, rate, depth, seed):
    """빙글빙글 도는 금속. 투척 방패의 정체성."""
    n = int(SR * dur)
    wob = 1.0 + depth * np.sin(2 * np.pi * rate * t(dur))
    tone = np.sin(2 * np.pi * np.cumsum(base * wob) / SR)
    air = bandpass(noise(dur, seed), 800, 5000) * (0.5 + 0.5 * wob) * 0.35
    return tone * 0.55 + air


def shield_throw_dash(dur=1.0, seed=18):
    """방패 투척 후 대쉬 — 던지고(회전) 끝에 벤다."""
    n = int(SR * dur)
    throw = _whoosh(dur, 600, 2400, peak=0.12, seed=seed) * 0.8
    spin = _spin(dur, 420, 13.0, 0.5, seed + 1) * np.linspace(1.0, 0.35, n) * 0.7

    out = throw + spin
    k = int(SR * 0.8)                      # 대쉬 끝 베기
    if k < n:
        m = n - k
        out[k:] += fit(_whoosh(dur - 0.8, 700, 3000, peak=0.4, seed=seed + 2), m)
        out[k:] += fit(_metal(dur - 0.8, [(1820, 0.45, 24)]), m)

    return reverb(soft_clip(extend(out, 0.5), 1.5), wet=0.3)


def shield_throw_slam(dur=1.1, seed=19):
    """방패 투척 후 내려찍기 — 회전은 같지만 끝이 '쾅'. 패링 불가라 귀로 구분돼야 한다."""
    n = int(SR * dur)
    throw = _whoosh(dur, 500, 2000, peak=0.12, seed=seed) * 0.8
    spin = _spin(dur, 360, 11.0, 0.45, seed + 1) * np.linspace(1.0, 0.3, n) * 0.7

    out = throw + spin
    k = int(SR * 0.82)                     # 내려찍기
    if k < n:
        m = n - k
        out[k:] += fit(sine(dur - 0.82, 78, 32), m) * perc(m, 0.001, 8) * 1.3
        out[k:] += fit(lowpass(noise(dur - 0.82, seed + 2), 900), m) * perc(m, 0.001, 11) * 0.9
        out[k:] += fit(_metal(dur - 0.82, [(300, 0.5, 10), (505, 0.3, 14)]), m) * 0.6

    return reverb(soft_clip(extend(out, 0.7), 1.8), wet=0.36, decay=5.0)


# ── 보조 2종 ───────────────────────────────────────────────────────────
def quick_step(dur=0.3, seed=20):
    """퀵스텝 — 짧고 가볍게 한 발. 공격이 아니라는 게 바로 들려야 한다."""
    n = int(SR * dur)
    step = _footstep(dur, 150, seed, amp=0.7)
    swishy = highpass(bandpass(noise(dur, seed + 1), 2000, 7000), 1500)
    swishy *= perc(n, 0.002, 30) * 0.4
    return reverb(soft_clip(extend(step + swishy, 0.25), 1.2), wet=0.18)


def rage(dur=2.4, seed=21):
    """분노 (30% 버프) — 선딜 2.5초. 포효가 길게 차오른다."""
    n = int(SR * dur)

    # 포효: 저음 톱니 + 거친 배음
    base = 86.0
    wob = 1.0 + 0.06 * np.sin(2 * np.pi * 5.5 * t(dur))
    roar = np.zeros(n)
    for h, g in [(1, 1.0), (2, 0.6), (3, 0.42), (5, 0.24), (7, 0.14)]:
        roar += np.sin(2 * np.pi * np.cumsum(base * h * wob) / SR) * g

    shape = np.linspace(0.12, 1.0, n) ** 1.6
    roar *= shape * 0.45

    grit = bandpass(noise(dur, seed), 300, 3500) * shape * 0.3
    sub = sine(dur, 44, 58) * shape * 0.6

    return reverb(soft_clip(extend(roar + grit + sub, 0.9), 2.0),
                  wet=0.4, decay=4.0, dur=1.4)


ALL = {
    "Arakus_NormalAttack_R": normal_attack_sword,
    "Arakus_NormalAttack_L": normal_attack_shield,
    "Arakus_NormalAttack_F": normal_attack_kick,
    "Arakus_SwordSwinging": sword_swinging,
    "Arakus_SweepingStrike": sweeping_strike,
    "Arakus_ShieldDash": shield_dash,
    "Arakus_Charge": charge,
    "Arakus_ShieldThrowDash": shield_throw_dash,
    "Arakus_ShieldThrowSlam": shield_throw_slam,
    "Arakus_QuickStep": quick_step,
    "Arakus_Rage": rage,
}
