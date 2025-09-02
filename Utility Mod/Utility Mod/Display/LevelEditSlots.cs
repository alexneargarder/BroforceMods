using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace Utility_Mod
{
    [Serializable]
    public class LevelEditSlots
    {
        [XmlArray( "Slots" )]
        [XmlArrayItem( "Slot" )]
        public List<LevelEditSlot> SlotsList { get; set; }

        // Dictionary for easier access, not serialized
        [XmlIgnore]
        private Dictionary<string, LevelEditRecord> slotsDict;

        [XmlIgnore]
        public Dictionary<string, LevelEditRecord> Slots
        {
            get
            {
                if ( slotsDict == null )
                {
                    RebuildDictionary();
                }
                return slotsDict;
            }
        }

        public LevelEditSlots()
        {
            SlotsList = new List<LevelEditSlot>();
            slotsDict = new Dictionary<string, LevelEditRecord>();
        }

        public void RebuildDictionary()
        {
            slotsDict = new Dictionary<string, LevelEditRecord>();
            if ( SlotsList != null )
            {
                foreach ( var slot in SlotsList )
                {
                    if ( slot != null && !string.IsNullOrEmpty( slot.Name ) )
                    {
                        slotsDict[slot.Name] = slot.Record;
                    }
                }
            }
        }

        public void SyncToList()
        {
            SlotsList.Clear();
            foreach ( var kvp in slotsDict )
            {
                SlotsList.Add( new LevelEditSlot( kvp.Key, kvp.Value ) );
            }
        }

        public void SaveSlot( string slotName, LevelEditRecord record )
        {
            if ( !string.IsNullOrEmpty( slotName ) && record != null )
            {
                // Create a deep copy of the record
                var copiedRecord = new LevelEditRecord();
                copiedRecord.LevelKey = record.LevelKey;
                copiedRecord.Actions = new List<LevelEditAction>( record.Actions );

                Slots[slotName] = copiedRecord;
                SyncToList();
            }
        }

        public LevelEditRecord LoadSlot( string slotName )
        {
            if ( Slots.ContainsKey( slotName ) )
            {
                // Return a deep copy
                var original = Slots[slotName];
                var copy = new LevelEditRecord();
                copy.LevelKey = original.LevelKey;
                copy.Actions = new List<LevelEditAction>( original.Actions );
                return copy;
            }
            return null;
        }

        public bool DeleteSlot( string slotName )
        {
            if ( Slots.ContainsKey( slotName ) )
            {
                Slots.Remove( slotName );
                SyncToList();
                return true;
            }
            return false;
        }
    }

    [Serializable]
    public class LevelEditSlot
    {
        [XmlAttribute( "name" )]
        public string Name { get; set; }

        [XmlElement( "Record" )]
        public LevelEditRecord Record { get; set; }

        public LevelEditSlot()
        {
            // Parameterless constructor required for XML serialization
        }

        public LevelEditSlot( string name, LevelEditRecord record )
        {
            Name = name;
            Record = record;
        }
    }
}