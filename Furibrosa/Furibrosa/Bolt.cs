using BroMakerLib;
using RocketLib;
using Rogueforce;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Furibrosa
{
    public class Bolt : PredabroSpear
    {
        public SpriteSM sprite;
        public bool explosive = false;
        public float explosiveTimer = 0;
        public float range = 48;
        public AudioClip[] explosionSounds;
        protected bool isStuckToUnit = false;
        protected Unit stuckToUnit = null;

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
                this.explosionSounds[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "explosion1.wav");
                this.explosionSounds[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "explosion2.wav");
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
                this.life = 0.46f;
            }
            this.unimpalementDamage = 8;
            this.trailDist = 12;
            this.stunOnHit = false;
            this.rotationSpeed = 0;
            this.dragUnitsSpeedM = 0.9f;
            this.projectileSize = 8;
            this.damage = this.damageInternal = this.fullDamage = Furibrosa.crossbowDamage;
            this.fadeDamage = false;
            this.damageType = DamageType.Bullet;
            this.canHitGrenades = true;
            this.affectScenery = true;
            this.horizontalProjectile = true;
            this.isWideProjectile = false;
            this.canReflect = true;
            this.canMakeEffectsMoreThanOnce = true;

            // Setup platform collider
            this.platformCollider = new GameObject("BoltCollider", new Type[] { typeof(Transform), typeof(BoxCollider) }).GetComponent<BoxCollider>();
            this.platformCollider.enabled = false;
            this.platformCollider.transform.parent = this.transform;
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
            GameObject foreground = new GameObject("BoltForeground", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) });
            foreground.transform.parent = this.transform;
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

        public override void Fire(float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy)
        {
            this.gameObject.SetActive(true);
            base.Fire(newX, newY, xI, yI, _zOffset, playerNum, FiredBy);
        }

        // Don't destroy projectile
        protected override void CheckSpawnPoint()
        {
            this.CheckWallsAtSpawnPoint();
            Map.DamageDoodads(this.damageInternal, this.damageType, base.X, base.Y, this.xI, this.yI, this.projectileSize, this.playerNum, out _, this);
            this.RegisterProjectile();
            this.CheckReturnZones();
            if ((this.canReflect && this.playerNum >= 0 && this.horizontalProjectile && Physics.Raycast(new Vector3(base.X - Mathf.Sign(this.xI) * this.projectileSize * 2f, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize * 3f, this.barrierLayer)) || (!this.horizontalProjectile && Physics.Raycast(new Vector3(base.X, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize + this.startProjectileSpeed * this.t, this.barrierLayer)))
            {
                this.ReflectProjectile(this.raycastHit);
            }
            else if ((this.canReflect && this.playerNum < 0 && this.horizontalProjectile && Physics.Raycast(new Vector3(base.X - Mathf.Sign(this.xI) * this.projectileSize * 2f, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize * 3f, this.friendlyBarrierLayer)) || (!this.horizontalProjectile && Physics.Raycast(new Vector3(base.X, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize + this.startProjectileSpeed * this.t, this.friendlyBarrierLayer)))
            {
                this.playerNum = 5;
                this.firedBy = null;
                this.ReflectProjectile(this.raycastHit);
            }
            else
            {
                this.TryHitUnitsAtSpawn();
            }
            this.CheckSpawnPointFragile();
        }

        // Don't destroy projectile
        protected override bool CheckWallsAtSpawnPoint()
        {
            Collider[] array = Physics.OverlapSphere(new Vector3(base.X, base.Y, 0f), 5f, this.groundLayer);
            bool flag = false;
            if (array.Length > 0)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    Collider y = null;
                    if (this.firedBy != null)
                    {
                        y = this.firedBy.GetComponent<Collider>();
                    }
                    if (this.firedBy == null || array[i] != y)
                    {
                        this.ProjectileApplyDamageToBlock(array[i].gameObject, this.damageInternal, this.damageType, this.xI, this.yI);
                        flag = true;
                    }
                }
            }
            return flag;
        }

        protected override void Update()
        {
            // Count down to explosion
            if ( this.stuckInPlace && this.explosive )
            {
                this.explosiveTimer += this.t;
                if ( this.explosiveTimer > 0.2f )
                {
                    this.Death();
                }
            }

            // Destroy bolt if it was stuck to a now dead or destroyed unit
            if ( this.isStuckToUnit && (this.stuckToUnit == null || this.stuckToUnit.health <= 0))
            {
                this.Death();
            }
            base.Update();
        }

        public void DestroyNearBySpears(Vector3 around)
        {
            Collider[] array = Physics.OverlapSphere(around, 9f, Map.unitLayer);
            foreach (Collider collider in array)
            {
                PredabroSpear componentInChildren = collider.GetComponentInChildren<PredabroSpear>();
                if (componentInChildren != null && componentInChildren != this)
                {
                    componentInChildren.Death();
                }
            }
        }

        public void StickToUnit(Transform trans, Vector3 point, Unit stuckUnit)
        {
            foreach (Unit unit in this.hitUnits)
            {
                unit.Damage(unit.health, DamageType.Spikes, this.xI, this.yI, (int)Mathf.Sign(this.xI), this.firedBy, base.X, base.Y);
            }
            this.stuckInPlace = true;
            this.superMachete = false;
            this.isStuckToUnit = true;
            this.stuckToUnit = stuckUnit;
            base.transform.parent = trans;
            base.X = point.x - Mathf.Sign(this.xI) * this.hitGroundOffset;
            this.SetPosition();
            this.life = float.PositiveInfinity;
            trans.SendMessage("AttachMe", base.transform);
            this.platformCollider.enabled = true;
            this.DestroyNearBySpears(point);
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
                        Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.specialSounds, 0.4f, base.transform.position, 1f, true, false, false, 0f);
                        if (!(firstUnit is DolphLundrenSoldier))
                        {
                            this.StickToUnit(firstUnit.transform, base.transform.position - new Vector3(7f * base.transform.localScale.x, 0f, 0f), firstUnit);
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
                        Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.specialSounds, 0.5f, base.transform.position, 1f, true, false, false, 0f);
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
            MapController.DamageGround(this.firedBy, ValueOrchestrator.GetModifiedDamage(this.damage, this.playerNum), DamageType.Explosion, this.range, base.X, base.Y, null, false);
            EffectsController.CreateExplosionRangePop(base.X, base.Y, -1f, this.range * 2f);
            Map.ExplodeUnits(this.firedBy, this.damage * 2, DamageType.Explosion, this.range * 1.3f, this.range, base.X, base.Y, 50f, 400f, this.playerNum, false, true, true);
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
