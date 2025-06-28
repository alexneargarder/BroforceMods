using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class CrateProjectile : CustomProjectile
    {
        public Shrapnel[] shrapnelPrefabs;

        protected override void Awake()
        {
            // Load shrapnel from normal crate block
            if ( shrapnelPrefabs == null )
            {
                shrapnelPrefabs = new Shrapnel[3];
                CrateBlock crateBlock = Map.Instance.activeTheme.blockPrefabWood[0] as CrateBlock;
                shrapnelPrefabs[0] = crateBlock.shrapnelPrefab;
                shrapnelPrefabs[1] = crateBlock.shrapnelBitPrefab;
                shrapnelPrefabs[2] = crateBlock.shrapnelBitPrefab3;
            }

            this.spriteWidth = 12f;
            this.spriteHeight = 12f;

            this.projectileSize = 12f;

            this.damage = 9;
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
        }

        protected virtual void ApplyGravity()
        {
            this.yI -= 500f * this.t;
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            EffectsController.CreateShrapnel( this.shrapnelPrefabs[0], base.X, base.Y, 12f, 150f, 7f, 0f, 120f );
            EffectsController.CreateShrapnel( this.shrapnelPrefabs[1], base.X, base.Y, 12f, 150f, 8f, 0f, 120f );
            EffectsController.CreateShrapnel( this.shrapnelPrefabs[2], base.X, base.Y, 12f, 150f, 8f, 0f, 120f );
        }


    }
}
