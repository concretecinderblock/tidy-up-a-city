using System;
using UnityEngine.InputSystem;

namespace TidyUp.Core
{
    /// <summary>
    /// Flat-screen gameplay actions (keyboard/mouse + gamepad), defined in code so bindings live next to
    /// the code that reads them. XR will get its own interaction source (XR Interaction Toolkit) that
    /// calls the same Interactor API, so gameplay code never depends on a device.
    /// </summary>
    public sealed class TidyInput : IDisposable
    {
        public readonly InputActionMap Map;
        public readonly InputAction Move;
        public readonly InputAction LookDelta;
        public readonly InputAction LookStick;
        public readonly InputAction Interact;
        public readonly InputAction Drop;
        public readonly InputAction CycleNext;
        public readonly InputAction CyclePrevious;
        public readonly InputAction Scroll;
        public readonly InputAction Sprint;
        public readonly InputAction TidySense;
        public readonly InputAction Pause;
        public readonly InputAction ShowMap;

        public TidyInput()
        {
            Map = new InputActionMap("Player");

            Move = Map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            LookDelta = Map.AddAction("LookDelta", InputActionType.Value, "<Mouse>/delta");
            LookStick = Map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            Interact = Map.AddAction("Interact", InputActionType.Button, "<Mouse>/leftButton");
            Interact.AddBinding("<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonSouth");

            Drop = Map.AddAction("Drop", InputActionType.Button, "<Keyboard>/g");
            Drop.AddBinding("<Mouse>/rightButton");
            Drop.AddBinding("<Gamepad>/buttonEast");

            CycleNext = Map.AddAction("CycleNext", InputActionType.Button, "<Keyboard>/tab");
            CycleNext.AddBinding("<Gamepad>/rightShoulder");
            CyclePrevious = Map.AddAction("CyclePrevious", InputActionType.Button, "<Keyboard>/q");
            CyclePrevious.AddBinding("<Gamepad>/leftShoulder");
            Scroll = Map.AddAction("Scroll", InputActionType.Value, "<Mouse>/scroll/y");

            Sprint = Map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            TidySense = Map.AddAction("TidySense", InputActionType.Button, "<Keyboard>/f");
            TidySense.AddBinding("<Gamepad>/buttonNorth");

            Pause = Map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");
            ShowMap = Map.AddAction("ShowMap", InputActionType.Button, "<Keyboard>/m");
            ShowMap.AddBinding("<Gamepad>/select");
        }

        public void Enable() => Map.Enable();
        public void Disable() => Map.Disable();

        public void Dispose()
        {
            Map.Disable();
            Map.Dispose();
        }
    }
}
