using UnityEngine;

/// Kinematic tile-collision platformer controller. Collides only with the world the player is in.
public class Player
{
    public const float HW = 0.32f, HH = 0.43f;
    const float MaxSpeed = 6.5f, Gravity = 44f, JumpV = 17f, MaxFall = 18f;
    const float E = 0.001f;

    public Vector2 pos, vel;
    public bool grounded;
    public int facing = 1;
    public float squash;
    public bool jumped, landed;
    float coyote, buffer;

    public Rect Box => new Rect(pos.x - HW, pos.y - HH, HW * 2, HH * 2);

    public Player Clone() => (Player)MemberwiseClone();

    public void Reset(Vector2 p)
    {
        pos = p;
        vel = Vector2.zero;
        grounded = false;
        coyote = buffer = squash = 0;
        facing = 1;
    }

    public void Step(float dt, LevelRuntime L, int world, float ix, bool jumpDown, bool jumpHeld)
    {
        jumped = landed = false;

        float accel;
        if (Mathf.Abs(ix) < 0.15f) { ix = 0; accel = grounded ? 60f : 20f; }
        else accel = grounded ? 75f : 45f;
        vel.x = Mathf.MoveTowards(vel.x, ix * MaxSpeed, accel * dt);
        if (ix > 0) facing = 1; else if (ix < 0) facing = -1;

        if (jumpDown) buffer = 0.13f; else buffer -= dt;
        if (grounded) coyote = 0.1f; else coyote -= dt;
        if (buffer > 0 && coyote > 0)
        {
            vel.y = JumpV;
            buffer = coyote = 0;
            jumped = true;
            squash = 0.25f;
        }

        float g = Gravity;
        if (vel.y > 0 && !jumpHeld) g *= 2.2f;  // short hop when jump released
        else if (vel.y < 0) g *= 1.15f;
        vel.y = Mathf.Max(vel.y - g * dt, -MaxFall);

        bool was = grounded;
        grounded = false;
        Vector2 d = vel * dt;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) / 0.25f));
        for (int i = 0; i < steps; i++)
        {
            MoveX(L, world, d.x / steps);
            MoveY(L, world, d.y / steps);
        }
        if (grounded && !was) { landed = true; squash = -0.22f; }
        squash = Mathf.MoveTowards(squash, 0, dt * 1.6f);
    }

    static int Fl(float v) => Mathf.FloorToInt(v);

    void MoveX(LevelRuntime L, int w, float dx)
    {
        if (dx == 0) return;
        pos.x += dx;
        int y0 = Fl(pos.y - HH + E), y1 = Fl(pos.y + HH - E);
        if (dx > 0)
        {
            int x1 = Fl(pos.x + HW - E);
            for (int y = y0; y <= y1; y++)
                if (L.Solid(x1, y, w)) { pos.x = x1 - HW; vel.x = 0; return; }
        }
        else
        {
            int x0 = Fl(pos.x - HW + E);
            for (int y = y0; y <= y1; y++)
                if (L.Solid(x0, y, w)) { pos.x = x0 + 1 + HW; vel.x = 0; return; }
        }
    }

    void MoveY(LevelRuntime L, int w, float dy)
    {
        if (dy == 0) return;
        pos.y += dy;
        int x0 = Fl(pos.x - HW + E), x1 = Fl(pos.x + HW - E);
        if (dy > 0)
        {
            int y1 = Fl(pos.y + HH - E);
            for (int x = x0; x <= x1; x++)
                if (L.Solid(x, y1, w)) { pos.y = y1 - HH; vel.y = 0; return; }
        }
        else
        {
            int y0 = Fl(pos.y - HH + E);
            for (int x = x0; x <= x1; x++)
                if (L.Solid(x, y0, w)) { pos.y = y0 + 1 + HH; vel.y = 0; grounded = true; return; }
        }
    }
}
