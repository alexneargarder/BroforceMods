using BroMakerLib;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class SoccerBallProjectile : CircularProjectile
    {
        protected override void Awake()
        {
            if ( sprite == null )
            {
                SpriteLowerLeftPixel = new Vector2( 0, 10 );
                SpritePixelDimensions = new Vector2( 10, 10 );
                spriteWidth = 10f;
                spriteHeight = 10f;
            }

            base.Awake();

            size = 5f;
            bounceM = 1.2f;
            bounceGroundDamage = 3;
            hitUnitForce = 0.75f;
            bounceOffEnemies = true;
            rotationSpeedMultiplier = 6f;
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            // Load death sound
            soundHolder.deathSounds = new AudioClip[1];
            soundHolder.deathSounds[0] = ResourcesController.GetAudioClip( SoundPath, "tireDeath.wav" );

            bounceSounds = ResourcesController.GetAudioClipArray( SoundPath, "soccerBounce", 3, 1 );
        }

        protected override void HitUnits()
        {
            if ( Mathf.Abs( xI ) > 80f && Map.HitUnits( this, playerNum, damage, damage, damageType, size + 1f, size + 4f, X, Y, xI * hitUnitForce, yI * hitUnitForce, true, true, false, alreadyHitUnits, false, false ) )
            {
                EffectsController.CreateProjectilePopWhiteEffect( X, Y - size * 0.5f );
                PlayHitSound();
                hitDelay = 0.1f;
                if ( bounceOffEnemies )
                {
                    float previousBounceM = bounceM;
                    bounceM *= 0.3f;
                    Bounce( true, false );
                    bounceM = previousBounceM;
                }
            }
        }

        protected override void PlayBounceSound( bool bounceX, bool bounceY )
        {
            if ( bounceX && Mathf.Abs( xI ) < 50 )
            {
                return;
            }

            if ( bounceY && Mathf.Abs( yI ) < 50 )
            {
                return;
            }

            if ( sound == null )
            {
                sound = Sound.GetInstance();
            }

            float volume = 0.4f;
            if ( bounceX && bounceY )
            {
                volume *= Mathf.Max( Mathf.Abs( xI ), Mathf.Abs( yI ) ) / 150f;
            }
            else if ( bounceX )
            {
                volume *= Mathf.Abs( xI ) / 150f;
            }
            else
            {
                volume *= Mathf.Abs( yI ) / 150f;
            }
            sound?.PlaySoundEffectAt( bounceSounds, volume, transform.position );
        }

        public override void Death()
        {
            if ( !dontMakeEffects )
            {
                MakeEffects();
            }
            DestroyGrenade();
        }

        protected override void MakeEffects()
        {
            float speed = 80f;
            EffectsController.CreateSmoke( X, Y - 3f, 0f, new Vector3( 0, speed ) );
            EffectsController.CreateSmoke( X, Y - 3f, 0f, new Vector3( 0, -speed ) );
            EffectsController.CreateSmoke( X, Y - 3f, 0f, new Vector3( speed, 0 ) );
            EffectsController.CreateSmoke( X, Y - 3f, 0f, new Vector3( -speed, 0 ) );
            EffectsController.CreateSmoke( X, Y - 3f, 0f, new Vector3( speed, speed ) );
            EffectsController.CreateSmoke( X, Y - 3f, 0f, new Vector3( speed, -speed ) );
            EffectsController.CreateSmoke( X, Y - 3f, 0f, new Vector3( -speed, speed ) );
            EffectsController.CreateSmoke( X, Y - 3f, 0f, new Vector3( -speed, -speed ) );
            //this.CreateGibs( this.xI, this.yI );
            PlayDeathSound();
        }
    }
}