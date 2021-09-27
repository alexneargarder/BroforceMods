/**
 * TODO
 * 
 * Make helicopter skip go in order
 * 
 * Set living, dead, and locked bros with cheat mod
 * 
 * Set lives with cheat mod ( for normal campaign)
 * 
 * Make campaign progress correctly when jumping to a level via cheat mod
 * 
 * Investigate if cut scenes destroy mod manager window
 * 
 * Add unlock all territories button
 * 
**/
/**
 * IDEAS
 * 
 * change sprint speed
 * 
 * give lives (for practicing iron bro with fren)
 * 
 * give infinite specials maybe
 * 
 * summon mech anywhere
 * 
 * infinite fuel mech
 * 
 * bind teleport to some controller key
 * 
 * buff slowdown time (pause time?)
 * 
 * 
**/
/**
 * DONE
 * 
 * Play different type of campaign in arcade
 * 
 * Set level to repeat with cheat mod
 * 
 * Figure out better way to get instance of map controller (doesn't work sometimes if exiting to menu through options)
 * 
 * 
**/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;

namespace Utility_Mod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        public static Dropdown campaignNum;
        public static int lastCampaignNum;
        public static Dropdown levelNum;
        public static int lastCampaignCompleted = -1;
        public static string[] levelList = new string[] { "Level 1", "Level 2", "Level 3", "Level 4", "Level 5", "Level 6", "Level 7", "Level 8", "Level 9", "Level 10",
            "Level 11", "Level 12", "Level 13", "Level 14", "Level 15"};
        public static string[] terrainList = new string[]
        {
            "vietnamterrain",
            "cambodiaterrain",
            "indonesiaterrain",
            "hongkongterrain",
            "indiaterrain",
            "philippinesterrain",
            "southkoreaterrain",
            "newguineaterrain",
            "kazakhstanterrain",
            "ukraineterrain",
            "panamaterrain",
            "congoterrain",
            "hawaiiterrain",
            "amazonterrain",
            "hellterrain",
            "usaterrain",
            "ellenchallengeterrain",
            "bombardchallengeterrain",
            "ammochallengeterrain",
            "dashchallengeterrain",
            "mechchallengeterrain",
            "macbroverchallengeterrain",
            "timebrochallengeterrain"
        };

        public static string[] campaignList = new string[]
        {
            "VIETMAN",
            "CAMBODIA",
            "INDONESIA",
            "HONG KONG",
            "INDIA",
            "PHILIPPINES",
            "SOUTH KOREA",
            "NEW GUINEA",
            "KAZAKHSTAN",
            "UKRAINE",
            "PANAMA",
            "DEM. REP. OF CONGO",
            "HAWAII",
            "THE AMAZON RAINFOREST",
            "UNITED STATES OF AMERICA",
            "WHITE HOUSE",
            "Alien Challenge",
            "Bombardment Challenge",
            "Ammo Challenge",
            "Dash Challenge",
            "Mech Challenge",
            "MacBrover Challenge",
            "Time Bro Challenge"
        };

        public static Dropdown TypeOfBuildNum;
        public static string[] TypeOfBuild = new string[] { "Online", "Expendabros", "TWITCHCON", "AlienDemo", "BossRush" };
        public static string CurrentBuild;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUpdate = OnUpdate;
            settings = Settings.Load<Settings>(modEntry);
            
            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);
            mod = modEntry;

            lastCampaignNum = -1;
            campaignNum = new Dropdown(125, 150, 200, 300, new string[] { "Campaign 1", "Campaign 2", "Campaign 3", "Campaign 4", "Campaign 5",
                "Campaign 6", "Campaign 7", "Campaign 8", "Campaign 9", "Campaign 10", "Campaign 11", "Campaign 12", "Campaign 13", "Campaign 14", "Campaign 15",
                "White House", "Alien Challenge", "Bombardment Challenge", "Ammo Challenge", "Dash Challenge", "Mech Challenge", "MacBrover Challenge", "Time Bro Challenge"}, settings.campaignNum);


            levelNum = new Dropdown(400, 150, 150, 300, levelList, settings.levelNum);

            TypeOfBuildNum = new Dropdown(100, 150, 150, 150, new string[] { "Normal Campaign", "Expendabros Campaign", "TwitchCon build", "Alien Demo", "Boss Rush Campaign" }, settings.BuildCampaignNum);

            CurrentBuild = TypeOfBuild[settings.BuildCampaignNum];

            return true;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            CurrentBuild = TypeOfBuild[TypeOfBuildNum.indexNumber];
            settings.BuildCampaignNum = TypeOfBuildNum.indexNumber;

            // Patch for avoiding the cursor of being block in Level Editor
            if (!LevelEditorGUI.IsActive) ShowMouseController.ShowMouse = false;
            Cursor.lockState = CursorLockMode.None;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            string previousToolTip;
            GUILayout.BeginHorizontal();
            {
                settings.cameraShake = GUILayout.Toggle(settings.cameraShake, new GUIContent("Camera Shake",
                    "Disable this to have camera shake automatically set to 0 at the start of a level"), GUILayout.Width(100f));

                Rect lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 500;

                settings.enableSkip = GUILayout.Toggle(settings.enableSkip, new GUIContent("Helicopter Skip",
                    "Skips helicopter on world map and immediately takes you into a level"), GUILayout.Width(200f));

                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();


            GUILayout.Space(25);


            GUILayout.BeginHorizontal();
            {
                settings.loopCurrent = GUILayout.Toggle(settings.loopCurrent, new GUIContent("Loop Current Level", "After beating a level you replay the current one instead of moving on"), GUILayout.ExpandWidth(false));

                Rect lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 300;

                settings.disableConfirm = GUILayout.Toggle(settings.disableConfirm, new GUIContent("Fix Mod Window Disappearing",
                    "Disables confirmation screen when restarting or returning to map/menu"), GUILayout.ExpandWidth(false));

                if (GUI.tooltip != previousToolTip)
                {
                    GUI.Label(lastRect, GUI.tooltip);
                    previousToolTip = GUI.tooltip;
                }
                
            }
            GUILayout.EndHorizontal();


            GUILayout.Space(25);


            GUILayout.BeginHorizontal(new GUILayoutOption[] { GUILayout.MinHeight(350), GUILayout.ExpandWidth(false) });

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            var warnStyle = new GUIStyle();
            warnStyle.normal.textColor = Color.yellow;
            warnStyle.fontStyle = FontStyle.Bold;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();

            TypeOfBuildNum.OnGUI(modEntry);
            settings.BuildCampaignNum = TypeOfBuildNum.indexNumber;

            if(CurrentBuild == "Online")
            {
                campaignNum.OnGUI(modEntry);

                determineLevelsInCampaign();

                levelNum.OnGUI(modEntry);

                Main.settings.levelNum = levelNum.indexNumber;

                if (GUILayout.Button(new GUIContent("Go to level", "This only works on the world map screen"), GUILayout.Width(100)))
                {
                    GoToLevel();
                }
            }

            if (GUI.tooltip != previousToolTip)
            {
                Rect lastRect = campaignNum.dropDownRect;
                lastRect.y += 20;
                lastRect.width += 300;

                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(25);

            GUILayout.BeginHorizontal();

            GUILayout.Space(1);

            if (!campaignNum.show && !levelNum.show && !TypeOfBuildNum.show && CurrentBuild == "Online")
            {

                if (GUILayout.Button(new GUIContent("Previous Level", "This only works in game"), new GUILayoutOption[] { GUILayout.Width(150), GUILayout.ExpandWidth(false) }))
                {
                    ChangeLevel(-1);
                }

                Rect lastRect = GUILayoutUtility.GetLastRect();
                lastRect.y += 20;
                lastRect.width += 300;

                if (GUILayout.Button(new GUIContent("Next Level", "This only works in game"), new GUILayoutOption[] { GUILayout.Width(150), GUILayout.ExpandWidth(false) }))
                {
                    ChangeLevel(1);
                }

                if (GUI.tooltip != previousToolTip)
                {
                    GUI.Label(lastRect, GUI.tooltip);
                    previousToolTip = GUI.tooltip;
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();


            GUILayout.EndHorizontal();

        }


        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            return true;
        }

        static void determineLevelsInCampaign()
        {
            Main.settings.campaignNum = campaignNum.indexNumber;

            if (lastCampaignNum != campaignNum.indexNumber)
            {
                int actualCampaignNum = campaignNum.indexNumber + 1;
                int numberOfLevels = 4;
                if ((actualCampaignNum >= 4 && actualCampaignNum <= 5) || (actualCampaignNum == 7))
                {
                    numberOfLevels = 3;
                }
                else if ((actualCampaignNum >= 1 && actualCampaignNum <= 3) || (actualCampaignNum == 6) || (actualCampaignNum >= 8 && actualCampaignNum <= 10))
                {
                    numberOfLevels = 4;
                }
                else if (actualCampaignNum >= 12 && actualCampaignNum <= 14)
                {
                    numberOfLevels = 5;
                }
                else if (actualCampaignNum == 11)
                {
                    numberOfLevels = 6;
                }
                else if (actualCampaignNum == 15)
                {
                    numberOfLevels = 14;
                }
                else
                {
                    numberOfLevels = 1;
                }

                if (levelNum.indexNumber + 1 > numberOfLevels)
                {
                    levelNum.indexNumber = numberOfLevels - 1;
                }
                levelNum = new Dropdown(400, 150, 125, 300, levelList.Take(numberOfLevels).ToArray(), levelNum.indexNumber, levelNum.show);
            }
            lastCampaignNum = campaignNum.indexNumber;
        }

        static void GoToLevel()
        {

            string terrainName = "vietmanterrain";
            string territoryName = "VIETMAN";

            territoryName = campaignList[campaignNum.indexNumber];
            terrainName = terrainList[campaignNum.indexNumber];

            WorldMapController_AddAction_Patch.GoToLevel(territoryName, levelNum.indexNumber, terrainName);
        }

        static void ChangeLevel(int levelNum)
        {
            // GameModeController instance = Traverse.Create(typeof(GameModeController)).Field("instance").GetValue() as GameModeController;

            LevelSelectionController.CurrentLevelNum += levelNum;

            Map.ClearSuperCheckpointStatus();
            GameModeController.RestartLevel();
        }

        public static void Log(String str)
        {
            mod.Logger.Log(str);
        }
    }

    [HarmonyPatch(typeof(WorldMapController), "ProcessNextAction")]
    static class WorldMapController_ProcessNextAction_Patch
    {
        static bool Prefix(WorldMapController __instance)
        {
            if (!Main.enabled)
                return true;
            if (!Main.settings.cameraShake)
            {
                PlayerOptions.Instance.cameraShakeAmount = 0f;
            }
            if (!Main.settings.enableSkip)
            {
                return true;
            }

            Traverse actionQueueTraverse = Traverse.Create(__instance);
            List<WorldMapController.QueuedAction> actionQueue = actionQueueTraverse.Field("actionQueue").GetValue() as List<WorldMapController.QueuedAction>;
            /*for (int i = 0; i < actionQueue.Count; i++)
            {
                Main.mod.Logger.Log(actionQueue[i].ToString());
            }
            if (actionQueue.Count == 0 || actionQueue == null)
            {
                Main.mod.Logger.Log("NO ACTIONS");
            }*/

            if (actionQueue.Count > 0)
            {

                WorldMapController.QueuedAction queuedAction = actionQueue[0];
                switch (queuedAction.actionType)
                {
                    case WorldMapController.QueuedActions.Helicopter:
                        WorldCamera.instance.MoveToHelicopter(0f);
                        break;
                    case WorldMapController.QueuedActions.Terrorist:
                        string lastSafe = WorldMapProgressController.currentProgress.lastSafeTerritory;



                        queuedAction.territory.BecomeEnemyBase();
                        WorldMapController.RestTransport(queuedAction.territory);
                        WorldMapController.EnterMission(queuedAction.territory.GetCampaignName(), queuedAction.territory.properties.loadingText, queuedAction.territory.properties);
                        break;
                    case WorldMapController.QueuedActions.Alien:
                        queuedAction.territory.SetState(TerritoryState.Infested);
                        WorldMapController.RestTransport(queuedAction.territory);
                        WorldMapController.EnterMission(queuedAction.territory.GetCampaignName(), queuedAction.territory.properties.loadingText, queuedAction.territory.properties);
                        break;
                    case WorldMapController.QueuedActions.Burning:
                        queuedAction.territory.SetState(TerritoryState.TerroristBurning);
                        WorldMapController.RestTransport(queuedAction.territory);
                        WorldMapController.EnterMission(queuedAction.territory.GetCampaignName(), queuedAction.territory.properties.loadingText, queuedAction.territory.properties);
                        break;
                    case WorldMapController.QueuedActions.Liberated:
                        queuedAction.territory.SetState(TerritoryState.Liberated);
                        break;
                    case WorldMapController.QueuedActions.Hell:
                        queuedAction.territory.SetState(TerritoryState.Hell);
                        WorldMapController.RestTransport(queuedAction.territory);
                        WorldMapController.EnterMission(queuedAction.territory.GetCampaignName(), queuedAction.territory.properties.loadingText, queuedAction.territory.properties);
                        break;
                    case WorldMapController.QueuedActions.Secret:
                        queuedAction.territory.SetState(TerritoryState.TerroristBase);
                        WorldMapController.RestTransport(queuedAction.territory);
                        WorldMapController.EnterMission(queuedAction.territory.GetCampaignName(), queuedAction.territory.properties.loadingText, queuedAction.territory.properties);
                        break;
                }
                actionQueue.RemoveAt(0);
                Traverse queueCounter = Traverse.Create(__instance);
                if (queuedAction.actionType == WorldMapController.QueuedActions.Hell)
                {
                    //queueCounter = 5.2f;
                    queueCounter.Field("queueCounter").SetValue(0);
                }
                else if (queuedAction.actionType == WorldMapController.QueuedActions.Liberated)
                {
                    //queueCounter = 2f;
                    queueCounter.Field("queueCounter").SetValue(0);
                }
                else if (queuedAction.actionType == WorldMapController.QueuedActions.Secret)
                {
                    //queueCounter = 0f;
                    queueCounter.Field("queueCounter").SetValue(0);
                }
                else
                {
                    //queueCounter = 1.6f;
                    queueCounter.Field("queueCounter").SetValue(0);
                }
            }
            else if (WorldCamera.instance.CamState != WorldCamera.CameraState.FollowHelicopter && WorldCamera.instance.CamState != WorldCamera.CameraState.MoveToHelicopter)
            {
                WorldCamera.instance.MoveToHelicopter(0f);
            }
            actionQueueTraverse.Field("actionQueue").SetValue(actionQueue);
            return false;
        }
    }


    [HarmonyPatch(typeof(WorldMapController), "AddAction")]
    static class WorldMapController_AddAction_Patch
    {
        public static WorldMapController instance;
        public static void GoToLevel(string campaignName, int levelNum, string terrainName)
        {
            if (instance == null || !Main.enabled)
            {
                //Main.Log("instance null");
                return;
            }
           
            WorldTerritory3D territoryObject = null; // = WorldMapController.GetTerritory(campaignName);

            WorldTerritory3D[] territories = Traverse.Create(typeof(WorldMapController)).Field("territories3D").GetValue() as WorldTerritory3D[];
            foreach (WorldTerritory3D ter in territories)
            {
                //Main.Log(ter.properties.territoryName + " == " + campaignName + " = " + (ter.properties.territoryName == campaignName));
                if (ter.properties.territoryName == campaignName)
                {
                    territoryObject = ter;
                }
            }

            if (territoryObject == null)
            {
                //Main.Log(campaignName + " territory not found");
                return;
            }

            WorldMapController.QueuedAction item = default(WorldMapController.QueuedAction);

            if (territoryObject.properties.isBurning)
            {
                item.actionType = WorldMapController.QueuedActions.Burning;

            }
            else if ((Main.campaignNum.indexNumber + 1) >= 11)
            {
                item.actionType = WorldMapController.QueuedActions.Alien;
            }
            else
            {
                item.actionType = WorldMapController.QueuedActions.Terrorist;
            }
			
			item.territory = territoryObject;
			(Traverse.Create(instance).Field("actionQueue").GetValue() as List<WorldMapController.QueuedAction>).Add(item);

            //Main.mod.Logger.Log(territoryObject.properties.territoryName);
            //Main.mod.Logger.Log("isburning: " + territoryObject.properties.isBurning + " isCity: " + territoryObject.properties.isCity + " isSecret: " +
            //    territoryObject.properties.isCity + " state: " + territoryObject.properties.state);

            //return;

            foreach (TerritoryProgress territoryProgress in WorldMapProgressController.currentProgress.territoryProgress)
            {
                //Main.mod.Logger.Log(territoryProgress.name.ToLower() + " == " + territoryObject.properties.territoryName.ToLower() + " = " + (territoryProgress.name.ToLower() == territoryObject.properties.territoryName.ToLower()));
                if (territoryProgress.name.ToLower() == terrainName)
                {
                    //Main.mod.Logger.Log("found territory changed level");
                    territoryProgress.startLevel = levelNum;
                }
            }
            

            WorldMapController.RestTransport(territoryObject);
            WorldMapController.EnterMission(territoryObject.GetCampaignName(), territoryObject.properties.loadingText, territoryObject.properties);


        }
        /*static void Prefix(WorldMapController __instance, ref WorldMapController.QueuedActions actionType, ref WorldTerritory3D territory)
        {
            if (!Main.enabled)
            {
                return;
            }

            instance = __instance;
            return;
            
        }*/

    }

    [HarmonyPatch(typeof(WorldMapController), "Update")]
    static class WorldMapController_Update_Patch
    {
        static void Prefix(WorldMapController __instance)
        {
            if (!Main.enabled)
                return;

            WorldMapController_AddAction_Patch.instance = __instance;
        }
    }

    [HarmonyPatch(typeof(GameModeController), "LevelFinish")]
    static class GameModeController_LevelFinish_Patch
    {
        static void Prefix(GameModeController __instance, LevelResult result)
        {
            if (Main.settings.loopCurrent && result == LevelResult.Success)
            {
                if (!Main.enabled || !Main.settings.loopCurrent)
                {
                    return;
                }
                //Main.mod.Logger.Log("current level num after finish called: " + LevelSelectionController.CurrentLevelNum);
                //LevelSelectionController.CurrentLevelNum -= 1;
                //GameModeController.RestartLevel();
                bool temp = Main.settings.disableConfirm;
                Main.settings.disableConfirm = true;
                PauseMenu_RestartLevel_Patch.Prefix(null);
                Main.settings.disableConfirm = temp;
            }
        }
    }





    [HarmonyPatch(typeof(PauseMenu), "ReturnToMenu")]
    static class PauseMenu_ReturnToMenu_Patch
    {
        static bool Prefix(PauseMenu __instance)
        {
            if (!Main.enabled || !Main.settings.disableConfirm)
            {
                return true;
            }

            PauseGameConfirmationPopup m_ConfirmationPopup = (Traverse.Create(__instance).Field("m_ConfirmationPopup").GetValue() as PauseGameConfirmationPopup);

            MethodInfo dynMethod = m_ConfirmationPopup.GetType().GetMethod("ConfirmReturnToMenu", BindingFlags.NonPublic | BindingFlags.Instance);
            dynMethod.Invoke(m_ConfirmationPopup, null);

            return false;
        }

    }

    [HarmonyPatch(typeof(PauseMenu), "ReturnToMap")]
    static class PauseMenu_ReturnToMap_Patch
    {
        static bool Prefix(PauseMenu __instance)
        {
            if (!Main.enabled || !Main.settings.disableConfirm)
            {
                return true;
            }

            __instance.CloseMenu();
            GameModeController.Instance.ReturnToWorldMap();
            return false;
        }

    }



    [HarmonyPatch(typeof(PauseMenu), "RestartLevel")]
    static class PauseMenu_RestartLevel_Patch
    {
        public static bool Prefix(PauseMenu __instance)
        {
            if (!Main.enabled || !Main.settings.disableConfirm)
            {
                return true;
            }

            Map.ClearSuperCheckpointStatus();

            (Traverse.Create(typeof(TriggerManager)).Field("alreadyTriggeredTriggerOnceTriggers").GetValue() as List<string>).Clear();

            if (GameModeController.publishRun)
            {
                GameModeController.publishRun = false;
                LevelEditorGUI.levelEditorActive = true;
            }
            PauseController.SetPause(PauseStatus.UnPaused);
            GameModeController.RestartLevel();

            return false;
        }
    }



    public class Settings : UnityModManager.ModSettings
    {
        public bool loopCurrent;
        public bool disableConfirm;
        public int campaignNum;
        public int levelNum;
        public bool cameraShake;
        public bool enableSkip;

        public int BuildCampaignNum;


        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }


    public class Dropdown
    {
        private Vector2 scrollViewVector;
        public Rect dropDownRect;
        public string[] list;

        public int indexNumber;
        public bool show;
        public Dropdown(float x, float y, float width, float height, string[] options, int setIndexNumber, bool setShow = false )
        {
            dropDownRect = new Rect(x, y, width, height);
            list = options;
            this.indexNumber = setIndexNumber;

            scrollViewVector = Vector2.zero;
            show = setShow;
        }
        
        public void OnGUI(UnityModManager.ModEntry modEntry)
        {


            //if (GUI.Button(new Rect((dropDownRect.x - 100), dropDownRect.y, dropDownRect.width, 25), ""))
            if (GUILayout.Button("", GUILayout.Width(dropDownRect.width)))
            {
                if (!show)
                {
                    show = true;
                }
                else
                {
                    show = false;
                }
            }
            Rect lastRect = GUILayoutUtility.GetLastRect();
            dropDownRect.x = lastRect.x;  // + lastRect.width + offsetX;
            dropDownRect.y = lastRect.y;
            //Main.mod.Logger.Log(lastRect.width + " last rect " + list[0]);
            //Main.mod.Logger.Log(dropDownRect.width + " dropdown width");


            if (show)
            {
                scrollViewVector = GUI.BeginScrollView(new Rect((dropDownRect.x), (dropDownRect.y + 25), dropDownRect.width, dropDownRect.height), scrollViewVector, new Rect(0, 0, dropDownRect.width, Mathf.Max(dropDownRect.height, (list.Length * 25))));

                GUI.Box(new Rect(0, 0, dropDownRect.width, Mathf.Max(dropDownRect.height, (list.Length * 25))), "");

                for (int index = 0; index < list.Length; index++)
                {

                    if (GUI.Button(new Rect(0, (index * 25), dropDownRect.width, 25), ""))
                    {
                        show = false;
                        indexNumber = index;
                    }

                    GUI.Label(new Rect(5, (index * 25), dropDownRect.width, 25), list[index]);

                }

                GUI.EndScrollView();
            }
            else
            {
                
                //GUI.Label(new Rect((dropDownRect.x - 95), dropDownRect.y, 300, 25), list[indexNumber]);
                GUI.Label(new Rect((dropDownRect.x + 5), dropDownRect.y, 300, 25), list[indexNumber]);
            }
        }
    }


    [HarmonyPatch(typeof(PlaytomicController), "isExhibitionBuild", MethodType.Getter)]
    static class isExhibitionBuild_Patch
    {
        static bool Prefix(ref bool __result)
        {
            if (!Main.enabled) return true;
            if (Main.CurrentBuild != "Expendabros" && Main.CurrentBuild != "Online")
            {
                if (Main.CurrentBuild == "AlienDemo")
                {
                    PlaytomicController.hasChosenBossRushDemo = false;
                    PlaytomicController.hasChosenAlienDemo = true;
                }
                else if (Main.CurrentBuild == "BossRush")
                {
                    PlaytomicController.hasChosenAlienDemo = false;
                    PlaytomicController.hasChosenBossRushDemo = true;
                }
                else
                {
                    PlaytomicController.hasChosenAlienDemo = false;
                    PlaytomicController.hasChosenBossRushDemo = false;
                }
                __result = true;
            }
            else
            {
                PlaytomicController.hasChosenBossRushDemo = false;
                    PlaytomicController.hasChosenAlienDemo = false;
                __result = false;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(PlaytomicController), "isOnlineBuild", MethodType.Getter)]
    static class isOnlineBuild_Patch
    {
        static bool Prefix(ref bool __result)
        {
            if (!Main.enabled) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlaytomicController), "isExpendabrosBuild", MethodType.Getter)]
    static class IsExpendabrosBuild_Patch
    {
        static bool Prefix(ref bool __result)
        {
            if (!Main.enabled) return true;
            if (Main.CurrentBuild == "Expendabros")
            {
                PlaytomicController.hasChosenBossRushDemo = false;
                PlaytomicController.hasChosenAlienDemo = false;
                __result = true;
            }
            else __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlaytomicController), "isAlienDemoBuild", MethodType.Getter)]
    static class isAlienDemoBuild_Patch
    {
        static bool Prefix(ref bool __result)
        {
            if (!Main.enabled) return true;
            if (Main.CurrentBuild == "AlienDemo")
            {
                PlaytomicController.hasChosenBossRushDemo = false;
                PlaytomicController.hasChosenAlienDemo = true;
                __result = true;
            }
            else
            {
                PlaytomicController.hasChosenAlienDemo = false;
                __result = false;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(PlaytomicController), "isBossRushBuild", MethodType.Getter)]
    static class isBossRushBuild_Patch
    {
        static bool Prefix(ref bool __result)
        {
            if (!Main.enabled) return true;
            if (Main.CurrentBuild == "BossRush")
            {
                PlaytomicController.hasChosenAlienDemo = false;
                PlaytomicController.hasChosenBossRushDemo = true;
                __result = true;
            }
            else
            {
                PlaytomicController.hasChosenBossRushDemo = false;
                __result = false;
            }
            return false;
        }
    }
    
    [HarmonyPatch(typeof(LevelSelectionController), "MainMenuScene", MethodType.Getter)]
    static class FixReturnToMenuWithExpendabrosBuild_Patch
    {
        static bool Prefix(ref string __result)
        {
            if (!Main.enabled) return true;
            __result = "MainMenu";
            return false;
        }
    }

    // Add the missing menu items, if not the basic campaign
    [HarmonyPatch(typeof(MainMenu), "SetupItems")]
    static class AddMissingMenu_Patch
    {
        static bool Prefix(MainMenu __instance)
        {
            if (!Main.enabled) return true;
            List<MenuBarItem> list = new List<MenuBarItem>(Traverse.Create(__instance).Field("masterItems").GetValue<MenuBarItem[]>());

            list.Insert(2, new MenuBarItem
            {
                color = list[0].color,
                size = list[0].size,
                name = "Versus",
                localisedKey = "MENU_MAIN_VERSUS",
                invokeMethod = "StartDeathMatch"
            });
            list.Insert(2, new MenuBarItem
            {
                color = list[0].color,
                size = list[0].size,
                name = "CUSTOM CAMPAIGN ^",
                localisedKey = "MENU_MAIN_CUSTOM_CAMPAIGN",
                invokeMethod = "CustomCampaign"
            });
            list.Insert(2, new MenuBarItem
            {
                color = list[0].color,
                size = list[0].size,
                name = "LEVEL EDITOR",
                localisedKey = "MENU_MAIN_LEVEL_EDITOR",
                invokeMethod = "LevelEditor"
            });
            list.Insert(2, new MenuBarItem
            {
                color = list[0].color,
                size = list[0].size,
                name = "JOIN ONLINE ^",
                localisedKey = "MENU_MAIN_JOIN_ONLINE",
                invokeMethod = "FindAGameToJoin"
            });

            if (PlayerProgress.Instance.lastFinishedLevelOffline <= 0 || PlaytomicController.isExhibitionBuild)
            {
                list.RemoveAll((MenuBarItem i) => i.name.ToUpper().Equals("CONTINUE ARCADE CAMPAIGN ^"));
            }
            else
            {
                list.RemoveAll((MenuBarItem i) => i.name.ToUpper().Equals("START ARCADE CAMPAIGN ^"));
            }

            Traverse.Create(__instance).Field("masterItems").SetValue(list.ToArray());

            return false;
        }
    }

    // Patch Arcade for ask you if you want to play it online
    [HarmonyPatch(typeof(MainMenu), "StartArcade")]
    static class CanOnlineArcadeInAnyBuild_Patch
    {
        static bool Prefix(MainMenu __instance)
        {
            if (!Main.enabled) return true;

            HeroUnlockController.ClearHeroUnlockIntervals();
            LevelSelectionController.ResetLevelAndGameModeToDefault();
            PlayerProgress.Instance.truckBloodSplatter = 0;
            PlayerProgress.Instance.lastMissionThatBloodiedTheTruck = string.Empty;
            HeroUnlockController.Initialize();
            GameState.Instance.ResetToDefault();
            PlayerProgress.Instance.arcadeIsInHardMode = false;
            GameState.Instance.arcadeHardMode = false;
            GameState.Instance.campaignName = LevelSelectionController.DefaultCampaign;
            GameState.Instance.sceneToLoad = LevelSelectionController.JoinScene;
            GameState.Instance.gameMode = GameMode.Campaign;
            GameState.Instance.loadMode = MapLoadMode.Campaign;
            GameState.Instance.Apply();
            MainMenu.TransitionToOnlineOrOffline(true); // Change
            __instance.MenuActive = false;

            return false;
        }
    }
    [HarmonyPatch(typeof(MainMenu), "TransitionToOnlineOrOffline")]
    static class CanOnlineArcadeInAnyBuild2_Patch
    {
        static bool Prefix(MainMenu __instance, bool onlineAvalable)
        {
            if (!Main.enabled) return true;

            MainMenu.instance.MenuActive = false;
            if (OnlineOrOfflineMenu.gotToCustomCampaignMenu)
            {
                MainMenu.instance.TransitionToCustomCampaign();
            }
            else
            {
                OnlineOrOfflineMenu.instance.onlineAvalable = onlineAvalable;
                OnlineOrOfflineMenu.Open();
            }

            return false;
        }
    }
    [HarmonyPatch(typeof(MainMenu), "GoToCampaignMenu")]
    static class ShowNormalCampaignSelectorMenu_Patch
    {
        static bool Prefix(MainMenu __instance)
        {
            if (!Main.enabled) return true;

            OnlineOrOfflineMenu.gotToCustomCampaignMenu = false;
            __instance.MenuActive = false;
            __instance.worldMapOrArcadeMenu.MenuActive = true;
            __instance.worldMapOrArcadeMenu.TransitionIn();

            return false;
        }
    }
}
