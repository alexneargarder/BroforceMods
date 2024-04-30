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

namespace Furibrosa
{
    [HeroPreset("Furibrosa", HeroType.Rambro)]
    public class Furibrosa : CustomHero
    {
        // General
        public static KeyBindingForPlayers switchWeaponKey;
        protected bool acceptedDeath = false;
        bool wasInvulnerable = false;

        // Primary
        public enum PrimaryState
        {
            Crossbow = 0,
            FlareGun = 1,
            Switching = 2
        }
        PrimaryState currentState = PrimaryState.Crossbow;
        PrimaryState nextState;
        protected Bolt boltPrefab, explosiveBoltPrefab;
        protected bool releasedFire = false;
        protected float chargeTime = 0f;
        protected int chargeCounter = 0;
        protected bool charged = false;
        protected Material crossbowMat, crossbowNormalMat, crossbowHoldingMat;
        protected Material flareGunMat, flareGunNormalMat, flareGunHoldingMat;
        protected float gunFramerate = 0f;
        protected MeshRenderer holdingArm;

        // Flare Primary
        Projectile flarePrefab;

        // Melee
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
        protected bool throwingMook = false;

        // Special

        // DEBUG
        public static string offsetXstr = "12";
        public static float offsetXVal = 12f;
        public static string offsetYstr = "4.5";
        public static float offsetYVal = 4.5f;

        #region General
        protected override void Awake()
        {
            if (switchWeaponKey == null )
            {
                LoadKeyBinding();
            }

            base.Awake();
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

            
            boltPrefab = new GameObject("Bolt", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(BoxCollider), typeof(Bolt)}).GetComponent<Bolt>();
            boltPrefab.gameObject.SetActive(false);
            boltPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro).projectile.soundHolder;

            BoxCollider collider = new GameObject("BoltCollider", new Type[] { typeof(Transform), typeof(BoxCollider) }).GetComponent<BoxCollider>();
            collider.enabled = false;
            collider.transform.parent = boltPrefab.transform;

            Transform transform = new GameObject("BoltForeground", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) }).transform;
            transform.parent = boltPrefab.transform;
            boltPrefab.Setup(false);

            explosiveBoltPrefab = new GameObject("ExplosiveBolt", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(BoxCollider), typeof(Bolt) }).GetComponent<Bolt>();
            explosiveBoltPrefab.gameObject.SetActive(false);
            explosiveBoltPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro).projectile.soundHolder;

            BoxCollider explosiveCollider = new GameObject("BoltCollider", new Type[] { typeof(Transform), typeof(BoxCollider) }).GetComponent<BoxCollider>();
            explosiveCollider.enabled = false;
            explosiveCollider.transform.parent = explosiveBoltPrefab.transform;

            Transform explosiveTransform = new GameObject("BoltForeground", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) }).transform;
            explosiveTransform.parent = explosiveBoltPrefab.transform;
            explosiveBoltPrefab.Setup(true);

            for (int i = 0; i < InstantiationController.PrefabList.Count; ++i)
            {
                if (InstantiationController.PrefabList[i].name == "Bullet Flare")
                {
                    this.flarePrefab = (InstantiationController.PrefabList[i] as GameObject).GetComponent<Projectile>();
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
            if ( switchWeaponKey.IsDown(playerNum) )
            {
                StartSwitchingWeapon();
            }

            // Check if invulnerability ran out
            if (this.wasInvulnerable && !this.invulnerable)
            {
                // Fix any not currently displayed textures
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (this.grabbedUnit != null)
            {
                if ( this.grabbedUnit.health > 0 && this.health > 0 )
                {
                    this.grabbedUnit.X = base.X + base.transform.localScale.x * offsetXVal;
                    this.grabbedUnit.Y = base.Y + offsetYVal;
                    if ( this.currentState == PrimaryState.Crossbow )
                    {
                        this.grabbedUnit.zOffset = -0.95f;
                    }
                    else if ( this.currentState == PrimaryState.FlareGun )
                    {
                        this.grabbedUnit.zOffset = -2;
                    }
                    this.grabbedUnit.transform.localScale = base.transform.localScale;
                }
                else
                {
                    this.ReleaseUnit(false);
                }
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



            // DEBUG options
            makeTextBox("offsetX", ref offsetXstr, ref offsetXVal);
            makeTextBox("offsetY", ref offsetYstr, ref offsetYVal);
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
                this.RunGun();
            } 
        }

        protected void SwitchWeapon()
        {
            this.gunFrame = 0;
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
        #endregion

        #region Melee
        protected override void StartCustomMelee()
        {
            // Throwback mook instead of doing melee
            if ( this.grabbedUnit != null )
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

            this.StartMeleeCommon();
        }

        protected override void AnimateCustomMelee()
        {
            this.AnimateMeleeCommon();
            if ( !this.throwingMook )
            {
                base.frameRate = 0.08f;
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
            if (base.frame >= 6)
            {
                base.frame = 0;
                this.CancelMelee();
            }
        }

        protected override void RunCustomMeleeMovement()
        {
            if (!this.useNewKnifingFrames)
            {
                if (base.Y > this.groundHeight + 1f)
                {
                    this.ApplyFallingGravity();
                }
            }
            else if (this.jumpingMelee)
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
                        this.xI = this.speed * 0.5f * base.transform.localScale.x + (this.meleeChosenUnit.X - base.X) * 6f;
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
        
        protected void ReleaseUnit(bool throwUnit)
        {
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

        protected void GrabUnit()
        {
            Unit unit = Map.GetNextClosestUnit(this.playerNum, base.transform.localScale.x > 0 ? DirectionEnum.Right : DirectionEnum.Left, 20f, 6f, base.X, base.Y, new List<Unit>());
            if ( unit != null)
            {
                this.meleeHasHit = true;
                this.grabbedUnit = unit;
                unit.Panic(1000f, true);
                unit.playerNum = this.playerNum;
                unit.gameObject.layer = 28;
                this.SwitchToHoldingMaterials();
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
        #endregion

        #region Special
        #endregion
    }
}
