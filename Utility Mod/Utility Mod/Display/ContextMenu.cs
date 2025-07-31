using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility_Mod
{
    /// <summary>
    /// Handles rendering and interaction for the context menu
    /// </summary>
    public class ContextMenu
    {
        #region Fields and Properties
        
        private List<MenuItem> items = new List<MenuItem>();
        private Vector2 position;
        private float width = 280f;  // Default width, will be calculated dynamically
        private float itemHeight = 28f;
        private float padding = 5f;
        private int hoveredIndex = -1;
        private GUIStyle menuStyle;
        private GUIStyle itemStyle;
        private GUIStyle itemHoverStyle;
        private GUIStyle separatorStyle;
        private GUIStyle submenuArrowStyle;
        private GUIStyle headerStyle;
        private bool stylesInitialized = false;
        private bool widthCalculated = false;
        private static int windowIdCounter = 1000;  // Window ID counter
        private int windowId;
        
        // Submenu handling
        private ContextMenu activeSubmenu;
        private int submenuParentIndex = -1;
        
        // Checkbox event
        public Action<MenuItem, bool> OnCheckboxToggled;
        
        // Public getter for width (needed for submenu positioning)
        public float Width { get { return width; } }
        
        // Public getter for position (needed for overlap checking)
        public Vector2 Position { get { return position; } }
        
        #endregion
        
        #region Initialization

        public ContextMenu()
        {
            // Don't initialize styles here - they must be created in OnGUI
            windowId = windowIdCounter++;
        }
        
        public void InvalidateStyles()
        {
            stylesInitialized = false;
            widthCalculated = false;
        }

        private void InitializeStyles()
        {
            var settings = Main.settings;
            
            // Menu background style
            menuStyle = new GUIStyle(GUI.skin.box);
            menuStyle.normal.background = CreateTexture(2, 2, new Color(
                settings.menuBackgroundR, 
                settings.menuBackgroundG, 
                settings.menuBackgroundB, 
                settings.menuBackgroundAlpha));
            menuStyle.border = new RectOffset(2, 2, 2, 2);
            menuStyle.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);

            // Normal item style
            itemStyle = new GUIStyle(GUI.skin.label);
            itemStyle.normal.textColor = new Color(
                settings.menuTextR,
                settings.menuTextG,
                settings.menuTextB);
            itemStyle.fontSize = settings.menuFontSize;
            itemStyle.alignment = TextAnchor.MiddleLeft;
            itemStyle.padding = new RectOffset(10, 10, 0, 0);
            itemStyle.wordWrap = false;  // Prevent text wrapping

            // Hover item style
            itemHoverStyle = new GUIStyle(itemStyle);
            itemHoverStyle.normal.background = CreateTexture(2, 2, new Color(
                settings.menuHighlightR,
                settings.menuHighlightG,
                settings.menuHighlightB,
                settings.menuHighlightAlpha));

            // Separator style
            separatorStyle = new GUIStyle();
            separatorStyle.normal.background = CreateTexture(2, 2, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            separatorStyle.fixedHeight = 1f;
            separatorStyle.margin = new RectOffset(5, 5, 5, 5);
            
            // Submenu arrow style
            submenuArrowStyle = new GUIStyle(itemStyle);
            submenuArrowStyle.alignment = TextAnchor.MiddleRight;
            submenuArrowStyle.padding = new RectOffset(0, 10, 0, 0);
            
            // Header style - centered, same size but with slightly muted color
            headerStyle = new GUIStyle(itemStyle);
            headerStyle.alignment = TextAnchor.MiddleCenter;
            headerStyle.fontSize = itemStyle.fontSize;  // Same size as regular items
            headerStyle.fontStyle = FontStyle.Normal;  // Not bold
            headerStyle.normal.textColor = new Color(
                Main.settings.menuTextR * 0.8f,  // Slightly dimmer
                Main.settings.menuTextG * 0.8f, 
                Main.settings.menuTextB * 0.8f);
        }

        private Texture2D CreateTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        
        #endregion
        
        #region Menu Structure Management
        
        public void SetPosition(Vector2 mousePosition)
        {
            // Convert mouse position to GUI coordinates (Y is inverted)
            position = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            
            // Note: Screen bounds adjustment is done in Draw() after width is calculated
        }

        public void AddItem(MenuItem item)
        {
            items.Add(item);
        }

        public void AddSeparator()
        {
            items.Add(MenuItem.CreateSeparator());
        }
        
        public List<MenuItem> GetItems()
        {
            return items;
        }
        
        #endregion
        
        #region Rendering

        public void Draw()
        {
            // Initialize styles on first draw (must be done in OnGUI)
            if (!stylesInitialized)
            {
                InitializeStyles();
                stylesInitialized = true;
            }

            // Calculate dynamic width on first draw (after styles are initialized)
            if (!widthCalculated && stylesInitialized)
            {
                CalculateDynamicWidth();
                widthCalculated = true;
                
                // Adjust position to keep menu on screen after width is calculated
                float adjustHeight = GetMenuHeight();
                
                if (position.x + width > Screen.width)
                    position.x = Screen.width - width - 10;
                
                if (position.y + adjustHeight > Screen.height)
                    position.y = Screen.height - adjustHeight - 10;
            }

            // Calculate menu rect
            float menuHeight = GetMenuHeight();
            Rect menuRect = new Rect(position.x, position.y, width, menuHeight);
            
            // Use GUI.Window for proper layering
            GUI.Window(windowId, menuRect, DrawWindow, "", GUIStyle.none);
            
            // Draw active submenu AFTER parent (higher window ID = on top)
            if (activeSubmenu != null)
            {
                activeSubmenu.Draw();
            }
        }
        
        // Calculate width without drawing (for submenus)
        public void CalculateWidthOnly()
        {
            if (!stylesInitialized)
            {
                InitializeStyles();
                stylesInitialized = true;
            }
            
            if (!widthCalculated)
            {
                CalculateDynamicWidth();
                widthCalculated = true;
            }
        }
        
        private void DrawWindow(int id)
        {
            // Menu rect is now relative to window (0,0)
            float menuHeight = GetMenuHeight();
            Rect localMenuRect = new Rect(0, 0, width, menuHeight);
            
            // Draw menu background
            GUI.Box(localMenuRect, "", menuStyle);

            // Update hovered index based on mouse position
            UpdateHoveredIndex(localMenuRect);

            // Handle submenu
            if (hoveredIndex >= 0 && hoveredIndex < items.Count)
            {
                if (items[hoveredIndex].HasSubMenu)
                {
                    // Hovering over item with submenu
                    if (submenuParentIndex != hoveredIndex)
                    {
                        // Different submenu parent - close old and open new immediately
                        CloseSubmenu();
                        OpenSubmenu(hoveredIndex);
                        submenuParentIndex = hoveredIndex;
                    }
                    // If same parent, keep submenu open
                }
            }

            // Draw items
            float y = padding;
            for (int i = 0; i < items.Count; i++)
            {
                MenuItem item = items[i];
                
                if (item.IsSeparator)
                {
                    // Draw separator
                    Rect separatorRect = new Rect(padding, y + itemHeight / 2, width - padding * 2, 1);
                    GUI.Box(separatorRect, "", separatorStyle);
                    y += itemHeight;
                }
                else
                {
                    // Draw menu item
                    Rect itemRect = new Rect(padding, y, width - padding * 2, itemHeight);
                    bool isHovered = (i == hoveredIndex) && !item.IsHeader;
                    
                    GUIStyle style = isHovered ? itemHoverStyle : itemStyle;
                    
                    // Special handling for multi-button items
                    if (item.MultiButtons != null && item.MultiButtons.Count > 0)
                    {
                        // Use larger height for multi-button items
                        float multiButtonItemHeight = 40f;
                        itemRect.height = multiButtonItemHeight;
                        
                        // Draw hover background
                        if (isHovered)
                        {
                            GUI.Box(itemRect, "", itemHoverStyle);
                        }
                        
                        // Create button style
                        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                        buttonStyle.fontSize = 24;
                        buttonStyle.alignment = TextAnchor.MiddleCenter;
                        buttonStyle.normal.textColor = new Color(
                            Main.settings.menuTextR, 
                            Main.settings.menuTextG, 
                            Main.settings.menuTextB);
                        
                        // Calculate button positions (centered)
                        float buttonWidth = 60f;
                        float buttonSpacing = 8f;
                        float totalWidth = (item.MultiButtons.Count * buttonWidth) + ((item.MultiButtons.Count - 1) * buttonSpacing);
                        float startX = itemRect.x + (itemRect.width - totalWidth) / 2;
                        float buttonY = itemRect.y + 4f;
                        float buttonHeight = multiButtonItemHeight - 8f;
                        
                        // Draw each button
                        for (int btnIndex = 0; btnIndex < item.MultiButtons.Count; btnIndex++)
                        {
                            var button = item.MultiButtons[btnIndex];
                            Rect buttonRect = new Rect(startX + (btnIndex * (buttonWidth + buttonSpacing)), buttonY, buttonWidth, buttonHeight);
                            
                            if (GUI.Button(buttonRect, new GUIContent(button.Text, button.Tooltip), buttonStyle))
                            {
                                button.OnClick?.Invoke();
                            }
                        }
                        
                        y += multiButtonItemHeight;
                        continue;  // Skip normal item rendering
                    }
                    
                    // Draw checkbox if needed
                    if (item.ShowCheckbox)
                    {
                        float checkboxSize = 20f;
                        Rect checkboxRect = new Rect(padding, y + (itemHeight - checkboxSize) / 2, checkboxSize, checkboxSize);
                        
                        bool newChecked = GUI.Toggle(checkboxRect, item.IsChecked, "");
                        if (newChecked != item.IsChecked)
                        {
                            if (item.IsToggleAction)
                            {
                                // Toggle action - call the toggle handler directly
                                item.IsChecked = newChecked;
                                item.OnToggle?.Invoke(newChecked);
                            }
                            else
                            {
                                // Quick action checkbox - handle in menu manager
                                OnCheckboxToggled?.Invoke(item, newChecked);
                            }
                        }
                        
                        // Adjust item rect to make room for checkbox
                        itemRect.x += checkboxSize + 5;
                        itemRect.width -= checkboxSize + 5;
                    }
                    
                    if (item.IsHeader)
                    {
                        // Draw header text without button behavior
                        GUI.Label(itemRect, item.Text, headerStyle);
                    }
                    else if (!item.Enabled)
                    {
                        // Draw disabled item (non-clickable)
                        var disabledStyle = new GUIStyle(itemStyle);
                        disabledStyle.normal.textColor = new Color(
                            itemStyle.normal.textColor.r * 0.5f,
                            itemStyle.normal.textColor.g * 0.5f,
                            itemStyle.normal.textColor.b * 0.5f,
                            0.7f);
                        GUI.Label(itemRect, item.Text, disabledStyle);
                    }
                    else if (GUI.Button(itemRect, item.Text, style))
                    {
                        if (!item.HasSubMenu)
                        {
                            // Check if Alt is held and this is a trackable action
                            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && 
                                item.MenuAction != null && !item.IsToggleAction)
                            {
                                // Set as quick action before executing
                                OnCheckboxToggled?.Invoke(item, true);
                            }
                            
                            // Execute action on click
                            item.OnClick?.Invoke();
                        }
                    }
                    
                    // Draw submenu arrow if item has submenu
                    if (item.HasSubMenu)
                    {
                        GUI.Label(itemRect, "►", submenuArrowStyle);
                    }
                    
                    y += itemHeight;
                }
            }
        }
        
        #endregion
        
        #region Layout Calculations
        
        public float GetMenuHeight()
        {
            float height = padding * 2;
            foreach (var item in items)
            {
                if (item.MultiButtons != null && item.MultiButtons.Count > 0)
                    height += 40f; // Multi-button item height
                else
                    height += itemHeight;
            }
            return height;
        }
        
        private void CalculateDynamicWidth()
        {
            float maxWidth = 0f;
            float checkboxWidth = 30f;  // Width for checkbox + spacing
            float submenuArrowWidth = 30f;  // Width for submenu arrow
            float minWidth = 200f;  // Minimum width for the menu
            float extraPadding = 40f;  // Extra padding for margins and safety
            
            // Calculate the maximum width needed for all items
            foreach (var item in items)
            {
                if (item.IsSeparator)
                    continue;
                    
                // Calculate text width
                GUIContent content = new GUIContent(item.Text);
                Vector2 textSize = itemStyle.CalcSize(content);
                float itemWidth = textSize.x;
                
                // Add checkbox width if needed
                if (item.ShowCheckbox)
                {
                    itemWidth += checkboxWidth;
                }
                
                // Add submenu arrow width if needed
                if (item.HasSubMenu)
                {
                    itemWidth += submenuArrowWidth;
                }
                
                maxWidth = Mathf.Max(maxWidth, itemWidth);
            }
            
            // Set the final width with padding and constraints
            width = Mathf.Max(minWidth, maxWidth + extraPadding + (padding * 2));
            
            // Cap the width to prevent excessively wide menus
            float maxAllowedWidth = Screen.width * 0.4f;  // Max 40% of screen width
            width = Mathf.Min(width, maxAllowedWidth);
        }
        
        #endregion
        
        #region Mouse & Input Handling

        private void UpdateHoveredIndex(Rect localMenuRect)
        {
            Vector2 mousePos = Event.current.mousePosition;
            
            // If submenu is active and mouse is over it, don't hover items in this menu
            if (activeSubmenu != null && activeSubmenu.IsMouseOver())
            {
                hoveredIndex = -1;
                return;
            }
            
            if (!localMenuRect.Contains(mousePos))
            {
                hoveredIndex = -1;
                return;
            }

            // Calculate which item is hovered (accounting for multi-button items)
            float relativeY = mousePos.y - padding;
            float y = 0;
            hoveredIndex = -1;
            
            for (int i = 0; i < items.Count; i++)
            {
                float itemHeightForThis = (items[i].MultiButtons != null && items[i].MultiButtons.Count > 0) ? 40f : itemHeight;
                if (relativeY >= y && relativeY < y + itemHeightForThis && !items[i].IsSeparator && !items[i].IsHeader && items[i].Enabled)
                {
                    hoveredIndex = i;
                    break;
                }
                y += itemHeightForThis;
            }
        }

        public bool IsMouseOver()
        {
            float menuHeight = GetMenuHeight();
            Rect menuRect = new Rect(position.x, position.y, width, menuHeight);
            Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool isOverMenu = menuRect.Contains(mousePos);
            
            // Also check if mouse is over submenu
            if (!isOverMenu && activeSubmenu != null)
            {
                return activeSubmenu.IsMouseOver();
            }
            
            return isOverMenu;
        }
        
        private bool IsMouseOverSubmenu()
        {
            return activeSubmenu != null && activeSubmenu.IsMouseOver();
        }
        
        #endregion
        
        #region Submenu Management
        
        private void OpenSubmenu(int parentIndex)
        {
            if (parentIndex < 0 || parentIndex >= items.Count || !items[parentIndex].HasSubMenu)
                return;
                
                
            // Create new submenu (don't call CloseSubmenu here as it resets submenuParentIndex)
            activeSubmenu = new ContextMenu();
            
            // Copy checkbox event handler
            activeSubmenu.OnCheckboxToggled = this.OnCheckboxToggled;
            
            // Add items from parent's subitems
            foreach (var subItem in items[parentIndex].SubItems)
            {
                if (subItem.IsSeparator)
                    activeSubmenu.AddSeparator();
                else
                    activeSubmenu.AddItem(subItem);
            }
            
            // Calculate submenu position (to the right of parent item)
            float parentY = Position.y + padding;
            // Account for multi-button items before the parent
            for (int i = 0; i < parentIndex; i++)
            {
                if (items[i].MultiButtons != null && items[i].MultiButtons.Count > 0)
                    parentY += 40f;
                else
                    parentY += itemHeight;
            }
            Vector2 submenuPos = new Vector2(Position.x + width - 5, parentY);
            
            // We need to know the submenu width before positioning, so trigger its calculation
            activeSubmenu.SetPosition(new Vector2(submenuPos.x, Screen.height - submenuPos.y));
            
            // Calculate submenu width without drawing
            activeSubmenu.CalculateWidthOnly();
            
            // Get submenu height for vertical bounds checking
            float submenuHeight = activeSubmenu.GetMenuHeight();
            
            // Now adjust position with the correct submenu width
            if (submenuPos.x + activeSubmenu.Width > Screen.width)
            {
                // Position to the left, but ensure it touches the parent menu
                submenuPos.x = Position.x - activeSubmenu.Width + 5;
            }
            
            // Check vertical bounds
            if (submenuPos.y + submenuHeight > Screen.height)
            {
                // Adjust Y position so submenu fits on screen
                submenuPos.y = Screen.height - submenuHeight - 10; // 10px margin from bottom
            }
            
            // Set the final position
            activeSubmenu.SetPosition(new Vector2(submenuPos.x, Screen.height - submenuPos.y));
        }
        
        private void CloseSubmenu()
        {
            activeSubmenu = null;
            submenuParentIndex = -1;
        }
        
        #endregion
    }
}