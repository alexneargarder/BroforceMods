using System;
using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RJBrocready
{
    [HeroPreset("R.J. Brocready", HeroType.Rambro)]
    public class Bro : CustomHero
    {
        // Primary
        Projectile[] projectiles;
        AudioSource flameSource;
        AudioClip flameStart, flameLoop, flameBurst;
        protected float fireFlashCounter = 0f;
        protected float burstCooldown = 0f;
        protected int burstCount = 0;
        protected const float originalFirerate = 0.2f;

        // Special

        // Melee

        // Misc
        protected bool theThing = false;
        protected bool acceptedDeath = false;
        protected bool wasInvulnerable = false;

        #region General
        protected override void Awake()
        {
            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            BaBroracus bro = HeroController.GetHeroPrefab(HeroType.BaBroracus) as BaBroracus;
            projectiles = new Projectile[] { bro.projectile, bro.projectile2, bro.projectile3 };
            this.flameBurst = bro.flameSoundEnd;

            this.flameStart = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "flameStart.wav");
            this.flameLoop = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "flameLoop.wav");

            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            this.flameSource = base.gameObject.AddComponent<AudioSource>();
            this.flameSource.rolloffMode = AudioRolloffMode.Linear;
            this.flameSource.maxDistance = 500f;
            this.flameSource.spatialBlend = 1f;
            this.flameSource.volume = 0.4f;
            this.flameSource.playOnAwake = false;
            this.flameSource.Stop();
        }

        protected override void Update()
        {
            if (this.invulnerable)
            {
                this.wasInvulnerable = true;
            }

            base.Update();
            if (this.acceptedDeath)
            {
                if (this.health <= 0 && !this.WillReviveAlready)
                {
                    return;
                }
                // Revived
                else
                {
                    // Handle revive
                    this.acceptedDeath = false;
                }
            }

            // Stop flamethrower when getting on helicopter
            if (this.isOnHelicopter)
            {
                this.flameSource.enabled = false;
            }

            // Check if invulnerability ran out
            if (this.wasInvulnerable && !this.invulnerable)
            {
                base.GetComponent<Renderer>().material.SetColor("_TintColor", Color.gray);
                gunSprite.meshRender.material.SetColor("_TintColor", Color.gray);
            }

            // Handle death
            if (base.actionState == ActionState.Dead && !this.acceptedDeath)
            {
                if (!this.WillReviveAlready)
                {
                    this.acceptedDeath = true;
                }
            }
        }

        public override void Death(float xI, float yI, DamageObject damage)
        {
            base.Death(xI, yI, damage);
            this.flameSource.Stop();
        }
        #endregion

        #region Primary
        protected override void StartFiring()
        {
            base.StartFiring();
            this.flameSource.loop = false;
            this.flameSource.clip = flameStart;
            this.flameSource.Play();
            this.fireFlashCounter = 0f;
            this.burstCooldown = UnityEngine.Random.Range(1f, 2f);
            this.burstCount = 0;
            this.fireRate = originalFirerate;
        }

        protected override void StopFiring()
        {
            base.StopFiring();
            this.flameSource.Stop();
        }

        protected override void RunFiring()
        {
            if (this.health <= 0)
            {
                return;
            }
            this.fireDelay -= this.t;
            if (this.fire)
            {
                this.StopRolling();
            }
            if (this.fire && this.fireDelay <= 0f)
            {
                this.fireCounter += this.t;
                this.fireFlashCounter += this.t;
                if (this.fireCounter >= this.fireRate)
                {
                    this.fireCounter -= this.fireRate;
                    this.UseFire();
                    this.SetGestureAnimation(GestureElement.Gestures.None);
                }
                if (this.fireFlashCounter >= 0.1f)
                {
                    this.fireFlashCounter -= 0.1f;
                    EffectsController.CreateMuzzleFlashEffect(base.X + base.transform.localScale.x * 11f, base.Y + 10f, -25f, (base.transform.localScale.x * 60f + this.xI) * 0.01f, (UnityEngine.Random.value * 60f - 30f) * 0.01f, base.transform);
                    //EffectsController.CreateMuzzleFlashEffect(base.X + base.transform.localScale.x * 11f, base.Y + 11f, -25f, (base.transform.localScale.x * 70f + this.xI) * 0.01f, (UnityEngine.Random.value * 70f - 35f) * 0.01f, base.transform);
                    this.FireFlashAvatar();
                }
            }

            this.fireDelay -= this.t;
            if ( this.fire )
            {
                // Start flame loop
                if ( !this.flameSource.isPlaying )
                {
                    this.flameSource.clip = flameLoop;
                    this.flameSource.loop = true;
                    this.flameSource.Play();
                }
                if ( this.fireDelay <= 0f)
                {
                    this.fireCounter += this.t;
                    this.fireFlashCounter += this.t;
                    if (burstCooldown > 0)
                    {
                        this.burstCooldown -= this.t;
                        if (burstCooldown < 0)
                        {
                            this.fireRate = 0.1f;
                            this.burstCount = UnityEngine.Random.Range(8, 18);
                            this.sound.PlaySoundEffectAt(this.flameBurst, 0.5f, base.transform.position, 1f, true, false, false, 0f);
                        }
                    }

                    if (this.fireCounter >= this.fireRate)
                    {
                        this.fireCounter -= this.fireRate;
                        this.UseFire();
                    }
                    if (this.fireFlashCounter >= 0.1f)
                    {
                        this.fireFlashCounter -= 0.1f;
                        EffectsController.CreateMuzzleFlashEffect(base.X + base.transform.localScale.x * 11f, base.Y + 10f, -25f, (base.transform.localScale.x * 60f + this.xI) * 0.01f, (UnityEngine.Random.value * 60f - 30f) * 0.01f, base.transform);
                        //EffectsController.CreateMuzzleFlashEffect(base.X + base.transform.localScale.x * 11f, base.Y + 11f, -25f, (base.transform.localScale.x * 70f + this.xI) * 0.01f, (UnityEngine.Random.value * 70f - 35f) * 0.01f, base.transform);
                        this.FireFlashAvatar();
                    }
                }
            }
        }

        protected override void UseFire()
        {
            if ( this.burstCooldown <= 0f )
            {
                this.FireWeapon(base.X + base.transform.localScale.x * 11f, base.Y + 10f, base.transform.localScale.x * 180f + this.xI, UnityEngine.Random.value * 30 - 15f);
                this.FireWeapon(base.X + base.transform.localScale.x * 11f, base.Y + 11f, base.transform.localScale.x * 210f + this.xI, UnityEngine.Random.value * 40f - 20f);
                --this.burstCount;
                if ( this.burstCount <= 0 )
                {
                    this.fireRate = originalFirerate;
                    this.burstCooldown = UnityEngine.Random.Range(0.5f, 2f);
                }
            }
            else
            {
                this.FireWeapon(base.X + base.transform.localScale.x * 11f, base.Y + 10f, base.transform.localScale.x * 30f + this.xI, UnityEngine.Random.value * 20f - 15f);
                this.FireWeapon(base.X + base.transform.localScale.x * 11f, base.Y + 11f, base.transform.localScale.x * 40f + this.xI, UnityEngine.Random.value * 30f - 20f);
            }
            Map.DisturbWildLife(base.X, base.Y, 60f, base.playerNum);
        }

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            this.gunFrame = 3;
            this.gunSprite.SetLowerLeftPixel((float)(32 * this.gunFrame), 32f);
            ProjectileController.SpawnProjectileLocally(this.projectiles[UnityEngine.Random.Range(0, 3)], this, x, y, xSpeed, ySpeed, base.playerNum);
        }

        protected override void RunGun()
        {
            if ( !theThing )
            {
                if ( this.fire )
                {
                    this.gunFrame = 3;
                    this.SetGunSprite(this.gunFrame, 0);
                }
                else if ( this.gunFrame > 0 )
                {
                    this.gunCounter += this.t;
                    if (this.gunCounter > 0.05f)
                    {
                        this.gunCounter -= 0.05f;
                        this.gunFrame--;
                        this.SetGunSprite(this.gunFrame, 0);
                    }
                }
            }
            else
            {

            }
        }
        #endregion

        #region Special
        #endregion

        #region Melee
        #endregion
    }
}
