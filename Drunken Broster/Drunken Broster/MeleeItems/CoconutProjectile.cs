using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class CoconutProjectile : CustomGrenade
    {
        protected int bounces;
        protected int bulletHits;
        protected int maxBounces = 6;
        protected int maxBulletHits = 5;
        protected bool shouldKillIfNotVisible = true;
        protected float clearHitEnemiesCooldown;

        protected override void Awake()
        {
            base.Awake();

            damage = 8;
            useAngularFriction = true;
            angularFrictionM = 5;
            bounceOffEnemies = true;
            bounceOffEnemiesMultiple = false;
            shootable = true;
            rotateAtRightAngles = false;
            size = 5;
            lifeM = 0.8f;
            frictionM = 0.6f;
            shrink = false;
        }

        public override void PrefabSetup()
        {
            // Load death sounds
            soundHolder.deathSounds = new AudioClip[2];
            soundHolder.deathSounds[0] = ResourcesController.GetAudioClip( SoundPath, "coconutDeath1.wav" );
            soundHolder.deathSounds[1] = ResourcesController.GetAudioClip( SoundPath, "coconutDeath2.wav" );

            // Load hit sounds
            soundHolder.hitSounds = new AudioClip[5];
            soundHolder.hitSounds[0] = ResourcesController.GetAudioClip( SoundPath, "coconutHit1.wav" );
            soundHolder.hitSounds[1] = ResourcesController.GetAudioClip( SoundPath, "coconutHit2.wav" );
            soundHolder.hitSounds[2] = ResourcesController.GetAudioClip( SoundPath, "coconutHit3.wav" );
            soundHolder.hitSounds[3] = ResourcesController.GetAudioClip( SoundPath, "coconutHit4.wav" );
            soundHolder.hitSounds[4] = ResourcesController.GetAudioClip( SoundPath, "coconutHit5.wav" );
        }

        protected override bool Update()
        {
            clearHitEnemiesCooldown += t;
            if ( clearHitEnemiesCooldown > 1f )
            {
                clearHitEnemiesCooldown -= 1f;
                alreadyBouncedOffUnits.Clear();
            }

            return base.Update();
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            Bounce( bounceX, bounceY, false );
        }

        protected void Bounce( bool bounceX, bool bounceY, bool bounceEnemy )
        {
            if ( bounceY && yI < -60f )
            {
                EffectsController.CreateLandPoofEffect( X, Y - size, Random.Range( 0, 2 ) * 2 - 1 );
            }
            if ( Mathf.Abs( xI ) + Mathf.Abs( yI ) > 150f || ( !bounceX && !bounceY && yI < -50f ) )
            {
                bounces++;
                if ( bounces > maxBounces )
                {
                    life = -0.1f;
                }
                Map.DisturbWildLife( X, Y, 32f, playerNum );
            }

            // Base bounce
            if ( bounceX )
            {
                yI *= 0.8f;
                if ( useAngularFriction )
                {
                    rI = yI * -Mathf.Sign( xI ) * angularFrictionM;
                }
                int num = (int)Mathf.Sign( xI );
                RaycastHit raycastHit;
                Physics.Raycast( new Vector3( X, Y + 5f, 0f ), Vector3.right * (float)num, out raycastHit, 12f, Map.fragileLayer );
                DoorDoodad doorDoodad = null;
                if ( raycastHit.collider )
                {
                    doorDoodad = raycastHit.collider.GetComponent<DoorDoodad>();
                }
                if ( doorDoodad )
                {
                    doorDoodad.Open( num );
                }
                else
                {
                    xI *= -0.6f;
                }
            }
            if ( bounceY )
            {
                if ( yI < 0f )
                {
                    xI *= 0.8f * frictionM;
                    if ( useAngularFriction )
                    {
                        rI = -xI * angularFrictionM;
                    }
                    RaycastHit raycastHit2;
                    if ( yI < -40f && IsMine && Physics.Raycast( new Vector3( X, Y + 5f, 0f ), Vector3.down, out raycastHit2, 12f, groundLayer ) )
                    {
                        raycastHit2.collider.SendMessage( "StepOn", this, SendMessageOptions.DontRequireReceiver );
                        Block component = raycastHit2.collider.GetComponent<Block>();
                        if ( component != null )
                        {
                            component.CheckForMine();
                        }
                    }
                }
                else
                {
                    xI *= 0.8f;
                    if ( useAngularFriction )
                    {
                        rI = xI * angularFrictionM;
                    }
                }
                yI *= -0.4f;
            }
            float num2 = Mathf.Abs( xI ) + Mathf.Abs( yI );
            if ( num2 > minVelocityBounceSound && Time.timeSinceLevelLoad > 0.4f )
            {
                float num3 = num2 / maxVelocityBounceVolume;
                float num4 = 0.05f + Mathf.Clamp( num3 * num3, 0f, 0.25f ) + ( bounceEnemy ? 0.3f : 0f );
                Sound.GetInstance().PlaySoundEffectAt( soundHolder.hitSounds, num4, transform.position );
            }
            if ( deathOnBounce )
            {
                Death();
            }
        }

        // Allow bouncing off enemies when moving horizontally
        protected override void BounceOffEnemies()
        {
            // Don't allow bounces off enemies if nearly at rest
            if ( Mathf.Abs( xI ) < 25 && Mathf.Abs( yI ) < 25 )
            {
                return;
            }

            if ( bounceOffEnemiesMultiple )
            {
                if ( Map.HitAllLivingUnits( firedBy, playerNum, damage, DamageType.Bounce, size - 2f, size + 2f, X, Y, xI, 30f, false, true ) )
                {
                    Bounce( false, false, true );
                    yI = 50f + 90f / weight;
                    xI = Mathf.Clamp( xI * 0.25f, -100f, 100f );
                    EffectsController.CreateBulletPoofEffect( X, Y - size * 0.5f );
                }
            }
            else if ( Map.HitAllLivingUnits( firedBy, playerNum, damage, DamageType.Bounce, size - 2f, size + 2f, X, Y, xI, 30f, false, true, alreadyBouncedOffUnits ) )
            {
                Bounce( false, false, true );
                if ( alreadyBouncedOffUnits.Count > 0 )
                {
                    yI = ( 50f + 90f / weight ) / (float)alreadyBouncedOffUnits.Count;
                }
                else
                {
                    yI = 50f + 90f / weight;
                }
                xI = Mathf.Clamp( xI * 0.25f, -100f, 100f );
                EffectsController.CreateBulletPoofEffect( X, Y - size * 0.5f );
            }
        }

        public override void Knock( float xDiff, float yDiff, float xI, float yI )
        {
            base.Knock( xDiff, yDiff, xI, yI );
            transform.parent = null;
            bulletHits++;
            if ( bulletHits > 2 )
            {
                bounces = (int)( (float)maxBounces * 1.667f );
            }
            if ( bulletHits > maxBulletHits )
            {
                life = -0.1f;
            }
        }

        protected override void RunLife()
        {
            if ( life <= 0f )
            {
                Death();
            }
        }

        public override void Death()
        {
            PlayDeathSound( 0.5f );
            EffectsController.CreateDirtParticles( X, Y, 12, 2f, 150f, xI * 0.5f, yI + 100f );
            EffectsController.CreateSemenParticles( BloodColor.Red, X, Y, 0f, 20, 2f, 2f, 150f, xI * 0.5f, 100f );
            DestroyGrenade();
        }

        public override bool ShouldKillIfNotVisible
        {
            get
            {
                return shouldKillIfNotVisible;
            }
            set
            {
            }
        }
    }
}
