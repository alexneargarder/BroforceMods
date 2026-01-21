using System;
using System.IO;
using System.Reflection;
using BroMakerLib;
using RocketLib;
using Rogueforce;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Furibrosa
{
    public class Bolt : PredabroSpear
    {
        public SpriteSM sprite;
        public bool explosive = false;
        public float explosiveTimer = 0;
        public float range = 48;
        public AudioClip[] explosionSounds;
        protected bool isStuckToUnit = false;
        protected Unit stuckToUnit = null;

        protected override void Awake()
        {
            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );

            canMakeEffectsMoreThanOnce = true;
            stunOnHit = false;
            groundLayer = ( 1 << LayerMask.NameToLayer( "Ground" ) | 1 << LayerMask.NameToLayer( "LargeObjects" ) | 1 << LayerMask.NameToLayer( "FLUI" ) );
            barrierLayer = ( 1 << LayerMask.NameToLayer( "MobileBarriers" ) | 1 << LayerMask.NameToLayer( "IndestructibleGround" ) );
            friendlyBarrierLayer = 1 << LayerMask.NameToLayer( "FriendlyBarriers" );
            fragileLayer = 1 << LayerMask.NameToLayer( "DirtyHippie" );
            zOffset = ( 1f - Random.value * 2f ) * 0.04f;
            random = new Randomf( Random.Range( 0, 10000 ) );

            if ( explosive )
            {
                explosionSounds = new AudioClip[2];
                explosionSounds[0] = ResourcesController.GetAudioClip( Path.Combine( directoryPath, "sounds" ), "explosion1.wav" );
                explosionSounds[1] = ResourcesController.GetAudioClip( Path.Combine( directoryPath, "sounds" ), "explosion2.wav" );
            }
        }

        private void OnDisable()
        {
            if ( stuckInPlace )
            {
                Death();
            }
        }

        public void Setup( bool isExplosive )
        {
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            Material material;
            if ( isExplosive )
            {
                material = ResourcesController.GetMaterial( directoryPath, "boltExplosive.png" );
            }
            else
            {
                material = ResourcesController.GetMaterial( directoryPath, "bolt.png" );
            }

            renderer.material = material;

            sprite = gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2( -3, 16 );
            sprite.pixelDimensions = new Vector2( 17, 16 );

            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 17;
            sprite.height = 16;
            sprite.offset = new Vector3( -10.5f, 0f, 10.82f );

            // Setup collider
            BoxCollider collider = gameObject.GetComponent<BoxCollider>();
            collider.center = new Vector3( 0f, 0.5f, 8f );
            collider.size = new Vector3( 26f, 2f, 0f );

            // Setup Variables
            explosive = isExplosive;
            if ( explosive )
            {
                groundLayersCrushLeft = 1;
                maxPenetrations = 2;
                life = 0.42f;
            }
            else
            {
                groundLayersCrushLeft = 1;
                maxPenetrations = 2;
                life = 0.46f;
            }

            unimpalementDamage = 8;
            trailDist = 12;
            stunOnHit = false;
            rotationSpeed = 0;
            dragUnitsSpeedM = 0.9f;
            projectileSize = 8;
            damage = damageInternal = fullDamage = Furibrosa.crossbowDamage;
            fadeDamage = false;
            damageType = DamageType.Bullet;
            canHitGrenades = true;
            affectScenery = true;
            horizontalProjectile = true;
            isWideProjectile = false;
            canReflect = true;
            canMakeEffectsMoreThanOnce = true;

            // Setup platform collider
            platformCollider = new GameObject( "BoltCollider", new Type[] { typeof( BoxCollider ) } ).GetComponent<BoxCollider>();
            platformCollider.enabled = false;
            platformCollider.transform.parent = transform;
            platformCollider = this.FindChildOfName( "BoltCollider" ).gameObject.GetComponent<BoxCollider>();
            platformCollider.material = new PhysicMaterial();
            platformCollider.material.dynamicFriction = 0.6f;
            platformCollider.material.staticFriction = 0.6f;
            platformCollider.material.bounciness = 0f;
            platformCollider.material.frictionCombine = PhysicMaterialCombine.Average;
            platformCollider.material.bounceCombine = PhysicMaterialCombine.Average;
            platformCollider.gameObject.layer = 15;
            ( platformCollider as BoxCollider ).size = new Vector3( 11f, 1f, 1f );

            // Setup foreground sprite
            GameObject foreground = new GameObject( "BoltForeground", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } );
            foreground.transform.parent = transform;
            foreground.GetComponent<MeshRenderer>().material = material;
            SpriteSM foregroundSprite = foreground.GetComponent<SpriteSM>();
            foregroundSprite.lowerLeftPixel = new Vector2( 14, 16 );
            foregroundSprite.pixelDimensions = new Vector2( 14, 16 );
            foregroundSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            foregroundSprite.width = 14;
            foregroundSprite.height = 16;
            foregroundSprite.offset = new Vector3( -1f, 0f, 0f );
            foreground.transform.localPosition = new Vector3( 5f, 0f, -0.62f );
        }

        public override void Fire( float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            gameObject.SetActive( true );
            base.Fire( newX, newY, xI, yI, _zOffset, playerNum, FiredBy );
        }

        // Don't destroy projectile
        protected override void CheckSpawnPoint()
        {
            CheckWallsAtSpawnPoint();
            Map.DamageDoodads( damageInternal, damageType, X, Y, xI, yI, projectileSize, playerNum, out _, this );
            RegisterProjectile();
            CheckReturnZones();
            if ( ( canReflect && playerNum >= 0 && horizontalProjectile && Physics.Raycast( new Vector3( X - Mathf.Sign( xI ) * projectileSize * 2f, Y, 0f ), new Vector3( xI, yI, 0f ), out raycastHit, projectileSize * 3f, barrierLayer ) ) || ( !horizontalProjectile && Physics.Raycast( new Vector3( X, Y, 0f ), new Vector3( xI, yI, 0f ), out raycastHit, projectileSize + startProjectileSpeed * t, barrierLayer ) ) )
            {
                ReflectProjectile( raycastHit );
            }
            else if ( ( canReflect && playerNum < 0 && horizontalProjectile && Physics.Raycast( new Vector3( X - Mathf.Sign( xI ) * projectileSize * 2f, Y, 0f ), new Vector3( xI, yI, 0f ), out raycastHit, projectileSize * 3f, friendlyBarrierLayer ) ) || ( !horizontalProjectile && Physics.Raycast( new Vector3( X, Y, 0f ), new Vector3( xI, yI, 0f ), out raycastHit, projectileSize + startProjectileSpeed * t, friendlyBarrierLayer ) ) )
            {
                playerNum = 5;
                firedBy = null;
                ReflectProjectile( raycastHit );
            }
            else
            {
                TryHitUnitsAtSpawn();
            }

            CheckSpawnPointFragile();
        }

        // Don't destroy projectile
        protected override bool CheckWallsAtSpawnPoint()
        {
            Collider[] array = Physics.OverlapSphere( new Vector3( X, Y, 0f ), 5f, groundLayer );
            bool flag = false;
            if ( array.Length > 0 )
            {
                for ( int i = 0; i < array.Length; i++ )
                {
                    Collider y = null;
                    if ( firedBy != null )
                    {
                        y = firedBy.GetComponent<Collider>();
                    }

                    if ( firedBy == null || array[i] != y )
                    {
                        ProjectileApplyDamageToBlock( array[i].gameObject, damageInternal, damageType, xI, yI );
                        flag = true;
                    }
                }
            }

            return flag;
        }

        protected override void Update()
        {
            // Count down to explosion
            if ( stuckInPlace && explosive )
            {
                explosiveTimer += t;
                if ( explosiveTimer > 0.2f )
                {
                    Death();
                }
            }

            // Destroy bolt if it was stuck to a now dead or destroyed unit
            if ( isStuckToUnit && ( stuckToUnit == null || stuckToUnit.health <= 0 ) )
            {
                Death();
            }

            base.Update();
        }

        public void DestroyNearBySpears( Vector3 around )
        {
            Collider[] array = Physics.OverlapSphere( around, 9f, Map.unitLayer );
            foreach ( Collider collider in array )
            {
                PredabroSpear componentInChildren = collider.GetComponentInChildren<PredabroSpear>();
                if ( componentInChildren != null && componentInChildren != this )
                {
                    componentInChildren.Death();
                }
            }
        }

        public void StickToUnit( Transform trans, Vector3 point, Unit stuckUnit )
        {
            foreach ( Unit unit in hitUnits )
            {
                unit.Damage( unit.health, DamageType.Spikes, xI, yI, ( int )Mathf.Sign( xI ), firedBy, X, Y );
            }

            stuckInPlace = true;
            superMachete = false;
            isStuckToUnit = true;
            stuckToUnit = stuckUnit;
            transform.parent = trans;
            X = point.x - Mathf.Sign( xI ) * hitGroundOffset;
            SetPosition();
            life = float.PositiveInfinity;
            trans.SendMessage( "AttachMe", transform );
            platformCollider.enabled = true;
            DestroyNearBySpears( point );
        }

        protected override void HitUnits()
        {
            if ( penetrationsCount < maxPenetrations )
            {
                Unit firstUnit = Map.GetFirstUnit( firedBy, playerNum, 5f, X, Y, true, true, hitUnits );
                if ( firstUnit != null )
                {
                    if ( firstUnit.IsHeavy() )
                    {
                        firstUnit.Damage( damageInternal, DamageType.Melee, xI, yI, ( int )Mathf.Sign( xI ), firedBy, X, Y );
                        MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                        Sound.GetInstance().PlaySoundEffectAt( soundHolder.specialSounds, 0.4f, transform.position, 1f, true, false, false, 0f );
                        if ( !( firstUnit is DolphLundrenSoldier ) )
                        {
                            StickToUnit( firstUnit.transform, transform.position - new Vector3( 7f * transform.localScale.x, 0f, 0f ), firstUnit );
                        }
                        else
                        {
                            Destroy( gameObject );
                        }
                    }
                    else
                    {
                        hitUnits.Add( firstUnit );
                        if ( stunOnHit )
                        {
                            firstUnit.Blind();
                        }

                        firstUnit.Impale( transform, Vector3.right * Mathf.Sign( xI ), damageInternal, xI, yI, 0f, 0f );
                        firstUnit.Y = Y - 8f;
                        if ( firstUnit is Mook )
                        {
                            firstUnit.useImpaledFrames = true;
                        }

                        Sound.GetInstance().PlaySoundEffectAt( soundHolder.specialSounds, 0.5f, transform.position, 1f, true, false, false, 0f );
                        penetrationsCount++;
                        // Only add life to non-explosive bolts
                        if ( !explosive )
                        {
                            life += 0.15f;
                        }

                        SortOfFollow.Shake( 0.1f );
                        EffectsController.CreateBloodParticles( firstUnit.bloodColor, X, Y, 4, 4f, 5f, 60f, xI * 0.2f, yI * 0.5f + 40f );
                        EffectsController.CreateMeleeStrikeEffect( X, Y, xI * 0.2f, yI * 0.5f + 24f );
                        if ( xI > 0f )
                        {
                            if ( firstUnit.X < X + 3f )
                            {
                                firstUnit.X = X + 3f;
                            }
                        }
                        else if ( xI < 0f && firstUnit.X > X - 3f )
                        {
                            firstUnit.X = X - 3f;
                        }
                    }
                }
            }
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( explosive )
            {
                Explode();
            }
            else
            {
                base.MakeEffects( particles, x, y, useRayCast, hitNormal, hitPoint );
            }
        }

        protected void Explode()
        {
            MapController.DamageGround( firedBy, ValueOrchestrator.GetModifiedDamage( damage, playerNum ), DamageType.Explosion, range, X, Y, null, false );
            EffectsController.CreateExplosionRangePop( X, Y, -1f, range * 2f );
            Map.ExplodeUnits( firedBy, 12, DamageType.Explosion, range * 1.3f, range, X, Y, 50f, 400f, playerNum, false, true, true );
            EffectsController.CreateExplosionRangePop( X, Y, -1f, range * 2f );
            EffectsController.CreateExplosion( X, Y, range * 0.25f, range * 0.25f, 120f, 1f, range * 1.5f, 1f, 0f, true );
            if ( sound == null )
            {
                sound = Sound.GetInstance();
            }

            if ( sound != null )
            {
                sound.PlaySoundEffectAt( explosionSounds, 0.7f, transform.position, 1f, true, false, false, 0f );
            }

            bool flag;
            Map.DamageDoodads( damage, DamageType.Explosion, X, Y, 0f, 0f, range, playerNum, out flag, null );
        }
    }
}