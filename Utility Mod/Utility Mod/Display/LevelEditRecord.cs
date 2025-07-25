using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using RocketLib.Utils;

namespace Utility_Mod
{
    [Serializable]
    public enum LevelEditActionType
    {
        SpawnUnit,
        DestroyUnit,
        SpawnBlock,
        DestroyBlock,
        SpawnDoodad,
        Teleport,
        SpawnZipline,
        MassDelete
    }

    [Serializable]
    public class LevelEditAction
    {
        [XmlAttribute]
        public LevelEditActionType ActionType { get; set; }
        
        [XmlAttribute]
        public float X { get; set; }
        
        [XmlAttribute]
        public float Y { get; set; }
        
        // For zipline - second point position
        [XmlAttribute]
        public float X2 { get; set; }
        
        [XmlAttribute]
        public float Y2 { get; set; }
        
        // For background blocks
        [XmlAttribute]
        public bool IsBackgroundBlock { get; set; }
        
        // For spawn actions - XML serializer doesn't handle nullable enums well
        [XmlIgnore]
        public UnitType? UnitType { get; set; }
        
        [XmlIgnore]
        public BlockType? BlockType { get; set; }
        
        [XmlIgnore]
        public SpawnableDoodadType? DoodadType { get; set; }
        
        // Workarounds for XML serialization of nullable enums
        [XmlAttribute("UnitType")]
        public string UnitTypeString
        {
            get { return UnitType.HasValue ? UnitType.Value.ToString() : null; }
            set 
            { 
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        UnitType = (UnitType)Enum.Parse(typeof(UnitType), value);
                    }
                    catch
                    {
                        UnitType = null;
                    }
                }
                else
                {
                    UnitType = null;
                }
            }
        }
        
        [XmlAttribute("BlockType")]
        public string BlockTypeString
        {
            get { return BlockType.HasValue ? BlockType.Value.ToString() : null; }
            set 
            { 
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        BlockType = (BlockType)Enum.Parse(typeof(BlockType), value);
                    }
                    catch
                    {
                        BlockType = null;
                    }
                }
                else
                {
                    BlockType = null;
                }
            }
        }
        
        [XmlAttribute("DoodadType")]
        public string DoodadTypeString
        {
            get { return DoodadType.HasValue ? DoodadType.Value.ToString() : null; }
            set 
            { 
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        DoodadType = (SpawnableDoodadType)Enum.Parse(typeof(SpawnableDoodadType), value);
                    }
                    catch
                    {
                        DoodadType = null;
                    }
                }
                else
                {
                    DoodadType = null;
                }
            }
        }
        
        // Parameterless constructor required for XML serialization
        public LevelEditAction()
        {
        }
        
        // Constructor for unit spawn
        public static LevelEditAction CreateUnitSpawn(Vector3 position, UnitType unitType)
        {
            return new LevelEditAction
            {
                ActionType = LevelEditActionType.SpawnUnit,
                X = position.x,
                Y = position.y,
                UnitType = unitType
            };
        }
        
        // Constructor for unit destroy
        public static LevelEditAction CreateUnitDestroy(Vector3 position)
        {
            return new LevelEditAction
            {
                ActionType = LevelEditActionType.DestroyUnit,
                X = position.x,
                Y = position.y
            };
        }
        
        // Constructor for block spawn
        public static LevelEditAction CreateBlockSpawn(Vector3 position, BlockType blockType)
        {
            return new LevelEditAction
            {
                ActionType = LevelEditActionType.SpawnBlock,
                X = position.x,
                Y = position.y,
                BlockType = blockType
            };
        }
        
        // Constructor for block destroy
        public static LevelEditAction CreateBlockDestroy(Vector3 position)
        {
            return new LevelEditAction
            {
                ActionType = LevelEditActionType.DestroyBlock,
                X = position.x,
                Y = position.y
            };
        }
        
        // Constructor for doodad spawn
        public static LevelEditAction CreateDoodadSpawn(Vector3 position, SpawnableDoodadType doodadType)
        {
            return new LevelEditAction
            {
                ActionType = LevelEditActionType.SpawnDoodad,
                X = position.x,
                Y = position.y,
                DoodadType = doodadType
            };
        }
        
        // Constructor for teleport
        public static LevelEditAction CreateTeleport(Vector3 position)
        {
            return new LevelEditAction
            {
                ActionType = LevelEditActionType.Teleport,
                X = position.x,
                Y = position.y
            };
        }
        
        // Constructor for zipline
        public static LevelEditAction CreateZipline(Vector3 position1, Vector3 position2)
        {
            return new LevelEditAction
            {
                ActionType = LevelEditActionType.SpawnZipline,
                X = position1.x,
                Y = position1.y,
                X2 = position2.x,
                Y2 = position2.y
            };
        }
        
        // Constructor for mass delete
        public static LevelEditAction CreateMassDelete(Vector3 startPos, Vector3 endPos)
        {
            return new LevelEditAction
            {
                ActionType = LevelEditActionType.MassDelete,
                X = startPos.x,
                Y = startPos.y,
                X2 = endPos.x,
                Y2 = endPos.y
            };
        }
        
        // Execute the action
        public void Execute()
        {
            Vector3 position = new Vector3(X, Y, 0);
            
            switch (ActionType)
            {
                case LevelEditActionType.SpawnUnit:
                    if (UnitType.HasValue)
                        Main.SpawnUnit(UnitType.Value, position);
                    break;
                    
                case LevelEditActionType.DestroyUnit:
                    // Find and destroy unit at position
                    DestroyUnitAtPosition(position);
                    break;
                    
                case LevelEditActionType.SpawnBlock:
                    if (BlockType.HasValue)
                        Main.SpawnBlock(position, BlockType.Value);
                    break;
                    
                case LevelEditActionType.DestroyBlock:
                    // Find and destroy block at position
                    DestroyBlockAtPosition(position);
                    break;
                    
                case LevelEditActionType.SpawnDoodad:
                    if (DoodadType.HasValue)
                        Main.SpawnDoodad(position, DoodadType.Value);
                    break;
                    
                case LevelEditActionType.Teleport:
                    Main.TeleportToCoords(X, Y);
                    break;
                    
                case LevelEditActionType.SpawnZipline:
                    SpawnAndConnectZipline();
                    break;
                    
                case LevelEditActionType.MassDelete:
                    ExecuteMassDelete();
                    break;
            }
        }
        
        private void SpawnAndConnectZipline()
        {
            // Spawn first zipline point
            Vector3 position1 = new Vector3(X, Y, 0);
            GameObject firstPoint = Main.SpawnDoodad(position1, SpawnableDoodadType.ZiplinePoint);
            if (firstPoint == null) return;
            
            ZiplinePoint ziplinePoint1 = firstPoint.GetComponent<ZiplinePoint>();
            if (ziplinePoint1 == null) return;
            
            // Spawn second zipline point
            Vector3 position2 = new Vector3(X2, Y2, 0);
            GameObject secondPoint = Main.SpawnDoodad(position2, SpawnableDoodadType.ZiplinePoint);
            if (secondPoint == null) return;
            
            ZiplinePoint ziplinePoint2 = secondPoint.GetComponent<ZiplinePoint>();
            if (ziplinePoint2 == null) return;
            
            // Connect the two points
            ziplinePoint1.otherPoint = ziplinePoint2;
            ziplinePoint1.SetupZipline();
        }
        
        private void DestroyBlockAtPosition(Vector3 position)
        {
            // Find block at this position
            int blockX = Mathf.RoundToInt(position.x / 16f);
            int blockY = Mathf.RoundToInt(position.y / 16f);
            
            if (IsBackgroundBlock)
            {
                // Use Map's proper deletion method for background blocks
                Map.ClearBackgroundBlock(blockX, blockY);
            }
            else
            {
                // Use Map's proper deletion method for foreground blocks (handles all types including ladders and indestructible blocks)
                Map.ClearForegroundBlock(blockX, blockY);
            }
        }
        
        private void DestroyUnitAtPosition(Vector3 position)
        {
            // Find nearest unit within a small radius
            float searchRadius = 16f;
            Unit nearestUnit = null;
            float nearestDist = float.MaxValue;
            
            foreach (Unit unit in Map.units)
            {
                if (unit != null && !unit.destroyed && unit.health > 0)
                {
                    float dist = Vector3.Distance(unit.transform.position, position);
                    if (dist < searchRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestUnit = unit;
                    }
                }
            }
            
            if (nearestUnit != null)
            {
                // Kill the unit
                nearestUnit.Damage(9999, DamageType.Normal, 0, 0, 0, null, nearestUnit.X, nearestUnit.Y);
            }
        }
        
        private void ExecuteMassDelete()
        {
            // Get the context menu manager and execute mass delete
            var manager = UnityEngine.Object.FindObjectOfType<ContextMenuManager>();
            if (manager != null && manager.modes != null)
            {
                Vector3 startPos = new Vector3(X, Y, 0);
                Vector3 endPos = new Vector3(X2, Y2, 0);
                manager.modes.ExecuteMassDelete(startPos, endPos);
            }
        }
    }
    
    [Serializable]
    public class LevelEditRecord
    {
        [XmlAttribute]
        public string LevelKey { get; set; }
        
        [XmlArray("Actions")]
        [XmlArrayItem("Action")]
        public List<LevelEditAction> Actions { get; set; }
        
        public LevelEditRecord()
        {
            Actions = new List<LevelEditAction>();
        }
    }
}