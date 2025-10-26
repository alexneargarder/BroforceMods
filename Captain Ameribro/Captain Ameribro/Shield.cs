using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using UnityEngine;

namespace Captain_Ameribro
{

    public class Shield : CustomProjectile
    {
        // General
        public float returnTime = 0.25f;
        protected bool hasReachedApex;
        protected float shieldSpeed;
        public float rotationSpeed = 2.0f;
        protected float hitUnitsDelay;
        protected int hitUnitsCount;
        public bool dropping = false;
        public SphereCollider shieldCollider;
        public float shieldLoopPitchM = 1f;
        protected float collectDelayTime = 0.1f;
        protected float windCounter;
        protected int windCount;
        public float windRotationSpeedM = 1f;
        protected float xStart;
        protected float lastXI;
        public float bounceXM = 0.5f;
        public float bounceYM = 0.33f;
        public float frictionM = 0.5f;
        public float bounceVolumeM = 1.0f;
        public float heightOffGround = 8f;
        protected List<Unit> alreadyHit = new List<Unit>();
        protected const float groundRotationSpeed = -10.0f;
        public TestVanDammeAnim throwingPlayer;
        protected bool shouldBoomerang = true;

        // Charging Variables
        public float shieldCharge = 0f;
        protected const float ChargeReturnScalar = 0.25f;
        public const float ChargeSpeedScalar = 100f;
        protected const int BaseDamage = 3;
        protected int bossDamage = 3;
        protected float knockbackXI = 150;
        protected float knockbackYI = 300;

        // Homing Variables
        protected float targetAngle;
        protected float targetX;
        protected float targetY;
        protected float originalAngle;
        public float seekRange = 75f;
        protected bool foundMook = false;
        protected float originalSpeed;
        protected float seekSpeedCurrent = 2f;
        protected float speed;
        public float seekTurningSpeedLerpM = 5f;
        public float seekTurningSpeedM = 20f;
        protected float angle;
        protected float seekCounter = 0.1f;
        public bool stopSeeking = false;
        protected int bounceCount = 5;
        protected bool startedSnap = false;
        protected float startingThrowX;
        protected float startingThrowY;
        protected Vector3 startingThrowVector;
        protected LayerMask ladderLayer;

        // Custom Trigger Variables
        public bool spawnedByTrigger = false;
        public bool permanentlyGrantShield = false;

        // Sounds
        public AudioClip[] shieldUnitBounce;

        protected override void Awake()
        {
            this.SpriteWidth = 16;
            this.SpriteHeight = 16;

            this.ladderLayer = 1 << LayerMask.NameToLayer( "Ladders" );

            this.shieldCollider = this.GetComponent<SphereCollider>();

            if ( this.DefaultSoundHolder == null )
            {
                this.DefaultSoundHolder = ( HeroController.GetHeroPrefab( HeroType.BroMax ) as BroMax ).boomerang.soundHolder;
            }

            this.soundVolume = 0.09f;

            base.Awake();
        }

        public override void PrefabSetup()
        {
            if ( shieldUnitBounce == null )
            {
                shieldUnitBounce = new AudioClip[3];
                shieldUnitBounce[0] = ResourcesController.GetAudioClip( SoundPath, "special1.wav" );
                shieldUnitBounce[1] = ResourcesController.GetAudioClip( SoundPath, "special2.wav" );
                shieldUnitBounce[2] = ResourcesController.GetAudioClip( SoundPath, "special3.wav" );
            }
        }

