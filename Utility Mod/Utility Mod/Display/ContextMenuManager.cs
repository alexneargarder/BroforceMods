using RocketLib;
using RocketLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityModManagerNet;
using static UnityEngine.UI.CanvasScaler;

namespace Utility_Mod
{
    /// <summary>
    /// Manages the context menu lifecycle and input handling
    /// </summary>
    public class ContextMenuManager : MonoBehaviour
    {
        #region Fields and Properties

        private ContextMenuBuilder builder;
        public ContextMenuModes modes;
        private ContextMenuUI ui;

        // Mode management
        public enum ContextMenuMode
        {
            Normal,
            Clone,
            ZiplinePlacement,
            Grab,
            MassDelete
        }

        public ContextMenuMode CurrentMode { get; set; } = ContextMenuMode.Normal;

        private static ContextMenuManager instance;
        public ContextMenu CurrentMenu { get; private set; }
        public bool isMenuOpen = false;
        private float rightClickHoldTime = 0f;
        private bool isHoldingRightClick = false;
        private Vector2 menuOpenPosition;
        private UnityModManager.ModEntry mod;
        public MenuAction lastExecutedAction = null;  // Track last action for right-click repeat

        public static ContextMenuManager Instance
        {
            get { return instance; }
        }

        // Properties to access mode fields through modes instance
        public ZiplinePoint PendingZiplinePoint
        {
            get { return modes?.pendingZiplinePoint; }
            set { if ( modes != null ) modes.pendingZiplinePoint = value; }
        }

        public Vector3 FirstZiplinePosition
        {
            get { return modes?.firstZiplinePosition ?? Vector3.zero; }
            set { if ( modes != null ) modes.firstZiplinePosition = value; }
        }

        public Unit unitToClone
        {
            get { return modes?.unitToClone; }
            set { if ( modes != null ) modes.unitToClone = value; }
        }

        public Block blockToClone
        {
            get { return modes?.blockToClone; }
            set { if ( modes != null ) modes.blockToClone = value; }
        }

        public Unit grabbedUnit
        {
            get { return modes?.grabbedUnit; }
            set { if ( modes != null ) modes.grabbedUnit = value; }
        }

        // Quick action feedback display
        private string quickActionFeedback = "";
        private float quickActionFeedbackTimer = 0f;
        private const float FEEDBACK_DISPLAY_TIME = 2f;

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            instance = this;
            mod = Main.mod;
            builder = new ContextMenuBuilder( this );
            modes = new ContextMenuModes( this );
            ui = new ContextMenuUI( this );
        }

        void OnDestroy()
        {
            if ( instance == this )
                instance = null;
        }

        void Update()
        {
            if ( !Main.enabled )
            {
                return;
            }

            // Update feedback timer
            if ( quickActionFeedbackTimer > 0 )
            {
                quickActionFeedbackTimer -= Time.unscaledDeltaTime;
            }

            // Skip processing if help dialog is showing
            if ( ui.showHelpDialog )
                return;

            if ( !ShouldProcessInput() )
                return;

            // Handle special modes before normal input
            if ( CurrentMode == ContextMenuMode.ZiplinePlacement )
            {
                if ( Input.GetMouseButtonDown( 1 ) )
                {
                    // Right-click places second zipline point
                    Vector3 worldPos = GetMouseWorldPosition();
                    modes.HandleZiplinePlacement( worldPos );
                    return;
                }

                // Cancel on escape or left click
                if ( Input.GetKeyDown( KeyCode.Escape ) || Input.GetMouseButtonDown( 0 ) )
                {
                    CurrentMode = ContextMenuMode.Normal;
                    modes.pendingZiplinePoint = null;
                    return;
                }
            }

            // Handle clone mode
            if ( CurrentMode == ContextMenuMode.Clone )
            {
                if ( Input.GetMouseButtonDown( 1 ) )
                {
                    // Right-click spawns a clone at current mouse position
                    Vector3 worldPos = GetMouseWorldPosition();

                    if ( modes.unitToClone != null )
                    {
                        // Clone the unit at mouse position
                        UnitType unitType = modes.unitToClone.GetUnitType();
                        Main.SpawnUnit( unitType, worldPos );

                        // Track the edit if recording
                        if ( Main.settings.isRecordingLevelEdits )
                        {
                            var action = MenuAction.CreateSpawnEnemy( unitType );
                            ui.TrackLevelEdit( action, worldPos );
                        }
                    }
                    else if ( modes.blockToClone != null )
                    {
                        // Clone the block at mouse position
                        // Snap to grid
                        Vector3 snappedPos = new Vector3(
                            Mathf.Round( worldPos.x / 16f ) * 16f,
                            Mathf.Round( worldPos.y / 16f ) * 16f,
                            0
                        );

                        // Always try the exact cloning method first for perfect visual preservation
                        if (!TryCloneBlockExact(modes.blockToClone, snappedPos))
                        {
                            // Fall back to the original method if exact cloning fails
                            BlockType? blockType = GetBlockTypeFromGroundType( modes.blockToClone.groundType, modes.blockToClone );
                            if ( blockType.HasValue )
                            {
                                Main.SpawnBlock( snappedPos, blockType.Value );

                                // Track the edit if recording
                                if ( Main.settings.isRecordingLevelEdits )
                                {
                                    var action = MenuAction.CreateSpawnBlock( blockType.Value );
                                    ui.TrackLevelEdit( action, snappedPos );
                                }
                            }
                            else
                            {
                            }
                        }
                    }
                    return;
                }

                // Cancel on escape or left click
                if ( Input.GetKeyDown( KeyCode.Escape ) || Input.GetMouseButtonDown( 0 ) )
                {
                    modes.ExitCloneMode();
                    return;
                }
            }

            // Handle grab mode
            if ( CurrentMode == ContextMenuMode.Grab )
            {
                
                if ( modes.grabbedUnit != null && !modes.grabbedUnit.destroyed && modes.grabbedUnit.health > 0 )
                {
                    // Reset velocities to prevent physics from interfering
                    modes.grabbedUnit.xI = 0f;
                    modes.grabbedUnit.yI = 0f;

                    // Prevent fall damage while grabbed
                    modes.grabbedUnit.invulnerable = true;

                    // Right-click to release
                    if ( Input.GetMouseButtonDown( 1 ) )
                    {
                        modes.ReleaseGrabbedUnit();
                        return;
                    }

                    // Cancel on escape or left click
                    if ( Input.GetKeyDown( KeyCode.Escape ) || Input.GetMouseButtonDown( 0 ) )
                    {
                        modes.ReleaseGrabbedUnit();
                        return;
                    }
                }
                else
                {
                    // Grabbed unit was destroyed or died, exit grab mode immediately
                    CurrentMode = ContextMenuMode.Normal;
                    modes.ReleaseGrabbedUnit();
                    modes.grabbedUnit = null;
                }
            }
            
            // Handle mass delete mode
            if ( CurrentMode == ContextMenuMode.MassDelete )
            {
                if ( Input.GetMouseButtonDown( 0 ) )
                {
                    // Start dragging
                    modes.massDeleteStartPos = GetMouseWorldPosition();
                    modes.isDraggingMassDelete = true;
                }
                else if ( Input.GetMouseButtonUp( 0 ) && modes.isDraggingMassDelete )
                {
                    // End dragging and delete everything in the rectangle
                    Vector3 currentPos = GetMouseWorldPosition();
                    modes.ExecuteMassDelete( modes.massDeleteStartPos, currentPos );
                    
                    // Exit mass delete mode
                    CurrentMode = ContextMenuMode.Normal;
                    modes.isDraggingMassDelete = false;
                }
                else if ( Input.GetKeyDown( KeyCode.Escape ) || Input.GetMouseButtonDown( 1 ) )
                {
                    // Cancel mass delete mode
                    CurrentMode = ContextMenuMode.Normal;
                    modes.isDraggingMassDelete = false;
                }
            }

            HandleContextMenuInput();

            // Check if zipline placement is active and first point still exists
            if ( CurrentMode == ContextMenuMode.ZiplinePlacement && modes.pendingZiplinePoint == null )
            {
                // First point was destroyed, cancel placement
                CurrentMode = ContextMenuMode.Normal;
            }
        }

