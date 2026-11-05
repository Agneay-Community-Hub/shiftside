using System.Collections.Generic;
using UnityEngine;

public class Monster
{
    public Vector2 pos;
    public int world, dir, tileX, tileY;
    public bool flyer;
    public float speed, phase;
    public Transform tr;
    public SpriteRenderer sr;

    public Rect Box => flyer
        ? new Rect(pos.x - 0.32f, pos.y - 0.32f, 0.64f, 0.64f)
        : new Rect(pos.x - 0.32f, pos.y - 0.40f, 0.64f, 0.80f);
}

public class Shard
{
    public int id;
    public Vector2 pos;
    public bool taken;
    public SpriteRenderer sr;
}

public class Checkpoint
{
    public Vector2 pos;
    public bool active;
    public int world;
    public SpriteRenderer sr;
    public Rect Box => new Rect(pos.x - 0.35f, pos.y, 0.7f, 1f);
}

/// A parsed, instantiated level: tile grids for both worlds plus their visuals.
public class LevelRuntime
{
    public readonly LevelDef Def;
    public readonly int W, H;
    readonly byte[,] solid, spike, marker; // bitmask: 1 = Sol, 2 = Umbra
    public Vector2 Start, ExitPos;
    public readonly List<Monster> Monsters = new List<Monster>();
    public readonly List<Shard> Shards = new List<Shard>();
    public readonly List<Checkpoint> Checkpoints = new List<Checkpoint>();
    public GameObject Root;

    struct Vis
    {
        public SpriteRenderer sr;
        public int mask;
        public bool spike;
    }

    readonly List<Vis> vis = new List<Vis>();
    SpriteRenderer portal, portalGlow;

    public Rect ExitBox => new Rect(ExitPos.x - 0.35f, ExitPos.y, 0.7f, 1.8f);

    public LevelRuntime(LevelDef def)
    {
        Def = def;
        H = def.Rows.Length;
        W = 0;
        foreach (var r in def.Rows) W = Mathf.Max(W, r.Length);
        solid = new byte[W, H];
        spike = new byte[W, H];
        marker = new byte[W, H];

        int walkers = 0;
        for (int row = 0; row < H; row++)
        {
            string line = def.Rows[row];
            int y = H - 1 - row;
            for (int x = 0; x < W; x++)
            {
                char c = x < line.Length ? line[x] : '.';
                switch (c)
                {
                    case '#': solid[x, y] = 3; break;
                    case '1': solid[x, y] = 1; break;
                    case '2': solid[x, y] = 2; break;
                    case '^': spike[x, y] = 3; break;
                    case 'x': spike[x, y] = 1; break;
                    case 'y': spike[x, y] = 2; break;
                    case '|': marker[x, y] = 3; break;
                    case '(': marker[x, y] = 1; break;
                    case ')': marker[x, y] = 2; break;
                    case 'P': Start = new Vector2(x + 0.5f, y + Player.HH + 0.002f); break;
                    case 'E': ExitPos = new Vector2(x + 0.5f, y); break;
                    case 'C': Checkpoints.Add(new Checkpoint { pos = new Vector2(x + 0.5f, y) }); break;
                    case '*': Shards.Add(new Shard { id = Shards.Count, pos = new Vector2(x + 0.5f, y + 0.5f) }); break;
                    case 'a':
                    case 'b':
                        Monsters.Add(new Monster
                        {
                            world = c == 'a' ? 0 : 1, flyer = false, dir = -1, tileX = x, tileY = y,
                            pos = new Vector2(x + 0.5f, y + 0.42f), speed = 2.0f + 0.35f * (walkers++ % 3),
                            phase = x * 0.37f
                        });
                        break;
                    case 'f':
                    case 'g':
                        Monsters.Add(new Monster
                        {
                            world = c == 'f' ? 0 : 1, flyer = true, dir = 1, tileX = x, tileY = y,
                            pos = new Vector2(x + 0.5f, y + 0.5f), speed = 2.4f, phase = x * 0.37f
                        });
                        break;
                }
            }
        }
    }

    // ---------- queries ----------

    public bool Solid(int x, int y, int world)
    {
        if (x < 0 || x >= W) return true;   // level sides are walls
        if (y < 0 || y >= H) return false;  // open sky above, void below
        return (solid[x, y] & (1 << world)) != 0;
    }

    public bool Marker(int x, int y, int world)
    {
        if (x < 0 || x >= W || y < 0 || y >= H) return false;
        return (marker[x, y] & (1 << world)) != 0;
    }

    public bool OverlapsSolid(Rect r, int world)
    {
        const float e = 0.002f;
        int x0 = Mathf.FloorToInt(r.xMin + e), x1 = Mathf.FloorToInt(r.xMax - e);
        int y0 = Mathf.FloorToInt(r.yMin + e), y1 = Mathf.FloorToInt(r.yMax - e);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (Solid(x, y, world)) return true;
        return false;
    }

