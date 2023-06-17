using System.Text.RegularExpressions;
using RT.Dijkstra;
using RT.Util;
using RT.Util.Consoles;
using RT.Util.ExtensionMethods;

namespace IWannaLockpick
{
    public static class Solver
    {
        public static void Run()
        {
            // Types:
            //   K = key
            //   N = negative key
            //   R = reverse key
            //   X = exact key
            //   Y = exact negative key
            //   S = star key
            //   U = unstar key
            //   0 = door
            //   1 = frozen door (req 1R)
            //   2 = painted door (req 3B)
            //   4 = withered door (req 5G)
            //   8 = negative door
            //   g = cursed door
            // Special colors:
            //   m = master key
            //   u = pure
            //   n = brown/curse
            //   (r/b/g for frozen/painted/withered)
            var state = new LockpickGameState(@"
████████████████████████████████████████████████████████████████████
████████8-cp8-cp84cc    0App8-cp████    0-pp8-cp████0-pc8-cc    ████
████████████████████    ████████████    ████████████████████    ████
████████████████████                                            ████
████████████████████                    ████████████████████    ████
████0-pc8-cp####K4pp                    08pp0-pc8-cp0-pc████    ████
████████████████████                    ████████████████████    ████
████████████████████                    01pp0-pc████████████    ████
████████████████████                    ████████████████████82cc████
████!!!!0Bpp                            02pp0-pc████████████8-cp████
████████████████████████████████████████████████████████████████████
");
            var result = FindSolutions(state, Array.Empty<Step<int, string>>(), new HashSet<Node<int, string>>()).MaxElement(sol => ((LockpickGameState) sol.Last().Node).Keys.Get('p', 0));
            Console.WriteLine($"Best score: {((LockpickGameState) result.Last().Node).Keys.Get('p', 0)}");
            //var result = DijkstrasAlgorithm.Run(state, 0, (a, b) => a + b, out var totalWeight);
            ConsoleUtil.WriteLine(result.Select(step => new ConsoleColoredString($"{((LockpickGameState) step.Node).ToColoredString(step.Label)}\n{step.Label}")).JoinColoredString("\n\n"));
        }

        private static IEnumerable<Step<int, string>[]> FindSolutions(Node<int, string> state, Step<int, string>[] sofar, HashSet<Node<int, string>> already)
        {
            if (state.IsFinal)
            {
                Console.WriteLine($"Found solution with score {((LockpickGameState) state).Keys.Get('p', 0)}.");
                yield return sofar;
                yield break;
            }
            foreach (var edge in state.Edges)
                if (already.Add(edge.Node))
                    foreach (var result in FindSolutions(edge.Node, sofar.Insert(sofar.Length, new Step<int, string>(edge.Node, edge.Label)), already))
                        yield return result;
        }
    }

    class LockpickGameState : Node<int, string>
    {
        public static readonly Dictionary<char, int> LetterCounts = "A=24,B=256"
            .Split(',').Select(str => str.Split('=')).ToDictionary(arr => arr[0][0], arr => int.Parse(arr[1]));

        public int X { get; private set; }
        public int Y { get; private set; }
        public string[] Board { get; private set; }
        public Dictionary<char, int> Keys { get; private set; }
        public HashSet<char> StarredKeys { get; private set; }
        public HashSet<char> Toggles { get; private set; }
        public override bool IsFinal => Board[Y].Substring(4 * X, 4) == "!!!!";

        public LockpickGameState(int x, int y, string[] board, Dictionary<char, int> keys, HashSet<char> starredKeys, HashSet<char> toggles)
        {
            X = x;
            Y = y;
            Board = board;
            Keys = keys.Where(kvp => kvp.Value != 0).ToDictionary();
            StarredKeys = starredKeys;
            Toggles = toggles;
        }

