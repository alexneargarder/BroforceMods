using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using BSett = BroMakerLib.Settings;
using BroMakerLib.Storages;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loaders;

namespace Swap_Bros_Mod
{
    public enum DPAD
    {
        LEFT,
        RIGHT,
        UP,
        DOWN,
        NONE
    }

    public class KeyBind
    {
        public KeyCode kc;
        public bool waitingForInput = false;
        public string DPADString = "NONE";
        public DPAD DPADKey = DPAD.NONE;
        public string joystick = "NONE";
    }

    public static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static float cooldown = 0;
        public static Settings settings;
        public static int currentCharIndex = 0;
        public static bool[] switched = { false, false, false, false };
        public static bool manualSpawn = false;
        public static bool creatingBroList = false;

        public static List<string> allNormal = new List<string> { "Rambro", "Brommando", "B. A. Broracus", "Brodell Walker", "Bro Hard", "MacBrover", "Brade", "Bro Dredd", "Bro In Black", "Snake Broskin", "Brominator",
                "Brobocop", "Indiana Brones", "Ash Brolliams", "Mr. Anderbro", "The Boondock Bros", "Brochete", "Bronan the Brobarian", "Ellen Ripbro", "Time Bro", "Broniversal Soldier",
                "Colonel James Broddock", "Cherry Broling", "Bro Max", "The Brode", "Double Bro Seven", "The Brodator", "The Brocketeer", "Broheart", "The Brofessional", "Broden",
                "The Brolander", "Dirty Brory", "Tank Bro", "Bro Lee", "Seth Brondle", "Xebro", "Desperabro", "Broffy the Vampire Slayer", "Burt Brommer", "Demolition Bro" };
        public static List<string> allExpendabros = new List<string> {"Broney Ross", "Lee Broxmas", "Bronnar Jensen", "Bro Caesar", "Trent Broser", "Broctor Death", "Toll Broad"};
        public static List<string> allUnfinished = new List<string> { "Chev Brolios", "Casey Broback", "The Scorpion Bro" };
        public static List<string> allCustomBros = new List<string>();
        public static List<string> actuallyAllCustomBros = new List<string>();
        public static List<string> allBros = new List<string>();
        public static int numCustomBros = 0;

        public static List<string> currentBroList = new List<string>();
        public static List<string> currentBroListUnseen = new List<string>();
        public static string[] broList;
        public static string[] previousSelection = new string[] { "", "", "", "" };
        public static int maxBroNum = 40;
        public static bool isHardcore = false;

        public static bool changingEnabledBros = false;
        public static float displayWarningTime = 0f;
        public static bool[] filteredBroList;
        public static bool brosRemoved = false;
        public static GUIStyle buttonStyle;
        public static GUIStyle warningStyle;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = Settings.Load<Settings>(modEntry);
            // Update selGridInt to an array
            if ( settings.selGridInt.Length < 4 )
            {
                settings.selGridInt = new int[] { 0, 0, 0, 0 };
            }
            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            mod = modEntry;
            try
            {
                harmony.PatchAll(assembly);
            }
            catch (Exception e)
            {
                mod.Logger.Error(e.ToString());
            }

            if ( Main.settings.enableBromaker )
            {
                try
                {
                    LoadCustomBros();
                }
                catch
                {
                    Main.Log("BroMaker is not installed.");
                    Main.settings.enableBromaker = false;
                }
            }

            int[] previousSelGridInts = new int[4];
            settings.selGridInt.CopyTo(previousSelGridInts, 0);
            CreateBroList();
            settings.selGridInt = previousSelGridInts;

            // Initialize enabled bro list
            if (settings.enabledBros == null || settings.enabledBros.Count() == 0)
            {
                allBros = new List<string>();
                allBros.AddRange(allNormal);
                if (settings.includeExpendabros)
                {
                    allBros.AddRange(allExpendabros);
                }
                if (settings.includeUnfinishedCharacters)
                {
                    allBros.AddRange(allUnfinished);
                }
                if (settings.enableBromaker)
                {
                    allBros.AddRange(allCustomBros);
                }
                settings.enabledBros = new List<string>(allBros);
                filteredBroList = Enumerable.Repeat(true, allBros.Count()).ToArray();
            }
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if ( buttonStyle == null )
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.normal.textColor = Color.red;
                buttonStyle.hover.textColor = Color.red;
                buttonStyle.active.textColor = Color.red;

