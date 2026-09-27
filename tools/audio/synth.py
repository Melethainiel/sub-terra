"""Synthesises every sound of the game: ambiences, music, effects.

    python3 tools/audio/synth.py                     # everything
    python3 tools/audio/synth.py step dice_hit       # just those
    python3 tools/audio/synth.py --spectra /tmp/s    # plus a spectrogram of each

Nothing is recorded or downloaded: every sound is built here from noise, damped
resonators and additive tones, then placed in a cave by convolution with a
synthetic impulse response. Deterministic — each sound has its own seed — so the
files only change when this script does. Needs numpy and ffmpeg (libvorbis).

Writes Ogg Vorbis to resources/audio/: ambience/ (stereo loops), music/ (a
stereo loop), sfx/ (mono, one file per variant: step_0.ogg, step_1.ogg…).
"""

import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "resources", "audio")


# ── Building blocks ──────────────────────────────────────────────────────────

def samples(duration):
    return int(round(duration * SR))


def noise(duration, rng, colour="white"):
    """White, pink (-3 dB/oct) or brown (-6 dB/oct) noise, shaped in the spectrum."""
    n = samples(duration)
    x = rng.standard_normal(n)
    if colour == "white":
        return x
    spectrum = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = f[1]
    spectrum *= (1 / np.sqrt(f)) if colour == "pink" else (1 / f)
    y = np.fft.irfft(spectrum, n)
    return y / (np.std(y) + 1e-12)


def padded(filter_):
    """Runs a spectral filter on the signal with silence either side of it. An FFT
    filter is circular: without the padding, a hit at the start rings on at the very
    end of the buffer, and every sound grows a ghost second impact."""
    def run(x, *args, **kwargs):
        pad = samples(0.6)
        y = filter_(np.concatenate([np.zeros(pad), x, np.zeros(pad)]), *args, **kwargs)
        return y[pad:pad + len(x)]
    return run


@padded
def band(x, low=None, high=None, width=0.35):
    """A band-pass (or low/high-pass) with gentle, log-spaced cosine edges."""
    n = len(x)
    spectrum = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1 / SR)
    lf = np.log2(np.maximum(f, 1.0))
    gain = np.ones_like(f)
    if low:
        edge = np.clip((lf - (np.log2(low) - width)) / (2 * width), 0, 1)
        gain *= 0.5 - 0.5 * np.cos(np.pi * edge)
    if high:
        edge = np.clip(((np.log2(high) + width) - lf) / (2 * width), 0, 1)
        gain *= 0.5 - 0.5 * np.cos(np.pi * edge)
    return np.fft.irfft(spectrum * gain, n)


@padded
def peaks(x, formants):
    """Resonant peaks — (centre, width in Hz, gain) — laid over a spectrum: a voice's formants."""
    n = len(x)
    spectrum = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1 / SR)
    gain = np.full_like(f, 0.05)
    for centre, width, g in formants:
        gain += g * np.exp(-0.5 * ((f - centre) / width) ** 2)
    return np.fft.irfft(spectrum * gain, n)


def envelope(duration, attack=0.005, decay=0.3, hold=0.0, curve=4.0):
    """Rises in `attack`, holds, then falls exponentially over `decay`."""
    n = samples(duration)
    t = np.arange(n) / SR
    env = np.ones(n)
    a = t < attack
    env[a] = (t[a] / attack) ** 2 if attack > 0 else 1.0
    after = t >= attack + hold
    env[after] = np.exp(-curve * (t[after] - attack - hold) / max(decay, 1e-4))
    return env


def modes(duration, rng, freqs, decays, amps=None, jitter=0.0):
    """Damped sinusoids: how wood, stone and metal ring when struck."""
    n = samples(duration)
    t = np.arange(n) / SR
    amps = amps if amps is not None else [1.0] * len(freqs)
    out = np.zeros(n)
    for f, d, a in zip(freqs, decays, amps):
        f = f * (1 + jitter * rng.uniform(-1, 1))
        out += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi)) * np.exp(-t / d)
    # Over the last quarter: a resonator still ringing at the end fades rather than stops.
    return fade_out(out, max(0.04, duration * 0.25))


