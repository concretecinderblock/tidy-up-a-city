using System.Text;
using TidyUp.Core;
using TidyUp.Hints;
using TidyUp.Items;
using TidyUp.Locations;
using TidyUp.Mechanics.Cleaning;
using TidyUp.Story;
using UnityEngine;
using UnityEngine.UI;

namespace TidyUp.UI
{
    /// <summary>
    /// Grey-box HUD: reticle label, inventory strip, checklist, transient messages.
    /// Lives with the player and follows the active location (GameServices.ActiveLocationChanged): inside a
    /// location the checklist shows its categories; on the street it shows every location's progress.
    /// Text is rebuilt only when something changes (event-driven), never per frame.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private Interactor _interactor;
        [SerializeField] private PlayerCarry _carry;
        [SerializeField] private TidySense _tidySense;
        [SerializeField] private MemoryService _memories;

        [Header("Widgets")]
        [SerializeField] private Text _tidySenseLabel;
        [SerializeField] private Text _focusLabel;
        [SerializeField] private Text _inventoryLabel;
        [SerializeField] private Text _checklistLabel;
        [SerializeField] private Text _messageLabel;
        [SerializeField] private float _messageDuration = 3f;

        private readonly StringBuilder _builder = new StringBuilder(256);
        private CanvasGroup _group;
        private bool _hiddenForMenu;
        private readonly System.Collections.Generic.List<string> _kinds = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.Dictionary<string, int> _kindCounts = new System.Collections.Generic.Dictionary<string, int>();
        private float _messageHideTime = -1f;
        private LocationProgress _progress;
        private HintService _hints;
        private CleaningProgress _cleaning;
        private LocationRuntime _runtime;
        private MegaLocation _mega;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (_interactor != null)
            {
                _interactor.FocusChanged += ShowFocus;
                _interactor.Message += ShowMessage;
            }
            if (_carry != null) _carry.Changed += RefreshInventory;
            if (_tidySense != null)
            {
                _tidySense.StatusChanged += RefreshTidySense;
                _tidySense.Message += ShowMessage;
            }
            if (_memories != null)
            {
                _memories.Message += ShowMessage;
                _memories.MemoryStarted += HideForMemory;
                _memories.MemoryFinished += ShowAfterMemory;
            }
            GameServices.ActiveLocationChanged += Bind;
            GameServices.Announcement += ShowMessage;
            MegaLocation.CurrentChanged += BindMega;
            BindMega(MegaLocation.Current);
            Bind(GameServices.ActiveLocation);
        }

        private void OnDisable()
        {
            if (_interactor != null)
            {
                _interactor.FocusChanged -= ShowFocus;
                _interactor.Message -= ShowMessage;
            }
            if (_carry != null) _carry.Changed -= RefreshInventory;
            if (_tidySense != null)
            {
                _tidySense.StatusChanged -= RefreshTidySense;
                _tidySense.Message -= ShowMessage;
            }
            if (_memories != null)
            {
                _memories.Message -= ShowMessage;
                _memories.MemoryStarted -= HideForMemory;
                _memories.MemoryFinished -= ShowAfterMemory;
            }
            GameServices.ActiveLocationChanged -= Bind;
            GameServices.Announcement -= ShowMessage;
            MegaLocation.CurrentChanged -= BindMega;
            BindMega(null);
            Bind(null);
        }

        private void Start()
        {
            ShowFocus(null);
            RefreshInventory();
            if (_tidySense != null) RefreshTidySense(_tidySense);
            if (_messageLabel != null && _messageHideTime < 0f) _messageLabel.text = string.Empty;
            RefreshChecklist();
        }

        private void Update()
        {
            // Step aside while the pause menu or a memory owns the screen.
            bool hide = (GameServices.Input != null && GameServices.Input.MenuOpen) || (_memories != null && _memories.IsPlaying);
            if (hide != _hiddenForMenu && _group != null)
            {
                _hiddenForMenu = hide;
                _group.alpha = hide ? 0f : 1f;
            }

            if (_messageHideTime > 0f && Time.unscaledTime >= _messageHideTime)
            {
                _messageHideTime = -1f;
                if (_messageLabel != null) _messageLabel.text = string.Empty;
            }
        }

        /// <summary>Follow a location (or the street when null).</summary>
        private void Bind(LocationRuntime location)
        {
            if (_progress != null)
            {
                _progress.ProgressChanged -= HandleProgressChanged;
                _progress.Completed -= HandleCompleted;
            }
            if (_hints != null) _hints.UnlocksChanged -= HandleUnlocksChanged;
            if (_cleaning != null) _cleaning.ProgressChanged -= HandleCleaningChanged;

            _runtime = location;
            _progress = location != null ? location.Progress : null;
            _hints = location != null ? location.Hints : null;
            _cleaning = location != null ? location.Cleaning : null;
            if (_cleaning != null) _cleaning.ProgressChanged += HandleCleaningChanged;

            if (_progress != null)
            {
                _progress.ProgressChanged += HandleProgressChanged;
                _progress.Completed += HandleCompleted;
            }
            if (_hints != null) _hints.UnlocksChanged += HandleUnlocksChanged;
            RefreshChecklist();
        }

        private void BindMega(MegaLocation mega)
        {
            if (_mega != null) _mega.Completed -= HandleMegaCompleted;
            _mega = mega;
            if (_mega != null) _mega.Completed += HandleMegaCompleted;
            RefreshChecklist();
        }

        private void HandleMegaCompleted(MegaLocation mega)
        {
            string name = mega.Location != null ? mega.Location.DisplayName : "Location";
            ShowMessage("The whole " + name + " is tidy again!");
        }

        // The memory overlay owns the screen while it plays.
        private void HideForMemory(MemoryDefinition memory)
        {
            if (_group != null) _group.alpha = 0f;
        }

        private void ShowAfterMemory(MemoryDefinition memory)
        {
            if (_group != null) _group.alpha = 1f;
        }

        private void ShowFocus(string label)
        {
            if (_focusLabel != null) _focusLabel.text = label ?? string.Empty;
        }

        private void ShowMessage(string message)
        {
            if (_messageLabel == null) return;
            _messageLabel.text = message;
            _messageHideTime = Time.unscaledTime + _messageDuration;
        }

        private void RefreshInventory()
        {
            if (_inventoryLabel == null || _carry == null) return;
            CarryInventory<TidyItem> inventory = _carry.Inventory;

            // "Basket 14/20   Croissant ×8  ·  [Baguette ×6]": grouped by kind, selected kind highlighted.
            _builder.Clear();
            _builder.Append(_carry.TierName).Append(' ').Append(inventory.Count);
            if (!_carry.Unlimited) _builder.Append('/').Append(inventory.Capacity);
            TidyItem selected = inventory.Selected;
            _kinds.Clear();
            _kindCounts.Clear();
            for (int i = 0; i < inventory.Count; i++)
            {
                TidyItem item = inventory.Items[i];
                string name = item.Definition != null ? item.Definition.DisplayName : item.DisplayName;
                if (_kindCounts.TryGetValue(name, out int n)) _kindCounts[name] = n + 1;
                else
                {
                    _kindCounts[name] = 1;
                    _kinds.Add(name);
                }
            }
            string selectedName = selected == null ? null : selected.Definition != null ? selected.Definition.DisplayName : selected.DisplayName;
            if (_kinds.Count > 0) _builder.Append("   ");
            for (int k = 0; k < _kinds.Count; k++)
            {
                bool isSelected = _kinds[k] == selectedName;
                if (isSelected) _builder.Append("<b><color=#FFD86B>[");
                _builder.Append(_kinds[k]);
                if (_kindCounts[_kinds[k]] > 1) _builder.Append(" ×").Append(_kindCounts[_kinds[k]]);
                if (isSelected) _builder.Append("]</color></b>");
                if (k < _kinds.Count - 1) _builder.Append("  ·  ");
            }
            _inventoryLabel.text = _builder.ToString();
        }

        private const int MaxCategoryLines = 8;
        private const int ShownCategories = 5;
        private readonly System.Collections.Generic.List<Checklist.Category> _categories = new System.Collections.Generic.List<Checklist.Category>();

        private void AppendCategory(Checklist.Category category)
        {
            _builder.Append('\n').Append(category.Label).Append("  ");
            if (category.IsComplete) _builder.Append("<color=#9BE39B>✓</color>");
            else _builder.Append(Mathf.RoundToInt(category.Ratio * 100f)).Append('%');
        }

        private void HandleProgressChanged(LocationProgress progress) => RefreshChecklist();
        private void HandleCleaningChanged(CleaningProgress cleaning) => RefreshChecklist();

        private void RefreshChecklist()
        {
            if (_checklistLabel == null) return;
            _builder.Clear();

            if (_progress == null)
            {
                BuildStreetOverview();
                _checklistLabel.text = _builder.ToString();
                return;
            }

            string locationName = _runtime != null ? _runtime.DisplayName : _progress.Location != null ? _progress.Location.DisplayName : "Location";
            if (_mega != null && _runtime != null && _runtime.IsZone)
            {
                // A mega location: the grand total over every room first, then the room you're in.
                string megaName = _mega.Location != null ? _mega.Location.DisplayName : "Location";
                _builder.Append("<b><size=22>").Append(megaName).Append("  ").Append(_mega.Placed.ToString("N0")).Append('/').Append(_mega.Total.ToString("N0")).Append("</size></b>\n");
            }
            _builder.Append("<b>").Append(locationName).Append("  ").Append(_progress.Placed).Append('/').Append(_progress.Total).Append("</b>");
            if (_cleaning != null && _cleaning.Total > 0)
            {
                _builder.Append("\nCleaned  ");
                if (_cleaning.IsComplete) _builder.Append("<color=#9BE39B>✓</color>");
                else _builder.Append(_cleaning.Cleaned).Append('/').Append(_cleaning.Total);
            }

            // Category breakdown is a hint tool that unlocks over time; the total is always shown.
            Checklist checklist = _progress.Checklist;
            bool showCategories = checklist != null && (_hints == null || _hints.ChecklistUnlocked);
            if (showCategories && checklist.CategoryCount <= MaxCategoryLines)
            {
                for (int i = 0; i < checklist.CategoryCount; i++) AppendCategory(checklist.GetCategory(i));
            }
            else if (showCategories)
            {
                // Big locations have dozens of kinds: a count, then the unfinished kinds closest to done.
                _categories.Clear();
                int done = 0;
                for (int i = 0; i < checklist.CategoryCount; i++)
                {
                    Checklist.Category category = checklist.GetCategory(i);
                    if (category.IsComplete) done++;
                    else _categories.Add(category);
                }
                _categories.Sort((a, b) => b.Ratio.CompareTo(a.Ratio));
                _builder.Append("\nKinds done  ").Append(done).Append('/').Append(checklist.CategoryCount);
                for (int i = 0; i < _categories.Count && i < ShownCategories; i++) AppendCategory(_categories[i]);
                if (_categories.Count > ShownCategories) _builder.Append("\n<color=#C8C8C8>+ ").Append(_categories.Count - ShownCategories).Append(" more</color>");
            }
            _checklistLabel.text = _builder.ToString();
        }

        /// <summary>On the street: every location on it, with progress from the session store.</summary>
        private void BuildStreetOverview()
        {
            LocationLoader loader = GameServices.Loader;
            if (loader == null || loader.Locations.Length == 0) return;
            _builder.Append("<b>Old Town Street</b>");
            foreach (LocationDefinition location in loader.Locations)
            {
                if (location == null) continue;
                _builder.Append('\n').Append(location.DisplayName).Append("  ");
                int total = location.TotalSlots;
                int placed = LocationStateStore.PlacedCount(location.Id);
                if (!LocationStateStore.IsVisited(location.Id)) _builder.Append("<color=#C8C8C8>not visited</color>");
                else if (total > 0 && placed >= total) _builder.Append("<color=#9BE39B>✓</color>");
                else _builder.Append(placed).Append('/').Append(total);
            }
        }

        private void RefreshTidySense(TidySense sense)
        {
            if (_tidySenseLabel == null) return;
            if (!sense.IsUnlocked) _tidySenseLabel.text = string.Empty;
            else if (sense.IsReady) _tidySenseLabel.text = "Tidy Sense ready  [F]";
            else _tidySenseLabel.text = "Tidy Sense  " + Mathf.CeilToInt(sense.CooldownRemaining) + "s";
        }

        private void HandleUnlocksChanged(HintService hints)
        {
            RefreshChecklist();
            if (_tidySense != null) RefreshTidySense(_tidySense);
        }

        private void HandleCompleted(LocationProgress progress)
        {
            string locationName = _runtime != null && _runtime.Progress == progress ? _runtime.DisplayName : progress.Location != null ? progress.Location.DisplayName : "Location";
            ShowMessage(locationName + " is tidy again!");
        }

        internal void EditorConfigure(Interactor interactor, PlayerCarry carry, TidySense tidySense, MemoryService memories,
            Text focusLabel, Text inventoryLabel, Text checklistLabel, Text messageLabel, Text tidySenseLabel)
        {
            _interactor = interactor;
            _carry = carry;
            _tidySense = tidySense;
            _memories = memories;
            _focusLabel = focusLabel;
            _inventoryLabel = inventoryLabel;
            _checklistLabel = checklistLabel;
            _messageLabel = messageLabel;
            _tidySenseLabel = tidySenseLabel;
        }
    }
}
