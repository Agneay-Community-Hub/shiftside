using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Headless solvability check: beam search over input sequences, run through the real
/// Player + LevelRuntime code. Monsters move independently of the player, so their
/// positions are precomputed per frame.
///   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod SimTest.Run
public static class SimTest
{
    const float Dt = 1f / 60f;
    const int FramesPerChunk = 6;
    const int MaxChunks = 600;      // 60 seconds
    const int Beam = 700;

    struct MonsterBox { public int world; public Rect box; }

    class Node
    {
        public Player p;
        public int world;
        public Node parent;
        public string act;
        public float score;
    }

    public static void Run()
    {
        bool allOk = true;
        for (int i = 0; i < Levels.All.Length; i++)
        {
            var (ok, info) = Solve(i);
            allOk &= ok;
            Debug.Log($"[SimTest] L{i + 1} {Levels.All[i].Name}: {(ok ? "SOLVED" : "NOT SOLVED")} {info}");
        }
        Debug.Log("[SimTest] " + (allOk ? "ALL LEVELS SOLVABLE" : "SOME LEVELS FAILED"));
    }

    static (bool, string) Solve(int index)
    {
        var L = new LevelRuntime(Levels.All[index]);
        int totalFrames = MaxChunks * FramesPerChunk + 2;
        var timeline = new List<MonsterBox[]>(totalFrames);
        for (int f = 0; f < totalFrames; f++)
        {
            var arr = new MonsterBox[L.Monsters.Count];
            for (int m = 0; m < arr.Length; m++) arr[m] = new MonsterBox { world = L.Monsters[m].world, box = L.Monsters[m].Box };
            timeline.Add(arr);
            L.UpdateMonsters(Dt);
        }

        Vector2 exit = L.ExitPos + new Vector2(0, 0.9f);
        var start = new Node { p = new Player(), world = 0 };
        start.p.Reset(L.Start);
        var beam = new List<Node> { start };
        float best = float.MinValue;

        for (int chunk = 0; chunk < MaxChunks; chunk++)
        {
            int f0 = chunk * FramesPerChunk;
            var next = new Dictionary<long, Node>();
            foreach (var n in beam)
            {
                for (int ix = -1; ix <= 1; ix++)
                for (int jump = 0; jump <= 1; jump++)
                for (int shift = 0; shift <= 1; shift++)
                {
                    var p = n.p.Clone();
                    int w = n.world;
                    if (shift == 1)
                    {
                        if (L.OverlapsSolid(p.Box, 1 - w)) continue;
                        w = 1 - w;
                        if (Hit(timeline[f0], p.Box, w) || L.SpikeHit(p.Box, w)) continue;
                    }
                    bool dead = false, won = false;
                    for (int k = 0; k < FramesPerChunk && !dead && !won; k++)
                    {
                        p.Step(Dt, L, w, ix, jump == 1 && k == 0, jump == 1);
                        var b = p.Box;
                        if (p.pos.y < -2.5f || L.SpikeHit(b, w) || Hit(timeline[f0 + k + 1], b, w)) dead = true;
                        else if (L.ExitBox.Overlaps(b)) won = true;
                    }
                    if (dead) continue;
                    var node = new Node { p = p, world = w, parent = n, act = $"{"L-R"[ix + 1]}{(jump == 1 ? "J" : "")}{(shift == 1 ? "S" : "")}" };
                    if (won) return (true, $"in {(chunk + 1) * 0.1f:F1}s, {CountShifts(node)} shifts");
                    node.score = -Vector2.Distance(p.pos, exit) + p.pos.x * 0.05f;
                    long key = ((long)Mathf.RoundToInt(p.pos.x * 4) << 32) ^ ((long)Mathf.RoundToInt(p.pos.y * 4) << 12)
                               ^ ((long)Mathf.RoundToInt(p.vel.y / 3f) << 4) ^ (long)(w + (p.grounded ? 2 : 0));
                    if (!next.TryGetValue(key, out var ex) || ex.score < node.score) next[key] = node;
                }
            }
            if (next.Count == 0) return (false, $"all paths died at {chunk * 0.1f:F1}s");
            beam = new List<Node>(next.Values);
            beam.Sort((a, b) => b.score.CompareTo(a.score));
            if (beam.Count > Beam)
            {
                // keep the best, plus a spread of the rest for diversity
                var keep = beam.GetRange(0, Beam * 2 / 3);
                int stride = Mathf.Max(1, (beam.Count - keep.Count) / (Beam / 3));
                for (int i = keep.Count; i < beam.Count && keep.Count < Beam; i += stride) keep.Add(beam[i]);
                beam = keep;
            }
            best = Mathf.Max(best, beam[0].score);
        }
        var top = beam[0];
        return (false, $"timeout; best pos {top.p.pos} world {top.world}");
    }

    static bool Hit(MonsterBox[] boxes, Rect r, int w)
    {
        foreach (var m in boxes)
            if (m.world == w && m.box.Overlaps(r)) return true;
        return false;
    }

    static int CountShifts(Node n)
    {
        int c = 0;
        for (; n != null; n = n.parent) if (n.act != null && n.act.Contains("S")) c++;
        return c;
    }
}
