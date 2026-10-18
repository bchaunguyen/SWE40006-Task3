using Microsoft.AspNetCore.Mvc;
using WordBomb.Models;

namespace WordBomb.Controllers;

public record MoveReq(string Id, int R, int C);
public record GuessReq(string Id, string Word);

public class HomeController : Controller
{
    public IActionResult Index() => View();

    [HttpPost]
    public IActionResult New()
    {
        var g = GameFactory.Create();
        GameStore.Games[g.Id] = g;
        return Json(ToDto(g, "Pick a tile in the top row to begin."));
    }

    [HttpPost]
    public IActionResult Reveal([FromBody] MoveReq req)
    {
        if (!GameStore.Games.TryGetValue(req.Id, out var g)) return NotFound();
        lock (g.Lock)
        {
            if (g.Over) return Json(ToDto(g, "The game is over."));
            if (req.R < 0 || req.R >= g.Rows || req.C < 0 || req.C >= g.Cols)
                return BadRequest();

            if (g.Pos == null)
            {
                if (req.R != 0) return Json(ToDto(g, "You must start in the top row."));
            }
            else
            {
                int dist = Math.Abs(req.R - g.Pos.Value.r) + Math.Abs(req.C - g.Pos.Value.c);
                if (dist != 1) return Json(ToDto(g, "You can only move up, down, left or right."));
            }

            var cell = g.Grid[req.R, req.C];
            if (cell.Mine)
            {
                cell.Exploded = true;
                g.Lives--;
                if (g.Lives <= 0) g.Over = true;
                return Json(ToDto(g, g.Over ? "Out of lives!" : "💥 You hit a mine! Lost 1 life."));
            }

            cell.Revealed = true;
            g.Pos = (req.R, req.C);
            return Json(ToDto(g, ""));
        }
    }

    [HttpPost]
    public IActionResult Guess([FromBody] GuessReq req)
    {
        if (!GameStore.Games.TryGetValue(req.Id, out var g)) return NotFound();
        lock (g.Lock)
        {
            if (g.Over) return Json(ToDto(g, "The game is over."));

            if (string.Equals(req.Word?.Trim(), g.Word, StringComparison.OrdinalIgnoreCase))
            {
                g.Won = g.Over = true;
                return Json(ToDto(g, "🎉 Correct!"));
            }
            g.Lives--;
            if (g.Lives <= 0) g.Over = true;
            return Json(ToDto(g, g.Over ? "Wrong guess. Out of lives!" : "Wrong guess. Lost 1 life."));
        }
    }

    static object ToDto(Game g, string message)
    {
        var revealed = new List<object>();
        var exploded = new List<object>();
        var slots = new string[g.Word.Length];
        Array.Fill(slots, "");

        for (int r = 0; r < g.Rows; r++)
            for (int c = 0; c < g.Cols; c++)
            {
                var cell = g.Grid[r, c];
                if (cell.Exploded) exploded.Add(new { r, c });
                if (!cell.Revealed) continue;
                bool onPath = cell.PathIndex >= 0;
                if (onPath) slots[cell.PathIndex] = cell.Letter.ToString();
                revealed.Add(new
                {
                    r,
                    c,
                    n = g.CountMines(r, c),
                    letter = onPath ? cell.Letter.ToString() : null
                });
            }

        return new
        {
            id = g.Id,
            rows = g.Rows,
            cols = g.Cols,
            lives = g.Lives,
            hint = g.Hint,
            length = g.Word.Length,
            slots,
            revealed,
            exploded,
            pos = g.Pos == null ? null : new { r = g.Pos.Value.r, c = g.Pos.Value.c },
            over = g.Over,
            won = g.Won,
            word = g.Over ? g.Word : null,
            message
        };
    }
}