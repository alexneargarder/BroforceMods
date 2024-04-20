using System;
using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using System.IO;
using System.Reflection;
using UnityEngine;
using RocketLib;
using Rogueforce;
using JetBrains.Annotations;
using RocketLib.Collections;

namespace Furibrosa
{
    [HeroPreset("Furibrosa", HeroType.Rambro)]
    public class Furibrosa : CustomHero
    {
        // General
        public static KeyBindingForPlayers switchWeaponKey;

        // Primary
        public enum PrimaryState
        {
            Crossbow = 0,
            FlareGun = 1,
            Switching = 2
        }
        PrimaryState currentState = PrimaryState.Crossbow;
        PrimaryState nextState;
        protected Bolt boltPrefab, explosiveBoltPrefab;
        protected bool releasedFire = false;
        protected float chargeTime = 0f;
        protected float crossbowDelay = 0f;
        protected bool charged = false;
        protected Material crossbowMat, crossbowHoldingMat, flareGunMat, flareGunHoldingMat;

        // Melee

        // Special

        protected bool acceptedDeath = false;
        bool wasInvulnerable = false;

        #region General
        protected override void Awake()
        {
            if (switchWeaponKey == null )
            {
                LoadKeyBinding();
            }

            base.Awake();
        }

        protected override void Start()
        {
            base.Start();

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            
            this.crossbowMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "gunSpriteCrossbow.png"), ResourcesController.Particle_AlphaBlend);
            this.crossbowHoldingMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "gunSpriteCrossbowHolding.png"), ResourcesController.Particle_AlphaBlend);
            this.flareGunMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "gunSpriteFlareGun.png"), ResourcesController.Particle_AlphaBlend);
            this.flareGunHoldingMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "gunSpriteFlareGunHolding.png"), ResourcesController.Particle_AlphaBlend);

            this.gunSprite.gameObject.layer = 28;
            this.gunSprite.meshRender.material = this.crossbowMat;

            
            boltPrefab = new GameObject("Bolt", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(BoxCollider), typeof(Bolt)}).GetComponent<Bolt>();
            boltPrefab.enabled = false;
            boltPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro).projectile.soundHolder;

            BoxCollider collider = new GameObject("BoltCollider", new Type[] { typeof(Transform), typeof(BoxCollider) }).GetComponent<BoxCollider>();
            collider.enabled = false;
            collider.transform.parent = boltPrefab.transform;

            Transform transform = new GameObject("BoltForeground", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) }).transform;
            transform.parent = boltPrefab.transform;
            boltPrefab.Setup(false);

            explosiveBoltPrefab = new GameObject("ExplosiveBolt", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(BoxCollider), typeof(Bolt) }).GetComponent<Bolt>();
            explosiveBoltPrefab.enabled = false;
            explosiveBoltPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro).projectile.soundHolder;

            BoxCollider explosiveCollider = new GameObject("BoltCollider", new Type[] { typeof(Transform), typeof(BoxCollider) }).GetComponent<BoxCollider>();
            explosiveCollider.enabled = false;
            explosiveCollider.transform.parent = explosiveBoltPrefab.transform;

            Transform explosiveTransform = new GameObject("BoltForeground", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) }).transform;
            explosiveTransform.parent = explosiveBoltPrefab.transform;
            explosiveBoltPrefab.Setup(true);
        }

        protected override void Update()
        {
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
                    this.acceptedDeath = false;
                }
            }

            if (this.invulnerable)
            {
                this.wasInvulnerable = true;
            }

            // Switch Weapon Pressed
            if ( switchWeaponKey.IsDown(playerNum) )
            {
                StartSwitchingWeapon();
            }

            // Check if invulnerability ran out
            if (this.wasInvulnerable && !this.invulnerable)
            {
                // Fix any not currently displayed textures
            }
        }

        public static void LoadKeyBinding()
        {
            if ( !AllModKeyBindings.TryGetKeyBinding("Furibrosa", "Switch Weapon", out switchWeaponKey) )
            {
                switchWeaponKey = new KeyBindingForPlayers("Switch Weapon", "Furibrosa");
            }
        }

        public override void UIOptions()
        {
            if (switchWeaponKey == null)
            {
                LoadKeyBinding();
            }
            int player;

            GUILayout.Space(10);
            switchWeaponKey.OnGUI(out player, true);
            GUILayout.Space(10);
        }
        #endregion

        #region Primary
        protected override void StartFiring()
        {
            this.chargeTime = 0f;
            this.charged = false;
            base.StartFiring();
        }

        protected override void ReleaseFire()
        {
            this.releasedFire = true;
            base.ReleaseFire();
        }

        protected override void RunFiring()
        {
            if (this.health <= 0)
            {
                return;
            }

            if ( this.currentState == PrimaryState.Crossbow )
            {
                if ( this.crossbowDelay > 0f )
                {
                    this.crossbowDelay -= this.t;
                }
                if ( this.crossbowDelay <= 0f )
                {
                    if (this.fire)
                    {
                        this.StopRolling();
                        this.chargeTime += this.t;
                    }
                    else if (this.releasedFire)
                    {
                        this.UseFire();
                        this.FireFlashAvatar();
                        this.SetGestureAnimation(GestureElement.Gestures.None);
                    }
                }
            }
            
        }

        protected override void UseFire()
        {
            if (this.doingMelee)
            {
                this.CancelMelee();
            }
            this.releasedFire = false;
            float num = base.transform.localScale.x;
            if (!base.IsMine && base.Syncronize)
            {
                num = (float)this.syncedDirection;
            }
            if (Connect.IsOffline)
            {
                this.syncedDirection = (int)base.transform.localScale.x;
            }
            this.FireWeapon(base.X, base.Y, num * 300f, 0);
            Map.DisturbWildLife(base.X, base.Y, 60f, base.playerNum);
        }

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            // Fire crossbow
            if ( this.currentState == PrimaryState.Crossbow )
            {
                // Fire explosive bolt
                if ( this.charged )
                {
                    x = base.X + base.transform.localScale.x * 10f;
                    y = base.Y + 8f;
                    xSpeed = base.transform.localScale.x * 500;
                    ySpeed = 0;
                    this.gunFrame = 3;
                    this.SetGunSprite(this.gunFrame, 0);
                    this.TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally(explosiveBoltPrefab, this, x, y, xSpeed, ySpeed, base.playerNum) as Bolt;
                    BMLogger.Log("life: " + firedBolt.life);
                    firedBolt.enabled = true;
                }
                // Fire normal bolt
                else
                {
                    x = base.X + base.transform.localScale.x * 10f;
                    y = base.Y + 8f;
                    xSpeed = base.transform.localScale.x * 350;
                    ySpeed = 0;
                    this.gunFrame = 3;
                    this.SetGunSprite(this.gunFrame, 0);
                    this.TriggerBroFireEvent();
                    EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed, base.transform);
                    Bolt firedBolt = ProjectileController.SpawnProjectileLocally(boltPrefab, this, x, y, xSpeed, ySpeed, base.playerNum) as Bolt;
                    firedBolt.enabled = true;
                }
                this.crossbowDelay = 0.3f;
            }
        }

        protected override void RunGun()
        {
            if ( this.currentState == PrimaryState.Crossbow )
            {
                if ( this.fire )
                {
                    if ( this.chargeTime > 0.2f )
                    {
                        if ( !this.charged )
                        {
                            this.gunCounter += this.t;
                            if (this.gunCounter < 1f && this.gunCounter > 0.09f)
                            {
                                this.gunCounter -= 0.09f;
                                if ( this.gunFrame < 2 )
                                {
                                    ++this.gunFrame;
                                    if (this.gunFrame == 2)
                                    {
                                        this.gunFrame = 7;
                                    }
                                }
                                else
                                {
                                    --this.gunFrame;
                                    if ( this.gunFrame == 3 )
                                    {
                                        this.gunFrame = 4;
                                        this.charged = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            this.gunCounter += this.t;
                            if (this.gunCounter > 0.06f)
                            {
                                this.gunCounter -= 0.06f;
                                --this.gunFrame;
                            }
                            if (this.gunFrame < 2)
                            {
                                this.gunFrame = 3;
                            }
                            //this.gunFrame = 5;
                        }
                        this.SetGunSprite(this.gunFrame + 14, 0);
                    }
                }
                else if (!this.WallDrag && this.gunFrame > 0)
                {
                    this.gunCounter += this.t;
                    if (this.gunCounter > 0.0334f)
                    {
                        this.gunCounter -= 0.0334f;
                        this.gunFrame--;
                        this.SetGunSprite(this.gunFrame, 0);
                    }
                }
            }
            // Animate flaregun
            else if (this.currentState == PrimaryState.FlareGun)
            {
                this.SetGunSprite(0, 0);
            }
            // Animate switching
            else
            {
                this.gunCounter += this.t;
                if (this.gunCounter > 0.08f)
                {
                    this.gunCounter -= 0.08f;
                    ++this.gunFrame;
                }

                if (this.gunFrame > 5)
                {
                    this.SwitchWeapon();
                }
                else
                {
                    this.SetGunSprite(22 + this.gunFrame, 0);
                }
            }

        }

        protected void StartSwitchingWeapon()
        {
            if ( !this.usingSpecial && this.currentState != PrimaryState.Switching )
            {
                this.CancelMelee();
                this.SetGestureAnimation(GestureElement.Gestures.None);
                if ( this.currentState == PrimaryState.Crossbow )
                {
                    this.nextState = PrimaryState.FlareGun;
                }
                else
                {
                    this.nextState = PrimaryState.Crossbow;
                }
                this.currentState = PrimaryState.Switching;
                this.gunFrame = 0;
                this.SetGunSprite(22 + this.gunFrame, 0);
            } 
        }

        protected void SwitchWeapon()
        {
            this.gunFrame = 0;
            this.currentState = this.nextState;
            if ( this.currentState == PrimaryState.FlareGun )
            {
                this.gunSprite.meshRender.material = this.flareGunMat;
            }
            else
            {
                this.gunSprite.meshRender.material = this.crossbowMat;
            }
            this.SetGunSprite(0, 0);
        }
        #endregion

        #region Melee
        #endregion

        #region Special
        #endregion
    }
}
