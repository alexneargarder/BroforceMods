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

        protected override void Awake()
        {
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
            this.yI -= 500f * this.t;
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            EffectsController.CreateGibs( this.gibHolder, base.transform.position.x, base.transform.position.y, 140f, 170f, 0f, 140f );
            EffectsController.CreateDustParticles( base.transform.position.x, base.transform.position.y, 140, 6f, 130f, 0f, 100f, new Color( 0.854901969f, 0.65882355f, 0.172549024f, 0.9f ) );
            this.beeSimulator.Restart();
        }

    }
}
