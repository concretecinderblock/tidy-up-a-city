using System;
using System.Collections;
using System.Collections.Generic;
using TidyUp.Items;
using TidyUp.Mechanics.Cleaning;
using TidyUp.Mechanics.OrderedSort;
using TidyUp.Mechanics.ShelfSort;
using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>
    /// Look / identify / pick up / place. Uses a ray from the camera (reticle). The public Try* methods
    /// take explicit targets so an XR hand interactor can drive the same logic later.
    /// </summary>
    public sealed class Interactor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Camera _camera;
        [SerializeField] private PlayerCarry _carry;
        [SerializeField] private FeedbackService _feedback;
        [SerializeField] private float _reach = 3f;
        [SerializeField] private LayerMask _mask = ~(1 << 2); // everything except Ignore Raycast
        [SerializeField] private float _dropDistance = 0.8f;

        private TidyItem _focusItem;
        private ItemSlot _focusSlot;
        private Interactable _focusInteractable;
        private int _focusLabelVersion;
        private Cleanable _focusCleanable;
        private Vector3 _focusPoint;

        // Wipe minigame (family K): runs while the player keeps looking at what they're wiping.
        private readonly TimingBar _wipeBar = new TimingBar();
        private readonly System.Random _wipeRandom = new System.Random();
        private Cleanable _wipeTarget;
        private string _wipeName;
        private float _lookedAwayFor;
        private WipeBarView _wipeView;

        /// <summary>The dusty thing under the reticle, if any (family K).</summary>
        public Cleanable FocusCleanable => _focusCleanable;
        public bool IsWiping => _wipeTarget != null;
        /// <summary>The wipe minigame's state while wiping (for tests and UI).</summary>
        public TimingBar WipeBar => _wipeBar;

        public TidyItem FocusItem => _focusItem;
        public ItemSlot FocusSlot => _focusSlot;

        /// <summary>Raised when the looked-at thing changes. Arg: label to show, or null for nothing.</summary>
        public event Action<string> FocusChanged;

        /// <summary>Short player-facing messages ("Hands full", "Doesn't go here").</summary>
        public event Action<string> Message;

        private void Update()
        {
            UpdateFocus();
            if (_input == null) return;
            if (UpdateWipe()) return; // E belongs to the wipe bar while it runs

            if (_input.CycleDirection != 0)
            {
                CycleKind(_input.CycleDirection);
                RefreshFocusLabel();
            }
            if (_input.InteractPressed) Interact();
            if (_input.DropPressed) TryDropSelected();
        }

        private void UpdateFocus()
        {
            TidyItem item = null;
            ItemSlot slot = null;
            Interactable interactable = null;
            Cleanable cleanable = null;

            if (_camera != null && (_input == null || !_input.GameplayBlocked))
            {
                Transform cam = _camera.transform;
                if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, _reach, _mask, QueryTriggerInteraction.Collide))
                {
                    item = hit.collider.GetComponentInParent<TidyItem>();
                    if (item == null) interactable = hit.collider.GetComponentInParent<Interactable>();
                    if (item == null && interactable == null)
                    {
                        slot = hit.collider.GetComponentInParent<ItemSlot>();
                        // Slot triggers enclose their occupant, so an occupied slot means "looking at its item".
                        if (slot != null && !slot.IsEmpty)
                        {
                            item = slot.Occupant;
                            slot = null;
                        }
                    }
                    // Dusty things (family K) come before their empty slots: clean first, then stock.
                    if (item == null && interactable == null)
                    {
                        Cleanable dusty = hit.collider.GetComponentInParent<Cleanable>();
                        if (dusty != null && !dusty.IsClean)
                        {
                            cleanable = dusty;
                            slot = null;
                        }
                    }
                    _focusPoint = hit.point;
                }
            }

            // Reference comparison on purpose: an object destroyed by a scene unload must count as a change.
            if (ReferenceEquals(item, _focusItem) && ReferenceEquals(slot, _focusSlot) && ReferenceEquals(interactable, _focusInteractable)
                && ReferenceEquals(cleanable, _focusCleanable))
            {
                // Living items can change what they look like while you watch (disguise reveal, shy shimmer).
                ILivingBehaviour living = item != null ? item.Living : null;
                if (living == null) return;
                if (item.State == ItemState.Loose) living.OnGazed(Time.deltaTime);
                if (living.LabelVersion != _focusLabelVersion) RefreshFocusLabel();
                return;
            }
            _focusItem = item;
            _focusSlot = slot;
            _focusInteractable = interactable;
            _focusCleanable = cleanable;
            FocusChanged?.Invoke(BuildFocusLabel());
        }

        // ───────────── Cleaning (family K): quick minigames ─────────────

        /// <summary>E on something dusty: dust bunnies pop out on fixtures; tops, windows and smudges get the wipe bar.</summary>
        private void StartCleaning(Cleanable cleanable)
        {
            if (cleanable.Game == CleanGame.DustBunnies)
            {
                if (cleanable.BunniesOut) Message?.Invoke("Poof the dust bunnies!");
                else cleanable.ReleaseBunnies(_camera != null ? _camera.transform.position : transform.position);
                RefreshFocusLabel();
                return;
            }

            _wipeTarget = cleanable;
            _wipeName = cleanable.Label.ToLowerInvariant();
            _lookedAwayFor = 0f;
            _wipeBar.Start(cleanable.WipeHitsNeeded, _wipeRandom);
            if (_wipeView == null) _wipeView = WipeBarView.Create(transform);
            _wipeView.Show();
            _wipeView.Refresh(_wipeBar, _wipeName);
            RefreshFocusLabel();
        }

        /// <summary>Runs the wipe bar. Returns true while it owns the interact button.</summary>
        private bool UpdateWipe()
        {
            if (_wipeTarget == null) return false;
            // Stop when it's done, the menu opens, or the player looks away for a moment (progress is kept).
            bool lookingAt = ReferenceEquals(_focusCleanable, _wipeTarget);
            _lookedAwayFor = lookingAt ? 0f : _lookedAwayFor + Time.deltaTime;
            if (_wipeTarget.IsClean || _input.GameplayBlocked || _lookedAwayFor > 0.4f)
            {
                StopWipe();
                return false;
            }

            _wipeBar.Advance(Time.deltaTime);
            if (_input.InteractPressed)
            {
                if (_wipeBar.Press())
                {
                    _wipeView.Flash();
                    _feedback?.PlayPoof(_focusPoint);
                    _wipeTarget.WipeHit(_wipeBar.Hits, _focusPoint);
                    if (_wipeBar.Done)
                    {
                        StopWipe();
                        return true;
                    }
                }
                else
                {
                    _feedback?.PlayRejected(_focusPoint); // a gentle "not quite", no penalty
                }
            }
            _wipeView.Refresh(_wipeBar, _wipeName);
            return true;
        }

        private void StopWipe()
        {
            _wipeTarget = null;
            if (_wipeView != null) _wipeView.Hide();
            RefreshFocusLabel();
        }

        private void OnDisable()
        {
            if (_wipeTarget != null) StopWipe();
        }

        private string BuildFocusLabel()
        {
            _focusLabelVersion = _focusItem != null && _focusItem.Living != null ? _focusItem.Living.LabelVersion : 0;
            if (_focusInteractable != null) return _focusInteractable.CanInteract ? _focusInteractable.FocusLabel : null;
            if (_focusCleanable != null)
            {
                string name = _focusCleanable.Label.ToLowerInvariant();
                if (ReferenceEquals(_wipeTarget, _focusCleanable)) return null; // the wipe bar says it all
                if (_focusCleanable.BunniesOut) return $"Poof the dust bunnies!  {_focusCleanable.BunniesLeft} left";
                return _focusCleanable.Game == CleanGame.DustBunnies
                    ? $"Dusty {name}  ·  [E] shoo the dust bunnies"
                    : $"Dusty {name}  ·  [E] wipe";
            }
            if (_focusItem != null)
            {
                return _focusItem.State == ItemState.Placed ? _focusItem.DisplayName + "  ✓" : _focusItem.DisplayName;
            }
            if (_focusSlot != null && _focusSlot.IsEmpty)
            {
                string label = string.IsNullOrEmpty(_focusSlot.Label) ? "Empty slot" : _focusSlot.Label;
                TidyItem selected = _carry.Inventory.Selected;
                return selected != null ? $"{label}  ← place {selected.DisplayName}" : label;
            }
            return null;
        }

        private void Interact()
        {
            if (_focusInteractable != null)
            {
                if (_focusInteractable.CanInteract) _focusInteractable.Interact(this);
                RefreshFocusLabel();
                return;
            }

            if (_focusItem != null)
            {
                if (_focusItem.State == ItemState.Loose) TryPickUp(_focusItem);
                else if (_focusItem.State == ItemState.Placed) Message?.Invoke($"{_focusItem.DisplayName} is already where it belongs.");
                return;
            }

            if (_focusCleanable != null)
            {
                StartCleaning(_focusCleanable);
                return;
            }

            if (_focusSlot != null) TryPlace(_carry.Inventory.Selected, _focusSlot);
        }

        public bool TryPickUp(TidyItem item)
        {
            if (item == null || item.State != ItemState.Loose) return false;

            CarryInventory<TidyItem> inventory = _carry.Inventory;
            if (inventory.IsFull)
            {
                _feedback?.PlayRejected(item.transform.position);
                Message?.Invoke($"Hands full ({inventory.Count}/{inventory.Capacity}). Put something away first.");
                return false;
            }

            // Living items may slip away (skittish hop) or reveal themselves (disguise) instead of being caught.
            ILivingBehaviour living = item.Living;
            if (living != null && !living.TryCatch(out string slipMessage))
            {
                if (!string.IsNullOrEmpty(slipMessage)) Message?.Invoke(slipMessage);
                RefreshFocusLabel();
                return false;
            }

            Vector3 position = item.transform.position;
            inventory.TryAdd(item);
            item.PickUp();
            _feedback?.PlayPickup(position);
            RefreshFocusLabel();
            return true;
        }

        /// <summary>Moves the selection to the next carried item of a different kind.</summary>
        private void CycleKind(int direction)
        {
            CarryInventory<TidyItem> inventory = _carry.Inventory;
            TidyItem start = inventory.Selected;
            if (start == null) return;
            for (int i = 0; i < inventory.Count; i++)
            {
                inventory.Cycle(direction);
                TidyItem now = inventory.Selected;
                if (now == null || now.Definition != start.Definition) return;
            }
        }

        public bool TryPlace(TidyItem item, ItemSlot slot)
        {
            if (slot == null || !slot.IsEmpty) return false;
            if (item == null)
            {
                Message?.Invoke("Your hands are empty.");
                return false;
            }

            if (!slot.CanAccept(item))
            {
                // Soft rejection: gentle sound, item stays in hand.
                _feedback?.PlayRejected(slot.SnapPoint.position);
                Message?.Invoke(RejectionMessage(item, slot));
                return false;
            }

            _carry.Inventory.Remove(item);
            item.PlaceInto(slot);
            SlotGroup group = slot.GetComponentInParent<SlotGroup>();
            // Flat-screen placement snaps to the slot's exact rotation; XR will pass the hand rotation here.
            bool perfect = SlotRules.IsPerfectAlignment(slot.Definition, slot.SnapPoint.rotation, item.transform.rotation);

            // Then everything else in hand that belongs on this fixture goes to the nearest fitting empty slots.
            List<ItemSlot> run = group != null ? PlaceRun(group, slot.SnapPoint.position, item.Definition) : null;

            float fill = group != null ? group.FillRatio : 1f;
            if (run == null || run.Count == 0)
            {
                _feedback?.PlayPlaced(slot.SnapPoint.position, fill, perfect);
            }
            else
            {
                run.Insert(0, slot);
                StartCoroutine(Cascade(run, group));
                Message?.Invoke($"Placed {run.Count} items");
            }
            RefreshFocusLabel();
            return true;
        }

        /// <summary>
        /// Places carried items into empty slots of this fixture that accept them, nearest first; the item kind
        /// just placed goes first. Returns the slots filled (logic is instant; the cascade is only feedback).
        /// </summary>
        private List<ItemSlot> PlaceRun(SlotGroup group, Vector3 from, ItemDefinition firstKind)
        {
            CarryInventory<TidyItem> inventory = _carry.Inventory;
            var filled = new List<ItemSlot>();
            if (inventory.IsEmpty) return filled;

            // Group carried items by kind, the just-placed kind first.
            var kinds = new List<ItemDefinition>();
            var byKind = new Dictionary<ItemDefinition, List<TidyItem>>();
            foreach (TidyItem carried in inventory.Items)
            {
                if (carried == null || carried.Definition == null) continue;
                if (!byKind.TryGetValue(carried.Definition, out List<TidyItem> list))
                {
                    byKind[carried.Definition] = list = new List<TidyItem>();
                    if (carried.Definition == firstKind) kinds.Insert(0, carried.Definition);
                    else kinds.Add(carried.Definition);
                }
                list.Add(carried);
            }

            var candidates = new List<ItemSlot>();
            foreach (ItemDefinition kind in kinds)
            {
                List<TidyItem> items = byKind[kind];
                candidates.Clear();
                foreach (ItemSlot s in group.Slots)
                {
                    if (s != null && s.CanAccept(items[0])) candidates.Add(s);
                }
                if (candidates.Count == 0) continue;
                candidates.Sort((a, b) => (a.SnapPoint.position - from).sqrMagnitude.CompareTo((b.SnapPoint.position - from).sqrMagnitude));

                int n = Mathf.Min(items.Count, candidates.Count);
                for (int i = 0; i < n; i++)
                {
                    inventory.Remove(items[i]);
                    items[i].PlaceInto(candidates[i]);
                    filled.Add(candidates[i]);
                }
            }
            return filled;
        }

        /// <summary>A quick rising run of tones and sparkles over the slots just filled (capped, so it stays snappy).</summary>
        private IEnumerator Cascade(List<ItemSlot> slots, SlotGroup group)
        {
            if (_feedback == null) yield break;
            const int maxNotes = 14;
            int count = slots.Count;
            int notes = Mathf.Min(count, maxNotes);
            float startFill = group.Count > 0 ? (float)(group.FilledCount - count) / group.Count : 0f;
            for (int i = 0; i < notes; i++)
            {
                // Spread the notes over the whole run so the last one lands on the fixture's real fill.
                int index = notes == 1 ? 0 : Mathf.RoundToInt((float)i / (notes - 1) * (count - 1));
                float fill = startFill + (float)(index + 1) / Mathf.Max(1, group.Count);
                _feedback.PlayPlaced(slots[index].SnapPoint.position, fill, false);
                yield return new WaitForSecondsRealtime(0.045f);
            }
        }

        /// <summary>On ordered shelves, point the player toward the right spot instead of a plain "no".</summary>
        private static string RejectionMessage(TidyItem item, ItemSlot slot)
        {
            if (!slot.IsOpen && slot.Gate != null) return slot.Gate.ClosedMessage;
            SlotGroup group = slot.GetComponentInParent<SlotGroup>();
            if (group != null && group.IsOrdered && item.Definition != null)
            {
                int target = group.IndexOfExactItem(item.Definition.Id);
                if (target >= 0)
                {
                    OrderHint hint = OrderedSortRules.Direction(group.IndexOf(slot), target, group.Columns);
                    return OrderedSortRules.Describe(item.DisplayName, hint);
                }
            }
            return $"{item.DisplayName} doesn't go here.";
        }

        public bool TryDropSelected()
        {
            TidyItem item = _carry.Inventory.Selected;
            if (item == null || _camera == null) return false;

            Transform cam = _camera.transform;
            Vector3 position = cam.position + cam.forward * _dropDistance;
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, _dropDistance, _mask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point - cam.forward * 0.15f;
            }

            _carry.Inventory.Remove(item);
            item.SetLoose(position, Quaternion.Euler(0f, cam.eulerAngles.y, 0f));
            RefreshFocusLabel();
            return true;
        }

        private void RefreshFocusLabel() => FocusChanged?.Invoke(BuildFocusLabel());

        internal void EditorConfigure(PlayerInputReader input, Camera camera, PlayerCarry carry, FeedbackService feedback)
        {
            _input = input;
            _camera = camera;
            _carry = carry;
            _feedback = feedback;
        }
    }
}
