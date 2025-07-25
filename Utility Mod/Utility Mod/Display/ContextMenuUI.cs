using System;
using UnityEngine;

namespace Utility_Mod
{
    /// <summary>
    /// Handles UI rendering and help system for the context menu
    /// </summary>
    public class ContextMenuUI
    {
        private ContextMenuManager manager;
        
        // Help dialog state
        public bool showHelpDialog = false;
        private Vector2 helpDialogScrollPos = Vector2.zero;
        private GUIStyle helpTitleStyle;
        private GUIStyle helpHeaderStyle;
        private GUIStyle helpKeyStyle;
        private bool helpStylesInitialized = false;
        
        public ContextMenuUI(ContextMenuManager manager)
        {
            this.manager = manager;
        }
        
        public void ShowHelpDialog()
        {
            showHelpDialog = true;
            manager.CloseMenu();  // Close the context menu when showing help
        }
        
        public void ShowHelpMenu()
        {
            showHelpDialog = true;
        }
        
        public void DrawHelpDialog()
        {
            // Initialize styles once
            if (!helpStylesInitialized)
            {
                InitializeHelpStyles();
                helpStylesInitialized = true;
            }
            
            // Calculate dialog size and position
            float dialogWidth = 800f;
            float dialogHeight = 600f;
            float x = (Screen.width - dialogWidth) / 2f;
            float y = (Screen.height - dialogHeight) / 2f;
            
            // Draw modal background (semi-transparent overlay)
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", GUIStyle.none);
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            
            // Draw dialog box with more opaque background
            GUILayout.BeginArea(new Rect(x, y, dialogWidth, dialogHeight));
            
            // Create a more opaque background style
            GUIStyle dialogStyle = new GUIStyle(GUI.skin.box);
            dialogStyle.normal.background = CreateSolidColorTexture(new Color(0.1f, 0.1f, 0.1f, 0.95f));
            GUI.Box(new Rect(0, 0, dialogWidth, dialogHeight), "", dialogStyle);
            
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            
            // Title
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Context Menu Help", helpTitleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.Space(20);
            
            // Scrollable content area
            helpDialogScrollPos = GUILayout.BeginScrollView(helpDialogScrollPos, GUILayout.Height(dialogHeight - 100));
            
            // Keyboard shortcuts section
            GUILayout.Label("Keyboard Shortcuts", helpHeaderStyle);
            GUILayout.Space(10);
            
            DrawHelpLine("Right-click", "Open context menu (or execute quick action if set)");
            DrawHelpLine("Hold Right-click", $"Open context menu after {Main.settings.contextMenuHoldDuration:F1}s delay");
            DrawHelpLine("Shift + Right-click", "Paint mode - continuously spawn objects while dragging");
            DrawHelpLine("Ctrl + Click", "Keep menu open after selecting an item");
            DrawHelpLine("Alt + Click", "Set menu item as quick action");
            DrawHelpLine("Right-click (menu open)", "Repeat last executed action");
            DrawHelpLine("Left-click outside menu", "Close menu");
            DrawHelpLine("Escape", "Close menu");
            
            GUILayout.Space(20);
            
            // Quick actions section
            GUILayout.Label("Quick Actions", helpHeaderStyle);
            GUILayout.Space(10);
            GUILayout.Label("Set quick actions using one of these methods:", GUI.skin.label);
            DrawHelpLine("  Checkbox", "Click the checkbox next to any menu item");
            DrawHelpLine("  Alt + Click", "Hold Alt and click any menu item to set it as quick action");
            GUILayout.Space(5);
            GUILayout.Label("When a quick action is set, right-clicking will execute it immediately.", GUI.skin.label);
            GUILayout.Label("To open the menu when a quick action is set, hold right-click.", GUI.skin.label);
            
            GUILayout.Space(20);
            
            // Paint mode section
            GUILayout.Label("Paint Mode", helpHeaderStyle);
            GUILayout.Space(10);
            GUILayout.Label("Hold Shift + Right-click and drag to continuously spawn objects.", GUI.skin.label);
            GUILayout.Label($"Current Settings:", GUI.skin.label);
            DrawHelpLine("  Enemy Mode", Main.settings.enemyPaintMode == EnemyPaintMode.TimeBased ? "Time-based" : "Distance-based");
            if (Main.settings.enemyPaintMode == EnemyPaintMode.TimeBased)
            {
                DrawHelpLine("  Enemy Delay", $"{Main.settings.enemyPaintDelay:F1} seconds");
            }
            else
            {
                DrawHelpLine("  Enemy Distance", $"{Main.settings.enemyPaintDistance:F0} blocks");
            }
            DrawHelpLine("  Block/Doodad Distance", $"{Main.settings.blockPaintDistance:F0} blocks");
            
            GUILayout.Space(20);
            
            // Action descriptions section
            GUILayout.Label("Action Types", helpHeaderStyle);
            GUILayout.Space(10);
            DrawHelpLine("Teleport Here", "Instantly teleport the player to the clicked location");
            DrawHelpLine("Spawn Enemy", "Create enemy units at the clicked location");
            DrawHelpLine("Spawn Object", "Place blocks, explosives, crates, or doodads in the world");
            DrawHelpLine("Level Control", "Navigate levels, set starting level, go to specific level");
            DrawHelpLine("Waypoints", "Set teleport waypoints for quick navigation");
            DrawHelpLine("Zipline Placement", "Click twice to create a zipline between two points");
            
            GUILayout.Space(20);
            
            // Context actions section
            GUILayout.Label("Context Actions", helpHeaderStyle);
            GUILayout.Space(10);
            GUILayout.Label("Right-click directly on objects for context-specific actions:", GUI.skin.label);
            GUILayout.Space(5);
            DrawHelpLine("On Enemies", "Kill, Toggle Friendly/Hostile, Clone Enemy, Grab Enemy");
            DrawHelpLine("On Players", "Give Extra Life, Refill Special, Grab Player");
            DrawHelpLine("On Blocks", "Destroy Block, Clone Block");
            DrawHelpLine("On Friendly Units", "Make Hostile, Clone Unit, Grab Unit");
            
            GUILayout.Space(20);
            
            // Special modes section
            GUILayout.Label("Special Modes", helpHeaderStyle);
            GUILayout.Space(10);
            DrawHelpLine("Clone Mode", "After selecting clone, Right-click to place copies. Click to exit" );
            DrawHelpLine("Grab Mode", "Move units with cursor. Right-click to release, ESC to cancel");
            DrawHelpLine("Paint Mode", "Hold Shift + Right-click to continuously spawn while dragging");
            
            GUILayout.Space(20);
            
            // Recently used section
            GUILayout.Label("Recently Used Actions", helpHeaderStyle);
            GUILayout.Space(10);
            GUILayout.Label("The menu shows your last 5 used actions at the top for quick access.", GUI.skin.label);
            
            GUILayout.EndScrollView();
            
            GUILayout.Space(10);
            
            // Close button
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(100)))
            {
                showHelpDialog = false;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            GUILayout.EndVertical();
            
            GUILayout.EndArea();
            
            // Handle clicks outside dialog
            if (Event.current.type == EventType.MouseDown)
            {
                Vector2 mousePos = Event.current.mousePosition;
                Rect dialogRect = new Rect(x, y, dialogWidth, dialogHeight);
                if (!dialogRect.Contains(mousePos))
                {
                    showHelpDialog = false;
                    Event.current.Use();
                }
            }
        }
        
