using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.CustomObjects.Projectiles;
using Drunken_Broster.MeleeItems;
using HarmonyLib;
using RocketLib;
using Rogueforce;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using ResourcesController = BroMakerLib.ResourcesController;

namespace Drunken_Broster
{
    [HeroPreset( "Drunken Broster", HeroType.Rambro )]
    public class DrunkenBroster : CustomHero
    {
        // TODO: remove all of these
        [SaveableSetting]
        public static int currentItem;
        public static KeyBindingForPlayers switchItemKey = AllModKeyBindings.LoadKeyBinding( "Drunken Broster", "Switch Item" );
        public static KeyBindingForPlayers switchItemBackKey = AllModKeyBindings.LoadKeyBinding( "Drunken Broster", "Switch Item Back" );
        public static KeyBindingForPlayers debugKey = AllModKeyBindings.LoadKeyBinding( "Drunken Broster", "Debug Key" );
        public static DrunkenBroster currentBroster;
        [SaveableSetting]
        public static float currentYOffset = 0f;
        [SaveableSetting]
        public static string currentYOffsetString = "0";
        public float spawnCountdown = 0.25f;

        // General
        protected bool acceptedDeath = false;
        bool wasInvulnerable = false;
        private Traverse setRumbleTraverse;
        protected Material normalSprite;
        protected Material drunkSprite;

        // Primary
        protected float postAttackHitPauseTime = 0f;
        protected bool hasHitThisAttack = false;
        protected float faderTrailDelay = 0f;
        protected float lastSoundTime = 0f;
        protected int attackSpriteRow = 0;
        protected List<Unit> alreadyHit = new List<Unit>();
        protected float hitClearCounter = 0f;
        protected bool hasHitWithWall = false;
        protected bool hasHitWithFists = false;
        protected bool hasMadeEffects = false;
        protected bool attackStationary = false;
        public bool attackUpwards = false;
        public bool attackDownwards = false;
        public bool attackForwards = false;
        protected int stationaryAttackCounter = -1;
        protected int attackDirection = 0;
        protected bool hasAttackedUpwards = false;
        protected bool hasAttackedDownwards = false;
        protected bool hasAttackedForwards = false;
        protected int attackFrames = 0;
        protected int attackStationaryStrikeFrame = 4;
        protected int attackUpwardsStrikeFrame = 2;
        protected int attackDownwardsStrikeFrame = 3;
        protected int attackForwardsStrikeFrame = 2;
        protected bool attackHasHit = false;
        protected const int soberEnemyFistDamage = 6;
        protected const int soberGroundFistDamage = 6;
        protected const int drunkEnemyFistDamage = 11;
        protected const int drunkGroundFistDamage = 11;
        protected int enemyFistDamage = soberEnemyFistDamage;
        protected int groundFistDamage = soberGroundFistDamage;
        public Shrapnel shrapnelSpark;
        public FlickerFader hitPuff;
        protected float lastAttackingTime = 0;
        protected bool startNewAttack = false;
        public float fistVolume = 0.7f;
        public float wallHitVolume = 0.25f;
        protected bool playedWallHit = false;

        // Melee
        public enum MeleeItem
        {
            Tire = 0,
            AcidEgg = 1,
            Beehive = 2,
            Bottle = 3,
            Crate = 4,
            Coconut = 5,
            ExplosiveBarrel = 6,
            SoccerBall = 7,
            AlienEgg = 8,
            Skull = 9,
            None = 10,
        }
        protected MeleeItem chosenItem = MeleeItem.None;
        protected MeleeItem heldItem = MeleeItem.None;
        protected bool holdingItem = false;
        public CustomGrenade tireProjectile;
        public CustomProjectile acidEggProjectile;
        public CustomProjectile beehiveProjectile;
        public CustomGrenade bottleProjectile;
        public CustomProjectile crateProjectile;
        public CustomGrenade coconutProjectile;
        public CustomGrenade explosiveBarrelProjectile;
        public CustomGrenade soccerBallProjectile;
        public CustomProjectile alienEggProjectile;
        public SkullProjectile skullProjectile;
        public MeshRenderer gunSpriteMelee;
        public SpriteSM gunSpriteMeleeSprite;
        public SpriteSM originalGunSprite;
        public Material meleeSpriteGrabThrowing;
        protected bool throwingHeldItem = false;
        protected bool thrownItem = false;
        protected bool hitSpecialDoodad = false;
        protected bool progressedFarEnough = false;
        protected float explosionCounter = -1f;
        protected float flameCounter = 0f;
        protected float warningCounter = 0f;
        protected bool warningOn = false;
        public FlickerFader fire1, fire2, fire3;
        public AudioClip[] barrelExplodeSounds;

        // Special
        public AudioClip slurp;
        public bool wasDrunk = false; // Was drunk before starting special
        public bool drunk = false;
        protected const float maxDrunkTime = 12f;
        public float drunkCounter = 0f;
        protected int usingSpecialFrame = 0; // Used to avoid problems with jumping / wall climbing resetting special animation
        protected float originalSpeed = 0;
        [SaveableSetting]
        public static bool enableCameraTilt = true;
        [SaveableSetting]
        public static bool lowIntensityMode = false;
        public bool IsDoingMelee { get { return this.doingMelee; } }
        protected bool usedSpecial = false;
        protected bool playedSpecialSound = false;
        protected bool bufferedSpecial = false;

        // Roll
        protected float slideExtraSpeed = 0f;
        protected bool isSlideRoll = false;
        protected float dashSlideCooldown = 0f;
        protected float lastDashTime = -1f;
        protected float doubleTapWindow = 0.3f;
        protected bool bufferedSlideRoll = false;
        protected float bufferedSlideRollTime = 0f;
        protected AudioSource rollSound;

        #region General
        protected override void Start()
        {
            base.Start();

            // Needed to have custom melee functions called, actual type is irrelevant
            this.meleeType = MeleeType.Disembowel;

            this.originalSpeed = this.speed;
            BroLee broLeePrefab = HeroController.GetHeroPrefab( HeroType.BroLee ) as BroLee;
            this.faderSpritePrefab = broLeePrefab.faderSpritePrefab;
            this.shrapnelSpark = broLeePrefab.shrapnelSpark;
            this.hitPuff = broLeePrefab.hitPuff;

            // Load sprites
            this.normalSprite = base.GetComponent<Renderer>().material;
            this.drunkSprite = ResourcesController.GetMaterial( directoryPath, "drunkSprite.png" );

            // Setup melee gunsprite
            gunSpriteMelee = new GameObject( "GunSpriteMelee", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } ).GetComponent<MeshRenderer>();
            gunSpriteMelee.transform.parent = this.transform;
            gunSpriteMelee.gameObject.SetActive( false );
            gunSpriteMelee.material = ResourcesController.GetMaterial( directoryPath, "gunSpriteMelee.png" );
            gunSpriteMeleeSprite = gunSpriteMelee.gameObject.GetComponent<SpriteSM>();
            gunSpriteMeleeSprite.RecalcTexture();
            gunSpriteMeleeSprite.SetTextureDefaults();
            gunSpriteMeleeSprite.lowerLeftPixel = new Vector2( 0, 32 );
            gunSpriteMeleeSprite.pixelDimensions = new Vector2( 32, 32 );
            gunSpriteMeleeSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            gunSpriteMeleeSprite.width = 32;
            gunSpriteMeleeSprite.height = 32;
            gunSpriteMeleeSprite.transform.localPosition = new Vector3( 0, 0, -0.9f );
            gunSpriteMeleeSprite.CalcUVs();
            gunSpriteMeleeSprite.UpdateUVs();
            gunSpriteMeleeSprite.offset = new Vector3( 0f, 15f, 0f );

            // Store original gunsprite
            this.originalGunSprite = this.gunSprite;

            // Load melee sprite for grabbing and throwing animations
            this.meleeSpriteGrabThrowing = ResourcesController.GetMaterial( directoryPath, "meleeSpriteGrabThrowing.png" );

            // Setup throwables
            tireProjectile = CustomGrenade.CreatePrefab<TireProjectile>();
            acidEggProjectile = CustomProjectile.CreatePrefab<AcidEggProjectile>();
            beehiveProjectile = CustomProjectile.CreatePrefab<BeehiveProjectile>();
            bottleProjectile = CustomGrenade.CreatePrefab<BottleProjectile>();
            crateProjectile = CustomProjectile.CreatePrefab<CrateProjectile>();
            coconutProjectile = CustomGrenade.CreatePrefab<CoconutProjectile>();
            explosiveBarrelProjectile = CustomGrenade.CreatePrefab<ExplosiveBarrelProjectile>();
            soccerBallProjectile = CustomGrenade.CreatePrefab<SoccerBallProjectile>();
            alienEggProjectile = CustomProjectile.CreatePrefab<AlienEggProjectile>();
            skullProjectile = SkullProjectile.CreatePrefab();

            // Cache Traverse for SetRumble method
            if ( this.player != null )
            {
                this.setRumbleTraverse = Traverse.Create( this.player ).Method( "SetRumble", new Type[] { typeof( float ) } );
            }

            // TODO: remove this
            DrunkenBroster.currentBroster = this;

            // TODO: remove this
            this.doRollOnLand = true;
        }

        protected override void Update()
        {
            base.Update();
            // Don't run any code past this point if the character is dead
            if ( this.acceptedDeath )
            {
                if ( this.health <= 0 && !this.WillReviveAlready )
                {
                    return;
                }
                // Revived
                else
                {
                    this.acceptedDeath = false;
                }
            }

            if ( this.invulnerable )
            {
                this.wasInvulnerable = true;
            }

            // Check if invulnerability ran out
            if ( this.wasInvulnerable && !this.invulnerable )
            {
                // Fix any not currently displayed textures
                this.wasInvulnerable = false;
                this.normalSprite.SetColor( "_TintColor", Color.gray );
                this.drunkSprite.SetColor( "_TintColor", Color.gray );
                this.meleeSpriteGrabThrowing.SetColor( "_TintColor", Color.gray );
                this.gunSpriteMelee.material.SetColor( "_TintColor", Color.gray );
            }

            // Check if character has died
            if ( base.actionState == ActionState.Dead && !this.acceptedDeath )
            {
                if ( !this.WillReviveAlready )
                {
                    this.acceptedDeath = true;
                }
            }

            // Count down to becoming sober
            if ( this.drunk )
            {
                this.drunkCounter -= this.t;
                // If drunk counter has run out, try to start becoming sober animation
                if ( this.drunkCounter <= 0 )
                {
                    this.TryToBecomeSober();
                }
            }

            DrunkenCameraManager.UpdateCameraTilt();

            this.postAttackHitPauseTime -= this.t;
            if ( ( this.attackForwards || this.attackUpwards || this.attackDownwards ) && this.xIAttackExtra != 0f )
            {
                this.faderTrailDelay -= this.t / Time.timeScale;
                if ( this.faderTrailDelay < 0f )
                {
                    this.CreateFaderTrailInstance();
                    this.faderTrailDelay = 0.034f;
                }
            }

            // Ensure we're never left holding a grenade if we somehow set throwingHeldObject
            if ( this.heldGrenade != null && !this.throwingHeldObject )
            {
                this.ReleaseHeldObject( false );
            }

            // TODO: remove this
            if ( DrunkenBroster.switchItemKey.PressedDown( this.playerNum ) )
            {
                ++DrunkenBroster.currentItem;
                if ( DrunkenBroster.currentItem > 9 )
                {
                    DrunkenBroster.currentItem = 0;
                }
            }
            else if ( DrunkenBroster.switchItemBackKey.PressedDown( this.playerNum ) )
            {
                --DrunkenBroster.currentItem;
                if ( DrunkenBroster.currentItem < 0 )
                {
                    DrunkenBroster.currentItem = 9;
                }
            }

            // TODO: remove this
            if ( DrunkenBroster.debugKey.PressedDown( this.playerNum ) )
            {
                this.Debug();
            }

            // Run flame / warning effect if holding explosive barrel
            if ( this.holdingItem && this.heldItem == MeleeItem.ExplosiveBarrel )
            {
                this.RunBarrelEffects();
            }

            this.RunRolling();
        }

        // TODO: remove this
        public static void makeTextBox( string label, ref string text, ref float val )
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label( label );
            text = GUILayout.TextField( text );
            GUILayout.EndHorizontal();

