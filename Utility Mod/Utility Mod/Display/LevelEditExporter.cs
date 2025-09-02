using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;

namespace Utility_Mod
{
    public static class LevelEditExporter
    {
        public static string ExportToString( LevelEditRecord record )
        {
            if ( record == null || record.Actions == null || record.Actions.Count == 0 )
            {
                return null;
            }

            try
            {
                // Serialize to XML (reusing existing XML serialization from LevelEditRecord)
                XmlSerializer serializer = new XmlSerializer( typeof( LevelEditRecord ) );

                using ( StringWriter writer = new StringWriter() )
                {
                    serializer.Serialize( writer, record );
                    string xml = writer.ToString();

                    // Convert to Base64
                    byte[] bytes = Encoding.UTF8.GetBytes( xml );
                    return Convert.ToBase64String( bytes );
                }
            }
            catch ( Exception ex )
            {
                Main.mod.Logger.Error( $"Failed to export level edits: {ex.Message}" );
                return null;
            }
        }

        public static LevelEditRecord ImportFromString( string encodedData )
        {
            if ( string.IsNullOrEmpty( encodedData ) )
            {
                return null;
            }

            try
            {
                // Decode from Base64
                byte[] bytes = Convert.FromBase64String( encodedData );
                string xml = Encoding.UTF8.GetString( bytes );

                // Deserialize from XML
                XmlSerializer serializer = new XmlSerializer( typeof( LevelEditRecord ) );

                using ( StringReader reader = new StringReader( xml ) )
                {
                    LevelEditRecord record = (LevelEditRecord)serializer.Deserialize( reader );

                    // Basic validation
                    if ( record != null && record.Actions != null )
                    {
                        // Remove any null actions
                        record.Actions.RemoveAll( a => a == null );

                        // Validate action types
                        foreach ( var action in record.Actions )
                        {
                            if ( !IsValidAction( action ) )
                            {
                                Main.mod.Logger.Warning( $"Skipping invalid action: {action.ActionType}" );
                                record.Actions.Remove( action );
                            }
                        }
                    }

                    return record;
                }
            }
            catch ( FormatException )
            {
                Main.mod.Logger.Error( "Invalid import data format - not valid Base64" );
                return null;
            }
            catch ( Exception ex )
            {
                Main.mod.Logger.Error( $"Failed to import level edits: {ex.Message}" );
                return null;
            }
        }

        private static bool IsValidAction( LevelEditAction action )
        {
            if ( action == null ) return false;

            // Check if action type is valid
            if ( !Enum.IsDefined( typeof( LevelEditActionType ), action.ActionType ) )
            {
                return false;
            }

            // Check for required parameters based on action type
            switch ( action.ActionType )
            {
                case LevelEditActionType.SpawnBlock:
                    return action.BlockType.HasValue;

                case LevelEditActionType.SpawnUnit:
                    return action.UnitType.HasValue;

                case LevelEditActionType.SpawnDoodad:
                    return action.DoodadType.HasValue;

                case LevelEditActionType.SpawnZipline:
                case LevelEditActionType.MassDelete:
                    // These need two points
                    return !float.IsNaN( action.X2 ) && !float.IsNaN( action.Y2 );

                default:
                    // Other actions just need position
                    return !float.IsNaN( action.X ) && !float.IsNaN( action.Y );
            }
        }

        public static void CopyToClipboard( string text )
        {
            if ( string.IsNullOrEmpty( text ) )
            {
                return;
            }

            try
            {
                GUIUtility.systemCopyBuffer = text;
                Main.mod.Logger.Log( "Level edits copied to clipboard" );
            }
            catch ( Exception ex )
            {
                Main.mod.Logger.Error( $"Failed to copy to clipboard: {ex.Message}" );
            }
        }

        public static string GetFromClipboard()
        {
            try
            {
                return GUIUtility.systemCopyBuffer;
            }
            catch ( Exception ex )
            {
                Main.mod.Logger.Error( $"Failed to read from clipboard: {ex.Message}" );
                return null;
            }
        }

        public static string GetExportSummary( LevelEditRecord record )
        {
            if ( record == null || record.Actions == null )
            {
                return "No actions to export";
            }

            // Count action types
            Dictionary<LevelEditActionType, int> counts = new Dictionary<LevelEditActionType, int>();
            foreach ( var action in record.Actions )
            {
                if ( !counts.ContainsKey( action.ActionType ) )
                {
                    counts[action.ActionType] = 0;
                }
                counts[action.ActionType]++;
            }

            // Build summary
            StringBuilder summary = new StringBuilder();
            summary.AppendLine( $"Level: {record.LevelKey ?? "Unknown"}" );
            summary.AppendLine( $"Total Actions: {record.Actions.Count}" );

            foreach ( var kvp in counts )
            {
                summary.AppendLine( $"  {kvp.Key}: {kvp.Value}" );
            }

            return summary.ToString();
        }
    }
}