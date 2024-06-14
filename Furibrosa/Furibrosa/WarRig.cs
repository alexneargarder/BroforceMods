using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using BroMakerLib.CustomObjects.Bros;
using RocketLib;
using HarmonyLib;
using BroMakerLib;
using System.IO;
using System.Reflection;
using BroMakerLib.Loggers;
using RocketLib.Collections;
using static Furibrosa.Furibrosa;

namespace Furibrosa
{
    public class WarRig : Mook
    {
        // General Variables
        string directoryPath;
        public Unit pilotUnit = null;
        public bool pilotted = false;
        public Furibrosa summoner = null;
        protected MobileSwitch pilotSwitch;
        protected bool hasResetDamage;
        protected int shieldDamage;
        public bool hasBeenPiloted;
        protected int fireAmount;
        protected int knockCount;
        public bool alwaysKnockOnExplosions = false;
        protected float pilotUnitDelay;
        protected bool fixedBubbles = false;
        public Material originalSpecialSprite;
        public Material specialSprite;
        protected PlayerHUD hud;
        public const int maxDamageBeforeExploding = 50;
        protected int deathCount = 0;
        protected float smokeCounter = 0f;
        protected float deathCountdownCounter = 0f;
        protected float deathCountdown = 0f;
        protected int deathCountdownExplodeThreshold = 20;

        // Movement variables
        protected float wheelsCounter = 0f;
        protected int wheelsFrame = 0;
        protected int crushingGroundLayers;
        protected float fallDamageHurtSpeed = -450;
        protected float fallDamageHurtSpeedHero = -550;
        protected float fallDamageDeathSpeed = -600;
        protected float fallDamageDeathSpeedHero = -750;
        protected float boostFuel = 1f;
        protected const float originalGroundFriction = 0.75f;
        protected float groundFriction = 0.75f;
        protected float lastDashTime = -5f;
        
        // Collision Variables
        protected float frontHeadHeight;
        protected float distanceToFront;
        protected float distanceToBack;
        public BoxCollider platform;
        protected int crushDamage = 5;
        protected float crushXRange = 30f;
        protected float crushYRange = 50f;
        protected float crushXOffset = 53f;
        protected float crushYOffset = 30f;
        protected float unitXRange = 6f;
        protected float unitYRange = 30f;
        protected float crushDamageCooldown = 0f;

        // Sprite Variables
        public SpriteSM wheelsSprite, bumperSprite, longSmokestacksSprite, shortSmokestacksSprite, frontSmokestacksSprite;
        protected float smokestackCooldown = 0f;
        protected float smokestackCounter = 0f;
        protected int smokestackFrame = 0;
        protected float blueSmokeCooldown = 0f;
        protected float blueSmokeCounter = 0f;
        protected int blueSmokeFrame = 0;
        protected bool usingBlueFlame = false;

        // Startup Variables
        protected bool reachedStartingPoint = false;
        public float targetX = 0f;
        public bool keepGoingBeyondTarget = false;
        public float secondTargetX = 0f;
        public float summonedDirection = 1f;

        // Primary Variables
        public enum FuriosaState
        {
            InVehicle = 0,
            GoingOut = 1,
            HangingOut = 2,
            GoingIn = 3,
        }
        FuriosaState currentFuriosaState = FuriosaState.InVehicle;
        protected float hangingOutTimer = 0f;
        PrimaryState currentPrimaryState = PrimaryState.Crossbow;
        PrimaryState nextPrimaryState;
        protected bool releasedFire = false;
        protected float chargeTime = 0f;
        protected bool charged = false;
        public Material crossbowMat, flareGunMat;
        protected float chargeFramerate = 0.09f;
        public static bool doubleTapSwitch = true;
        protected float lastDownPressTime = -1f;

        // Special Variables
        protected int specialFrame = 0;
        protected float specialFrameCounter = 0f;
        public static Harpoon harpoonPrefab;

        #region General
        public SpriteSM LoadSprite(GameObject gameObject, string spritePath, Vector3 offset)
        {
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

            //Material material = ResourcesController.GetMaterial(directoryPath, spritePath);
            Material material = ResourcesController.CreateMaterial(Path.Combine(directoryPath, spritePath), ResourcesController.Particle_AlphaBlend);
            renderer.material = material;

            SpriteSM sprite = gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2(0, 64);
            sprite.pixelDimensions = new Vector2(128, 64);
            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 128;
            sprite.height = 64;
            sprite.offset = offset;

            gameObject.layer = 19;

            return sprite;
        }

