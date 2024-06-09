using System;
using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using System.IO;
using System.Reflection;
using UnityEngine;
using RocketLib;
using Rogueforce;
using JetBrains.Annotations;
using RocketLib.Collections;
using Networking;
using System.Linq;
using HarmonyLib;
using World.LevelEdit.Triggers;
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
        public static Projectile flarePrefab;
        public static bool doubleTapSwitch = true;
        protected float lastDownPressTime = -1f;

        // Melee
        protected MeshRenderer holdingArm;
        public static List<Unit> grabbedUnits = new List<Unit> { null, null, null, null };
        Unit grabbedUnit
        {
            get
            {
                return grabbedUnits[this.playerNum];
            }
            set
            {
                grabbedUnits[this.playerNum] = value;
            }
        }
        protected bool unitWasGrabbed = false;
        protected bool throwingMook = false;
        protected float holdingXOffset = 0f;
        protected float holdingYOffset = 0f;

        // Special
        static protected WarRig warRigPrefab;
        protected WarRig currentWarRig;
        protected bool holdingSpecial = false;
        protected float holdingSpecialTime = 0f;

        // Debug
        public static Furibrosa currentChar;
        public Gib lastGib;

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

                BoxCollider collider = new GameObject("BoltCollider", new Type[] { typeof(Transform), typeof(BoxCollider) }).GetComponent<BoxCollider>();
                collider.enabled = false;
                collider.transform.parent = boltPrefab.transform;

                Transform transform = new GameObject("BoltForeground", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) }).transform;
                transform.parent = boltPrefab.transform;
                boltPrefab.Setup(false);
                UnityEngine.Object.DontDestroyOnLoad(boltPrefab);
            }
            
            if ( explosiveBoltPrefab == null )
            {
                explosiveBoltPrefab = new GameObject("ExplosiveBolt", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(BoxCollider), typeof(Bolt) }).GetComponent<Bolt>();
                explosiveBoltPrefab.gameObject.SetActive(false);
                explosiveBoltPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro).projectile.soundHolder;

                BoxCollider explosiveCollider = new GameObject("BoltCollider", new Type[] { typeof(Transform), typeof(BoxCollider) }).GetComponent<BoxCollider>();
                explosiveCollider.enabled = false;
                explosiveCollider.transform.parent = explosiveBoltPrefab.transform;

                Transform explosiveTransform = new GameObject("BoltForeground", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) }).transform;
                explosiveTransform.parent = explosiveBoltPrefab.transform;
                explosiveBoltPrefab.Setup(true);
                UnityEngine.Object.DontDestroyOnLoad(explosiveBoltPrefab);
            }
            
            for (int i = 0; i < InstantiationController.PrefabList.Count; ++i)
            {
                if (InstantiationController.PrefabList[i].name == "Bullet Flare")
                {
                    flarePrefab = (InstantiationController.PrefabList[i] as GameObject).GetComponent<Projectile>();
                    break;
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
                        if (InstantiationController.PrefabList[i].name == "ZMookArmouredGuy")
                        {
                            warRig = UnityEngine.Object.Instantiate(InstantiationController.PrefabList[i], Vector3.zero, Quaternion.identity) as GameObject;
                        }
                    }

                    if (warRig != null)
                    {
                        warRigPrefab = warRig.AddComponent<WarRig>();
                        warRigPrefab.Setup();
                    }
                    UnityEngine.Object.DontDestroyOnLoad(warRigPrefab);
                }
            } 
            catch( Exception ex )
            {
                BMLogger.Log("Exception creating WarRig: " + ex.ToString());
            }

            // Debug
            currentChar = this;
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
                    if (this.holdingSpecialTime > 0.5f)
                    {
                        this.currentWarRig.keepGoingBeyondTarget = true;
                        this.currentWarRig.secondTargetX = SortOfFollow.GetScreenMaxX() - 20f;
                        this.holdingSpecial = false;
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

            // DEBUG
            if ( GUILayout.Button("create gibs") )
            {
                if (currentChar.currentWarRig != null )
                {
                    Gib gibPrefab = currentChar.currentWarRig.gibs.transform.GetChild(0).GetComponent<Gib>();
                    try
                    {
                        Gib gib = EffectsController.InstantiateEffect(gibPrefab) as Gib;
                        gib.GetComponent<Renderer>().sharedMaterial = currentChar.currentWarRig.GetComponent<MeshRenderer>().material;
                        gib.SetupSprite(gibPrefab.doesRotate, gibPrefab.GetLowerLeftPixel(), gibPrefab.GetPixelDimensions(), gibPrefab.GetSpriteOffset(), gibPrefab.rotateFrames);
                        float xI2 = gibPrefab.transform.localPosition.x * (float)1 / 16f * 1 + xI;
                        gib.Launch(currentChar.currentWarRig.X + gibPrefab.transform.localPosition.x * (float)1, currentChar.currentWarRig.Y + gibPrefab.transform.localPosition.y + 50f, xI2, gibPrefab.transform.localPosition.y / 16f * 1 + yI);
                        currentChar.lastGib = gib;
                    }
                    catch (Exception ex)
                    {
                        BMLogger.Log("failed launching gib: " + ex.ToString());
                    }
                }
            }
        }

        public override void HarmonyPatches(Harmony harmony)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll(assembly);
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
                this.fireDelay = 0.5f;
            }
            else if ( this.currentState == PrimaryState.FlareGun )
            {
                x = base.X + base.transform.localScale.x * 12f;
                y = base.Y + 8f;
                xSpeed = base.transform.localScale.x * 450;
                ySpeed = UnityEngine.Random.Range(15, 50);
                EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
                ProjectileController.SpawnProjectileLocally(flarePrefab, this, x, y, xSpeed, ySpeed, base.playerNum);
                this.gunFrame = 3;
                this.fireDelay = 0.8f;
            }
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
                if (this.gunCounter > 0.08f)
                {
                    this.gunCounter -= 0.08f;
                    ++this.gunFrame;
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
            if (this.grabbedUnit != null)
            {
                this.ReleaseUnit(false);
            }
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
            if (base.frame == 3)
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

        public override void Death(float xI, float yI, DamageObject damage)
        {
            if ( this.grabbedUnit != null )
            {
                this.ReleaseUnit(false);
            }
            base.Death(xI, yI, damage);
        }

        protected override void OnDestroy()
        {
            if (this.grabbedUnit != null)
            {
                this.ReleaseUnit(false);
            }
            base.OnDestroy();
        }

        protected void ReleaseUnit(bool throwUnit)
        {
            this.unitWasGrabbed = false;
            this.grabbedUnit.playerNum = -1;
            (this.grabbedUnit as Mook).blindTime = 0;
            if ( throwUnit )
            {
                this.ThrowBackMook(this.grabbedUnit as Mook);
            }
            this.grabbedUnit.gameObject.layer = 25;
            this.grabbedUnit = null;
            this.SwitchToNormalMaterials();
            this.ChangeFrame();
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
                // Pickup unit if not heavy and not a dog
                if ( !unit.IsHeavy() && !(unit is MookDog || (unit is AlienMosquito && !(unit is HellLostSoul))) )
                {
                    this.meleeHasHit = true;
                    this.grabbedUnit = unit;
                    unit.Panic(1000f, true);
                    unit.playerNum = this.playerNum;
                    unit.gameObject.layer = 28;
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
               this.meleeHasHit = this.TryMeleeTerrain(0, 4);
            }
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
            return new Vector3(SortOfFollow.GetScreenMinX() - 65f, base.Y, 0f);
        }

        protected override void UseSpecial()
        {
            if (this.SpecialAmmo > 0 && this.specialGrenade != null)
            {
                this.SpecialAmmo--;
                this.DestroyCurrentWarRig();
                this.currentWarRig = UnityEngine.Object.Instantiate<WarRig>(warRigPrefab, DetermineWarRigSpawn(), Quaternion.identity);
                this.currentWarRig.targetX = base.X + 10f;
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
        #endregion
    }
}
