using System;
using System.Collections.Generic;

/// <summary>
/// The one lettering of every work surface: small block capitals on a 3 × 5 grid, each cell
/// <see cref="Cell"/> × <see cref="Cell"/> pixels at scale 1 (<see cref="Height"/> pixels tall, about eight a letter),
/// proportional where it reads better. Small letters are written as capitals, a letter with an accent as the plain one,
/// typographic quotes and dashes as their plain signs; anything else as a space (<see cref="HasGlyph"/> says which do
/// have one). Here are the glyphs and the measuring - how wide a text is, cut to fit, broken into lines - so a test can
/// hold every name to them; <c>CraftArt.Label</c> draws with them.
/// </summary>
public static class CraftLettering
{
    /// <summary>Pixels a glyph cell, at scale 1.</summary>
    public const int Cell = 2;
    /// <summary>A label's height in pixels at scale 1 (its capitals).</summary>
    public const float Height = 5 * Cell;

    // Five rows each, top first, '#' lit. Most glyphs are three cells wide; M, W, % and & five and N four, so they cannot
    // be taken for H, X or 8; the stops, the colon and the apostrophe one. Letters are capitals only (small ones are drawn
    // as these). Digits are all three wide, so a changing reading does not jump about.
    private static readonly Dictionary<char, string> Rows = new Dictionary<char, string>
    {
        ['A'] = ".#.|#.#|###|#.#|#.#", ['B'] = "##.|#.#|##.|#.#|##.", ['C'] = ".##|#..|#..|#..|.##", ['D'] = "##.|#.#|#.#|#.#|##.",
        ['E'] = "###|#..|##.|#..|###", ['F'] = "###|#..|##.|#..|#..", ['G'] = ".##|#..|#.#|#.#|.##", ['H'] = "#.#|#.#|###|#.#|#.#",
        ['I'] = "###|.#.|.#.|.#.|###", ['J'] = "..#|..#|..#|#.#|.#.", ['K'] = "#.#|##.|#..|##.|#.#", ['L'] = "#..|#..|#..|#..|###",
        ['M'] = "#...#|##.##|#.#.#|#...#|#...#", ['N'] = "#..#|##.#|#.##|#..#|#..#", ['O'] = ".#.|#.#|#.#|#.#|.#.",
        ['P'] = "##.|#.#|##.|#..|#..", ['Q'] = ".#.|#.#|#.#|##.|.##", ['R'] = "##.|#.#|##.|#.#|#.#", ['S'] = ".##|#..|.#.|..#|##.",
        ['T'] = "###|.#.|.#.|.#.|.#.", ['U'] = "#.#|#.#|#.#|#.#|###", ['V'] = "#.#|#.#|#.#|#.#|.#.",
        ['W'] = "#...#|#...#|#.#.#|##.##|#...#", ['X'] = "#.#|#.#|.#.|#.#|#.#", ['Y'] = "#.#|#.#|.#.|.#.|.#.", ['Z'] = "###|..#|.#.|#..|###",
        ['0'] = "###|#.#|#.#|#.#|###", ['1'] = ".#.|##.|.#.|.#.|###", ['2'] = "##.|..#|.#.|#..|###", ['3'] = "##.|..#|.#.|..#|##.",
        ['4'] = "#.#|#.#|###|..#|..#", ['5'] = "###|#..|##.|..#|##.", ['6'] = ".##|#..|###|#.#|###", ['7'] = "###|..#|.#.|.#.|.#.",
        ['8'] = "###|#.#|###|#.#|###", ['9'] = "###|#.#|###|..#|##.",
        [' '] = "..|..|..|..|..", ['.'] = ".|.|.|.|#", [','] = "..|..|..|.#|#.", [':'] = ".|#|.|#|.", [';'] = "..|.#|..|.#|#.",
        ['\''] = "#|#|.|.|.", ['"'] = "#.#|#.#|...|...|...", ['!'] = "#|#|#|.|#", ['?'] = "##.|..#|.#.|...|.#.",
        ['-'] = "...|...|###|...|...", ['+'] = "...|.#.|###|.#.|...", ['='] = "...|###|...|###|...", ['_'] = "...|...|...|...|###",
        ['/'] = "..#|..#|.#.|#..|#..", ['\\'] = "#..|#..|.#.|..#|..#", ['('] = ".#|#.|#.|#.|.#", [')'] = "#.|.#|.#|.#|#.",
        ['['] = "##|#.|#.|#.|##", [']'] = "##|.#|.#|.#|##", ['<'] = "..#|.#.|#..|.#.|..#", ['>'] = "#..|.#.|..#|.#.|#..",
        ['%'] = "##..#|##.#.|..#..|.#.##|#..##", ['°'] = ".#.|#.#|.#.|...|...", ['×'] = "...|#.#|.#.|#.#|...",
        ['#'] = "#.#|###|#.#|###|#.#", ['&'] = ".##..|#..#.|.##..|#..##|.##.#", ['*'] = "#.#|.#.|#.#|...|...",
        ['…'] = ".....|.....|.....|.....|#.#.#",
    };

