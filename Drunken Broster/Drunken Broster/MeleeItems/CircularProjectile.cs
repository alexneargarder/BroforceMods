using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using RocketLib;
using Rogueforce;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class CircularProjectile : CustomGrenade
    {
        protected List<Unit> alreadyHitUnits = new List<Unit>();
        protected float hitDelay;
        protected float damageCooldown;
        protected RaycastHit raycastHit;
        protected LayerMask groundAndLadderLayer;
        protected int bounceGroundDamage = 10;
        protected float hitUnitForce = 1.0f;
        public AudioClip[] bounceSounds;
        public AudioClip[] hitSounds;

        protected override void Awake()
        {
            size = 8f;
            life = 1e6f;
            trailType = TrailType.None;
            shrink = false;
            shootable = true;
            drag = 0.45f;
            destroyInsideWalls = false;
            rotateAtRightAngles = false;
            rotationSpeedMultiplier = 5f;
            damage = 3;
            damageType = DamageType.Crush;
            health = 15;
            bounceOffEnemies = true;
            weight = 2f;
            friendlyFire = false;
            bounceM = 1.0f;
            bounceOffEnemies = false;
            zOffset = 0.5f;

            // Allow this grenade to be hit by bullets and other attacks
            ShootableCircularDoodad doodad = this.GetOrAddComponent<ShootableCircularDoodad>();
            doodad.radius = 6f;
            doodad.owner = this;

            // Make tire roll over ladders
            groundAndLadderLayer = ( 1 << LayerMask.NameToLayer( "Ground" ) | 1 << LayerMask.NameToLayer( "IndestructibleGround" ) | 1 << LayerMask.NameToLayer( "LargeObjects" ) );

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            hitSounds = ResourcesController.GetAudioClipArray( SoundPath, "meleeHitBlunt", 2 );
        }

        public override void Launch( float newX, float newY, float xI, float yI )
        {
            base.Launch( newX, newY, xI, yI );
            rI = this.xI * -1f * rotationSpeedMultiplier;
            life = startLife = 1e6f;
        }

        // Don't register grenade so it can't be thrown back
        protected override void RegisterGrenade()
        {
        }

        // Disable warnings
        protected override void RunWarnings()
        {
        }

        protected override bool Update()
        {
            rI = xI * -1f * rotationSpeedMultiplier;

            if ( hitDelay > 0f )
            {
                hitDelay -= t;
            }
            else
            {
                HitUnits();
            }

            if ( damageCooldown > 0f )
            {
                damageCooldown -= t;
            }

            return base.Update();
        }

        protected override void CheckWallCollisions( ref bool bounceY, ref bool bounceX, ref float yIT, ref float xIT )
        {
            // Use sphere-based collision instead of point-based
            if ( ConstrainToBlocksWithSphere( X, Y, size, ref xIT, ref yIT, ref bounceX, ref bounceY, groundAndLadderLayer ) )
            {
                Bounce( bounceX, bounceY );
            }

            // Parent to moving platforms
            parentedCollider = null;
            if ( moveWithRestingTransform && Physics.Raycast( transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit raycastHit, size + 2f, Map.groundLayer ) && raycastHit.distance - 2f <= size )
            {
                parentedCollider = raycastHit.collider;
                Y += size - ( raycastHit.distance - 2f );
                parentedPosition = parentedCollider.transform.position;
                if ( yI < 0f )
                {
                    Bounce( false, true );
                }
            }
        }

        private bool ConstrainToBlocksWithSphere( float x, float y, float size, ref float xIT, ref float yIT, ref bool bounceX, ref bool bounceY, LayerMask floorLayer )
        {
            bool hitSomething = false;

            // Skip if no movement
            if ( xIT == 0f && yIT == 0f )
            {
                return false;
            }

            // Calculate desired position after movement
            float newX = x + xIT;
            float newY = y + yIT;

            // Check if the new position would overlap with anything
            Collider[] overlaps = Physics.OverlapSphere( new Vector3( newX, newY, 0f ), size, floorLayer );

            foreach ( Collider hit in overlaps )
            {
                // Handle ladder collisions specially
                if ( hit.gameObject.layer == LayerMask.NameToLayer( "Ladders" ) )
                {
                    // Ladders only block downward movement
                    if ( yIT < 0f )
                    {
                        yIT = 0f;
                        bounceY = true;
                        hitSomething = true;
                    }
                }
                else
                {
                    // Regular collision - need to figure out which direction to bounce
                    Bounds bounds = hit.bounds;

                    // Calculate how much we're overlapping in each direction
                    float leftOverlap = ( newX + size ) - ( bounds.min.x );
                    float rightOverlap = ( bounds.max.x ) - ( newX - size );
                    float bottomOverlap = ( newY + size ) - ( bounds.min.y );
                    float topOverlap = ( bounds.max.y ) - ( newY - size );

                    // Only process if we're actually overlapping
                    if ( leftOverlap > 0 && rightOverlap > 0 && bottomOverlap > 0 && topOverlap > 0 )
                    {
                        // Find the smallest overlap to determine bounce direction
                        float minOverlap = Mathf.Min( Mathf.Min( leftOverlap, rightOverlap ), Mathf.Min( bottomOverlap, topOverlap ) );

                        if ( minOverlap == leftOverlap && xIT > 0 )
                        {
                            xIT = -leftOverlap;
                            bounceX = true;
                        }
                        else if ( minOverlap == rightOverlap && xIT < 0 )
                        {
                            xIT = rightOverlap;
                            bounceX = true;
                        }
                        else if ( minOverlap == bottomOverlap && yIT > 0 )
                        {
                            yIT = -bottomOverlap;
                            bounceY = true;
                        }
                        else if ( minOverlap == topOverlap && yIT < 0 )
                        {
                            yIT = topOverlap;
                            bounceY = true;
                        }

                        hitSomething = true;
                    }
                }
            }

            return hitSomething;
        }

        protected virtual void HitUnits()
        {
            if ( Mathf.Abs( xI ) > 80f && Map.HitUnits( this, playerNum, damage, damage, damageType, size, size + 4f, X, Y, xI * 2f * hitUnitForce, Mathf.Abs( xI * 2.5f ) * hitUnitForce, true, true, false, alreadyHitUnits, false, false ) )
            {
                PlayHitSound();
                hitDelay = 0.1f;
                if ( bounceOffEnemies )
                {
                    float previousBounceM = bounceM;
                    bounceM *= 0.5f;
                    Bounce( true, false );
                    bounceM = previousBounceM;
                }
            }
        }

        protected virtual void PlayHitSound()
        {
            if ( sound == null )
            {
                sound = Sound.GetInstance();
            }

            float volume = Mathf.Max( 0.5f * ( ( Mathf.Abs( xI ) + Mathf.Abs( yI ) ) / 250f ), 0.2f );

            sound?.PlaySoundEffectAt( hitSounds, volume, transform.position );
        }

        protected override void HitFragile()
        {
            Vector3 vector = new Vector3( xI, yI, 0f );
            Vector3 normalized = vector.normalized;
            Collider[] array = Physics.OverlapSphere( new Vector3( X + normalized.x * 2f, Y + normalized.y * 2f, 0f ), 2f, fragileLayer );
            foreach (Collider t1 in array)
            {
                EffectsController.CreateProjectilePuff( X, Y );
                // Hit all fragile (including doors)
                t1.gameObject.SendMessage( "Damage", new DamageObject( 1, damageType, xI, yI, X, Y, this ), SendMessageOptions.DontRequireReceiver );
            }
        }

        public virtual bool Damage( DamageObject damageObject )
        {
            if ( damageCooldown <= 0f )
            {
                health -= damageObject.damage;
                Knock( damageObject.x, damageObject.y, damageObject.xForce, damageObject.yForce );
                damageCooldown = 0.1f;
                if ( health <= 0 )
                {
                    Death();
                }
            }
            return true;
        }

        public override void Knock( float xDiff, float yDiff, float xI, float yI )
        {
            // Boost impact if hitting it in the opposite direction to make turning it around easier
            if ( Mathf.Sign( this.xI ) != Mathf.Sign( xI ) )
            {
                xI *= 1.5f;
            }
            this.xI += Mathf.Sign( xI ) * Mathf.Max( Mathf.Min( Mathf.Abs( ( xI / weight ) ), 300f ), 100f );
            this.yI += Mathf.Sign( yI ) * Mathf.Max( Mathf.Min( Mathf.Abs( ( yI / weight ) ), 300f ), 0f );
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            if ( bounceX )
            {
                // Try to hit ground if moving fast enough
                // Center
                if ( Mathf.Abs( xI ) > 100f && Physics.Raycast( new Vector3( X, Y, 0f ), new Vector3( Mathf.Sign( xI ), 0f ), out raycastHit, size + 3f, Map.groundAndDamageableObjects ) && raycastHit.distance < size + 2f )
                {
                    ProjectileApplyDamageToBlock( raycastHit.collider.gameObject, bounceGroundDamage, damageType, xI, yI );
                }
                // Down
                else if ( Mathf.Abs( xI ) > 100f && Physics.Raycast( new Vector3( X, Y + 4f, 0f ), new Vector3( Mathf.Sign( xI ), 0f ), out raycastHit, size + 3f, Map.groundAndDamageableObjects ) && raycastHit.distance < size + 2f )
                {
                    ProjectileApplyDamageToBlock( raycastHit.collider.gameObject, bounceGroundDamage, damageType, xI, yI );
                }
                // Up
                else if ( Mathf.Abs( xI ) > 100f && Physics.Raycast( new Vector3( X, Y - 4f, 0f ), new Vector3( Mathf.Sign( xI ), 0f ), out raycastHit, size + 3f, Map.groundAndDamageableObjects ) && raycastHit.distance < size + 2f )
                {
                    ProjectileApplyDamageToBlock( raycastHit.collider.gameObject, bounceGroundDamage, damageType, xI, yI );
                }

                PlayBounceSound( bounceX, bounceY );

                xI *= -0.8f * bounceM;
                rI *= -1f;
                alreadyHitUnits.Clear();
            }
            if ( bounceY )
            {
                PlayBounceSound( bounceX, bounceY );
                yI *= -0.6f * bounceM;
            }
        }

        protected virtual void PlayBounceSound( bool bounceX, bool bounceY )
        {
            if ( bounceX && Mathf.Abs( xI ) < 50 )
            {
                return;
            }

            if ( bounceY && Mathf.Abs( yI ) < 50 )
            {
                return;
            }

            if ( sound == null )
            {
                sound = Sound.GetInstance();
            }

            float volume = 0.4f;
            if ( bounceX && bounceY )
            {
                volume *= Mathf.Max( Mathf.Abs( xI ), Mathf.Abs( yI ) ) / 150f;
            }
            else if ( bounceX )
            {
                volume *= Mathf.Abs( xI ) / 150f;
            }
            else
            {
                volume *= Mathf.Abs( yI ) / 150f;
            }
            sound?.PlaySoundEffectAt( bounceSounds, volume, transform.position );
        }

        protected virtual void ProjectileApplyDamageToBlock( GameObject blockObject, int damage, DamageType type, float forceX, float forceY )
        {
            MapController.Damage_Networked( firedBy, blockObject, ValueOrchestrator.GetModifiedDamage( damage, playerNum ), type, forceX, forceY, X, Y );
        }
    }
}