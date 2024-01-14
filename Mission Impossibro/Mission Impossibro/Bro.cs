using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BroMakerLib;
using BroMakerLib.Loggers;
using BroMakerLib.CustomObjects.Bros;
using UnityEngine;
using HarmonyLib;
using System.IO;
using System.Reflection;

namespace Mission_Impossibro
{
    [HeroPreset("Mission Impossibro", HeroType.Rambro)]
    public class Bro : CustomHero
    {
        // Sprite variables
        Material normalMaterial, stealthMaterial, normalGunMaterial, stealthGunMaterial;
        bool wasInvulnerable = false;

        // Primary variables
        TranqDart lastFiredTranq;
        float fireCooldown;
        protected const float bulletSpeed = 600f;
        protected bool wallHangFiring = false;

        // Grapple variables
        LineRenderer grappleLine;
        protected Vector3 grappleHitPoint;
        protected const float grappleRange = 300f;
        protected const float grappleSpeed = 200f;
        protected float grappleMaterialScale = 0.25f;
        protected float grappleMaterialOffset = 0.25f;
        protected Vector3 grappleOffset = new Vector3(0f, 14f, 0f);
        protected bool grappleAttached = false;
        protected float grappleCooldown = 0f;
        protected bool exitingGrapple = false;
        protected int grappleFrame = 0;
        protected bool wasAttachedToBlock = false;
        protected Block grappleAttachBlock = null;

        // Special variables
        protected float specialTime = 0f;
        protected bool stealthActive = false;
        protected bool triggeringExplosives = false;
        protected bool readyToDetonate = false;
        protected int usingSpecialFrame = 0;
        protected List<Explosive> currentExplosives;
        protected const int MaxExplosives = 5;
        protected Explosive explosivePrefab;

        // DEBUG variables
        public static string offsetXstr = "3";
        public static string offsetYstr = "0";
        public static float offsetX = 3f;
        public static float offsetY = 0f;


        protected override void Awake()
        {
            base.Awake();

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            projectile = new GameObject("TranqDart", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(TranqDart) }).GetComponent<TranqDart>();
            projectile.soundHolder = (HeroController.GetHeroPrefab(HeroType.Rambro) as Rambro).projectile.soundHolder;
            projectile.enabled = false;