def tone(duration, freq, harmonics=1, detune=0.0, rng=None, glide=None):
    """Additive tone. `harmonics` > 1 gives a band-limited saw; `glide` ends on another pitch."""
    n = samples(duration)
    t = np.arange(n) / SR
    f = np.full(n, float(freq)) if glide is None else np.geomspace(freq, glide, n)
    phase = 2 * np.pi * np.cumsum(f) / SR
    out = np.zeros(n)
    for h in range(1, harmonics + 1):
        if f.max() * h > SR / 2.2:
            break
        out += np.sin(h * phase * (1 + detune * (rng.uniform(-1, 1) if rng is not None else 0))) / h
    return out


def bell(duration, freq, ratios=(1, 2.0, 3.01, 4.2, 5.43), decay=1.2, rng=None):
    rng = rng or np.random.default_rng(0)
    return modes(duration, rng, [freq * r for r in ratios], [decay / (1 + 0.6 * i) for i in range(len(ratios))],
                 [1.0 / (1 + 0.8 * i) for i in range(len(ratios))])


def clicks(duration, rng, count, low, high, spread=1.0, level=1.0):
    """Grit: many tiny random impacts — gravel, debris, a die's rattle."""
    n = samples(duration)
    out = np.zeros(n)
    for _ in range(count):
        at = int(min(n - 1, abs(rng.exponential(spread)) * n / 4 if spread < 1 else rng.uniform(0, n)))
        size = samples(0.012)
        grain = rng.standard_normal(size) * np.exp(-np.arange(size) / (size / 5))
        end = min(n, at + size)
        out[at:end] += grain[: end - at] * rng.uniform(0.2, 1.0)
    return band(out, low, high) * level


def place(total, piece, at):
    """Adds `piece` into a buffer of `total` seconds, starting `at` seconds in."""
    out = np.zeros(samples(total))
    start = samples(at)
    end = min(len(out), start + len(piece))
    out[start:end] += piece[: end - start]
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[: len(p)] += p
    return out


def cave(x, seconds=1.4, wet=0.3, darkness=3000, rng=None, predelay=0.012):
    """A cave around the sound: convolution with a decaying, darkening noise tail."""
    rng = rng or np.random.default_rng(99)
    x = fade_out(x, 0.08)
    ir = rng.standard_normal(samples(seconds)) * np.exp(-6.9 * np.arange(samples(seconds)) / samples(seconds))
    ir = band(ir, 120, darkness)
    ir = np.concatenate([np.zeros(samples(predelay)), ir])
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
    n = len(x) + len(ir)
    size = 1 << (n - 1).bit_length()
    tail = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[:n]
    dry = np.concatenate([x, np.zeros(len(ir))])
    return (1 - wet) * dry + wet * tail * 0.6


def fade_out(x, seconds):
    """Brings a buffer's end down to silence: whatever still rings when it runs out
    would otherwise stop dead, with a click."""
    x = x.copy()
    n = min(len(x), samples(seconds))
    x[-n:] *= np.cos(np.linspace(0, np.pi / 2, n)) ** 2
    return x


def trim(x, threshold=1e-4):
    """Drops the silent end a reverb leaves."""
    loud = np.nonzero(np.abs(x) > threshold * np.max(np.abs(x)))[0]
    end = (loud[-1] + samples(0.02)) if len(loud) else len(x)
    x = x[:end]
    fade = min(len(x), samples(0.02))
    x[-fade:] *= np.linspace(1, 0, fade)
    return x


def normalise(x, peak_db=-3.0):
    return x / (np.max(np.abs(x)) + 1e-12) * 10 ** (peak_db / 20)


def loop(x, crossfade):
    """Makes a buffer loop seamlessly: its tail is faded over its head."""
    c = samples(crossfade)
    body, tail = x[:-c], x[-c:]
    ramp = np.sin(np.linspace(0, np.pi / 2, c)) ** 2
    body = body.copy()
    body[:c] = body[:c] * ramp + tail * (1 - ramp)
    return body


def smooth_random(duration, rng, rate):
    """A slow random wander between 0 and 1, `rate` changes a second."""
    points = max(4, int(duration * rate) + 2)
    knots = rng.uniform(0, 1, points)
    x = np.linspace(0, points - 1, samples(duration))
    lo = np.floor(x).astype(int)
    frac = x - lo
    hi = np.minimum(lo + 1, points - 1)
    s = (1 - np.cos(np.pi * frac)) / 2
    return knots[lo] * (1 - s) + knots[hi] * s


