using UnityEngine;

namespace Utility_Mod
{
    public class TestDummyDisplay : InfoDisplay
    {
        private TestDummy dummy;
        private GUIStyle buttonStyle;
        private GUIStyle hoverButtonStyle;

        void Awake()
        {
            displayMode = DisplayMode.WorldAnchored;
            screenOffset = new Vector2( 0, -200 );
            fontSize = 24;
            autoHide = true;  // Changed to true so ShouldDisplay() is checked
            keepInBounds = false;  // Don't force to stay in bounds
            hideWhenOffScreen = false;  // Let it naturally go off screen

            dummy = GetComponent<TestDummy>();
        }

        protected override string GetDisplayText()
        {
            if ( dummy == null )
                return "";

            return string.Format(
                "DPS: {0:F1}\n" +
                "Total: {1}\n" +
                "Hits: {2}\n" +
                "HP: {3}/{4}",
                dummy.currentDPS,
                dummy.totalDamage,
                dummy.hitCount,
                dummy.health,
                dummy.maxHealth
            );
        }

        protected override bool ShouldDisplay()
        {
            return dummy != null && dummy.showDPSOverlay;
        }

        protected override void InitializeStyle()
        {
            base.InitializeStyle();

            if ( dummy != null )
            {
                if ( dummy.currentDPS > 200 )
                    textColor = Color.red;
                else if ( dummy.currentDPS > 100 )
                    textColor = Color.yellow;
                else
                    textColor = Color.white;

                displayStyle.normal.textColor = textColor;
            }

            InitializeButtonStyles();
        }

        private void InitializeButtonStyles()
        {
            if ( buttonStyle == null )
            {
                buttonStyle = new GUIStyle( GUI.skin.button );
                buttonStyle.fontSize = 18;
                buttonStyle.fontStyle = FontStyle.Bold;
                buttonStyle.normal.textColor = Color.white;
                buttonStyle.alignment = TextAnchor.MiddleCenter;
                buttonStyle.padding = new RectOffset( 10, 10, 5, 5 );
            }

            if ( hoverButtonStyle == null )
            {
                hoverButtonStyle = new GUIStyle( buttonStyle );
                hoverButtonStyle.normal.textColor = Color.yellow;
            }
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if ( !enabled || ( autoHide && !ShouldDisplay() ) || dummy == null )
                return;

            float buttonWidth = 100f;
            float buttonHeight = 35f;
            float buttonSpacing = 10f;
            Rect buttonRect = new Rect(
                lastDisplayRect.x + ( lastDisplayRect.width - buttonWidth ) / 2,
                lastDisplayRect.yMax + buttonSpacing,
                buttonWidth,
                buttonHeight
            );

            bool isHovered = buttonRect.Contains( Event.current.mousePosition );
            GUIStyle currentButtonStyle = isHovered ? hoverButtonStyle : buttonStyle;

            if ( GUI.Button( buttonRect, "Reset", currentButtonStyle ) )
            {
                dummy.ResetStats();
            }
        }
    }
}