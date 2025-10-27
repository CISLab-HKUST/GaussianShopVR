using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GSShopUI
{
    public class ColorController : MonoBehaviour
    {
        // Color Controller is an unfortunate name for this class, as it refers to the MVC "Controller"
        // for storing information about the app state regarding the current color.  It also
        // maintains actions for objects to register with for status change notifications.
        [SerializeField] protected Color m_DefaultColor;
        [SerializeField] protected bool m_Hdr;
        protected Color m_CurrentColor = Color.cyan;
        public event Action<ColorPickerMode, Vector3> CurrentColorSet;

        public bool IsHdr { get { return m_Hdr; } }

        virtual public Color CurrentColor
        {
            get { return m_CurrentColor; }
            set
            {
                m_CurrentColor = value;
                var mode = ColorPickerUtils.GetActiveMode(m_Hdr);
                Vector3 raw = ColorPickerUtils.ColorToRawValue(mode, m_CurrentColor);
                TriggerCurrentColorSet(mode, raw);
            }
        }

        virtual public void SetCurrentColorSilently(Color color)
        {
            m_CurrentColor = color;
        }
        protected void TriggerCurrentColorSet(ColorPickerMode mode, Vector3 rawColor)
        {
            if (CurrentColorSet != null)
            {
                CurrentColorSet(mode, rawColor);
            }
        }
        public void SetColorToDefault()
        {
            CurrentColor = m_DefaultColor;
        }
    }
}