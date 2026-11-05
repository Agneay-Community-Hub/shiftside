using System;
using UnityEngine;

/// Procedural audio: every sound effect and both music layers are synthesised at startup.
/// The two music loops share tempo and length; shifting worlds crossfades between them.
public class Sfx
{
    const int SR = 22050;
    readonly AudioSource[] pool;
    int next;
    readonly System.Random rng = new System.Random(1337);

    public readonly AudioClip Jump, Land, ToSol, ToUmbra, Denied, Death, Win, ShardGet, Checkpoint;
    readonly AudioSource musicSol, musicUmbra;

    public Sfx(GameObject host)
    {
        pool = new AudioSource[10];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = host.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
        }

        Jump = Sweep("jump", 0.13f, 300, 620, 0.22f, 1);
        Land = Sweep("land", 0.07f, 120, 50, 0.35f, 0);
        ToUmbra = ShiftClip("toUmbra", 880, 196);
        ToSol = ShiftClip("toSol", 196, 880);
        Denied = Make("denied", 0.2f, t => 0.18f * (Wave(1, t * 110) + Wave(1, t * 117)) * 0.5f * (1 - t / 0.2f));
        Death = DeathClip();
        Win = Notes("win", new[] { 72, 76, 79, 84, 88 }, 0.09f, 0.6f);
        ShardGet = Notes("shard", new[] { 88, 95 }, 0.07f, 0.35f);
        Checkpoint = Notes("checkpoint", new[] { 67, 74, 79 }, 0.08f, 0.4f);

        // Sol: warm, major, brisk plucked arpeggio.  Umbra: same roots, minor, slow bells.
        var solChords = new[] { new[] { 60, 64, 67, 71 }, new[] { 57, 60, 64, 67 }, new[] { 53, 57, 60, 64 }, new[] { 55, 59, 62, 64 } };
        var umbChords = new[] { new[] { 60, 63, 67, 74 }, new[] { 56, 60, 63, 67 }, new[] { 53, 56, 60, 63 }, new[] { 55, 60, 62, 65 } };
        var bass = new[] { 36, 33, 29, 31 };
        var umbBass = new[] { 36, 32, 29, 31 };

