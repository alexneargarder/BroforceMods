using BroMakerLib.CustomObjects.Projectiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class ExplosiveBarrelProjectile : CustomProjectile
    {
        protected bool exploded = false;
        protected float range = 50f;

        protected override void Awake()
        {
            this.projectileSize = 12f;

            this.damage = 14;
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
            base.Update();
        }

        protected virtual void ApplyGravity()
        {
            this.yI -= 500f * this.t;
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
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