        #endregion

        #region Input Handling
        void LateUpdate()
        {
            if ( CurrentMode == ContextMenuMode.Grab && modes.grabbedUnit != null && !modes.grabbedUnit.destroyed && modes.grabbedUnit.health > 0 )
            {
                Vector3 worldPos = GetMouseWorldPosition();
                worldPos.y -= 2f * modes.grabbedUnit.height;

                // Ensure unit isn't moving
                modes.grabbedUnit.X = worldPos.x;
                modes.grabbedUnit.Y = worldPos.y;

                // Reset velocities to prevent physics from interfering
                modes.grabbedUnit.xI = 0f;
                modes.grabbedUnit.yI = 0f;

                // Move unit visually
                modes.grabbedUnit.transform.position = worldPos;
                modes.grabbedUnit.transform.rotation = Quaternion.identity;
            }
        }

        void OnGUI()
        {
            if ( !Main.enabled )
                return;

            // Draw context menu
            if ( isMenuOpen && CurrentMenu != null )
            {
                try
                {
                    CurrentMenu.Draw();
                }
                catch
                {
                    CloseMenu();
                }
            }

            // Draw help dialog
            if ( ui.showHelpDialog )
            {
                ui.DrawHelpDialog();
            }

            // Draw hold progress indicator
            if ( Main.settings.showHoldProgressIndicator && IsHoldingForMenu() && !isMenuOpen )
            {
                DrawHoldProgressIndicator();
            }

            // Draw zipline placement preview
            if ( CurrentMode == ContextMenuMode.ZiplinePlacement && modes.pendingZiplinePoint != null )
            {
                modes.DrawZiplinePreview();
            }

            // Draw clone mode UI
            if ( CurrentMode == ContextMenuMode.Clone )
            {
                modes.DrawCloneModeUI();
            }

            // Draw grab mode UI
            if ( CurrentMode == ContextMenuMode.Grab )
            {
                modes.DrawGrabModeUI();
            }
            
            // Draw mass delete mode UI
            if ( CurrentMode == ContextMenuMode.MassDelete )
            {
                modes.DrawMassDeleteUI();
            }

            // Draw quick action feedback
            if ( quickActionFeedbackTimer > 0 && !string.IsNullOrEmpty( quickActionFeedback ) )
            {
                DrawQuickActionFeedback();
            }
            
            ContextMenuBuilder.DrawProfileNameDialog();
        }

        private bool ShouldProcessInput()
        {
            // Check if context menu is enabled
            if ( !Main.settings.contextMenuEnabled )
            {
                return false;
            }

            // Allow menu to work anywhere, but still check for editor mode if Map exists
            try
            {
                if ( Map.isEditing )
                {
                    return false;
                }
            }
            catch
            {
                // Map might not exist in menus, that's ok
            }

            return true;
        }

        private void HandleContextMenuInput()
        {
            bool rightClickPressed = Input.GetMouseButtonDown( 1 );
            bool rightClickHeld = Input.GetMouseButton( 1 );
            bool rightClickReleased = Input.GetMouseButtonUp( 1 );

            // Debug logging
            if ( rightClickPressed && mod != null )
            {
            }

            // Handle menu dismissal (but not when in special modes)
            if ( isMenuOpen && CurrentMode == ContextMenuMode.Normal )
            {
                if ( Input.GetMouseButtonDown( 0 ) || Input.GetKeyDown( KeyCode.Escape ) )
                {
                    if ( !IsMouseOverMenu() )
                    {
                        CloseMenu();
                        return;
                    }
                }
            }

            // Handle right-click based on mode
            if ( Main.settings.contextMenuEnabled )
            {
                HandleEnabledMode( rightClickPressed, rightClickHeld, rightClickReleased );
            }
        }

        private void HandleEnabledMode( bool pressed, bool held, bool released )
        {
            // Handle Shift + right-click for paint mode
            if ( Input.GetKey( KeyCode.LeftShift ) || Input.GetKey( KeyCode.RightShift ) )
            {
                modes.HandlePaintMode( pressed, held, released );
                return;
            }

            // Handle right-click while menu is open to repeat last action (but not in special modes)
            if ( pressed && isMenuOpen && CurrentMode == ContextMenuMode.Normal )
            {
                if ( lastExecutedAction != null )
                {
                    ExecuteAction( lastExecutedAction, true );  // Use current mouse position
                }
                return;
            }

            // Handle normal right-click
            if ( pressed && !isMenuOpen )
            {
                isHoldingRightClick = true;
                rightClickHoldTime = 0f;
            }
            else if ( held && isHoldingRightClick )
            {
                rightClickHoldTime += Time.unscaledDeltaTime;

                // Open menu if held long enough
                if ( rightClickHoldTime >= Main.settings.contextMenuHoldDuration && !isMenuOpen )
                {
                    OpenMenu();
                    isHoldingRightClick = false;
                }
            }
            else if ( released )
            {
                // If menu didn't open and it was a quick click
                if ( isHoldingRightClick && !isMenuOpen && rightClickHoldTime < Main.settings.contextMenuHoldDuration )
                {
                    // Execute quick action if set, otherwise open menu
                    if ( Main.settings.selectedQuickAction != null )
                    {
                        ExecuteAction( Main.settings.selectedQuickAction, true );  // Use current mouse position
                    }
                    else
                    {
                        OpenMenu();
                    }
                }

                isHoldingRightClick = false;
                rightClickHoldTime = 0f;
            }
        }

