using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
namespace GSShopUI
{
    public enum DescriptionType
    {
        None = -1,
        Button = 0,
        Slider = 1,
        PreviewCube = 2,
        VerticalSlider = 3,
    }
    public class UIComponent : MonoBehaviour
    {
        public const float kUnavailableTintAmount = 0.4f;
        protected enum DescriptionState
        {
            Deactivated,
            Activating,
            Active,
            Deactivating,
            Unavailable
        }

        public enum ComponentMessage
        {
            NextPage,
            PrevPage,
            GotoPage,
            ClosePopup
        }

        [SerializeField] private DescriptionType m_DescriptionType = DescriptionType.Button;
        [SerializeField] protected float m_DescriptionYOffset;
        [SerializeField] protected string m_DescriptionText;
        [SerializeField] protected LocalizedString m_LocalizedDescription;
        [SerializeField] protected string m_DescriptionTextExtra;
        [SerializeField] protected LocalizedString m_LocalizedDescriptionExtra;
        [SerializeField] protected float m_DescriptionActivateSpeed = 12.0f;
        [SerializeField] protected float m_DescriptionZScale = 1.0f;

        protected Collider m_Collider;
        protected UIComponentManager m_Manager;

        protected GameObject m_Description;
        private UIComponentDescription m_DescriptionScript;
        protected float m_DescriptionActivateTimer;
        protected DescriptionState m_DescriptionState;

        protected bool m_HoldFocus = true;

        protected bool m_StealFocus;
        protected bool m_HadFocus;
        protected bool m_HadButtonPress;

        public virtual Collider GetCollider() { return m_Collider; }
        public string Description
        {
            get
            {
                try
                {
                    var locString = m_LocalizedDescription.GetLocalizedStringAsync().Result;
                    return locString;
                }
                catch
                {
                    return m_DescriptionText;
                }
            }
        }
        public string DescriptionExtra
        {
            get
            {
                try
                {
                    var locString = m_LocalizedDescriptionExtra.GetLocalizedStringAsync().Result;
                    return locString;
                }
                catch
                {
                    return m_DescriptionTextExtra;
                }
            }
        }
        public bool IsDescriptionActive()
        {
            return m_DescriptionState != DescriptionState.Deactivated;
        }

        public void SetDescriptionUnavailable(bool unavailable)
        {
            if (unavailable)
            {
                ForceDescriptionDeactivate();
                m_DescriptionState = DescriptionState.Unavailable;
            }
            else
            {
                m_DescriptionState = DescriptionState.Deactivated;
            }
        }


        public void AdjustDescriptionScale()
        {
            if (m_DescriptionScript)
            {
                m_DescriptionScript.AdjustDescriptionScale();
            }
        }

        protected void SetDescriptionVisualsAvailable(bool available)
        {
            if (m_DescriptionScript)
            {
                m_DescriptionScript.SetAvailabilityVisuals(available);
            }
        }
        virtual protected void Start() { }

        virtual protected void OnDisable() { }
        public void RegisterComponent()
        {
            // If we're already registered, clean that up.
            UnregisterComponent();

            // Register with our manager.
            if (transform.parent != null)
            {
                m_Manager = transform.parent.GetComponentInParent<UIComponentManager>();
                if (m_Manager != null)
                {
                    m_Manager.RegisterUIComponent(this);
                }
            }

            OnRegisterComponent();
        }
        void UnregisterComponent()
        {
            if (m_Manager != null)
            {
                m_Manager.UnregisterUIComponent(this);
            }
        }
        virtual protected void OnRegisterComponent()
        {
        }
        virtual protected void OnDestroy()
        {
            //LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            UnregisterComponent();
        }

        public void SetDescriptionActive(bool active)
        {
            if (m_DescriptionState == DescriptionState.Unavailable)
            {
                return;
            }
            if (!active)
            {
                if (m_DescriptionState == DescriptionState.Activating ||
                    m_DescriptionState == DescriptionState.Active)
                {
                    m_DescriptionState = DescriptionState.Deactivating;
                }
            }
            else
            {
                if (m_DescriptionState == DescriptionState.Deactivating ||
                    m_DescriptionState == DescriptionState.Deactivated)
                {
                    m_DescriptionState = DescriptionState.Activating;
                    if (m_Description)
                    {
                        m_Description.SetActive(true);
                        OnDescriptionActivated();
                    }
                }
            }
        }

        virtual protected void OnDescriptionActivated()
        {
        }

