using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime;
using UnityEngine;
using static Text3D;
using static UnityEngine.UI.CanvasScaler;
using Net = Networking.Networking;

namespace Control_Enemies_Mod
{
    public class HarmonyPatches
    {
        #region General Patches
        // Disable AI of enemy we're controlling
        [HarmonyPatch(typeof(PolymorphicAI), "Update")]
        static class PolymorphicAI_Update_Patch
        {
            public static bool Prefix(PolymorphicAI __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if ( __instance.name == "controlled" )
                {
                    __instance.mentalState = MentalState.Alerted;
                    return false;
                }

                return true;
            }
        }

        // Disable AI of enemy we're controlling
        [HarmonyPatch(typeof(PolymorphicAI), "LateUpdate")]
        static class PolymorphicAI_LateUpdate_Patch
        {
            public static bool Prefix(PolymorphicAI __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    __instance.mentalState = MentalState.Alerted;
                    return false;
                }

                return true;
            }
        }

        // Disable AI of enemy we're controlling
        [HarmonyPatch(typeof(TestVanDammeAnim), "GetEnemyMovement")]
        static class TestVanDammeAnim_GetEnemyMovement_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable taunting for mooks
        [HarmonyPatch(typeof(TestVanDammeAnim), "SetGestureAnimation")]
        static class TestVanDammeAnim_SetGestureAnimation_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance, ref GestureElement.Gestures gesture)
            {
                if (!Main.enabled || !Main.settings.disableTaunting)
                {
                    return true;
                }

                if (gesture == GestureElement.Gestures.Flex && __instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Make player respawn at mook if that option is enabled
        [HarmonyPatch(typeof(TestVanDammeAnim), "Death", new Type[] { typeof(float), typeof(float), typeof(DamageObject) })]
        static class TestVanDammeAnim_Death_Patch
        {
            public static bool outOfBounds = false;

            public static void Prefix(TestVanDammeAnim __instance, ref float xI, ref float yI, ref DamageObject damage)
            {
                if (!Main.enabled)
                {
                    return;
                }

                if (__instance.name == "controlled")
                {
                    // Track whether we're dying from out-of-bounds to know if we should be able to respawn from corpse
                    outOfBounds = damage.damageType == DamageType.OutOfBounds;
                }
            }

            public static void Postfix(TestVanDammeAnim __instance, ref float xI, ref float yI, ref DamageObject damage)
            {
                if (!Main.enabled)
                {
                    return;
                }

                if (__instance.name == "controlled")
                {
                    // If we're not respawning from a corpse or we don't have enough lives left, release the unit
                    if ( !(Main.settings.respawnFromCorpse && HeroController.players[__instance.playerNum].Lives > 0 && !outOfBounds && Main.previousCharacter[__instance.playerNum] != null ) )
                    {
                        Main.LeaveUnit(__instance, __instance.playerNum, true);
                    }
                }
            }
        }

        // Prevent game from reporting death to prevent respawn
        [HarmonyPatch(typeof(TestVanDammeAnim), "ReduceLives")]
        static class TestVanDammeAnim_ReduceLives_Patch
        {
            public static bool ignoreNextLifeLoss = false;

            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                // Don't report death if we don't want to lose lives when dying as a mook or if we want to respawn at their corpse
                if (__instance.name == "controlled" && __instance.playerNum >= 0 && __instance.playerNum < 4 && Main.currentlyEnemy[__instance.playerNum])
                {
                    bool chestBurstDeath = false;
                    if (__instance is AlienFaceHugger)
                    {
                        AlienFaceHugger faceHugger = __instance as AlienFaceHugger;
                        if (faceHugger.insemenationCompleted && faceHugger.layEggsInsideBros)
                        {
                            chestBurstDeath = true;
                            AlienXenomorph_Start_Patch.controlNextAlien = true;
                            AlienXenomorph_Start_Patch.controllerPlayerNum = __instance.playerNum;
                        }
                    }
                    // If we're respawning from corpse and didn't die from out of bounds or if we're spawning from a chestburster
                    if ((Main.settings.respawnFromCorpse && !TestVanDammeAnim_Death_Patch.outOfBounds && Main.previousCharacter[__instance.playerNum] != null) || chestBurstDeath)
                    {
                        if (Main.settings.loseLifeOnDeath && !chestBurstDeath)
                        {
                            HeroController.players[__instance.playerNum].RemoveLife();
                        }

                        // Setup timer to countdown to respawn
                        if (HeroController.players[__instance.playerNum].Lives > 0)
                        {
                            Main.countdownToRespawn[__instance.playerNum] = chestBurstDeath ? 10f : 0.6f;
                        }
                        // Actually die since we're out of lives
                        else
                        {
                            return true;
                        }

                        return false;
                    }
                    else if (!Main.settings.loseLifeOnDeath)
                    {
                        ignoreNextLifeLoss = true;
                    }
                }

                return true;
            }
        }

        // Prevent life loss when controlling enemy if life loss is disabled
        [HarmonyPatch(typeof(Player), "RemoveLife")]
        static class Player_RemoveLife_Patch
        {
            public static bool Prefix()
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if ( TestVanDammeAnim_ReduceLives_Patch.ignoreNextLifeLoss )
                {
                    TestVanDammeAnim_ReduceLives_Patch.ignoreNextLifeLoss = false;
                    return false;
                }

                return true;
            }
        }

        // Prevent HeroController from knowing our mook is dead to prevent us from respawning
        [HarmonyPatch(typeof(Player), "IsAlive")]
        static class Player_IsAlive_Patch
        {
            public static bool Prefix(Player __instance, ref bool __result)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if (Main.countdownToRespawn[__instance.playerNum] > 0)
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        // Prevent HeroController from knowing our mook is dead to prevent us from respawning
        [HarmonyPatch(typeof(PlayerHUD), "SetAvatarDead")]
        static class PlayerHUD_SetAvatarDead_Patch
        {
            public static bool Prefix(PlayerHUD __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                Traverse trav = Traverse.Create(__instance);
                int playerNum = (int)trav.Field("playerNum").GetValue();

                if (Main.countdownToRespawn[playerNum] > 0)
                {
                    trav.Field("SetToDead").SetValue(true);
                    return false;
                }

                return true;
            }
        }


        // Ensure heavy units are allowed on the helicopter
        [HarmonyPatch(typeof(TestVanDammeAnim), "IsOverFinish")]
        static class TestVanDammeAnim_IsOverFinish_Patch
        {
            public static LayerMask victoryLayer = 1 << LayerMask.NameToLayer("Finish");

            public static float AttachToHelicopter(TestVanDammeAnim __instance, float ladderXPos, Helicopter helicopter)
            {
                helicopter.attachedHeroes.Add(__instance);
                ladderXPos = helicopter.transform.position.x + 13f;
                helicopter.Leave();
                __instance.transform.parent = helicopter.ladderHolder;
                float x = helicopter.transform.position.x;
                if (__instance.X > x)
                {
                    __instance.transform.localScale = new Vector3(-1f, 1f, 1f);
                    __instance.X = x + 5f;
                }
                else
                {
                    __instance.transform.localScale = new Vector3(1f, 1f, 1f);
                    __instance.X = x - 5f;
                }
                __instance.transform.position = new Vector3(Mathf.Round(__instance.X), Mathf.Round(__instance.Y), -50f);
                return ladderXPos;
            }

            public static bool Prefix(TestVanDammeAnim __instance, ref float ladderXPos, ref bool __result)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                if ( __instance.name == "controlled" && __instance.IsHeavy() )
                {
                    __result = false;
                    if (__instance.playerNum >= 0 && __instance.playerNum < 5)
                    {
                        Collider[] array = Physics.OverlapSphere(new Vector3(__instance.X, __instance.Y, 0f), 4f, victoryLayer);
                        if (array.Length > 0)
                        {
                            __instance.invulnerable = true;
                            if (__instance.GetComponent<AudioSource>() != null)
                            {
                                __instance.GetComponent<AudioSource>().Stop();
                            }
                            __instance.enabled = false;
                            if (array[0].transform.parent != null)
                            {
                                HelicopterFake component = array[0].transform.parent.GetComponent<HelicopterFake>();
                                if (component != null || Map.MapData.onlyTriggersCanWinLevel)
                                {
                                    Helicopter component2 = array[0].transform.parent.GetComponent<Helicopter>();
                                    ladderXPos = AttachToHelicopter(__instance, ladderXPos, component2);
                                    Net.RPC<Vector3, float, TestVanDammeAnim, Helicopter, bool>(PID.TargetAll, new RpcSignature<Vector3, float, TestVanDammeAnim, Helicopter, bool>(HeroController.Instance.AttachHeroToHelicopter), __instance.transform.localPosition, __instance.transform.localScale.x, __instance, component2, false, false);
                                    __result = true;
                                }
                                Helicopter component3 = array[0].transform.parent.GetComponent<Helicopter>();
                                if (component3 != null)
                                {
                                    ladderXPos = AttachToHelicopter(__instance, ladderXPos, component3);
                                    Net.RPC<Vector3, float, TestVanDammeAnim, Helicopter, bool>(PID.TargetAll, new RpcSignature<Vector3, float, TestVanDammeAnim, Helicopter, bool>(HeroController.Instance.AttachHeroToHelicopter), __instance.transform.localPosition, __instance.transform.localScale.x, __instance, component3, true, false);
                                }
                            }
                            Traverse trav = Traverse.Create(__instance);
                            trav.Field("jumpTime").SetValue(0.07f);
                            trav.Field("doubleJumpsLeft").SetValue(0);
                            HeroLevelExitPortal component4 = array[0].GetComponent<HeroLevelExitPortal>();
                            if (component4 != null)
                            {
                                typeof(TestVanDammeAnim).GetMethod("SuckIntoPortal", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, null);
                            }
                            GameModeController.LevelFinish(LevelResult.Success);
                            Map.StartLevelEndExplosionsOverNetwork();
                            __instance.isOnHelicopter = true;
                            __instance.playerNum = 5;
                            __result = true;
                        }
                    }
                    return false;
                }

                return true;
            }
        }

        // Clear previous character when rescuing a bro
        [HarmonyPatch(typeof(TestVanDammeAnim), "RecallBro")]
        static class TestVanDammeAnim_RecallBro_Patch
        {
            public static void Prefix(TestVanDammeAnim __instance)
            {
                if (!Main.enabled)
                {
                    return;
                }

                if ( __instance.playerNum >= 0 && __instance.playerNum < 4 )
                {
                    Main.previousCharacter[__instance.playerNum] = null;
                }
            }
        }

        // Used for competitive mode to allow players to kill each other
        [HarmonyPatch(typeof(GameModeController), "DoesPlayerNumDamage")]
        static class GameModeController_DoesPlayerNumDamage_Patch
        {
            public static void Postfix(GameModeController __instance, ref int fromNum, ref int toNum, ref bool __result)
            {
                if (!Main.enabled)
                {
                    return;
                }

                // FIXME: Override this to make versus mode work properly, need to have ghosts be able to damage main player, and have enemies be unable to damage ghosts
            }
        }

        // Prevent enemies from trying to show the start bubble
        [HarmonyPatch(typeof(TestVanDammeAnim), "ShowStartBubble")]
        static class TestVanDammeAnim_ShowStartBubble_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                // Don't let method execute if this is a controlled enemy
                return __instance.name != "controlled";
            }
        }

        // Prevent enemies from trying to show the start bubble
        [HarmonyPatch(typeof(TestVanDammeAnim), "RestartBubble", new Type[] { typeof(float) } )]
        static class TestVanDammeAnim_RestartBubble_float_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                // Don't let method execute if this is a controlled enemy
                return __instance.name != "controlled";
            }
        }

        // Prevent enemies from trying to show the start bubble
        [HarmonyPatch(typeof(TestVanDammeAnim), "RestartBubble", new Type[] {})]
        static class TestVanDammeAnim_RestartBubble_Patch
        {
            public static bool Prefix(TestVanDammeAnim __instance)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                // Don't let method execute if this is a controlled enemy
                return __instance.name != "controlled";
            }
        }

        // Use different avatar for players who are controlling enemies
        [HarmonyPatch(typeof(HeroController), "SwitchAvatarMaterial")]
        static class HeroController_SwitchAvatarMaterial_Patch
        {
            public static bool Prefix(ref SpriteSM sprite, ref bool __result)
            {
                if (!Main.enabled)
                {
                    return true;
                }

                PlayerHUD hud = sprite.gameObject.GetComponent<PlayerHUD>();
                if ( hud != null )
                {
                    int playerNum = (int) Traverse.Create(hud).Field("playerNum").GetValue();
                    // If we're assigning an avatar to a player who is an enemy, use a different avatar instead of the bro one
                    if (Main.currentlyEnemy[playerNum])
                    {
                        sprite.GetComponent<Renderer>().material = Main.defaultAvatarMat;
                        return false;
                    }
                }

                return true;
            }
        }
        #endregion

        #region Enemy Specific Patches
        // Fix controlled enemies taking fall damage
        #region FallDamagePatches
        // Disable fall damage for mooks
        [HarmonyPatch(typeof(Mook), "FallDamage")]
        static class Mook_FallDamage_Patch
        {
            public static bool Prefix(Mook __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for bruisers
        [HarmonyPatch(typeof(MookBigGuy), "FallDamage")]
        static class MookBigGuy_FallDamage_Patch
        {
            public static bool Prefix(MookBigGuy __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for aliens
        [HarmonyPatch(typeof(Alien), "FallDamage")]
        static class Alien_FallDamage_Patch
        {
            public static bool Prefix(Alien __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for AlienFaceHugger
        [HarmonyPatch(typeof(AlienFaceHugger), "FallDamage")]
        static class AlienFaceHugger_FallDamage_Patch
        {
            public static bool Prefix(AlienFaceHugger __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for AlienMelter
        [HarmonyPatch(typeof(AlienMelter), "FallDamage")]
        static class AlienMelter_FallDamage_Patch
        {
            public static bool Prefix(AlienMelter __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for Animal
        [HarmonyPatch(typeof(Animal), "FallDamage")]
        static class Animal_FallDamage_Patch
        {
            public static bool Prefix(Animal __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookArmouredGuy
        [HarmonyPatch(typeof(MookArmouredGuy), "FallDamage")]
        static class MookArmouredGuy_FallDamage_Patch
        {
            public static bool Prefix(MookArmouredGuy __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookBigGuyElite
        [HarmonyPatch(typeof(MookBigGuyElite), "FallDamage")]
        static class MookBigGuyElite_FallDamage_Patch
        {
            public static bool Prefix(MookBigGuyElite __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookDog
        [HarmonyPatch(typeof(MookDog), "FallDamage")]
        static class MookDog_FallDamage_Patch
        {
            public static bool Prefix(MookDog __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookHellBigGuy
        [HarmonyPatch(typeof(MookHellBigGuy), "FallDamage")]
        static class MookHellBigGuy_FallDamage_Patch
        {
            public static bool Prefix(MookHellBigGuy __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookHellBoomer
        [HarmonyPatch(typeof(MookHellBoomer), "FallDamage")]
        static class MookHellBoomer_FallDamage_Patch
        {
            public static bool Prefix(MookHellBoomer __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for MookSuicide
        [HarmonyPatch(typeof(MookSuicide), "FallDamage")]
        static class MookSuicide_FallDamage_Patch
        {
            public static bool Prefix(MookSuicide __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for SkinnedMook
        [HarmonyPatch(typeof(SkinnedMook), "FallDamage")]
        static class MSkinnedMook_FallDamage_Patch
        {
            public static bool Prefix(SkinnedMook __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }

        // Disable fall damage for Villager
        [HarmonyPatch(typeof(Villager), "FallDamage")]
        static class Villager_FallDamage_Patch
        {
            public static bool Prefix(Villager __instance, ref float yI)
            {
                if (!Main.enabled || !Main.settings.disableFallDamage)
                {
                    return true;
                }

                if (__instance.name == "controlled")
                {
                    return false;
                }

                return true;
            }
        }
        #endregion

        // Make spawned helldogs friendly
        #region HellDog
        [HarmonyPatch(typeof(HellDogEgg), "MakeEffects")]
        static class HellDogEgg_MakeEffects_Patch
        {
            public static bool nextDogFriendly = false;
            public static int playerNum = -1;

            public static void Prefix(HellDogEgg __instance)
            {
                if (!Main.enabled)
                {
                    return;
                }

                // If egg was fired by controlled enemy or friendly enemy, make it friendly
                if ( (__instance.firedBy.name == "controlled" || (__instance.firedBy as Mook).playerNum >= 0 ))
                {
                    nextDogFriendly = true;
                    playerNum = (__instance.firedBy as Mook).playerNum;
                }
            }
        }

        [HarmonyPatch(typeof(HellDog), "GrowFromEgg")]
        static class HellDog_GrowFromEgg_Patch
        {
            public static void Prefix(HellDog __instance)
            {
                if (!Main.enabled)
                {
                    return;
                }

                // Set this dog to friendly since it was spawned by a player
                if ( HellDogEgg_MakeEffects_Patch.nextDogFriendly )
                {
                    __instance.firingPlayerNum = HellDogEgg_MakeEffects_Patch.playerNum;
                    __instance.playerNum = HellDogEgg_MakeEffects_Patch.playerNum;
                    HellDogEgg_MakeEffects_Patch.nextDogFriendly = false;
                }
            }
        }
        #endregion

        // Become xenomorph when facehugging enemy
        #region Facehugger
        [HarmonyPatch(typeof(AlienXenomorph), "Start")]
        static class AlienXenomorph_Start_Patch
        {
            public static int controllerPlayerNum = -1;
            public static bool controlNextAlien = false;

            public static void Postfix(AlienXenomorph __instance)
            {
                if ( !Main.enabled )
                {
                    return;
                }

                // Automatically assume control of alien that spawned from facehugger that was controlled by a player
                if (controlNextAlien)
                {
                    Main.StartControllingUnit(controllerPlayerNum, __instance, true);
                    controlNextAlien = false;
                    Main.countdownToRespawn[controllerPlayerNum] = -1f;
                }
                // Make sure already controlled aliens don't have their playernum overwritten by the start function
                else if (__instance.name == "controlled")
                {
                    int playerNum = Main.currentUnit.IndexOf(__instance);
                    if (playerNum != __instance.playerNum)
                    {
                        Main.ReaffirmControl(playerNum);
                    }
                    __instance.SpecialAmmo = 0;
                }
            }
        }
        #endregion
        #endregion // End Enemy Specific Patches

        #region Spawning As Enemy Patches
        // Automatic spawn
        [HarmonyPatch(typeof(Player), "SpawnHero")]
        static class Player_SpawnHero_Patch
        {
            static void Prefix(Player __instance, ref HeroType nextHeroType)
            {
                if ( !Main.enabled )
                {
                    return;
                }

                Main.currentlyEnemy[__instance.playerNum] = false;

                if (Main.settings.spawnAsEnemyEnabled)
                {
                    Main.willReplaceBro[__instance.playerNum] = (Main.settings.spawnAsEnemyChance > 0 && Main.settings.spawnAsEnemyChance >= UnityEngine.Random.value * 100f);

                    // Disable BroMaker if it's installed
                    if (Main.willReplaceBro[__instance.playerNum])
                    {
                        Main.DisableBroMaker(__instance.playerNum);
                    }
                }
            }
            static void Postfix(Player __instance)
            {
                if ( !Main.enabled )
                {
                    return;
                }

                try
                {
                    if (Main.willReplaceBro[__instance.playerNum])
                    {
                        // Reenable BroMaker if it's installed
                        Main.EnableBroMaker();

                        Main.willReplaceBro[__instance.playerNum] = false;

                        if ( !Main.settings.alwaysChosen )
                        {
                            // Randomize unit
                            Main.settings.selGridInt[__instance.playerNum] = UnityEngine.Random.Range(0, Main.currentUnitList.Length - 1);
                        }
                        GameObject obj = Main.SpawnUnit(Main.GetSelectedUnit(__instance.playerNum), new Vector3(0f, 0f, 0f));
                        TestVanDammeAnim newUnit = obj.GetComponent<TestVanDammeAnim>();
                        Main.StartControllingUnit(__instance.playerNum, newUnit, false, false, true);
                        Main.WorkOutSpawnPosition(__instance, newUnit);
                    }
                    else
                    {
                        Main.previousCharacter[__instance.playerNum] = null;
                        Main.currentlyEnemy[__instance.playerNum] = false;
                    }
                }
                catch (Exception e)
                {
                    Main.Log("Exception Replacing Bro: " + e.ToString());
                }
            }
        }

        // Fix vanilla bro being visible for 1 frame in certain spawning situations
        [HarmonyPatch(typeof(Player), "InstantiateHero")]
        static class Player_InstantiateHero_Patch
        {
            static void Postfix(Player __instance, ref TestVanDammeAnim __result)
            {
                // If mod is disabled or if we aren't loading a custom character don't disable
                if (!Main.enabled || !Main.willReplaceBro[__instance.playerNum])
                {
                    return;
                }

                __result.gameObject.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(Map), "AddBroToHeroTransport")]
        static class Map_AddBroToHeroTransport_Patch
        {
            static bool Prefix(Map __instance, ref TestVanDammeAnim Bro)
            {
                // If mod is disabled or if we aren't loading a custom character don't disable
                return !Main.enabled || !Main.willReplaceBro[Bro.playerNum];
            }
        }

        [HarmonyPatch(typeof(Player), "WorkOutSpawnPosition")]
        static class Player_WorkOutSpawnPosition_Patch
        {
            static void Prefix(Player __instance, ref TestVanDammeAnim bro)
            {
                if (!Main.enabled)
                {
                    return;
                }

                if (Main.willReplaceBro[__instance.playerNum])
                {
                    Main.wasFirstDeployment[__instance.playerNum] = __instance.firstDeployment;
                }
            }
        }

        [HarmonyPatch(typeof(Player), "WorkOutSpawnScenario")]
        static class Player_WorkOutSpawnScenario_Patch
        {
            static void Postfix(Player __instance, ref Player.SpawnType __result)
            {
                if (!Main.enabled)
                {
                    return;
                }
                // Store spawning info of normal character so we can pass it on to the custom character
                else if (Main.willReplaceBro[__instance.playerNum])
                {
                    Main.previousSpawnInfo[__instance.playerNum] = __result;
                }
            }
        }

        [HarmonyPatch(typeof(HeroTransport), "ReleaseBros")]
        static class HeroTransport_ReleaseBros_Patch
        {
            public static void Postfix(HeroTransport __instance)
            {
                if (!Main.enabled)
                {
                    return;
                }

                for ( int i = 0; i < 4; ++i )
                {
                    if (HeroController.PlayerIsAlive(i) && Main.currentlyEnemy[i])
                    {
                        TestVanDammeAnim character = HeroController.players[i].character;
                        if ( character.enemyAI != null )
                        {
                            character.enemyAI.enabled = true;
                        }
                    }
                }
            }
        }

        // Check for swap button being pressed
        [HarmonyPatch(typeof(Player), "GetInput")]
        static class Player_GetInput_Patch
        {
            public static void Postfix(Player __instance)
            {
                if (!Main.enabled)
                {
                    return;
                }

                int curPlayer = __instance.playerNum;
                if (Main.settings.spawnAsEnemyEnabled)
                {
                    bool leftPressed = Main.swapEnemiesLeft.IsDown(curPlayer);
                    bool rightPressed = Main.swapEnemiesRight.IsDown(curPlayer);

                    if ((((leftPressed || rightPressed) && Main.currentSpawnCooldown[curPlayer] <= 0f && __instance.IsAlive()) || (Main.settings.clickingSwapEnabled && Main.switched[curPlayer])) && __instance.character.pilottedUnit == null)
                    {
                        float X, Y, XI, YI;
                        Vector3 vec = __instance.GetCharacterPosition();
                        X = vec.x;
                        Y = vec.y;
                        XI = (float)Traverse.Create(__instance.character).Field("xI").GetValue();
                        YI = (float)Traverse.Create(__instance.character).Field("yI").GetValue();

                        if (Main.settings.clickingSwapEnabled && Main.switched[curPlayer])
                        {
                            try
                            {
                                GameObject obj = Main.SpawnUnit(Main.GetSelectedUnit(curPlayer), vec);
                                TestVanDammeAnim newUnit = obj.GetComponent<TestVanDammeAnim>();
                                Main.StartControllingUnit(curPlayer, newUnit, false, false, true);

                                __instance.character.SetPositionAndVelocity(X, Y, XI, YI);
                                __instance.character.SetInvulnerable(0f, false);
                                Main.switched[curPlayer] = false;
                            }
                            catch (Exception ex)
                            {
                                Main.Log("ex: " + ex.ToString());
                            }
                            return;
                        }
                        else
                        {
                            if (leftPressed)
                            {
                                --Main.settings.selGridInt[curPlayer];
                                if (Main.settings.selGridInt[curPlayer] < 0)
                                {
                                    Main.settings.selGridInt[curPlayer] = Main.currentUnitList.Length - 1;
                                }
                            }
                            else if (rightPressed)
                            {
                                ++Main.settings.selGridInt[curPlayer];
                                if (Main.settings.selGridInt[curPlayer] > Main.currentUnitList.Length - 1)
                                {
                                    Main.settings.selGridInt[curPlayer] = 0;
                                }
                            }

                            GameObject obj = Main.SpawnUnit(Main.GetSelectedUnit(curPlayer), vec);
                            TestVanDammeAnim newUnit = obj.GetComponent<TestVanDammeAnim>();
                            Main.StartControllingUnit(curPlayer, newUnit, false, false, true);

                            __instance._character.SetPositionAndVelocity(X, Y, XI, YI);
                            __instance.character.SetInvulnerable(0f, false);

                            Main.currentSpawnCooldown[curPlayer] = Main.settings.spawnSwapCooldown;
                        }
                    }
                }

                if (Main.currentlyEnemy[curPlayer])
                {
                    bool special2Down = Main.special2[curPlayer].IsDown();
                    bool special3Down = Main.special3[curPlayer].IsDown();
                    // Pressed special2
                    if (!Main.special2[curPlayer].wasDown && special2Down)
                    {
                        if ( __instance.character is AlienClimber )
                        {
                            // Make climbers climb
                            Traverse.Create(__instance.character).SetFieldValue("climbButton", true);
                        }
                        else if (__instance.character is AlienMosquito)
                        {
                            // Make mosquito fly
                            AlienMosquito mosquito = __instance.character as AlienMosquito;
                            Traverse.Create(mosquito).SetFieldValue("flying", true);
                        }
                        else if (__instance.character is DolphLundrenSoldier)
                        {
                            // Give DolphLundren his super jump special manually
                            DolphLundrenSoldier character = __instance.character as DolphLundrenSoldier;
                            character.jumpForce = 1000;
                            Traverse.Create(__instance.character).SetFieldValue("usingSpecial2", true);
                        }
                        else
                        {
                            Traverse.Create(__instance.character).SetFieldValue("usingSpecial2", true);
                        }
                    }
                    // Release special2
                    else if (Main.special2[curPlayer].wasDown && !special2Down)
                    {
                        if (__instance.character is AlienClimber)
                        {
                            // Make climbers climb
                            Traverse.Create(__instance.character).SetFieldValue("climbButton", false);
                        }
                        else if ( __instance.character is AlienMosquito )
                        {
                            // Make mosquito fly
                            AlienMosquito mosquito = __instance.character as AlienMosquito;
                            Traverse.Create(mosquito).SetFieldValue("flying", false);
                        }
                        else if (__instance.character is DolphLundrenSoldier)
                        {
                            // Give DolphLundren his super jump special manually
                            DolphLundrenSoldier character = __instance.character as DolphLundrenSoldier;
                            character.jumpForce = 260;
                            Traverse.Create(__instance.character).SetFieldValue("usingSpecial2", false);
                        }
                        else
                        {
                            Traverse.Create(__instance.character).SetFieldValue("usingSpecial2", false);
                        }
                    }

                    // Pressed special3
                    if (!Main.special3[curPlayer].wasDown && special3Down)
                    {
                        if ( __instance.character is AlienMelter )
                        {
                            // Special 4 for alien melters to allow rolling attack
                            Traverse.Create(__instance.character).SetFieldValue("usingSpecial4", true);
                        }
                        else if (__instance.character is AlienMosquito)
                        {
                            // Make mosquito dive
                            AlienMosquito mosquito = __instance.character as AlienMosquito;
                            Traverse.Create(mosquito).SetFieldValue("diving", true);
                        }
                        else if ( __instance.character is DolphLundrenSoldier )
                        {
                            // Make sure dolph has seen the player to allow his special to activate
                            Traverse.Create(__instance.character.enemyAI).SetFieldValue("seenEnemyNum", 0);
                            Traverse.Create(__instance.character).SetFieldValue("usingSpecial3", true);
                        }
                        else
                        {
                            Traverse.Create(__instance.character).SetFieldValue("usingSpecial3", true);
                        }
                    }
                    // Release special3
                    else if (Main.special3[curPlayer].wasDown && !special3Down)
                    {
                        if ( __instance.character is AlienMelter )
                        {
                            // Special 4 for alien melters to allow rolling attack
                            Traverse.Create(__instance.character).SetFieldValue("usingSpecial4", false);
                        }
                        else if (__instance.character is AlienMosquito)
                        {
                            // Make mosquito dive
                            AlienMosquito mosquito = __instance.character as AlienMosquito;
                            Traverse.Create(mosquito).SetFieldValue("diving", false);
                        }
                        else
                        {
                            Traverse.Create(__instance.character).SetFieldValue("usingSpecial3", false);
                        }
                    }

                    Main.special2[curPlayer].wasDown = special2Down;
                    Main.special3[curPlayer].wasDown = special3Down;
                }
                return;
            }
        }

        // Don't let enemies have their playernum overwritten
        [HarmonyPatch(typeof(TestVanDammeAnim), "Start")]
        static class TestVanDammeAnim_Start_Patch
        {
            public static void Postfix(TestVanDammeAnim __instance)
            {
                if (!Main.enabled || !Main.settings.spawnAsEnemyEnabled)
                {
                    return;
                }

                if ( __instance.name == "controlled" )
                {
                    int playerNum = Main.currentUnit.IndexOf(__instance);
                    if (playerNum != __instance.playerNum)
                    {
                        Main.ReaffirmControl(playerNum);
                    }
                    __instance.SpecialAmmo = 0;
                }
            }
        }

        // Don't let enemies have their playernum overwritten
        [HarmonyPatch(typeof(Alien), "Start")]
        static class Alien_Start_Patch
        {
            public static void Postfix(Alien __instance)
            {
                if (!Main.enabled)
                {
                    return;
                }

                if (__instance.name == "controlled")
                {
                    int playerNum = Main.currentUnit.IndexOf(__instance);
                    if (playerNum != __instance.playerNum)
                    {
                        Main.ReaffirmControl(playerNum);
                    }
                    __instance.SpecialAmmo = 0;
                }
            }
        }
        #endregion
    }
}
