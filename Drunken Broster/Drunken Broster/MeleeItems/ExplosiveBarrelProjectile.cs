using BroMakerLib;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class ExplosiveBarrelProjectile : CircularProjectile
    {
        protected bool exploded;
        public float explosionCounter = 4f;
        protected float flameCounter;
        protected float warningCounter;
        protected bool warningOn;

        protected override void Awake()
        {
            if ( sprite == null )
            {
                SpriteLowerLeftPixel = new Vector2( 0, 16 );
                SpritePixelDimensions = new Vector2( 16, 16 );
                spriteWidth = 16f;
                spriteHeight = 16f;
            }

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            ThemeHolder theme = Map.Instance.jungleThemeReference;
            BarrelBlock explosiveBarrel = theme.blockPrefabBarrels[1].GetComponent<BarrelBlock>();
            fire1 = explosiveBarrel.fire1;
            fire2 = explosiveBarrel.fire2;
            fire3 = explosiveBarrel.fire3;
            soundHolder.deathSounds = explosiveBarrel.soundHolder.deathSounds;

            bounceSounds = ResourcesController.GetAudioClipArray( SoundPath, "barrelBounce", 1 );
        }

        protected override bool Update()
        {
            RunBarrelEffects();

            return base.Update();
        }

        protected void RunBarrelEffects()
        {
            if ( explosionCounter > 0f )
            {
                explosionCounter -= t;
                if ( explosionCounter < 3f )
                {
                    flameCounter += t;
                    if ( flameCounter > 0f && explosionCounter > 0.2f )
                    {
                        if ( explosionCounter < 1f )
                        {
                            flameCounter -= 0.09f;
                        }
                        else if ( explosionCounter < 2f )
                        {
                            flameCounter -= 0.12f;
                        }
                        else
                        {
                            flameCounter -= 0.16f;
                        }
                        Vector3 vector = Random.insideUnitCircle;
                        switch ( Random.Range( 0, 3 ) )
                        {
                            case 0:
                                EffectsController.CreateEffect( fire1, X + vector.x * 4f, Y + vector.y * 3f + 2f, Random.value * 0.0434f, Vector3.zero );
                                break;
                            case 1:
                                EffectsController.CreateEffect( fire2, X + vector.x * 4f, Y + vector.y * 3f + 2f, Random.value * 0.0434f, Vector3.zero );
                                break;
                            case 2:
                                EffectsController.CreateEffect( fire3, X + vector.x * 4f, Y + vector.y * 3f + 2f, Random.value * 0.0434f, Vector3.zero );
                                break;
                        }
                    }
                    RunBarrelWarning( t, explosionCounter );
                    if ( explosionCounter <= 0.1f )
                    {
                        Death();
                    }
                }
            }
        }

        protected void RunBarrelWarning( float t, float explosionTime )
        {
            if ( explosionTime < 2f )
            {
                warningCounter += t;
                if ( warningOn && warningCounter > 0.0667f )
                {
                    warningOn = false;
                    warningCounter -= 0.0667f;
                }
                else if ( warningCounter > 0.0667f && explosionTime < 0.75f )
                {
                    warningOn = true;
                    warningCounter -= 0.0667f;
                }
                else if ( warningCounter > 0.175f && explosionTime < 1.25f )
                {
                    warningOn = true;
                    warningCounter -= 0.175f;
                }
                else if ( warningCounter > 0.2f )
                {
                    warningOn = true;
                    warningCounter -= 0.2f;
                }
                sprite.SetLowerLeftPixel( ( warningOn ? 1 : 0 ) * 16f, 16f );
            }
        }

        public override bool Damage( DamageObject damageObject )
        {
            Death();
            return true;
        }

        protected override void PlayBounceSound( bool bounceX, bool bounceY )
        {
            if ( bounceX && Mathf.Abs( xI ) < 25 )
            {
                return;
            }

            if ( bounceY && Mathf.Abs( yI ) < 25 )
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
            MakeEffects();
            DestroyGrenade();
        }

        protected override void MakeEffects()
        {
            if ( exploded )
            {
                return;
            }
            exploded = true;
            float range = 55f;
            EffectsController.CreateExplosionRangePop( X, Y, -1f, range * 2f );
            EffectsController.CreateSparkShower( X, Y, 70, 3f, 200f, 0f, 250f, 0.6f, 0.5f );
            EffectsController.CreatePlumes( X, Y, 3, 8f, 315f, 0f, 0f );
            Vector3 vector = new Vector3( Random.value, Random.value );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( X + vector.x * range * 0.25f, Y + vector.y * range * 0.25f ), Random.value * 0.5f );
            vector = new Vector3( Random.value, Random.value );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( X + vector.x * range * 0.25f, Y + vector.y * range * 0.25f ) );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Large, new Vector3( X, Y ) );
            SortOfFollow.Shake( 1f );
            PlayDeathSound();
            Map.DisturbWildLife( X, Y, 120f, 5 );
            MapController.DamageGround( this, 12, DamageType.Fire, range, X, Y );
            Map.ShakeTrees( X, Y, 256f, 64f, 128f );
            MapController.BurnUnitsAround_NotNetworked( this, playerNum, 1, range * 2f, X, Y, true, true );
            Map.ExplodeUnits( this, 12, DamageType.Fire, range * 1.1f, range, X, Y - 6f, 350f, 300f, playerNum, false, true );
            Map.ExplodeUnits( this, 1, DamageType.Fire, range * 1f, range, X, Y - 6f, 350f, 300f, playerNum, false, true );
        }

    }
}