            grappleLine = new GameObject("GrappleLine", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
            grappleLine.transform.parent = this.transform;
            grappleLine.material = ResourcesController.GetMaterial(directoryPath, "line.png");

            explosivePrefab = new GameObject("Explosive", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(Explosive) }).GetComponent<Explosive>();
            explosivePrefab.enabled = false;
            explosivePrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.McBrover) as McBrover).projectile.soundHolder;
        }

        protected override void Start()
        {
            base.Start();

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            this.normalMaterial = this.material;
            this.stealthMaterial = ResourcesController.GetMaterial(directoryPath, "spriteStealth.png");

            this.normalGunMaterial = this.gunSprite.meshRender.material;
            this.stealthGunMaterial = ResourcesController.GetMaterial(directoryPath, "gunSpriteStealth.png");
        }

        protected override void Update()
        {
            if (this.invulnerable)
            {
                this.wasInvulnerable = true;
            }
            base.Update();
            // Check if invulnerability ran out
            if (this.wasInvulnerable && !this.invulnerable)
            {
                normalMaterial.SetColor("_TintColor", Color.gray);
                stealthMaterial.SetColor("_TintColor", Color.gray);
                gunSprite.meshRender.material.SetColor("_TintColor", Color.gray);
            }

            if ( fireCooldown > 0 )
            {
                fireCooldown -= this.t;
            }
            if ( grappleCooldown > 0 )
            {
                grappleCooldown -= this.t;
            }
            if ( specialTime > 0 )
            {
                specialTime -= this.t;
                if ( specialTime <= 0 )
                {
                    StartDetonating();
                }
            }

            if ( this.wallHangFiring && !this.wallDrag )
            {
                this.wallHangFiring = false;
            }

            if ( this.grappleAttached && this.buttonJump && !this.wasButtonJump && this.grappleCooldown <= 0 )
            {
                DetachGrapple();
            }

            // Detach grapple
            if ( this.actionState == ActionState.Dead )
            {
                InstantDetachGrapple();
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            // Detach grapple if block it's attached to is destroyed
            if ( this.grappleAttached && this.wasAttachedToBlock && (this.grappleAttachBlock == null || this.grappleAttachBlock.destroyed || this.grappleAttachBlock.health <= 0) )
            {
                DetachGrapple();
            }
        }

        public void makeTextBox(string label, ref string text, ref float val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            text = GUILayout.TextField(text);
            GUILayout.EndHorizontal();

            float temp;
            if ( float.TryParse(text, out temp) )
            {
                val = temp;
            }
        }

        public override void UIOptions()
        {
            makeTextBox("x", ref offsetXstr, ref offsetX);
            makeTextBox("y", ref offsetYstr, ref offsetY);
        }

        // Grapple methods
        protected override void AirJump()
        {
            base.AirJump();

            if ( !this.grappleAttached && this.grappleCooldown <= 0 && !this.doingMelee && SearchForGrapplePoint() )
            {
                AttachGrapple();
            }
        }

        protected override void CalculateMovement()
        {
            if ( this.grappleAttached )
            {
                base.CalculateMovement();
                if ( this.up )
                {
                    this.yI = grappleSpeed;
                }
                else if ( this.down )
                {
                    this.yI = -grappleSpeed;
                }
                else
                {
                    this.yI = 0;
                }
                this.xI = 0;
            }
            else
            {
                base.CalculateMovement();
            }
        }

        protected override void ApplyFallingGravity()
        {
            if (!this.grappleAttached)
            {
                base.ApplyFallingGravity();
            }
            // Make bro able to cling to walls indefinitely
            if ( this.wallDrag && this.yI < 0 )
            {
                if ( this.down )
                {
                    this.yI = -100;
                }
                else
                {
                    this.yI = 0;
                }
            }
        }

        protected override void RunMovement()
        {
            base.RunMovement();
            if ( this.grappleAttached )
            {
                UpdateGrapplePosition();
            }
        }

        public bool SearchForGrapplePoint()
        {
            if (Physics.Raycast(base.transform.position, Vector3.up, out raycastHit, grappleRange, this.groundLayer | this.barrierLayer))
            {
                grappleHitPoint = this.raycastHit.point;
                return true;
            }
            return false;
        }

        public void AttachGrapple()
        {
            this.grappleLine.enabled = true;
            grappleAttachBlock = this.raycastHit.collider.gameObject.GetComponent<Block>();
            this.wasAttachedToBlock = (grappleAttachBlock != null);
            this.grappleLine.SetPosition(0, base.transform.position + this.grappleOffset);
            this.grappleLine.SetPosition(1, this.grappleHitPoint);
            float magnitude = (this.grappleHitPoint - (base.transform.position + this.grappleOffset)).magnitude;
            this.grappleLine.material.SetTextureScale("_MainTex", new Vector2(magnitude * this.grappleMaterialScale, 1f));
            this.grappleLine.material.SetTextureOffset("_MainTex", new Vector2(magnitude * this.grappleMaterialOffset, 0f));
            this.grappleLine.startWidth = 1.5f;
            this.grappleLine.endWidth = 1.5f;
            grappleAttached = true;
            this.grappleCooldown = 0.1f;
            this.grappleFrame = 0;
            this.frameRate = 0.05f;
        }

        public void DetachGrapple()
        {
            this.grappleLine.enabled = false;
            grappleAttached = false;
            this.grappleCooldown = 0.1f;
            this.exitingGrapple = true;
        }

        // Doesn't play detach grapple animation
        public void InstantDetachGrapple()
        {
            this.DeactivateGun();
            this.grappleLine.enabled = false;
            grappleAttached = false;
            this.grappleCooldown = 0.1f;
        }

        public void UpdateGrapplePosition()
        {
            this.grappleLine.SetPosition(0, base.transform.position + this.grappleOffset);
            float magnitude = (this.grappleHitPoint - (base.transform.position + this.grappleOffset)).magnitude;
            this.grappleLine.material.SetTextureScale("_MainTex", new Vector2(magnitude * this.grappleMaterialScale, 1f));
            this.grappleLine.material.SetTextureOffset("_MainTex", new Vector2(magnitude * this.grappleMaterialOffset, 0f));
            // Detach grapple if above hit point
            if ( base.transform.position.y + 5 > this.grappleHitPoint.y )
            {
                DetachGrapple();
            }
        }

        protected override void ChangeFrame()
        {
            if (!(this.grappleAttached || this.exitingGrapple ))
            {
                base.ChangeFrame();
            }
            else
            {
                if ( this.usingSpecial )
                {
                    AnimateSpecial();
                }
                else
                {
                    AnimateGrapple();
                }
            }
        }

        public void AnimateGrapple()
        {
            this.SetSpriteOffset(0f, 0f);

            // Hold frame
            if ( !this.exitingGrapple && this.grappleFrame >= 2 )
            {
                if ( !this.stealthActive )
                {
                    // Tranq gun offset
                    this.SetGunPosition(3.4f, -1.1f);
                    this.ActivateGun();
                }
                else
                {
                    // Explosives offset
                    this.SetGunPosition(offsetX, offsetY);
                    this.ActivateGun();
                }
                this.grappleFrame = 2;
            }
            else if ( this.exitingGrapple )
            {
                // Deactivate gun once we've moved to the next frame where he's holding another gun if not in stealth
                if ( !this.stealthActive && this.grappleFrame == 3 )
                {
                    this.DeactivateGun();
                }
                // Finished exiting grapple
                else if ( this.grappleFrame > 4 )
                {
                    base.frame = 0;
                    this.SetGunPosition(0, 0);
                    this.ActivateGun();
                    this.sprite.SetLowerLeftPixel(0, this.spritePixelHeight);
                    this.exitingGrapple = false;
                    return;
                }
                // First frame of exitingGrapple
                else if ( !this.stealthActive )
                {
                    this.SetGunPosition(3.4f, -1.1f);
                }
                // All frames of exiting grapple
                else
                {
                    this.SetGunPosition(offsetX, offsetY);
                }
            }
            // Entering frames deactivate gun if stealth is not active
            else if ( !this.stealthActive )
            {
                this.DeactivateGun();
            }
            // Set stealth gun offset
            else
            {
                this.SetGunPosition(offsetX, offsetY);
            }
            this.sprite.SetLowerLeftPixel(this.grappleFrame * this.spritePixelWidth, 7 * this.spritePixelHeight);
            ++this.grappleFrame;
        }

        public override void PlayChimneyFlipSound(float volume)
        {
            base.PlayChimneyFlipSound(volume);

            if ( this.grappleAttached )
            {
                DetachGrapple();
            }
        }

        protected override void PressHighFiveMelee(bool forceHighFive = false)
        {
            if ( this.grappleAttached )
            {
                InstantDetachGrapple();
            }
            base.PressHighFiveMelee(forceHighFive);
        }

        protected override void PlayFootStepSound(AudioClip[] clips, float v, float p)
        {
            if ( !this.grappleAttached )
            {
                base.PlayFootStepSound(clips, v, p);
            }
        }

        // Primary fire methods
        protected override void SetGunPosition(float xOffset, float yOffset)
        {
            // Fixes arms being offset from body
            this.gunSprite.transform.localPosition = new Vector3(xOffset, yOffset + 0.4f, -1f);
        }

        protected override void StartFiring()
        {
            if (this.fireDelay <= 0f)
            {
                if (this.fireCooldown <= 0)
                {
                    this.fireCounter = this.fireRate;
                }
            }
            if ( !readyToDetonate )
            {
                readyToDetonate = true;
            }
        }

        protected override void UseFire()
        {
            if (this.doingMelee)
            {
                this.CancelMelee();
            }
            float num = base.transform.localScale.x;
            if (!base.IsMine && base.Syncronize)
            {
                num = (float)this.syncedDirection;
            }
            if (Connect.IsOffline)
            {
                this.syncedDirection = (int)base.transform.localScale.x;
            }

            // Fire while on grapple
            if (this.grappleAttached || this.exitingGrapple)
            {
                this.FireWeapon(base.X + num * 14f, base.Y + 10f, num * bulletSpeed, 0);
            }
            // Normal fire and stealth fire
            else if ( !this.wallDrag || this.stealthActive )
            {
                this.FireWeapon(base.X + num * 12f, base.Y + 10f, num * bulletSpeed, 0);
            }
            // Fire while wall dragging
            else
            {
                num *= -1;
                this.FireWeapon(base.X + num * 14f, base.Y + 10f, num * bulletSpeed, 0);
            }
            
            Map.DisturbWildLife(base.X, base.Y, 60f, base.playerNum);

            this.fireCooldown = this.fireRate;
        }

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            if ( !this.stealthActive )
            {
                y += 3;

                if ( this.grappleAttached )
                {
                    this.gunFrame = 1;
                    this.SetGunSprite(this.gunFrame + 22, 0);
                }
                else if ( !this.wallDrag )
                {
                    this.gunFrame = 3;
                    this.SetGunSprite(this.gunFrame, 0);
                }
                else
                {
                    this.gunFrame = 3;
                }
                
                this.TriggerBroFireEvent();
                EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed * 0.15f, base.transform);
                lastFiredTranq = ProjectileController.SpawnProjectileLocally(this.projectile, this, x, y, xSpeed, ySpeed, base.playerNum) as TranqDart;
                lastFiredTranq.Setup();

                // Play tranq dart gun sound
            }
            else if ( !this.triggeringExplosives && this.currentExplosives.Count < MaxExplosives )
            {
                this.gunFrame = 3;
                this.SetGunSprite(this.gunFrame, 0);

                Explosive explosive;
                float horizontalSpeed = 100f;
                float verticalSpeed = 50f;
                if ( this.down )
                {
                    horizontalSpeed = 0f;
                    verticalSpeed = -100f;
                }
                else if ( this.up )
                {
                    horizontalSpeed = 0f;
                    verticalSpeed = 250f;
                }
                currentExplosives.Add(explosive = ProjectileController.SpawnProjectileLocally(this.explosivePrefab, this, x, y, base.transform.localScale.x * horizontalSpeed + this.xI, verticalSpeed + this.yI, base.playerNum) as Explosive);
                SachelPack otherSachel = (HeroController.GetHeroPrefab(HeroType.McBrover) as McBrover).projectile as SachelPack;
                explosive.AssignNullValues(otherSachel);
                explosive.life = this.specialTime + 4f;
                explosive.enabled = true;

                // Play explosives throw sound

                if ( this.currentExplosives.Count == MaxExplosives )
                {
                    readyToDetonate = false;
                }
            }
            else if ( readyToDetonate )
            {
                StartDetonating();
            }
        }

        protected override void RunGun()
        {
            if (!this.WallDrag)
            {
                // FIring tranq gun
                if ( !this.stealthActive && !(this.grappleAttached || this.exitingGrapple) )
                {
                    if (this.gunFrame > 0)
                    {
                        this.gunCounter += this.t;
                        if (this.gunCounter > 0.0334f)
                        {
                            this.gunCounter -= 0.0334f;
                            this.gunFrame--;
                            if (this.gunFrame < 1 && this.fire)
                            {
                                this.gunFrame = 1;
                            }
                            this.SetGunSprite(this.gunFrame, 0);
                        }
                    }
                    else
                    {
                        this.SetGunSprite(0, 0);
                    }
                }
                // Trigerring explosives
                else if ( this.triggeringExplosives )
                {
                    this.gunCounter += this.t;
                    if (this.gunCounter > 0.12f)
                    {
                        this.gunCounter -= 0.12f;
                        ++this.gunFrame;
                        if (this.gunFrame < 4)
                        {
                            this.SetGunSprite(17 + this.gunFrame, 0);
                        }
                        else
                        {
                            if ( !this.usingSpecial )
                            {
                                this.StopSpecial();
                            }
                            this.gunFrame = 3;
                            this.SetGunSprite(17 + this.gunFrame, 0);
                        }
                    }
                }
                // Placing explosives
                else if ( this.stealthActive && this.currentExplosives.Count < MaxExplosives )
                {
                    if (this.gunFrame > 0)
                    {
                        this.gunCounter += this.t;
                        if (this.gunCounter > 0.0334f)
                        {
                            this.gunCounter -= 0.0334f;
                            this.gunFrame--;
                            this.SetGunSprite(this.gunFrame, 0);
                        }
                    }
                    else
                    {
                        this.SetGunSprite(0, 0);
                    }
                }
                // Shoot while on grapple
                else if (!this.stealthActive && (this.grappleAttached || this.exitingGrapple) )
                {
                    if (this.gunFrame > 0 && this.grappleFrame > 0 )
                    {
                        this.gunCounter += this.t;
                        if (this.gunCounter > 0.0334f)
                        {
                            this.gunCounter -= 0.0334f;
                            this.gunFrame--;
                            this.SetGunSprite(this.gunFrame + 22, 0);
                        }
                    }
                    else if ( this.grappleFrame > 0 )
                    {
                        this.SetGunSprite(22, 0);
                    }
                    else
                    {
                        this.SetGunSprite(0, 0);
                    }
                }
                // Out of explosives, wait to trigger
                else
                {
                    this.SetGunSprite(18, 0);
                }
            }
            // Shoot while wall clinging
            else if ( !this.stealthActive && !this.triggeringExplosives )
            {
                if ( this.gunFrame > 0 )
                {
                    this.wallHangFiring = true;

                    this.gunCounter += this.t;
                    if (this.gunCounter > 0.0334f)
                    {
                        this.gunCounter -= 0.0334f;
                        this.gunFrame--;
                        if (this.gunFrame < 2 && this.fire)
                        {
                            this.gunFrame = 2;
                        }
                        this.sprite.SetLowerLeftPixel((this.gunFrame + 7)  * this.spritePixelWidth,this.spritePixelHeight * 7);
                    }
                }
                // Keep animation ready to fire if we have fired previously
                else if ( this.wallHangFiring )
                {
                    this.sprite.SetLowerLeftPixel((this.gunFrame + 7) * this.spritePixelWidth, this.spritePixelHeight * 7);
                }
            }
        }

        protected override void AnimateWallDrag()
        {
            if ( this.wallHangFiring )
            {
                return;
            }
            this.wallClimbAnticipation = false;
            if (this.useNewKnifeClimbingFrames)
            {
                if (this.yI > 100f)
                {
                    if (this.knifeHand % 2 == 0)
                    {
                        if (base.frame > 1)
                        {
                            base.frame = 1;
                        }
                        int num = 12 + Mathf.Clamp(base.frame, 0, 1);
                        this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)(this.spritePixelHeight * 3));
                    }
                    else
                    {
                        if (base.frame > 1)
                        {
                            base.frame = 1;
                        }
                        int num2 = 22 + Mathf.Clamp(base.frame, 0, 1);
                        this.sprite.SetLowerLeftPixel((float)(num2 * this.spritePixelWidth), (float)(this.spritePixelHeight * 3));
                    }
                }
                else
                {
                    if (base.frame == 2)
                    {
                        this.PlayKnifeClimbSound();
                    }
                    if (this.knifeHand % 2 == 0)
                    {
                        int num3 = 12 + Mathf.Clamp(base.frame, 0, 4);
                        if (!FluidController.IsSubmerged(this) && this.down)
                        {
                            EffectsController.CreateFootPoofEffect(base.X + base.transform.localScale.x * 6f, base.Y + 16f, 0f, Vector3.up * 1f, BloodColor.None);
                        }
                        this.sprite.SetLowerLeftPixel((float)(num3 * this.spritePixelWidth), (float)(this.spritePixelHeight * 3));
                    }
                    else
                    {
                        int num4 = 22 + Mathf.Clamp(base.frame, 0, 4);
                        if (!FluidController.IsSubmerged(this) && this.down)
                        {
                            EffectsController.CreateFootPoofEffect(base.X + base.transform.localScale.x * 6f, base.Y + 16f, 0f, Vector3.up * 1f, BloodColor.None);
                        }
                        this.sprite.SetLowerLeftPixel((float)(num4 * this.spritePixelWidth), (float)(this.spritePixelHeight * 3));
                    }
                }
            }
            else if (this.knifeHand % 2 == 0)
            {
                int num5 = 11 + Mathf.Clamp(base.frame, 0, 2);
                if (!FluidController.IsSubmerged(this) && this.down)
                {
                    EffectsController.CreateFootPoofEffect(base.X + base.transform.localScale.x * 6f, base.Y + 12f, 0f, Vector3.up * 1f, BloodColor.None);
                }
                this.sprite.SetLowerLeftPixel((float)(num5 * this.spritePixelWidth), (float)this.spritePixelHeight);
            }
            else
            {
                int num6 = 14 + Mathf.Clamp(base.frame, 0, 2);
                if (!FluidController.IsSubmerged(this) && this.down)
                {
                    EffectsController.CreateFootPoofEffect(base.X + base.transform.localScale.x * 6f, base.Y + 12f, 0f, Vector3.up * 1f, BloodColor.None);
                }
                this.sprite.SetLowerLeftPixel((float)(num6 * this.spritePixelWidth), (float)this.spritePixelHeight);
            }
        }

        protected override void Jump(bool wallJump)
        {
            if ( wallJump )
            {
                this.wallHangFiring = false;
            }
            
            base.Jump(wallJump);
        }

        // Special methods
        protected override void PressSpecial()
        {
            if (!this.stealthActive && !this.hasBeenCoverInAcid && this.SpecialAmmo > 0)
            {
                if ( this.grappleAttached )
                {
                    this.usingSpecialFrame = 1;
                }
                else
                {
                    this.usingSpecialFrame = 0;
                }
                this.usingSpecial = true;
                //this.specialTime = 10f;
                this.specialTime = 100000000f;
                Map.ForgetPlayer(base.playerNum, true, false);
                this.currentExplosives = new List<Explosive>();
                this.fireRate = 0.3f;
            }
            else if (this.specialTime > 0)
            {
                StartDetonating();
            }
        }

        protected override void UseSpecial()
        {
            --this.SpecialAmmo;
        }

        public override bool IsInStealthMode()
        {
            return this.stealthActive || base.IsInStealthMode();
        }

        protected override void AlertNearbyMooks()
        {
            if (!this.stealthActive)
            {
                base.AlertNearbyMooks();
            }
        }

        protected void StartDetonating()
        {
            if ( !this.triggeringExplosives )
            {
                this.specialTime = 0;
                this.gunCounter = 0;
                this.triggeringExplosives = true;
                this.gunFrame = 1;
                this.SetGunSprite(17 + this.gunFrame, 0);
            }
        }

        protected void StopSpecial()
        {
            this.usingSpecial = true;
            if ( this.grappleAttached )
            {
                this.usingSpecialFrame = 4;
            }
            else
            {
                this.usingSpecialFrame = 5;
            }
            this.fireRate = 0.4f;
            // Detonate explosives
            foreach ( Explosive explosive in this.currentExplosives )
            {
                explosive.Death();
            }
        }

        protected override void AnimateSpecial()
        {
            base.frameRate = 0.0667f;
            if ( this.grappleAttached || this.exitingGrapple )
            {
                this.DeactivateGun();

                // Put on balaclava
                if (this.specialTime > 0)
                {
                    if (this.usingSpecialFrame > 4)
                    {
                        this.usingSpecial = false;
                        base.GetComponent<Renderer>().material = this.stealthMaterial;
                        this.gunSprite.meshRender.material = this.stealthGunMaterial;
                        this.stealthActive = true;
                        this.ActivateGun();
                        this.SetGunPosition(offsetX, offsetY);
                        this.gunFrame = 0;
                        this.UseSpecial();
                        this.ChangeFrame();
                        return;
                    }

                    this.sprite.SetLowerLeftPixel((26 + this.usingSpecialFrame) * this.spritePixelWidth, 9 * this.spritePixelHeight);

                    ++this.usingSpecialFrame;
                }
                // Take off balaclava
                else
                {
                    this.triggeringExplosives = false;

                    if (this.usingSpecialFrame < 1)
                    {
                        this.usingSpecial = false;
                        base.GetComponent<Renderer>().material = this.normalMaterial;
                        this.gunSprite.meshRender.material = this.normalGunMaterial;
                        this.stealthActive = false;
                        this.ActivateGun();
                        this.ChangeFrame();
                        return;
                    }

                    this.sprite.SetLowerLeftPixel((26 + this.usingSpecialFrame) * this.spritePixelWidth, 9 * this.spritePixelHeight);

                    --this.usingSpecialFrame;
                }
            }
            else
            {
                this.DeactivateGun();
                // Put on balaclava
                if (this.specialTime > 0)
                {
                    if (this.usingSpecialFrame > 5)
                    {
                        base.frame = 0;
                        this.usingSpecial = false;
                        base.GetComponent<Renderer>().material = this.stealthMaterial;
                        this.gunSprite.meshRender.material = this.stealthGunMaterial;
                        this.stealthActive = true;
                        this.gunFrame = 0;
                        this.ActivateGun();
                        this.ChangeFrame();
                        this.UseSpecial();
                        return;
                    }

                    this.sprite.SetLowerLeftPixel((26 + this.usingSpecialFrame) * this.spritePixelWidth, 8 * this.spritePixelHeight);

                    ++this.usingSpecialFrame;
                }
                // Take off balaclava
                else
                {
                    this.triggeringExplosives = false;

                    if (this.usingSpecialFrame < 0)
                    {
                        base.frame = 0;
                        this.usingSpecial = false;
                        base.GetComponent<Renderer>().material = this.normalMaterial;
                        this.gunSprite.meshRender.material = this.normalGunMaterial;
                        this.stealthActive = false;
                        this.gunFrame = 0;
                        this.ActivateGun();
                        this.ChangeFrame();
                        return;
                    }

                    this.sprite.SetLowerLeftPixel((26 + this.usingSpecialFrame) * this.spritePixelWidth, 8 * this.spritePixelHeight);

                    --this.usingSpecialFrame;
                }
            }
        }
    }
}
