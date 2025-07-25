using System;
using System.Collections.Generic;
using UnityEngine;
using World.Doodads;

namespace Utility_Mod
{
    /// <summary>
    /// Handles all special modes for the context menu (Clone, Grab, Zipline, Paint)
    /// </summary>
    public class ContextMenuModes
    {
        private ContextMenuManager manager;
        
        // Clone mode state
        public Unit unitToClone = null;
        public Block blockToClone = null;
        
        // Grab mode state
        public Unit grabbedUnit = null;
        
        // Zipline placement state
        public ZiplinePoint pendingZiplinePoint = null;
        public Vector3 firstZiplinePosition;
        
        // Paint mode state
        public bool isPaintMode = false;
        public float lastPaintTime = 0f;
        public Vector3 lastPaintPosition = Vector3.zero;
        
        // Mass delete mode state
        public Vector3 massDeleteStartPos;
        public bool isDraggingMassDelete = false;
        
        public ContextMenuModes(ContextMenuManager manager)
        {
            this.manager = manager;
        }
        
        // Clone Mode Methods
        public void QuickCloneUnderCursor()
        {
            // If already in clone mode, exit it
            if (manager.CurrentMode == ContextMenuManager.ContextMenuMode.Clone)
            {
                ExitCloneMode();
                return;
            }
            
            // Get object under cursor
            GameObject targetObject = manager.GetObjectUnderCursor();
            if (targetObject == null)
                return;
                
            // Check if it's a unit
            Unit unit = targetObject.GetComponent<Unit>();
            if (unit != null)
            {
                // Enter clone mode for this unit
                manager.CurrentMode = ContextMenuManager.ContextMenuMode.Clone;
                unitToClone = unit;
                blockToClone = null;
                return;
            }
            
            // Check if it's a block
            Block block = targetObject.GetComponent<Block>();
            if (block != null && !block.destroyed)
            {
                // Check if we support cloning this block type
                BlockType? clonableType = manager.GetBlockTypeFromGroundType(block.groundType, block);
                if (clonableType.HasValue)
                {
                    // Enter clone mode for this block
                    manager.CurrentMode = ContextMenuManager.ContextMenuMode.Clone;
                    unitToClone = null;
                    blockToClone = block;
                }
                // If not clonable, do nothing
            }
        }
        
        public void ExitCloneMode()
        {
            manager.CurrentMode = ContextMenuManager.ContextMenuMode.Normal;
            unitToClone = null;
            blockToClone = null;
        }
        
        public void DrawCloneModeUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            
            // Add dark background for better readability
            GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
            bgStyle.normal.background = CreateSolidColorTexture(new Color(0, 0, 0, 0.8f));
            
            string objectType = unitToClone != null ? "Enemy" : "Block";
            string instruction = $"Clone Mode Active - Cloning {objectType}\nRight-click to spawn clone • Left-click or ESC to cancel";
            
            Rect textRect = new Rect(Screen.width / 2 - 300, 50, 600, 70);
            GUI.Box(textRect, "", bgStyle);
            GUI.Label(textRect, instruction, style);
        }
        
        // Grab Mode Methods
        public void ReleaseGrabbedUnit()
        {
            if (grabbedUnit != null && !grabbedUnit.destroyed)
            {
                // Restore invulnerability state
                grabbedUnit.invulnerable = false;

                if ( grabbedUnit is Mook mook )
                {
                    mook.SetFieldValue( "_invulnerableTime", 0f );
                }

                // Add unit back to map
                Map.units.Add( grabbedUnit );
            }
            
            manager.CurrentMode = ContextMenuManager.ContextMenuMode.Normal;
            grabbedUnit = null;
        }
        
        public void DrawGrabModeUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            
            // Add dark background for better readability
            GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
            bgStyle.normal.background = CreateSolidColorTexture(new Color(0, 0, 0, 0.8f));
            
