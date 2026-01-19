using System;
using System.Collections.Generic;
using System.Reflection;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.CustomObjects.Projectiles;
using Drunken_Broster.MeleeItems;
using Drunken_Broster.Triggers;
using HarmonyLib;
using RocketLib;
using RocketLib.CustomTriggers;
using Rogueforce;
using UnityEngine;
using Random = UnityEngine.Random;
using ResourcesController = BroMakerLib.ResourcesController;

namespace Drunken_Broster
{
    [HeroPreset( "Drunken Broster" )]
    public class DrunkenBroster : CustomHero
    {
        // General
        protected bool acceptedDeath;
        bool wasInvulnerable;
        private Traverse setRumbleTraverse;
        protected Material normalSprite;
        protected Material drunkSprite;

        // Primary
        protected float postAttackHitPauseTime;
        protected bool hasHitThisAttack;
        protected float faderTrailDelay;
        protected float lastSoundTime;
        protected int attackSpriteRow;
        protected List<Unit> alreadyHit = new List<Unit>();
        protected float hitClearCounter = 0f;
        protected bool hasHitWithWall;
        protected bool hasHitWithFists;
        protected bool hasMadeEffects;
        protected bool attackStationary;
        public bool attackUpwards;
        public bool attackDownwards;
        public bool attackForwards;
        protected int stationaryAttackCounter = -1;
        protected int attackDirection;
        protected bool hasAttackedUpwards;
        protected bool hasAttackedDownwards;
        protected bool hasAttackedForwards;
        protected int attackFrames;
        protected int attackStationaryStrikeFrame = 4;
        protected int attackUpwardsStrikeFrame = 2;
        protected int attackDownwardsStrikeFrame = 3;
        protected int attackForwardsStrikeFrame = 2;
        protected bool attackHasHit;
        protected const int soberEnemyFistDamage = 10;
        protected const int soberGroundFistDamage = 10;
        protected const int drunkEnemyFistDamage = 15;
        protected const int drunkGroundFistDamage = 15;
        protected int enemyFistDamage = soberEnemyFistDamage;
        protected int groundFistDamage = soberGroundFistDamage;
        public Shrapnel shrapnelSpark;
        public FlickerFader hitPuff;
        protected float lastAttackingTime;
        protected bool startNewAttack;
        public float fistVolume = 0.7f;
        public float wallHitVolume = 0.25f;
        protected bool playedWallHit;

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
        protected MeleeItem lastThrownItem = MeleeItem.None;
        protected MeleeItem chosenItem = MeleeItem.None;
        protected MeleeItem heldItem = MeleeItem.None;
        protected bool holdingItem;
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
        protected bool throwingHeldItem;
        protected bool thrownItem;
        protected bool hitSpecialDoodad;
        protected bool progressedFarEnough;
        protected float explosionCounter = -1f;
        protected float flameCounter;
        protected float warningCounter;
        protected bool warningOn;
        public FlickerFader fire1, fire2, fire3;
        public AudioClip[] barrelExplodeSounds;
        public AudioClip[] soccerKickSounds;
        [SaveableSetting]
        public static List<MeleeItem> EnabledMeleeItems = new List<MeleeItem>
        {
            MeleeItem.Tire,
            MeleeItem.AcidEgg,
            MeleeItem.Beehive,
            MeleeItem.Bottle,
            MeleeItem.Crate,
            MeleeItem.Coconut,
            MeleeItem.ExplosiveBarrel,
            MeleeItem.SoccerBall,
            MeleeItem.AlienEgg,
            MeleeItem.Skull,
        };
        [SaveableSetting]
        public static bool CompletelyRandomMeleeItems;

        // Special
        public AudioClip slurp;
        public bool wasDrunk; // Was drunk before starting special
        public bool drunk;
        protected const float maxDrunkTime = 12f;
        public float drunkCounter;
        protected int usingSpecialFrame; // Used to avoid problems with jumping / wall climbing resetting special animation
        protected float originalSpeed;
        [SaveableSetting]
        public static bool enableCameraTilt = true;
        [SaveableSetting]
        public static bool lowIntensityMode;
        public bool IsDoingMelee => doingMelee;
        protected bool usedSpecial;
        protected bool playedSpecialSound;
        protected bool bufferedSpecial;

        // Roll
        [SaveableSetting]
        public static bool doubleTapDashToRoll = true;
        [SaveableSetting]
        public static bool pressKeybindToRoll;
        public static KeyBindingForPlayers rollKey = AllModKeyBindings.LoadKeyBinding( "Drunken Broster", "Roll Key" );
        protected float slideExtraSpeed;
        protected bool isSlideRoll;
        protected float dashSlideCooldown;
        protected float lastDashTime = -1f;
        protected float doubleTapWindow = 0.3f;
        protected bool bufferedSlideRoll;
        protected float bufferedSlideRollTime;
        protected AudioSource rollSound;

        #region General
        protected override void Start()
        {
            base.Start();

            // Needed to have custom melee functions called, actual type is irrelevant
            meleeType = MeleeType.Disembowel;
            currentMeleeType = meleeType;

            originalSpeed = speed;
            BroLee broLeePrefab = HeroController.GetHeroPrefab( HeroType.BroLee ) as BroLee;
            faderSpritePrefab = broLeePrefab.faderSpritePrefab;
            shrapnelSpark = broLeePrefab.shrapnelSpark;
            hitPuff = broLeePrefab.hitPuff;

            // Load sprites
            normalSprite = GetComponent<Renderer>().material;
            drunkSprite = ResourcesController.GetMaterial( DirectoryPath, "drunkSprite.png" );

            // Setup melee gunsprite
            gunSpriteMelee = new GameObject( "GunSpriteMelee", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } ).GetComponent<MeshRenderer>();
            gunSpriteMelee.transform.parent = transform;
            gunSpriteMelee.gameObject.SetActive( false );
            gunSpriteMelee.material = ResourcesController.GetMaterial( DirectoryPath, "gunSpriteMelee.png" );
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
            originalGunSprite = gunSprite;

            // Load melee sprite for grabbing and throwing animations
            meleeSpriteGrabThrowing = ResourcesController.GetMaterial( DirectoryPath, "meleeSpriteGrabThrowing.png" );

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
            if ( player != null )
            {
                setRumbleTraverse = Traverse.Create( player ).Method( "SetRumble", new Type[] { typeof( float ) } );
            }

            // Check if infinite drunk trigger is active
            bool infiniteDrunk = CustomTriggerStateManager.Get<bool>( "DrunkenBroster_InfiniteDrunk" );

            // Immediately become drunk on spawn if infinite drunk is active
            if ( infiniteDrunk )
            {
                BecomeDrunk();
            }
        }

        protected override void Update()
        {
            base.Update();
            // Don't run any code past this point if the character is dead
            if ( acceptedDeath )
            {
                if ( health <= 0 && !WillReviveAlready )
                {
                    return;
                }
                // Revived

                acceptedDeath = false;
            }

            if ( invulnerable )
            {
                wasInvulnerable = true;
            }

            // Check if invulnerability ran out
            if ( wasInvulnerable && !invulnerable )
            {
                // Fix any not currently displayed textures
                wasInvulnerable = false;
                normalSprite.SetColor( "_TintColor", Color.gray );
                drunkSprite.SetColor( "_TintColor", Color.gray );
                meleeSpriteGrabThrowing.SetColor( "_TintColor", Color.gray );
                gunSpriteMelee.material.SetColor( "_TintColor", Color.gray );
            }

            // Check if character has died
            if ( actionState == ActionState.Dead && !acceptedDeath )
            {
                if ( !WillReviveAlready )
                {
                    acceptedDeath = true;
                }
            }

            // Count down to becoming sober
            if ( drunk )
            {
                bool infiniteDrunk = CustomTriggerStateManager.Get<bool>( "DrunkenBroster_InfiniteDrunk" );
                if ( !infiniteDrunk )
                {
                    drunkCounter -= t;
                }

                // If drunk counter has run out, try to start becoming sober animation
                if ( drunkCounter <= 0 )
                {
                    TryToBecomeSober();
                }
            }

            DrunkenCameraManager.UpdateCameraTilt();

            postAttackHitPauseTime -= t;
            if ( ( attackForwards || attackUpwards || attackDownwards ) && xIAttackExtra != 0f )
            {
                faderTrailDelay -= t / Time.timeScale;
                if ( faderTrailDelay < 0f )
                {
                    CreateFaderTrailInstance();
                    faderTrailDelay = 0.034f;
                }
            }

            // Ensure we're never left holding a grenade if we somehow set throwingHeldObject
            if ( heldGrenade != null && !throwingHeldObject )
            {
                ReleaseHeldObject( false );
            }

            // Run flame / warning effect if holding explosive barrel
            if ( holdingItem && heldItem == MeleeItem.ExplosiveBarrel )
            {
                RunBarrelEffects();
            }

            RunRolling();
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
            // Only display tooltip if it's currently unset (otherwise we'll display BroMaker's tooltips
            if ( rollKey.OnGUI( out _, ( GUI.tooltip == string.Empty ) ) )
            {
                pressKeybindToRoll = true;
            }
            GUILayout.Space( 10 );

            if ( doubleTapDashToRoll != ( doubleTapDashToRoll = GUILayout.Toggle( doubleTapDashToRoll, "Double tap dash to perform slide roll" ) ) )
            {
                SaveSettings();
            }

            if ( pressKeybindToRoll != ( pressKeybindToRoll = GUILayout.Toggle( pressKeybindToRoll, "Press custom keybinding to perform slide roll" ) ) )
            {
                SaveSettings();
            }

            GUILayout.Space( 20 );
            GUILayout.Label( "Enabled Items:" );
            foreach ( MeleeItem item in Enum.GetValues( typeof( MeleeItem ) ) )
            {
                if ( item == MeleeItem.None ) continue;
                bool previousEnabledState = EnabledMeleeItems.Contains( item );
                bool newEnabledState = GUILayout.Toggle( previousEnabledState, item.ToString() );
                if ( previousEnabledState != newEnabledState )
                {
                    if ( newEnabledState )
                    {
                        EnabledMeleeItems.Add( item );
                    }
                    else
                    {
                        EnabledMeleeItems.Remove( item );
                    }
                    SaveSettings();
                }
            }

            GUILayout.Space( 10 );
            CompletelyRandomMeleeItems = GUILayout.Toggle( CompletelyRandomMeleeItems, "Allow all enabled melee items to be pulled on any level, and use equal weights" );

        }