        #endregion

        #region Menu Management

        private void OpenMenu()
        {
            if ( isMenuOpen )
                return;


            try
            {
                CurrentMenu = new ContextMenu();
                CurrentMenu.OnCheckboxToggled = HandleCheckboxToggled;

                builder.BuildMenuItems();

                menuOpenPosition = Input.mousePosition;
                CurrentMenu.SetPosition( menuOpenPosition );

                isMenuOpen = true;

            }
            catch
            {
                isMenuOpen = false;
                CurrentMenu = null;
            }
        }

        public void CloseMenu()
        {
            if ( !isMenuOpen )
                return;

            isMenuOpen = false;
            CurrentMenu = null;
        }

        private bool IsMouseOverMenu()
        {
            if ( CurrentMenu == null )
                return false;

            return CurrentMenu.IsMouseOver();
        }

        private void HandleCheckboxToggled( MenuItem item, bool isChecked )
        {
            if ( isChecked )
            {
                // Set as quick action
                Main.settings.selectedQuickAction = item.MenuAction;

                // Clear stored targets for context actions when setting as quick action
                if ( Main.settings.selectedQuickAction != null )
                {
                    Main.settings.selectedQuickAction.TargetUnit = null;
                    Main.settings.selectedQuickAction.TargetBlock = null;
                }

                // Uncheck all other items
                if ( CurrentMenu != null )
                {
                    UpdateCheckboxStates( CurrentMenu.GetItems(), item.ActionId );
                }
                
                // Show feedback if this was triggered by Alt-click
                if ( Input.GetKey( KeyCode.LeftAlt ) || Input.GetKey( KeyCode.RightAlt ) )
                {
                    ShowQuickActionFeedback( item.Text, true );
                }
            }
            else if ( Main.settings.selectedQuickAction != null && Main.settings.selectedQuickAction.Id == item.ActionId )
            {
                // Clear quick action
                Main.settings.selectedQuickAction = null;
                
                // Show feedback
                ShowQuickActionFeedback( item.Text, false );
            }

            // Update the clicked item's state
            item.IsChecked = isChecked;
        }

        private void ShowQuickActionFeedback( string actionName, bool wasSet )
        {
            if ( wasSet )
            {
                quickActionFeedback = $"Quick action set: {actionName}";
            }
            else
            {
                quickActionFeedback = "Quick action cleared";
            }
            quickActionFeedbackTimer = FEEDBACK_DISPLAY_TIME;
        }

        private void DrawQuickActionFeedback()
        {
            // Calculate alpha for fade out effect
            float alpha = Mathf.Clamp01( quickActionFeedbackTimer / FEEDBACK_DISPLAY_TIME );
            
            // Style setup
            GUIStyle style = new GUIStyle( GUI.skin.label );
            style.fontSize = 28;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = new Color( 1f, 1f, 1f, alpha );
            
            // Background style
            GUIStyle bgStyle = new GUIStyle( GUI.skin.box );
            bgStyle.normal.background = CreateTexture( 2, 2, new Color( 0f, 0f, 0f, 0.8f * alpha ) );
            
            // Draw at top center of screen
            Rect textRect = new Rect( Screen.width / 2 - 300, 100, 600, 70 );
            GUI.Box( textRect, "", bgStyle );
            GUI.Label( textRect, quickActionFeedback, style );
        }

        private Texture2D CreateTexture( int width, int height, Color color )
        {
            Color[] pixels = new Color[width * height];
            for ( int i = 0; i < pixels.Length; i++ )
            {
                pixels[i] = color;
            }

            Texture2D texture = new Texture2D( width, height );
            texture.SetPixels( pixels );
            texture.Apply();
            return texture;
        }

        private void DrawHoldProgressIndicator()
        {
            float progress = GetHoldProgress();
            if ( progress <= 0 )
                return;

            // Get mouse position
            Vector2 mousePos = Event.current.mousePosition;
            
            // Indicator settings
            float radius = 20f;
            float thickness = 3f;
            int segments = 32;
            float startAngle = -90f; // Start from top
            float progressAngle = progress * 360f;
            
            // Center position
            Vector2 center = new Vector2( mousePos.x + radius + 10, mousePos.y );
            
            // Draw background circle (darker)
            GUI.color = new Color( 0.2f, 0.2f, 0.2f, 0.5f );
            DrawCircularProgress( center, radius, thickness, 0f, 360f, segments );
            
            // Draw progress arc
            GUI.color = new Color( 1f, 1f, 1f, 0.8f );
            DrawCircularProgress( center, radius, thickness, startAngle, progressAngle, segments );
            
            // Reset color
            GUI.color = Color.white;
        }

        private void DrawCircularProgress( Vector2 center, float radius, float thickness, float startAngle, float arcAngle, int segments )
        {
            if ( arcAngle <= 0 )
                return;
                
            // Save current matrix
            Matrix4x4 matrix = GUI.matrix;
            
            // Calculate how many segments to draw based on progress
            int segmentsToDraw = Mathf.Max( 1, Mathf.RoundToInt( segments * ( arcAngle / 360f ) ) );
            float anglePerSegment = arcAngle / segmentsToDraw;
            
            // Draw each segment
            for ( int i = 0; i < segmentsToDraw; i++ )
            {
                float angle = startAngle + ( i * anglePerSegment );
                float radians = angle * Mathf.Deg2Rad;
                
                // Calculate position on circle
                Vector2 pos = center + new Vector2( Mathf.Cos( radians ), Mathf.Sin( radians ) ) * radius;
                
                // Rotate around center to align segment
                GUIUtility.RotateAroundPivot( angle + 90f, pos );
                
                // Draw segment
                GUI.DrawTexture( new Rect( pos.x - thickness / 2f, pos.y, thickness, thickness ), Texture2D.whiteTexture );
                
                // Restore matrix for next segment
                GUI.matrix = matrix;
            }
        }

