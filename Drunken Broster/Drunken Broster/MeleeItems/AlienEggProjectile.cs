using System;
using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using BroMakerLib.Loggers;
using HarmonyLib;
using Rogueforce;
using UnityEngine;
using Utility;
using Random = UnityEngine.Random;

namespace Drunken_Broster.MeleeItems
{
    public class AlienEggProjectile : CustomProjectile
    {
        public AudioClip[] pulseSounds;
        public AudioClip[] burstSounds;
        protected Shrapnel[] shrapnelPrefabs = new Shrapnel[3];
        protected int frame;
        protected float counter;
        protected float frameRate = 0.06f;
        protected int cycle;
        protected bool exploded;
        protected float r = 90f;
        protected float rI;
        protected float rotationSpeedMultiplier = 1.5f;
        protected BloodColor bloodColor = BloodColor.Green;
        protected float explodeRange = 40f;
        public GibHolder openedEggGibHolder;
        protected AlienFaceHugger faceHugger;
        protected bool createdFaceHugger;
        protected float spawnFacehuggerTimer = 0.2f;

        protected override void Awake()
        {
            if ( Sprite == null )
            {
                SpriteLowerLeftPixel = new Vector2( 0, 32 );
                SpritePixelDimensions = new Vector2( 32, 32 );
                SpriteWidth = 22;
                SpriteHeight = 22;
                SpriteOffset = new Vector3( 0, 5, 0 );
            }

            projectileSize = 8f;

            damage = 5;
            damageInternal = damage;
            fullDamage = damage;

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            AlienEggBlock prefab = Map.Instance.activeTheme.blockAlienEgg as AlienEggBlock;
            openedEggGibHolder = prefab.openedEggGibHolder;

            pulseSounds = ResourcesController.GetAudioClipArray( SoundPath, "egg_pulse", 2 );

            burstSounds = ResourcesController.GetAudioClipArray( SoundPath, "egg_burst", 3 );
        }

        public override void Fire( float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            rI = -Mathf.Sign( xI ) * ( 200f + Random.value * 200f ) * rotationSpeedMultiplier;

            base.Fire( newX, newY, xI, yI, _zOffset, playerNum, FiredBy );

            CreateFaceHugger();
        }

        protected void CreateFaceHugger()
        {
            if ( createdFaceHugger )
            {
                return;
            }

            createdFaceHugger = true;

            faceHugger = Map.SpawnFaceHugger( X, Y, 0f, 0f ) as AlienFaceHugger;
            faceHugger.RegisterUnit();
            Registry.RegisterDeterminsiticGameObject( faceHugger.gameObject );
            faceHugger.transform.parent = transform;
            faceHugger.ForceStart();
            faceHugger.gameObject.SetActive( false );
            faceHugger.playerNum = 5;
            faceHugger.firingPlayerNum = 5;
            faceHugger.gameObject.name = "friend";
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

            // Change frames
            counter += t;
            if ( counter > frameRate )
            {
                counter -= frameRate;
                ++frame;
                ChangeFrame();
            }

            r += rI * t;

            SetRotation();

            base.Update();

            if ( spawnFacehuggerTimer > 0 )
            {
                spawnFacehuggerTimer -= t;
                if ( spawnFacehuggerTimer <= 0 )
                {
                    try
                    {
                        MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                    }
                    catch ( Exception ex )
                    {
                        BMLogger.ExceptionLog( ex );
                    }
                    Death();
                }
            }
        }

        protected virtual void ApplyGravity()
        {
            yI -= 500f * t;
        }

        protected void ChangeFrame()
        {
            if ( frame == 1 )
            {
                if ( sound == null )
                {
                    sound = Sound.GetInstance();
                }

                sound?.PlaySoundEffectAt( pulseSounds, 0.4f, transform.position );
            }
            if ( frame >= 5 )
            {
                frame = 0;
                ++cycle;
            }
            Sprite.SetLowerLeftPixel_X( 32f * frame );
        }

