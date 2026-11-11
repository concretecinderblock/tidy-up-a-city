using System;
using System.Text;
using TidyUp.Core;
using TidyUp.Core.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TidyUp.UI
{
    public enum MenuMode
    {
        /// <summary>The start screen (MainMenu scene): continue, new game, load, controls, quit.</summary>
        Main = 0,
        /// <summary>In-game (Esc / Start): resume, save, load, controls, main menu, quit. Pauses the game.</summary>
        Pause = 1
    }

    /// <summary>
    /// Main menu and pause menu in one component. Builds its own grey-box uGUI at runtime (no prefab to keep
    /// in sync). Save slots: three cards with load / save / new game and delete, with confirmations for
    /// anything that replaces or removes a save. Works with mouse, keyboard and gamepad (Esc/Start/B = back).
    /// </summary>
    public sealed class GameMenu : MonoBehaviour
    {
        private enum SlotAction { NewGame, Load, Save }

        private static readonly Color ButtonNormal = new Color(0.96f, 0.87f, 0.7f);
        private static readonly Color ButtonHighlight = new Color(1f, 0.95f, 0.82f);
        private static readonly Color ButtonPressed = new Color(0.85f, 0.73f, 0.55f);
        private static readonly Color ButtonDisabled = new Color(0.62f, 0.58f, 0.54f, 0.55f);
        private static readonly Color Ink = new Color(0.25f, 0.15f, 0.08f);
        private static readonly Color Cream = new Color(1f, 0.95f, 0.84f);

        [SerializeField] private MenuMode _mode = MenuMode.Main;
        [Tooltip("Main menu: the scene a new or loaded game starts in.")]
        [SerializeField] private string _gameScene = "City";
        [Tooltip("Pause menu: save service of the running game (none in test scenes).")]
        [SerializeField] private SaveService _saves;
        [Tooltip("Pause menu: the player's input, to open on Esc/Start and block gameplay while open.")]
        [SerializeField] private PlayerInputReader _input;

        private SaveStorage _storage;
        private Font _font;
        private GameObject _root;
        private GameObject _slotsPanel;
        private GameObject _controlsPanel;
        private GameObject _confirmPanel;
        private Text _panelTitle;
        private Text _status;
        private Text _confirmText;
        private Button _confirmYes;
        private Button _firstButton;
        private Button _continueButton;
        private readonly Text[] _slotTexts = new Text[SaveStorage.SlotCount];
        private readonly Button[] _slotActions = new Button[SaveStorage.SlotCount];
        private readonly Text[] _slotActionLabels = new Text[SaveStorage.SlotCount];
        private readonly Button[] _slotDeletes = new Button[SaveStorage.SlotCount];
        private SlotAction _slotAction;
        private Action _onConfirm;
        private bool _open;
        private bool _onHome;
        private int _openedFrame = -1;

        private SaveStorage Storage => _saves != null ? _saves.Storage : (_storage ??= SaveStorage.Default);
        private bool CanSaveAndLoad => _mode == MenuMode.Main || _saves != null;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUi();
            if (_mode == MenuMode.Main)
            {
                Time.timeScale = 1f;
                Open();
            }
            else _root.SetActive(false);
        }

        private void OnEnable()
        {
            if (_input != null) _input.PauseRequested += Open;
        }

        private void OnDisable()
        {
            if (_input != null) _input.PauseRequested -= Open;
            if (_open && _mode == MenuMode.Pause) Close();
        }

        private void Update()
        {
            if (!_open || Time.frameCount == _openedFrame) return;
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                        (Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame));
            if (back) Back();
        }

        // ───────────── Open / close / navigation ─────────────

        public void Open()
        {
            if (_open) return;
            _open = true;
            _openedFrame = Time.frameCount;
            _root.SetActive(true);
            if (_mode == MenuMode.Pause)
            {
                Time.timeScale = 0f;
                if (_input != null)
                {
                    _input.MenuOpen = true;
                    _input.GameplayBlocked = true;
                }
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowHome();
        }

        public void Close()
        {
            if (!_open || _mode != MenuMode.Pause) return;
            _open = false;
            _root.SetActive(false);
            Time.timeScale = 1f;
            if (_input != null)
            {
                _input.MenuOpen = false;
                _input.GameplayBlocked = false;
                _input.SetCursorLocked(true);
            }
        }

        private void Back()
        {
            if (_confirmPanel.activeSelf) HideConfirm();
            else if (!_onHome) ShowHome();
            else if (_mode == MenuMode.Pause) Close();
        }

        private void ShowHome()
        {
            HideConfirm();
            _onHome = true;
            _slotsPanel.SetActive(false);
            _controlsPanel.SetActive(true); // the controls are always at hand on the home panel
            _panelTitle.text = _mode == MenuMode.Main ? "Welcome back to the city" : "Taking a breather";
            _status.text = _mode == MenuMode.Main
                ? "A mysterious Muddle has scattered the city's things.\nPut them back where they belong. Take your time: nothing can go wrong."
                : (SaveStorage.IsValidSlot(SaveSession.CurrentSlot) ? $"Playing in slot {SaveSession.CurrentSlot}. The game also saves itself when you step outside." : "This game isn't in a save slot yet. Use Save Game to keep it.");
            if (_continueButton != null) _continueButton.interactable = Storage.MostRecentSlot() > 0;
            Select(_firstButton);
        }

        private void ShowSlots(SlotAction action)
        {
            HideConfirm();
            _onHome = false;
            _slotAction = action;
            _controlsPanel.SetActive(false);
            _slotsPanel.SetActive(true);
            _panelTitle.text = action == SlotAction.NewGame ? "New game: choose a slot" : action == SlotAction.Load ? "Load game" : "Save game";
            _status.text = string.Empty;
            RefreshSlots();
            Select(_slotActions[0].interactable ? _slotActions[0] : _slotDeletes[0]);
        }

        private void ShowControls()
        {
            HideConfirm();
            _onHome = false;
            _slotsPanel.SetActive(false);
            _controlsPanel.SetActive(true);
            _panelTitle.text = "Controls";
            _status.text = "Look at things to read their names. Ghost outlines show where things go.";
        }

        private static void Select(Selectable selectable)
        {
            if (EventSystem.current != null && selectable != null) EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        // ───────────── Actions ─────────────

        private void Continue()
        {
            int slot = Storage.MostRecentSlot();
            if (slot > 0) LoadSlot(slot);
        }

        private void HandleSlotAction(int slot)
        {
            switch (_slotAction)
            {
                case SlotAction.NewGame:
                    if (Storage.Exists(slot)) Confirm($"Start a new game in slot {slot}?\nThe save that's there will be replaced.", () => StartNewGame(slot));
                    else StartNewGame(slot);
                    break;
                case SlotAction.Load:
                    if (_mode == MenuMode.Pause) Confirm($"Load slot {slot}?\nAnything since your last save in this game is lost.", () => LoadSlot(slot));
                    else LoadSlot(slot);
                    break;
                case SlotAction.Save:
                    if (Storage.Exists(slot) && slot != SaveSession.CurrentSlot) Confirm($"Save over slot {slot}?\nThe save that's there will be replaced.", () => SaveSlot(slot));
                    else SaveSlot(slot);
                    break;
            }
        }

        private void StartNewGame(int slot)
        {
            SaveSession.RequestNewGame(slot);
            LoadGameScene();
        }

        private void LoadSlot(int slot)
        {
            if (_mode == MenuMode.Pause && _saves != null)
            {
                if (!_saves.LoadSlot(slot, out string error)) ShowStatus(error);
                return;
            }
            if (!Storage.TryRead(slot, out SaveData data, out string readError))
            {
                ShowStatus(readError);
                return;
            }
            SaveSession.RequestLoad(slot, data);
            LoadGameScene();
        }

        private void SaveSlot(int slot)
        {
            if (_saves == null) return;
            bool ok = _saves.SaveToSlot(slot);
            RefreshSlots();
            ShowStatus(ok ? $"Saved to slot {slot}." : "Saving didn't work.");
        }

        private void DeleteSlot(int slot)
        {
            Confirm($"Delete the save in slot {slot}?\nThis can't be undone.", () =>
            {
                Storage.Delete(slot);
                if (SaveSession.CurrentSlot == slot) SaveSession.CurrentSlot = 0; // don't silently recreate it
                RefreshSlots();
                ShowStatus($"Slot {slot} is empty now.");
            });
        }

        private void GoToMainMenu()
        {
            string scene = _saves != null ? _saves.MainMenuScene : "MainMenu";
            bool hasSlot = SaveStorage.IsValidSlot(SaveSession.CurrentSlot);
            Confirm(hasSlot ? "Back to the main menu?\nYour game is saved first." : "Back to the main menu?\nThis game isn't saved in a slot, so its progress is lost.", () =>
            {
                _saves?.Autosave();
                Time.timeScale = 1f;
                SceneManager.LoadScene(scene);
            });
        }

        private void Quit()
        {
            Confirm(_mode == MenuMode.Pause && SaveStorage.IsValidSlot(SaveSession.CurrentSlot) ? "Quit the game?\nYour game is saved first." : "Quit the game?", () =>
            {
                _saves?.Autosave();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private void LoadGameScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_gameScene);
        }

        private void ShowStatus(string message) => _status.text = message ?? string.Empty;

        // ───────────── Slots ─────────────

        private void RefreshSlots()
        {
            for (int i = 0; i < SaveStorage.SlotCount; i++)
            {
                int slot = i + 1;
                bool exists = Storage.TryRead(slot, out SaveData data, out string error);
                bool fileThere = Storage.Exists(slot);
                _slotTexts[i].text = exists ? Describe(slot, data) : fileThere ? $"<b>Slot {slot}</b>\n<color=#9A3B2E>{error}</color>" : $"<b>Slot {slot}</b>\n<color=#8A7A6A>Empty</color>";

                switch (_slotAction)
                {
                    case SlotAction.NewGame:
                        _slotActionLabels[i].text = exists || fileThere ? "Start here" : "Start";
                        _slotActions[i].interactable = true;
                        break;
                    case SlotAction.Load:
                        _slotActionLabels[i].text = "Load";
                        _slotActions[i].interactable = exists;
                        break;
                    case SlotAction.Save:
                        _slotActionLabels[i].text = "Save";
                        _slotActions[i].interactable = _saves != null;
                        break;
                }
                _slotDeletes[i].interactable = fileThere;
            }
        }

        private static string Describe(int slot, SaveData data)
        {
            var sb = new StringBuilder(160);
            sb.Append("<b>Slot ").Append(slot).Append("</b>");
            if (slot == SaveSession.CurrentSlot) sb.Append("  <color=#2E6E5E>· this game</color>");
            sb.Append('\n');

            for (int i = 0; i < data.locations.Count; i++)
            {
                LocationSave l = data.locations[i];
                if (i > 0) sb.Append("  ·  ");
                sb.Append(string.IsNullOrEmpty(l.name) ? l.id : l.name).Append(' ');
                if (l.filled == null || l.filled.Length == 0) sb.Append('—');
                else if (l.PlacedCount == l.filled.Length) sb.Append('✓');
                else sb.Append(l.PlacedCount).Append('/').Append(l.filled.Length);
            }

            int minutes = Mathf.FloorToInt(data.playTimeSeconds / 60f);
            sb.Append('\n').Append(data.memories.Count).Append(data.memories.Count == 1 ? " memory" : " memories");
            sb.Append("  ·  ").Append(minutes < 1 ? "under a minute" : minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60} min");
            sb.Append("  ·  ").Append(string.IsNullOrEmpty(data.currentLocationId) ? "on the street" : "in the " + NameFor(data, data.currentLocationId));
            DateTime saved = data.SavedAt;
            if (saved != DateTime.MinValue) sb.Append("\n<color=#8A7A6A>Saved ").Append(saved.ToLocalTime().ToString("d MMM yyyy, HH:mm")).Append("</color>");
            return sb.ToString();
        }

        private static string NameFor(SaveData data, string id)
        {
            LocationSave l = data.FindLocation(id);
            return l != null && !string.IsNullOrEmpty(l.name) ? l.name : id;
        }

        // ───────────── Confirm dialog ─────────────

        private void Confirm(string message, Action onYes)
        {
            _onConfirm = onYes;
            _confirmText.text = message;
            _confirmPanel.SetActive(true);
            Select(_confirmYes);
        }

        private void HideConfirm()
        {
            _confirmPanel.SetActive(false);
            _onConfirm = null;
        }

        private void AcceptConfirm()
        {
            Action action = _onConfirm;
            HideConfirm();
            action?.Invoke();
        }

        // ───────────── UI construction ─────────────

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        private void BuildUi()
        {
            _root = new GameObject(_mode == MenuMode.Main ? "Main Menu UI" : "Pause Menu UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform root = (RectTransform)_root.transform;

            // Backdrop: solid and warm for the start screen, a soft dim over the game when paused.
            Image backdrop = Stretch(NewImage("Backdrop", root), Vector2.zero, Vector2.one);
            backdrop.color = _mode == MenuMode.Main ? new Color(0.33f, 0.22f, 0.27f) : new Color(0.1f, 0.07f, 0.06f, 0.62f);

            // Left column: title and buttons.
            Text title = NewText("Title", root, _mode == MenuMode.Main ? "Tidy Up a City" : "Paused", 76, Cream, TextAnchor.UpperLeft);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(140f, -110f), new Vector2(760f, 100f));
            title.fontStyle = FontStyle.Bold;
            if (_mode == MenuMode.Main)
            {
                Text subtitle = NewText("Subtitle", root, "A cozy sorting game", 30, new Color(1f, 0.82f, 0.6f), TextAnchor.UpperLeft);
                Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(144f, -200f), new Vector2(700f, 50f));
            }

            float y = -300f;
            Button AddMain(string label, Action onClick)
            {
                Button b = NewButton(label, root, new Vector2(420f, 64f), 30, onClick);
                Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(140f, y), new Vector2(420f, 64f));
                y -= 82f;
                return b;
            }

            if (_mode == MenuMode.Main)
            {
                _continueButton = AddMain("Continue", Continue);
                Button newGame = AddMain("New Game", () => ShowSlots(SlotAction.NewGame));
                AddMain("Load Game", () => ShowSlots(SlotAction.Load));
                AddMain("Controls", ShowControls);
                AddMain("Quit", Quit);
                _firstButton = Storage.MostRecentSlot() > 0 ? _continueButton : newGame;
            }
            else
            {
                _firstButton = AddMain("Resume", Close);
                if (CanSaveAndLoad)
                {
                    AddMain("Save Game", () => ShowSlots(SlotAction.Save));
                    AddMain("Load Game", () => ShowSlots(SlotAction.Load));
                }
                AddMain("Controls", ShowControls);
                if (_saves != null && Application.CanStreamedLevelBeLoaded(_saves.MainMenuScene)) AddMain("Main Menu", GoToMainMenu);
                AddMain("Quit", Quit);
            }

            // Right panel: home text, slots, controls.
            Image panel = NewImage("Panel", root);
            panel.color = new Color(0.99f, 0.95f, 0.87f, 0.97f);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-120f, -20f);
            panelRect.sizeDelta = new Vector2(1060f, 700f);

            _panelTitle = NewText("Panel Title", panelRect, string.Empty, 40, Ink, TextAnchor.UpperLeft);
            _panelTitle.fontStyle = FontStyle.Bold;
            Place(_panelTitle.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -36f), new Vector2(960f, 60f));
            _status = NewText("Status", panelRect, string.Empty, 24, new Color(0.4f, 0.28f, 0.18f), TextAnchor.UpperLeft);
            Place(_status.rectTransform, new Vector2(0f, 0f), new Vector2(50f, 120f), new Vector2(960f, 90f));
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;

            BuildSlots(panelRect);
            BuildControls(panelRect);
            BuildConfirm(root);
        }

        private void BuildSlots(RectTransform panel)
        {
            _slotsPanel = new GameObject("Slots", typeof(RectTransform));
            var rect = (RectTransform)_slotsPanel.transform;
            rect.SetParent(panel, false);
            Stretch(rect, Vector2.zero, Vector2.one);

            for (int i = 0; i < SaveStorage.SlotCount; i++)
            {
                int slot = i + 1;
                Image card = NewImage($"Slot {slot}", rect);
                card.color = new Color(0.94f, 0.87f, 0.75f);
                Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -120f - i * 160f), new Vector2(960f, 140f));

                // Text stops well before the buttons (which start ~330 px from the right edge).
                _slotTexts[i] = NewText("Info", card.rectTransform, string.Empty, 20, Ink, TextAnchor.MiddleLeft);
                Place(_slotTexts[i].rectTransform, new Vector2(0f, 1f), new Vector2(24f, -8f), new Vector2(580f, 124f));
                _slotTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;

                _slotActions[i] = NewButton("Load", card.rectTransform, new Vector2(150f, 56f), 26, () => HandleSlotAction(slot));
                _slotActionLabels[i] = _slotActions[i].GetComponentInChildren<Text>();
                Place((RectTransform)_slotActions[i].transform, new Vector2(1f, 0.5f), new Vector2(-180f, 0f), new Vector2(150f, 56f));
                _slotDeletes[i] = NewButton("Delete", card.rectTransform, new Vector2(130f, 56f), 24, () => DeleteSlot(slot));
                Place((RectTransform)_slotDeletes[i].transform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(130f, 56f));
            }
            _slotsPanel.SetActive(false);
        }

        private void BuildControls(RectTransform panel)
        {
            _controlsPanel = new GameObject("Controls", typeof(RectTransform));
            var rect = (RectTransform)_controlsPanel.transform;
            rect.SetParent(panel, false);
            Stretch(rect, Vector2.zero, Vector2.one);

            const string actions = "<b> </b>\nMove\nLook around\nSprint\nPick up · place · use\nChoose carried item\nDrop\nTidy Sense\nMenu";
            const string keyboard = "<b>Keyboard & mouse</b>\nW A S D / arrows\nMouse\nLeft Shift\nLeft mouse / E\nQ / Tab / wheel\nG / right mouse\nF\nEsc";
            const string gamepad = "<b>Gamepad</b>\nLeft stick\nRight stick\nLeft stick click\nA (south)\nLB / RB\nB (east)\nY (north)\nStart";
            Text a = NewText("Actions", rect, actions, 26, Ink, TextAnchor.UpperLeft);
            Text k = NewText("Keyboard", rect, keyboard, 26, Ink, TextAnchor.UpperLeft);
            Text g = NewText("Gamepad", rect, gamepad, 26, Ink, TextAnchor.UpperLeft);
            foreach (Text t in new[] { a, k, g }) t.lineSpacing = 1.25f;
            Place(a.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -120f), new Vector2(330f, 420f));
            Place(k.rectTransform, new Vector2(0f, 1f), new Vector2(390f, -120f), new Vector2(320f, 420f));
            Place(g.rectTransform, new Vector2(0f, 1f), new Vector2(720f, -120f), new Vector2(300f, 420f));
            _controlsPanel.SetActive(false);
        }

        private void BuildConfirm(RectTransform root)
        {
            Image blocker = Stretch(NewImage("Confirm", root), Vector2.zero, Vector2.one);
            blocker.color = new Color(0.08f, 0.05f, 0.04f, 0.6f);
            blocker.raycastTarget = true; // clicks outside the dialog don't reach the menu behind it
            _confirmPanel = blocker.gameObject;

            Image dialog = NewImage("Dialog", blocker.rectTransform);
            dialog.color = new Color(0.99f, 0.95f, 0.87f);
            dialog.rectTransform.sizeDelta = new Vector2(760f, 300f);
            _confirmText = NewText("Message", dialog.rectTransform, string.Empty, 28, Ink, TextAnchor.MiddleCenter);
            Place(_confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(680f, 150f));
            _confirmText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _confirmYes = NewButton("Yes", dialog.rectTransform, new Vector2(200f, 60f), 28, AcceptConfirm);
            Place((RectTransform)_confirmYes.transform, new Vector2(0.5f, 0f), new Vector2(-120f, 40f), new Vector2(200f, 60f));
            ((RectTransform)_confirmYes.transform).pivot = new Vector2(0.5f, 0f);
            Button no = NewButton("No", dialog.rectTransform, new Vector2(200f, 60f), 28, HideConfirm);
            Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(120f, 40f), new Vector2(200f, 60f));
            ((RectTransform)no.transform).pivot = new Vector2(0.5f, 0f);
            _confirmPanel.SetActive(false);
        }

        private Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Text NewText(string name, Transform parent, string text, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            return t;
        }

        private Button NewButton(string label, Transform parent, Vector2 size, int fontSize, Action onClick)
        {
            Image image = NewImage(label + " Button", parent);
            image.raycastTarget = true;
            image.color = Color.white;
            image.rectTransform.sizeDelta = size;
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonNormal;
            colors.highlightedColor = ButtonHighlight;
            colors.selectedColor = ButtonHighlight;
            colors.pressedColor = ButtonPressed;
            colors.disabledColor = ButtonDisabled;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            Text text = NewText("Label", image.rectTransform, label, fontSize, Ink, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y);
            if (anchor.x > 0f && anchor.x < 1f) rect.pivot = new Vector2(0.5f, rect.pivot.y);
            if (anchor.y > 0f && anchor.y < 1f) rect.pivot = new Vector2(rect.pivot.x, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image Stretch(Image image, Vector2 min, Vector2 max)
        {
            Stretch(image.rectTransform, min, max);
            return image;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal void EditorConfigure(MenuMode mode, string gameScene, SaveService saves, PlayerInputReader input)
        {
            _mode = mode;
            _gameScene = gameScene;
            _saves = saves;
            _input = input;
        }
    }
}
