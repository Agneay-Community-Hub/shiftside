using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Trailer mode: `Shiftside.exe -trailer <outDir>`
/// Replays solver-generated inputs (Resources/trailer_inputs.txt) through the real game at a fixed
/// 60 fps, saves every frame as a JPG, and rebuilds the game's soundtrack offline into trailer.wav.
public partial class GameManager
{
    public static bool Trailer, Scripted;
    static float sIx;
    static bool sJumpDown, sJumpHeld, sShift;

    class Shot
    {
        public int kind;        // 0 = text card, 1 = gameplay, 2 = end card
        public int level;       // level to play / backdrop level
        public string key;      // input script key
        public float dur;       // card duration
        public string[] lines;
        public float[] at;      // when each line appears
        public float[] flips;   // when the world flips during a card
        public int startWorld;
    }

    string trailerOut;
    int tFrame, warm = 20, shotIdx = -1, shotFrame;
    float shotT, deathT = -1;
    bool finished;
    string[] acts;
    Shot cardShot;
    List<Shot> shots;
    readonly Dictionary<string, string[]> tInputs = new Dictionary<string, string[]>();
    readonly List<float> tWorld = new List<float>();
    readonly List<(int frame, AudioClip clip, float vol, float pitch)> tEvents = new List<(int, AudioClip, float, float)>();

