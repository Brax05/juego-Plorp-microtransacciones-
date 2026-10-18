import os, sys, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import *
OUT = sys.argv[1]; os.makedirs(OUT, exist_ok=True)
R = np.random.default_rng(42)

def buf(d): return np.zeros(int(d*SR))
def add(b, x, at=0.0, g=1.0):
    s = int(at*SR); e = min(len(b), s+len(x)); b[s:e] += x[:e-s]*g
def sweep_noise(dur, f0, f1, q=3.0):
    n = int(dur*SR); x = R.standard_normal(n); out = np.zeros(n); seg = 512
    for i in range(0, n, seg):
        f = f0 + (f1-f0)*(i/n); lo, hi = f/(1+1/q), f*(1+1/q)
        out[i:i+seg] = bandpass(x[max(0,i-2048):i+seg], lo, min(hi, 20000))[-len(x[i:i+seg]):]
    return out
def glide(dur, f0, f1, shape=1.0):
    t = t_(dur); f = f0 + (f1-f0)*(t/dur)**shape
    return np.sin(2*np.pi*np.cumsum(f)/SR)
def save(name, x, rev=0.0, rev_s=0.8, loop=False):
    if rev > 0:
        st = reverb(np.stack([x, x], 1), rev_s, rev, 6000)[:, 0]
        x = st if not loop else x
    x = normalize(x if loop else trim_silence_start(x), -1.0)
    if not loop:
        # cut the reverb tail once it falls under -48 dB, keep 60 ms after
        a = np.abs(x); idx = np.where(a > 10**(-48/20))[0]
        if len(idx): x = x[:min(len(x), idx[-1] + int(.06*SR))]
        rms = np.sqrt((x**2).mean())
        if rms > .2: x *= .2/rms          # buzzy/square sounds: same loudness as the rest
        x = fade_edges(x, 1, 15)
    write_wav(f'{OUT}/{name}.wav', x)

# ---------- A. core / rewards / plant care ----------
b = buf(0.5)
for f, at, g in [(2093, 0, 1), (3136, .03, .6), (2637, .06, .5)]: add(b, bell(f, .45, .25), at, g)
for k in range(6): add(b, bell(R.uniform(4000, 7000), .12, .04), R.uniform(.02, .3), .25)
save('sfx_gem_drop', b, .2)

b = buf(0.45)
for i, m in enumerate([84, 88, 91, 96]): add(b, bell(midi(m), .35, .2), i*.045, .8)
add(b, sine_pad([midi(96)], .4, .02)*.2, .1)
save('sfx_gem_pickup', b, .25)

def coin(pitch=1.0):
    b = buf(.22)
    for f, at in [(1975*pitch, 0), (2637*pitch, .05)]:
        t = t_(.17); x = (np.sin(2*np.pi*f*t) + .4*np.sin(2*np.pi*f*2.4*t)) * np.exp(-t*22)
        add(b, x, at)
    return b
save('sfx_coin', coin(), .1)
b = buf(1.3)
for k in range(22): add(b, coin(R.uniform(.85, 1.25))*R.uniform(.4, 1), R.uniform(0, .9)**1.3, 1)
add(b, noise_hit(1.0, 3000, 9000, .4, .08), 0)
save('sfx_coin_shower', b, .15)

b = buf(.55)
add(b, marimba(midi(79), .5), 0); add(b, marimba(midi(86), .45), .09)
add(b, glide(.08, 500, 1400)*np.exp(-t_(.08)*30)*.3, .0)  # bubble
save('sfx_harvest_ready', b, .2)

b = buf(.3)
t = t_(.18); add(b, glide(.18, 650, 140, .6)*np.exp(-t*18), 0)
add(b, lowpass(R.standard_normal(int(.12*SR)), 1500)*expdec(int(.12*SR), .03)*.5, 0.01)
save('sfx_harvest_pop', b)

b = buf(.32)
n = int(.28*SR); add(b, sweep_noise(.28, 2200, 7000, 2.5)*env_adsr(n, .06, .05, .7, .15), 0, .9)
add(b, noise_hit(.03, 4000, 12000, .006, .6), .2)
save('sfx_cut', b)

b = buf(.65)
n = int(.55*SR); add(b, lowpass(highpass(R.standard_normal(n), 2200), 7000)*env_adsr(n, .03, .1, .5, .3)*.3, 0)
for k in range(9):
    f = R.uniform(900, 2600); tt = t_(.05)
    add(b, np.sin(2*np.pi*np.cumsum(f*(1+3*tt))/SR)*np.exp(-tt*70)*.5, R.uniform(.08, .55))
save('sfx_water', b, .12)

