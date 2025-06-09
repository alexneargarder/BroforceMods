using BroMakerLib;
using Rogueforce.PerkSystem.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Windows.Speech;

namespace Drunken_Broster.MeleeItems
{
    public class AcidEggProjectile : Projectile
    {
        public static Material storedMat;
        public SpriteSM sprite;
        protected Shrapnel[] shrapnelPrefabs = new Shrapnel[3];
        protected int frame = 0;
        protected float counter = 0;
        protected float frameRate = 0.07f;
        protected int cycle = 0;
        protected bool exploded = false;
        protected float range = 30f;

        protected BloodColor bloodColor = BloodColor.Green;
        protected float explodeRange = 40f;

        protected override void Awake()
        {
            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            directoryPath = Path.Combine( directoryPath, "projectiles" );
            storedMat = ResourcesController.GetMaterial( directoryPath, "AcidEgg.png" );

            renderer.material = storedMat;

            sprite = this.gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2( 0, 32 );
            sprite.pixelDimensions = new Vector2( 32, 32 );

            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 20;
            sprite.height = 20;
            sprite.offset = new Vector3( 0, 5, 0 );

            this.projectileSize = 7f;

            this.damage = 5;
            this.damageInternal = this.damage;
            this.fullDamage = this.damage;

            base.Awake();
        }

        protected override void SetRotation()
        {
            // Don't rotate based on momentum
            if ( this.xI > 0f )
            {
                base.transform.localScale = new Vector3( -1f, 1f, 1f );
            }
            else
            {
                base.transform.localScale = new Vector3( 1f, 1f, 1f );
            }

            base.transform.eulerAngles = new Vector3( 0f, 0f, 0f );
        }

        protected override void Update()
        {
            this.ApplyGravity();

            // Change frames
            this.counter += this.t;
            if ( this.counter > this.frameRate )
            {
                this.counter -= this.frameRate;
                ++this.frame;
                this.ChangeFrame();
            }

            //RocketLib.Utils.DrawDebug.DrawCrosshair( "egg_crosshair", base.transform.position, 7f, Color.red );

            base.Update();
        }

        protected virtual void ApplyGravity()
        {
            this.yI -= 450f * this.t;
        }

        protected void ChangeFrame()
        {
            if ( this.frame >= 6 )
            {
                this.frame = 0;
                ++this.cycle;
            }
            this.sprite.SetLowerLeftPixel_X( 32f * this.frame );
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( this.exploded )
            {
                return;
            }
            this.exploded = true;
            MapController.DamageGround( this, 15, DamageType.Explosion, this.explodeRange, base.X, base.Y, null, false );
            Map.ExplodeUnits( this, 9, DamageType.Acid, this.explodeRange * 2.3f, this.explodeRange * 1.5f, base.X, base.Y + 3f, 500f, 300f, base.playerNum, false, false, true );
            Map.ShakeTrees( base.X, base.Y, 128f, 48f, 80f );
            SortOfFollow.Shake( 1f );
            Map.DisturbWildLife( base.X, base.Y, 80f, base.playerNum );
            EffectsController.CreateExplosionRangePop( base.X, base.Y + 6f, -1f, this.explodeRange * 1.5f );
            EffectsController.CreateSlimeParticlesSpray( this.bloodColor, base.X, base.Y + 6f, 1f, 34, 6f, 5f, 300f, this.xI * 0.6f, this.yI * 0.2f + 150f, 0.6f );
            EffectsController.CreateSlimeExplosion( base.X, base.Y, 15f, 15f, 140f, 0f, 0f, 0f, 0f, 0, 20, 120f, 0f, Vector3.up, BloodColor.Green );
            EffectsController.CreateSlimeCover( 15, base.X, base.Y + 8f, 60f, false );

        }

    }
}
