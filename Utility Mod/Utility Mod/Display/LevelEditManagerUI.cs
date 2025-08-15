using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Utility_Mod
{
    public class LevelEditManagerUI : MonoBehaviour
    {
        private static LevelEditManagerUI instance;
        private bool isVisible = false;
        private Rect windowRect = new Rect(100, 100, 750, 850);

        private Vector2 scrollPosition;
        private Vector2 slotScrollPosition;
        
        private LevelEditRecord currentRecord;
        private string currentLevelKey;
        private LevelEditSlots currentSlots;
        private string[] slotNames;
        private int selectedSlotIndex = 0;
        private bool showNewSlotDialog = false;
        private string newSlotName = "";
        
        private HashSet<int> selectedIndices = new HashSet<int>();
        private int lastClickedIndex = -1;
        
        private GUIStyle windowStyle;
        private GUIStyle actionItemStyle;
        private GUIStyle selectedItemStyle;
        private GUIStyle buttonStyle;
        private GUIStyle labelStyle;
        private GUIStyle toggleStyle;
        private bool stylesInitialized = false;
        
        public static void Show()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("LevelEditManagerUI");
                instance = go.AddComponent<LevelEditManagerUI>();
                DontDestroyOnLoad(go);
            }
            
            instance.OpenWindow();
        }
        
        public static void Hide()
        {
            if (instance != null)
            {
                instance.CloseWindow();
            }
        }
        
        private void OpenWindow()
        {
            isVisible = true;
            RefreshCurrentLevel();
            
            // Load saved position if available, otherwise center the window
            if (Main.settings.levelEditManagerWindowX > 0 && Main.settings.levelEditManagerWindowY > 0)
            {
                windowRect.x = Main.settings.levelEditManagerWindowX;
                windowRect.y = Main.settings.levelEditManagerWindowY;
            }
            else
            {
                windowRect.x = (Screen.width - windowRect.width) / 2;
                windowRect.y = (Screen.height - windowRect.height) / 2;
            }
        }
        
        private void CloseWindow()
        {
            isVisible = false;
            selectedIndices.Clear();
            lastClickedIndex = -1;
            
            // Save window position
            Main.settings.levelEditManagerWindowX = windowRect.x;
            Main.settings.levelEditManagerWindowY = windowRect.y;
            Main.settings.Save(Main.mod);
        }
        
        private void RefreshCurrentLevel()
        {
            currentLevelKey = Main.GetCurrentLevelKey();
            
            // Get the current level's edit record (normal system)
            if (Main.settings.levelEditRecords.ContainsKey(currentLevelKey))
            {
                currentRecord = Main.settings.levelEditRecords[currentLevelKey];
            }
            else
            {
                currentRecord = null;
            }
            
            // Get available save slots for this level
            if (!Main.settings.levelEditSlots.ContainsKey(currentLevelKey))
            {
                Main.settings.levelEditSlots[currentLevelKey] = new LevelEditSlots();
            }
            
            currentSlots = Main.settings.levelEditSlots[currentLevelKey];
            currentSlots.RebuildDictionary();
            
            // Build slot names array for dropdown
            var slots = new List<string>(currentSlots.Slots.Keys);
            if (slots.Count == 0)
            {
                slots.Add("(No saved slots)");
            }
            slotNames = slots.ToArray();
            
            // Reset selection
            selectedSlotIndex = 0;
        }
        
        private void InitStyles()
        {
            if (stylesInitialized) return;
            
            // Scale factor based on screen size (similar to Main.cs UI scaling)
            float scaleFactor = Screen.width / 1920f;
            if (scaleFactor < 1f) scaleFactor = 1f;
            
            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.fontSize = Mathf.RoundToInt(16 * scaleFactor);
            windowStyle.fontStyle = FontStyle.Bold;
            
            // Make window background opaque
            var windowTex = CreateTexture(2, 2, new Color(0.15f, 0.15f, 0.15f, 0.95f));
            windowStyle.normal.background = windowTex;
            windowStyle.onNormal.background = windowTex;
            
            actionItemStyle = new GUIStyle(GUI.skin.box);
            actionItemStyle.alignment = TextAnchor.MiddleLeft;
            actionItemStyle.normal.textColor = Color.white;
            actionItemStyle.fontSize = Mathf.RoundToInt(14 * scaleFactor);
            actionItemStyle.padding = new RectOffset(8, 8, 4, 4);
            actionItemStyle.margin = new RectOffset(2, 2, 1, 1);
            
            // Make action items more visible with darker background
            var itemTex = CreateTexture(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.8f));
            actionItemStyle.normal.background = itemTex;
            
            selectedItemStyle = new GUIStyle(actionItemStyle);
            selectedItemStyle.normal.background = CreateTexture(2, 2, new Color(0.3f, 0.5f, 0.8f, 0.8f));
            
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = Mathf.RoundToInt(14 * scaleFactor);
            buttonStyle.padding = new RectOffset(10, 10, 5, 5);
            
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = Mathf.RoundToInt(14 * scaleFactor);
            
            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.fontSize = Mathf.RoundToInt(14 * scaleFactor);
            toggleStyle.normal.textColor = Color.white;
            
            stylesInitialized = true;
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
        
        private string GetFormattedTitle()
        {
            if (GameState.Instance == null)
            {
                return "No Level";
            }
            
            string campaignName = GameState.Instance.campaignName;
            int levelNum = GameState.Instance.levelNumber;
            
            // Find the campaign index
            int campaignIndex = -1;
            for (int i = 0; i < Main.actualCampaignNames.Length; i++)
            {
                if (Main.actualCampaignNames[i] == campaignName)
                {
                    campaignIndex = i;
                    break;
                }
            }
            
            if (campaignIndex >= 0 && campaignIndex < Main.campaignDisplayNames.Length)
            {
                // Use the display name format like "Campaign 1 - Level 2"
                return $"{Main.campaignDisplayNames[campaignIndex]} - Level {levelNum + 1}";
            }
            else
            {
                // Fallback if we can't find the campaign
                return $"Campaign {campaignIndex + 1} - Level {levelNum + 1}";
            }
        }
        
        void Update()
        {
            if (!isVisible) return;
            
            // Check if the level has changed
            string newLevelKey = Main.GetCurrentLevelKey();
            if (newLevelKey != currentLevelKey)
            {
                RefreshCurrentLevel();
            }
        }
        
        void OnGUI()
        {
            if (!isVisible) return;
            
            InitStyles();
            
            string title = GetFormattedTitle();
            windowRect = GUI.Window(12345, windowRect, DrawWindow, title, windowStyle);
            
            windowRect.x = Mathf.Clamp(windowRect.x, 0, Screen.width - windowRect.width);
            windowRect.y = Mathf.Clamp(windowRect.y, 0, Screen.height - windowRect.height);
            
            // Draw new slot dialog AFTER the main window so it appears on top
            if (showNewSlotDialog)
            {
                DrawNewSlotDialog();
            }
        }
        
        private void DrawWindow(int windowID)
        {
            DrawCloseButton();
            
            // Add top padding to prevent overlap with title bar
            GUILayout.Space(30);
            
            DrawRecordingControls();
            
            GUILayout.Space(15);
            
            DrawActionList();
            
            GUILayout.Space(10);
            
            DrawActionButtons();
            
            GUILayout.Space(10);
            
            DrawImportExportButtons();
            
            GUILayout.Space(10);
            
            DrawSaveSlotControls();
            
            GUILayout.Space(5);
            
            // Use Unity's built-in window dragging - more reliable
            GUI.DragWindow(new Rect(0, 0, windowRect.width - 45, 30));
        }
        
        private void DrawCloseButton()
        {
            // Create a temporary button style for the close button
            GUIStyle closeButtonStyle = new GUIStyle(GUI.skin.button);
            closeButtonStyle.fontSize = 18;
            closeButtonStyle.fontStyle = FontStyle.Bold;
            closeButtonStyle.alignment = TextAnchor.MiddleCenter;
            
            if (GUI.Button(new Rect(windowRect.width - 40, 3, 35, 28), "X", closeButtonStyle))
            {
                CloseWindow();
            }
        }
        
        private void DrawRecordingControls()
        {
            GUILayout.BeginHorizontal();
            
            bool newAutoReplay = GUILayout.Toggle(Main.settings.enableLevelEditReplay, " Auto-Replay", toggleStyle);
            if (newAutoReplay != Main.settings.enableLevelEditReplay)
            {
                Main.settings.enableLevelEditReplay = newAutoReplay;
                Main.settings.Save(Main.mod);
            }
            
            GUILayout.FlexibleSpace();
            
            if (Main.settings.isRecordingLevelEdits)
            {
                GUI.color = Color.red;
                if (GUILayout.Button("Stop Recording", buttonStyle))
                {
                    Main.settings.isRecordingLevelEdits = false;
                    Main.settings.Save(Main.mod);
                    RefreshCurrentLevel();
                }
                GUI.color = Color.white;
            }
            else
            {
                if (GUILayout.Button("Start Recording", buttonStyle))
                {
                    Main.settings.isRecordingLevelEdits = true;
                    RefreshCurrentLevel();
                }
            }
            
            GUILayout.Space(5); // Add some padding on the right
            
            GUILayout.EndHorizontal();
        }
        
        private void DrawActionList()
        {
            int actionCount = currentRecord?.Actions?.Count ?? 0;
            GUILayout.Label($"Actions ({actionCount}):", labelStyle);
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUI.skin.box, GUILayout.Height(380));
            
            if (currentRecord != null && currentRecord.Actions != null)
            {
                for (int i = 0; i < currentRecord.Actions.Count; i++)
                {
                    DrawActionItem(i, currentRecord.Actions[i]);
                }
            }
            else
            {
                GUILayout.Label("No actions recorded for this level", labelStyle);
            }
            
            GUILayout.EndScrollView();
        }
        
        private void DrawActionItem(int index, LevelEditAction action)
        {
            GUILayout.BeginHorizontal();
            
            bool isSelected = selectedIndices.Contains(index);
            GUIStyle style = isSelected ? selectedItemStyle : actionItemStyle;
            
            string actionText = FormatAction(index, action);
            
            if (GUILayout.Button(actionText, style, GUILayout.ExpandWidth(true)))
            {
                HandleActionClick(index);
            }
            
            GUI.color = Color.red;
            if (GUILayout.Button("X", buttonStyle, GUILayout.Width(25)))
            {
                DeleteAction(index);
            }
            GUI.color = Color.white;
            
            GUILayout.EndHorizontal();
        }
        
        private string FormatAction(int index, LevelEditAction action)
        {
            string position = $"({action.X:F0}, {action.Y:F0})";
            
            switch (action.ActionType)
            {
                case LevelEditActionType.SpawnBlock:
                    return $"{index + 1}. Spawn Block {position} - {action.BlockType}";
                    
                case LevelEditActionType.DestroyBlock:
                    return $"{index + 1}. Delete Block {position}";
                    
                case LevelEditActionType.SpawnUnit:
                    return $"{index + 1}. Spawn Unit {position} - {action.UnitType}";
                    
                case LevelEditActionType.DestroyUnit:
                    return $"{index + 1}. Delete Unit {position}";
                    
                case LevelEditActionType.SpawnDoodad:
                    return $"{index + 1}. Spawn Doodad {position} - {action.DoodadType}";
                    
                case LevelEditActionType.SpawnZipline:
                    return $"{index + 1}. Spawn Zipline ({action.X:F0}, {action.Y:F0}) → ({action.X2:F0}, {action.Y2:F0})";
                    
                case LevelEditActionType.MassDelete:
                    return $"{index + 1}. Mass Delete ({action.X:F0}, {action.Y:F0}) → ({action.X2:F0}, {action.Y2:F0})";
                    
                case LevelEditActionType.Teleport:
                    return $"{index + 1}. Teleport {position}";
                    
                default:
                    return $"{index + 1}. {action.ActionType} {position}";
            }
        }
        
        private void HandleActionClick(int index)
        {
            Event e = Event.current;
            
            if (e.control)
            {
                if (selectedIndices.Contains(index))
                    selectedIndices.Remove(index);
                else
                    selectedIndices.Add(index);
            }
            else if (e.shift && lastClickedIndex >= 0)
            {
                int start = Mathf.Min(lastClickedIndex, index);
                int end = Mathf.Max(lastClickedIndex, index);
                
                for (int i = start; i <= end; i++)
                {
                    selectedIndices.Add(i);
                }
            }
            else
            {
                selectedIndices.Clear();
                selectedIndices.Add(index);
            }
            
            lastClickedIndex = index;
        }
        
        private void DeleteAction(int index)
        {
            if (currentRecord != null && index >= 0 && index < currentRecord.Actions.Count)
            {
                currentRecord.Actions.RemoveAt(index);
                Main.settings.Save(Main.mod);
                
                selectedIndices.Clear();
                lastClickedIndex = -1;
            }
        }
        
        private void DrawActionButtons()
        {
            GUILayout.BeginHorizontal();
            
            bool hasSelection = selectedIndices.Count > 0;
            bool canMoveUp = hasSelection && selectedIndices.Min() > 0;
            bool canMoveDown = hasSelection && currentRecord != null && selectedIndices.Max() < currentRecord.Actions.Count - 1;
            
            GUI.enabled = canMoveUp;
            if (GUILayout.Button("↑", buttonStyle, GUILayout.Width(40)))
            {
                MoveSelectedActions(-1);
            }
            
            GUI.enabled = canMoveDown;
            if (GUILayout.Button("↓", buttonStyle, GUILayout.Width(40)))
            {
                MoveSelectedActions(1);
            }
            
            GUI.enabled = hasSelection;
            if (GUILayout.Button("Delete Selected", buttonStyle))
            {
                DeleteSelectedActions();
            }
            
            GUI.enabled = currentRecord != null && currentRecord.Actions.Count > 0;
            if (GUILayout.Button("Clear All", buttonStyle))
            {
                ClearAllActions();
            }
            
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();
        }
        
        private void MoveSelectedActions(int direction)
        {
            if (currentRecord == null || selectedIndices.Count == 0) return;
            
            List<int> sortedIndices = selectedIndices.OrderBy(i => i).ToList();
            if (direction > 0) sortedIndices.Reverse();
            
            foreach (int index in sortedIndices)
            {
                int newIndex = index + direction;
                if (newIndex >= 0 && newIndex < currentRecord.Actions.Count)
                {
                    var temp = currentRecord.Actions[index];
                    currentRecord.Actions[index] = currentRecord.Actions[newIndex];
                    currentRecord.Actions[newIndex] = temp;
                    
                    selectedIndices.Remove(index);
                    selectedIndices.Add(newIndex);
                }
            }
            
            Main.settings.Save(Main.mod);
        }
        
        private void DeleteSelectedActions()
        {
            if (currentRecord == null || selectedIndices.Count == 0) return;
            
            var sortedIndices = selectedIndices.OrderByDescending(i => i).ToList();
            foreach (int index in sortedIndices)
            {
                if (index >= 0 && index < currentRecord.Actions.Count)
                {
                    currentRecord.Actions.RemoveAt(index);
                }
            }
            
            selectedIndices.Clear();
            lastClickedIndex = -1;
            Main.settings.Save(Main.mod);
        }
        
        private void ClearAllActions()
        {
            if (currentRecord != null)
            {
                currentRecord.Actions.Clear();
                selectedIndices.Clear();
                lastClickedIndex = -1;
                Main.settings.Save(Main.mod);
            }
        }
        
        private void DrawImportExportButtons()
        {
            GUILayout.BeginHorizontal();
            
            bool hasActions = currentRecord != null && currentRecord.Actions != null && currentRecord.Actions.Count > 0;
            
            GUI.enabled = hasActions;
            if (GUILayout.Button("Export to Clipboard", buttonStyle))
            {
                ExportToClipboard();
            }
            
            GUI.enabled = true;
            if (GUILayout.Button("Import from Clipboard", buttonStyle))
            {
                ImportFromClipboard();
            }
            
            GUILayout.EndHorizontal();
        }
        
        private void ExportToClipboard()
        {
            if (currentRecord == null || currentRecord.Actions == null || currentRecord.Actions.Count == 0)
            {
                Main.mod.Logger.Warning("No actions to export");
                return;
            }
            
            string exportData = LevelEditExporter.ExportToString(currentRecord);
            if (!string.IsNullOrEmpty(exportData))
            {
                LevelEditExporter.CopyToClipboard(exportData);
                
            }
            else
            {
                Main.mod.Logger.Error("Failed to export level edits");
            }
        }
        
        private void ImportFromClipboard()
        {
            string clipboardData = LevelEditExporter.GetFromClipboard();
            if (string.IsNullOrEmpty(clipboardData))
            {
                Main.mod.Logger.Warning("Clipboard is empty");
                return;
            }
            
            LevelEditRecord importedRecord = LevelEditExporter.ImportFromString(clipboardData);
            if (importedRecord != null && importedRecord.Actions != null && importedRecord.Actions.Count > 0)
            {
                // Ask for confirmation if there are existing actions
                if (currentRecord != null && currentRecord.Actions != null && currentRecord.Actions.Count > 0)
                {
                    // For now, we'll just replace - in a full implementation we might want a confirmation dialog
                    
                }
                
                // Create or update the record for current level
                if (currentRecord == null)
                {
                    currentRecord = new LevelEditRecord();
                    currentRecord.LevelKey = currentLevelKey;
                    Main.settings.levelEditRecords[currentLevelKey] = currentRecord;
                }
                
                // Replace actions with imported ones
                currentRecord.Actions = importedRecord.Actions;
                
                // Save and refresh
                Main.settings.Save(Main.mod);
                RefreshCurrentLevel();
                
                // Clear selection
                selectedIndices.Clear();
                lastClickedIndex = -1;
                
                
            }
            else
            {
                Main.mod.Logger.Error("Failed to import level edits - invalid data format");
            }
        }
        
        private void DrawSaveSlotControls()
        {
            GUILayout.Label("Save Slots:", labelStyle);
            
            // Dropdown for slot selection
            bool hasSlots = currentSlots != null && currentSlots.Slots.Count > 0;
            
            if (hasSlots)
            {
                // Use a scrollable area for slots with fixed height
                GUILayout.BeginVertical(GUI.skin.box);
                slotScrollPosition = GUILayout.BeginScrollView(slotScrollPosition, GUILayout.Height(80));
                
                for (int i = 0; i < slotNames.Length; i++)
                {
                    bool isSelected = i == selectedSlotIndex;
                    GUI.backgroundColor = isSelected ? Color.cyan : Color.white;
                    if (GUILayout.Button(slotNames[i], buttonStyle))
                    {
                        selectedSlotIndex = i;
                    }
                    GUI.backgroundColor = Color.white;
                }
                
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                
                // Put Load, Update and Delete buttons horizontally below the slot list
                GUILayout.BeginHorizontal();
                
                // Load button
                if (GUILayout.Button("Load", buttonStyle))
                {
                    LoadFromSlot(slotNames[selectedSlotIndex]);
                }
                
                // Update/overwrite button
                GUI.color = Color.yellow;
                if (GUILayout.Button("Update", buttonStyle))
                {
                    if (currentRecord != null && currentRecord.Actions != null && currentRecord.Actions.Count > 0)
                    {
                        SaveToSlot(slotNames[selectedSlotIndex]);
                        
                    }
                }
                GUI.color = Color.white;
                
                // Delete button
                GUI.color = Color.red;
                if (GUILayout.Button("Delete", buttonStyle))
                {
                    DeleteSlot(slotNames[selectedSlotIndex]);
                }
                GUI.color = Color.white;
                
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("No saved slots yet", labelStyle);
            }
            
            GUILayout.Space(10);
            
            // Save button - always visible at bottom
            bool hasCurrentEdits = currentRecord != null && currentRecord.Actions != null && currentRecord.Actions.Count > 0;
            
            if (hasCurrentEdits)
            {
                GUI.color = Color.green;
                if (GUILayout.Button($"Save to New Slot ({currentRecord.Actions.Count} actions)", buttonStyle, GUILayout.Height(35)))
                {
                    showNewSlotDialog = true;
                    newSlotName = "Slot " + (currentSlots.Slots.Count + 1);
                }
                GUI.color = Color.white;
            }
            else
            {
                GUI.enabled = false;
                GUILayout.Button("No edits to save", buttonStyle, GUILayout.Height(35));
                GUI.enabled = true;
            }
        }
        
        private void DrawNewSlotDialog()
        {
            // Use GUI.Window for proper rendering
            Rect dialogRect = new Rect(
                (Screen.width - 400) / 2,
                (Screen.height - 200) / 2,
                400, 200
            );
            
            // Draw semi-transparent background first
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), CreateTexture(2, 2, Color.white));
            GUI.color = Color.white;
            
            // Draw the dialog window
            GUI.Window(99999, dialogRect, DrawSlotDialogContents, "New Save Slot");
        }
        
        private void DrawSlotDialogContents(int windowID)
        {
            GUILayout.Space(10);
            
            GUILayout.Label("Slot Name:", labelStyle);
            newSlotName = GUILayout.TextField(newSlotName, 30);
            
            GUILayout.Space(20);
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Save", buttonStyle, GUILayout.Width(100)))
            {
                if (!string.IsNullOrEmpty(newSlotName))
                {
                    SaveToSlot(newSlotName);
                    showNewSlotDialog = false;
                }
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Cancel", buttonStyle, GUILayout.Width(100)))
            {
                showNewSlotDialog = false;
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        
        private void LoadFromSlot(string slotName)
        {
            if (currentSlots != null && !string.IsNullOrEmpty(slotName))
            {
                var loadedRecord = currentSlots.LoadSlot(slotName);
                if (loadedRecord != null)
                {
                    // Replace current level edits with loaded slot
                    currentRecord = loadedRecord;
                    Main.settings.levelEditRecords[currentLevelKey] = currentRecord;
                    
                    // Clear selection
                    selectedIndices.Clear();
                    lastClickedIndex = -1;
                    
                    Main.settings.Save(Main.mod);
                    
                }
            }
        }
        
        private void SaveToSlot(string slotName)
        {
            if (currentSlots != null && currentRecord != null && !string.IsNullOrEmpty(slotName))
            {
                currentSlots.SaveSlot(slotName, currentRecord);
                currentSlots.SyncToList();
                RefreshCurrentLevel();
                Main.settings.Save(Main.mod);
                
            }
        }
        
        private void DeleteSlot(string slotName)
        {
            if (currentSlots != null && !string.IsNullOrEmpty(slotName))
            {
                if (currentSlots.DeleteSlot(slotName))
                {
                    currentSlots.SyncToList();
                    RefreshCurrentLevel();
                    Main.settings.Save(Main.mod);
                    
                }
            }
        }
    }
}