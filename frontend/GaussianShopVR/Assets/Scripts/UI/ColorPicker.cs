using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GSShopUI
{
    public class ColorPicker : MonoBehaviour
    {
        public event Action<Color> ColorPicked;
        public event Action ColorFinalized;
        public InputActionReference ColorActive;
        public bool m_active;
        public Color currentColor;

        [SerializeField]
        private ColorPickerSelector m_ColorPickerSelector;

        [SerializeField]
        private ColorPickerSlider m_ColorPickerSlider;

        [SerializeField]
        private Renderer m_ColorPickerSelectorBorderCube;

        [SerializeField]
        private Renderer m_ColorPickerSelectorBorderCylinder;

        [SerializeField]
        private GameObject m_CircleBack;

        [SerializeField]
        public GameObject m_Mesh;

        [SerializeField]
        protected Collider m_MeshCollider;

        [SerializeField]
        private ColorController m_ColorController;
        private GameObject m_ActiveInputObject;

        [SerializeField]
        protected Collider m_Collider;

        public ColorController Controller
        {
            get { return m_ColorController; }
        }

        // Start is called before the first frame update
        void Start()
        {
            CustomColorPaletteStorage.m_Instance.Mode = CustomColorPaletteStorage.m_Instance.Mode;
            ColorActive.action.performed += OnColorAdjustPerformed;
            ColorActive.action.canceled += OnColorAdjustCanceled;
        }

        private void OnColorAdjustPerformed(InputAction.CallbackContext context)
        {
            // Debug.Log("true");
            m_active = true;
        }

        private void OnColorAdjustCanceled(InputAction.CallbackContext context)
        {
            // Debug.Log("false");
            m_active = false;
        }

        void Awake()
        {
            //Generate Slider bar
            CustomColorPaletteStorage.m_Instance.ModeChanged += OnModeChanged;
            m_Collider = GetComponentInChildren<Collider>();
            m_ColorController = GetComponent<ColorController>();

            //for (var manager = m_Manager; manager != null; manager = manager.ParentManager)
            //{
            //    ColorController colorController = manager.GetComponent<ColorController>();
            //    if (colorController != null)
            //    {
            //        m_ColorController = colorController;
            //        break;
            //    }
            //}
            m_ColorController.CurrentColorSet += OnCurrentColorSet;
        }

        void OnDestroy()
        {
            CustomColorPaletteStorage.m_Instance.ModeChanged -= OnModeChanged;
            m_ColorController.CurrentColorSet -= OnCurrentColorSet;
        }

        public void SetColor(Color color)
        {
            m_ColorPickerSelector.SetTintColor(color);
            m_ColorPickerSlider.SetTintColor(color);
            if (m_ColorPickerSelectorBorderCube != null)
            {
                m_ColorPickerSelectorBorderCube.material.SetColor("_Color", color);
            }
            if (m_ColorPickerSelectorBorderCylinder != null)
            {
                m_ColorPickerSelectorBorderCylinder.material.SetColor("_Color", color);
            }
        }

        public bool UpdateStateWithInput(bool inputValid, Ray inputRay)
        {
            //Add a condition to control the action here
            //if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider))
            //{
            //    UpdateColorSelectorAndSlider(inputValid, inputRay, parentCollider);
            //    return true;
            //}
            //return false;
            UpdateColorSelectorAndSlider(inputValid, inputRay);
            return true;
        }

        void ResetActiveInputObject()
        {
            if (ColorFinalized != null)
            {
                // If we were picking a color, but now we're not, send a finalize event.
                if (
                    m_ActiveInputObject == m_ColorPickerSlider.gameObject
                    || m_ActiveInputObject == m_ColorPickerSelector.gameObject
                )
                {
                    ColorFinalized();
                }
            }
            m_ActiveInputObject = null;
        }

        void UpdateSelectorSlider(float value)
        {
            Vector3 newValue = m_ColorPickerSelector.RawValue;
            newValue.z = value;
            m_ColorPickerSelector.RawValue = newValue;
        }

        void UpdateSliderPosition()
        {
            m_ColorPickerSlider.RawValue = m_ColorPickerSelector.RawValue.z;
        }

        void UpdateColorSelectorAndSlider(bool inputValid, Ray inputRay)
        {
            // Reset our input object if we're not holding the trigger.
            // !InputManager.m_Instance.GetCommand(InputManager.ActionCommands.Activate)
            if (!m_active)
            {
                ResetActiveInputObject();
            }

            // Color limits if we're tied to the brush color.
            float luminanceMin = 0.0f;
            float saturationMax = 1.0f;
            ColorController brushController = m_ColorController as ColorController;
            if (brushController != null)
            {
                //luminanceMin = brushController.BrushLuminanceMin;
                //saturationMax = brushController.BrushSaturationMax;
            }
            // Debug.Log("m_ActiveInputObject :" + m_ActiveInputObject);
            // Cache mode cause we use it a bunch.
            ColorPickerMode mode = ColorPickerUtils.GetActiveMode(m_ColorController.IsHdr);

            // Check for collision against our color slider first.
            RaycastHit hitInfo;
            if (
                m_ActiveInputObject == null || m_ActiveInputObject == m_ColorPickerSlider.gameObject
            )
            {
                bool validCollision = BasePanel.DoesRayHitCollider(
                    inputRay,
                    m_ColorPickerSlider.GetCollider(),
                    out hitInfo
                );

                // TODO : ColorPickerSlider should be a UIComponent that handles this stuff
                // on its own.
                // If we're not colliding with the slider, but we were before, get our collision with
                // our parent collider.
                //if (!validCollision && m_ActiveInputObject == m_ColorPickerSlider.gameObject)
                //{
                //    //validCollision = BasePanel.DoesRayHitCollider(inputRay, parentCollider, out hitInfo);
                //}

                if (validCollision)
                {
                    // Over slider, check for mouse down.
                    // InputManager.m_Instance.GetCommand(InputManager.ActionCommands.Activate)
                    if (m_active)
                    {
                        float value = ColorPickerUtils.ApplySliderConstraint(
                            mode,
                            m_ColorPickerSlider.GetValueFromHit(hitInfo),
                            luminanceMin,
                            saturationMax
                        );
                        UpdateSelectorSlider(value);
                        UpdateSliderPosition();
                        Color newColor;
                        if (
                            ColorPickerUtils.RawValueToColor(
                                mode,
                                m_ColorPickerSelector.RawValue,
                                out newColor
                            )
                        )
                        {
                            m_ColorController.SetCurrentColorSilently(newColor);
                            currentColor = newColor;
                            DrawingTool.Instance.currentColor = newColor;
                            // GameObject GSModel = ModelManager.Instance.FindActivatedModel();
                            // if (GSModel != null)
                            // {
                            //     PointCloudGenerator pg = GSModel
                            //         .GetComponent<ToolChange>()
                            //         .pointCloudGenerator;
                            //     pg.currentPColor = newColor;
                            //     Debug.Log("current new color: " + pg.currentPColor);
                            // }
                            TriggerColorPicked(newColor);
                        }
                        else
                        {
                            // Indicates some logic fault: the user isn't modifying the color plane,
                            // so why is the color plane's value outside the valid range?
                            Debug.LogErrorFormat(
                                "Unexpected bad RawValue. mode:{0} val:{1}",
                                mode,
                                m_ColorPickerSelector.RawValue
                            );
                        }

                        //SketchSurfacePanel.m_Instance.VerifyValidToolWithColorUpdate();
                        m_ActiveInputObject = m_ColorPickerSlider.gameObject;
                    }
                }
            }

            if (
                m_ActiveInputObject == null
                || m_ActiveInputObject == m_ColorPickerSelector.gameObject
            )
            {
                if (
                    BasePanel.DoesRayHitCollider(
                        inputRay,
                        m_ColorPickerSelector.GetCollider(),
                        out hitInfo
                    )
                )
                {
                    // Over color picker, check for input.
                    //InputManager.m_Instance.GetCommand(InputManager.ActionCommands.Activate)
                    if (m_active)
                    {
                        Vector3 value = ColorPickerUtils.ApplyPlanarConstraint(
                            m_ColorPickerSelector.GetValueFromHit(hitInfo),
                            mode,
                            luminanceMin,
                            saturationMax
                        );
                        Color color;
                        if (ColorPickerUtils.RawValueToColor(mode, value, out color))
                        {
                            m_ColorPickerSelector.RawValue = value;
                            m_ColorController.SetCurrentColorSilently(color);
                            currentColor = color;
                            DrawingTool.Instance.currentColor = color;
                            // GameObject GSModel = ModelManager.Instance.FindActivatedModel();
                            // if (GSModel != null)
                            // {
                            //     PointCloudGenerator pg = GSModel
                            //         .GetComponent<ToolChange>()
                            //         .pointCloudGenerator;
                            //     pg.currentPColor = color;
                            //     Debug.Log("current new color: " + pg.currentPColor);
                            // }
                            TriggerColorPicked(color);
                            m_ColorPickerSlider.OnColorChanged(mode, value);

                            //SketchSurfacePanel.m_Instance.VerifyValidToolWithColorUpdate();
                            m_ActiveInputObject = m_ColorPickerSelector.gameObject;
                        }
                    }
                }
            }
        }

        void TriggerColorPicked(Color color)
        {
            if (ColorPicked != null)
            {
                ColorPicked(color);
            }
        }

        public bool CalculateReticleCollision(Ray castRay, ref Vector3 pos, ref Vector3 forward)
        {
            //see if our cast direction hits the selector
            RaycastHit selectorHitInfo;
            RaycastHit sliderHitInfo;

            bool selectorValid = BasePanel.DoesRayHitCollider(
                castRay,
                m_ColorPickerSelector.GetCollider(),
                out selectorHitInfo
            );
            bool sliderValid = BasePanel.DoesRayHitCollider(
                castRay,
                m_ColorPickerSlider.GetCollider(),
                out sliderHitInfo
            );

            if (selectorValid && sliderValid)
            {
                // Find the one that's closest and disable the other.
                if (
                    (selectorHitInfo.point - castRay.origin).sqrMagnitude
                    < (sliderHitInfo.point - castRay.origin).sqrMagnitude
                )
                {
                    sliderValid = false;
                }
                else
                {
                    selectorValid = false;
                }
            }

            // Custom transforms for colliding with an object.
            if (selectorValid)
            {
                m_ActiveInputObject = m_ColorPickerSelector.gameObject;
                pos = selectorHitInfo.point;
                forward = -m_ColorPickerSelector.transform.forward;
                return true;
            }
            else if (sliderValid)
            {
                m_ActiveInputObject = m_ColorPickerSlider.gameObject;
                pos = sliderHitInfo.point;
                forward = -m_ColorPickerSlider.transform.forward;
                return true;
            }

            return false;
        }

        public void ResetState()
        {
            ResetActiveInputObject();
        }

        void OnModeChanged()
        {
            ColorPickerMode mode = ColorPickerUtils.GetActiveMode(m_ColorController.IsHdr);
            ColorPickerInfo info = ColorPickerUtils.GetInfoForMode(mode);

            if (
                m_ColorPickerSelectorBorderCube != null
                && m_ColorPickerSelectorBorderCylinder != null
            )
            {
                m_ColorPickerSelectorBorderCube.enabled = true;
                m_ColorPickerSelectorBorderCylinder.enabled = true;
                if (info.cylindrical)
                {
                    m_ColorPickerSelectorBorderCylinder.enabled = true;
                }
                else
                {
                    m_ColorPickerSelectorBorderCube.enabled = true;
                }
            }

            if (m_CircleBack != null)
            {
                m_CircleBack.SetActive(info.cylindrical);
            }

            m_ColorPickerSelector.SetLocalMode(mode);
            m_ColorPickerSlider.SetLocalMode(mode);

            m_ColorController.CurrentColor = m_ColorController.CurrentColor;
        }

        void OnCurrentColorSet(ColorPickerMode mode, Vector3 rawColor)
        {
            m_ColorPickerSelector.RawValue = rawColor;
            m_ColorPickerSlider.RawValue = m_ColorPickerSelector.RawValue.z;
            m_ColorPickerSlider.OnColorChanged(mode, rawColor);
        }

        public virtual Collider GetCollider()
        {
            return m_Collider;
        }

        static public bool DoesRayHitCollider(Ray rRay, Collider rCollider, out RaycastHit rHitInfo)
        {
            return rCollider.Raycast(rRay, out rHitInfo, 100.0f);
        }

        virtual public void GetReticleTransformFromPosDir(
            Vector3 vInPos,
            Vector3 vInDir,
            out Vector3 vOutPos,
            out Vector3 vForward
        )
        {
            //by default, the collision point is ok, and the reticle's forward should be the same as the mesh
            vOutPos = vInPos;
            vForward = -transform.forward;

            Vector3 dir = Vector3.forward;
            Ray rCastRay = new Ray(vInPos - vInDir * 0.5f, vInDir);

            RaycastHit rHitInfo;
            CalculateReticleCollision(rCastRay, ref vOutPos, ref vForward);
            //if (DoesRayHitCollider(rCastRay, GetCollider(), out rHitInfo))
            //{
            //    vOutPos = rHitInfo.point;

            //}
            //Debug.Log("vInPos: " + vInPos + "vOutPos: " + vOutPos + "Collider: " + transform.GetComponent<BoxCollider>());
        }
    }
}
