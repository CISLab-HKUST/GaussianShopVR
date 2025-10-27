using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GSShopUI
{
    public class UIComponentManager : MonoBehaviour
    {
        private List<UIComponent> m_UIComponents;
        private GameObject m_ActiveInputObject;
        private UIComponent m_ActiveInputUIComponent;
        //private BasePanel m_PopUpPanel;


        public GameObject ActiveInputObject
        {
            get { return m_ActiveInputObject; }
            set { m_ActiveInputObject = value; }
        }
        public UIComponent ActiveInputUIComponent { get { return m_ActiveInputUIComponent; } }
        public UIComponentManager ParentManager
        {
            get
            {
                return transform.parent ?
                    transform.parent.GetComponentInParent<UIComponentManager>() : null;
            }
        }
        void Awake()
        {
            m_UIComponents = new List<UIComponent>();
        }

        //public BasePanel GetPanelForPopUps()
        //{
        //    // If m_PopUpPanel hasn't been set, recursively walk up and look for a panel.
        //    if (m_PopUpPanel == null)
        //    {
        //        UIComponentManager manager = this;
        //        while (manager != null)
        //        {
        //            m_PopUpPanel = GetPanelFromManager(manager);
        //            if (m_PopUpPanel == null)
        //            {
        //                manager = manager.ParentManager;
        //            }
        //            else
        //            {
        //                break;
        //            }
        //        }
        //    }

        //    if (m_PopUpPanel == null)
        //    {
        //        Debug.LogWarning("UIComponentManager requested panel for popups and nothing was found.");
        //    }
        //    return m_PopUpPanel;
        //}

        //BasePanel GetPanelFromManager(UIComponentManager manager)
        //{
        //    BasePanel panel = manager.GetComponent<BasePanel>();
        //    if (panel == null)
        //    {
        //        PopUpWindow popup = manager.GetComponent<PopUpWindow>();
        //        if (popup != null)
        //        {
        //            return popup.GetParentPanel();
        //        }
        //    }
        //    return panel;
        //}
        public void ResetInput()
        {
            m_ActiveInputObject = null;
            m_ActiveInputUIComponent = null;
        }
        public void RegisterUIComponent(UIComponent comp)
        {
            // Assert this is a unique object.
            Debug.AssertFormat(!m_UIComponents.Contains(comp), "Duplicate in RegisterUIComponent");
            m_UIComponents.Add(comp);
        }
        public void UnregisterUIComponent(UIComponent comp)
        {
            bool wasRemoved = m_UIComponents.Remove(comp);
            Debug.Assert(wasRemoved, "Attempted to unregister a UIComponent that wasn't registered!");
        }

        public void SetColor(Color col)
        {
            for (int i = 0; i < m_UIComponents.Count; ++i)
            {
                m_UIComponents[i].SetColor(col);
            }
        }

        public void UpdateVisuals()
        {
            for (int i = 0; i < m_UIComponents.Count; ++i)
            {
                m_UIComponents[i].UpdateVisuals();
            }
        }

        public void ManagerLostFocus()
        {
            for (int i = 0; i < m_UIComponents.Count; ++i)
            {
                m_UIComponents[i].ManagerLostFocus();
            }
        }

        public void Deactivate()
        {
            for (int i = 0; i < m_UIComponents.Count; ++i)
            {
                m_UIComponents[i].ResetState();
                m_UIComponents[i].ForceDescriptionDeactivate();
            }
        }

        public void GazeRatioChanged(float gazeRatio)
        {
            for (int i = 0; i < m_UIComponents.Count; ++i)
            {
                m_UIComponents[i].GazeRatioChanged(gazeRatio);
            }
        }

        public bool BrushPadAnimatesOnAnyHover()
        {
            for (int i = 0; i < m_UIComponents.Count; ++i)
            {
                if (m_UIComponents[i].BrushPadAnimatesOnHover())
                {
                    return true;
                }
            }
            return false;
        }

        //TODO: the input system is still ongoing

        //public void AssignControllerMaterials(InputManager.ControllerName controller)
        //{
        //    // There's an order of operations problem here.  In practice, I don't think it's an issue
        //    // right now, but this will need to be rethought if multiple UIComponents expect to assign
        //    // controller materials and play nicely.
        //    for (int i = 0; i < m_UIComponents.Count; ++i)
        //    {
        //        m_UIComponents[i].AssignControllerMaterials(controller);
        //    }
        //}

        //public float GetControllerPadShaderRatio(InputManager.ControllerName controller)
        //{
        //    float shaderRatio = 0.0f;
        //    for (int i = 0; i < m_UIComponents.Count; ++i)
        //    {
        //        // I guess we'll just take the max for all UIComponents?
        //        shaderRatio =
        //            Mathf.Max(m_UIComponents[i].GetControllerPadShaderRatio(controller), shaderRatio);
        //    }
        //    return shaderRatio;
        //}
        public bool RaycastAgainstCustomColliders(Ray ray, out RaycastHit hitInfo, float dist)
        {
            hitInfo = new RaycastHit();
            for (int i = 0; i < m_UIComponents.Count; ++i)
            {
                // There's an order of operations issue here.  In practice, UIComponents don't have
                // overlapping colliders, so it shouldn't matter.
                if (m_UIComponents[i].RaycastAgainstCustomCollider(ray, out hitInfo, dist))
                {
                    return true;
                }
            }
            return false;
        }
        public void CalculateReticleCollision(Ray castRay, ref Vector3 pos, ref Vector3 forward)
        {
            for (int i = 0; i < m_UIComponents.Count; ++i)
            {
                // There's an order of operations issue here.  In practice, UIComponents don't have
                // overlapping colliders, so it shouldn't matter.
                if (m_UIComponents[i].CalculateReticleCollision(castRay, ref pos, ref forward))
                {
                    return;
                }
            }
        }
    }

}