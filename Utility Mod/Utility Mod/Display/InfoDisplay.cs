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

        void OnGUI()
        {
            if ( !enabled || ( autoHide && !ShouldDisplay() ) )
                return;

            InitializeStyle();

            string text = GetDisplayText();
            if ( string.IsNullOrEmpty( text ) )
                return;

            if ( displayMode == DisplayMode.WorldAnchored )
            {
                // Check if object is on screen
                if ( hideWhenOffScreen && Camera.main != null )
                {
                    Vector3 screenPos = Camera.main.WorldToScreenPoint( transform.position );
                    // Check if behind camera or outside screen bounds
                    if ( screenPos.z < 0 ||
                        screenPos.x < 0 || screenPos.x > Screen.width ||
                        screenPos.y < 0 || screenPos.y > Screen.height )
                    {
                        return;
                    }
                }

                DisplayUtility.DrawWorldAnchoredBox( transform.position, text, displayStyle, screenOffset, keepInBounds );
            }
            else
            {
                Vector2 position = GetDisplayPosition();
                Vector2 size = displayStyle.CalcSize( new GUIContent( text ) );
                Rect rect = new Rect( position.x, position.y, size.x, size.y );

                if ( keepInBounds )
                    rect = DisplayUtility.KeepInScreenBounds( rect );

                DisplayUtility.DrawInfoBox( rect, text, displayStyle );
            }
        }
    }
}