using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using System.IO;
using System.Reflection;
using BroMakerLib.CustomObjects.Projectiles;

namespace Captain_Ameribro_Mod
{
    [HeroPreset( "Captain Ameribro", HeroType.Nebro )]
    class CaptainAmeribro : SwordHero
    {
        // Sprite variables
        public Material materialNormal, materialNormalShield, materialNormalNoShield, materialArmless;
        public Material gunMaterialNormal, gunMaterialNoShield;
        protected bool wasInvulnerable = false;

        // Audio variables
        public AudioClip shieldChargeShing;
        public AudioClip[] shieldMeleeSwing;
        public AudioClip[] shieldMeleeHit;
        public AudioClip[] shieldMeleeTerrain;
        public AudioClip airDashSound;
        public AudioClip[] effortSounds;
        public AudioClip[] ricochetSounds;
        public AudioClip[] pistolSounds;
        public int currentMeleeSound = 0;
        public float sliceVolume = 0.7f;
        public float wallHitVolume = 0.6f;

        // Special variables
        protected Shield shield;
        protected Shield thrownShield;
        public const float shieldSpeed = 400f;
        protected bool grabbingShield;
        protected int grabbingFrame;
        protected int specialFrame = 0;
        protected float specialFrameRate = 0.0334f;
        protected float specialFrameCounter = 0f;
        protected bool animateSpecial = false;
        protected float specialAttackDashCounter;
        protected bool isHoldingSpecial = false;
        protected float maxSpecialCharge = 1f;
        public float currentSpecialCharge = 0f;
        public bool playedShingNoise = false;
        public bool finishedStartup = false;
        public bool caughtShieldFromPrevious = false;

        // Default attack variables
        protected int punchingIndex = 0;
        protected const int normalAttackDamage = 7;
        protected bool heldGunFrame = false;

        // Melee variables
        protected const int meleeAttackDamage = 8;
        public float specialAttackDashTime = 0f;
        protected float airdashFadeCounter;
        public float airdashFadeRate = 0.1f;
        protected bool usingShieldMelee = false;
        protected Projectile pistolBullet;
        protected float airDashCooldown = 0f;
        protected bool alreadySpawnedBullet = false;

        // Misc Variables
        protected List<Unit> currentlyHitting;
        protected const int defaultSpeed = 130;
        protected bool acceptedDeath = false;
        protected InvulnerabilityFlash invulnerabilityFlash;

        protected override void Awake()
        {
            shield = CustomProjectile.CreatePrefab<Shield>( new List<Type>() { typeof( SphereCollider ) } );

            this.currentMeleeType = BroBase.MeleeType.Disembowel;
            this.meleeType = BroBase.MeleeType.Disembowel;

            pistolBullet = ( HeroController.GetHeroPrefab( HeroType.DoubleBroSeven ) as DoubleBroSeven ).projectile;

            base.Awake();
        }

        public override void PreloadAssets()
        {
            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            CustomHero.PreloadSprites( directoryPath, new List<string> { "captainAmeribroMainNoShield.png", "captainAmeribroArmless.png", "captainAmeribroGunNoShield.png" } );
            CustomHero.PreloadSprites( Path.Combine( directoryPath, "projectiles" ), new List<string> { "Shield.png" } );

            directoryPath = Path.Combine( directoryPath, "sounds" );
            CustomHero.PreloadSounds( directoryPath, new List<string> { "special1.wav", "special2.wav", "special3.wav", "ShieldShing.wav", "melee1part1.wav", "melee3part2.wav", "meleeterrainhit1.wav", "meleeterrainhit2.wav", "swish.wav", "grunt1.wav", "grunt2.wav", "grunt3.wav", "grunt4.wav", "grunt5.wav", "ricochet1.wav", "ricochet2.wav", "ricochet3.wav", "ricochet4.wav", "pistol1.wav", "pistol2.wav", "pistol3.wav", "pistol4.wav" } );
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            this.SetupSounds();
        }

        protected void SetupSounds()
        {
            string soundPath = Path.Combine( Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ), "sounds" );

            if ( shieldChargeShing == null )
            {
                shieldChargeShing = ResourcesController.GetAudioClip( soundPath, "ShieldShing.wav" );
            }

            if ( shieldMeleeSwing == null )
            {
                shieldMeleeSwing = new AudioClip[2];
                shieldMeleeSwing[0] = ResourcesController.GetAudioClip( soundPath, "melee1part1.wav" );
                shieldMeleeSwing[1] = ResourcesController.GetAudioClip( soundPath, "melee3part1.wav" );
            }

            if ( shieldMeleeHit == null )
            {
                shieldMeleeHit = new AudioClip[2];
                shieldMeleeHit[0] = ResourcesController.GetAudioClip( soundPath, "melee1part2.wav" );
                shieldMeleeHit[1] = ResourcesController.GetAudioClip( soundPath, "melee3part2.wav" );
            }

            if ( shieldMeleeTerrain == null )
            {
                shieldMeleeTerrain = new AudioClip[2];
                shieldMeleeTerrain[0] = ResourcesController.GetAudioClip( soundPath, "meleeterrainhit1.wav" );
                shieldMeleeTerrain[1] = ResourcesController.GetAudioClip( soundPath, "meleeterrainhit2.wav" );
            }

