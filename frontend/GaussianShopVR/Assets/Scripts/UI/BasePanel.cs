using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;

namespace GSShopUI
{
    public class BasePanel : MonoBehaviour
    {
        protected enum DescriptionState
        {
            Open,
            Closing,
            Closed
        }

        protected enum PanelState
        {
            Unused,
            Unavailable,
            Available,
        }

        [Serializable]
        public enum PanelType
        {
            SketchSurface,
            Color,
            Brush,
            AudioReactor,
            AdminPanelMobile,
            ToolsBasicMobile,
            ToolsBasic,
            Experimental,
            ToolsAdvancedMobile,
            MemoryWarning,
            Labs,
            Sketchbook,
            SketchbookMobile,
            BrushMobile,
            AppSettings,
            Tutorials,
            Reference,
            Lights,
            GuideTools,
            Environment,
            Camera,
            Testing,
            Poly,
            BrushExperimental,
            ToolsAdvanced,
            AppSettingsMobile,
            AdminPanel,
            ExtraPanel,
            ExtraMobile,
            PolyMobile,
            LabsMobile,
            ReferenceMobile,
            CameraPath,
            BrushLab,
            Multiplayer,
            WebcamPanel = 5200,
            Scripts = 6000,
            SnapSettings = 8000,
            StencilSettings = 20200,
            LayersPanel = 15000,
            TransformPanel = 12000,
        }

        private enum FixedTransitionState
        {
            Floating,
            FixedToFloating,
            FloatingToFixed,
            Fixed
        }

        [SerializeField] protected PanelType m_PanelType;

        [SerializeField] protected Collider m_Collider;
        [SerializeField] public GameObject m_Mesh;
        [SerializeField] protected Renderer m_Border;
        [SerializeField] protected Collider m_MeshCollider;
        [SerializeField] protected Vector3 m_ParticleBounds;

        //[SerializeField] protected PopupMapKey[] m_PanelPopUpMap;
        [SerializeField] protected string m_PanelDescription;
        [SerializeField] protected LocalizedString m_LocalizedPanelDescription;

        static public bool DoesRayHitCollider(Ray rRay, Collider rCollider, out RaycastHit rHitInfo)
        {
            return rCollider.Raycast(rRay, out rHitInfo, 100.0f);
        }

        public string PanelDescription
        {
            get
            {
                try
                {
                    var locString = m_LocalizedPanelDescription.GetLocalizedStringAsync().Result;
                    return locString;
                }
                catch
                {
                    return m_PanelDescription;
                }
            }
        }

        [SerializeField] protected GameObject m_PanelDescriptionPrefab;

        [SerializeField] protected Vector3 m_PanelDescriptionOffset;
        [SerializeField] protected Color m_PanelDescriptionColor;

        [SerializeField] protected GameObject m_PanelFlairPrefab;
        [SerializeField] protected Vector3 m_PanelFlairOffset;

        [SerializeField] protected float m_DescriptionSpringK = 4.0f;
        [SerializeField] protected float m_DescriptionSpringDampen = 0.2f;
        [SerializeField] protected float m_DescriptionClosedAngle = -90.0f;
        [SerializeField] protected float m_DescriptionOpenAngle = 0.0f;
        [SerializeField] protected float m_DescriptionAlphaDistance = 90.0f;

        [SerializeField] protected GameObject[] m_Decor;

        [SerializeField] protected float m_GazeHighlightScaleMultiplier = 1.2f;

        [SerializeField] private float m_BorderMeshWidth = 0.02f;
        [SerializeField] private float m_BorderMeshAdvWidth = 0.01f;

        [SerializeField] public float m_PanelSensitivity = 0.1f;
        [SerializeField] protected bool m_ClampToBounds = false;
        [SerializeField] protected Vector3 m_ReticleBounds;

        [SerializeField] public float m_BorderSphereHighlightRadius;
        [SerializeField] protected Vector2 m_PositioningSpheresBounds;
        [SerializeField] protected float m_PositioningSphereRadius = 0.4f;

        [SerializeField] public bool m_UseGazeRotation = false;
        [SerializeField] public float m_MaxGazeRotation = 20.0f;
        [SerializeField] protected float m_GazeActivateSpeed = 8.0f;

