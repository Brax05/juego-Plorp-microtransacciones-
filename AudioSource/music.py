import os, sys, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import *
OUT = sys.argv[1]; os.makedirs(OUT, exist_ok=True)
only = sys.argv[2:]
R = np.random.default_rng(3)
TAIL = 4.0

class Song:
    def __init__(self, bpm, bars, beats=4):
        self.bpm, self.bars, self.beats = bpm, bars, beats
        self.beat = 60.0 / bpm
        self.length = bars * beats * self.beat
        self.buf = np.zeros((int((self.length + TAIL) * SR), 2))
        self.dry = np.zeros_like(self.buf)       # parts with little reverb (bass, drums)
    def at(self, bar, beat=0.0): return (bar * self.beats + beat) * self.beat
    def note(self, inst, m, bar, beat, dur_beats, gain=0.5, pan=0.0, dry=False, **kw):
        # Sustained instruments stop at the note's end; the rest ring out.
        tail = {square: .02, triangle: .02, bass: .08, flute: .1, epiano: .4}.get(inst, .6)
        x = inst(midi(m), dur_beats * self.beat + tail, **kw)
        place(self.dry if dry else self.buf, x, self.at(bar, beat) + abs(R.normal(0, .004)), gain, pan)
    def hit(self, x, bar, beat, gain=0.5, pan=0.0, dry=True):
        place(self.dry if dry else self.buf, x, self.at(bar, beat) + abs(R.normal(0, .003)), gain, pan)
    def render(self, name, rev=0.3, rev_s=2.2, damp=5000, loop=True, peak=-1.5):
        wet = reverb(self.buf, rev_s, rev, damp)
        dry = reverb(self.dry, 1.0, .08, 4000)
        n = max(len(wet), len(dry)); mix = np.zeros((n, 2)); mix[:len(wet)] += wet; mix[:len(dry)] += dry
        if loop: mix = make_loop(mix, self.length)
        else: mix = mix[:int((self.length + TAIL) * SR)]
        mix = soft_limit(normalize(mix, 0) * 0.95, 1.1)
        mix = normalize(mix, peak)
        if not loop: mix = fade_edges(mix, 2, 400)
        write_ogg(f'{OUT}/{name}.ogg', mix)
        print(name, round(self.length, 1), 's')

def chord_tones(root, kind):
    q = {'maj': [0, 4, 7], 'min': [0, 3, 7], 'maj7': [0, 4, 7, 11], 'm7': [0, 3, 7, 10], '7': [0, 4, 7, 10],
         'sus4': [0, 5, 7], '6': [0, 4, 7, 9], 'm9': [0, 3, 7, 10, 14], 'add9': [0, 4, 7, 14]}[kind]
    return [root + i for i in q]

def mel(song, inst, notes, start_bar=0, gain=.45, pan=0.0, **kw):
    """notes: list of (beat_from_start, midi or None, dur)"""
    for b, m, d in notes:
        if m is None: continue
        bar, beat = divmod(b, song.beats)
        song.note(inst, m, start_bar + int(bar), beat, d, gain, pan, **kw)

def seq(pattern, beat_len=1.0):
    """'60 62 . 64-' style: tokens = midi or '.', '-' extends previous; returns (beat,midi,dur)"""
    out = []; pos = 0.0
    for tok in pattern.split():
        if set(tok) == {'-'}:            # "-" alone holds the previous note longer
            if out: b, m, d0 = out[-1]; out[-1] = (b, m, d0 + beat_len * len(tok))
            pos += beat_len * len(tok); continue
        n = tok.count('-'); tok = tok.replace('-', '')
        d = beat_len * (1 + n)
        if tok != '.': out.append((pos, int(tok), d))
        pos += d
    return out

# ======================= M-01 Menu / loading =======================
def m01():
    s = Song(78, 16)
    prog = [(60, 'maj7'), (57, 'm7'), (53, 'maj7'), (55, '6')] * 4
    for bar, (r, k) in enumerate(prog):
        tones = chord_tones(r, k)
        s.note(upright, r - 24, bar, 0, 3.5, .55, 0, dry=True)
        s.buf += 0  # pad
        place(s.buf, pad([midi(t) for t in tones], s.beat*4 + 1, 900, 1.0), s.at(bar), .18)
        arp = [tones[0]+12, tones[1]+12, tones[2]+12, tones[-1]+12, tones[2]+12, tones[1]+12, tones[2]+12, tones[-1]+12]
        for i, m in enumerate(arp): s.note(bell, m, bar, i*.5, .5, .16, -.4 + .1*i, tau=.5)
        for b in (0, 2): s.note(kalimba, tones[0] + 12, bar, b, 1.5, .22, .3); s.note(kalimba, tones[2] + 12, bar, b + .5, 1.5, .16, .35)
    melody = seq('76- 79 . 77 76 74- . . 72- 74 76- . . . '
                 '76- 79 . 81 79 77- . . 76- 74 72- . . . ', .5)
    mel(s, bell, melody, 4, .32, .1, tau=1.0)
    mel(s, bell, melody, 12, .3, -.1, tau=1.0)
    s.render('music_menu_loading', .32, 2.6)

