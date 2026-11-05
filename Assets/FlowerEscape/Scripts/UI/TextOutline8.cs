using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FlowerEscape.UI
{
    /// <summary>
    /// 8-direction text outline that approximates Figma's "outside" text stroke
    /// (Unity's built-in Outline only uses 4 diagonal copies, which looks jagged at 1.5px+).
    /// </summary>
    [AddComponentMenu("UI/Effects/Text Outline 8")]
    public class TextOutline8 : BaseMeshEffect
    {
        public Color outlineColor = new Color32(0x2a, 0x3c, 0x5e, 0xff);
        [Min(0f)] public float width = 1.5f;

        static readonly List<UIVertex> s_Verts = new List<UIVertex>();
        static readonly Vector2[] s_Dirs =
        {
            new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
        };

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || width <= 0f) return;
            s_Verts.Clear();
            vh.GetUIVertexStream(s_Verts);
            int original = s_Verts.Count;
            var result = new List<UIVertex>(original * (s_Dirs.Length + 1));
            foreach (var d in s_Dirs)
            {
                Vector3 off = d * width;
                for (int i = 0; i < original; i++)
                {
                    var v = s_Verts[i];
                    v.position += off;
                    Color32 c = outlineColor;
                    c.a = (byte)(c.a * v.color.a / 255);
                    v.color = c;
                    result.Add(v);
                }
            }
            result.AddRange(s_Verts);
            vh.Clear();
            vh.AddUIVertexTriangleStream(result);
        }
    }
}
