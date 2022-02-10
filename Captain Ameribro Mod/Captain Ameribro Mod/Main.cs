/**
 * TODO
 * 
 * ---Glitches---
 * 
 * Shield doesn't bounce again after first bounce (this may or may not be necessary depending on final decision for shield mechanics)
 * 
 * Make dying as captain america not break the game
 * 
 * Beating the level and going to next also breaks spawning
 * 
 * Can't do fancy edge mantle thing
 * 
 * Shield gets stuck on doors
 * 
 * 
 * 
 * Invinicibility flash seems like it could be broken, character sometimes appears dark for some reason
 * 
 * 
 * ---Unimplemented---
 * 
 * Make shield richochet back after hitting 3-4 enemies
 * 
 * Make crouching block damage, probably move you backwards, probably breaks shield after a high amount of damage
 * 
 * Make knife attack some sort of shield bash probably
 * 
**/
/**
 * IDEAS
 * 
 * Reflecting bullets is currently enabled for shield, it seems like bromax has this ability, maybe captain america should too?
 * Although what would his crouch be if throwing the shield reflects projectiles
 * Maybe when thrown it blocks bullets but crouch will reflect?
 * 
 * If shield sticking into wall is implemented, shield should stick into explosives and then shoot back when explosives explode
 * 
 * Ricochet (shield bouncing) could be limited based on momentum? Maybe based on how many enemies have been hit. 
 * Maybe it doesn't ricochet off walls, although it seems like it should
 * 
 * Instakilling normal enemies may be OP? needs testing
 * 
 * Crouching could lead to shield getting knocked out of hands? Maybe better than it breaking
 * 
 * Animate the shield in some way (either via effects controller or adding more frames)
 * 
**/
/**
 * DONE
 * 
 * Make collecting a new life work correctly (currently it doesn't remove ameribro and you just have two characters)
 * 
 * Make restarting work correctly (currently you can't restart and then swap again)
 * 
 * Make BroMax's projectile work after swapping and then restarting, currently seems to break (maybe need to clone it?) (seems to work)
 * 
 * Made a rough shield sprite
 * 
 * Made shield not slow down ever (idk if I'll keep that)
 * 
 * Made shield keep going after hitting enemies
 * 
 * Made shield do 3 damage instead of 1 (normal mooks seem to be 3 health)
**/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using System.IO;

namespace Captain_Ameribro_Mod
{
    public static class Main
    {
        public static UnityModManager.ModEntry mod;
        public static bool enabled;
        public static Settings settings;
        public static CaptainAmeribro bro;
        public static bool isCaptainAmeribro = false;

