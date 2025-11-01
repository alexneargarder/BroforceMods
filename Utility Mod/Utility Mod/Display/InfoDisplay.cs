using UnityEngine;

namespace Utility_Mod
{
    public abstract class InfoDisplay : MonoBehaviour
    {
        public enum DisplayMode
        {
            FollowCursor,
            WorldAnchored,
            ScreenFixed
        }

        public DisplayMode displayMode = DisplayMode.WorldAnchored;
        public Vector2 screenOffset = new Vector2( 0, -50 );
        public Vector2 fixedScreenPosition = new Vector2( 100, 100 );
        public bool autoHide = false;
        public bool keepInBounds = true;
        public bool hideWhenOffScreen = false;

        protected GUIStyle displayStyle;
        protected int fontSize = 16;
        protected Color textColor = Color.white;
        protected Rect lastDisplayRect;

        protected abstract string GetDisplayText();
        protected abstract bool ShouldDisplay();

        protected virtual Vector2 GetDisplayPosition()
        {
            switch ( displayMode )
            {
                case DisplayMode.FollowCursor:
                    return DisplayUtility.GetMouseGUIPosition() + screenOffset;

                case DisplayMode.WorldAnchored:
                    return DisplayUtility.WorldToGUIPosition( transform.position ) + screenOffset;

                case DisplayMode.ScreenFixed:
                    return fixedScreenPosition;

                default:
                    return Vector2.zero;
            }
        }

        protected virtual void InitializeStyle()
        {
            if ( displayStyle == null )
            {
                displayStyle = DisplayUtility.CreateInfoBoxStyle( fontSize, textColor );
            }
        }

        protected virtual void OnGUI()
        {
            if ( !enabled || ( autoHide && !ShouldDisplay() ) )
                return;

            InitializeStyle();

            string text = GetDisplayText();
            if ( string.IsNullOrEmpty( text ) )
                return;

            if ( displayMode == DisplayMode.WorldAnchored )
            {
                if ( Camera.main == null )
                    return;

                Vector3 screenPos = Camera.main.WorldToScreenPoint( transform.position );

                if ( hideWhenOffScreen )
                {
                    if ( screenPos.z < 0 ||
                        screenPos.x < 0 || screenPos.x > Screen.width ||
                        screenPos.y < 0 || screenPos.y > Screen.height )
                    {
                        return;
                    }
                }

                if ( screenPos.z < 0 )
                    return;

                Vector2 guiPos = new Vector2( screenPos.x + screenOffset.x, Screen.height - screenPos.y + screenOffset.y );
                Vector2 textSize = displayStyle.CalcSize( new GUIContent( text ) );
                lastDisplayRect = new Rect( guiPos.x - textSize.x / 2, guiPos.y - textSize.y, textSize.x, textSize.y );

                if ( keepInBounds )
                    lastDisplayRect = DisplayUtility.KeepInScreenBounds( lastDisplayRect );

                DisplayUtility.DrawInfoBox( lastDisplayRect, text, displayStyle );
            }
            else
            {
                Vector2 position = GetDisplayPosition();
                Vector2 size = displayStyle.CalcSize( new GUIContent( text ) );
                lastDisplayRect = new Rect( position.x, position.y, size.x, size.y );

                if ( keepInBounds )
                    lastDisplayRect = DisplayUtility.KeepInScreenBounds( lastDisplayRect );

                DisplayUtility.DrawInfoBox( lastDisplayRect, text, displayStyle );
            }
        }
    }
}