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

        // The Thing
        public enum ThingState
        {
            Human = 0,
            FakeDeath = 1,
            Thing = 2,
            MonsterForm = 3,
            Reforming = 4,
        }
        protected bool gibbed = false;
        protected bool theThing = false;
        protected ThingState currentState = ThingState.Human;
        protected float fakeDeathTime = 0f;
        Material thingMaterial, thingGunMaterial, thingMeleeMaterial, thingSpecialIconMaterial, thingSpecialMaterial, thingArmlessMaterial;
        protected float MonsterFormCounter = 0f;
        //protected const float MaxMonsterFormTime = 3f;
        protected const float MaxMonsterFormTime = 8f;

        // Misc
        protected bool acceptedDeath = false;
        protected bool wasInvulnerable = false;

        // DEBUG
        public static RJBrocready currentInstance;
        public static bool pauseMelee = false;
        public static string meleeOffsetXStr = "16";
        public static float meleeOffsetX = 16f;
        public static string meleeOffsetYStr = "16";
        public static float meleeOffsetY = 16f;

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

            this.thingMaterial = ResourcesController.GetMaterial(directoryPath, "thingSprite.png");
            this.thingGunMaterial = ResourcesController.GetMaterial(directoryPath, "thingGunSprite.png");
            this.thingMeleeMaterial = ResourcesController.GetMaterial(directoryPath, "thingMeleeSprite.png");
            this.thingSpecialIconMaterial = ResourcesController.GetMaterial(directoryPath, "thingSpecial.png");
            this.thingSpecialMaterial = ResourcesController.GetMaterial(directoryPath, "thingSpecialSprite.png");
            this.thingArmlessMaterial = ResourcesController.GetMaterial(directoryPath, "armlessSprite.png");

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
            currentInstance = this;
        }

        protected override void Update()
        {
            if (this.invulnerable)
            {
                this.wasInvulnerable = true;
            }

            if ( currentState != ThingState.FakeDeath )
            {
                base.Update();
            }
            else
            {
                this.SetDeltaTime();
                this.RunMovement();
                this.counter += this.t;
                if (this.counter > this.frameRate)
                {
                    this.counter -= this.frameRate;
                    this.IncreaseFrame();
                    this.AnimateFakeDeath();
                }

                if ( this.fakeDeathTime < 2f )
                {
                    this.fakeDeathTime += this.t;
                    if (this.fakeDeathTime >= 2f)
                    {
                        base.frame = 0;
                    }
                }
                this.CheckFacingDirection();
            }

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

            // Count down thing timer
            if ( this.MonsterFormCounter > 0f )
            {
                this.MonsterFormCounter -= this.t;
                if ( this.MonsterFormCounter <= 0f )
                {
                    this.currentState = ThingState.Reforming;
                    BMLogger.Log("start reforming");
                }
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

        public void makeTextBox(string label, ref string text, ref float val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            text = GUILayout.TextField(text);
            GUILayout.EndHorizontal();

            float.TryParse(text, out val);
        }

        public override void UIOptions()
        {
            if ( GUILayout.Button("die") )
            {
                currentInstance.FakeDeath(0f, 0f, null);
            }

            makeTextBox("offsetx", ref meleeOffsetXStr, ref meleeOffsetX);
            makeTextBox("offsety", ref meleeOffsetYStr, ref meleeOffsetY);
            pauseMelee = GUILayout.Toggle(pauseMelee, "pause melee");
        }
        #endregion

        #region Primary
        protected override void StartFiring()
        {
            if ( !theThing )
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
            else
            {

            }
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
            if ( !theThing )
            {
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
                if (this.fire)
                {
                    // Start flame loop
                    //if ( !this.flameSource.isPlaying )
                    if (!this.flameSource.loop && (this.flameSource.clip.length - this.flameSource.time) <= 0.02f)
                    {
                        this.flameSource.clip = flameLoop;
                        this.flameSource.loop = true;
                        this.flameSource.Play();
                    }
                    if (this.fireDelay <= 0f)
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
            else
            {

            }
        }

        protected override void UseFire()
        {
            if ( !theThing )
            {
                if (this.burstCooldown <= 0f)
                {
                    this.FireWeapon(base.X + base.transform.localScale.x * 11f, base.Y + 10f, base.transform.localScale.x * 180f + this.xI, UnityEngine.Random.value * 30 - 15f);
                    this.FireWeapon(base.X + base.transform.localScale.x * 11f, base.Y + 11f, base.transform.localScale.x * 210f + this.xI, UnityEngine.Random.value * 40f - 20f);
                    --this.burstCount;
                    if (this.burstCount <= 0)
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
            else
            {

            }
        }

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            if ( !theThing )
            {
                this.gunFrame = 3;
                this.gunSprite.SetLowerLeftPixel((float)(32 * this.gunFrame), 32f);
                ProjectileController.SpawnProjectileLocally(this.projectiles[UnityEngine.Random.Range(0, 3)], this, x, y, xSpeed, ySpeed, base.playerNum);
            }
            else
            {

            }
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
                if ( this.fire )
                {
                    // Animate thing attack
                }
                // Human form
                else if ( !this.doingMelee && this.currentState == ThingState.Thing )
                {
                    this.gunFrame = 0;
                    this.SetGunSprite(0, 0);
                }
                // Monster form
                else if ( this.currentState == ThingState.MonsterForm )
                {
                    this.SetGunPosition(meleeOffsetX, meleeOffsetY);
                    this.gunFrame = 8;
                    this.gunSprite.SetLowerLeftPixel(this.gunFrame * 64f, 64f);
                }
                // Going from monster back to human
                else if ( this.currentState == ThingState.Reforming )
                {
                    this.SetGunPosition(meleeOffsetX, meleeOffsetY);
                    this.gunCounter += this.t;
                    if ( this.gunCounter > 0.08f )
                    {
                        this.gunCounter -= 0.08f;
                        --this.gunFrame;
                        if (this.gunFrame >= 0)
                        {
                            this.gunSprite.SetLowerLeftPixel(this.gunFrame * 64f, 64f);
                        }
                        else
                        {
                            ExitMonsterForm();
                        }
                    }
                }

            }
        }

        protected override void SetGunSprite(int spriteFrame, int spriteRow)
        {
            if ( !theThing )
            {
                base.SetGunSprite(spriteFrame, spriteRow);
            }
            else
            {
                // Mess with gunsprite in Monster form
                if ( this.currentState != ThingState.Thing )
                {
                    return;
                }
                if (base.actionState == ActionState.Hanging)
                {
                    this.gunSprite.SetLowerLeftPixel((float)(this.gunSpritePixelWidth * (this.gunSpriteHangingFrame + spriteFrame)), 128);
                }
                else if (base.actionState == ActionState.ClimbingLadder && this.hangingOneArmed)
                {
                    this.gunSprite.SetLowerLeftPixel((float)(this.gunSpritePixelWidth * (this.gunSpriteHangingFrame + spriteFrame)), 128);
                }
                else if (this.attachedToZipline != null && base.actionState == ActionState.Jumping)
                {
                    this.gunSprite.SetLowerLeftPixel((float)(this.gunSpritePixelWidth * (this.gunSpriteHangingFrame + spriteFrame)), 128);
                }
                else
                {
                    this.gunSprite.SetLowerLeftPixel((float)(this.gunSpritePixelWidth * spriteFrame), 128);
                }
            }
        }
        #endregion

        #region Special
        protected override void UseSpecial()
        {
            if (this.SpecialAmmo > 0)
            {
                if ( !theThing )
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
                    UseThingSpecial();
                }
            }
            else
            {
                base.UseSpecial();
            }
        }

        public override void Knock(DamageType damageType, float xI, float yI, bool forceTumble)
        {
            // Override knock function to allow greater knockback from dynamite
            if ( damageType == DamageType.SelfEsteem )
            {
                this.impaledBy = null;
                this.impaledByTransform = null;
                if (this.frozenTime > 0f)
                {
                    return;
                }
                this.xI = Mathf.Clamp(this.xI + xI / 2f, -2000f, 2000f);
                this.xIBlast = Mathf.Clamp(this.xIBlast + xI / 2f, -2000f, 2000f);
                this.yI = this.yI + yI;
                if (this.IsParachuteActive && yI > 0f)
                {
                    this.IsParachuteActive = false;
                    this.Tumble();
                }
            }
            else
            {
                base.Knock(damageType, xI, yI, forceTumble);
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
                // Don't play hit sound multiple times
                if ( !this.meleeHasHit )
                {
                    this.sound.PlaySoundEffectAt(this.axeHitSound, 0.8f, base.transform.position, 1f, true, false, false, 0f);
                }
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
                if (this.theThing)
                {
                    base.frame = 0;
                    base.counter = -0.05f;
                    this.ResetMeleeValues();
                    this.lerpToMeleeTargetPos = 0f;
                    this.doingMelee = true;
                    this.showHighFiveAfterMeleeTimer = 0f;
                    this.SetMeleeType();
                    this.meleeStartPos = base.transform.position;
                    StartThingMelee();
                    this.AnimateMelee();
                }
                else
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
            // AnimateMeleeCommon except throwing mooks is disabled for the thing
            this.SetSpriteOffset(0f, 0f);
            this.rollingFrames = 0;
            if (base.frame == 1)
            {
                base.counter -= 0.0334f;
            }
            if (base.frame == 6 && this.meleeFollowUp)
            {
                base.counter -= 0.08f;
                base.frame = 1;
                this.meleeFollowUp = false;
                this.ResetMeleeValues();
            }
            this.frameRate = 0.025f;
            if (!this.theThing && base.frame == 2 && this.nearbyMook != null && this.nearbyMook.CanBeThrown() && this.highFive)
            {
                this.CancelMelee();
                this.ThrowBackMook(this.nearbyMook);
                this.nearbyMook = null;
            }

            if ( !theThing )
            {
                if (!this.throwingMook)
                {
                    base.frameRate = 0.06f;
                }
                int num = 24 + Mathf.Clamp(base.frame, 0, 7);
                int num2 = 10;
                this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)(num2 * this.spritePixelHeight));
                if (!this.throwingMook && (base.frame == 0 || base.frame == 1) && !this.playedAxeSound)
                {
                    this.sound.PlaySoundEffectAt(this.fireAxeSound, 1f, base.transform.position, 1f, true, false, false, 0f);
                    this.playedAxeSound = true;
                }
                else if (base.frame == 3)
                {
                    base.counter -= 0.066f;
                    this.MeleeAttack(true, true);
                }
                else if (base.frame > 3 && !this.meleeHasHit && base.frame < 6)
                {
                    this.MeleeAttack(false, false);
                }
                if (base.frame >= 8)
                {
                    base.frame = 0;
                    this.CancelMelee();
                    if (this.meleeBufferedPress)
                    {
                        this.meleeBufferedPress = false;
                        this.StartCustomMelee();
                    }
                }
            }
            else
            {
                AnimateThingMelee();
            }
        }

        protected override void RunCustomMeleeMovement()
        {
            if ( !theThing )
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
                    else if (base.frame <= 7)
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
            else
            {
                RunThingMeleeMovement();
            }
        }

        protected override void CancelMelee()
        {
            base.CancelMelee();
            if ( this.theThing )
            {
                this.CancelThingMelee();
            }
        }
        #endregion

        #region TheThing
        protected override void Gib(DamageType damageType, float xI, float yI)
        {
            gibbed = true;
            base.Gib(damageType, xI, yI);
        }

        public override void Death(float xI, float yI, DamageObject damage)
        {
            if ( !theThing && !gibbed && damage.damageType != DamageType.Acid && damage.damageType != DamageType.Spikes )
            {
                FakeDeath(xI, yI, damage);
            }
            else
            {
                base.Death(xI, yI, damage);
            }
            this.flameSource.Stop();
        }

        public void FakeDeath(float xI, float yI, DamageObject damage)
        {
            this.health = 1;
            currentState = ThingState.FakeDeath;
            theThing = true;
            this.CancelMelee();
            this.DeactivateGun();
            if (damage != null)
            {
                if (damage.damageType != DamageType.Fall && damage.damageType != DamageType.ChainsawImpale && damage.damageType != DamageType.SilencedBullet && damage.damageType != DamageType.Disembowel && damage.damageType != DamageType.Acid)
                {
                    this.xI = xI * 0.3f;
                    if (yI < 0f)
                    {
                        yI = 0f;
                    }
                    if (damage.damageType != DamageType.Melee)
                    {
                        this.yI = yI * 0.3f;
                    }
                    else
                    {
                        this.yI = yI * 0.5f;
                    }
                }
            }
            if (this.impaledByTransform == null && !this.isInQuicksand && damage != null && damage.damageType != DamageType.Acid && damage.damageType != DamageType.SilencedBullet && damage.damageType != DamageType.Disembowel && damage.damageType != DamageType.ChainsawImpale)
            {
                this.yI += 25f;
                float num2 = this.CalculateCeilingHeight();
                if (base.Y + this.headHeight < num2 - 5f && damage != null && damage.damageType != DamageType.Chainsaw && damage.damageType != DamageType.Shock && !this.isInQuicksand)
                {
                    base.Y += 5f;
                }
            }
            if (damage != null && damage.damageType != DamageType.SelfEsteem && damage.damageType != DamageType.SilencedBullet && damage.damageType != DamageType.Disembowel && damage.damageType != DamageType.Acid && damage.damageType != DamageType.Shock && this.frozenTime <= 0f)
            {
                EffectsController.CreateBloodParticles(this.bloodColor, base.X, base.Y + 6f, 10, 4f, 5f, 60f, this.xI * 0.5f, this.yI * 0.5f);
            }
            this.ReleaseHeldObject(false);
            if (base.GetComponent<Collider>() != null)
            {
                base.GetComponent<Collider>().enabled = false;
            }
            Map.ForgetPlayer(base.playerNum, false, false);
            base.GetComponent<Renderer>().material = this.thingMaterial;
            this.AnimateFakeDeath();
        }

        public override void Damage(int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY)
        {
            if ( currentState != ThingState.FakeDeath )
            {
                base.Damage(damage, damageType, xI, yI, direction, damageSender, hitX, hitY);
            }
        }

        public override bool IsInStealthMode()
        {
            return this.currentState == ThingState.FakeDeath || base.IsInStealthMode();
        }

        protected override void AlertNearbyMooks()
        {
            if (this.currentState != ThingState.FakeDeath)
            {
                base.AlertNearbyMooks();
            }
        }

        protected override void RunMovement()
        {
            if ( currentState != ThingState.FakeDeath )
            {
                base.RunMovement();
            }
            else
            {
                ActionState currentState = this.actionState;
                this.actionState = ActionState.Dead;
                base.RunMovement();
                this.actionState = currentState;
            }
        }

        protected void AnimateFakeDeath()
        {
            this.frameRate = 0.015f;
            if (base.Y > this.groundHeight + 0.2f && this.impaledByTransform == null)
            {
                this.AnimateFallingDeath();
            }
            else if ( this.fakeDeathTime < 2f )
            {
                this.AnimateActualDeath();
            }
            else
            {
                this.frameRate = 0.2f;
                this.sprite.SetLowerLeftPixel(((base.frame - 1) * this.spritePixelWidth), 10 * this.spritePixelHeight);
                // Finished turning
                if ( base.frame == 11 )
                {
                    this.BecomeTheThing();
                }
            }
        }

        protected void BecomeTheThing()
        {
            if (base.GetComponent<Collider>() != null)
            {
                base.GetComponent<Collider>().enabled = true;
            }
            this.currentState = ThingState.Thing;
            this.xI = 0;
            this.xIBlast = 0;
            this.counter = 1;
            this.lastParentedToTransform = null;
            this.gunFrame = 0;
            this.gunSprite.meshRender.material = this.thingGunMaterial;
            this.gunSprite.RecalcTexture();
            this.gunSpriteOffset.x = -3f;
            BroMakerUtilities.SetSpecialMaterials(this.playerNum, new List<Material> { this.thingSpecialIconMaterial }, new Vector2(3, 0),  0f);
            this.SpecialAmmo = 1;
            this.originalSpecialAmmo = 1;
        }

        protected void EnterMonsterForm()
        {
            this.MonsterFormCounter = MaxMonsterFormTime;
            this.currentState = ThingState.MonsterForm;
            this.doRollOnLand = false;
            this.canWallClimb = false;
        }

        protected void ExitMonsterForm()
        {
            this.currentState = ThingState.Thing;
            this.doRollOnLand = true;
            this.canWallClimb = true;

            base.GetComponent<Renderer>().material = this.thingMaterial;

            this.SetGunPosition(0, 0);
            this.gunSpriteOffset.x = -3f;
            this.gunSprite.pixelDimensions = new Vector2(32f, 32f);
            this.gunSprite.SetSize(32, 32);

            this.SetGunSprite(0, 0);
        }

        protected override void AnimateWallAnticipation()
        {
            // Don't animate wall anticipation if in monster form
            if ( this.currentState <= ThingState.Thing )
            {
                base.AnimateWallAnticipation();
            }
        }

        protected override void Jump(bool wallJump)
        {
            if ( this.currentState > ThingState.Thing  && wallJump )
            {
                return;
            }
            base.Jump(wallJump);
        }
        #endregion

        #region ThingPrimary
        #endregion

        #region ThingSpecial
        protected void UseThingSpecial()
        {

        }
        #endregion

        #region ThingMelee
        protected override void DeactivateGun()
        {
            if ( !(this.theThing && this.doingMelee) )
            {
                base.DeactivateGun();
            }
        }

        protected void StartThingMelee()
        {
            // Start melee from fully formed frame
            if ( this.currentState == ThingState.MonsterForm )
            {
                base.frame = 8;
            }
            // Reverse reforming and start from current frame
            else if ( this.currentState == ThingState.Reforming )
            {
                base.frame = this.gunFrame;
            }
            this.MonsterFormCounter = 0f;
            this.currentState = ThingState.Thing;
            this.gunSprite.meshRender.material = this.thingMeleeMaterial;
            this.gunSprite.pixelDimensions = new Vector2(64f, 64f);
            this.gunSprite.SetSize(64, 64);
            this.gunSprite.RecalcTexture();
            base.GetComponent<Renderer>().material = this.thingArmlessMaterial;
        }

        protected void ThingMeleeAttack(bool shouldTryHitTerrain, bool playMissSound)
        {
            bool flag;
            Map.DamageDoodads(3, DamageType.GibIfDead, base.X + (float)(base.Direction * 4), base.Y, 0f, 0f, 6f, base.playerNum, out flag, null);
            this.KickDoors(24f);
            Unit hitUnit;
            if (hitUnit = Map.HitClosestUnit(this, base.playerNum, 5, DamageType.GibIfDead, 12f, 24f, base.X + base.transform.localScale.x * 8f, base.Y + 8f, base.transform.localScale.x * 100f, 100f, true, true, base.IsMine, false, true))
            {
                Map.HitClosestUnit(this, base.playerNum, 5, DamageType.GibIfDead, 12f, 24f, base.X + base.transform.localScale.x * 8f, base.Y + 8f, base.transform.localScale.x * 100f, 100f, true, true, base.IsMine, false, true);

                //this.sound.PlaySoundEffectAt(this.axeHitSound, 0.8f, base.transform.position, 1f, true, false, false, 0f);
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

        protected void AnimateThingMelee()
        {
            if (pauseMelee)
            {
                --base.frame;
            }

            this.SetGunPosition(meleeOffsetX, meleeOffsetY);
            base.frameRate = 0.07f;
            //int num = 24 + Mathf.Clamp(base.frame, 0, 7);
            //int num2 = 10;
            //this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)(num2 * this.spritePixelHeight));
            this.sprite.SetLowerLeftPixel(0f, 32f);
            this.gunSprite.SetLowerLeftPixel(base.frame * 64f, 64f);
            if (!this.throwingMook && (base.frame == 0 || base.frame == 1) && !this.playedAxeSound)
            {
                //this.sound.PlaySoundEffectAt(this.fireAxeSound, 1f, base.transform.position, 1f, true, false, false, 0f);
                //this.playedAxeSound = true;
            }
            else if (base.frame == 13)
            {
                this.ThingMeleeAttack(true, true);
            }
            else if (base.frame > 13 && !this.meleeHasHit)
            {
                this.ThingMeleeAttack(false, false);
            }
            if (base.frame >= 15)
            {
                base.frame = 0;
                EnterMonsterForm();
                this.CancelMelee();
                if (this.meleeBufferedPress)
                {
                    this.meleeBufferedPress = false;
                    this.StartCustomMelee();
                }
            }
        }

        protected void RunThingMeleeMovement()
        {
            if (this.jumpingMelee)
            {
                this.ApplyFallingGravity();
                if (this.yI < this.maxFallSpeed)
                {
                    this.yI = this.maxFallSpeed;
                }
            }
            else
            {
                this.xI = 0f;
                this.ApplyFallingGravity();
            }
        }

        protected void CancelThingMelee()
        {
            this.gunSprite.meshRender.material = this.thingGunMaterial;
            this.gunSprite.RecalcTexture();
        }
        #endregion
    }
}
