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
        [SaveableSetting]
        public static bool doubleTapSwitch = true;
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
        static protected WarRig warRigPrefab;
        protected WarRig currentWarRig;
        public bool holdingSpecial = false;
        public float holdingSpecialTime = 0f;

        #region General
        protected override void Awake()
        {
            this.gameObject.layer = 19;
            base.Awake();
        }

        public override void PreloadAssets()
        {
            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            CustomHero.PreloadSprites( directoryPath, new List<string> { "gunSpriteCrossbow.png", "gunSpriteCrossbowHolding.png", "gunSpriteFlareGun.png", "gunSpriteFlareGunHolding.png", "special.png", "gunSpriteHolding.png", "vehicleSprite.png", "vehicleCrossbow.png", "vehicleFlareGun.png", "vehicleCrossbow.png", "vehicleWheels.png", "vehicleBumper.png", "vehicleLongSmokestacks.png", "vehicleShortSmokestacks.png", "vehicleFrontSmokestacks.png", "vehicleSpecial.png", "boltExplosive.png", "bolt.png", "harpoon.png" } );
            CustomHero.PreloadSounds( Path.Combine( directoryPath, "sounds" ), new List<string> { "crossbowShot1.wav", "crossbowShot2.wav", "crossbowShot3.wav", "crossbowShot4.wav", "flareShot1.wav", "flareShot2.wav", "flareShot3.wav", "flareShot4.wav", "charged.wav", "weaponSwap.wav", "meleeSwing1.wav", "meleeSwing2.wav", "meleeSwing3.wav", "vehicleIdleLoop.wav", "vehicleBoost.wav", "vehicleHornMedium.wav", "vehicleHornLong.wav", "vehicleHit1.wav", "vehicleHit2.wav", "vehicleHit3.wav", "harpoon.wav" } );
        }

        protected override void Start()
        {
            base.Start();

            this.soundHolderVoice = ( HeroController.GetHeroPrefab( HeroType.Xebro ) as Xebro ).soundHolderVoice;

            this.meleeType = MeleeType.Disembowel;

            this.crossbowMat = ResourcesController.GetMaterial( Path.Combine( directoryPath, "gunSpriteCrossbow.png" ) );
            this.crossbowNormalMat = this.crossbowMat;
            this.crossbowHoldingMat = ResourcesController.GetMaterial( Path.Combine( directoryPath, "gunSpriteCrossbowHolding.png" ) );

            this.flareGunMat = ResourcesController.GetMaterial( Path.Combine( directoryPath, "gunSpriteFlareGun.png" ) );
            this.flareGunNormalMat = this.flareGunMat;
            this.flareGunHoldingMat = ResourcesController.GetMaterial( Path.Combine( directoryPath, "gunSpriteFlareGunHolding.png" ) );

            this.gunSprite.gameObject.layer = 19;
            this.gunSprite.meshRender.material = this.crossbowMat;

            this.originalSpecialMat = ResourcesController.GetMaterial( directoryPath, "special.png" );

            if ( boltPrefab == null )
            {
                boltPrefab = new GameObject( "Bolt", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( BoxCollider ), typeof( Bolt ) } ).GetComponent<Bolt>();
                boltPrefab.gameObject.SetActive( false );
                boltPrefab.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Predabro ) as Predabro ).projectile.soundHolder;
                boltPrefab.Setup( false );
                UnityEngine.Object.DontDestroyOnLoad( boltPrefab );
            }

            if ( explosiveBoltPrefab == null )
            {
                explosiveBoltPrefab = new GameObject( "ExplosiveBolt", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( BoxCollider ), typeof( Bolt ) } ).GetComponent<Bolt>();
                explosiveBoltPrefab.gameObject.SetActive( false );
                explosiveBoltPrefab.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Predabro ) as Predabro ).projectile.soundHolder;
                explosiveBoltPrefab.Setup( true );
                UnityEngine.Object.DontDestroyOnLoad( explosiveBoltPrefab );
            }

            if ( flarePrefab == null )
            {
                for ( int i = 0; i < InstantiationController.PrefabList.Count; ++i )
                {
                    if ( InstantiationController.PrefabList[i] != null && InstantiationController.PrefabList[i].name == "Bullet Flare" )
                    {
                        Flare originalFlare = ( UnityEngine.Object.Instantiate( InstantiationController.PrefabList[i], Vector3.zero, Quaternion.identity ) as GameObject ).GetComponent<Flare>();
                        flarePrefab = originalFlare.gameObject.AddComponent<FuriosaFlare>();
                        flarePrefab.Setup( originalFlare );
                        flarePrefab.gameObject.SetActive( false );
                        UnityEngine.Object.DontDestroyOnLoad( flarePrefab );
                        break;
                    }
                }
            }

            holdingArm = new GameObject( "FuribrosaArm", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } ).GetComponent<MeshRenderer>();
            holdingArm.transform.parent = this.transform;
            holdingArm.gameObject.SetActive( false );
            holdingArm.material = ResourcesController.GetMaterial( directoryPath, "gunSpriteHolding.png" );
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
                if ( warRigPrefab == null )
                {
                    GameObject warRig = null;
                    for ( int i = 0; i < InstantiationController.PrefabList.Count; ++i )
                    {
                        if ( InstantiationController.PrefabList[i] != null && InstantiationController.PrefabList[i].name == "ZMookArmouredGuy" )
                        {
                            warRig = UnityEngine.Object.Instantiate( InstantiationController.PrefabList[i], Vector3.zero, Quaternion.identity ) as GameObject;
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
                    UnityEngine.Object.DontDestroyOnLoad( warRigPrefab );
                }
            }
            catch ( Exception ex )
            {
                BMLogger.Log( "Exception creating WarRig: " + ex.ToString() );
            }

            // Randomize starting weapon
            if ( !randomizedWeapon )
            {
                if ( UnityEngine.Random.value >= 0.5f )
                {
                    this.nextState = PrimaryState.FlareGun;
                    this.SwitchWeapon();
                }
                randomizedWeapon = true;
            }
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            // Load Audio
            string soundPath = Path.Combine( directoryPath, "sounds" );
            this.crossbowSounds = new AudioClip[4];
            this.crossbowSounds[0] = ResourcesController.GetAudioClip( soundPath, "crossbowShot1.wav" );
            this.crossbowSounds[1] = ResourcesController.GetAudioClip( soundPath, "crossbowShot2.wav" );
            this.crossbowSounds[2] = ResourcesController.GetAudioClip( soundPath, "crossbowShot3.wav" );
            this.crossbowSounds[3] = ResourcesController.GetAudioClip( soundPath, "crossbowShot4.wav" );

            this.flareSounds = new AudioClip[4];
            this.flareSounds[0] = ResourcesController.GetAudioClip( soundPath, "flareShot1.wav" );
            this.flareSounds[1] = ResourcesController.GetAudioClip( soundPath, "flareShot2.wav" );
            this.flareSounds[2] = ResourcesController.GetAudioClip( soundPath, "flareShot3.wav" );
            this.flareSounds[3] = ResourcesController.GetAudioClip( soundPath, "flareShot4.wav" );

            this.chargeSound = ResourcesController.GetAudioClip( soundPath, "charged.wav" );

            this.swapSound = ResourcesController.GetAudioClip( soundPath, "weaponSwap.wav" );

            this.meleeSwingSounds = new AudioClip[3];
            this.meleeSwingSounds[0] = ResourcesController.GetAudioClip( soundPath, "meleeSwing1.wav" );
            this.meleeSwingSounds[1] = ResourcesController.GetAudioClip( soundPath, "meleeSwing2.wav" );
            this.meleeSwingSounds[2] = ResourcesController.GetAudioClip( soundPath, "meleeSwing3.wav" );
        }

        protected override void Update()
        {
            base.Update();
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

            // Switch Weapon Pressed
            if ( playerNum >= 0 && playerNum < 4 && switchWeaponKey.IsDown( playerNum ) )
            {
                StartSwitchingWeapon();
            }

            // Check if invulnerability ran out
            if ( this.wasInvulnerable && !this.invulnerable )
            {
                // Fix any not currently displayed textures
                this.wasInvulnerable = false;
                base.GetComponent<Renderer>().material.SetColor( "_TintColor", Color.gray );
                this.crossbowNormalMat.SetColor( "_TintColor", Color.gray );
                this.crossbowHoldingMat.SetColor( "_TintColor", Color.gray );
                this.flareGunNormalMat.SetColor( "_TintColor", Color.gray );
                this.flareGunHoldingMat.SetColor( "_TintColor", Color.gray );
                this.holdingArm.material.SetColor( "_TintColor", Color.gray );
            }

            if ( this.holdingSpecial )
            {
                if ( this.special )
                {
                    this.holdingSpecialTime += this.t;
                    if ( this.holdingSpecialTime > 0.2f )
                    {
                        GoPastFuriosa();
                    }
                }
                else
                {
                    this.holdingSpecial = false;
                }
            }

            // Handle death
            if ( base.actionState == ActionState.Dead && !this.acceptedDeath )
            {
                if ( !this.WillReviveAlready )
                {
                    this.acceptedDeath = true;
                }
            }

            // Release unit if getting on helicopter
            if ( this.isOnHelicopter )
            {
                this.ReleaseUnit( false );
            }

            // Smoothly interpolate holding offsets
            if ( this.grabbedUnit != null && this.isInterpolating )
            {
                this.holdingXOffset = Mathf.Lerp( this.holdingXOffset, this.targetHoldingXOffset, Time.deltaTime * this.interpolationSpeed );
                this.holdingYOffset = Mathf.Lerp( this.holdingYOffset, this.targetHoldingYOffset, Time.deltaTime * this.interpolationSpeed );
                
                // Check if we're close enough to stop interpolating
                if ( Mathf.Abs( this.holdingXOffset - this.targetHoldingXOffset ) < 0.01f && 
                     Mathf.Abs( this.holdingYOffset - this.targetHoldingYOffset ) < 0.01f )
                {
                    this.holdingXOffset = this.targetHoldingXOffset;
                    this.holdingYOffset = this.targetHoldingYOffset;
                    this.isInterpolating = false;
                }
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if ( this.acceptedDeath )
            {
                return;
            }

            if ( this.grabbedUnit != null )
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
                    this.ReleaseUnit( false );
                }
            }
            // Unit was grabbed but no longer exists, switch to normal materials
            else if ( this.unitWasGrabbed )
            {
                this.unitWasGrabbed = false;
                grabbedUnits.RemoveWhere( unit => unit == null );
                this.grabbedUnit = null;
                this.gunSprite.gameObject.layer = 19;
                this.SwitchToNormalMaterials();
                this.ChangeFrame();
            }
        }

        public override void UIOptions()
        {
            GUILayout.Space( 10 );
            // Only display tooltip if it's currently unset (otherwise we'll display BroMaker's tooltips
            switchWeaponKey.OnGUI( out _, (GUI.tooltip == string.Empty) );
            GUILayout.Space( 10 );

            if ( doubleTapSwitch != ( doubleTapSwitch = GUILayout.Toggle( doubleTapSwitch, "Double Tap Down to Switch Weapons" ) ) )
            {
                // Settings changed, update json
                this.SaveSettings();
            }
        }

        public override void HarmonyPatches( Harmony harmony )
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll( assembly );
        }

        public override void Death( float xI, float yI, DamageObject damage )
        {
            this.ReleaseUnit( false );
            base.Death( xI, yI, damage );
        }

        protected override void OnDestroy()
        {
            this.ReleaseUnit( false );
            base.OnDestroy();
        }

        public override void RecallBro()
        {
            // Release unit if despawning
            this.ReleaseUnit( false );

            base.RecallBro();
        }

        protected override void ChangeFrame()
        {
            if ( !this.randomizedWeapon && this.isOnHelicopter )
            {
                this.randomizedWeapon = true;
            }
            base.ChangeFrame();
        }
        #endregion

        #region Primary
        protected override void SetGunPosition( float xOffset, float yOffset )
        {
            base.SetGunPosition( xOffset, yOffset );
            if ( this.grabbedUnit != null )
            {
                if ( !this.isInterpolating )
                {
                    this.holdingXOffset = 11f + xOffset;
                    this.holdingYOffset = 4.5f + yOffset;
                }
                this.holdingArm.transform.localPosition = this.gunSprite.transform.localPosition + new Vector3( 0f, 0f, 0.1f );
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
            if ( this.fireDelay < 0.2f )
            {
                this.releasedFire = true;
            }
            base.ReleaseFire();
        }

        protected override void RunFiring()
        {
            if ( this.health <= 0 )
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
                    if ( this.fire )
                    {
                        this.StopRolling();
                        this.chargeTime += this.t;
                    }
                    else if ( this.releasedFire )
                    {
                        this.UseFire();
                        this.SetGestureAnimation( GestureElement.Gestures.None );
                    }
                }
            }
            else if ( this.currentState == PrimaryState.FlareGun )
            {
                if ( this.fireDelay > 0f )
                {
                    this.fireDelay -= this.t;
                }
                if ( this.fireDelay <= 0f )
                {
                    if ( this.fire || this.releasedFire )
                    {
                        this.UseFire();
                        this.SetGestureAnimation( GestureElement.Gestures.None );
                        this.releasedFire = false;
                    }
                }
            }
        }

        protected override void UseFire()
        {
            if ( this.doingMelee )
            {
                this.CancelMelee();
            }
            this.releasedFire = false;
            if ( Connect.IsOffline )
            {
                this.syncedDirection = (int)base.transform.localScale.x;
            }
            this.FireWeapon( 0f, 0f, 0f, 0 );
            Map.DisturbWildLife( base.X, base.Y, 60f, base.playerNum );
        }

        protected override void FireWeapon( float x, float y, float xSpeed, float ySpeed )
        {
            // Fire crossbow
            if ( this.currentState == PrimaryState.Crossbow )
            {
                // Fire explosive bolt
                if ( this.charged )
                {
                    x = base.X + base.transform.localScale.x * 8f;
                    y = base.Y + 8f;
                    xSpeed = base.transform.localScale.x * 500 + ( this.xI / 2 );
                    ySpeed = 0;
                    this.gunFrame = 3;
                    this.SetGunSprite( this.gunFrame, 0 );
                    this.TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform );
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally( explosiveBoltPrefab, this, x, y, xSpeed, ySpeed, base.playerNum ) as Bolt;

                }
                // Fire normal bolt
                else
                {
                    x = base.X + base.transform.localScale.x * 8f;
                    y = base.Y + 8f;
                    xSpeed = base.transform.localScale.x * 400 + ( this.xI / 2 );
                    ySpeed = 0;
                    this.gunFrame = 3;
                    this.SetGunSprite( this.gunFrame, 0 );
                    this.TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform );
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally( boltPrefab, this, x, y, xSpeed, ySpeed, base.playerNum ) as Bolt;
                }
                this.PlayCrossbowSound( base.transform.position );
                this.fireDelay = crossbowDelay;
            }
            else if ( this.currentState == PrimaryState.FlareGun )
            {
                Projectile flare = null;
                if ( this.attachedToZipline == null )
                {
                    x = base.X + base.transform.localScale.x * 12f;
                    y = base.Y + 8f;
                    xSpeed = base.transform.localScale.x * 450;
                    ySpeed = UnityEngine.Random.Range( 15, 50 );
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform );
                    flare = ProjectileController.SpawnProjectileLocally( flarePrefab, this, x, y, xSpeed, ySpeed, base.playerNum );
                }
                // Move position of shot if attached to a zipline
                else
                {
                    x = base.X + base.transform.localScale.x * 4f;
                    y = base.Y + 8f;
                    xSpeed = base.transform.localScale.x * 450;
                    ySpeed = UnityEngine.Random.Range( 15, 50 );
                    EffectsController.CreateMuzzleFlashEffect( x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform );
                    flare = ProjectileController.SpawnProjectileLocally( flarePrefab, this, x, y, xSpeed, ySpeed, base.playerNum );
                }
                this.gunFrame = 3;
                this.PlayFlareSound( base.transform.position );
                this.fireDelay = flaregunDelay;
            }
        }

        public void PlayChargeSound( Vector3 position )
        {
            this.sound.PlaySoundEffectAt( this.chargeSound, 0.35f, position, 1f, true, false, true, 0f );
        }

        public void PlayCrossbowSound( Vector3 position )
        {
            this.sound.PlaySoundEffectAt( this.crossbowSounds, 0.35f, position, 1f, true, false, true, 0f );
        }

        public void PlayFlareSound( Vector3 position )
        {
            this.sound.PlaySoundEffectAt( this.flareSounds, 0.75f, position, 1f, true, false, true, 0f );
        }

        public void PlaySwapSound( Vector3 position )
        {
            this.sound.PlaySoundEffectAt( this.swapSound, 0.35f, position, 1f, true, false, true, 0f );
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
                            if ( this.gunCounter < 1f && this.gunCounter > 0.08f )
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
                            if ( this.gunCounter > this.gunFramerate )
                            {
                                this.gunCounter -= this.gunFramerate;
                                ++this.gunFrame;
                                if ( this.gunFrame > 9 )
                                {
                                    this.gunFrame = 6;
                                    if ( !this.charged )
                                    {
                                        ++this.chargeCounter;
                                        if ( this.chargeCounter > 1 )
                                        {
                                            this.PlayChargeSound( base.transform.position );
                                            this.charged = true;
                                            this.gunFramerate = 0.04f;
                                        }
                                    }
                                }
                            }
                        }
                        // Don't use SetGunSprite to avoid issues when on zipline
                        this.gunSprite.SetLowerLeftPixel( ( this.gunFrame + 14 ) * this.gunSpritePixelWidth, this.gunSpritePixelHeight );
                    }
                }
                else if ( !this.WallDrag && this.gunFrame > 0 )
                {
                    this.gunCounter += this.t;
                    if ( this.gunCounter > 0.045f )
                    {
                        this.gunCounter -= 0.045f;
                        this.gunFrame--;
                        this.SetGunSprite( this.gunFrame, 0 );
                    }
                }
            }
            // Animate flaregun
            else if ( this.currentState == PrimaryState.FlareGun )
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
                this.SetGunSprite( this.gunFrame, 0 );
            }
            // Animate switching
            else
            {
                this.gunCounter += this.t;
                if ( this.gunCounter > 0.07f )
                {
                    this.gunCounter -= 0.07f;
                    ++this.gunFrame;

                    if ( this.gunFrame == 3 )
                    {
                        this.PlaySwapSound( base.transform.position );
                    }
                }

                if ( this.gunFrame > 5 )
                {
                    this.SwitchWeapon();
                }
                else
                {
                    this.SetGunSprite( 25 + this.gunFrame, 0 );
                }
            }
        }

        public override void StartPilotingUnit( Unit pilottedUnit )
        {
            // Finish switching weapon
            if ( this.currentState == PrimaryState.Switching )
            {
                this.SwitchWeapon();
            }

            // Make sure to release any held units
            this.ReleaseUnit( false );

            // Ensure we don't double fire when exiting units
            this.fire = this.wasFire = false;
            base.StartPilotingUnit( pilottedUnit );
        }

        protected void StartSwitchingWeapon()
        {
            if ( !this.usingSpecial && this.currentState != PrimaryState.Switching && this.attachedToZipline == null )
            {
                this.CancelMelee();
                this.SetGestureAnimation( GestureElement.Gestures.None );
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
                this.gunSprite.meshRender.material = this.flareGunMat;
            }
            else
            {
                this.gunSprite.meshRender.material = this.crossbowMat;
            }
            if ( this.grabbedUnit != null )
            {
                this.holdingArm.gameObject.SetActive( true );
            }
            this.SetGunSprite( 0, 0 );
        }

        protected override void CheckInput()
        {
            base.CheckInput();
            if ( !acceptedDeath && base.actionState != ActionState.Dead && doubleTapSwitch && this.down && !this.wasDown && base.actionState != ActionState.ClimbingLadder )
            {
                if ( Time.realtimeSinceStartup - this.lastDownPressTime < 0.2f )
                {
                    this.StartSwitchingWeapon();
                }
                this.lastDownPressTime = Time.realtimeSinceStartup;
            }
        }

        public override void AttachToZipline( ZipLine zipLine )
        {
            // Finish weapon switch animation if attaching to zipline
            if ( this.currentState == PrimaryState.Switching )
            {
                this.SwitchWeapon();
            }
            base.AttachToZipline( zipLine );
        }
        #endregion

        #region Melee
        protected override void SetMeleeType()
        {
            base.SetMeleeType();
            // Set dashing melee to true if we're jumping and dashing so that we can transition to dashing on landing
            if ( this.jumpingMelee && ( this.right || this.left ) )
            {
                this.dashingMelee = true;
            }
        }

        protected override void StartCustomMelee()
        {
            // Throwback mook instead of doing melee
            if ( this.grabbedUnit != null )
            {
                this.ReleaseUnit( true );
                return;
            }

            if ( this.CanStartNewMelee() )
            {
                base.frame = 1;
                base.counter = -0.05f;
                this.AnimateMelee();
            }
            else if ( this.CanStartMeleeFollowUp() )
            {
                this.meleeFollowUp = true;
            }
            if ( !this.jumpingMelee )
            {
                this.dashingMelee = true;
                this.xI = (float)base.Direction * this.speed;
            }

            this.throwingMook = ( this.nearbyMook != null && this.nearbyMook.CanBeThrown() );

            if ( !this.doingMelee )
            {
                this.StartMeleeCommon();
            }
        }

        protected override void AnimateCustomMelee()
        {
            this.AnimateMeleeCommon();
            if ( !this.throwingMook )
            {
                base.frameRate = 0.07f;
            }
            this.sprite.SetLowerLeftPixel( ( 25 + base.frame ) * this.spritePixelWidth, 9 * this.spritePixelHeight );
            if ( base.frame == 2 )
            {
                this.sound.PlaySoundEffectAt( this.meleeSwingSounds, 0.3f, base.transform.position, 1f, true, false, true, 0f );
            }
            else if ( base.frame == 3 )
            {
                base.counter -= 0.066f;
                this.GrabUnit();
            }
            else if ( base.frame > 3 && !this.meleeHasHit )
            {
                this.GrabUnit();
            }

            if ( this.meleeHasHit && this.grabbedUnit != null )
            {
                switch ( base.frame )
                {
                    case 3:
                        this.targetHoldingXOffset = 5f;
                        this.targetHoldingYOffset = 1f;
                        this.isInterpolating = true;
                        break;
                    case 4:
                        this.targetHoldingXOffset = 7f;
                        this.targetHoldingYOffset = 2f;
                        this.isInterpolating = true;
                        break;
                    case 5:
                        this.targetHoldingXOffset = 9f;
                        this.targetHoldingYOffset = 3f;
                        this.isInterpolating = true;
                        break;
                    case 6:
                        this.targetHoldingXOffset = 11f;
                        this.targetHoldingYOffset = 4.5f;
                        this.isInterpolating = true;
                        break;
                }
            }
            // Cancel melee early when punching
            else if ( base.frame == 5 )
            {
                base.frame = 0;
                this.CancelMelee();
            }

            if ( base.frame >= 7 )
            {
                base.frame = 0;
                this.CancelMelee();
            }
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
                    this.yI = 0f;
                }
                else if ( base.frame <= 3 )
                {
                    if ( this.meleeChosenUnit == null )
                    {
                        if ( !this.isInQuicksand )
                        {
                            this.xI = this.speed * 1f * base.transform.localScale.x;
                        }
                        this.yI = 0f;
                    }
                    else if ( !this.isInQuicksand )
                    {
                        this.xI = this.speed * 0.5f * base.transform.localScale.x + ( this.meleeChosenUnit.X - base.X ) * 2f;
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
            else if ( base.Y > this.groundHeight + 1f )
            {
                this.CancelMelee();
            }
        }

        protected override void CancelMelee()
        {
            this.targetHoldingXOffset = 11f;
            this.targetHoldingYOffset = 4.5f;
            this.isInterpolating = true;
            if ( this.grabbedUnit != null )
            {
                this.SwitchToHoldingMaterials();
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
            Map.DamageDoodads( 3, DamageType.Knock, base.X + (float)( base.Direction * 4 ), base.Y, 0f, 0f, 6f, base.playerNum, out flag, null );
            this.KickDoors( 24f );
            Unit unit = GetNextClosestUnit( this.playerNum, base.transform.localScale.x > 0 ? DirectionEnum.Right : DirectionEnum.Left, 22f, 14f, base.X - base.transform.localScale.x * 4, base.Y + base.height / 2f, new List<Unit>() );
            if ( unit != null )
            {
                // Pickup unit if not heavy and not a dog and not on the ground
                if ( !unit.IsHeavy() && unit.actionState != ActionState.Fallen && !( unit is MookDog || ( unit is AlienMosquito && !( unit is HellLostSoul ) ) ) )
                {
                    this.meleeHasHit = true;
                    this.grabbedUnit = unit;
                    Furibrosa.grabbedUnits.Add( unit );
                    unit.Panic( 1000f, true );
                    unit.playerNum = this.playerNum;
                    this.gunSprite.gameObject.layer = 28;
                    this.doRollOnLand = false;
                    // Initialize holding offsets to prevent jumping
                    this.holdingXOffset = 5f;
                    this.holdingYOffset = 1f;
                    this.targetHoldingXOffset = 5f;
                    this.targetHoldingYOffset = 1f;
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
                    this.meleeHasHit = true;
                    Map.KnockAndDamageUnit( this, unit, 5, DamageType.Knock, 200f, 100f, (int)Mathf.Sign( base.transform.localScale.x ), true, base.X, base.Y, false );
                    this.sound.PlaySoundEffectAt( this.soundHolder.alternateMeleeHitSound, 0.3f, base.transform.position, 1f, true, false, false, 0f );
                    EffectsController.CreateProjectilePopWhiteEffect( base.X + ( this.width + 4f ) * base.transform.localScale.x, base.Y + this.height + 4f );
                }
            }
            // Try hit terrain
            else
            {
                this.meleeHasHit = this.TryMeleeTerrain( 4, 4 );
            }
        }

        protected void ReleaseUnit( bool throwUnit )
        {
            if ( this.grabbedUnit != null )
            {
                this.unitWasGrabbed = false;
                this.grabbedUnit.playerNum = -1;
                if ( grabbedUnit is Mook )
                {
                    Mook grabbedMook = grabbedUnit as Mook;
                    grabbedMook.blindTime = 0;
                    if ( throwUnit )
                    {
                        this.ThrowBackMook( grabbedMook );
                    }
                }
                else if ( grabbedUnit is Animal )
                {
                    Animal grabbedAnimal = grabbedUnit as Animal;
                    Traverse.Create( grabbedUnit ).SetFieldValue( "blindTime", 0f );
                    grabbedAnimal.xIBlast = base.transform.localScale.x * 450f;
                    grabbedAnimal.yI = 300f;
                }
                this.gunSprite.gameObject.layer = 19;
                Furibrosa.grabbedUnits.Remove( grabbedUnit );
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
            if ( this.currentState == PrimaryState.Crossbow )
            {
                this.gunSprite.meshRender.material = this.crossbowMat;
            }
            else if ( this.currentState == PrimaryState.FlareGun )
            {
                this.gunSprite.meshRender.material = this.flareGunMat;
            }
            this.holdingArm.gameObject.SetActive( true );
        }

        protected void SwitchToNormalMaterials()
        {
            this.crossbowMat = this.crossbowNormalMat;
            this.holdingArm.gameObject.SetActive( false );
            this.flareGunMat = this.flareGunNormalMat;
            if ( this.currentState == PrimaryState.Crossbow )
            {
                this.gunSprite.meshRender.material = this.crossbowMat;
            }
            else if ( this.currentState == PrimaryState.FlareGun )
            {
                this.gunSprite.meshRender.material = this.flareGunMat;
            }
        }

        public override void Damage( int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY )
        {
            // Check if we're holding a unit as a shield and facing the explosion
            if ( ( damageType == DamageType.Explosion || damageType == DamageType.Fire || damageType == DamageType.Acid ) && this.grabbedUnit != null && direction != base.transform.localScale.x )
            {
                Unit previousUnit = this.grabbedUnit;
                this.ReleaseUnit( false );
                previousUnit.Damage( damage * 2, damageType, xI, yI, direction, damageSender, hitX, hitY );
                this.Knock( damageType, xI, yI, false );
            }
            else
            {
                base.Damage( damage, damageType, xI, yI, direction, damageSender, hitX, hitY );
            }
        }

        public override void SetGestureAnimation( GestureElement.Gestures gesture )
        {
            // Don't allow flexing during melee
            if ( this.doingMelee )
            {
                return;
            }
            if ( gesture == GestureElement.Gestures.Flex )
            {
                this.ReleaseUnit( false );
            }
            base.SetGestureAnimation( gesture );
        }
        #endregion

        #region Special
        // Clear reference to current War Rig
        protected void ClearCurrentWarRig()
        {
            this.currentWarRig = null;
        }

        Vector3 DetermineWarRigSpawn()
        {
            // Facing right
            if ( base.transform.localScale.x > 0 )
            {
                return new Vector3( SortOfFollow.GetScreenMinX() - 65f, base.Y, 0f );
            }
            // Facing left
            else
            {
                return new Vector3( SortOfFollow.GetScreenMaxX() + 65f, base.Y, 0f );
            }
        }

        protected override void UseSpecial()
        {
            if ( this.SpecialAmmo > 0 && this.specialGrenade != null )
            {
                this.SpecialAmmo--;
                // Only clear reference, don't destroy existing War Rigs
                this.ClearCurrentWarRig();
                this.currentWarRig = UnityEngine.Object.Instantiate<WarRig>( warRigPrefab, DetermineWarRigSpawn(), Quaternion.identity );
                this.currentWarRig.SetTarget( this, base.X + base.transform.localScale.x * 10f, new Vector3( base.transform.localScale.x, this.currentWarRig.transform.localScale.y, this.currentWarRig.transform.localScale.z ), base.transform.localScale.x );
                this.currentWarRig.gameObject.SetActive( true );
                if ( this.special )
                {
                    this.holdingSpecial = true;
                    this.holdingSpecialTime = 0f;
                }
            }
            else
            {
                HeroController.FlashSpecialAmmo( base.playerNum );
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

        public void ClearInvulnerability()
        {
            this.invulnerableTime = 0;
            this.invulnerable = false;
            base.GetComponent<Renderer>().material.SetColor( "_TintColor", Color.gray );
            this.crossbowNormalMat.SetColor( "_TintColor", Color.gray );
            this.crossbowHoldingMat.SetColor( "_TintColor", Color.gray );
            this.flareGunNormalMat.SetColor( "_TintColor", Color.gray );
            this.flareGunHoldingMat.SetColor( "_TintColor", Color.gray );
            this.holdingArm.material.SetColor( "_TintColor", Color.gray );
        }
        #endregion
    }
}