    void TrailerInit()
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "-trailer")
            {
                Trailer = true;
                trailerOut = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : "trailer_frames";
            }
        if (!Trailer) return;

        Scripted = true;
        Time.captureFramerate = 60;
        AudioListener.volume = 0f;
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
        Directory.CreateDirectory(trailerOut);
        Sfx.OnPlay = (c, v, p) => tEvents.Add((tFrame, c, v, p));

        var ta = Resources.Load<TextAsset>("trailer_inputs");
        if (ta != null)
            foreach (var line in ta.text.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) tInputs[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Split(' ');
            }

        shots = new List<Shot>
        {
            Card(0, 5.0f, 0, new[] { "Some bridges are broken.", "...in this world." }, new[] { 0.3f, 2.4f }, new[] { 2.4f }),
            Play(0, "L1"),
            Card(1, 3.4f, 0, new[] { "Two realities. One space.", "SHIFT between them at any moment." }, new[] { 0.2f, 1.5f }, new[] { 1.5f }),
            Play(1, "L2"),
            Play(2, "L3"),
            Card(3, 3.4f, 0, new[] { "But you are not alone.", "...in either world." }, new[] { 0.2f, 1.6f }, new[] { 1.6f }),
            Play(3, "L4"),
            Play(4, "L5"),
            Play(5, "L6"),
            Play(6, "L7"),
            Card(7, 5.6f, 0, new[] { "That broken bridge might exist in the other world.", "Unfortunately, so might the monster standing on it." }, new[] { 0.3f, 2.7f }, new[] { 2.7f }),
            Play(7, "L8death"),
            Play(7, "L8"),
            new Shot { kind = 2, level = 0, dur = 7f, startWorld = 0, flips = new[] { 1.6f, 3.2f, 4.8f } },
        };

        StartCoroutine(Capture());
    }

    static Shot Card(int level, float dur, int world, string[] lines, float[] at, float[] flips) =>
        new Shot { kind = 0, level = level, dur = dur, startWorld = world, lines = lines, at = at, flips = flips };

    static Shot Play(int level, string key) => new Shot { kind = 1, level = level, key = key };

    void TrailerTick(float dt)
    {
        sJumpDown = sShift = sJumpHeld = false;
        sIx = 0;
        if (finished) return;
        if (warm > 0) { warm--; return; }

        if (shotIdx < 0 || ShotDone())
        {
            NextShot();
            if (finished) return;
        }

        var s = shots[shotIdx];
        float prevT = shotT;
        shotT += dt;

        if (s.kind == 1)
        {
            if (state == State.Dying && deathT < 0) deathT = 0;
            if (deathT >= 0) deathT += dt;
            int chunk = shotFrame / 6, k = shotFrame % 6;
            string a = acts != null && chunk < acts.Length ? acts[chunk] : "-";
            sIx = a[0] == 'L' ? -1 : a[0] == 'R' ? 1 : 0;
            bool j = a.IndexOf('J') >= 0;
            sJumpHeld = j;
            sJumpDown = j && k == 0;
            sShift = a.IndexOf('S') >= 0 && k == 0;
            shotFrame++;
        }
        else if (s.flips != null)
        {
            foreach (float f in s.flips)
                if (prevT < f && shotT >= f)
                {
                    world = 1 - world;
                    ringT = 0;
                    ring.transform.position = new Vector3(camPos.x, camPos.y, 0);
                    flash = 0.18f;
                    sfx.Play(world == 1 ? sfx.ToUmbra : sfx.ToSol, 0.55f);
                }
        }
    }

    bool ShotDone()
    {
        var s = shots[shotIdx];
        if (s.kind != 1) return shotT >= s.dur;
        if (state == State.Exiting && stateTimer <= 0.12f) return true;
        if (deathT >= 1.7f) return true;
        if (acts == null || shotFrame > acts.Length * 6 + 180)
        {
            Debug.LogWarning($"[Trailer] shot {s.key} did not finish (desync?)");
            return true;
        }
        return false;
    }

    void NextShot()
    {
        shotIdx++;
        if (shotIdx >= shots.Count) { Finish(); return; }
        var s = shots[shotIdx];
        shotT = 0;
        shotFrame = 0;
        deathT = -1;
        if (s.kind == 1)
        {
            tInputs.TryGetValue(s.key, out acts);
            levelIndex = s.level;
            LoadLevel(false);
            titleTimer = 0;
            fade = 0.7f;
        }
        else
        {
            cardShot = s;
            LoadBackdrop(s.level);
            state = State.Card;
            world = s.startWorld;
            worldT = world;
            fade = 0.8f;
        }
    }

    void DrawCard(float W, float H, float u, Color wc)
    {
        var s = cardShot;
        if (s == null) return;
        float outA = Mathf.Clamp01((s.dur - shotT) / 0.4f);
        if (s.kind == 0)
        {
            Overlay(new Color(0, 0, 0, 0.35f));
            for (int i = 0; i < s.lines.Length; i++)
            {
                float a = Mathf.Clamp01((shotT - s.at[i]) * 2.5f) * outA;
                Color c = i == 0 ? Color.white : wc;
                Text(new Rect(W * 0.03f, H * 0.15f + i * 78 * u, W * 0.94f, 74 * u), s.lines[i], 44 * u, Palette.A(c, a),
                    TextAnchor.MiddleCenter, i == 0 ? FontStyle.Bold : FontStyle.BoldAndItalic);
            }
        }
        else
        {
            Overlay(new Color(0, 0, 0, 0.62f));
            float a1 = Mathf.Clamp01(shotT * 2f) * outA;
            float a2 = Mathf.Clamp01((shotT - 0.8f) * 2f) * outA;
            float a3 = Mathf.Clamp01((shotT - 1.6f) * 2f) * outA;
            Text(new Rect(0, H * 0.20f, W, 170 * u), "SHIFTSIDE", 150 * u, Palette.A(wc, a1), TextAnchor.MiddleCenter, FontStyle.Bold);
            Text(new Rect(W * 0.1f, H * 0.45f, W * 0.8f, 50 * u), "A puzzle-platformer about two overlapping worlds", 30 * u, new Color(1, 1, 1, 0.9f * a2), TextAnchor.MiddleCenter, FontStyle.Italic);
            Text(new Rect(0, H * 0.60f, W, 50 * u), "Play free in your browser on itch.io", 34 * u, new Color(1, 1, 1, a3), TextAnchor.MiddleCenter, FontStyle.Bold);
            Text(new Rect(0, H * 0.69f, W, 40 * u), "Source: github.com/Agneay-Community-Hub/shiftside", 24 * u, new Color(1, 1, 1, 0.75f * a3));
            Text(new Rect(0, H * 0.84f, W, 40 * u), "Made for GitHub Game Off 2026   -   theme: SHIFT", 22 * u, new Color(1, 1, 1, 0.6f * a3));
        }
    }

    IEnumerator Capture()
    {
        var wait = new WaitForEndOfFrame();
        Texture2D tex = null;
        while (!finished)
        {
            yield return wait;
            if (warm > 0 || shotIdx < 0 || finished) continue;
            int w = Screen.width, h = Screen.height;
            if (tex == null || tex.width != w || tex.height != h) tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(false);
            File.WriteAllBytes(Path.Combine(trailerOut, $"f{tFrame:D5}.jpg"), tex.EncodeToJPG(94));
            tWorld.Add(worldT);
            tFrame++;
        }
    }

    void Finish()
    {
        finished = true;
        WriteSoundtrack(Path.Combine(trailerOut, "trailer.wav"));
        File.WriteAllText(Path.Combine(trailerOut, "done.txt"), $"{tFrame} frames {Screen.width}x{Screen.height}");
        Application.Quit();
    }

    void WriteSoundtrack(string path)
    {
        const int SR = 22050;
        int frames = Mathf.Max(1, tWorld.Count);
        int n = (int)(frames / 60f * SR);
        var mix = new float[n];

        float[] Data(AudioClip c)
        {
            var d = new float[c.samples * c.channels];
            c.GetData(d, 0);
            return d;
        }

        var ms = Data(sfx.MusicSolClip);
        var mu = Data(sfx.MusicUmbraClip);
        float total = n / (float)SR;
        for (int i = 0; i < n; i++)
        {
            int f = Mathf.Min(frames - 1, i * 60 / SR);
            float wt = tWorld[f];
            float t = i / (float)SR;
            float env = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((total - t) / 2.5f);
            mix[i] = 0.5f * env * ((1 - wt) * ms[i % ms.Length] + wt * mu[i % mu.Length]);
        }

        var cache = new Dictionary<AudioClip, float[]>();
        foreach (var e in tEvents)
        {
            if (e.clip == null) continue;
            if (!cache.TryGetValue(e.clip, out var d)) cache[e.clip] = d = Data(e.clip);
            int start = (int)(e.frame / 60f * SR);
            for (int k = 0; start + k < n; k++)
            {
                int src = (int)(k * e.pitch);
                if (src >= d.Length) break;
                mix[start + k] += d[src] * e.vol;
            }
        }

        float peak = 0.001f;
        foreach (var v in mix) peak = Mathf.Max(peak, Mathf.Abs(v));
        float g = peak > 0.95f ? 0.95f / peak : 1f;

        using (var bw = new BinaryWriter(File.Create(path)))
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + n * 2);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(SR); bw.Write(SR * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(n * 2);
            foreach (var v in mix) bw.Write((short)Mathf.Clamp(v * g * 32767f, -32768f, 32767f));
        }
    }
}
