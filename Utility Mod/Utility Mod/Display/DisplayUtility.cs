using System.Collections.Generic;
using UnityEngine;

namespace Utility_Mod
{
    public static class DisplayUtility
    {
        private static readonly Dictionary<Color, Texture2D> textureCache = new Dictionary<Color, Texture2D>();

        public static void DrawInfoBox( Rect rect, string text, GUIStyle style )
        {
            GUI.Box( rect, text, style );
        }

        public static void DrawWorldAnchoredBox( Vector3 worldPos, string text, GUIStyle style, Vector2 offset = default( Vector2 ), bool keepInBounds = true )
        {
            if ( Camera.main == null ) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint( worldPos );
            if ( screenPos.z < 0 ) return;

            Vector2 guiPos = new Vector2( screenPos.x + offset.x, Screen.height - screenPos.y + offset.y );

            Vector2 textSize = style.CalcSize( new GUIContent( text ) );
            Rect rect = new Rect( guiPos.x - textSize.x / 2, guiPos.y - textSize.y, textSize.x, textSize.y );

            if ( keepInBounds )
                rect = KeepInScreenBounds( rect );

            DrawInfoBox( rect, text, style );
        }

        public static Vector2 WorldToGUIPosition( Vector3 worldPos )
        {
            if ( Camera.main == null ) return Vector2.zero;
            Vector3 screenPos = Camera.main.WorldToScreenPoint( worldPos );
            return new Vector2( screenPos.x, Screen.height - screenPos.y );
        }

        public static Rect KeepInScreenBounds( Rect rect )
        {
            rect.x = Mathf.Clamp( rect.x, 0, Screen.width - rect.width );
            rect.y = Mathf.Clamp( rect.y, 0, Screen.height - rect.height );
            return rect;
        }

        public static Vector2 GetMouseGUIPosition()
        {
            Vector3 mousePos = Input.mousePosition;
            return new Vector2( mousePos.x, Screen.height - mousePos.y );
        }

        public static GUIStyle CreateInfoBoxStyle( int fontSize = 16, Color? textColor = null )
        {
            GUIStyle style = new GUIStyle( GUI.skin.box );
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = textColor ?? Color.white;
            style.alignment = TextAnchor.MiddleLeft;
            style.wordWrap = false;
            style.padding = new RectOffset( 10, 10, 5, 5 );
            return style;
        }

        public static Texture2D CreateSolidTexture( Color color )
        {
            if ( textureCache.ContainsKey( color ) )
                return textureCache[color];

            Texture2D texture = new Texture2D( 2, 2 );
            Color[] pixels = new Color[4];
            for ( int i = 0; i < pixels.Length; i++ )
                pixels[i] = color;
            texture.SetPixels( pixels );
            texture.Apply();

            textureCache[color] = texture;
            return texture;
        }
    }
}