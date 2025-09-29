using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.CustomObjects.Projectiles;
using HarmonyLib;
using Rogueforce;
using UnityEngine;

namespace RJBrocready
{
    [HeroPreset( "R.J. Brocready", HeroType.Rambro )]
    public class RJBrocready : CustomHero
    {
        // Primary
        Projectile[] projectiles;
        public AudioSource flameSource;
        public AudioClip flameStart, flameLoop, flameBurst;
        protected float fireFlashCounter = 0f;
        protected float burstCooldown = 0f;
        protected int burstCount = 0;
        protected const float originalFirerate = 0.2f;

        // Special
        Dynamite dynamitePrefab;
        protected bool spawnedDynamite = false;

        // Melee
        protected bool throwingMook = false;
        public AudioClip[] fireAxeSound;
        public AudioClip[] axeHitSound;
        protected bool playedAxeSound = false;
        protected bool meleeBufferedPress = false;
        protected bool meleeHasHitTerrain = false;

        // The Thing
        public enum ThingState
        {
            Human = 0,
            FakeDeath = 1,
            HumanForm = 2,
            EnteringMonsterForm = 3,
            MonsterForm = 4,
            Reforming = 5,
        }
        protected bool gibbed = false;
        protected bool canBecomeThing = true;
        protected bool theThing = false;
        public ThingState currentState = ThingState.Human;
        protected float fakeDeathTime = 0f;
        protected const float fakeDeathMaxTime = 1.25f;
        Material thingMaterial, thingGunMaterial, thingSpecialIconMaterial, thingArmlessMaterial, thingAvatarMaterial, thingMonsterFormMaterial;
        protected float MonsterFormCounter = 0f;
        protected const float MaxMonsterFormTime = 3f;
        protected const float MaxMonsterFormTimeReduced = 0.2f;
        protected float SpriteOffsetX = 0f;
        protected float SpriteOffsetY = 0f;
        protected bool wasAnticipatingWallClimb = false;
        protected bool currentlyAnticipatingWallClimb = false;
        public AudioClip[] thingTransformSounds;
        public AudioClip reformSound;
        protected float scareDelay = 0f;

        // The Thing Primary
        protected List<Unit> alreadyHitUnits;
        protected bool hasHitTerrain = false;
        protected bool releasedFire = false;
        protected bool firedOnce = false;
        protected float firePressed = 0f;
        protected bool playedHitSound = false;
        protected bool playedMissSound = false;
        public AudioClip[] whipStartSounds;
        public AudioClip whipStartSound;
        public AudioClip[] tentacleHitSounds;
        public AudioClip[] tentacleHitTerrainSounds;
        public AudioClip[] whipHitSounds;
        public AudioClip[] whipHitSounds2;
        public AudioClip[] whipMissSounds;

        // The Thing Melee
        public AudioClip[] biteSound;

        // The Thing Special
        SpriteSM tentacleWhipSprite;
        LineRenderer tentacleLine;
        public enum TentacleState
        {
            Inactive = 0,
            Extending = 1,
            Attached = 2,
            Retracting = 3,
            ReadyToEat = 4
        }
        protected bool tentacleHitUnit = false;
        protected bool tentacleHitGround = false;
        protected Unit unitHit;
        protected RaycastHit groundHit;
        protected Vector3 tentacleHitPoint, tentacleDirection;
        protected Vector3 tentacleOffset = new Vector3( 0f, 13f, 0f );
        protected float tentacleMaterialScale = 0.25f;
        protected float tentacleMaterialOffset = -0.125f;
        protected TentacleState currentTentacleState = TentacleState.Inactive;
        protected float tentacleExtendTime = 0f;
        protected float tentacleRange = 128f;
        protected LayerMask fragileGroundLayer;
        protected int tentacleDamage = 10;
        protected bool impaled = false;
        protected Transform tentacleImpaler;
        protected float tentacleRetractTimer = 0f;
        protected bool actuallyUsingSpecial = false;
        public AudioClip[] tentacleImpaleSounds;

        // Misc
        protected const float OriginalSpeed = 130f;
        protected const float MonsterFormSpeed = 100f;
        protected const float AttackingMonsterFormSpeed = 50f;
        protected bool acceptedDeath = false;
        protected bool wasInvulnerable = false;
        protected bool startAsTheThing = false;
        public static bool jsonLoaded = false;
        [SaveableSetting]
        public static List<bool> previouslyDiedInIronBro = new List<bool>() { false, false, false, false, false };
        [SaveableSetting]
        public static bool permanentlyBecomeThingInIronBro = true;

        #region General
        public override void PreloadAssets()
        {
            CustomHero.PreloadSprites( DirectoryPath, new List<string> { "tentacle.png", "tentacleLine.png", "thingSprite.png", "thingGunSprite.png", "thingSpecial.png", "armlessSprite.png", "thingAvatar.png", "thingMonsterSprite.png" } );
            CustomHero.PreloadSprites( ProjectilePath, new List<string> { "Dynamite.png" } );
            CustomHero.PreloadSounds( SoundPath, new List<string>() { "dynamiteExplosion.wav", "flameStart.wav", "flameLoop.wav", "fireAxe1.wav", "fireAxe2.wav", "fireAxe3.wav", "axeHit1.wav", "axeHit2.wav", "axeHit3.wav" } );
            CustomHero.PreloadSounds( Path.Combine( SoundPath, "ThingSounds" ), new List<string>() { "transform1.wav", "transform2.wav", "transform3.wav", "transformBack.wav", "whipStart1.wav", "whipStart2.wav", "whipStart3.wav", "KnifeStab2.wav", "tentacleHit1.wav", "tentacleHit2.wav", "tentacleHit3.wav", "tentacleHitTerrain1.wav", "tentacleHitTerrain2.wav", "whipHit11.wav", "whipHit12.wav", "whipHit13.wav", "whipHit21.wav", "whipHit22.wav", "whipHit23.wav", "whipMiss1.wav", "whipMiss2.wav", "whipMiss3.wav", "bite.wav", "bite2.wav", "bite3.wav", "tentacleImpale1.wav", "tentacleImpale2.wav", "tentacleImpale3.wav" } );
        }

        public override void HarmonyPatches( Harmony harmony )
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll( assembly );
        }

        public override void RegisterCustomTriggers()
        {
            base.RegisterCustomTriggers();

            RocketLib.CustomTriggers.CustomTriggerManager.RegisterCustomTrigger( typeof( RJBrocreadyAction ), typeof( RJBrocreadyActionInfo ), "R.J. Brocready - Disable The Thing", "Custom Bros" );
        }

        protected override void Start()
        {
            base.Start();

            BaBroracus bro = HeroController.GetHeroPrefab( HeroType.BaBroracus ) as BaBroracus;
            projectiles = new Projectile[] { bro.projectile, bro.projectile2, bro.projectile3 };
            this.flameBurst = bro.flameSoundEnd;

            dynamitePrefab = CustomGrenade.CreatePrefab<Dynamite>();

            tentacleWhipSprite = new GameObject( "TentacleWhip", new Type[] { typeof( MeshRenderer ), typeof( MeshFilter ), typeof( SpriteSM ) } ).GetComponent<SpriteSM>();
            MeshRenderer renderer = tentacleWhipSprite.GetComponent<MeshRenderer>();
            renderer.material = ResourcesController.GetMaterial( DirectoryPath, "tentacle.png" );
            renderer.material.mainTexture.wrapMode = TextureWrapMode.Clamp;
            tentacleWhipSprite.transform.parent = this.transform;
            tentacleWhipSprite.lowerLeftPixel = new Vector2( 0, 16 );
            tentacleWhipSprite.pixelDimensions = new Vector2( 128, 16 );
            tentacleWhipSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            tentacleWhipSprite.width = 128;
            tentacleWhipSprite.height = 8;
            tentacleWhipSprite.transform.localPosition = new Vector3( 65f, 12f, -2f );
            tentacleWhipSprite.SetTextureDefaults();
            tentacleWhipSprite.gameObject.SetActive( false );

            tentacleLine = new GameObject( "TentacleLine", new Type[] { typeof( LineRenderer ) } ).GetComponent<LineRenderer>();
            tentacleLine.transform.parent = this.transform;
            tentacleLine.material = ResourcesController.GetMaterial( DirectoryPath, "tentacleLine.png" );
            tentacleLine.material.mainTexture.wrapMode = TextureWrapMode.Repeat;

            this.tentacleImpaler = new GameObject( "TentacleImpaler" ).transform;

            this.fragileGroundLayer = this.fragileLayer | this.groundLayer;

            this.currentMeleeType = MeleeType.Disembowel;
            this.meleeType = MeleeType.Disembowel;

            this.thingMaterial = ResourcesController.GetMaterial( DirectoryPath, "thingSprite.png" );
            this.thingGunMaterial = ResourcesController.GetMaterial( DirectoryPath, "thingGunSprite.png" );
            this.thingSpecialIconMaterial = ResourcesController.GetMaterial( DirectoryPath, "thingSpecial.png" );
            this.thingArmlessMaterial = ResourcesController.GetMaterial( DirectoryPath, "armlessSprite.png" );
            this.thingAvatarMaterial = ResourcesController.GetMaterial( DirectoryPath, "thingAvatar.png" );
            this.thingMonsterFormMaterial = ResourcesController.GetMaterial( DirectoryPath, "thingMonsterSprite.png" );

            this.flameSource = base.gameObject.AddComponent<AudioSource>();
            this.flameSource.rolloffMode = AudioRolloffMode.Linear;
            this.flameSource.maxDistance = 500f;
            this.flameSource.spatialBlend = 1f;
            this.flameSource.volume = 0.4f;
            this.flameSource.playOnAwake = false;
            this.flameSource.Stop();

            if ( GameModeController.IsHardcoreMode && RJBrocready.permanentlyBecomeThingInIronBro )
            {
                startAsTheThing = previouslyDiedInIronBro[PlayerProgress.currentWorldMapSaveSlot];
            }

            if ( startAsTheThing )
            {
                this.theThing = true;
                this.BecomeTheThing();
            }
        }