# ======================= M-02 Farm day =======================
def m02():
    s = Song(92, 16)
    prog = [(55, 'maj'), (50, 'maj'), (52, 'min'), (48, 'maj'), (55, 'maj'), (48, 'maj'), (50, 'maj'), (55, 'maj')] * 2
    for bar, (r, k) in enumerate(prog):
        t = chord_tones(r, k)
        s.note(bass, r - 12, bar, 0, 1.8, .5, 0, dry=True); s.note(bass, r - 12 + 7, bar, 2, 1.8, .4, 0, dry=True)
        pick = [t[0], t[2], t[1] + 12, t[2], t[0] + 12, t[2], t[1] + 12, t[2]]
        for i, m in enumerate(pick): s.note(pluck_fast, m, bar, i*.5, 1.2, .2, -.35, bright=.55)
        for b in (1, 3): s.note(marimba, t[1] + 12, bar, b, .5, .16, .4); s.note(marimba, t[2] + 12, bar, b, .5, .12, .45)
        for i in range(8): s.hit(shaker(.07, .22 if i % 2 else .14), bar, i*.5, 1, .25)
        if bar % 2 == 0: s.hit(kick(.25)*.6, bar, 0, .45)
    a = seq('74 76 79- 76 74- 71 74- . 72 71 69- 67- . . '
            '74 76 79- 81 79- 76 74- . 72 74 71- 67- . . ', .5)
    mel(s, flute, a, 0, .28, .15)
    b = seq('79 81 83- 81 79- 76 79- . 81 79 76- 74- . . '
            '79 83 86- 83 81- 79 76- . 74 76 79- . - - ', .5)
    mel(s, bell, b, 8, .3, .05, tau=.6)
    mel(s, flute, b, 8, .12, .2)
    s.render('music_farm_day', .25, 1.8)

# ======================= M-03 Farm night =======================
def m03():
    s = Song(68, 16)
    prog = [(53, 'maj7'), (50, 'm9'), (46, 'maj7'), (48, 'sus4')] * 4
    for bar, (r, k) in enumerate(prog):
        t = chord_tones(r, k)
        place(s.buf, sine_pad([midi(x) for x in t[:3]], s.beat*4 + 1.5, 1.2), s.at(bar), .2)
        s.note(upright, r - 12, bar, 0, 3.5, .35, 0, dry=True)
        for i, m in enumerate([t[0], t[1] + 12, t[2] + 12, t[1] + 12]): s.note(epiano, m, bar, i, 1.5, .2, -.2 + .15*i)
        if R.random() < .7: s.note(bell, 84 + R.choice([0, 2, 4, 7, 9]), bar, R.uniform(0, 3.5), 2, .05, R.uniform(-.8, .8), tau=1.5)
    melody = seq('72- 74 76- . 77 76- 74 72- - . '
                 '69- 72 74- . 72 70- 69 67- - . ', 1)
    mel(s, epiano, melody, 4, .33, .1)
    mel(s, epiano, melody, 12, .3, -.05)
    crick = np.zeros((len(s.buf), 2))
    s.render('music_farm_night', .4, 3.0, 3500)

# ======================= M-04 House (lofi) =======================
def m04():
    s = Song(85, 16)
    prog = [(62, 'maj7'), (59, 'm7'), (52, 'm7'), (57, '7')] * 4
    for bar, (r, k) in enumerate(prog):
        t = chord_tones(r, k)
        walk = [r - 24, r - 24 + 4, r - 24 + 7, r - 24 + 9]
        for i, m in enumerate(walk): s.note(upright, m, bar, i, .95, .45, 0, dry=True)
        for b, g in ((0, .17), (1.5, .14), (2.5, .12)):
            for x in t[1:]: s.note(epiano, x, bar, b + R.uniform(0, .03), .9, g, .25)
        for i in range(8):
            sw = .5 * i + (.08 if i % 2 else 0)  # swing
            s.hit(brush(.18, .07 if i % 2 else .04), bar, sw, 1, -.2)
        for b in (1, 3): s.hit(brush(.3, .16), bar, b, 1, .1)
        s.hit(kick(.25)*.4, bar, 0, .4)
    melody = seq('78 76 74- 73 74 76- . 71 74 76 78- 76- . . '
                 '78 81 79 78 76- 74 73- 71 69- 71 73 74- . . ', .5)
    mel(s, pluck_fast, melody, 4, .3, -.15, bright=.3)
    mel(s, bell, melody, 12, .18, .2, tau=.8)
    s.render('music_house', .22, 1.6, 4000)

