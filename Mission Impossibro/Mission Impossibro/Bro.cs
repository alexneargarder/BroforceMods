using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BroMakerLib;
using BroMakerLib.Loggers;
using BroMakerLib.CustomObjects.Bros;
using UnityEngine;

namespace Mission_Impossibro
{
    [HeroPreset("Mission Impossibro", HeroType.Rambro)]
    public class Bro : CustomHero
    {
        TranqDart lastFiredTranq;
        float fireCooldown;

        protected override void Awake()
        {
            base.Awake();

            projectile = new GameObject("TranqDart", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(TranqDart) }).GetComponent<TranqDart>();
            projectile.soundHolder = (HeroController.GetHeroPrefab(HeroType.Rambro) as Rambro).projectile.soundHolder;
            projectile.enabled = false;
        }

        protected override void Start()
        {
            base.Start();
            
        }

        protected override void Update()
        {
            base.Update();

            fireCooldown -= this.t;
        }

        protected override void SetGunPosition(float xOffset, float yOffset)
        {
            // Fixes arms being offset from body
            this.gunSprite.transform.localPosition = new Vector3(xOffset + 1f, yOffset, -1f);
        }

        public override void UIOptions()
        {
            if ( GUILayout.Button("Check") )
            {
                BMLogger.Log("\n\n");
                Component[] allComponents;

                allComponents = (HeroController.GetHeroPrefab(HeroType.Rambro)).projectile.GetComponents(typeof(Component));
                foreach (Component comp in allComponents)
                {
                    BMLogger.Log("attached: " + comp.name + " also " + comp.GetType());
                }
                BMLogger.Log("\n\n");
            }
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
            lastFiredTranq = ProjectileController.SpawnProjectileLocally(this.projectile, this, x, y, base.transform.localScale.x * 400, 0, base.playerNum) as TranqDart;
            lastFiredTranq.Setup();
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