        public override void AfterPrefabSetup()
        {
            string thingSoundPath = Path.Combine( SoundPath, "ThingSounds" );

            this.flameStart = ResourcesController.GetAudioClip( SoundPath, "flameStart.wav" );
            this.flameLoop = ResourcesController.GetAudioClip( SoundPath, "flameLoop.wav" );

            this.fireAxeSound = new AudioClip[3];
            this.fireAxeSound[0] = ResourcesController.GetAudioClip( SoundPath, "fireAxe1.wav" );
            this.fireAxeSound[1] = ResourcesController.GetAudioClip( SoundPath, "fireAxe2.wav" );
            this.fireAxeSound[2] = ResourcesController.GetAudioClip( SoundPath, "fireAxe3.wav" );

            this.axeHitSound = new AudioClip[3];
            this.axeHitSound[0] = ResourcesController.GetAudioClip( SoundPath, "axeHit1.wav" );
            this.axeHitSound[1] = ResourcesController.GetAudioClip( SoundPath, "axeHit2.wav" );
            this.axeHitSound[2] = ResourcesController.GetAudioClip( SoundPath, "axeHit3.wav" );

            this.thingTransformSounds = new AudioClip[3];
            this.thingTransformSounds[0] = ResourcesController.GetAudioClip( thingSoundPath, "transform1.wav" );
            this.thingTransformSounds[1] = ResourcesController.GetAudioClip( thingSoundPath, "transform2.wav" );
            this.thingTransformSounds[2] = ResourcesController.GetAudioClip( thingSoundPath, "transform3.wav" );

            this.reformSound = ResourcesController.GetAudioClip( thingSoundPath, "transformBack.wav" );

            this.whipStartSounds = new AudioClip[3];
            this.whipStartSounds[0] = ResourcesController.GetAudioClip( thingSoundPath, "whipStart1.wav" );
            this.whipStartSounds[1] = ResourcesController.GetAudioClip( thingSoundPath, "whipStart2.wav" );
            this.whipStartSounds[2] = ResourcesController.GetAudioClip( thingSoundPath, "whipStart3.wav" );

            this.whipStartSound = ResourcesController.GetAudioClip( thingSoundPath, "KnifeStab2.wav" );

            this.tentacleHitSounds = new AudioClip[3];
            this.tentacleHitSounds[0] = ResourcesController.GetAudioClip( thingSoundPath, "tentacleHit1.wav" );
            this.tentacleHitSounds[1] = ResourcesController.GetAudioClip( thingSoundPath, "tentacleHit2.wav" );
            this.tentacleHitSounds[2] = ResourcesController.GetAudioClip( thingSoundPath, "tentacleHit3.wav" );

            this.tentacleHitTerrainSounds = new AudioClip[2];
            this.tentacleHitTerrainSounds[0] = ResourcesController.GetAudioClip( thingSoundPath, "tentacleHitTerrain1.wav" );
            this.tentacleHitTerrainSounds[1] = ResourcesController.GetAudioClip( thingSoundPath, "tentacleHitTerrain2.wav" );

            this.whipHitSounds = new AudioClip[3];
            this.whipHitSounds[0] = ResourcesController.GetAudioClip( thingSoundPath, "whipHit11.wav" );
            this.whipHitSounds[1] = ResourcesController.GetAudioClip( thingSoundPath, "whipHit12.wav" );
            this.whipHitSounds[2] = ResourcesController.GetAudioClip( thingSoundPath, "whipHit13.wav" );

            this.whipHitSounds2 = new AudioClip[3];
            this.whipHitSounds2[0] = ResourcesController.GetAudioClip( thingSoundPath, "whipHit21.wav" );
            this.whipHitSounds2[1] = ResourcesController.GetAudioClip( thingSoundPath, "whipHit22.wav" );
            this.whipHitSounds2[2] = ResourcesController.GetAudioClip( thingSoundPath, "whipHit23.wav" );

            this.whipMissSounds = new AudioClip[3];
            this.whipMissSounds[0] = ResourcesController.GetAudioClip( thingSoundPath, "whipMiss1.wav" );
            this.whipMissSounds[1] = ResourcesController.GetAudioClip( thingSoundPath, "whipMiss2.wav" );
            this.whipMissSounds[2] = ResourcesController.GetAudioClip( thingSoundPath, "whipMiss3.wav" );

            this.biteSound = new AudioClip[3];
            this.biteSound[0] = ResourcesController.GetAudioClip( thingSoundPath, "bite.wav" );
            this.biteSound[1] = ResourcesController.GetAudioClip( thingSoundPath, "bite2.wav" );
            this.biteSound[2] = ResourcesController.GetAudioClip( thingSoundPath, "bite3.wav" );

            this.tentacleImpaleSounds = new AudioClip[3];
            this.tentacleImpaleSounds[0] = ResourcesController.GetAudioClip( thingSoundPath, "tentacleImpale1.wav" );
            this.tentacleImpaleSounds[1] = ResourcesController.GetAudioClip( thingSoundPath, "tentacleImpale2.wav" );
            this.tentacleImpaleSounds[2] = ResourcesController.GetAudioClip( thingSoundPath, "tentacleImpale3.wav" );
        }

        public override int GetVariant()
        {
            // Always start as human variant
            return 0;
        }

        protected override void Update()
        {
            if ( this.invulnerable )
            {
                this.wasInvulnerable = true;
            }

            if ( currentState != ThingState.FakeDeath )
            {
                base.Update();
            }
            else
            {
                this.SetDeltaTime();
                this.RunMovement();
                this.counter += this.t;
                if ( this.counter > this.frameRate )
                {
                    this.counter -= this.frameRate;
                    this.IncreaseFrame();
                    this.AnimateFakeDeath();
                }

                if ( this.fakeDeathTime < fakeDeathMaxTime )
                {
                    this.fakeDeathTime += this.t;
                    if ( this.fakeDeathTime >= fakeDeathMaxTime )
                    {
                        base.frame = 0;
                    }
                }
                else
                {
                    this.scareDelay -= this.t;
                    if ( this.scareDelay <= 0f && base.frame > 4 )
                    {
                        this.scareDelay = 0.0667f;
                        Map.PanicUnits( base.X, base.Y, 100f, 6f, 2f, true, false );
                    }
                }
                this.CheckFacingDirection();
            }

            if ( this.acceptedDeath )
            {
                if ( this.health <= 0 && !this.WillReviveAlready )
                {
                    return;
                }
                // Revived
                else
                {
                    // Handle revive
                    this.acceptedDeath = false;
                }
            }

            // Stop flamethrower when getting on helicopter
            if ( this.isOnHelicopter )
            {
                this.flameSource.enabled = false;
                this.tentacleLine.enabled = false;
                this.tentacleWhipSprite.gameObject.SetActive( false );
                // Release unit if one is impaled
                if ( this.unitHit != null )
                {
                    this.unitHit.Unimpale( 3, DamageType.Blade, 0f, 0f, this );
                    if ( unitHit is Mook )
                    {
                        unitHit.useImpaledFrames = false;
                    }
                }
            }

            // Check if invulnerability ran out
            if ( this.wasInvulnerable && !this.invulnerable )
            {
                base.GetComponent<Renderer>().material.SetColor( "_TintColor", Color.gray );
                gunSprite.meshRender.material.SetColor( "_TintColor", Color.gray );
            }

            // Count down thing timer
            if ( this.MonsterFormCounter > 0f )
            {
                if ( this.down && this.ducking )
                {
                    this.MonsterFormCounter = Mathf.Min( this.MonsterFormCounter, MaxMonsterFormTimeReduced );
                }
                this.MonsterFormCounter -= this.t;
                if ( this.MonsterFormCounter <= 0f )
                {
                    this.currentState = ThingState.Reforming;
                    this.gunFrame = 0;
                    base.GetComponent<Renderer>().material = this.thingArmlessMaterial;
                    this.sprite.pixelDimensions = new Vector2( 32f, 32f );
                    this.sprite.SetSize( 32, 32 );
                    this.sprite.RecalcTexture();
                    this.spritePixelHeight = 32;
                    this.spritePixelWidth = 32;
                    this.SpriteOffsetX = 0f;
                    this.SpriteOffsetY = 0f;
                    this.sprite.SetLowerLeftPixel( 0f, 32f );
                    this.ActivateGun();
                    this.gunSprite.SetLowerLeftPixel( ( this.gunFrame + 12 ) * 64f, 64f );
                    this.ChangeFrame();
                    Sound.GetInstance().PlaySoundEffectAt( this.reformSound, 1f, base.transform.position, 1f, true, false, false, 0f );
                }
            }

            // Update thing's special tentacle
            if ( this.usingSpecial && this.currentTentacleState > TentacleState.Inactive )
            {
                this.UpdateTentacle();
            }

            // Handle death
            if ( base.actionState == ActionState.Dead && !this.acceptedDeath )
            {
                if ( !this.WillReviveAlready )
                {
                    this.acceptedDeath = true;
                }
            }
        }

        public override void UIOptions()
        {
            RJBrocready.permanentlyBecomeThingInIronBro = GUILayout.Toggle( RJBrocready.permanentlyBecomeThingInIronBro, "Carry over death between levels in IronBro" );
        }
        #endregion

        #region Primary
        protected override void StartFiring()
        {
            if ( !theThing )
            {
                base.StartFiring();
                this.flameSource.loop = false;
                this.flameSource.clip = flameStart;
                this.flameSource.Play();
                this.fireFlashCounter = 0f;
                this.burstCooldown = UnityEngine.Random.Range( 1f, 2f );
                this.burstCount = 0;
                this.fireRate = originalFirerate;
            }
            else
            {
                this.ThingStartFiring();
            }
        }

        protected override void StopFiring()
        {
            if ( !theThing )
            {
                base.StopFiring();
                this.flameSource.Stop();
            }
            else
            {
                this.ThingStopFiring();
            }
        }