        private void DrawHelpLine(string key, string description)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key + ":", helpKeyStyle, GUILayout.Width(200));
            GUILayout.Label(description, GUI.skin.label);
            GUILayout.EndHorizontal();
        }
        
        private void InitializeHelpStyles()
        {
            helpTitleStyle = new GUIStyle(GUI.skin.label);
            helpTitleStyle.fontSize = 20;
            helpTitleStyle.fontStyle = FontStyle.Bold;
            
            helpHeaderStyle = new GUIStyle(GUI.skin.label);
            helpHeaderStyle.fontSize = 16;
            helpHeaderStyle.fontStyle = FontStyle.Bold;
            
            helpKeyStyle = new GUIStyle(GUI.skin.label);
            helpKeyStyle.fontStyle = FontStyle.Bold;
        }
        
        private Texture2D CreateSolidColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
        
        public void TrackLevelEdit(MenuAction action, Vector3 position)
        {
            if (action == null || !Main.settings.isRecordingLevelEdits)
                return;
                
            string levelKey = Main.GetCurrentLevelKey();
            
            // Get or create record for this level
            if (!Main.settings.levelEditRecords.ContainsKey(levelKey))
            {
                Main.settings.levelEditRecords[levelKey] = new LevelEditRecord { LevelKey = levelKey };
            }
            
            var record = Main.settings.levelEditRecords[levelKey];
            LevelEditAction editAction = null;
            
            // Convert MenuAction to LevelEditAction
            switch (action.Type)
            {
                case MenuActionType.SpawnEnemy:
                    if (action.UnitType.HasValue)
                        editAction = LevelEditAction.CreateUnitSpawn(position, action.UnitType.Value);
                    break;
                    
                case MenuActionType.SpawnBlock:
                    if (action.BlockType.HasValue)
                        editAction = LevelEditAction.CreateBlockSpawn(position, action.BlockType.Value);
                    break;
                    
                case MenuActionType.SpawnDoodad:
                    if (action.DoodadType.HasValue)
                        editAction = LevelEditAction.CreateDoodadSpawn(position, action.DoodadType.Value);
                    break;
                    
                case MenuActionType.Teleport:
                    editAction = LevelEditAction.CreateTeleport(position);
                    break;
            }
            
            if (editAction != null)
            {
                record.Actions.Add(editAction);
            }
        }
        
        // Called by Main when a level loads to replay edits
        public static void ReplayLevelEdits()
        {
            if (!Main.settings.enableLevelEditReplay)
                return;
                
            string levelKey = Main.GetCurrentLevelKey();
            
            if (Main.settings.levelEditRecords.ContainsKey(levelKey))
            {
                var record = Main.settings.levelEditRecords[levelKey];
                
                foreach (var action in record.Actions)
                {
                    try
                    {
                        action.Execute();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}