# ── Effects ──────────────────────────────────────────────────────────────────

def step(rng):
    """A boot on stone and loose grit."""
    d = 0.45
    thud = modes(d, rng, [85, 140, 230], [0.04, 0.03, 0.02], [1, 0.6, 0.3], jitter=0.1)
    scuff = band(noise(d, rng), 250, 2500) * envelope(d, 0.003, 0.07)
    grit = clicks(d, rng, 14, 1800, 7000, spread=0.3, level=0.5)
    return trim(cave(mix(thud * 0.9, scuff * 0.5, grit), 0.7, 0.18, rng=rng))


def dice_hit(rng):
    """A resin die striking stone: a hard click and a short, bright body ring."""
    d = 0.3
    click = band(noise(d, rng), 2000, 12000) * envelope(d, 0.0005, 0.006)
    ring = modes(d, rng, [1850, 2620, 3710, 5230], [0.035, 0.028, 0.02, 0.012], [1, 0.8, 0.5, 0.3], jitter=0.06)
    body = modes(d, rng, [420, 690], [0.04, 0.03], [0.5, 0.3], jitter=0.05)
    return trim(cave(mix(click * 1.2, ring * 0.5, body * 0.5), 0.6, 0.15, rng=rng))


def dice_settle(rng):
    """The last small rattle as a die tips onto its face."""
    d = 0.5
    parts = []
    for i, at in enumerate([0.0, 0.07, 0.12, 0.155, 0.18]):
        hit = dice_hit(rng) * (0.6 ** i)
        parts.append(place(d, hit, at))
    return trim(mix(*parts))


def tile_place(rng):
    """A stone slab set down: a deep thud, grit settling."""
    d = 1.2
    thud = modes(d, rng, [55, 83, 131], [0.22, 0.16, 0.1], [1, 0.7, 0.4])
    body = band(noise(d, rng), None, 700) * envelope(d, 0.004, 0.18)
    grit = clicks(d, rng, 30, 1500, 6000, spread=0.5, level=0.35)
    return trim(cave(mix(thud, body * 0.6, grit), 1.4, 0.28, rng=rng))


def spikes(rng):
    """Blades shooting out of an iron grate: a metallic shing and a thud."""
    d = 2.6
    rasp = band(noise(d, rng), 2500, 11000) * envelope(d, 0.002, 0.05)
    ring = modes(d, rng, [2310, 3170, 4480, 5890, 7230], [0.5, 0.42, 0.3, 0.22, 0.15], [1, 0.8, 0.6, 0.45, 0.3], jitter=0.02)
    thud = modes(d, rng, [70, 120], [0.12, 0.08])
    return trim(cave(mix(rasp * 0.7, ring * 0.35, thud * 0.8), 1.2, 0.25, rng=rng))


def darts(rng):
    """Three darts out of the walls: whistles, and the wood they bite into."""
    d = 0.9
    parts = []
    for i, at in enumerate([0.0, 0.08, 0.15]):
        whoosh = band(noise(0.25, rng), 900, 5500) * np.sin(np.linspace(0, np.pi, samples(0.25))) ** 3
        thunk = modes(0.2, rng, [310, 880, 1450], [0.05, 0.03, 0.02], [1, 0.5, 0.3], jitter=0.08)
        parts += [place(d, whoosh * 0.6, at), place(d, thunk * 0.8, at + 0.2)]
    return trim(cave(mix(*parts), 0.9, 0.2, rng=rng))


def collapse(rng):
    """Rock coming down: a crack, the roar of it, debris raining after."""
    d = 4.5
    roar = band(noise(d, rng, "brown"), None, 260) * envelope(d, 0.04, 1.6, 0.2)
    crack = band(noise(d, rng), 400, 6000) * envelope(d, 0.001, 0.08)
    thud = modes(d, rng, [38, 57, 90], [0.6, 0.4, 0.25])
    debris = clicks(d, rng, 90, 900, 6500, spread=0.6, level=0.6)
    return trim(cave(mix(roar * 1.1, crack * 0.6, thud * 0.9, debris), 2.2, 0.35, darkness=2000, rng=rng))