b = buf(1.1)
add(b, sine_pad([midi(69), midi(73), midi(76)], 1.0, .25)*.6, 0)
t = t_(1.0); add(b, np.sin(2*np.pi*220*t)*env_adsr(len(t), .2, .2, .6, .4)*.25, 0)
for k in range(10): add(b, bell(R.uniform(2500, 5500), .2, .06)*.25, .1 + k*.07)
save('sfx_light', b, .3, 1.2)

b = buf(.65)
n = int(.5*SR); add(b, sweep_noise(.5, 1800, 500, 1.5)*env_adsr(n, .08, .1, .7, .2)*.8, 0)
t = t_(.15); add(b, np.sin(2*np.pi*95*t)*np.exp(-t*25)*.7, .48)
save('sfx_curtain_close', b)

b = buf(.55)
n = int(.45*SR); add(b, sweep_noise(.45, 600, 2600, 1.5)*env_adsr(n, .05, .1, .7, .2)*.8, 0)
save('sfx_curtain_open', b)

# ---------- B. farm life, house, pond ----------
def wind_loop(dur=12.0):
    n = int(dur*SR); x = R.standard_normal(n + SR)
    lfo = .5 + .5*np.sin(2*np.pi*np.arange(n+SR)/SR/ dur * 2)
    out = lowpass(x, 600)*(.4+.6*lfo)
    for k in range(10):
        s = R.uniform(0, dur); out[int(s*SR):int(s*SR)+int(.4*SR)] += bandpass(R.standard_normal(int(.4*SR)), 3000, 8000)*env_adsr(int(.4*SR), .1, .1, .5, .2)*.08
    st = np.stack([out, np.roll(out, 900)], 1)
    return make_loop(st, dur)
w = wind_loop(); write_wav(f'{OUT}/amb_wind_loop.wav', normalize(w, -6))

b = buf(1.0)
for at in (0, .28):
    t = t_(.2); f = 620 - 180*(t/.2); x = signal.sawtooth(2*np.pi*np.cumsum(f)/SR)*env_adsr(len(t), .01, .05, .7, .08)
    add(b, bandpass(x, 700, 2600), at, .8)
for k in range(10): add(b, noise_hit(.04, 300, 2000, .015, .5), .45 + k*.05)
save('sfx_crow', b, .1)

b = buf(.9)
for k in range(5): add(b, noise_hit(.03, 300, 1500, .01, .4), k*.07)
t = t_(.22); f = 900 + 900*np.sin(np.pi*t/.22); add(b, np.sin(2*np.pi*np.cumsum(f)/SR)*env_adsr(len(t), .01, .05, .8, .08)*.7, .38)
for k in range(8): add(b, noise_hit(.025, 400, 2500, .008, .35), .62 + k*.035)
save('sfx_fox', b, .1)

b = buf(1.4)
for at in (0, .32):
    t = t_(.22); x = signal.square(2*np.pi*340*t)*.5 + signal.square(2*np.pi*428*t)*.5
    add(b, lowpass(x, 2200)*env_adsr(len(t), .01, .03, .8, .05), at, .6)
t = t_(1.4); eng = lowpass(R.standard_normal(len(t)), 200)*(.6+.4*np.sin(2*np.pi*28*t))
add(b, eng*env_adsr(len(t), .1, .2, .6, .4)*.35, 0)
save('sfx_truck_honk', b, .1)

def bees_loop(dur=6.0):
    n = int(dur*SR); st = np.zeros((n+SR, 2))
    for k in range(4):
        t = t_(dur+1); f0 = R.uniform(190, 260)
        f = f0*(1 + .03*np.sin(2*np.pi*R.uniform(3, 6)*t))
        x = signal.sawtooth(2*np.pi*np.cumsum(f)/SR)*(.5+.5*np.sin(2*np.pi*R.uniform(.2, .6)*t + k))
        x = bandpass(x, 200, 2500)
        place(st, x*.25, 0, 1, R.uniform(-.7, .7))
    return make_loop(st, dur)
write_wav(f'{OUT}/amb_bees_loop.wav', normalize(bees_loop(), -6))

b = buf(.4)
t = t_(.2); add(b, glide(.2, 190, 110)*np.exp(-t*20), 0)
for k in range(6): add(b, noise_hit(.02, 1500, 6000, .006, .3), .02 + k*.02)
save('sfx_apple_drop', b, .08)

b = buf(1.7)
t = t_(1.3); pur = lowpass(R.standard_normal(len(t)), 260)*(.5+.5*np.sign(np.sin(2*np.pi*24*t)))
add(b, lowpass(pur, 300)*env_adsr(len(t), .2, .2, .8, .3)*1.2, 0)
t = t_(.35); f = 520 + 420*np.sin(np.pi*np.clip(t/.25, 0, 1))
mrrp = np.sin(2*np.pi*np.cumsum(f)/SR)*(1+.5*np.sin(2*np.pi*30*t))*env_adsr(len(t), .02, .05, .8, .1)
add(b, bandpass(mrrp, 300, 3000)*.6, 1.25)
save('sfx_cat_purr', b, .1)