        protected override void RunFiring()
        {
            if ( this.health <= 0 )
            {
                return;
            }
            if ( !theThing )
            {
                this.fireDelay -= this.t;
                if ( this.fire )
                {
                    this.StopRolling();
                }
                if ( this.fire && this.fireDelay <= 0f )
                {
                    this.fireCounter += this.t;
                    this.fireFlashCounter += this.t;
                    if ( this.fireCounter >= this.fireRate )
                    {
                        this.fireCounter -= this.fireRate;
                        this.UseFire();
                        this.SetGestureAnimation( GestureElement.Gestures.None );
                    }
                    if ( this.fireFlashCounter >= 0.1f )
                    {
                        this.fireFlashCounter -= 0.1f;
                        EffectsController.CreateMuzzleFlashEffect( base.X + base.transform.localScale.x * 11f, base.Y + 10f, -25f, ( base.transform.localScale.x * 60f + this.xI ) * 0.01f, ( UnityEngine.Random.value * 60f - 30f ) * 0.01f, base.transform );
                        this.FireFlashAvatar();
                    }
                }

                this.fireDelay -= this.t;
                if ( this.fire )
                {
                    // Start flame loop
                    if ( !this.flameSource.loop && ( this.flameSource.clip.length - this.flameSource.time ) <= 0.02f )
                    {
                        this.flameSource.clip = flameLoop;
                        this.flameSource.loop = true;
                        this.flameSource.Play();
                    }
                    if ( this.fireDelay <= 0f )
                    {
                        this.fireCounter += this.t;
                        this.fireFlashCounter += this.t;
                        if ( burstCooldown > 0 )
                        {
                            this.burstCooldown -= this.t;
                            if ( burstCooldown < 0 )
                            {
                                this.fireRate = 0.1f;
                                this.burstCount = UnityEngine.Random.Range( 12, 20 );
                                this.sound.PlaySoundEffectAt( this.flameBurst, 0.5f, base.transform.position, 1f, true, false, false, 0f );
                            }
                        }

                        if ( this.fireCounter >= this.fireRate )
                        {
                            this.fireCounter -= this.fireRate;
                            this.UseFire();
                        }
                        if ( this.fireFlashCounter >= 0.1f )
                        {
                            this.fireFlashCounter -= 0.1f;
                            EffectsController.CreateMuzzleFlashEffect( base.X + base.transform.localScale.x * 11f, base.Y + 10f, -25f, ( base.transform.localScale.x * 60f + this.xI ) * 0.01f, ( UnityEngine.Random.value * 60f - 30f ) * 0.01f, base.transform );
                            this.FireFlashAvatar();
                        }
                    }
                }
            }
            else
            {
                this.ThingRunFiring();
            }
        }

        protected override void UseFire()
        {
            if ( !theThing )
            {
                if ( this.burstCooldown <= 0f )
                {
                    this.FireWeapon( base.X + base.transform.localScale.x * 11f, base.Y + 10f, base.transform.localScale.x * 180f + this.xI, UnityEngine.Random.value * 30 - 15f );
                    this.FireWeapon( base.X + base.transform.localScale.x * 11f, base.Y + 11f, base.transform.localScale.x * 210f + this.xI, UnityEngine.Random.value * 40f - 20f );
                    --this.burstCount;
                    if ( this.burstCount <= 0 )
                    {
                        this.fireRate = originalFirerate;
                        this.burstCooldown = UnityEngine.Random.Range( 0.5f, 2f );
                    }
                }
                else
                {
                    this.FireWeapon( base.X + base.transform.localScale.x * 11f, base.Y + 10f, base.transform.localScale.x * 30f + this.xI, UnityEngine.Random.value * 20f - 15f );
                    this.FireWeapon( base.X + base.transform.localScale.x * 11f, base.Y + 11f, base.transform.localScale.x * 40f + this.xI, UnityEngine.Random.value * 30f - 20f );
                }
                Map.DisturbWildLife( base.X, base.Y, 60f, base.playerNum );
            }
            else
            {
                this.ThingUseFire();
            }
        }

        protected override void FireWeapon( float x, float y, float xSpeed, float ySpeed )
        {
            this.gunFrame = 3;
            this.SetGunSprite( this.gunFrame, 0 );
            ProjectileController.SpawnProjectileLocally( this.projectiles[UnityEngine.Random.Range( 0, 3 )], this, x, y, xSpeed, ySpeed, base.playerNum );
        }

        protected override void RunGun()
        {
            if ( !theThing )
            {
                if ( this.fire )
                {
                    this.gunFrame = 3;
                    this.SetGunSprite( this.gunFrame, 0 );
                }
                else if ( this.gunFrame > 0 )
                {
                    this.gunCounter += this.t;
                    if ( this.gunCounter > 0.05f )
                    {
                        this.gunCounter -= 0.05f;
                        this.gunFrame--;
                        this.SetGunSprite( this.gunFrame, 0 );
                    }
                }
            }
            else
            {
                this.ThingRunGun();
            }
        }

        protected override void SetGunSprite( int spriteFrame, int spriteRow )
        {
            if ( !theThing )
            {
                base.SetGunSprite( spriteFrame, spriteRow );
            }
            else
            {
                // Mess with gunsprite in Monster form
                if ( this.currentState != ThingState.HumanForm )
                {
                    return;
                }
                if ( base.actionState == ActionState.Hanging )
                {
                    this.gunSprite.SetLowerLeftPixel( 64f, 128f );
                }
                else if ( base.actionState == ActionState.ClimbingLadder && this.hangingOneArmed )
                {
                    this.gunSprite.SetLowerLeftPixel( 64f, 128f );
                }
                else if ( this.attachedToZipline != null && base.actionState == ActionState.Jumping )
                {
                    this.gunSprite.SetLowerLeftPixel( 64f, 128f );
                }
                else
                {
                    this.gunSprite.SetLowerLeftPixel( (float)( 64f * spriteFrame ), 128 );
                }
            }
        }
        #endregion

        #region Special
        protected override void PressSpecial()
        {
            if ( !this.theThing )
            {
                this.spawnedDynamite = false;
                base.PressSpecial();
            }
            else
            {
                this.ThingPressSpecial();
            }
        }

        protected override void UseSpecial()
        {
            if ( this.SpecialAmmo > 0 )
            {
                if ( !theThing )
                {
                    if ( !this.spawnedDynamite )
                    {
                        if ( this.down && this.IsOnGround() && this.ducking )
                        {
                            this.dynamitePrefab.SpawnGrenadeLocally( this, base.X + Mathf.Sign( base.transform.localScale.x ) * 6f, base.Y + 10f, 0.001f, 0.011f, Mathf.Sign( base.transform.localScale.x ) * 30f, 70f, base.playerNum, 0 );
                        }
                        else
                        {
                            this.dynamitePrefab.SpawnGrenadeLocally( this, base.X + Mathf.Sign( base.transform.localScale.x ) * 6f, base.Y + 10f, 0.001f, 0.011f, Mathf.Sign( base.transform.localScale.x ) * 300f, 250f, base.playerNum, 0 );
                        }
                        --this.SpecialAmmo;
                        this.spawnedDynamite = true;
                    }
                }
                else
                {
                    this.ThingUseSpecial();
                }
            }
            else
            {
                base.UseSpecial();
            }
        }

        protected override void AnimateSpecial()
        {
            if ( !this.theThing )
            {
                base.AnimateSpecial();
            }
            else
            {
                this.ThingAnimateSpecial();
            }
        }

        public override void Knock( DamageType damageType, float xI, float yI, bool forceTumble )
        {
            // Override knock function to allow greater knockback from dynamite
            if ( damageType == DamageType.SelfEsteem )
            {
                this.impaledBy = null;
                this.impaledByTransform = null;
                if ( this.frozenTime > 0f )
                {
                    return;
                }
                this.xI = Mathf.Clamp( this.xI + xI / 2f, -1200f, 1200f );
                this.xIBlast = Mathf.Clamp( this.xIBlast + xI / 2f, -1200f, 1200f );
                this.yI = this.yI + yI;
                if ( this.IsParachuteActive && yI > 0f )
                {
                    this.IsParachuteActive = false;
                    this.Tumble();
                }
            }
            else
            {
                base.Knock( damageType, xI, yI, forceTumble );
            }
        }
        #endregion

        #region Melee
        protected void MeleeAttack( bool shouldTryHitTerrain, bool playMissSound )
        {
            bool flag;
            Map.DamageDoodads( 3, DamageType.Fire, base.X + (float)( base.Direction * 4 ), base.Y, 0f, 0f, 8f, base.playerNum, out flag, null );
            this.KickDoors( 24f );
            Unit hitUnit;
            if ( hitUnit = Map.HitClosestUnit( this, base.playerNum, 2, DamageType.Blade, 12f, 24f, base.X + base.transform.localScale.x * 8f, base.Y + 8f, base.transform.localScale.x * 100f, 100f, true, true, base.IsMine, false, true ) )
            {
                if ( hitUnit.health > 30 )
                {
                    hitUnit.Damage( 7, DamageType.Fire, base.transform.localScale.x * 100f, 100f, (int)base.transform.localScale.x, this, base.X + base.transform.localScale.x * 8f, base.Y + 8f );
                }
                else
                {
                    hitUnit.Damage( 5, DamageType.Fire, base.transform.localScale.x * 100f, 100f, (int)base.transform.localScale.x, this, base.X + base.transform.localScale.x * 8f, base.Y + 8f );
                }
                // Don't play hit sound multiple times
                if ( !this.meleeHasHit )
                {
                    // Play default knife sound if we hit a metal enemy
                    if ( hitUnit.gameObject.CompareTag( "Metal" ) )
                    {
                        this.sound.PlaySoundEffectAt( this.soundHolder.meleeHitTerrainSound, 0.5f, base.transform.position, 1f, true, false, false, 0f );
                    }
                    else
                    {
                        this.sound.PlaySoundEffectAt( this.axeHitSound, 0.8f, base.transform.position, 1f, true, false, false, 0f );
                    }
                }
                this.meleeHasHit = true;
            }
            else if ( playMissSound )
            {
            }
            this.meleeChosenUnit = null;
            if ( shouldTryHitTerrain && this.TryMeleeTerrain( 0, 20 ) )
            {
                this.meleeHasHit = true;
            }
            this.TriggerBroMeleeEvent();
        }

        protected override bool TryMeleeTerrain( int offset = 0, int meleeDamage = 2 )
        {
            if ( !Physics.Raycast( new Vector3( base.X - base.transform.localScale.x * 4f, base.Y + 4f, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, (float)( 22 + offset ), this.groundLayer )
                && !Physics.Raycast( new Vector3( base.X - base.transform.localScale.x * 4f, base.Y + 12f, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, (float)( 22 + offset ), this.groundLayer ) )
            {
                return false;
            }
            Cage component = this.raycastHit.collider.GetComponent<Cage>();
            if ( component == null && this.raycastHit.collider.transform.parent != null )
            {
                component = this.raycastHit.collider.transform.parent.GetComponent<Cage>();
            }
            if ( component != null )
            {
                MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, component.health, DamageType.Melee, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y );
                return true;
            }
            if ( this.raycastHit.collider.GetComponent<Block>() != null )
            {
                meleeDamage *= 4;
            }
            // Hit non block entity so don't continue dealing damage
            else
            {
                this.meleeHasHitTerrain = true;
            }
            MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, meleeDamage, DamageType.Melee, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y );
            if ( !this.meleeHasHit )
            {
                this.sound.PlaySoundEffectAt( this.soundHolder.meleeHitTerrainSound, 0.3f, base.transform.position, 1f, true, false, false, 0f );
                EffectsController.CreateProjectilePopWhiteEffect( base.X + this.width * base.transform.localScale.x, base.Y + this.height + 4f );
            }
            return true;
        }