        private void UpdateCheckboxStates( List<MenuItem> items, string selectedActionId )
        {
            foreach ( var menuItem in items )
            {
                if ( menuItem.ShowCheckbox && menuItem.ActionId != selectedActionId )
                {
                    menuItem.IsChecked = false;
                }

                // Recursively update submenus
                if ( menuItem.HasSubMenu )
                {
                    UpdateCheckboxStates( menuItem.SubItems, selectedActionId );
                }
            }
        }

        public void UpdateSpawnToggleStates( List<MenuItem> items )
        {
            foreach ( var menuItem in items )
            {
                // Update spawn toggle checkboxes based on current settings
                if ( menuItem.Text == "Spawn at Custom Waypoint" )
                {
                    menuItem.IsChecked = Main.settings.changeSpawn;
                }
                else if ( menuItem.Text == "Spawn at Final Checkpoint" )
                {
                    menuItem.IsChecked = Main.settings.changeSpawnFinal;
                }

                // Recursively update submenus
                if ( menuItem.HasSubMenu )
                {
                    UpdateSpawnToggleStates( menuItem.SubItems );
                }
            }
        }

        public void ExecuteAction( MenuAction action, bool useCurrentMousePosition = false )
        {
            if ( action == null )
                return;

            // Track the action
            lastExecutedAction = action;
            AddToRecentlyUsed( action );

            // Get execution position
            Vector3 worldPos = Vector3.zero;
            if ( useCurrentMousePosition )
            {
                // Use current mouse position for quick actions
                worldPos = GetMouseWorldPosition();
            }
            else
            {
                // Use menu position for menu items
                Camera camera = Camera.main ?? Camera.current;
                if ( camera != null )
                {
                    worldPos = camera.ScreenToWorldPoint( menuOpenPosition );
                    worldPos.z = 0;
                }
            }

            // Handle special case for zipline placement
            if ( action.Type == MenuActionType.ZiplinePlacement )
            {
                // For second click, always use current mouse position
                if ( CurrentMode == ContextMenuMode.ZiplinePlacement )
                {
                    worldPos = GetMouseWorldPosition();
                }

                modes.HandleZiplinePlacement( worldPos );

                // Always close menu unless Ctrl is held
                if ( !Input.GetKey( KeyCode.LeftControl ) && !Input.GetKey( KeyCode.RightControl ) )
                {
                    CloseMenu();
                }
                return;
            }
            
            // Handle special case for mass delete
            if ( action.Type == MenuActionType.MassDelete )
            {
                // Enter mass delete mode
                CurrentMode = ContextMenuMode.MassDelete;
                modes.isDraggingMassDelete = false;
                
                // Close menu
                CloseMenu();
                return;
            }
            
            // Handle special case for grab actions
            if ( action.Type == MenuActionType.GrabEnemy || action.Type == MenuActionType.GrabPlayer )
            {
                // Execute the grab action
                action.Execute( worldPos, useCurrentMousePosition );
                
                // Close menu unless Ctrl is held
                if ( !Input.GetKey( KeyCode.LeftControl ) && !Input.GetKey( KeyCode.RightControl ) )
                {
                    CloseMenu();
                }
                return;
            }

            // Execute the action
            action.Execute( worldPos, useCurrentMousePosition );

            // Track level edit if recording is enabled
            if ( Main.settings.isRecordingLevelEdits )
            {
                ui.TrackLevelEdit( action, worldPos );
            }

            // Close menu unless Ctrl is held
            if ( !Input.GetKey( KeyCode.LeftControl ) && !Input.GetKey( KeyCode.RightControl ) )
            {
                CloseMenu();
            }
        }

        private void AddToRecentlyUsed( MenuAction action )
        {
            // Remove if already exists (by ID)
            Main.settings.recentlyUsedItems.RemoveAll( a => a.Id == action.Id );

            // Add to front
            Main.settings.recentlyUsedItems.Insert( 0, action );

            // Limit to max items
            while ( Main.settings.recentlyUsedItems.Count > Main.settings.maxRecentItems )
            {
                Main.settings.recentlyUsedItems.RemoveAt( Main.settings.recentlyUsedItems.Count - 1 );
            }
        }

        public float GetHoldProgress()
        {
            if ( !isHoldingRightClick || !Main.settings.contextMenuEnabled )
                return 0f;

            return Mathf.Clamp01( rightClickHoldTime / Main.settings.contextMenuHoldDuration );
        }

        public bool IsHoldingForMenu()
        {
            return isHoldingRightClick && Main.settings.contextMenuEnabled;
        }

        public bool IsMenuOpenOrPending()
        {
            return isMenuOpen || isHoldingRightClick;
        }

        public Vector3 GetMouseWorldPosition()
        {
            Camera camera = Camera.main ?? Camera.current;
            if ( camera != null )
            {
                Vector3 worldPos = camera.ScreenToWorldPoint( Input.mousePosition );
                worldPos.z = 0;
                return worldPos;
            }
            return Vector3.zero;
        }

        public void ShowHelpDialog()
        {
            ui.ShowHelpDialog();
        }

        public void ShowHelpMenu()
        {
            ui.ShowHelpMenu();
        }

        public void QuickCloneUnderCursor()
        {
            modes?.QuickCloneUnderCursor();
        }

