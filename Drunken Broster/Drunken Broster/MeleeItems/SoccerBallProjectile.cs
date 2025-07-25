﻿using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using BroMakerLib.Loggers;
using RocketLib;
using Rogueforce;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class SoccerBallProjectile : CircularProjectile
    {
        public AudioClip[] bounceSounds;

        protected override void Awake()
        {
            if ( this.sprite == null )
            {
                this.spriteLowerLeftPixel = new Vector2( 0, 10 );
                this.spritePixelDimensions = new Vector2( 10, 10 );
                this.spriteWidth = 10f;
                this.spriteHeight = 10f;
            }

            base.Awake();

            this.size = 5f;
            this.bounceM = 1.2f;
            this.bounceGroundDamage = 3;
            this.hitUnitForce = 0.75f;
            this.bounceOffEnemies = true;
            this.rotationSpeedMultiplier = 6f;
        }

        public override void PrefabSetup()
        {
            // Load death sound
            this.soundHolder.deathSounds = new AudioClip[1];
            this.soundHolder.deathSounds[0] = ResourcesController.GetAudioClip( soundPath, "tireDeath.wav" );

            try
            {
                this.bounceSounds = new AudioClip[3];
                this.bounceSounds[0] = ResourcesController.GetAudioClip( soundPath, "soccerBounce1.wav" );
                this.bounceSounds[1] = ResourcesController.GetAudioClip( soundPath, "soccerBounce2.wav" );
                this.bounceSounds[2] = ResourcesController.GetAudioClip( soundPath, "soccerBounce3.wav" );
            }
            catch ( Exception e )
            {
                BMLogger.ExceptionLog( e );
            }
        }

        protected override void HitUnits()
        {
            if ( Mathf.Abs( this.xI ) > 80f && Map.HitUnits( this, this.playerNum, this.damage, this.damage, this.damageType, this.size + 1f, this.size + 4f, this.X, this.Y, this.xI * this.hitUnitForce, this.yI * this.hitUnitForce, true, true, false, this.alreadyHitUnits, false, false ) )
            {
                //EffectsController.CreateEffect( EffectsController.instance.whiteFlashPopSmallPrefab, base.X + base.transform.localScale.x * 2f, base.Y + UnityEngine.Random.Range( -1, 1 ), 0f, 0f, Vector3.zero, null );
                //EffectsController.CreateProjectilePopWhiteEffect( base.X + base.transform.localScale.x * 3f, base.Y + UnityEngine.Random.Range( -1, 1 ) );
                this.hitDelay = 0.1f;
                if ( this.bounceOffEnemies )
                {
                    float previousBounceM = this.bounceM;
                    this.bounceM *= 0.3f;
                    this.Bounce( true, false );
                    this.bounceM = previousBounceM;
                }
            }
        }

        protected override void PlayBounceSound(bool bounceX, bool bounceY)
        {
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
            //this.CreateGibs( this.xI, this.yI );
            this.PlayDeathSound();
        }
    }
}