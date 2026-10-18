using System.Collections.Concurrent;

namespace WordBomb.Models;

public class Cell
{
    public bool Mine;
    public bool Revealed;
    public bool Exploded;
    public int PathIndex = -1;   // -1 = không nằm trên đường đi
    public char Letter;
}

public class Game
{
    public string Id = Guid.NewGuid().ToString("N");
    public string Word = "";
    public string Hint = "";
    public int Rows = 6, Cols = 7;
    public Cell[,] Grid = null!;
    public int Lives = 3;
    public (int r, int c)? Pos;
    public bool Over, Won;
    public readonly object Lock = new();

    public int CountMines(int r, int c)
    {
        int n = 0;
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int nr = r + dr, nc = c + dc;
                if (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols && Grid[nr, nc].Mine) n++;
            }
        return n;
    }
}

public static class GameStore
{
    public static readonly ConcurrentDictionary<string, Game> Games = new();
}

public static class GameFactory
{
    static readonly (string Word, string Hint)[] Words =
{
    ("ELEPHANT",  "The largest land animal"),
    ("COMPUTER",  "You are probably using one right now"),
    ("KEYBOARD",  "You type on it"),
    ("MOUNTAIN",  "Climbers love to reach its peak"),
    ("BUTTERFLY", "A caterpillar becomes one"),
    ("CHOCOLATE", "Sweet treat made from cocoa"),
    ("UMBRELLA",  "Keeps you dry in the rain"),
    ("PLANET",    "Earth is one of these"),
    ("GUITAR",    "A six-string instrument"),
    ("DOLPHIN",   "Intelligent sea mammal"),
};

    static readonly (int dr, int dc)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    public static Game Create(double mineRate = 0.3)
    {
        var rnd = new Random();
        var (word, hint) = Words[rnd.Next(Words.Length)];
        var g = new Game { Word = word, Hint = hint };

        List<(int r, int c)>? path;
        do { path = TryPath(rnd, g.Rows, g.Cols, word.Length); } while (path == null);

        g.Grid = new Cell[g.Rows, g.Cols];
        for (int r = 0; r < g.Rows; r++)
            for (int c = 0; c < g.Cols; c++)
                g.Grid[r, c] = new Cell { Mine = rnd.NextDouble() < mineRate };

        for (int i = 0; i < path.Count; i++)
        {
            var cell = g.Grid[path[i].r, path[i].c];
            cell.Mine = false;           // đường đi luôn an toàn
            cell.PathIndex = i;
            cell.Letter = word[i];
        }
        return g;
    }

    // DFS ngẫu nhiên: đường tự không cắt, dài đúng L ô, đi từ hàng 0 xuống hàng cuối
    static List<(int r, int c)>? TryPath(Random rnd, int R, int C, int L)
    {
        var path = new List<(int r, int c)> { (0, rnd.Next(C)) };
        var used = new bool[R, C];
        used[path[0].r, path[0].c] = true;
        int budget = 5000;

        bool Dfs()
        {
            var (r, c) = path[^1];
            if (path.Count == L) return r == R - 1;
            if (L - path.Count < R - 1 - r) return false;   // không kịp xuống đáy
            if (--budget < 0) return false;

            foreach (var (dr, dc) in Dirs.OrderBy(_ => rnd.Next()))
            {
                int nr = r + dr, nc = c + dc;
                if (nr < 0 || nr >= R || nc < 0 || nc >= C || used[nr, nc]) continue;
                used[nr, nc] = true; path.Add((nr, nc));
                if (Dfs()) return true;
                path.RemoveAt(path.Count - 1); used[nr, nc] = false;
            }
            return false;
        }
        return Dfs() ? path : null;
    }
}