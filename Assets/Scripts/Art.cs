using System;
using UnityEngine;

/// All sprites are generated procedurally at startup: no imported art.
public static class Art
{
    static Sprite tile, circle, ring, player, walker, flyer, spike, portal, shard, flag, px;

    public static Sprite Tile => tile != null ? tile : (tile = Make(16, 16, 16, new Vector2(0.5f, 0.5f), TilePx));
    public static Sprite Circle => circle != null ? circle : (circle = Make(64, 64, 64, new Vector2(0.5f, 0.5f), CirclePx, FilterMode.Bilinear));
    public static Sprite Ring => ring != null ? ring : (ring = Make(128, 128, 128, new Vector2(0.5f, 0.5f), RingPx, FilterMode.Bilinear));
    public static Sprite Player => player != null ? player : (player = Make(16, 16, 16, new Vector2(0.5f, 0f), PlayerPx));
    public static Sprite Walker => walker != null ? walker : (walker = Make(16, 16, 16, new Vector2(0.5f, 0f), (x, y) => MonsterPx(x, y, false)));
    public static Sprite Flyer => flyer != null ? flyer : (flyer = Make(16, 16, 16, new Vector2(0.5f, 0f), (x, y) => MonsterPx(x, y, true)));
    public static Sprite Spike => spike != null ? spike : (spike = Make(16, 16, 16, new Vector2(0.5f, 0.5f), SpikePx));
    public static Sprite Portal => portal != null ? portal : (portal = Make(16, 32, 16, new Vector2(0.5f, 0f), PortalPx));
    public static Sprite Shard => shard != null ? shard : (shard = Make(9, 9, 16, new Vector2(0.5f, 0.5f), ShardPx));
    public static Sprite Flag => flag != null ? flag : (flag = Make(16, 16, 16, new Vector2(0.5f, 0f), FlagPx));
    public static Sprite Pixel => px != null ? px : (px = Make(4, 4, 16, new Vector2(0.5f, 0.5f), (x, y) => Color.white));

    static Sprite Make(int w, int h, float ppu, Vector2 pivot, Func<int, int, Color> f, FilterMode filter = FilterMode.Point)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = f(x, y);
        t.SetPixels(pixels);
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, w, h), pivot, ppu);
    }

    static Color G(float v, float a = 1f) => new Color(v, v, v, a);
    static readonly Color Clear = new Color(0, 0, 0, 0);

    static Color TilePx(int x, int y)
    {
        if (x == 0 || y == 0 || x == 15 || y == 15) return G(0.52f);
        if (y >= 13) return G(1f);
        if (y <= 2) return G(0.68f);
        int h = (x * 7 + y * 13) % 11;
        return G(h == 0 ? 0.72f : 0.84f);
    }

    static Color CirclePx(int x, int y)
    {
        float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float a = Mathf.Clamp01(1f - d);
        return G(1f, a * a);
    }

    static Color RingPx(int x, int y)
    {
        float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float k = (d - 0.88f) / 0.07f;
        return G(1f, Mathf.Exp(-k * k));
    }

    static Color PlayerPx(int x, int y)
    {
        if (x < 3 || x > 12 || y > 13) return Clear;
        bool corner = (x == 3 || x == 12) && (y == 0 || y == 13);
        if (corner) return Clear;
        // eyes (looking right)
        if ((x == 8 || x == 11) && y >= 8 && y <= 10) return G(0.08f);
        // scarf band
        if (y == 5 || y == 6) return G(0.55f);
        if (y <= 1) return G(0.78f);
        if (x == 3 || x == 12 || y == 13) return G(0.85f);
        return G(1f);
    }

    static Color MonsterPx(int x, int y, bool wings)
    {
        float cx = 7.5f, cy = 6.5f;
        float dx = x - cx, dy = y - cy;
        bool body = dx * dx + dy * dy <= 6.6f * 6.6f && y >= 1;

        // eyes: bright sclera, dark pupils (looking right)
        float e1 = (x - 5f) * (x - 5f) + (y - 8f) * (y - 8f);
        float e2 = (x - 10f) * (x - 10f) + (y - 8f) * (y - 8f);
        if (body && ((x == 6 || x == 11) && (y == 8 || y == 7))) return G(0.05f);
        if (body && (e1 <= 3.2f || e2 <= 3.2f)) return G(1f);
        // jagged mouth
        if (body && x >= 4 && x <= 11 && (x % 2 == 0 ? y == 3 : y == 4)) return G(0.08f);
        if (body) return G(dy > 3 ? 0.82f : 0.7f);
        // horns / spikes on top
        if (y >= 12 && y <= 15 && x >= 2 && x <= 13)
        {
            float lx = x % 4;
            if (Mathf.Abs(lx - 1.5f) < (15.5f - y) * 0.45f) return G(0.6f);
        }
        if (!wings)
        {
            if (y == 0 && (x == 4 || x == 5 || x == 10 || x == 11)) return G(0.55f);
        }
        else
        {
            if ((x <= 1 || x >= 14) && y >= 6 && y <= 11 && Mathf.Abs(y - 9) <= (x <= 1 ? x + 1 : 16 - x)) return G(0.65f);
        }
        return Clear;
    }

    static Color SpikePx(int x, int y)
    {
        float lx = x % 8;
        float h = (4.0f - Mathf.Abs(lx - 3.5f)) * 2.4f;
        if (y < h) return G(lx < 3.5f ? 1f : 0.72f);
        return Clear;
    }

    static Color PortalPx(int x, int y)
    {
        float dx = (x - 7.5f) / 7.5f, dy = (y - 15.5f) / 15.5f;
        float d = dx * dx + dy * dy;
        if (d > 1f) return Clear;
        if (d > 0.62f) return G(1f);
        return G(1f, 0.18f + 0.5f * d);
    }

    static Color ShardPx(int x, int y)
    {
        float d = Mathf.Abs(x - 4f) + Mathf.Abs(y - 4f);
        if (d > 4.2f) return Clear;
        return G(x < 4 && y > 3 ? 1f : 0.75f);
    }

    static Color FlagPx(int x, int y)
    {
        if (x == 3 && y <= 14) return G(0.7f);
        if (x >= 4 && x <= 12 && y >= 9 && y <= 14)
        {
            if (x >= 10 && Mathf.Abs(y - 11.5f) < (x - 9) * 0.6f) return Clear; // swallowtail notch
            return G(1f);
        }
        if (y == 0 && x >= 1 && x <= 5) return G(0.5f);
        return Clear;
    }
}
