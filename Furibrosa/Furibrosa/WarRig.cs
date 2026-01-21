using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BroMakerLib;
using BroMakerLib.Loggers;
using Effects;
using HarmonyLib;
using RocketLib;
using Rogueforce;
using UnityEngine;
using static Furibrosa.Furibrosa;
using Random = UnityEngine.Random;

namespace Furibrosa
{
    public class WarRig : Mook
    {
        // General Variables
        string directoryPath;
        public Unit pilotUnit = null;
        public bool pilotted = false;
        public Furibrosa summoner = null;
        protected bool pilotIsFuribrosa = false;
        protected int previousLayer = 0;
        protected MobileSwitch pilotSwitch;
        protected bool hasResetDamage;
        protected float shieldDamage;
        public bool hasBeenPiloted;
        protected int fireAmount;
        protected int knockCount;
        public bool alwaysKnockOnExplosions = false;
        protected float pilotUnitDelay;
        protected bool fixedBubbles = false;
        public Material specialSprite;
        protected PlayerHUD hud;
        public const int maxDamageBeforeExploding = 100;
        protected int deathCount = 0;
        protected float smokeCounter = 0f;
        protected float deathCountdownCounter = 0f;
        protected float deathCountdown = 0f;
        protected int deathCountdownExplodeThreshold = 20;
        readonly List<MonoBehaviour> recentlyHitBy = new List<MonoBehaviour>();
        protected float hitCooldown = 0f;
        protected bool gibbed = false;

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
        protected const float CrushingSpeed = 200f;
        protected List<Unit> recentlyHitUnits = new List<Unit>();
        protected float crushUnitCooldown = 0f;
        protected float crushMomentum = 0f;
        protected float collisionHeadHeight;

        // Sprite Variables
        public SpriteSM wheelsSprite, bumperSprite, longSmokestacksSprite, shortSmokestacksSprite, frontSmokestacksSprite;
        protected float smokestackCooldown = 0f;
        protected float smokestackCounter = 0f;
        protected int smokestackFrame = 0;
        protected float blueSmokeCooldown = 0f;
        protected float blueSmokeCounter = 0f;
        protected int blueSmokeFrame = 0;
        protected bool usingBlueFlame = false;

        // Audio Variables
        public AudioSource vehicleEngineAudio;
        protected const float vehicleEngineVolume = 0.2f;
        public AudioSource vehicleHornAudio;
        public AudioClip vehicleIdleLoop, vehicleRev, vehicleHorn, vehicleHornLong;
        public AudioClip[] vehicleHit;
        public AudioClip harpoonFire;
        protected bool playedHornStart = false;
        protected bool playedRevStart = false;
        protected float startupTimer = 1.9f;
        protected bool releasedHorn = false;
        protected float hornTimer = 0f;

        // Startup Variables
        protected bool reachedStartingPoint = false;
        public float targetX = 0f;
        public bool keepGoingBeyondTarget = false;
        public float secondTargetX = 0f;
        public float summonedDirection = 1f;
        public bool manualSpawn = false;

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

        #region Setup
        public void Setup()
        {
            directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            gameObject.name = "WarRig";
            Destroy( gameObject.FindChildOfName( "ZMook" ) );
            Destroy( GetComponent<BigGuyAI>() );
            MookArmouredGuy mookArmouredGuy = gameObject.GetComponent<MookArmouredGuy>();
            Traverse trav = Traverse.Create( mookArmouredGuy );

            // Assign null values
            sprite = GetComponent<SpriteSM>();
            gunSprite = mookArmouredGuy.gunSprite;
            soundHolder = mookArmouredGuy.soundHolder;
            soundHolderFootSteps = mookArmouredGuy.soundHolderFootSteps;
            player1Bubble = mookArmouredGuy.player1Bubble;
            player2Bubble = mookArmouredGuy.player2Bubble;
            player3Bubble = mookArmouredGuy.player3Bubble;
            player4Bubble = mookArmouredGuy.player4Bubble;

            blood = mookArmouredGuy.blood;
            heroTrailPrefab = mookArmouredGuy.heroTrailPrefab;
            high5Bubble = mookArmouredGuy.high5Bubble;
            projectile = mookArmouredGuy.projectile;
            specialGrenade = mookArmouredGuy.specialGrenade;
            if ( specialGrenade != null )
            {
                specialGrenade.playerNum = mookArmouredGuy.specialGrenade.playerNum;
            }

            heroType = mookArmouredGuy.heroType;
            wallDragAudio = trav.GetFieldValue( "wallDragAudio" ) as AudioSource;
            SetOwner( mookArmouredGuy.Owner );

            Destroy( mookArmouredGuy );

            // Load all sprites
            // Ensure main sprite is behind all other sprites
            LoadSprite( gameObject, "vehicleSprite.png", new Vector3( 0f, 31f, 0.11f ) );

            // Load weapon sprites
            crossbowMat = ResourcesController.GetMaterial( Path.Combine( directoryPath, "vehicleCrossbow.png" ) );
            flareGunMat = ResourcesController.GetMaterial( Path.Combine( directoryPath, "vehicleFlareGun.png" ) );
            LoadSprite( gunSprite.gameObject, "vehicleCrossbow.png", new Vector3( 0f, 31f, 0.1f ) );

            // Load wheel sprites
            GameObject wheelsObject = new GameObject( "WarRigWheels", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } );
            wheelsObject.transform.parent = transform;
            wheelsSprite = LoadSprite( wheelsObject, "vehicleWheels.png", new Vector3( 0f, 31f, 0.1f ) );

            // Load bumper sprites
            GameObject bumperObject = new GameObject( "WarRigBumper", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } );
            bumperObject.transform.parent = transform;
            bumperSprite = LoadSprite( bumperObject, "vehicleBumper.png", new Vector3( 0f, 31f, 0.1f ) );

