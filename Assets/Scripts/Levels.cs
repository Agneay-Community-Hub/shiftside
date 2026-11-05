/// Level data. Each row is read top to bottom; '.' and ' ' are empty.
///
///  #  solid in both worlds        1  solid only in SOL       2  solid only in UMBRA
///  ^  spikes in both worlds       x  spikes only in SOL      y  spikes only in UMBRA
///  a  walker living in SOL        b  walker living in UMBRA
///  f  flyer living in SOL         g  flyer living in UMBRA
///  |  turn marker (all monsters)  (  turn marker (SOL only)  )  turn marker (UMBRA only)
///  P  player start (always SOL)   E  exit portal             C  checkpoint     *  shard
public class LevelDef
{
    public readonly string Name, Hint;
    public readonly string[] Rows;

    public LevelDef(string name, string hint, params string[] rows)
    {
        Name = name;
        Hint = hint;
        Rows = rows;
    }
}

public static class Levels
{
    public static readonly LevelDef[] All =
    {
        new LevelDef("The Broken Bridge",
            "That bridge is broken... in this world. Press SHIFT to cross into the other one.",
            "................................",
            "................................",
            "...............*................",
            "...P.......................E....",
            "#########222222222222###########",
            "#########............###########",
            "#########^^^^^^^^^^^^###########",
            "################################"),

        new LevelDef("Ghost Steps",
            "Faded platforms belong to the other world. Jump through them, then SHIFT in mid-air to land.",
            "..............................",
            "..............................",
            "...........................E..",
            ".........................#####",
            "................*.............",
            "....................1111......",
            "..............................",
            "...............2222...........",
            "..............................",
            "..........1111................",
            "..............................",
            ".....2222.....................",
            "..P.......^^^^^^^^^^^^^^^^^^^^",
            "##############################"),

        new LevelDef("Walls Between",
            "You can't shift into something solid. Find the gap, then SHIFT.",
            "#################################",
            "#.......1....2....1.2......2....#",
            "#.......1....2....1.2......2....#",
            "#.P.....1....2....1.2..xxx.2..E.#",
            "#################################"),

        new LevelDef("Residents",
            "Others live in the other world too. Their ghosts can't hurt you... unless you shift into them.",
            "........................................",
            "............................*...........",
            "........................................",
            "........................................",
            "..P..|.....b......|..|......a.....|..E..",
            "######222222222222####111111111111######",
            "######............####............######",
            "........................................"),

        new LevelDef("Freefall",
            "Look before you leap. SHIFT while falling to catch the right ledge.",
            "###########",
            "#.....P####",
            "#.....#####",
            "#......####",
            "#......####",
            "#......####",
            "#....|.####",
            "#..2222####",
            "#......####",
            "#....f.####",
            "#......####",
            "#......####",
            "#1111..####",
            "#......####",
            "#....|.####",
            "#......#..#",
            "#........E#",
            "#.22222####",
            "#^^^^^^####",
            "###########"),

        new LevelDef("Night Shift",
            "No room to jump. Wait on stone that exists in both worlds, and let them pass you by.",
            "###############################",
            "...............................",
            ".P.|....b..........*...b...|.E.",
            "####2222222#2222222#2222222####",
            "####.......................####",
            "...............................",
            "..............................."),

        new LevelDef("Crossfire",
            "When one world gets crowded, the other one might not be.",
            "####################################",
            "#...........1.......2.......1......#",
            "#P.(..).a...1..b....2.a.....1...E..#",
            "####################################",
            "####################################"),

        new LevelDef("Unfortunately",
            "That broken bridge might exist in the other world. Unfortunately, so might the monster standing on it.",
            ".........................................................",
            ".........................................................",
            ".........................................................",
            "............*............................................",
            ".........................................................",
            ".........................................................",
            "........111111111........................................",
            "..P......yyyyyyy)....b...)..C.(..a....b....a....b..)..E..",
            "#####2222222222222222222222####11111222221111122222######",
            "#####......................####....................######",
            ".........................................................",
            "........................................................."),
    };

    public static int TotalShards
    {
        get
        {
            int n = 0;
            foreach (var l in All)
                foreach (var r in l.Rows)
                    foreach (char c in r)
                        if (c == '*') n++;
            return n;
        }
    }
}
