using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class CoconutProjectile : CustomGrenade
    {
        protected int bounces;
        protected int bulletHits;
        protected int maxBounces = 6;
        protected int maxBulletHits = 5;
        protected bool shouldKillIfNotVisible = true;

        protected override void Awake()
        {
            base.Awake();

            this.damage = 8;
            this.useAngularFriction = true;
            this.angularFrictionM = 5;
            this.bounceOffEnemies = true;
            this.bounceOffEnemiesMultiple = true;
            this.shootable = true;
            this.rotateAtRightAngles = false;
            this.size = 5;
            this.lifeM = 0.8f;
            this.frictionM = 0.6f;
            this.shrink = false;
        }

        public override void PrefabSetup()
        {
            // Load death sounds
            this.soundHolder.deathSounds = new AudioClip[2];
            this.soundHolder.deathSounds[0] = ResourcesController.GetAudioClip( soundPath, "coconutDeath1.wav" );
            this.soundHolder.deathSounds[1] = ResourcesController.GetAudioClip( soundPath, "coconutDeath2.wav" );

            // Load hit sounds
            this.soundHolder.hitSounds = new AudioClip[5];
            this.soundHolder.hitSounds[0] = ResourcesController.GetAudioClip( soundPath, "coconutHit1.wav" );
            this.soundHolder.hitSounds[1] = ResourcesController.GetAudioClip( soundPath, "coconutHit2.wav" );
            this.soundHolder.hitSounds[2] = ResourcesController.GetAudioClip( soundPath, "coconutHit3.wav" );
            this.soundHolder.hitSounds[3] = ResourcesController.GetAudioClip( soundPath, "coconutHit4.wav" );
            this.soundHolder.hitSounds[4] = ResourcesController.GetAudioClip( soundPath, "coconutHit5.wav" );
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            if ( bounceY && this.yI < -60f )
            {
                EffectsController.CreateLandPoofEffect( base.X, base.Y - this.size, UnityEngine.Random.Range( 0, 2 ) * 2 - 1, BloodColor.None );
            }
            float num = Mathf.Abs( this.xI ) + Mathf.Abs( this.yI );
            if ( num > 150f || ( !bounceX && !bounceY && this.yI < -50f ) )
            {
                this.bounces++;
                if ( this.bounces > this.maxBounces )
                {
                    this.life = -0.1f;
                }
                Map.DisturbWildLife( base.X, base.Y, 32f, this.playerNum );
            }
            base.Bounce( bounceX, bounceY );
        }

        // Allow bouncing off enemies when moving horizontally
        protected override void BounceOffEnemies()
        {
            if ( this.bounceOffEnemiesMultiple )
            {
                if ( Map.HitAllLivingUnits( this.firedBy, this.playerNum, this.damage, DamageType.Bounce, this.size - 2f, this.size + 2f, base.X, base.Y, this.xI, 30f, false, true ) )
                {
                    this.Bounce( false, false );
                    this.yI = 50f + 90f / this.weight;
                    this.xI = Mathf.Clamp( this.xI * 0.25f, -100f, 100f );
                    EffectsController.CreateBulletPoofEffect( base.X, base.Y - this.size * 0.5f );
                }
            }
            else if ( Map.HitAllLivingUnits( this.firedBy, this.playerNum, this.damage, DamageType.Bounce, this.size - 2f, this.size + 2f, base.X, base.Y, this.xI, 30f, false, true, this.alreadyBouncedOffUnits ) )
            {
                this.Bounce( false, false );
                if ( this.alreadyBouncedOffUnits.Count > 0 )
                {
                    this.yI = ( 50f + 90f / this.weight ) / (float)this.alreadyBouncedOffUnits.Count;
                }
                else
                {
                    this.yI = 50f + 90f / this.weight;
                }
                this.xI = Mathf.Clamp( this.xI * 0.25f, -100f, 100f );
                EffectsController.CreateBulletPoofEffect( base.X, base.Y - this.size * 0.5f );
            }
        }

        public override void Knock( float xDiff, float yDiff, float xI, float yI )
        {
            base.Knock( xDiff, yDiff, xI, yI );
            base.transform.parent = null;
            this.bulletHits++;
            if ( this.bulletHits > 2 )
            {
                this.bounces = (int)( (float)this.maxBounces * 1.667f );
            }
            if ( this.bulletHits > this.maxBulletHits )
            {
                this.life = -0.1f;
            }
        }

        protected override void RunLife()
        {
            if ( this.life <= 0f )
            {
                this.Death();
            }
        }

        public override void Death()
        {
            this.PlayDeathSound( 0.5f );
            EffectsController.CreateDirtParticles( base.X, base.Y, 12, 2f, 150f, this.xI * 0.5f, this.yI + 100f );
            EffectsController.CreateSemenParticles( BloodColor.Red, base.X, base.Y, 0f, 20, 2f, 2f, 150f, this.xI * 0.5f, 100f );
            this.DestroyGrenade();
        }

        public override bool ShouldKillIfNotVisible
        {
            get
            {
                return this.shouldKillIfNotVisible;
            }
            set
            {
            }
        }
    }
}
