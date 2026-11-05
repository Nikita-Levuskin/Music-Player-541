"""Generate five original PCM WAV demos using only the Python standard library."""

import math
from pathlib import Path
import struct
import wave

DESTINATION = Path(__file__).resolve().parents[1] / "audio"
SAMPLE_RATE = 22050
TRACKS = [
    ("01 - Утро.wav", [261.63, 329.63, 392.00, 523.25, 392.00, 329.63], 0.5),
    ("02 - Движение.wav", [220.00, 261.63, 329.63, 440.00, 329.63, 261.63], 0.3),
    ("03 - Вечер.wav", [196.00, 246.94, 293.66, 392.00, 293.66, 246.94], 0.7),
    ("04 - Импульс.wav", [146.83, 220.00, 293.66, 349.23, 293.66, 220.00], 0.25),
    ("05 - Пространство.wav", [174.61, 220.00, 261.63, 349.23, 261.63, 220.00], 0.6),
]


def generate():
    DESTINATION.mkdir(exist_ok=True)
    for name, notes, beat in TRACKS:
        duration = 18
        frames = bytearray()
        for index in range(SAMPLE_RATE * duration):
            t = index / SAMPLE_RATE
            note = notes[int(t / beat) % len(notes)]
            phase = (t % beat) / beat
            envelope = min(1, phase * 35) * math.exp(-phase * 3)
            lead = (math.sin(2 * math.pi * note * t)
                    + 0.25 * math.sin(4 * math.pi * note * t)) * envelope
            bass = math.sin(2 * math.pi * notes[0] / 2 * t) * 0.18
            pulse = math.sin(2 * math.pi * 65 * t) * math.exp(-(t % beat) * 28) * 0.2
            fade = min(1, t / 0.15, (duration - t) / 0.4)
            left = int(max(-1, min(1, (lead * 0.42 + bass + pulse) * fade)) * 32767)
            right = int(max(-1, min(1, (lead * 0.38 + bass + pulse) * fade)) * 32767)
            frames.extend(struct.pack("<hh", left, right))
        with wave.open(str(DESTINATION / name), "wb") as target:
            target.setnchannels(2)
            target.setsampwidth(2)
            target.setframerate(SAMPLE_RATE)
            target.writeframes(frames)
        print(name)


if __name__ == "__main__":
    generate()