        public LockpickGameState(string boardDescription)
        {
            if (boardDescription == null)
                throw new ArgumentNullException(nameof(boardDescription));
            Board = boardDescription.Trim().Replace("\r", "").Split('\n');
            if (Board.Any(row => row.Length != Board[0].Length || row.Length % 4 != 0))
                throw new ArgumentException("Rows must be equal length and divisible by 4.", nameof(boardDescription));
            Y = Board.IndexOf(b => Regex.IsMatch(b, @"^(....)*####(....)*$"));
            X = Regex.Match(Board[Y], @"^((?:....)*)####(?:....)*$").Groups[1].Length / 4;
            Board = Board.Replace(Y, Board[Y].Remove(4 * X, 4).Insert(4 * X, "    "));
            Keys = new Dictionary<char, int>();
            StarredKeys = new HashSet<char>();
            Toggles = new HashSet<char>();
        }

        private IEnumerable<int> adjacent(int x, int y, int w)
        {
            if (x > 0)
                yield return x - 1 + w * y;
            if (x < w - 1)
                yield return x + 1 + w * y;
            if (y > 0)
                yield return x + w * (y - 1);
            if (y < Board.Length - 1)
                yield return x + w * (y + 1);
        }

        public override bool Equals(Node<int, string> other) => other is LockpickGameState state && state.X == X && state.Y == Y && Board.SequenceEqual(state.Board) &&
            Keys.All(kvp => state.Keys.Get(kvp.Key, -1) == kvp.Value) && state.Keys.All(kvp => Keys.Get(kvp.Key, -1) == kvp.Value) &&
            StarredKeys.All(state.StarredKeys.Contains) && state.StarredKeys.All(StarredKeys.Contains) &&
            Toggles.All(state.Toggles.Contains) && state.Toggles.All(Toggles.Contains);

        public override int GetHashCode() => Ut.ArrayHash(Board, X, Y,
            Keys.Where(kvp => kvp.Value > 0).OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}{(StarredKeys.Contains(kvp.Key) ? "*" : "")}={kvp.Value}").JoinString(";"),
            StarredKeys.Order().JoinString(), Toggles.Order().JoinString());

        public override string ToString() =>
            Board.Select((str, row) => (row == Y ? str.Remove(4 * X, 4).Insert(4 * X, "####") : str).JoinString())
                .JoinString("\n") +
            (Keys.Any() ? $"    {Keys.Select(kvp => $"{kvp.Key}{(StarredKeys.Contains(kvp.Key) ? "*" : "")}={kvp.Value}").JoinString(", ")}" : "") +
            (Toggles.Any() ? $"    Toggles: {Toggles.JoinString(", ")}" : "");

