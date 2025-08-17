using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using RocketLib;
using Rogueforce;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class TireProjectile : CircularProjectile
    {
        protected GibHolder gibs;

        protected override void Awake()
        {
            if ( this.sprite == null )
            {
                this.spriteLowerLeftPixel = new Vector2( 0, 16 );
                this.spritePixelDimensions = new Vector2( 16, 16 );
                this.spriteWidth = 16f;
                this.spriteHeight = 16f;
            }

            // Setup gibs
            if ( this.gibs == null )
            {
                this.InitializeGibs();
            }

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();
            // Load death sound
            this.soundHolder.deathSounds = new AudioClip[1];
            this.soundHolder.deathSounds[0] = ResourcesController.GetAudioClip( soundPath, "tireDeath.wav" );

            this.bounceSounds = new AudioClip[2];
            this.bounceSounds[0] = ResourcesController.GetAudioClip( soundPath, "soccerBounce2.wav" );
            this.bounceSounds[1] = ResourcesController.GetAudioClip( soundPath, "soccerBounce3.wav" );
        }

        protected void CreateGib( string name, Vector2 lowerLeftPixel, Vector2 pixelDimensions, float width, float height, Vector3 localPositionOffset )
        {
            BroMakerUtilities.CreateGibPrefab( name, lowerLeftPixel, pixelDimensions, width, height, new Vector3( 0f, 0f, 0f ), localPositionOffset, false, DoodadGibsType.Metal, 6, false, BloodColor.None, 1, true, 8, false, false, 3, 1, 1, 5f ).transform.parent = this.gibs.transform;
        }

        protected void InitializeGibs()
        {
            this.gibs = new GameObject( "TireProjectileGibs", new Type[] { typeof( GibHolder ) } ).GetComponent<GibHolder>();
            this.gibs.gameObject.SetActive( false );
            UnityEngine.Object.DontDestroyOnLoad( this.gibs );
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
            for ( int i = 0; i < this.gibs.transform.childCount; ++i )
            {
                gibs.transform.GetChild( i ).gameObject.layer = 19;
            }
        }

        protected virtual void CreateGibs( float xI, float yI )
        {
            xI = xI * 0.25f;
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
                    EffectsController.CreateGib( child.GetComponent<Gib>(), base.GetComponent<Renderer>().sharedMaterial, base.X, base.Y, xForce * ( 0.8f + UnityEngine.Random.value * 0.4f ), yForce * ( 0.8f + UnityEngine.Random.value * 0.4f ), xI, yI, 1 );
                }
            }
        }

        protected override void PlayBounceSound( bool bounceX, bool bounceY )
        {
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
            sound?.PlaySoundEffectAt( this.bounceSounds, volume, base.transform.position, 0.35f );
        }

        public override void Death()
        {
            if ( !this.dontMakeEffects )
            {
                this.MakeEffects();
            }
            this.DestroyGrenade();
        }

        protected override void MakeEffects()
        {
            float speed = 80f;
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( 0, speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( 0, -speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( speed, 0 ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( -speed, 0 ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( speed, speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( speed, -speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( -speed, speed ) );
            EffectsController.CreateSmoke( base.X, base.Y - 3f, 0f, new Vector3( -speed, -speed ) );
            this.CreateGibs( this.xI, this.yI );
            this.PlayDeathSound();
        }
    }
}