    // Letters with an accent (in capitals) and typographic signs, and the glyph each is drawn with.
    private const string Accented = "ÀÁÂÃÄÅĀĂĄÇĆČĎÈÉÊËĒĖĘĚÌÍÎÏĪĮŁÑŃŇÒÓÔÕÖØŌŐŔŘŚŠŞŤÙÚÛÜŪŮŰÝŸŹŻŽ‘’‚′“”„″–—‒−· ";
    private const string Plain = "AAAAAAAAACCCDEEEEEEEEIIIIIILNNNOOOOOOOORRSSSTUUUUUUUYYZZZ''''\"\"\"\"----. ";

    /// <summary>A glyph: how many cells wide, and its five rows (bit k lit = cell k from the left).</summary>
    public readonly struct Glyph
    {
        public readonly int Width;
        private readonly int r0, r1, r2, r3, r4;

        public Glyph(int width, int[] rows)
        {
            Width = width;
            r0 = rows[0];
            r1 = rows[1];
            r2 = rows[2];
            r3 = rows[3];
            r4 = rows[4];
        }

        /// <summary>Row <paramref name="r"/> (0 the top): bit k lit = cell k from the left.</summary>
        public int Row(int r) => r == 0 ? r0 : r == 1 ? r1 : r == 2 ? r2 : r == 3 ? r3 : r4;
    }

    private static Dictionary<char, Glyph> glyphs;

    private static Dictionary<char, Glyph> Glyphs
    {
        get
        {
            if (glyphs != null)
                return glyphs;
            var set = new Dictionary<char, Glyph>();
            foreach (KeyValuePair<char, string> kv in Rows)
            {
                string[] rows = kv.Value.Split('|');
                var bits = new int[5];
                for (int r = 0; r < 5 && r < rows.Length; r++)
                    for (int k = 0; k < rows[r].Length; k++)
                        if (rows[r][k] == '#') bits[r] |= 1 << k;
                set[kv.Key] = new Glyph(rows[0].Length, bits);
            }
            return glyphs = set;
        }
    }

    /// <summary>The character <paramref name="ch"/> is written as: itself, its capital, or the plain letter or sign of an accented one; '\0' when it has no glyph (it is written as a space).</summary>
    private static char Drawn(char ch)
    {
        if (Glyphs.ContainsKey(ch))
            return ch;
        char up = char.ToUpperInvariant(ch);
        if (Glyphs.ContainsKey(up))
            return up;
        int i = Accented.IndexOf(up);
        return i >= 0 && Glyphs.ContainsKey(Plain[i]) ? Plain[i] : '\0';
    }

    /// <summary>Is <paramref name="ch"/> written as itself (or its capital, or the plain letter of an accented one) - not as a space for want of a glyph?</summary>
    public static bool HasGlyph(char ch) => Drawn(ch) != '\0';

    /// <summary>The glyph <paramref name="ch"/> is drawn with: a small letter as its capital, a letter with an accent as the plain one, anything else unknown as a space.</summary>
    public static Glyph Of(char ch)
    {
        char d = Drawn(ch);
        return Glyphs[d != '\0' ? d : ' '];
    }

    /// <summary>How wide <paramref name="text"/> is written (pixels, from its left edge to its last lit cell's right edge, the shadow aside).</summary>
    public static float Width(string text, int scale = 1)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;
        int cells = 0;
        foreach (char ch in text)
            cells += Of(ch).Width + 1;
        return (cells - 1) * Cell * Math.Max(1, scale);
    }

    /// <summary><paramref name="text"/> cut to fit <paramref name="width"/> pixels, the cut marked with a full stop (the whole text when it fits).</summary>
    public static string Fit(string text, float width, int scale = 1)
    {
        if (string.IsNullOrEmpty(text) || Width(text, scale) <= width)
            return text ?? "";
        int cell = Cell * Math.Max(1, scale);
        float stop = Of('.').Width * cell, used = 0f;
        int n = 0;
        for (; n < text.Length; n++)
        {
            float w = (Of(text[n]).Width + 1) * cell;
            if (used + w + stop > width)
                break;
            used += w;
        }
        return n == 0 ? "" : text.Substring(0, n).TrimEnd() + ".";
    }

    /// <summary>
    /// <paramref name="text"/> broken at its spaces into lines no wider than <paramref name="width"/> pixels, no more than
    /// <paramref name="lines"/> of them (the last cut short with a full stop when the words run on; a word too long for a
    /// line is cut too).
    /// </summary>
    public static List<string> Wrap(string text, float width, int lines, int scale = 1)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(text) || lines <= 0)
            return list;
        string line = "";
        string[] words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            string tried = line.Length == 0 ? word : line + " " + word;
            if (Width(tried, scale) <= width)
            {
                line = tried;
                continue;
            }
            if (line.Length > 0)
            {
                if (list.Count == lines - 1)
                {
                    list.Add(Fit(line + " " + string.Join(" ", words, i, words.Length - i), width, scale));
                    return list;
                }
                list.Add(line);
            }
            line = Fit(word, width, scale);
        }
        if (line.Length > 0 && list.Count < lines)
            list.Add(line);
        return list;
    }
}