        public override void Fire( float x, float y, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            this.shieldSpeed = xI;
            if ( xI > 0f )
            {
                this.rotationSpeed *= -1f;
            }
            this.shieldCollider.transform.parent = base.transform.parent;

            //Calling projectile base fire
            //base.Fire( x, y, X, yI, _zOffset, playerNum, FiredBy);
            this.t = Time.deltaTime;
            this.damageInternal = this.damage;
            this.fullLife = this.life;
            this.fullDamage = this.damage;
            this.SetXY( x, y );
            this.xI = xI;
            this.yI = yI;

            // Init targetting variables
            this.speed = Mathf.Abs( this.xI );
            this.bounceCount = 5;

            this.originalSpeed = this.speed;
            if ( xI > 0f )
            {
                this.angle = 1.57079637f;
            }
            else
            {
                this.angle = -1.57079637f;
            }
            this.targetAngle = this.angle;
            this.originalAngle = this.angle;

            this.playerNum = playerNum;
            this.SetPosition();
            this.SetRotation();
            this.firedBy = FiredBy;
            Vector3 vector = new Vector3( xI, yI, 0f );
            this.startProjectileSpeed = vector.magnitude;
            if ( playerNum >= 0 && playerNum <= 3 )
            {
                ScaleProjectileWithPerks component = base.GetComponent<ScaleProjectileWithPerks>();
                component?.Setup( this );
            }
            this.CheckSpawnPoint();
            this.zOffset = _zOffset;
            this.CheckFriendlyFireMaterial();
            // End projectile base fire

            base.gameObject.AddComponent<AudioSource>();
            base.GetComponent<AudioSource>().playOnAwake = false;
            base.GetComponent<AudioSource>().rolloffMode = AudioRolloffMode.Linear;
            base.GetComponent<AudioSource>().minDistance = 100f;
            base.GetComponent<AudioSource>().dopplerLevel = 0.02f;
            base.GetComponent<AudioSource>().maxDistance = 220f;
            base.GetComponent<AudioSource>().spatialBlend = 1f;
            base.GetComponent<AudioSource>().volume = this.soundVolume;
            base.GetComponent<AudioSource>().loop = true;
            base.GetComponent<AudioSource>().clip = this.soundHolder.specialSounds[UnityEngine.Random.Range( 0, this.soundHolder.specialSounds.Length )];
            base.GetComponent<AudioSource>().Play();
            Sound.GetInstance().PlaySoundEffectAt( this.soundHolder.effortSounds, 0.4f, base.transform.position, 1.15f + UnityEngine.Random.value * 0.1f, true, false, false, 0f );
            this.xStart = x - Mathf.Sign( xI ) * 48f;
            this.lastXI = xI;
        }

        public void Setup( TestVanDammeAnim player, float currentSpecialCharge = 0f, bool enableBoomerang = true )
        {
            this.throwingPlayer = player;
            this.shouldBoomerang = enableBoomerang;

            this.shieldCharge = currentSpecialCharge;

            this.damageType = DamageType.Crush;

            this.damage = BaseDamage + (int)System.Math.Round( BaseDamage * currentSpecialCharge );

            this.damageInternal = this.damage;
            this.fullDamage = this.damage;
            this.bossDamage = 3 * this.damage + 4;

            this.knockbackXI = this.knockbackXI + ( this.knockbackXI * currentSpecialCharge );
            this.knockbackYI = this.knockbackYI + ( this.knockbackYI * currentSpecialCharge );

            if ( this.throwingPlayer != null )
            {
                this.startingThrowX = this.throwingPlayer.X;
                this.startingThrowY = this.throwingPlayer.Y;
                this.startingThrowVector = this.throwingPlayer.transform.position;
            }
            else
            {
                this.startingThrowX = 0f;
                this.startingThrowY = 0f;
                this.startingThrowVector = Vector3.zero;
            }

            base.transform.eulerAngles = new Vector3( 0f, 0f, 0f );

            this.enabled = true;

            this.returnTime += ChargeReturnScalar * this.shieldCharge;
        }

        protected override void CheckSpawnPoint()
        {
            Collider[] array = Physics.OverlapSphere( new Vector3( base.X, base.Y, 0f ), 5f, this.groundLayer );
            if ( array.Length > 0 )
            {
                this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                for ( int i = 0; i < array.Length; i++ )
                {
                    this.ProjectileApplyDamageToBlock( array[i].gameObject, this.damageInternal, this.damageType, this.xI, this.yI );
                }
                this.returnTime = 0f;
                this.xI = 0f;
            }
            this.RegisterProjectile();
            this.CheckReturnZones();
            if ( ( this.canReflect && this.playerNum >= 0 && this.horizontalProjectile && Physics.Raycast( new Vector3( base.X - Mathf.Sign( this.xI ) * this.projectileSize * 2f, base.Y, 0f ), new Vector3( this.xI, this.yI, 0f ), out this.raycastHit, this.projectileSize * 3f, this.barrierLayer ) ) || ( !this.horizontalProjectile && Physics.Raycast( new Vector3( base.X, base.Y, 0f ), new Vector3( this.xI, this.yI, 0f ), out this.raycastHit, this.projectileSize + this.startProjectileSpeed * this.t, this.barrierLayer ) ) )
            {
                this.ReflectProjectile( this.raycastHit );
            }
            else if ( ( this.canReflect && this.playerNum < 0 && this.horizontalProjectile && Physics.Raycast( new Vector3( base.X - Mathf.Sign( this.xI ) * this.projectileSize * 2f, base.Y, 0f ), new Vector3( this.xI, this.yI, 0f ), out this.raycastHit, this.projectileSize * 3f, this.friendlyBarrierLayer ) ) || ( !this.horizontalProjectile && Physics.Raycast( new Vector3( base.X, base.Y, 0f ), new Vector3( this.xI, this.yI, 0f ), out this.raycastHit, this.projectileSize + this.startProjectileSpeed * this.t, this.friendlyBarrierLayer ) ) )
            {
                this.ReflectProjectile( this.raycastHit );
            }
            this.CheckSpawnPointFragile();
        }