def guardian_wake(rng):
    """One of the Ashen Legion stirring: a growl through cooled rock, grinding."""
    d = 2.6
    t = np.arange(samples(d)) / SR
    vibrato = 1 + 0.03 * np.sin(2 * np.pi * 5.2 * t) + 0.02 * np.sin(2 * np.pi * 1.3 * t)
    phase = 2 * np.pi * np.cumsum(46 * vibrato) / SR
    saw = sum(np.sin(h * phase) / h for h in range(1, 60))
    growl = peaks(saw, [(320, 70, 1.0), (720, 110, 0.7), (1150, 150, 0.35)])
    growl *= envelope(d, 0.45, 1.0, 0.7, curve=3) * (0.8 + 0.2 * np.sin(2 * np.pi * 7 * t))
    grind = band(noise(d, rng), 90, 900) * smooth_random(d, rng, 6) * envelope(d, 0.3, 1.2, 0.6)
    return trim(cave(mix(growl * 0.9, grind * 0.5), 2.0, 0.35, darkness=1800, rng=rng))


def guardian_step(rng):
    """Its tread: heavier than a man's, stone on stone."""
    d = 1.0
    thud = modes(d, rng, [42, 68, 105], [0.28, 0.2, 0.12], [1, 0.7, 0.4], jitter=0.06)
    grind = band(noise(d, rng), 120, 1200) * envelope(d, 0.005, 0.12)
    return trim(cave(mix(thud, grind * 0.4), 1.5, 0.3, darkness=1600, rng=rng))


def guardian_strike(rng):
    """A blow from it: the swing, and what it lands on."""
    d = 1.0
    swing = band(noise(0.35, rng), 250, 2500) * np.sin(np.linspace(0, np.pi, samples(0.35))) ** 2
    impact = mix(modes(0.8, rng, [65, 110, 190], [0.18, 0.12, 0.08]), band(noise(0.8, rng), 300, 4000) * envelope(0.8, 0.001, 0.08))
    return trim(cave(mix(place(d, swing * 0.6, 0), place(d, impact, 0.28)), 1.2, 0.25, rng=rng))


def hurt(rng):
    """Hearts lost: a dull blow and a falling tone."""
    d = 0.8
    blow = mix(modes(d, rng, [95, 150], [0.12, 0.08]), band(noise(d, rng), 200, 1800) * envelope(d, 0.002, 0.06) * 0.6)
    fall = tone(d, 330, 4, glide=150) * envelope(d, 0.01, 0.35)
    return trim(cave(mix(blow, band(fall, None, 1500) * 0.35), 0.9, 0.2, rng=rng))


def heal(rng):
    """Hearts regained: two soft bells, rising."""
    d = 2.2
    d = 3.6
    return trim(cave(mix(place(d, bell(3.4, 880, rng=rng), 0), place(d, bell(3.4, 1318.5, rng=rng) * 0.8, 0.12)) * 0.6, 1.8, 0.35, rng=rng))


def key_pickup(rng):
    """A key lifted: a bright bell and a jingle."""
    d = 4.5
    chime = bell(4.5, 1320, ratios=(1, 2.76, 5.4, 8.93), decay=1.4, rng=rng)
    jingle = clicks(0.3, rng, 8, 3000, 10000, spread=0.4, level=0.3)
    return trim(cave(mix(chime * 0.6, jingle), 1.6, 0.3, rng=rng))


def key_deposit(rng):
    """A key into its lock: the clunk, then the lock answers."""
    d = 4.0
    clunk = modes(0.4, rng, [220, 480, 1100, 2400], [0.08, 0.06, 0.04, 0.02], [1, 0.7, 0.5, 0.3])
    answer = mix(bell(3.6, 659.3, rng=rng), bell(3.6, 987.8, rng=rng) * 0.7)
    return trim(cave(mix(place(d, clunk, 0), place(d, answer * 0.5, 0.18)), 2.0, 0.35, rng=rng))


def artefact(rng):
    """The Artefact revealed: a slow, shimmering chord."""
    d = 4.0
    chord = sum(tone(d, f, 1, detune=0.002, rng=rng) + tone(d, f * 1.003, 1) for f in (220, 261.6, 329.6, 440))
    chord *= envelope(d, 1.4, 1.8, 0.5, curve=3)
    sparkle = mix(*[place(d, bell(1.2, rng.uniform(1800, 3600), rng=rng) * 0.15, rng.uniform(0.3, 2.2)) for _ in range(9)])
    return trim(cave(mix(chord * 0.25, sparkle), 3.0, 0.45, rng=rng))


