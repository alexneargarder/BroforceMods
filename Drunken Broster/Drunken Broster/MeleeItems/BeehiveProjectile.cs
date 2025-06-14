using BroMakerLib;
using BroMakerLib.Loggers;
using Rogueforce.PerkSystem.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Windows.Speech;

namespace Drunken_Broster.MeleeItems
{
    public class BeehiveProjectile : Projectile
    {
        public static Material storedMat;
        public SpriteSM sprite;
        protected GibHolder gibHolder;
        public RealisticAngryBeeSimulator beeSimulator;
        public RealisticFlySimulatorClass[] flies;

        protected override void Awake()
        {
            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            directoryPath = Path.Combine( directoryPath, "projectiles" );
            storedMat = ResourcesController.GetMaterial( directoryPath, "Beehive.png" );

            renderer.material = storedMat;

            sprite = this.gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2( 0, 16 );
            sprite.pixelDimensions = new Vector2( 16, 16 );

            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 16;
            sprite.height = 16;
            sprite.offset = new Vector3( 0, 0, 0 );

            this.projectileSize = 8f;

            this.damage = 5;
            this.damageInternal = this.damage;
            this.fullDamage = this.damage;

            DoodadBeehive beehive = Map.Instance.activeTheme.blockBeeHive as DoodadBeehive;
            this.gibHolder = beehive.gibHolder;

            if ( this.beeSimulator == null )
            {
                RealisticFlySimulatorClass flyPrefab = beehive.GetComponentInChildren<RealisticFlySimulatorClass>();

                this.beeSimulator = UnityEngine.Object.Instantiate( beehive.GetComponentInChildren<RealisticAngryBeeSimulator>() );
                this.beeSimulator.transform.parent = this.transform;
                this.beeSimulator.gameObject.SetActive( false );

                this.flies = new RealisticFlySimulatorClass[18];
                for ( int i = 0; i < 18; ++i )
                {
                    this.flies[i] = UnityEngine.Object.Instantiate( flyPrefab );
                    this.flies[i].transform.parent = this.beeSimulator.transform;
                    this.flies[i].optionalFollowTransform = this.beeSimulator.transform;
                    this.flies[i].gameObject.SetActive( false );
                }

                this.beeSimulator.flies = this.flies;
            }
            
            base.Awake();
        }

        protected override void SetRotation()
        {
            // Don't rotate based on momentum
            if ( this.xI > 0f )
            {
                base.transform.localScale = new Vector3( -1f, 1f, 1f );
            }
            else
            {
                base.transform.localScale = new Vector3( 1f, 1f, 1f );
            }

            base.transform.eulerAngles = new Vector3( 0f, 0f, 0f );
        }

        protected override void Update()
        {
            this.ApplyGravity();

            base.Update();
        }

        protected virtual void ApplyGravity()
        {
            this.yI -= 450f * this.t;
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            EffectsController.CreateGibs( this.gibHolder, base.transform.position.x, base.transform.position.y, 140f, 170f, 0f, 140f );
            EffectsController.CreateDustParticles( base.transform.position.x, base.transform.position.y, 140, 6f, 130f, 0f, 100f, new Color( 0.854901969f, 0.65882355f, 0.172549024f, 0.9f ) );
            this.beeSimulator.Restart();
        }

    }
}
