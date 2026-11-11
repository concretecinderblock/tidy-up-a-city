using System;
using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Story
{
    /// <summary>
    /// Plays memories and keeps the journal of collected memories/clues. Blocks gameplay input while a
    /// memory plays (time keeps running, nothing in the world is paused).
    /// </summary>
    public sealed class MemoryService : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private MemoryView _view;
        [SerializeField] private FeedbackService _feedback;

        private readonly MemoryJournal _journal = new MemoryJournal();
        private MemoryDefinition _playing;

        public MemoryJournal Journal => _journal;
        public bool IsPlaying => _playing != null;

        /// <summary>Short HUD messages ("Something glimmers on the counter…").</summary>
        public event Action<string> Message;
        public event Action<MemoryDefinition> MemoryStarted;
        public event Action<MemoryDefinition> MemoryFinished;

        public bool Play(MemoryDefinition memory)
        {
            if (memory == null || IsPlaying || _view == null) return false;
            _playing = memory;
            if (_input != null) _input.GameplayBlocked = true;
            _feedback?.PlayRestore(false);
            MemoryStarted?.Invoke(memory);
            _view.Play(memory, HandleViewFinished);
            return true;
        }

        public void Announce(string message)
        {
            if (!string.IsNullOrEmpty(message)) Message?.Invoke(message);
        }

        private void HandleViewFinished()
        {
            MemoryDefinition memory = _playing;
            _playing = null;
            if (_input != null) _input.GameplayBlocked = false;

            bool isNew = _journal.Collect(memory.Id);
            _feedback?.PlayHint();
            MemoryFinished?.Invoke(memory);
            if (isNew) Message?.Invoke($"Memory kept: {memory.Title}  ·  {_journal.Count} found");
        }

        internal void EditorConfigure(PlayerInputReader input, MemoryView view, FeedbackService feedback)
        {
            _input = input;
            _view = view;
            _feedback = feedback;
        }
    }
}