def curse(rng):
    """The curse falls: a dissonant swell, sucked in, then a boom."""
    d = 5.0
    cluster = sum(tone(d, f, 6) for f in (55, 58.3, 82.4, 87.3)) * envelope(d, 1.6, 2.4, 0.3, curve=3)
    swell = band(noise(1.6, rng), 1500, 9000) * np.linspace(0, 1, samples(1.6)) ** 3
    boom = mix(tone(3.0, 50, 1, glide=28) * envelope(3.0, 0.003, 1.2), band(noise(3.0, rng, "brown"), None, 200) * envelope(3.0, 0.005, 0.9))
    return trim(cave(mix(band(cluster, None, 1200) * 0.3, place(d, swell * 0.5, 0), place(d, boom, 1.6)), 3.0, 0.4, darkness=1800, rng=rng))


def eruption(rng):
    """The mountain opens: a blast and a long, falling rumble."""
    d = 11.0
    blast = mix(tone(d, 55, 1, glide=26) * envelope(d, 0.002, 2.0), band(noise(d, rng), None, 3000) * envelope(d, 0.001, 0.5))
    rumble = band(noise(d, rng, "brown"), None, 150) * envelope(d, 0.3, 4.0, 1.0, curve=3)
    debris = clicks(d, rng, 160, 800, 5000, spread=0.8, level=0.5)
    return trim(cave(mix(blast * 1.1, rumble, debris), 3.0, 0.35, darkness=1600, rng=rng))


def lava_flood(rng):
    """Lava pouring in: a hiss and heavy bubbling."""
    d = 2.8
    hiss = band(noise(d, rng), 1200, 7000) * smooth_random(d, rng, 5) * envelope(d, 0.3, 1.2, 1.0)
    bubbles = mix(*[place(d, tone(0.12, rng.uniform(70, 160), 3, glide=rng.uniform(180, 320)) * envelope(0.12, 0.005, 0.06), rng.uniform(0, 2.4))
                    for _ in range(40)])
    return trim(cave(mix(hiss * 0.35, bubbles * 0.6), 1.5, 0.3, rng=rng))


def dig(rng):
    """A pick into rock, three times, and the rubble shifting."""
    d = 1.4
    parts = []
    for at in (0.0, 0.33, 0.66):
        tock = modes(0.3, rng, [1180, 2870, 4120], [0.05, 0.03, 0.02], [1, 0.6, 0.3], jitter=0.05)
        crack = band(noise(0.3, rng), 600, 5000) * envelope(0.3, 0.001, 0.04)
        parts.append(place(d, mix(tock * 0.6, crack * 0.8), at))
    parts.append(clicks(d, rng, 40, 1200, 6000, spread=1.0, level=0.3))
    return trim(cave(mix(*parts), 1.2, 0.25, rng=rng))


def item_drop(rng):
    """Something set down on stone."""
    d = 0.5
    return trim(cave(mix(modes(d, rng, [180, 420, 900], [0.06, 0.04, 0.02]), clicks(d, rng, 6, 2000, 8000, 0.3, 0.3)), 0.8, 0.2, rng=rng))


def ability(rng):
    """An ability played: a rising sparkle over a soft swell."""
    d = 1.6
    swell = band(noise(d, rng), 1500, 8000) * np.sin(np.linspace(0, np.pi, samples(d))) ** 2
    notes = mix(*[place(d, bell(0.9, f, rng=rng) * 0.2, i * 0.07) for i, f in enumerate((880, 1108.7, 1318.5, 1760, 2217.5))])
    return trim(cave(mix(swell * 0.15, notes), 1.6, 0.35, rng=rng))


def turn_begin(rng):
    """A turn passes: a low, soft gong."""
    # Heard every turn: light and short, not a gong to sit through.
    return trim(cave(bell(3.5, 220, ratios=(1, 2.4, 3.9, 5.1), decay=1.1, rng=rng) * 0.5, 2.0, 0.3, darkness=2000, rng=rng))