        protected override void RunProjectile( float t )
        {
            base.RunProjectile( t ); // Calls move projectile
            this.returnTime -= t;
            this.collectDelayTime -= t;

            if ( this.returnTime <= 0f && !this.dropping )
            {
                if ( this.Y < -50 )
                {
                    this.ReturnShield( this.firedBy );
                }
                if ( this.shouldBoomerang )
                {
                    if ( !this.hasReachedApex && this.speed < ( 0.5 * this.originalSpeed ) ) // Shield has slowed down enough to be considered at Apex
                    {
                        this.hasReachedApex = true;

                        this.seekTurningSpeedLerpM = 3f;
                        this.seekTurningSpeedM = 20f;
                        this.seekSpeedCurrent = this.seekTurningSpeedM / 10;

                        this.BeginReturnSeek();


                    }
                    else if ( this.hasReachedApex )
                    {
                        this.ReturnSeek();
                    }
                    this.CheckReturnShield();
                }
                else if ( !this.shouldBoomerang )
                {
                    this.StartDropping();
                }
                if ( this.shouldBoomerang && this.hasReachedApex ) // Check if shield should start snapping
                {
                    if ( !startedSnap && Vector3.Distance( this.transform.position, this.throwingPlayer.transform.position ) < 100 )
                    {
                        startedSnap = true;
                        this.seekTurningSpeedLerpM = 60f;
                        this.seekTurningSpeedM = 20f;
                    }
                }
            }
            if ( !this.dropping )
            {
                float num = 140f + Mathf.Abs( this.xI ) * 0.5f;
                if ( this.speed != 0f && Time.timeScale > 0f )
                {
                    float pitch = Mathf.Clamp( num / Mathf.Abs( this.speed ) * 1.2f * this.shieldLoopPitchM, 0.5f * this.shieldLoopPitchM, 1f * this.shieldLoopPitchM ) * Time.timeScale;
                    base.GetComponent<AudioSource>().pitch = pitch;
                }
                base.transform.Rotate( 0f, 0f, num * this.rotationSpeed * t, Space.Self );

                this.windCounter += t;
                if ( this.windCounter > 0.0667f )
                {
                    this.windCount++;
                    this.windCounter -= 0.0667f;
                    EffectsController.CreateBoomerangWindEffect( base.X, base.Y, 5f, 0f, 0f, base.transform, 0f, (float)( this.windCount * 27 ) * this.rotationSpeed );
                }
            }
            else
            {
                base.transform.Rotate( 0f, 0f, this.rotationSpeed * t, Space.Self );
                this.CheckReturnShield();
            }
            if ( Mathf.Sign( this.lastXI ) != Mathf.Sign( this.xI ) )
            {
                this.alreadyHit.Clear();
            }
            this.lastXI = this.xI;
        }

        // Handles figuring out where the Shield should return to
        protected void BeginReturnSeek()
        {
            this.foundMook = true;
            this.targetX = this.throwingPlayer.X;
            this.targetY = this.throwingPlayer.Y + throwingPlayer.height + 5;

            float y = this.targetX - base.X;
            float x = this.targetY - base.Y;
            this.targetAngle = global::Math.GetAngle( x, y );

            DetermineReturnAngle( this.throwingPlayer.transform.position );
        }

        protected void ReturnSeek()
        {
            CalculateSeek( this.throwingPlayer.X, this.throwingPlayer.Y );
        }

