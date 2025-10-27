using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace GSShopUI
{
    public class TextMeasureScript : MonoBehaviour
    {
        static public TextMeasureScript m_Instance;
        private TextMeshPro m_TextMesh;

        //dictionary key-- override IEquatable to cut down on GC and increase speed
        private struct TextParams : IEquatable<TextParams>
        {
            public float m_FontSize;
            public TMP_FontAsset m_Font;
            public string m_Text;

            public bool Equals(TextParams other)
            {
                return (m_FontSize == other.m_FontSize) &&
                    m_Text.Equals(other.m_Text) && m_Font.name.Equals(other.m_Font.name);
            }
            public override bool Equals(object other)
            {
                if (!(other is TextParams))
                {
                    return false;
                }
                return Equals((TextParams)other);
            }
            public override int GetHashCode()
            {
                return m_FontSize.GetHashCode() ^ m_Text.GetHashCode();
            }
            public static bool operator ==(TextParams a, TextParams b)
            {
                return a.m_FontSize == b.m_FontSize &&
                    a.m_Text.Equals(b.m_Text) && a.m_Font.name.Equals(b.m_Font.name);
            }
            public static bool operator !=(TextParams a, TextParams b)
            {
                return a.m_FontSize == b.m_FontSize &&
                    a.m_Text.Equals(b.m_Text) && a.m_Font.name.Equals(b.m_Font.name);
            }
        }
        private Dictionary<TextParams, Vector2> m_StringSizeMap;

        void Awake()
        {
            m_Instance = this;
            m_TextMesh = GetComponent<TextMeshPro>();
            m_StringSizeMap = new Dictionary<TextParams, Vector2>();
        }

        static public float GetTextWidth(TextMeshPro text)
        {
            return m_Instance.GetTextWidth(text.fontSize, text.font, text.text);
        }

        static public float GetTextHeight(TextMeshPro text)
        {
            return m_Instance.GetTextHeight(text.fontSize, text.font, text.text);
        }

        public float GetTextWidth(float fFontSize, TMP_FontAsset rFont, string sText)
        {
            //look for this string in the dictionary first
            TextParams rParams = new TextParams
            {
                m_FontSize = fFontSize,
                m_Font = rFont,
                m_Text = sText
            };

            if (m_StringSizeMap.ContainsKey(rParams))
            {
                return m_StringSizeMap[rParams].x;
            }

            //add new string to our map
            Vector2 vSize = AddNewString(rParams, fFontSize, rFont, sText);
            return vSize.x;
        }

        public float GetTextHeight(float fFontSize, TMP_FontAsset rFont, string sText)
        {
            //look for this string in the dictionary first
            TextParams rParams = new TextParams
            {
                m_FontSize = fFontSize,
                m_Font = rFont,
                m_Text = sText
            };

            if (m_StringSizeMap.ContainsKey(rParams))
            {
                return m_StringSizeMap[rParams].y;
            }

            //add new string to our map
            Vector2 vSize = AddNewString(rParams, fFontSize, rFont, sText);
            return vSize.y;
        }

        Vector2 AddNewString(TextParams rParams, float fFontSize, TMP_FontAsset rFont, string sText)
        {
            m_TextMesh.fontSize = fFontSize;
            m_TextMesh.font = rFont;
            m_TextMesh.text = sText;
            m_TextMesh.ForceMeshUpdate(true, true);
            Vector2 vSize = new Vector2(m_TextMesh.preferredWidth, m_TextMesh.preferredHeight);
            m_StringSizeMap.Add(rParams, vSize);
            return vSize;
        }
    }
}