        // DEBUG
        public static int globalCounter = 0;
        public static bool requestDisplay = false;
        public static int requestedFrame = 0;
        public static string XIText;
        public static int requestedXI = 0;
        public static bool changeXI = false;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnToggle = OnToggle;
            settings = Settings.Load<Settings>(modEntry);
            var harmony = new Harmony(modEntry.Info.Id);
            var assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);
            mod = modEntry;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Trigger swap", GUILayout.Width(100)))
            {
                swapToCustom();
            }
            if (GUILayout.Button("Check attached", GUILayout.Width(200)))
            {
                checkAttached(HeroController.players[0].character.gameObject);
            }
            if (GUILayout.Button("Check attached projectile", GUILayout.Width(200)))
            {
                checkAttached(bro.shield.gameObject);
            }
            if (GUILayout.Button("Check attached gun", GUILayout.Width(200)))
            {
                checkAttached(bro.gunSprite.gameObject);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(25);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Previous frame", GUILayout.Width(200)))
            {
                requestedFrame--;

            }
            if (GUILayout.Button("Advance frame", GUILayout.Width(200)))
            {
                requestedFrame++;
                
            }
            if (GUILayout.Button("Check pos", GUILayout.Width(200)))
            {
                //bro.shield.Display();
                requestDisplay = true;
            }
            if (GUILayout.Button("Set Shield Ammo", GUILayout.Width(200)))
            {
                bro.SpecialAmmo = 1;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(25);
            GUILayout.BeginHorizontal();

            settings.animateShield = GUILayout.Toggle(settings.animateShield, "Allow shield animation to play", GUILayout.ExpandWidth(false));
            settings.moveShield = GUILayout.Toggle(settings.moveShield, "Allow shield to move", GUILayout.ExpandWidth(false));

            GUILayout.EndHorizontal();
            GUILayout.Space(25);
            GUILayout.BeginHorizontal();

            XIText = GUILayout.TextField(XIText, GUILayout.Width(100));

            if (GUILayout.Button("Set XI", GUILayout.Width(200)))
            {
                requestedXI = Int32.Parse(XIText);
                changeXI = true;
            }

            if (GUILayout.Button("Reset Counter", GUILayout.Width(100)))
            {
                globalCounter = 0;
            }

            GUILayout.EndHorizontal();
        }

        public static void checkAttached(GameObject gameObject )
        {
            Main.Log("\n\n");
            Component[] allComponents;
            allComponents = gameObject.GetComponents(typeof(Component));
            foreach (Component comp in allComponents)
            {
                Main.Log("attached: " + comp.name + " also " + comp.GetType());
            }
            bro.Display();
            Main.Log("\n\n");
        }

        public static void swapToCustom()
        {
            Dictionary<HeroType, HeroController.HeroDefinition> heroDefinition = Traverse.Create(HeroController.Instance).Field("_heroData").GetValue() as Dictionary<HeroType, HeroController.HeroDefinition>;
            Traverse oldVanDamm = Traverse.Create(HeroController.players[0].character);

            float fireRate = 0.166f;

            bro = HeroController.players[0].character.gameObject.AddComponent<CaptainAmeribro>();
            UnityEngine.Object.Destroy(HeroController.players[0].character.gameObject.GetComponent<WavyGrassEffector>());

            SpriteSM sprite = bro.gameObject.GetComponent<SpriteSM>();
            SoundHolder soundholder = oldVanDamm.Field("soundHolder").GetValue() as SoundHolder;
            TestVanDammeAnim neobro = HeroController.GetHeroPrefab(HeroType.Nebro);


            // LOADING CHARACTER SPRITE ARMLESS
            {
                string filePath = "D:\\Steam\\steamapps\\common\\Broforce\\Mods\\Development - captainameribro\\captainAmeribroArmless.png";
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                tex.LoadImage(File.ReadAllBytes(filePath));
                tex.wrapMode = TextureWrapMode.Clamp;

                Texture orig = sprite.meshRender.sharedMaterial.GetTexture("_MainTex");

                tex.anisoLevel = orig.anisoLevel;
                tex.filterMode = orig.filterMode;
                tex.mipMapBias = orig.mipMapBias;
                tex.wrapMode = orig.wrapMode;

                Material armless = Material.Instantiate(sprite.meshRender.sharedMaterial);
                armless.mainTexture = tex;
                bro.materialArmless = armless;
                //sprite.meshRender.sharedMaterial.SetTexture("_MainTex", tex);
                //bro.materialArmless = sprite.meshRender.sharedMaterial;
            }

            // LOADING CHARACTER SPRITE
            {
                string filePath = "D:\\Steam\\steamapps\\common\\Broforce\\Mods\\Development - captainameribro\\captainAmeribro.png";
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                tex.LoadImage(File.ReadAllBytes(filePath));
                tex.wrapMode = TextureWrapMode.Clamp;

                Texture orig = sprite.meshRender.sharedMaterial.GetTexture("_MainTex");

                tex.anisoLevel = orig.anisoLevel;
                tex.filterMode = orig.filterMode;
                tex.mipMapBias = orig.mipMapBias;
                tex.wrapMode = orig.wrapMode;

                sprite.meshRender.sharedMaterial.SetTexture("_MainTex", tex);
                bro.materialNormal = sprite.meshRender.sharedMaterial;
            }

            
            // LOADING GUN SPRITE WITHOUT SHIELD
            {
                //bro.gunSpriteNoShield = HeroController.players[0].character.gunSprite;

                string filePathGun = "D:\\Steam\\steamapps\\common\\Broforce\\Mods\\Development - captainameribro\\captainAmeribroGunNoShield.png";
                var texGun = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                texGun.LoadImage(File.ReadAllBytes(filePathGun));
                texGun.wrapMode = TextureWrapMode.Clamp;

                Texture origGun = neobro.gunSprite.GetComponent<Renderer>().sharedMaterial.GetTexture("_MainTex");

                SpriteSM gunSpriteCopy = SpriteSM.Instantiate(neobro.gunSprite);

                //bro.gunSpriteNoShield.Copy(gunSpriteCopy);

                texGun.anisoLevel = origGun.anisoLevel;
                texGun.filterMode = origGun.filterMode;
                texGun.mipMapBias = origGun.mipMapBias;
                texGun.wrapMode = origGun.wrapMode;

                bro.gunTextureNoShield = texGun;
            }
            // LOADING GUN SPRITE WITH SHIELD
            {
                bro.gunSprite = HeroController.players[0].character.gunSprite;

                string filePathGun = "D:\\Steam\\steamapps\\common\\Broforce\\Mods\\Development - captainameribro\\captainAmeribroGunWithShield.png";
                var texGun = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                texGun.LoadImage(File.ReadAllBytes(filePathGun));
                texGun.wrapMode = TextureWrapMode.Clamp;

                Texture origGun = neobro.gunSprite.GetComponent<Renderer>().sharedMaterial.GetTexture("_MainTex");

                SpriteSM gunSpriteCopy = SpriteSM.Instantiate(neobro.gunSprite);

                bro.gunSprite.Copy(gunSpriteCopy);

                texGun.anisoLevel = origGun.anisoLevel;
                texGun.filterMode = origGun.filterMode;
                texGun.mipMapBias = origGun.mipMapBias;
                texGun.wrapMode = origGun.wrapMode;

                bro.gunTextureWithShield = texGun;
                bro.gunSprite.GetComponent<Renderer>().material.mainTexture = texGun;
            }


            // PASSING REFERENCES TO NEW PROJECTILE
            Boomerang boom = (HeroController.players[0].character as BroMax).boomerang as Boomerang;
            //Boomerang clone = boom.Clone<Boomerang>();
            foreach (Component comp in boom.GetComponentsInParent(typeof(Component)))
            {
                Main.Log("attached: " + comp.name + " also " + comp.GetType());
            }
            Boomerang clone = Boomerang.Instantiate(boom);
            Traverse boomerangTraverse = Traverse.Create(clone);
            //Traverse boomerangTraverseOrig = Traverse.Create(boom);
            //BoxCollider attachBoxCollider = boomerangTraverse.Field("boomerangCollider").GetValue() as BoxCollider;

            //BoxCollider attachBoxCollider = BoxCollider.Instantiate(boomerangTraverse.Field("boomerangCollider").GetValue() as BoxCollider);
            //SoundHolder boomerangSoundHolder = boomerangTraverse.Field("soundHolder").GetValue() as SoundHolder;
            //float rotationSpeed = boomerangTraverse.Field("rotationSpeed").GetValue<float>();
            bro.attachBoxCollider = BoxCollider.Instantiate(boomerangTraverse.Field("boomerangCollider").GetValue() as BoxCollider);
            bro.boomerangSoundHolder = boomerangTraverse.Field("soundHolder").GetValue() as SoundHolder;
            bro.rotationSpeed = boomerangTraverse.Field("rotationSpeed").GetValue<float>();
            bro.shieldTransform = clone.transform.parent;

            UnityEngine.Object.Destroy(clone.gameObject.GetComponent<Boomerang>());


            Shield shield = clone.gameObject.AddComponent<Shield>();

            shield.Setup(bro.attachBoxCollider, bro.shieldTransform, bro.boomerangSoundHolder, bro.rotationSpeed, false);

            //return;
            // LOADING PROJECTILE SPRITE
            //MeshRenderer meshRender = shield.gameObject.GetComponent<MeshRenderer>();
            MeshRenderer meshRender = clone.gameObject.GetComponent<MeshRenderer>();
            //Main.Log("after get component");
            {
                string filePath = "D:\\Steam\\steamapps\\common\\Broforce\\Mods\\Development - captainameribro\\captainAmeribroShield.png";
                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                tex.LoadImage(File.ReadAllBytes(filePath));
                //Main.Log("after load iamge");
                tex.wrapMode = TextureWrapMode.Clamp;

                Texture orig = meshRender.sharedMaterial.mainTexture;

                tex.anisoLevel = orig.anisoLevel;
                tex.filterMode = orig.filterMode;
                tex.mipMapBias = orig.mipMapBias;
                tex.wrapMode = orig.wrapMode;
                //Main.Log("after orig texture");

                meshRender.material.mainTexture = tex;
                //meshRender.sharedMaterial.mainTexture = tex;
                //Main.Log("at end");
            }


            // PASSING REFERENCES TO NEW VAN DAMM
            bro.Setup(sprite, HeroController.players[0], HeroController.players[0].character.playerNum, shield, soundholder, fireRate);

            UnityEngine.Object.Destroy(HeroController.players[0].character.gameObject.GetComponent<BroMax>());

            //HeroController.players[0].character = bro;

            //Networking.Networking.RPC<int, HeroType, bool>(PID.TargetAll, new RpcSignature<int, HeroType, bool>(bro.SetUpHero), 0, HeroType.BroMax, true, false);
            // Give hero controller reference to new van damm
            bro.SetUpHero(0, HeroType.BroMax, true);

            isCaptainAmeribro = true;
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

    }

    public class Settings : UnityModManager.ModSettings
    {
        // DEBUG
        public bool animateShield;
        public bool moveShield;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }

    /*[HarmonyPatch(typeof(TestVanDammeAnim), "PlayAttackSound", new Type[] { })]
    static class TestVanDammeAnim_PlayAttackSound2_Patch
    {
        public static void Prefix(TestVanDammeAnim __instance)
        {
            Main.Log("playing attack sound2");
        }

    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "PlayAttackSound", new Type[] { typeof(float) })]
    static class TestVanDammeAnim_PlayAttackSound_Patch
    {
        public static void Prefix(TestVanDammeAnim __instance, ref float v)
        {
            Main.Log("playing attack sound: " + v);
        }

    }*/

            [HarmonyPatch(typeof(Player), "GetInput")]
    static class Player_GetInput_Patch
    {
        public static void Postfix(Player __instance, ref bool buttonGesture)
        {
            if (!Main.enabled)
            {
                return;
            }
            if (buttonGesture && Main.globalCounter == 0)
            {
                Main.swapToCustom();
                Main.globalCounter++;
            }
        }
    }

    /*[HarmonyPatch(typeof(TestVanDammeAnim), "AnimateChimneyFlip")]
    static class TestVanDammeAnim_AnimateChimneyFlip_Patch
    {
        static void Prefix(TestVanDammeAnim __instance)
        {
            //Main.Log("animated chimney flip in testvandamme");

        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "ConstrainToCeiling")]
    static class TestVanDammeAnim_ConstrainToCeiling_Patch
    {
        static void Prefix(TestVanDammeAnim __instance)
        {
            //Main.Log("called constrain to ceiling in testvandamme");
            
        }
    }*/

    /*[HarmonyPatch(typeof(TestVanDammeAnim), "StartDashing")]
    static class TestVanDammeAnim_StartDashing_Patch
    {
        static void Prefix(TestVanDammeAnim __instance)
        {

            Main.Log("IN TEST VAN DAMME START DASHING");

        }

        static void Postfix(TestVanDammeAnim __instance)
        {

            Main.Log("AFTER TESTVANDAMME START DASHING");

        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "PlayDashSound")]
    static class TestVanDammeAnim_PlayDashSound_Patch
    {
        static void Prefix(TestVanDammeAnim __instance)
        {
            Main.Log("IN PLAY DASH SOUND");
        }

        static void Postfix(TestVanDammeAnim __instance)
        {

            Main.Log("AFTER PLAY DASH SOUND");

        }
    }*/
    /*[HarmonyPatch(typeof(TestVanDammeAnim), "RunFiring")]
    static class TestVanDammeAnim_RunFiring_Patch
    {
        static void Prefix(TestVanDammeAnim __instance)
        {
            Traverse trav = Traverse.Create(__instance);
            float fireCounter = trav.Field("fireCounter").GetValue<float>();
            float t = trav.Field("t").GetValue<float>();
            //Main.Log("fireDelay: " + __instance.fireDelay + "     firecounter: " + fireCounter + "    firerate: " + __instance.fireRate + "   this.t: " + t);
            if (__instance.fire && __instance.fireDelay <= 0f)
            {
                fireCounter += t;
                if (fireCounter >= __instance.fireRate)
                {

                    Main.Log("running fire");
                }
            }
        }
    }*/

    /*[HarmonyPatch(typeof(Blade), "UseFire")]
    static class Blade_UseFire_Patch
    {
        static void Prefix(Blade __instance)
        {
            Main.Log("use fire neo");
            return;
            Traverse trav = Traverse.Create(__instance);
            float fireCounter = trav.Field("fireCounter").GetValue<float>();
            float t = trav.Field("t").GetValue<float>();
            Main.Log("fireDelay: " + __instance.fireDelay + "     firecounter: " + fireCounter + "    firerate: " + __instance.fireRate + "   this.t: " + t);
            Main.Log("use fire neo");
        }
    }

    [HarmonyPatch(typeof(BroMax), "UseSpecial")]
    static class BroMax_UseSpecial_Patch
    {
        static void Prefix(BroMax __instance)
        {
            Component[] allComponents3;
            allComponents3 = __instance.boomerang.GetComponents(typeof(Component));
            foreach (Component comp in allComponents3)
            {
                Main.Log("attached: " + comp.name + " also " + comp.GetType());
            }
        }
        static void Postfix(BroMax __instance)
        {
            Component[] allComponents3;
            allComponents3 = __instance.boomerang.GetComponents(typeof(Component));
            foreach (Component comp in allComponents3)
            {
                Main.Log("attached: " + comp.name + " also " + comp.GetType());
            }

        }
    }*/

    /*[HarmonyPatch(typeof(ProjectileController), "SpawnProjectileOverNetwork")]
    static class ProjectileController_SpawnProjectileOverNetwork_Patch
    {
        static void Prefix(BroMax __instance, ref Projectile prefab, ref MonoBehaviour FiredBy, ref float x, ref float y, ref float xI, ref float yI, ref bool synced, ref int playerNum, ref bool AddTemporaryPlayerTarget, ref bool executeImmediately, ref float _zOffset)
        {
            Main.Log("firedby: " + FiredBy);
            Main.Log("x: " + x + "   y: " + y + "    xi: " + xI + "   yi: " + yI + "   synced: " + synced + "   playernum: " + playerNum + "   addtemptarget: " + AddTemporaryPlayerTarget + "  executeImmediately: " + executeImmediately + "   zoffset: " + _zOffset);
        }
    }*/

    /*[HarmonyPatch(typeof(HeroController), "MayIRescueThisBro")]
    static class HeroController_MayIRescueThisBro_Patch
    {
        static bool Prefix(HeroController __instance, ref int playerNum, ref RescueBro rescueBro, ref Ack ackRequest)
        {
            Main.Log("MayIRescueThisBro called");
            Main.Log("playerNum: " + playerNum + "    rescuebro: " + rescueBro.ToString() + "    ackrequest: " + ackRequest.ToString());
            Main.Log("\n\n");
            int num = -1;
            List<int> list = new List<int>();
            bool[] playersPlaying = Traverse.Create(typeof(HeroController)).Field("playersPlaying").GetValue() as bool[];
            for (int i = 0; i < 4; i++)
            {
                if (HeroController.players[i] != null && playersPlaying[i] && !HeroController.players[i].IsAlive() && !HeroController.players[i].firstDeployment)
                {
                    list.Add(i);
                }
            }
            if (__instance.playerDeathOrder.Count == 0 && list.Count != 0)
            {
                num = list[0];
            }
            Main.Log("num: " + num + "    size: " + list.Count());
            while (__instance.playerDeathOrder.Count != 0 && list.Count != 0)
            {
                int num2 = __instance.playerDeathOrder[0];
                Main.Log("removing: " + __instance.playerDeathOrder[0]);
                __instance.playerDeathOrder.RemoveAt(0);
                if (HeroController.players[num2] != null && playersPlaying[num2] && !HeroController.players[num2].IsAlive())
                {
                    num = num2;
                    break;
                }
            }
            Main.Log("num: " + num);
            return false;
        }

        static void Postfix(HeroController __instance)
        {
            Main.Log("AFTER MayIRescueThisBro called");
        }
    }*/

    /*[HarmonyPatch(typeof(HeroController), "ChangeBro")]
    static class HeroController_ChangeBro_Patch
    {
        static void Prefix(HeroController __instance)
        {
            Main.Log("ChangeBro called");
        }

        static void Postfix(HeroController __instance)
        {
            Main.Log("after ChangeBro called");
        }
    }

    [HarmonyPatch(typeof(HeroController), "SwapBro")]
    static class HeroController_SwapBro_Patch
    {
        static void Prefix(HeroController __instance)
        {
            Main.Log("swap bros called");
        }

        static void Postfix(HeroController __instance)
        {
            Main.Log("after swap bros called");
        }
    }

    [HarmonyPatch(typeof(TestVanDammeAnim), "OnDestroy")]
    static class TestVanDammeAnim_OnDestroy_Patch
    {
        static void Prefix(HeroController __instance)
        {
            Main.Log("OnDestroy called");
        }

        static void Postfix(HeroController __instance)
        {
            Main.Log("after OnDestroy called");
        }
    }*/

    [HarmonyPatch(typeof(Player), "SpawnHero")]
    static class Player_SpawnHero_Patch
    {
        static void Prefix(Player __instance, ref HeroType nextHeroType)
        {
            if (!Main.enabled)
                return;
            // This code works for getting rid of bro when collecting new character but it breaks when you try and restart the level because 
            // it trys to remove bro when it shouldn't (aka when restarting or continuing on to next level) should only run when collecting new character
            //  need to add some check for this, maybe some way to try remove or check for restart / level finish
            /*if (Main.isCaptainAmeribro)
            {
                UnityEngine.Object.Destroy(Main.bro.gameObject);
                Main.isCaptainAmeribro = false;
            }*/
            nextHeroType = HeroType.BroMax;
            

            //Main.Log("returning");
            return;

        }

    }


    /*    [HarmonyPatch(typeof(BundleReferences.TestVanDammeAnimReference), "Load")]
        static class AssetBundleReference_Load_Patch
        {
            static void Prefix(BundleReferences.TestVanDammeAnimReference __instance)
            {
                Main.Log("in asset bundle load");
                String assetPath = Traverse.Create(__instance).Field("assetPath").GetValue() as String;
                Main.Log("loading " + assetPath);
            }

        }
    */
    /*[HarmonyPatch(typeof(Player), "InstantiateHero")]
    static class Player_InstantiateHero_Patch
    {
        static bool Prefix(Player __instance, ref TestVanDammeAnim __result, ref HeroType heroTypeEnum, ref int PlayerNum, ref int ControllerNum)
        {
            if (!base.IsMine)
            {
                __result = null;
                return false;
            }
            //TestVanDammeAnim heroPrefab = HeroController.GetHeroPrefab(heroTypeEnum);
            TestVanDammeAnim heroPrefab = new CaptainAmeribro();
            TestVanDammeAnim testVanDammeAnim = Networking.Networking.InstantiateBuffered<TestVanDammeAnim>(heroPrefab, Vector3.zero, Quaternion.identity, new object[0], false);
            Networking.Networking.RPC<int, HeroType, bool>(PID.TargetAll, new RpcSignature<int, HeroType, bool>(testVanDammeAnim.SetUpHero), PlayerNum, heroTypeEnum, true, false);
            __instance.WorkOutSpawnPosition(testVanDammeAnim);
            if (!GameModeController.ShowStandardHUDS())
            {
                Networking.RPC<TestVanDammeAnim>(PID.TargetAll, new RpcSignature<TestVanDammeAnim>(__instance.SetUpDeathMatchHUD), testVanDammeAnim, false);
            }
            __result = testVanDammeAnim;
            return false;
        }
        static void Postfix(Player __instance, ref TestVanDammeAnim __result)
        {
            Main.Log("patched");
            __result = new CaptainAmeribro();
        }
    }*/


}