            if ( airDashSound == null )
            {
                airDashSound = ResourcesController.GetAudioClip( soundPath, "swish.wav" );
            }

            if ( effortSounds == null )
            {
                effortSounds = new AudioClip[5];
                effortSounds[0] = ResourcesController.GetAudioClip( soundPath, "grunt1.wav" );
                effortSounds[1] = ResourcesController.GetAudioClip( soundPath, "grunt2.wav" );
                effortSounds[2] = ResourcesController.GetAudioClip( soundPath, "grunt3.wav" );
                effortSounds[3] = ResourcesController.GetAudioClip( soundPath, "grunt4.wav" );
                effortSounds[4] = ResourcesController.GetAudioClip( soundPath, "grunt5.wav" );
            }

            if ( ricochetSounds == null )
            {
                ricochetSounds = new AudioClip[4];
                ricochetSounds[0] = ResourcesController.GetAudioClip( soundPath, "ricochet1.wav" );
                ricochetSounds[1] = ResourcesController.GetAudioClip( soundPath, "ricochet2.wav" );
                ricochetSounds[2] = ResourcesController.GetAudioClip( soundPath, "ricochet3.wav" );
                ricochetSounds[3] = ResourcesController.GetAudioClip( soundPath, "ricochet4.wav" );
            }

            if ( pistolSounds == null )
            {
                pistolSounds = new AudioClip[4];
                pistolSounds[0] = ResourcesController.GetAudioClip( soundPath, "pistol1.wav" );
                pistolSounds[1] = ResourcesController.GetAudioClip( soundPath, "pistol2.wav" );
                pistolSounds[2] = ResourcesController.GetAudioClip( soundPath, "pistol3.wav" );
                pistolSounds[3] = ResourcesController.GetAudioClip( soundPath, "pistol4.wav" );
            }
        }

        protected override void Start()
        {
            base.Start();

            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );

            materialNormal = this.material;

            materialNormalShield = materialNormal;
            materialNormalNoShield = ResourcesController.GetMaterial( directoryPath, "captainAmeribroMainNoShield.png" );

            materialArmless = ResourcesController.GetMaterial( directoryPath, "captainAmeribroArmless.png" );

            gunMaterialNormal = this.gunMaterial;
            gunMaterialNoShield = ResourcesController.GetMaterial( directoryPath, "captainAmeribroGunNoShield.png" );

            if ( this.shieldChargeShing == null )
            {
                this.SetupSounds();
            }

            this.finishedStartup = true;

