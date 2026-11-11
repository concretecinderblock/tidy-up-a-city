using System;
using UnityEngine;

namespace TidyUp.Story
{
    [Serializable]
    public struct MemoryLine
    {
        [Tooltip("Who says it. Empty = narration.")]
        public string Speaker;
        [TextArea(2, 4)] public string Text;
        [Tooltip("Seconds before the line advances on its own.")]
        public float Duration;

        public MemoryLine(string speaker, string text, float duration)
        {
            Speaker = speaker;
            Text = text;
            Duration = duration;
        }
    }

    /// <summary>
    /// A location's story memory (GDD section 6): a short flashback of the place on the day before the
    /// Muddle, ending in a clue fragment toward what caused it.
    /// </summary>
    [CreateAssetMenu(menuName = "Tidy Up/Memory Definition", fileName = "Memory_New")]
    public sealed class MemoryDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _title = "The Day Before";
        [SerializeField] private string _locationName;
        [SerializeField] private MemoryLine[] _lines = Array.Empty<MemoryLine>();
        [Tooltip("The mystery fragment the player keeps.")]
        [TextArea(2, 3)]
        [SerializeField] private string _clue;

        public string Id => _id;
        public string Title => _title;
        public string LocationName => _locationName;
        public MemoryLine[] Lines => _lines;
        public string Clue => _clue;

        internal void Configure(string id, string title, string locationName, MemoryLine[] lines, string clue)
        {
            _id = id;
            _title = title;
            _locationName = locationName;
            _lines = lines ?? Array.Empty<MemoryLine>();
            _clue = clue;
        }
    }
}
