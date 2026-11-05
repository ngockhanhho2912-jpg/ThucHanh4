using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlowerEscape.UI
{
    public enum GameplayState { BuyGear, Combat }

    [Serializable]
    public class ShopItemView
    {
        public Button button;
        public int cost;                 // 0 = free (watch ad)
        public GameObject content;       // flower + price tag, hidden when bought
        public Image flower;
        public Text level;
        [NonSerialized] public bool sold;
    }

    [Serializable]
    public class BoosterView
    {
        public Button button;
        public Text count;
        [NonSerialized] public int value;
    }

    /// <summary>
    /// Drives the main gameplay screen of Flower Escape in its two states:
    ///  - BuyGear: the gear board is large, the shop + Refresh/START bar is visible.
    ///  - Combat : the board shrinks to the bottom-left, boosters on the right, boat fights the wave.
    /// All references are wired by the editor builder (Flower Escape > Build Gameplay Scene).
    /// </summary>
    public class FlowerEscapeGameplayUI : MonoBehaviour
    {
        [Header("State roots")]
        public CanvasGroup buyGearRoot;
        public CanvasGroup combatRoot;
        public GameplayState startState = GameplayState.BuyGear;
        public float transitionTime = 0.28f;

        [Header("HUD")]
        public Text sunText;
        public Text levelText;
        public Text distanceText;
        public Text speedText;
        public RectTransform progressFill;
        public RectTransform distancePointer;
        public Button speedButton;
        public Button closeButton;

        [Header("Buy gear")]
        public Button startButton;
        public Button refreshButton;
        public Button adSunButton;
        public ShopItemView[] shopItems = Array.Empty<ShopItemView>();
        public Sprite[] shopFlowerPool = Array.Empty<Sprite>();

        [Header("Combat")]
        public BoosterView[] boosters = Array.Empty<BoosterView>();
        public float waveDuration = 15f;

        [Header("Economy")]
        public int sun = 165;
        public int refreshCost = 5;
        public int adReward = 50;
        public int waveReward = 20;
        public int level = 12;

        public GameplayState State { get; private set; }

        float _progressStartWidth;
        float _progressMaxWidth = 275f;
        float _pointerOffset;
        float _textOffset;
        float _distance = 123f;
        bool _fast;
        Coroutine _transition;
        Coroutine _wave;

        void Awake()
        {
            if (progressFill != null) _progressStartWidth = progressFill.sizeDelta.x;
            if (progressFill != null && distancePointer != null)
                _pointerOffset = distancePointer.anchoredPosition.x - (progressFill.anchoredPosition.x + progressFill.sizeDelta.x);
            if (distancePointer != null && distanceText != null)
                _textOffset = distanceText.rectTransform.anchoredPosition.x - distancePointer.anchoredPosition.x;

            foreach (var b in boosters) b.value = ParseInt(b.count, 3);

            Hook(startButton, () => SwitchTo(GameplayState.Combat));
            Hook(refreshButton, RefreshShop);
            Hook(adSunButton, () => AddSun(adReward));
            Hook(speedButton, ToggleSpeed);
            Hook(closeButton, () => { if (State == GameplayState.Combat) SwitchTo(GameplayState.BuyGear); });
            for (int i = 0; i < shopItems.Length; i++)
            {
                var item = shopItems[i];
                Hook(item.button, () => Buy(item));
            }
            for (int i = 0; i < boosters.Length; i++)
            {
                var b = boosters[i];
                Hook(b.button, () => UseBooster(b));
            }
        }

        void Start()
        {
            SetStateImmediate(startState);
            RefreshTexts();
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                SwitchTo(State == GameplayState.Combat ? GameplayState.BuyGear : GameplayState.Combat);
#endif
        }

        void OnDisable()
        {
            if (_fast) Time.timeScale = 1f;
        }

        static void Hook(Button b, Action a)
        {
            if (b == null) return;
            b.onClick.AddListener(() => a());
        }

        static int ParseInt(Text t, int fallback) =>
            t != null && int.TryParse(t.text, out int v) ? v : fallback;

        // ---------------------------------------------------------------- state

        public void SetStateImmediate(GameplayState s)
        {
            State = s;
            Apply(buyGearRoot, s == GameplayState.BuyGear ? 1f : 0f, 1f);
            Apply(combatRoot, s == GameplayState.Combat ? 1f : 0f, 1f);
            if (s == GameplayState.Combat && Application.isPlaying) StartWave();
        }

        public void SwitchTo(GameplayState s)
        {
            if (s == State && _transition == null) return;
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(Transition(s));
        }

        IEnumerator Transition(GameplayState to)
        {
            CanvasGroup from = to == GameplayState.Combat ? buyGearRoot : combatRoot;
            CanvasGroup into = to == GameplayState.Combat ? combatRoot : buyGearRoot;
            State = to;
            if (_wave != null) { StopCoroutine(_wave); _wave = null; }

            float half = transitionTime * 0.5f;
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                float k = t / half;
                Apply(from, 1f - k, Mathf.Lerp(1f, 0.96f, k));
                yield return null;
            }
            Apply(from, 0f, 1f);
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / half);
                Apply(into, k, Mathf.Lerp(1.04f, 1f, k));
                yield return null;
            }
            Apply(into, 1f, 1f);
            _transition = null;
            if (to == GameplayState.Combat) StartWave();
        }

        static void Apply(CanvasGroup g, float alpha, float scale)
        {
            if (g == null) return;
            g.alpha = alpha;
            g.interactable = alpha > 0.99f;
            g.blocksRaycasts = alpha > 0.99f;
            g.gameObject.SetActive(alpha > 0f);
            g.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // ---------------------------------------------------------------- combat

        void StartWave()
        {
            if (_wave != null) StopCoroutine(_wave);
            _wave = StartCoroutine(Wave());
        }

        IEnumerator Wave()
        {
            float startW = progressFill != null ? progressFill.sizeDelta.x : 0f;
            float targetW = Mathf.Min(_progressMaxWidth, startW + 56f);   // advance to the next skull marker
            float startDist = _distance;
            for (float t = 0f; t < waveDuration; t += Time.deltaTime)
            {
                float k = t / waveDuration;
                SetProgress(Mathf.Lerp(startW, targetW, k));
                _distance = startDist + 40f * k;
                if (distanceText != null) distanceText.text = Mathf.RoundToInt(_distance) + "m";
                yield return null;
            }
            SetProgress(targetW >= _progressMaxWidth ? _progressStartWidth : targetW);
            if (targetW >= _progressMaxWidth) { level++; RefreshTexts(); }
            AddSun(waveReward);
            _wave = null;
            SwitchTo(GameplayState.BuyGear);
        }

        void SetProgress(float width)
        {
            if (progressFill == null) return;
            progressFill.sizeDelta = new Vector2(width, progressFill.sizeDelta.y);
            if (distancePointer != null)
            {
                var p = distancePointer.anchoredPosition;
                distancePointer.anchoredPosition = new Vector2(progressFill.anchoredPosition.x + width + _pointerOffset, p.y);
                if (distanceText != null)
                {
                    var dt = distanceText.rectTransform;
                    dt.anchoredPosition = new Vector2(distancePointer.anchoredPosition.x + _textOffset, dt.anchoredPosition.y);
                }
            }
        }

        void UseBooster(BoosterView b)
        {
            if (State != GameplayState.Combat || b.value <= 0) return;
            b.value--;
            if (b.count != null) b.count.text = b.value.ToString();
            if (b.value <= 0 && b.button != null) b.button.interactable = false;
        }

        void ToggleSpeed()
        {
            _fast = !_fast;
            Time.timeScale = _fast ? 2f : 1f;
            if (speedText != null) speedText.text = _fast ? "x1" : "x2";
        }

        // ---------------------------------------------------------------- shop

        void Buy(ShopItemView item)
        {
            if (State != GameplayState.BuyGear || item.sold || item.content == null) return;
            if (item.cost > sun) { StartCoroutine(Shake(sunText != null ? sunText.rectTransform : null)); return; }
            AddSun(-item.cost);
            item.sold = true;
            if (item.content != null) item.content.SetActive(false);
        }

        void RefreshShop()
        {
            if (State != GameplayState.BuyGear) return;
            if (sun < refreshCost) { StartCoroutine(Shake(sunText != null ? sunText.rectTransform : null)); return; }
            AddSun(-refreshCost);
            foreach (var item in shopItems)
            {
                if (item.content == null) continue;
                item.sold = false;
                if (item.content != null) item.content.SetActive(true);
                if (item.flower != null && shopFlowerPool.Length > 0)
                    item.flower.sprite = shopFlowerPool[UnityEngine.Random.Range(0, shopFlowerPool.Length)];
                if (item.level != null) item.level.text = (1 << UnityEngine.Random.Range(0, 3)).ToString();
            }
        }

        void AddSun(int delta)
        {
            sun = Mathf.Max(0, sun + delta);
            RefreshTexts();
        }

        void RefreshTexts()
        {
            if (sunText != null) sunText.text = sun.ToString();
            if (levelText != null) levelText.text = "Level " + level;
        }

        static IEnumerator Shake(RectTransform rt)
        {
            if (rt == null) yield break;
            Vector2 p = rt.anchoredPosition;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                rt.anchoredPosition = p + new Vector2(Mathf.Sin(t * 60f) * 3f * (1f - t / 0.3f), 0f);
                yield return null;
            }
            rt.anchoredPosition = p;
        }
    }
}
