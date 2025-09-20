using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using World.LevelEdit.Triggers;

namespace Custom_Triggers_Mod
{
    public class HarmonyPatches
    {
        [HarmonyPatch( typeof( TriggerFactory ), "CreateAction" )]
        static class TriggerFactory_CreateAction_Patch
        {
            public static bool Prefix( ref TriggerActionInfo info, ref TriggerAction __result )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                Main.Log( "info name: " + info.name );
                if ( info.name == "SPECIALSTRING" )
                {
                    Main.Log( "overriding trigger action" );
                    try
                    {
                        __result = new TestCustomTriggerAction();

                        __result.Info = info;
                        __result.timeOffsetLeft = info.timeOffset;
                        __result.AssignDeterministicIDs();
                    }
                    catch ( Exception ex )
                    {
                        Main.Log( "failed creating custom trigger action: " + ex.ToString() );
                    }

                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( LevelEditorGUI ), "ShowTriggerMenu" )]
        static class LevelEditorGUI_ShowTriggerMenu_Patch
        {
            public static void PlayClickSound( LevelEditorGUI __instance )
            {
                if ( __instance != null && __instance.clickSound != null )
                {
                    Sound.GetInstance().PlaySoundEffect( __instance.clickSound, 0.3f, 0.9f + global::UnityEngine.Random.value * 0.2f );
                }
            }

