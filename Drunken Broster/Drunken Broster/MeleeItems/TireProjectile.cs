using BroMakerLib;
using BroMakerLib.Loggers;
using RocketLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class TireProjectile : Grenade
    {
        public static Material storedMat;
        private List<Unit> alreadyHitUnits = new List<Unit>();
        private float hitDelay = 0f;
        private float damageCooldown = 0f;

        protected override void Awake()
        {
            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            directoryPath = Path.Combine( directoryPath, "projectiles" );
            storedMat = ResourcesController.GetMaterial( directoryPath, "Tire.png" );

            renderer.material = storedMat;

            sprite = this.gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2( 0, 16 );
            sprite.pixelDimensions = new Vector2( 16, 16 );

            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 16;
            sprite.height = 16;
            sprite.offset = new Vector3( 0, 0, 0 );

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

            base.Awake();
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

        protected override void RunWarnings()
        {
        }

        protected virtual void HitUnits()
        {
            if ( Map.HitUnits( this, this.playerNum, this.damage, this.damage, this.damageType, this.size, this.size, this.X, this.Y, this.xI + ( Mathf.Sign( this.xI ) * 200f ), 300f, true, true, false, this.alreadyHitUnits, false, false ) )
            {
                this.hitDelay = 0.1f;
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
            float speed = 50f;
            EffectsController.CreateSmoke( base.X, base.Y - 2f, 0f, new Vector3( 0, speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 2f, 0f, new Vector3( 0, -speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 2f, 0f, new Vector3( speed, 0 ) );
            EffectsController.CreateSmoke( base.X, base.Y - 2f, 0f, new Vector3( -speed, 0 ) );
            EffectsController.CreateSmoke( base.X, base.Y - 2f, 0f, new Vector3( speed, speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 2f, 0f, new Vector3( speed, -speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 2f, 0f, new Vector3( -speed, speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 2f, 0f, new Vector3( -speed, -speed ) );
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            if ( bounceX )
            {
                this.xI *= -0.8f;
                this.rI *= -1f;
                this.alreadyHitUnits.Clear();
            }
            if ( bounceY )
            {
                this.yI *= -0.6f;
            }
        }

    }
}