            // Load long smokestack sprites
            GameObject longSmokestacksObject = new GameObject( "WarRigLongSmokestacks", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } );
            longSmokestacksObject.transform.parent = transform;
            longSmokestacksSprite = LoadSprite( longSmokestacksObject, "vehicleLongSmokestacks.png", new Vector3( 0f, 56f, 0.1f ) );
            longSmokestacksSprite.SetLowerLeftPixel( 0f, 128f );

            // Load short smokestack sprites
            GameObject shortSmokestacksObject = new GameObject( "WarRigShortSmokestacks", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } );
            shortSmokestacksObject.transform.parent = transform;
            shortSmokestacksSprite = LoadSprite( shortSmokestacksObject, "vehicleShortSmokestacks.png", new Vector3( 0f, 56f, 0.1f ) );
            shortSmokestacksSprite.SetLowerLeftPixel( 0f, 128f );

            // Load front smokestack sprites
            GameObject frontSmokestacksObject = new GameObject( "WarRigFrontSmokestacks", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } );
            frontSmokestacksObject.transform.parent = transform;
            frontSmokestacksSprite = LoadSprite( frontSmokestacksObject, "vehicleFrontSmokestacks.png", new Vector3( 0f, 31f, 0.1f ) );
            frontSmokestacksSprite.SetLowerLeftPixel( 0f, 128f );

            spritePixelWidth = 128;
            spritePixelHeight = 64;

            // Load special icon sprite
            specialSprite = ResourcesController.GetMaterial( directoryPath, "vehicleSpecial.png" );

            // Clear blood shrapnel
            blood = [];

            // Create Harpoon prefab if not yet created
            if ( harpoonPrefab == null )
            {
                harpoonPrefab = new GameObject( "Harpoon", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( BoxCollider ), typeof( Harpoon ) } ).GetComponent<Harpoon>();
                harpoonPrefab.gameObject.SetActive( false );
                harpoonPrefab.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Predabro ) as Predabro ).projectile.soundHolder;
                harpoonPrefab.Setup();
                DontDestroyOnLoad( harpoonPrefab );
            }

            // Create gibs
            InitializeGibs();

            // Setup audio
            if ( vehicleEngineAudio == null )
            {
                vehicleEngineAudio = gameObject.AddComponent<AudioSource>();
                vehicleEngineAudio.rolloffMode = AudioRolloffMode.Linear;
                vehicleEngineAudio.dopplerLevel = 0f;
                vehicleEngineAudio.minDistance = 100f;
                vehicleEngineAudio.maxDistance = 750f;
                vehicleEngineAudio.spatialBlend = 1f;
                vehicleEngineAudio.spatialize = false;
                vehicleEngineAudio.volume = vehicleEngineVolume;
            }

            if ( vehicleHornAudio == null )
            {
                vehicleHornAudio = gameObject.AddComponent<AudioSource>();
                vehicleHornAudio.rolloffMode = AudioRolloffMode.Linear;
                vehicleHornAudio.dopplerLevel = 0f;
                vehicleHornAudio.minDistance = 100f;
                vehicleHornAudio.maxDistance = 750f;
                vehicleHornAudio.spatialBlend = 1f;
                vehicleEngineAudio.spatialize = true;
                vehicleHornAudio.volume = 1f;
            }

            // Load Audio
            try
            {
                directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
                directoryPath = Path.Combine( directoryPath, "sounds" );
                vehicleIdleLoop = ResourcesController.GetAudioClip( directoryPath, "vehicleIdleLoop.wav" );
                vehicleRev = ResourcesController.GetAudioClip( directoryPath, "vehicleBoost.wav" );
                vehicleHorn = ResourcesController.GetAudioClip( directoryPath, "vehicleHornMedium.wav" );
                vehicleHornLong = ResourcesController.GetAudioClip( directoryPath, "vehicleHornLong.wav" );

                vehicleHit = new AudioClip[3];
                vehicleHit[0] = ResourcesController.GetAudioClip( directoryPath, "vehicleHit1.wav" );
                vehicleHit[1] = ResourcesController.GetAudioClip( directoryPath, "vehicleHit2.wav" );
                vehicleHit[2] = ResourcesController.GetAudioClip( directoryPath, "vehicleHit3.wav" );

                harpoonFire = ResourcesController.GetAudioClip( directoryPath, "harpoon.wav" );
            }
            catch ( Exception ex )
            {
                BMLogger.Log( "Exception Loading Audio: " + ex.ToString() );
            }

            gameObject.SetActive( false );
        }

        protected override void Awake()
        {
            base.Awake();

            // Make sure this component isn't being added from the mook class
            if ( GetComponent<DisableWhenOffCamera>() != null )
            {
                Destroy( GetComponent<DisableWhenOffCamera>() );
            }
        }

        protected override void Start()
        {
            base.Start();
            // Prevent tank from standing on platforms and ladders
            platformLayer = groundLayer;
            ladderLayer = groundLayer;
            speed = 225f;
            waistHeight = 10f;
            deadWaistHeight = 10f;
            // We use a different height for head and collision so that bullets won't hit in the wrong places
            collisionHeadHeight = 52f;
            height = 31f;
            headHeight = height;
            standingHeadHeight = height;
            deadHeadHeight = height;
            frontHeadHeight = 32f;
            halfWidth = 63f;
            feetWidth = 58f;
            width = 33f;
            distanceToFront = 32f;
            distanceToBack = 49f;
            doRollOnLand = false;
            canChimneyFlip = false;
            canWallClimb = false;
            canTumble = false;
            canDuck = false;
            canLedgeGrapple = false;
            jumpForce = 360;
            gunSprite.gameObject.layer = 19;
            originalSpecialAmmo = 3;
            SpecialAmmo = 3;
            bloodColor = BloodColor.None;
            dashSpeedM = 1.5f;

            // Setup audio
            if ( !vehicleEngineAudio )
            {
                vehicleEngineAudio = gameObject.AddComponent<AudioSource>();
                vehicleEngineAudio.rolloffMode = AudioRolloffMode.Linear;
                vehicleEngineAudio.dopplerLevel = 0f;
                vehicleEngineAudio.minDistance = 100f;
                vehicleEngineAudio.maxDistance = 750f;
                vehicleEngineAudio.spatialBlend = 1f;
                vehicleEngineAudio.spatialize = false;
                vehicleEngineAudio.volume = vehicleEngineVolume;
            }

            if ( !vehicleHornAudio )
            {
                vehicleHornAudio = gameObject.AddComponent<AudioSource>();
                vehicleHornAudio.rolloffMode = AudioRolloffMode.Linear;
                vehicleHornAudio.dopplerLevel = 0f;
                vehicleHornAudio.minDistance = 100f;
                vehicleHornAudio.maxDistance = 750f;
                vehicleHornAudio.spatialBlend = 1f;
                vehicleEngineAudio.spatialize = true;
                vehicleHornAudio.volume = 1f;
            }

            // Make sure gib holder exists
            if ( !gibs )
            {
                InitializeGibs();
            }

            // Default to playerNum 0 so that the vehicle doesn't kill the player before they start riding it
            playerNum = 0;

            // Make sure sprites look correct with multiple War Rigs on screen
            Vector3 playerSpriteOffset = new Vector3( 0f, 0f, ( summoner != null ? summoner.playerNum : Random.Range( 0, 4 ) ) * 0.1f );
            GetComponent<SpriteSM>().offset += playerSpriteOffset;
            gunSprite.offset += playerSpriteOffset;
            wheelsSprite.offset += playerSpriteOffset;
            bumperSprite.offset += playerSpriteOffset;
            longSmokestacksSprite.offset += playerSpriteOffset;
            shortSmokestacksSprite.offset += playerSpriteOffset;
            frontSmokestacksSprite.offset += playerSpriteOffset;

            DeactivateGun();
            GameObject platformObject = gameObject.FindChildOfName( "Platform" );
            if ( platformObject )
            {
                platform = platformObject.GetComponent<BoxCollider>();
                platform.center = new Vector3( -9f, 44f, -4.5f );
                platform.size = new Vector3( 80f, 12f, 64f );
            }

            DisableSprites();
        }

        public SpriteSM LoadSprite( GameObject gameObject, string spritePath, Vector3 offset )
        {
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

            Material material = ResourcesController.GetMaterial( directoryPath, spritePath );
            renderer.material = material;

            SpriteSM sprite = gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2( 0, 64 );
            sprite.pixelDimensions = new Vector2( 128, 64 );
            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 128;
            sprite.height = 64;
            sprite.offset = offset;

            gameObject.layer = 19;

            return sprite;
        }

        protected void FixPlayerBubble( ReactionBubble bubble )
        {
            bubble.transform.localPosition = new Vector3( 0f, 53f, 0f );
            bubble.SetPosition( bubble.transform.localPosition );
            Traverse bubbleTrav = Traverse.Create( bubble );
            bubble.RestartBubble();
            bubbleTrav.Field( "yStart" ).SetValue( bubble.transform.localPosition.y + 5 );
        }

        protected void CreateGib( string name, Vector2 lowerLeftPixel, Vector2 pixelDimensions, float width, float height, Vector3 localPositionOffset )
        {
            BroMakerUtilities.CreateGibPrefab( name, lowerLeftPixel, pixelDimensions, width, height, new Vector3( 0f, 0f, 0f ), localPositionOffset, false, DoodadGibsType.Metal, 6, false, BloodColor.None, 1, true, 8, false, false, 3, 1, 1, 7f ).transform.parent = gibs.transform;
        }

        protected void InitializeGibs()
        {
            gibs = new GameObject( "WarRigGibs", new Type[] { typeof( GibHolder ) } ).GetComponent<GibHolder>();
            gibs.gameObject.SetActive( false );
            DontDestroyOnLoad( gibs );
            CreateGib( "Scrap", new Vector2( 397, 8 ), new Vector2( 10, 4 ), 10f, 4f, new Vector3( -25f, 30f, 0f ) );
            CreateGib( "Scrap2", new Vector2( 413, 12 ), new Vector2( 6, 6 ), 6f, 6f, new Vector3( -14f, 20f, 0f ) );
            CreateGib( "Wheel", new Vector2( 427, 13 ), new Vector2( 11, 10 ), 13.75f, 12.5f, new Vector3( 36f, 8f, 0f ) );
            CreateGib( "Scrap3", new Vector2( 443, 14 ), new Vector2( 15, 10 ), 15f, 10f, new Vector3( 30f, 28f, 0f ) );
            CreateGib( "Scrap4", new Vector2( 462, 8 ), new Vector2( 5, 5 ), 5f, 5f, new Vector3( -17f, 44f, 0f ) );
            CreateGib( "Scrap5", new Vector2( 462, 18 ), new Vector2( 4, 6 ), 4f, 6f, new Vector3( -40f, 40f, 0f ) );
            CreateGib( "SmokestackScrap", new Vector2( 477, 21 ), new Vector2( 5, 16 ), 5f, 16f, new Vector3( -48f, 55f, 0f ) );
            CreateGib( "SmokestackScrap2", new Vector2( 477, 21 ), new Vector2( 5, 16 ), 5f, 16f, new Vector3( -42f, 55f, 0f ) );
            CreateGib( "BackSmokestackScrap", new Vector2( 484, 17 ), new Vector2( 5, 12 ), 5f, 12f, new Vector3( -1f, 57f, 0f ) );
            CreateGib( "BackSmokestackScrap2", new Vector2( 484, 17 ), new Vector2( 5, 12 ), 5f, 12f, new Vector3( 4.5f, 57f, 0f ) );
            CreateGib( "Scrap6", new Vector2( 401, 21 ), new Vector2( 4, 4 ), 4f, 4f, new Vector3( -34f, 32f, 0f ) );
            CreateGib( "Scrap7", new Vector2( 398, 31 ), new Vector2( 12, 5 ), 12f, 5f, new Vector3( 25f, 27f, 0f ) );
            CreateGib( "Scrap8", new Vector2( 414, 25 ), new Vector2( 6, 6 ), 6f, 6f, new Vector3( 6f, 47f, 0f ) );
            CreateGib( "Scrap9", new Vector2( 414, 31 ), new Vector2( 5, 4 ), 5f, 4f, new Vector3( 22f, 47f, 0f ) );
            CreateGib( "Scrap10", new Vector2( 428, 34 ), new Vector2( 11, 16 ), 11f, 16f, new Vector3( -16f, 34f, 0f ) );
            CreateGib( "Scrap11", new Vector2( 447, 27 ), new Vector2( 7, 5 ), 7f, 5f, new Vector3( 39f, 28f, 0f ) );
            CreateGib( "Scrap12", new Vector2( 462, 28 ), new Vector2( 5, 6 ), 5f, 6f, new Vector3( 22f, 29f, 0f ) );
            CreateGib( "ExhaustScrap", new Vector2( 396, 42 ), new Vector2( 13, 6 ), 13f, 6f, new Vector3( -8f, 49f, 0f ) );
            CreateGib( "Wheel", new Vector2( 411, 54 ), new Vector2( 13, 13 ), 17.5f, 17.5f, new Vector3( -50f, 8f, 0f ) );
            CreateGib( "Wheel2", new Vector2( 411, 54 ), new Vector2( 13, 13 ), 17.5f, 17.5f, new Vector3( -32f, 8f, 0f ) );
            CreateGib( "Wheel3", new Vector2( 411, 54 ), new Vector2( 13, 13 ), 17.5f, 17.5f, new Vector3( 1f, 8f, 0f ) );
            CreateGib( "ExhaustScrap2", new Vector2( 431, 54 ), new Vector2( 4, 12 ), 4f, 12f, new Vector3( 31f, 38f, 0f ) );
            CreateGib( "BumperScrap", new Vector2( 448, 53 ), new Vector2( 10, 22 ), 10f, 22f, new Vector3( 12f, 13f, 0f ) );
            CreateGib( "Scrap13", new Vector2( 459, 42 ), new Vector2( 15, 9 ), 15f, 9f, new Vector3( -15f, 10f, 0f ) );
            CreateGib( "Skull", new Vector2( 477, 41 ), new Vector2( 5, 7 ), 5f, 7f, new Vector3( 24f, 15f, 0f ) );
            CreateGib( "Skull2", new Vector2( 477, 41 ), new Vector2( 5, 7 ), 5f, 7f, new Vector3( 30f, 16f, 0f ) );
            CreateGib( "Skull3", new Vector2( 477, 41 ), new Vector2( 5, 7 ), 5f, 7f, new Vector3( 36f, 20f, 0f ) );
            CreateGib( "Skull4", new Vector2( 477, 41 ), new Vector2( 5, 7 ), 5f, 7f, new Vector3( 41f, 16f, 0f ) );
            CreateGib( "Skull5", new Vector2( 477, 41 ), new Vector2( 5, 7 ), 5f, 7f, new Vector3( 47f, 14f, 0f ) );

            // Make sure gibs are on layer 19 since the texture they're using is transparent
            for ( int i = 0; i < gibs.transform.childCount; ++i )
            {
                gibs.transform.GetChild( i ).gameObject.layer = 19;
            }
        }

        public void SetTarget( Furibrosa summoner, float targetX, Vector3 localScale, float summonedDirection, bool manualSpawn = false )
        {
            this.summoner = summoner;
            this.targetX = targetX;
            transform.localScale = localScale;
            this.summonedDirection = summonedDirection;
            this.manualSpawn = manualSpawn;
        }

        protected void MoveTowardsStart()
        {
            if ( !reachedStartingPoint )
            {
                if ( Tools.FastAbsWithinRange( X - targetX, 5 ) || Mathf.Sign( targetX - X ) != summonedDirection )
                {
                    if ( !manualSpawn && summoner != null && summoner.holdingSpecial )
                    {
                        summoner.GoPastFuriosa();
                    }

                    if ( !keepGoingBeyondTarget )
                    {
                        xI = 0f;
                    }

                    reachedStartingPoint = true;
                    groundFriction = originalGroundFriction;
                }
                else if ( Tools.FastAbsWithinRange( X - targetX, 75f ) && !( keepGoingBeyondTarget || ( !manualSpawn && summoner != null && summoner.holdingSpecial ) ) )
                {
                    groundFriction = 5f;
                }
                else
                {
                    xI = summonedDirection * speed * 2f;
                }
            }
            else if ( keepGoingBeyondTarget )
            {
                if ( Tools.FastAbsWithinRange( X - secondTargetX, 5 ) || Mathf.Sign( secondTargetX - X ) != summonedDirection )
                {
                    xI = 0f;
                    keepGoingBeyondTarget = false;
                    groundFriction = originalGroundFriction;
                }
                else if ( Tools.FastAbsWithinRange( X - secondTargetX, 75f ) )
                {
                    groundFriction = 5f;
                }
                else
                {
                    xI = summonedDirection * speed * 2f;
                }
            }
        }
        #endregion

        #region General
        protected override void Update()
        {
            if ( startupTimer > 0f )
            {
                RunStartup();
                return;
            }

            // Check if pilot unit was destroyed (in case they dropped out)
            if ( pilotted && !pilotUnit )
            {
                DisChargePilot( 0f, false, null );
            }

            base.Update();

            // Set camera position
            if ( pilotted )
            {
                // Controls where camera is
                if ( pilotIsFuribrosa )
                {
                    pilotUnit.SetXY( X, Y + 25f );
                    pilotUnit.row = row;
                    pilotUnit.collumn = collumn;
                    pilotUnit.transform.position = new Vector3( pilotUnit.X, pilotUnit.Y, 10f );
                    if ( pilotUnit.playerNum < 0 )
                    {
                        pilotUnit.gameObject.SetActive( false );
                    }
                }
                else
                {
                    // Position non-Furibrosa pilots at the window and keep them visible
                    pilotUnit.SetXY( X + 11f * transform.localScale.x, Y + 25f );
                    pilotUnit.row = row;
                    pilotUnit.collumn = collumn;
                    pilotUnit.transform.position = new Vector3( pilotUnit.X, pilotUnit.Y, transform.position.z + 0.3f );
                    pilotUnit.transform.localScale = transform.localScale;
                    pilotUnit.GetComponent<Renderer>().enabled = true;
                }
            }

            // Run ground crushing
            RunCrushGround();

            // Run boosting
            RunBoosting();

            // Move towards wherever player was when they summoned the war rig
            MoveTowardsStart();

            // Run animation loops for all other sprites
            AnimateWarRig();

            // Run Audio
            RunAudio();

            // Create pilot switch
            if ( pilotSwitch == null )
            {
                pilotSwitch = SwitchesController.CreatePilotMookSwitch( this, new Vector3( 0f, 40f, 0f ) );
            }

            // Decrement cooldown
            if ( pilotUnitDelay > 0f )
            {
                pilotUnitDelay -= t;
            }

            hitCooldown -= t;
            if ( hitCooldown <= 0 )
            {
                recentlyHitBy.Clear();
                hitCooldown = 0.2f;
            }

            // See if vehicle should die
            if ( shieldDamage + fireAmount > 130f )
            {
                deathCount = 9001;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if ( pilotted )
            {
                UpdateSpecialIcon();
            }
        }

        protected void RunStartup()
        {
            SetDeltaTime();

            if ( !manualSpawn )
            {
                // Stay near Furiosa
                if ( summonedDirection > 0 )
                {
                    SetPosition( new Vector3( SortOfFollow.GetScreenMinX() - 65f, transform.position.y, 0f ) );
                }
                else
                {
                    SetPosition( new Vector3( SortOfFollow.GetScreenMaxX() + 65f, transform.position.y, 0f ) );
                }
            }

            // Start horn audio
            if ( !playedHornStart )
            {
                vehicleHornAudio.clip = vehicleHorn;
                vehicleHornAudio.Play();
                playedHornStart = true;
            }
            // Disable horn audio after it's finished playing
            else if ( playedHornStart && !vehicleHornAudio.isPlaying )
            {
                vehicleHornAudio.enabled = false;
            }

            startupTimer -= t;

            if ( startupTimer < 0.8 && !playedRevStart )
            {
                // Play inbetween vehicle and player
                Sound.GetInstance().PlaySoundEffectAt( vehicleRev, 0.7f, summoner != null ? ( transform.position + summoner.transform.position ) / 2f : transform.position, 1f, true, false, false, 0f );
                playedRevStart = true;
            }

            if ( startupTimer < 0.6 )
            {
                // Start engine audio
                if ( !vehicleEngineAudio.isPlaying )
                {
                    vehicleEngineAudio.clip = vehicleIdleLoop;
                    vehicleEngineAudio.pitch = 1.3f;
                    vehicleEngineAudio.volume = 0;
                    vehicleEngineAudio.loop = true;
                    vehicleEngineAudio.Play();
                }

                vehicleEngineAudio.volume = ( 0.6f - startupTimer ) / 3f;
            }

            if ( startupTimer <= 0f )
            {
                vehicleEngineAudio.volume = vehicleEngineVolume;
                EnableSprites();
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

        public override bool CanInseminate( float xI, float yI )
        {
            return false;
        }

        public override bool IsHeavy()
        {
            return true;
        }

        protected override void CheckForTraps( ref float yIT )
        {
            float num = Y + yIT;
            if ( num <= groundHeight + 1f )
            {
                num = groundHeight + 1f;
            }

            if ( Map.isEditing || invulnerable )
            {
                return;
            }

            if ( !IsEnemy && !IsMine )
            {
                return;
            }

            if ( impaledByTransform == null && Physics.Raycast( new Vector3( X, num, 0f ), Vector3.down, out RaycastHit raycastHit, 25f, groundLayer ) )
            {
                Block component = raycastHit.collider.GetComponent<Block>();
                if ( component != null )
                {
                    if ( raycastHit.distance < 10f && ( IsMine || IsEnemy ) )
                    {
                        component.CheckForMine();
                    }
                }
            }

            if ( impaledByTransform == null && Physics.Raycast( new Vector3( X - 3f, num, 0f ), Vector3.down, out RaycastHit raycastHit2, 25f, groundLayer ) )
            {
                Block component2 = raycastHit2.collider.GetComponent<Block>();
                if ( component2 != null )
                {
                    if ( raycastHit2.distance < 10f && ( IsMine || IsEnemy ) )
                    {
                        component2.CheckForMine();
                    }
                }
            }

            if ( impaledByTransform == null && Physics.Raycast( new Vector3( X + 3f, num, 0f ), Vector3.down, out RaycastHit raycastHit3, 25f, groundLayer ) )
            {
                Block component3 = raycastHit3.collider.GetComponent<Block>();
                if ( component3 != null )
                {
                    if ( raycastHit3.distance < 10f && ( IsMine || IsEnemy ) )
                    {
                        component3.CheckForMine();
                    }
                }
            }
        }

        protected override void CheckRescues()
        {
        }

        protected override void CheckDashing()
        {
            if ( pilotted )
            {
                base.CheckDashing();
            }
        }

        protected override void CheckInput()
        {
            // Don't accept input unless pilot is present
            if ( pilotted )
            {
                base.CheckInput();
                if ( pilotIsFuribrosa && doubleTapSwitch && down && !wasDown && actionState != ActionState.ClimbingLadder )
                {
                    if ( Time.realtimeSinceStartup - lastDownPressTime < 0.2f )
                    {
                        StartSwitchingWeapon();
                    }

                    lastDownPressTime = Time.realtimeSinceStartup;
                }

                // Honk if flexing
                if ( isHero && buttonGesture )
                {
                    if ( !vehicleHornAudio.isPlaying )
                    {
                        vehicleHornAudio.enabled = true;
                        vehicleHornAudio.volume = 1f;
                        vehicleHornAudio.clip = vehicleHornLong;
                        vehicleHornAudio.Play();

                        hornTimer = 1f;
                    }
                }
            }
            else
            {
                ClearAllInput();
            }
        }
        #endregion

        #region Animation
        protected override void ChangeFrame()
        {
            if ( currentFuriosaState == FuriosaState.InVehicle )
            {
                if ( pilotted && pilotIsFuribrosa )
                {
                    sprite.SetLowerLeftPixel( 2 * spritePixelWidth, spritePixelHeight );
                }
                else
                {
                    // Show empty vehicle for non-Furibrosa pilots or when empty
                    sprite.SetLowerLeftPixel( 0, spritePixelHeight );
                }
            }
        }

        protected void EnableSprites()
        {
            sprite.meshRender.enabled = true;
            wheelsSprite.meshRender.enabled = true;
            bumperSprite.meshRender.enabled = true;
            longSmokestacksSprite.meshRender.enabled = true;
            shortSmokestacksSprite.meshRender.enabled = true;
            frontSmokestacksSprite.meshRender.enabled = true;
        }

        protected void DisableSprites()
        {
            sprite.meshRender.enabled = false;
            wheelsSprite.meshRender.enabled = false;
            bumperSprite.meshRender.enabled = false;
            longSmokestacksSprite.meshRender.enabled = false;
            shortSmokestacksSprite.meshRender.enabled = false;
            frontSmokestacksSprite.meshRender.enabled = false;
        }

        protected void AnimateWarRig()
        {
            // Animate wheels
            AnimateWheels();

            // Animate smokestacks
            AnimateSmokestacks();

            // Animate special
            AnimateSpecial();

            // Animate dying
            AnimateDying();
        }

        protected void AnimateWheels()
        {
            float currentSpeed = Mathf.Abs( xI );
            if ( currentSpeed > 1f )
            {
                wheelsCounter += ( currentSpeed * t / 66f ) * 0.175f * 1.25f;
                if ( wheelsCounter > 0.03f )
                {
                    wheelsCounter -= 0.03f;
                    ++wheelsFrame;

                    if ( wheelsFrame > 6 )
                    {
                        wheelsFrame = 0;
                    }
                }
            }

            AnimateRunning();
        }

        protected void AnimateSmokestacks()
        {
            float currentSpeed = Mathf.Abs( xI );
            // Animate long and short smokestacks
            if ( smokestackFrame == 0 && currentSpeed > 50f )
            {
                if ( smokestackCooldown > 0f )
                {
                    smokestackCooldown -= ( currentSpeed > 100f ? 2 : 1 ) * t;
                }

                if ( smokestackCooldown <= 0f )
                {
                    // Start smoke puff
                    smokestackCooldown = Random.Range( 0.5f, 1.5f );
                    smokestackFrame = 1;
                    usingBlueFlame = dashing;
                    longSmokestacksSprite.SetLowerLeftPixel( smokestackFrame * spritePixelWidth, dashing ? 256f : 128f );
                    shortSmokestacksSprite.SetLowerLeftPixel( smokestackFrame * spritePixelWidth, dashing ? 256f : 128f );
                }
            }
            else if ( smokestackFrame > 0 )
            {
                smokestackCounter += t;
                if ( smokestackCounter > 0.08f )
                {
                    smokestackCounter -= 0.08f;
                    ++smokestackFrame;

                    if ( smokestackFrame > 7 )
                    {
                        smokestackFrame = 0;
                        longSmokestacksSprite.SetLowerLeftPixel( smokestackFrame * spritePixelWidth, 128f );
                        shortSmokestacksSprite.SetLowerLeftPixel( smokestackFrame * spritePixelWidth, 128f );
                    }
                    else
                    {
                        longSmokestacksSprite.SetLowerLeftPixel( smokestackFrame * spritePixelWidth, usingBlueFlame ? 256f : 128f );
                        shortSmokestacksSprite.SetLowerLeftPixel( smokestackFrame * spritePixelWidth, usingBlueFlame ? 256f : 128f );
                    }
                }
            }
            else
            {
                longSmokestacksSprite.SetLowerLeftPixel( smokestackFrame * spritePixelWidth, 128f );
                shortSmokestacksSprite.SetLowerLeftPixel( smokestackFrame * spritePixelWidth, 128f );
            }

            // Animate front smokestacks
            if ( blueSmokeFrame == 0 && dashing )
            {
                if ( blueSmokeCooldown > 0f )
                {
                    blueSmokeCooldown -= t;
                }

                if ( blueSmokeCooldown <= 0f )
                {
                    // Start smoke puff
                    blueSmokeCooldown = Random.Range( 0.25f, 0.6f );
                    blueSmokeFrame = 1;
                    frontSmokestacksSprite.SetLowerLeftPixel( blueSmokeFrame * spritePixelWidth, 256f );
                }
            }
            else if ( blueSmokeFrame > 0 )
            {
                blueSmokeCounter += t;
                if ( blueSmokeCounter > 0.08f )
                {
                    blueSmokeCounter -= 0.08f;
                    ++blueSmokeFrame;

                    if ( blueSmokeFrame > 3 )
                    {
                        blueSmokeFrame = 0;
                        frontSmokestacksSprite.SetLowerLeftPixel( 0f, 128f );
                    }
                    else
                    {
                        frontSmokestacksSprite.SetLowerLeftPixel( blueSmokeFrame * spritePixelWidth, 256f );
                    }
                }
            }
            else
            {
                if ( blueSmokeCooldown > 0f )
                {
                    blueSmokeCooldown -= t;
                }

                frontSmokestacksSprite.SetLowerLeftPixel( 0f, 128f );
            }
        }

        protected override void AnimateSpecial()
        {
            if ( usingSpecial )
            {
                specialCounter += t;
                if ( specialCounter > 0.12f )
                {
                    specialCounter -= 0.12f;
                    ++specialFrame;

                    if ( specialFrame < 3 )
                    {
                        bumperSprite.SetLowerLeftPixel( specialFrame * spritePixelWidth, 2 * spritePixelHeight );
                    }
                    else
                    {
                        if ( specialFrame == 5 )
                        {
                            PlayHarpoonFireSound();
                        }
                        else if ( specialFrame == 6 )
                        {
                            UseSpecial();
                        }
                        else if ( specialFrame == 7 )
                        {
                            usingSpecial = false;
                            specialFrame = 3;
                        }
                        // Pause for a few frames before firing
                        else
                        {
                            bumperSprite.SetLowerLeftPixel( 3 * spritePixelWidth, 2 * spritePixelHeight );
                        }
                    }
                }
            }
            // Close bumper after firing
            else if ( specialFrame > 0 )
            {
                specialCounter += t;
                if ( specialCounter > 0.13f )
                {
                    specialCounter -= 0.13f;
                    --specialFrame;

                    if ( specialFrame != 0 )
                    {
                        bumperSprite.SetLowerLeftPixel( specialFrame * spritePixelWidth, 2 * spritePixelHeight );
                    }
                    else
                    {
                        bumperSprite.SetLowerLeftPixel( 0f, spritePixelHeight );
                    }
                }
            }
        }

        protected void AnimateDying()
        {
            // Handle spawning smoke and flashing red when close to death
            if ( health > 0 && deathCount >= 2 )
            {
                smokeCounter += t;
                if ( smokeCounter >= 0.1334f )
                {
                    smokeCounter -= 0.1334f;
                    EffectsController.CreateBlackPlumeParticle( X - 8f + Random.value * 16f, Y + 11f + Random.value * 2f, 3f, 20f, 0f, 60f, 2f, 1f );
                    EffectsController.CreateSparkShower( X - 6f + Random.value * 12f, Y + 11f + Random.value * 4f, 1, 2f, 100f, xI - 20f + Random.value * 40f, 100f, 0.5f, 1f );
                }
            }

            if ( deathCount > 2 )
            {
                deathCountdownCounter += t * ( 1f + Mathf.Clamp( deathCountdown / ( float )deathCountdownExplodeThreshold * 4f, 0f, 4f ) );
                if ( deathCountdownCounter >= 0.4667f )
                {
                    deathCountdownCounter -= 0.2667f;
                    deathCountdown += 1f;
                    EffectsController.CreateBlackPlumeParticle( X - 8f + Random.value * 16f, Y + 4f + Random.value * 2f, 3f, 20f, 0f, 60f, 2f, 1f );
                    if ( deathCountdown % 2f == 1f )
                    {
                        SetHurtMaterial();
                        float num = deathCountdown / ( float )deathCountdownExplodeThreshold * 1f;
                        PlaySpecial3Sound( 0.2f + 0.2f * num, 0.8f + 2f * num );
                    }
                    else
                    {
                        SetUnhurtMaterial();
                    }

                    if ( deathCountdown >= ( float )deathCountdownExplodeThreshold )
                    {
                        SetUnhurtMaterial();
                        Gib( DamageType.OutOfBounds, xI, yI + 150f );
                    }
                }
            }
        }

        // Set materials to default colors
        protected virtual void SetUnhurtMaterial()
        {
            sprite.meshRender.material.SetColor( "_TintColor", Color.gray );
            wheelsSprite.meshRender.material.SetColor( "_TintColor", Color.gray );
            bumperSprite.meshRender.material.SetColor( "_TintColor", Color.gray );
            longSmokestacksSprite.meshRender.material.SetColor( "_TintColor", Color.gray );
            shortSmokestacksSprite.meshRender.material.SetColor( "_TintColor", Color.gray );
            frontSmokestacksSprite.meshRender.material.SetColor( "_TintColor", Color.gray );
        }

        // Set materials to red tint
        protected virtual void SetHurtMaterial()
        {
            sprite.meshRender.material.SetColor( "_TintColor", Color.red );
            wheelsSprite.meshRender.material.SetColor( "_TintColor", Color.red );
            bumperSprite.meshRender.material.SetColor( "_TintColor", Color.red );
            longSmokestacksSprite.meshRender.material.SetColor( "_TintColor", Color.red );
            shortSmokestacksSprite.meshRender.material.SetColor( "_TintColor", Color.red );
            frontSmokestacksSprite.meshRender.material.SetColor( "_TintColor", Color.red );
        }

        protected override void AnimateRunning()
        {
            wheelsSprite.SetLowerLeftPixel( wheelsFrame * spritePixelWidth, spritePixelHeight * 2 );
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

        protected override void CreateGibs( float xI, float yI )
        {
            xI = xI * 0.25f;
            yI = yI * 0.25f + 60f;
            float xForce = 10f;
            float yForce = 10f;
            if ( gibs == null || gibs.transform == null )
            {
                return;
            }

            for ( int i = 0; i < gibs.transform.childCount; i++ )
            {
                Transform child = gibs.transform.GetChild( i );
                if ( child != null )
                {
                    EffectsController.CreateGib( child.GetComponent<Gib>(), GetComponent<Renderer>().sharedMaterial, X, Y, xForce * ( 0.8f + Random.value * 0.4f ), yForce * ( 0.8f + Random.value * 0.4f ), xI, yI, ( int )transform.localScale.x );
                }
            }
        }
        #endregion

        #region SoundEffects
        public void RunAudio()
        {
            float currentSpeed = Mathf.Abs( xI );

            // Play engine idle
            if ( !vehicleEngineAudio.isPlaying )
            {
                vehicleEngineAudio.volume = vehicleEngineVolume;
                vehicleEngineAudio.clip = vehicleIdleLoop;
                vehicleEngineAudio.loop = true;
                vehicleEngineAudio.Play();
            }

            // Control engine pitch
            if ( !reachedStartingPoint || keepGoingBeyondTarget )
            {
                vehicleEngineAudio.pitch = 1.3f;
            }
            else
            {
                vehicleEngineAudio.pitch = Mathf.Lerp( vehicleEngineAudio.pitch, currentSpeed / 250f + 1f, t * 2 );
            }

            // Control vehicle horn
            if ( hornTimer > 0f && ( !buttonGesture || releasedHorn ) )
            {
                if ( !buttonGesture )
                {
                    releasedHorn = true;
                }

                hornTimer -= t;

                vehicleHornAudio.volume = hornTimer;

                if ( hornTimer <= 0 )
                {
                    releasedHorn = false;
                    vehicleHornAudio.Stop();
                    vehicleHornAudio.enabled = false;
                }
            }
        }

        public void PlayRevSound()
        {
            Sound.GetInstance().PlaySoundEffectAt( vehicleRev, 0.6f, transform.position, 1f, true, false, true, 0f );
        }

        public void PlayRunOverUnitSound()
        {
            Sound.GetInstance().PlaySoundEffectAt( vehicleHit, 0.25f, transform.position, 1f, true, false, false, 0f );
        }

        public void PlayHarpoonFireSound()
        {
            Sound.GetInstance().PlaySoundEffectAt( harpoonFire, 0.55f, transform.position, 1f, true, false, true, 0f );
        }

        protected override void PlayHitSound( float v = 0.4F )
        {
        }

        protected override void PlayDeathSound()
        {
        }

        protected override void PlayDeathGargleSound()
        {
        }
        #endregion

        #region Collision
        // Overridden to prevent vehicle from being teleported around like a player
        protected new void ConstrainSpeedToSidesOfScreen()
        {
            if ( !IsHero || !reachedStartingPoint || keepGoingBeyondTarget )
            {
                return;
            }

            if ( X >= screenMaxX - 8f && ( xI > 0f || xIBlast > 0f ) )
            {
                xI = ( xIBlast = 0f );
            }

            if ( X <= screenMinX + 8f && ( xI < 0f || xIBlast < 0f ) )
            {
                xI = ( xIBlast = 0f );
            }

            if ( X < screenMinX - 30f && TriggerManager.DestroyOffscreenPlayers && IsMine )
            {
                Gib( DamageType.OutOfBounds, 840f, yI + 50f );
            }

            if ( SortOfFollow.GetFollowMode() == CameraFollowMode.MapExtents && Y >= screenMaxY - collisionHeadHeight && yI > 0f )
            {
                Y = screenMaxY - collisionHeadHeight;
                yI = 0f;
            }

            if ( Y < screenMinY - 30f )
            {
                if ( TriggerManager.DestroyOffscreenPlayers && IsMine )
                {
                    Gib( DamageType.OutOfBounds, xI, 840f );
                }

                belowScreenCounter += t;
                if ( isHero && belowScreenCounter > 2f && HeroController.CanLookForReposition() )
                {
                    float x = X;
                    float y = Y;
                    if ( Map.FindLadderNearPosition( ( screenMaxX + screenMinX ) / 2f, screenMinY, ref x, ref y ) )
                    {
                        SetXY( x, y );
                        holdUpTime = 0.3f;
                        yI = 150f;
                        xI = 0f;
                        ShowStartBubble();
                        if ( !GameModeController.IsDeathMatchMode && GameModeController.GameMode != GameMode.BroDown )
                        {
                            SetInvulnerable( 2f, false, false );
                        }
                    }

                    int num = 1;
                    if ( Map.FindHoleToJumpThroughAndAppear( ( screenMaxX + screenMinX ) / 2f, screenMinY, ref x, ref y, ref num ) )
                    {
                        SetXY( x, y );
                        if ( num > 0 )
                        {
                            holdRightTime = 0.3f;
                        }
                        else
                        {
                            holdLeftTime = 0.3f;
                        }

                        yI = 240f;
                        xI = ( float )( num * 70 );
                        ShowStartBubble();
                        if ( !GameModeController.IsDeathMatchMode && GameModeController.GameMode != GameMode.BroDown )
                        {
                            SetInvulnerable( 2f, false, false );
                        }
                    }

                    belowScreenCounter -= 0.5f;
                }
            }
            else
            {
                belowScreenCounter = 0f;
            }
        }

        // Overridden to use different head height
        public override float CalculateCeilingHeight()
        {
            ceilingHeight = 1000f;
            if ( Physics.Raycast( new Vector3( X, Y + 1f, 0f ), Vector3.up, out raycastHit, collisionHeadHeight + 400f, groundLayer ) && raycastHit.point.y < ceilingHeight )
            {
                ceilingHeight = raycastHit.point.y;
            }

            if ( Physics.Raycast( new Vector3( X + halfWidth, Y + 1f, 0f ), Vector3.up, out raycastHit, collisionHeadHeight + 400f, groundLayer ) && raycastHit.point.y < ceilingHeight )
            {
                ceilingHeight = raycastHit.point.y;
            }

            if ( Physics.Raycast( new Vector3( X - halfWidth, Y + 1f, 0f ), Vector3.up, out raycastHit, collisionHeadHeight + 400f, groundLayer ) && raycastHit.point.y < ceilingHeight )
            {
                ceilingHeight = raycastHit.point.y;
            }

            return ceilingHeight;
        }

        // Rewritten completely to support larger hitboxes
        protected override bool ConstrainToCeiling( ref float yIT )
        {
            // Disable collision until we've reached start
            if ( !reachedStartingPoint || keepGoingBeyondTarget )
            {
                return false;
            }

            if ( actionState == ActionState.Dead )
            {
                headHeight = deadHeadHeight;
                waistHeight = deadWaistHeight;
            }

            bool result = false;
            chimneyFlipConstrained = false;
            if ( yI >= 0f || WallDrag )
            {
                if ( transform.localScale.x > 0 )
                {
                    // Check top middle of vehicle left to right
                    Vector3 topLeft = new Vector3( X - distanceToBack, Y + collisionHeadHeight, 0f );
                    Vector3 topRight = new Vector3( X + distanceToFront, Y + collisionHeadHeight, 0f );
                    //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                    if ( Physics.Raycast( topLeft, Vector3.right, out raycastHit, Mathf.Abs( topRight.x - topLeft.x ), groundLayer ) && raycastHit.point.y < Y + collisionHeadHeight + yIT )
                    {
                        result = true;
                        HitCeiling( raycastHit );
                    }

                    if ( !result )
                    {
                        // Check top middle of vehicle right to left
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                        if ( Physics.Raycast( topRight, Vector3.left, out raycastHit, Mathf.Abs( topRight.x - topLeft.x ), groundLayer ) && raycastHit.point.y < Y + collisionHeadHeight + yIT )
                        {
                            result = true;
                            HitCeiling( raycastHit );
                        }
                    }

                    // Check front of vehicle left to right
                    if ( !result )
                    {
                        topLeft = new Vector3( X + distanceToFront, Y + frontHeadHeight, 0f );
                        topRight = new Vector3( X + halfWidth - 1f, Y + frontHeadHeight, 0f );
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if ( Physics.Raycast( topLeft, Vector3.right, out raycastHit, Mathf.Abs( topRight.x - topLeft.x ), groundLayer ) && raycastHit.point.y < Y + frontHeadHeight + yIT )
                        {
                            result = true;
                            HitCeiling( raycastHit, frontHeadHeight );
                        }
                    }

                    // Check front of vehicle right to left
                    if ( !result )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if ( Physics.Raycast( topRight, Vector3.left, out raycastHit, Mathf.Abs( topRight.x - topLeft.x ), groundLayer ) && raycastHit.point.y < Y + frontHeadHeight + yIT )
                        {
                            result = true;
                            HitCeiling( raycastHit, frontHeadHeight );
                        }
                    }
                }
                else
                {
                    // Check top middle of vehicle left to right
                    Vector3 topLeft = new Vector3( X - distanceToFront, Y + collisionHeadHeight, 0f );
                    Vector3 topRight = new Vector3( X + distanceToBack, Y + collisionHeadHeight, 0f );
                    //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                    if ( Physics.Raycast( topLeft, Vector3.right, out raycastHit, Mathf.Abs( topRight.x - topLeft.x ), groundLayer ) && raycastHit.point.y < Y + collisionHeadHeight + yIT )
                    {
                        result = true;
                        HitCeiling( raycastHit );
                    }

                    if ( !result )
                    {
                        // Check top middle of vehicle right to left
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                        if ( Physics.Raycast( topRight, Vector3.left, out raycastHit, Mathf.Abs( topRight.x - topLeft.x ), groundLayer ) && raycastHit.point.y < Y + collisionHeadHeight + yIT )
                        {
                            result = true;
                            HitCeiling( raycastHit );
                        }
                    }

                    // Check front of vehicle left to right
                    if ( !result )
                    {
                        topLeft = new Vector3( X - halfWidth + 1f, Y + frontHeadHeight, 0f );
                        topRight = new Vector3( X - distanceToFront, Y + frontHeadHeight, 0f );
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if ( Physics.Raycast( topLeft, Vector3.right, out raycastHit, Mathf.Abs( topRight.x - topLeft.x ), groundLayer ) && raycastHit.point.y < Y + frontHeadHeight + yIT )
                        {
                            result = true;
                            HitCeiling( raycastHit, frontHeadHeight );
                        }
                    }

                    // Check front of vehicle right to left
                    if ( !result )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if ( Physics.Raycast( topRight, Vector3.left, out raycastHit, Mathf.Abs( topRight.x - topLeft.x ), groundLayer ) && raycastHit.point.y < Y + frontHeadHeight + yIT )
                        {
                            result = true;
                            HitCeiling( raycastHit, frontHeadHeight );
                        }
                    }
                }
            }

            return result;
        }

        // Allows using a specific height
        protected void HitCeiling( RaycastHit ceilingHit, float customHeight )
        {
            if ( up || buttonJump )
            {
                ceilingHit.collider.SendMessage( "StepOn", this, SendMessageOptions.DontRequireReceiver );
            }

            yIT = ceilingHit.point.y - customHeight - Y;
            if ( !chimneyFlip && yI > 100f && ceilingHit.collider != null )
            {
                currentFootStepGroundType = ceilingHit.collider.tag;
                PlayFootStepSound( 0.2f, 0.6f );
            }

            yI = 0f;
            jumpTime = 0f;
            if ( ( canCeilingHang && CanCheckClimbAlongCeiling() && ( up || buttonJump ) ) || hangGrace > 0f )
            {
                StartHanging();
            }

            //RocketLib.Utils.DrawDebug.DrawLine("ceillingHit", ceilingHit.point, ceilingHit.point + new Vector3(5f, 0f, 0f), Color.green);
        }

        // Overridden to use new head height
        protected override void HitCeiling( RaycastHit ceilingHit )
        {
            HitCeiling( ceilingHit, collisionHeadHeight );

            // DEBUG
            //RocketLib.Utils.DrawDebug.DrawLine("ceillingHit", ceilingHit.point, ceilingHit.point + new Vector3(3f, 0f, 0f), Color.green);
        }

        // Rewritten completely to support larger hitboxes
        protected override bool ConstrainToWalls( ref float yIT, ref float xIT )
        {
            // Disable collision until we've reached start
            if ( !reachedStartingPoint || keepGoingBeyondTarget )
            {
                return false;
            }

            if ( !dashing || ( left && xIBlast > 0f ) || ( right && xIBlast < 0f ) || ( !left && !right && Mathf.Abs( xIBlast ) > 0f ) )
            {
                xIBlast *= 1f - t * 4f;
            }

            pushingTime -= t;
            canTouchRightWalls = false;
            canTouchLeftWalls = false;
            wasConstrainedLeft = constrainedLeft;
            wasConstrainedRight = constrainedRight;
            constrainedLeft = false;
            constrainedRight = false;
            //this.ConstrainToFragileBarriers(ref xIT, this.halfWidth);
            //this.ConstrainToMookBarriers(ref xIT, this.halfWidth);
            row = ( int )( ( Y + 16f ) / 16f );
            collumn = ( int )( ( X + 8f ) / 16f );
            wasLedgeGrapple = ledgeGrapple;
            ledgeGrapple = false;

            if ( transform.localScale.x > 0 )
            {
                // Check front of vehicle
                Vector3 bottomRight = new Vector3( X + halfWidth, Y, 0 );
                Vector3 topRight = new Vector3( X + halfWidth, Y + frontHeadHeight, 0 );
                //RocketLib.Utils.DrawDebug.DrawLine("wall", bottomRight, topRight, Color.red);
                if ( Physics.Raycast( bottomRight, Vector3.up, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ), groundLayer ) && raycastHitWalls.point.x < X + halfWidth + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x - 2f, raycastHitWalls.point.y, 0f ), Vector3.right, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x < X + halfWidth + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X + halfWidth );
                        return true;
                    }
                }

                if ( Physics.Raycast( topRight, Vector3.down, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ) - 0.5f, groundLayer ) && raycastHitWalls.point.x < X + halfWidth + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x - 2f, raycastHitWalls.point.y, 0f ), Vector3.right, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x < X + halfWidth + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X + halfWidth );
                        return true;
                    }
                }

                // Check top front of vehicle
                bottomRight = new Vector3( X + distanceToFront, Y + frontHeadHeight, 0 );
                topRight = new Vector3( X + distanceToFront, Y + collisionHeadHeight, 0 );
                //RocketLib.Utils.DrawDebug.DrawLine("wall2", bottomRight, topRight, Color.red);
                if ( Physics.Raycast( bottomRight, Vector3.up, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ), groundLayer ) && raycastHitWalls.point.x < X + distanceToFront + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x - 2f, raycastHitWalls.point.y, 0f ), Vector3.right, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x < X + distanceToFront + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X + ( halfWidth - distanceToFront ) );
                        return true;
                    }
                }

                if ( Physics.Raycast( topRight, Vector3.down, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ), groundLayer ) && raycastHitWalls.point.x < X + distanceToFront + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x - 2f, raycastHitWalls.point.y, 0f ), Vector3.right, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x < X + distanceToFront + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X + ( halfWidth - distanceToFront ) );
                        return true;
                    }
                }
            }
            else
            {
                // Check front of vehicle
                Vector3 bottomRight = new Vector3( X - halfWidth, Y, 0 );
                Vector3 topRight = new Vector3( X - halfWidth, Y + frontHeadHeight, 0 );
                //RocketLib.Utils.DrawDebug.DrawLine("wall", bottomRight, topRight, Color.red);
                if ( Physics.Raycast( bottomRight, Vector3.up, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ), groundLayer ) && raycastHitWalls.point.x > X - halfWidth + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x + 2f, raycastHitWalls.point.y, 0f ), Vector3.left, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x > X - halfWidth + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X - halfWidth );
                        return true;
                    }
                }

                if ( Physics.Raycast( topRight, Vector3.down, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ) - 0.5f, groundLayer ) && raycastHitWalls.point.x > X - halfWidth + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x + 2f, raycastHitWalls.point.y, 0f ), Vector3.left, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x > X - halfWidth + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X - halfWidth );
                        return true;
                    }
                }

                // Check top front of vehicle
                bottomRight = new Vector3( X - distanceToFront, Y + frontHeadHeight, 0 );
                topRight = new Vector3( X - distanceToFront, Y + collisionHeadHeight, 0 );
                //RocketLib.Utils.DrawDebug.DrawLine("wall2", bottomRight, topRight, Color.red);
                if ( Physics.Raycast( bottomRight, Vector3.up, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ), groundLayer ) && raycastHitWalls.point.x > X - distanceToFront + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x + 2f, raycastHitWalls.point.y, 0f ), Vector3.left, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x > X - distanceToFront + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X - ( halfWidth - distanceToFront ) );
                        return true;
                    }
                }

                if ( Physics.Raycast( topRight, Vector3.down, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ), groundLayer ) && raycastHitWalls.point.x > X - distanceToFront + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x + 2f, raycastHitWalls.point.y, 0f ), Vector3.left, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x > X - distanceToFront + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X - ( halfWidth - distanceToFront ) );
                        return true;
                    }
                }
            }

            // Check back of vehicle if being pushed in a direction we're not facing
            if ( transform.localScale.x < 0 && xIBlast > 0.01 )
            {
                // Check front of vehicle
                float backWidth = HalfWidth - 2f;
                Vector3 bottomRight = new Vector3( X + backWidth, Y, 0 );
                Vector3 topRight = new Vector3( X + backWidth, Y + collisionHeadHeight, 0 );
                //RocketLib.Utils.DrawDebug.DrawLine("wall", bottomRight, topRight, Color.red);
                if ( Physics.Raycast( bottomRight, Vector3.up, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ), groundLayer ) && raycastHitWalls.point.x < X + backWidth + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x - 2f, raycastHitWalls.point.y, 0f ), Vector3.right, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x < X + backWidth + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X + backWidth );
                        return true;
                    }
                }

                if ( Physics.Raycast( topRight, Vector3.down, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ) - 0.5f, groundLayer ) && raycastHitWalls.point.x < X + backWidth + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x - 2f, raycastHitWalls.point.y, 0f ), Vector3.right, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x < X + backWidth + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X + backWidth );
                        return true;
                    }
                }
            }
            else if ( transform.localScale.x > 0 && xIBlast < -0.01 )
            {
                // Check front of vehicle
                float backWidth = HalfWidth - 2f;
                Vector3 bottomRight = new Vector3( X - backWidth, Y, 0 );
                Vector3 topRight = new Vector3( X - backWidth, Y + collisionHeadHeight, 0 );
                //RocketLib.Utils.DrawDebug.DrawLine("wall", bottomRight, topRight, Color.red);
                if ( Physics.Raycast( bottomRight, Vector3.up, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ), groundLayer ) && raycastHitWalls.point.x > X - backWidth + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x + 2f, raycastHitWalls.point.y, 0f ), Vector3.left, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x > X - backWidth + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X - backWidth );
                        return true;
                    }
                }

                if ( Physics.Raycast( topRight, Vector3.down, out raycastHitWalls, Mathf.Abs( topRight.y - bottomRight.y ) - 0.5f, groundLayer ) && raycastHitWalls.point.x > X - backWidth + xIT )
                {
                    if ( ( Physics.Raycast( new Vector3( bottomRight.x + 2f, raycastHitWalls.point.y, 0f ), Vector3.left, out raycastHitWalls, 10f, groundLayer ) && raycastHitWalls.point.x > X - backWidth + xIT ) )
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.blue);
                        xI = 0f;
                        xIT = raycastHitWalls.point.x - ( X - backWidth );
                        return true;
                    }
                }
            }


            return false;
        }

        // Overridden to use new CanTouchGround
        protected override bool CanJumpOffGround()
        {
            return ( groundTransform != null && actionState != ActionState.Jumping ) || ( CanTouchGround( ( float )( ( !right || canTouchLeftWalls || Physics.Raycast( new Vector3( X, Y + 5f, 0f ), Vector3.left, out raycastHitWalls, 13.5f, groundLayer ) ) ? 0 : -13 ) + ( float )( ( !left || canTouchRightWalls || Physics.Raycast( new Vector3( X, Y + 5f, 0f ), Vector3.right, out raycastHitWalls, 13.5f, groundLayer ) ) ? 0 : 13 ) * ( ( !isInQuicksand ) ? 1f : 0.4f ) ) );
        }

        // Overridden to use feetWidth rather than a hard-coded value
        protected new bool CanTouchGround( float xOffset )
        {
            LayerMask mask = GetGroundLayer();
            if ( Physics.Raycast( new Vector3( X, Y + 14f, 0f ), Vector3.down, out RaycastHit raycastHit, 16f, mask ) )
            {
                SetCurrentFootstepSound( raycastHit.collider );
                return true;
            }

            if ( Physics.Raycast( new Vector3( X - feetWidth, Y + 14f, 0f ), Vector3.down, out raycastHit, 16f, mask ) )
            {
                SetCurrentFootstepSound( raycastHit.collider );
                return true;
            }

            if ( Physics.Raycast( new Vector3( X + feetWidth, Y + 14f, 0f ), Vector3.down, out raycastHit, 16f, mask ) )
            {
                SetCurrentFootstepSound( raycastHit.collider );
                return true;
            }

            if ( xOffset != 0f && Physics.Raycast( new Vector3( X + xOffset, Y + 12f, 0f ), Vector3.down, out raycastHit, 15f, mask ) )
            {
                SetCurrentFootstepSound( raycastHit.collider );
                return true;
            }

            if ( !Map.IsBlockLadder( X, Y ) && !down )
            {
                if ( Physics.Raycast( new Vector3( X, Y + 14f, 0f ), Vector3.down, out raycastHit, 16f, ladderLayer ) )
                {
                    SetCurrentFootstepSound( raycastHit.collider );
                    return true;
                }

                if ( !Map.IsBlockLadder( X - 3f, Y ) && !down && Physics.Raycast( new Vector3( X - 3f, Y + 14f, 0f ), Vector3.down, out raycastHit, 16f, ladderLayer ) )
                {
                    SetCurrentFootstepSound( raycastHit.collider );
                    return true;
                }

                if ( !Map.IsBlockLadder( X, Y ) && !down && Physics.Raycast( new Vector3( X - 3f, Y + 14f, 0f ), Vector3.down, out raycastHit, 16f, ladderLayer ) )
                {
                    SetCurrentFootstepSound( raycastHit.collider );
                    return true;
                }

                if ( xOffset != 0f && !Map.IsBlockLadder( X + xOffset, Y ) && !down && Physics.Raycast( new Vector3( X + xOffset, Y + 12f, 0f ), Vector3.down, out raycastHit, 15f, ladderLayer ) )
                {
                    SetCurrentFootstepSound( raycastHit.collider );
                    return true;
                }
            }

            return false;
        }

        protected override void Land()
        {
            if ( ( !isHero && yI < fallDamageHurtSpeed ) || ( isHero && yI < fallDamageHurtSpeedHero ) )
            {
                if ( ( !isHero && yI < fallDamageDeathSpeed ) || ( isHero && yI < fallDamageDeathSpeedHero ) )
                {
                    crushingGroundLayers = 2;
                    SortOfFollow.Shake( 0.3f );
                    EffectsController.CreateGroundWave( X, Y, 96f );
                    Map.ShakeTrees( X, Y, 144f, 64f, 128f );
                }
                else
                {
                    if ( isHero )
                    {
                        if ( yI <= fallDamageDeathSpeedHero )
                        {
                            crushingGroundLayers = 2;
                        }
                        else if ( yI < fallDamageHurtSpeedHero * 0.3f + fallDamageDeathSpeedHero * 0.7f )
                        {
                            crushingGroundLayers = 1;
                        }
                        else if ( yI < ( fallDamageHurtSpeedHero + fallDamageDeathSpeedHero ) / 2f )
                        {
                            crushingGroundLayers = 0;
                        }
                    }
                    else if ( yI < ( fallDamageHurtSpeed + fallDamageDeathSpeed ) / 2f )
                    {
                        crushingGroundLayers = 2;
                    }

                    SortOfFollow.Shake( 0.3f );
                    EffectsController.CreateGroundWave( X, Y, 80f );
                    Map.ShakeTrees( X, Y, 144f, 64f, 128f );
                }
            }
            else if ( crushingGroundLayers > 0 )
            {
                crushingGroundLayers--;
                SortOfFollow.Shake( 0.3f );
                Map.ShakeTrees( X, Y, 80f, 48f, 100f );
            }
            else if ( yI < -60f && health > 0 )
            {
                PlayFootStepSound( soundHolderFootSteps.landMetalSounds, 0.55f, 0.9f );
                gunFrame = 0;
                gunCounter = 0f;
                EffectsController.CreateGroundWave( X, Y + 10f, 64f );
                SortOfFollow.Shake( 0.2f );
            }
            else if ( health > 0 )
            {
                PlayFootStepSound( soundHolderFootSteps.landMetalSounds, 0.35f, 0.9f );
                SortOfFollow.Shake( 0.1f );
                gunFrame = 0;
                gunCounter = 0f;
            }

            jumpingMelee = false;
            timesKickedByVanDammeSinceLanding = 0;
            if ( health > 0 && playerNum >= 0 && yI < -150f )
            {
                Map.BotherNearbyMooks( X, Y, 24f, 16f, playerNum );
            }

            FallDamage( yI );
            StopAirDashing();
            lastLandTime = Time.realtimeSinceStartup;
            if ( yI < 0f && health > 0 && groundHeight > Y - 2f + yIT && yI < -70f )
            {
                EffectsController.CreateLandPoofEffect( X, groundHeight, ( Mathf.Abs( xI ) >= 30f ) ? ( -( int )transform.localScale.x ) : 0, GetFootPoofColor() );
            }

            if ( health > 0 )
            {
                if ( ( left || right ) && ( !left || !right ) )
                {
                    if ( xI > 0f )
                    {
                        xI += 100f;
                    }

                    if ( xI < 0f )
                    {
                        xI -= 100f;
                    }

                    actionState = ActionState.Running;
                    if ( delayedDashing || ( dashing && Time.time - leftTapTime > minDashTapTime && Time.time - rightTapTime > minDashTapTime ) )
                    {
                        StartDashing();
                    }

                    hasDashedInAir = false;
                    if ( useNewFrames )
                    {
                        if ( CanDoRollOnLand() )
                        {
                            RollOnLand();
                        }

                        counter = 0f;
                        AnimateRunning();
                        if ( !FluidController.IsSubmerged( this ) && groundHeight > Y - 8f )
                        {
                            EffectsController.CreateFootPoofEffect( X, groundHeight + 1f, 0f, Vector3.up * 1f, BloodColor.None );
                        }
                    }
                }
                else
                {
                    StopRolling();
                    SetActionstateToIdle();
                }
            }

            if ( yI < -50f )
            {
                if ( Physics.Raycast( new Vector3( X, Y + 5f, 0f ), Vector3.down, out raycastHit, 12f, groundLayer | Map.platformLayer ) )
                {
                    raycastHit.collider.SendMessage( "StepOn", this, SendMessageOptions.DontRequireReceiver );
                }

                if ( Physics.Raycast( new Vector3( X + 6f, Y + 5f, 0f ), Vector3.down, out raycastHit, 12f, groundLayer | Map.platformLayer ) )
                {
                    raycastHit.collider.SendMessage( "StepOn", this, SendMessageOptions.DontRequireReceiver );
                }

                if ( Physics.Raycast( new Vector3( X - 6f, Y + 5f, 0f ), Vector3.down, out raycastHit, 12f, groundLayer | Map.platformLayer ) )
                {
                    raycastHit.collider.SendMessage( "StepOn", this, SendMessageOptions.DontRequireReceiver );
                }
            }

            bool flag = false;
            if ( playerNum >= 0 && yI < -100f )
            {
                flag = PushGrassAway();
            }

            if ( health > 0 && !flag && yI < -100f )
            {
                PlayLandSound();
            }

            if ( bossBlockPieceCurrentlyStandingOn != null )
            {
                bossBlockPieceCurrentlyStandingOn.LandOn( yI );
            }

            if ( blockCurrentlyStandingOn != null )
            {
                blockCurrentlyStandingOn.LandOn( yI );
            }

            yI = 0f;
            if ( groundTransform != null )
            {
                lastParentedToTransform = groundTransform;
            }

            if ( IsParachuteActive )
            {
                IsParachuteActive = false;
            }
        }

        public void SetToGround()
        {
            LayerMask layer = ( 1 << LayerMask.NameToLayer( "Ground" ) ) | ( 1 << LayerMask.NameToLayer( "LargeObjects" ) ) | ( 1 << LayerMask.NameToLayer( "IndestructibleGround" ) );
            if ( Physics.Raycast( new Vector3( transform.position.x, transform.position.y + 14f, 0f ), Vector3.down, out raycastHit, 500f, layer ) )
            {
                SetPosition( raycastHit.point );
            }
        }

        protected override void FallDamage( float yI )
        {
            if ( ( !isHero && yI < fallDamageHurtSpeed ) || ( isHero && yI < fallDamageHurtSpeedHero ) )
            {
                if ( health > 0 )
                {
                    crushingGroundLayers = Mathf.Max( crushingGroundLayers, 2 );
                }

                if ( ( !isHero && yI < fallDamageDeathSpeed ) || ( isHero && yI < fallDamageDeathSpeedHero ) )
                {
                    Map.KnockAndDamageUnit( SingletonMono<MapController>.Instance, this, health + 40, DamageType.Crush, -1f, 450f, 0, false );
                    Map.ExplodeUnits( this, 25, DamageType.Crush, 64f, 25f, X, Y, 200f, 170f, playerNum, false, false, true );
                }
                else
                {
                    Map.KnockAndDamageUnit( SingletonMono<MapController>.Instance, this, health - 10, DamageType.Crush, -1f, 450f, 0, false );
                    Map.ExplodeUnits( this, 10, DamageType.Crush, 48f, 20f, X, Y, 150f, 120f, playerNum, false, false, true );
                }
            }
        }

        protected void RunCrushGround()
        {
            if ( Mathf.Sign( crushMomentum ) != Mathf.Sign( xI ) )
            {
                crushMomentum = Mathf.Sign( xI );
            }

            if ( ( Mathf.Abs( xI ) > Mathf.Abs( crushMomentum ) && crushDamageCooldown <= 0f ) || ( Mathf.Abs( crushMomentum ) > speed && !dashing ) )
            {
                if ( dashing )
                {
                    crushMomentum = Mathf.Lerp( crushMomentum, xI, t * 5 );
                }
                else
                {
                    crushMomentum = Mathf.Lerp( crushMomentum, xI, t * 3 );
                }
            }

            // Crush ground when moving towards player who summoned this vehicle
            if ( !reachedStartingPoint || keepGoingBeyondTarget )
            {
                if ( summonedDirection > 0 )
                {
                    right = true;
                    CrushGroundWhileMoving( 50, crushXRange, crushYRange, crushXOffset, crushYOffset );
                    right = false;
                }
                else
                {
                    left = true;
                    CrushGroundWhileMoving( 50, crushXRange, crushYRange, crushXOffset, crushYOffset );
                    left = false;
                }
            }
            // Crush ground while moving forward
            else if ( crushDamageCooldown <= 0f )
            {
                float currentSpeed = Mathf.Abs( dashing ? xI : crushMomentum );
                bool crushSpeedReached = ( dashing && currentSpeed > 100f );
                int currentGroundDamage = ( int )Mathf.Max( Mathf.Round( ( currentSpeed / 200f ) * ( crushDamage + ( crushSpeedReached ? 10f : 0f ) ) ), 1f );

                if ( CrushGroundWhileMoving( currentGroundDamage, crushXRange, crushYRange, crushXOffset, crushYOffset ) )
                {
                    crushMomentum -= ( ( crushMomentum > 150 ? 6.5f : 5.5f ) * Mathf.Sign( crushMomentum ) );
                    if ( !crushSpeedReached )
                    {
                        crushDamageCooldown = 0.04f;
                        shieldDamage += 0.7f;
                    }
                    else
                    {
                        shieldDamage += 0.5f;
                    }
                }
            }
            else
            {
                crushDamageCooldown -= t;
            }

            // Crush units
            if ( crushUnitCooldown > 0 )
            {
                crushUnitCooldown -= t;

                if ( crushUnitCooldown <= 0 )
                {
                    recentlyHitUnits.Clear();
                }
            }

            if ( Mathf.Abs( xI ) > 20 || dashing )
            {
                float currentSpeed = Mathf.Abs( xI );
                bool crushSpeedReached = ( dashing && currentSpeed > 100f );
                int currentUnitDamage = crushSpeedReached ? 30 : ( int )Mathf.Max( Mathf.Round( currentSpeed / speed * 20 ), 1 );
                if ( CrushUnitsWhileMoving( currentUnitDamage, unitXRange, unitYRange, crushXOffset, crushYOffset, out bool hitHeavy ) )
                {
                    if ( !hitHeavy )
                    {
                        shieldDamage += 8f;
                    }
                    else
                    {
                        shieldDamage += 12f;
                    }
                }
            }
        }

        protected virtual bool CrushGroundWhileMoving( int damageGroundAmount, float xRange, float yRange, float xOffset, float yOffset )
        {
            bool hitGround = false;
            if ( xI < 0 || left )
            {
                // Hit Ground
                // Range extended by 12 when facing left because otherwise we won't hit cages
                hitGround = DamageGround( this, damageGroundAmount, DamageType.Crush, xRange + 12f, yRange, X - xOffset, Y + yOffset );
                if ( Physics.Raycast( new Vector3( X - xOffset, Y + yOffset, 0f ), Vector3.left, out raycastHit, xRange, fragileLayer ) && raycastHit.collider.gameObject.GetComponent<Parachute>() == null )
                {
                    raycastHit.collider.gameObject.SendMessage( "Damage", new DamageObject( 2, DamageType.Crush, xI, yI, raycastHit.point.x, raycastHit.point.y, this ) );
                    hitGround = true;
                }

                // Hit Doodads
                Map.DamageDoodads( 1, DamageType.Crush, X - xOffset, Y + yOffset, xI, yI, 10f, playerNum, out bool _, null );
            }
            else if ( xI > 0 || right )
            {
                // Hit Ground
                hitGround = DamageGround( this, damageGroundAmount, DamageType.Crush, xRange, yRange, X + xOffset, Y + yOffset );
                if ( Physics.Raycast( new Vector3( X + xOffset, Y + yOffset, 0f ), Vector3.right, out raycastHit, xRange, fragileLayer ) && raycastHit.collider.gameObject.GetComponent<Parachute>() == null )
                {
                    raycastHit.collider.gameObject.SendMessage( "Damage", new DamageObject( 2, DamageType.Crush, xI, yI, raycastHit.point.x, raycastHit.point.y, this ) );
                    hitGround = true;
                }

                // Hit Doodads
                Map.DamageDoodads( 1, DamageType.Crush, X + xOffset, Y + yOffset, xI, yI, 10f, playerNum, out bool _, null );
            }
            // DEBUG
            //RocketLib.Utils.DrawDebug.DrawRectangle("ground", new Vector3(base.X + xOffset - xRange / 2f, base.Y + yOffset - yRange / 2f, 0f), new Vector3(base.X + xOffset + xRange / 2f, base.Y + yOffset + yRange / 2f, 0f), Color.red);

            return hitGround;
        }

        public bool DamageGround( MonoBehaviour damageSender, int damage, DamageType damageType, float width, float height, float x, float y )
        {
            bool result = false;
            width += 8f;
            height += 8f;
            Collider[] array = Physics.OverlapSphere( new Vector3( x, y, 0f ), Mathf.Max( width * 2f, height * 2f ) * 0.5f, Map.groundAndDamageableObjects );
            if ( array.Length > 0 )
            {
                foreach ( Collider t1 in array )
                {
                    Vector3 position = t1.transform.position;
                    if ( position.x >= x - width / 2f && position.x <= x + width / 2f && position.y >= y - height / 2f && position.y <= y + height / 2f
                         && !( t1.gameObject.HasComponent<BossBlockWeapon>() || t1.gameObject.HasComponent<BossBlockPiece>() ) )
                    {
                        // Don't damage relays that damage bosses
                        if ( t1.gameObject.HasComponent<DamageRelay>() )
                        {
                            DamageRelay relay = t1.gameObject.GetComponent<DamageRelay>();
                            if ( relay.unit != null && BroMakerUtilities.IsBoss( relay.unit ) )
                            {
                                continue;
                            }
                        }

                        float forceX = 0f;
                        float forceY = 0f;
                        if ( damageSender is Rocket )
                        {
                            Vector3 a = position - damageSender.transform.position;
                            a.Normalize();
                            a *= 40f;
                            forceX = a.x;
                            forceY = a.y;
                        }

                        MapController.Damage_Networked( damageSender, t1.gameObject, damage, damageType, forceX, forceY, x, y );
                        result = true;
                    }
                }
            }

            return result;
        }

        protected virtual bool CrushUnitsWhileMoving( int damageUnitsAmount, float unitsXRange, float unitsYRange, float xOffset, float yOffset, out bool hitHeavy )
        {
            Unit hitUnitsAsThis = pilotUnit ?? summoner;
            // Default to any other player
            if ( hitUnitsAsThis == null )
            {
                for ( int i = 0; i < 4; ++i )
                {
                    if ( HeroController.PlayerIsAlive( i ) )
                    {
                        hitUnitsAsThis = HeroController.players[i].character;
                    }
                }
            }

            float knockback = Mathf.Max( Mathf.Min( Mathf.Abs( xI ), 75f ), 225 );
            hitHeavy = false;
            if ( xI < 0 || left )
            {
                if ( HitUnits( this, hitUnitsAsThis, hitUnitsAsThis.playerNum, damageUnitsAmount, DamageType.GibIfDead, unitsXRange, unitsYRange, X - xOffset, Y + yOffset, -2 * knockback, 4 * knockback, true, true, true, out hitHeavy ) )
                {
                    PlayRunOverUnitSound();
                    crushUnitCooldown = 0.5f;
                    return true;
                }
            }
            else if ( xI > 0 || right )
            {
                if ( HitUnits( this, hitUnitsAsThis, hitUnitsAsThis.playerNum, damageUnitsAmount, DamageType.GibIfDead, unitsXRange, unitsYRange, X + xOffset, Y + yOffset, 2 * knockback, 4 * knockback, true, true, true, out hitHeavy ) )
                {
                    PlayRunOverUnitSound();
                    crushUnitCooldown = 0.5f;
                    return true;
                }
            }

            return false;
        }

        public bool HitUnits( MonoBehaviour damageSender, MonoBehaviour avoidID, int playerNum, int damage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, bool penetrates, bool knock, bool canGib, out bool hitHeavy )
        {
            hitHeavy = false;
            if ( Map.units == null )
            {
                return false;
            }

            bool result = false;
            int num = 999999;
            bool flag = false;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                Unit unit = Map.units[i];
                if ( unit != null && ( GameModeController.DoesPlayerNumDamage( playerNum, unit.playerNum ) || ( unit.playerNum < 0 && unit.CatchFriendlyBullets() ) ) && !unit.invulnerable && unit.health <= num
                     && !recentlyHitUnits.Contains( unit ) && !BroMakerUtilities.IsBoss( unit ) && !( unit is Tank ) )
                {
                    float num2 = unit.X - x;
                    if ( Mathf.Abs( num2 ) - xRange < unit.width )
                    {
                        float num3 = unit.Y + unit.height / 2f + 4f - y;
                        if ( Mathf.Abs( num3 ) - yRange < unit.height && ( avoidID == null || avoidID != unit || unit.CatchFriendlyBullets() ) )
                        {
                            recentlyHitUnits.Add( unit );
                            if ( !penetrates && unit.health > 0 )
                            {
                                num = 0;
                                flag = true;
                            }

                            if ( !canGib && unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, yI, ( int )Mathf.Sign( xI ), knock, x, y, false );
                                if ( unit != null )
                                {
                                    float multiplier = unit.IsHeavy() ? 2.5f : 1f;
                                    unit.Knock( damageType, multiplier * xI, multiplier * yI, true );
                                }
                            }
                            else
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( damage, playerNum ), damageType, xI, yI, ( int )Mathf.Sign( xI ), knock, x, y, false );
                                if ( unit != null )
                                {
                                    float multiplier = unit.IsHeavy() ? 2.5f : 1f;
                                    unit.Knock( damageType, multiplier * xI, multiplier * yI, true );
                                }
                            }

                            if ( unit.IsHeavy() )
                            {
                                hitHeavy = true;
                            }

                            result = true;
                            if ( flag )
                            {
                                return result;
                            }
                        }
                    }
                }
            }

            return result;
        }
        #endregion

        #region Movement
        // Overridden to ignore barbed wire
        protected override float GetSpeed
        {
            get
            {
                if ( player == null )
                {
                    return speed * 1;
                }

                return player.ValueOrchestrator.GetModifiedFloatValue( ValueOrchestrator.ModifiableType.MovementSpeed, speed ) * 1f;
            }
        }

        // Overridden to use different head height
        protected override void CalculateMovement()
        {
            if ( impaledByTransform != null || frozenTime > 0f )
            {
                return;
            }

            if ( CanBeAffectedByWind() && WindController.PositionHasWind( collumn, row ) )
            {
                WindController.AddWindSpeedSafe( collumn, row, ref xIBlast, ref yI );
                if ( IsAlive() && Sandstorm.Instance.IsRaging && SortOfFollow.IsItSortOfVisible( X, Y, 32f, 32f ) && !hasBeenCoverInAcid && Sandstorm.Instance.IsInDeadlySandstorm( X ) )
                {
                    CoverInAcid();
                }
            }

            if ( health <= 0 )
            {
                return;
            }

            if ( fire )
            {
                if ( !wasFire )
                {
                    StartFiring();
                    SetGestureAnimation( GestureElement.Gestures.None );
                }
            }
            else if ( wasFire )
            {
                StopFiring();
            }

            CheckDashing();
            if ( !right && !left && actionState == ActionState.Running )
            {
                if ( actionState != ActionState.ClimbingLadder && actionState != ActionState.Hanging && actionState != ActionState.Jumping )
                {
                    SetActionstateToIdle();
                }

                dashing = false;
            }

            if ( !dashing )
            {
                StopDashing();
            }

            if ( left )
            {
                if ( !wasLeft )
                {
                    if ( !dashing && !right && Time.time - leftTapTime < minDashTapTime )
                    {
                        if ( !dashing )
                        {
                            StartDashing();
                        }

                        dashTime = Time.time;
                    }

                    if ( holdingHighFive && CanAirDash( DirectionEnum.Left ) )
                    {
                        Airdash( true );
                    }
                    else
                    {
                        leftTapTime = Time.time;
                        ClampSpeedPressingLeft();
                        if ( actionState == ActionState.Idle )
                        {
                            actionState = ActionState.Running;
                            AnimateRunning();
                        }
                    }
                }

                if ( !right )
                {
                    AddSpeedLeft();
                }
            }

            if ( right )
            {
                if ( !wasRight )
                {
                    if ( !left && Time.time - rightTapTime < minDashTapTime )
                    {
                        if ( !dashing )
                        {
                            StartDashing();
                        }

                        dashTime = Time.time;
                    }

                    if ( holdingHighFive && CanAirDash( DirectionEnum.Right ) )
                    {
                        Airdash( true );
                    }
                    else
                    {
                        rightTapTime = Time.time;
                        ClampSpeedPressingRight();
                        if ( actionState == ActionState.Idle )
                        {
                            actionState = ActionState.Running;
                            AnimateRunning();
                        }
                    }
                }

                if ( !left )
                {
                    AddSpeedRight();
                }
            }
            else if ( wasRight )
            {
                if ( !left && !doingMelee && actionState != ActionState.ClimbingLadder && actionState != ActionState.Hanging && actionState != ActionState.Jumping )
                {
                    SetActionstateToIdle();
                }

                dashing = false;
            }

            if ( !left && !right && IsHero && Y < groundHeight + 1f && yI <= 0f )
            {
                DontSlipOverEdges();
            }

            if ( down && !wasDown )
            {
                PressDown();
                if ( IsParachuteActive )
                {
                    IsParachuteActive = false;
                    if ( IsHero )
                    {
                        invulnerableTime = 1.33f;
                    }
                }
            }

            if ( buttonJump )
            {
                lastButtonJumpTime = Time.time;
                if ( playerNum < 0 )
                {
                }

                if ( actionState == ActionState.Jumping && !doingMelee && ( jumpTime > 0f || ( !wasButtonJump && FluidController.IsSubmerged( this ) ) ) && yI < jumpForce )
                {
                    yI = jumpForce;
                }

                if ( !wasButtonJump || pressedJumpInAirSoJumpIfTouchGroundGrace > 0f )
                {
                    GetGroundHeightGround();
                    if ( airDashJumpGrace > 0f )
                    {
                        Jump( true );
                    }
                    else if ( ( !ducking ) && Time.time - lastJumpTime > 0.08f && CanJumpOffGround() && !isInQuicksand )
                    {
                        if ( yI < 0f )
                        {
                            Land();
                        }

                        Jump( false );
                    }
                    else if ( WallDrag && yI < 25f )
                    {
                        if ( right || left )
                        {
                            Jump( true );
                        }
                    }
                    else if ( isInQuicksand && ( ( canTouchLeftWalls && left ) || ( canTouchRightWalls && right ) ) && !wasButtonJump )
                    {
                        Jump( true );
                    }
                    else if ( canTouchLeftWalls && right && !wasButtonJump )
                    {
                        Jump( true );
                    }
                    else if ( canTouchRightWalls && left && !wasButtonJump )
                    {
                        Jump( true );
                    }
                    else if ( !ducking && actionState == ActionState.Jumping && left && Time.time - lastJumpTime > 0.3f && Physics.Raycast( new Vector3( X - 8f, Y + 10f, 0f ), Vector3.up, out raycastHit, collisionHeadHeight, groundLayer ) )
                    {
                        Jump( true );
                    }
                    else if ( !ducking && actionState == ActionState.Jumping && right && Time.time - lastJumpTime > 0.3f && Physics.Raycast( new Vector3( X + 8f, Y + 10f, 0f ), Vector3.up, out raycastHit, collisionHeadHeight, groundLayer ) )
                    {
                        Jump( true );
                    }
                    else if ( CanUseJetpack() )
                    {
                        UseJetpack();
                    }
                    else if ( !wasButtonJump )
                    {
                        AirJump();
                        pressedJumpInAirSoJumpIfTouchGroundGrace = 0.2f;
                    }
                }
                else if ( WallDrag )
                {
                    if ( !wallClimbing )
                    {
                        if ( !useNewKnifeClimbingFrames )
                        {
                            PlayKnifeClimbSound();
                        }

                        if ( useNewKnifeClimbingFrames && !wallClimbAnticipation && !chimneyFlip )
                        {
                            frame = 0;
                            lastKnifeClimbStabY = Y + knifeClimbStabHeight;
                            AnimateWallClimb();
                        }
                    }

                    wallClimbing = true;
                    if ( yI < 5f )
                    {
                        yI = 5f;
                    }
                }
                else
                {
                    wallClimbing = false;
                }

                if ( isInQuicksand && Physics.Raycast( new Vector3( X, Y + 16f, 0f ), Vector3.down, out raycastHitWalls, 15.5f, platformLayer ) && yI < 10f )
                {
                    yI = 10f;
                }
            }
            else
            {
                NotPressingJump();
            }
        }

        // Overridden to prevent vehicle from being teleported around like a player
        protected override void RunMovement()
        {
            CalculateGroundHeight();
            CheckForQuicksand();
            if ( actionState == ActionState.Dead )
            {
                if ( isInQuicksand )
                {
                    xI *= 1f - t * 20f;
                    xI = Mathf.Clamp( xI, -16f, 16f );
                    xIBlast *= 1f - t * 20f;
                }

                RunDeadGravity();
                if ( !( impaledByTransform == null ) )
                {
                    RunImpaledBlood();
                    xI = 0f;
                    xIBlast = 0f;
                    yIBlast = 0f;
                    if ( !impaledByTransform.gameObject.activeSelf )
                    {
                        impaledByTransform = null;
                    }
                }
            }
            else if ( IsOverFinish( ref ladderX ) )
            {
                actionState = ActionState.ClimbingLadder;
                yI = 0f;
                StopAirDashing();
            }
            else if ( attachedToZipline != null )
            {
                if ( down && !wasDown )
                {
                    attachedToZipline.DetachUnit( this );
                }

                if ( buttonJump && !wasButtonJump )
                {
                    attachedToZipline.DetachUnit( this );
                    yI = jumpForce;
                }
            }
            else if ( actionState != ActionState.ClimbingLadder && ( up || ( down && !IsGroundBelow() ) ) && ( !canDash || airdashTime <= 0f ) && IsOverLadder( ref ladderX ) )
            {
                actionState = ActionState.ClimbingLadder;
                yI = 0f;
                StopAirDashing();
            }
            else if ( actionState == ActionState.ClimbingLadder )
            {
                RunClimbingLadder();
            }
            else if ( doingMelee )
            {
                if ( isInQuicksand )
                {
                    xI *= 1f - t * 20f;
                    xI = Mathf.Clamp( xI, -2f, 2f );
                    xIBlast *= 1f - t * 20f;
                }

                RunMelee();
            }
            else if ( actionState == ActionState.Hanging )
            {
                RunHanging();
            }
            else if ( canAirdash && airdashTime > 0f )
            {
                RunAirDashing();
            }
            else if ( actionState == ActionState.Jumping )
            {
                if ( isInQuicksand )
                {
                    xI *= 1f - t * 20f;
                    xI = Mathf.Clamp( xI, -16f, 16f );
                    xIBlast *= 1f - t * 20f;
                }

                if ( jumpTime > 0f )
                {
                    jumpTime -= t;
                    if ( !buttonJump )
                    {
                        jumpTime = 0f;
                    }
                }

                if ( !( impaledByTransform != null ) )
                {
                    if ( wallClimbing )
                    {
                        ApplyWallClimbingGravity();
                    }
                    else if ( yI > 40f )
                    {
                        ApplyFallingGravity();
                    }
                    else
                    {
                        ApplyFallingGravity();
                    }
                }

                if ( yI < maxFallSpeed )
                {
                    yI = maxFallSpeed;
                }

                if ( yI < -50f )
                {
                    RunFalling();
                }

                if ( canCeilingHang && hangGrace > 0f )
                {
                    RunCheckHanging();
                }
            }
            else
            {
                if ( actionState == ActionState.Fallen )
                {
                    RunFallen();
                }

                EvaluateIsJumping();
            }

            yIT = ( yI + specialAttackYIBoost ) * t;
            if ( FluidController.IsSubmerged( X, Y ) )
            {
                yIT *= 0.65f;
            }

            if ( actionState != ActionState.Recalling )
            {
                if ( health > 0 && playerNum >= 0 && playerNum <= 3 )
                {
                    ConstrainSpeedToSidesOfScreen();
                }

                canTouchCeiling = ConstrainToCeiling( ref yIT );
                if ( FluidController.IsSubmerged( X, Y ) )
                {
                    xI *= 0.95f;
                    xIBlast *= 0.95f;
                }

                xIT = ( xI + xIBlast + xIAttackExtra + specialAttackXIBoost ) * t;
                ConstrainToWalls( ref yIT, ref xIT );
                if ( skinnedMookOnMyBack )
                {
                    xIT *= 0.95f;
                }

                X += xIT;
                CheckClimbAlongCeiling();
                CheckForTraps( ref yIT );
                if ( yI <= 0f )
                {
                    ConstrainToFloor( ref yIT );
                }
            }
            else
            {
                invulnerable = true;
                yI = 0f;
                yIT = yI * t;
                xI = 0f;
            }

            if ( WallDrag && ( parentHasMovedTime > 0f || fire ) )
            {
                wallDragTime = 0.25f;
            }

            Y += yIT;
            if ( !immuneToOutOfBounds )
            {
                bool flag = GameModeController.IsDeathMatchMode || GameModeController.GameMode == GameMode.BroDown;
                bool flag2 = flag && IsHero && ( SortOfFollow.IsZooming || !HeroController.isCountdownFinished );
                bool flag3 = flag && Y < screenMinY - 55f && playerNum >= 0;
                if ( !flag2 && ( Y < -44f || flag3 ) )
                {
                    if ( Map.isEditing )
                    {
                        Y = -20f;
                        yI = -yI * 1.5f;
                    }
                    else
                    {
                        float x = X;
                        float y = Y;
                        if ( IsHero && Map.lastYLoadOffset > 0 && Map.FindLadderNearPosition( ( screenMaxX + screenMinX ) / 2f, screenMinY, 16, ref x, ref y ) )
                        {
                            SetXY( x, y );
                            holdUpTime = 0.3f;
                            yI = 150f;
                            xI = 0f;
                            ShowStartBubble();
                        }
                        else if ( !IsHero || IsMine )
                        {
                            if ( HeroControllerTestInfo.HerosAreInvulnerable && IsHero )
                            {
                                yI += 1000f;
                            }
                            else
                            {
                                Gib( DamageType.OutOfBounds, xI, 840f );
                            }
                        }
                    }
                }
            }

            RunGroundFriction();
            if ( Y > groundHeight )
            {
                RunAirFriction();
            }

            if ( float.IsNaN( X ) )
            {
            }

            SetPosition();
        }

        protected override void StartDashing()
        {
            if ( canDash )
            {
                if ( actionState == ActionState.Jumping )
                {
                    hasDashedInAir = true;
                }

                if ( hasDashedInAir )
                {
                    dashSpeedM = lastDashSpeedM;
                }
                else
                {
                    dashSpeedM -= 0.5f;
                    if ( dashSpeedM < 1f )
                    {
                        dashSpeedM = 1f;
                    }
                }

                if ( actionState != ActionState.Jumping )
                {
                    if ( !dashing )
                    {
                        PlayDashSound( 0.3f );
                    }

                    dashing = true;
                    dashSpeedM = 1.5f;
                    delayedDashing = false;
                    EffectsController.CreateDashPoofEffect_Local( X, Y, ( Mathf.Abs( xI ) >= 1f ) ? ( ( int )transform.localScale.x ) : 0 );
                }
                else
                {
                    delayedDashing = true;
                }
            }
        }

        protected void RunBoosting()
        {
            // Reduce fuel when dashing
            if ( dashing )
            {
                // Apply initial boost if enough time has passed since previous dash
                if ( !wasdashButton && Time.time - lastDashTime > 1f )
                {
                    xI += transform.localScale.x * 100f;
                    lastDashTime = Time.time;
                    PlayRevSound();
                }

                boostFuel -= t * 0.35f;

                // Ran out of fuel
                if ( boostFuel <= 0f )
                {
                    boostFuel = 0f;
                    canDash = false;
                    dashing = false;
                    wasdashButton = false;
                }
            }
        }

        protected override void AddSpeedLeft()
        {
            if ( holdStillTime > 0f )
            {
                return;
            }
            else
            {
                if ( xI > -25f )
                {
                    xI = -25f;
                }

                xI -= speed * ( dashing ? 1f : 0.75f ) * t;
            }

            if ( xI < -( ( !dashing || ducking ) ? GetSpeed : ( GetSpeed * dashSpeedM ) ) )
            {
                xI = -( ( !dashing || ducking ) ? GetSpeed : ( GetSpeed * dashSpeedM ) );
            }
            else if ( xI > -50f && holdStillTime <= 0f )
            {
                xI -= speed * 2.6f * t * ( ( !IsParachuteActive ) ? 1f : 0.5f );
            }
        }

        protected override void AddSpeedRight()
        {
            if ( holdStillTime > 0f )
            {
                return;
            }
            else
            {
                if ( xI < 25f )
                {
                    xI = 25f;
                }

                xI += speed * ( dashing ? 1f : 0.75f ) * t;
            }

            if ( xI > ( ( !dashing || ducking ) ? GetSpeed : ( GetSpeed * dashSpeedM ) ) )
            {
                xI = ( ( !dashing || ducking ) ? GetSpeed : ( GetSpeed * dashSpeedM ) );
            }
            else if ( xI < 50f && holdStillTime <= 0f )
            {
                xI += speed * 2.6f * t * ( ( !IsParachuteActive ) ? 1f : 0.5f );
            }
        }

        protected override void Jump( bool wallJump )
        {
            if ( !wasButtonJump || pressedJumpInAirSoJumpIfTouchGroundGrace > 0f )
            {
            }

            if ( canAirdash && ( canTouchLeftWalls || canTouchRightWalls || !wallJump ) )
            {
                SetAirdashAvailable();
            }

            lastJumpTime = Time.time;
            actionState = ActionState.Jumping;
            if ( blockCurrentlyStandingOn != null && blockCurrentlyStandingOn.IsBouncy )
            {
                blockCurrentlyStandingOn.BounceOn();
            }

            if ( Physics.Raycast( new Vector3( X, Y + 2f, 0f ), Vector3.down, out this.raycastHit, 4f, Map.groundLayer ) && this.raycastHit.collider.GetComponent<BossBlockPiece>() != null )
            {
                BossBlockPiece component = raycastHit.collider.GetComponent<BossBlockPiece>();
                if ( component.isBouncy )
                {
                    yI = jumpForce * 1.9f;
                    component.BounceOn();
                }
                else
                {
                    yI = jumpForce;
                }
            }
            else
            {
                yI = jumpForce;
            }

            xIBlast += parentedDiff.x / t;
            float value = parentedDiff.y / t;
            yI += Mathf.Clamp( value, -100f, 400f );
            doubleJumpsLeft = 0;
            wallClimbAnticipation = false;
            if ( wallJump )
            {
                jumpTime = 0f;
                xI = 0f;
                if ( useNewKnifeClimbingFrames )
                {
                    frame = 0;
                    lastKnifeClimbStabY = Y + knifeClimbStabHeight;
                }
                else
                {
                    knifeHand++;
                }

                if ( left && Physics.Raycast( new Vector3( X, Y + collisionHeadHeight, 0f ), Vector3.left, out RaycastHit raycastHit, 10f, groundLayer ) )
                {
                    raycastHit.collider.SendMessage( "StepOn", this, SendMessageOptions.DontRequireReceiver );
                    SetCurrentFootstepSound( raycastHit.collider );
                    if ( useNewKnifeClimbingFrames )
                    {
                        AnimateWallAnticipation();
                    }
                }
                else if ( right && Physics.Raycast( new Vector3( X, Y + collisionHeadHeight, 0f ), Vector3.right, out raycastHit, 10f, groundLayer ) )
                {
                    raycastHit.collider.SendMessage( "StepOn", this, SendMessageOptions.DontRequireReceiver );
                    SetCurrentFootstepSound( raycastHit.collider );
                    if ( useNewKnifeClimbingFrames )
                    {
                        AnimateWallAnticipation();
                    }
                }

                PlayClimbSound();
            }
            else
            {
                jumpTime = JUMP_TIME;
                ChangeFrame();
                PlayJumpSound();
            }

            if ( !wallJump && groundHeight - Y > -2f )
            {
                EffectsController.CreateJumpPoofEffect( X, Y, ( Mathf.Abs( xI ) >= 30f ) ? ( -( int )transform.localScale.x ) : 0, GetFootPoofColor() );
            }

            airDashJumpGrace = 0f;
        }

        protected override void ApplyFallingGravity()
        {
            if ( reachedStartingPoint )
            {
                base.ApplyFallingGravity();
            }
        }

        protected override void RunGroundFriction()
        {
            if ( actionState == ActionState.Idle )
            {
                if ( Mathf.Abs( xI ) < 50f )
                {
                    xI *= 1f - t * ( 5 * groundFriction );
                }
                else
                {
                    xI *= 1f - t * groundFriction;
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
            health = maxHealth;
            if ( !hasResetDamage )
            {
                hasResetDamage = true;
                burnDamage = 0;
                shieldDamage = 0;
            }
        }

        public override void Damage( int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY )
        {
            if ( dashing && damageType != DamageType.SelfEsteem )
            {
                damage = 0;
            }

            // Limit how much one damage source can repeatedly damage the vehicle
            if ( damageType == DamageType.Melee && recentlyHitBy.Contains( damageSender ) )
            {
                return;
            }
            else if ( damageSender != null )
            {
                recentlyHitBy.Add( damageSender );
            }

            if ( damageSender is Helicopter helicopter )
            {
                helicopter.Damage( new DamageObject( helicopter.health, DamageType.Explosion, 0f, 0f, X, Y, this ) );
                xIBlast += xI * 0.1f + ( float )damage * 0.03f;
                this.yI += yI * 0.1f + ( float )damage * 0.03f;
            }
            // Ignore blades of helicopter
            else if ( damageSender is Mookopter && damageType == DamageType.Melee )
            {
                return;
            }
            else if ( damageSender is SawBlade sawBlade )
            {
                sawBlade.Damage( new DamageObject( sawBlade.health, DamageType.Explosion, 0f, 0f, X, Y, this ) );
                xIBlast += xI * 0.1f + ( float )damage * 0.03f;
                this.yI += yI * 0.1f + ( float )damage * 0.03f;
            }
            else if ( damageSender is MookDog mookDog )
            {
                mookDog.Damage( 0, DamageType.Knock, 0, 0, ( int )( -1f * mookDog.transform.localScale.x ), this, mookDog.X, mookDog.Y );
                mookDog.Panic( ( int )Mathf.Sign( xI ) * -1, 2f, true );
            }
            // Blow up falling explosive barrels
            else if ( damageSender is BarrelBlock barrel )
            {
                barrel.Explode();
            }
            else if ( damageSender is FallingBlock block )
            {
                block.Damage( new DamageObject( block.health, DamageType.Explosion, 0, 0, block.X, block.Y, this ) );
            }
            // Ignore damage by falling vehicles
            else if ( damageSender is HeroTransport )
            {
                return;
            }

            switch ( damageType )
            {
                case DamageType.Acid:
                    damageType = DamageType.Fire;
                    damage = Mathf.Max( damage, 15 );
                    damage *= 2;
                    goto case DamageType.Fire;
                case DamageType.Fire:
                    fireAmount += damage;
                    break;
                case DamageType.GibOnImpact:
                case DamageType.Crush:
                    damage = Mathf.Min( damage, 15 );
                    shieldDamage += damage;
                    damageType = DamageType.Normal;
                    break;
                case DamageType.Melee:
                    damage *= 2;
                    break;
                case DamageType.SelfEsteem:
                    break;
                case DamageType.Bounce:
                    return;
                default:
                    shieldDamage += damage;
                    break;
            }

            if ( health <= 0 )
            {
                knockCount++;
                if ( knockCount % 4 == 0 || ( alwaysKnockOnExplosions && damageType == DamageType.Explosion ) )
                {
                    yI = Mathf.Min( yI + 20f, 20f );
                    Y += 2f;
                    Knock( damageType, xI, 0f, false );
                }
                else
                {
                    Knock( damageType, xI, 0f, false );
                }
            }
            else if ( damageType != DamageType.SelfEsteem )
            {
                PlayDefendSound( damageType );
            }

            if ( damageType == DamageType.SelfEsteem && damage >= health && health > 0 )
            {
                Death( 0f, 0f, new DamageObject( damage, damageType, 0f, 0f, X, Y, this ) );
            }

            if ( shieldDamage + fireAmount > maxDamageBeforeExploding && pilotted )
            {
                if ( damageType == DamageType.Crush && shieldDamage > maxDamageBeforeExploding )
                {
                    DisChargePilot( 150f, false, null );
                    Gib( DamageType.OutOfBounds, xI, yI + 150f );
                }
                else
                {
                    deathCount = 9001;
                }
            }
        }

        protected virtual void PlayDefendSound( DamageType damageType )
        {
            if ( damageType == DamageType.Bullet )
            {
                Sound.GetInstance().PlaySoundEffectAt( soundHolder.defendSounds, 0.7f + Random.value * 0.4f, transform.position, 0.8f + 0.34f * Random.value, true, false, false, 0f );
            }
            // Add other damage sound
            else
            {
            }
        }

        public override void Knock( DamageType damageType, float xI, float yI, bool forceTumble )
        {
            if ( health > 0 )
            {
                knockCount++;
                if ( knockCount % 8 == 0 || ( alwaysKnockOnExplosions && damageType == DamageType.Explosion ) )
                {
                    KnockSimple( new DamageObject( 0, DamageType.Bullet, xI * 0.5f, yI * 0.3f, X, Y, null ) );
                }
            }
            else
            {
                base.Knock( damageType, xI, yI, forceTumble );
            }
        }

        public override void Death( float xI, float yI, DamageObject damage )
        {
            if ( GetComponent<Collider>() != null )
            {
                GetComponent<Collider>().enabled = false;
            }

            DeactivateGun();
            base.Death( xI, yI, damage );
            Gib( DamageType.InstaGib, xI, yI );
            if ( pilotUnit )
            {
                DisChargePilot( 150f, false, null );
            }
        }

        protected override void Gib( DamageType damageType, float xI, float yI )
        {
            if ( !destroyed && !gibbed )
            {
                if ( deathCount > 9000 )
                {
                    gibbed = true;
                    EffectsController.CreateMassiveExplosion( X, Y, 10f, 30f, 120f, 1f, 100f, 1f, 0.6f, 5, 70, 200f, 90f, 0.2f, 0.4f );
                    Map.ExplodeUnits( this, 20, DamageType.Explosion, 72f, 32f, X, Y + 6f, 200f, 150f, -15, true, false, true );
                    MapController.DamageGround( this, 15, DamageType.Explosion, 72f, X, Y, null, false );
                    SortOfFollow.Shake( 1f, 2f );
                }
                else
                {
                    gibbed = true;
                    EffectsController.CreateExplosion( X, Y + 5f, 8f, 8f, 120f, 0.5f, 100f, 1f, 0.6f, true );
                    EffectsController.CreateHugeExplosion( X, Y, 10f, 10f, 120f, 0.5f, 100f, 1f, 0.6f, 5, 70, 200f, 90f, 0.2f, 0.4f );
                    MapController.DamageGround( this, 15, DamageType.Explosion, 36f, X, Y, null, false );
                    Map.ExplodeUnits( this, 20, DamageType.Explosion, 48f, 32f, X, Y + 6f, 200f, 150f, -15, true, false, true );
                }

                base.Gib( damageType, xI, yI );
            }

            if ( pilotUnit )
            {
                DisChargePilot( 180f, false, null );
            }
        }
        #endregion

        #region Piloting
        public override bool CanPilotUnit( int newPlayerNum )
        {
            return true;
        }

        public override void PilotUnit( Unit pilotUnit )
        {
            PilotUnitRPC( pilotUnit );
        }

        public override void PilotUnitRPC( Unit newPilotUnit )
        {
            if ( !fixedBubbles )
            {
                FixPlayerBubble( player1Bubble );
                FixPlayerBubble( player2Bubble );
                FixPlayerBubble( player3Bubble );
                FixPlayerBubble( player4Bubble );
                fixedBubbles = true;
            }

            pilotUnitDelay = 0.2f;
            if ( pilotted && pilotUnit != newPilotUnit )
            {
                DisChargePilot( 150f, true, newPilotUnit );
            }

            ActuallyPilot( newPilotUnit );
        }

        protected virtual void ActuallyPilot( Unit PilotUnit )
        {
            if ( IsFrozen )
            {
                UnFreeze();
            }

            keepGoingBeyondTarget = false;
            reachedStartingPoint = true;
            groundFriction = originalGroundFriction;
            pilotUnit = PilotUnit;
            pilotted = true;
            playerNum = pilotUnit.playerNum;
            health = maxHealth;
            deathNotificationSent = false;
            isHero = true;
            firingPlayerNum = PilotUnit.playerNum;
            pilotUnit.StartPilotingUnit( this );
            RestartBubble();
            blindTime = 0f;
            stunTime = 0f;
            burnTime = 0f;
            ResetDamageAmounts();
            GetComponent<Collider>().enabled = true;
            //base.GetComponent<Collider>().gameObject.layer = LayerMask.NameToLayer("FriendlyBarriers");
            SetOwner( PilotUnit.Owner );
            hud = HeroController.players[PilotUnit.playerNum].hud;
            BroMakerUtilities.SetSpecialMaterials( PilotUnit.playerNum, specialSprite, new Vector2( 46f, 0f ), 5f );
            UpdateSpecialIcon();

            // Get info from Furiosa
            if ( PilotUnit is Furibrosa furibrosa )
            {
                pilotIsFuribrosa = true;
                if ( furibrosa.currentState == PrimaryState.Switching )
                {
                    currentPrimaryState = furibrosa.nextState;
                }
                else
                {
                    currentPrimaryState = furibrosa.currentState;
                }

                if ( currentPrimaryState == PrimaryState.FlareGun )
                {
                    gunSprite.meshRender.material = flareGunMat;
                }
                else
                {
                    gunSprite.meshRender.material = crossbowMat;
                }

                // Update summoner in case old furibrosa is null and new pilot tries to use sound effects
                summoner = furibrosa;
            }
            else
            {
                pilotIsFuribrosa = false;
                currentPrimaryState = PrimaryState.Crossbow;
                gunSprite.meshRender.material = crossbowMat;
                previousLayer = PilotUnit.gameObject.layer;
                // Ensure pilotting unit appears behind window
                PilotUnit.gameObject.layer = 20;
                PilotUnit.GetComponent<InvulnerabilityFlash>().enabled = false;
                BroBase bro = PilotUnit as BroBase;
                bro.frame = 0;
                bro.SetGestureAnimation( GestureElement.Gestures.None );
                bro.ForceChangeFrame();
            }

            SetGunSprite( 0, 0 );
        }

        protected virtual void DisChargePilot( float disChargeYI, bool stunPilot, Unit dischargedBy )
        {
            DisChargePilotRPC( disChargeYI, stunPilot, dischargedBy );
        }

        protected virtual void DisChargePilotRPC( float disChargeYI, bool stunPilot, Unit dischargedBy )
        {
            if ( pilotUnit != dischargedBy || pilotUnit == null )
            {
                currentFuriosaState = FuriosaState.InVehicle;
                gunFrame = 0;
                gunCounter = 0f;
                hangingOutTimer = 0f;
                charged = false;
                chargeTime = 0f;
                chargeFramerate = 0.09f;

                if ( pilotUnit )
                {
                    Furibrosa furibrosa = pilotUnit as Furibrosa;
                    if ( furibrosa && currentPrimaryState != furibrosa.currentState )
                    {
                        if ( currentPrimaryState == PrimaryState.Switching )
                        {
                            if ( nextPrimaryState != furibrosa.currentState )
                            {
                                furibrosa.nextState = nextPrimaryState;
                                furibrosa.SwitchWeapon();
                            }
                        }
                        else
                        {
                            furibrosa.nextState = currentPrimaryState;
                            furibrosa.SwitchWeapon();
                        }
                    }

                    // Remove invulnerability in case we received it while piloting vehicle
                    if ( furibrosa )
                    {
                        furibrosa.ClearInvulnerability();
                    }
                    else
                    {
                        // Reset layer to normal layer
                        pilotUnit.gameObject.layer = previousLayer;
                        // Re-enable invulnerability flash
                        pilotUnit.GetComponent<InvulnerabilityFlash>().enabled = true;
                    }

                    // Fix special ammo
                    BroBase bro = pilotUnit as BroBase;
                    foreach ( SpriteSM t1 in bro.player.hud.grenadeIcons )
                    {
                        t1.gameObject.SetActive( false );
                    }

                    if ( bro.pockettedSpecialAmmo.Count > 0 )
                    {
                        bro.player.hud.SetGrenadeMaterials( bro.pockettedSpecialAmmo[bro.pockettedSpecialAmmo.Count - 1] );
                        bro.player.hud.SetGrenades( 1 );
                    }
                    else
                    {
                        bro.player.hud.SetGrenadeMaterials( bro.heroType );
                        bro.player.hud.SetGrenades( bro.SpecialAmmo );
                    }
                    // Needed to ensure the icons always reappear, since if the remaining specials of the war rig match the remaining specials of the bro, no update happens.
                    Traverse.Create( bro.player.hud ).Method( "FlashSpecialIconsNormal" ).GetValue();

                    pilotUnit.GetComponent<Renderer>().enabled = true;
                    pilotUnit.DischargePilotingUnit( X, Mathf.Clamp( Y + 32f, -6f, 100000f ), xI + ( ( !stunPilot ) ? 0f : ( ( float )( Random.Range( 0, 2 ) * 2 - 1 ) * disChargeYI * 0.3f ) ), disChargeYI + 100f + ( ( pilotUnit.playerNum >= 0 ) ? 0f : ( disChargeYI * 0.5f ) ), stunPilot );
                }

                StopPlayerBubbles();
                pilotUnit = null;
                pilotted = false;
                isHero = false;
                fire = wasFire = false;
                hasBeenPiloted = true;
                releasedFire = false;
                fireDelay = 0f;
                dashing = false;
                DeactivateGun();
                SetSyncingInternal( false );

                currentPrimaryState = PrimaryState.Crossbow;
                ChangeFrame();
                RunGun();
            }
        }

        protected override void PressHighFiveMelee( bool forceHighFive = false )
        {
            if ( pilotUnitDelay <= 0f && pilotUnit && pilotUnit.IsMine )
            {
                DisChargePilot( 130f, false, null );
            }
        }
        #endregion

        #region Primary
        protected override void StartFiring()
        {
            charged = false;
            chargeTime = 0f;
            chargeFramerate = 0.09f;

            if ( pilotIsFuribrosa && currentPrimaryState != PrimaryState.Switching )
            {
                if ( currentFuriosaState == FuriosaState.InVehicle || currentFuriosaState == FuriosaState.GoingIn )
                {
                    if ( currentFuriosaState == FuriosaState.InVehicle )
                    {
                        gunFrame = 0;
                        gunCounter = 0f;
                    }

                    currentFuriosaState = FuriosaState.GoingOut;
                    hangingOutTimer = 8f;
                }

                base.StartFiring();
            }
        }

        protected override void ReleaseFire()
        {
            if ( fireDelay < 0.2f )
            {
                releasedFire = true;
            }

            base.ReleaseFire();
        }

        protected override void RunFiring()
        {
            if ( health <= 0 )
            {
                return;
            }

            // Reset timer whenever we press fire
            if ( fire && pilotIsFuribrosa )
            {
                hangingOutTimer = 8f;
            }

            // Don't fire unless furiosa is fully out the window
            if ( currentFuriosaState != FuriosaState.HangingOut )
            {
                return;
            }

            if ( currentPrimaryState == PrimaryState.Crossbow )
            {
                if ( fireDelay > 0f )
                {
                    fireDelay -= t;
                }

                if ( fireDelay <= 0f )
                {
                    if ( fire )
                    {
                        StopRolling();
                        chargeTime += t;
                    }
                    else if ( releasedFire )
                    {
                        UseFire();
                        SetGestureAnimation( GestureElement.Gestures.None );
                    }
                }
            }
            else if ( currentPrimaryState == PrimaryState.FlareGun )
            {
                if ( fireDelay > 0f )
                {
                    fireDelay -= t;
                }

                if ( fireDelay <= 0f )
                {
                    if ( fire || releasedFire )
                    {
                        UseFire();
                        SetGestureAnimation( GestureElement.Gestures.None );
                        releasedFire = false;
                    }
                }
            }
        }

        protected override void UseFire()
        {
            // Non-Furibrosa pilots can't fire weapons from the WarRig
            if ( !pilotIsFuribrosa )
            {
                return;
            }

            if ( doingMelee )
            {
                CancelMelee();
            }

            releasedFire = false;

            if ( Connect.IsOffline )
            {
                syncedDirection = ( int )transform.localScale.x;
            }

            FireWeapon( 0f, 0f, 0f, 0 );
            Map.DisturbWildLife( X, Y, 60f, playerNum );
        }

        protected override void FireWeapon( float x, float y, float xSpeed, float ySpeed )
        {
            // Fire crossbow
            if ( currentPrimaryState == PrimaryState.Crossbow )
            {
                // Fire explosive bolt
                if ( charged )
                {
                    x = X + transform.localScale.x * 7f;
                    y = Y + 35f;
                    xSpeed = transform.localScale.x * 500 + ( xI / 2 );
                    ySpeed = -50f;
                    gunFrame = 1;
                    SetGunSprite( gunFrame, 0 );
                    TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, transform );
                    ProjectileController.SpawnProjectileLocally( explosiveBoltPrefab, this, x, y, xSpeed, ySpeed, playerNum );
                }
                // Fire normal bolt
                else
                {
                    x = X + transform.localScale.x * 7f;
                    y = Y + 35f;
                    xSpeed = transform.localScale.x * 400 + ( xI / 2 );
                    ySpeed = -50f;
                    gunFrame = 1;
                    SetGunSprite( gunFrame, 0 );
                    TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, transform );
                    ProjectileController.SpawnProjectileLocally( boltPrefab, this, x, y, xSpeed, ySpeed, playerNum );
                }

                summoner.PlayCrossbowSound( transform.position );
                fireDelay = crossbowDelay;
            }
            else if ( currentPrimaryState == PrimaryState.FlareGun )
            {
                x = X + transform.localScale.x * 7f;
                y = Y + 35f;
                xSpeed = transform.localScale.x * 450;
                ySpeed = Random.Range( -25, 0 );
                EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, transform );
                ProjectileController.SpawnProjectileLocally( flarePrefab, this, x, y, xSpeed, ySpeed, playerNum );
                gunFrame = 3;
                summoner.PlayFlareSound( transform.position );
                fireDelay = flaregunDelay;
            }
        }

        protected override void RunGun()
        {
            // Count down timer for Furiosa hanging out of the window
            if ( hangingOutTimer > 0f )
            {
                hangingOutTimer -= t;
                if ( hangingOutTimer <= 0f )
                {
                    currentFuriosaState = FuriosaState.GoingIn;
                    gunFrame = 4;
                }
            }

            // Switch Weapon Pressed
            if ( pilotted && pilotIsFuribrosa && switchWeaponKey.IsDown( playerNum ) )
            {
                StartSwitchingWeapon();
            }

            // Animate in vehicle
            if ( currentFuriosaState == FuriosaState.InVehicle && currentPrimaryState != PrimaryState.Switching )
            {
                DeactivateGun();
                if ( pilotted )
                {
                    if ( pilotIsFuribrosa )
                    {
                        sprite.SetLowerLeftPixel( 2 * spritePixelWidth, spritePixelHeight );
                    }
                    else
                    {
                        sprite.SetLowerLeftPixel( 0, spritePixelHeight );
                    }
                }
            }
            // Animate leaning out
            else if ( currentFuriosaState == FuriosaState.GoingOut )
            {
                DeactivateGun();
                gunCounter += t;
                if ( gunCounter > 0.11f )
                {
                    gunCounter -= 0.11f;
                    ++gunFrame;
                    // Skip second frame
                    if ( gunFrame == 1 )
                    {
                        ++gunFrame;
                    }
                }

                sprite.SetLowerLeftPixel( spritePixelWidth * ( gunFrame + 3 ), ( currentPrimaryState == PrimaryState.FlareGun ? 2 : 3 ) * spritePixelHeight );

                // Finished going out
                if ( gunFrame == 4 )
                {
                    currentFuriosaState = FuriosaState.HangingOut;
                    gunFrame = 0;
                }
            }
            // Animate leaning in
            else if ( currentFuriosaState == FuriosaState.GoingIn )
            {
                DeactivateGun();
                gunCounter += t;
                if ( gunCounter > 0.11f )
                {
                    gunCounter -= 0.11f;
                    --gunFrame;
                }

                sprite.SetLowerLeftPixel( spritePixelWidth * ( 4 - gunFrame ), ( currentPrimaryState == PrimaryState.FlareGun ? 3 : 2 ) * spritePixelHeight );

                // Finished going out
                if ( gunFrame == 0 )
                {
                    currentFuriosaState = FuriosaState.InVehicle;
                }
            }
            // Animate crossbow
            else if ( currentPrimaryState == PrimaryState.Crossbow )
            {
                sprite.SetLowerLeftPixel( 1 * spritePixelWidth, spritePixelHeight );
                gunSprite.gameObject.SetActive( true );
                if ( fire )
                {
                    if ( chargeTime > 0.2f )
                    {
                        gunCounter += t;
                        if ( gunCounter > chargeFramerate )
                        {
                            gunCounter -= chargeFramerate;
                            ++gunFrame;
                            if ( gunFrame > 3 )
                            {
                                gunFrame = 0;
                                if ( !charged )
                                {
                                    summoner.PlayChargeSound( transform.position );
                                    charged = true;
                                    chargeFramerate = 0.04f;
                                }
                            }
                        }

                        SetGunSprite( gunFrame + 4, 0 );
                    }
                }
                else if ( !WallDrag && gunFrame > 0 )
                {
                    gunCounter += t;
                    if ( gunCounter > 0.045f )
                    {
                        gunCounter -= 0.045f;
                        ++gunFrame;
                        if ( gunFrame > 3 )
                        {
                            gunFrame = 0;
                        }

                        SetGunSprite( gunFrame, 0 );
                    }
                }
                else
                {
                    SetGunSprite( gunFrame, 0 );
                }
            }
            // Animate flaregun
            else if ( currentPrimaryState == PrimaryState.FlareGun )
            {
                sprite.SetLowerLeftPixel( 1 * spritePixelWidth, spritePixelHeight );
                gunSprite.gameObject.SetActive( true );
                if ( gunFrame > 0 )
                {
                    gunCounter += t;
                    if ( gunCounter > 0.0334f )
                    {
                        gunCounter -= 0.0334f;
                        --gunFrame;
                    }
                }

                SetGunSprite( gunFrame, 0 );
            }
            // Animate switching
            else if ( currentPrimaryState == PrimaryState.Switching )
            {
                DeactivateGun();
                gunCounter += t;

                // Animate leaning down to switch weapon
                if ( currentFuriosaState == FuriosaState.InVehicle )
                {
                    if ( gunCounter > 0.2f )
                    {
                        gunCounter -= 0.2f;
                        ++gunFrame;
                    }

                    sprite.SetLowerLeftPixel( ( 4 - gunFrame ) * spritePixelWidth, 2 * spritePixelHeight );

                    if ( gunFrame == 1 )
                    {
                        SwitchWeapon();
                    }
                }
                // Animate full lean back in and lean back out
                else
                {
                    // Ensure we don't start retracting while switching
                    hangingOutTimer = 8f;

                    if ( gunFrame != 3 && gunFrame != 4 )
                    {
                        if ( gunCounter > 0.10f )
                        {
                            gunCounter -= 0.10f;
                            ++gunFrame;
                        }
                    }
                    else
                    {
                        if ( gunCounter > 0.15f )
                        {
                            gunCounter -= 0.15f;
                            ++gunFrame;
                            if ( gunFrame == 5 )
                            {
                                summoner.PlaySwapSound( transform.position );
                            }
                        }
                    }

                    if ( gunFrame > 7 )
                    {
                        SwitchWeapon();
                    }
                    else
                    {
                        sprite.SetLowerLeftPixel( gunFrame * spritePixelWidth, ( nextPrimaryState == PrimaryState.FlareGun ? 2 : 3 ) * spritePixelHeight );
                    }
                }
            }
        }

        protected void StartSwitchingWeapon()
        {
            if ( !usingSpecial && currentPrimaryState != PrimaryState.Switching )
            {
                CancelMelee();
                SetGestureAnimation( GestureElement.Gestures.None );
                if ( currentPrimaryState == PrimaryState.Crossbow )
                {
                    nextPrimaryState = PrimaryState.FlareGun;
                }
                else
                {
                    nextPrimaryState = PrimaryState.Crossbow;
                }

                currentPrimaryState = PrimaryState.Switching;
                // Don't change frame if we're currently in the middle of another animation
                if ( currentFuriosaState != FuriosaState.GoingIn || currentFuriosaState != FuriosaState.GoingOut )
                {
                    gunFrame = 0;
                    gunCounter = 0f;
                    RunGun();
                }

                // Play swap sound if switching weapon inside vehicle
                if ( currentFuriosaState == FuriosaState.InVehicle )
                {
                    summoner.PlaySwapSound( transform.position );
                }
            }
        }

        protected void SwitchWeapon()
        {
            gunFrame = 0;
            gunCounter = 0f;
            currentPrimaryState = nextPrimaryState;
            if ( currentPrimaryState == PrimaryState.FlareGun )
            {
                gunSprite.meshRender.material = flareGunMat;
            }
            else
            {
                gunSprite.meshRender.material = crossbowMat;
            }

            SetGunSprite( 0, 0 );
        }
        #endregion

        #region Special
        protected void UpdateSpecialIcon()
        {
            hud.SetFuel( boostFuel, boostFuel <= 0.2f );

            // Re-enable grenade icons
            for ( int i = 0; i < SpecialAmmo; ++i )
            {
                hud.grenadeIcons[i].gameObject.SetActive( true );
            }
        }

        protected override void PressSpecial()
        {
            if ( SpecialAmmo > 0 )
            {
                if ( !usingSpecial )
                {
                    usingSpecial = true;
                    --SpecialAmmo;
                    specialFrame = 0;
                    specialFrameCounter = 0f;
                }
            }
            else
            {
                HeroController.FlashSpecialAmmo( playerNum );
            }
        }

        public void CreateMuzzleFlashBigEffect( float x, float y, float z, float xI, float yI, Transform parent )
        {
            if ( EffectsController.instance != null )
            {
                EffectsController.CreateEffect( EffectsController.instance.muzzleFlashBigPrefab, x, y, z, 0f, new Vector3( xI, yI, 0f ), parent );
                EffectsController.CreateEffect( EffectsController.instance.muzzleFlashBigGlowPrefab, x, y, z, 0f );
            }
        }

        protected override void UseSpecial()
        {
            float x = X + transform.localScale.x * 54f;
            float y = Y + 10f;
            float xSpeed = transform.localScale.x * 500 + ( xI / 2 );
            float ySpeed = 0;
            CreateMuzzleFlashBigEffect( X + transform.localScale.x * 46f, y, -25f, xSpeed * 0.15f, ySpeed, transform );
            ProjectileController.SpawnProjectileLocally( harpoonPrefab, this, x, y, xSpeed, ySpeed, playerNum );
            xIBlast -= transform.localScale.x * 150;
            yI += 200;
        }
        #endregion
    }
}