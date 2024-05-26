using BroMakerLib;
using BroMakerLib.Loggers;
using RocketLib;
using Rogueforce;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Utility;

namespace Furibrosa
{
    public class Bolt : PredabroSpear
    {
        public SpriteSM sprite;
        public bool explosive = false;
        public float explosiveTimer = 0;
        public float range = 48;
        public AudioClip[] explosionSounds;

        protected override void Awake()
        {
            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            this.canMakeEffectsMoreThanOnce = true;
            this.stunOnHit = false;
            this.groundLayer = (1 << LayerMask.NameToLayer("Ground") | 1 << LayerMask.NameToLayer("LargeObjects") | 1 << LayerMask.NameToLayer("FLUI"));
            this.barrierLayer = (1 << LayerMask.NameToLayer("MobileBarriers") | 1 << LayerMask.NameToLayer("IndestructibleGround"));
            this.friendlyBarrierLayer = 1 << LayerMask.NameToLayer("FriendlyBarriers");
            this.fragileLayer = 1 << LayerMask.NameToLayer("DirtyHippie");
            this.zOffset = (1f - UnityEngine.Random.value * 2f) * 0.04f;
            this.random = new Randomf(UnityEngine.Random.Range(0, 10000));

            if ( this.explosive )
            {
                this.explosionSounds = new AudioClip[2];
                this.explosionSounds[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "Explo_Grenade1.wav");
                this.explosionSounds[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "Explo_Grenade2.wav");
            }
        }

        private void OnDisable()
        {
            if ( this.stuckInPlace )
            {
                this.Death();
            }
        }

        public void Setup(bool isExplosive)
        {
            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Material material;
            if (isExplosive)
            {
                material = ResourcesController.GetMaterial(directoryPath, "boltExplosive.png");
            }
            else
            {
                material = ResourcesController.GetMaterial(directoryPath, "bolt.png");
            }

            renderer.material = material;

            this.sprite = this.gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2(-3, 16);
            sprite.pixelDimensions = new Vector2(17, 16);

            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 17;
            sprite.height = 16;
            sprite.offset = new Vector3(-10.5f, 0f, 10.82f);

            // Setup collider
            BoxCollider collider = this.gameObject.GetComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.5f, 8f);
            collider.size = new Vector3(26f, 2f, 0f);

            // Setup Variables
            this.explosive = isExplosive;
            if ( this.explosive )
            {
                this.groundLayersCrushLeft = 1;
                this.maxPenetrations = 2;
                this.life = 0.42f;
            }
            else
            {
                this.groundLayersCrushLeft = 1;
                this.maxPenetrations = 2;
                this.life = 0.44f;
            }
            this.unimpalementDamage = 8;
            this.trailDist = 12;
            this.stunOnHit = false;
            this.rotationSpeed = 0;
            this.dragUnitsSpeedM = 0.9f;
            this.projectileSize = 8;
            this.damage = 8;
            this.damageInternal = 8;
            this.fullDamage = 8;
            this.fadeDamage = false;
            this.damageType = DamageType.Bullet;
            this.canHitGrenades = true;
            this.affectScenery = true;
            this.horizontalProjectile = true;
            this.isWideProjectile = false;
            this.canReflect = true;
            this.canMakeEffectsMoreThanOnce = true;

            // Setup platform collider
            this.platformCollider = this.FindChildOfName("BoltCollider").gameObject.GetComponent<BoxCollider>();
            this.platformCollider.material = new PhysicMaterial();
            this.platformCollider.material.dynamicFriction = 0.6f;
            this.platformCollider.material.staticFriction = 0.6f;
            this.platformCollider.material.bounciness = 0f;
            this.platformCollider.material.frictionCombine = PhysicMaterialCombine.Average;
            this.platformCollider.material.bounceCombine = PhysicMaterialCombine.Average;
            this.platformCollider.gameObject.layer = 15;
            (this.platformCollider as BoxCollider).size = new Vector3(11f, 1f, 1f);

            // Setup foreground sprite
            GameObject foreground = this.FindChildOfName("BoltForeground").gameObject;
            foreground.GetComponent<MeshRenderer>().material = material;
            SpriteSM foregroundSprite = foreground.GetComponent<SpriteSM>();
            foregroundSprite.lowerLeftPixel = new Vector2(14, 16);
            foregroundSprite.pixelDimensions = new Vector2(14, 16);
            foregroundSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            foregroundSprite.width = 14;
            foregroundSprite.height = 16;
            foregroundSprite.offset = new Vector3(-1f, 0f, 0f);
            foreground.transform.localPosition = new Vector3(5f, 0f, -0.62f);
        }

