using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using Localisation;
using Mono.Cecil;
using static UnityEngine.UI.CanvasScaler;
using BroMakerLib;
using System.IO;

namespace Scorpion
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;

        public static Material avatarMat;
        public static Material bloodyAvatarMat;
        public static Material gunSpriteMat;
        public static AudioClip chainsawStart;
        public static AudioClip chainsawSpin;
        public static AudioClip chainsawWindDown;
        public static AudioSource[] source = new AudioSource[4];
        public static float[] rampageTime = new float[] { 0f, 0f, 0f, 0f };
        public static float[] rampageDamageDelay = new float[] { 0f, 0f, 0f, 0f };
        public static bool[] onRampage = new bool[] { false, false, false, false };
        public static bool[] haveSwitchedMaterial = new bool[] { false, false, false, false };
        public static bool[] hitChainsawLastFrame = new bool[] { false, false, false, false };
        public static int[] chainsawHits = new int[] { 0, 0, 0, 0 };
        public static float[] rampageFrameDelay = new float[] { 0f, 0f, 0f, 0f };
        public static int[] rampageFrame = new int[] { 0, 0, 0, 0 };
        public static int[] avatarFrame = new int[] { 0, 0, 0, 0 };
        public static float[] avatarAnimDelay = new float[] { 0f, 0f, 0f, 0f };

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = Settings.Load<Settings>(modEntry);
            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            mod = modEntry;
            try
            {
                harmony.PatchAll(assembly);
            }
            catch ( Exception ex )
            {
                mod.Logger.Error(ex.ToString());
            }
            

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            avatarMat = ResourcesController.GetMaterial(directoryPath, "avatar.png");
            avatarMat.mainTexture.wrapMode = TextureWrapMode.Clamp;
            bloodyAvatarMat = ResourcesController.GetMaterial(directoryPath, "avatarBloody.png");
            bloodyAvatarMat.mainTexture.wrapMode = TextureWrapMode.Clamp;
            gunSpriteMat = ResourcesController.GetMaterial(directoryPath, "gunSprite.png");
            

            chainsawStart = ResourcesController.GetAudioClip(directoryPath, "chainsawStart.wav");
            chainsawSpin = ResourcesController.GetAudioClip(directoryPath, "chainsawSpin.wav");
            chainsawWindDown = ResourcesController.GetAudioClip(directoryPath, "chainsawWindDown.wav");

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
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

        public static void StopChainsawAudio(TestVanDammeAnim character)
        {
            if (Main.source[character.playerNum] != null && Main.source[character.playerNum].isPlaying && Main.source[character.playerNum].clip == Main.chainsawSpin)
            {
                Main.source[character.playerNum].loop = false;
                Main.source[character.playerNum].clip = Main.chainsawWindDown;
                Main.source[character.playerNum].Play();
            }
        }
    }

    public class Settings : UnityModManager.ModSettings
    {
        public int count = 0;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "Start")]
    static class ScorpionBro_Awake_Patch
    {
        public static void Postfix(TestVanDammeAnim __instance)
        {
            if (!Main.enabled || !(__instance is ScorpionBro))
            {
                return;
            }

            int playerNum = __instance.playerNum;
            if ( playerNum >= 0 && playerNum < 4 )
            {
                __instance.player.hud.SetAvatar(Main.avatarMat);
                Main.rampageTime[playerNum] = 0f;
                Main.rampageDamageDelay[playerNum] = 0f;
                Main.onRampage[playerNum] = false;
                Main.haveSwitchedMaterial[playerNum] = false;
                Main.hitChainsawLastFrame[playerNum] = false;
                Main.chainsawHits[playerNum] = 0;
                Main.rampageFrameDelay[playerNum] = 0f;
                Main.rampageFrame[playerNum] = 0;
                Main.source[playerNum] = __instance.gameObject.AddComponent<AudioSource>();
            }
        }
    }

    [HarmonyPatch(typeof(ScorpionBro), "Update")]
    static class ScorpionBro_Update_Patch
    {
        public static void Postfix(ScorpionBro __instance)
        {
            if (!Main.enabled)
            {
                return;
            }

            int playerNum = __instance.playerNum;
            if ( playerNum < 0 || playerNum > 3 )
            {
                return;
            }
            try
            {
                if (Main.onRampage[playerNum])
                {
                    if (Main.source[playerNum].clip == Main.chainsawStart && !Main.source[playerNum].isPlaying)
                    {
                        Main.source[playerNum].loop = true;
                        Main.source[playerNum].clip = Main.chainsawSpin;
                        Main.source[playerNum].Play();
                    }
                    if ((Main.rampageTime[playerNum] -= Time.unscaledDeltaTime) < 0f || __instance.isOnHelicopter || !CutsceneController.PlayersCanMove())
                    {
                        Main.onRampage[playerNum] = false;
                        __instance.speed = 110;
                        __instance.SetInvulnerable(0.5f, true, false);
                        Main.StopChainsawAudio(__instance);
                        HeroController.SetAvatarCalm(playerNum, true);
                    }
                    else if ((Main.rampageDamageDelay[playerNum] -= Time.unscaledDeltaTime) < 0f)
                    {
                        Main.rampageDamageDelay[playerNum] += 0.03334f;
                        BloodColor bloodColor = BloodColor.None;
                        if (Map.HitUnits(__instance, __instance, __instance.playerNum, 1, DamageType.Chainsaw, 16f, 16f, __instance.X + __instance.transform.localScale.x * __instance.width / 2f, __instance.Y + __instance.height / 2f, __instance.transform.localScale.x * 70f, 70f, false, true, true, true, ref bloodColor, null, false))
                        {
                            Sound.GetInstance().PlaySoundEffectAt(__instance.soundHolder.effortSounds, 0.5f, __instance.transform.position, 1f, true, false, false, 0f);
                            if (bloodColor == BloodColor.Green || bloodColor == BloodColor.Red)
                            {
                                EffectsController.CreateBloodParticles(bloodColor, __instance.X + __instance.transform.localScale.x * __instance.width * 0.25f, __instance.Y + __instance.height / 2f, 5, 4f, 4f, 60f, __instance.transform.localScale.x * __instance.speed, 350f);
                            }
                            else
                            {
                                EffectsController.CreateSparkParticles(__instance.X + __instance.transform.localScale.x * __instance.width, __instance.Y + __instance.height * 2f, 1f, 5, 2f, 70f, __instance.transform.localScale.x * __instance.speed, 250f, UnityEngine.Random.value, 1f);
                            }
                            Main.source[playerNum].pitch = Mathf.Clamp(Main.source[playerNum].pitch + 0.03f, 0.85f, 1.25f);
                            if (bloodColor == BloodColor.Red && !Main.haveSwitchedMaterial[playerNum] && Main.chainsawHits[playerNum]++ > 15)
                            {
                                Main.haveSwitchedMaterial[playerNum] = true;
                                HeroController.players[__instance.playerNum].hud.SetAvatar(Main.bloodyAvatarMat);
                            }
                            Main.hitChainsawLastFrame[playerNum] = true;
                        }
                        else
                        {
                            Main.source[playerNum].pitch = Mathf.Lerp(Main.source[playerNum].pitch, 0.85f, 0.1667f);
                            Main.hitChainsawLastFrame[playerNum] = false;
                        }
                        MapController.DamageGround(__instance, 3, DamageType.Normal, 4f, __instance.X + __instance.transform.localScale.x * __instance.width / 2f, __instance.Y + __instance.height / 2f, null, false);
                        Map.DeflectProjectiles(__instance, __instance.playerNum, 20f, __instance.X + Mathf.Sign(__instance.transform.localScale.x) * 6f, __instance.Y + 6f, Mathf.Sign(__instance.transform.localScale.x) * 200f, true);
                        Map.PanicUnits(__instance.X, __instance.Y, 64f, 16f, (int)__instance.transform.localScale.x, 0.5f, false);
                        bool flag;
                        Map.DamageDoodads(3, DamageType.Knifed, __instance.X + (float)(__instance.Direction * 4), __instance.Y, 0f, 0f, 6f, __instance.playerNum, out flag, null);
                    }
                }
            }
            catch ( Exception ex )
            {
                Main.Log("exception: " + ex.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(ScorpionBro), "UseSpecial")]
    static class ScorpionBro_UseSpecial_Patch
    {
        public static bool Prefix(ScorpionBro __instance)
        {
            if (!Main.enabled)
            {
                return true;
            }

            int playerNum = __instance.playerNum;
            if (playerNum < 0 || playerNum > 3)
            {
                return true;
            }
            if (!Main.onRampage[playerNum] && __instance.SpecialAmmo > 0)
            {
                try
                {
                    __instance.gunSprite.meshRender.material = Main.gunSpriteMat;

                    __instance.SpecialAmmo--;
                    HeroController.SetSpecialAmmo(__instance.playerNum, __instance.SpecialAmmo);
                    Main.rampageTime[playerNum] = 5.5f;
                    Main.onRampage[playerNum] = true;
                    Main.source[playerNum].rolloffMode = AudioRolloffMode.Linear;
                    Main.source[playerNum].dopplerLevel = 0.1f;
                    Main.source[playerNum].minDistance = 500f;
                    Main.source[playerNum].volume = 0.4f;
                    Main.source[playerNum].clip = Main.chainsawStart;
                    Main.source[playerNum].loop = false;
                    Main.source[playerNum].Play();
                    HeroController.SetAvatarAngry(__instance.playerNum, __instance.usePrimaryAvatar);
                }
                catch(Exception ex )
                {
                    Main.Log("failed: " + ex.ToString());
                }
            }
            
            return false;
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "ChangeFrame")]
    static class TestVanDammeAnim_ChangeFrame_Patch
    {
        public static void Postfix(TestVanDammeAnim __instance)
        {
            if (!Main.enabled || !(__instance is ScorpionBro) )
            {
                return;
            }

            int playerNum = __instance.playerNum;
            if (playerNum < 0 || playerNum > 3)
            {
                return;
            }
            if (Main.onRampage[playerNum] && __instance.health > 0)
            {
                __instance.gunSprite.gameObject.SetActive(true);
                Main.rampageFrameDelay[playerNum] -= Time.unscaledDeltaTime;
                if (Main.rampageFrameDelay[playerNum] < 0f)
                {
                    Main.rampageFrameDelay[playerNum] = 0.02f;
                    Main.rampageFrame[playerNum] = (Main.rampageFrame[playerNum] + 1) % 4;
                }
                __instance.gunSprite.SetLowerLeftPixel((float)(48 * (16 + Main.rampageFrame[playerNum] + ((!Main.hitChainsawLastFrame[playerNum]) ? 0 : 3))), 48f);
            }
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "CanInseminate")]
    static class ScorpionBro_CanInseminate_Patch
    {
        public static void Postfix(TestVanDammeAnim __instance, ref bool __result)
        {
            if (!Main.enabled || !(__instance is ScorpionBro))
            {
                return;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return;
            }
            __result = Main.onRampage[__instance.playerNum] && __result;
        }
    }


    [HarmonyPatch(typeof(TestVanDammeAnim), "StartPilotingUnit")]
    static class ScorpionBro_StartPilotingUnit_Patch
    {
        public static void Prefix(TestVanDammeAnim __instance, ref Unit pilottedUnit)
        {
            if (!Main.enabled || !(__instance is ScorpionBro))
            {
                return;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return;
            }
            int playerNum = __instance.playerNum;
            Main.StopChainsawAudio(__instance);
            Main.rampageTime[playerNum] -= 100f;
            Main.onRampage[playerNum] = false;
            __instance.dashing = false;
            __instance.speed = 110;
            Traverse.Create(__instance).Field("doingMelee").SetValue(false);
            __instance.StartPilotingUnit(pilottedUnit);
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "Death")]
    static class ScorpionBro_Death_Patch
    {
        public static void Postfix(TestVanDammeAnim __instance)
        {
            if (!Main.enabled || !(__instance is ScorpionBro))
            {
                return;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return;
            }
            int playerNum = __instance.playerNum;
            Main.rampageTime[playerNum] -= 100f;
            Main.StopChainsawAudio(__instance);
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "Damage")]
    static class ScorpionBro_Damage_Patch
    {
        public static bool Prefix(TestVanDammeAnim __instance, ref int damage, ref DamageType damageType, ref float xI, ref float yI, ref int direction, ref MonoBehaviour damageSender, ref float hitX, ref float hitY )
        {
            if (!Main.enabled || !(__instance is ScorpionBro))
            {
                return true;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return true;
            }
            int playerNum = __instance.playerNum;
            Traverse trav = Traverse.Create(__instance);
            if ((damageType != DamageType.Melee && damageType != DamageType.Knifed && damageType != DamageType.Blade) || (!((bool) trav.Field("usingSpecial").GetValue()) && !Main.onRampage[playerNum]) || Mathf.Sign(__instance.transform.localScale.x) == Mathf.Sign(xI))
            {
                __instance.Damage(damage, damageType, xI, yI, direction, damageSender, hitX, hitY);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ScorpionBro), "StartFiring")]
    static class ScorpionBro_StartFiring_Patch
    {
        public static bool Prefix(ScorpionBro __instance)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return true;
            }
            if (Main.onRampage[__instance.playerNum])
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ScorpionBro), "RunFiring")]
    static class ScorpionBro_RunFiring_Patch
    {
        public static void Prefix(ScorpionBro __instance)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return;
            }
            if (Main.onRampage[__instance.playerNum])
            {
                __instance.fire = false;
                __instance.wasFire = false;
            }
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "CalculateMovement")]
    static class ScorpionBro_CalculateMovement_Patch
    {
        public static void Prefix(TestVanDammeAnim __instance)
        {
            if (!Main.enabled || !(__instance is ScorpionBro))
            {
                return;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return;
            }
            int playerNum = __instance.playerNum;
            if (Main.onRampage[playerNum])
            {
                if (__instance.transform.localScale.x < 0f)
                {
                    if (!__instance.right && __instance.left)
                    {
                        __instance.speed = 110 * 1.6f;
                    }
                    else if (!__instance.right)
                    {
                        __instance.speed = 110 * 1.3f;
                        __instance.left = true;
                    }
                }
                else if (!__instance.left && __instance.right)
                {
                    __instance.speed = 110 * 1.6f;
                }
                else if (!__instance.left)
                {
                    __instance.speed = 110 * 1.3f;
                    __instance.right = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "Inseminate")]
    static class ScorpionBro_Inseminate_Patch
    {
        public static bool Prefix(TestVanDammeAnim __instance, ref bool __result, ref AlienFaceHugger unit, ref float xForce, ref float yForce)
        {
            if (!Main.enabled || !(__instance is ScorpionBro))
            {
                return true;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return true;
            }
            __result = (!Main.onRampage[__instance.playerNum] || Mathf.Sign(__instance.transform.localScale.x) == Mathf.Sign(__instance.xI)) && __instance.Inseminate(unit, xForce, yForce);

            return false;
        }
    }

    [HarmonyPatch(typeof(ScorpionBro), "StartMelee")]
    static class ScorpionBro_StartMelee_Patch
    {
        public static bool Prefix(ScorpionBro __instance)
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return true;
            }

            return !Main.onRampage[__instance.playerNum];
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "RunAvatarFiring")]
    static class ScorpionBro_RunAvatarFiring_Patch
    {
        public static bool Prefix(TestVanDammeAnim __instance)
        {
            if (!Main.enabled || !(__instance is ScorpionBro))
            {
                return true;
            }

            if (__instance.playerNum < 0 || __instance.playerNum > 3)
            {
                return true;
            }

            int playerNum = __instance.playerNum;
            if (Main.onRampage[playerNum])
            {
                Main.avatarAnimDelay[playerNum] -= Time.unscaledDeltaTime;
                if (Main.avatarAnimDelay[playerNum] < 0f)
                {
                    Main.avatarAnimDelay[playerNum] = 0.05f;
                    Main.avatarFrame[playerNum] = (Main.avatarFrame[playerNum] + 1) % 4;
                }
                __instance.player.hud.avatar.SetLowerLeftPixel(new Vector2(32f * (float)(4 + Main.avatarFrame[playerNum]), 64f));

                return false;
            }

            return true;
        }
    }
}