        // New method to try to clone a block exactly - returns true if successful
        public bool TryCloneBlockExact(Block sourceBlock, Vector3 position)
        {
            if (sourceBlock == null || sourceBlock.destroyed)
                return false;
                
            // Snap to grid
            int column = (int)Mathf.Round(position.x / 16f);
            int row = (int)Mathf.Round(position.y / 16f);
            
            // Bounds checking
            if (Map.MapData == null || column < 0 || column >= Map.MapData.Width || row < 0 || row >= Map.MapData.Height)
                return false;
                
            if (Map.blocks != null && Map.blocks[column, row] != null && !Map.blocks[column, row].destroyed)
            {
                return false;
            }
                
            try 
            {
                // Build the cache if needed
                if (themeBlockCaches == null)
                {
                    BuildBlockPrefabCache();
                }
                
                // Try to find the prefab in our cache
                string sourceName = sourceBlock.name.Replace("(Clone)", "").Trim();
                
                // Remove coordinate suffixes - handle both _X_Y and _X_Y_Z_W patterns
                int underscoreIndex = sourceName.LastIndexOf('_');
                if (underscoreIndex > 0)
                {
                    string afterLastUnderscore = sourceName.Substring(underscoreIndex + 1);
                    // Check if it ends with a number
                    if (Regex.IsMatch(afterLastUnderscore, @"^\d+$"))
                    {
                        // Look for the start of the coordinate pattern
                        int coordStart = underscoreIndex;
                        while (coordStart > 0)
                        {
                            int prevUnderscore = sourceName.LastIndexOf('_', coordStart - 1);
                            if (prevUnderscore < 0)
                                break;
                            string between = sourceName.Substring(prevUnderscore + 1, coordStart - prevUnderscore - 1);
                            if (!Regex.IsMatch(between, @"^\d+$"))
                                break;
                            coordStart = prevUnderscore;
                        }
                        sourceName = sourceName.Substring(0, coordStart);
                    }
                }
                
                // Get current theme name
                string currentThemeName = GetCurrentThemeName();
                
                Block prefabBlock = null;
                GameObject prefabObject = null; // For boulders
                
                // Special handling for boulder blocks
                if (sourceBlock is BoulderBlock && boulderPrefabCache != null && boulderPrefabCache.ContainsKey(sourceName))
                {
                    prefabObject = boulderPrefabCache[sourceName];
                    if (prefabObject != null)
                    {
                        prefabBlock = prefabObject.GetComponent<Block>();
                    }
                }
                else if (themeBlockCaches != null)
                {
                    // First check current theme
                    if (!string.IsNullOrEmpty(currentThemeName) && themeBlockCaches.ContainsKey(currentThemeName))
                    {
                        var currentThemeCache = themeBlockCaches[currentThemeName];
                        if (currentThemeCache.ContainsKey(sourceName))
                        {
                            prefabBlock = currentThemeCache[sourceName];
                        }
                    }
                    
                    // If not found in current theme, check other themes
                    if (prefabBlock == null)
                    {
                        foreach (var themeCache in themeBlockCaches)
                        {
                            if (themeCache.Key == currentThemeName) continue; // Skip current theme, already checked
                            
                            if (themeCache.Value.ContainsKey(sourceName))
                            {
                                prefabBlock = themeCache.Value[sourceName];
                                break;
                            }
                        }
                    }
                    
                    // Check if the cached prefab is still valid
                    if (prefabBlock != null && (prefabBlock.gameObject == null))
                    {
                        prefabBlock = null;
                    }
                }
                
                
                if (prefabBlock != null)
                {
                    // Get the BlockType for tracking purposes
                    BlockType? blockType = GetBlockTypeFromGroundType(sourceBlock.groundType, sourceBlock);
                    
                    // Use our modified SpawnBlockInternal with the exact prefab
                    Main.SpawnBlockInternal(row, column, blockType ?? BlockType.Brick, prefabBlock);
                    
                    // Track the edit if recording
                    if (Main.settings.isRecordingLevelEdits && blockType.HasValue)
                    {
                        var action = MenuAction.CreateSpawnBlock(blockType.Value);
                        ui.TrackLevelEdit(action, new Vector3(column * 16f, row * 16f, 5f));
                    }
                    
                    return true;
                }
            }
            catch
            {
            }
            
            // Fallback: not using exact cloning
            return false;
        }
        
        // Static cache for block prefabs, organized by theme name
        private static Dictionary<string, Dictionary<string, Block>> themeBlockCaches = null;
        // Cache for boulder GameObjects (which aren't Block components)
        private static Dictionary<string, GameObject> boulderPrefabCache = null;
        
        // Get the name of the current active theme
        private static string GetCurrentThemeName()
        {
            if (Map.Instance == null || Map.Instance.activeTheme == null)
                return "";
                
            // Compare activeTheme reference to known themes
            if (Map.Instance.jungleThemeReference != null && Map.Instance.activeTheme == Map.Instance.jungleThemeReference.Asset)
                return "jungle";
            if (Map.Instance.cityThemeReference != null && Map.Instance.activeTheme == Map.Instance.cityThemeReference.Asset)
                return "city";
            if (Map.Instance.desertThemeReference != null && Map.Instance.activeTheme == Map.Instance.desertThemeReference.Asset)
                return "desert";
            if (Map.Instance.burningJungleThemeReference != null && Map.Instance.activeTheme == Map.Instance.burningJungleThemeReference.Asset)
                return "burningJungle";
            if (Map.Instance.forestThemeReference != null && Map.Instance.activeTheme == Map.Instance.forestThemeReference.Asset)
                return "forest";
            if (Map.Instance.hellThemeReference != null && Map.Instance.activeTheme == Map.Instance.hellThemeReference.Asset)
                return "hell";
            if (Map.Instance.americaThemeReference != null && Map.Instance.activeTheme == Map.Instance.americaThemeReference.Asset)
                return "america";
                
            // Unknown theme
            return "";
        }
        
        // Build a comprehensive cache of all block prefabs from all themes
        private static void BuildBlockPrefabCache()
        {
            if (themeBlockCaches != null)
                return;
                
            themeBlockCaches = new Dictionary<string, Dictionary<string, Block>>();
            boulderPrefabCache = new Dictionary<string, GameObject>();
            
            // Get all theme references from Map.Instance
            if (Map.Instance == null)
            {
                return;
            }
            
            // Build cache for each theme
            // Process each theme individually
            if (Map.Instance.jungleThemeReference != null && Map.Instance.jungleThemeReference.Asset != null)
                BuildThemeCache("jungle", Map.Instance.jungleThemeReference.Asset);
            if (Map.Instance.cityThemeReference != null && Map.Instance.cityThemeReference.Asset != null)
                BuildThemeCache("city", Map.Instance.cityThemeReference.Asset);
            if (Map.Instance.desertThemeReference != null && Map.Instance.desertThemeReference.Asset != null)
                BuildThemeCache("desert", Map.Instance.desertThemeReference.Asset);
            if (Map.Instance.burningJungleThemeReference != null && Map.Instance.burningJungleThemeReference.Asset != null)
                BuildThemeCache("burningJungle", Map.Instance.burningJungleThemeReference.Asset);
            if (Map.Instance.forestThemeReference != null && Map.Instance.forestThemeReference.Asset != null)
                BuildThemeCache("forest", Map.Instance.forestThemeReference.Asset);
            if (Map.Instance.hellThemeReference != null && Map.Instance.hellThemeReference.Asset != null)
                BuildThemeCache("hell", Map.Instance.hellThemeReference.Asset);
            if (Map.Instance.americaThemeReference != null && Map.Instance.americaThemeReference.Asset != null)
                BuildThemeCache("america", Map.Instance.americaThemeReference.Asset);
            
            // Create a "shared" theme for SharedLevelObjectsHolder
            if (Map.Instance.sharedObjectsReference != null && Map.Instance.sharedObjectsReference.Asset != null)
            {
                var sharedCache = new Dictionary<string, Block>();
                themeBlockCaches["shared"] = sharedCache;
                
                var sharedHolder = Map.Instance.sharedObjectsReference.Asset;
                System.Reflection.FieldInfo[] sharedFields = sharedHolder.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                foreach (var field in sharedFields)
                {
                    if (typeof(Block).IsAssignableFrom(field.FieldType))
                    {
                        Block block = field.GetValue(sharedHolder) as Block;
                        if (block != null)
                        {
                            string name = block.name.Replace("(Clone)", "").Trim();
                            sharedCache[name] = block;
                        }
                    }
                }
            }
            
        }
        
