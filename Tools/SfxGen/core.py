"""소리를 만드는 기본 부품들. 여기엔 '악기'가 없고 '재료'만 있다."""
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100


# ── 시간축 ────────────────────────────────────────────────────────────
def t(dur):
    return np.linspace(0.0, dur, int(SR * dur), endpoint=False)


# ── 소리의 재료 ────────────────────────────────────────────────────────
def noise(dur, seed=None):
    rng = np.random.default_rng(seed)
    return rng.uniform(-1.0, 1.0, int(SR * dur))


def sine(dur, freq_start, freq_end=None):
    """freq_end를 주면 주파수가 미끄러진다 (슬라이드)."""
    tt = t(dur)
    if freq_end is None:
        phase = 2 * np.pi * freq_start * tt
    else:
        f = np.linspace(freq_start, freq_end, len(tt))
        phase = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(phase)


# ── 음량 곡선 (엔벨로프) ────────────────────────────────────────────────
def perc(n, attack=0.002, decay=3.0):
    """때리는 소리. 확 올라갔다 지수적으로 사라진다."""
    tt = np.arange(n) / SR
    a = int(SR * attack) or 1
    env = np.exp(-decay * tt)
    env[:a] *= np.linspace(0.0, 1.0, a)
    return env


def swell(n, peak=0.45):
    """휘두르는 소리. 중간에 가장 크다."""
    x = np.linspace(0.0, 1.0, n)
    return np.exp(-((x - peak) ** 2) / (2 * 0.16 ** 2))


# ── 필터 ──────────────────────────────────────────────────────────────
def bandpass(x, lo, hi, order=4):
    sos = signal.butter(order, [lo / (SR / 2), min(hi, SR / 2 - 100) / (SR / 2)],
                        btype="band", output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x, cutoff, order=4):
    sos = signal.butter(order, min(cutoff, SR / 2 - 100) / (SR / 2),
                        btype="low", output="sos")
    return signal.sosfilt(sos, x)


def sweep_bandpass(x, lo_start, lo_end, width=2.2, blocks=96):
    """통과 대역이 시간에 따라 움직인다. 휘두르는 '쉭' 소리의 핵심."""
    out = np.zeros_like(x)
    edges = np.linspace(0, len(x), blocks + 1).astype(int)
    centers = np.geomspace(lo_start, lo_end, blocks)
    for i in range(blocks):
        s, e = edges[i], edges[i + 1]
        if e <= s:
            continue
        pad = min(s, 2048)
        seg = x[s - pad:e]
        c = centers[i]
        out[s:e] = bandpass(seg, c / width, c * width)[pad:]
    return out


def highpass(x, cutoff, order=2):
    sos = signal.butter(order, max(cutoff, 20) / (SR / 2), btype="high", output="sos")
    return signal.sosfilt(sos, x)


# ── 공간감 ────────────────────────────────────────────────────────────
def _impulse(decay, dur, bright, seed):
    """돌 복도 같은 공간의 잔향을 흉내 낸 임펄스 응답."""
    rng = np.random.default_rng(seed)
    n = int(SR * dur)
    tt = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-decay * tt)
    ir = lowpass(ir, bright)
    ir[:int(SR * 0.004)] = 0.0          # 초기 공백 — 벽까지 날아가는 시간
    m = np.max(np.abs(ir))
    return ir / m if m > 0 else ir


def fit(x, m):
    """길이를 정확히 m 샘플로 맞춘다. 초 단위 계산에서 1샘플씩 어긋나는 걸 막는다."""
    if len(x) == m:
        return x
    if len(x) > m:
        return x[:m]
    return np.concatenate([x, np.zeros(m - len(x))])


def extend(x, tail=0.5):
    """잔향이 잘리지 않게 뒤에 빈 공간을 붙인다."""
    return np.concatenate([x, np.zeros(int(SR * tail))])


def reverb(x, wet=0.28, decay=7.0, dur=0.9, bright=5200, seed=99):
    """
    합성음이 '붕 뜨는' 가장 큰 이유가 잔향이 없어서다.
    석조 보스룸을 가정하고 짧은 잔향을 섞는다. wet이 클수록 멀고 넓게 들린다.
    """
    if wet <= 0:
        return x
    ir = _impulse(decay, dur, bright, seed)
    wetsig = signal.fftconvolve(x, ir)[:len(x)]
    m = np.max(np.abs(wetsig))
    if m > 0:
        wetsig /= m
    return x * (1.0 - wet * 0.5) + wetsig * wet


# ── 마무리 ────────────────────────────────────────────────────────────
def normalize(x, peak=0.89):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def soft_clip(x, drive=1.0):
    """살짝 찌그러뜨려 꽉 찬 느낌을 준다."""
    return np.tanh(x * drive) / np.tanh(drive)


def fade_out(x, dur=0.01):
    n = int(SR * dur)
    if n < len(x):
        x[-n:] *= np.linspace(1.0, 0.0, n)
    return x


def save(path, x):
    x = fade_out(normalize(x))
    wavfile.write(path, SR, (x * 32767).astype(np.int16))
    return path
