using BroMakerLib;
using RocketLib;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Furibrosa
{
    public class Harpoon : PredabroSpear
    {
        public SpriteSM sprite;

        protected override void Awake()
        {
            this.canMakeEffectsMoreThanOnce = true;
            this.stunOnHit = false;
            this.groundLayer = (1 << LayerMask.NameToLayer("Ground") | 1 << LayerMask.NameToLayer("LargeObjects") | 1 << LayerMask.NameToLayer("FLUI"));
            this.barrierLayer = (1 << LayerMask.NameToLayer("MobileBarriers") | 1 << LayerMask.NameToLayer("IndestructibleGround"));
            this.friendlyBarrierLayer = 1 << LayerMask.NameToLayer("FriendlyBarriers");
            this.fragileLayer = 1 << LayerMask.NameToLayer("DirtyHippie");
            this.zOffset = (1f - UnityEngine.Random.value * 2f) * 0.04f;
            this.random = new Randomf(UnityEngine.Random.Range(0, 10000));
        }

        private void OnDisable()
        {
            if (this.stuckInPlace)
            {
                this.Death();
            }
        }

        public void Setup()
        {
            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Material material = ResourcesController.GetMaterial(directoryPath, "harpoon.png");

            renderer.material = material;

            this.sprite = this.gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2(-3, 10);
            sprite.pixelDimensions = new Vector2(17, 3);

            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 17;
            sprite.height = 5;
            sprite.offset = new Vector3(-10.5f, 0f, 10.82f);

            // Setup collider
            BoxCollider collider = this.gameObject.GetComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.5f, 8f);
            collider.size = new Vector3(26f, 2f, 0f);

            // Setup Variables
            this.groundLayersCrushLeft = 5;
            this.maxPenetrations = 8;
            this.life = 0.44f;
            this.unimpalementDamage = 8;
            this.trailDist = 12;
            this.stunOnHit = false;
            this.rotationSpeed = 0;
            this.dragUnitsSpeedM = 0.9f;
            this.projectileSize = 14;
            this.damage = 30;
            this.damageInternal = 30;
            this.fullDamage = 30;
            this.fadeDamage = false;
            this.damageType = DamageType.Bullet;
            this.canHitGrenades = true;
            this.affectScenery = true;
            this.horizontalProjectile = true;
            this.isWideProjectile = false;
            this.canReflect = true;
            this.canMakeEffectsMoreThanOnce = true;

            // Setup platform collider
            this.platformCollider = new GameObject("HarpoonCollider", new Type[] { typeof(BoxCollider) }).GetComponent<BoxCollider>();
            this.platformCollider.enabled = false;
            this.platformCollider.transform.parent = this.transform;
            this.platformCollider.material = new PhysicMaterial();
            this.platformCollider.material.dynamicFriction = 0.6f;
            this.platformCollider.material.staticFriction = 0.6f;
            this.platformCollider.material.bounciness = 0f;
            this.platformCollider.material.frictionCombine = PhysicMaterialCombine.Average;
            this.platformCollider.material.bounceCombine = PhysicMaterialCombine.Average;
            this.platformCollider.gameObject.layer = 15;
            (this.platformCollider as BoxCollider).center = new Vector3(0f, 2f, 0f);
            (this.platformCollider as BoxCollider).size = new Vector3(20f, 1f, 1f);

            // Setup foreground sprite
            GameObject foreground = new GameObject("HarpoonForeground", new Type[] { typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) });
            foreground.transform.parent = this.transform;
            foreground.GetComponent<MeshRenderer>().material = material;
            SpriteSM foregroundSprite = foreground.GetComponent<SpriteSM>();
            foregroundSprite.lowerLeftPixel = new Vector2(14, 10);
            foregroundSprite.pixelDimensions = new Vector2(20, 3);
            foregroundSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            foregroundSprite.width = 20;
            foregroundSprite.height = 5;
            foregroundSprite.offset = new Vector3(-1f, 0f, 0f);
            foreground.transform.localPosition = new Vector3(9f, 0f, -0.62f);
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
            base.Update();
        }

        protected override void HitUnits()
        {
            if (this.penetrationsCount < this.maxPenetrations)
            {
                Unit firstUnit = Map.GetFirstUnit(this.firedBy, this.playerNum, 10f, base.X, base.Y, true, true, this.hitUnits);
                if (firstUnit != null)
                {
                    if (firstUnit.IsHeavy())
                    {
                        this.hitUnits.Add(firstUnit);
                        firstUnit.Damage(this.damageInternal, DamageType.Melee, this.xI, this.yI, (int)Mathf.Sign(this.xI), this.firedBy, base.X, base.Y);
                        this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
                        Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.specialSounds, 0.4f, base.transform.position, 1f, true, false, false, 0f);
                        // Don't pierce through bosses
                        if ( BroMakerUtilities.IsBoss(firstUnit))
                        {
                            this.Death();
                        }
                    }
                    else
                    {
                        this.hitUnits.Add(firstUnit);
                        if (this.stunOnHit)
                        {
                            firstUnit.Blind();
                        }
                        firstUnit.Impale(base.transform, Vector3.right * Mathf.Sign(this.xI), this.damageInternal, this.xI, this.yI, 0f, 1f);
                        firstUnit.Y = base.Y - 8f;
                        if (firstUnit is Mook)
                        {
                            firstUnit.useImpaledFrames = true;
                        }
                        Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.specialSounds, 0.5f, base.transform.position, 1f, true, false, false, 0f);
                        this.penetrationsCount++;
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

        protected override void Bounce(RaycastHit raycastHit)
        {
            // Ensure we don't keep applying damage to boss blocks
            if ( this.groundLayersCrushLeft > 0 && raycastHit.collider.gameObject.HasComponent<DamageRelay>() )
            {
                this.groundLayersCrushLeft = 1;
                this.damage = 45;
            }
            base.Bounce(raycastHit);
            this.damage = 30;
        }

        protected override void MakeEffects(bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint)
        {
            if (this.hasMadeEffects && !this.canMakeEffectsMoreThanOnce)
            {
                return;
            }
            if (useRayCast)
            {
                if (particles)
                {
                    EffectsController.CreateSparkShower(hitPoint.x + hitNormal.x * 3f, hitPoint.y + hitNormal.y * 3f, this.sparkCount, 2f, 60f, hitNormal.x * 60f, hitNormal.y * 30f, 0.2f, 0f);
                }
                EffectsController.CreateProjectilePopEffect(hitPoint.x + hitNormal.x * 3f, hitPoint.y + hitNormal.y * 3f);
            }
            else
            {
                if (particles)
                {
                    EffectsController.CreateSparkShower(x, y, 10, 2f, 60f, -this.xI * 0.2f, 35f, 0.2f, 0f);
                    EffectsController.CreateShrapnel(this.shrapnel, x, y, 5f, 20f, 6f, 0f, 0f);
                }
                EffectsController.CreateProjectilePopEffect(x, y);
            }
            if (!particles)
            {
                bool flag;
                Map.DamageDoodads(this.damageInternal, this.damageType, x, y, this.xI, this.yI, 8f, this.playerNum, out flag, this);
            }
            this.hasMadeEffects = true;
        }
    }
}