# ======================= M-05 Arcade / PLORP-OS =======================
def m05():
    s = Song(120, 16)
    prog = [(60, 'maj'), (57, 'min'), (53, 'maj'), (55, 'maj')] * 4
    for bar, (r, k) in enumerate(prog):
        t = chord_tones(r, k)
        for i in range(8): s.note(triangle, (r - 12) if i % 2 == 0 else (r - 12 + 12), bar, i*.5, .45, .35, 0, dry=True)
        arp = [t[0] + 12, t[1] + 12, t[2] + 12, t[1] + 12] * 4
        for i, m in enumerate(arp): s.note(square, m, bar, i*.25, .22, .06, .3, duty=.25, vol=.5)
        for b in (0, 2): s.hit(kick(.18)*.7, bar, b, .5)
        for b in (1, 3): s.hit(chip_snare(.12), bar, b, .35)
        for i in range(8): s.hit(noise_hit(.03, 7000, 12000, .008, .2), bar, i*.5 + .25, 1)
    a = seq('72 . 76 79 . 76 72 . 74 . 77 81 . 79 77 . '
            '76 . 79 84 . 83 81 79 77 . 76 74 72- . . . ', .5)
    mel(s, square, a, 0, .14, -.1, duty=.5, vol=.5)
    mel(s, square, a, 8, .14, -.1, duty=.5, vol=.5)
    harm = [(b, m - 5 if m else None, d) for b, m, d in a]
    mel(s, square, harm, 8, .07, .2, duty=.125, vol=.5)
    s.render('music_arcade', .12, 1.0, 6000)

# ======================= M-06 Pond / fishing =======================
def m06():
    s = Song(70, 16)
    prog = [(57, 'm9'), (53, 'maj7'), (48, 'add9'), (55, 'maj')] * 4
    for bar, (r, k) in enumerate(prog):
        t = chord_tones(r, k)
        place(s.buf, sine_pad([midi(x) for x in t[:3]], s.beat*4 + 1.5, 1.5), s.at(bar), .14)
        harp = [t[0], t[1], t[2], t[0] + 12, t[1] + 12, t[2] + 12, t[1] + 12, t[2]]
        for i, m in enumerate(harp): s.note(pluck_fast, m, bar, i*.5, 2, .17, -.5 + .12*i, bright=.8, tau=1.6)
        s.note(upright, r - 12, bar, 0, 3.5, .3, 0, dry=True)
        for j in range(R.integers(1, 4)): s.note(bell, R.choice([81, 84, 86, 88, 91, 93]), bar, R.uniform(0, 3.8), 2, .05, R.uniform(-.9, .9), tau=1.2)
    a = seq('76- 79 81- . 79 76- 74 72- - . '
            '74- 76 79- . 76 74- 72 69- - . ', 1)
    mel(s, kalimba, a, 4, .3, .15)
    mel(s, flute, a, 12, .15, -.2)
    s.render('music_pond', .38, 3.0, 4500)

# ======================= M-07 Garden =======================
def m07():
    s = Song(80, 16)
    prog = [(53, 'maj'), (55, 'm7'), (57, 'm7'), (58, 'maj'), (53, 'maj'), (58, 'maj'), (55, 'm7'), (60, 'sus4')] * 2
    for bar, (r, k) in enumerate(prog):
        t = chord_tones(r, k)
        for i, m in enumerate([t[0] - 12, t[2] - 12, t[1], t[2] - 12]):
            s.note(pluck_fast, m, bar, i, .6, .2, -.3, bright=.25, tau=.25)  # pizzicato
        s.note(bass, r - 12, bar, 0, 3, .3, 0, dry=True)
        for b in (0.5, 2.5): s.note(bell, t[1] + 24, bar, b, .5, .08, .5, tau=.3)
    a = seq('77 79 81- 84 81 79- 77 79 81 77- . . . '
            '74 77 79- 81 79 77- 76 74 72- . - - - ', .5)
    mel(s, bell, a, 0, .32, .1, tau=1.4)   # music box
    mel(s, bell, a, 8, .28, -.1, tau=1.4)
    mel(s, marimba, [(b, m - 12, d) for b, m, d in a], 8, .14, .3)
    s.render('music_garden', .3, 2.2)

# ======================= M-08 Star trip jingle =======================
def m08():
    s = Song(100, 4)
    for i, m in enumerate([60, 64, 67, 72, 76, 79, 84, 88]): s.note(bell, m, 0, i*.25, 2, .3, -.6 + .15*i, tau=.8)
    place(s.buf, pad([midi(x) for x in (60, 64, 67, 72)], s.beat*12, 1500, 3.0), s.at(0), .35)
    fan = [(2, 67, .5), (2.5, 72, .5), (3, 76, .5), (3.5, 79, 1.5), (5.5, 76, .5), (6, 84, 4)]
    for b, m, d in fan:
        bar, beat = divmod(b, 4)
        s.note(square, m, int(bar), beat, d, .1, .1, duty=.5, vol=.4)
        s.note(bell, m, int(bar), beat, d, .25, -.1, tau=1.2)
    s.note(bass, 36, 1, 2, 6, .45, 0, dry=True)
    for k in range(20): s.note(bell, int(R.choice([84, 88, 91, 96, 100])), 2, R.uniform(0, 6), 1, .07, R.uniform(-1, 1), tau=.4)
    s.render('music_star_trip_jingle', .35, 2.5, loop=False)

tracks = {'m01': m01, 'm02': m02, 'm03': m03, 'm04': m04, 'm05': m05, 'm06': m06, 'm07': m07, 'm08': m08}
for k, f in tracks.items():
    if not only or k in only: f()
