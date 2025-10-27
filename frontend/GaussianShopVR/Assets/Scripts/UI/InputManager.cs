using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.InputSystem;
using KeyMap = System.Collections.Generic.Dictionary<
    int,
    UnityEngine.InputSystem.Key[]>;

namespace GSShopUI
{
    public class InputManager : MonoBehaviour
    {
        const string PLAYER_PREF_WAND_ON_RIGHT = "WandOnRight";

        // Controller-swap gesture tunables
        const float kSwapDistMeters = 0.04f;
        const float kSwapResetDistMeters = 0.16f;
        const float kSwapForwardAngle = 130f;  // degrees
        const float kSwapVelocityAngle = 150f; // degrees
        const float kSwapAcceleration = 10f;   // decimeters / second^2

        public enum ControllerName
        {
            Wand = 0,
            Brush,
            Num,
            None
        }

        public enum ActionCommands
        {
            Activate,
            AltActivate,
            PanelShowHide,
            LockToHead,
            PivotRotation,
            WandRotation,
            LockToController,
            Scale,
            Sensitivity,
            Reset, // Advanced Keyboard Shortcut mode only
            Undo,
            Redo,
            Delete,
            Abort,
            Panic,
            RewindTimeline,
            AdvanceTimeline,
            TimelineHome,
            TimelineEnd,
            MultiCamSelection,
            WorldTransformReset,
            Teleport,
            ShowPinCushion,
            ToggleDefaultTool,
            RespawnPanels,
            SwapControls,
            MenuContextClick,
            PinWidget,
            ToggleSelection,
            GroupStrokes,
            DuplicateSelection,
            Confirm,
            Cancel,
            Trash,
            Share,
            Fly,
        }

        public enum KeyboardShortcut
        {

        }

        private static readonly KeyMap m_KeyMap = new KeyMap
        {


        };

        private static readonly KeyMap m_NoHeadsetKeyMap = new KeyMap
        {
        };

        private static readonly KeyMap m_DemoKeyMap = new KeyMap
        { };

        private KeyMap ActiveKeyMap
        {
            get { return m_NoHeadsetKeyMap; }
        }

        [System.Serializable]
        public struct HmdInfo
        {
            public MeshRenderer m_Renderer;
        }

        public struct TouchInput
        {
            public bool m_Valid;
            public Vector2 m_Pos;
        }

        public static InputManager m_Instance;
        private ControllerInfo[] m_ControllerInfos;
        public static ControllerInfo[] Controllers { get => m_Instance.m_ControllerInfos; }

        public static ControllerInfo Wand { get => Controllers[(int)ControllerName.Wand]; }

        public static ControllerInfo Brush { get => Controllers[(int)ControllerName.Brush]; }
        //public static event Action OnSwapControllers;

        //[SerializeField] Transform


        [SerializeField] HmdInfo m_HmdInfo;

        private bool m_AllowVrControllers = true;

        private float m_InputThreshold = 0.0001f;

        private TouchInput m_Touch;
        private bool m_WandOnRight;

        public event Action ControllerPosesApplied;

        public bool AllowVrControllers
        {
            get => m_AllowVrControllers;
            set
            {
                m_AllowVrControllers = value;
                EnableVrControllers(value);
            }
        }

        public void EnableVrControllers(bool bEnable)
        {
            for (int i = 0; i < m_ControllerInfos.Length; ++i)
            {
                m_ControllerInfos[i].Transform.gameObject.SetActive(bEnable);
            }
        }

        public bool WandOnRight
        {
            get => m_WandOnRight;
            set
            {
                if (m_WandOnRight == value) { return; }
                m_WandOnRight = value;
                PlayerPrefs.SetInt(PLAYER_PREF_WAND_ON_RIGHT, WandOnRight ? 1 : 0);

                //TODO:
                // var vrControllers = App.VrSdk.VrControls;
            }
        }

        public void EnablePoseTracking(bool enabled)
        {
            //UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(App.VrSdk.GetVrCamera(), !enabled);
            //if (enabled)
            //{
                //App.VrSdk.RestorePoseTracking();
            //}
            //else
            {
                //App.VrSdk.DisablePoseTracking();
            }
            //App.VrSdk.VrControls.EnablePoseTracking(enabled);
            //UnityEngine.XR.InputTracking.disablePositionalTracking = !enabled;
        }

        void Awake()
        {
            m_Instance = this;

            // Instantiate so we can mutate without modifying a global asset
            // (the assumption is that m_HmdInfo never changes)
            if (m_HmdInfo.m_Renderer)
            {
                m_HmdInfo.m_Renderer.sharedMaterial =
                    Instantiate(m_HmdInfo.m_Renderer.sharedMaterial);
            }
        }

        void OnEnable()
        {
            
        }

        public void CreateControllerInfos()
        {

        }
        public float GetWandScrollAmount()
        {
            return Wand.GetScrollXDelta();
            //return Wand.GetScrollYDelta();
        }
        public bool GetCommand(ActionCommands rCommand)
        {
            switch (rCommand)
            {
                case ActionCommands.Activate:
                    return Brush.GetCommand(rCommand);
                case ActionCommands.AltActivate:
                    return Wand.GetCommand(rCommand);
                case ActionCommands.WandRotation:
                    return Wand.GetCommand(rCommand);
                case ActionCommands.LockToController:
                    return Wand.GetCommand(rCommand) || Brush.GetCommand(rCommand);
                case ActionCommands.Scale:
                    return Brush.GetCommand(rCommand);
                case ActionCommands.Sensitivity:
                    return Mathf.Abs(Mouse.current.scroll.x.ReadValue()) > m_InputThreshold;
                case ActionCommands.Panic:
                    return Wand.GetCommand(rCommand);
                case ActionCommands.MultiCamSelection:
                    return Brush.GetCommand(rCommand);
                case ActionCommands.ShowPinCushion:
                    return Brush.GetCommand(rCommand);
                case ActionCommands.DuplicateSelection:
                    return Brush.GetCommand(rCommand);
                case ActionCommands.Undo:
                case ActionCommands.Redo:
                    return Wand.GetCommand(rCommand);
                case ActionCommands.Fly:
                    return Brush.GetCommand(rCommand);
            }


            return false;
        }
    }
}