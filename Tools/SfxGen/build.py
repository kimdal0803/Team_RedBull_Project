"""
사운드 16개를 한 번에 만들어 WAV로 저장한다.

    python3 build.py <출력폴더>

수치를 만져서 다시 뽑고 싶으면 patterns.py / states.py만 고치면 된다.
"""
import os
import sys

from core import save
import patterns
import states


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "out"
    os.makedirs(out, exist_ok=True)

    made = []
    for group in (patterns.ALL, states.ALL):
        for name, fn in group.items():
            path = os.path.join(out, name + ".wav")
            save(path, fn())
            made.append((name, os.path.getsize(path)))

    for name, size in made:
        print(f"{size/1024:7.1f} KB  {name}.wav")
    print(f"\n총 {len(made)}개 → {out}")


if __name__ == "__main__":
    main()
