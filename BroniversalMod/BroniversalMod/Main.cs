using System;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using System.IO;
using System.Runtime;
using Mono.Cecil;


namespace BroniversalMod
{
    static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;

        public static Material normalMat, metalMat, metalAvatarMat;
        public static float[] brominatorTime = new float[] { 0f, 0f, 0f, 0f };
        public static bool[] brominatorMode = new bool[] { false, false, false, false };

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            mod = modEntry;
            try
            {
                harmony.PatchAll(assembly);

                if (metalMat == null)
                {
                    string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                    metalMat = CreateMaterial(Path.Combine(directoryPath, "metalSprite.png"), Shader.Find("Unlit/Depth Cutout With ColouredImage"));
                }
            }
            catch ( Exception ex )
            {
                Main.Log("Exception: " + ex.ToString());
            }

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
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

        public static Material CreateMaterial(string filePath, Shader shader)
        {
            var tex = CreateTexture(filePath);
            if (tex != null)
            {
                var mat = new Material(shader);
                mat.mainTexture = tex;
                return mat;
            }
            return null;
        }

        public static Texture2D CreateTexture(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            return CreateTexture(File.ReadAllBytes(filePath));
        }

        public static Texture2D CreateTexture(byte[] imageBytes)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(imageBytes);
            tex.filterMode = FilterMode.Point;
            tex.anisoLevel = 1;
            tex.mipMapBias = 0;
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }
    }

    [HarmonyPatch(typeof(BroBase), "Start")]
    static class BroBase_Start_Patch
    {
        public static void Postfix(BroBase __instance)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (__instance is BroniversalSoldier)
            {
                Main.brominatorMode[__instance.playerNum] = false;
                Main.brominatorTime[__instance.playerNum] = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(BroniversalSoldier), "UseSpecial")]
    static class BroniversalSoldier_UseSpecial_Patch
    {
        public static void Prefix(BroniversalSoldier __instance)
        {
            if (!Main.enabled)
            {
                return;
            }

            if ( __instance.SpecialAmmo > 0 )
            {
                int playerNum = __instance.playerNum;
                // Already in metal mode, cancel metal mode
                if ( Main.brominatorMode[playerNum] )
                {
                    Main.brominatorMode[playerNum] = false;
                    Main.brominatorTime[playerNum] = 0f;
                    if ( !__instance.invulnerable )
                        Main.normalMat.SetColor("_TintColor", Color.gray);
                    __instance.GetComponent<Renderer>().material = Main.normalMat;
                    HeroController.SetAvatarMaterial(playerNum, HeroController.GetAvatarMaterial(HeroType.BroniversalSoldier));
                }
                else
                {
                    Main.brominatorMode[playerNum] = true;
                    Main.brominatorTime[playerNum] = 5.5f;
                    Main.normalMat = __instance.GetComponent<Renderer>().material;
                    if ( !__instance.invulnerable )
                        Main.metalMat.SetColor("_TintColor", Color.gray);
                    __instance.GetComponent<Renderer>().material = Main.metalMat;
                    if ( Main.metalAvatarMat == null )
                    {
                        Main.metalAvatarMat = (HeroController.GetHeroPrefab(HeroType.Brominator) as Brominator).brominatorRobotAvatar;
                    }
                    HeroController.SetAvatarMaterial(playerNum, Main.metalAvatarMat);
                }
            }
        }
    }

    [HarmonyPatch(typeof(BroniversalSoldier), "Update")]
    static class BroniversalSoldier_Update_Patch
    {
        public static void Prefix(BroniversalSoldier __instance)
        {
            if (!Main.enabled)
            {
                return;
            }

            int playerNum = __instance.playerNum;
            if (Main.brominatorMode[playerNum])
            {
                Main.brominatorTime[playerNum] -= __instance.T;
                // Ran out of time, cancel metal mode
                if ( Main.brominatorTime[playerNum] <= 0 )
                {
                    Main.brominatorMode[playerNum] = false;
                    if (!__instance.invulnerable)
                        Main.normalMat.SetColor("_TintColor", Color.gray);
                    __instance.GetComponent<Renderer>().material = Main.normalMat;
                    HeroController.SetAvatarMaterial(playerNum, HeroController.GetAvatarMaterial(HeroType.BroniversalSoldier));
                }
            }
        }
    }

    [HarmonyPatch(typeof(BroniversalSoldier), "Damage")]
    static class BroniversalSoldier_Damage_Patch
    {
        public static bool Prefix(BroniversalSoldier __instance, ref int damage, ref DamageType damageType, ref float xI, ref float yI, ref MonoBehaviour damageSender )
        {
            if (!Main.enabled)
            {
                return true;
            }

            if (!Main.brominatorMode[__instance.playerNum])
            {
                return true;
            }
            else
            {
                Helicopter helicopter = damageSender as Helicopter;
                if (helicopter)
                {
                    helicopter.Damage(new DamageObject(helicopter.health, DamageType.Explosion, 0f, 0f, __instance.X, __instance.Y, __instance));
                }
                SawBlade sawBlade = damageSender as SawBlade;
                if (sawBlade != null)
                {
                    sawBlade.Damage(new DamageObject(sawBlade.health, DamageType.Explosion, 0f, 0f, __instance.X, __instance.Y, __instance));
                }
                MookDog mookDog = damageSender as MookDog;
                if (mookDog != null)
                {
                    mookDog.Panic((int)Mathf.Sign(xI) * -1, 2f, true);
                }
                __instance.xIBlast += xI * 0.1f + (float)damage * 0.03f;
                __instance.yI += yI * 0.1f + (float)damage * 0.03f;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "CanBeImpaledByGroundSpikes")]
    static class TestVanDammeAnim_CanBeImpaledByGroundSpikes_Patch
    {
        public static void Postfix(TestVanDammeAnim __instance, ref bool __result)
        {
            if (!Main.enabled)
            {
                return;
            }

            if (__instance is BroniversalSoldier)
            {
                __result = __result && !Main.brominatorMode[__instance.playerNum];
            }
        }
    }
}