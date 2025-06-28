using BroMakerLib.CustomObjects.Projectiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class ExplosiveBarrel : CustomGrenade
    {
        protected bool exploded = false;

        protected override void Awake()
        {
            if ( !this.RanSetup )
            {
                this.defaultSoundHolder = ( HeroController.GetHeroPrefab( HeroType.DoubleBroSeven ) as DoubleBroSeven ).martiniGlass.soundHolder;
            }

            base.Awake();

            // Setup properties
            this.size = 8f;
            this.ShouldKillIfNotVisible = false;
            this.damage = 25;
            this.useAngularFriction = true;
            this.angularFrictionM = 6f;
            this.bounceOffEnemies = true;
            this.rotateAtRightAngles = false;
            this.lifeM = 6;
            this.frictionM = 0.6f;
            this.shrink = false;
            this.destroyInsideWalls = false;
            this.rotationSpeedMultiplier = 1.5f;
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            this.Death();
        }

        protected override void MakeEffects()
        {
            if ( this.exploded )
            {
                return;
            }
            this.exploded = true;
            EffectsController.CreateExplosionRangePop( base.X, base.Y, -1f, this.range * 2f );
            EffectsController.CreateSparkShower( base.X, base.Y, 70, 3f, 200f, 0f, 250f, 0.6f, 0.5f );
            EffectsController.CreatePlumes( base.X, base.Y, 3, 8f, 315f, 0f, 0f );
            Vector3 vector = this.random.insideUnitCircle;
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( base.X + vector.x * this.range * 0.25f, base.Y + vector.y * this.range * 0.25f ), this.random.value * 0.5f );
            vector = this.random.insideUnitCircle;
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( base.X + vector.x * this.range * 0.25f, base.Y + vector.y * this.range * 0.25f ), 0f );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Large, new Vector3( base.X, base.Y ), 0f );
            SortOfFollow.Shake( 1f );
            this.PlayDeathSound();
            Map.DisturbWildLife( base.X, base.Y, 120f, 5 );
            MapController.DamageGround( this, 12, this.damageType, this.range, base.X, base.Y, null, false );
            Map.ShakeTrees( base.X, base.Y, 256f, 64f, 128f );
            MapController.BurnUnitsAround_NotNetworked( this, -1, 1, this.range * 2f, base.X, base.Y, true, true );
            Map.ExplodeUnits( this, 12, this.damageType, this.range * 1.2f, this.range, base.X, base.Y - 6f, 200f, 300f, 15, false, false, true );
            Map.ExplodeUnits( this, 1, this.damageType, this.range * 1f, this.range, base.X, base.Y - 6f, 200f, 300f, -1, false, false, true );
        }
    }
}
