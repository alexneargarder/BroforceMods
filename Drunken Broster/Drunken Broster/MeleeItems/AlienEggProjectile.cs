using System;
using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using BroMakerLib.Loggers;
using HarmonyLib;
using Rogueforce;
using UnityEngine;
using Utility;

namespace Drunken_Broster.MeleeItems
{
    public class AlienEggProjectile : CustomProjectile
    {
        public AudioClip[] pulseSounds;
        public AudioClip[] burstSounds;
        protected Shrapnel[] shrapnelPrefabs = new Shrapnel[3];
        protected int frame = 0;
        protected float counter = 0;
        protected float frameRate = 0.06f;
        protected int cycle = 0;
        protected bool exploded = false;
        protected float r = 90f;
        protected float rI = 0f;
        protected float rotationSpeedMultiplier = 1.5f;
        protected BloodColor bloodColor = BloodColor.Green;
        protected float explodeRange = 40f;
        public GibHolder openedEggGibHolder;
        protected AlienFaceHugger faceHugger;
        protected bool createdFaceHugger = false;
        protected float spawnFacehuggerTimer = 0.2f;

        protected override void Awake()
        {
            if ( this.Sprite == null )
            {
                this.SpriteLowerLeftPixel = new Vector2( 0, 32 );
                this.SpritePixelDimensions = new Vector2( 32, 32 );
                this.SpriteWidth = 22;
                this.SpriteHeight = 22;
                this.SpriteOffset = new Vector3( 0, 5, 0 );
            }

            this.projectileSize = 8f;

            this.damage = 5;
            this.damageInternal = this.damage;
            this.fullDamage = this.damage;

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            AlienEggBlock prefab = Map.Instance.activeTheme.blockAlienEgg as AlienEggBlock;
            this.openedEggGibHolder = prefab.openedEggGibHolder;

            this.pulseSounds = ResourcesController.GetAudioClipArray( SoundPath, "egg_pulse", 2 );

            this.burstSounds = ResourcesController.GetAudioClipArray( SoundPath, "egg_burst", 3 );
        }

        public override void Fire( float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            this.rI = -Mathf.Sign( xI ) * ( 200f + UnityEngine.Random.value * 200f ) * this.rotationSpeedMultiplier;

            base.Fire( newX, newY, xI, yI, _zOffset, playerNum, FiredBy );

            this.CreateFaceHugger();
        }

        protected void CreateFaceHugger()
        {
            if ( this.createdFaceHugger )
            {
                return;
            }

            this.createdFaceHugger = true;

            this.faceHugger = Map.SpawnFaceHugger( this.X, this.Y, 0f, 0f ) as AlienFaceHugger;
            this.faceHugger.RegisterUnit();
            Registry.RegisterDeterminsiticGameObject( this.faceHugger.gameObject );
            this.faceHugger.transform.parent = base.transform;
            this.faceHugger.ForceStart();
            this.faceHugger.gameObject.SetActive( false );
            this.faceHugger.playerNum = 5;
            this.faceHugger.firingPlayerNum = 5;
            this.faceHugger.gameObject.name = "friend";
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

            // Change frames
            this.counter += this.t;
            if ( this.counter > this.frameRate )
            {
                this.counter -= this.frameRate;
                ++this.frame;
                this.ChangeFrame();
            }

            this.r += this.rI * this.t;

            this.SetRotation();

            base.Update();

            if ( this.spawnFacehuggerTimer > 0 )
            {
                this.spawnFacehuggerTimer -= this.t;
                if ( this.spawnFacehuggerTimer <= 0 )
                {
                    try
                    {
                        this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                    }
                    catch ( Exception ex )
                    {
                        BMLogger.ExceptionLog( ex );
                    }
                    this.Death();
                }
            }
        }

        protected virtual void ApplyGravity()
        {
            this.yI -= 500f * this.t;
        }

