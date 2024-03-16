using System;
using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using System.IO;
using System.Reflection;
using UnityEngine;
using System.Net;
using HarmonyLib;

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
        protected const float MaxMonsterFormTime = 3f;

        // The Thing Special
        SpriteSM tentacleWhipSprite;
        LineRenderer tentacleLine;
        public enum TentacleState
        { 
            Inactive = 0,
            Extending = 1,
            Attached = 2,
            Retracting = 3,
            ReadyToEat = 4
        }
        protected bool tentacleHitUnit = false;
        protected bool tentacleHitGround = false;
        protected Unit unitHit;
        protected RaycastHit groundHit;
        protected Vector3 tentacleHitPoint, tentacleDirection;
        protected Vector3 tentacleOffset = new Vector3(0f, 13f, 0f);
        protected float tentacleMaterialScale = 0.25f;
        protected float tentacleMaterialOffset = -0.125f;
        protected TentacleState currentTentacleState = TentacleState.Inactive;
        protected float tentacleExtendTime = 0f;
        protected float tentacleRange = 128f;
        protected LayerMask fragileGroundLayer;
        protected int tentacleDamage = 3;
        protected bool impaled = false;
        protected Transform tentacleImpaler;
        protected float tentacleRetractTimer = 0f;

        // Misc
        protected const float OriginalSpeed = 130f;
        protected const float MonsterFormSpeed = 90f;
        protected const float AttackingMonsterFormSpeed = 50f; 
        protected bool acceptedDeath = false;
        protected bool wasInvulnerable = false;
        protected bool startAsTheThing = true;

        // DEBUG
        public static RJBrocready currentInstance;
        public static bool pauseMelee = false;
        public static string meleeOffsetXStr = "16";
        public static float meleeOffsetX = 16f;
        public static string meleeOffsetYStr = "16";
        public static float meleeOffsetY = 16f;

        #region General
        protected override void Start()
        {
            base.Start();
            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            BaBroracus bro = HeroController.GetHeroPrefab(HeroType.BaBroracus) as BaBroracus;
            projectiles = new Projectile[] { bro.projectile, bro.projectile2, bro.projectile3 };
            this.flameBurst = bro.flameSoundEnd;

            dynamitePrefab = new GameObject("Dynamite", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(Dynamite) }).GetComponent<Dynamite>();
            dynamitePrefab.enabled = false;
            dynamitePrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.McBrover) as McBrover).projectile.soundHolder;

            tentacleWhipSprite = new GameObject("TentacleWhip", new Type[] { typeof(Transform), typeof(MeshRenderer), typeof(MeshFilter), typeof(SpriteSM) }).GetComponent<SpriteSM>();
            MeshRenderer renderer = tentacleWhipSprite.GetComponent<MeshRenderer>();
            renderer.material = ResourcesController.GetMaterial(directoryPath, "tentacle.png");
            renderer.material.mainTexture.wrapMode = TextureWrapMode.Clamp;
            tentacleWhipSprite.transform.parent = this.transform;
            tentacleWhipSprite.lowerLeftPixel = new Vector2(0, 16);
            tentacleWhipSprite.pixelDimensions = new Vector2(128, 16);
            tentacleWhipSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            tentacleWhipSprite.width = 128;
            tentacleWhipSprite.height = 8;
            tentacleWhipSprite.transform.localPosition = new Vector3(65f, 12f, -2f);
            tentacleWhipSprite.SetTextureDefaults();
            tentacleWhipSprite.gameObject.SetActive(false);

            tentacleLine = new GameObject("TentacleLine", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
            tentacleLine.transform.parent = this.transform;
            tentacleLine.material = ResourcesController.GetMaterial(directoryPath, "tentacleLine.png");

            this.tentacleImpaler = new GameObject("TentacleImpaler", new Type[] { typeof(Transform) }).transform;

            this.fragileGroundLayer = this.fragileLayer | this.groundLayer;

            this.flameStart = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "flameStart.wav");
            this.flameLoop = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "flameLoop.wav");

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

            this.flameSource = base.gameObject.AddComponent<AudioSource>();
            this.flameSource.rolloffMode = AudioRolloffMode.Linear;
            this.flameSource.maxDistance = 500f;
            this.flameSource.spatialBlend = 1f;
            this.flameSource.volume = 0.4f;
            this.flameSource.playOnAwake = false;
            this.flameSource.Stop();
            currentInstance = this;

            if ( startAsTheThing )
            {
                this.theThing = true;
                base.GetComponent<Renderer>().material = this.thingMaterial;
                this.BecomeTheThing();
            }
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
                }
            }

            // Update thing's special tentacle
            if ( this.usingSpecial && this.currentTentacleState > TentacleState.Inactive )
            {
                this.UpdateTentacle();
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
                DamageObject damage = new DamageObject(1, DamageType.Bullet, 100, 0, currentInstance.X, currentInstance.Y, currentInstance );
                currentInstance.FakeDeath(0f, 0f, damage);
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
                this.ThingStartFiring();
            }
        }

        protected override void StopFiring()
        {
            if ( !theThing )
            {
                base.StopFiring();
                this.flameSource.Stop();
            }
            else
            {
                this.ThingStopFiring();
            }
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
                this.ThingRunFiring();
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
                this.ThingUseFire();
            }
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
                this.ThingRunGun();
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
        protected override void PressSpecial()
        {
            if ( !this.theThing )
            {
                base.PressSpecial();
            }
            else
            {
                this.ThingPressSpecial();
            }
        }

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
                    this.ThingUseSpecial();
                }
            }
            else
            {
                base.UseSpecial();
            }
        }

        protected override void AnimateSpecial()
        {
            if ( !this.theThing )
            {
                base.AnimateSpecial();
            }
            else
            {
                this.ThingAnimateSpecial();
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
                    ThingStartMelee();
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
                ThingAnimateMelee();
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
                ThingRunMeleeMovement();
            }
        }

        protected override void CancelMelee()
        {
            if (this.theThing)
            {
                this.ThingCancelMelee();
            }
            base.CancelMelee();
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
            this.CancelMelee();
            this.rollingFrames = 0;
            this.doRollOnLand = false;
            this.canWallClimb = false;
            this.canChimneyFlip = false;
            this.theThing = true;
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
            base.frame = 0;
            this.up = this.down = this.left = this.right = this.wasUp = this.wasDown = this.wasLeft = this.wasRight = this.dashing = false;
            this.throwingHeldObject = this.throwingMook = false;
            base.actionState = ActionState.Idle;
            this.AnimateFakeDeath();
        }

        public override void Damage(int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY)
        {
            if ( currentState != ThingState.FakeDeath && !(this.theThing && this.usingSpecial) )
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
            this.frameRate = 0.0334f;
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
                this.frameRate = 0.15f;
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
            this.doRollOnLand = true;
            this.canWallClimb = true;
            this.canChimneyFlip = true;
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

        protected void SwitchToMonsterSprites(Material gunMat)
        {
            bool changedMaterial = false;
            if ( gunMat != this.gunSprite.meshRender.material )
            {
                this.gunSprite.meshRender.material = gunMat;
                changedMaterial = true;
            }
            this.gunSprite.pixelDimensions = new Vector2(64f, 64f);
            this.gunSprite.SetSize(64, 64);
            if ( changedMaterial )
            {
                this.gunSprite.RecalcTexture();
            }
            base.GetComponent<Renderer>().material = this.thingArmlessMaterial;
        }

        protected void EnterMonsterForm()
        {
            this.speed = MonsterFormSpeed;
            this.MonsterFormCounter = MaxMonsterFormTime;
            this.currentState = ThingState.MonsterForm;
            this.doRollOnLand = false;
            this.canWallClimb = false;
            this.canChimneyFlip = false;
        }

        protected void ExitMonsterForm()
        {
            this.speed = OriginalSpeed;
            this.currentState = ThingState.Thing;
            this.doRollOnLand = true;
            this.canWallClimb = true;
            this.canChimneyFlip = true;

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
        protected void ThingStartFiring()
        {
            // Switch sprites to monster sprites
            if ( this.currentState == ThingState.Thing )
            {
                SwitchToMonsterSprites(this.thingGunMaterial);
                this.currentState = ThingState.MonsterForm;
            }
            this.gunSprite.SetLowerLeftPixel(this.gunFrame * 64f, 64f);
        }

        protected void ThingStopFiring()
        {
            if ( this.gunFrame > 9 )
            {
                this.gunFrame = 11;
            }
            EnterMonsterForm();
        }

        protected void ThingRunFiring()
        {
        }

        protected void ThingUseFire()
        {
            this.ThingFireWeapon(base.X + base.transform.localScale.x * 10f, base.Y + 8f, base.transform.localScale.x * 200f, 200f);
            Map.DisturbWildLife(base.X, base.Y, 60f, base.playerNum);
        }

        protected void ThingFireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            bool flag;
            Map.DamageDoodads(3, DamageType.Knock, x, y, xSpeed, ySpeed, 6f, base.playerNum, out flag, null);
            this.KickDoors(24f);
            if (Map.HitClosestUnit(this, base.playerNum, 3, DamageType.Knock, 12f, 24f, x, y, xSpeed, ySpeed, true, true, base.IsMine, false, true))
            {
                //this.sound.PlaySoundEffectAt(this.axeHitSound, 0.8f, base.transform.position, 1f, true, false, false, 0f);
            }

            this.ThingHitTerrain(0, 2);
        }

        protected void ThingRunGun()
        {
            if ( this.doingMelee || this.usingSpecial )
            {
                // Do nothing
            }
            else if (this.fire)
            {
                // Animate thing attack
                // Start frame 9
                // Start loop frame 10
                // End loop frame 17
                this.SetGunPosition(meleeOffsetX, meleeOffsetY);
                this.gunCounter += this.t;
                if ( this.gunCounter > 0.12f )
                {
                    this.gunCounter -= 0.12f;
                    ++this.gunFrame;
                    if ( this.gunFrame > 15 )
                    {
                        this.gunFrame = 12;
                    }

                    if ( this.gunFrame > 9 )
                    {
                        this.ThingUseFire();
                    }

                    this.gunSprite.SetLowerLeftPixel(this.gunFrame * 64f, 64f);
                }
            }
            // Human form
            else if (!this.doingMelee && this.currentState == ThingState.Thing)
            {
                this.gunFrame = 0;
                this.SetGunSprite(0, 0);
            }
            // Monster form
            else if (this.currentState == ThingState.MonsterForm)
            {
                if ( this.gunFrame > 8 )
                {
                    this.gunCounter += this.t;
                    if ( this.gunCounter > 0.08f )
                    {
                        this.gunCounter -= 0.08f;
                        --this.gunFrame;
                    }
                }
                this.SetGunPosition(meleeOffsetX, meleeOffsetY);
                this.gunFrame = 8;
                this.gunSprite.SetLowerLeftPixel(this.gunFrame * 64f, 64f);
            }
            // Going from monster back to human
            else if (this.currentState == ThingState.Reforming)
            {
                this.SetGunPosition(meleeOffsetX, meleeOffsetY);
                this.gunCounter += this.t;
                if (this.gunCounter > 0.08f)
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
        #endregion

        #region ThingSpecial
        protected void ThingPressSpecial()
        {
            if ( !this.usingSpecial && this.SpecialAmmo > 0 && !this.hasBeenCoverInAcid && !this.doingMelee)
            {
                this.usingSpecial = true;
                this.pressSpecialFacingDirection = (int)base.transform.localScale.x;

                this.speed = AttackingMonsterFormSpeed;
                this.currentTentacleState = TentacleState.Inactive;
                this.tentacleHitUnit = false;

                this.SwitchToMonsterSprites(thingSpecialMaterial);
                if ( this.currentState == ThingState.MonsterForm )
                {
                    base.frame = 8;
                }
                else if ( this.currentState == ThingState.Reforming )
                {
                    base.frame = this.gunFrame;
                }
                this.ChangeFrame();
            }
            else if ( this.SpecialAmmo <= 0 )
            {
                HeroController.FlashSpecialAmmo(base.playerNum);
            }
        }

        protected void ThingUseSpecial()
        {

        }

        protected void ThingAnimateSpecial()
        {
            base.frameRate = 0.08f;

            this.SetGunPosition(meleeOffsetX, meleeOffsetY);
            this.sprite.SetLowerLeftPixel(0f, 32f);
            this.gunSprite.SetLowerLeftPixel(base.frame * 64f, 64f);


            if ( base.frame == 16 && this.currentTentacleState == TentacleState.Inactive )
            {
                StartTentacleWhip();
            }
            else if (this.currentTentacleState > TentacleState.Inactive && this.currentTentacleState < TentacleState.ReadyToEat )
            {
                base.frame = 16;
            }
            else if ( base.frame >= 16 && base.frame < 22 )
            {
                base.frame = 22;
            }
            // Eat mook
            else if ( base.frame == 23 )
            {
                BMLogger.Log("eaten");
                this.ThingMeleeAttack(true, true);
                this.tentacleLine.enabled = false;
                this.currentTentacleState = TentacleState.Inactive;
            }
            else if ( base.frame >= 25 )
            {
                this.usingSpecial = this.usingPockettedSpecial = false;
                this.EnterMonsterForm();
                this.SwitchToMonsterSprites(this.thingGunMaterial);
                this.ChangeFrame();
            }
        }

        protected void StartTentacleWhip()
        {
            this.currentTentacleState = TentacleState.Extending;
            this.tentacleExtendTime = 0.02f;
            this.tentacleRetractTimer = 0f;
            this.tentacleDirection = new Vector3(base.transform.localScale.x, 0f, 0f);
            this.tentacleDirection.Normalize();
            this.tentacleWhipSprite.gameObject.SetActive(true);
            impaled = false;
            tentacleHitGround = false;
            tentacleHitUnit = false;
        }
        
        protected void UpdateTentacle()
        {
            try
            {
                DrawTentacle();
            }
            catch ( Exception ex )
            {
                BMLogger.Log("exception: " + ex.ToString());
            }
        }

        public Unit HitClosestUnit(MonoBehaviour damageSender, int playerNum, float xRange, float yRange, float x, float y, int direction, Vector3 startPoint, bool haveHitGround, Vector3 groundVector)
        {
            if (Map.units == null)
            {
                return null;
            }
            int num = 999999;
            float num2 = Mathf.Max(xRange, yRange);
            Unit unit = null;
            for (int i = Map.units.Count - 1; i >= 0; i--)
            {
                Unit unit3 = Map.units[i];
                if (unit3 != null && !unit3.invulnerable && unit3.health <= num && GameModeController.DoesPlayerNumDamage(playerNum, unit3.playerNum))
                {
                    float f = unit3.X - x;
                    if (Mathf.Abs(f) - xRange < unit3.width && Mathf.Sign(f) == direction)
                    {
                        float f2 = unit3.Y + unit3.height / 2f + 3f - y;
                        if (Mathf.Abs(f2) - yRange < unit3.height)
                        {
                            float num4 = Mathf.Abs(f) + Mathf.Abs(f2);
                            if (num4 < num2)
                            {
                                if (unit3.health > 0)
                                {
                                    unit = unit3;
                                    num2 = num4;
                                }
                            }
                        }
                    }
                }
            }

            if (unit != null && (!haveHitGround || Mathf.Abs(unit.X - x) < Mathf.Abs(groundVector.x - x)))
            {
                return unit;
            }
            return null;
        }

        public bool HitProjectiles(int playerNum, int damage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, int direction)
        {
            bool result = false;
            for (int i = Map.damageableProjectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = Map.damageableProjectiles[i];
                if (projectile != null && GameModeController.DoesPlayerNumDamage(playerNum, projectile.playerNum))
                {
                    float f = projectile.X - x;
                    if (Mathf.Abs(f) - xRange < projectile.projectileSize && Mathf.Sign(f) == direction)
                    {
                        float f2 = projectile.Y - y;
                        if (Mathf.Abs(f2) - yRange < projectile.projectileSize)
                        {
                            Map.DamageProjectile(projectile, damage, damageType, xI, yI, 0f, playerNum);
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        protected void TentacleLineHitDetection(float tentacleRange, Vector3 startPoint)
        {
            RaycastHit groundHit = this.raycastHit;
            bool haveHitGround = false;
            float currentRange = tentacleRange;
            Vector3 endPoint;

            // Hit ground
            if (Physics.Raycast(startPoint, (base.transform.localScale.x > 0 ? Vector3.right : Vector3.left), out raycastHit, currentRange, this.fragileGroundLayer))
            {
                groundHit = this.raycastHit;
                // Shorten the range we check for raycast hits, we don't care about hitting anything past the current terrain.
                currentRange = this.raycastHit.distance;
                haveHitGround = true;
            }

            Unit unit;
            // Check for unit collision
            if ((unit = HitClosestUnit(this, this.playerNum, currentRange, 6, startPoint.x, startPoint.y, (int)base.transform.localScale.x, startPoint, haveHitGround, groundHit.point)) != null)
            {
                tentacleHitUnit = true;
                unitHit = unit;
                endPoint = new Vector3(unit.X, startPoint.y, 0);
            }
            // Use ground collsion if no unit was hit
            else if (haveHitGround)
            {
                tentacleHitGround = true;
                this.groundHit = groundHit;
                endPoint = new Vector3(groundHit.point.x, groundHit.point.y, 0);
            }
            // Nothing hit
            else
            {
                endPoint = new Vector3(startPoint.x + base.transform.localScale.x * tentacleRange, startPoint.y, 0);
            }

            HitProjectiles(this.playerNum, tentacleDamage, DamageType.Knifed, Mathf.Abs(endPoint.x - startPoint.x), 6f, startPoint.x, startPoint.y, base.transform.localScale.x * 30, 20, (int)base.transform.localScale.x);
        }

/*        protected void DamageUnit(Unit unit, Vector3 startPoint)
        {
            // Only damage visible objects
            if (unit != null && SortOfFollow.IsItSortOfVisible(unit.transform.position, 24, 24f))
            {
                unit.Damage(protonUnitDamage, DamageType.Fire, base.transform.localScale.x, 0, (int)base.transform.localScale.x, this, unit.X, unit.Y);
                unit.Knock(DamageType.Fire, base.transform.localScale.x * 30, 20, false);
            }
;
            if (this.effectCooldown <= 0)
            {
                Puff puff = EffectsController.CreateEffect(EffectsController.instance.whiteFlashPopSmallPrefab, unit.X + base.transform.localScale.x * 4, startPoint.y + UnityEngine.Random.Range(-3, 3), 0f, 0f, Vector3.zero, null);
                this.effectCooldown = 0.15f;
            }

            protonDamageCooldown = 0.08f;
        }*/

        protected void DamageCollider(RaycastHit hit)
        {
            // Only damage visible objects
            if (SortOfFollow.IsItSortOfVisible(hit.point, 24, 24f))
            {
                Unit unit = hit.collider.GetComponent<Unit>();
                // Damage unit
                if (unit != null)
                {
                    // Add damage if we're hitting a boss
                    unit.Damage(tentacleDamage + (unit.health > 30 ? 1 : 0), DamageType.Knifed, base.transform.localScale.x, 0, (int)base.transform.localScale.x, this, hit.point.x, hit.point.y);
                    unit.Knock(DamageType.Fire, base.transform.localScale.x * 30, 20, false);
                }
                // Damage other
                else
                {
                    hit.collider.SendMessage("Damage", new DamageObject(tentacleDamage, DamageType.Knifed, 0f, 0f, hit.point.x, hit.point.y, this));
                }
            }

            Puff puff = EffectsController.CreateEffect(EffectsController.instance.whiteFlashPopSmallPrefab, hit.point.x + base.transform.localScale.x * 4, hit.point.y + UnityEngine.Random.Range(-3, 3), 0f, 0f, Vector3.zero, null);
        }

        protected void DrawTentacle()
        {
            if ( this.currentTentacleState == TentacleState.Extending )
            {
                float max = this.tentacleRange;
                Vector2 tentaclePosition = new Vector2(128f - Mathf.Clamp(this.tentacleExtendTime * 1280f, 0f, max), 16f);
                float currentRange = 128f - tentaclePosition.x;
                if ( !tentacleHitUnit && !tentacleHitGround )
                {
                    TentacleLineHitDetection(currentRange, base.transform.position + this.tentacleOffset);
                }

                if ( tentacleHitUnit )
                {
                    this.tentacleHitPoint = new Vector3( unitHit.transform.position.x, base.transform.position.y + this.tentacleOffset.y, 0 );
                }
                else if ( tentacleHitGround )
                {
                    this.tentacleHitPoint = new Vector3( groundHit.point.x, base.transform.position.y + this.tentacleOffset.y, 0);
                }
                else
                {
                    this.tentacleHitPoint = base.transform.position + this.tentacleOffset + this.tentacleDirection.normalized * this.tentacleRange;
                }
                float num = this.tentacleHitPoint.x - (base.transform.position.x + this.tentacleOffset.x);
                if (num > 0f != base.transform.localScale.x > 0f)
                {
                    num *= -1f;
                }
                num *= Mathf.Sign(this.tentacleHitPoint.x - (base.transform.position.x + this.tentacleOffset.x));
                this.tentacleWhipSprite.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
                this.tentacleWhipSprite.gameObject.SetActive(true);
                this.tentacleExtendTime += this.t / 2f;
                this.tentacleWhipSprite.SetLowerLeftPixel(tentaclePosition);
                this.tentacleWhipSprite.UpdateUVs();
                if (tentaclePosition.x <= 0 || this.tentacleHitUnit || this.tentacleHitGround )
                {
                    this.currentTentacleState = TentacleState.Attached;
                    this.tentacleWhipSprite.gameObject.SetActive(false);
                    this.tentacleLine.enabled = true;
                    this.tentacleLine.startWidth = 3f;
                    this.tentacleLine.endWidth = 3f;
                    this.tentacleLine.widthMultiplier = 1f;
                    this.tentacleLine.textureMode = LineTextureMode.Stretch;
                    this.tentacleLine.SetPosition(0, base.transform.position + this.tentacleOffset);
                    this.tentacleLine.SetPosition(1, this.tentacleHitPoint);
                    float magnitude = (this.tentacleHitPoint - (base.transform.position + this.tentacleOffset)).magnitude;
                    if (base.transform.localScale.x < 0f)
                    {
                        this.tentacleLine.material.SetTextureScale("_MainTex", new Vector2(magnitude * tentacleMaterialScale, 1f));
                        this.tentacleLine.material.SetTextureOffset("_MainTex", new Vector2(magnitude * tentacleMaterialOffset, 0f));
                    }
                    else if (base.transform.localScale.x > 0f)
                    {
                        this.tentacleLine.material.SetTextureScale("_MainTex", new Vector2(magnitude * tentacleMaterialScale, -1f));
                        this.tentacleLine.material.SetTextureOffset("_MainTex", new Vector2(magnitude * tentacleMaterialOffset, 0f));
                    }
                }
            }
            else if ( this.currentTentacleState == TentacleState.Attached )
            {
                this.tentacleRetractTimer += this.t;
                // Impale unit if possible or hit unit / ground if not
                if ( !this.impaled )
                {
                    if ( this.tentacleHitUnit )
                    {
                        this.tentacleImpaler.position = tentacleHitPoint;
                        this.unitHit.Impale(tentacleImpaler, new Vector3(base.transform.localScale.x, 0f, 0f), 0, base.transform.localScale.x, 0, 0, 0);
                        this.unitHit.Y = this.tentacleHitPoint.y - 8f;
                        if (unitHit is Mook)
                        {
                            unitHit.useImpaledFrames = true;
                            Traverse.Create((unitHit as Mook)).Field("impaledPosition").SetValue(new Vector3(base.transform.localScale.x < 0 ? float.MaxValue : float.MinValue, 0, 0));
                        }
                    }
                    else if ( this.tentacleHitGround )
                    {   
                        DamageCollider(groundHit);
                    }
                    this.impaled = true;
                }
                this.tentacleLine.SetPosition(0, base.transform.position + this.tentacleOffset);
                this.tentacleLine.SetPosition(1, this.tentacleHitPoint);
                this.tentacleLine.startWidth = 3f;
                this.tentacleLine.endWidth = 3f;
                this.tentacleLine.widthMultiplier = 1f;
                if (this.tentacleRetractTimer > 0.25f)
                {
                    this.currentTentacleState = TentacleState.Retracting;
                }
            }
            else if ( this.currentTentacleState == TentacleState.Retracting || this.currentTentacleState == TentacleState.ReadyToEat )
            {
                // Move tentacleHitPoint towards player
                this.tentacleHitPoint = Vector3.MoveTowards(this.tentacleHitPoint, base.transform.position + this.tentacleOffset, (tentacleHitUnit ? 200f : 400f) * this.t);
                // Update tentacleImpaler position
                this.tentacleImpaler.position = tentacleHitPoint;

                this.tentacleLine.SetPosition(0, base.transform.position + this.tentacleOffset);
                this.tentacleLine.SetPosition(1, this.tentacleHitPoint);
                this.tentacleLine.startWidth = 3f;
                this.tentacleLine.endWidth = 3f;
                this.tentacleLine.widthMultiplier = 1f;

                // If tentacleHitPoint is close to player, move to ready to eat state
                if ( this.currentTentacleState != TentacleState.ReadyToEat && Tools.FastAbsWithinRange(tentacleHitPoint.x - base.transform.position.x, 27f))
                {
                    base.frame = 21;
                    this.currentTentacleState = TentacleState.ReadyToEat;
                }
            }
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

        protected void ThingStartMelee()
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
            SwitchToMonsterSprites(this.thingMeleeMaterial);
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
            if (shouldTryHitTerrain && this.ThingHitTerrain(0, 3))
            {
                this.meleeHasHit = true;
            }
            this.TriggerBroMeleeEvent();
        }

        protected bool ThingHitTerrain(int offset = 0, int meleeDamage = 2)
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
            //this.sound.PlaySoundEffectAt(this.soundHolder.meleeHitTerrainSound, 0.3f, base.transform.position, 1f, true, false, false, 0f);
            // Play hitting terrain sound for the thing
            EffectsController.CreateProjectilePopWhiteEffect(base.X + this.width * base.transform.localScale.x, base.Y + this.height + 4f);
            return true;
        }

        protected void ThingAnimateMelee()
        {
            if (pauseMelee)
            {
                --base.frame;
            }

            this.SetGunPosition(meleeOffsetX, meleeOffsetY);
            base.frameRate = 0.07f;
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
                this.CancelMelee();
                if (this.meleeBufferedPress)
                {
                    this.meleeBufferedPress = false;
                    this.StartCustomMelee();
                }
            }
        }

        protected void ThingRunMeleeMovement()
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

        protected void ThingCancelMelee()
        {
            if ( this.doingMelee )
            {
                this.gunSprite.meshRender.material = this.thingGunMaterial;
                this.gunSprite.RecalcTexture();
                EnterMonsterForm();
            }
        }
        #endregion
    }
}
