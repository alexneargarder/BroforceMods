using BroMakerLib.CustomObjects.Projectiles;
using System.IO;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class BottleProjectile : CustomGrenade
    {
        protected bool madeEffects = false;

        protected override void Awake()
        {
            if ( !this.RanSetup )
            {
                this.defaultSoundHolder = ( HeroController.GetHeroPrefab( HeroType.DoubleBroSeven ) as DoubleBroSeven ).martiniGlass.soundHolder;
            }

            base.Awake();

            // Setup properties
            this.size = 4f;
            this.ShouldKillIfNotVisible = false;
            this.damage = 12;
            this.useAngularFriction = true;
            this.angularFrictionM = 6f;
            this.bounceOffEnemies = true;
            this.rotateAtRightAngles = false;
            this.lifeM = 6;
            this.frictionM = 0.6f;
            this.shrink = false;
            this.destroyInsideWalls = false;
            this.rotationSpeedMultiplier = 2.5f;
        }

        public override void Launch( float newX, float newY, float xI, float yI )
        {
            base.Launch( newX, newY, xI, yI );
            this.rI = -Mathf.Sign( xI ) * ( 200f + UnityEngine.Random.value * 175f ) * this.rotationSpeedMultiplier;
            this.r = -45f;
            this.SetPosition();
            
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            this.Death();
        }

        protected override void BounceOffEnemies()
        {
            if ( Map.HitLivingUnits( this, this.playerNum, this.damage, this.damageType, this.size - 2f, this.size + 4f, this.X - Mathf.Sign(xI) * 2f, this.Y, this.xI / 3f, 50f, false, true, false, false ) )
            {
                this.Bounce( false, false );
            }
        }

        public override void Death()
        {
            this.MakeEffects();
            this.DestroyGrenade();
        }

        protected override void MakeEffects()
        {
            if ( this.madeEffects )
            {
                return;
            }
            this.madeEffects = true;
            Map.AttractMooks( this.X, this.Y, 100f, 100f );
            EffectsController.CreateGlassShards( base.X, base.Y, 20, 4f, 4f, 80f, 60f, this.xI * 0.2f, 50f, 0.2f, 1f, 0.3f, 0.5f );
        }
    }
}