b = buf(1.0)
n = int(.35*SR); add(b, sweep_noise(.35, 1500, 5000, 3)*env_adsr(n, .05, .05, .7, .15)*.6, 0)
for k in range(5): add(b, noise_hit(.01, 3000, 9000, .003, .3), .35 + k*.03)
t = t_(.25); add(b, glide(.25, 900, 260, .5)*np.exp(-t*14)*.8, .62)
add(b, noise_hit(.25, 800, 5000, .06, .4), .62)
save('sfx_fish_cast', b, .15)

b = buf(.45)
t = t_(.2); add(b, glide(.2, 380, 120, .5)*np.exp(-t*14), 0)
add(b, glide(.12, 600, 1200)*np.exp(-t_(.12)*30)*.4, .07)
add(b, noise_hit(.2, 600, 4000, .05, .35), .0)
save('sfx_fish_bite', b, .1)

# ---------- C. extras used by the game ----------
b = buf(.08); t = t_(.05); add(b, np.sin(2*np.pi*1200*t)*np.exp(-t*90) + .3*np.sin(2*np.pi*2400*t)*np.exp(-t*120), 0); save('sfx_ui_click', b)
b = buf(.3)
for i, f in enumerate([330, 247]): add(b, square(f, .12, .5, .4), i*.11)
save('sfx_ui_error', lowpass(b, 2500))
b = buf(.4)
for i, m in enumerate([72, 76, 79]): add(b, marimba(midi(m), .3), i*.06)
save('sfx_upgrade', b, .15)
b = buf(1.4)
for i, m in enumerate([72, 76, 79, 84]): add(b, bell(midi(m), .6, .3), i*.09, .7); add(b, pluck_fast(midi(m-12), .6, .6), i*.09, .5)
add(b, sine_pad([midi(72), midi(76), midi(79), midi(84)], .9, .05)*.4, .36)
save('sfx_tier_up', b, .3, 1.2)
b = buf(2.2)
for i, (m, d) in enumerate([(72, .15), (76, .15), (79, .15), (84, .5), (83, .15), (84, .8)]):
    at = [0, .15, .3, .45, 1.0, 1.15][i]; add(b, bell(midi(m), d+.5, .3), at, .7); add(b, kalimba(midi(m-12), d+.4), at, .5)
for k in range(8): add(b, bell(R.uniform(3000, 6000), .15, .05)*.2, 1.2 + k*.06)
save('sfx_order_delivered', b, .3, 1.3)
b = buf(1.6)
for i, m in enumerate([67, 72, 76, 79, 84, 88]): add(b, bell(midi(m), .8, .4), i*.07, .6)
save('sfx_achievement', b, .35, 1.4)
b = buf(.25)
add(b, noise_hit(.06, 200, 3000, .015, .8), 0); t = t_(.1); add(b, glide(.1, 300, 80)*np.exp(-t*30)*.6, 0)
save('sfx_bug_squash', b)
b = buf(.7)
for k in range(30): add(b, noise_hit(.015, 2000, 7000, .004, R.uniform(.2, .5)), R.uniform(0, .5))
add(b, sine_pad([midi(76)], .6, .05)*.15, .1)
save('sfx_fertilizer', b, .15)
b = buf(.3)
n = int(.22*SR); add(b, sweep_noise(.22, 3000, 6000, 2)*env_adsr(n, .02, .05, .6, .1)*.6, 0)
add(b, bell(3520, .2, .06)*.4, .12)
save('sfx_leaf_clean', b, .1)
b = buf(.8)
add(b, bell(midi(84), .7, .35), 0); add(b, bell(midi(91), .6, .3), .12, .7)
save('sfx_dish_ready', b, .25)
def meow(base=650, dur=.5):
    t = t_(dur); f = base*(1 + .45*np.sin(np.pi*np.clip(t/dur*1.2, 0, 1)))
    ph = 2*np.pi*np.cumsum(f)/SR
    x = signal.sawtooth(ph)*.5 + np.sin(ph)
    form = bandpass(x, 700, 2600)*0.8 + bandpass(x, 2800, 4200)*.3
    return form*env_adsr(len(t), .04, .1, .7, .15)
