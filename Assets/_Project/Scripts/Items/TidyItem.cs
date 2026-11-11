using UnityEngine;

namespace TidyUp.Items
{
    public enum ItemState
    {
        Loose = 0,
        Carried = 1,
        Placed = 2
    }

    /// <summary>
    /// Runtime item in a location scene. Carried items stay in their own location scene (deactivated);
    /// they are never moved between scenes (hard-binding, CLAUDE.md section 4).
    /// An optional ILivingBehaviour on the same object gives it personality (family I).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TidyItem : MonoBehaviour
    {
        [SerializeField] private ItemDefinition _definition;

        private Rigidbody _body;
        private ItemState _state = ItemState.Loose;
        private ItemSlot _slot;
        private ILivingBehaviour _living;
        private bool _livingLookedUp;

        public ItemDefinition Definition => _definition;
        public ItemState State => _state;
        public ItemSlot Slot => _slot;


        /// <summary>The living behaviour, or null for ordinary items. Looked up once (Awake order safe).</summary>
        public ILivingBehaviour Living
        {
            get
            {
                if (!_livingLookedUp)
                {
                    _livingLookedUp = true;
                    _living = GetComponent<ILivingBehaviour>();
                }
                return _living;
            }
        }

        public string DisplayName
        {
            get
            {
                string disguised = Living?.DisplayNameOverride;
                if (disguised != null) return disguised;
                return _definition != null ? _definition.DisplayName : name;
            }
        }

        private void Awake()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
        }

        internal void SetDefinition(ItemDefinition definition) => _definition = definition;

        /// <summary>Takes the item into the player's hands.</summary>
        public void PickUp()
        {
            DetachFromSlot();
            _state = ItemState.Carried;
            Living?.OnStateChanged(_state);
            gameObject.SetActive(false);
        }

        /// <summary>Snaps the item into a slot. The caller validates first (ItemSlot.CanAccept).</summary>
        public void PlaceInto(ItemSlot slot)
        {
            DetachFromSlot();
            gameObject.SetActive(true);
            Transform snap = slot.SnapPoint;
            SetPhysicsActive(false);
            Teleport(snap.position, snap.rotation);
            _slot = slot;
            _state = ItemState.Placed;
            slot.SetOccupant(this);
            Living?.OnStateChanged(_state);
        }

        /// <summary>Puts the item in the world as a loose physics object (it falls and settles).</summary>
        public void SetLoose(Vector3 position, Quaternion rotation)
        {
            DetachFromSlot();
            gameObject.SetActive(true);
            Teleport(position, rotation);
            SetPhysicsActive(true);
            _state = ItemState.Loose;
            Living?.OnStateChanged(_state);
        }

        /// <summary>
        /// Puts the item down loose but at rest: no physics simulation until the player touches it. This is how
        /// the Muddle lays out thousands of items without paying for thousands of rigidbodies.
        /// </summary>
        public void SetLooseResting(Vector3 position, Quaternion rotation)
        {
            DetachFromSlot();
            gameObject.SetActive(true);
            SetPhysicsActive(false);
            Teleport(position, rotation);
            _state = ItemState.Loose;
            Living?.OnStateChanged(_state);
        }

        /// <summary>Called by ItemSlot when the tidy-state occupant registers at load.</summary>
        internal void BindToSlotAtLoad(ItemSlot slot)
        {
            _slot = slot;
            _state = ItemState.Placed;
            SetPhysicsActive(false);
        }

        /// <summary>For living behaviours: switch between physics and scripted (kinematic) motion.</summary>
        public void SetSimulated(bool simulated) => SetPhysicsActive(simulated);

        /// <summary>For living behaviours: move transform and Rigidbody together.</summary>
        public void MoveTo(Vector3 position, Quaternion rotation) => Teleport(position, rotation);

        private void DetachFromSlot()
        {
            if (_slot == null) return;
            ItemSlot slot = _slot;
            _slot = null;
            slot.ClearOccupant(this);
        }

        /// <summary>
        /// Moves transform and Rigidbody together. With interpolation on, setting only the transform
        /// gets overwritten by the body's stored pose on the next physics step.
        /// </summary>
        private void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            transform.SetPositionAndRotation(position, rotation);
            _body.position = position;
            _body.rotation = rotation;
        }

        private void SetPhysicsActive(bool active)
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (active)
            {
                _body.isKinematic = false;
                _body.interpolation = RigidbodyInterpolation.Interpolate;
                _body.WakeUp();
                return;
            }
            // Resting and placed items never move by physics: no interpolation work for thousands of them.
            _body.interpolation = RigidbodyInterpolation.None;

            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            _body.isKinematic = true;
        }
    }
}
