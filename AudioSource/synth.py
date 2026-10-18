"""Small procedural synth for Plorp's farm: instruments, reverb, SFX helpers, writers."""
import numpy as np
from scipy import signal
import subprocess, os

SR = 44100
rng = np.random.default_rng(7)

def t_(dur): return np.arange(int(dur * SR)) / SR

def env_adsr(n, a=0.005, d=0.1, s=0.6, r=0.2, sus_time=None):
    a_n, d_n, r_n = int(a*SR), int(d*SR), int(r*SR)
    if sus_time is None: sus_n = max(0, n - a_n - d_n - r_n)
    else: sus_n = int(sus_time*SR)
    e = np.concatenate([np.linspace(0,1,max(a_n,1)), np.linspace(1,s,max(d_n,1)), np.full(sus_n, s), np.linspace(s,0,max(r_n,1))])
    if len(e) < n: e = np.pad(e, (0, n-len(e)))
    return e[:n]

def expdec(n, tau): return np.exp(-np.arange(n)/SR/tau)

def midi(m): return 440.0 * 2 ** ((m - 69) / 12)

def lowpass(x, fc, order=2):
    b, a = signal.butter(order, min(fc/(SR/2), .99), 'low'); return signal.lfilter(b, a, x)
def highpass(x, fc, order=2):
    b, a = signal.butter(order, max(fc/(SR/2), .0005), 'high'); return signal.lfilter(b, a, x)
def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo/(SR/2), min(hi/(SR/2), .99)], 'band'); return signal.lfilter(b, a, x)

# ---------------- instruments (mono, return array) ----------------
def pluck(f, dur, bright=0.5, decay=0.996):
    """Karplus-Strong guitar / ukulele / harp."""
    n = int(dur*SR); p = max(2, int(SR / f))
    buf = rng.uniform(-1, 1, p); buf = lowpass(buf, 2000 + 6000*bright, 1)
    out = np.zeros(n); idx = 0
    for i in range(n):
        v = buf[idx]; nxt = buf[(idx+1) % p]
        buf[idx] = decay * 0.5 * (v + nxt); out[i] = v; idx = (idx+1) % p
    out *= env_adsr(n, .001, .01, 1, .05)
    return out

def pluck_fast(f, dur, bright=0.5, tau=0.9):
    """Cheaper plucked string: decaying harmonics (no python loop)."""
    t = t_(dur); out = np.zeros_like(t)
    for h in range(1, 9):
        amp = (1/h**(1.6-bright)) * np.exp(-t*(1.2 + h*0.9)/tau)
        out += amp*np.sin(2*np.pi*f*h*t + rng.uniform(0, 6.28))
    out += 0.02*rng.standard_normal(len(t))*np.exp(-t*80)  # pick noise
    return out * env_adsr(len(t), .002, .01, 1, .05)

def marimba(f, dur):
    t = t_(dur)
    out = np.sin(2*np.pi*f*t)*np.exp(-t*5) + 0.35*np.sin(2*np.pi*f*4*t)*np.exp(-t*14) + 0.15*np.sin(2*np.pi*f*9.9*t)*np.exp(-t*30)
    return out * env_adsr(len(t), .002, .01, 1, .03)

def bell(f, dur, tau=1.2):
    """Celesta / glockenspiel / music box."""
    t = t_(dur)
    out = np.sin(2*np.pi*f*t)*np.exp(-t/tau) + 0.5*np.sin(2*np.pi*f*2.0*t)*np.exp(-t/(tau*.5)) \
        + 0.3*np.sin(2*np.pi*f*2.76*t)*np.exp(-t/(tau*.35)) + 0.12*np.sin(2*np.pi*f*5.4*t)*np.exp(-t/(tau*.2))
    return out * env_adsr(len(t), .001, .01, 1, .02)

def kalimba(f, dur):
    t = t_(dur)
    out = np.sin(2*np.pi*f*t)*np.exp(-t*3.5) + 0.25*np.sin(2*np.pi*f*5.95*t)*np.exp(-t*25)
    out += 0.08*rng.standard_normal(len(t))*np.exp(-t*300)
    return out * env_adsr(len(t), .001, .01, 1, .03)

def epiano(f, dur, vel=1.0):
    """Felt / electric piano: FM bell-ish with soft attack."""
    t = t_(dur)
    mod = np.sin(2*np.pi*f*1.0*t) * (1.2*np.exp(-t*3)+0.2)
    out = np.sin(2*np.pi*f*t + mod) * np.exp(-t*1.6) + 0.15*np.sin(2*np.pi*f*2*t)*np.exp(-t*3)
    return lowpass(out * env_adsr(len(t), .008, .05, 1, .15), 3500) * vel

def pad(freqs, dur, bright=1200, att=0.8):
    t = t_(dur); out = np.zeros_like(t)
    for f in freqs:
        for det in (-0.12, 0, 0.11):
            ff = f * 2**(det/12)
            out += signal.sawtooth(2*np.pi*ff*t + rng.uniform(0, 6.28)) * 0.3
    out = lowpass(out, bright, 2)
    return out * env_adsr(len(t), att, .3, .8, min(1.0, dur*.4), None) / max(1, len(freqs))

def sine_pad(freqs, dur, att=1.0):
    t = t_(dur); out = np.zeros_like(t)
    for f in freqs:
        out += np.sin(2*np.pi*f*t) + 0.3*np.sin(2*np.pi*f*2.003*t + 1)
    vib = 1 + 0.15*np.sin(2*np.pi*0.3*t)
    return out * vib * env_adsr(len(t), att, .4, .8, min(1.2, dur*.4)) / max(1, len(freqs))

