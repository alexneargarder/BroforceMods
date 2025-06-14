using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using Drunken_Broster.MeleeItems;
using HarmonyLib;
using Rogueforce;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Drunken_Broster
{
    [HeroPreset( "Drunken Broster", HeroType.Rambro )]
    public class DrunkenBroster : CustomHero
    {
        // General
        protected bool acceptedDeath = false;
        bool wasInvulnerable = false;

        // Sounds
        AudioClip slurp;

        // Sprites
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
        protected bool hasPlayedAttackHitSound = false;
        protected bool attackStationary = false;
        protected bool attackUpwards = false;
        protected bool attackDownwards = false;
        protected bool attackForwards = false;
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
        protected int enemyFistDamage = 5;
        protected int groundFistDamage = 5;
        public Shrapnel shrapnelSpark;
        public FlickerFader hitPuff;
        protected float lastAttackingTime = 0;
        protected bool startNewAttack = false;
        public float fistVolume = 0.7f;
        public float wallHitVolume = 0.3f;


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
        protected Grenade tireProjectile;
        protected Projectile acidEggProjectile;
        protected Projectile beehiveProjectile;
        protected Projectile bottleProjectile;
        protected Projectile crateProjectile;
        protected Projectile coconutProjectile;
        protected Projectile explosiveBarrelProjectile;
        protected Projectile soccerBallProjectile;
        protected Projectile alienEggProjectile;
        protected Projectile skullProjectile;
        protected MeshRenderer gunSpriteMelee;
        protected SpriteSM gunSpriteMeleeSprite;
        protected Material meleeSpriteGrabThrowing;
        protected bool throwingHeldItem = false;
        protected bool thrownItem = false;
        protected bool hitSpecialDoodad = false;
        protected bool progressedFarEnough = false;

        // Special
        public bool wasDrunk = false; // Was drunk before starting special
        public bool drunk = false;
        protected const float maxDrunkTime = 15f;
        public float drunkCounter = 0f;
        protected int usingSpecialFrame = 0;
        protected float originalSpeed = 0;

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
            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            this.normalSprite = base.GetComponent<Renderer>().material;
            this.drunkSprite = ResourcesController.GetMaterial( directoryPath, "drunkSprite.png" );

            // Setup melee gunsprite
            gunSpriteMelee = new GameObject( "GunSpriteMelee", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } ).GetComponent<MeshRenderer>();
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

            // Load melee sprite for grabbing and throwing animations
            this.meleeSpriteGrabThrowing = ResourcesController.GetMaterial( directoryPath, "meleeSpriteGrabThrowing.png" );

            // Setup throwables
            // Load tire
            tireProjectile = new GameObject( "TireProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( TireProjectile ) } ).GetComponent<Grenade>();
            tireProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            tireProjectile.enabled = false;

            // Load acid eggg
            acidEggProjectile = new GameObject( "AcidEggProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( AcidEggProjectile ) } ).GetComponent<Projectile>();
            acidEggProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            acidEggProjectile.enabled = false;

            // TODO: Load beehive
            beehiveProjectile = new GameObject( "CrateProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( CrateProjectile ) } ).GetComponent<Projectile>();
            beehiveProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            beehiveProjectile.enabled = false;

            // TODO: Load bottle
            bottleProjectile = new GameObject( "CrateProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( CrateProjectile ) } ).GetComponent<Projectile>();
            bottleProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            bottleProjectile.enabled = false;

            // Load crate
            crateProjectile = new GameObject( "CrateProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( CrateProjectile ) } ).GetComponent<Projectile>();
            crateProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            crateProjectile.enabled = false;

            // TODO: Load coconut
            coconutProjectile = new GameObject( "CrateProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( CrateProjectile ) } ).GetComponent<Projectile>();
            coconutProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            coconutProjectile.enabled = false;

            // TODO: Load explosive barrel
            explosiveBarrelProjectile = new GameObject( "CrateProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( CrateProjectile ) } ).GetComponent<Projectile>();
            explosiveBarrelProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            explosiveBarrelProjectile.enabled = false;

            // TODO: Load soccer ball
            soccerBallProjectile = new GameObject( "CrateProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( CrateProjectile ) } ).GetComponent<Projectile>();
            soccerBallProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            soccerBallProjectile.enabled = false;

            // TODO: Load alien egg
            alienEggProjectile = new GameObject( "CrateProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( CrateProjectile ) } ).GetComponent<Projectile>();
            alienEggProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            alienEggProjectile.enabled = false;

            // TODO: Load skull
            skullProjectile = new GameObject( "CrateProjectile", new Type[] { typeof( Transform ), typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ), typeof( CrateProjectile ) } ).GetComponent<Projectile>();
            skullProjectile.soundHolder = ( HeroController.GetHeroPrefab( HeroType.Rambro ) as Rambro ).projectile.soundHolder;
            skullProjectile.enabled = false;

            // Load sounds
            directoryPath = Path.Combine( directoryPath, "sounds" );
            slurp = ResourcesController.CreateAudioClip( directoryPath, "slurp.wav" );

            // TODO: enable rolling
            doRollOnLand = false;
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
        }

        public override void UIOptions()
        {
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

        protected override void ActivateGun()
        {
            if ( this.holdingItem )
            {
                this.gunSpriteMelee.gameObject.SetActive( true );
            }
            else
            {
                base.ActivateGun();
            }
        }

        protected override void DeactivateGun()
        {
            if ( this.holdingItem )
            {
                this.gunSpriteMelee.gameObject.SetActive( false );
            }
            else
            {
                base.DeactivateGun();
            }
        }

        protected override void SetGunSprite( int spriteFrame, int spriteRow )
        {
            SpriteSM currentSprite;

            if ( !this.holdingItem )
            {
                currentSprite = this.gunSprite;
            }
            else
            {
                currentSprite = this.gunSpriteMeleeSprite;
                spriteRow += ( (int)this.heldItem ) * 2;
            }

            if ( base.actionState == ActionState.Hanging )
            {
                currentSprite.SetLowerLeftPixel( gunSpritePixelWidth * ( gunSpriteHangingFrame + spriteFrame ), gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
            else if ( base.actionState == ActionState.ClimbingLadder && hangingOneArmed )
            {
                currentSprite.SetLowerLeftPixel( gunSpritePixelWidth * ( gunSpriteHangingFrame + spriteFrame ), gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
            else if ( attachedToZipline != null && base.actionState == ActionState.Jumping )
            {
                currentSprite.SetLowerLeftPixel( gunSpritePixelWidth * ( gunSpriteHangingFrame + spriteFrame ), gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
            else
            {
                currentSprite.SetLowerLeftPixel( gunSpritePixelWidth * spriteFrame, gunSpritePixelHeight * ( 1 + spriteRow ) );
            }
        }

        protected override void StartFiring()
        {
            // Don't allow attacking while drinking / becoming sober
            if ( this.usingSpecial )
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

            // Don't allow attacking while using melele
            if ( this.doingMelee )
            {
                this.fire = this.wasFire = false;
                return;
            }

            this.startNewAttack = false;
            this.hasPlayedAttackHitSound = false;
            this.hasHitWithWall = false;
            this.hasHitWithFists = false;
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
                this.StartAttackForwardsLeft();
            }
            // Forward Attack Right Start
            else if ( this.right && !this.hasAttackedForwards )
            {
                this.StartAttackForwardsRight();
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

        protected void StartAttackForwardsLeft()
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
                    this.xIAttackExtra = 20f;
                }
            }
            else if ( ( this.attackForwards || this.attackUpwards || this.attackDownwards ) && this.hasHitThisAttack )
            {
                this.StopAttack();
                if ( !this.drunk )
                {
                    this.xIAttackExtra = -300f;
                }
                this.MakeKungfuSound();
            }
            else
            {
                this.StopAttack();
                if ( !this.drunk )
                {
                    this.xIAttackExtra = -200f;
                }
                this.MakeKungfuSound();
            }
            this.postAttackHitPauseTime = 0f;
            this.hasAttackedForwards = true;
            this.attackFrames = 0;
            this.yI = 0f;
            this.attackForwards = true;
            this.attackDirection = -1;
            this.jumpTime = 0f;
            this.ChangeFrame();
            this.CreateFaderTrailInstance();
            this.airdashDirection = DirectionEnum.Left;
            this.ClearCurrentAttackVariables();
        }

        protected void StartAttackForwardsRight()
        {
            this.attackSpriteRow = ( this.attackSpriteRow + 1 ) % 2;
            this.FireFlashAvatar();
            if ( base.actionState == ActionState.ClimbingLadder )
            {
                base.actionState = ActionState.Jumping;
            }
            if ( ( this.attackForwards || this.attackUpwards || this.attackDownwards ) && !this.hasHitThisAttack )
            {
                this.StopAttack();
                if ( !this.drunk )
                {
                    this.xIAttackExtra = -20f;
                }
            }
            else if ( ( this.attackForwards || this.attackUpwards || this.attackDownwards ) && this.hasHitThisAttack )
            {
                this.StopAttack();
                if ( !this.drunk )
                {
                    this.xIAttackExtra = 300f;
                }
                this.MakeKungfuSound();
            }
            else
            {
                this.StopAttack();
                if ( !this.drunk )
                {
                    this.xIAttackExtra = 200f;
                }
                this.MakeKungfuSound();
            }
            this.hasAttackedForwards = true;
            this.postAttackHitPauseTime = 0f;
            this.attackFrames = 0;
            this.yI = 0f;
            this.attackForwards = true;
            this.attackDirection = 1;
            this.jumpTime = 0f;
            this.ChangeFrame();
            this.CreateFaderTrailInstance();
            this.airdashDirection = DirectionEnum.Right;
            this.ClearCurrentAttackVariables();
        }

        protected void StartAttackStationary()
        {
            this.FireFlashAvatar();
            if ( base.actionState == ActionState.ClimbingLadder )
            {
                base.actionState = ActionState.Jumping;
            }
            this.StopAttack();
            this.MakeKungfuSound();
            this.postAttackHitPauseTime = 0f;
            this.attackFrames = 0;
            this.attackStationary = true;
            this.jumpTime = 0f;
            ++this.stationaryAttackCounter;
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
                if ( !this.attackHasHit )
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
                        this.PlayFistSound();
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
                if ( HitUnitsStationaryAttack( this, base.playerNum, this.enemyFistDamage, 1, DamageType.Blade, 13f, 13f, base.X + base.transform.localScale.x * 7f, base.Y + 7f, 700f * base.transform.localScale.x, 250f, true, true, false, this.alreadyHit ) )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayFistSound();
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

            if ( !this.attackHasHit )
            {
                this.DeflectProjectiles();
            }
            if ( !this.attackHasHit )
            {
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 9f, base.transform.localScale.x * 180f, 80f );
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 12f, new Vector3( base.transform.localScale.x, 0f, 0f ), 9f, base.transform.localScale.x * 180f, 80f );
            }
        }

        protected void PerformAttackForwards()
        {
            this.lastAttackingTime = Time.time;
            // Sober attack
            if ( !this.drunk )
            {
                DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 7f, base.transform.localScale.x * 250f + this.xI, 200f, 6f, base.playerNum, out _, this );
                if ( HitUnits( this, base.playerNum, this.enemyFistDamage, 1, DamageType.Blade, 8f, 13f, base.X + base.transform.localScale.x * 7f, base.Y + 7f, base.transform.localScale.x * 420f, 200f, true, true, true, this.alreadyHit ) )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayFistSound();
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
                    if ( HitUnits( this, base.playerNum, this.enemyFistDamage, 1, DamageType.Blade, 4f, 10f, base.X + base.transform.localScale.x * 7f, base.Y + 7f, base.transform.localScale.x * 220f, 450f, true, true, false, this.alreadyHit ) )
                    {
                        if ( !this.hasHitWithFists )
                        {
                            this.PlayFistSound();
                        }
                        this.hasHitWithFists = true;
                        this.attackHasHit = true;
                        this.hasAttackedDownwards = false;
                        this.hasAttackedUpwards = false;
                        this.hasAttackedForwards = false;
                        this.hasHitThisAttack = true;
                        HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
                        for ( int i = 0; i < this.alreadyHit.Count; i++ )
                        {
                            this.alreadyHit[i].BackSomersault( false );
                        }
                    }
                    // Pause if we hit one of drunken master's doodads
                    else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
                    {
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
                    if ( HitUnits( this, base.playerNum, this.enemyFistDamage + 4, 3, DamageType.Blade, 5f, 8f, base.X + base.transform.localScale.x * 7f, base.Y + 7f, base.transform.localScale.x * 520f, 200f, true, true, false, this.alreadyHit ) )
                    {
                        if ( !this.hasHitWithFists )
                        {
                            this.PlayFistSound();
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
                        HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
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
                        this.hasHitWithFists = true;
                        this.attackHasHit = true;
                        this.hasAttackedDownwards = false;
                        this.hasAttackedUpwards = false;
                        this.hasAttackedForwards = false;
                        this.hasHitThisAttack = true;
                        this.TimeBump();
                        this.xIAttackExtra = 0f;
                        this.postAttackHitPauseTime = 0.1f;
                        HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
                        this.xI = 0f;
                        this.yI = 0f;
                    }
                }

            }
            if ( !this.attackHasHit )
            {
                this.DeflectProjectiles();
            }
            if ( !this.attackHasHit )
            {
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 9f, base.transform.localScale.x * 180f, 80f );
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 12f, new Vector3( base.transform.localScale.x, 0f, 0f ), 9f, base.transform.localScale.x * 180f, 80f );
            }
        }

        protected void PerformAttackUpwards()
        {
            DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 6f ), base.Y + 12f, base.transform.localScale.x * 80f, 1100f, 7f, base.playerNum, out _, this );
            this.lastAttackingTime = Time.time;
            if ( HitUnits( this, base.playerNum, this.enemyFistDamage, 1, DamageType.Blade, 13f, 13f, base.X + base.transform.localScale.x * 6f, base.Y + 12f, base.transform.localScale.x * 80f, 1100f, true, true, true, this.alreadyHit ) )
            {
                if ( !this.hasHitWithFists )
                {
                    this.PlayFistSound();
                }
                this.hasHitWithFists = true;
                this.attackHasHit = true;
                this.hasAttackedDownwards = false;
                this.hasAttackedForwards = false;
                this.hasHitThisAttack = true;
                this.TimeBump();
                if ( this.drunk )
                {
                    HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
                }
            }
            // Pause if we hit one of drunken master's doodads
            else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
            {
                this.hasHitWithFists = true;
                this.attackHasHit = true;
                this.hasAttackedDownwards = false;
                this.hasAttackedForwards = false;
                this.hasHitThisAttack = true;
                this.TimeBump();
                if ( this.drunk )
                {
                    HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
                }
            }
            if ( !this.attackHasHit )
            {
                this.DeflectProjectiles();
            }
            if ( !this.attackHasHit )
            {
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x * 0.5f, 1f, 0f ), 12f, base.transform.localScale.x * 80f, 280f );
                this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0.5f, 0f ), 12f, base.transform.localScale.x * 80f, 280f );
            }
        }

        protected void PerformAttackDownwards()
        {
            // Sober Attack
            if ( !this.drunk )
            {
                DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 2f, base.transform.localScale.x * 120f, 100f, 6f, base.playerNum, out _, this );
                this.lastAttackingTime = Time.time;
                if ( HitUnits( this, base.playerNum, this.enemyFistDamage + 2, 1, DamageType.Blade, 9f, 6f, base.X + base.transform.localScale.x * 7f, base.Y + 2f, base.transform.localScale.x * 120f, 100f, true, true, true, this.alreadyHit ) )
                {
                    if ( !this.hasHitWithFists )
                    {
                        this.PlayFistSound();
                    }
                    this.hasHitWithFists = true;
                    this.attackHasHit = true;
                    this.hasAttackedForwards = false;
                    this.hasAttackedUpwards = false;
                    this.hasHitThisAttack = true;
                    this.xIAttackExtra = 0f;
                    this.TimeBump();
                    this.postAttackHitPauseTime = 0.15f;
                    this.xI = 0f;
                    this.yI = 0f;
                    if ( this.drunk )
                    {
                        HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
                    }
                }
                // Pause if we hit one of drunken master's doodads
                else if ( this.hitSpecialDoodad && !this.hasHitThisAttack )
                {
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
                    if ( this.drunk )
                    {
                        HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
                    }
                }
                if ( !this.attackHasHit )
                {
                    this.DeflectProjectiles();
                }
                if ( !this.attackHasHit )
                {
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x * 0.4f, -1f, 0f ), 14f, base.transform.localScale.x * 80f, -180f );
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 3f, base.Y + 6f, new Vector3( base.transform.localScale.x * 0.8f, -0.2f, 0f ), 12f, base.transform.localScale.x * 80f, -180f );
                }
            }
            // Drunk Attack
            else
            {
                DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y, base.transform.localScale.x * 120f, 100f, 6f, base.playerNum, out _, this );
                this.lastAttackingTime = Time.time;
                if ( !this.attackHasHit )
                {
                    this.DeflectProjectiles();
                }
            }
        }

        // Unused
        protected override void UseFire()
        {
        }

        // Unused
        protected override void FireWeapon( float x, float y, float xSpeed, float ySpeed )
        {
        }

        protected void FireWeaponGround( float x, float y, Vector3 raycastDirection, float distance, float xSpeed, float ySpeed )
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
            EffectsController.CreateShrapnel( this.shrapnelSpark, x, y, 4f, 30f, 3f, xI, yI );
            EffectsController.CreateEffect( this.hitPuff, x, y, 0f );
        }

        protected virtual void MakeEffects()
        {
            EffectsController.CreateShrapnel( this.shrapnelSpark, this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f, 4f, 30f, 3f, this.raycastHit.normal.x * 60f, this.raycastHit.normal.y * 30f );
            EffectsController.CreateEffect( this.hitPuff, this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f );
        }

        protected override void SetGunPosition( float xOffset, float yOffset )
        {
            this.gunSprite.transform.localPosition = new Vector3( xOffset, yOffset, -1f );
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

        public bool HitUnits( MonoBehaviour damageSender, int playerNum, int damage, int corpseDamage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, bool penetrates, bool knock, bool canGib, List<Unit> alreadyHitUnits )
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
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 16.5f, base.Y + 2f, Vector3.up, this.headHeight + Mathf.Abs( this.yI * this.t ), base.transform.localScale.x * 80f, 100f );
                    }
                    if ( !this.attackHasHit && this.attackFrames < 7 )
                    {
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 4.5f, base.Y + 2f, Vector3.up, this.headHeight + Mathf.Abs( this.yI * this.t ), base.transform.localScale.x * 80f, 100f );
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
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 16.5f, base.Y + 2f, Vector3.up, this.headHeight + Mathf.Abs( this.yI * this.t ), base.transform.localScale.x * 80f, 100f );
                    }
                    if ( !this.attackHasHit && this.attackFrames < 7 )
                    {
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 4.5f, base.Y + 2f, Vector3.up, this.headHeight + Mathf.Abs( this.yI * this.t ), base.transform.localScale.x * 80f, 100f );
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

        protected void StopAttack()
        {
            this.hasHitThisAttack = false;
            this.attackStationary = ( this.attackUpwards = ( this.attackDownwards = ( this.attackForwards = false ) ) );
            this.attackFrames = 0;
            base.frame = 0;
            this.xIAttackExtra = 0f;
            if ( base.Y > this.groundHeight + 1f )
            {
                base.actionState = ActionState.Jumping;
            }
            else if ( this.right || this.left )
            {
                base.actionState = ActionState.Running;
            }
            else
            {
                base.actionState = ActionState.Idle;
            }
            if ( this.startNewAttack )
            {
                this.startNewAttack = false;
                this.StartFiring();
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
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 9f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 8f, base.transform.localScale.x * 180f, 80f );
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
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 9f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 8f, base.transform.localScale.x * 180f, 80f );
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
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 9f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 8f, base.transform.localScale.x * 180f, 80f );
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
                    this.FireWeaponGround( base.X + base.transform.localScale.x * 9f, base.Y + 6f, new Vector3( base.transform.localScale.x, 0f, 0f ), 3f, base.transform.localScale.x * 180f, 80f );
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
                this.attackFrames++;
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
            // Call base FlashAvatar to cause rumble
            base.FireFlashAvatar();
            this.avatarGunFireTime = 0.1f;
            HeroController.SetAvatarFireFrame( base.playerNum, UnityEngine.Random.Range( 5, 8 ) );
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
                if ( this.fire )
                {
                    if ( !this.wasFire && this.avatarGunFireTime <= 0f )
                    {
                        HeroController.SetAvatarAngry( base.playerNum, this.usePrimaryAvatar );
                    }
                    if ( this.fire )
                    {
                        if ( this.gunFrame > 0 || this.attackFrames > 0 )
                        {
                            this.avatarAngryTime = 0.1f;
                        }
                        else
                        {
                            this.avatarAngryTime = 0f;
                            HeroController.SetAvatarCalm( base.playerNum, this.usePrimaryAvatar );
                        }
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

        private void TimeBump()
        {
            TimeController.StopTime( 0.025f, 0.1f, 0f, false, false, false );
        }

        private void MakeKungfuSound()
        {
            if ( Time.time - this.lastSoundTime > 0.3f )
            {
                this.lastSoundTime = Time.time;
                // TODO: Add kung fu noises from bro lee
                //Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.attack3Sounds, 0.6f, base.transform.position, 1f, true, false, false, 0f);
            }
        }

        protected override void PlayAttackSound()
        {
            // TODO: Add attack sound
            //base.PlayAttackSound();
        }

        public void PlayFistSound()
        {
            if ( this.sound == null )
            {
                this.sound = Sound.GetInstance();
            }
            if ( this.sound != null )
            {
                // TODO: Add fist hit sound
                //this.sound.PlaySoundEffectAt(this.soundHolder.special2Sounds, this.sliceVolume, base.transform.position, 1f, true, false, false, 0f);
            }
        }

        public void PlayWallSound()
        {
            if ( this.sound == null )
            {
                this.sound = Sound.GetInstance();
            }
            if ( this.sound != null )
            {
                // TODO: Add wall sound, whatever that is, check brocsnipes or bro lee's attack2sounds 
                //this.sound.PlaySoundEffectAt(this.soundHolder.attack2Sounds, this.wallHitVolume, base.transform.position, 1f, true, false, false, 0f);
            }
        }

        protected override void PlayAidDashSound()
        {
            // TODO: Add air dash sound
            //this.PlaySpecialAttackSound(0.5f);
        }
        #endregion

        #region Melee
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

            // Switch to melee sprite
            base.GetComponent<Renderer>().material = this.meleeSpriteGrabThrowing;

            // Choose an item to throw
            this.chosenItem = this.ChooseItem();

            AnimateMelee();
        }

        protected MeleeItem ChooseItem()
        {
            //return MeleeItem.AcidEgg;
            return MeleeItem.Tire;
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

            // TODO: Switch to this code when sprites are in correct places
            //int row = ( (int)this.chosenItem ) + 1;
            int row = ( (int)0 ) + 1;

            this.sprite.SetLowerLeftPixel( (float)( base.frame * this.spritePixelWidth ), (float)( row * this.spritePixelHeight ) );

            if ( base.frame == 3 )
            {
                this.PerformMeleeAttack( true, true );
            }
            else if ( base.frame == 4 && !this.meleeHasHit )
            {
                this.PerformMeleeAttack( true, true );
            }

            if ( base.frame >= 3 )
            {
                // Indicate melee has progressed far enough to receive an item if cancelled
                this.progressedFarEnough = true;
            }

            if ( base.frame >= 10 )
            {
                base.frame = 0;
                this.CancelMelee();
            }
        }

        protected void AnimateThrowingHeldItem()
        {
            this.frameRate = 0.09f;

            // TODO: Switch to this code when sprites are in correct places
            //int row = ( (int)this.chosenItem ) + 1;
            int row = 1;

            int throwStart = 10;

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

        protected void PerformMeleeAttack( bool shouldTryHitTerrain, bool playMissSound )
        {
            Map.DamageDoodads( 3, DamageType.Knifed, base.X + (float)( base.Direction * 4 ), base.Y + 7f, 0f, 0f, 6f, base.playerNum, out _, null );
            this.KickDoors( 24f );
            this.meleeChosenUnit = null;

            // Perform attack based on item type
            switch ( this.chosenItem )
            {
                // Blunt hit
                case MeleeItem.Tire:
                case MeleeItem.Bottle:
                case MeleeItem.Crate:
                case MeleeItem.Coconut:
                case MeleeItem.ExplosiveBarrel:
                case MeleeItem.SoccerBall:
                    if ( Map.HitClosestUnit( this, base.playerNum, 1, DamageType.Knock, 8f, 24f, base.X + base.transform.localScale.x * 6f, base.Y + 7f, base.transform.localScale.x * 200f, 350f, true, false, base.IsMine, false, true ) )
                    {
                        this.PlayMeleeHitSound();
                        this.meleeHasHit = true;
                    }
                    else if ( playMissSound )
                    {
                        this.PlayMeleeMissSound();
                    }
                    break;

                // Acid hit
                case MeleeItem.AcidEgg:
                case MeleeItem.AlienEgg:
                    if ( Map.HitClosestUnit( this, base.playerNum, 1, DamageType.Acid, 8f, 24f, base.X + base.transform.localScale.x * 6f, base.Y + 7f, base.transform.localScale.x * 200f, 350f, true, false, base.IsMine, false, true ) )
                    {
                        this.PlayMeleeHitSound();
                        this.meleeHasHit = true;
                    }
                    else if ( playMissSound )
                    {
                        this.PlayMeleeMissSound();
                    }
                    break;

                // Fire hit
                case MeleeItem.Skull:
                    break;

                // Bee hit (panics units)
                case MeleeItem.Beehive:
                    break;
            }

            if ( shouldTryHitTerrain && this.TryMeleeTerrain( 0, 2 ) )
            {
                this.meleeHasHit = true;
            }
            this.TriggerBroMeleeEvent();
        }

        protected void PlayMeleeHitSound()
        {
            // TODO: add melee sound effects
            //this.sound.PlaySoundEffectAt( this.soundHolder.meleeHitSound, 1f, base.transform.position, 1f, true, false, false, 0f );
        }

        protected void PlayMeleeMissSound()
        {
            // TODO: add melee sound effects
            //this.sound.PlaySoundEffectAt( this.soundHolder.missSounds, 0.3f, base.transform.position, 1f, true, false, false, 0f );
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
                this.canChimneyFlip = true;
                this.throwingHeldItem = false;
                this.holdingItem = false;
                this.heldItem = MeleeItem.None;
                this.thrownItem = false;
                this.gunSprite.gameObject.SetActive( this.gunSpriteMelee.gameObject.activeSelf );
                this.gunSpriteMelee.gameObject.SetActive( false );
            }
            base.CancelMelee();
        }

        protected void SwitchToHeldItem()
        {
            this.holdingItem = true;
            this.heldItem = this.chosenItem;
            this.chosenItem = MeleeItem.None;
            this.gunSpriteMelee.gameObject.SetActive( this.gunSprite.gameObject.activeSelf );
            this.gunSprite.gameObject.SetActive( false );
        }

        protected void StartThrowingItem()
        {
            // Don't start a throw if we're already throwing, or doing another animation
            if ( this.throwingHeldItem || this.doingMelee || this.chimneyFlip || this.usingSpecial )
            {
                return;
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

            // Switch to melee sprite
            base.GetComponent<Renderer>().material = this.meleeSpriteGrabThrowing;

            AnimateMelee();
        }

        protected void ThrowHeldItem()
        {
            BroforceObject projectile;
            switch ( this.heldItem )
            {
                case MeleeItem.Tire:
                    projectile = ProjectileController.SpawnGrenadeLocally( this.tireProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, 0f, 0f, base.transform.localScale.x * 275f, 50f, base.playerNum, 0 ) as Grenade;
                    break;
                case MeleeItem.AcidEgg:
                    projectile = ProjectileController.SpawnProjectileLocally( this.acidEggProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
                case MeleeItem.Beehive:
                    projectile = ProjectileController.SpawnProjectileLocally( this.beehiveProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
                case MeleeItem.Bottle:
                    projectile = ProjectileController.SpawnProjectileLocally( this.bottleProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
                case MeleeItem.Crate:
                    projectile = ProjectileController.SpawnProjectileLocally( this.crateProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
                case MeleeItem.Coconut:
                    projectile = ProjectileController.SpawnProjectileLocally( this.coconutProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
                case MeleeItem.ExplosiveBarrel:
                    projectile = ProjectileController.SpawnProjectileLocally( this.explosiveBarrelProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
                case MeleeItem.SoccerBall:
                    projectile = ProjectileController.SpawnProjectileLocally( this.soccerBallProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
                case MeleeItem.AlienEgg:
                    projectile = ProjectileController.SpawnProjectileLocally( this.alienEggProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
                case MeleeItem.Skull:
                    projectile = ProjectileController.SpawnProjectileLocally( this.skullProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
                default:
                    projectile = ProjectileController.SpawnProjectileLocally( this.crateProjectile, this, base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 225f, 125f, base.playerNum ) as Projectile;
                    break;
            }
            projectile.enabled = true;

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
            // Make sure we aren't holding anything before drinking
            if ( this.heldItem == MeleeItem.None )
            {
                if ( this.SpecialAmmo > 0 )
                {
                    this.wasDrunk = false;
                    base.PressSpecial();
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
                this.usingSpecialFrame++;
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
                this.usingSpecialFrame++;
            }
        }

        protected void PlayDrinkingSound()
        {
            this.sound.PlaySoundEffectAt( this.slurp, 0.35f, base.transform.position, 1f, true, false, true, 0f );
        }

        protected override void UseSpecial()
        {
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
            base.frame = 0;
            this.usingSpecialFrame = 0;
            this.usingSpecial = false;
            this.ActivateGun();
            this.ChangeFrame();
            this.speed = this.originalSpeed;
        }

        protected void BecomeDrunk()
        {
            base.GetComponent<Renderer>().material = this.drunkSprite;
            // TODO: revert drunk counter
            //this.drunkCounter = maxDrunkTime;
            this.drunkCounter = 1000;
            this.drunk = true;
            this.speed = this.originalSpeed = 110;
            this.enemyFistDamage = 11;
            this.groundFistDamage = 10;
            this.attackDownwardsStrikeFrame = 2;
        }

        protected bool TryToBecomeSober()
        {
            if ( !hasBeenCoverInAcid && !doingMelee && !usingSpecial )
            {
                this.wasDrunk = true;
                this.usingSpecial = true;
                base.frame = 0;
                this.pressSpecialFacingDirection = (int)base.transform.localScale.x;
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
            this.enemyFistDamage = 5;
            this.groundFistDamage = 5;
            this.attackDownwardsStrikeFrame = 3;
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

        protected override void Jump( bool wallJump )
        {
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
        }

        protected override void Land()
        {
            if ( this.attackDownwards )
            {
                if ( !this.drunk )
                {
                    if ( !this.attackHasHit && this.attackFrames < 7 )
                    {
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 16.5f, base.Y + 16.5f, Vector3.down, 18f + Mathf.Abs( this.yI * this.t ), base.transform.localScale.x * 80f, 100f );
                    }
                    if ( !this.attackHasHit && this.attackFrames < 7 )
                    {
                        this.FireWeaponGround( base.X + base.transform.localScale.x * 5.5f, base.Y + 16.5f, Vector3.down, 18f + Mathf.Abs( this.yI * this.t ), base.transform.localScale.x * 80f, 100f );
                    }
                    this.attackDownwards = false;
                    this.attackFrames = 0;
                    base.Land();
                }
                else if ( !this.hasHitThisAttack )
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
                    explosionGroundWave.playerNum = 15;
                    explosionGroundWave.avoidObject = this;
                    explosionGroundWave.origins = this;
                    this.invulnerable = true;
                    if ( Map.HitUnits( this, 20, DamageType.Crush, 30f, 10f, base.X, base.Y - 4f, 0f, this.yI, true, false ) )
                    {
                    }
                    MapController.DamageGround( this, 25, DamageType.Crush, 40f, base.X, base.Y + 8f, null, false );
                    this.invulnerable = false;
                    this.xI = ( this.xIBlast = 0f );
                }
                else
                {
                    base.Land();
                }
            }
            else
            {
                base.Land();

                // Switch to dashing melee
                if ( this.doingMelee )
                {
                    this.dashingMelee = true;
                    this.jumpingMelee = false;
                }
            }
        }

        // Don't grab laddeer when doing drunk downwards attack
        protected override bool IsOverLadder( ref float ladderXPos )
        {
            return !( this.drunk && this.attackDownwards ) && base.IsOverLadder( ref ladderXPos );
        }

        // Don't grab laddeer when doing drunk downwards attack
        protected override bool IsOverLadder( float xOffset, ref float ladderXPos )
        {
            return !( this.drunk && this.attackDownwards ) && base.IsOverLadder( xOffset, ref ladderXPos );
        }
        #endregion
    }
}