            // Check if grabbed unit is a player
            string unitType = "Enemy";
            if (grabbedUnit != null)
            {
                for (int i = 0; i < HeroController.players.Length; i++)
                {
                    if (HeroController.players[i] != null && HeroController.players[i].character == grabbedUnit)
                    {
                        unitType = "Player";
                        break;
                    }
                }
            }
            
            string instruction = $"Grab Mode Active - Moving {unitType}\nRight-click to release • Left-click or ESC to cancel";
            
            Rect textRect = new Rect(Screen.width / 2 - 300, 50, 600, 70);
            GUI.Box(textRect, "", bgStyle);
            GUI.Label(textRect, instruction, style);
        }
        
        // Zipline Mode Methods
        public void HandleZiplinePlacement(Vector3 position)
        {
            if (manager.CurrentMode != ContextMenuManager.ContextMenuMode.ZiplinePlacement)
            {
                // First click - spawn first zipline point at menu position
                GameObject firstPoint = Main.SpawnDoodad(position, SpawnableDoodadType.ZiplinePoint);
                if (firstPoint != null)
                {
                    pendingZiplinePoint = firstPoint.GetComponent<ZiplinePoint>();
                    if (pendingZiplinePoint != null)
                    {
                        manager.CurrentMode = ContextMenuManager.ContextMenuMode.ZiplinePlacement;
                        firstZiplinePosition = position;
                    }
                }
            }
            else
            {
                // Second click - spawn second point and connect at position
                GameObject secondPoint = Main.SpawnDoodad(position, SpawnableDoodadType.ZiplinePoint);
                if (secondPoint != null)
                {
                    ZiplinePoint secondZiplinePoint = secondPoint.GetComponent<ZiplinePoint>();
                    if (secondZiplinePoint != null && pendingZiplinePoint != null)
                    {
                        // Clean up any existing connections
                        // Note: ResetZipline is internal, so we manually clean up
                        if (pendingZiplinePoint.otherPoint != null)
                        {
                            var otherPoint = pendingZiplinePoint.otherPoint;
                            pendingZiplinePoint.otherPoint = null;
                            otherPoint.otherPoint = null;
                            if (pendingZiplinePoint.zipLine != null)
                                UnityEngine.Object.Destroy(pendingZiplinePoint.zipLine.gameObject);
                        }
                        if (secondZiplinePoint.otherPoint != null)
                        {
                            var otherPoint = secondZiplinePoint.otherPoint;
                            secondZiplinePoint.otherPoint = null;
                            otherPoint.otherPoint = null;
                            if (secondZiplinePoint.zipLine != null)
                                UnityEngine.Object.Destroy(secondZiplinePoint.zipLine.gameObject);
                        }
                        
                        // Connect the two points
                        pendingZiplinePoint.otherPoint = secondZiplinePoint;
                        pendingZiplinePoint.SetupZipline();
                        
                        // Track the zipline as a single action
                        if (Main.settings.isRecordingLevelEdits)
                        {
                            string levelKey = Main.GetCurrentLevelKey();
                            if (!string.IsNullOrEmpty(levelKey))
                            {
                                if (!Main.settings.levelEditRecords.ContainsKey(levelKey))
                                {
                                    Main.settings.levelEditRecords[levelKey] = new LevelEditRecord { LevelKey = levelKey };
                                }
                                
                                var editAction = LevelEditAction.CreateZipline(firstZiplinePosition, position);
                                Main.settings.levelEditRecords[levelKey].Actions.Add(editAction);
                            }
                        }
                    }
                }
                
                // Reset state
                manager.CurrentMode = ContextMenuManager.ContextMenuMode.Normal;
                pendingZiplinePoint = null;
                
                // Close menu unless Ctrl is held
                if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                {
                    manager.CloseMenu();
                }
            }
        }
        
        public void DrawZiplinePreview()
        {
            // Convert world positions to screen positions
            Camera cam = Camera.main;
            if (cam == null || pendingZiplinePoint == null)
                return;
                
            Vector3 startPos = cam.WorldToScreenPoint(pendingZiplinePoint.transform.position);
            Vector3 currentMousePos = Input.mousePosition;
            
            // Unity GUI has inverted Y axis
            startPos.y = Screen.height - startPos.y;
            currentMousePos.y = Screen.height - currentMousePos.y;
            
            // Draw line from first point to mouse cursor
            Color oldColor = GUI.color;
            GUI.color = Color.cyan;
            
            // Calculate line points
            Vector2 start = new Vector2(startPos.x, startPos.y);
            Vector2 end = new Vector2(currentMousePos.x, currentMousePos.y);
            
            // Draw the line using GUI
            DrawLine(start, end, 3f);
            
            // Draw instruction text with better styling
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            
            // Add dark background for better readability
            GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
            bgStyle.normal.background = CreateSolidColorTexture(new Color(0, 0, 0, 0.8f));
            
            string instruction = "Zipline Placement\nRight-click to place second point • Left-click or ESC to cancel";
            Rect textRect = new Rect(Screen.width / 2 - 300, 50, 600, 70);
            GUI.Box(textRect, "", bgStyle);
            GUI.Label(textRect, instruction, style);
            
            GUI.color = oldColor;
        }
        
        // Paint Mode Methods
        public void HandlePaintMode(bool pressed, bool held, bool released)
        {
            if (pressed)
            {
                // Start paint mode
                isPaintMode = true;
                lastPaintTime = Time.time;
                lastPaintPosition = GetMouseWorldPosition();
                
                // Execute action at initial position
                if (manager.lastExecutedAction != null)
                {
                    ExecutePaintAction(manager.lastExecutedAction, lastPaintPosition);
                }
                else if (Main.settings.selectedQuickAction != null)
                {
                    ExecutePaintAction(Main.settings.selectedQuickAction, lastPaintPosition);
                }
            }
            else if (held && isPaintMode)
            {
                // Continue painting
                Vector3 currentPosition = GetMouseWorldPosition();
                
                if (manager.lastExecutedAction != null || Main.settings.selectedQuickAction != null)
                {
                    MenuAction action = manager.lastExecutedAction ?? Main.settings.selectedQuickAction;
                    
                    // Check if we should paint based on the action type
                    if (ShouldPaint(action, currentPosition))
                    {
                        ExecutePaintAction(action, currentPosition);
                        lastPaintTime = Time.time;
                        lastPaintPosition = currentPosition;
                    }
                }
            }
            else if (released)
            {
                // End paint mode
                isPaintMode = false;
            }
        }
        
        public bool ShouldPaint(MenuAction action, Vector3 currentPosition)
        {
            switch (action.Type)
            {
                case MenuActionType.SpawnBlock:
                case MenuActionType.SpawnDoodad:
                    // For blocks/doodads, check if we've moved enough distance in blocks
                    float distanceInUnits = Vector3.Distance(currentPosition, lastPaintPosition);
                    float distanceInBlocks = distanceInUnits / 16f;
                    
                    if (distanceInBlocks >= Main.settings.blockPaintDistance)
                    {
                        // Check if we're within map bounds
                        int currentColumn = (int)Mathf.Round(currentPosition.x / 16f);
                        int currentRow = (int)Mathf.Round(currentPosition.y / 16f);
                        
                        if (currentColumn >= 0 && currentColumn < Map.MapData.Width && 
                            currentRow >= 0 && currentRow < Map.MapData.Height)
                        {
                            // For blocks, also check if the position is empty
                            if (action.Type == MenuActionType.SpawnBlock)
                            {
                                return Map.blocks[currentColumn, currentRow] == null || Map.blocks[currentColumn, currentRow].destroyed;
                            }
                            return true; // Doodads can be placed anywhere
                        }
                    }
                    return false;
                    
                case MenuActionType.SpawnEnemy:
                    // For enemies, check time or distance based on settings
                    if (Main.settings.enemyPaintMode == EnemyPaintMode.TimeBased)
                    {
                        float timeSinceLastPaint = Time.time - lastPaintTime;
                        return timeSinceLastPaint >= Main.settings.enemyPaintDelay;
                    }
                    else // DistanceBased
                    {
                        // Measure distance in blocks (16 units = 1 block)
                        float enemyDistanceInUnits = Vector3.Distance(currentPosition, lastPaintPosition);
                        float enemyDistanceInBlocks = enemyDistanceInUnits / 16f;
                        return enemyDistanceInBlocks >= Main.settings.enemyPaintDistance;
                    }
                    
                default:
                    // Other actions don't support paint mode
                    return false;
            }
        }
        
        public void ExecutePaintAction(MenuAction action, Vector3 position)
        {
            // Don't track paint actions in recently used
            action.Execute(position);
            
            // But do track for level edit recording
            manager.TrackLevelEdit(action, position);
        }
        
        // Helper Methods
        private void DrawLine(Vector2 pointA, Vector2 pointB, float width)
        {
            // Save current GUI matrix
            Matrix4x4 matrix = GUI.matrix;
            
            // Calculate angle and length
            float angle = Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x) * 180f / Mathf.PI;
            float length = Vector2.Distance(pointA, pointB);
            
            // Set up rotation and position
            GUIUtility.RotateAroundPivot(angle, pointA);
            GUI.DrawTexture(new Rect(pointA.x, pointA.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            
            // Restore GUI matrix
            GUI.matrix = matrix;
        }
        
        private Texture2D CreateSolidColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
        
        private Vector3 GetMouseWorldPosition()
        {
            Camera camera = Camera.main ?? Camera.current;
            if (camera != null)
            {
                Vector3 worldPos = camera.ScreenToWorldPoint(Input.mousePosition);
                worldPos.z = 0;
                return worldPos;
            }
            return Vector3.zero;
        }
        
        // Mass Delete Mode Methods
        public void DrawMassDeleteUI()
        {
            // Draw rectangle if dragging
            if (isDraggingMassDelete)
            {
                Vector3 currentPos = GetMouseWorldPosition();
                DrawSelectionRectangle(massDeleteStartPos, currentPos);
            }
            
            // Draw instructions
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            
            // Add dark background for better readability
            GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
            bgStyle.normal.background = CreateSolidColorTexture(new Color(0, 0, 0, 0.8f));
            
            string instruction = "Mass Delete Mode\nClick and drag to select area • Right-click or ESC to cancel";
            Rect textRect = new Rect(Screen.width / 2 - 300, 50, 600, 70);
            GUI.Box(textRect, "", bgStyle);
            GUI.Label(textRect, instruction, style);
        }
        
        private void DrawSelectionRectangle(Vector3 worldStart, Vector3 worldEnd)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            
            // Convert world positions to screen positions
            Vector3 screenStart = cam.WorldToScreenPoint(worldStart);
            Vector3 screenEnd = cam.WorldToScreenPoint(worldEnd);
            
            // Unity GUI has inverted Y axis
            screenStart.y = Screen.height - screenStart.y;
            screenEnd.y = Screen.height - screenEnd.y;
            
            // Calculate rectangle
            float left = Mathf.Min(screenStart.x, screenEnd.x);
            float top = Mathf.Min(screenStart.y, screenEnd.y);
            float width = Mathf.Abs(screenEnd.x - screenStart.x);
            float height = Mathf.Abs(screenEnd.y - screenStart.y);
            
            // Draw rectangle outline
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0f, 0f, 0.8f); // Red with transparency
            
            // Draw border (4 rectangles for the outline)
            float borderWidth = 2f;
            GUI.DrawTexture(new Rect(left, top, width, borderWidth), Texture2D.whiteTexture); // Top
            GUI.DrawTexture(new Rect(left, top + height - borderWidth, width, borderWidth), Texture2D.whiteTexture); // Bottom
            GUI.DrawTexture(new Rect(left, top, borderWidth, height), Texture2D.whiteTexture); // Left
            GUI.DrawTexture(new Rect(left + width - borderWidth, top, borderWidth, height), Texture2D.whiteTexture); // Right
            
            // Draw semi-transparent fill
            GUI.color = new Color(1f, 0f, 0f, 0.1f);
            GUI.DrawTexture(new Rect(left, top, width, height), Texture2D.whiteTexture);
            
            GUI.color = oldColor;
        }
        
        public void ExecuteMassDelete(Vector3 startPos, Vector3 endPos)
        {
            // Record the mass delete action if recording is enabled
            if (Main.settings.isRecordingLevelEdits)
            {
                string levelKey = Main.GetCurrentLevelKey();
                if (!string.IsNullOrEmpty(levelKey))
                {
                    if (!Main.settings.levelEditRecords.ContainsKey(levelKey))
                    {
                        Main.settings.levelEditRecords[levelKey] = new LevelEditRecord { LevelKey = levelKey };
                    }
                    
                    var massDeleteAction = LevelEditAction.CreateMassDelete(startPos, endPos);
                    Main.settings.levelEditRecords[levelKey].Actions.Add(massDeleteAction);
                }
            }
            
            // Calculate bounds
            float minX = Mathf.Min(startPos.x, endPos.x);
            float maxX = Mathf.Max(startPos.x, endPos.x);
            float minY = Mathf.Min(startPos.y, endPos.y);
            float maxY = Mathf.Max(startPos.y, endPos.y);
            
            // Delete all doodads in the rectangle
            DeleteDoodadsInArea(minX, maxX, minY, maxY);
            
            // Delete checkpoints in the rectangle
            DeleteCheckpointsInArea(minX, maxX, minY, maxY);
            
            // Delete all units in the rectangle (precise position check)
            List<Unit> unitsToDelete = new List<Unit>();
            foreach (Unit unit in Map.units)
            {
                if (unit != null && !unit.destroyed && unit.health > 0)
                {
                    // Skip player units
                    bool isPlayer = false;
                    for (int i = 0; i < HeroController.players.Length; i++)
                    {
                        if (HeroController.players[i] != null && HeroController.players[i].character == unit)
                        {
                            isPlayer = true;
                            break;
                        }
                    }
                    
                    if (!isPlayer)
                    {
                        // Use exact unit position for precise detection
                        if (unit.X >= minX && unit.X <= maxX && unit.Y >= minY && unit.Y <= maxY)
                        {
                            unitsToDelete.Add(unit);
                        }
                    }
                }
            }
            
            // Execute deletions
            foreach (Unit unit in unitsToDelete)
            {
                // Kill the unit
                unit.Damage(9999, DamageType.Normal, 0, 0, 0, null, unit.X, unit.Y);
            }
            
            // Delete all blocks in the rectangle (precise grid detection)
            // Convert world coords to grid coords precisely
            int startCol = Mathf.FloorToInt(minX / 16f);
            int endCol = Mathf.CeilToInt(maxX / 16f);
            int startRow = Mathf.FloorToInt(minY / 16f);
            int endRow = Mathf.CeilToInt(maxY / 16f);
            
            // Clamp to map bounds
            startCol = Mathf.Max(0, startCol);
            endCol = Mathf.Min(Map.MapData.Width - 1, endCol);
            startRow = Mathf.Max(0, startRow);
            endRow = Mathf.Min(Map.MapData.Height - 1, endRow);
            
            for (int col = startCol; col <= endCol; col++)
            {
                for (int row = startRow; row <= endRow; row++)
                {
                    // Check if block center is within selection bounds
                    float blockCenterX = col * 16f + 8f;
                    float blockCenterY = row * 16f + 8f;
                    
                    if (blockCenterX >= minX && blockCenterX <= maxX && 
                        blockCenterY >= minY && blockCenterY <= maxY)
                    {
                        // Delete foreground block (includes ladders and indestructible blocks)
                        Block block = Map.blocks[col, row];
                        if (block != null && !block.destroyed)
                        {
                            // Use Map's proper deletion method (handles all block types)
                            Map.ClearForegroundBlock(col, row);
                        }
                        
                        // Delete background block
                        Block bgBlock = Map.backGroundBlocks[col, row];
                        if (bgBlock != null && !bgBlock.destroyed)
                        {
                            // Use Map's proper deletion method for background blocks
                            Map.ClearBackgroundBlock(col, row);
                        }
                        
                        // Also check persistent background blocks
                        if (Map.persistentBackgroundBlocks != null && 
                            col < Map.persistentBackgroundBlocks.GetLength(0) && 
                            row < Map.persistentBackgroundBlocks.GetLength(1))
                        {
                            Block persistentBgBlock = Map.persistentBackgroundBlocks[col, row];
                            if (persistentBgBlock != null && !persistentBgBlock.destroyed)
                            {
                                // Destroy persistent background block
                                UnityEngine.Object.Destroy(persistentBgBlock.gameObject);
                                Map.persistentBackgroundBlocks[col, row] = null;
                            }
                        }
                    }
                }
            }
        }
        
        private void DeleteDoodadsInArea(float minX, float maxX, float minY, float maxY)
        {
            // Try multiple approaches to find all doodads and background objects
            List<GameObject> objectsToDelete = new List<GameObject>();
            HashSet<GameObject> processedObjects = new HashSet<GameObject>();
            
            // Approach 1: Find all Doodad objects in the scene
            Doodad[] allDoodads = UnityEngine.Object.FindObjectsOfType<Doodad>();
            
            foreach (Doodad doodad in allDoodads)
            {
                if (doodad != null && !doodad.destroyed)
                {
                    Vector3 pos = doodad.transform.position;
                    if (pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY)
                    {
                        objectsToDelete.Add(doodad.gameObject);
                        processedObjects.Add(doodad.gameObject);
                    }
                }
            }
            
            // Approach 2: Find ALL GameObjects on layer 8 (background layer) in the area
            GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj != null && !processedObjects.Contains(obj))
                {
                    Vector3 pos = obj.transform.position;
                    if (pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY)
                    {
                        // Delete everything on layer 8 (background layer)
                        if (obj.layer == 8)
                        {
                            string nameLower = obj.name.ToLower();
                            
                            // Skip only critical system objects
                            if (!nameLower.Contains("camera") &&
                                !nameLower.Contains("light") &&
                                !nameLower.Contains("manager"))
                            {
                                objectsToDelete.Add(obj);
                            }
                        }
                    }
                }
            }
            
            // Delete all found objects
            foreach (GameObject obj in objectsToDelete)
            {
                if (obj != null)
                {
                    // Check for specific doodad types
                    DoodadDestroyable destroyable = obj.GetComponent<DoodadDestroyable>();
                    if (destroyable != null)
                    {
                        DamageObject damage = new DamageObject(9999, DamageType.Normal, 0, 0, 0, 0, null);
                        destroyable.Damage(damage);
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(obj);
                    }
                }
            }
        }
        
        private void DeleteCheckpointsInArea(float minX, float maxX, float minY, float maxY)
        {
            // Find all checkpoint objects
            CheckPoint[] allCheckpoints = UnityEngine.Object.FindObjectsOfType<CheckPoint>();
            
            foreach (CheckPoint checkpoint in allCheckpoints)
            {
                if (checkpoint != null)
                {
                    Vector3 pos = checkpoint.transform.position;
                    if (pos.x >= minX && pos.x <= maxX && pos.y >= minY && pos.y <= maxY)
                    {
                        // Remove from map's checkpoint list
                        if (Map.checkPoints != null && Map.checkPoints.Contains(checkpoint))
                        {
                            Map.checkPoints.Remove(checkpoint);
                        }
                        
                        // Destroy the checkpoint
                        UnityEngine.Object.Destroy(checkpoint.gameObject);
                    }
                }
            }
        }
    }
}