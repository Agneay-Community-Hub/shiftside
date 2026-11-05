using UnityEngine;

/// Colours for the two realities. World 0 = SOL (warm, day), world 1 = UMBRA (cool, night).
public static class Palette
{
    public static readonly Color SolBg = new Color(0.17f, 0.09f, 0.12f);
    public static readonly Color UmbraBg = new Color(0.03f, 0.07f, 0.15f);

    public static readonly Color Sol = new Color(1f, 0.62f, 0.28f);
    public static readonly Color Umbra = new Color(0.33f, 0.80f, 1f);

    public static readonly Color BothSol = new Color(0.93f, 0.84f, 0.74f);
    public static readonly Color BothUmbra = new Color(0.66f, 0.74f, 0.90f);

    public static readonly Color SolMonster = new Color(1f, 0.30f, 0.32f);
    public static readonly Color UmbraMonster = new Color(0.78f, 0.45f, 1f);

    public const float Ghost = 0.17f;

    public static Color World(int w) => w == 0 ? Sol : Umbra;
    public static Color WorldT(float t) => Color.Lerp(Sol, Umbra, t);
    public static Color Monster(int w) => w == 0 ? SolMonster : UmbraMonster;

    public static Color A(Color c, float a) { c.a = a; return c; }

    /// Alpha for an object that only exists in world `w`, given the blend t (0 = Sol, 1 = Umbra).
    public static float Presence(int w, float t) => w == 0 ? Mathf.Lerp(1f, Ghost, t) : Mathf.Lerp(Ghost, 1f, t);
}
