using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// Headless solvability check: beam search over input sequences, run through the real
/// Player + LevelRuntime code. Monsters move independently of the player, so their
/// positions are precomputed per frame.
///   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod SimTest.Run
///   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod SimTest.ExportTrailer
public static class SimTest
{
    const float Dt = 1f / 60f;
    const int FramesPerChunk = 6;   // must match GameManager trailer replay
    const int MaxChunks = 600;      // 60 seconds
    const int Beam = 700;
    const int ShiftCooldownChunks = 2;

    struct MonsterBox { public int world; public Rect box; }

    class Node
    {
        public Player p;
        public int world, lastShift = -99, shifts, lastIx;
        public Node parent;
        public string act;
        public float score, penalty;
    }

    enum Goal { Exit, ShiftIntoMonster }

    public static void Run()
    {
        bool allOk = true;
        for (int i = 0; i < Levels.All.Length; i++)
        {
            var (ok, info, _) = Solve(i, Goal.Exit);
            allOk &= ok;
            Debug.Log($"[SimTest] L{i + 1} {Levels.All[i].Name}: {(ok ? "SOLVED" : "NOT SOLVED")} {info}");
        }
        Debug.Log("[SimTest] " + (allOk ? "ALL LEVELS SOLVABLE" : "SOME LEVELS FAILED"));
    }

    /// Writes human-looking input scripts for the trailer to Assets/Resources/trailer_inputs.txt
    public static void ExportTrailer()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Levels.All.Length; i++)
        {
            var (ok, info, acts) = Solve(i, Goal.Exit);
            Debug.Log($"[SimTest] trailer L{i + 1}: {ok} {info}");
            if (ok) sb.AppendLine($"L{i + 1}={acts}");
        }
        var (dok, dinfo, dacts) = Solve(Levels.All.Length - 1, Goal.ShiftIntoMonster);
        Debug.Log($"[SimTest] trailer death clip: {dok} {dinfo}");
        if (dok) sb.AppendLine($"L8death={dacts}");
        Directory.CreateDirectory("Assets/Resources");
        File.WriteAllText("Assets/Resources/trailer_inputs.txt", sb.ToString());
        UnityEditor.AssetDatabase.Refresh();
    }

    static (bool, string, string) Solve(int index, Goal goal)
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
                    if (shift == 1 && chunk - n.lastShift < ShiftCooldownChunks) continue;
                    var p = n.p.Clone();
                    int w = n.world;
                    string act = $"{"L-R"[ix + 1]}{(jump == 1 ? "J" : "")}{(shift == 1 ? "S" : "")}";
                    if (shift == 1)
                    {
                        if (L.OverlapsSolid(p.Box, 1 - w)) continue;
                        w = 1 - w;
                        if (Hit(timeline[f0], p.Box, w))
                        {
                            if (goal == Goal.ShiftIntoMonster && w == 1 && p.pos.x > 16f)
                                return (true, $"death at {(chunk + 1) * 0.1f:F1}s", Path(new Node { parent = n, act = act }));
                            continue;
                        }
                        if (L.SpikeHit(p.Box, w)) continue;
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
                    var node = new Node
                    {
                        p = p, world = w, parent = n, act = act,
                        lastShift = shift == 1 ? chunk : n.lastShift,
                        shifts = n.shifts + shift, lastIx = ix,
                        // keep inputs calm: penalise shifts, jumps and direction changes
                        penalty = n.penalty + shift * 0.35f + jump * 0.08f + (ix != n.lastIx ? 0.05f : 0f)
                    };
                    if (won)
                    {
                        if (goal == Goal.Exit) return (true, $"in {(chunk + 1) * 0.1f:F1}s, {node.shifts} shifts", Path(node));
                        continue;
                    }
                    node.score = -Vector2.Distance(p.pos, exit) + p.pos.x * 0.05f - node.penalty;
                    long key = ((long)Mathf.RoundToInt(p.pos.x * 4) << 32) ^ ((long)Mathf.RoundToInt(p.pos.y * 4) << 12)
                               ^ ((long)Mathf.RoundToInt(p.vel.y / 3f) << 4) ^ (long)(w + (p.grounded ? 2 : 0));
                    if (!next.TryGetValue(key, out var ex) || ex.score < node.score) next[key] = node;
                }
            }
            if (next.Count == 0) return (false, $"all paths died at {chunk * 0.1f:F1}s", null);
            beam = new List<Node>(next.Values);
            beam.Sort((a, b) => b.score.CompareTo(a.score));
            if (beam.Count > Beam)
            {
                var keep = beam.GetRange(0, Beam * 2 / 3);
                int stride = Mathf.Max(1, (beam.Count - keep.Count) / (Beam / 3));
                for (int i = keep.Count; i < beam.Count && keep.Count < Beam; i += stride) keep.Add(beam[i]);
                beam = keep;
            }
        }
        return (false, $"timeout; best pos {beam[0].p.pos} world {beam[0].world}", null);
    }

    static string Path(Node n)
    {
        var acts = new List<string>();
        for (; n != null && n.act != null; n = n.parent) acts.Add(n.act);
        acts.Reverse();
        return string.Join(" ", acts);
    }

    static bool Hit(MonsterBox[] boxes, Rect r, int w)
    {
        foreach (var m in boxes)
            if (m.world == w && m.box.Overlaps(r)) return true;
        return false;
    }
}