        // Check whether there are blocks above / below to determine whether shield needs to return in a straight line or if it can circle around
        protected void DetermineReturnAngle( Vector3 playerPos )
        {
            bool above = false, below = false;
            float distanceBelow, distanceAbove;
            // Check below shield
            if ( Physics.Raycast( new Vector3( base.X, base.Y, 0f ), Vector3.down, out this.raycastHit, 100f, this.groundLayer ) )
            {
                distanceBelow = Vector3.Distance( this.transform.position, this.raycastHit.point );
                below = distanceBelow < 100;
            }
            // Check above shield
            if ( Physics.Raycast( new Vector3( base.X, base.Y, 0f ), Vector3.up, out this.raycastHit, 100f, this.groundLayer ) )
            {
                distanceAbove = Vector3.Distance( this.transform.position, this.raycastHit.point );
                above = distanceAbove < 100;
            }

            // We check the terrain around the player in case the shield entered an area with no ground above or below
            if ( Physics.Raycast( playerPos, Vector3.down, out this.raycastHit, 100f, this.groundLayer ) )
            {
                distanceBelow = Vector3.Distance( playerPos, this.raycastHit.point );
                below = below || distanceBelow < 50;
            }
            if ( Physics.Raycast( playerPos, Vector3.down, out this.raycastHit, 100f, this.ladderLayer ) )
            {
                distanceBelow = Vector3.Distance( playerPos, this.raycastHit.point );
                below = below || distanceBelow < 50;
            }

            if ( Physics.Raycast( playerPos, Vector3.up, out this.raycastHit, 100f, this.groundLayer ) )
            {
                distanceAbove = Vector3.Distance( playerPos, this.raycastHit.point );
                if ( distanceAbove < 100 )
                {
                    above = below = true;
                }
            }
            if ( Physics.Raycast( playerPos, Vector3.up, out this.raycastHit, 100f, this.ladderLayer ) )
            {
                distanceAbove = Vector3.Distance( playerPos, this.raycastHit.point );
                if ( distanceAbove < 100 )
                {
                    above = below = true;
                }
            }

            // Blocks above and below, or path to player is blocked, or shield is already heading towards player, so return shield straight
            if ( below && above || ( Mathf.Sign( xI ) == Mathf.Sign( playerPos.x - this.X ) ) )
            {
                this.angle = this.targetAngle;
            }
            else if ( below ) // Blocks below return shield up
            {
                Vector3 currentPlayerPos = playerPos;
                currentPlayerPos.y += this.throwingPlayer.height + 2;
                Vector3 direction = currentPlayerPos - this.transform.position;

                // Check if anything is in the way between area above shield and player
                if ( Physics.Raycast( this.transform.position + new Vector3( 0, 30, 0 ), direction, out this.raycastHit, Vector3.Distance( this.transform.position, currentPlayerPos ), this.groundLayer ) )
                {
                    this.angle = this.targetAngle;
                }
                else
                {
                    if ( this.xI > 0 ) // Right
                    {
                        this.angle -= ( Mathf.PI / 4 );
                    }
                    else if ( this.xI < 0 ) // Left
                    {
                        this.angle += ( Mathf.PI / 4 );
                    }
                }
            }
            else if ( above ) // Blocks above return shield down
            {
                Vector3 currentPlayerPos = playerPos;
                currentPlayerPos.y += this.throwingPlayer.height + 2;
                Vector3 direction = currentPlayerPos - this.transform.position;

                // Check if anything is in the way between area below shield and player
                if ( Physics.Raycast( this.transform.position - new Vector3( 0, 30, 0 ), direction, out this.raycastHit, Vector3.Distance( this.transform.position, currentPlayerPos ), this.groundLayer ) )
                {
                    this.angle = this.targetAngle;
                }
                else
                {
                    if ( this.xI > 0 ) // Right
                    {
                        this.angle += ( Mathf.PI / 4 );
                    }
                    else if ( this.xI < 0 ) // Left
                    {
                        this.angle -= ( Mathf.PI / 4 );
                    }
                }

            }
        }

        // Called by RunProjectile Base method
        protected override void MoveProjectile()
        {
            if ( !this.stopSeeking && !this.dropping )
            {
                this.RunSeeking();
                if ( this.targetAngle > this.angle + 3.14159274f )
                {
                    this.angle += 6.28318548f;
                }
                else if ( this.targetAngle < this.angle - 3.14159274f )
                {
                    this.angle -= 6.28318548f;
                }
                if ( this.reversing )
                {
                    if ( this.IsHeldByZone() )
                    {
                        this.speed *= 1f - this.t * 8f;
                    }
                    else
                    {
                        this.speed = Mathf.Lerp( this.speed, this.originalSpeed, this.t * 8f );
                    }
                }
                else if ( ( this.returnTime >= 0f || this.hasReachedApex ) ) // If moving forward or towards player and hasn't reached return time
                {
                    this.speed = Mathf.Lerp( this.speed, this.originalSpeed, this.t * 10f );
                }
                else if ( this.shouldBoomerang )
                {
                    this.speed = Mathf.Lerp( this.speed, 0, this.t * 10f );
                }
                else
                {
                    this.speed *= 1f - this.t * 0.5f;
                }
                if ( !this.dropping )
                {
                    this.seekSpeedCurrent = Mathf.Lerp( this.seekSpeedCurrent, this.seekTurningSpeedM, this.seekTurningSpeedLerpM * this.t );
                    this.angle = Mathf.Lerp( this.angle, this.targetAngle, this.t * this.seekSpeedCurrent );
                    Vector2 vector = global::Math.Point2OnCircle( this.angle, this.speed );
                    this.xI = vector.x;
                    this.yI = vector.y;
                }
                else
                {
                    this.xI *= 1f - this.t * 0.3f;
                }
            }

            base.MoveProjectile();
            this.shieldCollider.transform.position = base.transform.position;
            if ( this.dropping )
            {
                // If the shield was spawned by a trigger, ensure it doesn't fall through the ground while offscreen
                if ( this.spawnedByTrigger && !SortOfFollow.IsItSortOfVisible( base.transform.position ) )
                {
                    this.xI = this.yI = 0;
                }
                else
                {
                    ApplyGravity();
                }
            }
        }

