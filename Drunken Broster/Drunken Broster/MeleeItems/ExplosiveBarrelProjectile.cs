using BroMakerLib;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class ExplosiveBarrelProjectile : CircularProjectile
    {
        protected bool exploded = false;
        public float explosionCounter = 4f;
        protected float flameCounter = 0f;
        protected float warningCounter = 0f;
        protected bool warningOn = false;

        protected override void Awake()
        {
            if ( this.sprite == null )
            {
                this.SpriteLowerLeftPixel = new Vector2( 0, 16 );
                this.SpritePixelDimensions = new Vector2( 16, 16 );
                this.spriteWidth = 16f;
                this.spriteHeight = 16f;
            }

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            ThemeHolder theme = Map.Instance.jungleThemeReference;
            BarrelBlock explosiveBarrel = theme.blockPrefabBarrels[1].GetComponent<BarrelBlock>();
            this.fire1 = explosiveBarrel.fire1;
            this.fire2 = explosiveBarrel.fire2;
            this.fire3 = explosiveBarrel.fire3;
            this.soundHolder.deathSounds = explosiveBarrel.soundHolder.deathSounds;

            this.bounceSounds = ResourcesController.GetAudioClipArray( SoundPath, "barrelBounce", 1 );
        }

        protected override bool Update()
        {
            this.RunBarrelEffects();

            return base.Update();
        }

        protected void RunBarrelEffects()
        {
            if ( this.explosionCounter > 0f )
            {
                this.explosionCounter -= this.t;
                if ( this.explosionCounter < 3f )
                {
                    this.flameCounter += this.t;
                    if ( this.flameCounter > 0f && this.explosionCounter > 0.2f )
                    {
                        if ( this.explosionCounter < 1f )
                        {
                            this.flameCounter -= 0.09f;
                        }
                        else if ( this.explosionCounter < 2f )
                        {
                            this.flameCounter -= 0.12f;
                        }
                        else
                        {
                            this.flameCounter -= 0.16f;
                        }
                        Vector3 vector = UnityEngine.Random.insideUnitCircle;
                        switch ( UnityEngine.Random.Range( 0, 3 ) )
                        {
                            case 0:
                                EffectsController.CreateEffect( this.fire1, base.X + vector.x * 4f, base.Y + vector.y * 3f + 2f, UnityEngine.Random.value * 0.0434f, Vector3.zero );
                                break;
                            case 1:
                                EffectsController.CreateEffect( this.fire2, base.X + vector.x * 4f, base.Y + vector.y * 3f + 2f, UnityEngine.Random.value * 0.0434f, Vector3.zero );
                                break;
                            case 2:
                                EffectsController.CreateEffect( this.fire3, base.X + vector.x * 4f, base.Y + vector.y * 3f + 2f, UnityEngine.Random.value * 0.0434f, Vector3.zero );
                                break;
                        }
                    }
                    this.RunBarrelWarning( this.t, this.explosionCounter );
                    if ( this.explosionCounter <= 0.1f )
                    {
                        this.Death();
                    }
                }
            }
        }

        protected void RunBarrelWarning( float t, float explosionTime )
        {
            if ( explosionTime < 2f )
            {
                this.warningCounter += t;
                if ( this.warningOn && this.warningCounter > 0.0667f )
                {
                    this.warningOn = false;
                    this.warningCounter -= 0.0667f;
                }
                else if ( this.warningCounter > 0.0667f && explosionTime < 0.75f )
                {
                    this.warningOn = true;
                    this.warningCounter -= 0.0667f;
                }
                else if ( this.warningCounter > 0.175f && explosionTime < 1.25f )
                {
                    this.warningOn = true;
                    this.warningCounter -= 0.175f;
                }
                else if ( this.warningCounter > 0.2f )
                {
                    this.warningOn = true;
                    this.warningCounter -= 0.2f;
                }
                this.sprite.SetLowerLeftPixel( ( this.warningOn ? 1 : 0 ) * 16f, 16f );
            }
        }

        public override bool Damage( DamageObject damageObject )
        {
            this.Death();
            return true;
        }

        protected override void PlayBounceSound( bool bounceX, bool bounceY )
        {
            if ( bounceX && Mathf.Abs( this.xI ) < 25 )
            {
                return;
            }

            if ( bounceY && Mathf.Abs( this.yI ) < 25 )
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
            sound?.PlaySoundEffectAt( this.bounceSounds, volume, base.transform.position );
        }

        public override void Death()
        {
            this.MakeEffects();
            this.DestroyGrenade();
        }

        protected override void MakeEffects()
        {
            if ( this.exploded )
            {
                return;
            }
            this.exploded = true;
            float range = 55f;
            EffectsController.CreateExplosionRangePop( base.X, base.Y, -1f, range * 2f );
            EffectsController.CreateSparkShower( base.X, base.Y, 70, 3f, 200f, 0f, 250f, 0.6f, 0.5f );
            EffectsController.CreatePlumes( base.X, base.Y, 3, 8f, 315f, 0f, 0f );
            Vector3 vector = new Vector3( UnityEngine.Random.value, UnityEngine.Random.value );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( base.X + vector.x * range * 0.25f, base.Y + vector.y * range * 0.25f ), UnityEngine.Random.value * 0.5f );
            vector = new Vector3( UnityEngine.Random.value, UnityEngine.Random.value );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Medium, new Vector3( base.X + vector.x * range * 0.25f, base.Y + vector.y * range * 0.25f ), 0f );
            EffectsController.CreateShaderExplosion( EffectsController.ExplosionSize.Large, new Vector3( base.X, base.Y ), 0f );
            SortOfFollow.Shake( 1f );
            this.PlayDeathSound();
            Map.DisturbWildLife( base.X, base.Y, 120f, 5 );
            MapController.DamageGround( this, 12, DamageType.Fire, range, base.X, base.Y, null, false );
            Map.ShakeTrees( base.X, base.Y, 256f, 64f, 128f );
            MapController.BurnUnitsAround_NotNetworked( this, this.playerNum, 1, range * 2f, base.X, base.Y, true, true );
            Map.ExplodeUnits( this, 12, DamageType.Fire, range * 1.1f, range, base.X, base.Y - 6f, 350f, 300f, this.playerNum, false, true, true );
            Map.ExplodeUnits( this, 1, DamageType.Fire, range * 1f, range, base.X, base.Y - 6f, 350f, 300f, this.playerNum, false, true, true );
        }

    }
}
