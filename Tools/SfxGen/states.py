"""
상태 변화 사운드 5종 — 패링·그로기·페이즈 전환·사망·피격.

패턴음과 섞여도 묻히면 안 되므로 음역대를 일부러 비켜 놓았다.
  패링   아주 높게 (1.5k~9k)   — 패턴음 대부분이 이 위로 안 올라감
  그로기 아주 낮게 (60~260)
  피격   중간, 아주 짧게        — 자주 울리므로 귀에 안 거슬려야 한다
"""
import numpy as np

from core import (SR, t, noise, sine, perc, bandpass, lowpass,
                  soft_clip, reverb, extend, fit)


def parry_success(dur=0.65, seed=31):
    """패링 성공 — 높고 맑은 쇳소리. 이 게임에서 가장 기분 좋아야 할 소리."""
    n = int(SR * dur)

    ring = np.zeros(n)
    for f, g, d in [(1520, 1.0, 4.5), (2280, 0.82, 5.5), (3410, 0.6, 7),
                    (4980, 0.44, 10), (6730, 0.28, 14), (9100, 0.16, 19)]:
        ring += sine(dur, f) * perc(n, 0.0004, d) * g

    spark = bandpass(noise(dur, seed), 3200, 12000) * perc(n, 0.0003, 65) * 0.6

    # 아주 살짝 위로 미끄러지게 해서 '성공'의 기분을 준다
    lift = sine(dur, 980, 1240) * perc(n, 0.002, 16) * 0.3

    return reverb(soft_clip(extend(ring * 0.6 + spark + lift, 0.5), 1.3),
                  wet=0.3, decay=9.0, bright=9000)


def groggy(dur=1.2, seed=32):
    """그로기 — 힘이 빠져 무너지는 소리. 음이 아래로 미끄러진다."""
    n = int(SR * dur)

    fall = sine(dur, 265, 58) * perc(n, 0.012, 2.5) * 0.95
    growl = lowpass(noise(dur, seed), 820) * perc(n, 0.02, 3.0) * 0.45
    wob = 1.0 + 0.2 * np.sin(2 * np.pi * 6.5 * t(dur))      # 비틀거림

    clank = np.zeros(n)                                      # 갑옷이 덜그럭
    k = int(SR * 0.18)
    if k < n:
        m = n - k
        for f, g, d in [(410, 0.4, 16), (690, 0.25, 21)]:
            clank[k:] += fit(sine(dur, f), m) * perc(m, 0.001, d) * g

    return reverb(soft_clip(extend((fall + growl) * wob + clank, 0.6), 1.3),
                  wet=0.33, decay=5.5)


def phase_change(dur=1.8, seed=33):
    """페이즈 전환 — 바닥에서 솟구쳐 한 방으로 끝난다."""
    n = int(SR * dur)

    rise = sine(dur, 46, 205) * np.linspace(0.12, 1.0, n) ** 2.2 * 0.85
    air = bandpass(noise(dur, seed), 200, 3200) * np.linspace(0.0, 1.0, n) ** 3.5 * 0.45

    hit = np.zeros(n)
    k = int(SR * 1.3)
    if k < n:
        m = n - k
        for f, g, d in [(66, 1.0, 8), (148, 0.6, 12), (318, 0.32, 16)]:
            hit[k:] += fit(sine(dur, f), m) * perc(m, 0.001, d) * g
        hit[k:] += fit(lowpass(noise(dur, seed + 1), 1400), m) * perc(m, 0.001, 10) * 0.6

    return reverb(soft_clip(extend(rise + air + hit * 1.15, 0.8), 1.8),
                  wet=0.4, decay=4.2, dur=1.3)


def death(dur=2.3, seed=34):
    """사망 — 폭발 뒤 갑옷이 무너져 내린다. 슬로우모션 0.9초를 받쳐줘야 한다."""
    n = int(SR * dur)

    boom = lowpass(noise(dur, seed), 400) * perc(n, 0.001, 3.2) * 1.2
    sub = sine(dur, 115, 26) * perc(n, 0.002, 2.2)
    debris = bandpass(noise(dur, seed + 1), 700, 6500) * perc(n, 0.004, 5.0) * 0.4

    # 0.5초 뒤부터 금속 조각이 떨어지는 소리
    clatter = np.zeros(n)
    rng = np.random.default_rng(seed + 2)
    for tm in rng.uniform(0.45, 1.7, 9):
        k = int(SR * tm)
        m = min(int(SR * 0.3), n - k)
        if m <= 0:
            continue
        f = rng.uniform(600, 2400)
        clatter[k:k + m] += (fit(sine(0.3, f), m) * perc(m, 0.001, 30)
                             * rng.uniform(0.1, 0.28))

    tail = sine(dur, 190, 86) * perc(n, 0.3, 1.3) * 0.28

    return reverb(soft_clip(extend(boom + sub + debris + clatter + tail, 1.0), 1.9),
                  wet=0.42, decay=3.6, dur=1.6)


def boss_hurt(dur=0.26, seed=35):
    """보스 피격 — 아주 짧게. 초당 여러 번 울리므로 거슬리면 안 된다."""
    n = int(SR * dur)

    tap = np.zeros(n)
    for f, g, d in [(620, 1.0, 30), (1010, 0.55, 38), (1640, 0.3, 46)]:
        tap += sine(dur, f) * perc(n, 0.0005, d) * g

    dull = sine(dur, 160, 95) * perc(n, 0.001, 36) * 0.5
    return reverb(soft_clip(extend(tap * 0.5 + dull, 0.2), 1.2), wet=0.16)


ALL = {
    "State_ParrySuccess": parry_success,
    "State_Groggy": groggy,
    "State_PhaseChange": phase_change,
    "State_Death": death,
    "State_BossHurt": boss_hurt,
}