        protected override void StartMelee()
        {
            // Make it so we use the knife melee when touching walls so we don't fall off and have no way to get through
            if ( !this.doingMelee && this.theThing && ( this.wallDrag || this.currentlyAnticipatingWallClimb ) )
            {
                this.meleeType = MeleeType.Knife;
            }
            else if ( this.doingMelee )
            {
                this.meleeType = currentMeleeType;
            }
            base.StartMelee();
            this.meleeType = MeleeType.Disembowel;
        }

        // Sets up melee attack
        protected override void StartCustomMelee()
        {
            if ( this.IsInseminated() || this.HasFaceHugger() )
            {
                return;
            }
            if ( this.CanStartNewMelee() )
            {
                base.frame = 1;
                base.counter = -0.05f;
                this.AnimateMelee();
                this.throwingMook = ( this.nearbyMook != null && this.nearbyMook.CanBeThrown() );
                this.playedAxeSound = false;
                this.meleeBufferedPress = false;
            }
            else if ( this.CanStartMeleeFollowUp() )
            {
                this.meleeBufferedPress = true;
            }
            this.StartMeleeCommon();
        }

        protected override void SetMeleeType()
        {
            base.SetMeleeType();
            // Set dashing melee to true if we're jumping and dashing so that we can transition to dashing on landing
            if ( this.jumpingMelee && ( this.right || this.left ) )
            {
                this.dashingMelee = true;
            }
        }

        protected override void StartMeleeCommon()
        {
            if ( !this.meleeFollowUp && this.CanStartNewMelee() )
            {
                if ( this.theThing )
                {
                    base.frame = 0;
                    base.counter = -0.05f;
                    this.ResetMeleeValues();
                    this.lerpToMeleeTargetPos = 0f;
                    this.doingMelee = true;
                    this.showHighFiveAfterMeleeTimer = 0f;
                    this.SetMeleeType();
                    this.meleeStartPos = base.transform.position;
                    ThingStartMelee();
                    this.AnimateMelee();
                }
                else
                {
                    base.frame = 0;
                    base.counter = -0.05f;
                    this.ResetMeleeValues();
                    this.lerpToMeleeTargetPos = 0f;
                    this.doingMelee = true;
                    this.showHighFiveAfterMeleeTimer = 0f;
                    this.DeactivateGun();
                    this.SetMeleeType();
                    this.meleeStartPos = base.transform.position;
                    this.AnimateMelee();
                }
            }
        }

        protected override void ResetMeleeValues()
        {
            base.ResetMeleeValues();
            this.meleeHasHitTerrain = false;
        }

        protected override bool CanStartNewMelee()
        {
            return !( this.doingMelee || this.usingSpecial );
        }

        protected override bool CanStartMeleeFollowUp()
        {
            return true;
        }