        // Helper method to build cache for a single theme
        private static void BuildThemeCache(string themeName, ThemeHolder theme)
        {
            if (theme == null)
                return;
                
            var themeCache = new Dictionary<string, Block>();
            themeBlockCaches[themeName] = themeCache;
            
            System.Reflection.FieldInfo[] fields = theme.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            foreach (var field in fields)
            {
                // Handle Block fields
                if (typeof(Block).IsAssignableFrom(field.FieldType))
                {
                    Block block = field.GetValue(theme) as Block;
                    if (block != null)
                    {
                        string name = block.name.Replace("(Clone)", "").Trim();
                        themeCache[name] = block;
                    }
                }
                // Handle Block arrays
                else if (field.FieldType.IsArray && typeof(Block).IsAssignableFrom(field.FieldType.GetElementType()))
                {
                    Block[] blocks = field.GetValue(theme) as Block[];
                    if (blocks != null)
                    {
                        foreach (Block block in blocks)
                        {
                            if (block != null)
                            {
                                string name = block.name.Replace("(Clone)", "").Trim();
                                themeCache[name] = block;
                            }
                        }
                    }
                }
                // Handle boulders (GameObject array)
                else if (field.Name == "boulders" && field.FieldType == typeof(GameObject[]))
                {
                    GameObject[] boulders = field.GetValue(theme) as GameObject[];
                    if (boulders != null)
                    {
                        foreach (GameObject boulder in boulders)
                        {
                            if (boulder != null)
                            {
                                string name = boulder.name.Replace("(Clone)", "").Trim();
                                boulderPrefabCache[name] = boulder;
                            }
                        }
                    }
                }
                // Handle crateDoodads (SpriteSM array that might contain blocks)
                else if (field.Name == "crateDoodads" && field.FieldType == typeof(SpriteSM[]))
                {
                    SpriteSM[] doodads = field.GetValue(theme) as SpriteSM[];
                    if (doodads != null)
                    {
                        foreach (SpriteSM doodad in doodads)
                        {
                            if (doodad != null && doodad.gameObject != null)
                            {
                                // Check if this doodad has a Block component
                                Block block = doodad.gameObject.GetComponent<Block>();
                                if (block != null)
                                {
                                    string name = block.name.Replace("(Clone)", "").Trim();
                                    themeCache[name] = block;
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private BlockType? MapAmmoCrateType(object ammoType)
        {
            if (ammoType == null)
                return BlockType.Crate;
                
            string typeName = ammoType.ToString();
            switch (typeName)
            {
                case "Standard":
                    return BlockType.AmmoCrate;
                case "Airstrike":
                    return BlockType.AirStrikeAmmoCrate;
                case "Timeslow":
                    return BlockType.TimeAmmoCrate;
                case "RemoteControlCar":
                    return BlockType.RCCarAmmoCrate;
                case "MechDrop":
                    return BlockType.MechDropAmmoCrate;
                case "AlienPheromones":
                    return BlockType.AlienPheromonesAmmoCrate;
                case "Steroids":
                    return BlockType.SteroidsAmmoCrate;
                case "Piggy":
                    return BlockType.PigAmmoCrate;
                case "Perk":
                    return BlockType.FlexAmmoCrate;
                case "Dollars":
                    return BlockType.MoneyCrate;
                case "Revive":
                    return BlockType.PillsCrate;
                default:
                    return BlockType.Crate;
            }
        }
        
        
        public BlockType? GetBlockTypeFromGroundType( GroundType groundType, Block block = null )
        {
            // Special handling for blocks that report misleading ground types
            if (block != null)
            {
                // DoodadCrate blocks report Earth ground type but should remain crates
                if (block is DoodadCrate)
                {
                    return BlockType.Crate;
                }
            }
            
            // Map GroundType to BlockType based on available BlockType values
            switch ( groundType )
            {
                // Basic terrain
                case GroundType.Earth:
                case GroundType.EarthTop:
                case GroundType.EarthMiddle:
                    return BlockType.Dirt;
                case GroundType.CaveRock:
                    return BlockType.CaveRock;
                case GroundType.Sand:
                    return BlockType.SandyEarth;
                case GroundType.DesertSand:
                    return BlockType.DesertSand;

                // Environment-specific terrain
                case GroundType.CaveEarth:
                    return BlockType.CaveEarth;
                case GroundType.HellRock:
                    return BlockType.HellRock;
                case GroundType.DesertCaveRock:
                    return BlockType.DesertCaveRock;
                case GroundType.DesertCaveEarth:
                    return BlockType.DesertCaveEarth;
                case GroundType.DesertEarth:
                    return BlockType.DesertEarth;
                case GroundType.CityEarth:
                    return BlockType.CityEarth;
                case GroundType.Skulls:
                    return BlockType.Skulls;
                case GroundType.Bones:
                    return BlockType.Bones;

                // Alien terrain
                case GroundType.AlienEarth:
                    return BlockType.AlienEarth;
                case GroundType.AlienFlesh:
                    return BlockType.AlienFlesh;
                case GroundType.AlienExplodingFlesh:
                    return BlockType.AlienExplodingFlesh;
                case GroundType.AlienDirt:
                    return BlockType.AlienDirt;

                // Building materials
                case GroundType.Brick:
                case GroundType.BrickMiddle:
                    return BlockType.Brick;
                case GroundType.BrickTop:
                    return BlockType.BrickTop;
                case GroundType.Steel:
                    return BlockType.Steel;
                case GroundType.Metal:
                    return BlockType.Metal;

                // Military structures
                case GroundType.Bunker:
                    return BlockType.Bunker;
                case GroundType.WatchTower:
                    return BlockType.WatchTower;

                // Roofs
                case GroundType.Roof:
                    return BlockType.Roof;
                case GroundType.ThatchRoof:
                    return BlockType.ThatchRoof;
                case GroundType.FactoryRoof:
                    return BlockType.FactoryRoof;
                case GroundType.DesertRoof:
                    return BlockType.DesertRoof;
                case GroundType.DesertRoofRed:
                    return BlockType.DesertRoofRed;
                case GroundType.TentRoof:
                    return BlockType.TentRoof;

                // Sacred/Temple structures
                case GroundType.SacredTemple:
                    return BlockType.SacredTemple;
                case GroundType.SacredTempleGold:
                    return BlockType.SacredTempleGold;

                // City/Desert structures
                case GroundType.CityBrick:
                    return BlockType.CityBrick;
                case GroundType.DesertBrick:
                    return BlockType.DesertBrick;
                case GroundType.CityRoad:
                    return BlockType.CityRoad;
                case GroundType.CityAssMouth:
                    return BlockType.CityAssMouth;

                // Bridges
                case GroundType.Bridge:
                case GroundType.BridgeTop:
                    return BlockType.Bridge;
                case GroundType.Bridge2:
                    return BlockType.Bridge2;
                case GroundType.MetalBridge:
                    return BlockType.MetalBridge;
                case GroundType.MetalBridge2:
                    return BlockType.MetalBridge2;
                case GroundType.SacredBridge:
                    return BlockType.SacredBridge;
                case GroundType.AlienBridge:
                    return BlockType.AlienBridge;

                // Ladders
                case GroundType.Ladder:
                    return BlockType.Ladder;
                case GroundType.AlienLadder:
                    return BlockType.AlienLadder;
                case GroundType.MetalLadder:
                    return BlockType.MetalLadder;
                case GroundType.CityLadder:
                    return BlockType.CityLadder;
                case GroundType.DesertLadder:
                    return BlockType.DesertLadder;

                // Special blocks
                case GroundType.FallingBlock:
                    return BlockType.FallingBlock;
                case GroundType.Quicksand:
                    return BlockType.Quicksand;
                case GroundType.Boulder:
                    return BlockType.Boulder;
                case GroundType.BoulderBig:
                    return BlockType.BoulderBig;
                case GroundType.Sandbag:
                    return BlockType.Sandbag;
                case GroundType.Vault:
                    return BlockType.Vault;
                case GroundType.SmallCageBlock:
                    return BlockType.SmallCageBlock;
                case GroundType.StandardCage:
                    return BlockType.StandardCage;

                // Large blocks
                case GroundType.BigBlock:
                    return BlockType.BigBlock;
                case GroundType.SacredBigBlock:
                    return BlockType.SacredBigBlock;

                // Barrels/Explosives
                case GroundType.Barrel:
                    return BlockType.ExplosiveBarrel;
                case GroundType.PropaneBarrel:
                    return BlockType.PropaneTank;
                case GroundType.DesertOilBarrel:
                    return BlockType.DesertOilBarrel;
                case GroundType.OilTank:
                    return BlockType.OilTank;

                // Organic destructibles
                case GroundType.Beehive:
                    return BlockType.BeeHive;
                case GroundType.AlienEgg:
                    return BlockType.AlienEgg;
                case GroundType.AlienEggExplosive:
                    return BlockType.AlienEggExplosive;

                // Crates
                case GroundType.Chest:
                    return BlockType.Crate;
                case GroundType.AmmoCrate:
                    return BlockType.AmmoCrate;
                case GroundType.MoneyCrate:
                    return BlockType.MoneyCrate;
                case GroundType.PillsCrate:
                    return BlockType.PillsCrate;

                // Pipes and structures
                case GroundType.Pipe:
                    return BlockType.Pipe;
                case GroundType.OilPipe:
                    return BlockType.OilPipe;
                case GroundType.Statue:
                    return BlockType.Statue;
                case GroundType.TyreBlock:
                    return BlockType.TyreBlock;

                // Hazards
                case GroundType.AlienBarbShooter:
                    return BlockType.AlienBarbShooter;
                case GroundType.BuriedRocket:
                    return BlockType.BuriedRocket;

                // Special cases - These GroundTypes don't have direct BlockType equivalents
                // Map them to the closest equivalent
                case GroundType.WoodenBlock:
                    // Special handling for crates - check if it's a CrateBlock with ammo type
                    if ( block != null )
                    {
                        CrateBlock crateBlock = block as CrateBlock;
                        if ( crateBlock != null && crateBlock.containsPresent )
                        {
                            // Map ammo type to specific crate BlockType
                            switch ( crateBlock.ammoType )
                            {
                                case PockettedSpecialAmmoType.Standard:
                                    // Check if it has a pickup - if not, it's just a regular crate
                                    // The pickup field is private, so we check containsPresent instead
                                    return BlockType.AmmoCrate;
                                case PockettedSpecialAmmoType.Airstrike:
                                    return BlockType.AirStrikeAmmoCrate;
                                case PockettedSpecialAmmoType.Timeslow:
                                    return BlockType.TimeAmmoCrate;
                                case PockettedSpecialAmmoType.RemoteControlCar:
                                    return BlockType.RCCarAmmoCrate;
                                case PockettedSpecialAmmoType.MechDrop:
                                    return BlockType.MechDropAmmoCrate;
                                case PockettedSpecialAmmoType.AlienPheromones:
                                    return BlockType.AlienPheromonesAmmoCrate;
                                case PockettedSpecialAmmoType.Steroids:
                                    return BlockType.SteroidsAmmoCrate;
                                case PockettedSpecialAmmoType.Piggy:
                                    return BlockType.PigAmmoCrate;
                                case PockettedSpecialAmmoType.Perk:
                                    return BlockType.FlexAmmoCrate;
                                case PockettedSpecialAmmoType.Dollars:
                                    return BlockType.MoneyCrate;
                                case PockettedSpecialAmmoType.Revive:
                                    return BlockType.PillsCrate;
                                default:
                                    return BlockType.Crate; // Generic crate for unknown types
                            }
                        }
                    }
                    return BlockType.Crate; // Default for non-crate wooden blocks or crates without presents
                case GroundType.WoodenWall1:
                case GroundType.WoodenWall2:
                    return BlockType.Crate; // Closest wooden equivalent

                case GroundType.Cage:
                    return BlockType.StandardCage;

                case GroundType.HellEarth:
                    return BlockType.HellRock; // HellEarth doesn't exist in BlockType

                case GroundType.AssMouth:
                    return BlockType.CityAssMouth;

                // Background/Behind blocks - map to their foreground equivalents
                case GroundType.EarthBehind:
                case GroundType.DesertBackgroundEarth:
                    return BlockType.Dirt;
                case GroundType.BrickBehind:
                case GroundType.BrickBehindDoodads:
                case GroundType.DesertBackgroundBrick:
                case GroundType.CityBackgroundBrick:
                    return BlockType.Brick;
                case GroundType.BunkerBehind:
                    return BlockType.Bunker;
                case GroundType.AlienEarthBehind:
                    return BlockType.AlienEarth;
                case GroundType.WoodenBehind:
                case GroundType.WoodBackground:
                    return BlockType.Crate;
                case GroundType.BathroomBehind:
                case GroundType.ShaftBehind:
                case GroundType.FactoryBehind:
                case GroundType.VentBehind:
                case GroundType.VaultBehind:
                    return BlockType.Brick; // Generic brick for background elements

                // Wall types
                case GroundType.Wall:
                case GroundType.WallTop:
                    return BlockType.Brick;

                // Doodads and non-block types - default to dirt
                case GroundType.Empty:
                case GroundType.Tree:
                case GroundType.TreeBushes:
                case GroundType.Doodad:
                case GroundType.OutdoorDoodad:
                case GroundType.IndoorDoodad:
                case GroundType.Mook:
                case GroundType.CheckPoint:
                case GroundType.MookDoor:
                case GroundType.HiddenExplosives:
                case GroundType.PureEvil:
                case GroundType.HutScaffolding:
                case GroundType.SingleBlock:
                case GroundType.Parallax1:
                case GroundType.Parallax2:
                case GroundType.Parallax3:
                case GroundType.Corpses:
                    return null; // We don't support cloning these types

                default:
                    // Don't support cloning unmapped types
                    return null;
            }
        }

        public int GetLevelCountForCampaign( int campaignIndex )
        {
            // Match the exact logic from Main.cs OnGUI method
            int actualCampaignNum = campaignIndex + 1;
            int numberOfLevels = 1;

            switch ( actualCampaignNum )
            {
                case 1: numberOfLevels = 4; break;
                case 2: numberOfLevels = 4; break;
                case 3: numberOfLevels = 4; break;
                case 4: numberOfLevels = 4; break;
                case 5: numberOfLevels = 3; break;
                case 6: numberOfLevels = 4; break;
                case 7: numberOfLevels = 5; break;
                case 8: numberOfLevels = 4; break;
                case 9: numberOfLevels = 4; break;
                case 10: numberOfLevels = 4; break;
                case 11: numberOfLevels = 6; break;
                case 12: numberOfLevels = 5; break;
                case 13: numberOfLevels = 5; break;
                case 14: numberOfLevels = 5; break;
                case 15: numberOfLevels = 14; break;
                default: numberOfLevels = 1; break;
            }

            return numberOfLevels;
        }

        public GameObject GetObjectUnderCursor()
        {
            Vector3 worldPos = GetMouseWorldPosition();

            Camera camera = Camera.main ?? Camera.current;
            if (camera == null)
                return null;

            // Use sphere cast for more generous hit detection
            Ray ray = camera.ScreenPointToRay( Input.mousePosition );
            RaycastHit[] hits = Physics.SphereCastAll( ray, 8f, 100f ); // 8 unit radius for generous detection

            // Find the closest unit or block
            GameObject closestObject = null;
            float closestDistance = float.MaxValue;

            foreach ( RaycastHit hit in hits )
            {
                // Check if it's a unit (including parent objects for multi-part bosses)
                Unit unit = hit.collider.GetComponent<Unit>();
                if ( unit == null )
                {
                    // Check parent objects for Unit component
                    unit = hit.collider.GetComponentInParent<Unit>();
                }

                if ( unit != null )
                {
                    float distance = Vector3.Distance( worldPos, unit.transform.position );
                    if ( distance < closestDistance )
                    {
                        closestDistance = distance;
                        closestObject = unit.gameObject; // Use the Unit's gameObject, not the collider's
                    }
                    continue;
                }

                // Check if it's a block
                Block block = hit.collider.GetComponent<Block>();
                if ( block == null )
                {
                    // Check parent objects for Block component
                    block = hit.collider.GetComponentInParent<Block>();
                }

                if ( block != null && !block.destroyed )
                {
                    float distance = Vector3.Distance( worldPos, block.transform.position );
                    if ( distance < closestDistance )
                    {
                        closestDistance = distance;
                        closestObject = block.gameObject; // Use the Block's gameObject
                    }
                }
            }

            // If we found something with sphere cast, return it
            if ( closestObject != null )
                return closestObject;

            // Original raycast as secondary check for precise clicks
            RaycastHit directHit;
            if ( Physics.Raycast( ray, out directHit, 100f ) )
            {
                // Check if it's a unit (including parent objects)
                Unit unit = directHit.collider.GetComponent<Unit>();
                if ( unit == null )
                    unit = directHit.collider.GetComponentInParent<Unit>();
                if ( unit != null )
                    return unit.gameObject;

                // Check if it's a block (including parent objects)
                Block block = directHit.collider.GetComponent<Block>();
                if ( block == null )
                    block = directHit.collider.GetComponentInParent<Block>();
                if ( block != null && !block.destroyed )
                    return block.gameObject;
            }

            // Fallback: Check map grid for blocks
            int column = (int)Mathf.Round( worldPos.x / 16f );
            int row = (int)Mathf.Round( worldPos.y / 16f );

            if ( Map.MapData != null && Map.blocks != null && column >= 0 && column < Map.MapData.Width && row >= 0 && row < Map.MapData.Height )
            {
                if ( Map.blocks[column, row] != null && !Map.blocks[column, row].destroyed )
                {
                    return Map.blocks[column, row].gameObject;
                }
            }

            return null;
        }

        public void TrackLevelEdit( MenuAction action, Vector3 position )
        {
            ui.TrackLevelEdit( action, position );
        }

        // Called by Main when a level loads to replay edits
        public static void ReplayLevelEdits()
        {
            ContextMenuUI.ReplayLevelEdits();
        }

        #endregion
    }
}