            float.TryParse( text, out val );
        }


        public override void UIOptions()
        {
            // Camera tilt settings
            GUILayout.Space( 5 );

            bool previousEnableState = enableCameraTilt;
            enableCameraTilt = GUILayout.Toggle( enableCameraTilt, "Enable Camera Tilt" );

            GUILayout.Space( 5 );

            bool previousLowIntensityState = lowIntensityMode;
            if ( enableCameraTilt )
            {
                lowIntensityMode = GUILayout.Toggle( lowIntensityMode, "Low Intensity Mode" );
            }

            // Handle settings changes
            if ( previousEnableState != enableCameraTilt || previousLowIntensityState != lowIntensityMode )
            {
                DrunkenCameraManager.OnSettingsChanged();
            }

            GUILayout.Space( 25 );

            // TODO: remove this debugging
            GUILayout.Space( 20 );
            GUILayout.BeginHorizontal();

            if ( GUILayout.Button( "-", GUILayout.Width( 50 ) ) )
            {
                if ( DrunkenBroster.currentItem > 0 )
                {
                    --DrunkenBroster.currentItem;
                }
            }
            if ( GUILayout.Button( "+", GUILayout.Width( 50 ) ) )
            {
                if ( DrunkenBroster.currentItem < 9 )
                {
                    ++DrunkenBroster.currentItem;
                }
            }

            GUILayout.Label( "Current Item: " + DrunkenBroster.currentItem );

            GUILayout.EndHorizontal();

            GUILayout.Space( 10 );
            switchItemKey.OnGUI( out _, true );
            GUILayout.Space( 10 );
            switchItemBackKey.OnGUI( out _, true );
            GUILayout.Space( 10 );
            debugKey.OnGUI( out _, true );
            GUILayout.Space( 10 );

            makeTextBox( "offset", ref currentYOffsetString, ref currentYOffset );

            //DrunkenBroster.freezeProjectile = GUILayout.Toggle( DrunkenBroster.freezeProjectile, "Freeze projectile" );
            //if ( GUILayout.Button( "Spawn Gibs", GUILayout.Width( 100 ) ) )
            //{
            //    DrunkenBroster.spawnGibs = true;
            //}

            if ( GUILayout.Button( "Print Debug" ) )
            {
                PrintDebug();
            }


        }

        // TODO: remove this
        public static void PrintDebug()
        {
            //Grenade coconut = null;
            //for ( int i = 0; i < Map.damageableScenery.Count; ++i )
            //{
            //    if ( Map.damageableScenery[i].GetComponent<CoconutSpawner>() != null )
            //    {
            //        coconut = Map.damageableScenery[i].GetComponent<CoconutSpawner>().coconutPrefab;
            //    }    
            //}

            //currentBroster.coconutProjectile.GenerateMatchingCode( coconut );
        }

        // TODO: remove this
        public void Debug()
        {
            // Towards corner
            //this.tireProjectile.SpawnGrenadeLocally( this, 464, 445 + currentYOffset, 0, 0, 350, -350, base.playerNum, 0 );
            // Above corner
            //this.tireProjectile.SpawnGrenadeLocally( this, 485 + currentYOffset, 445, 0, 0, 0, -10, base.playerNum, 0 );
            // Above ladder
            //this.tireProjectile.SpawnGrenadeLocally( this, 863 + currentYOffset, 440, 0, 0, 0, -10, base.playerNum, 0 );
            // Above hole
            //this.tireProjectile.SpawnGrenadeLocally( this, 832 + currentYOffset, 410, 0, 0, 0, -10, base.playerNum, 0 );

            // To the right of spawn
            this.tireProjectile.SpawnGrenadeLocally( this, 826.53f + currentYOffset, 504.32f, 0, 0, 0, -10, base.playerNum, 0 );
        }

        public override void HarmonyPatches( Harmony harmony )
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll( assembly );
        }

        public override void PreloadAssets()
        {
            // TODO: setup preloading assets
        }

        public override void BeforePrefabSetup()
        {
            this.SoundHolderHeroType = HeroType.BroLee;
        }

        public override void AfterPrefabSetup()
        {
            ThemeHolder theme = Map.Instance.jungleThemeReference;
            BarrelBlock explosiveBarrel = theme.blockPrefabBarrels[1].GetComponent<BarrelBlock>();
            this.fire1 = explosiveBarrel.fire1;
            this.fire2 = explosiveBarrel.fire2;
            this.fire3 = explosiveBarrel.fire3;
            this.barrelExplodeSounds = explosiveBarrel.soundHolder.deathSounds;

            string soundPath = Path.Combine( this.directoryPath, "sounds" );

            this.soundHolder.attack3Sounds = ResourcesController.GetAudioClipArray( soundPath, "kungFu", 13 );

            this.soundHolder.attack4Sounds = ResourcesController.GetAudioClipArray( soundPath, "slide_", 2 );

            this.slurp = ResourcesController.GetAudioClip( soundPath, "slurp.wav" );

            this.soundHolder.meleeHitSound = ResourcesController.GetAudioClipArray( soundPath, "meleeHitBlunt", 2 );
        }
        #endregion

        #region Primary
        protected override void RunGun()
        {
            if ( !this.WallDrag && this.gunFrame > 0 )
            {
                this.gunCounter += this.t;
                if ( this.gunCounter > 0.015f )
                {
                    this.gunCounter -= 0.015f;
                    this.gunFrame--;
                    if ( this.gunFrame < 0 )
                    {
                        this.gunFrame = 0;
                    }
                    this.SetGunSprite( 0, 0 );
                }
            }
        }

        protected override void SetGunSprite( int spriteFrame, int spriteRow )
        {
            if ( this.holdingItem )
            {
                spriteRow += ( (int)this.heldItem ) * 2;

                if ( this.heldItem == MeleeItem.ExplosiveBarrel && this.warningOn )
                {
                    spriteRow += 8;
                }
            }

            if ( base.actionState == ActionState.Hanging )
            {
                this.gunSprite.SetLowerLeftPixel( gunSpritePixelWidth * ( gunSpriteHangingFrame + spriteFrame ), gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
            else if ( base.actionState == ActionState.ClimbingLadder && hangingOneArmed )
            {
                this.gunSprite.SetLowerLeftPixel( gunSpritePixelWidth * ( gunSpriteHangingFrame + spriteFrame ), gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
            else if ( attachedToZipline != null && base.actionState == ActionState.Jumping )
            {
                this.gunSprite.SetLowerLeftPixel( gunSpritePixelWidth * ( gunSpriteHangingFrame + spriteFrame ), gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
            else
            {
                this.gunSprite.SetLowerLeftPixel( gunSpritePixelWidth * spriteFrame, gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
        }

        protected override void StartFiring()
        {
            if ( this.rollingFrames > 11 )
            {
                this.fire = this.wasFire = false;
                return;
            }

            // Don't allow attacking while drinking / becoming sober / using melee
            if ( this.usingSpecial || this.doingMelee )
            {
                this.fire = this.wasFire = false;
                return;
            }

            // If holding an item from melee, throw it instead
            if ( this.holdingItem )
            {
                this.StartThrowingItem();
                return;
            }

            // Release any grenades / coconuts we are holding
            if ( this.throwingHeldObject )
            {
                this.ReleaseHeldObject( false );
                this.throwingHeldObject = false;
            }

            this.startNewAttack = false;
            this.hasHitWithWall = false;
            this.hasHitWithFists = false;
            this.hasMadeEffects = false;
            // Buffering attack
            if ( this.attackForwards || this.attackDownwards || this.attackUpwards || this.attackStationary )
            {
                this.startNewAttack = true;
            }
            // Upward Attack Start
            else if ( this.up && !this.hasAttackedUpwards )
            {
                this.StartAttackUpwards();
            }
            // Downward Attack Start
            else if ( this.down && !this.hasAttackedDownwards )
            {
                this.StartAttackDownwards();
            }
            // Forward Attack Left Start
            else if ( this.left && !this.hasAttackedForwards )
            {
                this.StartAttackForwards( false );
            }
            // Forward Attack Right Start
            else if ( this.right && !this.hasAttackedForwards )
            {
                this.StartAttackForwards( true );
            }
            // Stationary Attack Start
            else if ( !this.up && !this.down && !this.left && !this.right )
            {
                this.StartAttackStationary();
            }
        }

        protected void StartAttackUpwards()
        {
            if ( base.actionState == ActionState.ClimbingLadder )
            {
                base.actionState = ActionState.Jumping;
            }
            this.FireFlashAvatar();
            this.MakeKungfuSound();
            this.StopAttack();
            if ( this.yI > 50f )
            {
                this.yI = 50f;
            }
            this.jumpTime = 0f;
            this.hasAttackedUpwards = true;
            this.attackFrames = 0;
            this.attackUpwards = true;
            this.ChangeFrame();
            this.airdashDirection = DirectionEnum.Up;
            this.ClearCurrentAttackVariables();
        }

        protected void StartAttackDownwards()
        {
            base.actionState = ActionState.Jumping;
            this.StopAttack();
            this.FireFlashAvatar();
            if ( !this.drunk )
            {
                this.yI = 150f;
                this.xI = base.transform.localScale.x * 80f;
            }
            else
            {
                this.yI = 200f;
                this.xI = base.transform.localScale.x * 80f;
                this.canWallClimb = false;
            }
            this.MakeKungfuSound();
            this.hasAttackedDownwards = true;
            this.attackFrames = 0;
            this.attackDownwards = true;
            this.jumpTime = 0f;
            this.ChangeFrame();
            this.airdashDirection = DirectionEnum.Down;
            this.ClearCurrentAttackVariables();
        }

        protected void StartAttackForwards( bool right )
        {
            this.FireFlashAvatar();
            this.attackSpriteRow = ( this.attackSpriteRow + 1 ) % 2;
            if ( base.actionState == ActionState.ClimbingLadder )
            {
                base.actionState = ActionState.Jumping;
            }
            if ( ( this.attackForwards || this.attackUpwards || this.attackDownwards ) && !this.hasHitThisAttack )
            {
                this.StopAttack();
                if ( !this.drunk )
                {
                    this.xIAttackExtra = right ? -20f : 20f;
                }
            }
            else if ( ( this.attackForwards || this.attackUpwards || this.attackDownwards ) && this.hasHitThisAttack )
            {
                this.StopAttack();
                if ( !this.drunk )
                {
                    this.xIAttackExtra = right ? 300f : -300f;
                }
                this.MakeKungfuSound();
            }
            else
            {
                this.StopAttack();
                if ( !this.drunk )
                {
                    this.xIAttackExtra = right ? 200f : -200f;
                }
                this.MakeKungfuSound();
            }
            this.postAttackHitPauseTime = 0f;
            this.hasAttackedForwards = true;
            this.attackFrames = 0;
            this.yI = 0f;
            this.attackForwards = true;
            this.attackDirection = right ? 1 : -1;
            base.transform.localScale = new Vector3( right ? 1f : -1f, this.yScale, 1f );
            this.jumpTime = 0f;
            this.ChangeFrame();
            this.CreateFaderTrailInstance();
            this.airdashDirection = right ? DirectionEnum.Right : DirectionEnum.Left;
            this.ClearCurrentAttackVariables();
        }

        protected void StartAttackStationary()
        {
            this.FireFlashAvatar();
            this.StopAttack();
            this.MakeKungfuSound();
            this.postAttackHitPauseTime = 0f;
            this.attackFrames = 0;
            this.attackStationary = true;
            this.jumpTime = 0f;
            ++this.stationaryAttackCounter;
            // Leg Sweep
            if ( this.stationaryAttackCounter % 2 == 0 )
            {
                this.attackStationaryStrikeFrame = 4;
            }
            // Forward punch
            {
                this.attackStationaryStrikeFrame = 2;
            }
            this.ChangeFrame();
            //this.CreateFaderTrailInstance();
            this.ClearCurrentAttackVariables();
        }

        protected override void RunFiring()
        {
            if ( this.fire )
            {
                this.rollingFrames = 0;
            }
            if ( this.attackStationary || this.attackUpwards || this.attackForwards || this.attackDownwards )
            {
                if ( !this.attackHasHit || this.drunk )
                {
                    if ( this.attackStationary && this.attackFrames >= this.attackStationaryStrikeFrame - 1 )
                    {
                        this.DeflectProjectiles();
                    }
                    else if ( this.attackForwards && this.attackFrames >= this.attackForwardsStrikeFrame - 1 )
                    {
                        this.DeflectProjectiles();
                    }
                    else if ( this.attackUpwards && this.attackFrames >= this.attackUpwardsStrikeFrame - 1 )
                    {
                        this.DeflectProjectiles();
                    }
                    else if ( this.attackDownwards && this.attackFrames >= this.attackDownwardsStrikeFrame - 1 )
                    {
                        this.DeflectProjectiles();
                    }
                }
                // Stationary Attack
                if ( this.attackStationary && this.attackFrames >= this.attackStationaryStrikeFrame && this.attackFrames <= 5 )
                {
                    this.PerformAttackStationary();
                }
                // Forwards Attack
                else if ( this.attackForwards && this.attackFrames >= this.attackForwardsStrikeFrame - 1 && this.attackFrames <= 5 )
                {
                    this.PerformAttackForwards();
                }
                // Upwards Attack
                else if ( this.attackUpwards && this.attackFrames >= this.attackUpwardsStrikeFrame && this.attackFrames <= 5 )
                {
                    this.PerformAttackUpwards();
                }
                // Downwards Attack
                else if ( this.attackDownwards && this.attackFrames >= this.attackDownwardsStrikeFrame && this.attackFrames <= 6 )
                {
                    this.PerformAttackDownwards();
                }
            }
        }

        protected void PerformAttackStationary()
        {
            this.lastAttackingTime = Time.time;
            // Leg Sweep Attack
            if ( this.stationaryAttackCounter % 2 == 0 )
            {
                DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 7f, 10f * base.transform.localScale.x, 950f, 6f, base.playerNum, out _, this );
                if ( HitUnitsStationaryAttack( this, base.playerNum, this.enemyFistDamage, 1, DamageType.Blade, 13f, 13f, base.X + base.transform.localScale.x * 7f, base.Y + 7f, 10f * base.transform.localScale.x, 950f, true, true, false, this.alreadyHit ) )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayPrimaryHitSound();
                    }
                    this.hasHitWithFists = true;
                    this.attackHasHit = true;
                    this.hasAttackedDownwards = false;
                    this.hasAttackedUpwards = false;
                    this.hasAttackedForwards = false;
                    this.hasHitThisAttack = true;
                    this.TimeBump();
                    this.xIAttackExtra = 0f;
                    this.postAttackHitPauseTime = 0.2f;
                    this.xI = 0f;
                    this.yI = 0f;
                    for ( int i = 0; i < this.alreadyHit.Count; i++ )
                    {
                        this.alreadyHit[i].FrontSomersault();
                    }
                }
                // Pause if we hit one of drunken master's doodads
                else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayWallSound();
                    }
                    this.hasHitWithFists = true;
                    this.attackHasHit = true;
                    this.hasAttackedDownwards = false;
                    this.hasAttackedUpwards = false;
                    this.hasAttackedForwards = false;
                    this.hasHitThisAttack = true;
                    this.TimeBump();
                    this.xIAttackExtra = 0f;
                    this.postAttackHitPauseTime = 0.1f;
                    this.xI = 0f;
                    this.yI = 0f;
                }
            }
            // Perform Forward Fist Attack
            else
            {
                DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 7f, 700f * base.transform.localScale.x, 250f, 6f, base.playerNum, out _, this );
                if ( HitUnitsStationaryAttack( this, base.playerNum, this.enemyFistDamage, 1, DamageType.Blade, 13f, 8f, base.X + base.transform.localScale.x * 7f, base.Y + 5f, 700f * base.transform.localScale.x, 250f, true, true, false, this.alreadyHit ) )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayPrimaryHitSound();
                    }
                    this.hasHitWithFists = true;
                    this.attackHasHit = true;
                    this.hasAttackedDownwards = false;
                    this.hasAttackedUpwards = false;
                    this.hasAttackedForwards = false;
                    this.hasHitThisAttack = true;
                    this.TimeBump();
                    this.xIAttackExtra = 0f;
                    this.postAttackHitPauseTime = 0.2f;
                    this.xI = 0f;
                    this.yI = 0f;
                    for ( int i = 0; i < this.alreadyHit.Count; i++ )
                    {
                        this.alreadyHit[i].FrontSomersault();
                    }
                }
                // Pause if we hit one of drunken master's doodads
                else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayWallSound();
                    }
                    this.hasHitWithFists = true;
                    this.attackHasHit = true;
                    this.hasAttackedDownwards = false;
                    this.hasAttackedUpwards = false;
                    this.hasAttackedForwards = false;
                    this.hasHitThisAttack = true;
                    this.TimeBump();
                    this.xIAttackExtra = 0f;
                    this.postAttackHitPauseTime = 0.1f;
                    this.xI = 0f;
                    this.yI = 0f;
                }
            }

            if ( !this.attackHasHit || this.drunk )
            {
                this.DeflectProjectiles();
            }
            if ( !this.attackHasHit )
            {
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 9f );
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 12f, new Vector3( base.transform.localScale.x, 0f, 0f ), 9f );
            }
        }

        protected void PerformAttackForwards()
        {
            this.lastAttackingTime = Time.time;
            // Sober attack
            if ( !this.drunk )
            {
                DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 7f, base.transform.localScale.x * 250f + this.xI, 200f, 6f, base.playerNum, out _, this );
                if ( HitUnits( this, base.playerNum, this.enemyFistDamage, 1, DamageType.Blade, 7f, 13f, base.X + base.transform.localScale.x * ( 3f + this.attackSpriteRow == 0 ? 1f : 0f ), base.Y + 7f, base.transform.localScale.x * 520f, 225f, true, true, true, this.alreadyHit, false ) )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayPrimaryHitSound();
                    }
                    this.hasHitWithFists = true;
                    this.attackHasHit = true;
                    this.hasAttackedDownwards = false;
                    this.hasAttackedUpwards = false;
                    this.hasAttackedForwards = false;
                    this.hasHitThisAttack = true;
                    this.TimeBump();
                    this.xIAttackExtra = 0f;
                    this.postAttackHitPauseTime = 0.2f;
                    this.xI = 0f;
                    this.yI = 0f;
                    for ( int i = 0; i < this.alreadyHit.Count; i++ )
                    {
                        this.alreadyHit[i].BackSomersault( false );
                    }
                }
                // Pause if we hit one of drunken master's doodads
                else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayWallSound();
                    }
                    this.hasHitWithFists = true;
                    this.attackHasHit = true;
                    this.hasAttackedDownwards = false;
                    this.hasAttackedUpwards = false;
                    this.hasAttackedForwards = false;
                    this.hasHitThisAttack = true;
                    this.TimeBump();
                    this.xIAttackExtra = 0f;
                    this.postAttackHitPauseTime = 0.1f;
                    this.xI = 0f;
                    this.yI = 0f;
                }
            }
            // Drunk attack
            else
            {
                // Spin attack
                if ( this.attackSpriteRow == 0 )
                {
                    DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 7f, base.transform.localScale.x * 220f, 450f, 6f, base.playerNum, out _, this );
                    if ( HitUnits( this, base.playerNum, this.enemyFistDamage, 1, DamageType.Blade, 4f, 10f, base.X + base.transform.localScale.x * 7f, base.Y + 7f, base.transform.localScale.x * 220f, 450f, true, true, false, this.alreadyHit, true ) )
                    {
                        if ( !this.hasHitWithFists )
                        {
                            this.PlayPrimaryHitSound();
                        }
                        this.hasHitWithFists = true;
                        this.attackHasHit = true;
                        this.hasAttackedDownwards = false;
                        this.hasAttackedUpwards = false;
                        this.hasAttackedForwards = false;
                        this.hasHitThisAttack = true;
                        for ( int i = 0; i < this.alreadyHit.Count; i++ )
                        {
                            this.alreadyHit[i].BackSomersault( false );
                        }
                    }
                    else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
                    {
                        if ( !this.hasHitWithFists )
                        {
                            this.PlayWallSound();
                        }
                        this.hasHitWithFists = true;
                        this.attackHasHit = true;
                        this.hasAttackedDownwards = false;
                        this.hasAttackedUpwards = false;
                        this.hasAttackedForwards = false;
                        this.hasHitThisAttack = true;
                    }
                }
                // Fist attack
                else
                {
                    DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 7f, base.transform.localScale.x * 520f, 200f, 6f, base.playerNum, out _, this );
                    if ( HitUnits( this, base.playerNum, this.enemyFistDamage + 8, 3, DamageType.Blade, 5f, 8f, base.X + base.transform.localScale.x * 7f, base.Y + 7f, base.transform.localScale.x * 700f, 250f, true, true, false, this.alreadyHit, false ) )
                    {
                        if ( !this.hasHitWithFists )
                        {
                            this.PlayPrimaryHitSound();
                        }
                        this.hasHitWithFists = true;
                        this.attackHasHit = true;
                        this.hasAttackedDownwards = false;
                        this.hasAttackedUpwards = false;
                        this.hasAttackedForwards = false;
                        this.hasHitThisAttack = true;
                        this.TimeBump( 0.4f );
                        this.xIAttackExtra = 0f;
                        this.postAttackHitPauseTime = 0.25f;
                        this.xI = 0f;
                        this.yI = 0f;
                        for ( int i = 0; i < this.alreadyHit.Count; i++ )
                        {
                            this.alreadyHit[i].BackSomersault( false );
                        }
                    }
                    // Pause if we hit one of drunken master's doodads
                    else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
                    {
                        if ( !this.hasHitWithFists )
                        {
                            this.PlayWallSound();
                        }
                        this.hasHitWithFists = true;
                        this.attackHasHit = true;
                        this.hasAttackedDownwards = false;
                        this.hasAttackedUpwards = false;
                        this.hasAttackedForwards = false;
                        this.hasHitThisAttack = true;
                        this.TimeBump();
                        this.xIAttackExtra = 0f;
                        this.postAttackHitPauseTime = 0.1f;
                        this.xI = 0f;
                        this.yI = 0f;
                    }
                }

            }
            if ( !this.attackHasHit || this.drunk )
            {
                this.DeflectProjectiles();
            }
            if ( !this.attackHasHit )
            {
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 9f );
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 12f, new Vector3( base.transform.localScale.x, 0f, 0f ), 9f );
            }
        }

        protected void PerformAttackUpwards()
        {
            if ( base.actionState == ActionState.ClimbingLadder )
            {
                base.actionState = ActionState.Jumping;
            }
            DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 6f ), base.Y + 12f, base.transform.localScale.x * 80f, 1100f, 7f, base.playerNum, out _, this );
            this.lastAttackingTime = Time.time;
            if ( HitUnits( this, base.playerNum, this.enemyFistDamage, 1, DamageType.Blade, 13f, 13f, base.X + base.transform.localScale.x * 6f, base.Y + 12f, base.transform.localScale.x * 80f, 1100f, true, true, true, this.alreadyHit, true ) )
            {
                if ( !this.hasHitWithFists )
                {
                    this.PlayPrimaryHitSound();
                }
                this.hasHitWithFists = true;
                this.attackHasHit = true;
                this.hasAttackedDownwards = false;
                this.hasAttackedForwards = false;
                this.hasHitThisAttack = true;
                if ( this.drunk )
                {
                    this.TimeBump( 0.1f );
                }
                else
                {
                    this.TimeBump();
                }
            }
            // Pause if we hit one of drunken master's doodads
            else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
            {
                if ( !this.hasHitWithFists )
                {
                    this.PlayWallSound();
                }
                this.hasHitWithFists = true;
                this.attackHasHit = true;
                this.hasAttackedDownwards = false;
                this.hasAttackedForwards = false;
                this.hasHitThisAttack = true;
                this.TimeBump();
            }
            if ( !this.attackHasHit || this.drunk )
            {
                this.DeflectProjectiles();
            }
            if ( !this.attackHasHit )
            {
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x * 0.5f, 1f, 0f ), 12f );
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0.5f, 0f ), 12f );
            }
        }

        protected void PerformAttackDownwards()
        {
            if ( base.actionState == ActionState.ClimbingLadder )
            {
                base.actionState = ActionState.Jumping;
            }
            // Sober Attack
            if ( !this.drunk )
            {
                DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 2f, base.transform.localScale.x * 120f, 100f, 6f, base.playerNum, out _, this );
                this.lastAttackingTime = Time.time;
                if ( HitUnits( this, base.playerNum, this.enemyFistDamage + 2, 1, DamageType.Blade, 9f, 4f, base.X + base.transform.localScale.x * 7f, base.Y + 3f, base.transform.localScale.x * 120f, 100f, true, true, true, this.alreadyHit, true ) )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayPrimaryHitSound();
                    }
                    this.hasHitWithFists = true;
                    this.attackHasHit = true;
                    this.hasAttackedForwards = false;
                    this.hasAttackedUpwards = false;
                    this.hasHitThisAttack = true;
                    this.xIAttackExtra = 0f;
                    this.TimeBump();
                    this.postAttackHitPauseTime = 0.08f;
                    this.xI = 0f;
                    this.yI = 0f;
                }
                // Pause if we hit one of drunken master's doodads
                else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayWallSound();
                    }
                    this.hasHitWithFists = true;
                    this.attackHasHit = true;
                    this.hasAttackedForwards = false;
                    this.hasAttackedUpwards = false;
                    this.hasHitThisAttack = true;
                    this.xIAttackExtra = 0f;
                    this.TimeBump();
                    this.postAttackHitPauseTime = 0.1f;
                    this.xI = 0f;
                    this.yI = 0f;
                }
                if ( !this.attackHasHit )
                {
                    this.DeflectProjectiles();
                }
                if ( !this.attackHasHit )
                {
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x * 0.4f, -1f, 0f ), 14f );
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x * 0.8f, -0.2f, 0f ), 12f );
                }
            }
            // Drunk Attack
            else
            {
                DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y, base.transform.localScale.x * 120f, 100f, 6f, base.playerNum, out _, this );
                this.lastAttackingTime = Time.time;
                this.DeflectProjectiles();
            }
        }

        protected override void UseFire()
        {
        }

        protected override void FireWeapon( float x, float y, float xSpeed, float ySpeed )
        {
        }

        protected void DownwardHitGround()
        {
            if ( !this.attackHasHit && this.attackFrames < 7 )
            {
                this.FireWeaponGround( base.X + base.transform.localScale.x * 16.5f, base.Y + 16.5f, Vector3.down, 18f + Mathf.Abs( this.yI * this.t ) );
            }
            if ( !this.attackHasHit && this.attackFrames < 7 )
            {
                this.FireWeaponGround( base.X + base.transform.localScale.x * 5.5f, base.Y + 16.5f, Vector3.down, 18f + Mathf.Abs( this.yI * this.t ) );
            }
            this.attackDownwards = false;
            this.attackFrames = 0;
        }

        protected void DownwardHitGroundDrunk()
        {
            // Fast forward frames if landed early
            if ( this.attackFrames < 5 )
            {
                this.attackFrames = 5;
                this.ChangeFrame();
            }
            this.attackHasHit = true;
            this.hasHitThisAttack = true;
            this.canWallClimb = true;
            ExplosionGroundWave explosionGroundWave = EffectsController.CreateShockWave( base.X, base.Y + 4f, 50f );
            explosionGroundWave.playerNum = this.playerNum;
            explosionGroundWave.avoidObject = this;
            explosionGroundWave.origins = this;
            if ( Map.HitUnits( this, this, this.playerNum, 20, DamageType.Crush, 30f, 10f, base.X, base.Y - 4f, 0f, this.yI, true, false, true, false ) )
            {
                this.PlayPrimaryHitSound();
                this.TimeBump( 0.3f );
            }
            MapController.DamageGround( this, 35, DamageType.Crush, 50f, base.X, base.Y + 8f, null, false );
            this.xI = ( this.xIBlast = 0f );
        }

        protected void FireWeaponGround( float x, float y, Vector3 raycastDirection, float distance )
        {
            if ( Physics.Raycast( new Vector3( x, y, 0f ), raycastDirection, out this.raycastHit, distance, this.groundLayer ) )
            {
                if ( !this.hasHitWithWall )
                {
                    SortOfFollow.Shake( 0.15f );
                    this.MakeEffects();
                }
                MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, this.groundFistDamage, DamageType.Blade, this.xI, 0f, this.raycastHit.point.x, this.raycastHit.point.y );
                // If we hit a steelblock, then don't allow further hits
                if ( this.drunk && this.raycastHit.collider.gameObject.GetComponent<SteelBlock>() != null )
                {
                    this.hasHitWithWall = true;
                    this.attackHasHit = true;
                }
                // If we're not drunk then let the hit register, otherwise allow hits to continue in drunk mode
                else if ( !this.drunk )
                {
                    this.hasHitWithWall = true;
                    this.attackHasHit = true;
                }
                this.PlayWallSound();
            }
        }

        protected virtual void MakeEffects( float x, float y, float xI, float yI )
        {
            if ( this.hasMadeEffects )
            {
                return;
            }
            this.hasMadeEffects = true;
            EffectsController.CreateShrapnel( this.shrapnelSpark, x, y, 4f, 30f, 3f, xI, yI );
            EffectsController.CreateEffect( this.hitPuff, x, y, 0f );
        }

        protected virtual void MakeEffects()
        {
            if ( this.hasMadeEffects )
            {
                return;
            }
            this.hasMadeEffects = true;
            EffectsController.CreateShrapnel( this.shrapnelSpark, this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f, 4f, 30f, 3f, this.raycastHit.normal.x * 60f, this.raycastHit.normal.y * 30f );
            EffectsController.CreateEffect( this.hitPuff, this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f );
        }

        protected override void SetGunPosition( float xOffset, float yOffset )
        {
            this.gunSprite.transform.localPosition = new Vector3( xOffset, yOffset - 1f, -1f );
        }

        public bool HitUnitsStationaryAttack( MonoBehaviour damageSender, int playerNum, int damage, int corpseDamage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, bool penetrates, bool knock, bool canGib, List<Unit> alreadyHitUnits )
        {
            if ( Map.units == null )
            {
                return false;
            }
            bool result = false;
            bool flag = false;
            int num = 999999;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                Unit unit = Map.units[i];
                if ( unit != null && GameModeController.DoesPlayerNumDamage( playerNum, unit.playerNum ) && !unit.invulnerable && unit.health <= num )
                {
                    float f = unit.X - x;
                    if ( Mathf.Abs( f ) - xRange < unit.width )
                    {
                        float num2 = unit.Y + unit.height / 2f + 3f - y;
                        if ( Mathf.Abs( num2 ) - yRange < unit.height && !alreadyHitUnits.Contains( unit ) )
                        {
                            alreadyHitUnits.Add( unit );
                            if ( !penetrates && unit.health > 0 )
                            {
                                num = 0;
                                flag = true;
                            }
                            // Send units hit in midair away rather than up
                            if ( !unit.IsOnGround() )
                            {
                                xI *= this.drunk ? 1.75f : 1.25f;
                                yI *= this.drunk ? 1.75f : 1.25f;
                            }
                            else
                            {
                                xI *= this.drunk ? 1.5f : 1f;
                                yI *= this.drunk ? 1.5f : 1f;
                            }
                            if ( !canGib && unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                            }
                            else if ( unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( corpseDamage, playerNum ), damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                            }
                            else
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( damage, playerNum ), damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y, false );
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

        public bool HitUnits( MonoBehaviour damageSender, int playerNum, int damage, int corpseDamage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, bool penetrates, bool knock, bool canGib, List<Unit> alreadyHitUnits, bool hitDead )
        {
            if ( Map.units == null )
            {
                return false;
            }
            bool result = false;
            bool flag = false;
            int num = 999999;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                Unit unit = Map.units[i];
                if ( unit != null && GameModeController.DoesPlayerNumDamage( playerNum, unit.playerNum ) && !unit.invulnerable && unit.health <= num && ( hitDead || unit.health > 0 ) )
                {
                    float f = unit.X - x;
                    if ( Mathf.Abs( f ) - xRange < unit.width )
                    {
                        float num2 = unit.Y + unit.height / 2f + 3f - y;
                        if ( Mathf.Abs( num2 ) - yRange < unit.height && !alreadyHitUnits.Contains( unit ) )
                        {
                            alreadyHitUnits.Add( unit );
                            if ( !penetrates && unit.health > 0 )
                            {
                                num = 0;
                                flag = true;
                            }
                            // Send units hit in midair further
                            if ( !unit.IsOnGround() )
                            {
                                xI *= this.drunk ? 3.0f : 2.5f;
                                yI *= this.drunk ? 1.5f : 1.25f;
                            }
                            if ( !canGib && unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                            }
                            else if ( unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( corpseDamage, playerNum ), damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                            }
                            else
                            {
                                damage = ValueOrchestrator.GetModifiedDamage( damage, playerNum );
                                // Don't allow instagibs
                                if ( damage > unit.health )
                                {
                                    damage = unit.health;
                                }
                                Map.KnockAndDamageUnit( damageSender, unit, damage, damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y, false );
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

        // Changed to store hit doodad
        public bool DamageDoodads( int damage, DamageType damageType, float x, float y, float xI, float yI, float range, int playerNum, out bool hitImpenetrableDoodad, MonoBehaviour sender )
        {
            hitImpenetrableDoodad = false;
            hitSpecialDoodad = false;
            bool result = false;
            for ( int i = Map.destroyableDoodads.Count - 1; i >= 0; i-- )
            {
                Doodad doodad = Map.destroyableDoodads[i];
                if ( !( doodad == null ) && ( playerNum >= 0 || doodad.CanBeDamagedByMooks ) )
                {
                    if ( playerNum < 0 || !doodad.immuneToHeroDamage )
                    {
                        if ( doodad.IsPointInRange( x, y, range ) )
                        {
                            bool flag = false;
                            if ( doodad is ShootableCircularDoodad circularDoodad )
                            {
                                if ( this.hasHitThisAttack )
                                {
                                    continue;
                                }
                                hitSpecialDoodad = true;

                                // Create hit effect
                                Vector2 currentPoint = new Vector2( x, y );
                                Vector2 centerPoint = new Vector2( circularDoodad.centerX, circularDoodad.centerY );
                                float distance = Vector2.Distance( currentPoint, centerPoint ) - circularDoodad.radius;
                                Vector2 hitPoint = Vector2.MoveTowards( currentPoint, centerPoint, distance + 0.5f );
                                this.MakeEffects( hitPoint.x, hitPoint.y, xI, yI );
                            }
                            doodad.DamageOptional( new DamageObject( damage, damageType, xI, yI, x, y, sender ), ref flag );
                            if ( flag )
                            {
                                result = true;
                                if ( doodad.isImpenetrable )
                                {
                                    hitImpenetrableDoodad = true;
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        protected void DeflectProjectiles()
        {
            if ( Map.DeflectProjectiles( this, base.playerNum, 10f, base.X + Mathf.Sign( base.transform.localScale.x ) * 6f, base.Y + 6f, Mathf.Sign( base.transform.localScale.x ) * 200f, true ) )
            {
                if ( !this.hasHitWithWall )
                {
                    this.PlayWallSound();
                }
                this.hasHitWithWall = true;
                this.attackHasHit = true;
            }
        }

        protected override void HitCeiling( RaycastHit ceilingHit )
        {
            if ( !this.drunk )
            {
                base.HitCeiling( ceilingHit );
                if ( this.attackUpwards )
                {
                    if ( !this.attackHasHit && this.attackFrames < 7 )
                    {
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 16.5f, base.Y + 2f, Vector3.up, this.headHeight + Mathf.Abs( this.yI * this.t ) );
                    }
                    if ( !this.attackHasHit && this.attackFrames < 7 )
                    {
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 4.5f, base.Y + 2f, Vector3.up, this.headHeight + Mathf.Abs( this.yI * this.t ) );
                    }
                    this.attackUpwards = false;
                    this.attackFrames = 0;
                }
            }
            else
            {
                if ( this.attackUpwards )
                {
                    if ( !this.attackHasHit && this.attackFrames < 7 )
                    {
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 16.5f, base.Y + 2f, Vector3.up, this.headHeight + Mathf.Abs( this.yI * this.t ) );
                    }
                    if ( !this.attackHasHit && this.attackFrames < 7 )
                    {
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 4.5f, base.Y + 2f, Vector3.up, this.headHeight + Mathf.Abs( this.yI * this.t ) );
                    }
                }
                this.HitCeilingDrunk( ceilingHit );
            }
        }

        protected virtual void HitCeilingDrunk( RaycastHit ceilingHit )
        {
            if ( up || buttonJump )
            {
                ceilingHit.collider.SendMessage( "StepOn", this, SendMessageOptions.DontRequireReceiver );
            }

            if ( !chimneyFlip && yI > 100f && ceilingHit.collider != null )
            {
                currentFootStepGroundType = ceilingHit.collider.tag;
                PlayFootStepSound( 0.2f, 0.6f );
            }

            BroforceObject hitBlock = ceilingHit.collider.gameObject.GetComponent<BroforceObject>();
            if ( hitBlock != null && hitBlock.health > 0 )
            {
                yIT = ceilingHit.point.y - headHeight - base.Y;
                yI = 0f;
                jumpTime = 0f;
            }

            if ( ( canCeilingHang && CanCheckClimbAlongCeiling() && ( up || buttonJump ) ) || hangGrace > 0f )
            {
                StartHanging();
            }
        }

        protected void StopAttack( bool allowBuffering = true )
        {
            this.hasHitThisAttack = false;
            this.attackStationary = ( this.attackUpwards = ( this.attackDownwards = ( this.attackForwards = false ) ) );
            this.playedWallHit = false;
            this.hasMadeEffects = false;
            this.attackFrames = 0;
            base.frame = 0;
            this.xIAttackExtra = 0f;
            if ( base.Y > this.groundHeight + 1f )
            {
                if ( base.actionState != ActionState.ClimbingLadder )
                {
                    base.actionState = ActionState.Jumping;
                }
            }
            else if ( this.right || this.left )
            {
                base.actionState = ActionState.Running;
            }
            else
            {
                base.actionState = ActionState.Idle;
            }

            if ( allowBuffering )
            {
                // Use special if it was buffered during attack
                if ( this.bufferedSpecial )
                {
                    this.bufferedSpecial = false;
                    this.PressSpecial();
                }
                // Start a new attack if it was buffered during attack
                else if ( this.startNewAttack )
                {
                    this.startNewAttack = false;
                    this.StartFiring();
                }
            }

            if ( base.Y < this.groundHeight + 1f )
            {
                this.StopAirDashing();
            }
            this.canWallClimb = true;
        }

        protected void AnimateAttackStationary()
        {
            // Leg Sweep
            if ( this.stationaryAttackCounter % 2 == 0 )
            {
                this.DeactivateGun();
                if ( this.attackFrames < this.attackStationaryStrikeFrame )
                {
                    this.frameRate = 0.0667f;
                }
                else if ( this.attackFrames < 5 )
                {
                    this.frameRate = 0.055f;
                }
                else
                {
                    this.frameRate = 0.055f;
                }
                if ( this.attackFrames == 8 )
                {
                    if ( this.startNewAttack )
                    {
                        this.startNewAttack = false;
                        this.StartFiring();
                    }
                }
                if ( this.attackFrames == this.attackStationaryStrikeFrame )
                {
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 9f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 8f );
                    this.PlayAttackSound();
                }
                if ( this.attackFrames == this.attackStationaryStrikeFrame + 1 )
                {
                    this.xIAttackExtra = 0f;
                }
                if ( this.attackFrames >= 8 )
                {
                    this.StopAttack();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( this.attackFrames, 0, 7 );
                    this.sprite.SetLowerLeftPixel( num * this.spritePixelWidth, this.spritePixelHeight * 6 );
                }
            }
            // Punch forward
            else
            {
                this.DeactivateGun();
                if ( this.attackFrames < this.attackStationaryStrikeFrame )
                {
                    this.frameRate = 0.075f;
                }
                else if ( this.attackFrames < 5 )
                {
                    this.frameRate = 0.055f;
                }
                else
                {
                    this.frameRate = 0.055f;
                }
                if ( this.attackFrames == 8 )
                {
                    if ( this.startNewAttack )
                    {
                        this.startNewAttack = false;
                        this.StartFiring();
                    }
                }
                if ( this.attackFrames == this.attackStationaryStrikeFrame )
                {
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 9f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 8f );
                    this.PlayAttackSound();
                }
                if ( this.attackFrames == this.attackStationaryStrikeFrame + 1 )
                {
                    this.xIAttackExtra = 0f;
                }
                if ( this.attackFrames >= 8 )
                {
                    this.StopAttack();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( this.attackFrames, 0, 7 );
                    this.sprite.SetLowerLeftPixel( num * this.spritePixelWidth, this.spritePixelHeight * 7 );
                }
            }
        }

        protected void AnimateAttackForwards()
        {
            // Sober animation
            if ( !this.drunk )
            {
                this.DeactivateGun();
                if ( this.attackFrames < this.attackForwardsStrikeFrame )
                {
                    this.frameRate = 0.055f;
                }
                else if ( this.attackFrames < 5 )
                {
                    this.frameRate = 0.065f;
                }
                else
                {
                    this.frameRate = 0.055f;
                }

                if ( this.attackFrames < this.attackForwardsStrikeFrame + 1 )
                {
                    this.CreateFaderTrailInstance();
                }
                else if ( this.attackFrames == 8 )
                {
                    if ( this.startNewAttack )
                    {
                        this.startNewAttack = false;
                        this.StartFiring();
                    }
                }

                if ( this.attackFrames == this.attackForwardsStrikeFrame )
                {
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 9f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 8f );
                    this.PlayAttackSound();
                }
                else if ( this.attackFrames == this.attackForwardsStrikeFrame + 1 )
                {
                    this.xIAttackExtra = 0f;
                }
                if ( this.attackFrames >= 8 )
                {
                    this.StopAttack();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( this.attackFrames, 0, 7 );
                    this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( this.spritePixelHeight * ( 8 + this.attackSpriteRow ) ) );
                }
            }
            // Drunk animation
            else
            {
                this.DeactivateGun();
                if ( this.attackFrames < this.attackForwardsStrikeFrame )
                {
                    this.frameRate = 0.10f;
                }
                else if ( this.attackFrames < 5 )
                {
                    this.frameRate = 0.075f;
                }
                else
                {
                    this.frameRate = 0.075f;
                }

                if ( this.attackFrames < this.attackForwardsStrikeFrame + 1 )
                {
                    this.CreateFaderTrailInstance();
                }
                else if ( this.attackFrames == 8 )
                {
                    if ( this.startNewAttack )
                    {
                        this.startNewAttack = false;
                        this.StartFiring();
                    }
                }

                // Spin attack
                if ( this.attackSpriteRow == 0 )
                {
                    if ( this.attackFrames > 2 && this.attackFrames < 8 )
                    {
                        this.xIAttackExtra = attackDirection * 275f;
                    }
                    else
                    {
                        this.xIAttackExtra = 0f;
                    }
                }
                // Fist attack
                else
                {
                    if ( this.attackFrames > 1 && this.attackFrames < 7 && !this.attackHasHit )
                    {
                        this.xIAttackExtra = attackDirection * 275f;
                    }
                    else
                    {
                        this.xIAttackExtra = 0f;
                    }
                }


                if ( this.attackFrames == this.attackForwardsStrikeFrame )
                {
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 9f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 3f );
                    this.PlayAttackSound();
                }
                if ( this.attackFrames >= 8 )
                {
                    this.StopAttack();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( this.attackFrames, 0, 7 );
                    this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( this.spritePixelHeight * ( 8 + this.attackSpriteRow ) ) );
                }
            }
        }

        protected void AnimateAttackUpwards()
        {
            // Sober animation
            if ( !this.drunk )
            {
                this.DeactivateGun();
                if ( this.attackFrames < this.attackUpwardsStrikeFrame )
                {
                    this.frameRate = 0.075f;
                }
                else if ( this.attackFrames < 5 )
                {
                    this.frameRate = 0.065f;
                }
                else
                {
                    this.frameRate = 0.065f;
                }
                if ( this.attackFrames == this.attackUpwardsStrikeFrame )
                {
                    this.xI = base.transform.localScale.x * 50f;
                    this.yI = 240f;
                    this.PlayAttackSound();
                }
                if ( this.attackFrames < this.attackUpwardsStrikeFrame + 2 )
                {
                    this.CreateFaderTrailInstance();
                }
                if ( this.startNewAttack && this.attackFrames == this.attackUpwardsStrikeFrame + 1 )
                {
                    this.startNewAttack = false;
                    this.StartFiring();
                }
                if ( this.hasHitThisAttack && this.attackFrames == 6 )
                {
                    this.xIAttackExtra = 0f;
                    this.postAttackHitPauseTime = 0.25f;
                    this.xI = 0f;
                    this.yI = 0f;
                }
                if ( this.attackFrames >= 10 || ( this.attackFrames == 8 && this.startNewAttack ) )
                {
                    this.StopAttack();
                    this.ChangeFrame();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( this.attackFrames, 0, 7 );
                    this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( this.spritePixelHeight * 10 ) );
                }
            }
            // Drunk animation
            else
            {
                this.DeactivateGun();
                if ( this.attackFrames < this.attackUpwardsStrikeFrame )
                {
                    this.frameRate = 0.1f;
                }
                else if ( this.attackFrames < 5 )
                {
                    this.frameRate = 0.11f;
                }
                else
                {
                    this.frameRate = 0.075f;
                }
                if ( this.attackFrames == this.attackUpwardsStrikeFrame )
                {
                    this.xI = base.transform.localScale.x * 50f;
                    this.yI = 325f;
                    this.PlayAttackSound();
                }
                if ( this.attackFrames < this.attackUpwardsStrikeFrame + 2 )
                {
                    this.CreateFaderTrailInstance();
                }
                if ( this.startNewAttack && this.attackFrames == this.attackUpwardsStrikeFrame + 1 )
                {
                    this.startNewAttack = false;
                    this.StartFiring();
                }
                if ( this.hasHitThisAttack && this.attackFrames == 6 )
                {
                    this.xIAttackExtra = 0f;
                    this.postAttackHitPauseTime = 0.25f;
                    this.xI = 0f;
                    this.yI = 0f;
                }
                if ( this.attackFrames >= 10 || ( this.attackFrames == 8 && this.startNewAttack ) )
                {
                    this.StopAttack();
                    this.ChangeFrame();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( this.attackFrames, 0, 7 );
                    this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( this.spritePixelHeight * 10 ) );
                }
            }
        }

        protected void AnimateAttackDownwards()
        {
            if ( !this.drunk )
            {
                this.DeactivateGun();
                if ( this.attackFrames < this.attackDownwardsStrikeFrame )
                {
                    this.frameRate = 0.0767f;
                }
                else if ( this.attackFrames <= 5 )
                {
                    this.frameRate = 0.0767f;
                }
                else
                {
                    this.frameRate = 0.06f;
                }
                if ( this.attackFrames < this.attackDownwardsStrikeFrame + 2 )
                {
                    this.CreateFaderTrailInstance();
                }
                if ( this.startNewAttack && this.attackFrames == this.attackDownwardsStrikeFrame + 1 )
                {
                    this.startNewAttack = false;
                    this.StartFiring();
                }
                if ( this.attackFrames == this.attackDownwardsStrikeFrame )
                {
                    if ( !this.usingSpecial || !this.hasHitThisAttack )
                    {
                        this.yI = -250f;
                    }
                    this.xI = base.transform.localScale.x * 60f;
                    this.PlayAttackSound();
                }
                if ( this.attackFrames >= 9 || ( this.attackFrames == 6 && this.startNewAttack ) )
                {
                    this.StopAttack();
                    this.ChangeFrame();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( this.attackFrames, 0, 7 );
                    this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( this.spritePixelHeight * 11 ) );
                }
                if ( this.attackFrames == 7 && this.usingSpecial )
                {
                    if ( !this.hasHitThisAttack )
                    {
                        this.usingSpecial = false;
                    }
                    if ( this.usingSpecial )
                    {
                        this.UseSpecial();
                    }
                }
            }
            else
            {
                this.DeactivateGun();
                if ( this.attackFrames < this.attackDownwardsStrikeFrame )
                {
                    this.frameRate = 0.19f;
                }
                else if ( this.attackFrames <= 5 )
                {
                    this.frameRate = 0.08f;
                }
                else
                {
                    this.frameRate = 0.08f;
                }
                if ( this.attackFrames < this.attackDownwardsStrikeFrame + 2 )
                {
                    this.CreateFaderTrailInstance();
                }

                // Hold frame until hitting ground
                if ( !this.hasHitThisAttack && this.attackFrames > 5 )
                {
                    this.attackFrames = 5;
                }

                if ( this.attackFrames == this.attackDownwardsStrikeFrame )
                {
                    if ( !this.usingSpecial || !this.hasHitThisAttack )
                    {
                        this.yI = -300f;
                    }
                    this.xI = base.transform.localScale.x * 60f;
                    this.PlayAttackSound();
                }
                if ( this.attackFrames >= 9 )
                {
                    this.StopAttack();
                    this.ChangeFrame();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( this.attackFrames, 0, 7 );
                    this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( this.spritePixelHeight * 11 ) );
                }
            }
        }

        protected override void IncreaseFrame()
        {
            base.IncreaseFrame();
            if ( this.attackStationary || this.attackUpwards || this.attackDownwards || this.attackForwards )
            {
                ++this.attackFrames;
            }
            else if ( this.usingSpecial )
            {
                ++this.usingSpecialFrame;
            }
        }

        protected override void ChangeFrame()
        {
            if ( this.health <= 0 || this.chimneyFlip )
            {
                base.ChangeFrame();
            }
            else if ( this.attackStationary )
            {
                this.AnimateAttackStationary();
            }
            else if ( this.attackUpwards )
            {
                this.AnimateAttackUpwards();
            }
            else if ( this.attackDownwards )
            {
                this.AnimateAttackDownwards();
            }
            else if ( this.attackForwards )
            {
                this.AnimateAttackForwards();
            }
            else
            {
                base.ChangeFrame();
            }
        }

        protected override void AnimateZipline()
        {
            base.AnimateZipline();
            this.SetGunSprite( this.gunFrame, 0 );
        }

        protected override void CreateFaderTrailInstance()
        {
            FaderSprite component = this.faderSpritePrefab.GetComponent<FaderSprite>();
            FaderSprite faderSprite = EffectsController.InstantiateEffect( component, base.transform.position, base.transform.rotation ) as FaderSprite;
            if ( faderSprite != null )
            {
                faderSprite.transform.localScale = base.transform.localScale;
                faderSprite.SetMaterial( base.GetComponent<Renderer>().material, this.sprite.lowerLeftPixel, this.sprite.pixelDimensions, this.sprite.offset );
                faderSprite.fadeM = 0.15f;
                faderSprite.maxLife = 0.15f;
                faderSprite.moveForwards = true;
            }
        }

        protected override void FireFlashAvatar()
        {
            if ( this.drunk )
            {
                this.avatarGunFireTime = 0.25f;
                HeroController.SetAvatarFireFrame( base.playerNum, UnityEngine.Random.Range( 5, 8 ) );
            }
            if ( this.player != null && this.setRumbleTraverse != null )
            {
                this.setRumbleTraverse.GetValue( new object[] { this.rumbleAmountPerShot } );
            }
        }

        protected override void RunAvatarFiring()
        {
            if ( this.health > 0 )
            {
                if ( this.avatarGunFireTime > 0f )
                {
                    this.avatarGunFireTime -= this.t;
                    if ( this.avatarGunFireTime <= 0f )
                    {
                        if ( this.avatarAngryTime > 0f )
                        {
                            HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
                        }
                        else
                        {
                            HeroController.SetAvatarCalm( base.playerNum, this.usePrimaryAvatar );
                        }
                    }
                }

                if ( this.fire && !this.drunk )
                {
                    if ( !this.wasFire && this.avatarGunFireTime <= 0f )
                    {
                        HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
                    }

                    if ( this.attackStationary || this.attackForwards || this.attackUpwards || this.attackDownwards )
                    {
                        this.avatarAngryTime = 0.15f;
                    }
                    else
                    {
                        this.avatarAngryTime = 0f;
                        HeroController.SetAvatarCalm( base.playerNum, this.usePrimaryAvatar );
                    }
                }
                else if ( this.avatarAngryTime > 0f )
                {
                    this.avatarAngryTime -= this.t;
                    if ( this.avatarAngryTime <= 0f )
                    {
                        HeroController.SetAvatarCalm( base.playerNum, this.usePrimaryAvatar );
                    }
                }
            }
        }

        protected void ClearCurrentAttackVariables()
        {
            this.alreadyHit.Clear();
            this.hasHitWithFists = false;
            this.attackHasHit = false;
            this.hasHitWithWall = false;
        }

        private void TimeBump( float timeStop = 0.025f )
        {
            TimeController.StopTime( timeStop, 0.1f, 0f, false, false, false );
        }

        private void MakeKungfuSound()
        {
            if ( Time.time - this.lastSoundTime > 0.3f )
            {
                this.lastSoundTime = Time.time;
                Sound.GetInstance().PlaySoundEffectAt( this.soundHolder.attack3Sounds, 0.6f, base.transform.position, this.drunk ? 0.85f : 1f, true, false, false, 0f );
            }
        }

        // Sound played when starting your primary attack, sounds like a swish
        protected override void PlayAttackSound()
        {
            base.PlayAttackSound();
        }

        // Sound played when hitting an enemy with your primary attack
        protected void PlayPrimaryHitSound( float volume = 0.6f )
        {
            if ( this.sound == null )
            {
                this.sound = Sound.GetInstance();
            }
            this.sound?.PlaySoundEffectAt( this.soundHolder.special2Sounds, volume, base.transform.position, 1f, true, false, false, 0f );
        }

        // Sound played when hitting a wall with your primary attack
        public void PlayWallSound()
        {
            // Don't play wall sound multiple times
            if ( this.playedWallHit )
            {
                return;
            }
            this.playedWallHit = true;
            if ( this.sound == null )
            {
                this.sound = Sound.GetInstance();
            }
            this.sound?.PlaySoundEffectAt( this.soundHolder.attack2Sounds, this.wallHitVolume, base.transform.position, 1f, true, false, false, 0f );
        }

        protected override void PlayAidDashSound()
        {
            this.PlaySpecialAttackSound( 0.5f );
        }

        protected bool IsAttacking()
        {
            return ( this.fire && this.gunFrame > 1 ) || Time.time - this.lastAttackingTime < 0.0445f || ( ( this.attackDownwards || this.attackForwards || this.attackUpwards ) && this.attackFrames > 1 && this.attackFrames < this.attackForwardsStrikeFrame + 2 );
        }

        // Prevent damage from melee attacks when attacking
        public override void Damage( int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY )
        {
            if ( ( damageType != DamageType.Drill && damageType != DamageType.Melee && damageType != DamageType.Knifed && damageType != DamageType.Explosion && damageType != DamageType.Fire ) || !this.IsAttacking() || ( Mathf.Sign( base.transform.localScale.x ) == Mathf.Sign( xI ) && damageType != DamageType.Drill ) )
            {
                base.Damage( damage, damageType, xI, yI, direction, damageSender, hitX, hitY );
            }
        }
        #endregion

        #region Melee
        // Handle grenade throwing better
        protected override void PressHighFiveMelee( bool forceHighFive = false )
        {
            if ( this.health <= 0 )
            {
                return;
            }
            if ( this.MustIgnoreHighFiveMeleePress() )
            {
                return;
            }
            this.SetGestureAnimation( GestureElement.Gestures.None );
            Grenade nearbyGrenade = Map.GetNearbyGrenade( 20f, base.X, base.Y + this.waistHeight );
            this.FindNearbyMook();
            TeleportDoor nearbyTeleportDoor = Map.GetNearbyTeleportDoor( base.X, base.Y );
            if ( nearbyTeleportDoor != null && this.CanUseSwitch() && nearbyTeleportDoor.Activate( this ) )
            {
                return;
            }
            Switch nearbySwitch = Map.GetNearbySwitch( base.X, base.Y );
            if ( GameModeController.IsDeathMatchMode || GameModeController.GameMode == GameMode.BroDown )
            {
                if ( nearbySwitch != null && this.CanUseSwitch() )
                {
                    nearbySwitch.Activate( this );
                }
                else
                {
                    bool flag = false;
                    for ( int i = -1; i < 4; i++ )
                    {
                        if ( i != base.playerNum && Map.IsUnitNearby( i, base.X + base.transform.localScale.x * 16f, base.Y + 8f, 28f, 14f, true, out this.meleeChosenUnit ) )
                        {
                            this.StartMelee();
                            flag = true;
                        }
                    }
                    if ( !flag && nearbySwitch != null && this.CanUseSwitch() )
                    {
                        nearbySwitch.Activate( this );
                    }
                }
            }
            // Don't allow throwing items during melee, attacks, or rolls
            if ( nearbyGrenade != null && !( this.doingMelee || this.attackForwards || this.attackDownwards || this.attackUpwards || this.attackStationary || this.rollingFrames > 0 ) )
            {
                this.ThrowBackGrenade( nearbyGrenade );
            }
            else if ( !GameModeController.IsDeathMatchMode || !this.doingMelee )
            {
                if ( Map.IsCitizenNearby( base.X, base.Y, 32, 32 ) )
                {
                    if ( !this.doingMelee )
                    {
                        this.StartHighFive();
                    }
                }
                else if ( forceHighFive && !this.doingMelee )
                {
                    this.StartHighFive();
                }
                else if ( nearbySwitch != null && this.CanUseSwitch() )
                {
                    nearbySwitch.Activate( this );
                }
                else if ( this.meleeChosenUnit == null && Map.IsUnitNearby( -1, base.X + base.transform.localScale.x * 16f, base.Y + 8f, 28f, 14f, false, out this.meleeChosenUnit ) )
                {
                    this.StartMelee();
                }
                else if ( this.CheckBustCage() )
                {
                    this.StartMelee();
                }
                else if ( HeroController.IsAnotherPlayerNearby( base.playerNum, base.X, base.Y, 32f, 32f ) )
                {
                    if ( !this.doingMelee )
                    {
                        this.StartHighFive();
                    }
                }
                else
                {
                    this.StartMelee();
                }
            }
        }

        // Don't reset base.counter on every melee press
        protected override void StartMelee()
        {
            this.currentMeleeType = this.meleeType;
            RaycastHit raycastHit;
            if ( ( Physics.Raycast( new Vector3( base.X, base.Y + 5f, 0f ), Vector3.down, out raycastHit, 16f, this.platformLayer ) || Physics.Raycast( new Vector3( base.X + 4f, base.Y + 5f, 0f ), Vector3.down, out raycastHit, 16f, this.platformLayer ) || Physics.Raycast( new Vector3( base.X - 4f, base.Y + 5f, 0f ), Vector3.down, out raycastHit, 16f, this.platformLayer ) ) && raycastHit.collider.GetComponentInParent<Animal>() != null )
            {
                this.currentMeleeType = BroBase.MeleeType.Knife;
            }
            switch ( this.currentMeleeType )
            {
                case BroBase.MeleeType.Knife:
                    base.counter = 0f;
                    this.StartKnifeMelee();
                    break;
                case BroBase.MeleeType.Punch:
                case BroBase.MeleeType.JetpackPunch:
                    this.StartPunch();
                    break;
                case BroBase.MeleeType.Disembowel:
                case BroBase.MeleeType.FlipKick:
                case BroBase.MeleeType.Tazer:
                case BroBase.MeleeType.Custom:
                case BroBase.MeleeType.ChuckKick:
                case BroBase.MeleeType.VanDammeKick:
                case BroBase.MeleeType.ChainSaw:
                case BroBase.MeleeType.ThrowingKnife:
                case BroBase.MeleeType.Smash:
                case BroBase.MeleeType.BrobocopPunch:
                case BroBase.MeleeType.PistolWhip:
                case BroBase.MeleeType.HeadButt:
                case BroBase.MeleeType.TeleportStab:
                    this.StartCustomMelee();
                    break;
            }
        }

        protected override void StartCustomMelee()
        {
            // Don't allow melees to start while doing a melee
            if ( this.doingMelee )
            {
                return;
            }

            // If we're already holding an item, throw that item instead
            if ( this.holdingItem )
            {
                this.StartThrowingItem();
                return;
            }

            // If we're throwing back a grenade, and the grenade has already left our hands, turn off the throwingHeldObject flag
            if ( this.throwingHeldObject && this.heldGrenade == null && this.heldMook == null )
            {
                this.throwingHeldObject = false;
            }

            this.StopAttack( false );
            base.frame = 0;
            base.counter = -0.05f;
            ResetMeleeValues();
            lerpToMeleeTargetPos = 0f;
            doingMelee = true;
            showHighFiveAfterMeleeTimer = 0f;
            DeactivateGun();
            SetMeleeType();
            meleeStartPos = base.transform.position;
            this.progressedFarEnough = false;
            this.canWallClimb = false;

            // Switch to melee sprite
            base.GetComponent<Renderer>().material = this.meleeSpriteGrabThrowing;

            // Choose an item to throw
            this.chosenItem = this.ChooseItem();

            AnimateMelee();
        }

        protected MeleeItem ChooseItem()
        {
            return (MeleeItem)( DrunkenBroster.currentItem );
        }

        protected override void AnimateCustomMelee()
        {
            this.SetSpriteOffset( 0f, 0f );
            this.rollingFrames = 0;

            if ( !this.throwingHeldItem )
            {
                this.AnimatePullingOutItem();
            }
            else
            {
                this.AnimateThrowingHeldItem();
            }
        }

        protected void AnimatePullingOutItem()
        {
            if ( base.frame == 2 && this.nearbyMook != null && this.nearbyMook.CanBeThrown() && this.highFive )
            {
                this.CancelMelee();
                this.ThrowBackMook( this.nearbyMook );
                this.nearbyMook = null;
                return;
            }

            if ( this.frame < 4 )
            {
                this.frameRate = 0.075f;
            }
            else
            {
                this.frameRate = 0.06f;
            }

            int row = ( (int)this.chosenItem ) + 1;

            this.sprite.SetLowerLeftPixel( (float)( base.frame * this.spritePixelWidth ), (float)( row * this.spritePixelHeight ) );

            if ( base.frame == 3 )
            {
                this.PerformMeleeAttack( true, true );
            }
            else if ( base.frame == 4 && !this.meleeHasHit )
            {
                this.PerformMeleeAttack( true, false );
            }

            if ( base.frame >= 3 )
            {
                // Indicate melee has progressed far enough to receive an item if cancelled
                this.progressedFarEnough = true;
            }

            if ( base.frame >= 11 )
            {
                base.frame = 0;
                this.CancelMelee();
            }
        }

        protected void AnimateThrowingHeldItem()
        {
            this.frameRate = 0.09f;

            int row = ( (int)this.heldItem ) + 1;

            int throwStart = 11;

            this.sprite.SetLowerLeftPixel( (float)( ( base.frame + throwStart ) * this.spritePixelWidth ), (float)( row * this.spritePixelHeight ) );

            if ( base.frame == 4 && !this.thrownItem )
            {
                this.ThrowHeldItem();
            }

            if ( base.frame >= 8 )
            {
                base.frame = 0;
                this.CancelMelee();
            }
        }

        protected void RunBarrelEffects()
        {
            if ( !this.doingMelee && this.explosionCounter > 0f )
            {
                this.explosionCounter -= this.t;
                if ( this.explosionCounter < 3f )
                {
                    this.flameCounter += this.t;
                    if ( this.flameCounter > 0f && this.explosionCounter > 0.2f )
                    {
                        if ( this.explosionCounter < 1f )
                        {
                            this.flameCounter -= 0.09f;
                        }
                        else if ( this.explosionCounter < 2f )
                        {
                            this.flameCounter -= 0.12f;
                        }
                        else
                        {
                            this.flameCounter -= 0.2f;
                        }
                        Vector3 vector = UnityEngine.Random.insideUnitCircle;
                        switch ( UnityEngine.Random.Range( 0, 3 ) )
                        {
                            case 0:
                                EffectsController.CreateEffect( this.fire1, base.X + vector.x * 3f, base.Y + vector.y * 3f + 8f, UnityEngine.Random.value * 0.0434f, Vector3.zero );
                                break;
                            case 1:
                                EffectsController.CreateEffect( this.fire2, base.X + vector.x * 3f, base.Y + vector.y * 3f + 8f, UnityEngine.Random.value * 0.0434f, Vector3.zero );
                                break;
                            case 2:
                                EffectsController.CreateEffect( this.fire3, base.X + vector.x * 3f, base.Y + vector.y * 3f + 8f, UnityEngine.Random.value * 0.0434f, Vector3.zero );
                                break;
                        }
                    }
                    this.RunBarrelWarning( this.t, this.explosionCounter );
                    if ( this.explosionCounter <= 0.1f )
                    {
                        this.ExplodeBarrelInHands();
                    }
                }
            }
        }

        protected void RunBarrelWarning( float t, float explosionTime )
        {
            if ( explosionTime < 2f )
            {
                this.warningCounter += t;
                if ( this.warningOn && this.warningCounter > 0.0667f )
                {
                    this.warningOn = false;
                    this.warningCounter -= 0.0667f;
                }
                else if ( this.warningCounter > 0.0667f && explosionTime < 0.75f )
                {
                    this.warningOn = true;
                    this.warningCounter -= 0.0667f;
                }
                else if ( this.warningCounter > 0.175f && explosionTime < 1.25f )
                {
                    this.warningOn = true;
                    this.warningCounter -= 0.175f;
                }
                else if ( this.warningCounter > 0.2f )
                {
                    this.warningOn = true;
                    this.warningCounter -= 0.2f;
                }
                this.SetGunSprite( 0, 0 );
            }
        }

        protected void ExplodeBarrelInHands()
        {
            float range = 50f;
            EffectsController.CreateExplosionRangePop( base.X, base.Y, -1f, range * 2f );
            EffectsController.CreateSparkShower( base.X, base.Y, 70, 3f, 200f, 0f, 250f, 0.6f, 0.5f );
            EffectsController.CreatePlumes( base.X, base.Y, 3, 8f, 315f, 0f, 0f );
            Vector3 vector = new Vector3( UnityEngine.Random.value, UnityEngine.Random.value );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( base.X + vector.x * range * 0.25f, base.Y + vector.y * range * 0.25f ), UnityEngine.Random.value * 0.5f );
            vector = new Vector3( UnityEngine.Random.value, UnityEngine.Random.value );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( base.X + vector.x * range * 0.25f, base.Y + vector.y * range * 0.25f ), 0f );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Large, new Vector3( base.X, base.Y ), 0f );
            SortOfFollow.Shake( 1f );
            this.sound.PlaySoundEffectAt( this.barrelExplodeSounds, 0.7f, base.transform.position, 1f, true, false, false, 0f );
            Map.DisturbWildLife( base.X, base.Y, 120f, 5 );
            MapController.DamageGround( this, 12, DamageType.Fire, range, base.X, base.Y, null, false );
            Map.ShakeTrees( base.X, base.Y, 256f, 64f, 128f );
            MapController.BurnUnitsAround_NotNetworked( this, this.playerNum, 1, range * 2f, base.X, base.Y, true, true );
            Map.ExplodeUnits( this, 12, DamageType.Fire, range * 1.2f, range, base.X, base.Y - 6f, 200f, 300f, this.playerNum, false, true, true );
            Map.ExplodeUnits( this, 1, DamageType.Fire, range * 1f, range, base.X, base.Y - 6f, 200f, 300f, this.playerNum, false, true, true );
            this.ExtraKnock( -1 * base.transform.localScale.x * 150, 400 );

            // Remove held item
            this.ClearHeldItem();
        }

        protected void ExtraKnock( float xI, float yI )
        {
            this.impaledBy = null;
            this.impaledByTransform = null;
            if ( this.frozenTime > 0f )
            {
                return;
            }
            this.xI = Mathf.Clamp( this.xI + xI / 2f, -1200f, 1200f );
            this.xIBlast = Mathf.Clamp( this.xIBlast + xI / 2f, -1200f, 1200f );
            this.yI += +yI;
            if ( this.IsParachuteActive && yI > 0f )
            {
                this.IsParachuteActive = false;
                this.Tumble();
            }
        }

        protected void PerformMeleeAttack( bool shouldTryHitTerrain, bool playMissSound )
        {
            Map.DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 7f, 0f, 0f, 6f, base.playerNum, out _, null );
            this.KickDoors( 24f );
            this.meleeChosenUnit = null;

            int meleeDamage = 9;
            DamageType meleeDamageType = DamageType.Melee;
            float xI = 200f;
            float yI = 350f;

            // Perform attack based on item type
            switch ( this.chosenItem )
            {
                // Blunt hit
                case MeleeItem.Tire:
                    break;
                case MeleeItem.Bottle:
                    meleeDamage = 8;
                    xI = 125f;
                    yI = 400f;
                    break;
                case MeleeItem.Crate:
                    meleeDamage = 11;
                    break;
                case MeleeItem.Coconut:
                    meleeDamage = 9;
                    xI = 150f;
                    yI = 400f;
                    break;
                case MeleeItem.ExplosiveBarrel:
                    meleeDamage = 11;
                    break;
                case MeleeItem.SoccerBall:
                    meleeDamage = 10;
                    xI = 125f;
                    yI = 400f;
                    break;

                // Acid hit
                case MeleeItem.AcidEgg:
                case MeleeItem.AlienEgg:
                    meleeDamageType = DamageType.Acid;
                    meleeDamage = 1;
                    break;

                // Fire hit
                case MeleeItem.Skull:
                    meleeDamageType = DamageType.Fire;
                    meleeDamage = 3;
                    break;

                // Bee hit
                case MeleeItem.Beehive:
                    meleeDamage = 8;
                    break;
            }

            if ( Map.HitClosestUnit( this, base.playerNum, meleeDamage + ( this.drunk ? 4 : 0 ), meleeDamageType, 8f, 24f, base.X + base.transform.localScale.x * 6f, base.Y + 7f, base.transform.localScale.x * xI, yI, true, false, base.IsMine, false, true ) )
            {
                this.PlayMeleeHitSound();
                this.meleeHasHit = true;
                // Create acid spray
                if ( meleeDamageType == DamageType.Acid )
                {
                    EffectsController.CreateSlimeParticlesSpray( BloodColor.Green, base.X + this.width * base.transform.localScale.x, base.Y + this.height + 4f, 1f, 34, 6f, 5f, 300f, this.xI * 0.6f, this.yI * 0.2f + 150f, 0.6f );
                    EffectsController.CreateSlimeCover( 15, base.X, base.Y + this.height + 5f, 15f, false );
                }
            }
            else if ( playMissSound )
            {
                this.PlayMeleeMissSound();
            }

            if ( shouldTryHitTerrain )
            {
                bool hitTerrain = false;
                switch ( meleeDamageType )
                {
                    case DamageType.Acid:
                        hitTerrain = this.TryMeleeTerrainAcid( 1, meleeDamage );
                        break;
                    case DamageType.Fire:
                        break;
                    default:
                        hitTerrain = this.TryMeleeTerrain( 1, meleeDamage + 3 );
                        break;
                }
                if ( hitTerrain )
                {
                    this.meleeHasHit = true;
                }
            }
            this.TriggerBroMeleeEvent();
        }

        protected override bool TryMeleeTerrain( int offset = 0, int meleeDamage = 2 )
        {
            if ( !Physics.Raycast( new Vector3( base.X - base.transform.localScale.x * 4f, base.Y + 4f, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, (float)( 16 + offset ), this.groundLayer ) )
            {
                return false;
            }
            Cage cage = this.raycastHit.collider.GetComponent<Cage>();
            if ( cage == null && this.raycastHit.collider.transform.parent != null )
            {
                cage = this.raycastHit.collider.transform.parent.GetComponent<Cage>();
            }
            if ( cage != null )
            {
                MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, cage.health, DamageType.Melee, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y );
                return true;
            }
            MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, meleeDamage, DamageType.Melee, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y );
            this.sound.PlaySoundEffectAt( this.soundHolder.meleeHitTerrainSound, 0.4f, base.transform.position, 1f, true, false, false, 0f );
            EffectsController.CreateProjectilePopWhiteEffect( base.X + this.width * base.transform.localScale.x, base.Y + this.height + 4f );
            return true;
        }

        protected bool TryMeleeTerrainAcid( int offset = 0, int meleeDamage = 2 )
        {
            if ( !Physics.Raycast( new Vector3( base.X - base.transform.localScale.x * 4f, base.Y + 4f, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, (float)( 16 + offset ), this.groundLayer ) )
            {
                return false;
            }
            Cage cage = this.raycastHit.collider.GetComponent<Cage>();
            if ( cage == null && this.raycastHit.collider.transform.parent != null )
            {
                cage = this.raycastHit.collider.transform.parent.GetComponent<Cage>();
            }
            if ( cage != null )
            {
                MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, cage.health, DamageType.Acid, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y );
                return true;
            }
            MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, meleeDamage, DamageType.Acid, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y );
            this.sound.PlaySoundEffectAt( this.soundHolder.meleeHitTerrainSound, 0.4f, base.transform.position, 1f, true, false, false, 0f );
            EffectsController.CreateProjectilePopWhiteEffect( base.X + this.width * base.transform.localScale.x, base.Y + this.height + 4f );
            // Create acid spray
            EffectsController.CreateSlimeParticlesSpray( BloodColor.Green, base.X + this.width * base.transform.localScale.x, base.Y + this.height + 4f, 1f, 34, 6f, 5f, 300f, this.xI * 0.6f, this.yI * 0.2f + 150f, 0.6f );
            EffectsController.CreateSlimeCover( 15, base.X, base.Y + this.height + 5f, 15f, false );
            return true;
        }

        protected void PlayMeleeHitSound()
        {
            this.sound.PlaySoundEffectAt( this.soundHolder.meleeHitSound, 0.6f, base.transform.position, 1f, true, false, false, 0f );
        }

        protected void PlayMeleeMissSound()
        {
            this.sound.PlaySoundEffectAt( this.soundHolder.missSounds, 0.3f, base.transform.position, 1f, true, false, false, 0f );
        }

        protected override void CancelMelee()
        {
            if ( this.drunk )
            {
                base.GetComponent<Renderer>().material = this.drunkSprite;
            }
            else
            {
                base.GetComponent<Renderer>().material = this.normalSprite;
            }

            if ( !this.throwingHeldItem && this.progressedFarEnough )
            {
                this.SwitchToHeldItem();
            }
            else if ( this.thrownItem )
            {
                this.ClearHeldItem();
            }
            else
            {
                this.throwingHeldItem = false;
            }

            this.canWallClimb = true;
            this.jumpTime = 0f;
            this.progressedFarEnough = false;
            this.throwingHeldItem = false;
            this.thrownItem = false;

            base.CancelMelee();
        }

        protected void SwitchToHeldItem()
        {
            this.holdingItem = true;
            this.heldItem = this.chosenItem;
            this.chosenItem = MeleeItem.None;
            this.gunSpriteMelee.gameObject.SetActive( this.originalGunSprite.gameObject.activeSelf );
            this.originalGunSprite.gameObject.SetActive( false );
            this.gunSprite = this.gunSpriteMeleeSprite;

            // Setup barrel warning
            if ( this.heldItem == MeleeItem.ExplosiveBarrel )
            {
                this.explosionCounter = 5f;
                this.warningOn = false;
            }
        }

        protected void ClearHeldItem()
        {
            this.canChimneyFlip = true;
            this.throwingHeldItem = false;
            this.holdingItem = false;
            this.heldItem = MeleeItem.None;
            this.thrownItem = false;
            this.originalGunSprite.gameObject.SetActive( this.gunSpriteMelee.gameObject.activeSelf );
            this.gunSpriteMelee.gameObject.SetActive( false );
            this.gunSprite = this.originalGunSprite;
        }

        protected void StartThrowingItem()
        {
            // Don't start a throw if we're already throwing, or doing another animation
            if ( this.throwingHeldItem || this.doingMelee || this.chimneyFlip || this.usingSpecial )
            {
                return;
            }

            // Release any grenades / coconuts we are holding
            if ( this.throwingHeldObject )
            {
                this.ReleaseHeldObject( false );
                this.throwingHeldObject = false;
            }

            this.throwingHeldItem = true;
            this.usingSpecial = this.fire = this.wasFire = false;

            base.frame = 0;
            base.counter = -0.05f;
            ResetMeleeValues();
            lerpToMeleeTargetPos = 0f;
            doingMelee = true;
            showHighFiveAfterMeleeTimer = 0f;
            DeactivateGun();
            SetMeleeType();
            meleeStartPos = base.transform.position;
            this.progressedFarEnough = false;
            this.canWallClimb = false;

            // Switch to melee sprite
            base.GetComponent<Renderer>().material = this.meleeSpriteGrabThrowing;

            base.ChangeFrame();
        }

        protected void ThrowHeldItem()
        {
            bool angleDownwards = this.down && this.IsOnGround() && this.ducking;
            switch ( this.heldItem )
            {
                case MeleeItem.Tire:
                    this.tireProjectile.SpawnGrenadeLocally( this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, 0f, 0f, base.transform.localScale.x * ( angleDownwards ? 150f : 275f ), angleDownwards ? 25f : 50f, base.playerNum, 0 );
                    break;
                case MeleeItem.AcidEgg:
                    this.acidEggProjectile.SpawnProjectileLocally( this, base.X + base.transform.localScale.x * 7.5f, base.Y + 14f, base.transform.localScale.x * ( angleDownwards ? 200f : 350f ), angleDownwards ? 50f : 125f, base.playerNum );
                    break;
                case MeleeItem.Beehive:
                    this.beehiveProjectile.SpawnProjectileLocally( this, base.X + base.transform.localScale.x * 8f, base.Y + 15f, base.transform.localScale.x * ( angleDownwards ? 200f : 400f ), angleDownwards ? 50f : 85f, base.playerNum );
                    break;
                case MeleeItem.Bottle:
                    this.bottleProjectile.SpawnGrenadeLocally( this, base.X + base.transform.localScale.x * 7.5f, base.Y + 14f, 0f, 0f, base.transform.localScale.x * ( angleDownwards ? 225f : 450f ), angleDownwards ? 50f : 75f, base.playerNum );
                    break;
                case MeleeItem.Crate:
                    this.crateProjectile.SpawnProjectileLocally( this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * ( angleDownwards ? 150f : 225f ), angleDownwards ? 75f : 100f, base.playerNum );
                    break;
                case MeleeItem.Coconut:
                    this.coconutProjectile.SpawnGrenadeLocally( this, base.X + base.transform.localScale.x * 8f, base.Y + 15f, 0f, 0f, base.transform.localScale.x * ( angleDownwards ? 200f : 350f ), angleDownwards ? 40f : 75f, base.playerNum );
                    break;
                case MeleeItem.ExplosiveBarrel:
                    ExplosiveBarrelProjectile explosiveBarrel = this.explosiveBarrelProjectile.SpawnGrenadeLocally( this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, 0f, 0f, base.transform.localScale.x * ( angleDownwards ? 150f : 275f ), angleDownwards ? 25f : 50f, base.playerNum, 0 ) as ExplosiveBarrelProjectile;
                    explosiveBarrel.explosionCounter = Mathf.Max( 4 - Mathf.Abs( 5 - this.explosionCounter ), 1 );
                    break;
                case MeleeItem.SoccerBall:
                    this.soccerBallProjectile.SpawnGrenadeLocally( this, base.X + base.transform.localScale.x * 10f, base.Y + 3f, 0f, 0f, base.transform.localScale.x * ( angleDownwards ? 200f : 350f ), angleDownwards ? 60f : 150f, base.playerNum, 0 );
                    break;
                case MeleeItem.AlienEgg:
                    this.alienEggProjectile.SpawnProjectileLocally( this, base.X + base.transform.localScale.x * 7.5f, base.Y + 14f, base.transform.localScale.x * ( angleDownwards ? 200f : 350f ), angleDownwards ? 50f : 125f, base.playerNum );
                    break;
                case MeleeItem.Skull:
                    this.skullProjectile.SpawnProjectileLocally( this, base.X + base.transform.localScale.x * 8f, base.Y + 15f, base.transform.localScale.x * ( angleDownwards ? 200f : 350f ), angleDownwards ? 40f : 100f, base.playerNum );
                    break;
                default:

                    break;
            }

            thrownItem = true;
        }

        protected override void RunCustomMeleeMovement()
        {
            if ( this.jumpingMelee )
            {
                this.ApplyFallingGravity();
                if ( this.yI < this.maxFallSpeed )
                {
                    this.yI = this.maxFallSpeed;
                }
            }
            else if ( this.dashingMelee )
            {
                if ( base.frame <= 1 )
                {
                    this.xI = 0f;
                    if ( this.actionState != ActionState.Jumping )
                    {
                        this.yI = 0f;
                    }
                }
                else if ( base.frame <= 3 )
                {
                    if ( this.meleeChosenUnit == null )
                    {
                        if ( !this.isInQuicksand )
                        {
                            this.xI = this.speed * 1f * base.transform.localScale.x;
                        }
                        if ( this.actionState != ActionState.Jumping )
                        {
                            this.yI = 0f;
                        }
                    }
                    else if ( !this.isInQuicksand )
                    {
                        this.xI = this.speed * 0.5f * base.transform.localScale.x + ( this.meleeChosenUnit.X - base.X ) * 6f;
                    }
                }
                else if ( base.frame <= 5 )
                {
                    if ( !this.isInQuicksand )
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
            else
            {
                this.ApplyFallingGravity();
            }
        }

        public override void SetGestureAnimation( GestureElement.Gestures gesture )
        {
            // Don't allow flexing during melee
            if ( this.doingMelee )
            {
                return;
            }
            base.SetGestureAnimation( gesture );
        }

        // Don't cancel melee when hitting wall
        protected override void HitLeftWall()
        {
        }

        // Don't cancel melee when hitting wall
        protected override void HitRightWall()
        {
        }

        // Don't allow ledge grapple to reset melee
        protected override void LedgeGrapple( bool left, bool right, float radius, float heightOpenOffset )
        {
            if ( !left || !right )
            {
                this.yI = 150f;
                RaycastHit raycastHit;
                if ( Physics.Raycast( new Vector3( base.X + ( ( !right ) ? 0f : ( radius + 3f ) ) + ( ( !left ) ? 0f : ( -radius - 3f ) ), base.Y + heightOpenOffset, 0f ), Vector3.down, out raycastHit, 23f, this.groundLayer ) )
                {
                    this.ledgeGrapple = true;
                    if ( !this.wasLedgeGrapple && !this.fire && !this.usingSpecial && !this.doingMelee && !( this.attackStationary || this.attackUpwards || this.attackForwards || this.attackDownwards ) )
                    {
                        base.frame = 0;
                        this.ChangeFrame();
                    }
                    this.ledgeOffsetY = raycastHit.point.y - base.Y;
                }
            }
        }

        // Properly cancel melee when being stopped
        public override void Stop()
        {
            this.CancelMelee();
            this.startNewAttack = false;
            this.StopAttack( false );
            base.Stop();
        }

        // Properly cancel melee when being covered in acid
        protected override void CoverInAcidRPC()
        {
            this.CancelMelee();
            base.CoverInAcidRPC();
        }
        #endregion

        #region Special
        // Animate drunk idle
        public override void AnimateActualIdleFrames()
        {
            if ( this.drunk && this.gunFrame <= 0 && !this.fire && !this.holdingItem )
            {
                this.SetSpriteOffset( 0f, 0f );
                this.DeactivateGun();
                this.frameRate = 0.14f;
                int num = base.frame % 7;
                this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( this.spritePixelHeight * 9 ) );
            }
            else
            {
                base.AnimateActualIdleFrames();
            }
        }

        protected override void AnimateActualNewRunningFrames()
        {
            this.frameRate = ( this.isInQuicksand ? ( this.runningFrameRate * 3f ) : this.runningFrameRate );
            if ( this.IsSurroundedByBarbedWire() )
            {
                this.frameRate *= 2f;
            }
            if ( this.drunk && !this.dashing )
            {
                this.frameRate = 0.07f;
            }
            int num = base.frame % 8;
            if ( base.frame % 4 == 0 && !FluidController.IsSubmerged( this ) )
            {
                EffectsController.CreateFootPoofEffect( base.X, base.Y + 2f, 0f, Vector3.up * 1f - Vector3.right * base.transform.localScale.x * 60.5f, this.GetFootPoofColor() );
            }
            if ( base.frame % 4 == 0 && !this.ledgeGrapple )
            {
                this.PlayFootStepSound();
            }
            this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( ( !this.dashing || !this.useDashFrames ) ? ( this.spritePixelHeight * 2 ) : ( this.spritePixelHeight * 4 ) ) );
            if ( this.gunFrame <= 0 && !this.doingMelee )
            {
                this.SetGunSprite( num, 1 );
            }
        }

        private bool IsWalking()
        {
            return base.Y < this.groundHeight + 0.5f && this.yI < 10f;
        }

        protected override void PressSpecial()
        {
            if ( this.usingSpecial || this.doingMelee || this.hasBeenCoverInAcid || this.throwingHeldObject )
            {
                return;
            }
            // Buffer special use if currently attacking
            if ( this.attackForwards || this.attackUpwards || this.attackDownwards || this.attackStationary )
            {
                this.bufferedSpecial = true;
                return;
            }
            // Make sure we aren't holding anything before drinking
            if ( this.heldItem == MeleeItem.None )
            {
                // Don't use additional specials if already drunk
                if ( this.drunk )
                {
                    return;
                }

                if ( this.SpecialAmmo > 0 )
                {
                    // Cancel rolling
                    if ( this.rollingFrames > 0 )
                    {
                        this.StopRolling();
                    }
                    this.wasDrunk = false;
                    this.canChimneyFlip = false;
                    this.usingSpecial = true;
                    base.frame = 0;
                    this.usingSpecialFrame = 0;
                    this.pressSpecialFacingDirection = (int)base.transform.localScale.x;
                    this.usedSpecial = false;
                    this.playedSpecialSound = false;
                    this.ChangeFrame();
                }
                else
                {
                    HeroController.FlashSpecialAmmo( base.playerNum );
                }
            }
            // Throw held item
            else
            {
                this.StartThrowingItem();
            }
        }

        protected override void AnimateSpecial()
        {
            this.SetSpriteOffset( 0f, 0f );
            this.DeactivateGun();
            this.invulnerable = true;
            this.invulnerableTime = 0.3f;
            // Animate drinking to become drunk
            if ( !this.wasDrunk )
            {
                this.frameRate = 0.1f;
                this.sprite.SetLowerLeftPixel( (float)( this.usingSpecialFrame * this.spritePixelWidth ), (float)( this.spritePixelHeight * 8 ) );
                if ( this.usingSpecialFrame < 10 && this.IsWalking() )
                {
                    this.speed = 0f;
                }
                else
                {
                    this.speed = this.originalSpeed;
                }
                if ( this.usingSpecialFrame == 4 )
                {
                    this.frameRate = 0.35f;
                    this.PlayDrinkingSound();
                }
                else if ( this.usingSpecialFrame == 6 )
                {
                    this.frameRate = 0.15f;
                    this.UseSpecial();
                }
                else if ( this.usingSpecialFrame >= 7 )
                {
                    this.frameRate = 0.2f;
                    this.StopUsingSpecial();
                    return;
                }
            }
            // Animate becoming sober
            else
            {
                this.frameRate = 0.11f;
                this.sprite.SetLowerLeftPixel( (float)( this.usingSpecialFrame * this.spritePixelWidth ), (float)( this.spritePixelHeight * 10 ) );
                if ( this.usingSpecialFrame < 11 && this.IsWalking() )
                {
                    this.speed = 0f;
                }
                else
                {
                    this.speed = this.originalSpeed;
                }

                if ( this.usingSpecialFrame == 1 )
                {
                    this.PlayBecomeSoberSound();

                    this.frameRate = 0.2f;
                }
                else if ( this.usingSpecialFrame == 6 )
                {
                    this.UseSpecial();
                }
                else if ( this.usingSpecialFrame >= 10 )
                {
                    this.StopUsingSpecial();
                    return;
                }
            }
        }

        protected void PlayDrinkingSound()
        {
            if ( this.playedSpecialSound )
            {
                return;
            }
            this.playedSpecialSound = true;
            this.sound.PlaySoundEffectAt( this.slurp, 1f, base.transform.position, 0.9f, true, false, true, 0f );
        }

        protected void PlayBecomeSoberSound()
        {
            if ( this.playedSpecialSound )
            {
                return;
            }
            this.playedSpecialSound = true;
            this.sound.PlaySoundEffectAt( this.soundHolderVoice.effortGrunt, 0.45f, base.transform.position, 0.9f, true, false, true, 0f );
        }

        protected override void UseSpecial()
        {
            if ( this.usedSpecial )
            {
                return;
            }
            this.usedSpecial = true;
            if ( !this.wasDrunk )
            {
                this.BecomeDrunk();
                --this.SpecialAmmo;
            }
            else
            {
                this.BecomeSober();
            }
        }

        protected void StopUsingSpecial()
        {
            this.StopRolling();
            base.frame = 0;
            this.usingSpecialFrame = 0;
            this.usingSpecial = false;
            this.ActivateGun();
            this.ChangeFrame();
            this.speed = this.originalSpeed;
            this.canChimneyFlip = true;
            this.usedSpecial = false;
            this.playedSpecialSound = false;
        }

        protected void BecomeDrunk()
        {
            base.GetComponent<Renderer>().material = this.drunkSprite;
            this.drunkCounter = maxDrunkTime;
            this.drunk = true;
            this.speed = this.originalSpeed = 110;
            this.enemyFistDamage = drunkEnemyFistDamage;
            this.groundFistDamage = drunkGroundFistDamage;
            this.attackDownwardsStrikeFrame = 2;
            DrunkenCameraManager.RegisterDrunk( this );
        }

        protected bool TryToBecomeSober()
        {
            if ( !hasBeenCoverInAcid && !doingMelee && !usingSpecial )
            {
                this.canChimneyFlip = false;
                this.wasDrunk = true;
                this.usingSpecial = true;
                base.frame = 0;
                this.pressSpecialFacingDirection = (int)base.transform.localScale.x;
                this.usedSpecial = false;
                this.playedSpecialSound = false;
                return true;
            }
            return false;
        }

        protected void BecomeSober()
        {
            base.GetComponent<Renderer>().material = this.normalSprite;
            this.drunkCounter = 0;
            this.drunk = false;
            this.speed = this.originalSpeed = 130;
            this.enemyFistDamage = soberEnemyFistDamage;
            this.groundFistDamage = soberGroundFistDamage;
            this.attackDownwardsStrikeFrame = 3;

            if ( this.rollingFrames > 0 )
            {
                this.StopRolling();
            }

            DrunkenCameraManager.UnregisterDrunk( this );
        }

        protected void ResetCameraTilt()
        {
            if ( this.drunk )
            {
                DrunkenCameraManager.UnregisterDrunk( this );
            }
        }

        public override void Death( float xI, float yI, DamageObject damage )
        {
            DrunkenCameraManager.UnregisterDrunk( this );
            base.Death( xI, yI, damage );
        }

        protected override void OnDestroy()
        {
            DrunkenCameraManager.UnregisterDrunk( this, true );
        }
        #endregion

        #region Movement
        protected override void StopAirDashing()
        {
            base.StopAirDashing();
            this.hasAttackedDownwards = false;
            this.hasAttackedUpwards = false;
            this.hasAttackedForwards = false;
        }

        protected override void RunMovement()
        {
            if ( this.health <= 0 )
            {
                this.xIAttackExtra = 0f;
            }
            base.RunMovement();
        }

        protected override void CheckFacingDirection()
        {
            // Don't allow changing directions when using a forwards attack or doing melee
            if ( !this.attackForwards && !this.doingMelee )
            {
                base.CheckFacingDirection();
            }
        }

        protected override void ApplyFallingGravity()
        {
            if ( this.postAttackHitPauseTime < 0f )
            {
                if ( this.chimneyFlip || this.isInQuicksand )
                {
                    base.ApplyFallingGravity();
                }
                else if ( !this.attackForwards )
                {
                    if ( this.attackDownwards && this.attackFrames < this.attackDownwardsStrikeFrame )
                    {
                        this.yI -= 1100f * this.t * 0.3f;
                    }
                    else if ( this.attackUpwards && this.attackFrames >= this.attackUpwardsStrikeFrame )
                    {
                        this.yI -= 1100f * this.t * 0.5f;
                    }
                    else
                    {
                        base.ApplyFallingGravity();
                    }
                }
                // Attack forwards gravity
                else
                {
                    if ( !this.drunk )
                    {
                        if ( this.attackFrames > this.attackForwardsStrikeFrame )
                        {
                            base.ApplyFallingGravity();
                        }
                    }
                    else
                    {
                        if ( this.attackFrames > 5 )
                        {
                            base.ApplyFallingGravity();
                        }
                    }
                }
            }
        }

        protected bool CanAddXSpeed()
        {
            return !this.attackDownwards && !this.attackUpwards && this.postAttackHitPauseTime < 0f;
        }

        protected override void AddSpeedLeft()
        {
            // Don't add speed left if attacking forward facing right
            if ( !( this.attackForwards && this.attackDirection == 1 ) && this.CanAddXSpeed() )
            {
                base.AddSpeedLeft();
                if ( this.attackForwards && this.attackFrames > 4 && this.xI < -this.speed * 0.5f )
                {
                    this.xI = -this.speed * 0.5f;
                }
            }
        }

        protected override void AddSpeedRight()
        {
            // Don't add speed right if attacking forward facing left
            if ( !( this.attackForwards && this.attackDirection == -1 ) && this.CanAddXSpeed() )
            {
                base.AddSpeedRight();
                if ( this.attackForwards && this.attackFrames > 4 && this.xI > this.speed * 0.5f )
                {
                    this.xI = this.speed * 0.5f;
                }
            }
        }

        // Don't grab ladder when doing upwards / downwards attacks
        protected override bool IsOverLadder( ref float ladderXPos )
        {
            return !( this.attackUpwards || this.attackDownwards ) && base.IsOverLadder( ref ladderXPos );
        }

        // Don't grab ladder when doing drunk downwards attack
        protected override bool IsOverLadder( float xOffset, ref float ladderXPos )
        {
            return !( this.attackUpwards || this.attackDownwards ) && base.IsOverLadder( xOffset, ref ladderXPos );
        }

        #region Roll
        protected void RunRolling()
        {
            if ( rollingFrames > 0 )
            {
                if ( this.isSlideRoll )
                {
                    if ( this.rollingFrames <= 12 )
                    {
                        if ( this.rollingFrames >= 7 )
                        {
                            float boostSpeed = 20f + ( this.drunk ? 10f : 0f );
                            this.slideExtraSpeed = Mathf.Lerp( this.slideExtraSpeed, boostSpeed, this.t * 5f );
                        }
                        else
                        {

                            float boostSpeed = 30f + ( this.drunk ? 10f : 0f );
                            this.slideExtraSpeed = Mathf.Lerp( this.slideExtraSpeed, boostSpeed, this.t * 5f );
                        }
                    }
                    else
                    {
                        float decelerationSpeed = this.drunk ? -80f * 0.8f : -80f; // Drunk mode: +20% slide distance
                        this.slideExtraSpeed = Mathf.Lerp( this.slideExtraSpeed, decelerationSpeed, this.t * 3f );
                    }

                    // Handle invulnerability during slide roll
                    if ( this.drunk && this.rollingFrames <= 26 && this.rollingFrames >= 13 )
                    {
                        this.invulnerable = true;
                        this.invulnerableTime = 0.1f; // Keep refreshing invulnerability
                    }
                    else if ( !this.drunk && this.rollingFrames <= 24 && this.rollingFrames >= 15 )
                    {
                        this.invulnerable = true;
                        this.invulnerableTime = 0.1f; // Keep refreshing invulnerability
                    }

                    // Deflect projectiles only during attack frames
                    if ( this.rollingFrames <= 10 && this.rollingFrames >= 1 )
                    {
                        Map.DeflectProjectiles( this, this.playerNum, 8f,
                            base.X + base.transform.localScale.x * 2f, base.Y + 6f,
                            base.transform.localScale.x * 200f, true );
                    }
                }
            }
            else
            {
                this.slideExtraSpeed = 0f;
            }

            if ( !this.doingMelee )
            {
                this.speed = this.originalSpeed + this.slideExtraSpeed;
            }


            this.dashSlideCooldown -= this.t;

            if ( this.bufferedSlideRoll )
            {
                this.bufferedSlideRollTime -= this.t;
                if ( this.bufferedSlideRollTime <= 0f )
                {
                    this.bufferedSlideRoll = false;
                }
            }

            if ( base.actionState == ActionState.Jumping )
            {
                this.StopRolling();
            }
        }

        protected override void Jump( bool wallJump )
        {
            // Don't allow wall jumping while doing melee
            if ( this.doingMelee && wallJump )
            {
                return;
            }
            // Allow jumping after strike frame on upwards, forwards, and stationary attacks
            if ( ( !this.attackUpwards || this.attackFrames > this.attackUpwardsStrikeFrame ) && ( !this.attackForwards || this.attackFrames > this.attackForwardsStrikeFrame ) && ( !this.attackStationary || this.attackFrames > this.attackStationaryStrikeFrame || this.hasHitThisAttack ) )
            {
                // Don't allow jumping during drunk downwards attack
                if ( !( this.drunk && this.attackDownwards ) )
                {
                    base.Jump( wallJump );

                    // Switch to jumping melee
                    if ( this.doingMelee )
                    {
                        this.jumpingMelee = true;
                        this.dashingMelee = false;
                    }
                }
            }
            // Cancel slide
            this.StopRolling();
        }

        protected override void StartDashing()
        {
            bool isNewDashPress = this.dashButton && !this.wasdashButton;
            bool isDoubleTap = false;

            if ( isNewDashPress && this.lastDashTime > 0 && Time.time - this.lastDashTime <= this.doubleTapWindow )
            {
                isDoubleTap = true;
            }

            base.StartDashing();

            if ( isDoubleTap && this.dashSlideCooldown <= 0f && this.rollingFrames <= 0 )
            {
                if ( this.actionState == ActionState.Running )
                {
                    this.slideExtraSpeed = this.originalSpeed * 0.75f;
                    this.RollOnLand( true );
                    this.dashSlideCooldown = this.drunk ? 0.5f : 0.6f;
                }
                else
                {
                    this.bufferedSlideRoll = true;
                    this.bufferedSlideRollTime = 0.5f;
                }
            }

            if ( isNewDashPress )
            {
                this.lastDashTime = Time.time;
            }
        }

        protected override void Land()
        {
            if ( this.attackDownwards )
            {
                if ( !this.drunk )
                {
                    this.DownwardHitGround();
                    base.Land();
                }
                else if ( !this.hasHitThisAttack )
                {
                    this.DownwardHitGroundDrunk();
                }
                else
                {
                    base.Land();
                }
            }
            else
            {
                float yI = this.yI;

                // Clear slide roll flag before landing (base.Land might trigger a normal roll)
                this.isSlideRoll = false;

                base.Land();

                // Reset dash time when landing to prevent false double-tap detection across jumps
                this.lastDashTime = -1f;

                if ( this.bufferedSlideRoll && this.dashSlideCooldown <= 0f && this.rollingFrames <= 0 )
                {
                    this.bufferedSlideRoll = false;
                    this.slideExtraSpeed = this.originalSpeed * 0.75f;
                    this.RollOnLand( true );
                    this.dashSlideCooldown = this.drunk ? 0.5f : 0.6f;
                }

                // Switch to dashing melee
                if ( this.doingMelee )
                {
                    this.dashingMelee = true;
                    this.jumpingMelee = false;
                }

                if ( this.rollingFrames > 0 && this.isSlideRoll && this.rollingFrames >= 26 )
                {
                    this.slideExtraSpeed = Mathf.Abs( yI ) * 0.3f;

                    // Shorten animation to make it look more natural
                    this.rollingFrames -= 1;
                    this.ChangeFrame();
                }

            }
        }

        protected override bool CanDoRollOnLand()
        {
            if ( this.bufferedSlideRoll || this.dashSlideCooldown > 0.55f )
            {
                return false;
            }

            return this.doRollOnLand && this.yI < -350f && this.skinnedMookOnMyBack == null;
        }

        protected override void RollOnLand()
        {
            RollOnLand( false );
        }

        protected void RollOnLand( bool forceSlideRoll )
        {
            // Force slide roll when requested (double-tap dash) or when drunk
            if ( forceSlideRoll || this.drunk )
            {
                this.rollingFrames = 27;
                this.isSlideRoll = true;
            }
            else
            {
                this.isSlideRoll = false;
                base.RollOnLand();
            }

            // Disable invulnerability flash during roll
            InvulnerabilityFlash flash = base.GetComponent<InvulnerabilityFlash>();
            if ( flash != null )
            {
                flash.enabled = false;
            }
        }

        // Don't stop rolling when slide rolling into wall
        protected override void AssignPushingTime()
        {
            if ( !( this.isSlideRoll && this.rollingFrames > 0 ) )
            {
                base.AssignPushingTime();
            }
        }

        protected override void StopRolling()
        {
            if ( this.rollSound != null && this.rollSound.isPlaying )
            {
                this.rollSound.Stop();
                this.rollSound = null;
            }
            this.isSlideRoll = false;
            InvulnerabilityFlash flash = base.GetComponent<InvulnerabilityFlash>();
            if ( flash != null )
            {
                flash.enabled = true;
            }
            base.StopRolling();
        }

        protected override void AnimateRolling()
        {
            if ( !this.isSlideRoll )
            {
                base.AnimateRolling();
                return;
            }

            // Frame 27 - 23 = falling onto ground
            // Frame 22 - 18 = lying on ground
            // Frame 17 - 11 = getting up
            // Frame 10 - 6  = first attack
            // Frame 5 - 1   = second attack
            this.frameRate = 0.075f;
            int lastFrame = 27;
            // Laying on ground
            if ( this.rollingFrames <= ( lastFrame - 5 ) && this.rollingFrames >= ( lastFrame - 10 ) )
            {
                this.frameRate = 0.065f;
            }
            // Get up attack
            else if ( this.rollingFrames < 12 )
            {
                this.frameRate = 0.05f;
            }

            if ( this.rollingFrames > 10 && this.rollingFrames < ( lastFrame - 2 ) && Mathf.Abs( this.xI ) > 30f )
            {
                EffectsController.CreateFootPoofEffect( base.X - base.transform.localScale.x * 6f, base.Y + 1.5f, 0f, new Vector3( base.transform.localScale.x * -20f, 0f ), BloodColor.None );
            }
            if ( this.rollingFrames == ( lastFrame - 4 ) )
            {
                this.rollSound = this.sound.PlaySoundEffectAt( this.soundHolder.attack4Sounds, 0.25f, base.transform.position, 1f, true, false, false, 0f );
            }

            if ( this.rollingFrames <= 10 && this.rollingFrames >= 6 )
            {
                if ( this.rollingFrames == 10 )
                {
                    this.PlayAttackSound();
                    this.MakeKungfuSound();

                    int damage = this.drunk ? 12 : 8;
                    float knockbackX = base.transform.localScale.x * 250f;
                    float knockbackY = 350f;

                    this.hasHitThisAttack = false;
                    this.hasMadeEffects = false;
                    DamageDoodads( 3, DamageType.Knifed, base.X + base.transform.localScale.x * 6f, base.Y + 6f, 0f, 0f, 8f, base.playerNum, out _, null );
                    this.hasMadeEffects = false;

                    if ( Map.HitUnits( this, this, this.playerNum, damage, DamageType.Crush, 12f, 12f, base.X + base.transform.localScale.x * 6f, base.Y + 6f, knockbackX, knockbackY, true, false, false, false ) )
                    {
                        this.PlayPrimaryHitSound( 0.5f );
                        SortOfFollow.Shake( this.drunk ? 0.5f : 0.3f );
                    }
                    else if ( this.hitSpecialDoodad )
                    {
                        this.playedWallHit = false;
                        this.PlayWallSound();
                    }

                    this.KickDoors( 24f );
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 4f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 10f );
                }
            }
            else if ( this.rollingFrames <= 5 && this.rollingFrames >= 1 )
            {
                if ( this.rollingFrames == 5 )
                {
                    this.PlayAttackSound();

                    int damage = this.drunk ? 12 : 8;
                    float knockbackX = base.transform.localScale.x * 300f;
                    float knockbackY = 400f;

                    this.hasHitThisAttack = false;
                    this.hasMadeEffects = false;
                    DamageDoodads( 3, DamageType.Knifed, base.X + base.transform.localScale.x * 6f, base.Y + 6f, 0f, 0f, 8f, base.playerNum, out _, null );
                    this.hasMadeEffects = false;

                    if ( Map.HitUnits( this, this, this.playerNum, damage, DamageType.Crush, 12f, 12f, base.X + base.transform.localScale.x * 6f, base.Y + 6f, knockbackX, knockbackY, true, false, this.drunk, false ) )
                    {
                        this.PlayPrimaryHitSound( 0.5f );
                        SortOfFollow.Shake( this.drunk ? 0.5f : 0.3f );

                        if ( this.drunk )
                        {
                            Map.ExplodeUnits( this, 3, DamageType.Knock, 10f, 10f, base.X + base.transform.localScale.x * 6f, base.Y + 6f, knockbackX * 1.5f, knockbackY * 1.2f, this.playerNum, false, false, false );
                        }
                    }
                    else if ( this.hitSpecialDoodad )
                    {
                        this.playedWallHit = false;
                        this.PlayWallSound();
                    }

                    this.KickDoors( 24f );
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 4f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 10f );
                }
            }

            this.sprite.SetLowerLeftPixel( (float)( ( lastFrame - this.rollingFrames ) * this.spritePixelWidth ), (float)( this.spritePixelHeight * 16 ) );
            this.DeactivateGun();
        }
        #endregion
        #endregion
    }
}