        public void Setup()
        {
            directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            this.gameObject.name = "WarRig";
            UnityEngine.Object.Destroy(this.gameObject.FindChildOfName("ZMook"));
            UnityEngine.Object.Destroy(this.GetComponent<BigGuyAI>());
            MookArmouredGuy mookArmouredGuy = this.gameObject.GetComponent<MookArmouredGuy>();
            Traverse trav = Traverse.Create(mookArmouredGuy);

            // Assign null values
            this.sprite = this.GetComponent<SpriteSM>();
            this.gunSprite = mookArmouredGuy.gunSprite;
            this.soundHolder = mookArmouredGuy.soundHolder;
            this.soundHolderFootSteps = mookArmouredGuy.soundHolderFootSteps;
            this.player1Bubble = mookArmouredGuy.player1Bubble;
            this.player2Bubble = mookArmouredGuy.player2Bubble;
            this.player3Bubble = mookArmouredGuy.player3Bubble;
            this.player4Bubble = mookArmouredGuy.player4Bubble;

            this.blood = mookArmouredGuy.blood;
            this.heroTrailPrefab = mookArmouredGuy.heroTrailPrefab;
            this.high5Bubble = mookArmouredGuy.high5Bubble;
            this.projectile = mookArmouredGuy.projectile;
            this.specialGrenade = mookArmouredGuy.specialGrenade;
            if ( this.specialGrenade != null )
            {
                this.specialGrenade.playerNum = mookArmouredGuy.specialGrenade.playerNum;
            }
            this.heroType = mookArmouredGuy.heroType;
            this.wallDragAudio = trav.GetFieldValue("wallDragAudio") as AudioSource;
            this.SetOwner(mookArmouredGuy.Owner);

            UnityEngine.Object.Destroy(mookArmouredGuy);

            // Load all sprites
            // Ensure main sprite is behind all other sprites
            LoadSprite(this.gameObject, "vehicleSprite.png", new Vector3(0f, 31f, 0.2f));

            // Load weapon sprites
            this.crossbowMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "vehicleCrossbow.png"), ResourcesController.Particle_AlphaBlend);
            this.flareGunMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "vehicleFlareGun.png"), ResourcesController.Particle_AlphaBlend);
            LoadSprite(this.gunSprite.gameObject, "vehicleCrossbow.png", new Vector3(0f, 31f, 0.1f));

            // Load wheel sprites
            GameObject wheelsObject = new GameObject("WarRigWheels", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) });
            wheelsObject.transform.parent = this.transform;
            this.wheelsSprite = LoadSprite(wheelsObject, "vehicleWheels.png", new Vector3(0f, 31f, 0.1f));

            // Load bumper sprites
            GameObject bumperObject = new GameObject("WarRigBumper", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM)} );
            bumperObject.transform.parent = this.transform;
            this.bumperSprite = LoadSprite(bumperObject, "vehicleBumper.png", new Vector3(0f, 31f, 0.1f));

            // Load long smokestack sprites
            GameObject longSmokestacksObject = new GameObject("WarRigLongSmokestacks", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) });
            longSmokestacksObject.transform.parent = this.transform;
            this.longSmokestacksSprite = LoadSprite(longSmokestacksObject, "vehicleLongSmokestacks.png", new Vector3(0f, 56f, 0.1f));
            this.longSmokestacksSprite.SetLowerLeftPixel(0f, 128f);

            // Load short smokestack sprites
            GameObject shortSmokestacksObject = new GameObject("WarRigShortSmokestacks", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) });
            shortSmokestacksObject.transform.parent = this.transform;
            this.shortSmokestacksSprite = LoadSprite(shortSmokestacksObject, "vehicleShortSmokestacks.png", new Vector3(0f, 56f, 0.1f));
            this.shortSmokestacksSprite.SetLowerLeftPixel(0f, 128f);

            // Load front smokestack sprites
            GameObject frontSmokestacksObject = new GameObject("WarRigFrontSmokestacks", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) });
            frontSmokestacksObject.transform.parent = this.transform;
            this.frontSmokestacksSprite = LoadSprite(frontSmokestacksObject, "vehicleFrontSmokestacks.png", new Vector3(0f, 31f, 0.1f));
            this.frontSmokestacksSprite.SetLowerLeftPixel(0f, 128f);

            this.spritePixelWidth = 128;
            this.spritePixelHeight = 64;

            // Load special icon sprite
            this.originalSpecialSprite = ResourcesController.GetMaterial(directoryPath, "special.png");
            this.specialSprite = ResourcesController.GetMaterial(directoryPath, "vehicleSpecial.png");

            // Clear blood shrapnel
            this.blood = new Shrapnel[] { };

            // Create Harpoon prefab if not yet created
            if (harpoonPrefab == null)
            {
                harpoonPrefab = new GameObject("Harpoon", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(BoxCollider), typeof(Harpoon) }).GetComponent<Harpoon>();
                harpoonPrefab.gameObject.SetActive(false);
                harpoonPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro).projectile.soundHolder;
                harpoonPrefab.Setup();
                UnityEngine.Object.DontDestroyOnLoad(harpoonPrefab);
            }

            // Create gibs
            InitializeGibs();

            this.gameObject.SetActive(false);
        }

        protected void FixPlayerBubble(ReactionBubble bubble)
        {
            bubble.transform.localPosition = new Vector3(0f, 53f, 0f);
            bubble.SetPosition(bubble.transform.localPosition);
            Traverse bubbleTrav = Traverse.Create(bubble);
            bubble.RestartBubble();
            bubbleTrav.Field("yStart").SetValue(bubble.transform.localPosition.y + 5);
        }

        protected void InitializeGibs()
        {
            this.gibs = new GameObject("WarRigGibs", new Type[] { typeof(Transform), typeof(GibHolder) }).GetComponent<GibHolder>();
            this.gibs.gameObject.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(this.gibs);
            CreateGib("Scrap", new Vector2(397, 8), new Vector2(10, 4), 10f, 4f, new Vector3(-25f, 30f, 0f));
            CreateGib("Scrap2", new Vector2(413, 12), new Vector2(6, 6), 6f, 6f, new Vector3(-14f, 20f, 0f));
            CreateGib("Wheel", new Vector2(427, 13), new Vector2(11, 10), 13.75f, 12.5f, new Vector3(36f, 8f, 0f));
            CreateGib("Scrap3", new Vector2(443, 14), new Vector2(15, 10), 15f, 10f, new Vector3(30f, 28f, 0f));
            CreateGib("Scrap4", new Vector2(462, 8), new Vector2(5, 5), 5f, 5f, new Vector3(-17f, 44f, 0f));
            CreateGib("Scrap5", new Vector2(462, 18), new Vector2(4, 6), 4f, 6f, new Vector3(-40f, 40f, 0f));
            CreateGib("SmokestackScrap", new Vector2(477, 21), new Vector2(5, 16), 5f, 16f, new Vector3(-48f, 55f, 0f));
            CreateGib("SmokestackScrap2", new Vector2(477, 21), new Vector2(5, 16), 5f, 16f, new Vector3(-42f, 55f, 0f));
            CreateGib("BackSmokestackScrap", new Vector2(484, 17), new Vector2(5, 12), 5f, 12f, new Vector3(-1f, 57f, 0f));
            CreateGib("BackSmokestackScrap2", new Vector2(484, 17), new Vector2(5, 12), 5f, 12f, new Vector3(4.5f, 57f, 0f));
            CreateGib("Scrap6", new Vector2(401, 21), new Vector2(4, 4), 4f, 4f, new Vector3(-34f, 32f, 0f));
            CreateGib("Scrap7", new Vector2(398, 31), new Vector2(12, 5), 12f, 5f, new Vector3(25f, 27f, 0f));
            CreateGib("Scrap8", new Vector2(414, 25), new Vector2(6, 6), 6f, 6f, new Vector3(6f, 47f, 0f));
            CreateGib("Scrap9", new Vector2(414, 31), new Vector2(5, 4), 5f, 4f, new Vector3(22f, 47f, 0f));
            CreateGib("Scrap10", new Vector2(428, 34), new Vector2(11, 16), 11f, 16f, new Vector3(-16f, 34f, 0f));
            CreateGib("Scrap11", new Vector2(447, 27), new Vector2(7, 5), 7f, 5f, new Vector3(39f, 28f, 0f));
            CreateGib("Scrap12", new Vector2(462, 28), new Vector2(5, 6), 5f, 6f, new Vector3(22f, 29f, 0f));
            CreateGib("ExhaustScrap", new Vector2(396, 42), new Vector2(13, 6), 13f, 6f, new Vector3(-8f, 49f, 0f));
            CreateGib("Wheel", new Vector2(411, 54), new Vector2(13, 13), 17.5f, 17.5f, new Vector3(-50f, 8f, 0f));
            CreateGib("Wheel2", new Vector2(411, 54), new Vector2(13, 13), 17.5f, 17.5f, new Vector3(-32f, 8f, 0f));
            CreateGib("Wheel3", new Vector2(411, 54), new Vector2(13, 13), 17.5f, 17.5f, new Vector3(1f, 8f, 0f));
            CreateGib("ExhaustScrap2", new Vector2(431, 54), new Vector2(4, 12), 4f, 12f, new Vector3(31f, 38f, 0f));
            CreateGib("BumperScrap", new Vector2(448, 53), new Vector2(10, 22), 10f, 22f, new Vector3(12f, 13f, 0f));
            CreateGib("Scrap13", new Vector2(459, 42), new Vector2(15, 9), 15f, 9f, new Vector3(-15f, 10f, 0f));
            CreateGib("Skull", new Vector2(477, 41), new Vector2(5, 7), 5f, 7f, new Vector3(24f, 15f, 0f));
            CreateGib("Skull2", new Vector2(477, 41), new Vector2(5, 7), 5f, 7f, new Vector3(30f, 16f, 0f));
            CreateGib("Skull3", new Vector2(477, 41), new Vector2(5, 7), 5f, 7f, new Vector3(36f, 20f, 0f));
            CreateGib("Skull4", new Vector2(477, 41), new Vector2(5, 7), 5f, 7f, new Vector3(41f, 16f, 0f));
            CreateGib("Skull5", new Vector2(477, 41), new Vector2(5, 7), 5f, 7f, new Vector3(47f, 14f, 0f));

            // Make sure gibs are on layer 19 since the texture they're using is transparent
            for (int i = 0; i < this.gibs.transform.childCount; ++i)
            {
                gibs.transform.GetChild(i).gameObject.layer = 19;
            }
        }

        protected void CreateGib(string name, Vector2 lowerLeftPixel, Vector2 pixelDimensions, float width, float height, Vector3 localPositionOffset )
        {
            BroMakerUtilities.CreateGibPrefab(name, lowerLeftPixel, pixelDimensions, width, height, new Vector3(0f, 0f, 0f), localPositionOffset, false, DoodadGibsType.Metal, 6, false, BloodColor.None, 1, true, 8, false, false, 3, 1, 5, 1).transform.parent = this.gibs.transform;
        }

        protected override void Start()
        {
            base.Start();
            // Prevent tank from standing on platforms and ladders
            this.platformLayer = this.groundLayer;
            this.ladderLayer = this.groundLayer;
            this.speed = 200f;
            this.waistHeight = 10f;
            this.deadWaistHeight = 10f;
            this.height = 52f;
            this.headHeight = this.height;
            this.standingHeadHeight = this.height;
            this.deadHeadHeight = this.height;
            this.frontHeadHeight = 32f;
            this.halfWidth = 63f;
            this.feetWidth = 58f;
            this.width = 62f;
            this.distanceToFront = 32f;
            this.distanceToBack = 49f;
            this.doRollOnLand = false;
            this.canChimneyFlip = false;
            this.canWallClimb = false;
            this.canTumble = false;
            this.canDuck = false;
            this.canLedgeGrapple = false;
            this.jumpForce = 360;
            this.gunSprite.gameObject.layer = 19;
            this.originalSpecialAmmo = 3;
            this.SpecialAmmo = 3;
            this.bloodColor = BloodColor.None;

            // Make sure gib holder exists
            if ( this.gibs == null )
            {
                InitializeGibs();
            }

            // Default to playerNum 0 so that the vehicle doesn't kill the player before they start riding it
            this.playerNum = 0;

            this.DeactivateGun();
            GameObject platformObject = this.gameObject.FindChildOfName("Platform");
            if ( platformObject != null )
            {
                this.platform = platformObject.GetComponent<BoxCollider>();
                this.platform.center = new Vector3(-9f, 44f, -4.5f);
                this.platform.size = new Vector3(80f, 12f, 64f);
            }
        }

        protected override void Update()
        {
            base.Update();
            if (this.pilotUnitDelay > 0f)
            {
                this.pilotUnitDelay -= this.t;
            }
            if (pilotted)
            {
                // Controls where camera is
                this.pilotUnit.SetXY(base.X, base.Y + 25f);
                this.pilotUnit.row = this.row;
                this.pilotUnit.collumn = this.collumn;
                this.pilotUnit.transform.position = new Vector3(this.pilotUnit.X, this.pilotUnit.Y, 10f);
                if (this.pilotUnit.playerNum < 0)
                {
                    this.pilotUnit.gameObject.SetActive(false);
                }
            }
            if (this.pilotSwitch == null)
            {
                this.pilotSwitch = SwitchesController.CreatePilotMookSwitch(this, new Vector3(0f, 40f, 0f));
            }

            // Crush ground when moving towards player who summoned this vehicle
            if ( !this.reachedStartingPoint || this.keepGoingBeyondTarget )
            {
                if ( this.summonedDirection > 0 )
                {
                    this.right = true;
                    this.CrushGroundWhileMoving(50, 25, crushXRange, crushYRange, unitXRange, unitYRange, crushXOffset, crushYOffset);
                    this.right = false;
                }
                else
                {
                    this.left = true;
                    this.CrushGroundWhileMoving(50, 25, crushXRange, crushYRange, unitXRange, unitYRange, crushXOffset, crushYOffset);
                    this.left = false;
                }
            }
            // Crush ground while moving forward
            else if ( crushDamageCooldown <= 0f )
            {
                float currentSpeed = Mathf.Abs(xI);
                if( this.CrushGroundWhileMoving((int)Mathf.Max(Mathf.Round( currentSpeed / 200f * (crushDamage + ((currentSpeed > 150f ||( this.dashing && currentSpeed > 30f)) ? 5f : 0f))), 1f), 25, crushXRange, crushYRange, unitXRange, unitYRange, crushXOffset, crushYOffset) )
                {
                    if ( currentSpeed < 150f && !(this.dashing && currentSpeed > 30f) )
                    {
                        crushDamageCooldown = 0.05f;
                    }
                }
            }
            else
            {
                crushDamageCooldown -= this.t;
            }

            // Reduce fuel when dashing
            if ( this.dashing )
            {
                // Apply initial boost if enough time has passed since previous dash
                if ( !this.wasdashButton && Time.time - lastDashTime > 1f )
                {
                    this.xI += base.transform.localScale.x * 100f;
                    this.lastDashTime = Time.time;
                }

                this.boostFuel -= this.t * 0.1f;

                // Ran out of fuel
                if ( this.boostFuel <= 0f )
                {
                    this.boostFuel = 0f;
                    this.canDash = false;
                    this.dashing = false;
                    this.wasdashButton = false;
                }
            }

            // Move towards wherever player was when they summoned the war rig
            if ( !this.reachedStartingPoint )
            {
                if ( Tools.FastAbsWithinRange(this.X - this.targetX, 5) )
                {
                    if ( summoner.holdingSpecial )
                    {
                        summoner.GoPastFuriosa();
                    }
                    if ( !this.keepGoingBeyondTarget )
                    {
                        this.xI = 0f;
                    }
                    this.reachedStartingPoint = true;
                    this.groundFriction = originalGroundFriction;
                }
                else if ( Tools.FastAbsWithinRange(this.X - this.targetX, 75f) && !(this.keepGoingBeyondTarget || summoner.holdingSpecial) )
                {
                    this.groundFriction = 5f;
                }
                else
                {
                    this.xI = this.summonedDirection * this.speed * 2f;
                }
            }
            else if ( this.keepGoingBeyondTarget )
            {
                if (Tools.FastAbsWithinRange(this.X - this.secondTargetX, 5))
                {
                    this.xI = 0f;
                    this.keepGoingBeyondTarget = false;
                    this.groundFriction = originalGroundFriction;
                }
                else if (Tools.FastAbsWithinRange(this.X - this.secondTargetX, 75f))
                {
                    this.groundFriction = 5f;
                }
                else
                {
                    this.xI = this.summonedDirection * this.speed * 2f;
                }
            }

            // Handle spawning smoke and flashing red when close to death
            if (this.health > 0 && this.deathCount >= 2)
            {
                this.smokeCounter += this.t;
                if (this.smokeCounter >= 0.1334f)
                {
                    this.smokeCounter -= 0.1334f;
                    EffectsController.CreateBlackPlumeParticle(base.X - 8f + UnityEngine.Random.value * 16f, base.Y + 11f + UnityEngine.Random.value * 2f, 3f, 20f, 0f, 60f, 2f, 1f);
                    EffectsController.CreateSparkShower(base.X - 6f + UnityEngine.Random.value * 12f, base.Y + 11f + UnityEngine.Random.value * 4f, 1, 2f, 100f, this.xI - 20f + UnityEngine.Random.value * 40f, 100f, 0.5f, 1f);
                }
            }
            if (this.deathCount > 2)
            {
                this.deathCountdownCounter += this.t * (1f + Mathf.Clamp(this.deathCountdown / (float)this.deathCountdownExplodeThreshold * 4f, 0f, 4f));
                if (this.deathCountdownCounter >= 0.4667f)
                {
                    this.deathCountdownCounter -= 0.2667f;
                    this.deathCountdown += 1f;
                    EffectsController.CreateBlackPlumeParticle(base.X - 8f + UnityEngine.Random.value * 16f, base.Y + 4f + UnityEngine.Random.value * 2f, 3f, 20f, 0f, 60f, 2f, 1f);
                    if (this.deathCountdown % 2f == 1f)
                    {
                        this.SetHurtMaterial();
                        float num = this.deathCountdown / (float)this.deathCountdownExplodeThreshold * 1f;
                        this.PlaySpecial3Sound(0.2f + 0.2f * num, 0.8f + 2f * num);
                    }
                    else
                    {
                        this.SetUnhurtMaterial();
                    }
                    if (this.deathCountdown >= (float)this.deathCountdownExplodeThreshold)
                    {
                        this.SetUnhurtMaterial();
                        this.Gib(DamageType.OutOfBounds, this.xI, this.yI + 150f);
                    }
                }
            }

            // Run animation loops for all other sprites
            AnimateWarRig();

            // Count down timer for Furiosa hanging out of the window
            if ( this.hangingOutTimer > 0f )
            {
                this.hangingOutTimer -= this.t;
                if ( this.hangingOutTimer <= 0f )
                {
                    this.currentFuriosaState = FuriosaState.GoingIn;
                    this.gunFrame = 4;
                }
            }

            // Switch Weapon Pressed
            if (this.pilotted && switchWeaponKey.IsDown(playerNum))
            {
                StartSwitchingWeapon();
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if ( this.pilotted )
            {
                this.UpdateSpecialIcon();
            }
        }

        public override bool CanBeThrown()
        {
            return false;
        }

        protected override bool CanBeAffectedByWind()
        {
            return false;
        }

        public override bool IsHeavy()
        {
            return true;
        }

        protected override void CheckForTraps(ref float yIT)
        {
        }

        protected override void CheckRescues()
        {
        }

        protected override void CheckDashing()
        {
            if ( this.pilotted )
            {
                base.CheckDashing();
            }
        }

        protected override void CheckInput()
        {
            // Don't accept input unless pilot is present
            if (this.pilotted)
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
            else
            {
                this.ClearAllInput();
            }
        }
        #endregion

        #region Animation
        protected override void ChangeFrame()
        {
            // Don't mess with animations if furiosa is leaning out
            if ( currentFuriosaState == FuriosaState.InVehicle )
            {
                // Animate furiosa in vehicle
                if (this.pilotted)
                {
                    this.sprite.SetLowerLeftPixel(2 * this.spritePixelWidth, this.spritePixelHeight);
                }
                // Animate empty vehicle
                else
                {
                    this.sprite.SetLowerLeftPixel(0, this.spritePixelHeight);
                }
            }
        }

        protected void AnimateWarRig()
        {
            // Animate wheels
            float currentSpeed = Mathf.Abs(this.xI);
            if (currentSpeed > 1f)
            {
                this.wheelsCounter += (currentSpeed * this.t / 66f) * 0.175f * 1.25f;
                if (this.wheelsCounter > 0.03f)
                {
                    this.wheelsCounter -= 0.03f;
                    ++this.wheelsFrame;

                    if (this.wheelsFrame > 6)
                    {
                        this.wheelsFrame = 0;
                    }
                }
            }
            AnimateRunning();

            // Animate long and short smokestacks
            if (this.smokestackFrame == 0 && currentSpeed > 50f)
            {
                if ( this.smokestackCooldown > 0f )
                {
                    this.smokestackCooldown -= (currentSpeed > 100f ? 2 : 1) * this.t;
                }
                
                if ( this.smokestackCooldown <= 0f )
                {
                    // Start smoke puff
                    this.smokestackCooldown = UnityEngine.Random.Range(0.5f, 1.5f);
                    this.smokestackFrame = 1;
                    this.usingBlueFlame = this.dashing;
                    this.longSmokestacksSprite.SetLowerLeftPixel(this.smokestackFrame * this.spritePixelWidth, this.dashing ? 256f : 128f);
                    this.shortSmokestacksSprite.SetLowerLeftPixel(this.smokestackFrame * this.spritePixelWidth, this.dashing ? 256f : 128f);
                }
            }
            else if ( this.smokestackFrame > 0 )
            {
                this.smokestackCounter += this.t;
                if (this.smokestackCounter > 0.08f)
                {
                    this.smokestackCounter -= 0.08f;
                    ++this.smokestackFrame;

                    if (this.smokestackFrame > 7)
                    {
                        this.smokestackFrame = 0;
                        this.longSmokestacksSprite.SetLowerLeftPixel(this.smokestackFrame * this.spritePixelWidth, 128f);
                        this.shortSmokestacksSprite.SetLowerLeftPixel(this.smokestackFrame * this.spritePixelWidth, 128f);
                    }
                    else
                    {
                        this.longSmokestacksSprite.SetLowerLeftPixel(this.smokestackFrame * this.spritePixelWidth, this.dashing ? 256f : 128f);
                        this.shortSmokestacksSprite.SetLowerLeftPixel(this.smokestackFrame * this.spritePixelWidth, this.dashing ? 256f : 128f);
                    }
                    
                }
            }
            else
            {
                this.longSmokestacksSprite.SetLowerLeftPixel(this.smokestackFrame * this.spritePixelWidth, 128f);
                this.shortSmokestacksSprite.SetLowerLeftPixel(this.smokestackFrame * this.spritePixelWidth, 128f);
            }

            // Animate front smokestacks
            if (this.blueSmokeFrame == 0 && this.dashing)
            {
                if (this.blueSmokeCooldown > 0f)
                {
                    this.blueSmokeCooldown -= this.t;
                }

                if (this.blueSmokeCooldown <= 0f)
                {
                    // Start smoke puff
                    this.blueSmokeCooldown = UnityEngine.Random.Range(0.25f, 0.6f);
                    this.blueSmokeFrame = 1;
                    this.frontSmokestacksSprite.SetLowerLeftPixel(this.blueSmokeFrame * this.spritePixelWidth, 256f);
                }
            }
            else if (this.blueSmokeFrame > 0)
            {
                this.blueSmokeCounter += this.t;
                if (this.blueSmokeCounter > 0.08f)
                {
                    this.blueSmokeCounter -= 0.08f;
                    ++this.blueSmokeFrame;

                    if (this.blueSmokeFrame > 3)
                    {
                        this.blueSmokeFrame = 0;
                        this.frontSmokestacksSprite.SetLowerLeftPixel(0f, 128f);
                    }
                    else
                    {
                        this.frontSmokestacksSprite.SetLowerLeftPixel(this.blueSmokeFrame * this.spritePixelWidth, 256f);
                    }
                }
            }
            else
            {
                if (this.blueSmokeCooldown > 0f)
                {
                    this.blueSmokeCooldown -= this.t;
                }

                this.frontSmokestacksSprite.SetLowerLeftPixel(0f, 128f);
            }

            // Animate special
            if ( this.usingSpecial )
            {
                this.specialCounter += this.t;
                if ( this.specialCounter > 0.12f )
                {
                    this.specialCounter -= 0.12f;
                    ++this.specialFrame;

                    if ( this.specialFrame < 3 )
                    {
                        this.bumperSprite.SetLowerLeftPixel(this.specialFrame * this.spritePixelWidth, 2 * this.spritePixelHeight);
                    }
                    else
                    {
                        if ( this.specialFrame == 6 )
                        {
                            this.UseSpecial();
                        }
                        else if ( this.specialFrame == 7)
                        {
                            this.usingSpecial = false;
                            this.specialFrame = 3;
                        }
                        // Pause for a few frames before firing
                        else
                        {
                            this.bumperSprite.SetLowerLeftPixel(3 * this.spritePixelWidth, 2 * this.spritePixelHeight);
                        }
                    }
                }
            }
            // Close bumper after firing
            else if ( this.specialFrame > 0 )
            {
                this.specialCounter += this.t;
                if (this.specialCounter > 0.13f)
                {
                    this.specialCounter -= 0.13f;
                    --this.specialFrame;

                    if ( specialFrame != 0 )
                    {
                        this.bumperSprite.SetLowerLeftPixel(this.specialFrame * this.spritePixelWidth, 2 * this.spritePixelHeight);
                    }
                    else
                    {
                        this.bumperSprite.SetLowerLeftPixel(0f, this.spritePixelHeight);
                    }
                }
            }
        }

        protected override void AnimateRunning()
        {
            this.wheelsSprite.SetLowerLeftPixel(wheelsFrame * this.spritePixelWidth, this.spritePixelHeight * 2);
        }

        protected override void AnimateWallAnticipation()
        {
        }

        protected override void AnimateImpaled()
        {
        }

        protected override void RunAvatarRunning()
        {
        }

        protected override void ActivateGun()
        {
        }

        protected override void CreateGibs(float xI, float yI)
        {
            xI = xI * 0.25f;
            yI = yI * 0.25f + 60f;
            float xForce = 10f;
            float yForce = 10f;
            if (gibs == null || gibs.transform == null)
            {
                return;
            }
            for (int i = 0; i < gibs.transform.childCount; i++)
            {
                Transform child = gibs.transform.GetChild(i);
                if (child != null)
                {
                    EffectsController.CreateGib(child.GetComponent<Gib>(), base.GetComponent<Renderer>().sharedMaterial, base.X, base.Y, xForce * (0.8f + UnityEngine.Random.value * 0.4f), yForce * (0.8f + UnityEngine.Random.value * 0.4f), xI, yI, (int)base.transform.localScale.x);
                }
            }
        }
        #endregion

        #region Movement
        // Overridden to prevent vehicle from being teleported around like a player
        protected override void RunMovement()
        {
            this.CalculateGroundHeight();
            this.CheckForQuicksand();
            if (base.actionState == ActionState.Dead)
            {
                if (this.isInQuicksand)
                {
                    this.xI *= 1f - this.t * 20f;
                    this.xI = Mathf.Clamp(this.xI, -16f, 16f);
                    this.xIBlast *= 1f - this.t * 20f;
                }
                this.RunDeadGravity();
                if (!(this.impaledByTransform == null))
                {
                    this.RunImpaledBlood();
                    this.xI = 0f;
                    this.xIBlast = 0f;
                    this.yIBlast = 0f;
                    if (!this.impaledByTransform.gameObject.activeSelf)
                    {
                        this.impaledByTransform = null;
                    }
                }
            }
            else if (this.IsOverFinish(ref this.ladderX))
            {
                base.actionState = ActionState.ClimbingLadder;
                this.yI = 0f;
                this.StopAirDashing();
            }
            else if (this.attachedToZipline != null)
            {
                if (this.down && !this.wasDown)
                {
                    this.attachedToZipline.DetachUnit(this);
                }
                if (this.buttonJump && !this.wasButtonJump)
                {
                    this.attachedToZipline.DetachUnit(this);
                    this.yI = this.jumpForce;
                }
            }
            else if (base.actionState != ActionState.ClimbingLadder && (this.up || (this.down && !this.IsGroundBelow())) && (!this.canDash || this.airdashTime <= 0f) && this.IsOverLadder(ref this.ladderX))
            {
                base.actionState = ActionState.ClimbingLadder;
                this.yI = 0f;
                this.StopAirDashing();
            }
            else if (base.actionState == ActionState.ClimbingLadder)
            {
                this.RunClimbingLadder();
            }
            else if (this.doingMelee)
            {
                if (this.isInQuicksand)
                {
                    this.xI *= 1f - this.t * 20f;
                    this.xI = Mathf.Clamp(this.xI, -2f, 2f);
                    this.xIBlast *= 1f - this.t * 20f;
                }
                this.RunMelee();
            }
            else if (base.actionState == ActionState.Hanging)
            {
                this.RunHanging();
            }
            else if (this.canAirdash && this.airdashTime > 0f)
            {
                this.RunAirDashing();
            }
            else if (base.actionState == ActionState.Jumping)
            {
                if (this.isInQuicksand)
                {
                    this.xI *= 1f - this.t * 20f;
                    this.xI = Mathf.Clamp(this.xI, -16f, 16f);
                    this.xIBlast *= 1f - this.t * 20f;
                }
                if (this.jumpTime > 0f)
                {
                    this.jumpTime -= this.t;
                    if (!this.buttonJump)
                    {
                        this.jumpTime = 0f;
                    }
                }
                if (!(this.impaledByTransform != null))
                {
                    if (this.wallClimbing)
                    {
                        this.ApplyWallClimbingGravity();
                    }
                    else if (this.yI > 40f)
                    {
                        this.ApplyFallingGravity();
                    }
                    else
                    {
                        this.ApplyFallingGravity();
                    }
                }
                if (this.yI < this.maxFallSpeed)
                {
                    this.yI = this.maxFallSpeed;
                }
                if (this.yI < -50f)
                {
                    this.RunFalling();
                }
                if (this.canCeilingHang && this.hangGrace > 0f)
                {
                    this.RunCheckHanging();
                }
            }
            else
            {
                if (base.actionState == ActionState.Fallen)
                {
                    this.RunFallen();
                }
                this.EvaluateIsJumping();
            }
            this.yIT = (this.yI + this.specialAttackYIBoost) * this.t;
            if (FluidController.IsSubmerged(base.X, base.Y))
            {
                this.yIT *= 0.65f;
            }
            if (base.actionState != ActionState.Recalling)
            {
                if (this.health > 0 && base.playerNum >= 0 && base.playerNum <= 3)
                {
                    this.ConstrainSpeedToSidesOfScreen();
                }
                this.canTouchCeiling = this.ConstrainToCeiling(ref this.yIT);
                if (FluidController.IsSubmerged(base.X, base.Y))
                {
                    this.xI *= 0.95f;
                    this.xIBlast *= 0.95f;
                }
                this.xIT = (this.xI + this.xIBlast + this.xIAttackExtra + this.specialAttackXIBoost) * this.t;
                this.ConstrainToWalls(ref this.yIT, ref this.xIT);
                if (this.skinnedMookOnMyBack)
                {
                    this.xIT *= 0.95f;
                }
                base.X += this.xIT;
                this.CheckClimbAlongCeiling();
                this.CheckForTraps(ref this.yIT);
                if (this.yI <= 0f)
                {
                    this.ConstrainToFloor(ref this.yIT);
                }
            }
            else
            {
                this.invulnerable = true;
                this.yI = 0f;
                this.yIT = this.yI * this.t;
                this.xI = 0f;
            }
            if (this.WallDrag && (this.parentHasMovedTime > 0f || this.fire))
            {
                this.wallDragTime = 0.25f;
            }
            base.Y += this.yIT;
            if (!this.immuneToOutOfBounds)
            {
                bool flag = GameModeController.IsDeathMatchMode || GameModeController.GameMode == GameMode.BroDown;
                bool flag2 = flag && base.IsHero && (SortOfFollow.IsZooming || !HeroController.isCountdownFinished);
                bool flag3 = flag && base.Y < this.screenMinY - 55f && base.playerNum >= 0;
                if (!flag2 && (base.Y < -44f || flag3))
                {
                    if (Map.isEditing)
                    {
                        base.Y = -20f;
                        this.yI = -this.yI * 1.5f;
                    }
                    else
                    {
                        float x = base.X;
                        float y = base.Y;
                        if (base.IsHero && Map.lastYLoadOffset > 0 && Map.FindLadderNearPosition((this.screenMaxX + this.screenMinX) / 2f, this.screenMinY, 16, ref x, ref y))
                        {
                            this.SetXY(x, y);
                            this.holdUpTime = 0.3f;
                            this.yI = 150f;
                            this.xI = 0f;
                            this.ShowStartBubble();
                        }
                        else if (!base.IsHero || base.IsMine)
                        {
                            if (HeroControllerTestInfo.HerosAreInvulnerable && base.IsHero)
                            {
                                this.yI += 1000f;
                            }
                            else
                            {
                                this.Gib(DamageType.OutOfBounds, this.xI, 840f);
                            }
                        }
                    }
                }
            }
            this.RunGroundFriction();
            if (base.Y > this.groundHeight)
            {
                this.RunAirFriction();
            }
            if (float.IsNaN(base.X))
            {
            }
            this.SetPosition();
        }

        // Overridden to prevent vehicle from being teleported around like a player
        protected new void ConstrainSpeedToSidesOfScreen()
        {
            if (base.X >= this.screenMaxX - 8f && (this.xI > 0f || this.xIBlast > 0f))
            {
                this.xI = (this.xIBlast = 0f);
            }
            if (base.X <= this.screenMinX + 8f && (this.xI < 0f || this.xIBlast < 0f))
            {
                this.xI = (this.xIBlast = 0f);
            }
            if (base.X < this.screenMinX - 30f && TriggerManager.DestroyOffscreenPlayers && base.IsMine)
            {
                this.Gib(DamageType.OutOfBounds, 840f, this.yI + 50f);
            }
            if (SortOfFollow.GetFollowMode() == CameraFollowMode.MapExtents && base.Y >= this.screenMaxY - this.headHeight && this.yI > 0f)
            {
                base.Y = this.screenMaxY - this.headHeight;
                this.yI = 0f;
            }
            if (base.Y < this.screenMinY - 30f)
            {
                if (TriggerManager.DestroyOffscreenPlayers && base.IsMine)
                {
                    this.Gib(DamageType.OutOfBounds, this.xI, 840f);
                }
                this.belowScreenCounter += this.t;
                if (this.isHero && this.belowScreenCounter > 2f && HeroController.CanLookForReposition())
                {
                    float x = base.X;
                    float y = base.Y;
                    if (Map.FindLadderNearPosition((this.screenMaxX + this.screenMinX) / 2f, this.screenMinY, ref x, ref y))
                    {
                        this.SetXY(x, y);
                        this.holdUpTime = 0.3f;
                        this.yI = 150f;
                        this.xI = 0f;
                        this.ShowStartBubble();
                        if (!GameModeController.IsDeathMatchMode && GameModeController.GameMode != GameMode.BroDown)
                        {
                            this.SetInvulnerable(2f, false, false);
                        }
                    }
                    int num = 1;
                    if (Map.FindHoleToJumpThroughAndAppear((this.screenMaxX + this.screenMinX) / 2f, this.screenMinY, ref x, ref y, ref num))
                    {
                        this.SetXY(x, y);
                        if (num > 0)
                        {
                            this.holdRightTime = 0.3f;
                        }
                        else
                        {
                            this.holdLeftTime = 0.3f;
                        }
                        this.yI = 240f;
                        this.xI = (float)(num * 70);
                        this.ShowStartBubble();
                        if (!GameModeController.IsDeathMatchMode && GameModeController.GameMode != GameMode.BroDown)
                        {
                            this.SetInvulnerable(2f, false, false);
                        }
                    }
                    this.belowScreenCounter -= 0.5f;
                }
            }
            else
            {
                this.belowScreenCounter = 0f;
            }
        }

        // Rewritten completely to support larger hitboxes
        protected override bool ConstrainToCeiling(ref float yIT)
        {
            // Disable collision until we've reached start
            if ( !this.reachedStartingPoint )
            {
                return false;
            }
            if (base.actionState == ActionState.Dead)
            {
                this.headHeight = this.deadHeadHeight;
                this.waistHeight = this.deadWaistHeight;
            }
            bool result = false;
            this.chimneyFlipConstrained = false;
            if (this.yI >= 0f || this.WallDrag)
            {
                if ( base.transform.localScale.x > 0 )
                {
                    // Check top middle of vehicle left to right
                    Vector3 topLeft = new Vector3(base.X - distanceToBack, base.Y + this.headHeight, 0f);
                    Vector3 topRight = new Vector3(base.X + distanceToFront, base.Y + this.headHeight, 0f);
                    //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                    if ( Physics.Raycast(topLeft, Vector3.right, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT )
                    {
                        result = true;
                        this.HitCeiling(this.raycastHit);
                    }

                    if ( !result )
                    {
                        // Check top middle of vehicle right to left
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                        if (Physics.Raycast(topRight, Vector3.left, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit);
                        }
                    }
                    
                    // Check front of vehicle left to right
                    if ( !result )
                    {
                        topLeft = new Vector3(base.X + distanceToFront, base.Y + frontHeadHeight, 0f);
                        topRight = new Vector3(base.X + halfWidth - 1f, base.Y + frontHeadHeight, 0f);
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if (Physics.Raycast(topLeft, Vector3.right, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.frontHeadHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit, frontHeadHeight);
                        }
                    }

                    // Check front of vehicle right to left
                    if (!result)
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if (Physics.Raycast(topRight, Vector3.left, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.frontHeadHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit, frontHeadHeight);
                        }
                    }
                }
                else
                {
                    // Check top middle of vehicle left to right
                    Vector3 topLeft = new Vector3(base.X - distanceToFront, base.Y + this.headHeight, 0f);
                    Vector3 topRight = new Vector3(base.X + distanceToBack, base.Y + this.headHeight, 0f);
                    //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                    if (Physics.Raycast(topLeft, Vector3.right, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                    {
                        result = true;
                        this.HitCeiling(this.raycastHit);
                    }

                    if (!result)
                    {
                        // Check top middle of vehicle right to left
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                        if (Physics.Raycast(topRight, Vector3.left, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit);
                        }
                    }

                    // Check front of vehicle left to right
                    if (!result)
                    {
                        topLeft = new Vector3(base.X - halfWidth + 1f, base.Y + frontHeadHeight, 0f);
                        topRight = new Vector3(base.X - distanceToFront, base.Y + frontHeadHeight, 0f);
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if (Physics.Raycast(topLeft, Vector3.right, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.frontHeadHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit, frontHeadHeight);
                        }
                    }

                    // Check front of vehicle right to left
                    if (!result)
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if (Physics.Raycast(topRight, Vector3.left, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.frontHeadHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit, frontHeadHeight);
                        }
                    }
                }
            }
            return result;
        }

        protected void HitCeiling(RaycastHit ceilingHit, float customHeight)
        {
            if (this.up || this.buttonJump)
            {
                ceilingHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
            }
            this.yIT = ceilingHit.point.y - customHeight - base.Y;
            if (!this.chimneyFlip && this.yI > 100f && ceilingHit.collider != null)
            {
                this.currentFootStepGroundType = ceilingHit.collider.tag;
                this.PlayFootStepSound(0.2f, 0.6f);
            }
            this.yI = 0f;
            this.jumpTime = 0f;
            if ((this.canCeilingHang && this.CanCheckClimbAlongCeiling() && (this.up || this.buttonJump)) || this.hangGrace > 0f)
            {
                this.StartHanging();
            }

            //RocketLib.Utils.DrawDebug.DrawLine("ceillingHit", ceilingHit.point, ceilingHit.point + new Vector3(5f, 0f, 0f), Color.green);
        }

        protected override void HitCeiling(RaycastHit ceilingHit)
        {
            base.HitCeiling(ceilingHit);

            //RocketLib.Utils.DrawDebug.DrawLine("ceillingHit", ceilingHit.point, ceilingHit.point + new Vector3(3f, 0f, 0f), Color.green);
        }

        // Rewritten completely to support larger hitboxes
        protected override bool ConstrainToWalls(ref float yIT, ref float xIT)
        {
            // Disable collision until we've reached start
            if (!this.reachedStartingPoint)
            {
                return false;
            }
            if (!this.dashing || (this.left && this.xIBlast > 0f) || (this.right && this.xIBlast < 0f) || (!this.left && !this.right && Mathf.Abs(this.xIBlast) > 0f))
            {
                this.xIBlast *= 1f - this.t * 4f;
            }
            this.pushingTime -= this.t;
            this.canTouchRightWalls = false;
            this.canTouchLeftWalls = false;
            this.wasConstrainedLeft = this.constrainedLeft;
            this.wasConstrainedRight = this.constrainedRight;
            this.constrainedLeft = false;
            this.constrainedRight = false;
            this.ConstrainToFragileBarriers(ref xIT, this.halfWidth);
            this.ConstrainToMookBarriers(ref xIT, this.halfWidth);
            this.row = (int)((base.Y + 16f) / 16f);
            this.collumn = (int)((base.X + 8f) / 16f);
            this.wasLedgeGrapple = this.ledgeGrapple;
            this.ledgeGrapple = false;

            if (base.transform.localScale.x > 0)
            {
                // Check front of vehicle
                Vector3 bottomRight = new Vector3(base.X + this.halfWidth, base.Y, 0);
                Vector3 topRight = new Vector3(base.X + this.halfWidth, base.Y + this.frontHeadHeight, 0);
                //RocketLib.Utils.DrawDebug.DrawLine("wall", bottomRight, topRight, Color.red);
                if (Physics.Raycast(bottomRight, Vector3.up, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x - 2f, raycastHitWalls.point.y, 0f), Vector3.right, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X + this.halfWidth);
                        return true;
                    }
                }

                if (Physics.Raycast(topRight, Vector3.down, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y) - 0.5f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x - 2f, raycastHitWalls.point.y, 0f), Vector3.right, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X + this.halfWidth);
                        return true;
                    }
                }

                // Check top front of vehicle
                bottomRight = new Vector3(base.X + this.distanceToFront, base.Y + this.frontHeadHeight, 0);
                topRight = new Vector3(base.X + this.distanceToFront, base.Y + this.headHeight, 0);
                //RocketLib.Utils.DrawDebug.DrawLine("wall2", bottomRight, topRight, Color.red);
                if (Physics.Raycast(bottomRight, Vector3.up, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.distanceToFront + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x - 2f, raycastHitWalls.point.y, 0f), Vector3.right, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.distanceToFront + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X + (this.halfWidth - distanceToFront));
                        return true;
                    }
                }

                if (Physics.Raycast(topRight, Vector3.down, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.distanceToFront + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x - 2f, raycastHitWalls.point.y, 0f), Vector3.right, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.distanceToFront + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X + (this.halfWidth - distanceToFront));
                        return true;
                    }
                }
            }
            else
            {
                // Check front of vehicle
                Vector3 bottomRight = new Vector3(base.X - this.halfWidth, base.Y, 0);
                Vector3 topRight = new Vector3(base.X - this.halfWidth, base.Y + this.frontHeadHeight, 0);
                //RocketLib.Utils.DrawDebug.DrawLine("wall", bottomRight, topRight, Color.red);
                if (Physics.Raycast(bottomRight, Vector3.up, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x + 2f, raycastHitWalls.point.y, 0f), Vector3.left, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X - this.halfWidth);
                        return true;
                    }
                }

                if (Physics.Raycast(topRight, Vector3.down, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y) - 0.5f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x + 2f, raycastHitWalls.point.y, 0f), Vector3.left, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X - this.halfWidth);
                        return true;
                    }
                }

                // Check top front of vehicle
                bottomRight = new Vector3(base.X - this.distanceToFront, base.Y + this.frontHeadHeight, 0);
                topRight = new Vector3(base.X - this.distanceToFront, base.Y + this.headHeight, 0);
                //RocketLib.Utils.DrawDebug.DrawLine("wall2", bottomRight, topRight, Color.red);
                if (Physics.Raycast(bottomRight, Vector3.up, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.distanceToFront + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x + 2f, raycastHitWalls.point.y, 0f), Vector3.left, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.distanceToFront + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X - (this.halfWidth - distanceToFront));
                        return true;
                    }
                }

                if (Physics.Raycast(topRight, Vector3.down, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.distanceToFront + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x + 2f, raycastHitWalls.point.y, 0f), Vector3.left, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.distanceToFront + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X - (this.halfWidth - distanceToFront));
                        return true;
                    }
                }
            }


            return false;
        }

        protected override void AddSpeedLeft()
        {
            if (this.holdStillTime <= 0f)
            {
                if (this.xI > -25f)
                {
                    this.xI = -25f;
                }
                this.xI -= this.speed * (this.dashing ? 1f : 0.5f) * this.t;
            }
            if (this.xI < -((!this.dashing || this.ducking) ? this.GetSpeed : (this.GetSpeed * this.dashSpeedM)))
            {
                this.xI = -((!this.dashing || this.ducking) ? this.GetSpeed : (this.GetSpeed * this.dashSpeedM));
            }
            else if (this.xI > -50f && this.holdStillTime <= 0f)
            {
                this.xI -= this.speed * 2.6f * this.t * ((!this.IsParachuteActive) ? 1f : 0.5f);
            }
        }

        protected override void AddSpeedRight()
        {
            if (this.holdStillTime <= 0f)
            {
                if (this.xI < 25f)
                {
                    this.xI = 25f;
                }
                this.xI += this.speed * (this.dashing ? 1f : 0.5f) * this.t;
            }
            if (this.xI > ((!this.dashing || this.ducking) ? this.GetSpeed : (this.GetSpeed * this.dashSpeedM)))
            {
                this.xI = ((!this.dashing || this.ducking) ? this.GetSpeed : (this.GetSpeed * this.dashSpeedM));
            }
            else if (this.xI < 50f && this.holdStillTime <= 0f)
            {
                this.xI += this.speed * 2.6f * this.t * ((!this.IsParachuteActive) ? 1f : 0.5f);
            }
        }

        protected override void Jump(bool wallJump)
        {
            if (!this.wasButtonJump || this.pressedJumpInAirSoJumpIfTouchGroundGrace > 0f)
            {
            }
            if (this.canAirdash && (this.canTouchLeftWalls || this.canTouchRightWalls || !wallJump))
            {
                this.SetAirdashAvailable();
            }
            this.lastJumpTime = Time.time;
            base.actionState = ActionState.Jumping;
            if (this.blockCurrentlyStandingOn != null && this.blockCurrentlyStandingOn.IsBouncy)
            {
                this.blockCurrentlyStandingOn.BounceOn();
            }
            if (this.blockCurrentlyStandingOn != null && this.IsSurroundedByBarbedWire())
            {
                this.yI = this.jumpForce * 0.8f;
                EffectsController.CreateBloodParticles(this.bloodColor, base.X, base.Y + 10f, -5f, 3, 4f, 4f, 50f, this.xI * 0.8f, this.yI);
                this.barbedWireWithin.PlayCutSound();
            }
            else if (Physics.Raycast(new Vector3(base.X, base.Y + 2f, 0f), Vector3.down, out this.raycastHit, 4f, Map.groundLayer) && this.raycastHit.collider.GetComponent<BossBlockPiece>() != null)
            {
                BossBlockPiece component = this.raycastHit.collider.GetComponent<BossBlockPiece>();
                if (component.isBouncy)
                {
                    this.yI = this.jumpForce * 1.9f;
                    component.BounceOn();
                }
                else
                {
                    this.yI = this.jumpForce;
                }
            }
            else
            {
                this.yI = this.jumpForce;
            }
            this.xIBlast += this.parentedDiff.x / this.t;
            float value = this.parentedDiff.y / this.t;
            this.yI += Mathf.Clamp(value, -100f, 400f);
            this.doubleJumpsLeft = 0;
            this.wallClimbAnticipation = false;
            if (wallJump)
            {
                this.jumpTime = 0f;
                this.xI = 0f;
                if (this.useNewKnifeClimbingFrames)
                {
                    base.frame = 0;
                    this.lastKnifeClimbStabY = base.Y + this.knifeClimbStabHeight;
                }
                else
                {
                    this.knifeHand++;
                }
                RaycastHit raycastHit;
                if (this.left && Physics.Raycast(new Vector3(base.X, base.Y + this.headHeight, 0f), Vector3.left, out raycastHit, 10f, this.groundLayer))
                {
                    raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    if (this.useNewKnifeClimbingFrames)
                    {
                        this.AnimateWallAnticipation();
                    }
                }
                else if (this.right && Physics.Raycast(new Vector3(base.X, base.Y + this.headHeight, 0f), Vector3.right, out raycastHit, 10f, this.groundLayer))
                {
                    raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    if (this.useNewKnifeClimbingFrames)
                    {
                        this.AnimateWallAnticipation();
                    }
                }
                this.PlayClimbSound();
            }
            else
            {
                if (!this.IsSurroundedByBarbedWire())
                {
                    this.jumpTime = this.JUMP_TIME;
                }
                this.ChangeFrame();
                this.PlayJumpSound();
            }
            if (!wallJump && this.groundHeight - base.Y > -2f)
            {
                EffectsController.CreateJumpPoofEffect(base.X, base.Y, (Mathf.Abs(this.xI) >= 30f) ? (-(int)base.transform.localScale.x) : 0, this.GetFootPoofColor());
            }
            this.airDashJumpGrace = 0f;
        }

        protected override bool CanJumpOffGround()
        {
            return this.CanTouchGround((float)((!this.right || this.canTouchLeftWalls || Physics.Raycast(new Vector3(base.X, base.Y + 5f, 0f), Vector3.left, out this.raycastHitWalls, 13.5f, this.groundLayer)) ? 0 : -13) + (float)((!this.left || this.canTouchRightWalls || Physics.Raycast(new Vector3(base.X, base.Y + 5f, 0f), Vector3.right, out this.raycastHitWalls, 13.5f, this.groundLayer)) ? 0 : 13) * ((!this.isInQuicksand) ? 1f : 0.4f));
        }

        protected new bool CanTouchGround(float xOffset)
        {
            LayerMask mask = this.GetGroundLayer();
            RaycastHit raycastHit;
            if (Physics.Raycast(new Vector3(base.X, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, mask))
            {
                this.SetCurrentFootstepSound(raycastHit.collider);
                return true;
            }
            if (Physics.Raycast(new Vector3(base.X - feetWidth, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, mask))
            {
                this.SetCurrentFootstepSound(raycastHit.collider);
                return true;
            }
            if (Physics.Raycast(new Vector3(base.X + feetWidth, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, mask))
            {
                this.SetCurrentFootstepSound(raycastHit.collider);
                return true;
            }
            if (xOffset != 0f && Physics.Raycast(new Vector3(base.X + xOffset, base.Y + 12f, 0f), Vector3.down, out raycastHit, 15f, mask))
            {
                this.SetCurrentFootstepSound(raycastHit.collider);
                return true;
            }
            if (!Map.IsBlockLadder(base.X, base.Y) && !this.down)
            {
                if (Physics.Raycast(new Vector3(base.X, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, this.ladderLayer))
                {
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    return true;
                }
                if (!Map.IsBlockLadder(base.X - 3f, base.Y) && !this.down && Physics.Raycast(new Vector3(base.X - 3f, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, this.ladderLayer))
                {
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    return true;
                }
                if (!Map.IsBlockLadder(base.X, base.Y) && !this.down && Physics.Raycast(new Vector3(base.X - 3f, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, this.ladderLayer))
                {
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    return true;
                }
                if (xOffset != 0f && !Map.IsBlockLadder(base.X + xOffset, base.Y) && !this.down && Physics.Raycast(new Vector3(base.X + xOffset, base.Y + 12f, 0f), Vector3.down, out raycastHit, 15f, this.ladderLayer))
                {
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    return true;
                }
            }
            return false;
        }

        protected override void Land()
        {
            if ((!this.isHero && this.yI < this.fallDamageHurtSpeed) || (this.isHero && this.yI < this.fallDamageHurtSpeedHero))
            {
                if ((!this.isHero && this.yI < this.fallDamageDeathSpeed) || (this.isHero && this.yI < this.fallDamageDeathSpeedHero))
                {
                    this.crushingGroundLayers = 2;
                    SortOfFollow.Shake(0.3f);
                    EffectsController.CreateGroundWave(base.X, base.Y, 96f);
                    Map.ShakeTrees(base.X, base.Y, 144f, 64f, 128f);
                }
                else
                {
                    if (this.isHero)
                    {
                        if (this.yI <= this.fallDamageDeathSpeedHero)
                        {
                            this.crushingGroundLayers = 2;
                        }
                        else if (this.yI < this.fallDamageHurtSpeedHero * 0.3f + this.fallDamageDeathSpeedHero * 0.7f)
                        {
                            this.crushingGroundLayers = 1;
                        }
                        else if (this.yI < (this.fallDamageHurtSpeedHero + this.fallDamageDeathSpeedHero) / 2f)
                        {
                            this.crushingGroundLayers = 0;
                        }
                    }
                    else if (this.yI < (this.fallDamageHurtSpeed + this.fallDamageDeathSpeed) / 2f)
                    {
                        this.crushingGroundLayers = 2;
                    }
                    SortOfFollow.Shake(0.3f);
                    EffectsController.CreateGroundWave(base.X, base.Y, 80f);
                    Map.ShakeTrees(base.X, base.Y, 144f, 64f, 128f);
                }
            }
            else if (this.crushingGroundLayers > 0)
            {
                this.crushingGroundLayers--;
                SortOfFollow.Shake(0.3f);
                Map.ShakeTrees(base.X, base.Y, 80f, 48f, 100f);
            }
            else if (this.yI < -60f && this.health > 0)
            {
                this.PlayFootStepSound(this.soundHolderFootSteps.landMetalSounds, 0.55f, 0.9f);
                this.gunFrame = 0;
                this.gunCounter = 0f;
                EffectsController.CreateGroundWave(base.X, base.Y + 10f, 64f);
                SortOfFollow.Shake(0.2f);
            }
            else if (this.health > 0)
            {
                this.PlayFootStepSound(this.soundHolderFootSteps.landMetalSounds, 0.35f, 0.9f);
                SortOfFollow.Shake(0.1f);
                this.gunFrame = 0;
                this.gunCounter = 0f;
            }
            this.jumpingMelee = false;
            this.timesKickedByVanDammeSinceLanding = 0;
            if (this.health > 0 && base.playerNum >= 0 && this.yI < -150f)
            {
                Map.BotherNearbyMooks(base.X, base.Y, 24f, 16f, base.playerNum);
            }
            this.FallDamage(this.yI);
            this.StopAirDashing();
            this.lastLandTime = Time.realtimeSinceStartup;
            if (this.yI < 0f && this.health > 0 && this.groundHeight > base.Y - 2f + this.yIT && this.yI < -70f)
            {
                EffectsController.CreateLandPoofEffect(base.X, this.groundHeight, (Mathf.Abs(this.xI) >= 30f) ? (-(int)base.transform.localScale.x) : 0, this.GetFootPoofColor());
            }
            if (this.health > 0)
            {
                if ((this.left || this.right) && (!this.left || !this.right))
                {
                    if (this.xI > 0f)
                    {
                        this.xI += 100f;
                    }
                    if (this.xI < 0f)
                    {
                        this.xI -= 100f;
                    }
                    base.actionState = ActionState.Running;
                    if (this.delayedDashing || (this.dashing && Time.time - this.leftTapTime > this.minDashTapTime && Time.time - this.rightTapTime > this.minDashTapTime))
                    {
                        this.StartDashing();
                    }
                    this.hasDashedInAir = false;
                    if (this.useNewFrames)
                    {
                        if (this.CanDoRollOnLand())
                        {
                            this.RollOnLand();
                        }
                        this.counter = 0f;
                        this.AnimateRunning();
                        if (!FluidController.IsSubmerged(this) && this.groundHeight > base.Y - 8f)
                        {
                            EffectsController.CreateFootPoofEffect(base.X, this.groundHeight + 1f, 0f, Vector3.up * 1f, BloodColor.None);
                        }
                    }
                }
                else
                {
                    this.StopRolling();
                    this.SetActionstateToIdle();
                }
            }
            if (this.yI < -50f)
            {
                if (Physics.Raycast(new Vector3(base.X, base.Y + 5f, 0f), Vector3.down, out this.raycastHit, 12f, this.groundLayer | Map.platformLayer))
                {
                    this.raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                }
                if (Physics.Raycast(new Vector3(base.X + 6f, base.Y + 5f, 0f), Vector3.down, out this.raycastHit, 12f, this.groundLayer | Map.platformLayer))
                {
                    this.raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                }
                if (Physics.Raycast(new Vector3(base.X - 6f, base.Y + 5f, 0f), Vector3.down, out this.raycastHit, 12f, this.groundLayer | Map.platformLayer))
                {
                    this.raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                }
            }
            bool flag = false;
            if (base.playerNum >= 0 && this.yI < -100f)
            {
                flag = this.PushGrassAway();
            }
            if (this.health > 0 && !flag && this.yI < -100f)
            {
                this.PlayLandSound();
            }
            if (this.bossBlockPieceCurrentlyStandingOn != null)
            {
                this.bossBlockPieceCurrentlyStandingOn.LandOn(this.yI);
            }
            if (this.blockCurrentlyStandingOn != null)
            {
                this.blockCurrentlyStandingOn.LandOn(this.yI);
            }
            this.yI = 0f;
            if (this.groundTransform != null)
            {
                this.lastParentedToTransform = this.groundTransform;
            }
            if (this.IsParachuteActive)
            {
                this.IsParachuteActive = false;
            }
        }

        protected override void FallDamage(float yI)
        {
            if ((!this.isHero && yI < this.fallDamageHurtSpeed) || (this.isHero && yI < this.fallDamageHurtSpeedHero))
            {
                if (this.health > 0)
                {
                    this.crushingGroundLayers = Mathf.Max(this.crushingGroundLayers, 2);
                }
                if ((!this.isHero && yI < this.fallDamageDeathSpeed) || (this.isHero && yI < this.fallDamageDeathSpeedHero))
                {
                    Map.KnockAndDamageUnit(SingletonMono<MapController>.Instance, this, this.health + 40, DamageType.Crush, -1f, 450f, 0, false);
                    Map.ExplodeUnits(this, 25, DamageType.Crush, 64f, 25f, base.X, base.Y, 200f, 170f, base.playerNum, false, false, true);
                }
                else
                {
                    Map.KnockAndDamageUnit(SingletonMono<MapController>.Instance, this, this.health - 10, DamageType.Crush, -1f, 450f, 0, false);
                    Map.ExplodeUnits(this, 10, DamageType.Crush, 48f, 20f, base.X, base.Y, 150f, 120f, base.playerNum, false, false, true);
                }
            }
        }

        protected virtual bool CrushGroundWhileMoving(int damageGroundAmount, int damageUnitsAmount, float xRange, float yRange, float unitsXRange, float unitsYRange, float xOffset, float yOffset)
        {
            bool hitGround = false;
            if (this.xI < 0 || this.left)
            {
                
                if (Map.HitUnits(this, summoner, summoner.playerNum, damageUnitsAmount, DamageType.Crush, unitsXRange, unitsYRange, base.X - xOffset, base.Y + yOffset, this.xI, 40f, true, true, true, false))
                {
                    // Play sound effect for hitting unit
                }

                hitGround = MapController.DamageGround(this, damageGroundAmount, DamageType.Crush, xRange, yRange, base.X - xOffset, base.Y + yOffset, true);
                if (Physics.Raycast(new Vector3(base.X - xOffset, base.Y + yOffset, 0f), Vector3.left, out this.raycastHit, xRange, this.fragileLayer) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null)
                {
                    this.raycastHit.collider.gameObject.SendMessage("Damage", new DamageObject(1, DamageType.Crush, this.xI, this.yI, this.raycastHit.point.x, this.raycastHit.point.y, this));
                    hitGround = true;
                }
            }
            else if (this.xI > 0 || this.right)
            {
                if (Map.HitUnits(this, summoner, summoner.playerNum, damageUnitsAmount, DamageType.Crush, unitsXRange, unitsYRange, base.X + xOffset, base.Y + yOffset, this.xI, 40f, true, true, true, false))
                {
                    // Play sound effect for hitting unit
                }

                hitGround = MapController.DamageGround(this, damageGroundAmount, DamageType.Crush, xRange, yRange, base.X + xOffset, base.Y + yOffset, true);
                if (Physics.Raycast(new Vector3(base.X + xOffset, base.Y + yOffset, 0f), Vector3.right, out this.raycastHit, xRange, this.fragileLayer) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null)
                {
                    this.raycastHit.collider.gameObject.SendMessage("Damage", new DamageObject(1, DamageType.Crush, this.xI, this.yI, this.raycastHit.point.x, this.raycastHit.point.y, this));
                    hitGround = true;
                }
            }
            // DEBUG
            //RocketLib.Utils.DrawDebug.DrawRectangle("ground", new Vector3(base.X + xOffset - xRange / 2f, base.Y + yOffset - yRange / 2f, 0f), new Vector3(base.X + xOffset + xRange / 2f, base.Y + yOffset + yRange / 2f, 0f), Color.red);

            return hitGround;
        }

        protected override void ApplyFallingGravity()
        {
            if ( this.reachedStartingPoint )
            {
                base.ApplyFallingGravity();
            }
        }

        protected override void RunGroundFriction()
        {
            if ( base.actionState == ActionState.Idle )
            {
                if ( Mathf.Abs(this.xI) < 50f )
                {
                    this.xI *= 1f - this.t * (5 * this.groundFriction);
                }
                else
                {
                    this.xI *= 1f - this.t * this.groundFriction;
                }
            }
        }

        protected override void DontSlipOverEdges()
        {
        }
        #endregion

        #region BeingDamaged
        protected virtual void ResetDamageAmounts()
        {
            this.health = this.maxHealth;
            if (!this.hasResetDamage)
            {
                this.hasResetDamage = true;
                this.burnDamage = 0;
                this.shieldDamage = 0;
            }
        }
        public override void Damage(int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY)
        {
            if (this.dashing && damageType != DamageType.SelfEsteem)
            {
                damage = 0;
            }

            switch (damageType)
            {
                case DamageType.Acid:
                    damageType = DamageType.Fire;
                    goto case DamageType.Fire;
                case DamageType.Fire:
                    this.fireAmount += damage;
                    break;
                case DamageType.GibOnImpact:
                case DamageType.Crush:
                    this.shieldDamage += Mathf.Max(damage, 10);
                    damageType = DamageType.Normal;
                    break;
                case DamageType.SelfEsteem:
                    break;
                case DamageType.Bounce:
                    return;
                default:
                    this.shieldDamage += damage;
                    break;
            }
            if (this.health <= 0)
            {
                this.knockCount++;
                if (this.knockCount % 4 == 0 || (this.alwaysKnockOnExplosions && damageType == DamageType.Explosion))
                {
                    yI = Mathf.Min(yI + 20f, 20f);
                    base.Y += 2f;
                    this.Knock(damageType, xI, 0f, false);
                }
                else
                {
                    this.Knock(damageType, xI, 0f, false);
                }
            }
            else if (damageType != DamageType.SelfEsteem)
            {
                this.PlayDefendSound(damageType);
            }
            if (damageType == DamageType.SelfEsteem && damage >= this.health && this.health > 0)
            {
                this.Death(0f, 0f, new DamageObject(damage, damageType, 0f, 0f, base.X, base.Y, this));
            }

            if (this.shieldDamage + this.fireAmount > maxDamageBeforeExploding && this.pilotted)
            {
                if (damageType == DamageType.Crush && this.shieldDamage > 50)
                {
                    this.DisChargePilot(150f, false, null);
                    this.Gib(DamageType.OutOfBounds, xI, yI + 150f);
                }
                else
                {
                    this.deathCount = 9001;
                    this.pressSpecialFacingDirection = (int)base.transform.localScale.x;
                }
            }
        }

        protected virtual void PlayDefendSound( DamageType damageType )
        {
            if ( damageType == DamageType.Bullet )
            {
                Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.defendSounds, 0.7f + UnityEngine.Random.value * 0.4f, base.transform.position, 0.8f + 0.34f * UnityEngine.Random.value, true, false, false, 0f);
            }
            // Add other damage sound
            else
            {

            }
        }

        public override void Knock(DamageType damageType, float xI, float yI, bool forceTumble)
        {
            if (this.health > 0)
            {
                this.knockCount++;
                if (this.knockCount % 8 == 0 || (this.alwaysKnockOnExplosions && damageType == DamageType.Explosion))
                {
                    this.KnockSimple(new DamageObject(0, DamageType.Bullet, xI * 0.5f, yI * 0.3f, base.X, base.Y, null));
                }
            }
            else
            {
                base.Knock(damageType, xI, yI, forceTumble);
            }
        }

        // Set materials to default colors
        protected virtual void SetUnhurtMaterial()
        {
            this.sprite.meshRender.material.SetColor("_TintColor", Color.gray);
        }

        // Set materials to red tint
        protected virtual void SetHurtMaterial()
        {
            this.sprite.meshRender.material.SetColor("_TintColor", Color.red);
        }

        public override void Death(float xI, float yI, DamageObject damage)
        {
            if (base.GetComponent<Collider>() != null)
            {
                base.GetComponent<Collider>().enabled = false;
            }
            this.DeactivateGun();
            base.Death(xI, yI, damage);
            this.Gib(DamageType.InstaGib, xI, yI);
            if (this.pilotUnit)
            {
                this.DisChargePilot(150f, false, null);
            }
        }

        protected override void Gib(DamageType damageType, float xI, float yI)
        {
            if ( !this.destroyed )
            {
                if (this.deathCount > 9000)
                {
                    EffectsController.CreateMassiveExplosion(base.X, base.Y, 10f, 30f, 120f, 1f, 100f, 1f, 0.6f, 5, 70, 200f, 90f, 0.2f, 0.4f);
                    Map.ExplodeUnits(this, 20, DamageType.Explosion, 72f, 32f, base.X, base.Y + 6f, 200f, 150f, -15, true, false, true);
                    MapController.DamageGround(this, 15, DamageType.Explosion, 72f, base.X, base.Y, null, false);
                    SortOfFollow.Shake(1f, 2f);
                }
                else
                {
                    EffectsController.CreateExplosion(base.X, base.Y + 5f, 8f, 8f, 120f, 0.5f, 100f, 1f, 0.6f, true);
                    EffectsController.CreateHugeExplosion(base.X, base.Y, 10f, 10f, 120f, 0.5f, 100f, 1f, 0.6f, 5, 70, 200f, 90f, 0.2f, 0.4f);
                    MapController.DamageGround(this, 15, DamageType.Explosion, 36f, base.X, base.Y, null, false);
                    Map.ExplodeUnits(this, 20, DamageType.Explosion, 48f, 32f, base.X, base.Y + 6f, 200f, 150f, -15, true, false, true);
                }
                base.Gib(damageType, xI, yI);
            }
            if (this.pilotUnit)
            {
                this.DisChargePilot(180f, false, null);
            }
        }
        #endregion

        #region Piloting
        public override bool CanPilotUnit(int newPlayerNum)
        {
            return HeroController.players[playerNum].character is Furibrosa;
        }

        public override void PilotUnitRPC(Unit newPilotUnit)
        {
            if (!this.fixedBubbles)
            {
                FixPlayerBubble(this.player1Bubble);
                FixPlayerBubble(this.player2Bubble);
                FixPlayerBubble(this.player3Bubble);
                FixPlayerBubble(this.player4Bubble);
                this.fixedBubbles = true;
            }
            this.pilotUnitDelay = 0.2f;
            if (this.pilotted && this.pilotUnit != newPilotUnit)
            {
                this.DisChargePilot(150f, true, newPilotUnit);
            }
            this.ActuallyPilot(newPilotUnit);
        }

        protected virtual void ActuallyPilot(Unit PilotUnit)
        {
            if (base.IsFrozen)
            {
                base.UnFreeze();
            }
            this.keepGoingBeyondTarget = false;
            this.reachedStartingPoint = true;
            this.groundFriction = originalGroundFriction;
            this.pilotUnit = PilotUnit;
            this.pilotted = true;
            base.playerNum = this.pilotUnit.playerNum;
            this.health = this.maxHealth;
            this.deathNotificationSent = false;
            this.isHero = true;
            this.firingPlayerNum = PilotUnit.playerNum;
            this.pilotUnit.StartPilotingUnit(this);
            this.RestartBubble();
            this.blindTime = 0f;
            this.stunTime = 0f;
            this.burnTime = 0f;
            this.ResetDamageAmounts();
            base.GetComponent<Collider>().enabled = true;
            //base.GetComponent<Collider>().gameObject.layer = LayerMask.NameToLayer("FriendlyBarriers");
            base.SetOwner(PilotUnit.Owner);
            this.hud = HeroController.players[PilotUnit.playerNum].hud;
            BroMakerUtilities.SetSpecialMaterials(PilotUnit.playerNum, this.specialSprite, new Vector2(46f, 0f), 5f);
            this.UpdateSpecialIcon();

            // Get info from Furiosa;
            Furibrosa furibrosa = PilotUnit as Furibrosa;
            if ( furibrosa.currentState == PrimaryState.Switching )
            {
                this.currentPrimaryState = furibrosa.nextState;
            }
            else
            {
                this.currentPrimaryState = furibrosa.currentState;
            }

            if (this.currentPrimaryState == PrimaryState.FlareGun)
            {
                this.gunSprite.meshRender.material = this.flareGunMat;
            }
            else
            {
                this.gunSprite.meshRender.material = this.crossbowMat;
            }
            this.SetGunSprite(0, 0);
        }

        protected virtual void DisChargePilot(float disChargeYI, bool stunPilot, Unit dischargedBy)
        {
            DisChargePilotRPC(disChargeYI, stunPilot, dischargedBy);
        }

        protected virtual void DisChargePilotRPC(float disChargeYI, bool stunPilot, Unit dischargedBy)
        {
            if (this.pilotUnit != dischargedBy)
            {
                this.currentFuriosaState = FuriosaState.InVehicle;
                this.gunFrame = 0;
                this.gunCounter = 0f;
                this.hangingOutTimer = 0f;
                this.charged = false;
                this.chargeTime = 0f;
                this.chargeFramerate = 0.09f;

                // Make sure furiosa is holding the same weapon she was holding in the vehicle
                Furibrosa furibrosa = this.pilotUnit as Furibrosa;
                if ( this.currentPrimaryState != furibrosa.currentState )
                {
                    if ( this.currentPrimaryState == PrimaryState.Switching )
                    {
                        // We don't need to change Furiosa's state unless it's a different one than the one we're switching to
                        if (this.nextPrimaryState != furibrosa.currentState)
                        {
                            furibrosa.nextState = this.nextPrimaryState;
                            furibrosa.SwitchWeapon();
                        }
                    }
                    else
                    {
                        furibrosa.nextState = this.currentPrimaryState;
                        furibrosa.SwitchWeapon();
                    }
                }    

                this.pilotUnit.GetComponent<Renderer>().enabled = true;
                this.pilotUnit.DischargePilotingUnit(base.X, Mathf.Clamp(base.Y + 32f, -6f, 100000f), this.xI + ((!stunPilot) ? 0f : ((float)(UnityEngine.Random.Range(0, 2) * 2 - 1) * disChargeYI * 0.3f)), disChargeYI + 100f + ((this.pilotUnit.playerNum >= 0) ? 0f : (disChargeYI * 0.5f)), stunPilot);
                base.StopPlayerBubbles();
                BroMakerUtilities.SetSpecialMaterials(this.pilotUnit.playerNum, this.originalSpecialSprite, new Vector2(5f, 0f), 0f);
                this.pilotUnit = null;
                this.pilotted = false;
                this.isHero = false;
                this.fire = this.wasFire = false;
                this.hasBeenPiloted = true;
                this.releasedFire = false;
                this.fireDelay = 0f;
                this.dashing = false;
                this.DeactivateGun();
                base.SetSyncingInternal(false);

                this.currentPrimaryState = PrimaryState.Crossbow;
                this.ChangeFrame();
                this.RunGun();
            }
        }

        protected override void PressHighFiveMelee(bool forceHighFive = false)
        {
            if (this.pilotUnitDelay <= 0f && this.pilotUnit && this.pilotUnit.IsMine)
            {
                this.DisChargePilot(130f, false, null);
            }
        }
        #endregion

        #region Primary
        protected override void StartFiring()
        {
            this.charged = false;
            this.chargeTime = 0f;
            this.chargeFramerate = 0.09f;

            if ( this.currentPrimaryState != PrimaryState.Switching )
            {
                if ( this.currentFuriosaState == FuriosaState.InVehicle || this.currentFuriosaState == FuriosaState.GoingIn )
                {
                    if (this.currentFuriosaState == FuriosaState.InVehicle)
                    {
                        this.gunFrame = 0;
                        this.gunCounter = 0f;
                    }
                    this.currentFuriosaState = FuriosaState.GoingOut;
                    this.hangingOutTimer = 8f;
                }
                
                base.StartFiring();
            }
        }

        protected override void ReleaseFire()
        {
            if (this.fireDelay < 0.1f)
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

            // Reset timer whenever we press fire
            if ( this.fire )
            {
                this.hangingOutTimer = 8f;
            }
            // Don't fire unless furiosa is fully out the window
            if ( this.currentFuriosaState != FuriosaState.HangingOut )
            {
                return;
            }
            if (this.currentPrimaryState == PrimaryState.Crossbow)
            {
                if (this.fireDelay > 0f)
                {
                    this.fireDelay -= this.t;
                }
                if (this.fireDelay <= 0f)
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
            else if (this.currentPrimaryState == PrimaryState.FlareGun)
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

        // DEBUG
        protected float debugOffsetXBolt = 7f;
        protected float debugOffsetYBolt = 35f;
        protected float debugOffsetXFlare = 7f;
        protected float debugOffsetYFlare = 35f;

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            // Fire crossbow
            if (this.currentPrimaryState == PrimaryState.Crossbow)
            {
                // Fire explosive bolt
                if (this.charged)
                {
                    x = base.X + base.transform.localScale.x * debugOffsetXBolt;
                    y = base.Y + debugOffsetYBolt;
                    xSpeed = base.transform.localScale.x * 500 + (this.xI / 2);
                    ySpeed = -50f;
                    this.gunFrame = 1;
                    this.SetGunSprite(this.gunFrame, 0);
                    this.TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally(explosiveBoltPrefab, this, x, y, xSpeed, ySpeed, base.playerNum) as Bolt;
                    firedBolt.gameObject.SetActive(true);

                }
                // Fire normal bolt
                else
                {
                    x = base.X + base.transform.localScale.x * debugOffsetXBolt;
                    y = base.Y + debugOffsetYBolt;
                    xSpeed = base.transform.localScale.x * 400 + (this.xI / 2);
                    ySpeed = -50f;
                    this.gunFrame = 1;
                    this.SetGunSprite(this.gunFrame, 0);
                    this.TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally(boltPrefab, this, x, y, xSpeed, ySpeed, base.playerNum) as Bolt;
                    firedBolt.gameObject.SetActive(true);
                }
                this.fireDelay = 0.5f;
            }
            else if (this.currentPrimaryState == PrimaryState.FlareGun)
            {
                x = base.X + base.transform.localScale.x * debugOffsetXFlare;
                y = base.Y + debugOffsetYFlare;
                xSpeed = base.transform.localScale.x * 450;
                ySpeed = UnityEngine.Random.Range(-25, 0);
                EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
                ProjectileController.SpawnProjectileLocally(flarePrefab, this, x, y, xSpeed, ySpeed, base.playerNum);
                this.gunFrame = 3;
                this.fireDelay = 0.8f;
            }
        }

        protected override void RunGun()
        {
            if ( this.currentFuriosaState == FuriosaState.InVehicle && this.currentPrimaryState != PrimaryState.Switching )
            {
                this.DeactivateGun();
                if (this.pilotted)
                {
                    this.sprite.SetLowerLeftPixel(2 * this.spritePixelWidth, this.spritePixelHeight);
                }
            }
            else if ( this.currentFuriosaState == FuriosaState.GoingOut )
            {
                this.DeactivateGun();
                this.gunCounter += this.t;
                if (this.gunCounter > 0.11f)
                {
                    this.gunCounter -= 0.11f;
                    ++this.gunFrame;
                    // Skip second frame
                    if (this.gunFrame == 1)
                    {
                        ++this.gunFrame;
                    }
                }

                this.sprite.SetLowerLeftPixel( this.spritePixelWidth * (this.gunFrame + 3), (this.currentPrimaryState == PrimaryState.FlareGun ? 2 : 3) * this.spritePixelHeight );

                // Finished going out
                if ( this.gunFrame == 4 )
                {
                    this.currentFuriosaState = FuriosaState.HangingOut;
                    this.gunFrame = 0;
                }
            }
            else if ( this.currentFuriosaState == FuriosaState.GoingIn )
            {
                this.DeactivateGun();
                this.gunCounter += this.t;
                if (this.gunCounter > 0.11f)
                {
                    this.gunCounter -= 0.11f;
                    --this.gunFrame;
                }

                this.sprite.SetLowerLeftPixel(this.spritePixelWidth * (4 - this.gunFrame), (this.currentPrimaryState == PrimaryState.FlareGun ? 3 : 2) * this.spritePixelHeight);

                // Finished going out
                if (this.gunFrame == 0)
                {
                    this.currentFuriosaState = FuriosaState.InVehicle;
                }
            }
            // Animate crossbow
            else if (this.currentPrimaryState == PrimaryState.Crossbow)
            {
                this.sprite.SetLowerLeftPixel(1 * this.spritePixelWidth, this.spritePixelHeight);
                this.gunSprite.gameObject.SetActive(true);
                if (this.fire)
                {
                    if (this.chargeTime > 0.2f)
                    {
                        this.gunCounter += this.t;
                        if (this.gunCounter > this.chargeFramerate)
                        {
                            this.gunCounter -= this.chargeFramerate;
                            ++this.gunFrame;
                            if (this.gunFrame > 3)
                            {
                                this.gunFrame = 0;
                                if (!this.charged)
                                {
                                    this.charged = true;
                                    this.chargeFramerate = 0.04f;
                                }
                            }
                        }
                        this.SetGunSprite(this.gunFrame + 4, 0);
                    }
                }
                else if (!this.WallDrag && this.gunFrame > 0)
                {
                    this.gunCounter += this.t;
                    if (this.gunCounter > 0.045f)
                    {
                        this.gunCounter -= 0.045f;
                        ++this.gunFrame;
                        if ( this.gunFrame > 3 )
                        {
                            this.gunFrame = 0;
                        }
                        this.SetGunSprite(this.gunFrame, 0);
                    }
                }
                else
                {
                    this.SetGunSprite(this.gunFrame, 0);
                }
            }
            // Animate flaregun
            else if (this.currentPrimaryState == PrimaryState.FlareGun)
            {
                this.sprite.SetLowerLeftPixel(1 * this.spritePixelWidth, this.spritePixelHeight);
                this.gunSprite.gameObject.SetActive(true);
                if (this.gunFrame > 0)
                {
                    this.gunCounter += this.t;
                    if (this.gunCounter > 0.0334f)
                    {
                        this.gunCounter -= 0.0334f;
                        --this.gunFrame;
                    }
                }
                this.SetGunSprite(this.gunFrame, 0);
            }
            // Animate switching
            else if (this.currentPrimaryState == PrimaryState.Switching)
            {
                this.DeactivateGun();
                this.gunCounter += this.t;

                // Animate leaning down to switch weapon
                if ( this.currentFuriosaState == FuriosaState.InVehicle )
                {
                    if ( this.gunCounter > 0.2f )
                    {
                        this.gunCounter -= 0.2f;
                        ++this.gunFrame;
                    }

                    this.sprite.SetLowerLeftPixel((4 - this.gunFrame) * this.spritePixelWidth, 2 * this.spritePixelHeight);

                    if ( this.gunFrame == 1 )
                    {
                        this.SwitchWeapon();
                    }
                }
                // Animate full lean back in and lean back out
                else
                {
                    // Ensure we don't start retracting while switching
                    this.hangingOutTimer = 8f;

                    if ( this.gunFrame != 3 && this.gunFrame != 4 )
                    {
                        if (this.gunCounter > 0.10f)
                        {
                            this.gunCounter -= 0.10f;
                            ++this.gunFrame;
                        }
                    }
                    else
                    {
                        if (this.gunCounter > 0.15f)
                        {
                            this.gunCounter -= 0.15f;
                            ++this.gunFrame;
                        }
                    }

                    if (this.gunFrame > 7)
                    {
                        this.SwitchWeapon();
                    }
                    else
                    {
                        this.sprite.SetLowerLeftPixel(this.gunFrame * this.spritePixelWidth, (this.nextPrimaryState == PrimaryState.FlareGun ? 2 : 3) * this.spritePixelHeight);
                    }
                }
            }
        }

        protected void StartSwitchingWeapon()
        {
            if (!this.usingSpecial && this.currentPrimaryState != PrimaryState.Switching)
            {
                this.CancelMelee();
                this.SetGestureAnimation(GestureElement.Gestures.None);
                if (this.currentPrimaryState == PrimaryState.Crossbow)
                {
                    this.nextPrimaryState = PrimaryState.FlareGun;
                }
                else
                {
                    this.nextPrimaryState = PrimaryState.Crossbow;
                }
                this.currentPrimaryState = PrimaryState.Switching;
                // Don't change frame if we're currently in the middle of another animation
                if ( this.currentFuriosaState != FuriosaState.GoingIn || this.currentFuriosaState != FuriosaState.GoingOut )
                {
                    this.gunFrame = 0;
                    this.gunCounter = 0f;
                    this.RunGun();
                }
            }
        }

        protected void SwitchWeapon()
        {
            this.gunFrame = 0;
            this.gunCounter = 0f;
            this.currentPrimaryState = this.nextPrimaryState;
            if (this.currentPrimaryState == PrimaryState.FlareGun)
            {
                this.gunSprite.meshRender.material = this.flareGunMat;
            }
            else
            {
                this.gunSprite.meshRender.material = this.crossbowMat;
            }
            this.SetGunSprite(0, 0);
        }
        #endregion

        #region Special
        protected void UpdateSpecialIcon()
        {
            this.hud.SetFuel(this.boostFuel, this.boostFuel <= 0.2f);

            // Re-enable grenade icons
            for (int i = 0; i < this.SpecialAmmo; ++i)
            {
                this.hud.grenadeIcons[i].gameObject.SetActive(true);
            }
        }

        protected override void PressSpecial()
        {
            if ( this.SpecialAmmo > 0 )
            {
                this.usingSpecial = true;
                --this.SpecialAmmo;
                this.specialFrame = 0;
                this.specialFrameCounter = 0f;
            }
            else
            {
                HeroController.FlashSpecialAmmo(base.playerNum);
            }
        }

        public void CreateMuzzleFlashBigEffect(float x, float y, float z, float xI, float yI, Transform parent)
        {
            if (EffectsController.instance != null)
            {
                EffectsController.CreateEffect(EffectsController.instance.muzzleFlashBigPrefab, x, y, z, 0f, new Vector3(xI, yI, 0f), parent);
                EffectsController.CreateEffect(EffectsController.instance.muzzleFlashBigGlowPrefab, x, y, z, 0f);
            }
        }

        protected override void UseSpecial()
        {
            float x = base.X + base.transform.localScale.x * 54f;
            float y = base.Y + 10f;
            float xSpeed = base.transform.localScale.x * 500 + (this.xI / 2);
            float ySpeed = 0;
            CreateMuzzleFlashBigEffect(base.X + base.transform.localScale.x * 46f, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
            Harpoon firedHarpoon = ProjectileController.SpawnProjectileLocally(harpoonPrefab, this, x, y, xSpeed, ySpeed, base.playerNum) as Harpoon;
            firedHarpoon.gameObject.SetActive(true);
        }
        #endregion
    }
}
