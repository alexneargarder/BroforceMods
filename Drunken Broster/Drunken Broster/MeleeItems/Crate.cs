using BroMakerLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Windows.Speech;

namespace Drunken_Broster.MeleeItems
{
    class CrateProjectile : Projectile
    {
        public static Material storedMat;
        public SpriteSM storedSprite;
        protected Shrapnel[] shrapnelPrefabs = new Shrapnel[3];

        protected override void Awake()
        {
            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            directoryPath = Path.Combine( directoryPath, "projectiles" );
            storedMat = ResourcesController.GetMaterial( directoryPath, "Crate.png" );
            
            renderer.material = storedMat;

            SpriteSM sprite = this.gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2( 0, 16 );
            sprite.pixelDimensions = new Vector2( 16, 16 );

            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 16;
            sprite.height = 16;
            sprite.offset = new Vector3( 0, 0, 0 );

            storedSprite = sprite;

            // Load shrapnel from normal crate block
            CrateBlock crateBlock = Map.Instance.activeTheme.blockPrefabWood[0] as CrateBlock;
            shrapnelPrefabs[0] = crateBlock.shrapnelPrefab;
            shrapnelPrefabs[1] = crateBlock.shrapnelBitPrefab;
            shrapnelPrefabs[2] = crateBlock.shrapnelBitPrefab3;

            this.projectileSize = 15f;

            this.damage = 7;
            this.damageInternal = this.damage;
            this.fullDamage = this.damage;

            base.Awake();
        }

        protected override void SetRotation()
        {
            // Don't rotate based on momentum
            if ( this.xI > 0f )
            {
                base.transform.localScale = new Vector3( -1f, 1f, 1f );
            }
            else
            {
                base.transform.localScale = new Vector3( 1f, 1f, 1f );
            }

            base.transform.eulerAngles = new Vector3( 0f, 0f, 0f );
        }

        protected override void Update()
        {
            this.ApplyGravity();
            base.Update();

            Vector3 middle = base.transform.position;
            Vector3 right = base.transform.position + new Vector3(this.projectileSize, 0, 0);
            Vector3 left = base.transform.position - new Vector3( this.projectileSize, 0, 0 );
        }

        protected virtual void ApplyGravity()
        {
            this.yI -= 450f * this.t;
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            EffectsController.CreateShrapnel( this.shrapnelPrefabs[0], base.X, base.Y, 12f, 150f, 7f, 0f, 120f );
            EffectsController.CreateShrapnel( this.shrapnelPrefabs[1], base.X, base.Y, 12f, 150f, 8f, 0f, 120f );
            EffectsController.CreateShrapnel( this.shrapnelPrefabs[2], base.X, base.Y, 12f, 150f, 8f, 0f, 120f );
        }


    }
}
