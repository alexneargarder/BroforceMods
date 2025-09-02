using System.Collections.Generic;
using UnityEngine;

namespace Utility_Mod
{
    public class MousePositionDisplay : InfoDisplay
    {
        void Awake()
        {
            displayMode = DisplayMode.FollowCursor;
            screenOffset = new Vector2( 20, 20 );
            fontSize = 18;
            autoHide = true;
        }

        protected override string GetDisplayText()
        {
            if ( Camera.main == null )
                return "";

            Vector3 worldPos = Camera.main.ScreenToWorldPoint( Input.mousePosition );

            List<string> displayLines = new List<string>();
            displayLines.Add( string.Format( "X: {0:F2}, Y: {1:F2}", worldPos.x, worldPos.y ) );

            return string.Join( "\n", displayLines.ToArray() );
        }

        protected override bool ShouldDisplay()
        {
            return Main.settings.showMousePosition;
        }
    }
}