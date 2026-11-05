using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FlowerEscape.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlowerEscape.EditorTools
{
    /// <summary>
    /// Builds Assets/FlowerEscape/Scenes/Gameplay.unity from the Figma layout export.
    ///
    /// Layout strategy (responsive for 9:21 phones and 3:4 tablets):
    ///  * CanvasScaler 402x874 (the Figma frame) with "Expand": the whole design always fits,
    ///    extra space appears vertically on tall phones and horizontally on tablets.
    ///  * The environment background covers the full screen (CoverFitter).
    ///  * Content lives in a 402px column inside the safe area. Each group is a virtual 402x874
    ///    "Figma frame" pinned either to the top (HUD, battle field) or to the bottom (gear board, shop,
    ///    ad strip) of the column, so the extra height opens up as water between them.
    ///  * Full-width strips (shop bar, ad strip) bleed to the screen edges on wide screens.
    /// </summary>
    public static class FlowerEscapeSceneBuilder
    {
        public const string Root = "Assets/FlowerEscape/";
        public const string LayoutPath = Root + "Data/figma_layout.txt";
        public const string ScenePath = Root + "Scenes/Gameplay.unity";
        public const string FontPath = Root + "Fonts/Lalezar-Regular.ttf";
        const float FrameW = 402f, FrameH = 874f;

        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Flower Escape/Build Gameplay Scene", priority = 0)]
        public static void BuildMenu()
        {
            Build(capture: true);
            EditorUtility.DisplayDialog("Flower Escape", "Gameplay scene built:\n" + ScenePath + "\n\nPreviews: " + PreviewDir, "OK");
        }

        [MenuItem("Flower Escape/Open Gameplay Scene", priority = 1)]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static string PreviewDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "FlowerEscapeBuild"));

        // ===================================================================================== build

        public static void Build(bool capture)
        {
            Log.Clear();
            Log.AppendLine("Build " + DateTime.Now);
            var leaves = FigmaLayout.Load(LayoutPath);
            MarkShared(leaves);
            // Progress / HP bars change width at runtime -> 9-slice their rounded caps (4px at 1x = 12px at 3x).
            foreach (var l in leaves.Where(l => l.kind == 'I' && l.name.StartsWith("slider gameplay_")))
                FlowerEscapeSpriteImporter.SetBorder(l.sprite, new Vector4(12, 0, 12, 0));

            Scene previousActive = SceneManager.GetActiveScene();
            Scene existing = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = existing.IsValid() && existing.isLoaded;
            Scene scene;
            if (wasOpen && SceneManager.sceneCount == 1)
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                if (wasOpen) EditorSceneManager.CloseScene(existing, true);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }
            SceneManager.SetActiveScene(scene);

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) Log.AppendLine("WARN font missing: " + FontPath);

            // ---- camera + event system
            var camGo = new GameObject("UI Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1b, 0x3a, 0x5c, 0xff);
            cam.cullingMask = 1 << 5;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif

            // ---- canvas
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(FrameW, FrameH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var bgLayer = Stretch("Background", canvasGo.transform);
            var safe = Stretch("SafeArea", canvasGo.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var column = NewRect("Column", safe);
            column.anchorMin = new Vector2(0.5f, 0f);
            column.anchorMax = new Vector2(0.5f, 1f);
            column.pivot = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(FrameW, 0f);
            column.anchoredPosition = Vector2.zero;

            var buyGear = Stretch("State_BuyGear", column);
            var buyGearGroup = buyGear.gameObject.AddComponent<CanvasGroup>();
            var buyTop = PinnedFrame("Top", buyGear, top: true);
            var buyBottom = PinnedFrame("Bottom", buyGear, top: false);

            var combat = Stretch("State_Combat", column);
            var combatGroup = combat.gameObject.AddComponent<CanvasGroup>();
            var world = PinnedFrame("World", combat, top: true);
            var combatBottom = PinnedFrame("Bottom", combat, top: false);

            var hud = PinnedFrame("HUD", column, top: true);
            var adStrip = PinnedFrame("AdStrip", column, top: false);

            // ---- background (cover the whole screen)
            var bgLeaf = leaves.First(l => l.name == "environment background");
            var bgRt = NewRect("environment background", bgLayer);
            AddImage(bgRt.gameObject, bgLeaf.sprite);
            var cover = bgRt.gameObject.AddComponent<CoverFitter>();
            cover.designSize = bgLeaf.rect.size;
            cover.focus = new Vector2(0.5f, 1f);

            // ---- black strip behind the ad so it reaches the screen edges on 3:4
            var adFill = Leaf("AdStrip_Fill", adStrip, new Rect(0f, 778f, FrameW, 96f));
            adFill.gameObject.AddComponent<Image>().color = Color.black;
            adFill.GetComponent<Image>().raycastTarget = false;
            Bleed(adFill, 0f, FrameW);

            // ---- leaves
            var built = new Dictionary<FigmaLeaf, RectTransform>();
            foreach (var l in leaves.OrderBy(l => l.order))
            {
                if (l == bgLeaf || l.name == "environment background") continue;
                if (l.shared && l.frame == FigmaLayout.BuyGearFrame) continue;   // built once from Main
                RectTransform parent;
                if (l.shared) parent = l.rect.y >= 770f ? adStrip : hud;
                else if (l.frame == FigmaLayout.MainFrame) parent = l.rect.y >= 463f ? combatBottom : world;
                else parent = l.rect.y < 199f ? buyTop : buyBottom;

                var rt = Leaf(l.name, parent, l.rect);
                if (l.kind == 'I')
                {
                    var img = AddImage(rt.gameObject, l.sprite);
                    if (l.sliced) img.type = Image.Type.Sliced;
                    if (l.name.StartsWith("slider gameplay_")) img.type = Image.Type.Sliced;
                }
                else
                {
                    AddText(rt.gameObject, l, font);
                }
                built[l] = rt;
            }

            // ---- controller + interactive wiring
            var ui = canvasGo.AddComponent<FlowerEscapeGameplayUI>();
            ui.buyGearRoot = buyGearGroup;
            ui.combatRoot = combatGroup;
            Wire(ui, leaves, built, buyBottom, combatBottom);

            // full-width shop bar
            foreach (var kv in built.Where(kv => kv.Key.name == "bar bottom buy gear"))
                Bleed(kv.Value, kv.Key.rect.x, kv.Key.rect.width);

            SetLayerRecursively(canvasGo, 5);

            // ---- save
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            Log.AppendLine($"Saved {ScenePath}: {built.Count} layers ({leaves.Count(l => l.shared) / 2} shared)");

            if (capture)
            {
                try { CaptureAll(canvas, cam, ui); }
                catch (Exception e) { Log.AppendLine("Capture failed: " + e); }
            }

            if (!wasOpen || SceneManager.sceneCount > 1)
            {
                if (previousActive.IsValid() && previousActive != scene && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            WriteLog();
        }

        // ===================================================================================== wiring

        static void Wire(FlowerEscapeGameplayUI ui, List<FigmaLeaf> leaves, Dictionary<FigmaLeaf, RectTransform> built,
            RectTransform buyBottom, RectTransform combatBottom)
        {
            RectTransform Find(string frame, string name, Func<FigmaLeaf, bool> extra = null) =>
                built.Where(kv => kv.Key.frame == frame && kv.Key.name == name && (extra == null || extra(kv.Key)))
                     .Select(kv => kv.Value).FirstOrDefault();
            Text T(RectTransform rt) => rt != null ? rt.GetComponent<Text>() : null;
            const string M = FigmaLayout.MainFrame, B = FigmaLayout.BuyGearFrame;

            // HUD
            ui.sunText = T(Find(M, "165"));
            ui.levelText = T(Find(M, "Level 12"));
            ui.distanceText = T(Find(M, "123m"));
            ui.speedText = T(Find(M, "x2"));
            ui.progressFill = Find(M, "slider gameplay_fill_cyan");
            ui.distancePointer = Find(M, "distance pointer");
            ui.speedButton = MakeButton(Find(M, "button_xspeed"));
            ui.closeButton = MakeButton(Find(M, "Group 25"));
            MakeButton(Find(M, "Setting"));
            if (ui.progressFill != null) SetLeftPivot(ui.progressFill);

            // Buy gear bar
            ui.startButton = MakeButton(Find(B, "button_big_cyan"));
            ui.refreshButton = MakeButton(Find(B, "button_big_green"));
            ui.adSunButton = MakeButton(Find(B, "button_big_yellow"));

            // Shop items: everything inside each 82px card column (x = 16 + 96*i, y 595..690)
            var items = new List<ShopItemView>();
            var pool = new List<Sprite>();
            for (int i = 0; i < 4; i++)
            {
                float x0 = 16f + 96f * i, x1 = x0 + 82f;
                var members = built.Where(kv => kv.Key.frame == B && kv.Key.rect.y >= 595f && kv.Key.rect.yMax <= 690f
                                                && kv.Key.Center.x >= x0 && kv.Key.Center.x <= x1)
                                   .OrderBy(kv => kv.Key.order).ToList();
                var card = members.FirstOrDefault(kv => kv.Key.name == "button_buy gear");
                if (card.Value == null) continue;
                var group = Stretch("ShopItem_" + i, buyBottom);
                group.SetSiblingIndex(card.Value.GetSiblingIndex());
                card.Value.SetParent(group, false);
                var content = Stretch("Content", group);
                foreach (var m in members.Where(m => m.Value != card.Value)) m.Value.SetParent(content, false);

                var view = new ShopItemView { button = MakeButton(card.Value), content = content.gameObject };
                var flower = members.FirstOrDefault(m => m.Key.kind == 'I' && m.Key.name.StartsWith("Gear"));
                if (flower.Value != null)
                {
                    view.flower = flower.Value.GetComponent<Image>();
                    if (view.flower.sprite != null && !pool.Contains(view.flower.sprite)) pool.Add(view.flower.sprite);
                }
                view.level = members.Where(m => m.Key.kind == 'T' && m.Key.fontSize > 30f).Select(m => T(m.Value)).FirstOrDefault();
                var price = members.FirstOrDefault(m => m.Key.kind == 'T' && m.Key.fontSize < 30f);
                view.cost = price.Key == null ? -1 : int.TryParse(price.Key.text, out int c) ? c : 0;
                if (members.Count <= 1) view.content = null;   // empty / locked slot
                items.Add(view);
            }
            ui.shopItems = items.ToArray();
            ui.shopFlowerPool = pool.ToArray();

            // Boosters (Main, right column, 73px pitch from y=491)
            var boosters = new List<BoosterView>();
            for (int i = 0; i < 4; i++)
            {
                float y0 = 489f + 73f * i, y1 = y0 + 73f;
                var members = built.Where(kv => kv.Key.frame == M && kv.Key.rect.x >= 330f && kv.Key.rect.y >= y0 && kv.Key.rect.y < y1)
                                   .OrderBy(kv => kv.Key.order).ToList();
                var bg = members.FirstOrDefault(kv => kv.Key.name == "button_booster");
                if (bg.Value == null) continue;
                var group = Stretch("Booster_" + i, combatBottom);
                group.SetSiblingIndex(bg.Value.GetSiblingIndex());
                foreach (var m in members) m.Value.SetParent(group, false);
                boosters.Add(new BoosterView
                {
                    button = MakeButton(bg.Value),
                    count = members.Where(m => m.Key.kind == 'T').Select(m => T(m.Value)).FirstOrDefault(),
                });
            }
            ui.boosters = boosters.ToArray();

            // Idle motion on the battle field
            var crew = new HashSet<string> { "boat", "char nerd", "char emo", "char tomboy", "char sexy", "image 55" };
            var rng = new System.Random(12);
            foreach (var kv in built.Where(kv => kv.Key.frame == M && kv.Key.rect.y < 463f))
            {
                string n = kv.Key.name;
                if (crew.Contains(n)) AddBob(kv.Value, 1.8f, 0.55f, 0f);
                else if (n == "Boss1") AddBob(kv.Value, 3f, 0.4f, 0.3f);
                else if (n.StartsWith("Mob1_")) AddBob(kv.Value, n == "Mob1_Flying" ? 4f : 2f, 0.8f + (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble());
            }

            Log.AppendLine($"Wired: shop={items.Count} boosters={boosters.Count} pool={pool.Count} start={(ui.startButton != null)}");
        }

        static void AddBob(RectTransform rt, float amp, float freq, float phase)
        {
            var b = rt.gameObject.AddComponent<Bobber>();
            b.amplitude = amp;
            b.frequency = freq;
            b.phase = phase;
        }

        static Button MakeButton(RectTransform rt)
        {
            if (rt == null) return null;
            var img = rt.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            var b = rt.gameObject.GetComponent<Button>();
            if (b == null) b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            var colors = b.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            b.colors = colors;
            b.targetGraphic = img;
            rt.gameObject.AddComponent<PressScale>();
            return b;
        }

        static void SetLeftPivot(RectTransform rt)
        {
            float w = rt.sizeDelta.x;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x - w * 0.5f, rt.anchoredPosition.y);
        }

        static void Bleed(RectTransform rt, float x, float w)
        {
            var b = rt.gameObject.AddComponent<HorizontalBleed>();
            b.designX = x;
            b.designWidth = w;
            b.frameWidth = FrameW;
        }

        // ===================================================================================== helpers

        static void MarkShared(List<FigmaLeaf> leaves)
        {
            var main = leaves.Where(l => l.frame == FigmaLayout.MainFrame).ToList();
            foreach (var b in leaves.Where(l => l.frame == FigmaLayout.BuyGearFrame))
            {
                var twin = main.FirstOrDefault(m => !m.shared && m.kind == b.kind && m.name == b.name &&
                                                    Mathf.Abs(m.rect.x - b.rect.x) <= 1.5f && Mathf.Abs(m.rect.y - b.rect.y) <= 1.5f &&
                                                    Mathf.Abs(m.rect.width - b.rect.width) <= 1.5f && Mathf.Abs(m.rect.height - b.rect.height) <= 1.5f);
                if (twin == null) continue;
                twin.shared = b.shared = true;
                twin.twin = b;
                b.twin = twin;
            }
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Stretch(string name, Transform parent)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>A virtual 402x874 Figma frame pinned to the top or bottom edge of the column.</summary>
        static RectTransform PinnedFrame(string name, Transform parent, bool top)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(FrameW, FrameH);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>A layer positioned with Figma coordinates (top-left origin) inside a pinned frame.</summary>
        static RectTransform Leaf(string name, RectTransform frame, Rect r)
        {
            var rt = NewRect(name, frame);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = r.size;
            rt.anchoredPosition = new Vector2(r.x + r.width * 0.5f, -(r.y + r.height * 0.5f));
            return rt;
        }

        static readonly HashSet<string> MissingSprites = new HashSet<string>();

        static Image AddImage(GameObject go, string key)
        {
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FlowerEscapeSpriteImporter.SpriteFolder + key + ".png");
            if (sprite == null)
            {
                if (MissingSprites.Add(key)) Log.AppendLine("WARN missing sprite: " + key);
                img.color = new Color(1f, 0f, 1f, 0.5f);
            }
            img.sprite = sprite;
            img.preserveAspect = false;
            return img;
        }

        static void AddText(GameObject go, FigmaLeaf l, Font font)
        {
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = l.text;
            t.fontSize = Mathf.Max(1, Mathf.RoundToInt(l.fontSize));
            t.color = l.color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            if (l.hasStroke && l.strokeWidth > 0f)
            {
                var o = go.AddComponent<TextOutline8>();
                o.outlineColor = l.stroke;
                o.width = l.strokeWidth;
            }
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
        }

        static void AddToBuildSettings()
        {
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ===================================================================================== previews

        static readonly (int w, int h, string tag)[] PreviewSizes =
        {
            (1080, 2520, "9x21"),
            (1536, 2048, "3x4"),
            (1206, 2622, "figma"),
        };

        [MenuItem("Flower Escape/Render Previews (open scene)", priority = 20)]
        public static void CaptureOpenScene()
        {
            var ui = UnityEngine.Object.FindAnyObjectByType<FlowerEscapeGameplayUI>();
            if (ui == null) { Debug.LogWarning("Open the Gameplay scene first."); return; }
            Log.Clear();
            CaptureAll(ui.GetComponent<Canvas>(), ui.GetComponent<Canvas>().worldCamera, ui);
            WriteLog();
            EditorUtility.RevealInFinder(PreviewDir);
        }

        static void CaptureAll(Canvas canvas, Camera cam, FlowerEscapeGameplayUI ui)
        {
            Directory.CreateDirectory(PreviewDir);
            var states = new[] { GameplayState.BuyGear, GameplayState.Combat };
            foreach (var s in states)
            {
                foreach (var size in PreviewSizes)
                {
                    ui.SetStateImmediate(s);
                    string file = Path.Combine(PreviewDir, $"preview_{s}_{size.tag}.png");
                    Render(canvas, cam, size.w, size.h, file);
                    Log.AppendLine("Preview " + file);
                }
            }
            ui.SetStateImmediate(ui.startState);
        }

        /// <summary>
        /// Renders the canvas at an arbitrary resolution: temporarily switches it to World Space sized
        /// exactly like CanvasScaler(402x874, Expand) would size it on that screen.
        /// </summary>
        static void Render(Canvas canvas, Camera cam, int w, int h, string file)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            var crt = (RectTransform)canvas.transform;
            var mode = canvas.renderMode;
            var oldPos = crt.position; var oldScale = crt.localScale;
            float factor = Mathf.Min(w / FrameW, h / FrameH);
            Vector2 size = new Vector2(w / factor, h / factor);
            const float unit = 0.01f;

            scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            crt.sizeDelta = size;
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.localScale = Vector3.one * unit;
            crt.position = Vector3.zero;
            crt.rotation = Quaternion.identity;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographic = true;
            cam.orthographicSize = size.y * unit * 0.5f;

            foreach (var f in canvas.GetComponentsInChildren<SafeAreaFitter>(true)) { f.ignoreSafeArea = true; f.Apply(); }
            Canvas.ForceUpdateCanvases();
            foreach (var f in canvas.GetComponentsInChildren<CoverFitter>(true)) f.Apply();
            foreach (var f in canvas.GetComponentsInChildren<HorizontalBleed>(true)) f.Apply();
            Canvas.ForceUpdateCanvases();

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            bool rendered = false;
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req))
                {
                    RenderPipeline.SubmitRenderRequest(cam, req);
                    rendered = true;
                }
            }
            catch (Exception e) { Log.AppendLine("SubmitRenderRequest failed, falling back: " + e.Message); }
            if (!rendered) cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            cam.targetTexture = prevTarget;
            cam.ResetAspect();
            RenderTexture.ReleaseTemporary(rt);
            canvas.renderMode = mode;
            crt.localScale = oldScale;
            crt.position = oldPos;
            scaler.enabled = true;
            foreach (var f in canvas.GetComponentsInChildren<SafeAreaFitter>(true)) { f.ignoreSafeArea = false; f.Apply(); }
        }

        static void WriteLog()
        {
            Directory.CreateDirectory(PreviewDir);
            File.WriteAllText(Path.Combine(PreviewDir, "build_log.txt"), Log.ToString());
            Debug.Log("[FlowerEscape] " + Log);
        }
    }
}
