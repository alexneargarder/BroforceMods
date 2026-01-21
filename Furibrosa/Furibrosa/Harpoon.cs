using System;
using System.IO;
using System.Reflection;
using BroMakerLib;
using RocketLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Furibrosa
{
    public class Harpoon : PredabroSpear
    {
        public SpriteSM sprite;

        protected override void Awake()
        {
            canMakeEffectsMoreThanOnce = true;
            stunOnHit = false;
            groundLayer = ( 1 << LayerMask.NameToLayer( "Ground" ) | 1 << LayerMask.NameToLayer( "LargeObjects" ) | 1 << LayerMask.NameToLayer( "FLUI" ) );
            barrierLayer = ( 1 << LayerMask.NameToLayer( "MobileBarriers" ) | 1 << LayerMask.NameToLayer( "IndestructibleGround" ) );
            friendlyBarrierLayer = 1 << LayerMask.NameToLayer( "FriendlyBarriers" );
            fragileLayer = 1 << LayerMask.NameToLayer( "DirtyHippie" );
            zOffset = ( 1f - Random.value * 2f ) * 0.04f;
            random = new Randomf( Random.Range( 0, 10000 ) );
        }

        private void OnDisable()
        {
            if ( stuckInPlace )
            {
                Death();
            }
        }

        public void Setup()
        {
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            Material material = ResourcesController.GetMaterial( directoryPath, "harpoon.png" );

            renderer.material = material;

            sprite = gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2( -3, 10 );
            sprite.pixelDimensions = new Vector2( 17, 3 );

            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 17;
            sprite.height = 5;
            sprite.offset = new Vector3( -10.5f, 0f, 10.82f );

            // Setup collider
            BoxCollider collider = gameObject.GetComponent<BoxCollider>();
            collider.center = new Vector3( 0f, 0.5f, 8f );
            collider.size = new Vector3( 26f, 2f, 0f );

            // Setup Variables
            groundLayersCrushLeft = 5;
            maxPenetrations = 8;
            life = 0.44f;
            unimpalementDamage = 8;
            trailDist = 12;
            stunOnHit = false;
            rotationSpeed = 0;
            dragUnitsSpeedM = 0.9f;
            projectileSize = 14;
            damage = 30;
            damageInternal = 30;
            fullDamage = 30;
            fadeDamage = false;
            damageType = DamageType.Bullet;
            canHitGrenades = true;
            affectScenery = true;
            horizontalProjectile = true;
            isWideProjectile = false;
            canReflect = true;
            canMakeEffectsMoreThanOnce = true;

            // Setup platform collider
            platformCollider = new GameObject( "HarpoonCollider", new Type[] { typeof( BoxCollider ) } ).GetComponent<BoxCollider>();
            platformCollider.enabled = false;
            platformCollider.transform.parent = transform;
            platformCollider.material = new PhysicMaterial();
            platformCollider.material.dynamicFriction = 0.6f;
            platformCollider.material.staticFriction = 0.6f;
            platformCollider.material.bounciness = 0f;
            platformCollider.material.frictionCombine = PhysicMaterialCombine.Average;
            platformCollider.material.bounceCombine = PhysicMaterialCombine.Average;
            platformCollider.gameObject.layer = 15;
            ( platformCollider as BoxCollider ).center = new Vector3( 0f, 2f, 0f );
            ( platformCollider as BoxCollider ).size = new Vector3( 20f, 1f, 1f );

            // Setup foreground sprite
            GameObject foreground = new GameObject( "HarpoonForeground", new Type[] { typeof( MeshFilter ), typeof( MeshRenderer ), typeof( SpriteSM ) } );
            foreground.transform.parent = transform;
            foreground.GetComponent<MeshRenderer>().material = material;
            SpriteSM foregroundSprite = foreground.GetComponent<SpriteSM>();
            foregroundSprite.lowerLeftPixel = new Vector2( 14, 10 );
            foregroundSprite.pixelDimensions = new Vector2( 20, 3 );
            foregroundSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            foregroundSprite.width = 20;
            foregroundSprite.height = 5;
            foregroundSprite.offset = new Vector3( -1f, 0f, 0f );
            foreground.transform.localPosition = new Vector3( 9f, 0f, -0.62f );
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
            base.Update();
        }

        protected override void HitUnits()
        {
            if ( penetrationsCount < maxPenetrations )
            {
                Unit firstUnit = Map.GetFirstUnit( firedBy, playerNum, 10f, X, Y, true, true, hitUnits );
                if ( firstUnit != null )
                {
                    if ( firstUnit.IsHeavy() )
                    {
                        hitUnits.Add( firstUnit );
                        firstUnit.Damage( damageInternal, DamageType.Melee, xI, yI, ( int )Mathf.Sign( xI ), firedBy, X, Y );
                        MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                        Sound.GetInstance().PlaySoundEffectAt( soundHolder.specialSounds, 0.4f, transform.position, 1f, true, false, false, 0f );
                        // Don't pierce through bosses
                        if ( BroMakerUtilities.IsBoss( firstUnit ) )
                        {
                            Death();
                        }
                    }
                    else
                    {
                        hitUnits.Add( firstUnit );
                        if ( stunOnHit )
                        {
                            firstUnit.Blind();
                        }

                        firstUnit.Impale( transform, Vector3.right * Mathf.Sign( xI ), damageInternal, xI, yI, 0f, 1f );
                        firstUnit.Y = Y - 8f;
                        if ( firstUnit is Mook )
                        {
                            firstUnit.useImpaledFrames = true;
                        }

                        Sound.GetInstance().PlaySoundEffectAt( soundHolder.specialSounds, 0.5f, transform.position, 1f, true, false, false, 0f );
                        penetrationsCount++;
                        SortOfFollow.Shake( 0.3f );
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

        protected override void Bounce( RaycastHit raycastHit )
        {
            // Ensure we don't keep applying damage to boss blocks
            if ( groundLayersCrushLeft > 0 && raycastHit.collider.gameObject.HasComponent<DamageRelay>() )
            {
                groundLayersCrushLeft = 1;
                damage = 45;
            }

            base.Bounce( raycastHit );
            damage = 30;
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( hasMadeEffects && !canMakeEffectsMoreThanOnce )
            {
                return;
            }

            if ( useRayCast )
            {
                if ( particles )
                {
                    EffectsController.CreateSparkShower( hitPoint.x + hitNormal.x * 3f, hitPoint.y + hitNormal.y * 3f, sparkCount, 2f, 60f, hitNormal.x * 60f, hitNormal.y * 30f, 0.2f, 0f );
                }

                EffectsController.CreateProjectilePopEffect( hitPoint.x + hitNormal.x * 3f, hitPoint.y + hitNormal.y * 3f );
            }
            else
            {
                if ( particles )
                {
                    EffectsController.CreateSparkShower( x, y, 10, 2f, 60f, -xI * 0.2f, 35f, 0.2f, 0f );
                    EffectsController.CreateShrapnel( shrapnel, x, y, 5f, 20f, 6f, 0f, 0f );
                }

                EffectsController.CreateProjectilePopEffect( x, y );
            }

            if ( !particles )
            {
                bool flag;
                Map.DamageDoodads( damageInternal, damageType, x, y, xI, yI, 8f, playerNum, out flag, this );
            }

            hasMadeEffects = true;
        }
    }
}