                buttonStyle.onHover.textColor = Color.green;
                buttonStyle.onNormal.textColor = Color.green;
                buttonStyle.onActive.textColor = Color.green;
            }

            if ( warningStyle == null )
            {
                warningStyle = new GUIStyle(GUI.skin.label);

                warningStyle.normal.textColor = Color.red;
                warningStyle.hover.textColor = Color.red;
                warningStyle.active.textColor = Color.red;
                warningStyle.onHover.textColor = Color.red;
                warningStyle.onNormal.textColor = Color.red;
                warningStyle.onActive.textColor = Color.red;
            }

            if (isHardcore != GameModeController.IsHardcoreMode)
            {
                isHardcore = GameModeController.IsHardcoreMode;
                if (!settings.ignoreCurrentUnlocked)
                {
                    CreateBroList();
                }
            }

            if (settings.enableBromaker && CustomCountChanged() && !(GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked))
            {
                LoadCustomBros();
                CreateBroList();
            }

            GUILayout.BeginHorizontal();


            GUILayout.BeginVertical();

            settings.clickingEnabled = GUILayout.Toggle(settings.clickingEnabled, new GUIContent("Clicking a new bro causes swap",
                "Clicking a bro in the menu below will swap you to that bro immediately"), GUILayout.ExpandWidth(false));

            settings.disableConfirm = GUILayout.Toggle(settings.disableConfirm, new GUIContent("Fix mod window disappearing",
                "Disables confirmation screen when restarting or returning to map/menu"), GUILayout.ExpandWidth(false));

            if ( settings.filterBros != (settings.filterBros = GUILayout.Toggle(settings.filterBros, new GUIContent("Filter Bros",
                "Only spawn as enabled characters"), GUILayout.ExpandWidth(false))) )
            {
                CreateBroList();
            }

            settings.ignoreForcedBros = GUILayout.Toggle(settings.ignoreForcedBros, new GUIContent("Ignore Forced Bros",
                "Controls whether filtering will prevent you from spawning as certain bros on levels which force you to use specific bros"), GUILayout.ExpandWidth(false));

            // Display the tooltip from the element that has mouseover or keyboard focus
            Rect lastRect = GUILayoutUtility.GetLastRect();
            lastRect.y += 20;
            lastRect.width += 300;
            GUI.Label(lastRect, GUI.tooltip);

            GUILayout.EndVertical();


            GUILayout.BeginVertical();

            settings.alwaysChosen = GUILayout.Toggle(settings.alwaysChosen, new GUIContent("Always spawn as chosen bro",
                "Prevents restarting a level or collecting a new bro from swapping to a new character"), GUILayout.ExpandWidth(false));

            bool previousSetting = settings.ignoreCurrentUnlocked;
            if (settings.ignoreCurrentUnlocked)
            {
                settings.ignoreCurrentUnlocked = GUILayout.Toggle(settings.ignoreCurrentUnlocked, new GUIContent("Ignore currently unlocked bros in IronBro",
                    "Enabled: In IronBro you can switch to any bro regardless of who you have collected"), GUILayout.ExpandWidth(false));
            }
            else
            {
                settings.ignoreCurrentUnlocked = GUILayout.Toggle(settings.ignoreCurrentUnlocked, new GUIContent("Ignore currently unlocked bros in IronBro",
                    "Disabled: In IronBro you can only switch to bros you have collected"), GUILayout.ExpandWidth(false));
            }
            if (previousSetting != settings.ignoreCurrentUnlocked && isHardcore)
            {
                CreateBroList();
            }

            settings.useVanillaBroSelection = GUILayout.Toggle(settings.useVanillaBroSelection, new GUIContent("Use Vanilla Randomization", settings.useVanillaBroSelection ? "Enabled: When filtering bros, prioritize bros that " +
                "you haven't played yet on this level" : "Disabled: When filtering bros, choose completely randomly from the enabled bros"), GUILayout.ExpandWidth(false));

            GUI.Label(lastRect, GUI.tooltip);

            GUILayout.EndVertical();


            GUILayout.BeginVertical();

            if (settings.includeExpendabros != (settings.includeExpendabros = GUILayout.Toggle(settings.includeExpendabros, new GUIContent("Include Expendabro Bros",
                "Include bros from Expendabros"), GUILayout.ExpandWidth(false))))
            {
                CreateBroList();
                CreateFilteredBroList();
            }

            if (settings.enableBromaker != (settings.enableBromaker = GUILayout.Toggle(settings.enableBromaker, new GUIContent("Include BroMaker Bros",
                "Shows custom bros installed with BroMaker"), GUILayout.ExpandWidth(false))))
            {
                if (settings.enableBromaker)
                {
                    try
                    {
                        LoadCustomBros();
                        CreateBroList();
                        CreateFilteredBroList();
                    }
                    catch
                    {
                        Main.Log("BroMaker is not installed.");
                        Main.settings.enableBromaker = false;
                    }
                }
                else
                {
                    CreateBroList();
                    CreateFilteredBroList();
                }
            }

            if (settings.includeUnfinishedCharacters != (settings.includeUnfinishedCharacters = GUILayout.Toggle(settings.includeUnfinishedCharacters, new GUIContent("Include Unfinished Bros",
                "Include bros the developers didn't finish"), GUILayout.ExpandWidth(false))))
            {
                CreateBroList();
                CreateFilteredBroList();
            }

            GUI.Label(lastRect, GUI.tooltip);
            string previousToolTip = GUI.tooltip;

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(30);

            GUILayout.BeginHorizontal();

            GUILayout.Label(String.Format("Cooldown between swaps: {0:0.00}s", Main.settings.swapCoolDown), GUILayout.Width(225), GUILayout.ExpandWidth(false));
            Main.settings.swapCoolDown = GUILayout.HorizontalSlider(Main.settings.swapCoolDown, 0, 2);

            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            for (int i = 0; i < 4; ++i)
            {
                if (GUILayout.Button("Player " + (i + 1) + " Options - " + (settings.showSettings[i] ? "Hide" : "Show")))
                {
                    settings.showSettings[i] = !settings.showSettings[i];
                }
                if (settings.showSettings[i])
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(
                        new GUIContent("Swap Bro Left: " + (settings.swapLeftKeys[i].waitingForInput ? "Press Any Key/Button" : (settings.swapLeftKeys[i].DPADKey == DPAD.NONE ? settings.swapLeftKeys[i].kc.ToString() : "DPAD " + settings.swapLeftKeys[i].DPADString)),
                        "Set a key for swapping bros, or press Delete to unbind")
                        ) && !InputReader.IsBlocked)
                    {
                        settings.swapLeftKeys[i].waitingForInput = true;
                        UnityModManager.UI.Instance.StartCoroutine(BindKey(settings.swapLeftKeys[i], i));
                    }

                    if (GUILayout.Button(
                        new GUIContent("Swap Bro Right: " + (settings.swapRightKeys[i].waitingForInput ? "Press Any Key/Button" : (settings.swapRightKeys[i].DPADKey == DPAD.NONE ? settings.swapRightKeys[i].kc.ToString() : "DPAD " + settings.swapRightKeys[i].DPADString)),
                        "Set a key for swapping bros, or press Delete to unbind")
                        ) && !InputReader.IsBlocked)
                    {
                        settings.swapRightKeys[i].waitingForInput = true;
                        UnityModManager.UI.Instance.StartCoroutine(BindKey(settings.swapRightKeys[i], i));
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button(new GUIContent(changingEnabledBros ? "Save Changes" : "Enter Filtering Mode",
                                               "Enable or disable bros for this player"), GUILayout.ExpandWidth(false), GUILayout.Width(300)) )
                    {
                        changingEnabledBros = !changingEnabledBros;
                        if ( changingEnabledBros )
                        {
                            displayWarningTime = 0f;
                            settings.filterBros = true;
                            CreateFilteredBroList();
                        }
                        else
                        {
                            // Check that at least one bro is enabled
                            bool atleastOne = false;
                            for (int x = 0; x < filteredBroList.Length; ++x)
                            {
                                if (filteredBroList[x])
                                {
                                    atleastOne = true;
                                    break;
                                }
                            }
                            if ( atleastOne )
                            {
                                displayWarningTime = 0f;
                                UpdateFilteredBroList();
                                CreateBroList();
                            }
                            else
                            {
                                displayWarningTime = 10f;
                                changingEnabledBros = !changingEnabledBros;
                            }
                        }
                    }

                    lastRect = GUILayoutUtility.GetLastRect();
                    lastRect.y += 20;
                    lastRect.width += 300;

                    // Don't allow sorting method to change while filtering bros
                    if ( !changingEnabledBros )
                    {
                        GUILayout.FlexibleSpace();
                        // Display option to change sorting method
                        if ( GUILayout.Button(new GUIContent(settings.sortingMethodName, "Change the sorting used for displaying the bro list"), GUILayout.Width(300)) )
                        {
                            switch (settings.sorting)
                            {
                                case SortingMethod.UnlockOrder:
                                    settings.sorting = SortingMethod.AlphabeticalAZ;
                                    settings.sortingMethodName = "Sorting Method: Alphabetical A-Z";
                                    break;
                                case SortingMethod.AlphabeticalAZ:
                                    settings.sorting = SortingMethod.AlphabeticalZA;
                                    settings.sortingMethodName = "Sorting Method: Alphabetical Z-A";
                                    break;
                                case SortingMethod.AlphabeticalZA:
                                    settings.sorting = SortingMethod.UnlockOrder;
                                    settings.sortingMethodName = "Sorting Method: Unlock Order";
                                    break;
                            }
                            CreateBroList();
                        }
                    }

                    if (displayWarningTime <= 0f)
                    {
                        if (!GUI.tooltip.Equals(previousToolTip))
                        {
                            GUI.Label(lastRect, GUI.tooltip);
                        }
                        previousToolTip = GUI.tooltip;
                    }
                    else
                    {
                        displayWarningTime -= Time.unscaledDeltaTime;
                        GUI.Label(lastRect, "Must have at least one bro enabled", warningStyle);

                        previousToolTip = GUI.tooltip;
                    }

                    GUILayout.EndHorizontal();

                    GUILayout.Space(25);

                    GUILayout.BeginHorizontal();

                    if ( !creatingBroList )
                    {
                        // Display filtering menu
                        if (changingEnabledBros)
                        {
                            GUILayout.BeginVertical();
                            GUILayout.BeginHorizontal();
                            for ( int j = 0; j < allBros.Count(); ++j )
                            {
                                if ( j % 5 == 0 )
                                {
                                    GUILayout.EndHorizontal();
                                    GUILayout.BeginHorizontal();
                                }

                                filteredBroList[j] = GUILayout.Toggle(filteredBroList[j], allBros[j], buttonStyle, GUILayout.Height(26), GUILayout.Width(180));
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(20);
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button("Select All", GUILayout.Width(200)))
                            {
                                for (int j = 0; j < filteredBroList.Length; ++j)
                                {
                                    filteredBroList[j] = true;
                                }
                            }
                            if (GUILayout.Button("Unselect All", GUILayout.Width(200)))
                            {
                                for (int j = 0; j < filteredBroList.Length; ++j)
                                {
                                    filteredBroList[j] = false;
                                }
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.EndVertical();
                        }
                        // Display bro selection menu
                        else
                        {
                            if (settings.selGridInt[i] < 0 || settings.selGridInt[i] >= broList.Length)
                            {
                                settings.selGridInt[i] = 0;
                            }

                            if (settings.selGridInt[i] <= maxBroNum)
                            {
                                if (settings.clickingEnabled)
                                {
                                    if (settings.selGridInt[i] != (settings.selGridInt[i] = GUILayout.SelectionGrid(settings.selGridInt[i], broList, 5, GUILayout.Height(30 * Mathf.Ceil(broList.Length / 5.0f)))) 
                                        && (Map.Instance != null) )
                                    {
                                        switched[i] = true;
                                    }
                                }
                                else
                                {
                                    settings.selGridInt[i] = GUILayout.SelectionGrid(settings.selGridInt[i], broList, 5, GUILayout.Height(30 * Mathf.Ceil(broList.Length / 5.0f)));
                                }
                            }
                            else
                            {
                                CreateBroList();
                            }
                        }
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                }
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        public static string[] axis6 = { "Joy1 Axis 6", "Joy2 Axis 6", "Joy3 Axis 6", "Joy4 Axis 6" };
        public static string[] axis7 = { "Joy1 Axis 7", "Joy2 Axis 7", "Joy3 Axis 7", "Joy4 Axis 7" };
        public static void CheckDPADDirection( ref KeyBind kb )
        {
            for ( int i = 0; i < 4; ++i )
            {
                float x = Input.GetAxis( axis6[i] );
                float y = Input.GetAxis( axis7[i] );

                if (x < -0.8)
                {
                    kb.DPADString = "Joy" + (i + 1) + " LEFT";
                    kb.DPADKey = DPAD.LEFT;
                    kb.joystick = "Joy" + (i + 1) + " Axis 6";
                    return;
                }
                else if (x > 0.8)
                {
                    kb.DPADString = "Joy" + (i + 1) + " RIGHT";
                    kb.DPADKey = DPAD.RIGHT;
                    kb.joystick = "Joy" + (i + 1) + " Axis 6";
                    return;
                }
                else if (y < -0.8)
                {
                    kb.DPADString = "Joy" + (i + 1) + " DOWN";
                    kb.DPADKey = DPAD.DOWN;
                    kb.joystick = "Joy" + (i + 1) + " Axis 7";
                    return;
                }
                else if (y > 0.8)
                {
                    kb.DPADString = "Joy" + (i + 1) + " UP";
                    kb.DPADKey = DPAD.UP;
                    kb.joystick = "Joy" + (i + 1) + " Axis 7";
                    return;
                }
            }
            kb.DPADString = "NONE";
            kb.DPADKey = DPAD.NONE;
            kb.joystick = "NONE";
        }

        private static IEnumerator BindKey(KeyBind kb, int player)
        {
            InputReader.IsBlocked = true;
            yield return new WaitForSeconds(0.2f);
            KeyCode[] keyCodes = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().ToArray();
            bool exit = false;
            while (!exit)
            {
                foreach (KeyCode keyCode in keyCodes)
                {
                    if (Input.GetKeyUp(keyCode))
                    {
                        if (keyCode == KeyCode.Delete)
                        {
                            kb.kc = KeyCode.None; 
                        }
                        else
                        {
                            kb.kc = keyCode;
                        }
                        kb.waitingForInput = false;
                        kb.DPADKey = DPAD.NONE;
                        exit = true;
                    }
                }
                CheckDPADDirection(ref kb);
                if ( kb.DPADKey != DPAD.NONE )
                {
                    kb.kc = KeyCode.None;
                    kb.waitingForInput = false;
                    exit = true;
                }
                yield return null;
            }
            InputReader.IsBlocked = false;
        }

        public static bool wasKeyPressed( KeyBind kb )
        {
            if ( kb.kc != KeyCode.None )
            {
                return Input.GetKeyUp(kb.kc);
            }
            else if ( kb.DPADKey != DPAD.NONE )
            {
                float x = Input.GetAxis( kb.joystick );

                return (x < -0.8 && (kb.DPADKey == DPAD.LEFT || kb.DPADKey == DPAD.DOWN)) || (x > 0.8 && (kb.DPADKey == DPAD.RIGHT || kb.DPADKey == DPAD.DOWN));
            }
            return false;
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

        public static void Log(String str)
        {
            mod.Logger.Log(str);
        }

        public static void CreateBroList()
        {
            creatingBroList = true;
            brosRemoved = false;
            if ( currentBroList != null )
            {
                for ( int i = 0; i < 4; ++i )
                {
                    if ( settings.selGridInt[i] > 0 && settings.selGridInt[i] < currentBroList.Count() )
                    {
                        previousSelection[i] = currentBroList[settings.selGridInt[i]];
                    }
                }
            }
            // If in IronBro and not ignoring unlocked characters, only show unlocked ones
            if ( GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked )
            {
                currentBroList = new List<string>();
                for (int i = 0; i < GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count(); ++i)
                {
                    currentBroList.Add(HeroTypeToString(GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros[i]));
                }

                if (settings.enableBromaker)
                {
                    LoadCustomBros();
                    currentBroList.AddRange(allCustomBros);
                }
            }
            else
            {
                if ( settings.filterBros && settings.enabledBros.Count() > 0 )
                {
                    currentBroList = new List<string>();
                    for ( int i = 0; i < allNormal.Count(); ++i )
                    {
                        if (settings.enabledBros.Contains(allNormal[i]) )
                        {
                            currentBroList.Add(allNormal[i]);
                        }
                        else
                        {
                            brosRemoved = true;
                        }
                    }

                    if (settings.includeExpendabros)
                    {
                        for ( int i = 0; i < allExpendabros.Count(); ++i )
                        {
                            if (settings.enabledBros.Contains(allExpendabros[i]) )
                            {
                                currentBroList.Add(allExpendabros[i]);
                            }
                            else
                            {
                                brosRemoved = true;
                            }
                        }
                    }
                    if (settings.includeUnfinishedCharacters)
                    {
                        for (int i = 0; i < allUnfinished.Count(); ++i)
                        {
                            if (settings.enabledBros.Contains(allUnfinished[i]))
                            {
                                currentBroList.Add(allUnfinished[i]);
                            }
                            else
                            {
                                brosRemoved = true;
                            }
                        }
                    }
                    if (settings.enableBromaker)
                    {
                        for ( int i = 0; i < allCustomBros.Count(); ++i )
                        {
                            if (settings.enabledBros.Contains(allCustomBros[i]) )
                            {
                                currentBroList.Add(allCustomBros[i]);
                            }
                            else
                            {
                                brosRemoved = true;
                            }
                        }
                    }
                }
                else
                {
                    currentBroList = new List<string>();
                    currentBroList.AddRange(allNormal);

                    if (settings.includeExpendabros)
                    {
                        currentBroList.AddRange(allExpendabros);
                    }
                    if (settings.includeUnfinishedCharacters)
                    {
                        currentBroList.AddRange(allUnfinished);
                    }
                    if (settings.enableBromaker)
                    {
                        currentBroList.AddRange(allCustomBros);
                    }
                }
            }

            maxBroNum = currentBroList.Count() - 1;

            if (settings.sorting == SortingMethod.AlphabeticalAZ)
            {
                currentBroList.Sort();
            }
            else if (settings.sorting == SortingMethod.AlphabeticalZA)
            {
                currentBroList.Sort();
                currentBroList.Reverse();
            }

            broList = currentBroList.ToArray();
            for (int i = 0; i < 4; ++i)
            {
                settings.selGridInt[i] = currentBroList.IndexOf(previousSelection[i]);
                if ( settings.selGridInt[i] == -1 )
                {
                    settings.selGridInt[i] = 0;
                }
            }

            currentBroListUnseen.Clear();
            currentBroListUnseen.AddRange(currentBroList);
            creatingBroList = false;
        }

        public static void CreateFilteredBroList()
        {
            allBros = new List<string>();
            allBros.AddRange(allNormal);
            if (settings.includeExpendabros)
            {
                allBros.AddRange(allExpendabros);
            }
            if (settings.includeUnfinishedCharacters)
            {
                allBros.AddRange(allUnfinished);
            }
            if (settings.enableBromaker)
            {
                allBros.AddRange(allCustomBros);
            }

            if (settings.sorting == SortingMethod.AlphabeticalAZ)
            {
                allBros.Sort();
            }
            else if (settings.sorting == SortingMethod.AlphabeticalZA)
            {
                allBros.Sort();
                allBros.Reverse();
            }

            filteredBroList = Enumerable.Repeat(false, allBros.Count()).ToArray();
            // Find index of the enabled bro in allBros and set the corresponding index in filteredBroList to true
            for (int i = 0; i < settings.enabledBros.Count(); ++i)
            {
                int index = allBros.IndexOf(settings.enabledBros[i]);
                if (index != -1)
                {
                    filteredBroList[index] = true;
                }
            }
        }

        public static void UpdateFilteredBroList()
        {
            settings.enabledBros.Clear();
            for ( int i = 0; i < filteredBroList.Length; ++i )
            {
                if (filteredBroList[i])
                {
                    settings.enabledBros.Add(allBros[i]);
                }
            }
        }

        public static bool CustomCountChanged()
        {
            return MakerObjectStorage.Bros.Length != allCustomBros.Count();
        }

        public static void LoadCustomBros()
        {
            if ( GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked )
            {
                allCustomBros = BSett.instance.availableBros;
                actuallyAllCustomBros = new List<string>();
                for (int i = 0; i < MakerObjectStorage.Bros.Length; ++i)
                {
                    actuallyAllCustomBros.Add(MakerObjectStorage.Bros[i].name);
                }
            }
            else
            {
                allCustomBros = new List<string>();
                for (int i = 0; i < MakerObjectStorage.Bros.Length; ++i)
                {
                    allCustomBros.Add(MakerObjectStorage.Bros[i].name);
                }
                actuallyAllCustomBros = allCustomBros;
            }
            
        }

        public static bool IsBroCustom(int index)
        {
            return actuallyAllCustomBros.Contains(currentBroList[index]);
        }

        public static string GetSelectedBroName(int playerNum)
        {
            return currentBroList[settings.selGridInt[playerNum]];
        }

        public static HeroType GetSelectedBroHeroType(int playerNum)
        {
            return StringToHeroType(currentBroList[settings.selGridInt[playerNum]]);
        }

        public static void SetSelectedBro(int playerNum, HeroType nextHero)
        {
            if (GameModeController.IsHardcoreMode && !settings.ignoreCurrentUnlocked)
            {
                settings.selGridInt[playerNum] = currentBroList.IndexOf(HeroTypeToString(nextHero));
                if ( settings.selGridInt[playerNum] == -1)
                {
                    settings.selGridInt[playerNum] = 0;
                }
            }
            else
            {
                settings.selGridInt[playerNum] = currentBroList.IndexOf( HeroTypeToString(nextHero) );
            }
        }

        public static bool CheckIfCustomBro(TestVanDammeAnim character, ref string name)
        {
            if ( character is CustomHero )
            {
                name = (character as CustomHero).info.name;
                return true;
            }

            return false;
        }

        public static void MakeCustomBroSpawn(int curPlayer, string name)
        {
            LoadHero.willReplaceBro[curPlayer] = true;
            BSett.instance.overrideNextBroSpawn = true;
            BSett.instance.nextBroSpawn = name;
        }

        public static void EnableCustomBroSpawning()
        {
            BSett.instance.disableSpawning = false;
        }

        public static void DisableCustomBroSpawning(int curPlayer)
        {
            BSett.instance.disableSpawning = true;
            LoadHero.willReplaceBro[curPlayer] = false;
        }

        public static bool IsCustomBroSpawning(int curPlayer)
        {
            return LoadHero.willReplaceBro[curPlayer];
        }

        public static bool IsCustcenePlaying()
        {
            return LoadHero.playCutscene;
        }

        public static int GetHardcoreCount()
        {
            if ( settings.enableBromaker )
            {
                return GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count() + GetBromakerHardcoreAmount();
            }
            else
            {
                return GameState.Instance.currentWorldmapSave.hardcoreModeAvailableBros.Count();
            }
        }

        public static int GetBromakerHardcoreAmount()
        {
            return BSett.instance.availableBros.Count();
        }

        public static string HeroTypeToString(HeroType hero)
        {
            switch (hero)
            {
                case HeroType.Rambro: return "Rambro";
                case HeroType.Brommando: return "Brommando";
                case HeroType.BaBroracus: return "B. A. Broracus";
                case HeroType.BrodellWalker: return "Brodell Walker";
                case HeroType.BroHard: return "Bro Hard";
                case HeroType.McBrover: return "MacBrover";
                case HeroType.Blade: return "Brade";
                case HeroType.BroDredd: return "Bro Dredd";
                case HeroType.Brononymous: return "Bro In Black";
                case HeroType.SnakeBroSkin: return "Snake Broskin";
                case HeroType.Brominator: return "Brominator";
                case HeroType.Brobocop: return "Brobocop";
                case HeroType.IndianaBrones: return "Indiana Brones";
                case HeroType.AshBrolliams: return "Ash Brolliams";
                case HeroType.Nebro: return "Mr. Anderbro";
                case HeroType.BoondockBros: return "The Boondock Bros";
                case HeroType.Brochete: return "Brochete";
                case HeroType.BronanTheBrobarian: return "Bronan the Brobarian";
                case HeroType.EllenRipbro: return "Ellen Ripbro";
                case HeroType.TimeBroVanDamme: return "Time Bro";
                case HeroType.BroniversalSoldier: return "Broniversal Soldier";
                case HeroType.ColJamesBroddock: return "Colonel James Broddock";
                case HeroType.CherryBroling: return "Cherry Broling";
                case HeroType.BroMax: return "Bro Max";
                case HeroType.TheBrode: return "The Brode";
                case HeroType.DoubleBroSeven: return "Double Bro Seven";
                case HeroType.Predabro: return "The Brodator";
                case HeroType.TheBrocketeer: return "The Brocketeer";
                case HeroType.BroveHeart: return "Broheart";
                case HeroType.TheBrofessional: return "The Brofessional";
                case HeroType.Broden: return "Broden";
                case HeroType.TheBrolander: return "The Brolander";
                case HeroType.DirtyHarry: return "Dirty Brory";
                case HeroType.TankBro: return "Tank Bro";
                case HeroType.BroLee: return "Bro Lee";
                case HeroType.BrondleFly: return "Seth Brondle";
                case HeroType.Xebro: return "Xebro";
                case HeroType.Desperabro: return "Desperabro";
                case HeroType.Broffy: return "Broffy the Vampire Slayer";
                case HeroType.BroGummer: return "Burt Brommer";
                case HeroType.DemolitionBro: return "Demolition Bro";

                // Expendabros
                case HeroType.BroneyRoss: return "Broney Ross";
                case HeroType.LeeBroxmas: return "Lee Broxmas";
                case HeroType.BronnarJensen: return "Bronnar Jensen";
                case HeroType.HaleTheBro: return "Bro Caesar";
                case HeroType.TrentBroser: return "Trent Broser";
                case HeroType.Broc: return "Broctor Death";
                case HeroType.TollBroad: return "Toll Broad";

                // Unfinished
                case HeroType.ChevBrolios: return "Chev Brolios";
                case HeroType.CaseyBroback: return "Casey Broback";
                case HeroType.ScorpionBro: return "The Scorpion Bro";
            }
            return "";
        }

        public static HeroType StringToHeroType(string hero)
        {
            switch (hero)
            {
                case "Rambro": return HeroType.Rambro;
                case "Brommando": return HeroType.Brommando;
                case "B. A. Broracus": return HeroType.BaBroracus;
                case "Brodell Walker": return HeroType.BrodellWalker;
                case "Bro Hard": return HeroType.BroHard;
                case "MacBrover": return HeroType.McBrover;
                case "Brade": return HeroType.Blade;
                case "Bro Dredd": return HeroType.BroDredd;
                case "Bro In Black": return HeroType.Brononymous;
                case "Snake Broskin": return HeroType.SnakeBroSkin;
                case "Brominator": return HeroType.Brominator;
                case "Brobocop": return HeroType.Brobocop;
                case "Indiana Brones": return HeroType.IndianaBrones;
                case "Ash Brolliams": return HeroType.AshBrolliams;
                case "Mr. Anderbro": return HeroType.Nebro;
                case "The Boondock Bros": return HeroType.BoondockBros;
                case "Brochete": return HeroType.Brochete;
                case "Bronan the Brobarian": return HeroType.BronanTheBrobarian;
                case "Ellen Ripbro": return HeroType.EllenRipbro;
                case "Time Bro": return HeroType.TimeBroVanDamme;
                case "Broniversal Soldier": return HeroType.BroniversalSoldier;
                case "Colonel James Broddock": return HeroType.ColJamesBroddock;
                case "Cherry Broling": return HeroType.CherryBroling;
                case "Bro Max": return HeroType.BroMax;
                case "The Brode": return HeroType.TheBrode;
                case "Double Bro Seven": return HeroType.DoubleBroSeven;
                case "The Brodator": return HeroType.Predabro;
                case "The Brocketeer": return HeroType.TheBrocketeer;
                case "Broheart": return HeroType.BroveHeart;
                case "The Brofessional": return HeroType.TheBrofessional;
                case "Broden": return HeroType.Broden;
                case "The Brolander": return HeroType.TheBrolander;
                case "Dirty Brory": return HeroType.DirtyHarry;
                case "Tank Bro": return HeroType.TankBro;
                case "Bro Lee": return HeroType.BroLee;
                case "Seth Brondle": return HeroType.BrondleFly;
                case "Xebro": return HeroType.Xebro;
                case "Desperabro": return HeroType.Desperabro;
                case "Broffy the Vampire Slayer": return HeroType.Broffy;
                case "Burt Brommer": return HeroType.BroGummer;
                case "Demolition Bro": return HeroType.DemolitionBro;
                
                // Expendabros
                case "Broney Ross": return HeroType.BroneyRoss;
                case "Lee Broxmas": return HeroType.LeeBroxmas;
                case "Bronnar Jensen": return HeroType.BronnarJensen;
                case "Bro Caesar": return HeroType.HaleTheBro;
                case "Trent Broser": return HeroType.TrentBroser;
                case "Broctor Death": return HeroType.Broc;
                case "Toll Broad": return HeroType.TollBroad;

                // Unfinished
                case "Chev Brolios": return HeroType.ChevBrolios;
                case "Casey Broback": return HeroType.CaseyBroback;
                case "The Scorpion Bro": return HeroType.ScorpionBro;
            }
            return HeroType.None;
        }
    }
}

    
