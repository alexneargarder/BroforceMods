using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using HarmonyLib;
using RocketLib;
using RocketLib.CustomTriggers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Furibrosa
{
    [HeroPreset( "Furibrosa", HeroType.Rambro )]
    public class Furibrosa : CustomHero
    {
        // General
        public static KeyBindingForPlayers switchWeaponKey = AllModKeyBindings.LoadKeyBinding( "Furibrosa", "Switch Weapon" );
        protected bool acceptedDeath = false;
        bool wasInvulnerable = false;
        public static bool jsonLoaded = false;
        public Material originalSpecialMat;

        // Sounds
        public AudioClip[] crossbowSounds;
        public AudioClip[] flareSounds;
        public AudioClip chargeSound;
        public AudioClip swapSound;
        public AudioClip[] meleeSwingSounds;

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
        public static FuriosaFlare flarePrefab;
        [SaveableSetting] public static bool doubleTapSwitch = true;
        protected float lastDownPressTime = -1f;
        protected bool randomizedWeapon = false;
        public const float crossbowDelay = 0.65f;
        public const float flaregunDelay = 0.8f;
        public const int crossbowDamage = 13;
        public const int flaregunDamage = 11;

        // Melee
        protected MeshRenderer holdingArm;
        public static HashSet<Unit> grabbedUnits = new HashSet<Unit>();
        Unit grabbedUnit;
        protected bool unitWasGrabbed = false;
        protected bool throwingMook = false;
        protected float holdingXOffset = 0f;
        protected float holdingYOffset = 0f;
        protected float targetHoldingXOffset = 0f;
        protected float targetHoldingYOffset = 0f;
        protected float interpolationSpeed = 20f;
        protected bool isInterpolating = false;

        // Special
        public static WarRig warRigPrefab;
        public WarRig currentWarRig;
        public bool holdingSpecial = false;
        public float holdingSpecialTime = 0f;

        #region General
        protected override void Awake()
        {
            gameObject.layer = 19;
            base.Awake();
        }

        public override void PreloadAssets()
        {
            PreloadSprites( DirectoryPath, new List<string> { "gunSpriteCrossbow.png", "gunSpriteCrossbowHolding.png", "gunSpriteFlareGun.png", "gunSpriteFlareGunHolding.png", "special.png", "gunSpriteHolding.png", "vehicleSprite.png", "vehicleCrossbow.png", "vehicleFlareGun.png", "vehicleCrossbow.png", "vehicleWheels.png", "vehicleBumper.png", "vehicleLongSmokestacks.png", "vehicleShortSmokestacks.png", "vehicleFrontSmokestacks.png", "vehicleSpecial.png", "boltExplosive.png", "bolt.png", "harpoon.png" } );

            PreloadSounds( SoundPath, new List<string> { "crossbowShot1.wav", "crossbowShot2.wav", "crossbowShot3.wav", "crossbowShot4.wav", "flareShot1.wav", "flareShot2.wav", "flareShot3.wav", "flareShot4.wav", "charged.wav", "weaponSwap.wav", "meleeSwing1.wav", "meleeSwing2.wav", "meleeSwing3.wav", "vehicleIdleLoop.wav", "vehicleBoost.wav", "vehicleHornMedium.wav", "vehicleHornLong.wav", "vehicleHit1.wav", "vehicleHit2.wav", "vehicleHit3.wav", "harpoon.wav" } );
        }

        public override void HarmonyPatches( Harmony harmony )
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll( assembly );
        }

        public override void RegisterCustomTriggers()
        {
            CustomTriggerManager.RegisterCustomTrigger( typeof( FuribrosaSummonWarRigAction ), typeof( FuribrosaSummonWarRigActionInfo ), "Furibrosa - Summon War Rig", "Custom Bros" );
        }

        protected override void Start()
        {
            base.Start();

            meleeType = MeleeType.Disembowel;

            crossbowMat = ResourcesController.GetMaterial( Path.Combine( DirectoryPath, "gunSpriteCrossbow.png" ) );
            crossbowNormalMat = crossbowMat;
            crossbowHoldingMat = ResourcesController.GetMaterial( Path.Combine( DirectoryPath, "gunSpriteCrossbowHolding.png" ) );

            flareGunMat = ResourcesController.GetMaterial( Path.Combine( DirectoryPath, "gunSpriteFlareGun.png" ) );
            flareGunNormalMat = flareGunMat;
            flareGunHoldingMat = ResourcesController.GetMaterial( Path.Combine( DirectoryPath, "gunSpriteFlareGunHolding.png" ) );

            gunSprite.gameObject.layer = 19;
            gunSprite.meshRender.material = crossbowMat;

            originalSpecialMat = ResourcesController.GetMaterial( DirectoryPath, "special.png" );

            if ( boltPrefab == null )
            {
                boltPrefab = new GameObject( "Bolt", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( BoxCollider ), typeof( Bolt ) } ).GetComponent<Bolt>();
                boltPrefab.gameObject.SetActive( false );
                boltPrefab.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Predabro ) as Predabro ).projectile.soundHolder;
                boltPrefab.Setup( false );
                DontDestroyOnLoad( boltPrefab );
            }

            if ( explosiveBoltPrefab == null )
            {
                explosiveBoltPrefab = new GameObject( "ExplosiveBolt", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( BoxCollider ), typeof( Bolt ) } ).GetComponent<Bolt>();
                explosiveBoltPrefab.gameObject.SetActive( false );
                explosiveBoltPrefab.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Predabro ) as Predabro ).projectile.soundHolder;
                explosiveBoltPrefab.Setup( true );
                DontDestroyOnLoad( explosiveBoltPrefab );
            }

            if ( flarePrefab == null )
            {
                for ( int i = 0; i < InstantiationController.PrefabList.Count; ++i )
                {
                    if ( InstantiationController.PrefabList[i] != null && InstantiationController.PrefabList[i].name == "Bullet Flare" )
                    {
                        Flare originalFlare = ( Instantiate( InstantiationController.PrefabList[i], Vector3.zero, Quaternion.identity ) as GameObject ).GetComponent<Flare>();
                        flarePrefab = originalFlare.gameObject.AddComponent<FuriosaFlare>();
                        flarePrefab.Setup( originalFlare );
                        flarePrefab.gameObject.SetActive( false );
                        DontDestroyOnLoad( flarePrefab );
                        break;
                    }
                }
            }

            holdingArm = new GameObject( "FuribrosaArm", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } ).GetComponent<MeshRenderer>();
            holdingArm.transform.parent = transform;
            holdingArm.gameObject.SetActive( false );
            holdingArm.material = ResourcesController.GetMaterial( DirectoryPath, "gunSpriteHolding.png" );
            SpriteSM holdingArmSprite = holdingArm.gameObject.GetComponent<SpriteSM>();
            holdingArmSprite.RecalcTexture();
            holdingArmSprite.SetTextureDefaults();
            holdingArmSprite.lowerLeftPixel = new Vector2( 0, 32 );
            holdingArmSprite.pixelDimensions = new Vector2( 32, 32 );
            holdingArmSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            holdingArmSprite.width = 32;
            holdingArmSprite.height = 32;
            holdingArmSprite.transform.localPosition = new Vector3( 0, 0, -0.9f );
            holdingArmSprite.CalcUVs();
            holdingArmSprite.UpdateUVs();
            holdingArmSprite.offset = new Vector3( 0f, 15f, 0f );

            // Create WarRig
            try
            {
                CreateWarRigPrefab();
            }
            catch ( Exception ex )
            {
                BMLogger.Log( "Exception creating WarRig: " + ex.ToString() );
            }

            // Randomize starting weapon
            if ( !randomizedWeapon )
            {
                if ( Random.value >= 0.5f )
                {
                    nextState = PrimaryState.FlareGun;
                    SwitchWeapon();
                }

                randomizedWeapon = true;
            }
        }

        public static void CreateWarRigPrefab()
        {
            if ( warRigPrefab == null )
            {
                GameObject warRig = null;
                for ( int i = 0; i < InstantiationController.PrefabList.Count; ++i )
                {
                    if ( InstantiationController.PrefabList[i] != null && InstantiationController.PrefabList[i].name == "ZMookArmouredGuy" )
                    {
                        warRig = Instantiate( InstantiationController.PrefabList[i], Vector3.zero, Quaternion.identity ) as GameObject;
                    }
                }

                if ( warRig != null )
                {
                    warRigPrefab = warRig.AddComponent<WarRig>();
                    warRigPrefab.Setup();
                }
                else
                {
                    throw new Exception( "Mech Prefab not found" );
                }

                DontDestroyOnLoad( warRigPrefab );
            }
        }

        public override void BeforePrefabSetup()
        {
            SoundHolderVoiceType = SoundHolderVoiceTypes.Female;
        }

        public override void AfterPrefabSetup()
        {
            // Load Audio
            crossbowSounds = new AudioClip[4];
            crossbowSounds[0] = ResourcesController.GetAudioClip( SoundPath, "crossbowShot1.wav" );
            crossbowSounds[1] = ResourcesController.GetAudioClip( SoundPath, "crossbowShot2.wav" );
            crossbowSounds[2] = ResourcesController.GetAudioClip( SoundPath, "crossbowShot3.wav" );
            crossbowSounds[3] = ResourcesController.GetAudioClip( SoundPath, "crossbowShot4.wav" );

            flareSounds = new AudioClip[4];
            flareSounds[0] = ResourcesController.GetAudioClip( SoundPath, "flareShot1.wav" );
            flareSounds[1] = ResourcesController.GetAudioClip( SoundPath, "flareShot2.wav" );
            flareSounds[2] = ResourcesController.GetAudioClip( SoundPath, "flareShot3.wav" );
            flareSounds[3] = ResourcesController.GetAudioClip( SoundPath, "flareShot4.wav" );

            chargeSound = ResourcesController.GetAudioClip( SoundPath, "charged.wav" );

            swapSound = ResourcesController.GetAudioClip( SoundPath, "weaponSwap.wav" );

            meleeSwingSounds = new AudioClip[3];
            meleeSwingSounds[0] = ResourcesController.GetAudioClip( SoundPath, "meleeSwing1.wav" );
            meleeSwingSounds[1] = ResourcesController.GetAudioClip( SoundPath, "meleeSwing2.wav" );
            meleeSwingSounds[2] = ResourcesController.GetAudioClip( SoundPath, "meleeSwing3.wav" );
        }

        protected override void Update()
        {
            base.Update();
            if ( acceptedDeath )
            {
                if ( health <= 0 && !WillReviveAlready )
                {
                    return;
                }
                // Revived
                else
                {
                    acceptedDeath = false;
                }
            }

            if ( invulnerable )
            {
                wasInvulnerable = true;
            }

            // Switch Weapon Pressed
            if ( playerNum >= 0 && playerNum < 4 && switchWeaponKey.IsDown( playerNum ) )
            {
                StartSwitchingWeapon();
            }

            // Check if invulnerability ran out
            if ( wasInvulnerable && !invulnerable )
            {
                // Fix any not currently displayed textures
                wasInvulnerable = false;
                GetComponent<Renderer>().material.SetColor( "_TintColor", Color.gray );
                crossbowNormalMat.SetColor( "_TintColor", Color.gray );
                crossbowHoldingMat.SetColor( "_TintColor", Color.gray );
                flareGunNormalMat.SetColor( "_TintColor", Color.gray );
                flareGunHoldingMat.SetColor( "_TintColor", Color.gray );
                holdingArm.material.SetColor( "_TintColor", Color.gray );
            }

            if ( holdingSpecial )
            {
                if ( special )
                {
                    holdingSpecialTime += t;
                    if ( holdingSpecialTime > 0.2f )
                    {
                        GoPastFuriosa();
                    }
                }
                else
                {
                    holdingSpecial = false;
                }
            }

            // Handle death
            if ( actionState == ActionState.Dead && !acceptedDeath )
            {
                if ( !WillReviveAlready )
                {
                    acceptedDeath = true;
                }
            }

            // Release unit if getting on helicopter
            if ( isOnHelicopter )
            {
                ReleaseUnit( false );
            }

            // Smoothly interpolate holding offsets
            if ( grabbedUnit != null && isInterpolating )
            {
                holdingXOffset = Mathf.Lerp( holdingXOffset, targetHoldingXOffset, Time.deltaTime * interpolationSpeed );
                holdingYOffset = Mathf.Lerp( holdingYOffset, targetHoldingYOffset, Time.deltaTime * interpolationSpeed );

                // Check if we're close enough to stop interpolating
                if ( Mathf.Abs( holdingXOffset - targetHoldingXOffset ) < 0.01f &&
                     Mathf.Abs( holdingYOffset - targetHoldingYOffset ) < 0.01f )
                {
                    holdingXOffset = targetHoldingXOffset;
                    holdingYOffset = targetHoldingYOffset;
                    isInterpolating = false;
                }
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if ( acceptedDeath )
            {
                return;
            }

            if ( grabbedUnit != null )
            {
                if ( grabbedUnit.health > 0 && health > 0 )
                {
                    grabbedUnit.X = X + transform.localScale.x * holdingXOffset;
                    grabbedUnit.Y = Y + holdingYOffset;
                    if ( currentState == PrimaryState.Crossbow )
                    {
                        grabbedUnit.zOffset = -0.95f;
                    }
                    else if ( currentState == PrimaryState.FlareGun )
                    {
                        grabbedUnit.zOffset = -2;
                    }

                    grabbedUnit.transform.localScale = transform.localScale;
                    unitWasGrabbed = true;
                }
                else
                {
                    ReleaseUnit( false );
                }
            }
            // Unit was grabbed but no longer exists, switch to normal materials
            else if ( unitWasGrabbed )
            {
                unitWasGrabbed = false;
                grabbedUnits.RemoveWhere( unit => unit == null );
                grabbedUnit = null;
                gunSprite.gameObject.layer = 19;
                SwitchToNormalMaterials();
                ChangeFrame();
            }
        }

        public override void UIOptions()
        {
            GUILayout.Space( 10 );
            // Only display tooltip if it's currently unset (otherwise we'll display BroMaker's tooltips
            switchWeaponKey.OnGUI( out _, ( GUI.tooltip == string.Empty ) );
            GUILayout.Space( 10 );

            if ( doubleTapSwitch != ( doubleTapSwitch = GUILayout.Toggle( doubleTapSwitch, "Double Tap Down to Switch Weapons" ) ) )
            {
                // Settings changed, update json
                SaveSettings();
            }
        }

        public override void Death( float xI, float yI, DamageObject damage )
        {
            ReleaseUnit( false );
            base.Death( xI, yI, damage );
        }

        protected override void OnDestroy()
        {
            ReleaseUnit( false );
            base.OnDestroy();
        }

        public override void RecallBro()
        {
            // Release unit if despawning
            ReleaseUnit( false );

            base.RecallBro();
        }

        protected override void ChangeFrame()
        {
            if ( !randomizedWeapon && isOnHelicopter )
            {
                randomizedWeapon = true;
            }

            base.ChangeFrame();
        }
        #endregion

        #region Primary
        protected override void SetGunPosition( float xOffset, float yOffset )
        {
            base.SetGunPosition( xOffset, yOffset );
            if ( grabbedUnit != null )
            {
                if ( !isInterpolating )
                {
                    holdingXOffset = 11f + xOffset;
                    holdingYOffset = 4.5f + yOffset;
                }

                holdingArm.transform.localPosition = gunSprite.transform.localPosition + new Vector3( 0f, 0f, 0.1f );
                holdingArm.transform.localScale = gunSprite.transform.localScale;
            }
        }

        protected override void StartFiring()
        {
            chargeTime = 0f;
            chargeCounter = 0;
            charged = false;
            base.StartFiring();
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

            if ( currentState == PrimaryState.Crossbow )
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
            else if ( currentState == PrimaryState.FlareGun )
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
            if ( currentState == PrimaryState.Crossbow )
            {
                // Fire explosive bolt
                if ( charged )
                {
                    x = X + transform.localScale.x * 8f;
                    y = Y + 8f;
                    xSpeed = transform.localScale.x * 500 + ( xI / 2 );
                    ySpeed = 0;
                    gunFrame = 3;
                    SetGunSprite( gunFrame, 0 );
                    TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, transform );
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally( explosiveBoltPrefab, this, x, y, xSpeed, ySpeed, playerNum ) as Bolt;
                }
                // Fire normal bolt
                else
                {
                    x = X + transform.localScale.x * 8f;
                    y = Y + 8f;
                    xSpeed = transform.localScale.x * 400 + ( xI / 2 );
                    ySpeed = 0;
                    gunFrame = 3;
                    SetGunSprite( gunFrame, 0 );
                    TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, transform );
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally( boltPrefab, this, x, y, xSpeed, ySpeed, playerNum ) as Bolt;
                }

                PlayCrossbowSound( transform.position );
                fireDelay = crossbowDelay;
            }
            else if ( currentState == PrimaryState.FlareGun )
            {
                Projectile flare = null;
                if ( attachedToZipline == null )
                {
                    x = X + transform.localScale.x * 12f;
                    y = Y + 8f;
                    xSpeed = transform.localScale.x * 450;
                    ySpeed = Random.Range( 15, 50 );
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, transform );
                    flare = ProjectileController.SpawnProjectileLocally( flarePrefab, this, x, y, xSpeed, ySpeed, playerNum );
                }
                // Move position of shot if attached to a zipline
                else
                {
                    x = X + transform.localScale.x * 4f;
                    y = Y + 8f;
                    xSpeed = transform.localScale.x * 450;
                    ySpeed = Random.Range( 15, 50 );
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, transform );
                    flare = ProjectileController.SpawnProjectileLocally( flarePrefab, this, x, y, xSpeed, ySpeed, playerNum );
                }

                gunFrame = 3;
                PlayFlareSound( transform.position );
                fireDelay = flaregunDelay;
            }
        }

        public void PlayChargeSound( Vector3 position )
        {
            sound.PlaySoundEffectAt( chargeSound, 0.35f, position, 1f, true, false, true, 0f );
        }

        public void PlayCrossbowSound( Vector3 position )
        {
            sound.PlaySoundEffectAt( crossbowSounds, 0.35f, position, 1f, true, false, true, 0f );
        }

        public void PlayFlareSound( Vector3 position )
        {
            sound.PlaySoundEffectAt( flareSounds, 0.75f, position, 1f, true, false, true, 0f );
        }

        public void PlaySwapSound( Vector3 position )
        {
            sound.PlaySoundEffectAt( swapSound, 0.35f, position, 1f, true, false, true, 0f );
        }

        protected override void RunGun()
        {
            if ( currentState == PrimaryState.Crossbow )
            {
                if ( fire )
                {
                    if ( chargeTime > 0.2f )
                    {
                        // Starting charge
                        if ( chargeCounter == 0 )
                        {
                            gunCounter += t;
                            if ( gunCounter < 1f && gunCounter > 0.08f )
                            {
                                gunCounter -= 0.08f;
                                ++gunFrame;
                                if ( gunFrame > 5 )
                                {
                                    ++chargeCounter;
                                    gunFramerate = 0.09f;
                                }
                            }
                        }
                        // Holding pattern
                        else
                        {
                            gunCounter += t;
                            if ( gunCounter > gunFramerate )
                            {
                                gunCounter -= gunFramerate;
                                ++gunFrame;
                                if ( gunFrame > 9 )
                                {
                                    gunFrame = 6;
                                    if ( !charged )
                                    {
                                        ++chargeCounter;
                                        if ( chargeCounter > 1 )
                                        {
                                            PlayChargeSound( transform.position );
                                            charged = true;
                                            gunFramerate = 0.04f;
                                        }
                                    }
                                }
                            }
                        }

                        // Don't use SetGunSprite to avoid issues when on zipline
                        gunSprite.SetLowerLeftPixel( ( gunFrame + 14 ) * gunSpritePixelWidth, gunSpritePixelHeight );
                    }
                }
                else if ( !WallDrag && gunFrame > 0 )
                {
                    gunCounter += t;
                    if ( gunCounter > 0.045f )
                    {
                        gunCounter -= 0.045f;
                        gunFrame--;
                        SetGunSprite( gunFrame, 0 );
                    }
                }
            }
            // Animate flaregun
            else if ( currentState == PrimaryState.FlareGun )
            {
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
            else
            {
                gunCounter += t;
                if ( gunCounter > 0.07f )
                {
                    gunCounter -= 0.07f;
                    ++gunFrame;

                    if ( gunFrame == 3 )
                    {
                        PlaySwapSound( transform.position );
                    }
                }

                if ( gunFrame > 5 )
                {
                    SwitchWeapon();
                }
                else
                {
                    SetGunSprite( 25 + gunFrame, 0 );
                }
            }
        }

        public override void StartPilotingUnit( Unit pilottedUnit )
        {
            // Finish switching weapon
            if ( currentState == PrimaryState.Switching )
            {
                SwitchWeapon();
            }

            // Make sure to release any held units
            ReleaseUnit( false );

            // Ensure we don't double fire when exiting units
            fire = wasFire = false;
            base.StartPilotingUnit( pilottedUnit );
        }

        protected void StartSwitchingWeapon()
        {
            if ( !usingSpecial && currentState != PrimaryState.Switching && attachedToZipline == null )
            {
                CancelMelee();
                SetGestureAnimation( GestureElement.Gestures.None );
                if ( currentState == PrimaryState.Crossbow )
                {
                    nextState = PrimaryState.FlareGun;
                }
                else
                {
                    nextState = PrimaryState.Crossbow;
                }

                currentState = PrimaryState.Switching;
                gunFrame = 0;
                gunCounter = 0f;
                fireDelay = 0f;
                RunGun();
            }
        }

        public void SwitchWeapon()
        {
            gunFrame = 0;
            gunCounter = 0f;
            currentState = nextState;
            if ( currentState == PrimaryState.FlareGun )
            {
                gunSprite.meshRender.material = flareGunMat;
            }
            else
            {
                gunSprite.meshRender.material = crossbowMat;
            }

            if ( grabbedUnit != null )
            {
                holdingArm.gameObject.SetActive( true );
            }

            SetGunSprite( 0, 0 );
        }

        protected override void CheckInput()
        {
            base.CheckInput();
            if ( !acceptedDeath && actionState != ActionState.Dead && doubleTapSwitch && down && !wasDown && actionState != ActionState.ClimbingLadder )
            {
                if ( Time.realtimeSinceStartup - lastDownPressTime < 0.2f )
                {
                    StartSwitchingWeapon();
                }

                lastDownPressTime = Time.realtimeSinceStartup;
            }
        }

        public override void AttachToZipline( ZipLine zipLine )
        {
            // Finish weapon switch animation if attaching to zipline
            if ( currentState == PrimaryState.Switching )
            {
                SwitchWeapon();
            }

            base.AttachToZipline( zipLine );
        }
        #endregion

        #region Melee
        protected override void SetMeleeType()
        {
            base.SetMeleeType();
            // Set dashing melee to true if we're jumping and dashing so that we can transition to dashing on landing
            if ( jumpingMelee && ( right || left ) )
            {
                dashingMelee = true;
            }
        }

        protected override void StartCustomMelee()
        {
            // Throwback mook instead of doing melee
            if ( grabbedUnit != null )
            {
                ReleaseUnit( true );
                return;
            }

            if ( CanStartNewMelee() )
            {
                frame = 1;
                counter = -0.05f;
                AnimateMelee();
            }
            else if ( CanStartMeleeFollowUp() )
            {
                meleeFollowUp = true;
            }

            if ( !jumpingMelee )
            {
                dashingMelee = true;
                xI = ( float )Direction * speed;
            }

            throwingMook = ( nearbyMook != null && nearbyMook.CanBeThrown() );

            if ( !doingMelee )
            {
                StartMeleeCommon();
            }
        }

        protected override void AnimateCustomMelee()
        {
            AnimateMeleeCommon();
            if ( !throwingMook )
            {
                frameRate = 0.07f;
            }

            sprite.SetLowerLeftPixel( ( 25 + frame ) * spritePixelWidth, 9 * spritePixelHeight );
            if ( frame == 2 )
            {
                sound.PlaySoundEffectAt( meleeSwingSounds, 0.3f, transform.position, 1f, true, false, true, 0f );
            }
            else if ( frame == 3 )
            {
                counter -= 0.066f;
                GrabUnit();
            }
            else if ( frame > 3 && !meleeHasHit )
            {
                GrabUnit();
            }

            if ( meleeHasHit && grabbedUnit != null )
            {
                switch ( frame )
                {
                    case 3:
                        targetHoldingXOffset = 5f;
                        targetHoldingYOffset = 1f;
                        isInterpolating = true;
                        break;
                    case 4:
                        targetHoldingXOffset = 7f;
                        targetHoldingYOffset = 2f;
                        isInterpolating = true;
                        break;
                    case 5:
                        targetHoldingXOffset = 9f;
                        targetHoldingYOffset = 3f;
                        isInterpolating = true;
                        break;
                    case 6:
                        targetHoldingXOffset = 11f;
                        targetHoldingYOffset = 4.5f;
                        isInterpolating = true;
                        break;
                }
            }
            // Cancel melee early when punching
            else if ( frame == 5 )
            {
                frame = 0;
                CancelMelee();
            }

            if ( frame >= 7 )
            {
                frame = 0;
                CancelMelee();
            }
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
                    yI = 0f;
                }
                else if ( frame <= 3 )
                {
                    if ( meleeChosenUnit == null )
                    {
                        if ( !isInQuicksand )
                        {
                            xI = speed * 1f * transform.localScale.x;
                        }

                        yI = 0f;
                    }
                    else if ( !isInQuicksand )
                    {
                        xI = speed * 0.5f * transform.localScale.x + ( meleeChosenUnit.X - X ) * 2f;
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
            else if ( Y > groundHeight + 1f )
            {
                CancelMelee();
            }
        }

        protected override void CancelMelee()
        {
            targetHoldingXOffset = 11f;
            targetHoldingYOffset = 4.5f;
            isInterpolating = true;
            if ( grabbedUnit != null )
            {
                SwitchToHoldingMaterials();
            }

            base.CancelMelee();
        }

        public static Unit GetNextClosestUnit( int playerNum, DirectionEnum direction, float xRange, float yRange, float x, float y, List<Unit> alreadyFoundUnits )
        {
            if ( Map.units == null )
            {
                return null;
            }

            float num = xRange;
            Unit unit = null;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                Unit unit2 = Map.units[i];
                if ( unit2 != null && !unit2.invulnerable && unit2.health > 0 && GameModeController.DoesPlayerNumDamage( playerNum, unit2.playerNum ) && !alreadyFoundUnits.Contains( unit2 ) )
                {
                    float num2 = unit2.Y + unit2.height / 2f + 3f - y;
                    if ( Mathf.Abs( num2 ) - yRange < unit2.height )
                    {
                        float num3 = unit2.X - x;
                        if ( Mathf.Abs( num3 ) - num < unit2.width && ( ( direction == DirectionEnum.Down && num2 < 0f ) || ( direction == DirectionEnum.Up && num2 > 0f ) || ( direction == DirectionEnum.Right && num3 > 0f ) || ( direction == DirectionEnum.Left && num3 < 0f ) || direction == DirectionEnum.Any ) )
                        {
                            unit = unit2;
                            num = Mathf.Abs( num2 );
                        }
                    }
                }
            }

            if ( unit != null )
            {
                return unit;
            }

            return null;
        }

        protected void GrabUnit()
        {
            bool flag;
            Map.DamageDoodads( 3, DamageType.Knock, X + ( float )( Direction * 4 ), Y, 0f, 0f, 6f, playerNum, out flag, null );
            KickDoors( 24f );
            Unit unit = GetNextClosestUnit( playerNum, transform.localScale.x > 0 ? DirectionEnum.Right : DirectionEnum.Left, 22f, 14f, X - transform.localScale.x * 4, Y + height / 2f, new List<Unit>() );
            if ( unit != null )
            {
                // Pickup unit if not heavy and not a dog and not on the ground
                if ( !unit.IsHeavy() && unit.actionState != ActionState.Fallen && !( unit is MookDog || ( unit is AlienMosquito && !( unit is HellLostSoul ) ) ) )
                {
                    meleeHasHit = true;
                    grabbedUnit = unit;
                    grabbedUnits.Add( unit );
                    unit.Panic( 1000f, true );
                    unit.playerNum = playerNum;
                    gunSprite.gameObject.layer = 28;
                    doRollOnLand = false;
                    // Initialize holding offsets to prevent jumping
                    holdingXOffset = 5f;
                    holdingYOffset = 1f;
                    targetHoldingXOffset = 5f;
                    targetHoldingYOffset = 1f;
                    if ( unit is MookJetpack )
                    {
                        MookJetpack mookJetpack = unit as MookJetpack;
                        Traverse mookTraverse = Traverse.Create( mookJetpack );
                        ( mookTraverse.GetFieldValue( "jetpackAudio" ) as AudioSource ).Stop();
                        mookTraverse.SetFieldValue( "jetpacksOn", false );
                        mookTraverse.SetFieldValue( "spiralling", false );
                    }
                }
                // Punch unit
                else
                {
                    meleeHasHit = true;
                    Map.KnockAndDamageUnit( this, unit, 5, DamageType.Knock, 200f, 100f, ( int )Mathf.Sign( transform.localScale.x ), true, X, Y, false );
                    sound.PlaySoundEffectAt( soundHolder.alternateMeleeHitSound, 0.3f, transform.position, 1f, true, false, false, 0f );
                    EffectsController.CreateProjectilePopWhiteEffect( X + ( width + 4f ) * transform.localScale.x, Y + height + 4f );
                }
            }
            // Try hit terrain
            else
            {
                meleeHasHit = TryMeleeTerrain( 4, 4 );
            }
        }

        protected void ReleaseUnit( bool throwUnit )
        {
            if ( grabbedUnit != null )
            {
                unitWasGrabbed = false;
                grabbedUnit.playerNum = -1;
                if ( grabbedUnit is Mook )
                {
                    Mook grabbedMook = grabbedUnit as Mook;
                    grabbedMook.blindTime = 0;
                    if ( throwUnit )
                    {
                        ThrowBackMook( grabbedMook );
                    }
                }
                else if ( grabbedUnit is Animal )
                {
                    Animal grabbedAnimal = grabbedUnit as Animal;
                    Traverse.Create( grabbedUnit ).SetFieldValue( "blindTime", 0f );
                    grabbedAnimal.xIBlast = transform.localScale.x * 450f;
                    grabbedAnimal.yI = 300f;
                }

                gunSprite.gameObject.layer = 19;
                grabbedUnits.Remove( grabbedUnit );
                grabbedUnit = null;
                SwitchToNormalMaterials();
                ChangeFrame();
            }

            doRollOnLand = true;
        }

        protected void SwitchToHoldingMaterials()
        {
            crossbowMat = crossbowHoldingMat;
            flareGunMat = flareGunHoldingMat;
            if ( currentState == PrimaryState.Crossbow )
            {
                gunSprite.meshRender.material = crossbowMat;
            }
            else if ( currentState == PrimaryState.FlareGun )
            {
                gunSprite.meshRender.material = flareGunMat;
            }

            holdingArm.gameObject.SetActive( true );
        }

        protected void SwitchToNormalMaterials()
        {
            crossbowMat = crossbowNormalMat;
            holdingArm.gameObject.SetActive( false );
            flareGunMat = flareGunNormalMat;
            if ( currentState == PrimaryState.Crossbow )
            {
                gunSprite.meshRender.material = crossbowMat;
            }
            else if ( currentState == PrimaryState.FlareGun )
            {
                gunSprite.meshRender.material = flareGunMat;
            }
        }

        public override void Damage( int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY )
        {
            // Check if we're holding a unit as a shield and facing the explosion
            if ( ( damageType == DamageType.Explosion || damageType == DamageType.Fire || damageType == DamageType.Acid ) && grabbedUnit != null && direction != transform.localScale.x )
            {
                Unit previousUnit = grabbedUnit;
                ReleaseUnit( false );
                previousUnit.Damage( damage * 2, damageType, xI, yI, direction, damageSender, hitX, hitY );
                Knock( damageType, xI, yI, false );
            }
            else
            {
                base.Damage( damage, damageType, xI, yI, direction, damageSender, hitX, hitY );
            }
        }

        public override void SetGestureAnimation( GestureElement.Gestures gesture )
        {
            // Don't allow flexing during melee
            if ( doingMelee )
            {
                return;
            }

            if ( gesture == GestureElement.Gestures.Flex )
            {
                ReleaseUnit( false );
            }

            base.SetGestureAnimation( gesture );
        }
        #endregion

        #region Special
        // Clear reference to current War Rig
        protected void ClearCurrentWarRig()
        {
            currentWarRig = null;
        }

        Vector3 DetermineWarRigSpawn()
        {
            // Facing right
            if ( transform.localScale.x > 0 )
            {
                return new Vector3( SortOfFollow.GetScreenMinX() - 65f, Y, 0f );
            }
            // Facing left
            else
            {
                return new Vector3( SortOfFollow.GetScreenMaxX() + 65f, Y, 0f );
            }
        }

        protected override void UseSpecial()
        {
            if ( SpecialAmmo > 0 && specialGrenade != null )
            {
                SpecialAmmo--;
                // Only clear reference, don't destroy existing War Rigs
                ClearCurrentWarRig();
                currentWarRig = Instantiate<WarRig>( warRigPrefab, DetermineWarRigSpawn(), Quaternion.identity );
                currentWarRig.SetTarget( this, X + transform.localScale.x * 10f, new Vector3( transform.localScale.x, currentWarRig.transform.localScale.y, currentWarRig.transform.localScale.z ), transform.localScale.x );
                currentWarRig.gameObject.SetActive( true );
                if ( special )
                {
                    holdingSpecial = true;
                    holdingSpecialTime = 0f;
                }
            }
            else
            {
                HeroController.FlashSpecialAmmo( playerNum );
                ActivateGun();
            }

            pressSpecialFacingDirection = 0;
        }

        // Makes the War Rig continue moving past where Furiosa summoned it to
        public void GoPastFuriosa()
        {
            currentWarRig.keepGoingBeyondTarget = true;

            if ( currentWarRig.summonedDirection == 1 )
            {
                currentWarRig.secondTargetX = SortOfFollow.GetScreenMaxX() - 20f;
            }
            else
            {
                currentWarRig.secondTargetX = SortOfFollow.GetScreenMinX() + 20f;
            }

            holdingSpecial = false;
        }

        public void ClearInvulnerability()
        {
            invulnerableTime = 0;
            invulnerable = false;
            GetComponent<Renderer>().material.SetColor( "_TintColor", Color.gray );
            crossbowNormalMat.SetColor( "_TintColor", Color.gray );
            crossbowHoldingMat.SetColor( "_TintColor", Color.gray );
            flareGunNormalMat.SetColor( "_TintColor", Color.gray );
            flareGunHoldingMat.SetColor( "_TintColor", Color.gray );
            holdingArm.material.SetColor( "_TintColor", Color.gray );
        }
        #endregion
    }
}