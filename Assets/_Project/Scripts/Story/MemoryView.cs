using System;
using TidyUp.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TidyUp.Story
{
    /// <summary>
    /// Grey-box memory presentation: a warm sepia overlay over the room, the title, then one line at a time,
    /// ending on the clue. Interact advances, Drop skips. Runs on unscaled time; Update only while playing.
    /// </summary>
    public sealed class MemoryView : MonoBehaviour
    {
        private enum Phase { Hidden, FadeIn, Title, Line, Clue, FadeOut }

        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _speakerLabel;
        [SerializeField] private Text _lineLabel;
        [SerializeField] private Text _promptLabel;
        [SerializeField] private float _fadeDuration = 0.8f;
        [SerializeField] private float _titleDuration = 2.5f;
        [SerializeField] private float _clueDuration = 5f;
        [SerializeField] private float _textFadeDuration = 0.5f;

        private MemoryDefinition _memory;
        private Action _onFinished;
        private Phase _phase = Phase.Hidden;
        private int _lineIndex;
        private float _phaseTime;
        private int _startFrame;

        public bool IsPlaying => _phase != Phase.Hidden;

        private void Awake()
        {
            SetVisible(0f);
            enabled = false;
        }

        public void Play(MemoryDefinition memory, Action onFinished)
        {
            _memory = memory;
            _onFinished = onFinished;
            _startFrame = Time.frameCount; // the button press that started it must not also advance it
            ShowTitle();
            Enter(Phase.FadeIn);
            enabled = true;
        }

        private void Update()
        {
            _phaseTime += Time.unscaledDeltaTime;
            bool confirm = _input != null && _input.ConfirmPressed && Time.frameCount != _startFrame;
            bool skip = _input != null && _input.CancelPressed && Time.frameCount != _startFrame;

            if (skip && _phase != Phase.FadeOut && _phase != Phase.Clue)
            {
                ShowClue();
                Enter(Phase.Clue);
                return;
            }

            switch (_phase)
            {
                case Phase.FadeIn:
                    SetVisible(_phaseTime / _fadeDuration);
                    FadeText(_titleLabel);
                    if (_phaseTime >= _fadeDuration) Enter(Phase.Title);
                    break;

                case Phase.Title:
                    FadeText(_titleLabel);
                    if (confirm || _phaseTime >= _titleDuration) NextLine(0);
                    break;

                case Phase.Line:
                    FadeText(_speakerLabel);
                    FadeText(_lineLabel);
                    float duration = _memory.Lines[_lineIndex].Duration > 0f ? _memory.Lines[_lineIndex].Duration : 4f;
                    if (confirm || _phaseTime >= duration) NextLine(_lineIndex + 1);
                    break;

                case Phase.Clue:
                    FadeText(_lineLabel);
                    if (confirm || skip || _phaseTime >= _clueDuration) Enter(Phase.FadeOut);
                    break;

                case Phase.FadeOut:
                    SetVisible(1f - _phaseTime / _fadeDuration);
                    if (_phaseTime >= _fadeDuration) Finish();
                    break;
            }
        }

        private void NextLine(int index)
        {
            if (_memory.Lines == null || index >= _memory.Lines.Length)
            {
                ShowClue();
                Enter(Phase.Clue);
                return;
            }

            _lineIndex = index;
            MemoryLine line = _memory.Lines[index];
            _titleLabel.text = string.Empty;
            _speakerLabel.text = string.IsNullOrEmpty(line.Speaker) ? string.Empty : line.Speaker;
            _lineLabel.fontStyle = string.IsNullOrEmpty(line.Speaker) ? FontStyle.Italic : FontStyle.Normal;
            _lineLabel.text = string.IsNullOrEmpty(line.Speaker) ? line.Text : $"“{line.Text}”";
            Enter(Phase.Line);
        }

        private void ShowTitle()
        {
            _titleLabel.text = string.IsNullOrEmpty(_memory.LocationName)
                ? _memory.Title
                : $"{_memory.Title}\n<size=26>{_memory.LocationName}</size>";
            _speakerLabel.text = string.Empty;
            _lineLabel.text = string.Empty;
            if (_promptLabel != null) _promptLabel.text = "E  continue    ·    G  skip";
        }

        private void ShowClue()
        {
            _titleLabel.text = string.Empty;
            _speakerLabel.text = "A clue to the Muddle";
            _lineLabel.fontStyle = FontStyle.Normal;
            _lineLabel.text = _memory.Clue;
            SetAlpha(_speakerLabel, 1f);
        }

        private void Enter(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
            if (phase == Phase.Line || phase == Phase.Clue)
            {
                SetAlpha(_lineLabel, 0f);
                if (phase == Phase.Line) SetAlpha(_speakerLabel, 0f);
            }
            if (phase == Phase.FadeIn) SetAlpha(_titleLabel, 0f);
        }

        private void Finish()
        {
            _phase = Phase.Hidden;
            SetVisible(0f);
            enabled = false;
            Action callback = _onFinished;
            _onFinished = null;
            _memory = null;
            callback?.Invoke();
        }

        private void FadeText(Text text)
        {
            if (text == null) return;
            SetAlpha(text, _textFadeDuration <= 0f ? 1f : Mathf.Clamp01(_phaseTime / _textFadeDuration));
        }

        private static void SetAlpha(Text text, float alpha)
        {
            if (text == null) return;
            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }

        private void SetVisible(float alpha)
        {
            if (_group == null) return;
            _group.alpha = Mathf.Clamp01(alpha);
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        internal void EditorConfigure(PlayerInputReader input, CanvasGroup group, Text title, Text speaker, Text line, Text prompt)
        {
            _input = input;
            _group = group;
            _titleLabel = title;
            _speakerLabel = speaker;
            _lineLabel = line;
            _promptLabel = prompt;
        }
    }
}
