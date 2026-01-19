using BroMakerLib.CustomObjects.Projectiles;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class BottleProjectile : CustomGrenade
    {
        protected bool madeEffects;

        protected override void Awake()
        {
            if ( !RanSetup )
            {
                DefaultSoundHolder = ( HeroController.GetHeroPrefab( HeroType.DoubleBroSeven ) as DoubleBroSeven ).martiniGlass.soundHolder;
            }

            base.Awake();

            // Setup properties
            size = 4f;
            ShouldKillIfNotVisible = false;
            damage = 12;
            useAngularFriction = true;
            angularFrictionM = 6f;
            bounceOffEnemies = true;
            rotateAtRightAngles = false;
            lifeM = 6;
            frictionM = 0.6f;
            shrink = false;
            destroyInsideWalls = false;
            rotationSpeedMultiplier = 2.5f;
        }

        public override void Launch( float newX, float newY, float xI, float yI )
        {
            base.Launch( newX, newY, xI, yI );
            rI = -Mathf.Sign( xI ) * ( 200f + Random.value * 175f ) * rotationSpeedMultiplier;
            r = -45f;
            SetPosition();

        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            Death();
        }

        protected override void BounceOffEnemies()
        {
            if ( Map.HitLivingUnits( this, playerNum, damage, damageType, size - 2f, size + 4f, X - Mathf.Sign( xI ) * 2f, Y, xI / 3f, 50f, false, true, false ) )
            {
                Bounce( false, false );
            }
        }

        public override void Death()
        {
            MakeEffects();
            DestroyGrenade();
        }

        protected override void MakeEffects()
        {
            if ( madeEffects )
            {
                return;
            }
            madeEffects = true;
            Map.AttractMooks( X, Y, 100f, 100f );
            EffectsController.CreateGlassShards( X, Y, 20, 4f, 4f, 80f, 60f, xI * 0.2f, 50f, 0.2f, 1f, 0.3f, 0.5f );
        }
    }
}
