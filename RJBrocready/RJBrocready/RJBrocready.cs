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
    public class RJBrocready : CustomHero
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
        Dynamite dynamitePrefab;

        // Melee
        protected bool throwingMook = false;
        AudioClip[] fireAxeSound;
        AudioClip[] axeHitSound;
        protected bool playedAxeSound = false;
        protected bool meleeBufferedPress = false;

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

            dynamitePrefab = new GameObject("Dynamite", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(Dynamite) }).GetComponent<Dynamite>();
            dynamitePrefab.enabled = false;
            dynamitePrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.McBrover) as McBrover).projectile.soundHolder;

            this.currentMeleeType = MeleeType.Disembowel;
            this.meleeType = MeleeType.Disembowel;

            this.fireAxeSound = new AudioClip[3];
            this.fireAxeSound[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "fireAxe1.wav");
            this.fireAxeSound[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "fireAxe2.wav");
            this.fireAxeSound[2] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "fireAxe3.wav");

            this.axeHitSound = new AudioClip[3];
            this.axeHitSound[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "axeHit1.wav");
            this.axeHitSound[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "axeHit2.wav");
            this.axeHitSound[2] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "axeHit3.wav");

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
                //if ( !this.flameSource.isPlaying )
                if ( !this.flameSource.loop && (this.flameSource.clip.length - this.flameSource.time) <= 0.02f )
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
        protected override void UseSpecial()
        {
            if (this.SpecialAmmo > 0)
            {
                Dynamite dynamite;
                if (this.down && this.IsOnGround() && this.ducking)
                {
                    dynamite = ProjectileController.SpawnGrenadeLocally(this.dynamitePrefab, this, base.X + Mathf.Sign(base.transform.localScale.x) * 6f, base.Y + 10f, 0.001f, 0.011f, Mathf.Sign(base.transform.localScale.x) * 30f, 70f, base.playerNum, 0) as Dynamite;
                }
                else
                {
                    dynamite = ProjectileController.SpawnGrenadeLocally(this.dynamitePrefab, this, base.X + Mathf.Sign(base.transform.localScale.x) * 6f, base.Y + 10f, 0.001f, 0.011f, Mathf.Sign(base.transform.localScale.x) * 300f, 250f, base.playerNum, 0) as Dynamite;
                }
                dynamite.enabled = true;
                --this.SpecialAmmo;
            }
            else
            {
                base.UseSpecial();
            }
        }
        #endregion

        #region Melee
        protected void MeleeAttack(bool shouldTryHitTerrain, bool playMissSound)
        {
            bool flag;
            Map.DamageDoodads(3, DamageType.Fire, base.X + (float)(base.Direction * 4), base.Y, 0f, 0f, 6f, base.playerNum, out flag, null);
            this.KickDoors(24f);
            Unit hitUnit;
            if (hitUnit = Map.HitClosestUnit(this, base.playerNum, 2, DamageType.Blade, 12f, 24f, base.X + base.transform.localScale.x * 8f, base.Y + 8f, base.transform.localScale.x * 100f, 100f, true, true, base.IsMine, false, true))
            {
                hitUnit.Damage(1, DamageType.Fire, base.transform.localScale.x * 100f, 100f, (int)base.transform.localScale.x, this, base.X + base.transform.localScale.x * 8f, base.Y + 8f);
                this.sound.PlaySoundEffectAt(this.axeHitSound, 0.8f, base.transform.position, 1f, true, false, false, 0f);
                this.meleeHasHit = true;
            }
            else if (playMissSound)
            {
            }
            this.meleeChosenUnit = null;
            if (shouldTryHitTerrain && this.TryMeleeTerrain(0, 2))
            {
                this.meleeHasHit = true;
            }
            this.TriggerBroMeleeEvent();
        }

        protected override bool TryMeleeTerrain(int offset = 0, int meleeDamage = 2)
        {
            if (!Physics.Raycast(new Vector3(base.X - base.transform.localScale.x * 4f, base.Y + 4f, 0f), new Vector3(base.transform.localScale.x, 0f, 0f), out this.raycastHit, (float)(16 + offset), this.groundLayer))
            {
                return false;
            }
            Cage component = this.raycastHit.collider.GetComponent<Cage>();
            if (component == null && this.raycastHit.collider.transform.parent != null)
            {
                component = this.raycastHit.collider.transform.parent.GetComponent<Cage>();
            }
            if (component != null)
            {
                MapController.Damage_Networked(this, this.raycastHit.collider.gameObject, component.health, DamageType.Melee, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y);
                return true;
            }
            MapController.Damage_Networked(this, this.raycastHit.collider.gameObject, meleeDamage, DamageType.Melee, 0f, 40f, this.raycastHit.point.x, this.raycastHit.point.y);
            this.sound.PlaySoundEffectAt(this.soundHolder.meleeHitTerrainSound, 0.3f, base.transform.position, 1f, true, false, false, 0f);
            EffectsController.CreateProjectilePopWhiteEffect(base.X + this.width * base.transform.localScale.x, base.Y + this.height + 4f);
            return true;
        }


        // Sets up melee attack
        protected override void StartCustomMelee()
        {
            if (this.CanStartNewMelee())
            {
                base.frame = 1;
                base.counter = -0.05f;
                this.AnimateMelee();
                this.throwingMook = (this.nearbyMook != null && this.nearbyMook.CanBeThrown());
                this.playedAxeSound = false;
                this.meleeBufferedPress = false;
            }
            else if (this.CanStartMeleeFollowUp())
            {
                this.meleeBufferedPress = true;
            }
            this.StartMeleeCommon();
        }

        protected override void StartMeleeCommon()
        {
            if (!this.meleeFollowUp && this.CanStartNewMelee())
            {
                base.frame = 0;
                base.counter = -0.05f;
                this.ResetMeleeValues();
                this.lerpToMeleeTargetPos = 0f;
                this.doingMelee = true;
                this.showHighFiveAfterMeleeTimer = 0f;
                this.DeactivateGun();
                this.SetMeleeType();
                this.meleeStartPos = base.transform.position;
                this.AnimateMelee();
            }
        }

        protected override bool CanStartNewMelee()
        {
            return !this.doingMelee;
        }

        protected override bool CanStartMeleeFollowUp()
        {
            return true;
        }

        // Calls MeleeAttack
        protected override void AnimateCustomMelee()
        {
            this.AnimateMeleeCommon();
            if (!this.throwingMook)
            {
                base.frameRate = 0.06f;
            }
            int num = 24 + Mathf.Clamp(base.frame, 0, 7);
            int num2 = 10;
            this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)(num2 * this.spritePixelHeight));
            if ( !this.throwingMook && (base.frame == 0 || base.frame == 1) && !this.playedAxeSound )
            {
                this.sound.PlaySoundEffectAt(this.fireAxeSound, 1f, base.transform.position, 1f, true, false, false, 0f);
                this.playedAxeSound = true;
            }
            else if (base.frame == 3)
            {
                base.counter -= 0.066f;
                this.MeleeAttack(true, true);
            }
            else if (base.frame > 3 && !this.meleeHasHit)
            {
                this.MeleeAttack(false, false);
            }
            if (base.frame >= 8)
            {
                base.frame = 0;
                this.CancelMelee();
                if ( this.meleeBufferedPress )
                {
                    this.meleeBufferedPress = false;
                    this.StartCustomMelee();
                }
            }
        }

        protected override void RunCustomMeleeMovement()
        {
            if (this.jumpingMelee)
            {
                this.ApplyFallingGravity();
                if (this.yI < this.maxFallSpeed)
                {
                    this.yI = this.maxFallSpeed;
                }
            }
            else if (this.dashingMelee)
            {
                if (base.frame <= 1)
                {
                    this.xI = 0f;
                    this.yI = 0f;
                }
                else if (base.frame <= 3)
                {
                    if (this.meleeChosenUnit == null)
                    {
                        if (!this.isInQuicksand)
                        {
                            this.xI = this.speed * 1f * base.transform.localScale.x;
                        }
                        this.yI = 0f;
                    }
                    else if (!this.isInQuicksand)
                    {
                        this.xI = this.speed * 0.5f * base.transform.localScale.x + (this.meleeChosenUnit.X - base.X) * 6f;
                    }
                }
                else if (base.frame <= 5)
                {
                    if (!this.isInQuicksand)
                    {
                        this.xI = this.speed * 0.3f * base.transform.localScale.x;
                    }
                    this.ApplyFallingGravity();
                }
                else
                {
                    this.ApplyFallingGravity();
                }
            }
            else if (base.Y > this.groundHeight + 1f)
            {
                this.CancelMelee();
            }
        }
        #endregion
    }
}