        protected void RunSeeking()
        {
            if ( !this.IsHeldByZone() )
            {
                this.seekCounter += this.t;
                if ( this.seekCounter > 0.1f )
                {
                    this.seekCounter -= 0.03f;
                    this.CalculateSeek();
                }
            }
        }

        protected void CalculateSeek( float manualTargetX, float manualTargetY )
        {
            this.foundMook = true;
            this.targetX = manualTargetX;
            this.targetY = manualTargetY + throwingPlayer.height + 2;

            float y = this.targetX - base.X;
            float x = this.targetY - base.Y;
            this.targetAngle = global::Math.GetAngle( x, y );
        }

        protected void CalculateSeek()
        {
            if ( !this.foundMook )
            {
                Unit nearestVisibleUnitDamagebleBy = Map.GetNearestVisibleUnitDamagebleBy( this.playerNum, (int)this.seekRange, base.X, base.Y, false );
                // Check that we found a unit, it hasn't already been hit, and it is in the direction the shield is traveling.
                if ( nearestVisibleUnitDamagebleBy != null && nearestVisibleUnitDamagebleBy.gameObject.activeInHierarchy && !this.alreadyHit.Contains( nearestVisibleUnitDamagebleBy ) && ( Mathf.Sign( nearestVisibleUnitDamagebleBy.X - this.X ) == Mathf.Sign( this.xI ) ) )
                {
                    this.foundMook = true;
                    this.targetX = nearestVisibleUnitDamagebleBy.X;
                    this.targetY = nearestVisibleUnitDamagebleBy.Y + throwingPlayer.height + 4;
                }
                else
                {
                    this.targetX = base.X + this.xI;
                    this.targetY = base.Y + this.yI;
                }
            }
            float y = this.targetX - base.X;
            float x = this.targetY - base.Y;
            this.targetAngle = global::Math.GetAngle( x, y );
        }

        protected override void HitProjectiles()
        {
            if ( Map.HitProjectiles( this.playerNum, this.damageInternal, this.damageType, this.projectileSize, base.X, base.Y, this.xI, this.yI, 0.1f ) )
            {
                this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
            }
        }

