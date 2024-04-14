using BroMakerLib;
using BroMakerLib.Loggers;
using RocketLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Furibrosa
{
    public class Bolt : PredabroSpear
    {
        public static Material storedMat;
        protected SpriteSM sprite;

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
            // Do nothing
        }

        public void Setup()
        {
            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (storedMat == null)
            {
                storedMat = ResourcesController.GetMaterial(directoryPath, "bolt.png");
            }

            renderer.material = storedMat;

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
            this.groundLayersCrushLeft = 1;
            this.maxPenetrations = 1;
            this.unimpalementDamage = 8;
            this.trailDist = 12;
            this.stunOnHit = false;
            this.rotationSpeed = 0;
            this.dragUnitsSpeedM = 0.9f;
            this.projectileSize = 8;
            this.damage = 5;
            this.damageInternal = 5;
            this.fullDamage = 5;
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

            // Setup foreground sprite
            GameObject foreground = this.FindChildOfName("BoltForeground").gameObject;
            foreground.GetComponent<MeshRenderer>().material = storedMat;
            SpriteSM foregroundSprite = foreground.GetComponent<SpriteSM>();
            foregroundSprite.lowerLeftPixel = new Vector2(14, 16);
            foregroundSprite.pixelDimensions = new Vector2(14, 16);
            foregroundSprite.plane = SpriteBase.SPRITE_PLANE.XY;
            foregroundSprite.width = 14;
            foregroundSprite.height = 16;
            foregroundSprite.offset = new Vector3(-1f, 0f, 0f);
            foreground.transform.localPosition = new Vector3(5f, 0f, -0.62f);
        }
    }
}
