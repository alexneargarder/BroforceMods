using System;
using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using System.IO;
using System.Reflection;
using UnityEngine;
using RocketLib;
using HarmonyLib;
using Newtonsoft.Json;

namespace Furibrosa
{
    [HeroPreset("Furibrosa", HeroType.Rambro)]
    public class Furibrosa : CustomHero
    {
        // General
        public static KeyBindingForPlayers switchWeaponKey;
        protected bool acceptedDeath = false;
        bool wasInvulnerable = false;
        public static bool jsonLoaded = false;

        // Sounds
        protected AudioClip[] crossbowSounds;
        protected AudioClip[] flareSounds;
        protected AudioClip chargeSound;
        protected AudioClip swapSound;
        protected AudioClip[] meleeSwingSounds;

        // Primary
        public enum PrimaryState
        {
            Crossbow = 0,
            FlareGun = 1,
            Switching = 2
        }
        public PrimaryState currentState = PrimaryState.Crossbow;
        public PrimaryState nextState;
        public static Bolt boltPrefab, explosiveBoltPrefab;
        protected bool releasedFire = false;
        protected float chargeTime = 0f;
        protected int chargeCounter = 0;
        protected bool charged = false;
        protected Material crossbowMat, crossbowNormalMat, crossbowHoldingMat;
        protected Material flareGunMat, flareGunNormalMat, flareGunHoldingMat;
        protected float gunFramerate = 0f;
        public static Flare flarePrefab;
        public static bool doubleTapSwitch = true;
        protected float lastDownPressTime = -1f;
        protected bool randomizedWeapon = false;
        public const float crossbowDelay = 0.6f;
        public const float flaregunDelay = 0.8f;
        public const int crossbowDamage = 13;
        public const int flaregunDamage = 11;

        // Melee
        protected MeshRenderer holdingArm;
        public static List<Unit> grabbedUnits = new List<Unit> { null, null, null, null };
        Unit grabbedUnit
        {
            get
            {
                if ( this.previousPlayerNum < 0 || this.previousPlayerNum > 3 )
                {
                    return null;
                }
                else
                {
                    return grabbedUnits[this.previousPlayerNum];
                }
            }
            set
            {
                grabbedUnits[this.previousPlayerNum] = value;
            }
        }
        protected int previousPlayerNum = -1;
        protected bool unitWasGrabbed = false;
        protected bool throwingMook = false;
        protected float holdingXOffset = 0f;
        protected float holdingYOffset = 0f;

        // Special
        static protected WarRig warRigPrefab;
        protected WarRig currentWarRig;
        public bool holdingSpecial = false;
        public float holdingSpecialTime = 0f;

        // Debug
        public Flare originalFlare = null;

        #region General
        protected override void Awake()
        {
            if (switchWeaponKey == null )
            {
                LoadKeyBinding();
            }
            LoadJson();

            base.Awake();
        }

        public static void LoadJson()
        {
            if (!jsonLoaded)
            {
                string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string jsonPath = Path.Combine(directoryPath, "FuribrosaSettings.json");

                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    doubleTapSwitch = JsonConvert.DeserializeObject<bool>(json);
                }

                jsonLoaded = true;
            }
        }

        public static void WriteJson()
        {
            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string jsonPath = Path.Combine(directoryPath, "FuribrosaSettings.json");

            // Write previouslyDiedInIronBro to json
            string json = JsonConvert.SerializeObject(doubleTapSwitch, Formatting.Indented);
            File.WriteAllText(jsonPath, json);
        }

