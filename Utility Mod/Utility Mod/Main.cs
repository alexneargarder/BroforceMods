/**
 * TODO
 * 
 * Add inifnite damage option for one hitting bosses
 * 
 * Add new levels
 * 
 * Fix infinite specials locking brolander out of having special, 
 * maybe also fix double bro seven only having option to drink ( probably only refresh specials when they reach 0 )
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
 * give lives (for practicing iron bro)
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
 * Make helicopter skip go in order
 * 
 * add invincibility toggle
 * 
 * add teleporting system with waypoints you can set
 * 
 * summon mech anywhere
 * 
 * give infinite specials
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
using UnityEngine.SceneManagement;

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

        public static string teleportX = "0";
        public static string teleportY = "0";

        public static bool loadedScene = false;
        public static float waitForLoad = 4.0f;

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
            

            return true;
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
                    "Skips helicopter on world map and immediately takes you into a level"), GUILayout.Width(120f));

                GUI.Label(lastRect, GUI.tooltip);

                settings.endingSkip = GUILayout.Toggle(settings.endingSkip, new GUIContent("Ending Skip",
                    "Speeds up the ending"), GUILayout.Width(100f));

                GUI.Label(lastRect, GUI.tooltip);

                settings.disableConfirm = GUILayout.Toggle(settings.disableConfirm, new GUIContent("Fix Mod Window Disappearing",
                    "Disables confirmation screen when restarting or returning to map/menu"), GUILayout.ExpandWidth(false));

                GUI.Label(lastRect, GUI.tooltip);
                previousToolTip = GUI.tooltip;
            }
            GUILayout.EndHorizontal();


            GUILayout.Space(25);

            GUIStyle headerStyle = new GUIStyle( GUI.skin.button );


            headerStyle.fontStyle = FontStyle.Bold;
            // 163, 232, 255
            headerStyle.normal.textColor = new Color(0.639216f, 0.909804f, 1f);


            
           // GUILayout.Label("Level Controls", headerStyle);

            if ( GUILayout.Button("Level Controls", headerStyle) )
            {
                settings.showLevelOptions = !settings.showLevelOptions;
            }

            if ( settings.showLevelOptions )
            {
                GUILayout.BeginHorizontal();
                {
                    settings.loopCurrent = GUILayout.Toggle(settings.loopCurrent, new GUIContent("Loop Current Level", "After beating a level you replay the current one instead of moving on"), GUILayout.ExpandWidth(false));

                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    lastRect.y += 20;
                    lastRect.width += 300;



                    if (GUI.tooltip != previousToolTip)
                    {
                        GUI.Label(lastRect, GUI.tooltip);
                        previousToolTip = GUI.tooltip;
                    }

                }
                GUILayout.EndHorizontal();


                GUILayout.Space(25);


                GUILayout.BeginHorizontal(new GUILayoutOption[] { GUILayout.MinHeight((campaignNum.show || levelNum.show) ? 350 : 100), GUILayout.ExpandWidth(false) });
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.BeginHorizontal();
                        {
                            campaignNum.OnGUI(modEntry);

                            determineLevelsInCampaign();

                            levelNum.OnGUI(modEntry);

                            Main.settings.levelNum = levelNum.indexNumber;

                            if (GUILayout.Button(new GUIContent("Go to level", "This only works on the world map screen"), GUILayout.Width(100)))
                            {
                                GoToLevel();
                            }

                            if (GUI.tooltip != previousToolTip)
                            {
                                Rect lastRect = campaignNum.dropDownRect;
                                lastRect.y += 20;
                                lastRect.width += 300;

                                GUI.Label(lastRect, GUI.tooltip);
                                previousToolTip = GUI.tooltip;
                            }
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.Space(25);

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Space(1);

                            if (!campaignNum.show && !levelNum.show)
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
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            } // End Level Controls

            TestVanDammeAnim currentCharacter = null;


            
            if (settings.quickLoadScene)
            {
                if ( loadedScene && HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null)
                {
                    currentCharacter = HeroController.players[0].character;
                }
            }
            else if (HeroController.Instance != null && HeroController.players != null && HeroController.players[0] != null)
            {
                currentCharacter = HeroController.players[0].character;
            }


            if (GUILayout.Button("Cheat Options", headerStyle))
            {
                settings.showCheatOptions = !settings.showCheatOptions;
            }

            if ( settings.showCheatOptions )
            {
                GUILayout.BeginHorizontal();
                {
                    settings.invulnerable = GUILayout.Toggle(settings.invulnerable, "Invincibility");

                    GUILayout.Space(20);

                    settings.infiniteSpecials = GUILayout.Toggle(settings.infiniteSpecials, "Infinite Specials");

                    GUILayout.Space(20);

                    settings.disableEnemySpawn = GUILayout.Toggle(settings.disableEnemySpawn, "Disable Enemy Spawns");

                    GUILayout.Space(20);

                    settings.quickMainMenu = GUILayout.Toggle(settings.quickMainMenu, "Speed up Main Menu Loading");

                    GUILayout.Space(20);

                    if (GUILayout.Button("Summon Mech", GUILayout.Width(140)))
                    {
                        if (currentCharacter != null)
                        {
                            ProjectileController.SpawnGrenadeOverNetwork(ProjectileController.GetMechDropGrenadePrefab(), currentCharacter, currentCharacter.X + Mathf.Sign(currentCharacter.transform.localScale.x) * 8f, currentCharacter.Y + 8f, 0.001f, 0.011f, Mathf.Sign(currentCharacter.transform.localScale.x) * 200f, 150f, currentCharacter.playerNum);
                        }
                    }

                    GUILayout.Space(20);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(25);

                GUILayout.BeginHorizontal(GUILayout.Width(500));

                GUILayout.Label("Scene to Load: ");

                settings.sceneToLoad = GUILayout.TextField(settings.sceneToLoad, GUILayout.Width(200));

                GUILayout.EndHorizontal();

                settings.quickLoadScene = GUILayout.Toggle(settings.quickLoadScene, "Immediately load chosen scene", GUILayout.Width(200));

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Load Current Scene", GUILayout.Width(200)))
                {
                    if (!Main.settings.cameraShake)
                    {
                        PlayerOptions.Instance.cameraShakeAmount = 0f;
                    }

                    Utility.SceneLoader.LoadScene(settings.sceneToLoad);
                }

                if (GUILayout.Button("Get Current Scene", GUILayout.Width(200)))
                {
                    Scene[] scenes = SceneManager.GetAllScenes();
                    for (int i = 0; i < scenes.Length; ++i)
                    {
                        Main.Log("Scene Name: " + scenes[i].name);
                    }
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(25);
            } // End Cheat Options


            if (GUILayout.Button("Teleport Options", headerStyle))
            {
                settings.showTeleportOptions = !settings.showTeleportOptions;
            }

            if ( settings.showTeleportOptions )
            {
                GUILayout.BeginHorizontal();
                {
                    if (currentCharacter != null)
                    {
                        GUILayout.Label("Position: " + currentCharacter.X.ToString("0.00") + ", " + currentCharacter.Y.ToString("0.00"));
                    }
                    else
                    {
                        GUILayout.Label("Position: ");
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(15);

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("X", GUILayout.Width(10));
                    teleportX = GUILayout.TextField(teleportX, GUILayout.Width(100));
                    GUILayout.Space(20);
                    GUILayout.Label("Y", GUILayout.Width(10));
                    GUILayout.Space(10);
                    teleportY = GUILayout.TextField(teleportY, GUILayout.Width(100));

                    if (GUILayout.Button("Teleport", GUILayout.Width(100)))
                    {
                        float x, y;
                        if (float.TryParse(teleportX, out x) && float.TryParse(teleportY, out y))
                        {
                            if (currentCharacter != null)
                            {
                                currentCharacter.X = x;
                                currentCharacter.Y = y;
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(15);

                settings.changeSpawn = GUILayout.Toggle(settings.changeSpawn, "Override Default Spawn Position");

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Save Position for Custom Spawn", GUILayout.Width(250)))
                    {
                        if (currentCharacter != null)
                        {
                            settings.SpawnPositionX = currentCharacter.X;
                            settings.SpawnPositionY = currentCharacter.Y;
                        }
                    }

                    if (GUILayout.Button("Teleport to Custom Spawn Position", GUILayout.Width(300)))
                    {
                        if (currentCharacter != null)
                        {
                            currentCharacter.X = settings.SpawnPositionX;
                            currentCharacter.Y = settings.SpawnPositionY;
                        }
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(15);

                for (int i = 0; i < settings.waypointsX.Length; ++i)
                {
                    GUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Save Position to Waypoint " + (i + 1), GUILayout.Width(250)))
                        {
                            if (currentCharacter != null)
                            {
                                settings.waypointsX[i] = currentCharacter.X;
                                settings.waypointsY[i] = currentCharacter.Y;
                            }
                        }

                        if (GUILayout.Button("Teleport to Waypoint " + (i + 1), GUILayout.Width(200)))
                        {
                            if (currentCharacter != null)
                            {
                                currentCharacter.X = settings.waypointsX[i];
                                currentCharacter.Y = settings.waypointsY[i];
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            } // End Teleport Options

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

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            if (!enabled) return;

            if (!loadedScene && settings.quickLoadScene)
            {
                waitForLoad -= dt;
                if ( waitForLoad < 0 )
                {
                    try
                    {
                        if (!Main.settings.cameraShake)
                        {
                            PlayerOptions.Instance.cameraShakeAmount = 0f;
                        }

                        Utility.SceneLoader.LoadScene("WorldMap3D");
                        Utility.SceneLoader.LoadScene(settings.sceneToLoad);
                    }
                    catch (Exception ex)
                    {

                    }
                    loadedScene = true;
                }
            }

            if ( (settings.invulnerable || settings.infiniteSpecials ) && HeroController.Instance != null  )
            {
                for ( int i = 0; i < 4; ++i )
                {
                    if ( HeroController.PlayerIsAlive(i))
                    {
                        if (settings.invulnerable)
                        {
                            HeroController.players[i].character.SetInvulnerable(1, false);
                        }

                        if (settings.infiniteSpecials)
                        {
                            HeroController.players[i].character.SetSpecialAmmoRPC(HeroController.players[i].character.originalSpecialAmmo);
                        }    
                    }
                }
            }

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
        public static int nextCampaign = 0;

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

            // DEBUG
            WorldTerritory3D[] territories = Traverse.Create(__instance).Field("territories3D").GetValue() as WorldTerritory3D[];
            //Main.Log("BEGIN");
            for ( int i = 0; i < 15; ++i )
            {
                foreach (WorldTerritory3D ter in territories)
                {
                    //Main.Log(ter.properties.territoryName + " == " + campaignName + " = " + (ter.properties.territoryName == campaignName));
                    if (ter.properties.territoryName == Main.campaignList[i])
                    {
                        //Main.Log(ter.GetCampaignName());
                        break;
                    }
                }
            }

            if (actionQueue.Count > 0)
            {

                WorldMapController.QueuedAction queuedAction = actionQueue[0];
                switch (queuedAction.actionType)
                {
                    case WorldMapController.QueuedActions.Helicopter:
                        WorldCamera.instance.MoveToHelicopter(0f);
                        break;
                    case WorldMapController.QueuedActions.Terrorist:
                        queuedAction.territory.BecomeEnemyBase();
                        break;
                    case WorldMapController.QueuedActions.Alien:
                        queuedAction.territory.SetState(TerritoryState.Infested);
                        break;
                    case WorldMapController.QueuedActions.Burning:
                        queuedAction.territory.SetState(TerritoryState.TerroristBurning);
                        break;
                    case WorldMapController.QueuedActions.Liberated:
                        queuedAction.territory.SetState(TerritoryState.Liberated);
                        break;
                    case WorldMapController.QueuedActions.Hell:
                        queuedAction.territory.SetState(TerritoryState.Hell);
                        break;
                    case WorldMapController.QueuedActions.Secret:
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

            nextCampaign = -1;
            foreach (WorldTerritory3D ter in territories)
            {
                if (ter.properties.state == TerritoryState.Liberated)
                {
                    for (int i = 0; i < Main.campaignList.Length; ++i)
                    {
                        if (ter.properties.territoryName == Main.campaignList[i])
                        {
                            if (i > nextCampaign)
                            {
                                nextCampaign = i;
                            }
                            break;
                        }
                    }
                }
            }
            ++nextCampaign;

            foreach (WorldTerritory3D ter in territories)
            {
                if (ter.properties.state == TerritoryState.TerroristBase || ter.properties.state == TerritoryState.Hell
                    || ter.properties.state == TerritoryState.Infested || ter.properties.state == TerritoryState.TerroristBurning
                    || ter.properties.state == TerritoryState.TerroristAirBase)
                {
                    if ( ter.properties.territoryName == Main.campaignList[nextCampaign] && GameState.Instance.campaignName != ter.GetCampaignName() )
                    {
                        WorldMapController.RestTransport(ter);
                        WorldMapController.EnterMission(ter.GetCampaignName(), ter.properties.loadingText, ter.properties);
                    }
                }
            }

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
            if (!Main.enabled)
            {
                return;
            }

            if (Main.settings.loopCurrent && result == LevelResult.Success)
            {
                //Main.mod.Logger.Log("current level num after finish called: " + LevelSelectionController.CurrentLevelNum);
                //LevelSelectionController.CurrentLevelNum -= 1;
                //GameModeController.RestartLevel();
                bool temp = Main.settings.disableConfirm;
                Main.settings.disableConfirm = true;
                PauseMenu_RestartLevel_Patch.Prefix(null);
                Main.settings.disableConfirm = temp;
            }

            
        }

        static void Postfix(GameModeController __instance, LevelResult result)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (Main.settings.endingSkip && (result == LevelResult.Success))
            {
                GameModeController.MakeFinishInstant();
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

    [HarmonyPatch(typeof(MapController), "SpawnMook_Networked")]
    static class MapController_SpawnMook_Networked
    {
        public static bool Prefix()
        {
            if (!Main.enabled || !Main.settings.disableEnemySpawn)
            {
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(MapController), "SpawnMook_Local")]
    static class MapController_SpawnMook_Local
    {
        public static bool Prefix()
        {
            if (!Main.enabled || !Main.settings.disableEnemySpawn)
            {
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Map), "PlaceDoodad")]
    static class Map_PlaceDoodad
    {
        public static bool Prefix(Map __instance, ref DoodadInfo doodad, ref GameObject __result)
        {
            if (!Main.enabled || !Main.settings.disableEnemySpawn)
            {
                return true;
            }

            if ( doodad.type == DoodadType.Mook || doodad.type == DoodadType.Alien || doodad.type == DoodadType.HellEnemy || doodad.type == DoodadType.AlienBoss || doodad.type == DoodadType.HellBoss )
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Player), "WorkOutSpawnScenario")]
    static class Player_WorkOutSpawnScenario_Patch
    {
        public static void Postfix(ref Player.SpawnType __result)
        {
            if (!Main.enabled || !Main.settings.changeSpawn)
            {
                return;
            }

            if ( __result != Player.SpawnType.RespawnAtRescueBro )
            {
                __result = Player.SpawnType.Unknown;
            }  
        }
    }

    [HarmonyPatch(typeof(Player), "SetSpawnPositon")]
    static class Player_SetSpawnPositon_Patch
    {
        public static void Prefix(Player __instance, ref TestVanDammeAnim bro, ref Player.SpawnType spawnType, ref bool spawnViaAirDrop, ref Vector3 pos)
        {
            if (!Main.enabled || !Main.settings.changeSpawn)
            {
                return;
            }

            if ( spawnType != Player.SpawnType.RespawnAtRescueBro )
            {
                spawnType = Player.SpawnType.CustomSpawnPoint;
                spawnViaAirDrop = false;
                pos.x = Main.settings.SpawnPositionX;
                pos.y = Main.settings.SpawnPositionY;
            }
        }
    }

    [HarmonyPatch(typeof(MainMenu), "Start")]
    static class MainMenu_Start_Patch
    {
        public static void Prefix(MainMenu __instance)
        {
            if (!Main.enabled || !Main.settings.quickMainMenu )
            {
                return;
            }

            Traverse.Create(__instance).Method("InitializeMenu").GetValue();
        }
    }

    public class Settings : UnityModManager.ModSettings
    {
        public bool cameraShake = false;
        public bool enableSkip = true;
        public bool endingSkip = true;
        public bool disableConfirm = true;

        public bool loopCurrent = false;
        public int campaignNum = 0;
        public int levelNum = 0;

        public bool invulnerable = false;
        public bool infiniteSpecials = false;
        public bool disableEnemySpawn = false;
        public bool changeSpawn = false;
        public bool quickLoadScene = false;
        public bool quickMainMenu = false;

        public float[] waypointsX = new float[5];
        public float[] waypointsY = new float[5];

        public float SpawnPositionX = 0;
        public float SpawnPositionY = 0;

        public string sceneToLoad;

        public bool showLevelOptions = false;
        public bool showCheatOptions = false;
        public bool showTeleportOptions = false;

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




       

    

 




}
