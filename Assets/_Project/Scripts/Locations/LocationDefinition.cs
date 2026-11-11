using System;
using TidyUp.Items;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>Static data for one location (CLAUDE.md section 4).</summary>
    [CreateAssetMenu(menuName = "Tidy Up/Location Definition", fileName = "Location_New")]
    public sealed class LocationDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _district;
        [Tooltip("Scene name for additive loading. Becomes an Addressables reference once Addressables is installed.")]
        [SerializeField] private string _sceneName;
        [SerializeField] private MechanicFamily[] _mechanicFamilies = Array.Empty<MechanicFamily>();
        [SerializeField] private ItemDefinition[] _items = Array.Empty<ItemDefinition>();

        [Tooltip("The story memory that surfaces when this location is fully restored.")]
        [SerializeField] private Story.MemoryDefinition _memory;
        [Tooltip("Written by the scene builder.")]
        [SerializeField] private int _totalSlots;

        [Header("Muddle")]
        [SerializeField] private int _muddleSeed = 1;
        [Range(0f, 1f)]
        [SerializeField] private float _muddleFraction = 1f;

        public string Id => _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
        public string District => _district;
        public string SceneName => _sceneName;
        public MechanicFamily[] MechanicFamilies => _mechanicFamilies;
        public ItemDefinition[] Items => _items;
        public Story.MemoryDefinition Memory => _memory;
        /// <summary>Number of slots in the location (written by the scene builder), for progress on the street.</summary>
        public int TotalSlots => _totalSlots;
        public int MuddleSeed => _muddleSeed;
        public float MuddleFraction => _muddleFraction;

        internal void Configure(string id, string displayName, string district, string sceneName,
            MechanicFamily[] families, ItemDefinition[] items, int muddleSeed, float muddleFraction)
        {
            _id = id;
            _displayName = displayName;
            _district = district;
            _sceneName = sceneName;
            _mechanicFamilies = families ?? Array.Empty<MechanicFamily>();
            _items = items ?? Array.Empty<ItemDefinition>();
            _muddleSeed = muddleSeed;
            _muddleFraction = muddleFraction;
        }

        internal void SetMemory(Story.MemoryDefinition memory) => _memory = memory;
        internal void SetTotalSlots(int totalSlots) => _totalSlots = totalSlots;
    }
}