            public static bool Prefix( LevelEditorGUI __instance, ref Vector2 ___scrollPos, ref TriggerInfo ___selectedTrigger, ref MapData ___mapData, ref TriggerActionInfo ___selectedAction, ref SelectionIndicator[] ___targetSelectionIndicatorOthers, ref bool ___movingAction, ref List<TriggerType> ___triggerTypes )
            {
                if ( !Main.enabled )
                {
                    return true;
                }

                ___scrollPos = GUILayout.BeginScrollView( ___scrollPos, LevelEditorGUI.GetGuiSkin().scrollView );
                GUILayout.BeginHorizontal( new GUILayoutOption[0] );
                if ( GUILayout.Button( "New Trigger", new GUILayoutOption[0] ) )
                {
                    PlayClickSound( __instance );
                    ___selectedTrigger = new TriggerInfo();
                    ___mapData.TriggerList.Add( ___selectedTrigger );
                }
                if ( ___selectedTrigger != null )
                {
                    if ( GUILayout.Button( "Deselect Trigger", new GUILayoutOption[0] ) )
                    {
                        PlayClickSound( __instance );
                        ___selectedTrigger = null;
                        ___selectedAction = null;
                    }
                    if ( GUILayout.Button( "Delete", new GUILayoutOption[0] ) )
                    {
                        PlayClickSound( __instance );
                        ___mapData.TriggerList.Remove( ___selectedTrigger );
                        ___selectedTrigger = null;
                    }
                }
                GUILayout.EndHorizontal();
                if ( ___selectedTrigger == null )
                {
                    if ( ___mapData.TriggerList != null )
                    {
                        foreach ( TriggerInfo triggerInfo in ___mapData.TriggerList )
                        {
                            if ( GUILayout.Button( ( triggerInfo.name ?? "Unnamed" ) + " (" + triggerInfo.type.ToString() + ")", new GUILayoutOption[0] ) )
                            {
                                PlayClickSound( __instance );
                                ___selectedTrigger = triggerInfo;
                            }
                        }
                    }
                    GUILayout.Label( "Entity Triggers:", new GUILayoutOption[0] );
                    if ( ___mapData.entityTriggers != null )
                    {
                        foreach ( TriggerInfo triggerInfo2 in ___mapData.entityTriggers )
                        {
                            if ( GUILayout.Button( ( triggerInfo2.name ?? "Unnamed" ) + " (" + triggerInfo2.type.ToString() + ")", new GUILayoutOption[0] ) )
                            {
                                PlayClickSound( __instance );
                                ___selectedTrigger = triggerInfo2;
                            }
                        }
                    }
                }
                else
                {
                    GUILayout.BeginHorizontal( new GUILayoutOption[0] );
                    GUILayout.Label( "Editing Trigger:", new GUILayoutOption[0] );
                    ___selectedTrigger.name = GUILayout.TextField( ___selectedTrigger.name ?? string.Empty, new GUILayoutOption[0] );
                    GUILayout.EndHorizontal();
                    ___selectedTrigger.startEnabled = GUILayout.Toggle( ___selectedTrigger.startEnabled, "Start Enabled", new GUILayoutOption[0] );
                    if ( ___selectedAction == null )
                    {
                        GUILayout.BeginHorizontal( new GUILayoutOption[0] );
                        if ( GUILayout.Button( "Deselect Trigger", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            ___selectedTrigger = null;
                            ___selectedAction = null;
                            return false;
                        }
                        if ( GUILayout.Button( "Delete Trigger", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            ___mapData.TriggerList.Remove( ___selectedTrigger );
                            ___selectedTrigger = null;
                            return false;
                        }
                        GUILayout.EndHorizontal();
                        ___selectedTrigger.onlyTriggerOnce = GUILayout.Toggle( ___selectedTrigger.onlyTriggerOnce, "Only Trigger Once Per Play Session\n (for cutscenes) - please name the trigger", new GUILayoutOption[0] );
                        GUILayout.Label( "Type:", new GUILayoutOption[0] );
                        ___selectedTrigger.type = (TriggerType)LevelEditorGUI.SelectList( ___triggerTypes, ___selectedTrigger.type, LevelEditorGUI.GetGuiSkin().customStyles[0], LevelEditorGUI.GetGuiSkin().customStyles[1] );
                        if ( ___selectedTrigger.type == TriggerType.Area || ___selectedTrigger.type == TriggerType.CheckTerrain || ___selectedTrigger.type == TriggerType.OnScreen || ___selectedTrigger.type == TriggerType.CheckEnemies )
                        {
                            if ( ___selectedTrigger.bottomLeft == null )
                            {
                                ___selectedTrigger.bottomLeft = new GridPoint( 0, 0 );
                            }
                            if ( ___selectedTrigger.upperRight == null )
                            {
                                ___selectedTrigger.upperRight = new GridPoint( 0, 0 );
                            }
                            GUILayout.Label( string.Concat( new object[]
                            {
                        "Area: ",
                        ___selectedTrigger.bottomLeft.collumn,
                        ", ",
                        ___selectedTrigger.bottomLeft.row,
                        " -> ",
                        ___selectedTrigger.upperRight.collumn,
                        ", ",
                        ___selectedTrigger.upperRight.row
                            } ), new GUILayoutOption[0] );
                            GUILayout.Label( "Hold LEFT SHIFT and drag to select area", new GUILayoutOption[0] );
                        }
                        else if ( ___selectedTrigger.type == TriggerType.Variable )
                        {
                            GUILayout.Label( "Variable Identifier:", new GUILayoutOption[0] );
                            ___selectedTrigger.variableName = GUILayout.TextField( ___selectedTrigger.variableName, 24, new GUILayoutOption[0] );
                            if ( ___selectedTrigger.evaluateOnSmallerThan )
                            {
                                if ( GUILayout.Button( "Trigger When Smaller or Equal Than:", new GUILayoutOption[0] ) )
                                {
                                    ___selectedTrigger.evaluateOnSmallerThan = false;
                                }
                            }
                            else if ( GUILayout.Button( "Trigger When Greater Or Equal Than:", new GUILayoutOption[0] ) )
                            {
                                ___selectedTrigger.evaluateOnSmallerThan = true;
                            }
                            float.TryParse( GUILayout.TextField( ___selectedTrigger.evaluateAgainstValue.ToString( "0.00" ), new GUILayoutOption[0] ), out ___selectedTrigger.evaluateAgainstValue );
                        }
                        else if ( ___selectedTrigger.type == TriggerType.Entity )
                        {
                            GUILayout.Label( "This trigger will be activated when ANY of the doodads tagged with this same tag are killed/destroyed", new GUILayoutOption[0] );
                            GUILayout.BeginHorizontal( new GUILayoutOption[0] );
                            ___selectedTrigger.tag = GUILayout.TextField( ___selectedTrigger.tag ?? string.Empty, new GUILayoutOption[0] );
                            GUILayout.EndHorizontal();
                        }
                        else if ( ___selectedTrigger.type == TriggerType.EnemyDeath )
                        {
                            GUILayout.Label( "Enemy Death Frequency (how many deaths before triggering):", new GUILayoutOption[0] );
                            int.TryParse( GUILayout.TextField( ___selectedTrigger.enemyDeathFrequency.ToString(), new GUILayoutOption[0] ), out ___selectedTrigger.enemyDeathFrequency );
                        }
                        bool flag = GUILayout.Toggle( ___selectedTrigger.useDefaultBrotality, "Use Default Brotality to Limit Trigger: ", new GUILayoutOption[0] );
                        bool flag2 = GUILayout.Toggle( ___selectedTrigger.useCustomBrotality, "Use Custom Brotality to Limit Trigger: ", new GUILayoutOption[0] );
                        if ( flag2 && !___selectedTrigger.useCustomBrotality )
                        {
                            ___selectedTrigger.useDefaultBrotality = false;
                            ___selectedTrigger.useCustomBrotality = true;
                        }
                        else if ( flag && !___selectedTrigger.useDefaultBrotality )
                        {
                            ___selectedTrigger.useDefaultBrotality = true;
                            ___selectedTrigger.useCustomBrotality = false;
                        }
                        else if ( !flag && !flag2 )
                        {
                            ___selectedTrigger.useDefaultBrotality = false;
                            ___selectedTrigger.useCustomBrotality = false;
                        }
                        if ( ___selectedTrigger.useCustomBrotality )
                        {
                            GUILayout.Label( "Brotality must be at least at level: (0 for always) to trigger", new GUILayoutOption[0] );
                            int.TryParse( GUILayout.TextField( ___selectedTrigger.minBrotalityLevel.ToString(), new GUILayoutOption[0] ), out ___selectedTrigger.minBrotalityLevel );
                        }
                    }
                    for ( int i = 0; i < ___targetSelectionIndicatorOthers.Length; i++ )
                    {
                        if ( ___selectedTrigger.actions.Count > i )
                        {
                            BombardmentActionInfo bombardmentActionInfo = ___selectedTrigger.actions[i] as BombardmentActionInfo;
                            if ( bombardmentActionInfo != null && bombardmentActionInfo.targetPoint.row > 0 && bombardmentActionInfo.targetPoint.collumn > 0 )
                            {
                                ___targetSelectionIndicatorOthers[i].HighlightSquare( bombardmentActionInfo.targetPoint.row, bombardmentActionInfo.targetPoint.row, bombardmentActionInfo.targetPoint.collumn, bombardmentActionInfo.targetPoint.collumn, ( ___selectedAction != ___selectedTrigger.actions[i] ) ? Color.blue : Color.red, false );
                            }
                        }
                        else
                        {
                            ___targetSelectionIndicatorOthers[i].UnHighlightSquare();
                        }
                    }
                    if ( ___movingAction )
                    {
                        if ( ___selectedTrigger == null || ___selectedAction == null )
                        {
                            ___movingAction = false;
                        }
                        GUILayout.Label( "Move Action To:", new GUILayoutOption[0] );
                        foreach ( TriggerInfo triggerInfo3 in ___mapData.TriggerList )
                        {
                            if ( GUILayout.Button( ( triggerInfo3.name ?? "Unnamed" ) + " (" + triggerInfo3.type.ToString() + ")", new GUILayoutOption[0] ) )
                            {
                                PlayClickSound( __instance );
                                ___selectedTrigger.actions.Remove( ___selectedAction );
                                triggerInfo3.actions.Add( ___selectedAction );
                                ___selectedAction = null;
                                ___movingAction = false;
                            }
                        }
                    }
                    else if ( ___selectedAction == null )
                    {
                        if ( GUILayout.Button( "Add New Camera Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            CameraActionInfo cameraActionInfo = new CameraActionInfo();
                            cameraActionInfo.type = TriggerActionType.CameraMove;
                            ___selectedTrigger.actions.Add( cameraActionInfo );
                            ___selectedAction = cameraActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Splosion Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            ExplosionActionInfo explosionActionInfo = new ExplosionActionInfo();
                            explosionActionInfo.type = TriggerActionType.Explosion;
                            ___selectedTrigger.actions.Add( explosionActionInfo );
                            ___selectedAction = explosionActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Collapse Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            CollapseActionInfo collapseActionInfo = new CollapseActionInfo();
                            collapseActionInfo.type = TriggerActionType.Collapse;
                            ___selectedTrigger.actions.Add( collapseActionInfo );
                            ___selectedAction = collapseActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Burn Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            BurnActionInfo burnActionInfo = new BurnActionInfo();
                            burnActionInfo.type = TriggerActionType.Burn;
                            ___selectedTrigger.actions.Add( burnActionInfo );
                            ___selectedAction = burnActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Spawn Resource Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            SpawnResourceActionInfo spawnResourceActionInfo = new SpawnResourceActionInfo();
                            spawnResourceActionInfo.type = TriggerActionType.SpawnResource;
                            ___selectedTrigger.actions.Add( spawnResourceActionInfo );
                            ___selectedAction = spawnResourceActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Spawn Mooks Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            SpawnMooksActionInfo spawnMooksActionInfo = new SpawnMooksActionInfo();
                            spawnMooksActionInfo.type = TriggerActionType.SpawnMooks;
                            ___selectedTrigger.actions.Add( spawnMooksActionInfo );
                            ___selectedAction = spawnMooksActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Spawn Aliens Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            AlienSpawnActionInfo alienSpawnActionInfo = new AlienSpawnActionInfo();
                            alienSpawnActionInfo.type = TriggerActionType.AlienSpawn;
                            ___selectedTrigger.actions.Add( alienSpawnActionInfo );
                            ___selectedAction = alienSpawnActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Bombardment Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            BombardmentActionInfo bombardmentActionInfo2 = new BombardmentActionInfo();
                            bombardmentActionInfo2.type = TriggerActionType.Bombardment;
                            ___selectedTrigger.actions.Add( bombardmentActionInfo2 );
                            ___selectedAction = bombardmentActionInfo2;
                        }
                        if ( GUILayout.Button( "Add New Level Event Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            LevelEventActionInfo levelEventActionInfo = new LevelEventActionInfo();
                            levelEventActionInfo.type = TriggerActionType.LevelEvent;
                            ___selectedTrigger.actions.Add( levelEventActionInfo );
                            ___selectedAction = levelEventActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Spawn Block Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            SpawnBlockActionInfo spawnBlockActionInfo = new SpawnBlockActionInfo();
                            spawnBlockActionInfo.type = TriggerActionType.SpawnBlock;
                            ___selectedTrigger.actions.Add( spawnBlockActionInfo );
                            ___selectedAction = spawnBlockActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Variable Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            VariableActionInfo variableActionInfo = new VariableActionInfo();
                            variableActionInfo.type = TriggerActionType.Variable;
                            ___selectedTrigger.actions.Add( variableActionInfo );
                            ___selectedAction = variableActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Weather Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            WeatherActionInfo weatherActionInfo = new WeatherActionInfo();
                            weatherActionInfo.type = TriggerActionType.Weather;
                            ___selectedTrigger.actions.Add( weatherActionInfo );
                            ___selectedAction = weatherActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Character Command Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            CharacterActionInfo characterActionInfo = new CharacterActionInfo();
                            characterActionInfo.type = TriggerActionType.Character;
                            ___selectedTrigger.actions.Add( characterActionInfo );
                            ___selectedAction = characterActionInfo;
                        }
                        if ( ( Application.isEditor || LevelEditorGUI.hackedEditorOn ) && GUILayout.Button( "Add New Execute Function Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            ExecuteFunctionActionInfo executeFunctionActionInfo = new ExecuteFunctionActionInfo();
                            executeFunctionActionInfo.type = TriggerActionType.ExecuteFunction;
                            ___selectedTrigger.actions.Add( executeFunctionActionInfo );
                            ___selectedAction = executeFunctionActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Bro Change Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            BroChangeActionInfo broChangeActionInfo = new BroChangeActionInfo();
                            broChangeActionInfo.type = TriggerActionType.BroChange;
                            ___selectedTrigger.actions.Add( broChangeActionInfo );
                            ___selectedAction = broChangeActionInfo;
                        }
                        if ( GUILayout.Button( "Add Mammoth Kopter Command", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            MammothKopterCommandActionInfo mammothKopterCommandActionInfo = new MammothKopterCommandActionInfo();
                            mammothKopterCommandActionInfo.type = TriggerActionType.MammothKopterCommand;
                            ___selectedTrigger.actions.Add( mammothKopterCommandActionInfo );
                            ___selectedAction = mammothKopterCommandActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Change Available Bros Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            ChangeAvailableBrosActionInfo changeAvailableBrosActionInfo = new ChangeAvailableBrosActionInfo();
                            changeAvailableBrosActionInfo.type = TriggerActionType.ChangeAvailableBros;
                            ___selectedTrigger.actions.Add( changeAvailableBrosActionInfo );
                            ___selectedAction = changeAvailableBrosActionInfo;
                        }
                        if ( GUILayout.Button( "Add Protect Area From Replayed Destruction Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            ProtectAreaFromReplayedDestructionActionInfo protectAreaFromReplayedDestructionActionInfo = new ProtectAreaFromReplayedDestructionActionInfo();
                            protectAreaFromReplayedDestructionActionInfo.type = TriggerActionType.ProtectAreaFromReplayedDestruction;
                            ___selectedTrigger.actions.Add( protectAreaFromReplayedDestructionActionInfo );
                            ___selectedAction = protectAreaFromReplayedDestructionActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Sandstorm Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            SandStormActionInfo sandStormActionInfo = new SandStormActionInfo();
                            sandStormActionInfo.type = TriggerActionType.Sandstorm;
                            ___selectedTrigger.actions.Add( sandStormActionInfo );
                            ___selectedAction = sandStormActionInfo;
                        }
                        if ( GUILayout.Button( "Add New Rogueforce Bombardment Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            RogueforceBombardmentActionInfo rogueforceBombardmentActionInfo = new RogueforceBombardmentActionInfo();
                            rogueforceBombardmentActionInfo.type = TriggerActionType.RogueforceBombardmentChange;
                            ___selectedTrigger.actions.Add( rogueforceBombardmentActionInfo );
                            ___selectedAction = rogueforceBombardmentActionInfo;
                        }

                        GUILayout.Label( "Current Actions:", new GUILayoutOption[0] );
                        int num = 1;
                        foreach ( TriggerActionInfo triggerActionInfo in ___selectedTrigger.actions )
                        {
                            GUILayout.BeginHorizontal( new GUILayoutOption[0] );
                            string text = num.ToString() + ":";
                            if ( !string.IsNullOrEmpty( triggerActionInfo.name ) )
                            {
                                string text2 = text;
                                text = string.Concat( new string[]
                                {
                            text2,
                            triggerActionInfo.name,
                            " (",
                            triggerActionInfo.ToString(),
                            ")"
                                } );
                            }
                            else
                            {
                                text += triggerActionInfo.ToString();
                            }
                            GUILayout.Label( text, new GUILayoutOption[0] );
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal( new GUILayoutOption[0] );
                            GUILayout.Label( "Delay:", new GUILayoutOption[0] );
                            float.TryParse( GUILayout.TextField( triggerActionInfo.timeOffset.ToString( "0.00" ), new GUILayoutOption[0] ), out triggerActionInfo.timeOffset );
                            if ( GUILayout.Button( "Edit", new GUILayoutOption[0] ) )
                            {
                                PlayClickSound( __instance );
                                ___selectedAction = triggerActionInfo;
                            }
                            GUILayout.EndHorizontal();
                        }
                        num++;
                    }
                    else
                    {
                        GUILayout.BeginHorizontal( new GUILayoutOption[0] );
                        GUILayout.Label( "Editing Action: ", new GUILayoutOption[0] );
                        ___selectedAction.name = GUILayout.TextField( ___selectedAction.name ?? string.Empty, new GUILayoutOption[0] );
                        GUILayout.EndHorizontal();
                        GUILayout.Label( "Editing " + ___selectedAction.type + " action", new GUILayoutOption[0] );
                        ___selectedAction.onlyOnHardMode = GUILayout.Toggle( ___selectedAction.onlyOnHardMode, "Only On Hard Mode", new GUILayoutOption[0] );
                        GUILayout.BeginHorizontal( new GUILayoutOption[0] );
                        if ( GUILayout.Button( "Deselect Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            ___selectedAction = null;
                        }
                        if ( GUILayout.Button( "Delete Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            ___selectedTrigger.actions.Remove( ___selectedAction );
                            ___selectedAction = null;
                        }
                        if ( GUILayout.Button( "Move Action", new GUILayoutOption[0] ) )
                        {
                            PlayClickSound( __instance );
                            ___movingAction = true;
                        }
                        GUILayout.EndHorizontal();
                        if ( ___selectedAction != null )
                        {
                            ___selectedAction.ShowGUI( __instance );
                        }
                    }
                }
                GUILayout.EndScrollView();

                return false;
            }
        }


    }
}