        // Calls MeleeAttack
        protected override void AnimateCustomMelee()
        {
            // AnimateMeleeCommon except throwing mooks is disabled for the thing
            this.SetSpriteOffset( 0f, 0f );
            this.rollingFrames = 0;
            if ( base.frame == 1 )
            {
                base.counter -= 0.0334f;
            }
            if ( base.frame == 6 && this.meleeFollowUp )
            {
                base.counter -= 0.08f;
                base.frame = 1;
                this.meleeFollowUp = false;
                this.ResetMeleeValues();
            }
            this.frameRate = 0.025f;
            if ( !this.theThing && base.frame == 2 && this.nearbyMook != null && this.nearbyMook.CanBeThrown() && this.highFive )
            {
                this.CancelMelee();
                this.ThrowBackMook( this.nearbyMook );
                this.nearbyMook = null;
            }

            if ( !theThing )
            {
                if ( !this.throwingMook )
                {
                    base.frameRate = 0.06f;
                }
                int num = 24 + Mathf.Clamp( base.frame, 0, 7 );
                int num2 = 10;
                this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( num2 * this.spritePixelHeight ) );
                if ( !this.throwingMook && ( base.frame == 0 || base.frame == 1 ) && !this.playedAxeSound )
                {
                    this.sound.PlaySoundEffectAt( this.fireAxeSound, 1f, base.transform.position, 1f, true, false, false, 0f );
                    this.playedAxeSound = true;
                }
                else if ( base.frame == 3 )
                {
                    base.counter -= 0.066f;
                    this.MeleeAttack( true, true );
                }
                else if ( base.frame > 3 && base.frame < 6 )
                {
                    if ( !this.meleeHasHit )
                    {
                        this.MeleeAttack( false, false );
                    }
                    else if ( !this.meleeHasHitTerrain )
                    {
                        this.TryMeleeTerrain( 0, 15 );
                    }
                }
                if ( base.frame >= 8 )
                {
                    base.frame = 0;
                    this.CancelMelee();
                    if ( this.meleeBufferedPress )
                    {
                        this.meleeBufferedPress = false;
                        this.StartCustomMelee();
                    }
                }
            }
            else
            {
                ThingAnimateMelee();
            }
        }

        protected override void RunCustomMeleeMovement()
        {
            if ( !theThing )
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
                            this.xI = this.speed * 0.5f * base.transform.localScale.x + ( this.meleeChosenUnit.X - base.X ) * 6f;
                        }
                    }
                    else if ( base.frame <= 7 )
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
            else
            {
                ThingRunMeleeMovement();
            }
        }

        protected override void CancelMelee()
        {
            if ( this.theThing && this.currentMeleeType != MeleeType.Knife )
            {
                this.ThingCancelMelee();
            }
            this.playedAxeSound = false;
            base.CancelMelee();
        }

        // Don't cancel melee when hitting wall
        protected override void HitLeftWall()
        {
        }

        // Don't cancel melee when hitting wall
        protected override void HitRightWall()
        {
        }
        #endregion

        #region TheThing
        public void SetCanBecomeThing( bool enabled )
        {
            this.canBecomeThing = enabled;
        }

        protected override void Gib( DamageType damageType, float xI, float yI )
        {
            gibbed = true;
            // Cancel out of fake death state to allow character to actually die
            if ( this.currentState == ThingState.FakeDeath )
            {
                this.currentState = ThingState.Human;
            }
            base.Gib( damageType, xI, yI );
        }

        public override void Death( float xI, float yI, DamageObject damage )
        {
            if ( !theThing && canBecomeThing && !gibbed && damage.damageType != DamageType.Acid && damage.damageType != DamageType.Spikes )
            {
                if ( this.IsInseminated() || this.HasFaceHugger() )
                {
                    this.DisConnectFaceHugger();
                    this.inseminatedCounter = 0f;
                }
                FakeDeath( xI, yI, damage );
            }
            else if ( theThing )
            {
                ThingState previousState = this.currentState;
                ExitMonsterForm();
                // Explode into blood if we're in monster form and not coming back to life
                if ( previousState > ThingState.HumanForm && !this.willComeBackToLife )
                {
                    damage.damageType = DamageType.Crush;
                    this.Gib( damage.damageType, Mathf.Sign( xI ) * 50, yI );
                    EffectsController.CreateSlimeExplosion( base.X, base.Y + 5f, 10f, 10f, 140f, 0f, 0f, 0f, 0.5f, 0, 20, 120f, 0f, Vector3.up, BloodColor.Red );
                }
                base.Death( xI, yI, damage );
            }
            else
            {
                base.Death( xI, yI, damage );
            }
            this.flameSource.Stop();
        }

        public void FakeDeath( float xI, float yI, DamageObject damage )
        {
            this.flameSource.Stop();
            this.DeactivateGun();

            this.health = 1;
            currentState = ThingState.FakeDeath;
            this.CancelMelee();
            this.rollingFrames = 0;
            this.doRollOnLand = false;
            this.canWallClimb = false;
            this.canChimneyFlip = false;
            this.theThing = true;
            if ( damage != null )
            {
                if ( damage.damageType != DamageType.Fall && damage.damageType != DamageType.ChainsawImpale && damage.damageType != DamageType.SilencedBullet && damage.damageType != DamageType.Disembowel && damage.damageType != DamageType.Acid )
                {
                    this.xI = xI * 0.3f;
                    if ( yI < 0f )
                    {
                        yI = 0f;
                    }
                    if ( damage.damageType != DamageType.Melee )
                    {
                        this.yI = yI * 0.3f;
                    }
                    else
                    {
                        this.yI = yI * 0.5f;
                    }
                }
            }
            if ( this.impaledByTransform == null && !this.isInQuicksand && damage != null && damage.damageType != DamageType.Acid && damage.damageType != DamageType.SilencedBullet && damage.damageType != DamageType.Disembowel && damage.damageType != DamageType.ChainsawImpale )
            {
                this.yI += 25f;
                float num2 = this.CalculateCeilingHeight();
                if ( base.Y + this.headHeight < num2 - 5f && damage != null && damage.damageType != DamageType.Chainsaw && damage.damageType != DamageType.Shock && !this.isInQuicksand )
                {
                    base.Y += 5f;
                }
            }
            if ( damage != null && damage.damageType != DamageType.SelfEsteem && damage.damageType != DamageType.SilencedBullet && damage.damageType != DamageType.Disembowel && damage.damageType != DamageType.Acid && damage.damageType != DamageType.Shock && this.frozenTime <= 0f )
            {
                EffectsController.CreateBloodParticles( this.bloodColor, base.X, base.Y + 6f, 10, 4f, 5f, 60f, this.xI * 0.5f, this.yI * 0.5f );
            }
            this.ReleaseHeldObject( false );
            if ( base.GetComponent<Collider>() != null )
            {
                base.GetComponent<Collider>().enabled = false;
            }
            Map.ForgetPlayer( base.playerNum, false, false );
            base.GetComponent<Renderer>().material = this.thingMaterial;
            base.frame = 0;
            this.up = this.down = this.left = this.right = this.wasUp = this.wasDown = this.wasLeft = this.wasRight = this.dashing = false;
            this.throwingHeldObject = this.throwingMook = false;
            base.actionState = ActionState.Idle;
            HeroController.SetAvatarCalm( base.playerNum, this.usePrimaryAvatar );
            this.SetGestureAnimation( GestureElement.Gestures.None );
            if ( GameModeController.IsHardcoreMode && RJBrocready.permanentlyBecomeThingInIronBro )
            {
                previouslyDiedInIronBro[PlayerProgress.currentWorldMapSaveSlot] = true;
                this.SaveSettings();
            }

            this.GetComponent<InvulnerabilityFlash>().enabled = false;
            this.invulnerable = true;
            this.AnimateFakeDeath();
        }

        public override void Damage( int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY )
        {
            if ( currentState != ThingState.FakeDeath && !( this.theThing && this.usingSpecial ) )
            {
                base.Damage( damage, damageType, xI, yI, direction, damageSender, hitX, hitY );
            }
        }

        public override bool IsInStealthMode()
        {
            return this.currentState == ThingState.FakeDeath || base.IsInStealthMode();
        }

        protected override void AlertNearbyMooks()
        {
            if ( this.currentState != ThingState.FakeDeath )
            {
                base.AlertNearbyMooks();
            }
        }

        protected override void RunMovement()
        {
            if ( currentState != ThingState.FakeDeath )
            {
                base.RunMovement();
            }
            else
            {
                ActionState currentState = this.actionState;
                this.actionState = ActionState.Dead;
                base.RunMovement();
                this.actionState = currentState;
            }
        }

        protected void AnimateFakeDeath()
        {
            this.frameRate = 0.0334f;
            if ( this.fakeDeathTime >= fakeDeathMaxTime )
            {
                this.frameRate = 0.12f;
                this.sprite.SetLowerLeftPixel( ( ( base.frame - 1 ) * this.spritePixelWidth ), 10 * this.spritePixelHeight );
                // Finished turning
                if ( base.frame == 11 )
                {
                    this.GetComponent<InvulnerabilityFlash>().enabled = true;
                    this.invulnerable = false;
                    this.BecomeTheThing();
                }
            }
            else if ( base.Y > this.groundHeight + 0.2f && this.impaledByTransform == null )
            {
                this.AnimateFallingDeath();
            }
            else
            {
                this.AnimateActualDeath();
            }
        }

        protected void BecomeTheThing()
        {
            if ( base.GetComponent<Collider>() != null )
            {
                base.GetComponent<Collider>().enabled = true;
            }
            this.SwitchVariant( 1 );
            this.headHeight = this.standingHeadHeight;
            this.waistHeight = this.standingWaistHeight;
            this.doRollOnLand = true;
            this.canWallClimb = true;
            this.canChimneyFlip = true;
            this.currentState = ThingState.HumanForm;
            this.xI = 0;
            this.xIBlast = 0;
            this.counter = 1;
            this.lastParentedToTransform = null;
            this.gunFrame = 0;
            this.gunSprite.meshRender.material = this.thingGunMaterial;
            this.gunSprite.pixelDimensions = new Vector2( 64f, 64f );
            this.gunSprite.SetSize( 64, 64 );
            this.gunSprite.RecalcTexture();
            this.SpecialAmmo = 2;
            this.originalSpecialAmmo = 2;
        }

        protected void EnterMonsterForm()
        {
            if ( this.hasBeenCoverInAcid )
            {
                return;
            }

            if ( this.currentState < ThingState.EnteringMonsterForm )
            {
                // Start entering monster form
                this.currentState = ThingState.EnteringMonsterForm;
                this.gunFrame = 0;
                Sound.GetInstance().PlaySoundEffectAt( this.thingTransformSounds, 1f, base.transform.position, 1f, true, false, false, 0f );
            }
            else if ( this.currentState == ThingState.Reforming )
            {
                // Turn back into a monster
                if ( this.gunFrame < 2 )
                {
                    this.currentState = ThingState.MonsterForm;
                }
                else
                {
                    this.gunFrame = 0;
                    this.currentState = ThingState.EnteringMonsterForm;
                }
                Sound.GetInstance().PlaySoundEffectAt( this.thingTransformSounds, 1f, base.transform.position, 1f, true, false, false, 0f );
            }

            this.releasedFire = false;
            this.firedOnce = false;

            if ( this.attachedToZipline != null )
            {
                this.attachedToZipline.DetachUnit( this );
            }
            // Disable counter so we don't revert in the middle of an attack
            this.MonsterFormCounter = -1f;
            this.speed = MonsterFormSpeed;
            this.doRollOnLand = false;
            this.canWallClimb = false;
            this.canChimneyFlip = false;
            this.canCeilingHang = false;
            SetGestureAnimation( GestureElement.Gestures.None );

            this.ActivateGun();

            // Switch to armless sprite
            if ( base.GetComponent<Renderer>().material != this.thingArmlessMaterial )
            {
                base.GetComponent<Renderer>().material = this.thingArmlessMaterial;
                this.sprite.pixelDimensions = new Vector2( 32f, 32f );
                this.sprite.SetSize( 32, 32 );
                this.sprite.RecalcTexture();
                this.spritePixelHeight = 32;
                this.spritePixelWidth = 32;
                this.SpriteOffsetX = 0f;
                this.SpriteOffsetY = 0f;
            }
            this.sprite.SetLowerLeftPixel( 0f, 32f );
            this.ChangeFrame();
            this.gunCounter = 1f;
            this.ThingRunGun();
            this.gunCounter = 0f;
        }

        protected void EnterMonsterFormIdle()
        {
            if ( this.hasBeenCoverInAcid )
            {
                return;
            }

            this.releasedFire = false;
            this.firedOnce = false;

            if ( this.attachedToZipline != null )
            {
                this.attachedToZipline.DetachUnit( this );
            }
            this.speed = MonsterFormSpeed;
            this.currentState = ThingState.MonsterForm;
            this.MonsterFormCounter = MaxMonsterFormTime;
            this.doRollOnLand = false;
            this.canWallClimb = false;
            this.canChimneyFlip = false;
            SetGestureAnimation( GestureElement.Gestures.None );

            this.gunFrame = 0;
            this.DeactivateGun();

            if ( base.GetComponent<Renderer>().material != this.thingMonsterFormMaterial )
            {
                base.GetComponent<Renderer>().material = this.thingMonsterFormMaterial;
                this.sprite.pixelDimensions = new Vector2( 64f, 64f );
                this.sprite.SetSize( 64, 64 );
                this.sprite.RecalcTexture();
                this.spritePixelHeight = 64;
                this.spritePixelWidth = 64;
                this.SpriteOffsetX = 16f;
                this.SpriteOffsetY = 16f;
            }
            this.ChangeFrame();
        }

        protected void ExitMonsterForm()
        {
            this.MonsterFormCounter = -1f;
            this.speed = OriginalSpeed;
            this.currentState = ThingState.HumanForm;
            this.doRollOnLand = true;
            this.canWallClimb = true;
            this.canChimneyFlip = true;
            this.canCeilingHang = true;

            this.SetGunPosition( 0, 0 );
            this.CurrentGunSpriteOffset = new Vector2( -2f, 0f );
            this.SetGunSprite( 0, 0 );

            base.GetComponent<Renderer>().material = this.thingMaterial;
            this.sprite.pixelDimensions = new Vector2( 32f, 32f );
            this.sprite.SetSize( 32, 32 );
            this.sprite.RecalcTexture();
            this.spritePixelHeight = 32;
            this.spritePixelWidth = 32;
            this.SpriteOffsetX = 0f;
            this.SpriteOffsetY = 0f;
            this.ChangeFrame();
        }

        protected void AnimateBecomingMonster()
        {
            if ( this.gunFrame >= 6 )
            {
                this.currentState = ThingState.MonsterForm;
            }

            this.gunSprite.SetLowerLeftPixel( this.gunFrame * 64f, 64f );
        }

        protected void AnimateBecomingHuman()
        {
            this.gunCounter += this.t;
            if ( this.gunCounter > 0.08f )
            {
                this.gunCounter -= 0.08f;
                ++this.gunFrame;
                if ( this.gunFrame > 5 )
                {
                    ExitMonsterForm();
                }
                else
                {
                    this.gunSprite.SetLowerLeftPixel( ( this.gunFrame + 12 ) * 64f, 64f );
                }
            }
        }

        protected override void SetSpriteOffset( float xOffset, float yOffset )
        {
            base.SetSpriteOffset( this.SpriteOffsetX + xOffset, this.SpriteOffsetY + yOffset );
        }

        protected override void AnimateWallAnticipation()
        {
            // Don't animate wall anticipation if in monster form
            if ( this.currentState <= ThingState.HumanForm )
            {
                base.AnimateWallAnticipation();
            }
            else
            {
                this.AnimateJumping();
                this.MonsterFormCounter = Mathf.Min( MonsterFormCounter, MaxMonsterFormTimeReduced );
            }
            this.wasAnticipatingWallClimb = true;
        }

        protected override void Jump( bool wallJump )
        {
            if ( this.currentState > ThingState.HumanForm && wallJump )
            {
                return;
            }
            base.Jump( wallJump );
        }

        protected override void ChangeFrame()
        {
            base.ChangeFrame();
            if ( this.theThing && !( this.usingSpecial || this.doingMelee ) && this.currentState == ThingState.MonsterForm )
            {
                this.frameRate = Mathf.Max( 0.0334f, this.frameRate );
            }
            if ( this.wasAnticipatingWallClimb )
            {
                this.currentlyAnticipatingWallClimb = true;
            }
            else
            {
                this.currentlyAnticipatingWallClimb = false;
            }
            this.wasAnticipatingWallClimb = false;
        }

        public override void SetGestureAnimation( GestureElement.Gestures gesture )
        {
            if ( !this.doingMelee && !( gesture != GestureElement.Gestures.None && this.theThing && this.currentState > ThingState.HumanForm ) )
            {
                base.SetGestureAnimation( gesture );
            }
            else
            {
                this.MonsterFormCounter = Mathf.Min( MonsterFormCounter, MaxMonsterFormTimeReduced );
            }
        }

        protected override void CoverInAcidRPC()
        {
            // if current state is greater than human form, exit monster form before covering in acid
            if ( this.currentState > ThingState.HumanForm )
            {
                ExitMonsterForm();
            }
            base.CoverInAcidRPC();
        }

        public override bool Inseminate( AlienFaceHugger unit, float xForce, float yForce )
        {
            if ( this.theThing )
            {
                ExitMonsterForm();
            }
            return base.Inseminate( unit, xForce, yForce );
        }

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
            if ( nearbyGrenade != null && this.currentState <= ThingState.HumanForm )
            {
                this.doingMelee = ( this.dashingMelee = false );
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
        #endregion

        #region ThingPrimary
        protected void ThingStartFiring()
        {
            if ( !this.IsInseminated() && !this.HasFaceHugger() && !this.hasBeenCoverInAcid && !this.releasedFire && !( this.usingSpecial || this.doingMelee ) )
            {
                // Switch sprites to monster sprites
                EnterMonsterForm();
            }
            else if ( this.releasedFire )
            {
                this.firedOnce = false;
            }
            this.firePressed = 0f;
        }

        protected void ThingStopFiring()
        {
            if ( !( this.usingSpecial || this.doingMelee ) )
            {
                if ( this.currentState == ThingState.EnteringMonsterForm || this.currentState == ThingState.MonsterForm )
                {
                    this.releasedFire = true;
                }
            }
        }

        protected void ThingRunFiring()
        {
        }

        protected void ThingUseFire()
        {
            this.ThingFireWeapon( base.X, base.Y + 8f, base.transform.localScale.x * 300f, 600f );
            Map.DisturbWildLife( base.X, base.Y, 60f, base.playerNum );
        }

        public static bool HitUnits( MonoBehaviour damageSender, int playerNum, int damage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, bool penetrates, bool knock, bool canGib, bool canHeadShot, List<Unit> hitUnits, bool onlyDamageInRadius = false )
        {
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
                if ( unit != null && ( GameModeController.DoesPlayerNumDamage( playerNum, unit.playerNum ) || ( unit.playerNum < 0 && unit.CatchFriendlyBullets() ) ) && !unit.invulnerable && unit.health <= num )
                {
                    float num2 = unit.X - x;
                    if ( Mathf.Abs( num2 ) - xRange < unit.width )
                    {
                        float num3 = unit.Y + unit.height / 2f + 4f - y;
                        if ( Mathf.Abs( num3 ) - yRange < unit.height && ( !onlyDamageInRadius || Mathf.Sqrt( num2 * num2 + num3 * num3 ) <= xRange + unit.width ) )
                        {
                            if ( !hitUnits.Contains( unit ) )
                            {
                                if ( hitUnits != null )
                                {
                                    hitUnits.Add( unit );
                                }
                                if ( !penetrates && unit.health > 0 )
                                {
                                    num = 0;
                                    flag = true;
                                }
                                // Deal extra damage to bosses
                                if ( unit.health > 30 )
                                {
                                    damage += 3;
                                }
                                if ( !canGib && unit.health <= 0 )
                                {
                                    Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                                }
                                else if ( num3 < -unit.height && canHeadShot && unit.CanHeadShot() )
                                {
                                    Map.HeadShotUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( damage, playerNum ), damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y );
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
            }
            return result;
        }

        protected void ThingFireWeapon( float x, float y, float xSpeed, float ySpeed )
        {
            Map.DamageDoodads( 5, DamageType.Blade, x, y, xSpeed, ySpeed, 14f, base.playerNum, out _, null );
            if ( Physics.Raycast( new Vector3( base.X - 6f * base.transform.localScale.x, base.Y + this.waistHeight, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, 33f, this.fragileLayer ) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null )
            {
                this.raycastHit.collider.gameObject.SendMessage( "Open", (int)base.transform.localScale.x );
                MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, 1, DamageType.Crush, base.transform.localScale.x * 500f, 50f, base.X, base.Y );
            }
            if ( HitUnits( this, playerNum, 9, DamageType.Blade, 20f, 24f, x + base.transform.localScale.x * 5f, y, xSpeed, ySpeed, true, true, true, false, alreadyHitUnits ) )
            {
            }

            if ( !this.hasHitTerrain )
            {
                this.hasHitTerrain = this.ThingHitTerrain( 33f, 4f, 0, 14f, 5, false );
            }
        }

        protected override void ClearFireInput()
        {
            if ( this.theThing && this.fire )
            {
                this.releasedFire = true;
            }
            base.ClearFireInput();
        }

        public override void ClearAllInput()
        {
            if ( this.theThing && this.fire )
            {
                this.releasedFire = true;
            }
            base.ClearAllInput();
        }

        protected void ThingRunGun()
        {
            this.firePressed += this.t;
            if ( this.hasBeenCoverInAcid || this.doingMelee || this.usingSpecial || base.actionState == ActionState.Recalling )
            {
                // Do nothing
            }
            else if ( this.currentState == ThingState.EnteringMonsterForm )
            {
                this.gunCounter += this.t;
                if ( this.gunCounter > 0.08f )
                {
                    this.gunCounter -= 0.08f;
                    ++this.gunFrame;

                    AnimateBecomingMonster();
                }
                if ( this.currentState == ThingState.MonsterForm )
                {
                    this.gunFrame = 0;
                }
            }
            // Going from monster back to human
            else if ( this.currentState == ThingState.Reforming )
            {
                AnimateBecomingHuman();
            }
            // Human form
            else if ( this.currentState == ThingState.HumanForm )
            {
                this.gunFrame = 0;
                this.SetGunSprite( 0, 0 );
            }
            // Attacking
            else if ( this.fire || ( this.releasedFire && !this.firedOnce ) )
            {
                // Animate thing attack
                this.gunCounter += this.t;
                if ( this.gunCounter > 0.065f )
                {
                    this.gunCounter -= 0.065f;
                    ++this.gunFrame;
                    if ( this.gunFrame < 2 )
                    {
                        this.gunFrame = 2;
                    }
                    if ( this.gunFrame > 11 )
                    {
                        this.gunFrame = 5;
                    }

                    if ( this.gunFrame == 5 )
                    {
                        this.alreadyHitUnits = new List<Unit>();
                        this.hasHitTerrain = false;
                        this.playedHitSound = false;
                        this.playedMissSound = false;
                        Sound.GetInstance().PlaySoundEffectAt( this.whipStartSounds, 0.2f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                    }

                    if ( this.gunFrame > 8 && this.gunFrame < 11 )
                    {
                        this.ThingUseFire();
                    }

                    // Play whip sound
                    if ( this.gunFrame >= 9 && this.gunFrame < 11 )
                    {
                        if ( !this.playedHitSound && this.alreadyHitUnits.Count > 0 )
                        {
                            this.playedHitSound = true;
                            Sound.GetInstance().PlaySoundEffectAt( this.tentacleHitSounds, 0.7f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                        }
                        else if ( !this.playedHitSound && this.hasHitTerrain )
                        {
                            this.playedHitSound = true;
                            Sound.GetInstance().PlaySoundEffectAt( this.tentacleHitTerrainSounds, 0.4f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                        }
                        else if ( !this.playedMissSound && !this.playedHitSound )
                        {
                            this.playedMissSound = true;
                            Sound.GetInstance().PlaySoundEffectAt( this.whipMissSounds, 0.2f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                        }
                    }
                    // If we're on the last frame of the firing animation and the fire button hasn't been pressed since the last loop,
                    // then allow firing to stop if the button is no longer being held
                    else if ( this.gunFrame == 11 && this.firePressed > 0.3f )
                    {
                        this.firedOnce = true;
                    }

                    this.gunSprite.SetLowerLeftPixel( this.gunFrame * 64f, 192f );
                }
            }
            else if ( this.releasedFire )
            {
                if ( this.gunFrame > 4 )
                {
                    this.gunFrame = 4;
                }
                this.gunCounter += this.t;
                if ( this.gunCounter > 0.07f )
                {
                    this.gunCounter -= 0.07f;
                    --this.gunFrame;
                }
                if ( this.gunFrame > 1 )
                {
                    this.gunSprite.SetLowerLeftPixel( this.gunFrame * 64f, 192f );
                }
                else
                {
                    this.releasedFire = false;
                    EnterMonsterFormIdle();
                }
            }
            // Monster form
            else if ( this.currentState == ThingState.MonsterForm )
            {
                this.gunFrame = 0;
                this.DeactivateGun();
            }
        }

        protected override void SetGunPosition( float xOffset, float yOffset )
        {
            if ( this.theThing )
            {
                base.SetGunPosition( 16f + xOffset, 16f + yOffset );
            }
            else
            {
                base.SetGunPosition( xOffset, yOffset );
            }
        }
        #endregion

        #region ThingSpecial
        protected override void StartPockettedSpecial()
        {
            // Don't use special if in thing form
            if ( !( this.theThing && this.currentState != ThingState.HumanForm ) )
            {
                base.StartPockettedSpecial();
            }
            else
            {
                // Hasten turning back into human form so we can use the pocketted special
                this.MonsterFormCounter = Mathf.Min( MonsterFormCounter, MaxMonsterFormTimeReduced );
            }
        }

        protected void ThingPressSpecial()
        {
            if ( !this.usingSpecial && this.SpecialAmmo > 0 && !this.hasBeenCoverInAcid && !this.doingMelee && !this.acceptedDeath && !this.IsInseminated() && !this.HasFaceHugger() )
            {
                SetGestureAnimation( GestureElement.Gestures.None );
                this.usingSpecial = true;
                this.actuallyUsingSpecial = true;
                this.canDuck = false;
                this.ducking = false;
                this.pressSpecialFacingDirection = (int)base.transform.localScale.x;

                this.currentTentacleState = TentacleState.Inactive;
                this.tentacleHitUnit = false;

                this.EnterMonsterForm();
                this.gunFrame = -1;
                this.speed = AttackingMonsterFormSpeed;
                --this.SpecialAmmo;
                this.GetComponent<InvulnerabilityFlash>().enabled = false;
                this.invulnerable = true;
            }
            else if ( this.SpecialAmmo <= 0 )
            {
                HeroController.FlashSpecialAmmo( base.playerNum );
            }
        }

        protected void ThingUseSpecial()
        {

        }

        protected void ThingAnimateSpecial()
        {
            ++this.gunFrame;
            // Make feet animated
            this.usingSpecial = false;
            this.ChangeFrame();
            this.usingSpecial = true;
            this.ActivateGun();
            if ( this.currentState == ThingState.EnteringMonsterForm )
            {
                base.frameRate = 0.08f;
                this.AnimateBecomingMonster();
                if ( this.currentState == ThingState.MonsterForm )
                {
                    this.gunFrame = 0;
                }
            }
            else
            {
                this.gunSprite.SetLowerLeftPixel( this.gunFrame * 64f, 256f );

                base.frameRate = 0.08f;

                if ( this.gunFrame == 5 )
                {
                    Sound.GetInstance().PlaySoundEffectAt( this.whipStartSounds, 0.2f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                }
                else if ( this.gunFrame == 7 && this.currentTentacleState == TentacleState.Inactive )
                {
                    StartTentacleWhip();
                }
                else if ( this.currentTentacleState > TentacleState.Inactive && this.currentTentacleState < TentacleState.ReadyToEat && this.gunFrame > 8 )
                {
                    this.gunFrame = 8;
                }
                else if ( gunFrame == 10 )
                {
                    this.sound.PlaySoundEffectAt( this.biteSound, 0.25f, base.transform.position, 1f, true, false, false, 0f );
                }
                // Eat mook
                else if ( this.gunFrame == 12 )
                {
                    this.EatMook();
                }
                else if ( this.gunFrame > 15 )
                {
                    this.usingSpecial = this.usingPockettedSpecial = false;
                    this.actuallyUsingSpecial = false;
                    this.canDuck = true;
                    this.invulnerable = false;
                    this.GetComponent<InvulnerabilityFlash>().enabled = true;
                    this.EnterMonsterFormIdle();
                }

                if ( this.currentTentacleState == TentacleState.ReadyToEat )
                {
                    base.frameRate = 0.07f;
                }
            }
        }

        protected void EatMook()
        {
            // Eat Mook
            this.ThingMeleeAttack( true, true );
            this.tentacleLine.enabled = false;
            this.currentTentacleState = TentacleState.Inactive;
            // Release unit if still alive
            if ( this.unitHit.health > 0 )
            {
                this.unitHit.Unimpale( 3, DamageType.Blade, 0f, 0f, this );
                if ( unitHit is Mook )
                {
                    unitHit.useImpaledFrames = false;
                }
            }
            // Blood explosion if enemy was killed
            else
            {
                float range = 25f;
                float blastForce = 15f;
                EffectsController.CreateExplosionRangePop( base.X + base.transform.localScale.x * 3f, base.Y + 3f, -1f, range * 2 );
                Map.ExplodeUnits( this, 13, DamageType.Explosion, range, range, base.X + base.transform.localScale.x * 3f, base.Y + 3f, blastForce * 40f, blastForce * 15f, base.playerNum, false, false, true );
                EffectsController.CreateSlimeExplosion( base.X + base.transform.localScale.x * 3f, base.Y + 3f, 15f, 15f, 140f, 0f, 0f, 0f, 0f, 0, 20, 120f, 0f, Vector3.up, BloodColor.Red );
                Map.DisturbWildLife( base.X, base.Y, 80f, base.playerNum );
                Map.DamageDoodads( 20, DamageType.Explosion, base.X, base.Y, 0f, 0f, range, base.playerNum, out _, null );
            }
        }

        protected void StartTentacleWhip()
        {
            this.currentTentacleState = TentacleState.Extending;
            this.tentacleExtendTime = 0.02f;
            this.tentacleRetractTimer = 0f;
            this.tentacleDirection = new Vector3( base.transform.localScale.x, 0f, 0f );
            this.tentacleDirection.Normalize();
            this.tentacleWhipSprite.gameObject.SetActive( true );
            impaled = false;
            tentacleHitGround = false;
            tentacleHitUnit = false;
        }

        protected void UpdateTentacle()
        {
            DrawTentacle();
        }

        public Unit HitClosestUnit( MonoBehaviour damageSender, int playerNum, float xRange, float yRange, float x, float y, int direction, Vector3 startPoint, bool haveHitGround, Vector3 groundVector )
        {
            if ( Map.units == null )
            {
                return null;
            }
            int num = 999999;
            float num2 = Mathf.Max( xRange, yRange );
            Unit unit = null;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                Unit unit3 = Map.units[i];
                if ( unit3 != null && !unit3.invulnerable && unit3.health <= num && GameModeController.DoesPlayerNumDamage( playerNum, unit3.playerNum ) )
                {
                    float f = unit3.X - x;
                    if ( Mathf.Abs( f ) - xRange < unit3.width && Mathf.Sign( f ) == direction )
                    {
                        float f2 = unit3.Y + unit3.height / 2f + 3f - y;
                        if ( Mathf.Abs( f2 ) - yRange < unit3.height )
                        {
                            float num4 = Mathf.Abs( f ) + Mathf.Abs( f2 );
                            if ( num4 < num2 )
                            {
                                if ( unit3.health > 0 )
                                {
                                    unit = unit3;
                                    num2 = num4;
                                }
                            }
                        }
                    }
                }
            }

            if ( unit != null && ( !haveHitGround || Mathf.Abs( unit.X - x ) < Mathf.Abs( groundVector.x - x ) ) )
            {
                return unit;
            }
            return null;
        }

        public bool HitProjectiles( int playerNum, int damage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, int direction )
        {
            bool result = false;
            for ( int i = Map.damageableProjectiles.Count - 1; i >= 0; i-- )
            {
                Projectile projectile = Map.damageableProjectiles[i];
                if ( projectile != null && GameModeController.DoesPlayerNumDamage( playerNum, projectile.playerNum ) )
                {
                    float f = projectile.X - x;
                    if ( Mathf.Abs( f ) - xRange < projectile.projectileSize && Mathf.Sign( f ) == direction )
                    {
                        float f2 = projectile.Y - y;
                        if ( Mathf.Abs( f2 ) - yRange < projectile.projectileSize )
                        {
                            Map.DamageProjectile( projectile, damage, damageType, xI, yI, 0f, playerNum );
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        protected void TentacleLineHitDetection( float tentacleRange, Vector3 startPoint )
        {
            RaycastHit groundHit = this.raycastHit;
            bool haveHitGround = false;
            float currentRange = tentacleRange;
            Vector3 endPoint;

            // Hit ground
            if ( Physics.Raycast( startPoint, ( base.transform.localScale.x > 0 ? Vector3.right : Vector3.left ), out raycastHit, currentRange, this.fragileGroundLayer ) )
            {
                // If hitting door, ignore hit and just destroy door instead
                if ( this.raycastHit.collider.gameObject.GetComponent<DoorDoodad>() != null )
                {
                    this.raycastHit.collider.gameObject.SendMessage( "Open", (int)base.transform.localScale.x );
                    DamageCollider( this.raycastHit );
                    currentRange = this.raycastHit.distance;
                }
                else
                {
                    groundHit = this.raycastHit;
                    // Shorten the range we check for raycast hits, we don't care about hitting anything past the current terrain.
                    currentRange = this.raycastHit.distance;
                    haveHitGround = true;
                }
            }

            Unit unit;
            // Check for unit collision
            if ( ( unit = HitClosestUnit( this, this.playerNum, currentRange, 6, startPoint.x, startPoint.y, (int)base.transform.localScale.x, startPoint, haveHitGround, groundHit.point ) ) != null )
            {
                tentacleHitUnit = true;
                unitHit = unit;
                endPoint = new Vector3( unit.X, startPoint.y, 0 );
            }
            // Use ground collsion if no unit was hit
            else if ( haveHitGround )
            {
                tentacleHitGround = true;
                this.groundHit = groundHit;
                endPoint = new Vector3( groundHit.point.x, groundHit.point.y, 0 );
            }
            // Nothing hit
            else
            {
                endPoint = new Vector3( startPoint.x + base.transform.localScale.x * tentacleRange, startPoint.y, 0 );
            }

            HitProjectiles( this.playerNum, tentacleDamage, DamageType.Knifed, Mathf.Abs( endPoint.x - startPoint.x ), 6f, startPoint.x, startPoint.y, base.transform.localScale.x * 30, 20, (int)base.transform.localScale.x );
        }

        protected void DamageCollider( RaycastHit hit )
        {
            // Only damage visible objects
            if ( SortOfFollow.IsItSortOfVisible( hit.point, 24, 24f ) )
            {
                Unit unit = hit.collider.GetComponent<Unit>();
                // Damage unit
                if ( unit != null )
                {
                    // Add damage if we're hitting a boss
                    unit.Damage( tentacleDamage + ( unit.health > 30 ? 5 : 0 ), DamageType.Blade, base.transform.localScale.x, 0, (int)base.transform.localScale.x, this, hit.point.x, hit.point.y );
                    unit.Knock( DamageType.Blade, base.transform.localScale.x * 100, 200, false );
                }
                // Damage other
                else
                {
                    hit.collider.SendMessage( "Damage", new DamageObject( tentacleDamage, DamageType.Blade, 0f, 0f, hit.point.x, hit.point.y, this ) );
                }
            }

            Puff puff = EffectsController.CreateEffect( EffectsController.instance.whiteFlashPopSmallPrefab, hit.point.x + base.transform.localScale.x * 4, hit.point.y + UnityEngine.Random.Range( -3, 3 ), 0f, 0f, Vector3.zero, null );
        }

        protected void DrawTentacle()
        {
            if ( this.currentTentacleState == TentacleState.Extending )
            {
                float max = this.tentacleRange;
                Vector2 tentaclePosition = new Vector2( 128f - Mathf.Clamp( this.tentacleExtendTime * 1280f, 0f, max ), 16f );
                float currentRange = 128f - tentaclePosition.x;
                if ( !tentacleHitUnit && !tentacleHitGround )
                {
                    TentacleLineHitDetection( currentRange, base.transform.position + this.tentacleOffset );
                }

                if ( tentacleHitUnit )
                {
                    this.tentacleHitPoint = new Vector3( unitHit.transform.position.x, base.transform.position.y + this.tentacleOffset.y, 0 );
                }
                else if ( tentacleHitGround )
                {
                    this.tentacleHitPoint = new Vector3( groundHit.point.x, base.transform.position.y + this.tentacleOffset.y, 0 );
                }
                else
                {
                    this.tentacleHitPoint = base.transform.position + this.tentacleOffset + this.tentacleDirection.normalized * this.tentacleRange;
                }
                float num = this.tentacleHitPoint.x - ( base.transform.position.x + this.tentacleOffset.x );
                if ( num > 0f != base.transform.localScale.x > 0f )
                {
                    num *= -1f;
                }
                num *= Mathf.Sign( this.tentacleHitPoint.x - ( base.transform.position.x + this.tentacleOffset.x ) );
                this.tentacleWhipSprite.transform.localEulerAngles = new Vector3( 0f, 0f, 0f );
                this.tentacleWhipSprite.gameObject.SetActive( true );
                this.tentacleExtendTime += this.t / 2f;
                this.tentacleWhipSprite.SetLowerLeftPixel( tentaclePosition );
                this.tentacleWhipSprite.UpdateUVs();
                if ( tentaclePosition.x <= 0 || this.tentacleHitUnit || this.tentacleHitGround )
                {
                    Sound.GetInstance().PlaySoundEffectAt( this.whipStartSound, 0.4f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                    if ( this.tentacleHitUnit || this.tentacleHitGround )
                    {
                        Sound.GetInstance().PlaySoundEffectAt( this.whipHitSounds, 0.4f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                        Sound.GetInstance().PlaySoundEffectAt( this.whipHitSounds2, 0.3f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                    }
                    else
                    {
                        Sound.GetInstance().PlaySoundEffectAt( this.whipMissSounds, 0.5f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                    }

                    this.currentTentacleState = TentacleState.Attached;
                    this.tentacleWhipSprite.gameObject.SetActive( false );
                    this.tentacleLine.enabled = true;
                    this.tentacleLine.startWidth = 3f;
                    this.tentacleLine.endWidth = 3f;
                    this.tentacleLine.widthMultiplier = 1f;
                    this.tentacleLine.textureMode = LineTextureMode.Stretch;
                    this.tentacleLine.SetPosition( 0, base.transform.position + this.tentacleOffset );
                    this.tentacleLine.SetPosition( 1, this.tentacleHitPoint );
                    float magnitude = ( this.tentacleHitPoint - ( base.transform.position + this.tentacleOffset ) ).magnitude;
                    if ( base.transform.localScale.x < 0f )
                    {
                        this.tentacleLine.material.SetTextureScale( "_MainTex", new Vector2( magnitude * tentacleMaterialScale, 1f ) );
                        this.tentacleLine.material.SetTextureOffset( "_MainTex", new Vector2( magnitude * tentacleMaterialOffset, 0f ) );
                    }
                    else if ( base.transform.localScale.x > 0f )
                    {
                        this.tentacleLine.material.SetTextureScale( "_MainTex", new Vector2( magnitude * tentacleMaterialScale, -1f ) );
                        this.tentacleLine.material.SetTextureOffset( "_MainTex", new Vector2( magnitude * tentacleMaterialOffset, 0f ) );
                    }
                }
            }
            else if ( this.currentTentacleState == TentacleState.Attached )
            {
                this.tentacleRetractTimer += this.t;
                // Impale unit if possible or hit unit / ground if not
                if ( !this.impaled )
                {
                    if ( this.tentacleHitUnit )
                    {
                        // If unit is heavy, hit enemy but don't impale
                        if ( unitHit.IsHeavy() )
                        {
                            unitHit.Damage( 25, DamageType.Blade, base.transform.localScale.x * 500f, 200f, (int)base.transform.localScale.x, this, this.tentacleHitPoint.x, this.tentacleHitPoint.y );
                        }
                        else
                        {
                            // Play impale sound
                            Sound.GetInstance().PlaySoundEffectAt( this.tentacleImpaleSounds, 0.5f, base.transform.position, 1f + this.pitchShiftAmount, true, false, false, 0f );
                            this.tentacleImpaler.position = tentacleHitPoint;
                            this.unitHit.Impale( tentacleImpaler, new Vector3( base.transform.localScale.x, 0f, 0f ), 0, base.transform.localScale.x, 0, 0, 0 );
                            this.unitHit.Y = this.tentacleHitPoint.y - 8f;
                            if ( unitHit is Mook )
                            {
                                unitHit.useImpaledFrames = true;
                                Traverse.Create( ( unitHit as Mook ) ).Field( "impaledPosition" ).SetValue( new Vector3( base.transform.localScale.x < 0 ? float.MaxValue : float.MinValue, 0, 0 ) );
                            }
                        }
                    }
                    else if ( this.tentacleHitGround )
                    {
                        DamageCollider( groundHit );
                    }
                    this.impaled = true;
                }
                this.tentacleLine.SetPosition( 0, base.transform.position + this.tentacleOffset );
                this.tentacleLine.SetPosition( 1, this.tentacleHitPoint );
                this.tentacleLine.startWidth = 3f;
                this.tentacleLine.endWidth = 3f;
                this.tentacleLine.widthMultiplier = 1f;
                if ( this.tentacleRetractTimer > 0.25f )
                {
                    this.currentTentacleState = TentacleState.Retracting;
                }
            }
            else if ( this.currentTentacleState == TentacleState.Retracting || this.currentTentacleState == TentacleState.ReadyToEat )
            {
                // Move tentacleHitPoint towards player
                this.tentacleHitPoint = Vector3.MoveTowards( this.tentacleHitPoint, base.transform.position + this.tentacleOffset, ( tentacleHitUnit ? 200f : 400f ) * this.t );
                // Update tentacleImpaler position
                this.tentacleImpaler.position = tentacleHitPoint;

                this.tentacleLine.SetPosition( 0, base.transform.position + this.tentacleOffset );
                this.tentacleLine.SetPosition( 1, this.tentacleHitPoint );
                this.tentacleLine.startWidth = 3f;
                this.tentacleLine.endWidth = 3f;
                this.tentacleLine.widthMultiplier = 1f;

                // If tentacleHitPoint is close to player, move to ready to eat state
                if ( this.currentTentacleState != TentacleState.ReadyToEat && Tools.FastAbsWithinRange( tentacleHitPoint.x - base.transform.position.x, 45f ) )
                {
                    this.currentTentacleState = TentacleState.ReadyToEat;
                }
            }
        }
        #endregion

        #region ThingMelee
        protected override void DeactivateGun()
        {
            if ( !( this.theThing && ( this.doingMelee || this.actuallyUsingSpecial || this.fire || this.releasedFire || this.currentState == ThingState.EnteringMonsterForm || this.currentState == ThingState.Reforming ) ) )
            {
                base.DeactivateGun();
            }
        }

        protected void ThingStartMelee()
        {
            SetGestureAnimation( GestureElement.Gestures.None );
            EnterMonsterForm();
            this.gunFrame = 0;
            this.ActivateGun();
        }

        protected void ThingMeleeAttack( bool shouldTryHitTerrain, bool playMissSound )
        {
            bool flag;
            Map.DamageDoodads( 3, DamageType.Blade, base.X + (float)( base.Direction * 4 ), base.Y, 0f, 0f, 14f, base.playerNum, out flag, null );
            if ( Physics.Raycast( new Vector3( base.X - 6f * base.transform.localScale.x, base.Y + this.waistHeight, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, 20f, this.fragileLayer ) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null )
            {
                this.raycastHit.collider.gameObject.SendMessage( "Open", (int)base.transform.localScale.x );
                MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, 1, DamageType.Crush, base.transform.localScale.x * 500f, 50f, base.X, base.Y );
            }
            List<BroforceObject> temp = new List<BroforceObject>();

            if ( Map.HitUnits( this, base.playerNum, 1, 1, DamageType.Blade, 12f, 24f, base.X + transform.localScale.x * 8f, base.Y + 8f, transform.localScale.x * 100f, 100f, true, true, true, temp ) )
            {
                temp.Clear();
                Map.HitUnits( this, base.playerNum, 24, 25, DamageType.GibIfDead, 12f, 24f, base.X + transform.localScale.x * 8f, base.Y + 8f, transform.localScale.x * 100f, 100f, true, true, true, temp );
                this.meleeHasHit = true;
            }
            else if ( playMissSound )
            {
            }
            this.meleeChosenUnit = null;
            if ( shouldTryHitTerrain && this.ThingHitTerrain( 20, 4, 0, 12f, 15, true ) )
            {
                this.meleeHasHit = true;
            }
            this.TriggerBroMeleeEvent();
            SortOfFollow.Shake( 0.5f );
        }

        protected bool ThingHitTerrain( float xdistance, float ydistance, int offset, float yoffset, int meleeDamage, bool playSound )
        {
            // Check straight
            bool hit = Physics.Raycast( new Vector3( base.X - base.transform.localScale.x * 4f, base.Y + yoffset, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, (float)( xdistance + offset ), this.groundLayer );

            if ( ydistance > 0 )
            {
                // Check down
                if ( !hit )
                {
                    hit = Physics.Raycast( new Vector3( base.X - base.transform.localScale.x * 4f, base.Y + yoffset - ydistance, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, (float)( xdistance + offset ), this.groundLayer );
                }

                // Check up
                if ( !hit )
                {
                    hit = Physics.Raycast( new Vector3( base.X - base.transform.localScale.x * 4f, base.Y + yoffset + ydistance, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, (float)( xdistance + offset ), this.groundLayer );
                }
            }

            if ( !hit )
            {
                return false;
            }

            Cage component = this.raycastHit.collider.GetComponent<Cage>();
            if ( component == null && this.raycastHit.collider.transform.parent != null )
            {
                component = this.raycastHit.collider.transform.parent.GetComponent<Cage>();
            }
            if ( component != null )
            {
                MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, component.health, DamageType.Melee, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y );
                return true;
            }
            MapController.Damage_Networked( this, this.raycastHit.collider.gameObject, meleeDamage, DamageType.Melee, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y );
            // Play hitting terrain sound for the thing
            if ( playSound )
            {
                this.sound.PlaySoundEffectAt( this.soundHolder.meleeHitTerrainSound, 0.2f, base.transform.position, 1f, true, false, false, 0f );
            }
            EffectsController.CreateProjectilePopWhiteEffect( this.raycastHit.point.x, this.raycastHit.point.y );
            return true;
        }

        protected void ThingAnimateMelee()
        {
            if ( this.currentState == ThingState.EnteringMonsterForm )
            {
                base.frameRate = 0.06f;
                this.AnimateBecomingMonster();
                if ( this.currentState == ThingState.MonsterForm )
                {
                    this.gunFrame = 0;
                }
            }
            else
            {
                if ( this.gunFrame < 2 )
                {
                    this.gunFrame = 2;
                }
                base.frameRate = 0.07f;
                this.sprite.SetLowerLeftPixel( 0f, 32f );
                this.gunSprite.SetLowerLeftPixel( this.gunFrame * 64f, 320f );
                if ( this.gunFrame == 3 && !this.playedAxeSound )
                {
                    this.sound.PlaySoundEffectAt( this.biteSound, 0.25f, base.transform.position, 1f, true, false, false, 0f );
                    this.playedAxeSound = true;
                }
                else if ( this.gunFrame == 6 )
                {
                    this.ThingMeleeAttack( true, true );
                }
                else if ( this.gunFrame > 6 && this.gunFrame <= 7 && !this.meleeHasHit )
                {
                    this.ThingMeleeAttack( false, false );
                }
                if ( this.gunFrame > 9 )
                {
                    this.gunFrame = 0;
                    this.CancelMelee();
                    if ( this.meleeBufferedPress )
                    {
                        this.meleeBufferedPress = false;
                        this.StartCustomMelee();
                    }
                }
            }
            ++this.gunFrame;
        }

        protected void ThingRunMeleeMovement()
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
                // Stand still during transformation
                if ( this.currentState == ThingState.EnteringMonsterForm )
                {
                    this.xI = 0f;
                    this.yI = 0f;
                }
                else if ( this.gunFrame <= 1 )
                {
                    this.xI = this.speed * 0.1f * base.transform.localScale.x;
                    this.yI = 0f;
                }
                else if ( this.gunFrame <= 5 )
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
                        this.xI = this.speed * 0.6f * base.transform.localScale.x + ( this.meleeChosenUnit.X - base.X ) * 2f;
                    }
                }
                else if ( this.gunFrame <= 9 )
                {
                    if ( !this.isInQuicksand )
                    {
                        this.xI = this.speed * 0.2f * base.transform.localScale.x;
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

        protected void ThingCancelMelee()
        {
            if ( this.doingMelee )
            {
                this.jumpTime = -1f;
                this.gunSprite.meshRender.material = this.thingGunMaterial;
                this.gunSprite.RecalcTexture();
                EnterMonsterFormIdle();
            }
        }
        #endregion
    }
}