        public override IEnumerable<Edge<int, string>> Edges
        {
            get
            {
                var w = Board[Y].Length / 4;
                var h = Board.Length;

                var reachable = new HashSet<int> { X + w * Y };
                while (true)
                {
                    bool isEmptyCell(int c)
                    {
                        var x = (c % w) * 4;
                        var row = Board[c / w];
                        return row.Substring(x, 4) == "    " || (row[x] == 'P' && row[x + (Toggles.Contains(row[x + 1]) ? 3 : 2)] == ' ');
                    }

                    var count = reachable.Count;
                    reachable.AddRange(reachable.SelectMany(c => adjacent(c % w, c / w, w).Where(adj => isEmptyCell(adj))).ToArray());
                    if (reachable.Count == count)
                        break;
                }

                var __debug_output = false;

                var edges = new List<Edge<int, string>>();
                var output = new List<ConsoleColoredString>();
                var stuff = Board.Select((str, row) => (row == Y ? str.Remove(4 * X, 4).Color(null).Insert(4 * X, "####".Color(ConsoleColor.White, ConsoleColor.DarkGreen)) : str).Color('█', ConsoleColor.DarkBlue)).ToArray();
                foreach (var candidate in reachable.SelectMany(c => adjacent(c % w, c / w, w)).Distinct())
                    foreach (var edge in tryMove(candidate % w, candidate / w))
                    {
                        edges.Add(edge);
                        if (__debug_output)
                        {
                            if (edge.Label == null)
                                continue;
                            ConsoleColor? fore = null, back = null;
                            if (edge.Label.StartsWith("Open")) { fore = ConsoleColor.White; back = ConsoleColor.DarkCyan; }
                            else if (edge.Label.StartsWith("Use master key")) { fore = ConsoleColor.Yellow; back = ConsoleColor.DarkCyan; }
                            else if (edge.Label.StartsWith("Curse")) { fore = ConsoleColor.White; back = ConsoleColor.DarkYellow; }
                            else if (edge.Label.StartsWith("Uncurse")) { fore = ConsoleColor.Black; back = ConsoleColor.DarkYellow; }
                            else if (edge.Label.StartsWith("Unfreeze")) { fore = ConsoleColor.Red; back = ConsoleColor.DarkRed; }
                            else if (edge.Label.StartsWith("Unpaint")) { fore = ConsoleColor.Blue; back = ConsoleColor.DarkBlue; }
                            else if (edge.Label.StartsWith("Unwither")) { fore = ConsoleColor.Green; back = ConsoleColor.DarkGreen; }
                            else if (edge.Label.StartsWith("Pick up")) { fore = ConsoleColor.White; back = ConsoleColor.DarkMagenta; }
                            var m = Regex.Match(edge.Label, @"at (\d+),(\d+)");
                            if (m.Success && int.TryParse(m.Groups[1].Value, out var x) && int.TryParse(m.Groups[2].Value, out var y))
                                stuff[y] = stuff[y].ColorSubstring(4 * x, 4, fore, back);
                            output.Add(new ConsoleColoredString(edge.Label, fore, back) + "   " +
                                ((LockpickGameState) edge.Node).Apply(s => s.Keys.Select(k => $"{k.Key}{(s.StarredKeys.Contains(k.Key) ? "*" : "")}={k.Value}").Concat(s.StarredKeys.Except(s.Keys.Keys).Select(k => $"{k}*=0")).JoinString("; ")));
                        }
                    }
                if (__debug_output)
                {
                    ConsoleUtil.WriteLine(stuff.JoinColoredString("\n") + "    " + Keys.Select(kvp => $"{kvp.Key}{(StarredKeys.Contains(kvp.Key) ? "*" : "")}={kvp.Value}").Concat(StarredKeys.Except(Keys.Keys).Select(k => $"{k}*=0")).JoinString(", "));
                    foreach (var outp in output)
                        ConsoleUtil.WriteLine(outp);
                    Console.WriteLine();
                }
                return edges;
            }
        }

