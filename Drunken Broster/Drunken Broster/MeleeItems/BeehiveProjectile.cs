using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class BeehiveProjectile : CustomProjectile
    {
        protected GibHolder gibHolder;
        public RealisticAngryBeeSimulator beeSimulator;
        public RealisticFlySimulatorClass[] flies;
        protected float r = 90f;
        protected float rI = 0f;
        protected float rotationSpeedMultiplier = 1.25f;
        protected bool madeEffects = false;
        public AudioClip[] beeHiveSmashSounds;

        protected override void Awake()
        {
            this.spriteHeight = 14f;
            this.spriteWidth = 14f;

            this.damage = 5;
            this.damageInternal = this.damage;
            this.fullDamage = this.damage;

            this.projectileSize = 10f;

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

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            this.beeHiveSmashSounds = ResourcesController.GetAudioClipArray( soundPath, "beeHiveSmash", 3 );
        }

        public override void Fire( float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            this.rI = -Mathf.Sign( xI ) * ( 200f + UnityEngine.Random.value * 200f ) * this.rotationSpeedMultiplier;

            base.Fire( newX, newY, xI, yI, _zOffset, playerNum, FiredBy );
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

            base.transform.eulerAngles = new Vector3( 0f, 0f, this.r );
        }

        protected override void Update()
        {
            this.ApplyGravity();

            this.r += this.rI * this.t;

            this.SetRotation();

            base.Update();
        }

        protected virtual void ApplyGravity()
        {
            this.yI -= 500f * this.t;
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( this.madeEffects )
            {
                return;
            }
            this.madeEffects = true;
            
            if ( this.sound == null )
            {
                this.sound = Sound.GetInstance();
            }
            this.sound?.PlaySoundEffectAt( this.beeHiveSmashSounds, 0.4f, base.transform.position );

            EffectsController.CreateGibs( this.gibHolder, base.transform.position.x, base.transform.position.y, 140f, 170f, 0f, 140f );
            EffectsController.CreateDustParticles( base.transform.position.x, base.transform.position.y, 140, 6f, 130f, 0f, 100f, new Color( 0.854901969f, 0.65882355f, 0.172549024f, 0.9f ) );
            this.beeSimulator.Restart();
        }

    }
}
