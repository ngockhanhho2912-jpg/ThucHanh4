using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace FlowerEscape.EditorTools
{
    /// <summary>One visible layer of a Figma frame (image or text), in Figma coordinates (top-left origin, px).</summary>
    public class FigmaLeaf
    {
        public string frame;
        public char kind;          // 'I' image, 'T' text
        public string name;
        public Rect rect;          // x,y from the frame's top-left corner
        public string sprite;
        public bool sliced;
        // text
        public float fontSize;
        public Color color = Color.white;
        public bool hasStroke;
        public Color stroke = Color.black;
        public float strokeWidth;
        public string text;
        // builder bookkeeping
        public bool shared;
        public FigmaLeaf twin;
        public int order;

        public Vector2 Center => rect.center;
        public override string ToString() => $"{frame}/{name} {rect}";
    }

    /// <summary>Parser for Assets/FlowerEscape/Data/figma_layout.txt (exported from the Figma design).</summary>
    public static class FigmaLayout
    {
        public const string MainFrame = "Main";
        public const string BuyGearFrame = "Main - Buy gear";

        public static List<FigmaLeaf> Load(string path)
        {
            var leaves = new List<FigmaLeaf>();
            var remap = new Dictionary<string, (string to, bool sliced)>();
            string frame = null;
            int order = 0;
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.TrimEnd();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split('|');
                switch (p[0])
                {
                    case "R":
                        remap[p[1]] = (p[2], p.Length > 3 && p[3] == "1");
                        break;
                    case "F":
                        frame = p[1];
                        break;
                    case "I":
                        leaves.Add(new FigmaLeaf
                        {
                            frame = frame, kind = 'I', name = p[1], rect = R(p, 2), sprite = p[6], order = order++,
                        });
                        break;
                    case "T":
                        var t = new FigmaLeaf
                        {
                            frame = frame, kind = 'T', name = p[1], rect = R(p, 2), order = order++,
                            fontSize = F(p[6]), color = Hex(p[7]), hasStroke = p[8] != "-",
                            strokeWidth = F(p[9]), text = string.Join("|", p, 11, p.Length - 11).Replace("\\n", "\n"),
                        };
                        if (t.hasStroke) t.stroke = Hex(p[8]);
                        leaves.Add(t);
                        break;
                }
            }
            foreach (var l in leaves)
            {
                if (l.kind == 'I' && l.sprite != null && remap.TryGetValue(l.sprite, out var m))
                {
                    l.sprite = m.to;
                    l.sliced = m.sliced;
                }
            }
            return leaves;
        }

        static Rect R(string[] p, int i) => new Rect(F(p[i]), F(p[i + 1]), F(p[i + 2]), F(p[i + 3]));
        static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
