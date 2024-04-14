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

namespace Furibrosa
{
    [HeroPreset("Furibrosa", HeroType.Rambro)]
    public class Furibrosa : CustomHero
    {
        // General
        public static KeyBindingForPlayers switchWeaponKey;

        // Primary
        protected Bolt boltPrefab;
        protected bool releasedFire = false;

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

            boltPrefab = new GameObject("Bolt", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(BoxCollider), typeof(Bolt)}).GetComponent<Bolt>();
            boltPrefab.enabled = false;
            boltPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.Predabro) as Predabro).projectile.soundHolder;

            BoxCollider collider = new GameObject("BoltCollider", new Type[] { typeof(Transform), typeof(BoxCollider) }).GetComponent<BoxCollider>();
            collider.enabled = false;
            collider.transform.parent = boltPrefab.transform;

            Transform transform = new GameObject("BoltForeground", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) }).transform;
            transform.parent = boltPrefab.transform;

            boltPrefab.Setup();
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
            if (this.fire)
            {
                this.StopRolling();
            }
            this.fireCounter += this.t;
            if (this.releasedFire && this.fireCounter >= this.fireRate)
            {
                if (this.player != null && this.player.ValueOrchestrator != null)
                {
                    this.fireCounter -= this.player.ValueOrchestrator.GetModifiedFloatValue(ValueOrchestrator.ModifiableType.TimeBetweenShots, this.fireRate);
                }
                else
                {
                    this.fireCounter -= this.fireRate;
                }
                this.UseFire();
                this.FireFlashAvatar();
                this.SetGestureAnimation(GestureElement.Gestures.None);
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
            this.FireWeapon(base.X + num * 10f, base.Y + 8f, num * 300f, 0);
            //this.PlayAttackSound();
            Map.DisturbWildLife(base.X, base.Y, 60f, base.playerNum);
        }

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
            this.gunFrame = 3;
            this.SetGunSprite(this.gunFrame, 0);
            this.TriggerBroFireEvent();
            EffectsController.CreateMuzzleFlashEffect(x, y, -25f, xSpeed * 0.15f, ySpeed * 0.15f, base.transform);
            Bolt firedBolt = ProjectileController.SpawnProjectileLocally(boltPrefab, this, x, y, xSpeed, ySpeed, base.playerNum) as Bolt;
            firedBolt.enabled = true;
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
                    this.SetGunSprite(this.gunFrame, 0);
                }
            }
        }
        #endregion

        #region Melee
        #endregion

        #region Special
        #endregion
    }
}