        private IEnumerable<Edge<int, string>> tryMove(int nx, int ny)
        {
            Dictionary<TK, TV> replace<TK, TV>(Dictionary<TK, TV> dic, TK key, TV value) where TK : notnull
            {
                var copy = dic.ToDictionary();
                copy[key] = value;
                return copy;
            }
            HashSet<TK> with<TK>(HashSet<TK> hashset, TK value) { var h = new HashSet<TK>(hashset) { value }; return h; }
            HashSet<TK> without<TK>(HashSet<TK> hashset, TK value) { var h = new HashSet<TK>(hashset); h.Remove(value); return h; }

            var newTile = Board[ny].Substring(nx * 4, 4);
            if (newTile == "████" || newTile == "    ")
                yield break;

            var dist = (int) Math.Ceiling(Math.Sqrt((X - nx) * (X - nx) + (Y - ny) * (Y - ny)));

            // Reach the exit
            if (newTile == "!!!!")
            {
                yield return new Edge<int, string>(dist, $"Exit at {nx},{ny}", new LockpickGameState(nx, ny, Board, Keys, StarredKeys, Toggles));
                yield break;
            }

            var type = newTile[0];
            var numCh = newTile[1];
            var num = numCh >= '0' && numCh <= '9' ? numCh - '0' : numCh == '-' ? -1 : LetterCounts.Get(numCh, -1);
            var colorReq = newTile[2];
            var colorCh = newTile[3];
            var newBoard = Board.Replace(ny, Board[ny].Remove(4 * nx, 4).Insert(4 * nx, "    "));
            var isNegative = false;

            // Pick up a key
            if (type == 'K' || (type == 'N' && (isNegative = true)))
                yield return new Edge<int, string>(dist, $"Pick up {num} {colorReq} key at {nx},{ny}",
                    new LockpickGameState(nx, ny, newBoard, replace(Keys, colorReq, Keys.Get(colorReq, 0) + (StarredKeys.Contains(colorReq) ? 0 : isNegative ? -num : num)), StarredKeys, Toggles));

            // Pick up an exact key
            if (type == 'X' || (type == 'Y' && (isNegative = true)))
                yield return new Edge<int, string>(dist, $"Pick up {num} {colorReq} exact key at {nx},{ny}",
                    new LockpickGameState(nx, ny, newBoard, replace(Keys, colorReq, isNegative ? -num : num), StarredKeys, Toggles));

            // Pick up a star/unstar key
            if (type == 'S' || (type == 'U' && (isNegative = true)))
                yield return new Edge<int, string>(dist, $"Pick up {num} {colorReq} {(isNegative ? "unstar" : "star")} key at {nx},{ny}",
                    new LockpickGameState(nx, ny, newBoard, Keys, isNegative ? without(StarredKeys, colorReq) : with(StarredKeys, colorReq), Toggles));

            // Do something with a door
            const string str = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (str.Contains(type))
            {
                var bitfield = str.IndexOf(type);
                var isFrozen = (bitfield & 1) != 0;     // req 1R
                var isPainted = (bitfield & 2) != 0;     // req 3B
                var isWithered = (bitfield & 4) != 0;    // req 5G
                isNegative = (bitfield & 8) != 0;
                var isCursed = (bitfield & 16) != 0;

                // Open?
                var actualColorReq = isCursed ? 'n' : colorReq;
                var actualColorCh = isCursed ? 'n' : colorCh;
                if (!isFrozen && !isPainted && !isWithered &&
                    (
                        // Brown doors can be used regardless of cursed status
                        colorReq == 'n' ||
                        // Uncursed doors can only be used if you don’t have positive brown keys, unless they are pure
                        (!isCursed && (colorReq == 'u' || Keys.Get('n', 0) <= 0)) ||
                        // Cursed doors can only be used if you don’t have negative brown keys
                        (isCursed && Keys.Get('n', 0) >= 0)
                    ) &&
                    (
                        // To open a blast door, you must have a positive/negative amount of its color
                        (num == -1 && !isNegative && Keys.Get(actualColorReq, 0) > 0) ||
                        (num == -1 && isNegative && Keys.Get(actualColorReq, 0) < 0) ||
                        // To open a zero door, you must have exactly zero of the relevant key color
                        (num == 0 && Keys.Get(actualColorReq, 0) == 0) ||
                        // To open a normal door, you must have the relevant amount of positive or negative keys
                        (num > 0 && !isNegative && Keys.Get(actualColorReq, 0) >= num) ||
                        (num > 0 && isNegative && Keys.Get(actualColorReq, 0) <= -num)
                    )
                )
                    yield return new Edge<int, string>(dist, $"Open{(isNegative ? " negative" : "")}{(isCursed ? " cursed" : "")} {(num == -1 ? "blast" : num.ToString())} {colorReq} door at {nx},{ny}",
                        new LockpickGameState(nx, ny, newBoard, replace(Keys, actualColorCh,
                            Keys.Get(actualColorCh, 0) + (StarredKeys.Contains(actualColorCh) ? 0 : num == -1 ? -Keys.Get(actualColorReq, 0) : num == 0 ? 0 : (isNegative ? num : -num))), StarredKeys, Toggles));

                // Open with master key? (can’t be pure, or uncursed master door, or cursed when negative brown keys present)
                if (colorReq != 'u' && (colorReq != 'm' || (isCursed && Keys.Get('n', 0) >= 0)) && !isFrozen && !isPainted && !isWithered && Keys.Get('m', 0) > 0)
                    yield return new Edge<int, string>(dist, $"Use master key to open{(isNegative ? " negative" : "")}{(isCursed ? " cursed" : "")} {(num == -1 ? "blast" : num.ToString())} {colorReq} door at {nx},{ny}",
                        new LockpickGameState(nx, ny, newBoard, replace(Keys, 'm', Keys.Get('m', 0) - 1), StarredKeys, Toggles));

                // Curse a door? (can’t be pure or brown)
                if (!isCursed && colorReq != 'u' && colorReq != 'n' && Keys.Get('n', 0) > 0)
                    yield return new Edge<int, string>(dist, $"Curse {(num == -1 ? "blast" : num.ToString())} {colorReq} door at {nx},{ny}",
                        new LockpickGameState(X, Y, newBoard.Replace(ny, newBoard[ny].Remove(4 * nx, 4).Insert(4 * nx, $"{str[bitfield + 16]}{newTile[1]}{newTile[2]}{newTile[3]}")), Keys, StarredKeys, Toggles));

                // Uncurse a door? (can’t be pure or brown)
                if (isCursed && colorReq != 'u' && colorReq != 'n' && Keys.Get('n', 0) < 0)
                    yield return new Edge<int, string>(dist, $"Uncurse {(num == -1 ? "blast" : num.ToString())} {colorReq} door at {nx},{ny}",
                        new LockpickGameState(X, Y, newBoard.Replace(ny, newBoard[ny].Remove(4 * nx, 4).Insert(4 * nx, $"{str[bitfield - 16]}{newTile[1]}{newTile[2]}{newTile[3]}")), Keys, StarredKeys, Toggles));

                // Unfreeze?
                if (isFrozen && Keys.Get('r', 0) >= 1)
                    yield return new Edge<int, string>(dist, $"Unfreeze {colorReq} door at {nx},{ny}",
                        new LockpickGameState(X, Y, newBoard.Replace(ny, newBoard[ny].Remove(4 * nx, 4).Insert(4 * nx, $"{str[bitfield - 1]}{newTile[1]}{newTile[2]}{newTile[3]}")), Keys, StarredKeys, Toggles));

                // Unpaint?
                if (isPainted && Keys.Get('b', 0) >= 3)
                    yield return new Edge<int, string>(dist, $"Unpaint {colorReq} door at {nx},{ny}",
                        new LockpickGameState(X, Y, newBoard.Replace(ny, newBoard[ny].Remove(4 * nx, 4).Insert(4 * nx, $"{str[bitfield - 2]}{newTile[1]}{newTile[2]}{newTile[3]}")), Keys, StarredKeys, Toggles));

                // Unwither?
                if (isWithered && Keys.Get('g', 0) >= 5)
                    yield return new Edge<int, string>(dist, $"Unwither {colorReq} door at {nx},{ny}",
                        new LockpickGameState(X, Y, newBoard.Replace(ny, newBoard[ny].Remove(4 * nx, 4).Insert(4 * nx, $"{str[bitfield - 4]}{newTile[1]}{newTile[2]}{newTile[3]}")), Keys, StarredKeys, Toggles));
            }

            // Trigger a toggle?
            if (newTile[0] == 'T')
                yield return new Edge<int, string>(dist, $"Trigger toggle at {nx},{ny}", new LockpickGameState(nx, ny, newBoard, Keys, StarredKeys, Toggles.Contains(newTile[1]) ? without(Toggles, newTile[1]) : with(Toggles, newTile[1])));
        }

        public ConsoleColoredString ToColoredString(string label)
        {
            var match = label.NullOr(l => Regex.Match(l, @"at (\d+),(\d+)"));
            return Board.Select((str, row) => str.Split(4).Select((chunk, col) =>
                (chunk == "████" ? chunk.Color(ConsoleColor.DarkBlue) : (col == X && row == Y) ? "####".Color(ConsoleColor.White, ConsoleColor.Magenta) : chunk)
                    .Apply(ch => match != null && match.Success && int.TryParse(match.Groups[1].Value, out var x) && x == col && int.TryParse(match.Groups[2].Value, out var y) && y == row ? ch.Color(ConsoleColor.Yellow, ConsoleColor.DarkGreen) : ch)
            ).JoinColoredString()).JoinColoredString("\n") +
                "    " + Keys.Select(kvp => $"{kvp.Key}={kvp.Value}").JoinString(", ");
        }
    }
}