b = buf(.6); add(b, meow(), 0); save('sfx_pepito_meow', b, .1)
b = buf(.45)
t = t_(.3); f = 300 + 250*np.sin(np.pi*t/.3); x = np.sin(2*np.pi*np.cumsum(f)/SR)*(1+.8*np.sin(2*np.pi*35*t))*env_adsr(len(t), .01, .05, .8, .1)
add(b, lowpass(x, 1500), 0); add(b, glide(.08, 400, 1200)*np.exp(-t_(.08)*25)*.4, .28)
save('sfx_plorp_voice', b, .15)
b = buf(2.2)
t = t_(2.0); f = 600 + 120*np.sin(2*np.pi*5*t) + 200*np.sin(2*np.pi*.5*t)
add(b, np.sin(2*np.pi*np.cumsum(f)/SR)*env_adsr(len(t), .4, .2, .8, .8)*.6, 0)
save('sfx_ufo_pass', b, .25, 1.0)
b = buf(.9); t = t_(.8); f = 300 + 900*(t/.8)
add(b, np.sin(2*np.pi*np.cumsum(f)/SR)*(.6+.4*np.sin(2*np.pi*18*t))*env_adsr(len(t), .05, .1, .7, .2)*.6, 0)
add(b, sine_pad([midi(76)], .8, .1)*.3, 0)
save('sfx_beam', b, .2)
b = buf(.3); add(b, marimba(midi(81), .25), 0); add(b, coin(1.1)*.4, .02); save('sfx_egg_catch', b, .1)
b = buf(.3); t = t_(.15); add(b, glide(.15, 160, 70)*np.exp(-t*20), 0); add(b, noise_hit(.08, 300, 2500, .02, .6), 0); save('sfx_stone_hit', b)
b = buf(.25); add(b, square(midi(72), .12, .25, .5), 0); save('sfx_countdown', lowpass(b, 3000))
b = buf(.4); add(b, square(midi(84), .3, .25, .5), 0); add(b, square(midi(79), .3, .5, .3), 0); save('sfx_countdown_go', lowpass(b, 3500))
b = buf(1.4)
for i, m in enumerate([72, 76, 79, 84]): add(b, square(midi(m), .16 if i < 3 else .6, .5, .4), i*.14)
add(b, triangle(midi(48), 1.0, .5), 0)
save('sfx_minigame_end', lowpass(b, 4000), .15)
b = buf(1.8)
for i, m in enumerate([67, 71, 74, 79, 83]): add(b, kalimba(midi(m), 1.0), i*.1, .7); add(b, bell(midi(m+12), .8, .3), i*.1, .4)
save('sfx_streak_reward', b, .3, 1.3)

def rain_loop(dur=10.0):
    n = int(dur*SR); st = np.zeros((n+SR, 2))
    for c in range(2):
        st[:n, c] = lowpass(highpass(R.standard_normal(n), 400), 6000)*.25
    for k in range(int(dur*40)):
        place(st, noise_hit(.02, 2000, 9000, .005, R.uniform(.2, .6)), R.uniform(0, dur), 1, R.uniform(-.9, .9))
    return make_loop(st, dur)
write_wav(f'{OUT}/amb_rain_loop.wav', normalize(rain_loop(), -6))

def crickets_loop(dur=8.0):
    n = int(dur*SR); st = np.zeros((n+SR, 2))
    for k in range(5):
        f = R.uniform(3800, 4800); pan = R.uniform(-.8, .8); period = R.uniform(.6, 1.3); start = R.uniform(0, period)
        tt = start
        while tt < dur:
            t = t_(.12); chirp = np.sin(2*np.pi*f*t)*(.5+.5*np.sign(np.sin(2*np.pi*55*t)))*env_adsr(len(t), .005, .02, .8, .03)
            place(st, chirp*.12, tt, 1, pan); tt += period
    for c in range(2): st[:n, c] += lowpass(R.standard_normal(n), 300)*.03
    return make_loop(st, dur)
write_wav(f'{OUT}/amb_night_crickets_loop.wav', normalize(crickets_loop(), -8))

def birds_loop(dur=12.0):
    n = int(dur*SR); st = np.zeros((n+SR, 2))
    for k in range(9):
        at = R.uniform(0, dur); pan = R.uniform(-.9, .9); f0 = R.uniform(2500, 4200)
        for j in range(R.integers(2, 5)):
            t = t_(.09); f = f0*(1 + .3*np.sin(np.pi*t/.09))
            place(st, np.sin(2*np.pi*np.cumsum(f)/SR)*env_adsr(len(t), .01, .02, .7, .03)*.1, at + j*.12, 1, pan)
    return make_loop(st, dur)
write_wav(f'{OUT}/amb_day_birds_loop.wav', normalize(birds_loop(), -8))

def bubbling_loop(dur=4.0):
    n = int(dur*SR); b = np.zeros(n+SR)
    for k in range(int(dur*9)):
        f = R.uniform(250, 700); t = t_(.07); add(b, np.sin(2*np.pi*np.cumsum(f*(1+2*t))/SR)*np.exp(-t*50)*.3, R.uniform(0, dur))
    st = np.stack([b, b], 1)
    return make_loop(st, dur)
write_wav(f'{OUT}/amb_pot_bubbling_loop.wav', normalize(bubbling_loop(), -8))
print('done')
