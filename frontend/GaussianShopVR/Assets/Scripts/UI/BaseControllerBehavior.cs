using UnityEngine.Serialization;
using UnityEngine;
using UnityEngine.UIElements;

namespace GSShopUI
{
    public class BaseControllerBehavior : MonoBehaviour
    {
        public enum GripState
        {
            Standard,
            ReadyToGrip,
            Gripped
        }

        [SerializeField] private InputManager.ControllerName m_ControllerName;
        [SerializeField] private ControllerGeometry m_ControllerGeometryPrefab;
        [FormerlySerializedAs("m_Offset")][SerializeField] private Vector3 m_GeometryOffset;
        [FormerlySerializedAs("m_Rotation")][SerializeField] private Quaternion m_GeometryRotation = Quaternion.identity;


        private Color m_Tint;
        private float m_BaseIntensity;
        private float m_GlowIntensity;

        private GripState m_CurrentGripState;
        //private ControllerGeometry m_ControllerGeometry;
        private ControllerGeometry m_ControllerGeometry;
        public InputManager.ControllerName ControllerName => m_ControllerName;
        //private GameObject TransformVisuals => ControllerGeometry.TransformVisualsRenderer.gameObject;
        //public ControllerGeometry ControllerGeometry
        //{
        //    get
        //    {
        //        if (m_ControllerGeometry == null)
        //        {
        //            InstantiateControllerGeometryFromPrefab(null);
        //        }
        //        return m_ControllerGeometry;
        //    }
        //}

        public ControllerInfo ControllerInfo
        {
            get
            {
                int name = (int)ControllerName;
                var controllers = InputManager.Controllers;
                // This handles the ControllerName.None case, too.
                if (name >= 0 && name < controllers.Length)
                {
                    return controllers[name];
                }
                else
                {
                    return null;
                }
            }
        }
        public void InstantiateControllerGeometryFromPrefab(ControllerGeometry prefab)
        {
            bool changedController = false;
            if (m_ControllerGeometry != null)
            {
                Destroy(m_ControllerGeometry.gameObject);
                changedController = true;
            }

            if (prefab == null)
            {
                prefab = m_ControllerGeometryPrefab;
            }
            SetGeometry(Instantiate(prefab));

            if (changedController)
            {
                //InputManager.ControllersHaveChanged();
            }
        }
        private void SetGeometry(ControllerGeometry geom)
        {
            m_ControllerGeometry = geom;

            // The back-pointers is implicit; it's geometry.transform.parent.
            // worldPositionStays: false because we're about to overwrite it anyway

            //m_ControllerGeometry.transform.SetParent(this.transform, worldPositionStays: false);
            //Quaternion rot = m_GeometryRotation.IsInitialized() ? m_GeometryRotation : Quaternion.identity;
            //Coords.AsLocal[m_ControllerGeometry.transform] = TrTransform.TRS(m_GeometryOffset, rot, 1);
            //m_ControllerGeometry.OnBehaviorChanged();
        }
        public ControllerGeometry ControllerGeometry
        {
            get
            {
                if (m_ControllerGeometry == null)
                {
                    InstantiateControllerGeometryFromPrefab(null);
                }
                return m_ControllerGeometry;
            }
        }
        virtual protected void OnUpdate() { }
        virtual public void ActivateHint(bool bActivate) { }
    }
}