    public bool SpikeHit(Rect r, int world)
    {
        int x0 = Mathf.FloorToInt(r.xMin), x1 = Mathf.FloorToInt(r.xMax);
        int y0 = Mathf.FloorToInt(r.yMin), y1 = Mathf.FloorToInt(r.yMax);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (x < 0 || x >= W || y < 0 || y >= H) continue;
                if ((spike[x, y] & (1 << world)) == 0) continue;
                if (r.Overlaps(new Rect(x + 0.15f, y, 0.7f, 0.5f))) return true;
            }
        return false;
    }

    public Monster MonsterHit(Rect r, int world)
    {
        foreach (var m in Monsters)
            if (m.world == world && m.Box.Overlaps(r)) return m;
        return null;
    }

    // ---------- simulation ----------

    public void UpdateMonsters(float dt)
    {
        foreach (var m in Monsters)
        {
            if (m.flyer)
            {
                m.pos.y += m.dir * m.speed * dt;
                float front = m.pos.y + m.dir * 0.45f;
                int fy = Mathf.FloorToInt(front);
                if (fy < 0 || fy >= H || Solid(m.tileX, fy, m.world) || Marker(m.tileX, fy, m.world))
                {
                    m.pos.y = m.dir > 0 ? fy - 0.45f : fy + 1.45f;
                    m.dir = -m.dir;
                }
            }
            else
            {
                m.pos.x += m.dir * m.speed * dt;
                float front = m.pos.x + m.dir * 0.4f;
                int fx = Mathf.FloorToInt(front);
                if (Solid(fx, m.tileY, m.world) || Marker(fx, m.tileY, m.world) || !Solid(fx, m.tileY - 1, m.world))
                {
                    m.pos.x = m.dir > 0 ? fx - 0.4f : fx + 1.4f;
                    m.dir = -m.dir;
                }
            }
        }
    }

    // ---------- visuals ----------

    public void Build(Transform parent)
    {
        Root = new GameObject("Level " + Def.Name);
        Root.transform.SetParent(parent, false);

        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (solid[x, y] != 0)
                    vis.Add(new Vis { sr = MakeSprite("tile", Art.Tile, new Vector2(x + 0.5f, y + 0.5f), solid[x, y] == 3 ? 2 : 0), mask = solid[x, y] });
                if (spike[x, y] != 0)
                    vis.Add(new Vis { sr = MakeSprite("spike", Art.Spike, new Vector2(x + 0.5f, y + 0.5f), 3), mask = spike[x, y], spike = true });
            }

        portalGlow = MakeSprite("portalGlow", Art.Circle, ExitPos + new Vector2(0, 1f), 4);
        portalGlow.transform.localScale = new Vector3(3f, 4f, 1);
        portal = MakeSprite("portal", Art.Portal, ExitPos, 5);

        foreach (var m in Monsters)
        {
            m.sr = MakeSprite(m.flyer ? "flyer" : "walker", m.flyer ? Art.Flyer : Art.Walker, m.pos, m.world == 0 ? 7 : 6);
            m.tr = m.sr.transform;
        }

        foreach (var s in Shards)
            s.sr = MakeSprite("shard", Art.Shard, s.pos, 8);

        foreach (var c in Checkpoints)
            c.sr = MakeSprite("checkpoint", Art.Flag, c.pos, 5);
    }

    SpriteRenderer MakeSprite(string name, Sprite sprite, Vector2 pos, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(Root.transform, false);
        go.transform.localPosition = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    public void UpdateVisuals(float t, float time)
    {
        Color both = Color.Lerp(Palette.BothSol, Palette.BothUmbra, t);
        foreach (var v in vis)
        {
            if (v.mask == 3)
            {
                v.sr.color = v.spike ? Color.Lerp(both, Color.white, 0.4f) : both;
                continue;
            }
            int w = v.mask == 1 ? 0 : 1;
            float a = Palette.Presence(w, t);
            Color c = Palette.World(w);
            if (v.spike) c = Color.Lerp(c, Color.white, 0.25f);
            v.sr.color = Palette.A(c, a);
            float s = Mathf.Lerp(0.82f, 1f, (a - Palette.Ghost) / (1f - Palette.Ghost));
            v.sr.transform.localScale = new Vector3(s, s, 1);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f);
        Color pc = Color.Lerp(Palette.WorldT(t), Color.white, 0.35f + 0.25f * pulse);
        portal.color = pc;
        portal.transform.localScale = new Vector3(1f + 0.06f * pulse, 1f, 1f);
        portalGlow.color = Palette.A(Palette.WorldT(t), 0.25f + 0.15f * pulse);

        foreach (var m in Monsters)
        {
            float a = Palette.Presence(m.world, t);
            a = Mathf.Lerp(Palette.Ghost * 1.6f, 1f, (a - Palette.Ghost) / (1f - Palette.Ghost));
            m.sr.color = Palette.A(Palette.Monster(m.world), a);
            float bob = Mathf.Sin(time * (m.flyer ? 6f : 10f) + m.phase);
            if (m.flyer)
            {
                m.tr.localPosition = new Vector2(m.pos.x, m.pos.y - 0.5f + bob * 0.06f);
                m.tr.localScale = new Vector3(1f, 1f + bob * 0.05f, 1f);
            }
            else
            {
                m.tr.localPosition = new Vector2(m.pos.x, m.tileY);
                m.tr.localScale = new Vector3(m.dir * (1f - bob * 0.04f), 1f + bob * 0.06f, 1f);
            }
        }

        foreach (var s in Shards)
        {
            if (s.taken) { s.sr.enabled = false; continue; }
            s.sr.transform.localPosition = s.pos + new Vector2(0, Mathf.Sin(time * 2.5f + s.id) * 0.12f);
            s.sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 1.7f) * 15f);
            s.sr.color = Color.Lerp(new Color(1f, 0.95f, 0.6f), Color.white, pulse);
        }

        foreach (var c in Checkpoints)
            c.sr.color = c.active ? Color.Lerp(Palette.World(c.world), Color.white, 0.2f * pulse) : new Color(0.55f, 0.55f, 0.6f, 0.9f);
    }

    public void Destroy()
    {
        if (Root != null) Object.Destroy(Root);
    }
}
