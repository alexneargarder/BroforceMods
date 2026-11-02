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
            fontSize = 20;
            autoHide = true;
            keepInBounds = false;
            hideWhenOffScreen = false;

            dummy = GetComponent<TestDummy>();
        }

        protected override string GetDisplayText()
        {
            if ( dummy == null )
                return "";

            return string.Format(
                "HP: {0}/{1}\n" +
                "Current DPS: {2:F0}\n" +
                "Avg DPS:     {3:F0}\n" +
                "Total:       {4}\n" +
                "Hits:        {5}\n" +
                "Last Hit:    {6}\n" +
                "Avg/Min/Max: {7:F0} / {8} / {9}\n" +
                "Time:        {10:F1}s",
                dummy.health,
                dummy.maxHealth,
                dummy.currentDPS,
                dummy.averageDPS,
                dummy.totalDamage,
                dummy.hitCount,
                dummy.lastHitDamage,
                dummy.hitCount > 0 ? (float)dummy.totalDamage / dummy.hitCount : 0f,
                dummy.minHitDamage,
                dummy.maxHitDamage,
                dummy.engagementTime
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
                displayStyle.alignment = TextAnchor.MiddleLeft;
            }

            InitializeButtonStyles();
        }

        private void InitializeButtonStyles()
        {
            if ( buttonStyle == null )
            {
                buttonStyle = new GUIStyle( GUI.skin.button );
                buttonStyle.fontSize = 16;
                buttonStyle.fontStyle = FontStyle.Bold;
                buttonStyle.normal.textColor = Color.white;
                buttonStyle.alignment = TextAnchor.MiddleCenter;
                buttonStyle.padding = new RectOffset( 8, 8, 4, 4 );
            }

            if ( hoverButtonStyle == null )
            {
                hoverButtonStyle = new GUIStyle( buttonStyle );
                hoverButtonStyle.normal.textColor = Color.yellow;
            }
        }

        protected override void OnGUI()
        {
            if ( !enabled || ( autoHide && !ShouldDisplay() ) || dummy == null )
                return;

            InitializeStyle();

            string text = GetDisplayText();
            if ( string.IsNullOrEmpty( text ) )
                return;

            if ( Camera.main == null )
                return;

            Vector3 worldAnchor = dummy.mode == TestDummyMode.Free ? dummy.transform.position : dummy.spawnPosition;
            Vector3 screenPos = Camera.main.WorldToScreenPoint( worldAnchor );
            if ( screenPos.z < 0 )
                return;

            Vector2 guiPos = new Vector2( screenPos.x + screenOffset.x, Screen.height - screenPos.y + screenOffset.y );
            Vector2 textSize = displayStyle.CalcSize( new GUIContent( text ) );
            lastDisplayRect = new Rect( guiPos.x - textSize.x / 2, guiPos.y - textSize.y, textSize.x, textSize.y );

            DisplayUtility.DrawInfoBox( lastDisplayRect, text, displayStyle );

            float buttonHeight = 30f;
            float spacing = 6f;
            float modeButtonWidth = 60f;
            float resetButtonWidth = 70f;
            float totalWidth = modeButtonWidth + spacing + resetButtonWidth;
            float rowY = lastDisplayRect.yMax + spacing;
            float rowX = lastDisplayRect.x + ( lastDisplayRect.width - totalWidth ) / 2f;

            Rect modeRect = new Rect( rowX, rowY, modeButtonWidth, buttonHeight );
            Rect resetRect = new Rect( rowX + modeButtonWidth + spacing, rowY, resetButtonWidth, buttonHeight );

            GUIStyle modeStyle = modeRect.Contains( Event.current.mousePosition ) ? hoverButtonStyle : buttonStyle;
            GUIStyle resetStyle = resetRect.Contains( Event.current.mousePosition ) ? hoverButtonStyle : buttonStyle;

            string modeLabel = "[" + dummy.GetModeShortLabel() + "]";
            if ( GUI.Button( modeRect, modeLabel, modeStyle ) )
            {
                dummy.CycleMode();
            }

            if ( GUI.Button( resetRect, "Reset", resetStyle ) )
            {
                dummy.ResetStats();
                dummy.RestoreState();
            }
        }
    }
}
