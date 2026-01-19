using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class AcidEggProjectile : CustomProjectile
    {
        public AudioClip[] pulseSounds;
        public AudioClip[] burstSounds;
        protected int frame;
        protected float counter;
        protected float frameRate = 0.07f;
        protected int cycle;
        protected bool exploded;
        protected float range = 30f;
        protected float r = 90f;
        protected float rI;
        protected float rotationSpeedMultiplier = 1.5f;

        protected BloodColor bloodColor = BloodColor.Green;
        protected float explodeRange = 40f;

        protected override void Awake()
        {
            if ( !RanSetup )
            {
                SpriteLowerLeftPixel = new Vector2( 0, 32 );
                SpritePixelDimensions = new Vector2( 32, 32 );
                SpriteWidth = 22;
                SpriteHeight = 22;
                SpriteOffset = new Vector3( 0, 5, 0 );
            }

            projectileSize = 8f;

            damage = 5;
            damageInternal = damage;
            fullDamage = damage;

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            pulseSounds = ResourcesController.GetAudioClipArray( SoundPath, "egg_pulse", 2 );

            burstSounds = ResourcesController.GetAudioClipArray( SoundPath, "egg_burst", 3 );
        }

        public override void Fire( float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            rI = -Mathf.Sign( xI ) * ( 200f + Random.value * 200f ) * rotationSpeedMultiplier;

            base.Fire( newX, newY, xI, yI, _zOffset, playerNum, FiredBy );
        }

        protected override void SetRotation()
        {
            // Don't rotate based on momentum
            if ( xI > 0f )
            {
                transform.localScale = new Vector3( -1f, 1f, 1f );
            }
            else
            {
                transform.localScale = new Vector3( 1f, 1f, 1f );
            }

            transform.eulerAngles = new Vector3( 0f, 0f, r );
        }

        protected override void Update()
        {
            ApplyGravity();

            // Change frames
            counter += t;
            if ( counter > frameRate )
            {
                counter -= frameRate;
                ++frame;
                ChangeFrame();
            }

            r += rI * t;

            SetRotation();

            base.Update();
        }

        protected virtual void ApplyGravity()
        {
            yI -= 500f * t;
        }

        protected void ChangeFrame()
        {
            if ( frame == 1 )
            {
                if ( sound == null )
                {
                    sound = Sound.GetInstance();
                }

                sound?.PlaySoundEffectAt( pulseSounds, 0.4f, transform.position );
            }
            if ( frame >= 6 )
            {
                frame = 0;
                ++cycle;
            }
            Sprite.SetLowerLeftPixel_X( 32f * frame );
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( exploded )
            {
                return;
            }
            exploded = true;

            if ( sound == null )
            {
                sound = Sound.GetInstance();
            }
            sound?.PlaySoundEffectAt( burstSounds, 0.65f, transform.position );

            MapController.DamageGround( this, 15, DamageType.Explosion, explodeRange, X, Y );
            Map.ExplodeUnits( this, 9, DamageType.Acid, explodeRange * 2.3f, explodeRange * 1.5f, X, Y + 3f, 500f, 300f, playerNum, false, false );
            Map.ShakeTrees( X, Y, 128f, 48f, 80f );
            SortOfFollow.Shake( 1f );
            Map.DisturbWildLife( X, Y, 80f, playerNum );
            EffectsController.CreateExplosionRangePop( X, Y + 6f, -1f, explodeRange * 1.5f );
            EffectsController.CreateSlimeParticlesSpray( bloodColor, X, Y + 6f, 1f, 34, 6f, 5f, 300f, xI * 0.6f, yI * 0.2f + 150f, 0.6f );
            EffectsController.CreateSlimeExplosion( X, Y, 15f, 15f, 140f, 0f, 0f, 0f, 0f, 0, 20, 120f, 0f, Vector3.up );
            EffectsController.CreateSlimeCover( 15, X, Y + 8f, 60f );

        }

    }
}
