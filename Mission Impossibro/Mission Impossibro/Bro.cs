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

        // Grapple variables
        LineRenderer grappleLine;
        public static Bro Instance;
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

        // Special variables
        protected float specialTime = 0f;
        protected bool stealthActive = false;
        protected bool triggeringExplosives = false;
        protected bool readyToDetonate = false;
        protected int usingSpecialFrame = 0;
        protected List<Explosive> currentExplosives;
        protected const int MaxExplosives = 5;
        protected Explosive explosivePrefab;

        protected override void Awake()
        {
            base.Awake();

            projectile = new GameObject("TranqDart", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(TranqDart) }).GetComponent<TranqDart>();
            projectile.soundHolder = (HeroController.GetHeroPrefab(HeroType.Rambro) as Rambro).projectile.soundHolder;
            projectile.enabled = false;

            grappleLine = new GameObject("GrappleLine", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
            grappleLine.transform.parent = this.transform;
            grappleLine.material = ResourcesController.GetMaterial(".\\Mods\\Development - BroMaker\\Storage\\Bros\\Mission Impossibro\\line.png");

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
            // DEBUG
            Instance = this;
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

            if (this.grappleAttached && this.buttonJump && !this.wasButtonJump && this.grappleCooldown <= 0 )
            {
                DetachGrapple();
            }
        }

        public override void UIOptions()
        {
            if (GUILayout.Button("Try attach"))
            {
                if ( Instance.SearchForGrapplePoint() )
                {
                    Instance.AttachGrapple();
                }
            }
        }

        // Grapple methods
        protected override void AirJump()
        {
            base.AirJump();

            if ( !this.grappleAttached && this.grappleCooldown <= 0 && SearchForGrapplePoint() )
            {
                AttachGrapple();
            }
        }

        protected override void CalculateMovement()
        {
            if ( this.grappleAttached )
            {
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

        public void UpdateGrapplePosition()
        {
            this.grappleLine.SetPosition(0, base.transform.position + this.grappleOffset);
            float magnitude = (this.grappleHitPoint - (base.transform.position + this.grappleOffset)).magnitude;
            this.grappleLine.material.SetTextureScale("_MainTex", new Vector2(magnitude * this.grappleMaterialScale, 1f));
            this.grappleLine.material.SetTextureOffset("_MainTex", new Vector2(magnitude * this.grappleMaterialOffset, 0f));
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
                AnimateGrapple();
            }
        }

        public void AnimateGrapple()
        {
            this.SetSpriteOffset(0f, 0f);
            this.DeactivateGun();
            if ( !this.exitingGrapple && this.grappleFrame > 2 )
            {
                this.grappleFrame = 2;
            }
            else if ( this.exitingGrapple && this.grappleFrame > 4 )
            {
                base.frame = 0;
                this.ActivateGun();
                this.sprite.SetLowerLeftPixel(0, this.spritePixelHeight);
                this.exitingGrapple = false;
                return;
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

        // Primary fire methods
        protected override void SetGunPosition(float xOffset, float yOffset)
        {
            // Fixes arms being offset from body
            this.gunSprite.transform.localPosition = new Vector3(xOffset, yOffset, -1f);
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
            this.FireWeapon(base.X + num * 10f, base.Y + 8f, num * 400f, (float)UnityEngine.Random.Range(-20, 20));
            
            Map.DisturbWildLife(base.X, base.Y, 60f, base.playerNum);

            this.fireCooldown = this.fireRate;
        }

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            if ( !this.stealthActive )
            {
                this.gunFrame = 3;
                y += 3;
                this.SetGunSprite(this.gunFrame, 0);
                this.TriggerBroFireEvent();
                EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed * 0.15f, base.transform);
                lastFiredTranq = ProjectileController.SpawnProjectileLocally(this.projectile, this, x, y, base.transform.localScale.x * bulletSpeed, 0, base.playerNum) as TranqDart;
                lastFiredTranq.Setup();

                // Play tranq dart gun sound
            }
            else if ( !this.triggeringExplosives && this.currentExplosives.Count < MaxExplosives )
            {
                this.gunFrame = 4;
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
                explosive.life = this.specialTime + 2f;
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
                if ( !this.stealthActive )
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
                            this.StopSpecial();
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
                // Out of explosives, wait to trigger
                else
                {
                    this.SetGunSprite(18, 0);
                }
            }
        }

        // Special methods
        protected override void PressSpecial()
        {
            if (!this.hasBeenCoverInAcid && this.SpecialAmmo > 0)
            {
                this.usingSpecialFrame = 0;
                this.usingSpecial = true;
                this.specialTime = 10f;
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
            this.triggeringExplosives = false;
            this.usingSpecial = true;
            this.usingSpecialFrame = 5;
            this.fireRate = 0.4f;
            // Detonate explosives
            foreach ( Explosive explosive in this.currentExplosives )
            {
                explosive.Death();
            }
        }

        protected override void AnimateSpecial()
        {
            
            if ( this.grappleAttached || this.exitingGrapple )
            {
                // Put on balaclava
                if (this.specialTime > 0)
                {
                    if (this.usingSpecialFrame > 5)
                    {
                        this.usingSpecial = false;
                        base.GetComponent<Renderer>().material = this.stealthMaterial;
                        this.gunSprite.meshRender.material = this.stealthGunMaterial;
                        this.stealthActive = true;
                        this.UseSpecial();
                        return;
                    }

                    ++this.usingSpecialFrame;
                }
                // Take off balaclava
                else
                {
                    if (this.usingSpecialFrame < 0)
                    {
                        this.usingSpecial = false;
                        base.GetComponent<Renderer>().material = this.normalMaterial;
                        this.gunSprite.meshRender.material = this.normalGunMaterial;
                        this.stealthActive = false;
                        return;
                    }

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
                        this.UseSpecial();
                        return;
                    }

                    this.sprite.SetLowerLeftPixel((26 + this.usingSpecialFrame) * this.spritePixelWidth, 8 * this.spritePixelHeight);

                    ++this.usingSpecialFrame;
                }
                // Take off balaclava
                else
                {
                    if (this.usingSpecialFrame < 0)
                    {
                        base.frame = 0;
                        this.usingSpecial = false;
                        base.GetComponent<Renderer>().material = this.normalMaterial;
                        this.gunSprite.meshRender.material = this.normalGunMaterial;
                        this.stealthActive = false;
                        this.gunFrame = 0;
                        this.ActivateGun();
                        return;
                    }

                    this.sprite.SetLowerLeftPixel((26 + this.usingSpecialFrame) * this.spritePixelWidth, 8 * this.spritePixelHeight);

                    --this.usingSpecialFrame;
                }
            }
        }
    }
}