        protected override void Start()
        {
            base.Start();

            this.previousPlayerNum = this.playerNum;

            this.soundHolderVoice = (HeroController.GetHeroPrefab(HeroType.Xebro) as Xebro).soundHolderVoice;

            this.meleeType = MeleeType.Disembowel;

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            
            this.crossbowMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "gunSpriteCrossbow.png"), ResourcesController.Particle_AlphaBlend);
            this.crossbowNormalMat = this.crossbowMat;
            this.crossbowHoldingMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "gunSpriteCrossbowHolding.png"), ResourcesController.Particle_AlphaBlend);

            this.flareGunMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "gunSpriteFlareGun.png"), ResourcesController.Particle_AlphaBlend);
            this.flareGunNormalMat = this.flareGunMat;
            this.flareGunHoldingMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "gunSpriteFlareGunHolding.png"), ResourcesController.Particle_AlphaBlend);

            this.gunSprite.gameObject.layer = 28;
            this.gunSprite.meshRender.material = this.crossbowMat;

            if (boltPrefab == null)
            {
                boltPrefab = new GameObject("Bolt", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(BoxCollider), typeof(Bolt) }).GetComponent<Bolt>();
                boltPrefab.gameObject.SetActive(false);
                boltPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro).projectile.soundHolder;
                boltPrefab.Setup(false);
                UnityEngine.Object.DontDestroyOnLoad(boltPrefab);
            }
            
            if ( explosiveBoltPrefab == null )
            {
                explosiveBoltPrefab = new GameObject("ExplosiveBolt", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(BoxCollider), typeof(Bolt) }).GetComponent<Bolt>();
                explosiveBoltPrefab.gameObject.SetActive(false);
                explosiveBoltPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro).projectile.soundHolder;
                explosiveBoltPrefab.Setup(true);
                UnityEngine.Object.DontDestroyOnLoad(explosiveBoltPrefab);
            }
            
            if ( flarePrefab == null )
            {
                for (int i = 0; i < InstantiationController.PrefabList.Count; ++i)
                {
                    if (InstantiationController.PrefabList[i] != null &&  InstantiationController.PrefabList[i].name == "Bullet Flare")
                    {
                        flarePrefab = (UnityEngine.Object.Instantiate(InstantiationController.PrefabList[i], Vector3.zero, Quaternion.identity) as GameObject).GetComponent<Flare>();
                        flarePrefab.gameObject.SetActive(false);
                        flarePrefab.damage = flarePrefab.damageInternal = flaregunDamage;
                        Traverse.Create(flarePrefab).SetFieldValue("fullDamage", flarePrefab.damage);
                        flarePrefab.range = 9f;
                        UnityEngine.Object.DontDestroyOnLoad(flarePrefab);
                        originalFlare = (InstantiationController.PrefabList[i] as GameObject).GetComponent<Flare>();
                        break;
                    }
                }
            }

            holdingArm = new GameObject("FuribrosaArm", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) }).GetComponent<MeshRenderer>();
            holdingArm.transform.parent = this.transform;
            holdingArm.gameObject.SetActive(false);
            holdingArm.material = ResourcesController.GetMaterial(directoryPath, "gunSpriteHolding.png");
            SpriteSM holdingArmSprite = holdingArm.gameObject.GetComponent<SpriteSM>();
            holdingArmSprite.RecalcTexture();
            holdingArmSprite.SetTextureDefaults();
            holdingArmSprite.lowerLeftPixel = new Vector2(0, 32);
            holdingArmSprite.pixelDimensions = new Vector2(32, 32);
            holdingArmSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            holdingArmSprite.width = 32;
            holdingArmSprite.height = 32;
            holdingArmSprite.transform.localPosition = new Vector3(0, 0, -0.9f);
            holdingArmSprite.CalcUVs();
            holdingArmSprite.UpdateUVs();
            holdingArmSprite.offset = new Vector3(0f, 15f, 0f);

            // Create WarRig
            try
            {
                if (warRigPrefab == null)
                {
                    GameObject warRig = null;
                    for (int i = 0; i < InstantiationController.PrefabList.Count; ++i)
                    {
                        if ( InstantiationController.PrefabList[i] != null && InstantiationController.PrefabList[i].name == "ZMookArmouredGuy")
                        {
                            warRig = UnityEngine.Object.Instantiate(InstantiationController.PrefabList[i], Vector3.zero, Quaternion.identity) as GameObject;
                        }
                    }

                    if (warRig != null)
                    {
                        warRigPrefab = warRig.AddComponent<WarRig>();
                        warRigPrefab.Setup();
                    }
                    else
                    {
                        throw new Exception("Mech Prefab not found");
                    }
                    UnityEngine.Object.DontDestroyOnLoad(warRigPrefab);
                }
            } 
            catch( Exception ex )
            {
                BMLogger.Log("Exception creating WarRig: " + ex.ToString());
            }

            // Randomize starting weapon
            if ( !randomizedWeapon )
            {
                if (UnityEngine.Random.value >= 0.5f)
                {
                    this.nextState = PrimaryState.FlareGun;
                    this.SwitchWeapon();
                }
                randomizedWeapon = true;
            }

            // Load Audio
            directoryPath = Path.Combine(directoryPath, "sounds");
            this.crossbowSounds = new AudioClip[4];
            this.crossbowSounds[0] = ResourcesController.CreateAudioClip(directoryPath, "crossbowShot1.wav");
            this.crossbowSounds[1] = ResourcesController.CreateAudioClip(directoryPath, "crossbowShot2.wav");
            this.crossbowSounds[2] = ResourcesController.CreateAudioClip(directoryPath, "crossbowShot3.wav");
            this.crossbowSounds[3] = ResourcesController.CreateAudioClip(directoryPath, "crossbowShot4.wav");

            this.flareSounds = new AudioClip[4];
            this.flareSounds[0] = ResourcesController.CreateAudioClip(directoryPath, "flareShot1.wav");
            this.flareSounds[1] = ResourcesController.CreateAudioClip(directoryPath, "flareShot2.wav");
            this.flareSounds[2] = ResourcesController.CreateAudioClip(directoryPath, "flareShot3.wav");
            this.flareSounds[3] = ResourcesController.CreateAudioClip(directoryPath, "flareShot4.wav");

            this.chargeSound = ResourcesController.CreateAudioClip(directoryPath, "charged.wav");

            this.swapSound = ResourcesController.CreateAudioClip(directoryPath, "weaponSwap.wav");

            this.meleeSwingSounds = new AudioClip[3];
            this.meleeSwingSounds[0] = ResourcesController.CreateAudioClip(directoryPath, "meleeSwing1.wav");
            this.meleeSwingSounds[1] = ResourcesController.CreateAudioClip(directoryPath, "meleeSwing2.wav");
            this.meleeSwingSounds[2] = ResourcesController.CreateAudioClip(directoryPath, "meleeSwing3.wav");
        }

        protected override void Update()
        {
            base.Update();
            if (this.acceptedDeath)
            {
                if (this.health <= 0 && !this.WillReviveAlready)
                {
                    return;
                }
                // Revived
                else
                {
                    this.acceptedDeath = false;
                }
            }

            if (this.invulnerable)
            {
                this.wasInvulnerable = true;
            }

            // Switch Weapon Pressed
            if (switchWeaponKey.IsDown(playerNum))
            {
                StartSwitchingWeapon();
            }

            // Check if invulnerability ran out
            if (this.wasInvulnerable && !this.invulnerable)
            {
                // Fix any not currently displayed textures\
                base.GetComponent<Renderer>().material.SetColor("_TintColor", Color.gray);
                this.crossbowNormalMat.SetColor("_TintColor", Color.gray);
                this.crossbowHoldingMat.SetColor("_TintColor", Color.gray);
                this.flareGunNormalMat.SetColor("_TintColor", Color.gray);
                this.flareGunHoldingMat.SetColor("_TintColor", Color.gray);
                this.holdingArm.material.SetColor("_TintColor", Color.gray);
            }

            if (this.holdingSpecial)
            {
                if (this.special)
                {
                    this.holdingSpecialTime += this.t;
                    if (this.holdingSpecialTime > 0.2f)
                    {
                        GoPastFuriosa();
                    }
                }
                else
                {
                    this.holdingSpecial = false;
                }
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (this.grabbedUnit != null)
            {
                if ( this.grabbedUnit.health > 0 && this.health > 0 )
                {
                    this.grabbedUnit.X = base.X + base.transform.localScale.x * this.holdingXOffset;
                    this.grabbedUnit.Y = base.Y + this.holdingYOffset;
                    if ( this.currentState == PrimaryState.Crossbow )
                    {
                        this.grabbedUnit.zOffset = -0.95f;
                    }
                    else if ( this.currentState == PrimaryState.FlareGun )
                    {
                        this.grabbedUnit.zOffset = -2;
                    }
                    this.grabbedUnit.transform.localScale = base.transform.localScale;
                    this.unitWasGrabbed = true;
                }
                else
                {
                    this.ReleaseUnit(false);
                }
            }
            // Unit was grabbed but no longer exists, switch to normal materials
            else if ( this.unitWasGrabbed )
            {
                this.unitWasGrabbed = false;
                this.grabbedUnit = null;
                this.SwitchToNormalMaterials();
                this.ChangeFrame();
            }
        }

        public static void LoadKeyBinding()
        {
            if ( !AllModKeyBindings.TryGetKeyBinding("Furibrosa", "Switch Weapon", out switchWeaponKey) )
            {
                switchWeaponKey = new KeyBindingForPlayers("Switch Weapon", "Furibrosa");
            }
        }

        public void makeTextBox(string label, ref string text, ref float val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            text = GUILayout.TextField(text);
            GUILayout.EndHorizontal();

            float.TryParse(text, out val);
        }

        public void makeTextBox(string label, ref string text, ref int val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            text = GUILayout.TextField(text);
            GUILayout.EndHorizontal();

            int.TryParse(text, out val);
        }

        public override void UIOptions()
        {
            if (switchWeaponKey == null)
            {
                LoadKeyBinding();
            }
            int player;

            GUILayout.Space(10);
            switchWeaponKey.OnGUI(out player, true);
            GUILayout.Space(10);

            if ( doubleTapSwitch != (doubleTapSwitch = GUILayout.Toggle(doubleTapSwitch, "Double Tap Down to Switch Weapons")) )
            {
                // Settings changed, update json
                WriteJson();
            }
        }

        public override void HarmonyPatches(Harmony harmony)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);
        }

        public override void Death(float xI, float yI, DamageObject damage)
        {
            this.ReleaseUnit(false);
            this.DestroyCurrentWarRig();
            base.Death(xI, yI, damage);
        }

        protected override void OnDestroy()
        {
            this.ReleaseUnit(false);
            this.DestroyCurrentWarRig();
            base.OnDestroy();
        }

        protected override void CheckRescues()
        {
            if (HeroController.CheckRescueBros(base.playerNum, base.X, base.Y, 12f))
            {
                this.ReleaseUnit(false);
                this.DestroyCurrentWarRig();

                this.ShowStartBubble();
                this.SetInvulnerable(2f, false, false);
                StatisticsController.AddBrotalityGrace(3f);
            }
        }

        protected override void ChangeFrame()
        {
            if (!this.randomizedWeapon && this.isOnHelicopter)
            {
                this.randomizedWeapon = true;
            }
            base.ChangeFrame();
        }
        #endregion

        #region Primary
        protected override void SetGunPosition(float xOffset, float yOffset)
        {
            base.SetGunPosition(xOffset, yOffset);
            if ( this.grabbedUnit != null )
            {
                this.holdingArm.transform.localPosition = this.gunSprite.transform.localPosition + new Vector3(0f, 0f, 0.1f);
                this.holdingArm.transform.localScale = this.gunSprite.transform.localScale;
            }
        }

        protected override void StartFiring()
        {
            this.chargeTime = 0f;
            this.chargeCounter = 0;
            this.charged = false;
            base.StartFiring();
        }

        protected override void ReleaseFire()
        {
            if ( this.fireDelay < 0.1f )
            {
                this.releasedFire = true;
            }
            base.ReleaseFire();
        }

        protected override void RunFiring()
        {
            if (this.health <= 0)
            {
                return;
            }

            if ( this.currentState == PrimaryState.Crossbow )
            {
                if ( this.fireDelay > 0f )
                {
                    this.fireDelay -= this.t;
                }
                if ( this.fireDelay <= 0f )
                {
                    if (this.fire)
                    {
                        this.StopRolling();
                        this.chargeTime += this.t;
                    }
                    else if (this.releasedFire)
                    {
                        this.UseFire();
                        this.FireFlashAvatar();
                        this.SetGestureAnimation(GestureElement.Gestures.None);
                    }
                }
            }
            else if ( this.currentState == PrimaryState.FlareGun )
            {
                if (this.fireDelay > 0f)
                {
                    this.fireDelay -= this.t;
                }
                if (this.fireDelay <= 0f)
                {
                    if (this.fire || this.releasedFire)
                    {
                        this.UseFire();
                        this.FireFlashAvatar();
                        this.SetGestureAnimation(GestureElement.Gestures.None);
                        this.releasedFire = false;
                    }
                }
            }
        }

        protected override void UseFire()
        {
            if (this.doingMelee)
            {
                this.CancelMelee();
            }
            this.releasedFire = false;
            float num = base.transform.localScale.x;
            if (!base.IsMine && base.Syncronize)
            {
                num = (float)this.syncedDirection;
            }
            if (Connect.IsOffline)
            {
                this.syncedDirection = (int)base.transform.localScale.x;
            }
            this.FireWeapon(0f, 0f, 0f, 0);
            Map.DisturbWildLife(base.X, base.Y, 60f, base.playerNum);
        }

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            // Fire crossbow
            if ( this.currentState == PrimaryState.Crossbow )
            {
                // Fire explosive bolt
                if ( this.charged )
                {
                    x = base.X + base.transform.localScale.x * 10f;
                    y = base.Y + 8f;
                    xSpeed = base.transform.localScale.x * 500 + (this.xI / 2);
                    ySpeed = 0;
                    this.gunFrame = 3;
                    this.SetGunSprite(this.gunFrame, 0);
                    this.TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally(explosiveBoltPrefab, this, x, y, xSpeed, ySpeed, base.playerNum) as Bolt;
                    firedBolt.gameObject.SetActive(true);

                }
                // Fire normal bolt
                else
                {
                    x = base.X + base.transform.localScale.x * 10f;
                    y = base.Y + 8f;
                    xSpeed = base.transform.localScale.x * 400 + (this.xI / 2);
                    ySpeed = 0;
                    this.gunFrame = 3;
                    this.SetGunSprite(this.gunFrame, 0);
                    this.TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally(boltPrefab, this, x, y, xSpeed, ySpeed, base.playerNum) as Bolt;
                    firedBolt.gameObject.SetActive(true);
                }
                this.PlayCrossbowSound(base.transform.position);
                this.fireDelay = crossbowDelay;
            }
            else if ( this.currentState == PrimaryState.FlareGun )
            {
                x = base.X + base.transform.localScale.x * 12f;
                y = base.Y + 8f;
                xSpeed = base.transform.localScale.x * 450;
                ySpeed = UnityEngine.Random.Range(15, 50);
                EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
                Projectile flare = ProjectileController.SpawnProjectileLocally(flarePrefab, this, x, y, xSpeed, ySpeed, base.playerNum);
                flare.gameObject.SetActive(true);
                this.gunFrame = 3;
                this.PlayFlareSound(base.transform.position);
                this.fireDelay = flaregunDelay;
            }
        }

        public void PlayChargeSound(Vector3 position)
        {
            this.sound.PlaySoundEffectAt(this.chargeSound, 0.35f, position, 1f, true, false, true, 0f);
        }

        public void PlayCrossbowSound(Vector3 position)
        {
            this.sound.PlaySoundEffectAt(this.crossbowSounds, 0.35f, position, 1f, true, false, true, 0f);
        }

        public void PlayFlareSound(Vector3 position)
        {
            this.sound.PlaySoundEffectAt(this.flareSounds, 0.75f, position, 1f, true, false, true, 0f);
        }

        public void PlaySwapSound(Vector3 position)
        {
            this.sound.PlaySoundEffectAt(this.swapSound, 0.35f, position, 1f, true, false, true, 0f);
        }

        protected override void RunGun()
        {
            if ( this.currentState == PrimaryState.Crossbow )
            {
                if ( this.fire )
                {
                    if ( this.chargeTime > 0.2f )
                    {
                        // Starting charge
                        if ( this.chargeCounter == 0 )
                        {
                            this.gunCounter += this.t;
                            if (this.gunCounter < 1f && this.gunCounter > 0.08f)
                            {
                                this.gunCounter -= 0.08f;
                                ++this.gunFrame;
                                if ( this.gunFrame > 5 )
                                {
                                    ++this.chargeCounter;
                                    this.gunFramerate = 0.09f;
                                }
                            }
                        }
                        // Holding pattern
                        else
                        {
                            this.gunCounter += this.t;
                            if (this.gunCounter > this.gunFramerate)
                            {
                                this.gunCounter -= this.gunFramerate;
                                ++this.gunFrame;
                                if (this.gunFrame > 9)
                                {
                                    this.gunFrame = 6;
                                    if ( !this.charged )
                                    {
                                        ++this.chargeCounter;
                                        if (this.chargeCounter > 1)
                                        {
                                            this.PlayChargeSound(base.transform.position);
                                            this.charged = true;
                                            this.gunFramerate = 0.04f;
                                        }
                                    }
                                }
                            }
                        }
                        this.SetGunSprite(this.gunFrame + 14, 0);
                    }
                }
                else if (!this.WallDrag && this.gunFrame > 0)
                {
                    this.gunCounter += this.t;
                    if (this.gunCounter > 0.045f)
                    {
                        this.gunCounter -= 0.045f;
                        this.gunFrame--;
                        this.SetGunSprite(this.gunFrame, 0);
                    }
                }
            }
            // Animate flaregun
            else if (this.currentState == PrimaryState.FlareGun)
            {
                if ( this.gunFrame > 0 )
                {
                    this.gunCounter += this.t;
                    if ( this.gunCounter > 0.0334f )
                    {
                        this.gunCounter -= 0.0334f;
                        --this.gunFrame;
                    }
                }
                this.SetGunSprite(this.gunFrame, 0);
            }
            // Animate switching
            else
            {
                this.gunCounter += this.t;
                if (this.gunCounter > 0.07f)
                {
                    this.gunCounter -= 0.07f;
                    ++this.gunFrame;

                    if (this.gunFrame == 3)
                    {
                        this.PlaySwapSound(base.transform.position);
                    }
                }
                
                if (this.gunFrame > 5)
                {
                    this.SwitchWeapon();
                }
                else
                {
                    this.SetGunSprite(25 + this.gunFrame, 0);
                }
            }
        }

        public override void StartPilotingUnit(Unit pilottedUnit)
        {
            // Finish switching weapon
            if ( this.currentState == PrimaryState.Switching )
            {
                this.SwitchWeapon();
            }

            // Make sure to release any held units
            this.ReleaseUnit(false);

            // Ensure we don't double fire when exiting units
            this.fire = this.wasFire = false;
            base.StartPilotingUnit(pilottedUnit);
        }

        protected void StartSwitchingWeapon()
        {
            if ( !this.usingSpecial && this.currentState != PrimaryState.Switching )
            {
                this.CancelMelee();
                this.SetGestureAnimation(GestureElement.Gestures.None);
                if ( this.currentState == PrimaryState.Crossbow )
                {
                    this.nextState = PrimaryState.FlareGun;
                }
                else
                {
                    this.nextState = PrimaryState.Crossbow;
                }
                this.currentState = PrimaryState.Switching;
                this.gunFrame = 0;
                this.gunCounter = 0f;
                this.fireDelay = 0f;
                this.RunGun();
            } 
        }

        public void SwitchWeapon()
        {
            this.gunFrame = 0;
            this.gunCounter = 0f;
            this.currentState = this.nextState;
            if ( this.currentState == PrimaryState.FlareGun )
            {
                if ( this.grabbedUnit != null )
                {
                    this.holdingArm.gameObject.SetActive(false);
                }
                this.gunSprite.meshRender.material = this.flareGunMat;
            }
            else
            {
                if ( this.grabbedUnit != null )
                {
                    this.holdingArm.gameObject.SetActive(true);
                }
                this.gunSprite.meshRender.material = this.crossbowMat;
            }
            this.SetGunSprite(0, 0);
        }

        protected override void CheckInput()
        {
            base.CheckInput();
            if (doubleTapSwitch && this.down && !this.wasDown && base.actionState != ActionState.ClimbingLadder)
            {
                if (Time.realtimeSinceStartup - this.lastDownPressTime < 0.2f)
                {
                    this.StartSwitchingWeapon();
                }
                this.lastDownPressTime = Time.realtimeSinceStartup;
            }
        }
        #endregion

        #region Melee
        protected override void SetMeleeType()
        {
            base.SetMeleeType();
            // Set dashing melee to true if we're jumping and dashing so that we can transition to dashing on landing
            if (this.jumpingMelee && (this.right || this.left))
            {
                this.dashingMelee = true;
            }
        }

        protected override void StartCustomMelee()
        {
            // Throwback mook instead of doing melee
            if (this.grabbedUnit != null)
            {
                this.ReleaseUnit(true);
                return;
            }

            if (this.CanStartNewMelee())
            {
                base.frame = 1;
                base.counter = -0.05f;
                this.AnimateMelee();
            }
            else if (this.CanStartMeleeFollowUp())
            {
                this.meleeFollowUp = true;
            }
            if (!this.jumpingMelee)
            {
                this.dashingMelee = true;
                this.xI = (float)base.Direction * this.speed;
            }

            this.throwingMook = (this.nearbyMook != null && this.nearbyMook.CanBeThrown());

            if ( !this.doingMelee )
            {
                this.StartMeleeCommon();
            }
        }

        protected override void AnimateCustomMelee()
        {
            this.AnimateMeleeCommon();
            if (!this.throwingMook)
            {
                base.frameRate = 0.07f;
            }
            this.sprite.SetLowerLeftPixel((25 + base.frame) * this.spritePixelWidth, 9 * this.spritePixelHeight);
            if (base.frame == 2)
            {
                this.sound.PlaySoundEffectAt(this.meleeSwingSounds, 0.3f, base.transform.position, 1f, true, false, true, 0f);
            }
            else if (base.frame == 3)
            {
                base.counter -= 0.066f;
                this.GrabUnit();
            }
            else if (base.frame > 3 && !this.meleeHasHit)
            {
                this.GrabUnit();
            }

            if (this.meleeHasHit && this.grabbedUnit != null)
            {
                switch (base.frame)
                {
                    case 3:
                        this.holdingXOffset = 5f;
                        this.holdingYOffset = 1f;
                        break;
                    case 4:
                        this.holdingXOffset = 7f;
                        this.holdingYOffset = 2f;
                        break;
                    case 5:
                        this.holdingXOffset = 9f;
                        this.holdingYOffset = 3f;
                        break;
                    case 6:
                        this.holdingXOffset = 11f;
                        this.holdingYOffset = 4.5f;
                        break;
                }
            }
            // Cancel melee early when punching
            else if (base.frame == 5)
            {
                base.frame = 0;
                this.CancelMelee();
            }

            if (base.frame >= 7)
            {
                base.frame = 0;
                this.CancelMelee();
            }
        }

        protected override void RunCustomMeleeMovement()
        {
            if (this.jumpingMelee)
            {
                this.ApplyFallingGravity();
                if (this.yI < this.maxFallSpeed)
                {
                    this.yI = this.maxFallSpeed;
                }
            }
            else if (this.dashingMelee)
            {
                if (base.frame <= 1)
                {
                    this.xI = 0f;
                    this.yI = 0f;
                }
                else if (base.frame <= 3)
                {
                    if (this.meleeChosenUnit == null)
                    {
                        if (!this.isInQuicksand)
                        {
                            this.xI = this.speed * 1f * base.transform.localScale.x;
                        }
                        this.yI = 0f;
                    }
                    else if (!this.isInQuicksand)
                    {
                        this.xI = this.speed * 0.5f * base.transform.localScale.x + (this.meleeChosenUnit.X - base.X) * 2f;
                    }
                }
                else if (base.frame <= 5)
                {
                    if (!this.isInQuicksand)
                    {
                        this.xI = this.speed * 0.3f * base.transform.localScale.x;
                    }
                    this.ApplyFallingGravity();
                }
                else
                {
                    this.ApplyFallingGravity();
                }
            }
            else if (base.Y > this.groundHeight + 1f)
            {
                this.CancelMelee();
            }
        }

        protected override void CancelMelee()
        {
            this.holdingXOffset = 11f;
            this.holdingYOffset = 4.5f;
            if (this.grabbedUnit != null)
            {
                this.SwitchToHoldingMaterials();
            }
            base.CancelMelee();
        }

        public static Unit GetNextClosestUnit(int playerNum, DirectionEnum direction, float xRange, float yRange, float x, float y, List<Unit> alreadyFoundUnits)
        {
            if (Map.units == null)
            {
                return null;
            }
            float num = xRange;
            Unit unit = null;
            for (int i = Map.units.Count - 1; i >= 0; i--)
            {
                Unit unit2 = Map.units[i];
                if (unit2 != null && !unit2.invulnerable && unit2.health > 0 && GameModeController.DoesPlayerNumDamage(playerNum, unit2.playerNum) && !alreadyFoundUnits.Contains(unit2) )
                {
                    float num2 = unit2.Y + unit2.height / 2f + 3f - y;
                    if (Mathf.Abs(num2) - yRange < unit2.height)
                    {
                        float num3 = unit2.X - x;
                        if (Mathf.Abs(num3) - num < unit2.width && ((direction == DirectionEnum.Down && num2 < 0f) || (direction == DirectionEnum.Up && num2 > 0f) || (direction == DirectionEnum.Right && num3 > 0f) || (direction == DirectionEnum.Left && num3 < 0f) || direction == DirectionEnum.Any))
                        {
                            unit = unit2;
                            num = Mathf.Abs(num2);
                        }
                    }
                }
            }
            if (unit != null)
            {
                return unit;
            }
            return null;
        }

        protected void GrabUnit()
        {
            bool flag;
            Map.DamageDoodads(3, DamageType.Knock, base.X + (float)(base.Direction * 4), base.Y, 0f, 0f, 6f, base.playerNum, out flag, null);
            this.KickDoors(24f);
            Unit unit = GetNextClosestUnit(this.playerNum, base.transform.localScale.x > 0 ? DirectionEnum.Right : DirectionEnum.Left, 20f, 12f, base.X, base.Y + base.height / 2f, new List<Unit>());
            if ( unit != null )
            {
                // Pickup unit if not heavy and not a dog and not on the ground
                if ( !unit.IsHeavy() && unit.actionState != ActionState.Fallen && !(unit is MookDog || (unit is AlienMosquito && !(unit is HellLostSoul))) )
                {
                    this.meleeHasHit = true;
                    this.grabbedUnit = unit;
                    unit.Panic(1000f, true);
                    unit.playerNum = this.playerNum;
                    unit.gameObject.layer = 28;
                    this.doRollOnLand = false;
                }
                // Punch unit
                else
                {
                    this.meleeHasHit = true;
                    Map.KnockAndDamageUnit(this, unit, 5, DamageType.Knock, 200f, 100f, (int)Mathf.Sign(base.transform.localScale.x), true, base.X, base.Y, false);
                    this.sound.PlaySoundEffectAt(this.soundHolder.alternateMeleeHitSound, 0.3f, base.transform.position, 1f, true, false, false, 0f);
                    EffectsController.CreateProjectilePopWhiteEffect(base.X + (this.width + 4f) * base.transform.localScale.x, base.Y + this.height + 4f);
                }
            }
            // Try hit terrain
            else
            {
               this.meleeHasHit = this.TryMeleeTerrain(4, 4);
            }
        }

        protected void ReleaseUnit(bool throwUnit)
        {
            if (this.grabbedUnit != null)
            {
                this.unitWasGrabbed = false;
                this.grabbedUnit.playerNum = -1;
                (this.grabbedUnit as Mook).blindTime = 0;
                if (throwUnit)
                {
                    this.ThrowBackMook(this.grabbedUnit as Mook);
                }
                this.grabbedUnit.gameObject.layer = 25;
                this.grabbedUnit = null;
                this.SwitchToNormalMaterials();
                this.ChangeFrame();
            }
            this.doRollOnLand = true;
        }

        protected void SwitchToHoldingMaterials()
        {
            this.crossbowMat = this.crossbowHoldingMat;
            this.flareGunMat = this.flareGunHoldingMat;
            if (this.currentState == PrimaryState.Crossbow)
            {
                this.holdingArm.gameObject.SetActive(true);
                this.gunSprite.meshRender.material = this.crossbowMat;
            }
            else if (this.currentState == PrimaryState.FlareGun)
            {
                this.gunSprite.meshRender.material = this.flareGunMat;
            }
        }

        protected void SwitchToNormalMaterials()
        {
            this.crossbowMat = this.crossbowNormalMat;
            this.holdingArm.gameObject.SetActive(false);
            this.flareGunMat = this.flareGunNormalMat;
            if (this.currentState == PrimaryState.Crossbow)
            {
                this.gunSprite.meshRender.material = this.crossbowMat;
            }
            else if (this.currentState == PrimaryState.FlareGun)
            {
                this.gunSprite.meshRender.material = this.flareGunMat;
            }
        }

        public override void Damage(int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY)
        {
            // Check if we're holding a unit as a shield and facing the explosion
            if ( (damageType == DamageType.Explosion || damageType == DamageType.Fire || damageType == DamageType.Acid) && this.grabbedUnit != null && direction != base.transform.localScale.x )
            {
                Unit previousUnit = this.grabbedUnit;
                this.ReleaseUnit(false);
                previousUnit.Damage(damage * 2, damageType, xI, yI, direction, damageSender, hitX, hitY);
                this.Knock(damageType, xI, yI, false);
            }
            else
            {
                base.Damage(damage, damageType, xI, yI, direction, damageSender, hitX, hitY);
            }
        }

        public override void SetGestureAnimation(GestureElement.Gestures gesture)
        {
            if ( gesture == GestureElement.Gestures.Flex )
            {
                this.ReleaseUnit(false);
            }
            base.SetGestureAnimation(gesture);
        }
        #endregion

        #region Special
        // Make War Rig blow up
        protected void DestroyCurrentWarRig()
        {
            if (currentWarRig != null)
            {
                this.currentWarRig.Death();
                this.currentWarRig = null;
            }
        }

        Vector3 DetermineWarRigSpawn()
        {
            // Facing right
            if ( base.transform.localScale.x > 0 )
            {
                return new Vector3(SortOfFollow.GetScreenMinX() - 65f, base.Y, 0f);
            }
            // Facing left
            else
            {
                return new Vector3(SortOfFollow.GetScreenMaxX() + 65f, base.Y, 0f);
            }
        }

        protected override void UseSpecial()
        {
            if (this.SpecialAmmo > 0 && this.specialGrenade != null)
            {
                this.SpecialAmmo--;
                this.DestroyCurrentWarRig();
                this.currentWarRig = UnityEngine.Object.Instantiate<WarRig>(warRigPrefab, DetermineWarRigSpawn(), Quaternion.identity);
                this.currentWarRig.SetTarget(this, base.X + base.transform.localScale.x * 10f, new Vector3(base.transform.localScale.x, this.currentWarRig.transform.localScale.y, this.currentWarRig.transform.localScale.z), base.transform.localScale.x);
                this.currentWarRig.gameObject.SetActive(true);
                if ( this.special )
                {
                    this.holdingSpecial = true;
                    this.holdingSpecialTime = 0f;
                }
            }
            else
            {
                HeroController.FlashSpecialAmmo(base.playerNum);
                this.ActivateGun();
            }
            this.pressSpecialFacingDirection = 0;
        }

        // Makes the War Rig continue moving past where Furiosa summoned it to
        public void GoPastFuriosa()
        {
            this.currentWarRig.keepGoingBeyondTarget = true;

            if ( this.currentWarRig.summonedDirection == 1 )
            {
                this.currentWarRig.secondTargetX = SortOfFollow.GetScreenMaxX() - 20f;
            }
            else
            {
                this.currentWarRig.secondTargetX = SortOfFollow.GetScreenMinX() + 20f;
            }
            this.holdingSpecial = false;
        }
        #endregion
    }
}