        protected override bool HitWalls()
        {
            if ( this.xI < 0f )
            {
                if ( Physics.Raycast( new Vector3( base.X + 4f, base.Y + 4f, 0f ), Vector3.left, out this.raycastHit, 6f + this.heightOffGround, this.groundLayer ) || Physics.Raycast( new Vector3( base.X + 4f, base.Y - 4f, 0f ), Vector3.left, out this.raycastHit, 6f + this.heightOffGround, this.groundLayer ) )
                {
                    this.collectDelayTime = 0f;
                    if ( Mathf.Abs( this.xI ) > Mathf.Abs( this.shieldSpeed ) * 0.33f && !this.hasReachedApex )
                    {
                        EffectsController.CreateSuddenSparkShower( this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f, this.sparkCount / 2, 2f, 40f + UnityEngine.Random.value * 80f, this.raycastHit.normal.x * ( 100f + UnityEngine.Random.value * 210f ), this.raycastHit.normal.y * 120f + ( -60f + UnityEngine.Random.value * 350f ), 0.2f );
                    }
                    float bounceMultiplier = this.dropping ? 0.3f : 1f;
                    this.xI *= -this.bounceXM * bounceMultiplier;
                    if ( this.raycastHit.collider.gameObject.GetComponent<Block>() != null && !this.hasReachedApex ) // Hit a block / wall
                    {
                        this.raycastHit.collider.gameObject.GetComponent<Block>().Damage( new DamageObject( 2, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this ) );

                        HitBlock( raycastHit );
                    }
                    else if ( this.raycastHit.collider.gameObject.GetComponent<BossBlockPiece>() != null && !this.hasReachedApex ) // Hit boss block
                    {
                        this.raycastHit.collider.gameObject.GetComponent<BossBlockPiece>().Damage( new DamageObject( this.bossDamage, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this ) );

                        HitBlock( raycastHit );
                    }
                    else if ( this.raycastHit.collider.gameObject.GetComponent<BossBlockWeapon>() != null && !this.hasReachedApex ) // Hit boss weapon
                    {
                        this.raycastHit.collider.gameObject.GetComponent<BossBlockWeapon>().Damage( new DamageObject( this.bossDamage, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this ) );

                        HitBlock( raycastHit );
                    }
                    else if ( !this.hasReachedApex ) // Hit helicopter / certain boss blocks
                    {
                        HitBlock( raycastHit );

                        this.ProjectileApplyDamageToBlock( raycastHit.collider.gameObject, this.bossDamage, this.damageType, this.xI, this.yI );
                    }
                    if ( !this.hasReachedApex )
                    {
                        this.PlayBounceSoundWall();
                    }
                }
            }
            else if ( this.xI > 0f && ( Physics.Raycast( new Vector3( base.X - 4f, base.Y + 4f, 0f ), Vector3.right, out this.raycastHit, 4f + this.heightOffGround, this.groundLayer ) || Physics.Raycast( new Vector3( base.X - 4f, base.Y - 4f, 0f ), Vector3.right, out this.raycastHit, 4f + this.heightOffGround, this.groundLayer ) ) )
            {
                this.collectDelayTime = 0f;
                if ( Mathf.Abs( this.xI ) > Mathf.Abs( this.shieldSpeed ) * 0.33f && !this.hasReachedApex )
                {
                    EffectsController.CreateSuddenSparkShower( this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f, this.sparkCount / 2, 2f, 40f + UnityEngine.Random.value * 80f, this.raycastHit.normal.x * ( 100f + UnityEngine.Random.value * 210f ), this.raycastHit.normal.y * 120f + ( -60f + UnityEngine.Random.value * 350f ), 0.2f );
                }
                this.xI *= -this.bounceXM;
                if ( this.raycastHit.collider.gameObject.GetComponent<Block>() != null && !this.hasReachedApex ) // Hit a block / wall
                {
                    this.raycastHit.collider.gameObject.GetComponent<Block>().Damage( new DamageObject( 2, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this ) );

                    HitBlock( raycastHit );
                }
                else if ( this.raycastHit.collider.gameObject.GetComponent<BossBlockPiece>() != null && !this.hasReachedApex ) // Hit boss block
                {
                    this.raycastHit.collider.gameObject.GetComponent<BossBlockPiece>().Damage( new DamageObject( this.bossDamage, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this ) );

                    HitBlock( raycastHit );
                }
                else if ( this.raycastHit.collider.gameObject.GetComponent<BossBlockWeapon>() != null && !this.hasReachedApex ) // Hit boss weapon
                {
                    this.raycastHit.collider.gameObject.GetComponent<BossBlockWeapon>().Damage( new DamageObject( this.bossDamage, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this ) );

                    HitBlock( raycastHit );
                }
                else if ( !this.hasReachedApex )
                {
                    HitBlock( raycastHit );

                    this.ProjectileApplyDamageToBlock( raycastHit.collider.gameObject, this.bossDamage, this.damageType, this.xI, this.yI );
                }
                if ( !this.hasReachedApex )
                {
                    this.PlayBounceSoundWall();
                }
            }
            if ( this.dropping )
            {
                if ( this.yI < 0f )
                {
                    if ( Physics.Raycast( new Vector3( base.X, base.Y + 6f, 0f ), Vector3.down, out this.raycastHit, 6f + this.heightOffGround - this.yI * this.t, this.groundLayer ) )
                    {
                        this.stopSeeking = true;

                        if ( this.raycastHit.collider.gameObject.GetComponent<Block>() != null && this.yI < -30f )
                        {
                            this.raycastHit.collider.gameObject.GetComponent<Block>().Damage( new DamageObject( 0, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this ) );
                        }
                        this.xI *= this.frictionM;
                        if ( this.yI < -40f )
                        {
                            float vertBounceMultiplier = this.dropping ? 0.4f : 1f;
                            this.yI *= -this.bounceYM * vertBounceMultiplier;
                        }
                        else
                        {
                            this.yI = 0f;
                            base.Y = this.raycastHit.point.y + this.heightOffGround;
                        }
                        this.rotationSpeed = groundRotationSpeed * this.xI;
                        this.PlayBounceSoundWall();
                    }
                }
                else if ( this.yI > 0f && Physics.Raycast( new Vector3( base.X, base.Y - 6f, 0f ), Vector3.up, out this.raycastHit, 6f + this.heightOffGround + this.yI * this.t, this.groundLayer ) )
                {
                    this.stopSeeking = true;
                    if ( this.raycastHit.collider.gameObject.GetComponent<Block>() != null )
                    {
                        this.raycastHit.collider.gameObject.GetComponent<Block>().Damage( new DamageObject( 0, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this ) );
                    }
                    float vertBounceMultiplier = this.dropping ? 0.4f : 1f;
                    this.yI *= -( this.bounceYM + 0.1f ) * vertBounceMultiplier;
                    this.PlayBounceSoundWall();
                    this.rotationSpeed = groundRotationSpeed * this.xI;
                }
            }
            return true;
        }