        public override void HarmonyPatches( Harmony harmony )
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll( assembly );
        }

        public override void PreloadAssets()
        {
            PreloadSprites( DirectoryPath, new List<string> { "drunkSprite.png", "gunSpriteMelee.png", "meleeSpriteGrabThrowing.png" } );
            PreloadSprites( ProjectilePath, new List<string> { "AcidEggProjectile.png", "AlienEggProjectile.png", "BeehiveProjectile.png", "BottleProjectile.png", "CoconutProjectile.png", "CrateProjectile.png", "ExplosiveBarrelProjectile.png", "ExplosiveBarrelProjectileWarning.png", "SoccerBallProjectile.png", "TireProjectile.png" } );
            PreloadSounds( SoundPath, new List<string> { "barrelBounce0.wav", "beeHiveSmash0.wav", "beeHiveSmash1.wav", "beeHiveSmash2.wav", "coconutDeath1.wav", "coconutDeath2.wav", "coconutHit1.wav", "coconutHit2.wav", "coconutHit3.wav", "coconutHit4.wav", "coconutHit5.wav", "egg_burst0.wav", "egg_burst1.wav", "egg_burst2.wav", "egg_pulse0.wav", "egg_pulse1.wav", "kungFu0.wav", "kungFu1.wav", "kungFu10.wav", "kungFu11.wav", "kungFu12.wav", "kungFu2.wav", "kungFu3.wav", "kungFu4.wav", "kungFu5.wav", "kungFu6.wav", "kungFu7.wav", "kungFu8.wav", "kungFu9.wav", "meleeHitBlunt0.wav", "meleeHitBlunt1.wav", "slide_0.wav", "slide_1.wav", "slurp.wav", "soccerBounce1.wav", "soccerBounce2.wav", "soccerBounce3.wav", "tireDeath.wav" } );
        }

        public override void RegisterCustomTriggers()
        {
            CustomTriggerManager.RegisterCustomTrigger( typeof( DrunkenBrosterMeleePoolAction ), typeof( DrunkenBrosterMeleePoolActionInfo ), "Drunken Broster - Set Melee Item Pool", "Custom Bros" );
            CustomTriggerManager.RegisterCustomTrigger( typeof( DrunkenBrosterBecomeDrunkAction ), typeof( DrunkenBrosterBecomeDrunkActionInfo ), "Drunken Broster - Become Drunk", "Custom Bros" );
        }

        public override void BeforePrefabSetup()
        {
            SoundHolderHeroType = HeroType.BroLee;
        }

        public override void AfterPrefabSetup()
        {
            ThemeHolder theme = Map.Instance.jungleThemeReference;
            BarrelBlock explosiveBarrel = theme.blockPrefabBarrels[1].GetComponent<BarrelBlock>();
            fire1 = explosiveBarrel.fire1;
            fire2 = explosiveBarrel.fire2;
            fire3 = explosiveBarrel.fire3;
            barrelExplodeSounds = explosiveBarrel.soundHolder.deathSounds;

            soundHolder.attack3Sounds = ResourcesController.GetAudioClipArray( SoundPath, "kungFu", 13 );

            soundHolder.attack4Sounds = ResourcesController.GetAudioClipArray( SoundPath, "slide_", 2 );

            slurp = ResourcesController.GetAudioClip( SoundPath, "slurp.wav" );

            soundHolder.meleeHitSound = ResourcesController.GetAudioClipArray( SoundPath, "meleeHitBlunt", 2 );

            soccerKickSounds = ResourcesController.GetAudioClipArray( SoundPath, "soccerBounce", 2, 2 );
        }
        #endregion

        #region Primary
        protected override void RunGun()
        {
            if ( !WallDrag && gunFrame > 0 )
            {
                gunCounter += t;
                if ( gunCounter > 0.015f )
                {
                    gunCounter -= 0.015f;
                    gunFrame--;
                    if ( gunFrame < 0 )
                    {
                        gunFrame = 0;
                    }
                    SetGunSprite( 0, 0 );
                }
            }
        }

        protected override void SetGunSprite( int spriteFrame, int spriteRow )
        {
            if ( holdingItem )
            {
                spriteRow += ( (int)heldItem ) * 2;

                if ( heldItem == MeleeItem.ExplosiveBarrel && warningOn )
                {
                    spriteRow += 8;
                }
            }

            if ( actionState == ActionState.Hanging )
            {
                gunSprite.SetLowerLeftPixel( gunSpritePixelWidth * ( gunSpriteHangingFrame + spriteFrame ), gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
            else if ( actionState == ActionState.ClimbingLadder && hangingOneArmed )
            {
                gunSprite.SetLowerLeftPixel( gunSpritePixelWidth * ( gunSpriteHangingFrame + spriteFrame ), gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
            else if ( attachedToZipline != null && actionState == ActionState.Jumping )
            {
                gunSprite.SetLowerLeftPixel( gunSpritePixelWidth * ( gunSpriteHangingFrame + spriteFrame ), gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
            else
            {
                gunSprite.SetLowerLeftPixel( gunSpritePixelWidth * spriteFrame, gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
        }

        protected override void StartFiring()
        {
            if ( rollingFrames > 11 )
            {
                fire = wasFire = false;
                return;
            }

            // Don't allow attacking while drinking / becoming sober / using melee
            if ( usingSpecial || doingMelee )
            {
                fire = wasFire = false;
                return;
            }

            // If holding an item from melee, throw it instead
            if ( holdingItem )
            {
                StartThrowingItem();
                return;
            }

            // Release any grenades / coconuts we are holding
            if ( throwingHeldObject )
            {
                ReleaseHeldObject( false );
                throwingHeldObject = false;
            }

            startNewAttack = false;
            hasHitWithWall = false;
            hasHitWithFists = false;
            hasMadeEffects = false;
            // Buffering attack
            if ( attackForwards || attackDownwards || attackUpwards || attackStationary )
            {
                startNewAttack = true;
            }
            // Upward Attack Start
            else if ( up && !hasAttackedUpwards )
            {
                StartAttackUpwards();
            }
            // Downward Attack Start
            else if ( down && !hasAttackedDownwards )
            {
                StartAttackDownwards();
            }
            // Forward Attack Left Start
            else if ( left && !hasAttackedForwards )
            {
                StartAttackForwards( false );
            }
            // Forward Attack Right Start
            else if ( right && !hasAttackedForwards )
            {
                StartAttackForwards( true );
            }
            // Stationary Attack Start
            else if ( !up && !down && !left && !right )
            {
                StartAttackStationary();
            }
        }

        protected void StartAttackUpwards()
        {
            if ( actionState == ActionState.ClimbingLadder )
            {
                actionState = ActionState.Jumping;
            }
            FireFlashAvatar();
            MakeKungfuSound();
            StopAttack();
            if ( yI > 50f )
            {
                yI = 50f;
            }
            jumpTime = 0f;
            hasAttackedUpwards = true;
            attackFrames = 0;
            attackUpwards = true;
            ChangeFrame();
            airdashDirection = DirectionEnum.Up;
            ClearCurrentAttackVariables();
        }

        protected void StartAttackDownwards()
        {
            actionState = ActionState.Jumping;
            StopAttack();
            FireFlashAvatar();
            if ( !drunk )
            {
                yI = 150f;
                xI = transform.localScale.x * 80f;
            }
            else
            {
                yI = 200f;
                xI = transform.localScale.x * 80f;
                canWallClimb = false;
            }
            MakeKungfuSound();
            hasAttackedDownwards = true;
            attackFrames = 0;
            attackDownwards = true;
            jumpTime = 0f;
            ChangeFrame();
            airdashDirection = DirectionEnum.Down;
            ClearCurrentAttackVariables();
        }

        protected void StartAttackForwards( bool rightAttack )
        {
            FireFlashAvatar();
            attackSpriteRow = ( attackSpriteRow + 1 ) % 2;
            if ( actionState == ActionState.ClimbingLadder )
            {
                actionState = ActionState.Jumping;
            }
            if ( ( attackForwards || attackUpwards || attackDownwards ) && !hasHitThisAttack )
            {
                StopAttack();
                if ( !drunk )
                {
                    xIAttackExtra = rightAttack ? -20f : 20f;
                }
            }
            else if ( ( attackForwards || attackUpwards || attackDownwards ) && hasHitThisAttack )
            {
                StopAttack();
                if ( !drunk )
                {
                    xIAttackExtra = rightAttack ? 300f : -300f;
                }
                MakeKungfuSound();
            }
            else
            {
                StopAttack();
                if ( !drunk )
                {
                    xIAttackExtra = rightAttack ? 200f : -200f;
                }
                MakeKungfuSound();
            }
            postAttackHitPauseTime = 0f;
            hasAttackedForwards = true;
            attackFrames = 0;
            yI = 0f;
            attackForwards = true;
            attackDirection = rightAttack ? 1 : -1;
            transform.localScale = new Vector3( rightAttack ? 1f : -1f, yScale, 1f );
            jumpTime = 0f;
            ChangeFrame();
            CreateFaderTrailInstance();
            airdashDirection = rightAttack ? DirectionEnum.Right : DirectionEnum.Left;
            ClearCurrentAttackVariables();
        }

        protected void StartAttackStationary()
        {
            FireFlashAvatar();
            StopAttack();
            MakeKungfuSound();
            postAttackHitPauseTime = 0f;
            attackFrames = 0;
            attackStationary = true;
            jumpTime = 0f;
            ++stationaryAttackCounter;
            // Leg Sweep
            if ( stationaryAttackCounter % 2 == 0 )
            {
                attackStationaryStrikeFrame = 4;
            }
            // Forward punch
            {
                attackStationaryStrikeFrame = 2;
            }
            ChangeFrame();
            //this.CreateFaderTrailInstance();
            ClearCurrentAttackVariables();
        }

        protected override void RunFiring()
        {
            if ( fire )
            {
                rollingFrames = 0;
            }
            if ( attackStationary || attackUpwards || attackForwards || attackDownwards )
            {
                if ( !attackHasHit || drunk )
                {
                    if ( attackStationary && attackFrames >= attackStationaryStrikeFrame - 1 )
                    {
                        DeflectProjectiles();
                    }
                    else if ( attackForwards && attackFrames >= attackForwardsStrikeFrame - 1 )
                    {
                        DeflectProjectiles();
                    }
                    else if ( attackUpwards && attackFrames >= attackUpwardsStrikeFrame - 1 )
                    {
                        DeflectProjectiles();
                    }
                    else if ( attackDownwards && attackFrames >= attackDownwardsStrikeFrame - 1 )
                    {
                        DeflectProjectiles();
                    }
                }
                // Stationary Attack
                if ( attackStationary && attackFrames >= attackStationaryStrikeFrame && attackFrames <= 5 )
                {
                    PerformAttackStationary();
                }
                // Forwards Attack
                else if ( attackForwards && attackFrames >= attackForwardsStrikeFrame - 1 && attackFrames <= 5 )
                {
                    PerformAttackForwards();
                }
                // Upwards Attack
                else if ( attackUpwards && attackFrames >= attackUpwardsStrikeFrame && attackFrames <= 5 )
                {
                    PerformAttackUpwards();
                }
                // Downwards Attack
                else if ( attackDownwards && attackFrames >= attackDownwardsStrikeFrame && attackFrames <= 6 )
                {
                    PerformAttackDownwards();
                }
            }
        }

        protected void PerformAttackStationary()
        {
            lastAttackingTime = Time.time;
            // Leg Sweep Attack
            if ( stationaryAttackCounter % 2 == 0 )
            {
                DamageDoodads( 3, DamageType.Knifed, X + (float)( Direction * 4 ), Y + 7f, 10f * transform.localScale.x, 950f, 6f, playerNum, out _, this );
                if ( HitUnitsStationaryAttack( this, playerNum, enemyFistDamage + 2, 1, DamageType.Blade, 13f, 13f, X + transform.localScale.x * 7f, Y + 7f, 10f * transform.localScale.x, 950f, true, true, false, alreadyHit ) )
                {
                    if ( !hasHitWithFists )
                    {
                        PlayPrimaryHitSound();
                    }
                    hasHitWithFists = true;
                    attackHasHit = true;
                    hasAttackedDownwards = false;
                    hasAttackedUpwards = false;
                    hasAttackedForwards = false;
                    hasHitThisAttack = true;
                    TimeBump();
                    xIAttackExtra = 0f;
                    postAttackHitPauseTime = 0.2f;
                    xI = 0f;
                    yI = 0f;
                    foreach (Unit t1 in alreadyHit)
                    {
                        t1.FrontSomersault();
                    }
                }
                // Pause if we hit one of drunken master's doodads
                else if ( hitSpecialDoodad && !hasHitThisAttack )
                {
                    if ( !hasHitWithFists )
                    {
                        PlayWallSound();
                    }
                    hasHitWithFists = true;
                    attackHasHit = true;
                    hasAttackedDownwards = false;
                    hasAttackedUpwards = false;
                    hasAttackedForwards = false;
                    hasHitThisAttack = true;
                    TimeBump();
                    xIAttackExtra = 0f;
                    postAttackHitPauseTime = 0.1f;
                    xI = 0f;
                    yI = 0f;
                }
            }
            // Perform Forward Fist Attack
            else
            {
                DamageDoodads( 3, DamageType.Knifed, X + (float)( Direction * 4 ), Y + 7f, 700f * transform.localScale.x, 250f, 6f, playerNum, out _, this );
                if ( HitUnitsStationaryAttack( this, playerNum, enemyFistDamage + 2, 1, DamageType.Blade, 13f, 8f, X + transform.localScale.x * 7f, Y + 5f, 700f * transform.localScale.x, 250f, true, true, false, alreadyHit ) )
                {
                    if ( !hasHitWithFists )
                    {
                        PlayPrimaryHitSound();
                    }
                    hasHitWithFists = true;
                    attackHasHit = true;
                    hasAttackedDownwards = false;
                    hasAttackedUpwards = false;
                    hasAttackedForwards = false;
                    hasHitThisAttack = true;
                    TimeBump();
                    xIAttackExtra = 0f;
                    postAttackHitPauseTime = 0.2f;
                    xI = 0f;
                    yI = 0f;
                    foreach (Unit t1 in alreadyHit)
                    {
                        t1.FrontSomersault();
                    }
                }
                // Pause if we hit one of drunken master's doodads
                else if ( hitSpecialDoodad && !hasHitThisAttack )
                {
                    if ( !hasHitWithFists )
                    {
                        PlayWallSound();
                    }
                    hasHitWithFists = true;
                    attackHasHit = true;
                    hasAttackedDownwards = false;
                    hasAttackedUpwards = false;
                    hasAttackedForwards = false;
                    hasHitThisAttack = true;
                    TimeBump();
                    xIAttackExtra = 0f;
                    postAttackHitPauseTime = 0.1f;
                    xI = 0f;
                    yI = 0f;
                }
            }

            if ( !attackHasHit || drunk )
            {
                DeflectProjectiles();
            }
            if ( !attackHasHit )
            {
                FireWeaponGround( X + transform.localScale.x * 3f, Y + 6f, new Vector3( transform.localScale.x, 0f, 0f ), 9f );
                FireWeaponGround( X + transform.localScale.x * 3f, Y + 12f, new Vector3( transform.localScale.x, 0f, 0f ), 9f );
            }
        }

        protected void PerformAttackForwards()
        {
            lastAttackingTime = Time.time;
            // Sober attack
            if ( !drunk )
            {
                DamageDoodads( 3, DamageType.Knifed, X + (float)( Direction * 4 ), Y + 7f, transform.localScale.x * 250f + xI, 200f, 6f, playerNum, out _, this );
                if ( HitUnits( this, playerNum, enemyFistDamage, 1, DamageType.Blade, 7f, 13f, X + transform.localScale.x * ( 3f + attackSpriteRow == 0 ? 1f : 0f ), Y + 7f, transform.localScale.x * 520f, 225f, true, true, true, alreadyHit, false ) )
                {
                    if ( !hasHitWithFists )
                    {
                        PlayPrimaryHitSound();
                    }
                    hasHitWithFists = true;
                    attackHasHit = true;
                    hasAttackedDownwards = false;
                    hasAttackedUpwards = false;
                    hasAttackedForwards = false;
                    hasHitThisAttack = true;
                    TimeBump();
                    xIAttackExtra = 0f;
                    postAttackHitPauseTime = 0.2f;
                    xI = 0f;
                    yI = 0f;
                    foreach (Unit t1 in alreadyHit)
                    {
                        t1.BackSomersault( false );
                    }
                }
                // Pause if we hit one of drunken master's doodads
                else if ( hitSpecialDoodad && !hasHitThisAttack )
                {
                    if ( !hasHitWithFists )
                    {
                        PlayWallSound();
                    }
                    hasHitWithFists = true;
                    attackHasHit = true;
                    hasAttackedDownwards = false;
                    hasAttackedUpwards = false;
                    hasAttackedForwards = false;
                    hasHitThisAttack = true;
                    TimeBump();
                    xIAttackExtra = 0f;
                    postAttackHitPauseTime = 0.1f;
                    xI = 0f;
                    yI = 0f;
                }
            }
            // Drunk attack
            else
            {
                // Spin attack
                if ( attackSpriteRow == 0 )
                {
                    DamageDoodads( 3, DamageType.Knifed, X + (float)( Direction * 4 ), Y + 7f, transform.localScale.x * 220f, 450f, 6f, playerNum, out _, this );
                    if ( HitUnits( this, playerNum, enemyFistDamage, 1, DamageType.Blade, 4f, 10f, X + transform.localScale.x * 7f, Y + 7f, transform.localScale.x * 220f, 450f, true, true, false, alreadyHit, true ) )
                    {
                        if ( !hasHitWithFists )
                        {
                            PlayPrimaryHitSound();
                        }
                        hasHitWithFists = true;
                        attackHasHit = true;
                        hasAttackedDownwards = false;
                        hasAttackedUpwards = false;
                        hasAttackedForwards = false;
                        hasHitThisAttack = true;
                        foreach (Unit t1 in alreadyHit)
                        {
                            t1.BackSomersault( false );
                        }
                    }
                    else if ( hitSpecialDoodad && !hasHitThisAttack )
                    {
                        if ( !hasHitWithFists )
                        {
                            PlayWallSound();
                        }
                        hasHitWithFists = true;
                        attackHasHit = true;
                        hasAttackedDownwards = false;
                        hasAttackedUpwards = false;
                        hasAttackedForwards = false;
                        hasHitThisAttack = true;
                    }
                }
                // Fist attack
                else
                {
                    DamageDoodads( 3, DamageType.Knifed, X + (float)( Direction * 4 ), Y + 7f, transform.localScale.x * 520f, 200f, 6f, playerNum, out _, this );
                    if ( HitUnits( this, playerNum, enemyFistDamage + 8, 3, DamageType.Blade, 5f, 8f, X + transform.localScale.x * 7f, Y + 7f, transform.localScale.x * 700f, 250f, true, true, false, alreadyHit, false ) )
                    {
                        if ( !hasHitWithFists )
                        {
                            PlayPrimaryHitSound();
                        }
                        hasHitWithFists = true;
                        attackHasHit = true;
                        hasAttackedDownwards = false;
                        hasAttackedUpwards = false;
                        hasAttackedForwards = false;
                        hasHitThisAttack = true;
                        TimeBump( 0.4f );
                        xIAttackExtra = 0f;
                        postAttackHitPauseTime = 0.25f;
                        xI = 0f;
                        yI = 0f;
                        foreach (Unit t1 in alreadyHit)
                        {
                            t1.BackSomersault( false );
                        }
                    }
                    // Pause if we hit one of drunken master's doodads
                    else if ( hitSpecialDoodad && !hasHitThisAttack )
                    {
                        if ( !hasHitWithFists )
                        {
                            PlayWallSound();
                        }
                        hasHitWithFists = true;
                        attackHasHit = true;
                        hasAttackedDownwards = false;
                        hasAttackedUpwards = false;
                        hasAttackedForwards = false;
                        hasHitThisAttack = true;
                        TimeBump();
                        xIAttackExtra = 0f;
                        postAttackHitPauseTime = 0.1f;
                        xI = 0f;
                        yI = 0f;
                    }
                }

            }
            if ( !attackHasHit || drunk )
            {
                DeflectProjectiles();
            }
            if ( !attackHasHit )
            {
                FireWeaponGround( X + transform.localScale.x * 3f, Y + 6f, new Vector3( transform.localScale.x, 0f, 0f ), 9f );
                FireWeaponGround( X + transform.localScale.x * 3f, Y + 12f, new Vector3( transform.localScale.x, 0f, 0f ), 9f );
            }
        }

        protected void PerformAttackUpwards()
        {
            if ( actionState == ActionState.ClimbingLadder )
            {
                actionState = ActionState.Jumping;
            }
            DamageDoodads( 3, DamageType.Knifed, X + (float)( Direction * 6f ), Y + 12f, transform.localScale.x * 80f, 1100f, 7f, playerNum, out _, this );
            lastAttackingTime = Time.time;
            if ( HitUnits( this, playerNum, enemyFistDamage, 1, DamageType.Blade, 13f, 13f, X + transform.localScale.x * 6f, Y + 12f, transform.localScale.x * 80f, 1100f, true, true, true, alreadyHit, true ) )
            {
                if ( !hasHitWithFists )
                {
                    PlayPrimaryHitSound();
                }
                hasHitWithFists = true;
                attackHasHit = true;
                hasAttackedDownwards = false;
                hasAttackedForwards = false;
                hasHitThisAttack = true;
                if ( drunk )
                {
                    TimeBump( 0.1f );
                }
                else
                {
                    TimeBump();
                }
            }
            // Pause if we hit one of drunken master's doodads
            else if ( hitSpecialDoodad && !hasHitThisAttack )
            {
                if ( !hasHitWithFists )
                {
                    PlayWallSound();
                }
                hasHitWithFists = true;
                attackHasHit = true;
                hasAttackedDownwards = false;
                hasAttackedForwards = false;
                hasHitThisAttack = true;
                TimeBump();
            }
            if ( !attackHasHit || drunk )
            {
                DeflectProjectiles();
            }
            if ( !attackHasHit )
            {
                FireWeaponGround( X + transform.localScale.x * 3f, Y + 6f, new Vector3( transform.localScale.x * 0.5f, 1f, 0f ), 12f );
                FireWeaponGround( X + transform.localScale.x * 3f, Y + 6f, new Vector3( transform.localScale.x, 0.5f, 0f ), 12f );
            }
        }

        protected void PerformAttackDownwards()
        {
            if ( actionState == ActionState.ClimbingLadder )
            {
                actionState = ActionState.Jumping;
            }
            // Sober Attack
            if ( !drunk )
            {
                DamageDoodads( 3, DamageType.Knifed, X + (float)( Direction * 4 ), Y + 2f, transform.localScale.x * 120f, 100f, 6f, playerNum, out _, this );
                lastAttackingTime = Time.time;
                if ( HitUnits( this, playerNum, enemyFistDamage + 2, 1, DamageType.Blade, 9f, 4f, X + transform.localScale.x * 7f, Y + 3f, transform.localScale.x * 120f, 100f, true, true, true, alreadyHit, true ) )
                {
                    if ( !hasHitWithFists )
                    {
                        PlayPrimaryHitSound();
                    }
                    hasHitWithFists = true;
                    attackHasHit = true;
                    hasAttackedForwards = false;
                    hasAttackedUpwards = false;
                    hasHitThisAttack = true;
                    xIAttackExtra = 0f;
                    TimeBump();
                    postAttackHitPauseTime = 0.08f;
                    xI = 0f;
                    yI = 0f;
                }
                // Pause if we hit one of drunken master's doodads
                else if ( hitSpecialDoodad && !hasHitThisAttack )
                {
                    if ( !hasHitWithFists )
                    {
                        PlayWallSound();
                    }
                    hasHitWithFists = true;
                    attackHasHit = true;
                    hasAttackedForwards = false;
                    hasAttackedUpwards = false;
                    hasHitThisAttack = true;
                    xIAttackExtra = 0f;
                    TimeBump();
                    postAttackHitPauseTime = 0.1f;
                    xI = 0f;
                    yI = 0f;
                }
                if ( !attackHasHit )
                {
                    DeflectProjectiles();
                }
                if ( !attackHasHit )
                {
                    FireWeaponGround( X + transform.localScale.x * 3f, Y + 6f, new Vector3( transform.localScale.x * 0.4f, -1f, 0f ), 14f );
                    FireWeaponGround( X + transform.localScale.x * 3f, Y + 6f, new Vector3( transform.localScale.x * 0.8f, -0.2f, 0f ), 12f );
                }
            }
            // Drunk Attack
            else
            {
                DamageDoodads( 3, DamageType.Knifed, X + (float)( Direction * 4 ), Y, transform.localScale.x * 120f, 100f, 6f, playerNum, out _, this );
                lastAttackingTime = Time.time;
                DeflectProjectiles();
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
            if ( !attackHasHit && attackFrames < 7 )
            {
                FireWeaponGround( X + transform.localScale.x * 16.5f, Y + 16.5f, Vector3.down, 18f + Mathf.Abs( yI * t ) );
            }
            if ( !attackHasHit && attackFrames < 7 )
            {
                FireWeaponGround( X + transform.localScale.x * 5.5f, Y + 16.5f, Vector3.down, 18f + Mathf.Abs( yI * t ) );
            }
            attackDownwards = false;
            attackFrames = 0;
        }

        protected void DownwardHitGroundDrunk()
        {
            // Fast-forward frames if landed early
            if ( attackFrames < 5 )
            {
                attackFrames = 5;
                ChangeFrame();
            }
            attackHasHit = true;
            hasHitThisAttack = true;
            canWallClimb = true;
            ExplosionGroundWave explosionGroundWave = EffectsController.CreateShockWave( X, Y + 4f, 50f );
            explosionGroundWave.playerNum = playerNum;
            explosionGroundWave.avoidObject = this;
            explosionGroundWave.origins = this;
            if ( Map.HitUnits( this, this, playerNum, 20, DamageType.Crush, 30f, 10f, X, Y - 4f, 0f, yI, true, false, true, false ) )
            {
                PlayPrimaryHitSound();
                TimeBump( 0.3f );
            }
            MapController.DamageGround( this, 35, DamageType.Crush, 50f, X, Y + 8f );
            xI = ( xIBlast = 0f );
        }

        protected void FireWeaponGround( float x, float y, Vector3 raycastDirection, float distance )
        {
            if ( Physics.Raycast( new Vector3( x, y, 0f ), raycastDirection, out raycastHit, distance, groundLayer ) )
            {
                if ( !hasHitWithWall )
                {
                    SortOfFollow.Shake( 0.15f );
                    MakeEffects();
                }
                // Deal extra damage to bosses
                int damage = groundFistDamage;
                if ( raycastHit.collider.gameObject.layer == 30 )
                {
                    if ( drunk )
                    {
                        damage += 12;
                    }
                    else
                    {
                        damage += 7;
                    }
                }
                MapController.Damage_Networked( this, raycastHit.collider.gameObject, damage, DamageType.Blade, xI, 0f, raycastHit.point.x, raycastHit.point.y );
                // If we hit something on the LargeObjects layer, don't continue hitting stuff because it could be a boss
                if ( BroMakerUtilities.IsBoss( raycastHit.collider.gameObject ) || raycastHit.collider.gameObject.layer == 30 )
                {
                    hasHitWithWall = true;
                    attackHasHit = true;
                }
                // If we hit a steelblock, then don't allow further hits
                else if ( drunk && raycastHit.collider.gameObject.GetComponent<SteelBlock>() != null )
                {
                    hasHitWithWall = true;
                    attackHasHit = true;
                }
                // If we're not drunk then let the hit register, otherwise allow hits to continue in drunk mode
                else if ( !drunk )
                {
                    hasHitWithWall = true;
                    attackHasHit = true;
                }
                PlayWallSound();
            }
        }

        protected virtual void MakeEffects( float x, float y, float xI, float yI )
        {
            if ( hasMadeEffects )
            {
                return;
            }
            hasMadeEffects = true;
            EffectsController.CreateShrapnel( shrapnelSpark, x, y, 4f, 30f, 3f, xI, yI );
            EffectsController.CreateEffect( hitPuff, x, y, 0f );
        }

        protected virtual void MakeEffects()
        {
            if ( hasMadeEffects )
            {
                return;
            }
            hasMadeEffects = true;
            EffectsController.CreateShrapnel( shrapnelSpark, raycastHit.point.x + raycastHit.normal.x * 3f, raycastHit.point.y + raycastHit.normal.y * 3f, 4f, 30f, 3f, raycastHit.normal.x * 60f, raycastHit.normal.y * 30f );
            EffectsController.CreateEffect( hitPuff, raycastHit.point.x + raycastHit.normal.x * 3f, raycastHit.point.y + raycastHit.normal.y * 3f );
        }

        protected override void SetGunPosition( float xOffset, float yOffset )
        {
            gunSprite.transform.localPosition = new Vector3( xOffset, yOffset - 1f, -1f );
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
                            // Send units hit in midair further
                            if ( !unit.IsOnGround() && ( drunk || unit.health <= 0 ) )
                            {
                                xI *= drunk ? 1.75f : 1.25f;
                                yI *= drunk ? 1.75f : 1.25f;
                            }
                            else
                            {
                                xI *= drunk ? 1.7f : 1.2f;
                                yI *= drunk ? 1.7f : 1.2f;
                            }
                            // Cap speeds to +/- 1400
                            xI = Mathf.Clamp( xI, -1400f, 1400f );
                            yI = Mathf.Clamp( yI, -1400f, 1400f );
                            if ( !canGib && unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y );
                            }
                            else if ( unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( corpseDamage, playerNum ), damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y );
                            }
                            else
                            {
                                damage = ValueOrchestrator.GetModifiedDamage( damage, playerNum );
                                // Don't allow instagibs
                                if ( damage > unit.health )
                                {
                                    damage = unit.health;
                                }
                                Map.KnockAndDamageUnit( damageSender, unit, damage, damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y );
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
                // Don't hit dead units unless allowed or if they're in midair
                if ( unit != null && GameModeController.DoesPlayerNumDamage( playerNum, unit.playerNum ) && !unit.invulnerable && unit.health <= num && ( hitDead || unit.health > 0 || !unit.IsOnGround() ) )
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
                            if ( !unit.IsOnGround() && ( drunk || unit.health <= 0 ) )
                            {
                                xI *= drunk ? 3.0f : 2.5f;
                                yI *= drunk ? 1.5f : 1.25f;
                            }
                            // Cap speeds to +/- 1400
                            xI = Mathf.Clamp( xI, -1400f, 1400f );
                            yI = Mathf.Clamp( yI, -1400f, 1400f );
                            if ( !canGib && unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y );
                            }
                            else if ( unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( corpseDamage, playerNum ), damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y );
                            }
                            else
                            {
                                damage = ValueOrchestrator.GetModifiedDamage( damage, playerNum );
                                // Don't allow instagibs
                                if ( damage > unit.health )
                                {
                                    damage = unit.health;
                                }
                                Map.KnockAndDamageUnit( damageSender, unit, damage, damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y );
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
                                if ( hasHitThisAttack )
                                {
                                    continue;
                                }
                                hitSpecialDoodad = true;

                                // Create hit effect
                                Vector2 currentPoint = new Vector2( x, y );
                                Vector2 centerPoint = new Vector2( circularDoodad.centerX, circularDoodad.centerY );
                                float distance = Vector2.Distance( currentPoint, centerPoint ) - circularDoodad.radius;
                                Vector2 hitPoint = Vector2.MoveTowards( currentPoint, centerPoint, distance + 0.5f );
                                MakeEffects( hitPoint.x, hitPoint.y, xI, yI );
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
            if ( Map.DeflectProjectiles( this, playerNum, 10f, X + Mathf.Sign( transform.localScale.x ) * 6f, Y + 6f, Mathf.Sign( transform.localScale.x ) * 200f, true ) )
            {
                if ( !hasHitWithWall )
                {
                    PlayWallSound();
                }
                hasHitWithWall = true;
                attackHasHit = true;
            }
        }

        protected override void HitCeiling( RaycastHit ceilingHit )
        {
            if ( !drunk )
            {
                base.HitCeiling( ceilingHit );
                if ( attackUpwards )
                {
                    if ( !attackHasHit && attackFrames < 7 )
                    {
                        FireWeaponGround( X + transform.localScale.x * 16.5f, Y + 2f, Vector3.up, headHeight + Mathf.Abs( yI * t ) );
                    }
                    if ( !attackHasHit && attackFrames < 7 )
                    {
                        FireWeaponGround( X + transform.localScale.x * 4.5f, Y + 2f, Vector3.up, headHeight + Mathf.Abs( yI * t ) );
                    }
                    attackUpwards = false;
                    attackFrames = 0;
                }
            }
            else
            {
                if ( attackUpwards )
                {
                    if ( !attackHasHit && attackFrames < 7 )
                    {
                        FireWeaponGround( X + transform.localScale.x * 16.5f, Y + 2f, Vector3.up, headHeight + Mathf.Abs( yI * t ) );
                    }
                    if ( !attackHasHit && attackFrames < 7 )
                    {
                        FireWeaponGround( X + transform.localScale.x * 4.5f, Y + 2f, Vector3.up, headHeight + Mathf.Abs( yI * t ) );
                    }
                }
                HitCeilingDrunk( ceilingHit );
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
                yIT = ceilingHit.point.y - headHeight - Y;
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
            hasHitThisAttack = false;
            attackStationary = ( attackUpwards = ( attackDownwards = ( attackForwards = false ) ) );
            playedWallHit = false;
            hasMadeEffects = false;
            attackFrames = 0;
            frame = 0;
            xIAttackExtra = 0f;
            if ( Y > groundHeight + 1f )
            {
                if ( actionState != ActionState.ClimbingLadder )
                {
                    actionState = ActionState.Jumping;
                }
            }
            else if ( right || left )
            {
                actionState = ActionState.Running;
            }
            else
            {
                actionState = ActionState.Idle;
            }

            if ( allowBuffering )
            {
                // Use special if it was buffered during attack
                if ( bufferedSpecial )
                {
                    bufferedSpecial = false;
                    PressSpecial();
                }
                // Start a new attack if it was buffered during attack
                else if ( startNewAttack )
                {
                    startNewAttack = false;
                    StartFiring();
                }
            }

            if ( Y < groundHeight + 1f )
            {
                StopAirDashing();
            }
            canWallClimb = true;
        }

        protected void AnimateAttackStationary()
        {
            // Leg Sweep
            if ( stationaryAttackCounter % 2 == 0 )
            {
                DeactivateGun();
                if ( attackFrames < attackStationaryStrikeFrame )
                {
                    frameRate = 0.0667f;
                }
                else if ( attackFrames < 5 )
                {
                    frameRate = 0.055f;
                }
                else
                {
                    frameRate = 0.055f;
                }
                if ( attackFrames == 8 )
                {
                    if ( startNewAttack )
                    {
                        startNewAttack = false;
                        StartFiring();
                    }
                }
                if ( attackFrames == attackStationaryStrikeFrame )
                {
                    FireWeaponGround( X + transform.localScale.x * 9f, Y + 6f, new Vector3( transform.localScale.x, 0f, 0f ), 8f );
                    PlayAttackSound();
                }
                if ( attackFrames == attackStationaryStrikeFrame + 1 )
                {
                    xIAttackExtra = 0f;
                }
                if ( attackFrames >= 8 )
                {
                    StopAttack();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( attackFrames, 0, 7 );
                    sprite.SetLowerLeftPixel( num * spritePixelWidth, spritePixelHeight * 6 );
                }
            }
            // Punch forward
            else
            {
                DeactivateGun();
                if ( attackFrames < attackStationaryStrikeFrame )
                {
                    frameRate = 0.075f;
                }
                else if ( attackFrames < 5 )
                {
                    frameRate = 0.055f;
                }
                else
                {
                    frameRate = 0.055f;
                }
                if ( attackFrames == 8 )
                {
                    if ( startNewAttack )
                    {
                        startNewAttack = false;
                        StartFiring();
                    }
                }
                if ( attackFrames == attackStationaryStrikeFrame )
                {
                    FireWeaponGround( X + transform.localScale.x * 9f, Y + 6f, new Vector3( transform.localScale.x, 0f, 0f ), 8f );
                    PlayAttackSound();
                }
                if ( attackFrames == attackStationaryStrikeFrame + 1 )
                {
                    xIAttackExtra = 0f;
                }
                if ( attackFrames >= 8 )
                {
                    StopAttack();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( attackFrames, 0, 7 );
                    sprite.SetLowerLeftPixel( num * spritePixelWidth, spritePixelHeight * 7 );
                }
            }
        }

        protected void AnimateAttackForwards()
        {
            // Sober animation
            if ( !drunk )
            {
                DeactivateGun();
                if ( attackFrames < attackForwardsStrikeFrame )
                {
                    frameRate = 0.055f;
                }
                else if ( attackFrames < 5 )
                {
                    frameRate = 0.065f;
                }
                else
                {
                    frameRate = 0.055f;
                }

                if ( attackFrames < attackForwardsStrikeFrame + 1 )
                {
                    CreateFaderTrailInstance();
                }
                else if ( attackFrames == 8 )
                {
                    if ( startNewAttack )
                    {
                        startNewAttack = false;
                        StartFiring();
                    }
                }

                if ( attackFrames == attackForwardsStrikeFrame )
                {
                    FireWeaponGround( X + transform.localScale.x * 9f, Y + 6f, new Vector3( transform.localScale.x, 0f, 0f ), 8f );
                    PlayAttackSound();
                }
                else if ( attackFrames == attackForwardsStrikeFrame + 1 )
                {
                    xIAttackExtra = 0f;
                }
                if ( attackFrames >= 8 )
                {
                    StopAttack();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( attackFrames, 0, 7 );
                    sprite.SetLowerLeftPixel( (float)( num * spritePixelWidth ), (float)( spritePixelHeight * ( 8 + attackSpriteRow ) ) );
                }
            }
            // Drunk animation
            else
            {
                DeactivateGun();
                if ( attackFrames < attackForwardsStrikeFrame )
                {
                    frameRate = 0.10f;
                }
                else if ( attackFrames < 5 )
                {
                    frameRate = 0.075f;
                }
                else
                {
                    frameRate = 0.075f;
                }

                if ( attackFrames < attackForwardsStrikeFrame + 1 )
                {
                    CreateFaderTrailInstance();
                }
                else if ( attackFrames == 8 )
                {
                    if ( startNewAttack )
                    {
                        startNewAttack = false;
                        StartFiring();
                    }
                }

                // Spin attack
                if ( attackSpriteRow == 0 )
                {
                    if ( attackFrames > 2 && attackFrames < 8 )
                    {
                        xIAttackExtra = attackDirection * 275f;
                    }
                    else
                    {
                        xIAttackExtra = 0f;
                    }
                }
                // Fist attack
                else
                {
                    if ( attackFrames > 1 && attackFrames < 7 && !attackHasHit )
                    {
                        xIAttackExtra = attackDirection * 275f;
                    }
                    else
                    {
                        xIAttackExtra = 0f;
                    }
                }


                if ( attackFrames == attackForwardsStrikeFrame )
                {
                    FireWeaponGround( X + transform.localScale.x * 9f, Y + 6f, new Vector3( transform.localScale.x, 0f, 0f ), 3f );
                    PlayAttackSound();
                }
                if ( attackFrames >= 8 )
                {
                    StopAttack();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( attackFrames, 0, 7 );
                    sprite.SetLowerLeftPixel( (float)( num * spritePixelWidth ), (float)( spritePixelHeight * ( 8 + attackSpriteRow ) ) );
                }
            }
        }

        protected void AnimateAttackUpwards()
        {
            // Sober animation
            if ( !drunk )
            {
                DeactivateGun();
                if ( attackFrames < attackUpwardsStrikeFrame )
                {
                    frameRate = 0.075f;
                }
                else if ( attackFrames < 5 )
                {
                    frameRate = 0.065f;
                }
                else
                {
                    frameRate = 0.065f;
                }
                if ( attackFrames == attackUpwardsStrikeFrame )
                {
                    xI = transform.localScale.x * 50f;
                    yI = 240f;
                    PlayAttackSound();
                }
                if ( attackFrames < attackUpwardsStrikeFrame + 2 )
                {
                    CreateFaderTrailInstance();
                }
                if ( startNewAttack && attackFrames == attackUpwardsStrikeFrame + 1 )
                {
                    startNewAttack = false;
                    StartFiring();
                }
                if ( hasHitThisAttack && attackFrames == 6 )
                {
                    xIAttackExtra = 0f;
                    postAttackHitPauseTime = 0.25f;
                    xI = 0f;
                    yI = 0f;
                }
                if ( attackFrames >= 10 || ( attackFrames == 8 && startNewAttack ) )
                {
                    StopAttack();
                    ChangeFrame();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( attackFrames, 0, 7 );
                    sprite.SetLowerLeftPixel( (float)( num * spritePixelWidth ), (float)( spritePixelHeight * 10 ) );
                }
            }
            // Drunk animation
            else
            {
                DeactivateGun();
                if ( attackFrames < attackUpwardsStrikeFrame )
                {
                    frameRate = 0.1f;
                }
                else if ( attackFrames < 5 )
                {
                    frameRate = 0.11f;
                }
                else
                {
                    frameRate = 0.075f;
                }
                if ( attackFrames == attackUpwardsStrikeFrame )
                {
                    xI = transform.localScale.x * 50f;
                    yI = 325f;
                    PlayAttackSound();
                }
                if ( attackFrames < attackUpwardsStrikeFrame + 2 )
                {
                    CreateFaderTrailInstance();
                }
                if ( startNewAttack && attackFrames == attackUpwardsStrikeFrame + 1 )
                {
                    startNewAttack = false;
                    StartFiring();
                }
                if ( hasHitThisAttack && attackFrames == 6 )
                {
                    xIAttackExtra = 0f;
                    postAttackHitPauseTime = 0.25f;
                    xI = 0f;
                    yI = 0f;
                }
                if ( attackFrames >= 10 || ( attackFrames == 8 && startNewAttack ) )
                {
                    StopAttack();
                    ChangeFrame();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( attackFrames, 0, 7 );
                    sprite.SetLowerLeftPixel( (float)( num * spritePixelWidth ), (float)( spritePixelHeight * 10 ) );
                }
            }
        }

        protected void AnimateAttackDownwards()
        {
            if ( !drunk )
            {
                DeactivateGun();
                if ( attackFrames < attackDownwardsStrikeFrame )
                {
                    frameRate = 0.0767f;
                }
                else if ( attackFrames <= 5 )
                {
                    frameRate = 0.0767f;
                }
                else
                {
                    frameRate = 0.06f;
                }
                if ( attackFrames < attackDownwardsStrikeFrame + 2 )
                {
                    CreateFaderTrailInstance();
                }
                if ( startNewAttack && attackFrames == attackDownwardsStrikeFrame + 1 )
                {
                    startNewAttack = false;
                    StartFiring();
                }
                if ( attackFrames == attackDownwardsStrikeFrame )
                {
                    if ( !usingSpecial || !hasHitThisAttack )
                    {
                        yI = -250f;
                    }
                    xI = transform.localScale.x * 60f;
                    PlayAttackSound();
                }
                if ( attackFrames >= 9 || ( attackFrames == 6 && startNewAttack ) )
                {
                    StopAttack();
                    ChangeFrame();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( attackFrames, 0, 7 );
                    sprite.SetLowerLeftPixel( (float)( num * spritePixelWidth ), (float)( spritePixelHeight * 11 ) );
                }
                if ( attackFrames == 7 && usingSpecial )
                {
                    if ( !hasHitThisAttack )
                    {
                        usingSpecial = false;
                    }
                    if ( usingSpecial )
                    {
                        UseSpecial();
                    }
                }
            }
            else
            {
                DeactivateGun();
                if ( attackFrames < attackDownwardsStrikeFrame )
                {
                    frameRate = 0.19f;
                }
                else if ( attackFrames <= 5 )
                {
                    frameRate = 0.08f;
                }
                else
                {
                    frameRate = 0.08f;
                }
                if ( attackFrames < attackDownwardsStrikeFrame + 2 )
                {
                    CreateFaderTrailInstance();
                }

                // Hold frame until hitting ground
                if ( !hasHitThisAttack && attackFrames > 5 )
                {
                    attackFrames = 5;
                }

                if ( attackFrames == attackDownwardsStrikeFrame )
                {
                    if ( !usingSpecial || !hasHitThisAttack )
                    {
                        yI = -300f;
                    }
                    xI = transform.localScale.x * 60f;
                    PlayAttackSound();
                }
                if ( attackFrames >= 9 )
                {
                    StopAttack();
                    ChangeFrame();
                }
                else
                {
                    int num = 24 + Mathf.Clamp( attackFrames, 0, 7 );
                    sprite.SetLowerLeftPixel( (float)( num * spritePixelWidth ), (float)( spritePixelHeight * 11 ) );
                }
            }
        }

        protected override void IncreaseFrame()
        {
            base.IncreaseFrame();
            if ( attackStationary || attackUpwards || attackDownwards || attackForwards )
            {
                ++attackFrames;
            }
            else if ( usingSpecial )
            {
                ++usingSpecialFrame;
            }
        }

        protected override void ChangeFrame()
        {
            if ( health <= 0 || chimneyFlip )
            {
                base.ChangeFrame();
            }
            else if ( attackStationary )
            {
                AnimateAttackStationary();
            }
            else if ( attackUpwards )
            {
                AnimateAttackUpwards();
            }
            else if ( attackDownwards )
            {
                AnimateAttackDownwards();
            }
            else if ( attackForwards )
            {
                AnimateAttackForwards();
            }
            else
            {
                base.ChangeFrame();
            }
        }

        protected override void AnimateZipline()
        {
            base.AnimateZipline();
            SetGunSprite( gunFrame, 0 );
        }

        protected override void CreateFaderTrailInstance()
        {
            FaderSprite component = faderSpritePrefab.GetComponent<FaderSprite>();
            FaderSprite faderSprite = EffectsController.InstantiateEffect( component, transform.position, transform.rotation ) as FaderSprite;
            if ( faderSprite != null )
            {
                faderSprite.transform.localScale = transform.localScale;
                faderSprite.SetMaterial( GetComponent<Renderer>().material, sprite.lowerLeftPixel, sprite.pixelDimensions, sprite.offset );
                faderSprite.fadeM = 0.15f;
                faderSprite.maxLife = 0.15f;
                faderSprite.moveForwards = true;
            }
        }

        protected override void FireFlashAvatar()
        {
            if ( drunk )
            {
                avatarGunFireTime = 0.25f;
                HeroController.SetAvatarFireFrame( playerNum, Random.Range( 5, 8 ) );
            }
            if ( player != null && setRumbleTraverse != null )
            {
                setRumbleTraverse.GetValue( new object[] { rumbleAmountPerShot } );
            }
        }

        protected override void RunAvatarFiring()
        {
            if ( health > 0 )
            {
                if ( avatarGunFireTime > 0f )
                {
                    avatarGunFireTime -= t;
                    if ( avatarGunFireTime <= 0f )
                    {
                        if ( avatarAngryTime > 0f )
                        {
                            HeroController.SetAvatarAngry( playerNum, usePrimaryAvatar );
                        }
                        else
                        {
                            HeroController.SetAvatarCalm( playerNum, usePrimaryAvatar );
                        }
                    }
                }

                if ( fire && !drunk )
                {
                    if ( !wasFire && avatarGunFireTime <= 0f )
                    {
                        HeroController.SetAvatarAngry( playerNum, usePrimaryAvatar );
                    }

                    if ( attackStationary || attackForwards || attackUpwards || attackDownwards )
                    {
                        avatarAngryTime = 0.15f;
                    }
                    else
                    {
                        avatarAngryTime = 0f;
                        HeroController.SetAvatarCalm( playerNum, usePrimaryAvatar );
                    }
                }
                else if ( avatarAngryTime > 0f )
                {
                    avatarAngryTime -= t;
                    if ( avatarAngryTime <= 0f )
                    {
                        HeroController.SetAvatarCalm( playerNum, usePrimaryAvatar );
                    }
                }
            }
        }

        protected void ClearCurrentAttackVariables()
        {
            alreadyHit.Clear();
            hasHitWithFists = false;
            attackHasHit = false;
            hasHitWithWall = false;
        }

        private void TimeBump( float timeStop = 0.025f )
        {
            TimeController.StopTime( timeStop, 0.1f, 0f, false, false, false );
        }

        private void MakeKungfuSound()
        {
            if ( Time.time - lastSoundTime > 0.3f )
            {
                lastSoundTime = Time.time;
                Sound.GetInstance().PlaySoundEffectAt( soundHolder.attack3Sounds, 0.6f, transform.position, drunk ? 0.85f : 1f );
            }
        }

        // Sound played when hitting an enemy with your primary attack
        protected void PlayPrimaryHitSound( float volume = 0.6f )
        {
            if ( !sound )
            {
                sound = Sound.GetInstance();
            }
            sound?.PlaySoundEffectAt( soundHolder.special2Sounds, volume, transform.position );
        }

        // Sound played when hitting a wall with your primary attack
        public void PlayWallSound()
        {
            // Don't play wall sound multiple times
            if ( playedWallHit )
            {
                return;
            }
            playedWallHit = true;
            if ( !sound )
            {
                sound = Sound.GetInstance();
            }
            sound?.PlaySoundEffectAt( soundHolder.attack2Sounds, wallHitVolume, transform.position );
        }

        protected override void PlayAidDashSound()
        {
            PlaySpecialAttackSound( 0.5f );
        }

        protected bool IsAttacking()
        {
            return ( fire && gunFrame > 1 ) || Time.time - lastAttackingTime < 0.0445f || ( ( attackDownwards || attackForwards || attackUpwards ) && attackFrames > 1 && attackFrames < attackForwardsStrikeFrame + 2 );
        }

        // Prevent damage from melee attacks when attacking
        public override void Damage( int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY )
        {
            if ( ( damageType != DamageType.Drill && damageType != DamageType.Melee && damageType != DamageType.Knifed && damageType != DamageType.Explosion && damageType != DamageType.Fire ) || !IsAttacking() || ( Mathf.Sign( transform.localScale.x ) == Mathf.Sign( xI ) && damageType != DamageType.Drill ) )
            {
                base.Damage( damage, damageType, xI, yI, direction, damageSender, hitX, hitY );
            }
        }
        #endregion

        #region Melee
        // Handle grenade throwing better
        protected override void PressHighFiveMelee( bool forceHighFive = false )
        {
            if ( health <= 0 )
            {
                return;
            }
            if ( MustIgnoreHighFiveMeleePress() )
            {
                return;
            }
            SetGestureAnimation( GestureElement.Gestures.None );
            Grenade nearbyGrenade = Map.GetNearbyGrenade( 20f, X, Y + waistHeight );
            FindNearbyMook();
            TeleportDoor nearbyTeleportDoor = Map.GetNearbyTeleportDoor( X, Y );
            if ( nearbyTeleportDoor != null && CanUseSwitch() && nearbyTeleportDoor.Activate( this ) )
            {
                return;
            }
            Switch nearbySwitch = Map.GetNearbySwitch( X, Y );
            if ( GameModeController.IsDeathMatchMode || GameModeController.GameMode == GameMode.BroDown )
            {
                if ( nearbySwitch != null && CanUseSwitch() )
                {
                    nearbySwitch.Activate( this );
                }
                else
                {
                    bool flag = false;
                    for ( int i = -1; i < 4; i++ )
                    {
                        if ( i != playerNum && Map.IsUnitNearby( i, X + transform.localScale.x * 16f, Y + 8f, 28f, 14f, true, out meleeChosenUnit ) )
                        {
                            StartMelee();
                            flag = true;
                        }
                    }
                    if ( !flag && nearbySwitch != null && CanUseSwitch() )
                    {
                        nearbySwitch.Activate( this );
                    }
                }
            }
            // Don't allow throwing items during melee, attacks, or rolls
            if ( nearbyGrenade != null && !( doingMelee || attackForwards || attackDownwards || attackUpwards || attackStationary || rollingFrames > 0 ) )
            {
                ThrowBackGrenade( nearbyGrenade );
            }
            else if ( !GameModeController.IsDeathMatchMode || !doingMelee )
            {
                if ( Map.IsCitizenNearby( X, Y, 32, 32 ) )
                {
                    if ( !doingMelee )
                    {
                        StartHighFive();
                    }
                }
                else if ( forceHighFive && !doingMelee )
                {
                    StartHighFive();
                }
                else if ( nearbySwitch != null && CanUseSwitch() )
                {
                    nearbySwitch.Activate( this );
                }
                else if ( meleeChosenUnit == null && Map.IsUnitNearby( -1, X + transform.localScale.x * 16f, Y + 8f, 28f, 14f, false, out meleeChosenUnit ) )
                {
                    StartMelee();
                }
                else if ( CheckBustCage() )
                {
                    StartMelee();
                }
                else if ( HeroController.IsAnotherPlayerNearby( playerNum, X, Y, 32f, 32f ) )
                {
                    if ( !doingMelee )
                    {
                        StartHighFive();
                    }
                }
                else
                {
                    StartMelee();
                }
            }
        }

        // Don't reset base.counter on every melee press
        protected override void StartMelee()
        {
            currentMeleeType = meleeType;
            if ( ( Physics.Raycast( new Vector3( X, Y + 5f, 0f ), Vector3.down, out _, 16f, platformLayer ) || Physics.Raycast( new Vector3( X + 4f, Y + 5f, 0f ), Vector3.down, out raycastHit, 16f, platformLayer ) || Physics.Raycast( new Vector3( X - 4f, Y + 5f, 0f ), Vector3.down, out raycastHit, 16f, platformLayer ) ) && raycastHit.collider.GetComponentInParent<Animal>() != null )
            {
                currentMeleeType = MeleeType.Knife;
            }
            switch ( currentMeleeType )
            {
                case MeleeType.Knife:
                    counter = 0f;
                    StartKnifeMelee();
                    break;
                case MeleeType.Punch:
                case MeleeType.JetpackPunch:
                    StartPunch();
                    break;
                case MeleeType.Disembowel:
                case MeleeType.FlipKick:
                case MeleeType.Tazer:
                case MeleeType.Custom:
                case MeleeType.ChuckKick:
                case MeleeType.VanDammeKick:
                case MeleeType.ChainSaw:
                case MeleeType.ThrowingKnife:
                case MeleeType.Smash:
                case MeleeType.BrobocopPunch:
                case MeleeType.PistolWhip:
                case MeleeType.HeadButt:
                case MeleeType.TeleportStab:
                    StartCustomMelee();
                    break;
            }
        }

        protected override void StartCustomMelee()
        {
            // Don't allow melees to start while doing a melee
            if ( doingMelee )
            {
                return;
            }

            // If we're already holding an item, throw that item instead
            if ( holdingItem )
            {
                StartThrowingItem();
                return;
            }

            // If we're throwing back a grenade, and the grenade has already left our hands, turn off the throwingHeldObject flag
            if ( throwingHeldObject && heldGrenade == null && heldMook == null )
            {
                throwingHeldObject = false;
            }

            StopAttack( false );
            frame = 0;
            counter = -0.05f;
            ResetMeleeValues();
            lerpToMeleeTargetPos = 0f;
            doingMelee = true;
            showHighFiveAfterMeleeTimer = 0f;
            DeactivateGun();
            SetMeleeType();
            meleeStartPos = transform.position;
            progressedFarEnough = false;
            canWallClimb = false;

            // Switch to melee sprite
            GetComponent<Renderer>().material = meleeSpriteGrabThrowing;

            // Choose an item to throw
            chosenItem = ChooseItem();

            AnimateMelee();
        }

        protected MeleeItem ChooseItem()
        {
            List<MeleeItem> triggerPool = CustomTriggerStateManager.Get<List<MeleeItem>>( "DrunkenBroster_MeleePool" );
            if ( triggerPool != null && triggerPool.Count > 0 )
            {
                return triggerPool[Random.Range( 0, triggerPool.Count )];
            }

            if ( CompletelyRandomMeleeItems )
            {
                if ( EnabledMeleeItems.Count > 0 )
                {
                    int randomIndex = Random.Range( 0, EnabledMeleeItems.Count );
                    return EnabledMeleeItems[randomIndex];
                }

                return MeleeItem.Crate;
            }
            LevelTheme theme = Map.MapData.theme;
            bool hasAliens = Map.hasAliens;
            int rareItemBoost = 0;

            // Map similar themes to base themes
            if ( theme == LevelTheme.BurningJungle || theme == LevelTheme.Forest || theme == LevelTheme.Desert )
            {
                theme = LevelTheme.Jungle;
                rareItemBoost += 5;
            }
            else if ( theme == LevelTheme.America )
            {
                theme = LevelTheme.City;
                rareItemBoost += 5;
            }

            if ( lastThrownItem == MeleeItem.Crate || lastThrownItem == MeleeItem.Bottle )
            {
                rareItemBoost += 5;
            }

            var itemPool = new List<KeyValuePair<MeleeItem, int>>
            {
                // General pool (all themes)
                new KeyValuePair<MeleeItem, int>( MeleeItem.Crate, 110 ),
                new KeyValuePair<MeleeItem, int>( MeleeItem.Bottle, 90 )
            };

            switch ( theme )
            {
                case LevelTheme.Jungle:
                    if ( !hasAliens )
                    {
                        itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.Coconut, 25 + rareItemBoost ) );
                        itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.Beehive, 20 + rareItemBoost ) );
                    }
                    itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.ExplosiveBarrel, 12 + rareItemBoost ) );
                    break;

                case LevelTheme.City:
                    itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.SoccerBall, 25 + rareItemBoost ) );
                    itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.Tire, 13 + rareItemBoost ) );
                    itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.ExplosiveBarrel, 11 + rareItemBoost ) );
                    break;

                case LevelTheme.Hell:
                    itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.ExplosiveBarrel, 15 + rareItemBoost ) );
                    itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.Skull, 10 + rareItemBoost ) );
                    break;
            }

            if ( hasAliens )
            {
                itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.AlienEgg, 10 + rareItemBoost ) );
                itemPool.Add( new KeyValuePair<MeleeItem, int>( MeleeItem.AcidEgg, 20 + rareItemBoost ) );
            }

            // Reduce weight of last thrown item
            for ( int i = 0; i < itemPool.Count; i++ )
            {
                if ( itemPool[i].Key == lastThrownItem )
                {
                    itemPool[i] = new KeyValuePair<MeleeItem, int>( itemPool[i].Key, itemPool[i].Value / 8 );
                    break;
                }
            }

            // Remove all items that aren't in the enabled list
            itemPool.RemoveAll( item => !EnabledMeleeItems.Contains( item.Key ) );

            // Default to crate if no items are enabled
            if ( itemPool.Count == 0 )
            {
                return MeleeItem.Crate;
            }

            // Weighted random selection - higher weight = more common
            int totalWeight = 0;
            foreach ( var item in itemPool )
            {
                totalWeight += item.Value;
            }

            int randomValue = Random.Range( 0, totalWeight );
            int currentWeight = 0;

            foreach ( var item in itemPool )
            {
                currentWeight += item.Value;
                if ( randomValue < currentWeight )
                {
                    return item.Key;
                }
            }

            return MeleeItem.Crate;
        }

        protected override void AnimateCustomMelee()
        {
            SetSpriteOffset( 0f, 0f );
            rollingFrames = 0;

            if ( !throwingHeldItem )
            {
                AnimatePullingOutItem();
            }
            else
            {
                AnimateThrowingHeldItem();
            }
        }

        protected void AnimatePullingOutItem()
        {
            if ( frame == 2 && nearbyMook != null && nearbyMook.CanBeThrown() && highFive )
            {
                CancelMelee();
                ThrowBackMook( nearbyMook );
                nearbyMook = null;
                return;
            }

            frameRate = frame < 4 ? 0.075f : 0.06f;

            int row = ( (int)chosenItem ) + 1;

            sprite.SetLowerLeftPixel( (float)( frame * spritePixelWidth ), (float)( row * spritePixelHeight ) );

            if ( frame == 3 )
            {
                PerformMeleeAttack( true, true );
            }
            else if ( frame == 4 && !meleeHasHit )
            {
                PerformMeleeAttack( true, false );
            }

            if ( frame >= 3 )
            {
                // Indicate melee has progressed far enough to receive an item if canceled
                progressedFarEnough = true;
            }

            if ( frame >= 11 )
            {
                frame = 0;
                CancelMelee();
            }
        }

        protected void AnimateThrowingHeldItem()
        {
            frameRate = 0.09f;

            int row = ( (int)heldItem ) + 1;

            int throwStart = 11;

            sprite.SetLowerLeftPixel( (float)( ( frame + throwStart ) * spritePixelWidth ), (float)( row * spritePixelHeight ) );

            if ( frame == 4 && !thrownItem )
            {
                ThrowHeldItem();
            }

            if ( frame >= 8 )
            {
                frame = 0;
                CancelMelee();
            }
        }

        protected void RunBarrelEffects()
        {
            if ( !doingMelee && explosionCounter > 0f )
            {
                explosionCounter -= t;
                if ( explosionCounter < 3f )
                {
                    flameCounter += t;
                    if ( flameCounter > 0f && explosionCounter > 0.2f )
                    {
                        if ( explosionCounter < 1f )
                        {
                            flameCounter -= 0.09f;
                        }
                        else if ( explosionCounter < 2f )
                        {
                            flameCounter -= 0.12f;
                        }
                        else
                        {
                            flameCounter -= 0.2f;
                        }
                        Vector3 vector = Random.insideUnitCircle;
                        switch ( Random.Range( 0, 3 ) )
                        {
                            case 0:
                                EffectsController.CreateEffect( fire1, X + vector.x * 3f, Y + vector.y * 3f + 8f, Random.value * 0.0434f, Vector3.zero );
                                break;
                            case 1:
                                EffectsController.CreateEffect( fire2, X + vector.x * 3f, Y + vector.y * 3f + 8f, Random.value * 0.0434f, Vector3.zero );
                                break;
                            case 2:
                                EffectsController.CreateEffect( fire3, X + vector.x * 3f, Y + vector.y * 3f + 8f, Random.value * 0.0434f, Vector3.zero );
                                break;
                        }
                    }
                    RunBarrelWarning( t, explosionCounter );
                    if ( explosionCounter <= 0.1f )
                    {
                        ExplodeBarrelInHands();
                    }
                }
            }
        }

        protected void RunBarrelWarning( float t, float explosionTime )
        {
            if ( explosionTime < 2f )
            {
                warningCounter += t;
                if ( warningOn && warningCounter > 0.0667f )
                {
                    warningOn = false;
                    warningCounter -= 0.0667f;
                }
                else if ( warningCounter > 0.0667f && explosionTime < 0.75f )
                {
                    warningOn = true;
                    warningCounter -= 0.0667f;
                }
                else if ( warningCounter > 0.175f && explosionTime < 1.25f )
                {
                    warningOn = true;
                    warningCounter -= 0.175f;
                }
                else if ( warningCounter > 0.2f )
                {
                    warningOn = true;
                    warningCounter -= 0.2f;
                }
                SetGunSprite( 0, 0 );
            }
        }

        protected void ExplodeBarrelInHands()
        {
            float range = 50f;
            EffectsController.CreateExplosionRangePop( X, Y, -1f, range * 2f );
            EffectsController.CreateSparkShower( X, Y, 70, 3f, 200f, 0f, 250f, 0.6f, 0.5f );
            EffectsController.CreatePlumes( X, Y, 3, 8f, 315f, 0f, 0f );
            Vector3 vector = new Vector3( Random.value, Random.value );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( X + vector.x * range * 0.25f, Y + vector.y * range * 0.25f ), Random.value * 0.5f );
            vector = new Vector3( Random.value, Random.value );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( X + vector.x * range * 0.25f, Y + vector.y * range * 0.25f ) );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Large, new Vector3( X, Y ) );
            SortOfFollow.Shake( 1f );
            sound.PlaySoundEffectAt( barrelExplodeSounds, 0.7f, transform.position );
            Map.DisturbWildLife( X, Y, 120f, 5 );
            MapController.DamageGround( this, 12, DamageType.Fire, range, X, Y );
            Map.ShakeTrees( X, Y, 256f, 64f, 128f );
            MapController.BurnUnitsAround_NotNetworked( this, playerNum, 1, range * 2f, X, Y, true, true );
            Map.ExplodeUnits( this, 12, DamageType.Fire, range * 1.1f, range, X, Y - 6f, 200f, 300f, playerNum, false, true );
            Map.ExplodeUnits( this, 1, DamageType.Fire, range * 1f, range, X, Y - 6f, 200f, 300f, playerNum, false, true );
            ExtraKnock( -1 * transform.localScale.x * 150, 400 );

            // Remove held item
            ClearHeldItem();
        }

        protected void ExtraKnock( float xI, float yI )
        {
            impaledBy = null;
            impaledByTransform = null;
            if ( frozenTime > 0f )
            {
                return;
            }
            this.xI = Mathf.Clamp( this.xI + xI / 2f, -1200f, 1200f );
            xIBlast = Mathf.Clamp( xIBlast + xI / 2f, -1200f, 1200f );
            this.yI += +yI;
            if ( IsParachuteActive && yI > 0f )
            {
                IsParachuteActive = false;
                Tumble();
            }
        }

        protected void PerformMeleeAttack( bool shouldTryHitTerrain, bool playMissSound )
        {
            Map.DamageDoodads( 3, DamageType.Knifed, X + (float)( Direction * 4 ), Y + 7f, 0f, 0f, 6f, playerNum, out _ );
            KickDoors( 24f );
            meleeChosenUnit = null;

            int meleeDamage = 9;
            DamageType meleeDamageType = DamageType.Melee;
            float xI = 200f;
            float yI = 350f;

            // Perform attack based on item type
            switch ( chosenItem )
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

            Unit hitUnit = Map.HitClosestUnit( this, playerNum, meleeDamage + ( drunk ? 4 : 0 ), meleeDamageType, 8f, 24f, X + transform.localScale.x * 6f, Y + 7f, transform.localScale.x * xI, yI, true, false, IsMine, false );
            if ( hitUnit != null )
            {
                if ( meleeDamageType == DamageType.Fire )
                {
                    hitUnit.burnTime = 1f + Random.Range( 0.2f, 0.6f );
                    hitUnit.enemyAI.Panic( (int)transform.localScale.x, false );
                }
                PlayMeleeHitSound();
                meleeHasHit = true;
                // Create acid spray
                if ( meleeDamageType == DamageType.Acid )
                {
                    EffectsController.CreateSlimeParticlesSpray( BloodColor.Green, X + width * transform.localScale.x, Y + height + 4f, 1f, 34, 6f, 5f, 300f, this.xI * 0.6f, this.yI * 0.2f + 150f, 0.6f );
                    EffectsController.CreateSlimeCover( 15, X, Y + height + 5f, 15f );
                }
            }
            else if ( playMissSound )
            {
                PlayMeleeMissSound();
            }

            if ( shouldTryHitTerrain )
            {
                bool hitTerrain = false;
                switch ( meleeDamageType )
                {
                    case DamageType.Acid:
                        hitTerrain = TryMeleeTerrainAcid( 1, meleeDamage );
                        break;
                    case DamageType.Fire:
                        break;
                    default:
                        hitTerrain = TryMeleeTerrain( 1, meleeDamage + 3 );
                        break;
                }
                if ( hitTerrain )
                {
                    meleeHasHit = true;
                }
            }
            TriggerBroMeleeEvent();
        }

        protected override bool TryMeleeTerrain( int offset = 0, int meleeDamage = 2 )
        {
            if ( !Physics.Raycast( new Vector3( X - transform.localScale.x * 4f, Y + 4f, 0f ), new Vector3( transform.localScale.x, 0f, 0f ), out raycastHit, (float)( 16 + offset ), groundLayer ) )
            {
                return false;
            }
            Cage cage = raycastHit.collider.GetComponent<Cage>();
            if ( cage == null && raycastHit.collider.transform.parent != null )
            {
                cage = raycastHit.collider.transform.parent.GetComponent<Cage>();
            }
            if ( cage != null )
            {
                MapController.Damage_Networked( this, raycastHit.collider.gameObject, cage.health, DamageType.Melee, 0f, 40f, raycastHit.point.x, raycastHit.point.y );
                return true;
            }
            MapController.Damage_Networked( this, raycastHit.collider.gameObject, meleeDamage, DamageType.Melee, 0f, 40f, raycastHit.point.x, raycastHit.point.y );
            sound.PlaySoundEffectAt( soundHolder.meleeHitTerrainSound, 0.4f, transform.position );
            EffectsController.CreateProjectilePopWhiteEffect( X + width * transform.localScale.x, Y + height + 4f );
            return true;
        }

        protected bool TryMeleeTerrainAcid( int offset = 0, int meleeDamage = 2 )
        {
            if ( !Physics.Raycast( new Vector3( X - transform.localScale.x * 4f, Y + 4f, 0f ), new Vector3( transform.localScale.x, 0f, 0f ), out raycastHit, (float)( 16 + offset ), groundLayer ) )
            {
                return false;
            }
            Cage cage = raycastHit.collider.GetComponent<Cage>();
            if ( cage == null && raycastHit.collider.transform.parent != null )
            {
                cage = raycastHit.collider.transform.parent.GetComponent<Cage>();
            }
            if ( cage != null )
            {
                MapController.Damage_Networked( this, raycastHit.collider.gameObject, cage.health, DamageType.Acid, 0f, 40f, raycastHit.point.x, raycastHit.point.y );
                return true;
            }
            MapController.Damage_Networked( this, raycastHit.collider.gameObject, meleeDamage, DamageType.Acid, 0f, 40f, raycastHit.point.x, raycastHit.point.y );
            sound.PlaySoundEffectAt( soundHolder.meleeHitTerrainSound, 0.4f, transform.position );
            EffectsController.CreateProjectilePopWhiteEffect( X + width * transform.localScale.x, Y + height + 4f );
            // Create acid spray
            EffectsController.CreateSlimeParticlesSpray( BloodColor.Green, X + width * transform.localScale.x, Y + height + 4f, 1f, 34, 6f, 5f, 300f, xI * 0.6f, yI * 0.2f + 150f, 0.6f );
            EffectsController.CreateSlimeCover( 15, X, Y + height + 5f, 15f );
            return true;
        }

        protected void PlayMeleeHitSound()
        {
            sound.PlaySoundEffectAt( soundHolder.meleeHitSound, 0.6f, transform.position );
        }

        protected void PlayMeleeMissSound()
        {
            sound.PlaySoundEffectAt( soundHolder.missSounds, 0.3f, transform.position );
        }

        protected override void CancelMelee()
        {
            GetComponent<Renderer>().material = drunk ? drunkSprite : normalSprite;

            if ( !throwingHeldItem && progressedFarEnough )
            {
                SwitchToHeldItem();
            }
            else if ( thrownItem )
            {
                ClearHeldItem();
            }
            else
            {
                throwingHeldItem = false;
            }

            canWallClimb = true;
            jumpTime = 0f;
            progressedFarEnough = false;
            throwingHeldItem = false;
            thrownItem = false;

            base.CancelMelee();
        }

        protected void SwitchToHeldItem()
        {
            holdingItem = true;
            heldItem = chosenItem;
            chosenItem = MeleeItem.None;
            gunSpriteMelee.gameObject.SetActive( originalGunSprite.gameObject.activeSelf );
            originalGunSprite.gameObject.SetActive( false );
            gunSprite = gunSpriteMeleeSprite;

            // Setup barrel warning
            if ( heldItem == MeleeItem.ExplosiveBarrel )
            {
                explosionCounter = 5f;
                warningOn = false;
            }
        }

        protected void ClearHeldItem()
        {
            canChimneyFlip = true;
            throwingHeldItem = false;
            holdingItem = false;
            heldItem = MeleeItem.None;
            thrownItem = false;
            originalGunSprite.gameObject.SetActive( gunSpriteMelee.gameObject.activeSelf );
            gunSpriteMelee.gameObject.SetActive( false );
            gunSprite = originalGunSprite;
        }

        protected void StartThrowingItem()
        {
            // Don't start a throw if we're already throwing, or doing another animation, or dead
            if ( throwingHeldItem || doingMelee || chimneyFlip || usingSpecial || acceptedDeath || health <= 0 || HasBeenCoveredInAcid() )
            {
                return;
            }

            // Release any grenades / coconuts we are holding
            if ( throwingHeldObject )
            {
                ReleaseHeldObject( false );
                throwingHeldObject = false;
            }

            throwingHeldItem = true;
            usingSpecial = fire = wasFire = false;

            frame = 0;
            counter = -0.05f;
            ResetMeleeValues();
            lerpToMeleeTargetPos = 0f;
            doingMelee = true;
            showHighFiveAfterMeleeTimer = 0f;
            DeactivateGun();
            SetMeleeType();
            meleeStartPos = transform.position;
            progressedFarEnough = false;
            canWallClimb = false;

            // Ensure melee attack type is set, otherwise wrong animation plays
            currentMeleeType = MeleeType.Disembowel;

            // Switch to melee sprite
            GetComponent<Renderer>().material = meleeSpriteGrabThrowing;

            ChangeFrame();
        }

        protected void ThrowHeldItem()
        {
            bool angleDownwards = down && IsOnGround() && ducking;
            switch ( heldItem )
            {
                case MeleeItem.Tire:
                    tireProjectile.SpawnGrenadeLocally( this, X + transform.localScale.x * 10f, Y + 8f, 0f, 0f, transform.localScale.x * ( angleDownwards ? 150f : 275f ), angleDownwards ? 25f : 50f, playerNum, 0 );
                    break;
                case MeleeItem.AcidEgg:
                    acidEggProjectile.SpawnProjectileLocally( this, X + transform.localScale.x * 7.5f, Y + 14f, transform.localScale.x * ( angleDownwards ? 200f : 350f ), angleDownwards ? 50f : 125f, playerNum );
                    break;
                case MeleeItem.Beehive:
                    beehiveProjectile.SpawnProjectileLocally( this, X + transform.localScale.x * 8f, Y + 15f, transform.localScale.x * ( angleDownwards ? 200f : 400f ), angleDownwards ? 50f : 85f, playerNum );
                    break;
                case MeleeItem.Bottle:
                    bottleProjectile.SpawnGrenadeLocally( this, X + transform.localScale.x * 7.5f, Y + 14f, 0f, 0f, transform.localScale.x * ( angleDownwards ? 225f : 450f ), angleDownwards ? 50f : 75f, playerNum );
                    break;
                case MeleeItem.Crate:
                    crateProjectile.SpawnProjectileLocally( this, X + transform.localScale.x * 10f, Y + 8f, transform.localScale.x * ( angleDownwards ? 150f : 225f ), angleDownwards ? 75f : 100f, playerNum );
                    break;
                case MeleeItem.Coconut:
                    coconutProjectile.SpawnGrenadeLocally( this, X + transform.localScale.x * 8f, Y + 15f, 0f, 0f, transform.localScale.x * ( angleDownwards ? 200f : 375f ), angleDownwards ? 40f : 60f, playerNum );
                    break;
                case MeleeItem.ExplosiveBarrel:
                    ExplosiveBarrelProjectile explosiveBarrel = explosiveBarrelProjectile.SpawnGrenadeLocally( this, X + transform.localScale.x * 10f, Y + 8f, 0f, 0f, transform.localScale.x * ( angleDownwards ? 150f : 275f ), angleDownwards ? 25f : 50f, playerNum, 0 ) as ExplosiveBarrelProjectile;
                    explosiveBarrel.explosionCounter = Mathf.Max( 4 - Mathf.Abs( 5 - explosionCounter ), 1 );
                    break;
                case MeleeItem.SoccerBall:
                    sound.PlaySoundEffectAt( soccerKickSounds, 1f, transform.position );
                    soccerBallProjectile.SpawnGrenadeLocally( this, X + transform.localScale.x * 10f, Y + 3f, 0f, 0f, transform.localScale.x * ( angleDownwards ? 200f : 350f ), angleDownwards ? 60f : 150f, playerNum, 0 );
                    break;
                case MeleeItem.AlienEgg:
                    alienEggProjectile.SpawnProjectileLocally( this, X + transform.localScale.x * 7.5f, Y + 14f, transform.localScale.x * ( angleDownwards ? 200f : 375f ), angleDownwards ? 50f : 65f, playerNum );
                    break;
                case MeleeItem.Skull:
                    skullProjectile.SpawnProjectileLocally( this, X + transform.localScale.x * 8f, Y + 15f, transform.localScale.x * ( angleDownwards ? 200f : 350f ), angleDownwards ? 40f : 100f, playerNum );
                    break;
            }

            lastThrownItem = heldItem;
            thrownItem = true;
        }

        protected override void RunCustomMeleeMovement()
        {
            if ( jumpingMelee )
            {
                ApplyFallingGravity();
                if ( yI < maxFallSpeed )
                {
                    yI = maxFallSpeed;
                }
            }
            else if ( dashingMelee )
            {
                if ( frame <= 1 )
                {
                    xI = 0f;
                    if ( actionState != ActionState.Jumping )
                    {
                        yI = 0f;
                    }
                }
                else if ( frame <= 3 )
                {
                    if ( meleeChosenUnit == null )
                    {
                        if ( !isInQuicksand )
                        {
                            xI = speed * 1f * transform.localScale.x;
                        }
                        if ( actionState != ActionState.Jumping )
                        {
                            yI = 0f;
                        }
                    }
                    else if ( !isInQuicksand )
                    {
                        xI = speed * 0.5f * transform.localScale.x + ( meleeChosenUnit.X - X ) * 6f;
                    }
                }
                else if ( frame <= 5 )
                {
                    if ( !isInQuicksand )
                    {
                        xI = speed * 0.3f * transform.localScale.x;
                    }
                    ApplyFallingGravity();
                }
                else
                {
                    ApplyFallingGravity();
                }
            }
            else
            {
                ApplyFallingGravity();
            }
        }

        public override void SetGestureAnimation( GestureElement.Gestures gesture )
        {
            // Don't allow flexing during melee
            if ( doingMelee )
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
                yI = 150f;
                if ( Physics.Raycast( new Vector3( X + ( ( !right ) ? 0f : ( radius + 3f ) ) + ( ( !left ) ? 0f : ( -radius - 3f ) ), Y + heightOpenOffset, 0f ), Vector3.down, out RaycastHit raycastHit, 23f, groundLayer ) )
                {
                    ledgeGrapple = true;
                    if ( !wasLedgeGrapple && !fire && !usingSpecial && !doingMelee && !( attackStationary || attackUpwards || attackForwards || attackDownwards ) )
                    {
                        frame = 0;
                        ChangeFrame();
                    }
                    ledgeOffsetY = raycastHit.point.y - Y;
                }
            }
        }

        // Properly cancel melee when being stopped
        public override void Stop()
        {
            CancelMelee();
            startNewAttack = false;
            StopAttack( false );
            base.Stop();
        }

        // Properly cancel melee when being covered in acid
        protected override void CoverInAcidRPC()
        {
            CancelMelee();
            base.CoverInAcidRPC();
        }
        #endregion

        #region Special
        // Animate drunk idle
        public override void AnimateActualIdleFrames()
        {
            if ( drunk && gunFrame <= 0 && !fire && !holdingItem )
            {
                SetSpriteOffset( 0f, 0f );
                DeactivateGun();
                frameRate = 0.14f;
                int num = frame % 7;
                sprite.SetLowerLeftPixel( (float)( num * spritePixelWidth ), (float)( spritePixelHeight * 9 ) );
            }
            else
            {
                base.AnimateActualIdleFrames();
            }
        }

        protected override void AnimateActualNewRunningFrames()
        {
            frameRate = ( isInQuicksand ? ( runningFrameRate * 3f ) : runningFrameRate );
            if ( IsSurroundedByBarbedWire() )
            {
                frameRate *= 2f;
            }
            if ( drunk && !dashing )
            {
                frameRate = 0.07f;
            }
            int num = frame % 8;
            if ( frame % 4 == 0 && !FluidController.IsSubmerged( this ) )
            {
                EffectsController.CreateFootPoofEffect( X, Y + 2f, 0f, Vector3.up * 1f - Vector3.right * (transform.localScale.x * 60.5f), GetFootPoofColor() );
            }
            if ( frame % 4 == 0 && !ledgeGrapple )
            {
                PlayFootStepSound();
            }
            sprite.SetLowerLeftPixel( (float)( num * spritePixelWidth ), (float)( ( !dashing || !useDashFrames ) ? ( spritePixelHeight * 2 ) : ( spritePixelHeight * 4 ) ) );
            if ( gunFrame <= 0 && !doingMelee )
            {
                SetGunSprite( num, 1 );
            }
        }

        protected override void PressSpecial()
        {
            // Don't allow special if we're doing another animation, or dead
            if ( usingSpecial || doingMelee || hasBeenCoverInAcid || throwingHeldObject || acceptedDeath || health <= 0 || HasBeenCoveredInAcid() )
            {
                return;
            }
            // Buffer special use if currently attacking
            if ( attackForwards || attackUpwards || attackDownwards || attackStationary )
            {
                bufferedSpecial = true;
                return;
            }
            // Make sure we aren't holding anything before drinking
            if ( heldItem == MeleeItem.None )
            {
                // Don't use additional specials if already drunk
                if ( drunk )
                {
                    return;
                }

                if ( SpecialAmmo > 0 )
                {
                    // Cancel rolling
                    if ( rollingFrames > 0 )
                    {
                        StopRolling();
                    }
                    wasDrunk = false;
                    canChimneyFlip = false;
                    usingSpecial = true;
                    frame = 0;
                    usingSpecialFrame = 0;
                    pressSpecialFacingDirection = (int)transform.localScale.x;
                    usedSpecial = false;
                    playedSpecialSound = false;
                    ChangeFrame();
                }
                else
                {
                    HeroController.FlashSpecialAmmo( playerNum );
                }
            }
            // Throw held item
            else
            {
                StartThrowingItem();
            }
        }

        protected override void AnimateSpecial()
        {
            SetSpriteOffset( 0f, 0f );
            DeactivateGun();
            invulnerable = true;
            if ( invulnerableTime < 0.35f )
            {
                invulnerableTime = 0.3f;
            }
            // Animate drinking to become drunk
            if ( !wasDrunk )
            {
                frameRate = 0.1f;
                sprite.SetLowerLeftPixel( (float)( usingSpecialFrame * spritePixelWidth ), (float)( spritePixelHeight * 8 ) );
                if ( IsOnGround() )
                {
                    speed = 30f;
                }
                if ( usingSpecialFrame == 4 )
                {
                    frameRate = 0.35f;
                    PlayDrinkingSound();
                }
                else if ( usingSpecialFrame == 6 )
                {
                    frameRate = 0.15f;
                    UseSpecial();
                }
                else if ( usingSpecialFrame >= 7 )
                {
                    frameRate = 0.2f;
                    StopUsingSpecial();
                }
            }
            // Animate becoming sober
            else
            {
                frameRate = 0.11f;
                sprite.SetLowerLeftPixel( (float)( usingSpecialFrame * spritePixelWidth ), (float)( spritePixelHeight * 10 ) );
                if ( IsOnGround() )
                {
                    speed = 30f;
                }

                if ( usingSpecialFrame == 1 )
                {
                    PlayBecomeSoberSound();

                    frameRate = 0.2f;
                }
                else if ( usingSpecialFrame == 6 )
                {
                    UseSpecial();
                }
                else if ( usingSpecialFrame >= 10 )
                {
                    StopUsingSpecial();
                }
            }
        }

        protected void PlayDrinkingSound()
        {
            if ( playedSpecialSound )
            {
                return;
            }
            playedSpecialSound = true;
            sound.PlaySoundEffectAt( slurp, 1f, transform.position, 0.9f, true, false, true );
        }

        protected void PlayBecomeSoberSound()
        {
            if ( playedSpecialSound )
            {
                return;
            }
            playedSpecialSound = true;
            sound.PlaySoundEffectAt( soundHolderVoice.effortGrunt, 0.45f, transform.position, 0.9f, true, false, true );
        }

        protected override void UseSpecial()
        {
            if ( usedSpecial )
            {
                return;
            }
            usedSpecial = true;
            if ( !wasDrunk )
            {
                BecomeDrunk();
                --SpecialAmmo;
            }
            else
            {
                BecomeSober();
            }
        }

        protected void StopUsingSpecial()
        {
            StopRolling();
            frame = 0;
            usingSpecialFrame = 0;
            usingSpecial = false;
            ActivateGun();
            ChangeFrame();
            speed = originalSpeed;
            canChimneyFlip = true;
            usedSpecial = false;
            playedSpecialSound = false;
        }

        protected void BecomeDrunk()
        {
            GetComponent<Renderer>().material = drunkSprite;
            drunkCounter = maxDrunkTime;
            drunk = true;
            originalSpeed = 110;
            enemyFistDamage = drunkEnemyFistDamage;
            groundFistDamage = drunkGroundFistDamage;
            attackDownwardsStrikeFrame = 2;

            // Start holding bottle
            chosenItem = MeleeItem.Bottle;
            SwitchToHeldItem();

            DrunkenCameraManager.RegisterDrunk( this );
        }

        protected bool TryToBecomeSober()
        {
            if ( !hasBeenCoverInAcid && !doingMelee && !usingSpecial )
            {
                canChimneyFlip = false;
                wasDrunk = true;
                usingSpecial = true;
                frame = 0;
                pressSpecialFacingDirection = (int)transform.localScale.x;
                usedSpecial = false;
                playedSpecialSound = false;
                return true;
            }
            return false;
        }

        protected void BecomeSober()
        {
            GetComponent<Renderer>().material = normalSprite;
            drunkCounter = 0;
            drunk = false;
            originalSpeed = 130;
            enemyFistDamage = soberEnemyFistDamage;
            groundFistDamage = soberGroundFistDamage;
            attackDownwardsStrikeFrame = 3;

            if ( rollingFrames > 0 )
            {
                StopRolling();
            }

            DrunkenCameraManager.UnregisterDrunk( this );
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

        public static void ForceAllDrunk()
        {
            for ( int i = 0; i < 4; i++ )
            {
                if ( HeroController.PlayerIsAlive( i ) )
                {
                    try
                    {
                        if ( HeroController.players[i].character is DrunkenBroster db )
                        {
                            if ( !db.drunk )
                            {
                                db.PressSpecial();
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public static void ForceAllSoberUp()
        {
            for ( int i = 0; i < 4; i++ )
            {
                if ( HeroController.PlayerIsAlive( i ) )
                {
                    try
                    {
                        if ( HeroController.players[i].character is DrunkenBroster db && db.drunk )
                        {
                            db.drunkCounter = 0.1f;
                        }
                    }
                    catch { }
                }
            }
        }
        #endregion

        #region Movement
        protected override void StopAirDashing()
        {
            base.StopAirDashing();
            hasAttackedDownwards = false;
            hasAttackedUpwards = false;
            hasAttackedForwards = false;
        }

        protected override void RunMovement()
        {
            if ( health <= 0 )
            {
                xIAttackExtra = 0f;
            }
            base.RunMovement();
        }

        protected override void CheckFacingDirection()
        {
            // Don't allow changing directions when using a forwards attack or doing melee
            if ( !attackForwards && !doingMelee )
            {
                base.CheckFacingDirection();
            }
        }

        protected override void ApplyFallingGravity()
        {
            if ( postAttackHitPauseTime < 0f )
            {
                if ( chimneyFlip || isInQuicksand )
                {
                    base.ApplyFallingGravity();
                }
                else if ( !attackForwards )
                {
                    if ( attackDownwards && attackFrames < attackDownwardsStrikeFrame )
                    {
                        yI -= 1100f * t * 0.3f;
                    }
                    else if ( attackUpwards && attackFrames >= attackUpwardsStrikeFrame )
                    {
                        yI -= 1100f * t * 0.5f;
                    }
                    else
                    {
                        base.ApplyFallingGravity();
                    }
                }
                // Attack forwards gravity
                else
                {
                    if ( !drunk )
                    {
                        if ( attackFrames > attackForwardsStrikeFrame )
                        {
                            base.ApplyFallingGravity();
                        }
                    }
                    else
                    {
                        if ( attackFrames > 5 )
                        {
                            base.ApplyFallingGravity();
                        }
                    }
                }
            }
        }

        protected bool CanAddXSpeed()
        {
            return !attackDownwards && !attackUpwards && postAttackHitPauseTime < 0f;
        }

        protected override void AddSpeedLeft()
        {
            // Don't add speed left if attacking forward facing right
            if ( !( attackForwards && attackDirection == 1 ) && CanAddXSpeed() )
            {
                base.AddSpeedLeft();
                if ( attackForwards && attackFrames > 4 && xI < -speed * 0.5f )
                {
                    xI = -speed * 0.5f;
                }
            }
        }

        protected override void AddSpeedRight()
        {
            // Don't add speed right if attacking forward facing left
            if ( !( attackForwards && attackDirection == -1 ) && CanAddXSpeed() )
            {
                base.AddSpeedRight();
                if ( attackForwards && attackFrames > 4 && xI > speed * 0.5f )
                {
                    xI = speed * 0.5f;
                }
            }
        }

        // Don't grab ladder when doing upwards / downwards attacks
        protected override bool IsOverLadder( ref float ladderXPos )
        {
            return !( attackUpwards || attackDownwards ) && base.IsOverLadder( ref ladderXPos );
        }

        // Don't grab ladder when doing drunk downwards attack
        protected override bool IsOverLadder( float xOffset, ref float ladderXPos )
        {
            return !( attackUpwards || attackDownwards ) && base.IsOverLadder( xOffset, ref ladderXPos );
        }

        // Don't reset frames if doing melee
        public override void AttachToZipline( ZipLine zipLine )
        {
            if ( doingMelee )
            {
                attachedToZipline = zipLine;
            }
            else
            {
                base.AttachToZipline( zipLine );
            }
        }
        #endregion

        #region Roll
        protected void RunRolling()
        {
            // Check if custom keybind is enabled and was pressed
            if ( pressKeybindToRoll && rollKey[playerNum].PressedDown() && CanStartSlideRoll() )
            {
                if ( actionState != ActionState.Jumping )
                {
                    slideExtraSpeed = originalSpeed * 0.75f;
                    RollOnLand( true );
                    dashSlideCooldown = drunk ? 0.5f : 0.6f;
                }
                else
                {
                    bufferedSlideRoll = true;
                    bufferedSlideRollTime = 0.5f;
                }
            }

            if ( rollingFrames > 0 )
            {
                if ( isSlideRoll )
                {
                    if ( rollingFrames <= 12 )
                    {
                        if ( rollingFrames >= 7 )
                        {
                            float boostSpeed = 20f + ( drunk ? 10f : 0f );
                            slideExtraSpeed = Mathf.Lerp( slideExtraSpeed, boostSpeed, t * 5f );
                        }
                        else
                        {

                            float boostSpeed = 30f + ( drunk ? 10f : 0f );
                            slideExtraSpeed = Mathf.Lerp( slideExtraSpeed, boostSpeed, t * 5f );
                        }
                    }
                    else
                    {
                        float decelerationSpeed = drunk ? -80f * 0.8f : -80f; // Drunk mode: +20% slide distance
                        slideExtraSpeed = Mathf.Lerp( slideExtraSpeed, decelerationSpeed, t * 3f );
                    }

                    // Handle invulnerability during slide roll
                    if ( drunk && rollingFrames <= 26 && rollingFrames >= 13 && invulnerableTime < 0.2f )
                    {
                        invulnerable = true;
                        invulnerableTime = 0.1f; // Keep refreshing invulnerability
                    }
                    else if ( !drunk && rollingFrames <= 24 && rollingFrames >= 15 && invulnerableTime < 0.2f )
                    {
                        invulnerable = true;
                        invulnerableTime = 0.1f; // Keep refreshing invulnerability
                    }

                    // Deflect projectiles only during attack frames
                    if ( rollingFrames <= 10 && rollingFrames >= 1 )
                    {
                        if ( rollingFrames > 2 )
                        {
                            faderTrailDelay -= t / Time.timeScale;
                            if ( faderTrailDelay < 0f )
                            {
                                CreateFaderTrailInstance();
                                faderTrailDelay = 0.034f;
                            }
                        }

                        Map.DeflectProjectiles( this, playerNum, 8f,
                            X + transform.localScale.x * 2f, Y + 6f,
                            transform.localScale.x * 200f, true );
                    }
                }
            }
            else
            {
                slideExtraSpeed = 0f;
            }

            if ( !doingMelee && !usingSpecial )
            {
                speed = originalSpeed + slideExtraSpeed;
            }


            dashSlideCooldown -= t;

            if ( bufferedSlideRoll )
            {
                bufferedSlideRollTime -= t;
                if ( bufferedSlideRollTime <= 0f )
                {
                    bufferedSlideRoll = false;
                }
            }

            if ( actionState == ActionState.Jumping )
            {
                StopRolling();
            }
        }

        protected override void Jump( bool wallJump )
        {
            // Don't allow wall jumping while doing melee
            if ( doingMelee && wallJump )
            {
                return;
            }
            // Allow jumping after strike frame on upwards, forwards, and stationary attacks
            if ( ( !attackUpwards || attackFrames > attackUpwardsStrikeFrame ) && ( !attackForwards || attackFrames > attackForwardsStrikeFrame ) && ( !attackStationary || attackFrames > attackStationaryStrikeFrame || hasHitThisAttack ) )
            {
                // Don't allow jumping during drunk downwards attack
                if ( !( drunk && attackDownwards ) )
                {
                    base.Jump( wallJump );

                    // Switch to jumping melee
                    if ( doingMelee )
                    {
                        jumpingMelee = true;
                        dashingMelee = false;
                    }
                }
            }
            // Cancel slide
            StopRolling();
        }

        protected override void StartDashing()
        {
            bool isNewDashPress = dashButton && !wasdashButton;
            bool isDoubleTap = isNewDashPress && lastDashTime > 0 && Time.time - lastDashTime <= doubleTapWindow;

            base.StartDashing();

            if ( doubleTapDashToRoll && isDoubleTap && dashSlideCooldown <= 0f && rollingFrames <= 0 )
            {
                if ( actionState != ActionState.Jumping )
                {
                    slideExtraSpeed = originalSpeed * 0.75f;
                    RollOnLand( true );
                    dashSlideCooldown = drunk ? 0.5f : 0.6f;
                }
                else
                {
                    bufferedSlideRoll = true;
                    bufferedSlideRollTime = 0.5f;
                }
            }

            if ( isNewDashPress )
            {
                lastDashTime = Time.time;
            }
        }

        protected override void Land()
        {
            if ( attackDownwards )
            {
                if ( !drunk )
                {
                    DownwardHitGround();
                    base.Land();
                }
                else if ( !hasHitThisAttack )
                {
                    DownwardHitGroundDrunk();
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
                isSlideRoll = false;

                base.Land();

                // Reset dash time when landing to prevent false double-tap detection across jumps
                lastDashTime = -1f;

                if ( bufferedSlideRoll && dashSlideCooldown <= 0f && rollingFrames <= 0 )
                {
                    bufferedSlideRoll = false;
                    slideExtraSpeed = originalSpeed * 0.75f;
                    RollOnLand( true );
                    dashSlideCooldown = drunk ? 0.5f : 0.6f;
                }

                // Switch to dashing melee
                if ( doingMelee )
                {
                    dashingMelee = true;
                    jumpingMelee = false;
                }

                if ( rollingFrames > 0 && isSlideRoll && rollingFrames >= 26 )
                {
                    slideExtraSpeed = Mathf.Abs( yI ) * 0.3f;

                    // Shorten animation to make it look more natural
                    rollingFrames -= 1;
                    ChangeFrame();
                }

            }
        }

        protected bool CanStartSlideRoll()
        {
            return !doingMelee && !usingSpecial && !IsFlexing() && rollingFrames <= 0 && dashSlideCooldown <= 0f && ( right || left );
        }

        protected override bool CanDoRollOnLand()
        {
            if ( bufferedSlideRoll || dashSlideCooldown > 0.55f )
            {
                return false;
            }

            return doRollOnLand && yI < -350f && skinnedMookOnMyBack == null;
        }

        protected override void RollOnLand()
        {
            RollOnLand( false );
        }

        protected void RollOnLand( bool forceSlideRoll )
        {
            // Force slide roll when requested (double-tap dash) or when drunk
            if ( forceSlideRoll || drunk )
            {
                rollingFrames = 27;
                isSlideRoll = true;
            }
            else
            {
                isSlideRoll = false;
                base.RollOnLand();
            }

            // Disable invulnerability flash during roll
            InvulnerabilityFlash flash = GetComponent<InvulnerabilityFlash>();
            if ( flash != null )
            {
                flash.enabled = false;
            }
        }

        // Don't stop rolling when slide rolling into wall
        protected override void AssignPushingTime()
        {
            if ( !( isSlideRoll && rollingFrames > 0 ) )
            {
                base.AssignPushingTime();
            }
        }

        protected override void StopRolling()
        {
            if ( rollSound != null && rollSound.isPlaying )
            {
                rollSound.Stop();
                rollSound = null;
            }
            isSlideRoll = false;
            InvulnerabilityFlash flash = GetComponent<InvulnerabilityFlash>();
            if ( flash != null )
            {
                flash.enabled = true;
            }
            base.StopRolling();
        }

        protected override void AnimateRolling()
        {
            if ( !isSlideRoll )
            {
                base.AnimateRolling();
                return;
            }

            // Frame 27 - 23 = falling onto ground
            // Frame 22 - 18 = lying on ground
            // Frame 17 - 11 = getting up
            // Frame 10 - 6  = first attack
            // Frame 5 - 1   = second attack
            frameRate = 0.075f;
            int lastFrame = 27;
            // Laying on ground
            if ( rollingFrames <= ( lastFrame - 5 ) && rollingFrames >= ( lastFrame - 10 ) )
            {
                frameRate = 0.065f;
            }
            // Get up attack
            else if ( rollingFrames < 12 )
            {
                frameRate = 0.05f;
            }

            if ( rollingFrames > 10 && rollingFrames < ( lastFrame - 2 ) && Mathf.Abs( xI ) > 30f )
            {
                EffectsController.CreateFootPoofEffect( X - transform.localScale.x * 6f, Y + 1.5f, 0f, new Vector3( transform.localScale.x * -20f, 0f ) );
            }
            if ( rollingFrames == ( lastFrame - 4 ) )
            {
                rollSound = sound.PlaySoundEffectAt( soundHolder.attack4Sounds, 0.25f, transform.position );
            }

            if ( rollingFrames <= 10 && rollingFrames >= 6 )
            {
                if ( rollingFrames == 10 )
                {
                    PlayAttackSound();
                    MakeKungfuSound();

                    int damage = drunk ? 12 : 8;
                    float knockbackX = transform.localScale.x * 250f;
                    float knockbackY = 350f;

                    hasHitThisAttack = false;
                    hasMadeEffects = false;
                    DamageDoodads( 3, DamageType.Knifed, X + transform.localScale.x * 6f, Y + 6f, 0f, 0f, 8f, playerNum, out _, null );
                    hasMadeEffects = false;

                    if ( Map.HitUnits( this, this, playerNum, damage, DamageType.Crush, 12f, 12f, X + transform.localScale.x * 6f, Y + 6f, knockbackX, knockbackY, true, false, false, false ) )
                    {
                        PlayPrimaryHitSound( 0.5f );
                        SortOfFollow.Shake( drunk ? 0.5f : 0.3f );
                    }
                    else if ( hitSpecialDoodad )
                    {
                        playedWallHit = false;
                        PlayWallSound();
                    }

                    KickDoors( 24f );
                    FireWeaponGround( X + transform.localScale.x * 4f, Y + 6f, new Vector3( transform.localScale.x, 0f, 0f ), 10f );
                }
            }
            else if ( rollingFrames <= 5 && rollingFrames >= 1 )
            {
                if ( rollingFrames == 5 )
                {
                    PlayAttackSound();

                    int damage = drunk ? 12 : 8;
                    float knockbackX = transform.localScale.x * 300f;
                    float knockbackY = 400f;

                    hasHitThisAttack = false;
                    hasMadeEffects = false;
                    DamageDoodads( 3, DamageType.Knifed, X + transform.localScale.x * 6f, Y + 6f, 0f, 0f, 8f, playerNum, out _, null );
                    hasMadeEffects = false;

                    if ( Map.HitUnits( this, this, playerNum, damage, DamageType.Crush, 12f, 12f, X + transform.localScale.x * 6f, Y + 6f, knockbackX, knockbackY, true, false, drunk, false ) )
                    {
                        PlayPrimaryHitSound( 0.5f );
                        SortOfFollow.Shake( drunk ? 0.5f : 0.3f );

                        if ( drunk )
                        {
                            Map.ExplodeUnits( this, 3, DamageType.Knock, 10f, 10f, X + transform.localScale.x * 6f, Y + 6f, knockbackX * 1.5f, knockbackY * 1.2f, playerNum, false, false, false );
                        }
                    }
                    else if ( hitSpecialDoodad )
                    {
                        playedWallHit = false;
                        PlayWallSound();
                    }

                    KickDoors( 24f );
                    FireWeaponGround( X + transform.localScale.x * 4f, Y + 6f, new Vector3( transform.localScale.x, 0f, 0f ), 10f );
                }
            }

            sprite.SetLowerLeftPixel( (float)( ( lastFrame - rollingFrames ) * spritePixelWidth ), (float)( spritePixelHeight * 16 ) );
            DeactivateGun();
        }
        #endregion
    }
}
