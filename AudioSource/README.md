# Generadores de audio (Plorp Granja Idle)

Los sonidos y la música de `Assets/Resources/Audio/` salen de estos scripts (síntesis por código, sin IA).
Sirven para ajustar o regenerar; para la versión final puedes reemplazar cualquier archivo por uno hecho con
Suno, Udio, ElevenLabs, etc., **manteniendo el nombre**.

```
pip install numpy scipy        # y ffmpeg instalado
python AudioSource/sfx.py   salida_sfx      # efectos (wav mono) y ambientes en loop (wav estéreo)
python AudioSource/music.py salida_musica   # 8 pistas .ogg (todas o: python music.py salida m02 m05)
```

- `synth.py`: instrumentos (guitarra/ukelele, marimba, campanas/celesta, kalimba, piano suave, pads, bajo,
  contrabajo, flauta, chiptune), reverb y bucles sin corte.
- `music.py`: cada pista (M-01 a M-08) con su tempo, acordes y melodía.
- `sfx.py`: cada efecto del documento de diseño sonoro y los extra que usa el juego.