def ui_click(rng):
    """A card or a button: a small dry tick."""
    d = 0.15
    return trim(mix(modes(d, rng, [2400, 3900], [0.012, 0.008]) * 0.6, band(noise(d, rng), 1500, 9000) * envelope(d, 0.0005, 0.01) * 0.5))


def ui_card(rng):
    """A card slid across the table."""
    d = 0.35
    return trim(band(noise(d, rng), 1200, 7000) * np.sin(np.linspace(0, np.pi, samples(d))) ** 2 * 0.6)


def victory(rng):
    """The Artefact is out: bells climbing to a major chord."""
    d = 5.0
    notes = [(0.0, 440), (0.18, 554.4), (0.36, 659.3), (0.54, 880)]
    parts = [place(d, bell(3.5, f, rng=rng), at) for at, f in notes]
    parts.append(place(d, mix(*[bell(4.0, f, rng=rng) for f in (440, 554.4, 659.3, 880)]) * 0.6, 0.9))
    return trim(cave(mix(*parts) * 0.5, 2.5, 0.4, rng=rng))


def defeat(rng):
    """Forgotten for ever: a low drone sinking."""
    d = 6.0
    drone = band(tone(d, 110, 12, glide=82.4), None, 700) + band(tone(d, 130.8, 12, glide=98), None, 600) * 0.6
    return trim(cave(drone * envelope(d, 0.8, 3.5, 1.0, curve=3) * 0.35, 3.0, 0.45, darkness=1500, rng=rng))


# ── Ambience and music ───────────────────────────────────────────────────────

def cave_ambience(rng):
    """The temple itself: rock breathing far below, air moving, water dripping."""
    d, fade = 64.0, 4.0
    channels = []
    for side in range(2):
        low = band(noise(d + fade, rng, "brown"), 25, 140) * (0.6 + 0.4 * smooth_random(d + fade, rng, 0.08))
        air = band(noise(d + fade, rng, "pink"), 250, 1400) * (0.2 + 0.8 * smooth_random(d + fade, rng, 0.15) ** 2)
        drips = np.zeros(samples(d + fade))
        for _ in range(16):
            f = rng.uniform(700, 1400)
            drip = tone(0.09, f, 1, glide=f * 1.9) * envelope(0.09, 0.001, 0.04)
            drips += place(d + fade, drip * rng.uniform(0.2, 0.6), rng.uniform(0, d))
        drips = cave(drips, 3.0, 0.8, darkness=5000, rng=rng)[: samples(d + fade)]
        channels.append(loop(mix(low * 0.5, air * 0.12, drips * 0.5), fade))
    return np.stack(channels)


def volcano_rumble(rng):
    """The mountain awake under the temple — its level follows the eruption track."""
    d, fade = 32.0, 3.0
    channels = []
    for side in range(2):
        sub = band(noise(d + fade, rng, "brown"), 18, 75) * (0.5 + 0.5 * smooth_random(d + fade, rng, 0.2))
        churn = band(noise(d + fade, rng, "brown"), 60, 300) * smooth_random(d + fade, rng, 0.6) ** 2
        cracks = np.zeros(samples(d + fade))
        for _ in range(5):
            cracks += place(d + fade, band(noise(0.6, rng), 200, 2500) * envelope(0.6, 0.002, 0.15), rng.uniform(0, d))
        channels.append(loop(mix(sub, churn * 0.5, cave(cracks, 2.0, 0.6, darkness=1500, rng=rng)[: samples(d + fade)] * 0.3), fade))
    return np.stack(channels)


def theme(rng):
    """The menus' music: a dark drone, a slow aeolian drift, far-off bells."""
    d, fade = 72.0, 6.0
    total = d + fade
    n = samples(total)
    t = np.arange(n) / SR
    chords = [(55, 110, 130.8, 164.8), (43.65, 87.3, 130.8, 174.6), (36.7, 73.4, 110, 146.8), (41.2, 82.4, 123.5, 164.8)]
    span = total / len(chords)
    pad = np.zeros(n)
    for i, chord in enumerate(chords):
        weight = np.clip(1 - np.abs((t - (i + 0.5) * span) / (span * 0.75)), 0, 1) ** 1.5
        for f in chord:
            pad += (tone(total, f, 8, detune=0.0015, rng=rng) + tone(total, f * 1.004, 8)) * weight
    cutoff = band(pad, None, 520)
    breathing = 0.75 + 0.25 * np.sin(2 * np.pi * t / 17)
    bells = np.zeros(n)
    for at in np.arange(3.0, d, 7.5):
        f = rng.choice([440, 523.3, 659.3, 587.3, 392])
        bells += place(total, bell(9.0, f, decay=3.0, rng=rng) * 0.25, at + rng.uniform(-1, 1))
    left = loop(cave(cutoff * breathing * 0.08 + bells, 4.0, 0.5, darkness=4000, rng=rng)[:n], fade)
    right = loop(cave(cutoff * breathing * 0.08 + np.roll(bells, samples(0.02)), 4.0, 0.5, darkness=4000, rng=np.random.default_rng(7))[:n], fade)
    return np.stack([left, right])