        virtual public void SetColor(Color rColor) { }
        virtual public void GazeRatioChanged(float gazeRatio) { }
        virtual protected void OnDescriptionDeactivated()
        {
        }
        virtual public bool BrushPadAnimatesOnHover()
        {
            return false;
        }
        virtual public bool RaycastAgainstCustomCollider(Ray ray, out RaycastHit hitInfo, float dist)
        {
            hitInfo = new RaycastHit();
            return false;
        }
        virtual public void ResetState()
        {
            m_HadButtonPress = false;
            m_HadFocus = false;
        }
        virtual public void ManagerLostFocus() { }
        virtual public void ForceDescriptionDeactivate()
        {
            if (m_DescriptionState == DescriptionState.Unavailable)
            {
                return;
            }
            m_DescriptionActivateTimer = 0.0f;
            m_DescriptionState = DescriptionState.Deactivated;
            if (m_Description)
            {
                m_Description.SetActive(false);
                OnDescriptionDeactivated();
            }
        }
        public void SetDescriptionText(params string[] strings)
        {
            strings = strings.TakeWhile(s => !string.IsNullOrEmpty(s)).ToArray();

            m_DescriptionText = strings.Length > 0 ? strings[0] : null;
            m_DescriptionTextExtra = strings.Length > 1 ? strings[1] : null;
            int numberOfLines = Math.Max(strings.Length, 1);

            if (m_Description == null ||
                m_DescriptionScript && numberOfLines != m_DescriptionScript.NumberOfLines)
            {
                // Create a new description prefab.
                bool wasActive = m_Description == null ? false : m_Description.activeSelf;
                Destroy(m_Description);
                //m_Description = App.Config.CreateDescriptionFor(m_DescriptionType, numberOfLines);
                if (m_Description != null)
                {
                    // TODO
                    // m_DescriptionScript needs to be set for OnDescriptionChanged to work properly.
                    m_DescriptionScript = m_Description.GetComponent<UIComponentDescription>();
                    OnDescriptionChanged();
                    if (m_DescriptionScript)
                    {
                        m_DescriptionScript.YOffset = m_DescriptionYOffset;
                    }
                    m_Description.SetActive(wasActive);
                }
            }

            if (m_DescriptionScript)
            {
                m_DescriptionScript.SetDescription(strings);
            }
        }
        virtual protected void OnDescriptionChanged()
        {
            m_Description.transform.position = transform.position;
            m_Description.transform.rotation = transform.rotation;
            m_Description.transform.parent = transform;
            m_DescriptionScript.AdjustDescriptionScale();
        }
        virtual public bool UpdateStateWithInput(bool inputValid, Ray inputRay,
                                                 GameObject parentActiveObject, Collider parentCollider)
        {
            if (parentActiveObject == null || parentActiveObject == gameObject || m_StealFocus)
            {
                RaycastHit rHitInfo;
                if (BasePanel.DoesRayHitCollider(inputRay, GetCollider(), out rHitInfo))
                {
                    if (!m_HadFocus)
                    {
                        GainFocus();
                        m_HadFocus = true;
                    }
                    else
                    {
                        HasFocus(rHitInfo);
                    }

                    if (inputValid)
                    {
                        if (!m_HadButtonPress)
                        {
                            ButtonPressed(rHitInfo);
                            m_HadButtonPress = true;
                        }
                        else
                        {
                            ButtonHeld(rHitInfo);
                        }
                    }
                    else
                    {
                        if (m_HadButtonPress)
                        {
                            ButtonReleased();
                            m_HadButtonPress = false;
                        }
                    }
                    return true;
                }
                else
                {
                    if (m_HadButtonPress)
                    {
                        if (inputValid && m_HoldFocus)
                        {
                            if (BasePanel.DoesRayHitCollider(inputRay, parentCollider, out rHitInfo))
                            {
                                ButtonHeld(rHitInfo);
                                return true;
                            }
                        }
                        else
                        {
                            if (m_HoldFocus)
                            {
                                ButtonReleased();
                            }
                            m_HadButtonPress = false;
                        }
                    }
                    else if (m_HadFocus)
                    {
                        LostFocus();
                        m_HadFocus = false;
                    }
                }
            }
            return false;
        }
        virtual public void ButtonPressed(RaycastHit rHitInfo) { }
        virtual public void ButtonHeld(RaycastHit rHitInfo) { }
        virtual public void ButtonReleased(){ }
        virtual public void GainFocus() { }
        virtual public void HasFocus(RaycastHit rHitInfo) { }
        virtual public void LostFocus() { }

        virtual public void UpdateVisuals()
        {
            if (m_Description != null)
            {
                float fStep = m_DescriptionActivateSpeed * Time.deltaTime;
                if (m_DescriptionState == DescriptionState.Activating)
                {
                    m_DescriptionActivateTimer = Mathf.Min(m_DescriptionActivateTimer + fStep, 1.0f);

                    Vector3 vScale = m_Description.transform.localScale;
                    vScale.z = m_DescriptionActivateTimer * m_DescriptionZScale;
                    m_Description.transform.localScale = vScale;
                    AdjustDescriptionScale();

                    if (m_DescriptionActivateTimer >= 1.0f)
                    {
                        m_DescriptionState = DescriptionState.Active;
                    }
                }
                else if (m_DescriptionState == DescriptionState.Deactivating)
                {
                    m_DescriptionActivateTimer = Mathf.Max(m_DescriptionActivateTimer - fStep, 0.0f);

                    Vector3 vScale = m_Description.transform.localScale;
                    vScale.z = m_DescriptionActivateTimer * m_DescriptionZScale;
                    m_Description.transform.localScale = vScale;
                    AdjustDescriptionScale();

                    if (m_DescriptionActivateTimer <= 0.0f)
                    {
                        m_DescriptionState = DescriptionState.Deactivated;
                        m_Description.SetActive(false);
                        OnDescriptionDeactivated();
                    }
                }
            }
        }

        virtual public bool CalculateReticleCollision(Ray ray, ref Vector3 pos, ref Vector3 forward)
        {
            RaycastHit hitInfo;
            if (BasePanel.DoesRayHitCollider(ray, GetCollider(), out hitInfo))
            {
                pos = hitInfo.point;
                return true;
            }
            return false;
        }
        virtual public void ReceiveMessage(ComponentMessage type, int param) { }
        virtual public void AssignControllerMaterials(InputManager.ControllerName controller) { }
        virtual public float GetControllerPadShaderRatio(InputManager.ControllerName controller)
        {
            return 0.0f;
        }
    }
}