        protected void ChangeFrame()
        {
            if ( this.frame == 1 )
            {
                if ( sound == null )
                {
                    sound = Sound.GetInstance();
                }

                sound?.PlaySoundEffectAt( this.pulseSounds, 0.4f, base.transform.position );
            }
            if ( this.frame >= 5 )
            {
                this.frame = 0;
                ++this.cycle;
            }
            this.Sprite.SetLowerLeftPixel_X( 32f * this.frame );
        }

        protected override void TryHitUnitsAtSpawn()
        {
            if ( this.firedBy != null && this.firedBy.GetComponent<TestVanDammeAnim>() != null && this.firedBy.GetComponent<TestVanDammeAnim>().inseminatorUnit != null )
            {
                if ( HitUnits( this.firedBy, this.firedBy.GetComponent<TestVanDammeAnim>().inseminatorUnit, this.playerNum, this.damageInternal * 2, this.damageType, ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ), ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ), base.X - ( ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ) ) * (float)( (int)Mathf.Sign( this.xI ) ), base.Y, this.xI, this.yI ) )
                {
                    this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                    global::UnityEngine.Object.Destroy( base.gameObject );
                    this.hasHit = true;
                }
            }
            else if ( HitUnits( this.firedBy, this.firedBy, this.playerNum, this.damageInternal * 2, this.damageType, ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ), ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ), base.X - ( ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ) ) * (float)( (int)Mathf.Sign( this.xI ) ), base.Y, this.xI, this.yI ) )
            {
                this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                global::UnityEngine.Object.Destroy( base.gameObject );
                this.hasHit = true;
            }
        }

        protected override void HitUnits()
        {
            if ( HitUnits( this.firedBy, this.firedBy, this.playerNum, this.damageInternal, this.damageType, this.projectileSize, this.projectileSize / 2f, base.X, base.Y, this.xI, this.yI ) )
            {
                this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                global::UnityEngine.Object.Destroy( base.gameObject );
                this.hasHit = true;
                if ( this.giveDeflectAchievementOnMookKill )
                {
                    AchievementManager.AwardAchievement( Achievement.bronald_bradman, this.playerNum );
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
                                Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, yI, (int)Mathf.Sign( xI ), false, x, y, false );
                            }
                            else
                            {
                                // Check if can inseminate
                                if ( unit.IsNotReplicantHero && unit.CanInseminate( xI, yI ) )
                                {
                                    this.Death();
                                    // Directly inseminate rather than damaging unit
                                    unit.Inseminate( this.faceHugger, xI, yI );
                                    return false;
                                }
                                else
                                {
                                    Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( damage, playerNum ), damageType, xI, yI, (int)Mathf.Sign( xI ), false, x, y, false );
                                }
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
            this.CreateFaceHugger();

            this.faceHugger.SetXY( base.X, base.Y );
            this.faceHugger.transform.parent = null;
            this.faceHugger.gameObject.SetActive( true );
            this.faceHugger.transform.rotation = Quaternion.identity;
            this.faceHugger.playerNum = 5;
            this.faceHugger.firingPlayerNum = 5;
            Traverse trav = Traverse.Create( this.faceHugger );
            trav.Field( "usingSpecial" ).SetValue( true );
            trav.Method( "UseSpecial" ).GetValue();
            this.faceHugger.xI = this.xI;
            this.faceHugger.yI = this.yI + 65;
            EffectsController.CreateSlimeParticles( BloodColor.Green, base.X, base.Y, 45, 8f, 8f, 140f, this.faceHugger.xI * 0.5f, this.faceHugger.yI * 0.5f );
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( this.exploded )
            {
                return;
            }
            this.exploded = true;

            if ( sound == null )
            {
                sound = Sound.GetInstance();
            }
            sound?.PlaySoundEffectAt( this.burstSounds, 0.55f, base.transform.position );

            this.LaunchFaceHugger();
            EffectsController.CreateGibs( this.openedEggGibHolder, base.X, base.Y, 10f, 10f, 0f, 0f );
            Block.MakeAlienBlockCollapseEffects( base.X, base.Y, xI, yI, true );
        }

    }
}
