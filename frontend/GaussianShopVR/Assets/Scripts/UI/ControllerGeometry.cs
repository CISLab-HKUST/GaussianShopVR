using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GSShopUI
{

    public enum ControllerStyle
    {
        Unset,
        None,
        InitializingUnityXR,   // can change to "initialising" or "discovering"
        OculusTouch,
    }
    public class ControllerGeometry : MonoBehaviour
    {
        [SerializeField] private ControllerStyle m_ControllerStyle = ControllerStyle.None;

        [SerializeField] private Transform m_PointerAttachAnchor;
        [SerializeField] private Transform m_PointerAttachPoint;
        [SerializeField] private Transform m_ToolAttachAnchor;
        [SerializeField] private Transform m_ToolAttachPoint;
        [SerializeField] private Transform m_PinCushionSpawn;
        [SerializeField] private Transform m_MainAxisAttachPoint;
        [SerializeField] private Transform m_CameraAttachPoint;
        [SerializeField] private Transform m_ConsoleAttachPoint;
        [SerializeField] private Transform m_BaseAttachPoint;
        [SerializeField] private Transform m_GripAttachPoint;
        [SerializeField] private Transform m_DropperDescAttachPoint;

        [SerializeField] private Renderer m_MainMesh;
        [SerializeField] private Renderer m_TriggerMesh;
        [SerializeField] private Renderer[] m_OtherMeshes;
        [SerializeField] private Renderer m_LeftGripMesh;
        [SerializeField] private Renderer m_RightGripMesh;
        [SerializeField] private Transform m_PadTouchLocator;
        [SerializeField] private Transform m_TriggerAnchor;
        [SerializeField] private HintObjectScript m_PinHint;
        [SerializeField] private HintObjectScript m_UnpinHint;
        [SerializeField] private HintObjectScript m_PreviewKnotHint;
        [SerializeField] private Renderer m_TransformVisualsRenderer;
        [SerializeField] private GameObject m_ActivateEffectPrefab;
        [SerializeField] private GameObject m_HighlightEffectPrefab;
        [SerializeField] private GameObject m_XRayVisuals;

        [Header("Controller Animations")]
        [Tooltip("Range of rotation for TriggerAnchor, in degrees. Rotation is about the right axis")]
        [SerializeField] private Vector2 m_TriggerRotation;
        [SerializeField] private float m_TouchLocatorTranslateScale = 0.27f;
        [SerializeField] private float m_TouchLocatorTranslateClamp = 0.185f;
        [SerializeField] private Material m_GripReadyMaterial;
        [SerializeField] private Material m_GrippedMaterial;
        [SerializeField] private Vector3 m_LeftGripPopInVector;
        [SerializeField] private Vector3 m_LeftGripPopOutVector;

        [Header("Pad Controls")]
        [SerializeField] float m_PadPopUpAmount;
        [SerializeField] float m_PadScaleAmount;
        [SerializeField] float m_PadSpeed;

        [Header("Haptics")]
        [SerializeField] float m_HapticPulseOn;
        [SerializeField] float m_HapticPulseOff;

        [Header("Oculus Touch Buttons")]
        [SerializeField] private Transform m_Joystick;
        [SerializeField] private Renderer m_JoystickMesh;
        [SerializeField] private Renderer m_JoystickPad;
        [SerializeField] private Renderer m_Button01Mesh;
        [SerializeField] private Renderer m_Button02Mesh;

        [Header("Wmr Button")]
        [SerializeField] private Renderer m_PinCushion;

        [Header("Wand objects")]
        [SerializeField] private HintObjectScript m_MenuPanelHintObject;
        [SerializeField] private HintObjectScript m_QuickLoadHintObject;
        [SerializeField] private HintObjectScript m_SwipeHint;

        [Header("Brush objects")]
        [SerializeField] private LineRenderer m_GuideLine;
        [SerializeField] private HintObjectScript m_PaintHintObject;
        [SerializeField] private HintObjectScript m_BrushSizeHintObject;
        [SerializeField] private HintObjectScript m_PointAtPanelsHintObject;
        [SerializeField] private HintObjectScript m_ShareSketchHintObject;
        [SerializeField] private HintObjectScript m_FloatingPanelHintObject;
        [SerializeField] private HintObjectScript m_AdvancedModeHintObject;
        [SerializeField] private HintObjectScript m_SelectionHint;
        [SerializeField] private GameObject m_SelectionHintButton;
        [SerializeField] private HintObjectScript m_DeselectionHint;
        [SerializeField] private GameObject m_DeselectionHintButton;
        [SerializeField] private HintObjectScript m_DuplicateHint;
        [SerializeField] private HintObjectScript m_SaveIconHint;

        public Vector2 TriggerRotation { get { return m_TriggerRotation; } }
        public float TouchLocatorTranslateScale { get { return m_TouchLocatorTranslateScale; } }
        public float TouchLocatorTranslateClamp { get { return m_TouchLocatorTranslateClamp; } }
        public Material GripReadyMaterial { get { return m_GripReadyMaterial; } }
        public Material GrippedMaterial { get { return m_GrippedMaterial; } }
        public Material BaseGrippedMaterial { get { return m_BaseGrippedMaterial; } }
        public Vector3 LeftGripPopInVector { get { return m_LeftGripPopInVector; } }
        public Vector3 LeftGripPopOutVector { get { return m_LeftGripPopOutVector; } }

        public Transform PointerAttachAnchor { get { return m_PointerAttachAnchor; } }
        public Transform PointerAttachPoint { get { return m_PointerAttachPoint; } }
        public Transform ToolAttachAnchor { get { return m_ToolAttachAnchor; } }
        public Transform ToolAttachPoint { get { return m_ToolAttachPoint; } }
        public Transform PinCushionSpawn { get { return m_PinCushionSpawn; } }
        public Transform MainAxisAttachPoint { get { return m_MainAxisAttachPoint; } }
        public Transform CameraAttachPoint { get { return m_CameraAttachPoint; } }
        public Transform ConsoleAttachPoint { get { return m_ConsoleAttachPoint; } }
        public Transform BaseAttachPoint { get { return m_BaseAttachPoint; } }
        public Transform GripAttachPoint { get { return m_GripAttachPoint; } }
        public Transform DropperDescAttachPoint { get { return m_DropperDescAttachPoint; } }

        public Renderer MainMesh { get { return m_MainMesh; } }
        public Renderer TriggerMesh { get { return m_TriggerMesh; } }
        public Renderer[] OtherMeshes { get { return m_OtherMeshes; } }
        public Renderer LeftGripMesh { get { return m_LeftGripMesh; } }
        public Renderer RightGripMesh { get { return m_RightGripMesh; } }
        public Transform PadTouchLocator { get { return m_PadTouchLocator; } }
        public Transform TriggerAnchor { get { return m_TriggerAnchor; } }
        public HintObjectScript PinHint { get { return m_PinHint; } }
        public HintObjectScript UnpinHint { get { return m_UnpinHint; } }
        public HintObjectScript PreviewKnotHint { get { return m_PreviewKnotHint; } }
        public HintObjectScript SelectionHint { get { return m_SelectionHint; } }
        public GameObject SelectionHintButton { get { return m_SelectionHintButton; } }
        public HintObjectScript DeselectionHint { get { return m_DeselectionHint; } }
        public GameObject DeselectionHintButton { get { return m_DeselectionHintButton; } }
        public HintObjectScript DuplicateHint { get { return m_DuplicateHint; } }
        public HintObjectScript SaveIconHint { get { return m_SaveIconHint; } }
        public Renderer TransformVisualsRenderer { get { return m_TransformVisualsRenderer; } }
        public GameObject ActivateEffectPrefab { get { return m_ActivateEffectPrefab; } }
        public GameObject HighlightEffectPrefab { get { return m_HighlightEffectPrefab; } }
        public GameObject XRayVisuals { get { return m_XRayVisuals; } }

        // Rift & Knuckles controller components.
        public Transform Joystick { get { return m_Joystick; } }
        public Renderer JoystickMesh { get { return m_JoystickMesh; } }
        public Renderer JoystickPad { get { return m_JoystickPad; } }
        public Renderer Button01Mesh { get { return m_Button01Mesh; } }
        public Renderer Button02Mesh { get { return m_Button02Mesh; } }

        // Wmr controller components.
        public Renderer PinCushionMesh { get { return m_PinCushion; } }

        // Wand objects
        public HintObjectScript QuickLoadHintObject { get { return m_QuickLoadHintObject; } }
        public HintObjectScript SwipeHintObject { get { return m_SwipeHint; } }
        public HintObjectScript MenuPanelHintObject { get { return m_MenuPanelHintObject; } }

        // Brush objects
        public LineRenderer GuideLine { get { return m_GuideLine; } }
        public HintObjectScript PaintHintObject { get { return m_PaintHintObject; } }
        public HintObjectScript BrushSizeHintObject { get { return m_BrushSizeHintObject; } }
        public HintObjectScript PointAtPanelsHintObject { get { return m_PointAtPanelsHintObject; } }
        public HintObjectScript ShareSketchHintObject { get { return m_ShareSketchHintObject; } }
        public HintObjectScript FloatingPanelHintObject { get { return m_FloatingPanelHintObject; } }
        public HintObjectScript AdvancedModeHintObject { get { return m_AdvancedModeHintObject; } }

        public bool PadEnabled { get; set; }

        public BaseControllerBehavior Behavior { get => m_Behavior; }

        public InputManager.ControllerName ControllerName { get => m_ControllerName; }

        public ControllerStyle Style { get => m_ControllerStyle; }

        public ControllerStyle TempWritableStyle
        {
            set
            {
                if (m_ControllerStyle == value)
                { /* no warning */
                }
                // This is kind of a hack, because the same prefab is used for both "empty geometry"
                // and "initializing steam vr". In all other cases, m_ControllerStyle is expected
                // to be set properly in the prefab. Perhaps we can remove this last mutable case
                // and detect the initializing case differently.
                else if (m_ControllerStyle == ControllerStyle.None && value == ControllerStyle.InitializingUnityXR)
                {
                    /* no warning */
                }
                else
                {
                    Debug.LogWarningFormat(
                        "Unity bug? Prefab had incorrect m_ControllerStyle {0} != {1}; try re-importing it.",
                        m_ControllerStyle, value);
                }
                m_ControllerStyle = value;
            }
        }

        public ControllerInfo ControllerInfo { get => m_Behavior.ControllerInfo; }

        private bool EmptyGeometry
        {
            get => m_ControllerStyle == ControllerStyle.None
                || m_ControllerStyle == ControllerStyle.InitializingUnityXR;
        }

        class PopupAnimState
        {
            public readonly VRInput input;
            public readonly Transform anchor;
            public readonly float initialY;
            public readonly float initialScale;
            public float current;

            public PopupAnimState(Transform anchor, VRInput input)
            {
                this.anchor = anchor;
                this.input = input;
                this.current = 0;
                if (anchor != null)
                {
                    this.initialY = anchor.localPosition.y;
                    this.initialScale = anchor.localScale.x;
                }
            }
        }

        private PopupAnimState m_JoyAnimState;
        private PopupAnimState m_PadAnimState;
        private int m_LastPadButton;
        private Material m_BaseGrippedMaterial;
        private float m_LogitechPenHandednessHysteresis = 10.0f;
        // True if we're the default orientation, false if we need to be rotated 180 degrees.
        private bool m_LogitechPenHandedness;

        private BaseControllerBehavior m_Behavior;
        private InputManager.ControllerName m_ControllerName;

      

        // -------------------------------------------------------------------------------------------- //
        // Unity Events
        // -------------------------------------------------------------------------------------------- //

        private void Awake()
        {
            if (LeftGripMesh != null)
            {
                m_BaseGrippedMaterial = LeftGripMesh.material;

            }
        }
        //private ControllerMaterialCatalog Materials
        //{
        //    get { return ControllerMaterialCatalog.m_Instance; }
        //}
        private T SelectIfTouched<T>(VRInput input, T active, T inactive)
        {
            var info = ControllerInfo;
            if (info != null && info.GetVRInputTouch(input))
            {
                return active;
            }
            else
            {
                return inactive;
            }
        }

        //private ControllerMaterialCatalog Materials
        //{
        //    get { return ControllerMaterialCatalog.m_Instance; }
        //}

        private Material SelectThumbStickTouched(Material active, Material inactive)
        {
            return SelectIfTouched(VRInput.Thumbstick, active, inactive);
        }

        private Material SelectBasedOn(Material active, Material inactive)
        {
            var info = ControllerInfo;
            // TODO: we should remove this MenuContextClick command in favor of calling it Button04;
            // (and potentially rename button04 to something more descriptive). The extra indirection isn't
            // buying us anything, and it prevents us from using GetVrInputTouch(button04) which is
            // actually what we mean here.
            if (info != null && info.GetCommand(InputManager.ActionCommands.MenuContextClick))
            {
                return active;
            }
            else
            {
                return inactive;
            }
        }
    }
    
}