# ── Output ───────────────────────────────────────────────────────────────────

SFX = {
    "step": (step, 4), "dice_hit": (dice_hit, 3), "dice_settle": (dice_settle, 1),
    "tile_place": (tile_place, 2), "spikes": (spikes, 1), "darts": (darts, 1), "collapse": (collapse, 1),
    "guardian_wake": (guardian_wake, 1), "guardian_step": (guardian_step, 3), "guardian_strike": (guardian_strike, 1),
    "hurt": (hurt, 2), "heal": (heal, 1), "key_pickup": (key_pickup, 1), "key_deposit": (key_deposit, 1),
    "artefact": (artefact, 1), "curse": (curse, 1), "eruption": (eruption, 1), "lava_flood": (lava_flood, 1),
    "dig": (dig, 1), "item_drop": (item_drop, 1), "ability": (ability, 1), "turn_begin": (turn_begin, 1),
    "ui_click": (ui_click, 1), "ui_card": (ui_card, 1), "victory": (victory, 1), "defeat": (defeat, 1),
}

LOOPS = {"ambience/cave": (cave_ambience, -14.0), "ambience/volcano": (volcano_rumble, -8.0), "music/theme": (theme, -10.0)}


def write_ogg(path, data):
    """16-bit WAV through ffmpeg into Ogg Vorbis."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = np.atleast_2d(data)
    pcm = (np.clip(data.T, -1, 1) * 32767).astype(np.int16)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        wav = tmp.name
    with wave.open(wav, "wb") as w:
        w.setnchannels(pcm.shape[1])
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", "5", path], check=True)
    os.remove(wav)


def spectrum(path, picture):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", path, "-lavfi",
                    "showspectrumpic=s=800x300:legend=1:scale=log:fscale=log,format=rgb24", picture], check=True)


def main():
    args = sys.argv[1:]
    spectra = None
    if "--spectra" in args:
        spectra = args[args.index("--spectra") + 1]
        args = [a for a in args if a not in ("--spectra", spectra)]
        os.makedirs(spectra, exist_ok=True)

    for name, (build, variants) in SFX.items():
        if args and name not in args:
            continue
        for v in range(variants):
            rng = np.random.default_rng(sum(map(ord, name)) * 10 + v)
            data = normalise(build(rng), -3.0)
            path = os.path.join(OUT, "sfx", f"{name}_{v}.ogg" if variants > 1 else f"{name}.ogg")
            write_ogg(path, data)
            peak = 20 * np.log10(np.max(np.abs(data)))
            print(f"{os.path.relpath(path, OUT):28s} {len(data) / SR:5.2f} s  peak {peak:5.1f} dB")
            if spectra:
                spectrum(path, os.path.join(spectra, os.path.basename(path).replace(".ogg", ".png")))

    for name, (build, peak_db) in LOOPS.items():
        if args and name.split("/")[-1] not in args:
            continue
        rng = np.random.default_rng(sum(map(ord, name)))
        data = normalise(build(rng), peak_db)
        path = os.path.join(OUT, f"{name}.ogg")
        write_ogg(path, data)
        seam = np.max(np.abs(data[:, 0] - data[:, -1]))
        print(f"{os.path.relpath(path, OUT):28s} {data.shape[1] / SR:5.2f} s  peak {peak_db:5.1f} dB  seam jump {seam:.4f}")
        if spectra:
            spectrum(path, os.path.join(spectra, name.replace("/", "_") + ".png"))


if __name__ == "__main__":
    main()
