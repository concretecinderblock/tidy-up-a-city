using System;
using UnityEngine;

namespace TidyUp.Items
{
    /// <summary>
    /// Static data for one item type. Generated from CSV (Data/Items/*.csv) by the ItemImporter.
    /// The CSV is the source of truth: don't hand-edit imported fields.
    /// </summary>
    [CreateAssetMenu(menuName = "Tidy Up/Item Definition", fileName = "Item_New")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private string _homeLocationId;
        [SerializeField] private string[] _slotTypes = Array.Empty<string>();
        [SerializeField] private MechanicFamily _mechanicFamily = MechanicFamily.ShelfSort;
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;
        [SerializeField] private bool _isStray;
        [Tooltip("Ordered sorts (family B): alphabetical key (author surname), number (volume) or size.")]
        [SerializeField] private string _sortKey;
        [Tooltip("Living items (family I): skittish, floating, shy or disguised.")]
        [SerializeField] private LivingBehaviour _living;
        [Tooltip("Disguised items: the name shown until revealed.")]
        [SerializeField] private string _disguiseName;

        public string Id => _id;
        public string SortKey => _sortKey;
        public LivingBehaviour Living => _living;
        public string DisguiseName => _disguiseName;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
        public GameObject Prefab => _prefab;
        public string HomeLocationId => _homeLocationId;
        public string[] SlotTypes => _slotTypes;
        public MechanicFamily MechanicFamily => _mechanicFamily;
        public ItemRarity Rarity => _rarity;
        public bool IsStray => _isStray;

        internal void Configure(string id, string displayName, string homeLocationId, string[] slotTypes,
            MechanicFamily family, ItemRarity rarity, bool isStray)
        {
            _id = id;
            _displayName = displayName;
            _homeLocationId = homeLocationId;
            _slotTypes = slotTypes ?? Array.Empty<string>();
            _mechanicFamily = family;
            _rarity = rarity;
            _isStray = isStray;
        }

        internal void SetPrefab(GameObject prefab) => _prefab = prefab;
        internal void SetSortKey(string sortKey) => _sortKey = sortKey;

        internal void SetLiving(LivingBehaviour living, string disguiseName)
        {
            _living = living;
            _disguiseName = disguiseName;
        }
    }
}
