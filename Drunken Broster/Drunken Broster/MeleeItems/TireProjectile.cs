using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using RocketLib;
using Rogueforce;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class TireProjectile : CustomGrenade
    {
        protected List<Unit> alreadyHitUnits = new List<Unit>();
        protected float hitDelay = 0f;
        protected float damageCooldown = 0f;
        protected RaycastHit raycastHit;
        protected LayerMask groundAndLadderLayer;
        protected GibHolder gibs;

        protected override void Awake()
        {
            if ( this.sprite == null )
            {
                this.spriteLowerLeftPixel = new Vector2( 0, 16 );
                this.spritePixelDimensions = new Vector2( 16, 16 );
                this.spriteWidth = 16f;
                this.spriteHeight = 16f;
            }

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

            // Allow this grenade to be hit by bullets and other attacks
            ShootableCircularDoodad doodad = this.GetOrAddComponent<ShootableCircularDoodad>();
            doodad.radius = 6f;
            doodad.owner = this;

            // Make tire roll over ladders
            this.groundAndLadderLayer = ( 1 << LayerMask.NameToLayer( "Ground" ) | 1 << LayerMask.NameToLayer( "IndestructibleGround" ) | 1 << LayerMask.NameToLayer( "LargeObjects" ) | 1 << LayerMask.NameToLayer( "Ladders" ) );

            // Setup gibs
            if ( this.gibs == null )
            {
                this.InitializeGibs();
            }

            base.Awake();
        }

        public override void PrefabSetup()
        {
            // Load death sound
            this.soundHolder.deathSounds = new AudioClip[1];
            this.soundHolder.deathSounds[0] = ResourcesController.GetAudioClip( soundPath, "tireDeath.wav" );
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

        protected void CreateGib( string name, Vector2 lowerLeftPixel, Vector2 pixelDimensions, float width, float height, Vector3 localPositionOffset )
        {
            BroMakerUtilities.CreateGibPrefab( name, lowerLeftPixel, pixelDimensions, width, height, new Vector3( 0f, 0f, 0f ), localPositionOffset, false, DoodadGibsType.Metal, 6, false, BloodColor.None, 1, true, 8, false, false, 3, 1, 1, 5f ).transform.parent = this.gibs.transform;
        }

        protected void InitializeGibs()
        {
            this.gibs = new GameObject( "TireProjectileGibs", new Type[] { typeof( Transform ), typeof( GibHolder ) } ).GetComponent<GibHolder>();
            this.gibs.gameObject.SetActive( false );
            UnityEngine.Object.DontDestroyOnLoad( this.gibs );
            CreateGib( "TireHub", new Vector2( 22, 12 ), new Vector2( 8, 8 ), 8f, 8f, new Vector3( 0f, 0f, 0f ) );
            CreateGib( "TireLeftPiece", new Vector2( 18, 12 ), new Vector2( 3, 8 ), 3f, 8f, new Vector3( -6f, 0f, 0f ) );
            CreateGib( "TireBottomPiece", new Vector2( 22, 15 ), new Vector2( 8, 3 ), 8f, 3f, new Vector3( 0f, -6f, 0f ) );
            CreateGib( "TireRightPiece", new Vector2( 31, 12 ), new Vector2( 3, 8 ), 3f, 8f, new Vector3( 6f, 0f, 0f ) );
            CreateGib( "TireTopPiece", new Vector2( 22, 4 ), new Vector2( 8, 3 ), 8f, 3f, new Vector3( 0f, 6f, 0f ) );

            CreateGib( "TireBottomLeftPiece", new Vector2( 36, 4 ), new Vector2( 3, 3 ), 3f, 3f, new Vector3( -3f, -3f, 0f ) );
            CreateGib( "TireBottomRightPiece", new Vector2( 36, 8 ), new Vector2( 3, 3 ), 3f, 3f, new Vector3( 3f, -3f, 0f ) );
            CreateGib( "TireTopLeftPiece", new Vector2( 40, 8 ), new Vector2( 3, 3 ), 3f, 3f, new Vector3( -3f, 3f, 0f ) );
            CreateGib( "TireTopRightPiece", new Vector2( 40, 4 ), new Vector2( 3, 3 ), 3f, 3f, new Vector3( 3f, 3f, 0f ) );

            // Make sure gibs are on layer 19 since the texture they're using is transparent
            for ( int i = 0; i < this.gibs.transform.childCount; ++i )
            {
                gibs.transform.GetChild( i ).gameObject.layer = 19;
            }
        }

        protected virtual void CreateGibs( float xI, float yI )
        {
            xI = xI * 0.25f;
            yI = yI * 0.25f + 80f;
            float xForce = 200f;
            float yForce = 300f;
            if ( gibs == null || gibs.transform == null )
            {
                return;
            }
            for ( int i = 0; i < gibs.transform.childCount; i++ )
            {
                Transform child = gibs.transform.GetChild( i );
                if ( child != null )
                {
                    EffectsController.CreateGib( child.GetComponent<Gib>(), base.GetComponent<Renderer>().sharedMaterial, base.X, base.Y, xForce * ( 0.8f + UnityEngine.Random.value * 0.4f ), yForce * ( 0.8f + UnityEngine.Random.value * 0.4f ), xI, yI, 1 );
                }
            }
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

            float previousX = this.X;
            float previousY = this.Y;
            if ( DrunkenBroster.freezeProjectile )
            {
                this.xI = 0;
                this.yI = 0;
            }
            if ( DrunkenBroster.spawnGibs )
            {
                this.CreateGibs( 0, 0 );
                DrunkenBroster.spawnGibs = false;
            }

            bool result = base.Update();

            if (  DrunkenBroster.freezeProjectile )
            {
                this.X = previousX;
                this.Y = previousY;
            }

            return result;
        }

        // Override check wall collisions to prevent tire from falling through ladders
        protected override void CheckWallCollisions( ref bool bounceY, ref bool bounceX, ref float yIT, ref float xIT )
        {
            if ( ConstrainToBlockAndFloor( this, base.X, base.Y, this.size, ref xIT, ref yIT, ref bounceX, ref bounceY, this.groundAndLadderLayer ) )
            {
                this.Bounce( bounceX, bounceY );
            }
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

        protected bool ConstrainToBlockAndFloor( MonoBehaviour obj, float x, float y, float size, ref float xIT, ref float yIT, ref bool bounceX, ref bool bounceY, LayerMask floorLayer )
        {
            Grenade grenade = obj as Grenade;
            bool result = false;
            if ( xIT == 0f && yIT == 0f )
            {
                return false;
            }
            if ( xIT > 16f || xIT < -16f )
            {
                float num = Mathf.Sign( xIT ) * ( Mathf.Abs( xIT ) - 16f );
                float num2 = yIT;
                if ( ConstrainToBlockAndFloor( obj, x, y, size, ref num, ref num2, ref bounceX, ref bounceY, floorLayer ) )
                {
                    xIT = num;
                    yIT = num2;
                    return true;
                }
            }
            if ( yIT > 16f || yIT < -16f )
            {
                float num3 = Mathf.Sign( yIT ) * ( Mathf.Abs( yIT ) - 16f );
                float num4 = xIT;
                if ( ConstrainToBlockAndFloor( obj, x, y, size, ref num4, ref num3, ref bounceX, ref bounceY, floorLayer ) )
                {
                    xIT = num4;
                    yIT = num3;
                    return true;
                }
            }
            float x2;
            if ( xIT > 0f )
            {
                x2 = x + xIT + size;
            }
            else if ( xIT < 0f )
            {
                x2 = x + xIT - size;
            }
            else
            {
                x2 = x;
            }
            float y2;
            if ( yIT > 0f )
            {
                y2 = y + yIT + size;
            }
            else if ( yIT < 0f )
            {
                y2 = y + yIT - size;
            }
            else
            {
                y2 = y;
            }
            int collumn = Map.GetCollumn( x );
            int row = Map.GetRow( y );
            int collumn2 = Map.GetCollumn( x2 );
            int row2 = Map.GetRow( y2 );
            if ( row2 != row )
            {
                // Constrain to floor layer if moving down
                Collider[] array = Physics.OverlapSphere( new Vector3( (float)( collumn * 16 ), (float)( row2 * 16 ), 0f ), 0.1f, ( yIT < 0 ) ? floorLayer: Map.groundLayer );
                if ( array.Length > 0 )
                {
                    if ( yIT < 0f )
                    {
                        yIT = (float)( row2 * 16 + 8 ) + size - y;
                    }
                    else if ( yIT > 0f )
                    {
                        yIT = (float)( row2 * 16 - 8 ) - size - y;
                    }
                    result = true;
                    bounceY = true;
                    if ( grenade != null )
                    {
                        foreach ( Collider collider in array )
                        {
                            collider.SendMessage( "StepOn", grenade, SendMessageOptions.DontRequireReceiver );
                        }
                    }
                    for ( int j = 0; j < array.Length; j++ )
                    {
                        if ( array[j].GetComponent<SawBlade>() != null && grenade != null )
                        {
                            grenade.Death();
                        }
                    }
                }
            }
            if ( collumn2 != collumn )
            {
                Collider[] array = Physics.OverlapSphere( new Vector3( (float)( collumn2 * 16 ), (float)( row * 16 ), 0f ), 0.1f, Map.groundLayer );
                if ( array.Length > 0 )
                {
                    if ( xIT < 0f )
                    {
                        xIT = (float)( collumn2 * 16 + 8 ) + size - x;
                    }
                    else if ( xIT > 0f )
                    {
                        xIT = (float)( collumn2 * 16 - 8 ) - size - x;
                    }
                    result = true;
                    if ( grenade != null )
                    {
                        foreach ( Collider collider2 in array )
                        {
                            collider2.SendMessage( "StepOn", grenade );
                        }
                    }
                    bounceX = true;
                    for ( int l = 0; l < array.Length; l++ )
                    {
                        if ( array[l].GetComponent<SawBlade>() != null && grenade != null )
                        {
                            grenade.Death();
                        }
                    }
                }
            }
            if ( !bounceX && !bounceY && collumn2 != collumn && row2 != row )
            {
                Collider[] array = Physics.OverlapSphere( new Vector3( (float)( collumn2 * 16 ), (float)( row2 * 16 ), 0f ), 0.1f, Map.groundLayer );
                if ( array.Length > 0 )
                {
                    bounceX = true;
                    bounceY = true;
                    if ( xIT < 0f )
                    {
                        xIT = (float)( collumn2 * 16 + 8 ) + size - x;
                    }
                    else if ( xIT > 0f )
                    {
                        xIT = (float)( collumn2 * 16 - 8 ) - size - x;
                    }
                    if ( yIT < 0f )
                    {
                        yIT = (float)( row2 * 16 + 8 ) + size - y;
                    }
                    else if ( yIT > 0f )
                    {
                        yIT = (float)( row2 * 16 - 8 ) - size - y;
                    }
                    if ( grenade != null )
                    {
                        foreach ( Collider collider3 in array )
                        {
                            collider3.SendMessage( "StepOn", grenade );
                        }
                        for ( int n = 0; n < array.Length; n++ )
                        {
                            if ( array[n].GetComponent<SawBlade>() != null && grenade != null )
                            {
                                grenade.Death();
                            }
                        }
                    }
                    result = true;
                }
            }
            return result;
        }

        protected virtual void HitUnits()
        {
            if ( Mathf.Abs(this.xI) > 80f && Map.HitUnits( this, this.playerNum, this.damage, this.damage, this.damageType, this.size, this.size, this.X, this.Y, this.xI * 2f, Mathf.Abs(this.xI * 2.5f), true, true, false, this.alreadyHitUnits, false, false ) )
            {
                this.hitDelay = 0.1f;
            }
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

        public bool Damage( DamageObject damageObject )
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
            if ( Mathf.Sign(this.xI) != Mathf.Sign(xI) )
            {
                xI *= 1.5f;
            }
            this.xI += Mathf.Sign( xI ) * Mathf.Max( Mathf.Min( Mathf.Abs( ( xI / this.weight ) ), 300f ), 100f );
            this.yI += Mathf.Sign( yI ) * Mathf.Max( Mathf.Min( Mathf.Abs( ( yI / this.weight ) ), 300f ), 0f );
        }

        public override void Death()
        {
            if ( !this.dontMakeEffects )
            {
                this.MakeEffects();
            }
            this.DestroyGrenade();
        }

        protected override void MakeEffects()
        {
            float speed = 80f;
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( 0, speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( 0, -speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( speed, 0 ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( -speed, 0 ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( speed, speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( speed, -speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( -speed, speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( -speed, -speed ) );
            this.CreateGibs( this.xI, this.yI );
            this.PlayDeathSound();
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            if ( bounceX )
            {
                // Try to hit ground if moving fast enough
                // Center
                if ( Mathf.Abs( this.xI ) > 100f && Physics.Raycast( new Vector3( base.X, base.Y, 0f ), new Vector3( Mathf.Sign(this.xI), 0f ), out this.raycastHit, this.size + 3f, Map.groundAndDamageableObjects ) && this.raycastHit.distance < this.size + 2f )
                {
                    this.ProjectileApplyDamageToBlock( this.raycastHit.collider.gameObject, 10, this.damageType, this.xI, this.yI );
                }
                // Down
                else if ( Mathf.Abs( this.xI ) > 100f && Physics.Raycast( new Vector3( base.X, base.Y + 4f, 0f ), new Vector3( Mathf.Sign( this.xI ), 0f ), out this.raycastHit, this.size + 3f, Map.groundAndDamageableObjects ) && this.raycastHit.distance < this.size + 2f )
                {
                    this.ProjectileApplyDamageToBlock( this.raycastHit.collider.gameObject, 10, this.damageType, this.xI, this.yI );
                }
                // Up
                else if ( Mathf.Abs( this.xI ) > 100f && Physics.Raycast( new Vector3( base.X, base.Y - 4f, 0f ), new Vector3( Mathf.Sign( this.xI ), 0f ), out this.raycastHit, this.size + 3f, Map.groundAndDamageableObjects ) && this.raycastHit.distance < this.size + 2f )
                {
                    this.ProjectileApplyDamageToBlock( this.raycastHit.collider.gameObject, 10, this.damageType, this.xI, this.yI );
                }
                this.xI *= -0.8f;
                this.rI *= -1f;
                this.alreadyHitUnits.Clear();
            }
            if ( bounceY )
            {
                this.yI *= -0.6f;
            }
        }

        protected virtual void ProjectileApplyDamageToBlock( GameObject blockObject, int damage, DamageType type, float forceX, float forceY )
        {
            MapController.Damage_Networked( this.firedBy, blockObject, ValueOrchestrator.GetModifiedDamage( damage, this.playerNum ), type, forceX, forceY, base.X, base.Y );
        }
    }
}