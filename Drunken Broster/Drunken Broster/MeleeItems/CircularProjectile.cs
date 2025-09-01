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
        protected float hitDelay = 0f;
        protected float damageCooldown = 0f;
        protected RaycastHit raycastHit;
        protected LayerMask groundAndLadderLayer;
        protected int bounceGroundDamage = 10;
        protected float hitUnitForce = 1.0f;
        public AudioClip[] bounceSounds;
        public AudioClip[] hitSounds;

        protected override void Awake()
        {
            this.size = 8f;
            this.life = 1e6f;
            this.trailType = TrailType.None;
            this.shrink = false;
            this.shootable = true;
            this.drag = 0.45f;
            this.destroyInsideWalls = false;
            this.rotateAtRightAngles = false;
            this.rotationSpeedMultiplier = 5f;
            this.damage = 3;
            this.damageType = DamageType.Crush;
            this.health = 15;
            this.bounceOffEnemies = true;
            this.weight = 2f;
            this.friendlyFire = false;
            this.bounceM = 1.0f;
            this.bounceOffEnemies = false;
            this.zOffset = 0.5f;

            // Allow this grenade to be hit by bullets and other attacks
            ShootableCircularDoodad doodad = this.GetOrAddComponent<ShootableCircularDoodad>();
            doodad.radius = 6f;
            doodad.owner = this;

            // Make tire roll over ladders
            this.groundAndLadderLayer = ( 1 << LayerMask.NameToLayer( "Ground" ) | 1 << LayerMask.NameToLayer( "IndestructibleGround" ) | 1 << LayerMask.NameToLayer( "LargeObjects" ) );

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            this.hitSounds = ResourcesController.GetAudioClipArray( SoundPath, "meleeHitBlunt", 2 );
        }

        public override void Launch( float newX, float newY, float xI, float yI )
        {
            base.Launch( newX, newY, xI, yI );
            this.rI = this.xI * -1f * this.rotationSpeedMultiplier;
            this.life = this.startLife = 1e6f;
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
            this.rI = this.xI * -1f * this.rotationSpeedMultiplier;

            if ( this.hitDelay > 0f )
            {
                this.hitDelay -= this.t;
            }
            else
            {
                this.HitUnits();
            }

            if ( this.damageCooldown > 0f )
            {
                this.damageCooldown -= this.t;
            }

            return base.Update();
        }

        protected override void CheckWallCollisions( ref bool bounceY, ref bool bounceX, ref float yIT, ref float xIT )
        {
            // Use sphere-based collision instead of point-based
            if ( ConstrainToBlocksWithSphere( base.X, base.Y, this.size, ref xIT, ref yIT, ref bounceX, ref bounceY, this.groundAndLadderLayer ) )
            {
                this.Bounce( bounceX, bounceY );
            }

            // Parent to moving platforms
            this.parentedCollider = null;
            if ( this.moveWithRestingTransform && Physics.Raycast( base.transform.position + Vector3.up * 2f, Vector3.down, out RaycastHit raycastHit, this.size + 2f, Map.groundLayer ) && raycastHit.distance - 2f <= this.size )
            {
                this.parentedCollider = raycastHit.collider;
                base.Y += this.size - ( raycastHit.distance - 2f );
                this.parentedPosition = this.parentedCollider.transform.position;
                if ( this.yI < 0f )
                {
                    this.Bounce( false, true );
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
            if ( Mathf.Abs( this.xI ) > 80f && Map.HitUnits( this, this.playerNum, this.damage, this.damage, this.damageType, this.size, this.size + 4f, this.X, this.Y, this.xI * 2f * this.hitUnitForce, Mathf.Abs( this.xI * 2.5f ) * this.hitUnitForce, true, true, false, this.alreadyHitUnits, false, false ) )
            {
                this.PlayHitSound();
                this.hitDelay = 0.1f;
                if ( this.bounceOffEnemies )
                {
                    float previousBounceM = this.bounceM;
                    this.bounceM *= 0.5f;
                    this.Bounce( true, false );
                    this.bounceM = previousBounceM;
                }
            }
        }

        protected virtual void PlayHitSound()
        {
            if ( sound == null )
            {
                sound = Sound.GetInstance();
            }

            float volume = Mathf.Max( 0.5f * ( ( Mathf.Abs( this.xI ) + Mathf.Abs( this.yI ) ) / 250f ), 0.2f );

            sound?.PlaySoundEffectAt( this.hitSounds, volume, base.transform.position );
        }

        protected override void HitFragile()
        {
            Vector3 vector = new Vector3( this.xI, this.yI, 0f );
            Vector3 normalized = vector.normalized;
            Collider[] array = Physics.OverlapSphere( new Vector3( base.X + normalized.x * 2f, base.Y + normalized.y * 2f, 0f ), 2f, this.fragileLayer );
            for ( int i = 0; i < array.Length; i++ )
            {
                EffectsController.CreateProjectilePuff( base.X, base.Y );
                // Hit all fragile (including doors)
                array[i].gameObject.SendMessage( "Damage", new DamageObject( 1, this.damageType, this.xI, this.yI, base.X, base.Y, this ), SendMessageOptions.DontRequireReceiver );
            }
        }

        public virtual bool Damage( DamageObject damageObject )
        {
            if ( this.damageCooldown <= 0f )
            {
                this.health -= damageObject.damage;
                this.Knock( damageObject.x, damageObject.y, damageObject.xForce, damageObject.yForce );
                this.damageCooldown = 0.1f;
                if ( this.health <= 0 )
                {
                    this.Death();
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
            this.xI += Mathf.Sign( xI ) * Mathf.Max( Mathf.Min( Mathf.Abs( ( xI / this.weight ) ), 300f ), 100f );
            this.yI += Mathf.Sign( yI ) * Mathf.Max( Mathf.Min( Mathf.Abs( ( yI / this.weight ) ), 300f ), 0f );
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            if ( bounceX )
            {
                // Try to hit ground if moving fast enough
                // Center
                if ( Mathf.Abs( this.xI ) > 100f && Physics.Raycast( new Vector3( base.X, base.Y, 0f ), new Vector3( Mathf.Sign( this.xI ), 0f ), out this.raycastHit, this.size + 3f, Map.groundAndDamageableObjects ) && this.raycastHit.distance < this.size + 2f )
                {
                    this.ProjectileApplyDamageToBlock( this.raycastHit.collider.gameObject, this.bounceGroundDamage, this.damageType, this.xI, this.yI );
                }
                // Down
                else if ( Mathf.Abs( this.xI ) > 100f && Physics.Raycast( new Vector3( base.X, base.Y + 4f, 0f ), new Vector3( Mathf.Sign( this.xI ), 0f ), out this.raycastHit, this.size + 3f, Map.groundAndDamageableObjects ) && this.raycastHit.distance < this.size + 2f )
                {
                    this.ProjectileApplyDamageToBlock( this.raycastHit.collider.gameObject, this.bounceGroundDamage, this.damageType, this.xI, this.yI );
                }
                // Up
                else if ( Mathf.Abs( this.xI ) > 100f && Physics.Raycast( new Vector3( base.X, base.Y - 4f, 0f ), new Vector3( Mathf.Sign( this.xI ), 0f ), out this.raycastHit, this.size + 3f, Map.groundAndDamageableObjects ) && this.raycastHit.distance < this.size + 2f )
                {
                    this.ProjectileApplyDamageToBlock( this.raycastHit.collider.gameObject, this.bounceGroundDamage, this.damageType, this.xI, this.yI );
                }

                this.PlayBounceSound( bounceX, bounceY );

                this.xI *= -0.8f * this.bounceM;
                this.rI *= -1f;
                this.alreadyHitUnits.Clear();
            }
            if ( bounceY )
            {
                this.PlayBounceSound( bounceX, bounceY );
                this.yI *= -0.6f * this.bounceM;
            }
        }

        protected virtual void PlayBounceSound( bool bounceX, bool bounceY )
        {
            if ( bounceX && Mathf.Abs( this.xI ) < 50 )
            {
                return;
            }

            if ( bounceY && Mathf.Abs( this.yI ) < 50 )
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
            sound?.PlaySoundEffectAt( this.bounceSounds, volume, base.transform.position );
        }

        protected virtual void ProjectileApplyDamageToBlock( GameObject blockObject, int damage, DamageType type, float forceX, float forceY )
        {
            MapController.Damage_Networked( this.firedBy, blockObject, ValueOrchestrator.GetModifiedDamage( damage, this.playerNum ), type, forceX, forceY, base.X, base.Y );
        }
    }
}