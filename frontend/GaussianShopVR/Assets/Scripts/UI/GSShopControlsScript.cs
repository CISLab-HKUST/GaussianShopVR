using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GSShopUI
{
    public class GSShopControlsScript : MonoBehaviour
    {
        public const string kRemoveHeadsetFyi = "Remove headset to view.";
        const string kTiltBrushGalleryUrl = "https://poly.google.com/tiltbrush";
        const string kBlocksGalleryUrl = "https://poly.google.com/blocks";
        const string kPolyMainPageUri = "https://poly.google.com";

        static public GSShopControlsScript m_Instance;
        static bool sm_enableGrabHaptics = true;

        private ControlsType m_ControlsType;

        public enum GlobalCommands
        {
            Null,
            Save,
            SaveNew,
            Load,
            NewSketch,
            StraightEdge,
            AutoOrient,
            Undo,
            Redo,
            Tiltasaurus,
            LightingHdr,
            AudioVisualization,
            ResetAllPanels,
            SketchOrigin,
            SymmetryPlane,
            MultiMirror,
            ViewOnly,
            SaveGallery,
            LightingLdr,
            ShowSketchFolder,
            About,
            LoadNamedFile, // iParam1 : (optional) - send through a LoadSpeed as int
            DropCam,
            CuratedGallery,
            Unused_UploadToCloud,
            AnalyticsEnabled_Deprecated,
            Credits,
            LogOutOfGenericCloud,
            DraftingVisibility,
            DeleteSketch,
            ShowWindowGUI,
            MorePanels,
            Cameras,
            FAQ,
            ExportRaw,
            IRC,
            YouTubeChat,
            CameraOptions,
            StencilsDisabled,
            AdvancedTools,
            FloatingPanelsMode,
            StraightEdgeMeterDisplay,
            Sketchbook,
            ExportAll,
            Lights,
            SaveAndUpload,
            StraightEdgeShape,
            SaveOptions,
            SketchbookMenu,
            Disco,
            ViewOnlineGallery,
            CancelUpload,
            AdvancedPanelsToggle,
            Music,
            Duplicate,
            ToggleGroupStrokesAndWidgets,
            SaveModel,
            ViewPolyPage,
            ViewPolyGallery,
            ExportListed,
            RenderCameraPath,
            ToggleProfiling,
            DoAutoProfile,
            DoAutoProfileAndQuit,
            ToggleSettings,
            SummonMirror,
            InvertSelection,
            SelectAll,
            FlipSelection,
            ToggleBrushLab,
            ReleaseNotes,
            ToggleCameraPostEffects,
            ToggleWatermark,
            AccountInfo,

            // LoadConfirmUnsaved -> LoadWaitOnDownload -> LoadConfirmComplex -> LoadComplexHigh ->  Load
            LoadConfirmUnsaved,
            LoadConfirmComplex,
            MemoryWarning,
            MemoryExceeded,
            ViewLastUpload,
            LoadConfirmComplexHigh,
            ShowTos,
            ShowPrivacy,
            ShowQuestSideLoading,
            AshleysSketch,
            UnloadReferenceImageCatalog,
            SaveOnLocalChanges,
            ToggleCameraPathVisuals,
            ToggleCameraPathPreview,
            DeleteCameraPath,
            RecordCameraPath,
            SelectCameraPath,
            ToggleAutosimplification,
            ShowGoogleDrive,
            GoogleDriveSync_Folder, // iParam1: folder id as DriveSync.SyncedFolderType
            GoogleDriveSync,
            LoginToGenericCloud, // iParam1: Cloud enum
            UploadToGenericCloud, // iParam1: Cloud enum
            LoadWaitOnDownload,
            SignOutConfirm,
            ReadOnlyNotice,
            ShowContribution,
            WhatIsNew,

            // Open Brush Reserved Enums 1000-1999
            LanguagePopup = 1000,
            MultiplayerTogglePanel = 1001,
            MultiplayerPanelOptions = 1002, // iParam1: Popup options
            MultiplayerJoinRoom = 1004,
            EditMultiplayerRoomName = 1005,
            MultiplayerLeaveRoom = 1006,
            MultiplayerConnect = 1007,
            MultiplayerDisconnect = 1008,
            EditMultiplayerNickName = 1009,
            DisplaySynchInfo = 1010,
            SynchInfoPercentageUpdate = 1011,
            HideSynchInfo = 1012,

            RenameSketch = 5200,
            OpenLayerOptionsPopup = 5201,
            RenameLayer = 5202,
            OpenDirectorChooserPopup = 5800,
            OpenScriptsCommandsList = 6000,
            OpenScriptsList = 6001,
            OpenExampleScriptsList = 6002,
            SymmetryTwoHanded = 6003,
            OpenColorOptionsPopup = 7000,
            ChangeSnapAngle = 8000,
            MergeBrushStrokes = 10000,
            RepaintOptions = 11500,
            OpenNumericInputPopup = 12000
        }

        public enum ControlsType
        {
            KeyboardMouse,
            SixDofControllers,
            ViewingOnly
        }

        public enum DraftingVisibilityOption
        {
            Visible,
            Transparent,
            Hidden
        }

        public enum InputState
        {
            Standard,
            Pan,
            Rotation,
            HeadLock,
            ControllerLock,
            PushPull,
            BrushSize,
            Save,
            Load,
            Num
        }

        public enum LoadSpeed
        {
            Normal = -1,
            Quick = 1,
        }

        const float kControlPointHistoryMaxTime = 0.1f;

        class GazeResult
        {
            public bool m_HitWithGaze;
            public bool m_HitWithController;

            // ReSharper disable once NotAccessedField.Local
            public bool m_WithinView;
            public float m_ControllerDistance;
            public Vector3 m_GazePosition;
            public Vector3 m_ControllerPosition;
        }

        class InputStateConfig
        {
            public bool m_AllowDrawing;
            public bool m_AllowMovement;
            public bool m_ShowGizmo;
        }

        enum FadeState
        {
            None,
            FadeOn,
            FadeOff
        }

        enum GrabWidgetState
        {
            None,
            OneHand,
            TwoHands
        }

        enum GrabWorldState
        {
            Normal,
            ResettingTransform,
            ResetDone
        }

        private enum WorldTransformResetState
        {
            Default,
            Requested,
            FadingToBlack,
            FadingToScene,
        }

        enum RotationType
        {
            All,
            RollOnly
        }

        enum GrabIntersectionState
        {
            RequestIntersections,
            ReadBrush,
            ReadWand
        }

        public ControlsType ActiveControlsType
        {
            get { return m_ControlsType; }
            set { m_ControlsType = value; }
        }

        [SerializeField]
        GameObject m_RotationIconPrefab;

        [SerializeField]
        GameObject m_TransformGizmoPrefab;

        [SerializeField]
        GameObject m_UIReticle;

        [SerializeField]
        GameObject m_TestUIPanel;

        [SerializeField]
        GameObject m_TestControllerAnchor;

        [SerializeField]
        GameObject m_TestControllerPoint;

        private GameObject m_TransformGizmo;

        private int m_CurrentGazeObject;

        private GameObject m_RotationIcon;

        private bool m_ViewOnly = false;
        private bool m_PanelsVisibilityRequested;

        void Start()
        {
            //m_TransformGizmo = (GameObject)Instantiate(m_TransformGizmoPrefab);
            //m_TransformGizmo.transform.parent = transform;
            //m_TransformGizmoScript = m_TransformGizmo.GetComponent<TransformGizmoScript>();
            //m_TransformGizmo.SetActive(false);

            //m_RotationIcon = (GameObject)Instantiate(m_RotationIconPrefab);
            //m_RotationIcon.transform.position = m_SketchSurface.transform.position;
            //m_RotationIcon.transform.parent = m_SketchSurface.transform;
            //m_RotationIcon.SetActive(false);

            //GameObject pinCushionObj = (GameObject)Instantiate(m_PinCushionPrefab);
            //m_PinCushion = pinCushionObj.GetComponent<PinCushion>();

            m_CurrentGazeObject = -1;
            int hidePanelsDelay = 1;

            //StartCoroutine(DelayedHidePanels(hidePanelsDelay));
        }

        void Update()
        {
            UpdateActiveGazeObject();
        }

        void UpdateActiveGazeObject()
        {
            //BasePanel currentPanel = m_PanelManager.GetPanel(m_CurrentGazeObject);

            Vector3 reticlePos = Vector3.zero;
            Vector3 reticleForward = Vector3.zero;

            if (m_CurrentGazeObject == -1)
            {
                m_TestUIPanel
                    .GetComponent<ColorPicker>()
                    .GetReticleTransformFromPosDir(
                        m_TestControllerAnchor.transform.position,
                        m_TestControllerPoint.transform.position
                            - m_TestControllerAnchor.transform.position,
                        out reticlePos,
                        out reticleForward
                    );
                SetUIReticleTransform(reticlePos, -reticleForward);
            }
            Vector3 vReticlePos = GetUIReticlePos();
            Ray m_RecticleSelectionRay = new Ray(
                vReticlePos - m_TestUIPanel.transform.forward,
                m_TestUIPanel.transform.forward
            );
            m_TestUIPanel
                .GetComponent<ColorPicker>()
                .UpdateStateWithInput(true, m_RecticleSelectionRay);
            //currentPanel.GetReticleTransformFromPosDir(m_CurrentGazeHitPoint,
            //        m_GazeControllerRay.direction, out reticlePos, out reticleForward);

            //SetUIReticleTransform(reticlePos, -reticleForward);
            //m_UIReticle.SetActive(GetGazePanelActivationRatio() >= 1.0f);
        }

        public Vector3 GetUIReticlePos()
        {
            return m_UIReticle.transform.position;
        }

        public void ForceShowUIReticle(bool bVisible)
        {
            m_UIReticle.SetActive(bVisible);
        }

        public void SetUIReticleTransform(Vector3 vPos, Vector3 vForward)
        {
            m_UIReticle.transform.position = vPos;
            m_UIReticle.transform.forward = vForward;
        }

        public void RequestPanelsVisibility(bool bVisible)
        {
            // Always false in viewonly mode
            bVisible = m_ViewOnly ? false : bVisible;
            m_PanelsVisibilityRequested = bVisible;
        }
    }
}