        protected void HitBlock( RaycastHit raycastHit )
        {
            // Calculate new angle
            Bounce( raycastHit );

            if ( this.returnTime > 0f ) // Hasn't started returning
            {
                this.returnTime = 0f;
            }

            --this.bounceCount;
        }

        // Fix AssMouthOrifice and other doodads destroying shield
        protected override void RunDamageBackground( float t )
        {
            if ( !this.damagedBackground )
            {
                this.damageBackgroundCounter += t;
                if ( this.damageBackgroundCounter > 0f )
                {
                    this.damageBackgroundCounter -= t * 2f;
                    if ( Map.DamageDoodads( this.damageInternal, this.damageType, base.X, base.Y, this.xI, this.yI, this.projectileSize, this.playerNum, out _, this ) )
                    {
                        this.damagedBackground = true;
                        EffectsController.CreateEffect( this.flickPuff, base.X, base.Y );
                    }
                    if ( this.affectScenery && Map.PassThroughScenery( base.X, base.Y, this.xI, this.yI ) )
                    {
                        this.damageBackgroundCounter -= 0.33f;
                    }
                }
            }
        }

        public void StartDropping()
        {
            this.dropping = true;
            this.collectDelayTime = 0f;
            this.rotationSpeed = groundRotationSpeed * this.xI;
            base.GetComponent<AudioSource>().Stop();
            this.shieldCollider.enabled = false;
            this.stopSeeking = true;
        }

        protected void PlayBounceSoundWall()
        {
            float num = Mathf.Abs( this.xI ) + Mathf.Abs( this.yI );
            if ( num > 33f )
            {
                float num2 = num / 210f;
                float num3 = 0.05f + Mathf.Clamp( num2 * num2, 0f, 0.25f );

                if ( this.dropping )
                {
                    float speedRatio = Mathf.Clamp01( num / 300f );
                    num3 *= speedRatio * 0.5f;
                }

                Sound.GetInstance().PlaySoundEffectAt( this.shieldUnitBounce, num3 * this.bounceVolumeM, base.transform.position, 1f, true, false, false, 0f );
            }
        }

        protected void PlayBounceSoundUnit()
        {
            float num = Mathf.Abs( this.xI ) + Mathf.Abs( this.yI );
            if ( num > 33f )
            {
                float num2 = num / 210f;
                float num3 = 0.05f + Mathf.Clamp( num2 * num2, 0f, 0.25f );

                if ( this.dropping )
                {
                    float speedRatio = Mathf.Clamp01( num / 300f );
                    num3 *= speedRatio * 0.5f;
                }

                //Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.hitSounds[CaptainAmeribro.hitSound], num3 * this.bounceVolumeM, base.transform.position, 1f, true, false, false, 0f);
                Sound.GetInstance().PlaySoundEffectAt( this.shieldUnitBounce, num3 * this.bounceVolumeM, base.transform.position, 1f, true, false, false, 0f );
            }
        }

        protected override void RunLife()
        {
        }

        protected override void Bounce( RaycastHit raycastHit )
        {
            if ( this.returnTime > 0f )
            {
                this.xI = 0f;
                this.returnTime = 0f;
            }

            Vector3 curAngle = new Vector3( Math.Cos( this.angle ), Math.Sin( this.angle ), 0 );
            Vector3 bounceAngle = ( curAngle - 2 * Vector3.Dot( curAngle, raycastHit.normal ) * raycastHit.normal );
            this.angle = Mathf.Atan( bounceAngle.y / bounceAngle.x );
            this.targetAngle = this.angle;
            this.speed = 0.9f * this.speed;
            Vector2 vector = global::Math.Point2OnCircle( this.angle, this.speed );
            this.xI = vector.x;
            this.yI = vector.y;
            this.foundMook = false;
        }

