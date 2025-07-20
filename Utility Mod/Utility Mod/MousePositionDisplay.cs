using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility_Mod
{
    public class MousePositionDisplay : MonoBehaviour
    {
        private GUIStyle mousePositionStyle;
        
        void OnGUI()
        {
            if (!Main.settings.showMousePosition || Camera.main == null)
                return;

            // Initialize style if needed
            if (mousePositionStyle == null)
            {
                mousePositionStyle = new GUIStyle(GUI.skin.box);
                mousePositionStyle.fontSize = 18;
                mousePositionStyle.fontStyle = FontStyle.Bold;
                mousePositionStyle.normal.textColor = Color.white;
                mousePositionStyle.alignment = TextAnchor.MiddleLeft;
                mousePositionStyle.wordWrap = false;
                mousePositionStyle.padding = new RectOffset(10, 10, 5, 5);
            }

            // Get mouse position in world coordinates
            Vector3 mouseScreenPos = Input.mousePosition;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            
            // Build display text - easily extensible
            List<string> displayLines = new List<string>();
            
            // Add world position
            displayLines.Add(string.Format("X: {0:F2}, Y: {1:F2}", worldPos.x, worldPos.y));
            
            // Future additions can go here, for example:
            // if (Main.settings.showGridPosition)
            //     displayLines.Add(string.Format("Grid: {0}, {1}", (int)(worldPos.x / 16), (int)(worldPos.y / 16)));
            // if (Main.settings.showScreenPosition)
            //     displayLines.Add(string.Format("Screen: {0}, {1}", mouseScreenPos.x, mouseScreenPos.y));
            
            // Combine all lines
            string displayText = string.Join("\n", displayLines.ToArray());
            
            // Calculate size based on content
            Vector2 textSize = mousePositionStyle.CalcSize(new GUIContent(displayText));
            float width = Mathf.Max(200f, textSize.x + 20f);
            float height = textSize.y + 10f;
            
            // Positioning
            float offsetX = 20f;
            float offsetY = 20f;
            
            // Calculate position
            float x = mouseScreenPos.x + offsetX;
            float y = Screen.height - mouseScreenPos.y + offsetY; // GUI coordinates have Y inverted
            
            // Keep text within screen bounds
            if (x + width > Screen.width)
                x = mouseScreenPos.x - width - offsetX;
            if (y + height > Screen.height)
                y = Screen.height - mouseScreenPos.y - height - offsetY;
            
            // Draw using styled box
            GUI.Box(new Rect(x, y, width, height), displayText, mousePositionStyle);
        }
    }
}