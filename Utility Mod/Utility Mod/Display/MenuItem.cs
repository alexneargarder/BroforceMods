using System;
using System.Collections.Generic;

namespace Utility_Mod
{
    /// <summary>
    /// Represents a single item in the context menu
    /// </summary>
    public class MenuItem
    {
        #region Properties

        public string Text { get; private set; }
        public Action OnClick { get; private set; }
        public List<MenuItem> SubItems { get; private set; }
        public bool IsSeparator { get; private set; }
        public bool HasSubMenu => SubItems != null && SubItems.Count > 0;
        public bool ShowCheckbox { get; set; }
        public bool IsChecked { get; set; }
        public string ActionId { get; set; }  // Unique identifier for this action
        public MenuAction MenuAction { get; set; }  // Associated MenuAction object
        public bool IsToggleAction { get; set; }  // True for toggle actions (can't be quick actions)
        public Action<bool> OnToggle { get; set; }  // Called when toggle state changes
        public bool IsHeader { get; set; }  // True for header items (no hover effect)
        public bool Enabled { get; set; } = true;  // Whether the menu item is enabled (clickable)
        
        // Multi-button support
        public class ButtonInfo
        {
            public string Text { get; set; }
            public string Tooltip { get; set; }
            public Action OnClick { get; set; }
        }
        public List<ButtonInfo> MultiButtons { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a standard menu item with an action
        /// </summary>
        public MenuItem(string text, Action action)
        {
            Text = text;
            OnClick = action;
            IsSeparator = false;
        }

        /// <summary>
        /// Creates a menu item with submenu items
        /// </summary>
        public MenuItem(string text, List<MenuItem> subItems)
        {
            Text = text;
            SubItems = subItems;
            IsSeparator = false;
        }

        /// <summary>
        /// Creates a menu item with submenu that will be populated later
        /// </summary>
        public MenuItem(string text)
        {
            Text = text;
            SubItems = new List<MenuItem>();
            IsSeparator = false;
        }

        /// <summary>
        /// Private constructor for separators
        /// </summary>
        private MenuItem()
        {
            IsSeparator = true;
        }

        #endregion

        #region Factory Methods

        /// <summary>
        /// Creates a separator menu item
        /// </summary>
        public static MenuItem CreateSeparator()
        {
            return new MenuItem();
        }

        /// <summary>
        /// Creates a label menu item (no action)
        /// </summary>
        public static MenuItem CreateLabel(string text)
        {
            return new MenuItem(text, (Action)null);
        }
        
        /// <summary>
        /// Creates a header menu item (no action, no hover effect)
        /// </summary>
        public static MenuItem CreateHeader(string text)
        {
            return new MenuItem(text, (Action)null) { IsHeader = true };
        }

        #endregion

        #region Submenu Management

        /// <summary>
        /// Adds a submenu item
        /// </summary>
        public void AddSubItem(MenuItem item)
        {
            if (SubItems == null)
                SubItems = new List<MenuItem>();
            
            SubItems.Add(item);
        }

        /// <summary>
        /// Adds a separator to submenu
        /// </summary>
        public void AddSeparator()
        {
            if (SubItems == null)
                SubItems = new List<MenuItem>();
            
            SubItems.Add(CreateSeparator());
        }

        #endregion
    }
}