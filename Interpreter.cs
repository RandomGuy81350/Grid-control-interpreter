
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;

class Program
{
    const int GridSize = 32;

    struct Cell
    {
        public bool Visible;
        public ConsoleColor Color;
    }

    static Cell[,] grid = new Cell[GridSize, GridSize];
    static Cell[,] prevGrid = new Cell[GridSize, GridSize];
    static Dictionary<string, int> vars = new Dictionary<string, int>();
    static Dictionary<string, List<string>> arrs = new Dictionary<string, List<string>>();
    static Random rng = new Random();

    static void Main()
    {
        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
            {
                grid[i, j].Visible = true;
                grid[i, j].Color = ConsoleColor.White;
                prevGrid[i, j] = grid[i, j];
            }

        FullDraw();

        string[] lines = File.ReadAllLines("script.gyat");
        RunScript(lines);

        Console.ResetColor();
        Console.SetCursorPosition(0, GridSize);
    }

    static void RunScript(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string line = StripComments(raw).Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Array declaration
            if (line.StartsWith("let "))
            {
                var parts = line.Substring(4).Split(new[] { '=' }, 2);
                if (parts.Length != 2) continue;
                string name = parts[0].Trim();
                string expr = parts[1].Trim().TrimEnd(';');

                if (expr.StartsWith("{") && expr.EndsWith("}"))
                {
                    string inside = expr.Substring(1, expr.Length - 2);
                    var items = inside.Split(',').Select(s => s.Trim()).ToList();
                    arrs[name] = items;
                }
                else if (expr.StartsWith("irandom(") && expr.EndsWith(")"))
                {
                    int open = expr.IndexOf('('), comma = expr.IndexOf(','), close = expr.LastIndexOf(')');
                    int min = GetValue(expr.Substring(open + 1, comma - open - 1).Trim());
                    int max = GetValue(expr.Substring(comma + 1, close - comma - 1).Trim());
                    vars[name] = rng.Next(min, max + 1);
                }
                else
                {
                    vars[name] = GetValue(expr);
                }
            }
            else if (line.Contains("+="))
            {
                var parts = line.Split(new[] { "+=" }, StringSplitOptions.None);
                string name = parts[0].Trim();
                int val = GetValue(parts[1].Trim().TrimEnd(';'));
                vars[name] = (vars.ContainsKey(name) ? vars[name] : 0) + val;
            }
            else if (line.Contains("-="))
            {
                var parts = line.Split(new[] { "-=" }, StringSplitOptions.None);
                string name = parts[0].Trim();
                int val = GetValue(parts[1].Trim().TrimEnd(';'));
                vars[name] = (vars.ContainsKey(name) ? vars[name] : 0) - val;
            }
            else if (line.StartsWith("wait(")) // 🔹 NEW wait() function
            {
                int open = line.IndexOf('('), close = line.LastIndexOf(')');
                int ms = GetValue(line.Substring(open + 1, close - open - 1).Trim());
                Thread.Sleep(ms);
            }
            else if (line.StartsWith("loop("))
            {
                int open = line.IndexOf('('), comma = line.IndexOf(','), close = line.IndexOf(')');
                int count = GetValue(line.Substring(open + 1, comma - open - 1).Trim());
                int delay = GetValue(line.Substring(comma + 1, close - comma - 1).Trim());

                var body = ExtractBlock(lines, ref i);

                for (int k = 0; k < count; k++)
                {
                    RunScript(body.ToArray());
                    DrawGrid();
                    Thread.Sleep(delay);
                }
            }
            else if (line.StartsWith("while("))
            {
                int open = line.IndexOf('('), close = line.IndexOf(')');
                string cond = line.Substring(open + 1, close - open - 1).Trim();

                var whileBody = ExtractBlock(lines, ref i);

                while (EvaluateCondition(cond))
                {
                    RunScript(whileBody.ToArray());
                    DrawGrid();
                }
            }
            else if (line.StartsWith("C("))
            {
                int open = line.IndexOf('('), comma = line.IndexOf(','), close = line.IndexOf(')');
                string xExp = line.Substring(open + 1, comma - open - 1).Trim();
                string yExp = line.Substring(comma + 1, close - comma - 1).Trim();

                int x = GetValue(xExp);
                int y = GetValue(yExp);

                if (x >= 1 && x <= GridSize && y >= 1 && y <= GridSize)
                {
                    if (line.Contains(".visible=flip"))
                        grid[y - 1, x - 1].Visible = !grid[y - 1, x - 1].Visible;
                    else if (line.EndsWith("true;"))
                        grid[y - 1, x - 1].Visible = true;
                    else if (line.EndsWith("false;"))
                        grid[y - 1, x - 1].Visible = false;
                    else if (line.Contains(".color="))
                    {
                        string rhs = line.Split('=')[1].Trim().TrimEnd(';');
                        string colorName = ResolveValue(rhs);
                        grid[y - 1, x - 1].Color = ParseColor(colorName);
                    }

                    DrawGrid();
                }
            }
            else if (line.StartsWith("if("))
            {
                int open = line.IndexOf('('), close = line.IndexOf(')');
                string cond = line.Substring(open + 1, close - open - 1).Trim();

                bool result = EvaluateCondition(cond);

                var ifBody = ExtractBlock(lines, ref i);

                int next = NextNonEmptyIndex(lines, i + 1);
                List<string> elseBody = null;
                if (next != -1)
                {
                    string maybeElse = StripComments(lines[next]).Trim();
                    if (maybeElse.StartsWith("else"))
                    {
                        i = next;
                        elseBody = ExtractBlock(lines, ref i);
                    }
                }

                if (result)
                    RunScript(ifBody.ToArray());
                else if (elseBody != null)
                    RunScript(elseBody.ToArray());
            }
            else if (line.StartsWith("Crange2(")) // NEW fast rectangle
            {
                int open = line.IndexOf('(');
                int close = line.IndexOf(')');
                string inside = line.Substring(open + 1, close - open - 1);
                var parts = inside.Split(',');
                if (parts.Length != 4) throw new Exception("Crange2 requires 4 arguments");

                int x1 = GetValue(parts[0].Trim());
                int y1 = GetValue(parts[1].Trim());
                int x2 = GetValue(parts[2].Trim());
                int y2 = GetValue(parts[3].Trim());

                if (x1 > x2) (x1, x2) = (x2, x1);
                if (y1 > y2) (y1, y2) = (y2, y1);

                // Apply changes
                for (int yy = y1; yy <= y2; yy++)
                {
                    for (int xx = x1; xx <= x2; xx++)
                    {
                        if (xx >= 1 && xx <= GridSize && yy >= 1 && yy <= GridSize)
                        {
                            if (line.Contains(".visible=flip"))
                                grid[yy - 1, xx - 1].Visible = !grid[yy - 1, xx - 1].Visible;
                            else if (line.EndsWith("true;"))
                                grid[yy - 1, xx - 1].Visible = true;
                            else if (line.EndsWith("false;"))
                                grid[yy - 1, xx - 1].Visible = false;
                            else if (line.Contains(".color="))
                            {
                                string rhs = line.Split('=')[1].Trim().TrimEnd(';');
                                string colorName = ResolveValue(rhs);
                                grid[yy - 1, xx - 1].Color = ParseColor(colorName);
                            }
                        }
                    }
                }

                // Instant draw of just the region
                DrawRegion(x1, y1, x2, y2);
            }
            else if (line.StartsWith("Crange("))
            {
                int open = line.IndexOf('(');
                int close = line.IndexOf(')');
                string inside = line.Substring(open + 1, close - open - 1);
                var parts = inside.Split(',');
                if (parts.Length != 4) throw new Exception("Crange requires 4 arguments");

                int x1 = GetValue(parts[0].Trim());
                int y1 = GetValue(parts[1].Trim());
                int x2 = GetValue(parts[2].Trim());
                int y2 = GetValue(parts[3].Trim());

                if (x1 > x2) (x1, x2) = (x2, x1);
                if (y1 > y2) (y1, y2) = (y2, y1);

                for (int yy = y1; yy <= y2; yy++)
                {
                    for (int xx = x1; xx <= x2; xx++)
                    {
                        if (xx >= 1 && xx <= GridSize && yy >= 1 && yy <= GridSize)
                        {
                            if (line.Contains(".visible=flip"))
                                grid[yy - 1, xx - 1].Visible = !grid[yy - 1, xx - 1].Visible;
                            else if (line.EndsWith("true;"))
                                grid[yy - 1, xx - 1].Visible = true;
                            else if (line.EndsWith("false;"))
                                grid[yy - 1, xx - 1].Visible = false;
                            else if (line.Contains(".color="))
                            {
                                string rhs = line.Split('=')[1].Trim().TrimEnd(';');
                                string colorName = ResolveValue(rhs);
                                grid[yy - 1, xx - 1].Color = ParseColor(colorName);
                            }
                        }
                    }
                }

                DrawGrid();
            }
        }
    }

    static string StripComments(string line)
    {
        int idx = line.IndexOf("//");
        return idx >= 0 ? line.Substring(0, idx) : line;
    }

    static int NextNonEmptyIndex(string[] lines, int start)
    {
        for (int j = start; j < lines.Length; j++)
        {
            if (!string.IsNullOrWhiteSpace(StripComments(lines[j]).Trim()))
                return j;
        }
        return -1;
    }

    static int CountChar(string s, char c)
    {
        int cnt = 0;
        foreach (var ch in s)
            if (ch == c) cnt++;
        return cnt;
    }

    static List<string> ExtractBlock(string[] lines, ref int i)
    {
        int openLine = -1;
        for (int j = i; j < lines.Length; j++)
        {
            string t = StripComments(lines[j]);
            if (t.Contains("{"))
            {
                openLine = j;
                break;
            }
        }
        if (openLine == -1) throw new Exception($"Missing '{{' at line {i + 1}");

        int depth = 0;
        int closeLine = -1;
        for (int j = openLine; j < lines.Length; j++)
        {
            string t = StripComments(lines[j]);
            depth += CountChar(t, '{');
            depth -= CountChar(t, '}');
            if (depth == 0)
            {
                closeLine = j;
                break;
            }
        }
        if (closeLine == -1) throw new Exception($"Missing '}}' for block at line {openLine + 1}");

        var body = new List<string>();
        for (int k = openLine + 1; k <= closeLine - 1; k++)
            body.Add(lines[k]);

        i = closeLine;
        return body;
    }

    static bool EvaluateCondition(string cond)
    {
        string[] ops = { ">=", "<=", "!=", "==", "=", ">", "<" };
        string op = ops.FirstOrDefault(o => cond.Contains(o));
        if (op == null) throw new Exception($"Invalid condition: {cond}");

        string[] parts = cond.Split(new string[] { op }, StringSplitOptions.None);
        if (parts.Length != 2) throw new Exception($"Invalid condition: {cond}");

        int left = GetValue(parts[0].Trim());
        int right = GetValue(parts[1].Trim());

        switch (op)
        {
            case "=":
            case "==": return left == right;
            case "!=": return left != right;
            case ">": return left > right;
            case "<": return left < right;
            case ">=": return left >= right;
            case "<=": return left <= right;
            default: return false;
        }
    }

    static int GetValue(string expr)
    {
        expr = expr.Trim().TrimEnd(';');

        List<string> tokens = new List<string>();
        StringBuilder sb = new StringBuilder();
        foreach (char c in expr)
        {
            if (c == '+' || c == '-')
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString().Trim()); sb.Clear(); }
                tokens.Add(c.ToString());
            }
            else
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString().Trim());

        int result = EvalToken(tokens[0]);
        for (int i = 1; i < tokens.Count; i += 2)
        {
            string op = tokens[i];
            int val = EvalToken(tokens[i + 1]);
            if (op == "+") result += val;
            else if (op == "-") result -= val;
        }

        return result;
    }

    static int EvalToken(string s)
    {
        s = s.Trim();

        if (vars.ContainsKey(s)) return vars[s];

        if (arrs.ContainsKey(s)) return arrs[s].Count;

        if (int.TryParse(s, out int num)) return num;

        if (s.StartsWith("C(") && s.EndsWith(")"))
        {
            string inside = s.Substring(2, s.Length - 3);
            var parts = inside.Split(',');
            if (parts.Length != 2) throw new Exception("C(x,y) requires 2 arguments");

            int x = GetValue(parts[0].Trim());
            int y = GetValue(parts[1].Trim());

            if (x >= 1 && x <= GridSize && y >= 1 && y <= GridSize)
                return grid[y - 1, x - 1].Visible ? 1 : 0;
            return 0;
        }

        if (s.Contains("[") && s.EndsWith("]"))
        {
            int open = s.IndexOf('[');
            string name = s.Substring(0, open);
            string idxExp = s.Substring(open + 1, s.Length - open - 2);
            int idx = GetValue(idxExp);

            if (arrs.ContainsKey(name))
            {
                var list = arrs[name];
                if (idx >= 1 && idx <= list.Count)
                {
                    string val = list[idx - 1];
                    if (int.TryParse(val, out int n)) return n;
                    return !string.IsNullOrEmpty(val) && val != "0" ? 1 : 0;
                }
            }
        }

        return 0;
    }

    static string ResolveValue(string s)
    {
        s = s.Trim();
        if (vars.ContainsKey(s)) return vars[s].ToString();

        if (arrs.ContainsKey(s)) return arrs[s].FirstOrDefault() ?? "";

        if (s.Contains("[") && s.EndsWith("]"))
        {
            int open = s.IndexOf('[');
            string name = s.Substring(0, open);
            string idxExp = s.Substring(open + 1, s.Length - open - 2);
            int idx = GetValue(idxExp);

            if (arrs.ContainsKey(name))
            {
                var list = arrs[name];
                if (idx >= 1 && idx <= list.Count)
                    return list[idx - 1];
            }
        }

        return s;
    }

    static ConsoleColor ParseColor(string name)
    {
        return name.ToLower() switch
        {
            "red" => ConsoleColor.Red,
            "green" => ConsoleColor.Green,
            "white" => ConsoleColor.White,
            "blue" => ConsoleColor.Blue,
            "magenta" => ConsoleColor.Magenta,
            "yellow" => ConsoleColor.Yellow,
            "cyan" => ConsoleColor.Cyan,
            "black" => ConsoleColor.Black,
            "gray" => ConsoleColor.Gray,
            _ => ConsoleColor.White
        };
    }

    static void FullDraw()
    {
        Console.SetCursorPosition(0, 0);

        for (int i = 0; i < GridSize; i++)
        {
            int j = 0;
            while (j < GridSize)
            {
                ConsoleColor runColor = grid[i, j].Color;
                StringBuilder sb = new StringBuilder();

                while (j < GridSize && grid[i, j].Color == runColor)
                {
                    sb.Append(grid[i, j].Visible ? "█" : " ");
                    prevGrid[i, j] = grid[i, j];
                    j++;
                }

                Console.ForegroundColor = runColor;
                Console.Write(sb.ToString());
            }

            Console.WriteLine();
        }
    }

    static void DrawGrid()
    {
        for (int i = 0; i < GridSize; i++)
        {
            for (int j = 0; j < GridSize; j++)
            {
                if (grid[i, j].Visible != prevGrid[i, j].Visible ||
                    grid[i, j].Color != prevGrid[i, j].Color)
                {
                    Console.SetCursorPosition(j, i);
                    Console.ForegroundColor = grid[i, j].Color;
                    Console.Write(grid[i, j].Visible ? "█" : " ");
                    prevGrid[i, j] = grid[i, j];
                }
            }
        }
    }
    static void DrawRegion(int x1, int y1, int x2, int y2)
    {
        // Clamp to valid script-space (1..GridSize)
        if (x1 < 1) x1 = 1;
        if (y1 < 1) y1 = 1;
        if (x2 > GridSize) x2 = GridSize;
        if (y2 > GridSize) y2 = GridSize;

        for (int i = y1 - 1; i < y2; i++)
        {
            Console.SetCursorPosition(x1 - 1, i);

            int j = x1 - 1;
            while (j < x2)
            {
                ConsoleColor runColor = grid[i, j].Color;
                StringBuilder sb = new StringBuilder();

                while (j < x2 && grid[i, j].Color == runColor)
                {
                    sb.Append(grid[i, j].Visible ? "█" : " ");
                    prevGrid[i, j] = grid[i, j];
                    j++;
                }

                Console.ForegroundColor = runColor;
                Console.Write(sb.ToString());
            }
        }
    }
}
