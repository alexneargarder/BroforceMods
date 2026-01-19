using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using UnityEngine;
using Utility;

namespace Drunken_Broster.MeleeItems
{
    public class BeehiveProjectile : CustomProjectile
    {
        protected GibHolder gibHolder;
        public RealisticAngryBeeSimulator beeSimulator;
        public RealisticFlySimulatorClass[] flies;
        protected float r = 90f;
        protected float rI;
        protected float rotationSpeedMultiplier = 1.25f;
        protected bool madeEffects;
        public AudioClip[] deathSounds;

        protected override void Awake()
        {
            SpriteHeight = 14f;
            SpriteWidth = 14f;

            damage = 5;
            damageInternal = damage;
            fullDamage = damage;

            projectileSize = 10f;

            DoodadBeehive beehive = Map.Instance.activeTheme.blockBeeHive as DoodadBeehive;
            gibHolder = beehive.gibHolder;

            if ( beeSimulator == null )
            {
                RealisticFlySimulatorClass flyPrefab = beehive.GetComponentInChildren<RealisticFlySimulatorClass>();

                beeSimulator = Instantiate( beehive.GetComponentInChildren<RealisticAngryBeeSimulator>(), transform, true );
                beeSimulator.gameObject.SetActive( false );

                flies = new RealisticFlySimulatorClass[18];
                for ( int i = 0; i < 18; ++i )
                {
                    flies[i] = Instantiate( flyPrefab );
                    flies[i].transform.parent = beeSimulator.transform;
                    flies[i].optionalFollowTransform = beeSimulator.transform;
                    flies[i].gameObject.SetActive( false );
                }

                beeSimulator.flies = flies;
            }

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            deathSounds = ResourcesController.GetAudioClipArray( SoundPath, "beeHiveSmash", 3 );
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

            r += rI * t;

            SetRotation();

            base.Update();
        }

        protected virtual void ApplyGravity()
        {
            yI -= 500f * t;
        }

        protected override void HitUnits()
        {
            if ( reversing )
            {
                if ( Map.HitLivingUnits( firedBy ?? this, playerNum, damageInternal, damageType, projectileSize - 2f, projectileSize + 1f, X, Y, xI, yI, false, false ) )
                {
                    MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                    Destroy( gameObject );
                    hasHit = true;
                    if ( giveDeflectAchievementOnMookKill )
                    {
                        AchievementManager.AwardAchievement( Achievement.bronald_bradman, playerNum );
                    }
                }
            }
            else if ( Map.HitUnits( firedBy, firedBy, playerNum, damageInternal, damageType, projectileSize - 2f, projectileSize + 1f, X, Y, xI, yI, false, false, false, false ) )
            {
                MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                Destroy( gameObject );
                hasHit = true;
                if ( giveDeflectAchievementOnMookKill )
                {
                    AchievementManager.AwardAchievement( Achievement.bronald_bradman, playerNum );
                }
            }
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( madeEffects )
            {
                return;
            }
            madeEffects = true;

            if ( sound == null )
            {
                sound = Sound.GetInstance();
            }
            sound?.PlaySoundEffectAt( deathSounds, 0.4f, transform.position );

            EffectsController.CreateGibs( gibHolder, transform.position.x, transform.position.y, 140f, 170f, 0f, 140f );
            EffectsController.CreateDustParticles( transform.position.x, transform.position.y, 140, 6f, 130f, 0f, 100f, new Color( 0.854901969f, 0.65882355f, 0.172549024f, 0.9f ) );
            beeSimulator.Restart();
        }

    }
}
