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
    class TireProjectile : Grenade
    {
        public static Material storedMat;

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
            if ( this.shootable )
            {
                Map.RegisterShootableGrenade( this );
            }
        }

        protected override bool Update()
        {
            this.rI = this.xI * -1f * this.rotationSpeedMultiplier;
            return base.Update();
        }

        protected override void RunWarnings()
        {
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
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            if ( bounceX )
            {
                this.xI *= -0.8f;
                this.rI *= -1f;
            }
            if ( bounceY )
            {
                this.yI *= -0.6f;
            }
        }

    }
}