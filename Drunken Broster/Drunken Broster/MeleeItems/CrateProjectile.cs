using BroMakerLib.CustomObjects.Projectiles;
using UnityEngine;
using Utility;

namespace Drunken_Broster.MeleeItems
{
    public class CrateProjectile : CustomProjectile
    {
        public Shrapnel[] shrapnelPrefabs;
        protected bool madeEffects = false;

        protected override void Awake()
        {
            if ( !this.RanSetup )
            {
                this.SpriteWidth = 12f;
                this.SpriteHeight = 12f;
            }

            this.projectileSize = 12f;
            this.damage = 13;
            this.damageInternal = this.damage;
            this.fullDamage = this.damage;

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            // Load shrapnel from normal crate block
            shrapnelPrefabs = new Shrapnel[3];
            ThemeHolder jungleTheme = Map.Instance.jungleThemeReference;
            CrateBlock crateBlock = jungleTheme.blockPrefabWood[0] as CrateBlock;
            shrapnelPrefabs[0] = crateBlock.shrapnelPrefab;
            shrapnelPrefabs[1] = crateBlock.shrapnelBitPrefab;
            shrapnelPrefabs[2] = crateBlock.shrapnelBitPrefab3;

            // Store wood smash sounds
            this.soundHolder.deathSounds = crateBlock.soundHolder.deathSounds;
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

        protected override void HitUnits()
        {
            if ( this.reversing )
            {
                if ( Map.HitLivingUnits( this.firedBy ?? this, this.playerNum, this.damageInternal, this.damageType, this.projectileSize - 4f, this.projectileSize / 2f, base.X, base.Y, this.xI, 50f, false, false, true, false ) )
                {
                    this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                    global::UnityEngine.Object.Destroy( base.gameObject );
                    this.hasHit = true;
                    if ( this.giveDeflectAchievementOnMookKill )
                    {
                        AchievementManager.AwardAchievement( Achievement.bronald_bradman, this.playerNum );
                    }
                }
            }
            else if ( Map.HitUnits( this.firedBy, this.firedBy, this.playerNum, this.damageInternal, this.damageType, this.projectileSize - 4f, this.projectileSize / 2f, base.X, base.Y, this.xI, 50f, false, false, false, false ) )
            {
                this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                global::UnityEngine.Object.Destroy( base.gameObject );
                this.hasHit = true;
                if ( this.giveDeflectAchievementOnMookKill )
                {
                    AchievementManager.AwardAchievement( Achievement.bronald_bradman, this.playerNum );
                }
            }
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( this.madeEffects )
            {
                return;
            }
            this.madeEffects = true;

            this.PlayDeathSound();
            EffectsController.CreateShrapnel( this.shrapnelPrefabs[0], base.X, base.Y, 12f, 150f, 7f, 0f, 120f );
            EffectsController.CreateShrapnel( this.shrapnelPrefabs[1], base.X, base.Y, 12f, 150f, 8f, 0f, 120f );
            EffectsController.CreateShrapnel( this.shrapnelPrefabs[2], base.X, base.Y, 12f, 150f, 8f, 0f, 120f );
        }


    }
}