            if ( this.caughtShieldFromPrevious )
            {
                this.caughtShieldFromPrevious = false;
                ++this.SpecialAmmo;
            }
        }

        protected override void Update()
        {
            if ( this.invulnerable )
            {
                this.wasInvulnerable = true;
            }
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
                    if ( this.thrownShield != null )
                    {
                        this.thrownShield.ReturnShieldSilent();
                    }
                    this.SpecialAmmo = 1;
                    this.SwitchToWithShieldMats();
                    this.acceptedDeath = false;
                }
            }
            // Check if invulnerability ran out
            if ( this.wasInvulnerable && !this.invulnerable )
            {
                materialNormalShield.SetColor( "_TintColor", Color.gray );
                materialNormalNoShield.SetColor( "_TintColor", Color.gray );
                materialArmless.SetColor( "_TintColor", Color.gray );
                gunMaterialNormal.SetColor( "_TintColor", Color.gray );
                gunMaterialNoShield.SetColor( "_TintColor", Color.gray );
            }

            // Charge special
            if ( isHoldingSpecial )
            {
                currentSpecialCharge += this.t;
            }

            // Keep track of special frames
            this.specialCounter += this.t;
            if ( this.specialCounter > this.specialFrameRate )
            {
                if ( this.animateSpecial )
                {
                    this.AnimateSpecial();
                }
                this.specialCounter -= this.specialFrameRate;
                ++this.specialFrame;
            }

            // Make shield drop on death
            if ( base.actionState == ActionState.Dead && !this.acceptedDeath && !this.WillReviveAlready )
            {
                if ( thrownShield != null && !thrownShield.dropping )
                {
                    thrownShield.StartDropping();
                }
                this.gunFrame = 0;
                this.currentSpecialCharge = 0f;
                this.animateSpecial = this.usingSpecial = this.isHoldingSpecial = this.doingMelee = this.usingShieldMelee = false;
                this.acceptedDeath = true;
            }

            // Reflect projectiles if using melee
            if ( this.doingMelee && this.usingShieldMelee && base.frame > 0 )
            {
                if ( Map.DeflectProjectiles( this, this.playerNum, 10, this.X, this.Y, this.transform.localScale.x * 300, true ) )
                {
                    Sound.GetInstance().PlaySoundEffectAt( ricochetSounds, 0.6f, base.transform.position, 1f, true, false, false, 0f );
                }
            }

            // Reflect projectiles if airdashing
            if ( this.airdashTime > 0 && this.airdashTime < 0.27 )
            {
                if ( Map.DeflectProjectiles( this, this.playerNum, 8f, this.X, this.Y, this.transform.localScale.x * 300, true ) )
                {
                    Sound.GetInstance().PlaySoundEffectAt( ricochetSounds, 0.6f, base.transform.position, 1f, true, false, false, 0f );
                }
            }

            if ( airDashCooldown > 0 )
            {
                airDashCooldown -= this.t;
            }
        }

        protected override void ChangeFrame()
        {
            // Cancel holding special if doing any animation that won't work for the armless sprite
            if ( this.animateSpecial && ( this.chimneyFlip || this.wallClimbing || this.WallDrag || ( base.actionState == ActionState.Dead ) ||
                ( this.inseminatorUnit != null ) || ( this.impaledByTransform != null && this.useImpaledFrames ) || this.attachedToZipline != null ) )
            {
                this.CancelSpecial();
            }
            base.ChangeFrame();
            if ( this.doingMelee && !this.usingShieldMelee )
            {
                base.frameRate = 0.05f;
            }
        }

        protected override void AnimateWallAnticipation()
        {
            // Cancel holding special if we are about to grab a wall
            if ( this.animateSpecial )
            {
                this.CancelSpecial();
            }
            base.AnimateWallAnticipation();
        }

        protected override void UseSpecial()
        {
            if ( this.SpecialAmmo > 0 && !isHoldingSpecial )
            {
                if ( this.currentSpecialCharge > this.maxSpecialCharge )
                {
                    this.currentSpecialCharge = this.maxSpecialCharge;
                }

                this.SpecialAmmo--;

                SwitchToNoShieldMats();

                float chargedShieldSpeed = shieldSpeed + Shield.ChargeSpeedScalar * this.currentSpecialCharge;

                if ( Physics.Raycast( this.transform.position, Vector3.up, out this.raycastHit, 22, this.groundLayer ) )
                {
                    thrownShield = ProjectileController.SpawnProjectileLocally( this.shield, this, base.X + base.transform.localScale.x * 6f, base.Y + 10f, base.transform.localScale.x * chargedShieldSpeed, 0f, false, base.playerNum, false, false, 0f ) as Shield;
                }
                else
                {
                    thrownShield = ProjectileController.SpawnProjectileLocally( this.shield, this, base.X + base.transform.localScale.x * 6f, base.Y + 15f, base.transform.localScale.x * chargedShieldSpeed, 0f, false, base.playerNum, false, false, 0f ) as Shield;
                }

                thrownShield.Setup( this, this.currentSpecialCharge );

                this.currentSpecialCharge = 0;
            }
            else
            {
                base.UseSpecial();
            }
        }

        protected void SwitchToNoShieldMats()
        {
            if ( this._specialAmmo <= 0 )
            {
                this.materialNormal = this.materialNormalNoShield;
                this.gunSprite.meshRender.material = this.gunMaterialNoShield;
            }
        }

        protected void SwitchToWithShieldMats()
        {
            this.materialNormal = this.materialNormalShield;
            this.gunSprite.meshRender.material = this.gunMaterialNormal;

            if ( !this.animateSpecial )
            {
                base.GetComponent<Renderer>().material = this.materialNormalShield;
            }
        }

        public void ReturnShield( Shield shield )
        {
            if ( this.hasBeenCoverInAcid )
            {
                ++this.SpecialAmmo;
                return;
            }
            SwitchToWithShieldMats();
            if ( this.doingMelee )
            {
                this.doingMelee = false;
                this.jumpingMelee = false;
                this.dashingMelee = false;
                this.standingMelee = false;
                this.meleeFollowUp = false;
                this.meleeChosenUnit = null;
                this.counter = 0f;
                this.hasPlayedMissSound = false;
                if ( base.actionState != ActionState.ClimbingLadder )
                {
                    if ( base.Y > this.groundHeight )
                    {
                        base.actionState = ActionState.Jumping;
                    }
                    else if ( this.left || this.right )
                    {
                        base.actionState = ActionState.Running;
                    }
                    else
                    {
                        this.SetActionstateToIdle();
                    }
                }
            }
            if ( !( this.usingSpecial || this.animateSpecial ) )
            {
                this.usingSpecial = true;
                this.grabbingFrame = 0;
                this.grabbingShield = true;
                this.ChangeFrame();
            }
            this.SpecialAmmo++;
        }

        // Make shield drop if character is destroyed
        protected override void OnDestroy()
        {
            if ( thrownShield != null && !thrownShield.dropping )
            {
                thrownShield.StartDropping();
            }
            base.OnDestroy();
        }

        protected override void PressSpecial()
        {
            // Don't start holding special unless we actually have a shield to prevent shield from charging
            if ( this.SpecialAmmo > 0 && !( this.usingSpecial || this.animateSpecial || this.wallClimbing || this.wallDrag || this.attachedToZipline != null || this.IsGesturing() || this.frontSomersaulting ) )
            {
                if ( !this.hasBeenCoverInAcid && !this.doingMelee )
                {
                    this.speed = 80;
                    this.isHoldingSpecial = true;
                    this.specialFrame = 0;
                    this.specialFrameRate = 0.05f;
                    this.specialCounter = this.specialFrameRate;
                    this.animateSpecial = true;
                    this.playedShingNoise = false;
                    base.frame = 0;
                    this.pressSpecialFacingDirection = (int)base.transform.localScale.x;
                }
            }
        }

        protected override void ReleaseSpecial()
        {
            this.isHoldingSpecial = false;
            base.ReleaseSpecial();
        }

        protected void CancelSpecial()
        {
            this.isHoldingSpecial = false;
            this.speed = defaultSpeed;
            this.animateSpecial = false;
            this.currentSpecialCharge = 0f;
            this.gunFrame = 0;
            if ( !this.hasBeenCoverInAcid )
            {
                base.GetComponent<Renderer>().material = this.materialNormal;
            }
        }

        public override void AttachToZipline( ZipLine zipLine )
        {
            if ( this.animateSpecial )
            {
                this.CancelSpecial();
            }
            base.AttachToZipline( zipLine );
        }

        public override void SetGestureAnimation( GestureElement.Gestures gesture )
        {
            if ( this.animateSpecial && gesture != GestureElement.Gestures.None )
            {
                this.CancelSpecial();
            }
            base.SetGestureAnimation( gesture );
        }

        protected override void AnimateSpecial()
        {
            if ( this.grabbingShield )
            {
                this.SetSpriteOffset( 0f, 0f );
                this.DeactivateGun();
                this.frameRate = 0.05f;
                this.sprite.SetLowerLeftPixel( (float)( ( 26 + this.grabbingFrame ) * this.spritePixelWidth ), (float)( this.spritePixelHeight * 5 ) );
                ++this.grabbingFrame;
                if ( this.grabbingFrame > 3 )
                {
                    base.frame = 0;
                    this.usingSpecial = false;
                    this.grabbingShield = false;
                    this.ChangeFrame();
                }
            }
            else
            {
                if ( !this.hasBeenCoverInAcid )
                {
                    base.GetComponent<Renderer>().material = this.materialArmless;
                }
                if ( isHoldingSpecial && this.SpecialAmmo > 0 )
                {
                    if ( this.specialFrame > 2 && this.currentSpecialCharge < 0.25f )
                    {
                        this.specialFrameRate = 0.05f;
                        this.specialFrame = 2;
                    }
                    else if ( this.specialFrame > 5 && this.currentSpecialCharge < 1f )
                    {
                        this.specialFrame = 3;
                    }
                    else if ( this.specialFrame == 5 && this.currentSpecialCharge > 1f )
                    {
                        this.specialFrame = 6;
                    }
                    else if ( this.specialFrame > 5 )
                    {
                        this.specialFrameRate = 0.0333f;
                        this.specialFrame = 3;
                    }

                    if ( !this.playedShingNoise && this.currentSpecialCharge > 1f )
                    {
                        Sound.GetInstance().PlaySoundEffectAt( this.shieldChargeShing, 0.3f, base.transform.position, 1f, true, false, false, 0f );
                        this.playedShingNoise = true;
                    }

                    this.gunSprite.SetLowerLeftPixel( (float)( 32 * ( 1 + this.specialFrame ) ), 128f );
                }
                else
                {
                    if ( this.specialFrame > 2 && this.specialFrame < 7 )
                    {
                        this.specialFrame = 7;
                    }
                    else if ( this.specialFrame == 9 )
                    {
                        this.UseSpecial();
                        this.speed = defaultSpeed;
                    }

                    if ( this.specialFrame >= 11 )
                    {
                        base.frame = 0;
                        this.animateSpecial = ( this.usingPockettedSpecial = false );
                        if ( !this.hasBeenCoverInAcid )
                        {
                            base.GetComponent<Renderer>().material = this.materialNormal;
                        }
                        this.ChangeFrame();
                    }
                    else
                    {
                        this.specialFrameRate = 0.05f;
                        this.gunSprite.SetLowerLeftPixel( (float)( 32 * ( 1 + this.specialFrame ) ), 128f );
                    }
                }
            }
        }

        // Copie from Neo
        protected override void AnimateInseminationFrames()
        {
            int num = 24 + base.CalculateInseminationFrame();
            this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( this.spritePixelHeight * 8 ) );
        }

        // Copied from Neo
        protected override void SetGunSprite( int spriteFrame, int spriteRow )
        {
            if ( !this.animateSpecial )
            {
                if ( base.actionState == ActionState.ClimbingLadder && this.hangingOneArmed )
                {
                    this.gunSprite.SetLowerLeftPixel( (float)( this.gunSpritePixelWidth * ( 11 + spriteFrame ) ), (float)( this.gunSpritePixelHeight * ( 1 + spriteRow ) ) );
                }
                else if ( this.attachedToZipline != null && base.actionState == ActionState.Jumping )
                {
                    this.gunSprite.SetLowerLeftPixel( (float)( this.gunSpritePixelWidth * 11 ), (float)( this.gunSpritePixelHeight * 2 ) );
                }
                else
                {
                    base.SetGunSprite( spriteFrame, spriteRow );
                }
            }
        }

        public override void PlaySliceSound()
        {
            if ( this.sound == null )
            {
                this.sound = Sound.GetInstance();
            }
            if ( this.sound != null )
            {
                this.sound.PlaySoundEffectAt( this.soundHolder.special2Sounds, this.sliceVolume, base.transform.position, 1f, true, false, false, 0f );
            }
        }

        public override void PlayWallSound()
        {
            if ( this.sound == null )
            {
                this.sound = Sound.GetInstance();
            }
            if ( this.sound != null )
            {
                this.sound.PlaySoundEffectAt( this.soundHolder.defendSounds, this.wallHitVolume, base.transform.position, 1f, true, false, false, 0f );
            }
        }

        protected override void UseFire()
        {
            if ( !this.animateSpecial && this.airdashTime <= 0 )
            {
                base.UseFire();
                this.fireDelay = 0.25f;
            }
        }

        protected override void FireWeapon( float x, float y, float xSpeed, float ySpeed )
        {
            if ( this.attachedToZipline != null )
            {
                if ( base.transform.localScale.x > 0f )
                {
                    this.AirDashRight();
                }
                else
                {
                    this.AirDashLeft();
                }
                return;
            }
            Map.HurtWildLife( x + base.transform.localScale.x * 13f, y + 5f, 12f );
            this.gunFrame = 1;
            this.punchingIndex++;
            this.gunCounter = 0f;
            this.SetGunFrame();
            currentlyHitting = new List<Unit>();
            float num = base.transform.localScale.x * 12f;
            this.ConstrainToFragileBarriers( ref num, 16f );
            if ( Physics.Raycast( new Vector3( x - Mathf.Sign( base.transform.localScale.x ) * 12f, y + 5.5f, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, 18f, this.groundLayer | 1 << LayerMask.NameToLayer( "FLUI" ) ) || Physics.Raycast( new Vector3( x - Mathf.Sign( base.transform.localScale.x ) * 12f, y + 10.5f, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, 19f, this.groundLayer | 1 << LayerMask.NameToLayer( "FLUI" ) ) )
            {
                this.MakeEffects( this.raycastHit.point.x + base.transform.localScale.x * 4f, this.raycastHit.point.y );
                MapController.Damage_Local( this, this.raycastHit.collider.gameObject, normalAttackDamage, DamageType.Bullet, this.xI + base.transform.localScale.x * 200f, 0f, x, y );
                this.hasHitWithWall = true;
                if ( Map.HitUnits( this, base.playerNum, normalAttackDamage, DamageType.Melee, 12f, x, y, base.transform.localScale.x * 250, 100f, false, true, false, this.alreadyHit, false, false ) )
                {
                    this.hasHitWithSlice = true;
                }
                else
                {
                    this.hasHitWithSlice = false;
                }
                Map.DisturbWildLife( x, y, 80f, base.playerNum );
            }
            else
            {
                this.hasHitWithWall = false;
                if ( Map.HitUnits( this, base.playerNum, normalAttackDamage, DamageType.Melee, 10, x + base.transform.localScale.x * 0, y, base.transform.localScale.x * 250, 100f, false, true, false, this.alreadyHit, false, false ) )
                {
                    this.hasHitWithSlice = true;
                }
                else
                {
                    this.hasHitWithSlice = false;
                }
            }
        }

        protected void NormalAttackDamage( float x, float y, float xSpeed, float ySpeed )
        {
            float num = base.transform.localScale.x * 12f;
            this.ConstrainToFragileBarriers( ref num, 16f );
            if ( Physics.Raycast( new Vector3( x - Mathf.Sign( base.transform.localScale.x ) * 12f, y + 5.5f, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, 18f, this.groundLayer | 1 << LayerMask.NameToLayer( "FLUI" ) ) || Physics.Raycast( new Vector3( x - Mathf.Sign( base.transform.localScale.x ) * 12f, y + 10.5f, 0f ), new Vector3( base.transform.localScale.x, 0f, 0f ), out this.raycastHit, 19f, this.groundLayer | 1 << LayerMask.NameToLayer( "FLUI" ) ) )
            {
            }
            else
            {
                this.hasHitWithWall = false;
                if ( Map.HitUnits( this, base.playerNum, normalAttackDamage, DamageType.Melee, 10, x + base.transform.localScale.x * 0, y, base.transform.localScale.x * 250, 100f, false, true, false, this.alreadyHit, false, false ) )
                {
                    this.hasHitWithSlice = true;
                }
                else
                {
                    this.hasHitWithSlice = false;
                }
            }
        }

        protected override void RunGun()
        {
            if ( this.specialAttackDashTime > 0f )
            {
                this.gunFrame = 11;
                this.SetGunFrame();
            }
            else if ( !this.WallDrag && !this.animateSpecial )
            {
                if ( this.gunFrame > 0 )
                {
                    if ( !this.hasBeenCoverInAcid )
                    {
                        base.GetComponent<Renderer>().material = this.materialArmless;
                    }
                    this.gunCounter += this.t;
                    if ( this.gunCounter > 0.05f )
                    {
                        this.gunCounter -= 0.05f;
                        this.gunFrame++;
                        if ( this.gunFrame > 4 && !heldGunFrame )
                        {
                            this.NormalAttackDamage( base.X + base.transform.localScale.x * 10f, base.Y + 6.5f, base.transform.localScale.x * 400f, (float)UnityEngine.Random.Range( -20, 20 ) );
                            if ( this.hasHitWithSlice )
                            {
                                this.PlaySliceSound();
                                this.hasHitWithSlice = false;
                            }
                            else if ( this.hasHitWithWall )
                            {
                                this.PlayWallSound();
                                this.hasHitWithWall = false;
                            }
                            this.gunFrame = 4;
                            this.heldGunFrame = true;
                        }
                        if ( this.gunFrame >= 6 && heldGunFrame )
                        {
                            this.gunFrame = 0;
                        }
                        if ( this.gunFrame == 0 && this.punchingIndex % 2 == 1 )
                        {
                            this.gunSprite.SetLowerLeftPixel( (float)( 32 * this.gunFrame ), 32f );
                        }
                        else
                        {
                            this.SetGunFrame();
                        }
                        if ( this.gunFrame == 2 )
                        {
                            if ( this.hasHitWithSlice )
                            {
                                this.PlaySliceSound();
                                this.hasHitWithSlice = false;
                            }
                            else if ( this.hasHitWithWall )
                            {
                                this.PlayWallSound();
                                this.hasHitWithWall = false;
                            }
                        }
                    }
                }
                /*			else if (this.currentZone != null && this.currentZone.PoolIndex != -1)
                            {
                                this.gunSprite.SetLowerLeftPixel(0f, 128f);
                            }*/
            }
            if ( !this.animateSpecial && ( !this.gunSprite.gameObject.activeSelf || this.gunFrame == 0 ) && !this.hasBeenCoverInAcid )
            {
                base.GetComponent<Renderer>().material = this.materialNormal;
                this.heldGunFrame = false;
            }
        }

        protected void SetGunFrame()
        {
            if ( !this.ducking )
            {
                int num = this.punchingIndex % 2;
                if ( num != 0 )
                {
                    if ( num == 1 )
                    {
                        this.gunSprite.SetLowerLeftPixel( (float)( 32 * ( 5 + this.gunFrame ) ), 32f );
                    }
                }
                else
                {
                    this.gunSprite.SetLowerLeftPixel( (float)( 32 * this.gunFrame ), 32f );
                }
            }
            else
            {
                int num2 = this.punchingIndex % 2;
                if ( num2 != 0 )
                {
                    if ( num2 == 1 )
                    {
                        this.gunSprite.SetLowerLeftPixel( (float)( 32 * ( 15 + this.gunFrame ) ), 32f );
                    }
                }
                else
                {
                    this.gunSprite.SetLowerLeftPixel( (float)( 32 * ( 10 + this.gunFrame ) ), 32f );
                }
            }
        }

        // Copied from Neo
        protected override void SetGunPosition( float xOffset, float yOffset )
        {
            // Fixes arms being offset from body
            this.gunSprite.transform.localPosition = new Vector3( xOffset + 4f, yOffset, -1f );
        }

        protected override void PressHighFiveMelee( bool forceHighFive = false )
        {
            if ( this.right && this.CanAirDash( DirectionEnum.Right ) && this.SpecialAmmo > 0 )
            {
                if ( !this.wasHighFive )
                {
                    this.Airdash( true );
                }
            }
            else if ( this.left && this.CanAirDash( DirectionEnum.Left ) && this.SpecialAmmo > 0 )
            {
                if ( !this.wasHighFive )
                {
                    this.Airdash( true );
                }
            }
            else if ( this.airdashTime <= 0f )
            {
                base.PressHighFiveMelee( false );
            }
        }

        protected override void AirDashLeft()
        {
            if ( !this.animateSpecial && this.SpecialAmmo > 0 && this.airDashCooldown <= 0 )
            {
                if ( this.attachedToZipline != null )
                {
                    this.attachedToZipline.DetachUnit( this );
                    this.ActivateGun();
                }
                this.gunFrame = 0;
                this.SetGunSprite( 0, 0 );
                if ( this.invulnerabilityFlash == null )
                {
                    this.invulnerabilityFlash = this.GetComponent<InvulnerabilityFlash>();
                }
                if ( this.invulnerabilityFlash != null && this.invulnerableTime <= 0 )
                {
                    this.invulnerabilityFlash.enabled = false;
                }
                this.currentlyHitting = new List<Unit>();
                base.AirDashLeft();
                this.airDashCooldown = this.airdashTime + 0.2f;
            }
        }

        protected override void AirDashRight()
        {
            if ( !this.animateSpecial && this.SpecialAmmo > 0 && this.airDashCooldown <= 0 )
            {
                if ( this.attachedToZipline != null )
                {
                    this.attachedToZipline.DetachUnit( this );
                    this.ActivateGun();
                }
                this.gunFrame = 0;
                this.SetGunSprite( 0, 0 );
                if ( this.invulnerabilityFlash == null )
                {
                    this.invulnerabilityFlash = this.GetComponent<InvulnerabilityFlash>();
                }
                if ( this.invulnerabilityFlash != null && this.invulnerableTime <= 0 )
                {
                    this.invulnerabilityFlash.enabled = false;
                }
                this.currentlyHitting = new List<Unit>();
                base.AirDashRight();
                this.airDashCooldown = this.airdashTime + 0.2f;
            }
        }

        protected override void RunAirDashing()
        {
            base.RunAirDashing();
            // Re-enable invulnerability flash when finished air-dashing
            if ( this.airdashTime <= 0 && this.invulnerabilityFlash != null )
            {
                this.invulnerabilityFlash.enabled = true;
            }
        }

        protected override void RunLeftAirDash()
        {
            if ( this.airDashDelay > 0f )
            {
                this.airDashDelay -= this.t;
                this.yI = 0f;
                this.xI = 50f;
                base.transform.localScale = new Vector3( -1f, this.yScale, 1f );
                if ( this.airDashDelay <= 0f )
                {
                    this.ChangeFrame();
                    this.PlayAidDashSound();
                }
            }
            else
            {
                this.SetAirDashLeftSpeed();
            }
            this.specialAttackDashCounter += this.t;
            if ( this.specialAttackDashCounter > 0f )
            {
                this.specialAttackDashCounter -= 0.0333f;
                if ( Map.HitUnits( this, base.playerNum, 3, DamageType.Crush, 9f, base.X, base.Y, base.transform.localScale.x * ( 200 + UnityEngine.Random.value * 50f ), 350, true, true, false, currentlyHitting, true, false ) )
                {
                    this.sound.PlaySoundEffectAt( shieldMeleeHit, 0.3f, base.transform.position, 1f, true, false, false, 0f );
                }
            }
            if ( this.airDashDelay <= 0f )
            {
                this.airdashFadeCounter += Time.deltaTime;
                if ( this.airdashFadeCounter > this.airdashFadeRate )
                {
                    this.airdashFadeCounter -= this.airdashFadeRate;
                    this.CreateFaderTrailInstance();
                }
            }
        }

        protected override void RunRightAirDash()
        {
            if ( this.airDashDelay > 0f )
            {
                this.airDashDelay -= this.t;
                this.yI = 0f;
                this.xI = -50f;
                base.transform.localScale = new Vector3( 1f, this.yScale, 1f );
                if ( this.airDashDelay <= 0f )
                {
                    this.ChangeFrame();
                    this.PlayAidDashSound();
                }
            }
            else
            {
                this.SetAirDashRightSpeed();
            }
            this.specialAttackDashCounter += this.t;
            if ( this.specialAttackDashCounter > 0f )
            {
                this.specialAttackDashCounter -= 0.0333f;
                if ( Map.HitUnits( this, base.playerNum, 3, DamageType.Crush, 9f, base.X, base.Y, base.transform.localScale.x * ( 200 + UnityEngine.Random.value * 50f ), 350, true, true, false, currentlyHitting, true, false ) )
                {
                    this.sound.PlaySoundEffectAt( shieldMeleeHit, 0.3f, base.transform.position, 1f, true, false, false, 0f );
                }
            }
            if ( this.airDashDelay <= 0f )
            {
                this.airdashFadeCounter += Time.deltaTime;
                if ( this.airdashFadeCounter > this.airdashFadeRate )
                {
                    this.airdashFadeCounter -= this.airdashFadeRate;
                    this.CreateFaderTrailInstance();
                }
            }
        }

        protected override void PlayAidDashSound()
        {
            this.sound.PlaySoundEffectAt( effortSounds, 0.25f, base.transform.position, 1f, true, false, false, 0f );
            this.sound.PlaySoundEffectAt( airDashSound, 0.75f, base.transform.position, 1f, true, false, false, 0f );
        }

        protected override void CheckFacingDirection()
        {
            if ( !this.chimneyFlip && this.holdStillTime <= 0f && ( this.airdashTime <= 0 ) )
            {
                if ( this.usingSpecial && !this.turnAroundWhhileUsingSpecials && this.pressSpecialFacingDirection != 0 )
                {
                    base.transform.localScale = new Vector3( (float)this.pressSpecialFacingDirection, this.yScale, 1f );
                }
                else if ( this.xI < 0f || ( this.left && this.health > 0 ) )
                {
                    base.transform.localScale = new Vector3( -1f, this.yScale, 1f );
                }
                else if ( this.xI > 0f || ( this.right && this.health > 0 ) )
                {
                    base.transform.localScale = new Vector3( 1f, this.yScale, 1f );
                }
            }
        }

        // Performs melee attack
        protected void MeleeAttack( bool shouldTryHitTerrain, bool playMissSound )
        {
            bool flag;
            Map.DamageDoodads( meleeAttackDamage - 2, DamageType.Knock, base.X + (float)( base.Direction * 4 ), base.Y, 0f, 0f, 6f, base.playerNum, out flag, null );
            this.KickDoors( 24f );
            if ( Map.HitClosestUnit( this, base.playerNum, meleeAttackDamage, DamageType.Knock, 14f, 24f, base.X + base.transform.localScale.x * 8f, base.Y + 8f, base.transform.localScale.x * 300f, 600f, true, false, base.IsMine, false, true ) )
            {
                this.sound.PlaySoundEffectAt( shieldMeleeHit[this.currentMeleeSound], 0.5f, base.transform.position, 1f, true, false, false, 0f );
                this.meleeHasHit = true;
            }
            else if ( playMissSound )
            {
                //this.sound.PlaySoundEffectAt(this.soundHolder.missSounds, 0.3f, base.transform.position, 1f, true, false, false, 0f);
            }
            this.meleeChosenUnit = null;
            if ( shouldTryHitTerrain && this.TryMeleeTerrain( 0, meleeAttackDamage - 2 ) )
            {
                this.meleeHasHit = true;
                this.sound.PlaySoundEffectAt( shieldMeleeTerrain, 0.5f, base.transform.position, 1f, true, false, false, 0f );
            }
            this.TriggerBroMeleeEvent();
        }

        // Sets up melee attack
        protected override void StartCustomMelee()
        {
            if ( this.animateSpecial )
            {
                return;
            }
            if ( this.CanStartNewMelee() )
            {
                this.usingShieldMelee = this._specialAmmo > 0;
                this.alreadySpawnedBullet = false;
                if ( !( this.nearbyMook != null && this.nearbyMook.CanBeThrown() ) && this.usingShieldMelee )
                {
                    this.currentMeleeSound = UnityEngine.Random.Range( 0, shieldMeleeSwing.Length );
                    this.sound.PlaySoundEffectAt( shieldMeleeSwing[this.currentMeleeSound], 0.6f, base.transform.position, 1f, true, false, false, 0f );
                }
                base.frame = 1;
                base.counter = -0.05f;

                this.AnimateMelee();
            }
            else if ( this.CanStartMeleeFollowUp() )
            {
                this.meleeFollowUp = true;
                this.alreadySpawnedBullet = false;
            }
            if ( !this.jumpingMelee && this.usingShieldMelee )
            {
                this.dashingMelee = true;
                this.xI = (float)base.Direction * this.speed;
            }
            this.StartMeleeCommon();
        }

        // Calls MeleeAttack
        protected override void AnimateCustomMelee()
        {
            this.AnimateMeleeCommon();
            // Shield bash
            if ( this.usingShieldMelee )
            {
                int num = 25 + Mathf.Clamp( base.frame, 0, 6 );
                int num2 = 1;
                if ( !this.standingMelee )
                {
                    if ( this.jumpingMelee )
                    {
                        num = 17 + Mathf.Clamp( base.frame, 0, 6 );
                        num2 = 6;
                    }
                    else if ( this.dashingMelee )
                    {
                        num = 17 + Mathf.Clamp( base.frame, 0, 6 );
                        num2 = 6;
                        if ( base.frame == 4 )
                        {
                            base.counter -= 0.0334f;
                        }
                        else if ( base.frame == 5 )
                        {
                            base.counter -= 0.0334f;
                        }
                    }
                }
                this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( num2 * this.spritePixelHeight ) );
                if ( base.frame == 3 )
                {
                    base.counter -= 0.066f;
                    this.MeleeAttack( true, true );
                }
                else if ( base.frame > 3 && !this.meleeHasHit )
                {
                    this.MeleeAttack( false, false );
                }
                if ( base.frame >= 6 )
                {
                    base.frame = 0;
                    this.CancelMelee();
                }
            }
            // Fire gun
            else
            {
                int num = 25 + Mathf.Clamp( base.frame, 0, 6 );
                int num2 = 1;
                this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( num2 * this.spritePixelHeight ) );
                if ( base.frame == 3 && !this.alreadySpawnedBullet )
                {
                    this.sound.PlaySoundEffectAt( pistolSounds, 0.5f, base.transform.position, 1f, true, false, false, 0f );
                    Projectile bullet = ProjectileController.SpawnProjectileLocally( this.pistolBullet, this, this.X + ( this.transform.localScale.x * 12 ), this.Y + 13.5f, this.transform.localScale.x * 250, 0, base.playerNum );
                    EffectsController.CreateMuzzleFlashEffect( this.X + ( this.transform.localScale.x * 14 ), this.Y + 13.5f, -25f, this.transform.localScale.x * 100, 0, base.transform );
                    this.alreadySpawnedBullet = true;
                }
                if ( base.frame >= 6 )
                {
                    base.frame = 0;
                    this.CancelMelee();
                }
            }
        }

        protected override void RunCustomMeleeMovement()
        {
            if ( !this.useNewKnifingFrames )
            {
                if ( base.Y > this.groundHeight + 1f )
                {
                    this.ApplyFallingGravity();
                }
            }
            else if ( this.jumpingMelee )
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
                        if ( !this.isInQuicksand && this.usingShieldMelee )
                        {
                            this.xI = this.speed * 1f * base.transform.localScale.x;
                        }
                        this.yI = 0f;
                    }
                    else if ( !this.isInQuicksand && this.usingShieldMelee )
                    {
                        this.xI = this.speed * 0.5f * base.transform.localScale.x + ( this.meleeChosenUnit.X - base.X ) * 6f;
                    }
                }
                else if ( base.frame <= 5 )
                {
                    if ( !this.isInQuicksand && this.usingShieldMelee )
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
    }
}
