using BroMakerLib.CustomObjects.Projectiles;
using System.IO;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class BottleProjectile : CustomGrenade
    {
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
            this.rotationSpeedMultiplier = 2.5f;
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            this.Death();
        }

        public override void Death()
        {
            this.MakeEffects();
            this.DestroyGrenade();
        }

        protected override void MakeEffects()
        {
            EffectsController.CreateGlassShards( base.X, base.Y, 20, 4f, 4f, 80f, 60f, this.xI * 0.2f, 50f, 0.2f, 1f, 0.3f, 0.5f );
        }
    }
}