def bass(f, dur):
    t = t_(dur)
    out = np.sin(2*np.pi*f*t) + 0.3*np.sin(2*np.pi*2*f*t) + 0.1*signal.sawtooth(2*np.pi*f*t)
    return lowpass(out, 900) * env_adsr(len(t), .01, .1, .7, .08)

def upright(f, dur):
    t = t_(dur)
    out = (np.sin(2*np.pi*f*t) + 0.45*np.sin(2*np.pi*2*f*t) + 0.2*np.sin(2*np.pi*3*f*t)) * np.exp(-t*2.2)
    out += 0.05*rng.standard_normal(len(t))*np.exp(-t*90)
    return lowpass(out, 1200) * env_adsr(len(t), .004, .05, 1, .06)

def flute(f, dur):
    t = t_(dur)
    vib = np.sin(2*np.pi*5*t) * 0.004 * np.clip(t*2, 0, 1)
    ph = 2*np.pi*f*np.cumsum(1+vib)/SR
    out = np.sin(ph) + 0.25*np.sin(2*ph) + 0.08*np.sin(3*ph)
    breath = bandpass(rng.standard_normal(len(t)), f*0.8, f*3) * 0.08
    return (out + breath) * env_adsr(len(t), .06, .1, .8, .12)

def square(f, dur, duty=0.5, vol=0.5):
    t = t_(dur)
    return signal.square(2*np.pi*f*t, duty) * vol * env_adsr(len(t), .002, .05, .7, .03)

def triangle(f, dur, vol=0.6):
    t = t_(dur)
    return signal.sawtooth(2*np.pi*f*t, 0.5) * vol * env_adsr(len(t), .002, .02, .9, .02)

def noise_hit(dur, lo, hi, tau, vol=1.0):
    n = int(dur*SR)
    return bandpass(rng.standard_normal(n), lo, hi) * expdec(n, tau) * vol

def kick(dur=0.3):
    t = t_(dur); f = 120*np.exp(-t*30) + 45
    return np.sin(2*np.pi*np.cumsum(f)/SR) * np.exp(-t*12)

def chip_snare(dur=0.15):
    return noise_hit(dur, 1500, 8000, 0.05) * 0.8

def shaker(dur=0.08, vol=0.25):
    return noise_hit(dur, 5000, 12000, 0.02, vol)

def brush(dur=0.25, vol=0.25):
    n = int(dur*SR); e = env_adsr(n, .04, .05, .5, .1)
    return bandpass(rng.standard_normal(n), 2000, 9000) * e * vol

# ---------------- mixing / fx ----------------
def place(buf, x, start, gain=1.0, pan=0.0):
    """buf: (n,2). pan -1..1"""
    s = int(start*SR)
    if s < 0: x = x[-s:]; s = 0
    if s >= len(buf) or len(x) == 0: return
    e = min(len(buf), s + len(x)); x = x[:e-s] * gain
    l = np.cos((pan+1)*np.pi/4); r = np.sin((pan+1)*np.pi/4)
    buf[s:e, 0] += x * l; buf[s:e, 1] += x * r

def reverb_ir(seconds=2.0, damp=4000, seed=1):
    r = np.random.default_rng(seed); n = int(seconds*SR)
    ir = np.zeros((n, 2))
    for c in range(2):
        nz = r.standard_normal(n) * np.exp(-np.arange(n)/SR * (6.9/seconds))
        ir[:, c] = lowpass(nz, damp, 1)
    ir[0] = 0
    return ir / np.sqrt((ir**2).sum(axis=0))

def reverb(stereo, seconds=2.0, wet=0.25, damp=4000):
    ir = reverb_ir(seconds, damp)
    out = np.zeros((len(stereo)+len(ir)-1, 2))
    for c in range(2): out[:, c] = signal.fftconvolve(stereo[:, c], ir[:, c])
    dry = np.pad(stereo, ((0, len(ir)-1), (0, 0)))
    return dry*(1-wet) + out*wet*1.6

def make_loop(stereo_with_tail, loop_len):
    """Wrap everything after loop_len back to the start so the loop is seamless."""
    n = int(loop_len*SR); out = stereo_with_tail[:n].copy()
    tail = stereo_with_tail[n:]
    k = 0
    while k < len(tail):
        seg = tail[k:k+n]; out[:len(seg)] += seg; k += n
    return out

def normalize(x, peak_db=-1.0):
    m = np.max(np.abs(x)) or 1.0
    return x / m * 10**(peak_db/20)

def soft_limit(x, drive=1.2): return np.tanh(x*drive)/np.tanh(drive)

def fade_edges(x, ms_in=2, ms_out=10):
    n_in, n_out = int(ms_in/1000*SR), int(ms_out/1000*SR)
    x = x.copy()
    if x.ndim == 1:
        x[:n_in] *= np.linspace(0, 1, n_in); x[-n_out:] *= np.linspace(1, 0, n_out)
    else:
        x[:n_in] *= np.linspace(0, 1, n_in)[:, None]; x[-n_out:] *= np.linspace(1, 0, n_out)[:, None]
    return x

def trim_silence_start(x, thr=0.002):
    a = np.abs(x if x.ndim == 1 else x.max(axis=1)); i = np.argmax(a > thr)
    return x[i:]

def write_wav(path, x):
    from scipy.io import wavfile
    x = np.clip(x, -1, 1); wavfile.write(path, SR, (x*32767).astype(np.int16))

def write_ogg(path, stereo, quality=6):
    tmp = path + '.tmp.wav'; write_wav(tmp, stereo)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', tmp, '-c:a', 'libvorbis', '-q:a', str(quality), path], check=True)
    os.remove(tmp)