        protected override void TryHitUnitsAtSpawn()
        {
            if ( firedBy != null && firedBy.GetComponent<TestVanDammeAnim>() != null && firedBy.GetComponent<TestVanDammeAnim>().inseminatorUnit != null )
            {
                if ( HitUnits( firedBy, firedBy.GetComponent<TestVanDammeAnim>().inseminatorUnit, playerNum, damageInternal * 2, damageType, ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ), ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ), X - ( ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ) ) * (float)( (int)Mathf.Sign( xI ) ), Y, xI, yI ) )
                {
                    MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                    Destroy( gameObject );
                    hasHit = true;
                }
            }
            else if ( HitUnits( firedBy, firedBy, playerNum, damageInternal * 2, damageType, ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ), ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ), X - ( ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ) ) * (float)( (int)Mathf.Sign( xI ) ), Y, xI, yI ) )
            {
                MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                Destroy( gameObject );
                hasHit = true;
            }
        }

        protected override void HitUnits()
        {
            if ( HitUnits( firedBy, firedBy, playerNum, damageInternal, damageType, projectileSize, projectileSize / 2f, X, Y, xI, yI ) )
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

        protected bool HitUnits( MonoBehaviour damageSender, MonoBehaviour avoidID, int playerNum, int damage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI )
        {
            if ( Map.units == null )
            {
                return false;
            }
            bool flag = false;
            int num = 999999;
            bool flag2 = false;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                Unit unit = Map.units[i];
                if ( unit != null && ( GameModeController.DoesPlayerNumDamage( playerNum, unit.playerNum ) || ( unit.playerNum < 0 && unit.CatchFriendlyBullets() ) ) && !unit.invulnerable && unit.health <= num )
                {
                    float num2 = unit.X - x;
                    if ( Mathf.Abs( num2 ) - xRange < unit.width )
                    {
                        float num3 = unit.Y + unit.height / 2f + 4f - y;
                        if ( Mathf.Abs( num3 ) - yRange < unit.height && ( avoidID == null || avoidID != unit || unit.CatchFriendlyBullets() ) )
                        {
                            if ( unit.health > 0 )
                            {
                                num = 0;
                                flag2 = true;
                            }
                            if ( unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, yI, (int)Mathf.Sign( xI ), false, x, y );
                            }
                            else
                            {
                                // Check if can inseminate
                                if ( unit.IsNotReplicantHero && unit.CanInseminate( xI, yI ) )
                                {
                                    Death();
                                    // Directly inseminate rather than damaging unit
                                    unit.Inseminate( faceHugger, xI, yI );
                                    return false;
                                }

                                Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( damage, playerNum ), damageType, xI, yI, (int)Mathf.Sign( xI ), false, x, y );
                            }
                            bloodColor = unit.bloodColor;
                            flag = true;
                            if ( flag2 )
                            {
                                return flag;
                            }
                        }
                    }
                }
            }
            return flag;
        }

        protected void LaunchFaceHugger()
        {
            // Ensure facehugger has been created
            CreateFaceHugger();

            faceHugger.SetXY( X, Y );
            faceHugger.transform.parent = null;
            faceHugger.gameObject.SetActive( true );
            faceHugger.transform.rotation = Quaternion.identity;
            faceHugger.playerNum = 5;
            faceHugger.firingPlayerNum = 5;
            Traverse trav = Traverse.Create( faceHugger );
            trav.Field( "usingSpecial" ).SetValue( true );
            trav.Method( "UseSpecial" ).GetValue();
            faceHugger.xI = xI;
            faceHugger.yI = yI + 65;
            EffectsController.CreateSlimeParticles( BloodColor.Green, X, Y, 45, 8f, 8f, 140f, faceHugger.xI * 0.5f, faceHugger.yI * 0.5f );
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( exploded )
            {
                return;
            }
            exploded = true;

            if ( sound == null )
            {
                sound = Sound.GetInstance();
            }
            sound?.PlaySoundEffectAt( burstSounds, 0.55f, transform.position );

            LaunchFaceHugger();
            EffectsController.CreateGibs( openedEggGibHolder, X, Y, 10f, 10f, 0f, 0f );
            Block.MakeAlienBlockCollapseEffects( X, Y, xI, yI );
        }

    }
}