        [SerializeField] public Vector3 m_InitialSpawnPos;
        [SerializeField] public Vector3 m_InitialSpawnRotEulers;

        [SerializeField] public float m_WandAttachAngle;
        [SerializeField] public float m_WandAttachYOffset;
        [SerializeField] public float m_WandAttachHalfHeight;
        [SerializeField] private bool m_BeginFixed;
        [SerializeField] private bool m_CanBeFixedToWand = true;
        [SerializeField] private bool m_CanBeDetachedFromWand = true;

        [SerializeField] private float m_PopUpGazeDuration = .2f;

        [SerializeField] protected MeshRenderer[] m_PromoBorders;

        protected const float m_SwipeThreshold = 0.4f;
        protected Material m_BorderMaterial;

        [NonSerialized] public float m_SweetSpotDistance = 1.0f;
        [NonSerialized] public bool m_Fixed;
        [NonSerialized] public bool m_WandPrimedForAttach;
        [NonSerialized] public float m_WandAttachRadiusAdjust;
        [NonSerialized] public float m_WandAttachYOffset_Target;

        [NonSerialized] public float m_WandAttachYOffset_Stable;

        protected float m_PositioningPercent;

        protected int m_DelayedCommandParam;
        protected int m_DelayedCommandParam2;

        protected GameObject m_PanelDescriptionObject;
        protected Renderer m_PanelDescriptionRenderer;
        protected TextMeshPro m_PanelDescriptionTextMeshPro;

        protected Vector3 m_BaseScale;
        protected float m_AdjustedScale;

        protected Vector3 m_ReticleOffset;
        protected Vector3 m_Bounds;
        protected Vector3 m_WorkingReticleBounds;

        private float m_ScaledPositioningSphereRadius;
        private float m_PositioningExtent;
        private Vector3[] m_PositioningSpheres;
        private Vector3[] m_PositioningSpheresTransformed;

        protected PanelState m_CurrentState;
        protected PanelState m_DesiredState;
        protected bool m_GazeActive;

        private bool m_AdvancedModePanel;
        private float m_WandAttachYOffset_Initial;
        private float m_WandAttachAngle_Initial;

        private Vector3 m_GazeHitPositionCurrent;
        private Vector3 m_GazeHitPositionDesired;
        private float m_GazeHitPositionSpeed = 10.0f;
        protected UIComponentManager m_UIComponentManager;
        public Vector3 GetBounds() { return m_Bounds; }

        public float GetPositioningExtent() { return m_PositioningExtent; }

        public bool IsAvailable() { return m_CurrentState != PanelState.Unavailable; }
        public bool IsActive() { return m_GazeActive; }

        public bool BeginFixed { get { return m_BeginFixed; } }
        public void ClearBeginFixed() { m_BeginFixed = false; }
        public bool CanBeDetached { get { return m_CanBeDetachedFromWand; } }

        public bool IsInInitialPosition()
        {
            return m_WandAttachYOffset == m_WandAttachYOffset_Initial &&
                m_WandAttachAngle == m_WandAttachAngle_Initial;
        }

        public bool AdvancedModePanel { get { return m_AdvancedModePanel; } }

        //public Color GetGazeColorFromActiveGazePercent()
        //{
        //    //PanelManager pm = PanelManager.m_Instance;
        //    //return Color.Lerp(pm.PanelHighlightInactiveColor, pm.PanelHighlightActiveColor,
        //    //    m_GazeActivePercent);
        //}

        public PanelType Type
        {
            get { return m_PanelType; }
        }
        virtual public void SetInIntroMode(bool inIntro) { }
        public void SetPositioningPercent(float fPercent)
        {
            m_PositioningPercent = fPercent;
        }
        virtual public void PanelGazeActive(bool bActive)
        {
            m_GazeActive = bActive;
            m_GazeHitPositionCurrent = transform.position;
            m_UIComponentManager.ResetInput();
        }
        public void SetScale(float fScale)
        {
            m_AdjustedScale = fScale;
            transform.localScale = m_BaseScale * m_AdjustedScale;
        }
        public virtual bool ShouldRegister { get { return true; } }
    }
}
