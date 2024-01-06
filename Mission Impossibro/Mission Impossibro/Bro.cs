using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BroMakerLib;
using BroMakerLib.Loggers;
using BroMakerLib.CustomObjects.Bros;
using UnityEngine;
using HarmonyLib;

namespace Mission_Impossibro
{
    [HeroPreset("Mission Impossibro", HeroType.Rambro)]
    public class Bro : CustomHero
    {
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
        protected Vector3 grappleOffset = new Vector3(0f, 8f, 0f);
        protected bool grappleAttached = false;
        protected float grappleCooldown = 0f;
        protected bool exitingGrapple = false;
        protected int grappleFrame = 0;

        protected override void Awake()
        {
            base.Awake();

            projectile = new GameObject("TranqDart", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(TranqDart) }).GetComponent<TranqDart>();
            projectile.soundHolder = (HeroController.GetHeroPrefab(HeroType.Rambro) as Rambro).projectile.soundHolder;
            projectile.enabled = false;
            grappleLine = new GameObject("GrappleLine", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
            grappleLine.transform.parent = this.transform;
            grappleLine.material = ResourcesController.GetMaterial(".\\Mods\\Development - BroMaker\\Storage\\Bros\\Mission Impossibro\\line.png");
        }

        protected override void Start()
        {
            base.Start();
            // DEBUG
            Instance = this;
        }

        protected override void Update()
        {
            base.Update();

            fireCooldown -= this.t;
            grappleCooldown -= this.t;

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

            BMLogger.Log("in air jump");

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
                BMLogger.Log("found grapple point");
                return true;
            }
            BMLogger.Log("NO grapple point found");
            return false;
        }

        public void AttachGrapple()
        {
            BMLogger.Log("attempting attach grapple");
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

        // Primary fire methods
        protected override void SetGunPosition(float xOffset, float yOffset)
        {
            // Fixes arms being offset from body
            this.gunSprite.transform.localPosition = new Vector3(xOffset + 1f, yOffset, -1f);
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
        }

        protected override void UseFire()
        {
            base.UseFire();

            this.fireCooldown = 0.6f;
        }

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            this.gunFrame = 3;
            y += 3;
            this.SetGunSprite(this.gunFrame, 0);
            this.TriggerBroFireEvent();
            EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed * 0.15f, base.transform);
            lastFiredTranq = ProjectileController.SpawnProjectileLocally(this.projectile, this, x, y, base.transform.localScale.x * bulletSpeed, 0, base.playerNum) as TranqDart;
            lastFiredTranq.Setup();
        }

        protected override void ActivateGun()
        {
            base.ActivateGun();
        }

        protected override void RunGun()
        {
            if (!this.WallDrag && this.gunFrame > 0)
            {
                this.gunCounter += this.t;
                if (this.gunCounter > 0.0334f)
                {
                    this.gunCounter -= 0.0334f;
                    this.gunFrame--;
                    if ( this.gunFrame < 1 && this.fire )
                    {
                        this.gunFrame = 1;
                    }
                    this.SetGunSprite(this.gunFrame, 0);
                }
            }
        }
    }
}
