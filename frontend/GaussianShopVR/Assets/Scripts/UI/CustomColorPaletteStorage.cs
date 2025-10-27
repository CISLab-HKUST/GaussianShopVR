using UnityEngine;
using System;

namespace GSShopUI
{
    public class CustomColorPaletteStorage : MonoBehaviour
    {
        static public CustomColorPaletteStorage m_Instance;

        public struct StoredColor
        {
            public Color color;
            public bool valid;
        }

        [SerializeField]
        private ModeAndPickerInfo[] m_ModeToPickerInfo;

        [SerializeField]
        private int m_NumColors = 7;

        private StoredColor[] m_StoredColors;
        private ColorPickerMode m_Mode;

        public event Action StoredColorsChanged;

        public event Action ModeChanged;
        public ModeAndPickerInfo[] ModeToPickerInfo
        {
            get { return m_ModeToPickerInfo; }
        }
        public ColorPickerMode Mode
        {
            get { return m_Mode; }
            set
            {
                var info = ColorPickerUtils.GetInfoForMode(value);
                if (info != null && !info.hdr)
                {
                    m_Mode = value;
                    PlayerPrefs.SetInt("ColorMode", (int)m_Mode);
                    if (ModeChanged != null)
                    {
                        ModeChanged();
                    }
                }
            }
        }

        void Awake()
        {
            m_Instance = this;
            m_StoredColors = new StoredColor[m_NumColors];
            m_Mode = ColorPickerMode.HS_L_Polar;
        }

        void Start()
        {
            if (PlayerPrefs.HasKey("ColorMode"))
            {
                int mode = PlayerPrefs.GetInt("ColorMode");
                if (mode < (int)ColorPickerMode.NUM_MODES)
                {
                    Mode = (ColorPickerMode)mode;
                }
            }
        }

        public int GetNumValidColors()
        {
            for (int i = 0; i < m_StoredColors.Length; ++i)
            {
                if (!m_StoredColors[i].valid)
                {
                    return i;
                }
            }
            return m_StoredColors.Length;
        }

        public Color GetColor(int index)
        {
            Debug.Assert(index >= 0 && index < m_StoredColors.Length);
            return m_StoredColors[index].color;
        }

        // Update is called once per frame
        void Update() { }
    }
}
