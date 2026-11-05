using System.Collections.Generic;
using UnityEngine;

/// SHIFTSIDE — a puzzle-platformer about two overlapping realities.
/// Everything (camera, art, audio, levels, UI) is built from code by this component.
public partial class GameManager : MonoBehaviour
{
    enum State { Menu, Playing, Dying, Exiting, Ending, Card }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<GameManager>() == null) new GameObject("Game").AddComponent<GameManager>();
    }

    class Particle { public Transform tr; public SpriteRenderer sr; public Vector2 v; public float life, max, size; }
    class Star { public SpriteRenderer sr; public Vector2 lp; public float phase, depth; }

    State state;
    Camera cam;
    Sfx sfx;
    LevelRuntime lvl;
    readonly Player player = new Player();
    Transform playerTr;
    SpriteRenderer playerSr;

    int levelIndex, world, respawnWorld, activeCheckpoint = -1;
    float worldT, time, shiftCd, stateTimer, fade = 1f, shake, denyFlash, flash, captionTimer, titleTimer, menuTimer;
    Vector2 respawnPos, camPos;
    string caption = "";
    int totalDeaths, levelDeaths, unlocked, menuSel, deniedHints, shiftsThisLevel;
    float runTime;
    bool musicOn = true;
    readonly HashSet<int> pendingShards = new HashSet<int>();

    readonly List<Particle> parts = new List<Particle>();
    readonly Stack<Particle> freeParts = new Stack<Particle>();
    readonly List<Star> stars = new List<Star>();
    SpriteRenderer ring, sun, moon;
    float ringT = -1f;
    Transform bgRoot;
    GUIStyle style;

    static readonly string[] SpikeLines = { "Ouch.", "Pointy.", "Those were real." };
    static readonly string[] CaughtLines = { "Caught.", "It saw you first.", "Wrong place, wrong world." };

    // ------------------------------------------------------------------ setup

    void Awake()
    {
        Application.targetFrameRate = 60;
        TrailerInit();
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, 0, -10);

        sfx = new Sfx(gameObject);
        unlocked = Mathf.Clamp(PlayerPrefs.GetInt("unlocked", 1), 1, Levels.All.Length);
        totalDeaths = PlayerPrefs.GetInt("deaths", 0);

        BuildBackground();

        var pgo = new GameObject("Player");
        playerTr = pgo.transform;
        playerSr = pgo.AddComponent<SpriteRenderer>();
        playerSr.sprite = Art.Player;
        playerSr.sortingOrder = 20;

        var rgo = new GameObject("ShiftRing");
        ring = rgo.AddComponent<SpriteRenderer>();
        ring.sprite = Art.Ring;
        ring.sortingOrder = 40;
        ring.enabled = false;

        EnterMenu();
    }

    void BuildBackground()
    {
        bgRoot = new GameObject("Background").transform;
        bgRoot.SetParent(cam.transform, false);
        bgRoot.localPosition = new Vector3(0, 0, 20);

        sun = BgSprite(Art.Circle, new Vector2(8f, 3.5f), 11f, -30);
        moon = BgSprite(Art.Circle, new Vector2(-8f, 3.5f), 7f, -30);

        var r = new System.Random(42);
        for (int i = 0; i < 110; i++)
        {
            var s = new Star
            {
                lp = new Vector2((float)r.NextDouble() * 48f - 24f, (float)r.NextDouble() * 20f - 10f),
                phase = (float)r.NextDouble() * 10f,
                depth = 0.05f + (float)r.NextDouble() * 0.25f
            };
            s.sr = BgSprite(Art.Pixel, s.lp, 0.6f + (float)r.NextDouble() * 0.9f, -20);
            stars.Add(s);
        }
    }

    SpriteRenderer BgSprite(Sprite sp, Vector2 p, float scale, int order)
    {
        var go = new GameObject("bg");
        go.transform.SetParent(bgRoot, false);
        go.transform.localPosition = p;
        go.transform.localScale = new Vector3(scale, scale, 1);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingOrder = order;
        return sr;
    }

    // ------------------------------------------------------------------ input

    static bool ShiftPressed() => Scripted ? sShift :
        Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) ||
        Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.K) ||
        Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton2);

    static bool JumpPressed() => Scripted ? sJumpDown :
        Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) ||
        Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.JoystickButton0);

    static bool JumpHeld() => Scripted ? sJumpHeld :
        Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ||
        Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.JoystickButton0);

    static bool ConfirmPressed() =>
        Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
        Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton7);

    static float Horizontal()
    {
        if (Scripted) return sIx;
        float h = 0;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1;
        float axis = 0;
        try { axis = Input.GetAxisRaw("Horizontal"); } catch { }
        if (Mathf.Abs(axis) > Mathf.Abs(h)) h = axis;
        return Mathf.Clamp(h, -1, 1);
    }

    // ------------------------------------------------------------------ flow

    void EnterMenu()
    {
        state = State.Menu;
        world = 0;
        LoadBackdrop(0);
        menuSel = Mathf.Clamp(PlayerPrefs.GetInt("last", 0), 0, unlocked - 1);
        fade = 1f;
    }

    void LoadBackdrop(int index)
    {
        lvl?.Destroy();
        lvl = new LevelRuntime(Levels.All[index]);
        lvl.Build(transform);
        playerTr.gameObject.SetActive(false);
        camPos = ClampCam(new Vector2(lvl.W / 2f, lvl.H / 2f));
    }

    void StartLevel(int index)
    {
        levelIndex = index;
        PlayerPrefs.SetInt("last", index);
        runTime = 0;
        LoadLevel(false);
        fade = 1f;
    }

    void LoadLevel(bool fromCheckpoint)
    {
        lvl?.Destroy();
        lvl = new LevelRuntime(Levels.All[levelIndex]);
        lvl.Build(transform);

        if (!fromCheckpoint)
        {
            activeCheckpoint = -1;
            respawnPos = lvl.Start;
            respawnWorld = 0;
            levelDeaths = 0;
            shiftsThisLevel = 0;
            pendingShards.Clear();
            titleTimer = 3f;
        }
        else if (activeCheckpoint >= 0 && activeCheckpoint < lvl.Checkpoints.Count)
        {
            var c = lvl.Checkpoints[activeCheckpoint];
            c.active = true;
            c.world = respawnWorld;
        }
        foreach (var s in lvl.Shards)
            if (pendingShards.Contains(s.id) || HasShard(levelIndex, s.id)) s.taken = true;

        world = respawnWorld;
        worldT = world;
        player.Reset(respawnPos);
        playerTr.gameObject.SetActive(true);
        playerTr.localScale = Vector3.one;
        state = State.Playing;
        shiftCd = 0;
        camPos = ClampCam(player.pos);
    }

    static bool HasShard(int level, int id) => !Trailer && PlayerPrefs.GetInt($"shard_{level}_{id}", 0) == 1;

    static int ShardsOwned()
    {
        int n = 0;
        for (int l = 0; l < Levels.All.Length; l++)
            for (int i = 0; i < 8; i++)
                if (HasShard(l, i)) n++;
        return n;
    }

    void Die(string msg)
    {
        if (state != State.Playing) return;
        state = State.Dying;
        stateTimer = 0.85f;
        levelDeaths++;
        totalDeaths++;
        if (!Trailer) PlayerPrefs.SetInt("deaths", totalDeaths);
        caption = msg;
        captionTimer = 1.8f;
        shake = 0.4f;
        sfx.Play(sfx.Death, 0.8f);
        Burst(player.pos, 26, Color.Lerp(Color.white, Palette.World(world), 0.5f), 7f, 0.9f);
        playerTr.gameObject.SetActive(false);
    }

    void CompleteLevel()
    {
        state = State.Exiting;
        stateTimer = 1.1f;
        sfx.Play(sfx.Win, 0.8f);
        Burst(lvl.ExitPos + Vector2.up, 30, Color.white, 6f, 1.2f);
        if (Trailer) return;
        foreach (int id in pendingShards) PlayerPrefs.SetInt($"shard_{levelIndex}_{id}", 1);
        unlocked = Mathf.Clamp(Mathf.Max(unlocked, levelIndex + 2), 1, Levels.All.Length);
        PlayerPrefs.SetInt("unlocked", unlocked);
        PlayerPrefs.Save();
    }

    void TryShift()
    {
        int target = 1 - world;
        if (lvl.OverlapsSolid(player.Box, target))
        {
            sfx.Play(sfx.Denied, 0.7f);
            denyFlash = 0.35f;
            shake = Mathf.Max(shake, 0.12f);
            if (deniedHints < 3)
            {
                deniedHints++;
                caption = "Something solid is standing there in the other world.";
                captionTimer = 1.6f;
            }
            return;
        }

        world = target;
        shiftCd = 0.12f;
        shiftsThisLevel++;
        sfx.Play(world == 1 ? sfx.ToUmbra : sfx.ToSol, 0.55f);
        ringT = 0;
        ring.transform.position = new Vector3(player.pos.x, player.pos.y, 0);
        flash = 0.18f;
        Burst(player.pos, 10, Palette.World(world), 3.5f, 0.5f);

        if (lvl.MonsterHit(player.Box, world) != null)
            Die("Unfortunately, so was the monster.");
        else if (lvl.SpikeHit(player.Box, world))
            Die("The other world had spikes there.");
    }

    // Go quiet when the window/tab isn't focused, and resume when it comes back.
    void OnApplicationFocus(bool focused) => AudioListener.pause = !focused;
    void OnApplicationPause(bool paused) => AudioListener.pause = paused;

    // ------------------------------------------------------------------ loop

    void Update()
    {
        float dt = Trailer ? 1f / 60f : Mathf.Min(Time.deltaTime, 1f / 30f);
        time += dt;

        if (Trailer) TrailerTick(dt);
        else if (Input.GetKeyDown(KeyCode.M)) musicOn = !musicOn;

        switch (state)
        {
            case State.Card:
                lvl.UpdateMonsters(dt);
                break;
            case State.Menu: UpdateMenu(dt); break;
            case State.Playing: UpdatePlaying(dt); break;
            case State.Dying:
                lvl.UpdateMonsters(dt);
                stateTimer -= dt;
                if (stateTimer <= 0) LoadLevel(true);
                break;
            case State.Exiting:
                stateTimer -= dt;
                player.pos = Vector2.Lerp(player.pos, lvl.ExitPos + new Vector2(0, 0.9f), 1 - Mathf.Exp(-dt * 8));
                playerTr.localScale = Vector3.one * Mathf.Clamp01(stateTimer);
                if (stateTimer <= 0 && !Trailer)
                {
                    if (levelIndex + 1 < Levels.All.Length) { levelIndex++; PlayerPrefs.SetInt("last", levelIndex); LoadLevel(false); }
                    else EnterEnding();
                }
                break;
            case State.Ending:
                menuTimer += dt;
                if (menuTimer > 1.5f && (ConfirmPressed() || Input.GetKeyDown(KeyCode.Escape))) EnterMenu();
                break;
        }

        worldT = Mathf.MoveTowards(worldT, world, dt / 0.2f);
        lvl?.UpdateVisuals(worldT, time);
        UpdatePlayerVisual(dt);
        UpdateCamera(dt);
        UpdateBackground();
        UpdateFx(dt);
        sfx.SetMusic(worldT, musicOn ? 0.45f : 0f);

        fade = state == State.Exiting && stateTimer < 0.5f
            ? Mathf.MoveTowards(fade, 1f, dt * 3f)
            : Mathf.MoveTowards(fade, 0f, dt * 2f);
        captionTimer -= dt;
        titleTimer -= dt;
        denyFlash = Mathf.MoveTowards(denyFlash, 0, dt);
        flash = Mathf.MoveTowards(flash, 0, dt);
    }

    void UpdateMenu(float dt)
    {
        lvl.UpdateMonsters(dt);
        menuTimer += dt;
        if (menuTimer > 2.4f)
        {
            menuTimer = 0;
            world = 1 - world;
        }
        int max = Mathf.Min(unlocked, Levels.All.Length) - 1;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) menuSel = Mathf.Max(0, menuSel - 1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) menuSel = Mathf.Min(max, menuSel + 1);
        if (ConfirmPressed() && fade < 0.5f) StartLevel(menuSel);
    }

    void EnterEnding()
    {
        state = State.Ending;
        menuTimer = 0;
        world = 1;
        LoadBackdrop(Levels.All.Length - 1);
        PlayerPrefs.Save();
    }

    void UpdatePlaying(float dt)
    {
        runTime += dt;
        shiftCd -= dt;
        if (!Scripted && Input.GetKeyDown(KeyCode.Escape)) { EnterMenu(); return; }
        if (!Scripted && Input.GetKeyDown(KeyCode.R)) { LoadLevel(activeCheckpoint >= 0); return; }

        if (ShiftPressed() && shiftCd <= 0) TryShift();
        if (state != State.Playing) return;

        player.Step(dt, lvl, world, Horizontal(), JumpPressed(), JumpHeld());
        if (player.jumped) sfx.Play(sfx.Jump, 0.35f, Random.Range(0.95f, 1.05f));
        if (player.landed)
        {
            sfx.Play(sfx.Land, 0.5f);
            Burst(player.pos - new Vector2(0, Player.HH), 5, Palette.A(Color.white, 0.6f), 1.5f, 0.3f);
        }

        lvl.UpdateMonsters(dt);

        Rect pb = player.Box;
        if (player.pos.y < -2.5f) { Die("Lost between worlds."); return; }
        if (lvl.SpikeHit(pb, world)) { Die(SpikeLines[levelDeaths % SpikeLines.Length]); return; }
        if (lvl.MonsterHit(pb, world) != null) { Die(CaughtLines[levelDeaths % CaughtLines.Length]); return; }

        foreach (var s in lvl.Shards)
        {
            if (s.taken || (s.pos - player.pos).sqrMagnitude > 0.6f * 0.6f) continue;
            s.taken = true;
            pendingShards.Add(s.id);
            sfx.Play(sfx.ShardGet, 0.7f);
            Burst(s.pos, 14, new Color(1f, 0.95f, 0.6f), 4f, 0.6f);
        }

        for (int i = 0; i < lvl.Checkpoints.Count; i++)
        {
            var c = lvl.Checkpoints[i];
            if (c.active || !c.Box.Overlaps(pb)) continue;
            foreach (var o in lvl.Checkpoints) o.active = false;
            c.active = true;
            c.world = world;
            activeCheckpoint = i;
            respawnPos = new Vector2(c.pos.x, c.pos.y + Player.HH + 0.002f);
            respawnWorld = world;
            sfx.Play(sfx.Checkpoint, 0.6f);
            Burst(c.pos + new Vector2(0, 0.8f), 12, Palette.World(world), 3f, 0.6f);
        }

        if (lvl.ExitBox.Overlaps(pb)) CompleteLevel();
    }

    // ------------------------------------------------------------------ visuals

    void UpdatePlayerVisual(float dt)
    {
        if (!playerTr.gameObject.activeSelf) return;
        float sq = player.squash;
        float sx = sq >= 0 ? 1 - sq * 0.5f : 1 - sq * 0.8f;
        float sy = 1 + sq;
        if (state == State.Exiting) { sx *= playerTr.localScale.y; sy *= playerTr.localScale.y; }
        else if (player.grounded && Mathf.Abs(player.vel.x) > 0.5f) sy += Mathf.Sin(time * 22f) * 0.04f;
        playerTr.position = new Vector3(player.pos.x, player.pos.y - Player.HH, 0);
        playerTr.localScale = new Vector3(player.facing * sx, sy, 1);
        Color c = Color.Lerp(Color.white, Palette.WorldT(worldT), 0.22f);
        if (denyFlash > 0) c = Color.Lerp(c, new Color(1f, 0.25f, 0.25f), denyFlash / 0.35f);
        playerSr.color = c;
    }

    Vector2 ClampCam(Vector2 p)
    {
        if (lvl == null) return p;
        float hh = cam.orthographicSize, hw = hh * cam.aspect;
        float minX = hw, maxX = lvl.W - hw;
        float minY = hh - 2f, maxY = lvl.H + 1f - hh;
        p.x = minX > maxX ? lvl.W / 2f : Mathf.Clamp(p.x, minX, maxX);
        p.y = minY > maxY ? lvl.H / 2f : Mathf.Clamp(p.y, minY, maxY);
        return p;
    }

    void UpdateCamera(float dt)
    {
        Vector2 target = state == State.Playing || state == State.Dying || state == State.Exiting
            ? player.pos + new Vector2(player.facing * 1.5f, 0.8f)
            : new Vector2(lvl.W / 2f, lvl.H / 2f);
        target = ClampCam(target);
        camPos = Vector2.Lerp(camPos, target, 1 - Mathf.Exp(-dt * 5f));
        Vector2 sh = shake > 0 ? Random.insideUnitCircle * shake * 0.5f : Vector2.zero;
        shake = Mathf.MoveTowards(shake, 0, dt * 1.2f);
        cam.transform.position = new Vector3(camPos.x + sh.x, camPos.y + sh.y, -10);
        cam.backgroundColor = Color.Lerp(Palette.SolBg, Palette.UmbraBg, worldT);
    }

    void UpdateBackground()
    {
        sun.color = Palette.A(new Color(1f, 0.55f, 0.3f), 0.22f * (1 - worldT));
        moon.color = Palette.A(new Color(0.55f, 0.75f, 1f), 0.2f * worldT);
        sun.transform.localPosition = new Vector3(8f, 3.5f - worldT * 3f, 0);
        moon.transform.localPosition = new Vector3(-8f, 0.5f + worldT * 3f, 0);
        Color starC = Color.Lerp(new Color(1f, 0.8f, 0.6f), new Color(0.75f, 0.9f, 1f), worldT);
        foreach (var s in stars)
        {
            float tw = 0.5f + 0.5f * Mathf.Sin(time * 1.5f + s.phase);
            float x = Mathf.Repeat(s.lp.x - camPos.x * s.depth + 24f, 48f) - 24f;
            float y = Mathf.Repeat(s.lp.y - camPos.y * s.depth + 10f, 20f) - 10f;
            s.sr.transform.localPosition = new Vector3(x, y, 0);
            s.sr.color = Palette.A(starC, (0.15f + 0.45f * worldT) * (0.4f + 0.6f * tw));
        }
    }

    void Burst(Vector2 at, int count, Color c, float speed, float life)
    {
        for (int i = 0; i < count; i++)
        {
            Particle p;
            if (freeParts.Count > 0) p = freeParts.Pop();
            else
            {
                var go = new GameObject("p");
                p = new Particle { tr = go.transform, sr = go.AddComponent<SpriteRenderer>() };
                p.sr.sprite = Art.Pixel;
                p.sr.sortingOrder = 30;
            }
            p.tr.gameObject.SetActive(true);
            p.tr.position = at;
            p.v = Random.insideUnitCircle * speed + Vector2.up * speed * 0.3f;
            p.max = p.life = life * Random.Range(0.6f, 1.1f);
            p.size = Random.Range(0.7f, 1.3f);
            p.sr.color = c;
            parts.Add(p);
        }
    }

    void UpdateFx(float dt)
    {
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            var p = parts[i];
            p.life -= dt;
            if (p.life <= 0)
            {
                p.tr.gameObject.SetActive(false);
                freeParts.Push(p);
                parts.RemoveAt(i);
                continue;
            }
            p.v.y -= 10f * dt;
            p.v *= 1f - dt * 1.5f;
            p.tr.position += (Vector3)(p.v * dt);
            float k = p.life / p.max;
            p.tr.localScale = Vector3.one * p.size * k;
            p.sr.color = Palette.A(p.sr.color, k);
        }

        if (ringT >= 0)
        {
            ringT += dt;
            float k = ringT / 0.55f;
            if (k >= 1) { ringT = -1; ring.enabled = false; }
            else
            {
                ring.enabled = true;
                float e = 1 - (1 - k) * (1 - k);
                ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 34f, e);
                ring.color = Palette.A(Palette.World(world), 0.7f * (1 - k));
            }
        }
    }

    // ------------------------------------------------------------------ UI

    void Text(Rect r, string s, float size, Color c, TextAnchor a = TextAnchor.MiddleCenter, FontStyle fs = FontStyle.Normal)
    {
        style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
        style.alignment = a;
        style.fontStyle = fs;
        float o = Mathf.Max(1f, size / 14f);
        style.normal.textColor = new Color(0, 0, 0, c.a * 0.65f);
        GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), s, style);
        style.normal.textColor = c;
        GUI.Label(r, s, style);
    }

    void Overlay(Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        if (style == null) style = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, clipping = TextClipping.Overflow };
        float u = Screen.height / 720f;
        float W = Screen.width, H = Screen.height;
        Color wc = Color.Lerp(Palette.WorldT(worldT), Color.white, 0.15f);

        if (flash > 0) Overlay(Palette.A(Palette.World(world), flash * 0.6f));

        switch (state)
        {
            case State.Menu:
            {
                Overlay(new Color(0, 0, 0, 0.45f));
                Text(new Rect(0, H * 0.10f, W, 130 * u), "SHIFTSIDE", 110 * u, wc, TextAnchor.MiddleCenter, FontStyle.Bold);
                Text(new Rect(W * 0.1f, H * 0.29f, W * 0.8f, 80 * u),
                    "That broken bridge might exist in the other world.\nUnfortunately, so might the monster standing on it.",
                    24 * u, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, FontStyle.Italic);

                var L = Levels.All[menuSel];
                string sel = (menuSel > 0 ? "<   " : "     ") + $"Level {menuSel + 1}: {L.Name}" + (menuSel < unlocked - 1 ? "   >" : "     ");
                Text(new Rect(0, H * 0.47f, W, 50 * u), sel, 32 * u, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                float blink = 0.55f + 0.45f * Mathf.Sin(time * 4f);
                Text(new Rect(0, H * 0.56f, W, 40 * u), "Press SPACE or ENTER to begin", 26 * u, Palette.A(wc, blink));

                Text(new Rect(W * 0.08f, H * 0.68f, W * 0.84f, 90 * u),
                    "Move  A / D or Arrows      Jump  Space / W / Z      SHIFT WORLDS  Shift / X\nRestart  R      Menu  Esc      Music  M",
                    19 * u, new Color(1, 1, 1, 0.75f));
                Text(new Rect(0, H * 0.83f, W, 30 * u), $"Shards {ShardsOwned()} / {Levels.TotalShards}      Deaths {totalDeaths}", 18 * u, new Color(1, 1, 1, 0.6f));
                Text(new Rect(0, H - 44 * u, W, 30 * u), "Made for GitHub Game Off 2026  -  theme: SHIFT", 16 * u, new Color(1, 1, 1, 0.45f));
                break;
            }
            case State.Playing:
            case State.Dying:
            case State.Exiting:
            {
                float pad = 18 * u;
                if (Trailer)
                {
                    Text(new Rect(W * 0.5f, pad, W * 0.5f - pad, 40 * u), world == 0 ? "SOL" : "UMBRA", 32 * u, Palette.World(world), TextAnchor.UpperRight, FontStyle.Bold);
                    if (captionTimer > 0)
                        Text(new Rect(W * 0.1f, H * 0.36f, W * 0.8f, 60 * u), caption, 34 * u, new Color(1, 1, 1, Mathf.Clamp01(captionTimer * 1.5f)), TextAnchor.MiddleCenter, FontStyle.Bold);
                    break;
                }
                Text(new Rect(pad, pad, W * 0.6f, 34 * u), $"{levelIndex + 1} / {Levels.All.Length}   {lvl.Def.Name.ToUpperInvariant()}", 24 * u, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
                string stats = $"Deaths {levelDeaths}";
                if (lvl.Shards.Count > 0)
                {
                    int got = 0;
                    foreach (var s in lvl.Shards) if (s.taken) got++;
                    stats += $"     Shards {got} / {lvl.Shards.Count}";
                }
                Text(new Rect(pad, pad + 32 * u, W * 0.6f, 28 * u), stats, 18 * u, new Color(1, 1, 1, 0.7f), TextAnchor.UpperLeft);
                Text(new Rect(W * 0.5f, pad, W * 0.5f - pad, 40 * u), world == 0 ? "SOL" : "UMBRA", 32 * u, Palette.A(Palette.World(world), 1f), TextAnchor.UpperRight, FontStyle.Bold);
                Text(new Rect(W * 0.5f, pad + 38 * u, W * 0.5f - pad, 24 * u), "SHIFT to cross", 15 * u, new Color(1, 1, 1, 0.5f), TextAnchor.UpperRight);

                float hintA = Mathf.Clamp01((3.2f - titleTimer) * 1.5f) * 0.9f;
                Text(new Rect(W * 0.1f, H - 90 * u, W * 0.8f, 70 * u), lvl.Def.Hint, 21 * u, new Color(1, 1, 1, hintA), TextAnchor.MiddleCenter, FontStyle.Italic);

                if (titleTimer > 0)
                {
                    float a = Mathf.Clamp01(titleTimer);
                    Text(new Rect(0, H * 0.22f, W, 80 * u), lvl.Def.Name, 56 * u, Palette.A(wc, a), TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                if (captionTimer > 0)
                    Text(new Rect(W * 0.1f, H * 0.36f, W * 0.8f, 60 * u), caption, 30 * u, new Color(1, 1, 1, Mathf.Clamp01(captionTimer * 1.5f)), TextAnchor.MiddleCenter, FontStyle.Bold);
                break;
            }
            case State.Card:
                DrawCard(W, H, u, wc);
                break;
            case State.Ending:
            {
                Overlay(new Color(0, 0, 0, 0.35f));
                Text(new Rect(0, H * 0.16f, W, 100 * u), "You made it across.", 64 * u, wc, TextAnchor.MiddleCenter, FontStyle.Bold);
                Text(new Rect(W * 0.12f, H * 0.34f, W * 0.76f, 120 * u),
                    "Every bridge was whole somewhere.\nEvery monster was standing on one.\nYou learned which world to be in, and when.",
                    24 * u, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, FontStyle.Italic);
                Text(new Rect(0, H * 0.58f, W, 40 * u), $"Total deaths {totalDeaths}      Shards {ShardsOwned()} / {Levels.TotalShards}", 24 * u, Color.white);
                Text(new Rect(0, H * 0.70f, W, 40 * u), "Thanks for playing SHIFTSIDE  -  GitHub Game Off 2026", 20 * u, new Color(1, 1, 1, 0.7f));
                if (menuTimer > 1.5f)
                    Text(new Rect(0, H * 0.80f, W, 40 * u), "Press SPACE to return to the menu", 20 * u, Palette.A(wc, 0.6f + 0.4f * Mathf.Sin(time * 4)));
                break;
            }
        }

        if (fade > 0.001f) Overlay(new Color(0, 0, 0, fade));
    }
}
