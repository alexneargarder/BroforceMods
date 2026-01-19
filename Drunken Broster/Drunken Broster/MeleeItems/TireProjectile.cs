using System;
using BroMakerLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Drunken_Broster.MeleeItems
{
    public class TireProjectile : CircularProjectile
    {
        protected GibHolder gibs;

        protected override void Awake()
        {
            if ( sprite == null )
            {
                SpriteLowerLeftPixel = new Vector2( 0, 16 );
                SpritePixelDimensions = new Vector2( 16, 16 );
                spriteWidth = 16f;
                spriteHeight = 16f;
            }

            // Setup gibs
            if ( gibs == null )
            {
                InitializeGibs();
            }

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();
            // Load death sound
            soundHolder.deathSounds = new AudioClip[1];
            soundHolder.deathSounds[0] = ResourcesController.GetAudioClip( SoundPath, "tireDeath.wav" );

            bounceSounds = new AudioClip[2];
            bounceSounds[0] = ResourcesController.GetAudioClip( SoundPath, "soccerBounce2.wav" );
            bounceSounds[1] = ResourcesController.GetAudioClip( SoundPath, "soccerBounce3.wav" );
        }

        protected void CreateGib( string name, Vector2 lowerLeftPixel, Vector2 pixelDimensions, float width, float height, Vector3 localPositionOffset )
        {
            BroMakerUtilities.CreateGibPrefab( name, lowerLeftPixel, pixelDimensions, width, height, new Vector3( 0f, 0f, 0f ), localPositionOffset, false, DoodadGibsType.Metal, 6, false, BloodColor.None, 1, true, 8, false, false, 3, 1, 1, 5f ).transform.parent = gibs.transform;
        }

        protected void InitializeGibs()
        {
            gibs = new GameObject( "TireProjectileGibs", new Type[] { typeof( GibHolder ) } ).GetComponent<GibHolder>();
            gibs.gameObject.SetActive( false );
            DontDestroyOnLoad( gibs );
            CreateGib( "TireHub", new Vector2( 22, 12 ), new Vector2( 8, 8 ), 8f, 8f, new Vector3( 0f, 0f, 0f ) );
            CreateGib( "TireLeftPiece", new Vector2( 18, 12 ), new Vector2( 3, 8 ), 3f, 8f, new Vector3( -6f, 0f, 0f ) );
            CreateGib( "TireBottomPiece", new Vector2( 22, 15 ), new Vector2( 8, 3 ), 8f, 3f, new Vector3( 0f, -6f, 0f ) );
            CreateGib( "TireRightPiece", new Vector2( 31, 12 ), new Vector2( 3, 8 ), 3f, 8f, new Vector3( 6f, 0f, 0f ) );
            CreateGib( "TireTopPiece", new Vector2( 22, 4 ), new Vector2( 8, 3 ), 8f, 3f, new Vector3( 0f, 6f, 0f ) );

            CreateGib( "TireBottomLeftPiece", new Vector2( 36, 4 ), new Vector2( 3, 3 ), 3f, 3f, new Vector3( -3f, -3f, 0f ) );
            CreateGib( "TireBottomRightPiece", new Vector2( 36, 8 ), new Vector2( 3, 3 ), 3f, 3f, new Vector3( 3f, -3f, 0f ) );
            CreateGib( "TireTopLeftPiece", new Vector2( 40, 8 ), new Vector2( 3, 3 ), 3f, 3f, new Vector3( -3f, 3f, 0f ) );
            CreateGib( "TireTopRightPiece", new Vector2( 40, 4 ), new Vector2( 3, 3 ), 3f, 3f, new Vector3( 3f, 3f, 0f ) );

            // Make sure gibs are on layer 19 since the texture they're using is transparent
            for ( int i = 0; i < gibs.transform.childCount; ++i )
            {
                gibs.transform.GetChild( i ).gameObject.layer = 19;
            }
        }

        protected virtual void CreateGibs( float xI, float yI )
        {
            xI *= 0.25f;
            yI = yI * 0.25f + 80f;
            float xForce = 200f;
            float yForce = 300f;
            if ( gibs == null || gibs.transform == null )
            {
                return;
            }
            for ( int i = 0; i < gibs.transform.childCount; i++ )
            {
                Transform child = gibs.transform.GetChild( i );
                if ( child != null )
                {
                    EffectsController.CreateGib( child.GetComponent<Gib>(), GetComponent<Renderer>().sharedMaterial, X, Y, xForce * ( 0.8f + Random.value * 0.4f ), yForce * ( 0.8f + Random.value * 0.4f ), xI, yI, 1 );
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

            float volume = 0.9f;
            if ( bounceX && bounceY )
            {
                volume *= Mathf.Max( Mathf.Abs( xI ), Mathf.Abs( yI ) ) / 250f;
            }
            else if ( bounceX )
            {
                volume *= Mathf.Abs( xI ) / 250f;
            }
            else
            {
                volume *= Mathf.Abs( yI ) / 250f;
            }
            sound?.PlaySoundEffectAt( bounceSounds, volume, transform.position, 0.35f );
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
            CreateGibs( xI, yI );
            PlayDeathSound();
        }
    }
}