using Samples.Whisper;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.UIElements;

namespace GSShopUI
{
    using ActionCommands = InputManager.ActionCommands;
    public abstract class ControllerInfo
    {
        public ControllerInfo(BaseControllerBehavior behavior)
        {
            m_Behavior = behavior;
            m_Transform = behavior.transform;

        }
        protected static bool IsInPosition(Vector2 padOrStickPos, VRInput input)
        {
            switch (input)
            {
                case VRInput.Button01:
                    return padOrStickPos.x < 0.0f;
                case VRInput.Button02:
                    return padOrStickPos.x > 0.0f;
                case VRInput.Button05:
                    return padOrStickPos.y > 0.0f && (Mathf.Abs(padOrStickPos.y) > Mathf.Abs(padOrStickPos.x));
                case VRInput.Button06:
                    return padOrStickPos.y < 0.0f && (Mathf.Abs(padOrStickPos.y) > Mathf.Abs(padOrStickPos.x));

                case VRInput.Any:
                    return true;

                default:
                    return true;
            }
        }
        private readonly Transform m_Transform;
        public Transform Transform => m_Transform;
        public BaseControllerBehavior Behavior => m_Behavior;

        private readonly BaseControllerBehavior m_Behavior;
        public bool GetCommand(ActionCommands rCommand)
        {
            switch (rCommand)
            {
                case ActionCommands.Activate:
                    return IsTrigger();
                case ActionCommands.AltActivate:
                    return IsTrigger();
                case ActionCommands.WandRotation:
                    return GetPadTouch() || GetThumbStickTouch();
                case ActionCommands.LockToController:
                    return GetControllerGrip();
                case ActionCommands.Scale:
                    return GetPadTouch() || GetThumbStickTouch();
                case ActionCommands.Panic:
                    return IsTrigger();
                case ActionCommands.MultiCamSelection:
                    return GetVRInput(VRInput.Button04 /*full-pad-button*/);
                case ActionCommands.MenuContextClick:
                    return GetVRInput(VRInput.Button04 /*full-pad-button*/);
                case ActionCommands.ShowPinCushion:
                    return GetVRInput(VRInput.Button03);
                case ActionCommands.DuplicateSelection:
                    return GetVRInput(VRInput.Button04);
                case ActionCommands.Undo:
                    return GetVRInput(VRInput.Button01 /*half_left*/);
                case ActionCommands.Redo:
                    return GetVRInput(VRInput.Button02 /*half_right*/);
                case ActionCommands.Fly:
                    return IsTrigger();
            }

            return false;
        }
        public bool GetControllerGrip()
        {
            return GetVRInput(VRInput.Grip);
        }
        public bool GetThumbStickTouch()
        {
            return GetVRInputTouch(VRInput.Thumbstick);
        }
        public bool IsTrigger()
        {
            return GetVRInput(VRInput.Trigger);
        }
        public ControllerGeometry Geometry => Behavior.ControllerGeometry;
        public bool GetPadTouch()
        {
            return GetVRInputTouch(VRInput.Touchpad);
        }
        public abstract float GetScrollXDelta();
        public abstract float GetScrollYDelta();
        public abstract bool GetVRInput(VRInput input);
        public abstract bool GetVRInputTouch(VRInput input);
    }
}