        protected override void CheckSpawnPoint()
        {
            
        }

        protected override void Update()
        {
            if ( this.stuckInPlace && this.explosive )
            {
                this.explosiveTimer += this.t;
                if ( this.explosiveTimer > 0.2f )
                {
                    this.Death();
                }
            }
            base.Update();
        }

        protected override void HitUnits()
        {
            if (this.penetrationsCount < this.maxPenetrations)
            {
                Unit firstUnit = Map.GetFirstUnit(this.firedBy, this.playerNum, 5f, base.X, base.Y, true, true, this.hitUnits);
                if (firstUnit != null)
                {
                    if (firstUnit.IsHeavy())
                    {
                        firstUnit.Damage(this.damageInternal, DamageType.Melee, this.xI, this.yI, (int)Mathf.Sign(this.xI), this.firedBy, base.X, base.Y);
                        this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
                        if (!(firstUnit is DolphLundrenSoldier))
                        {
                            this.Stick(firstUnit.transform, base.transform.position);
                        }
                        else
                        {
                            UnityEngine.Object.Destroy(base.gameObject);
                        }
                    }
                    else
                    {
                        this.hitUnits.Add(firstUnit);
                        if (this.stunOnHit)
                        {
                            firstUnit.Blind();
                        }
                        firstUnit.Impale(base.transform, Vector3.right * Mathf.Sign(this.xI), this.damageInternal, this.xI, this.yI, 0f, 0f);
                        firstUnit.Y = base.Y - 8f;
                        if (firstUnit is Mook)
                        {
                            firstUnit.useImpaledFrames = true;
                        }
                        Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.specialSounds, 0.4f, base.transform.position, 1f, true, false, false, 0f);
                        this.penetrationsCount++;
                        // Only add life to non-explosive bolts
                        if ( !this.explosive )
                        {
                            this.life += 0.15f;
                        }
                        SortOfFollow.Shake(0.3f);
                        EffectsController.CreateBloodParticles(firstUnit.bloodColor, base.X, base.Y, 4, 4f, 5f, 60f, this.xI * 0.2f, this.yI * 0.5f + 40f);
                        EffectsController.CreateMeleeStrikeEffect(base.X, base.Y, this.xI * 0.2f, this.yI * 0.5f + 24f);
                        if (this.xI > 0f)
                        {
                            if (firstUnit.X < base.X + 3f)
                            {
                                firstUnit.X = base.X + 3f;
                            }
                        }
                        else if (this.xI < 0f && firstUnit.X > base.X - 3f)
                        {
                            firstUnit.X = base.X - 3f;
                        }
                    }
                }
            }
        }

        protected override void MakeEffects(bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint)
        {
            if ( this.explosive )
            {
                this.Explode();
            }
            else
            {
                base.MakeEffects(particles, x, y, useRayCast, hitNormal, hitPoint);
            }
        }

        protected void Explode()
        {
            MapController.DamageGround(this.firedBy, ValueOrchestrator.GetModifiedDamage(this.damage, this.playerNum), this.damageType, this.range, base.X, base.Y, null, false);
            EffectsController.CreateExplosionRangePop(base.X, base.Y, -1f, this.range * 2f);
            Map.ExplodeUnits(this.firedBy, this.damage * 3, this.damageType, this.range * 1.3f, this.range, base.X, base.Y, 50f, 400f, this.playerNum, false, true, true);
            EffectsController.CreateExplosionRangePop(base.X, base.Y, -1f, this.range * 2f);
            EffectsController.CreateExplosion(base.X, base.Y, this.range * 0.25f, this.range * 0.25f, 120f, 1f, this.range * 1.5f, 1f, 0f, true);
            if (this.sound == null)
            {
                this.sound = Sound.GetInstance();
            }
            if (this.sound != null)
            {
                this.sound.PlaySoundEffectAt(this.explosionSounds, 0.7f, base.transform.position, 1f, true, false, false, 0f);
            }
            bool flag;
            Map.DamageDoodads(this.damage, DamageType.Explosion, base.X, base.Y, 0f, 0f, this.range, this.playerNum, out flag, null);
        }
    }
}