        protected override void HitUnits()
        {
            if ( this.dropping && Mathf.Abs( this.xI ) < 10f && Mathf.Abs( this.yI ) < 10f )
                return;

            if ( this.hitUnitsDelay > 0f )
            {
                this.hitUnitsDelay -= this.t;
            }
            else
            {
                if ( this.reversing || this.hasReachedApex )
                {
                    if ( Map.HitLivingUnits( this, this.playerNum, this.damageInternal, this.damageType, this.projectileSize, this.projectileSize, base.X, base.Y, Mathf.Sign( this.xI ) * this.knockbackXI, this.knockbackYI, true, true, true, false ) )
                    {
                        this.PlayBounceSoundUnit();
                        this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                        this.hitUnitsDelay = 0.0667f;

                        this.hitUnitsCount++;
                    }
                }
                else if ( Map.HitUnits( this.firedBy, this.playerNum, this.damageInternal, 1, this.damageType, this.projectileSize, this.projectileSize * 1.3f, base.X, base.Y, Mathf.Sign( this.xI ) * this.knockbackXI, this.knockbackYI, true, true, true, this.alreadyHit, false, true ) )
                {
                    this.PlayBounceSoundUnit();
                    this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                    this.hitUnitsDelay = 0.0667f;
                    this.hitUnitsCount++;

                    if ( !this.hasReachedApex )
                    {
                        this.foundMook = false;

                        this.angle = this.originalAngle;
                        this.targetAngle = this.originalAngle;
                    }
                }
            }
        }

        protected override void HitWildLife()
        {
        }

        protected void CheckReturnShield()
        {
            if ( this.collectDelayTime <= 0f || this.hitUnitsCount > 2 )
            {
                // If throwing player is still alive only return to them
                if ( this.firedBy != null && this.throwingPlayer.health > 0 )
                {
                    float f = this.firedBy.transform.position.x - base.X;
                    float f2 = this.firedBy.transform.position.y + 10f - base.Y;
                    if ( Mathf.Abs( f ) < 9f && Mathf.Abs( f2 ) < 14f )
                    {
                        if ( this != null )
                        {
                            this.ReturnShield( this.firedBy );
                        }
                    }
                }
                // Return to any player
                else
                {
                    for ( int i = 0; i < HeroController.players.Length; ++i )
                    {
                        if ( HeroController.players[i] != null && HeroController.players[i].IsAliveAndSpawnedHero() )
                        {
                            TestVanDammeAnim character = HeroController.players[i].character;
                            float f = character.transform.position.x - base.X;
                            float f2 = character.transform.position.y + 10f - base.Y;
                            if ( Mathf.Abs( f ) < 9f && Mathf.Abs( f2 ) < 14f )
                            {
                                if ( this != null )
                                {
                                    this.ReturnShield( character );
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ReturnShield( MonoBehaviour receiver )
        {
            if ( receiver is CaptainAmeribro captainAmeribro )
            {
                // Only play return animation if finished startup and sprites have loaded
                if ( captainAmeribro.finishedStartup )
                {
                    captainAmeribro.ReturnShield( this );
                }
                else
                {
                    captainAmeribro.caughtShieldFromPrevious = true;
                    this.ReturnShieldSilent();
                }
            }
            else if ( receiver is TestVanDammeAnim character )
            {
                CustomPockettedSpecial.AddPockettedSpecial( character, new PockettedShield() );
            }
            if ( this.dropping )
            {
                EffectsController.CreatePuffDisappearEffect( base.X, base.Y + 2f, 0f, 0f );
            }

            // If spawned by trigger, grant 
            if ( this.spawnedByTrigger && this.permanentlyGrantShield )
            {
                RocketLib.CustomTriggers.CustomTriggerStateManager.SetDuringLevel( "Captain Ameribro Grant Shield", true );
            }

            Sound.GetInstance().PlaySoundEffectAt( this.soundHolder.powerUp, 0.7f, base.transform.position, 0.95f + UnityEngine.Random.value * 0.1f, true, false, false, 0f );
            this.DeregisterProjectile();
            UnityEngine.Object.Destroy( base.gameObject );
        }

        public void ReturnShieldSilent()
        {
            this.DeregisterProjectile();
            UnityEngine.Object.Destroy( base.gameObject );
        }

        protected virtual void ApplyGravity()
        {
            this.yI -= 600f * this.t;
        }

        public override void Death()
        {
        }
    }
}