        musicSol = host.AddComponent<AudioSource>();
        musicSol.clip = Music("musicSol", solChords, bass, false);
        musicUmbra = host.AddComponent<AudioSource>();
        musicUmbra.clip = Music("musicUmbra", umbChords, umbBass, true);
        foreach (var m in new[] { musicSol, musicUmbra })
        {
            m.loop = true;
            m.volume = 0f;
            m.playOnAwake = false;
            m.Play();
        }
    }

    /// Optional observer (used by trailer mode to rebuild the soundtrack offline).
    public static Action<AudioClip, float, float> OnPlay;
    public AudioClip MusicSolClip => musicSol.clip;
    public AudioClip MusicUmbraClip => musicUmbra.clip;

    public void Play(AudioClip c, float vol = 1f, float pitch = 1f)
    {
        OnPlay?.Invoke(c, vol, pitch);
        var s = pool[next];
        next = (next + 1) % pool.Length;
        s.clip = c;
        s.volume = vol;
        s.pitch = pitch;
        s.Play();
    }

    public void SetMusic(float worldT, float volume)
    {
        musicSol.volume = volume * (1f - worldT);
        musicUmbra.volume = volume * worldT;
        if (musicUmbra.isPlaying && musicSol.isPlaying && Mathf.Abs(musicSol.timeSamples - musicUmbra.timeSamples) > 2000)
            musicUmbra.timeSamples = musicSol.timeSamples;
    }

    // ---------- synthesis helpers ----------

    static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    /// 0 sine, 1 square, 2 triangle, 3 saw. `ph` is in cycles.
    static float Wave(int w, double ph)
    {
        double p = ph - Math.Floor(ph);
        switch (w)
        {
            case 0: return (float)Math.Sin(2 * Math.PI * p);
            case 1: return p < 0.5 ? 1f : -1f;
            case 2: return (float)(4 * Math.Abs(p - 0.5) - 1);
            default: return (float)(2 * p - 1);
        }
    }

    static AudioClip Make(string name, float dur, Func<float, float> f)
    {
        int n = Mathf.CeilToInt(dur * SR);
        var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)SR), -1f, 1f);
        var c = AudioClip.Create(name, n, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    static AudioClip Sweep(string name, float dur, float f0, float f1, float vol, int wave)
    {
        double ph = 0;
        return Make(name, dur, t =>
        {
            float k = t / dur;
            ph += Mathf.Lerp(f0, f1, k) / SR;
            float env = (1 - k) * (1 - k) * Mathf.Min(1f, t * 300f);
            return vol * env * Wave(wave, ph);
        });
    }

    AudioClip ShiftClip(string name, float f0, float f1)
    {
        double a = 0, b = 0;
        float lp = 0;
        const float dur = 0.42f;
        return Make(name, dur, t =>
        {
            float k = t / dur;
            float f = Mathf.Lerp(f0, f1, k * k * (3 - 2 * k));
            a += f / SR;
            b += f * 1.5f / SR;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            lp += (noise - lp) * 0.08f;
            float env = Mathf.Sin(Mathf.PI * k);
            return env * (0.22f * Wave(0, a) + 0.1f * Wave(0, b) + 0.5f * lp);
        });
    }

    AudioClip DeathClip()
    {
        double ph = 0;
        float lp = 0;
        const float dur = 0.6f;
        return Make("death", dur, t =>
        {
            float k = t / dur;
            ph += Mathf.Lerp(420, 50, k) / SR;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            lp += (noise - lp) * 0.3f;
            float env = Mathf.Pow(1 - k, 2f);
            return env * (0.25f * Wave(1, ph) * 0.6f + 0.45f * lp);
        });
    }

    static AudioClip Notes(string name, int[] notes, float step, float vol)
    {
        float dur = step * notes.Length + 0.35f;
        return Make(name, dur, t =>
        {
            float s = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                float lt = t - i * step;
                if (lt < 0) continue;
                float env = Mathf.Exp(-lt * 9f) * Mathf.Min(1f, lt * 400f);
                s += Wave(2, lt * Hz(notes[i])) * env;
            }
            return s * vol * 0.6f;
        });
    }

    static AudioClip Music(string name, int[][] chords, int[] bass, bool dark)
    {
        const float chordDur = 4f;
        int n = (int)(chordDur * chords.Length * SR);
        var d = new float[n];
        int len = (int)(chordDur * SR);

        for (int c = 0; c < chords.Length; c++)
        {
            int start = c * len;

            // Pad: overlaps 1s into the next chord, fading in/out, wrapped so the loop is seamless.
            int padLen = len + SR;
            foreach (int m in chords[c])
            {
                double i1 = Hz(m) / SR, i2 = Hz(m) * 1.004 / SR, p1 = 0, p2 = 0;
                for (int i = 0; i < padLen; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Clamp01(t / 0.9f) * Mathf.Clamp01((padLen - i) / (float)SR);
                    p1 += i1; p2 += i2;
                    float s = dark
                        ? 0.6f * Wave(0, p1) + 0.6f * Wave(0, p2 * 0.5)
                        : 0.5f * Wave(2, p1) + 0.5f * Wave(0, p2);
                    d[(start + i) % n] += s * env * 0.045f;
                }
            }

            // Bass pulse every 2 seconds.
            double pb = 0, ib = Hz(bass[c]) / SR;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float beat = t % 2f;
                float env = Mathf.Exp(-beat * (dark ? 0.9f : 1.6f)) * Mathf.Clamp01(beat * 40f);
                pb += ib;
                d[(start + i) % n] += Wave(0, pb) * env * 0.16f;
            }

            // Arpeggio.
            float step = dark ? 0.5f : 0.25f;
            int count = (int)(chordDur / step);
            int[] pat = dark ? new[] { 0, 2, 1, 3, 2, 0, 3, 1 } : new[] { 0, 1, 2, 3, 2, 1, 2, 3, 0, 1, 2, 3, 1, 3, 2, 1 };
            for (int k = 0; k < count; k++)
            {
                if (dark && k % 4 == 3) continue;
                int note = chords[c][pat[k % pat.Length] % chords[c].Length] + 12;
                if (!dark && k % 8 == 7) note += 12;
                double inc = Hz(note) / SR, p = 0;
                int s0 = start + (int)(k * step * SR);
                int L = (int)((dark ? 2.2f : 0.45f) * SR);
                for (int i = 0; i < L; i++)
                {
                    float t = i / (float)SR;
                    float env = Mathf.Exp(-t * (dark ? 2.4f : 9f)) * Mathf.Clamp01(t * 300f);
                    p += inc;
                    float s = dark ? Wave(0, p) + 0.35f * Wave(0, p * 2.76) : Wave(2, p);
                    d[(s0 + i) % n] += s * env * (dark ? 0.065f : 0.075f);
                }
            }
        }

        float max = 0.0001f;
        for (int i = 0; i < n; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
        float g = 0.85f / max;
        for (int i = 0; i < n; i++) d[i] *= g;

        var clip = AudioClip.Create(name, n, 1, SR, false);
        clip.SetData(d, 0);
        return clip;
    }
}
