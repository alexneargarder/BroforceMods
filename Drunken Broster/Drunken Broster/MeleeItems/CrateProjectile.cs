using BroMakerLib.CustomObjects.Projectiles;
using Rogueforce;
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
            this.damage = 16;
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

        public bool HitUnits( MonoBehaviour damageSender, int playerNum, int damage, int corpseDamage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, bool penetrates, bool knock, bool canGib, bool hitDead, MonoBehaviour avoidID = null )
        {
            if ( Map.units == null )
            {
                return false;
            }
            bool result = false;
            bool flag = false;
            int num = 999999;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                Unit unit = Map.units[i];
                if ( unit != null && ( avoidID == null || avoidID != unit ) && GameModeController.DoesPlayerNumDamage( playerNum, unit.playerNum ) && !unit.invulnerable && unit.health <= num && ( hitDead || unit.health > 0 ) )
                {
                    float f = unit.X - x;
                    if ( Mathf.Abs( f ) - xRange < unit.width )
                    {
                        float num2 = unit.Y + unit.height / 2f + 3f - y;
                        if ( Mathf.Abs( num2 ) - yRange < unit.height )
                        {
                            if ( !penetrates && unit.health > 0 )
                            {
                                num = 0;
                                flag = true;
                            }
                            if ( !canGib && unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                            }
                            else if ( unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( corpseDamage, playerNum ), damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                            }
                            else
                            {
                                damage = ValueOrchestrator.GetModifiedDamage( damage, playerNum );
                                // Don't allow instagibs
                                if ( damage > unit.health )
                                {
                                    damage = unit.health;
                                }
                                Map.KnockAndDamageUnit( damageSender, unit, damage, damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                            }
                            result = true;
                            if ( flag )
                            {
                                return result;
                            }
                        }
                    }
                }
            }
            return result;
        }

        protected override void HitUnits()
        {
            if ( this.reversing )
            {
                if ( HitUnits( this.firedBy ?? this, this.playerNum, this.damageInternal, this.damageInternal, this.damageType, this.projectileSize - 4f, this.projectileSize / 2f, base.X, base.Y, this.xI, 50f, false, false, false, false ) )
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
            else if ( HitUnits( this.firedBy, this.playerNum, this.damageInternal, this.damageInternal, this.damageType, this.projectileSize - 4f, this.projectileSize / 2f, base.X, base.Y, this.xI, 50f, false, false, false, true ) )
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

        // Don't apply double damage to units unlike base implementation
        protected override void TryHitUnitsAtSpawn()
        {
            if ( this.firedBy != null && this.firedBy.GetComponent<TestVanDammeAnim>() != null && this.firedBy.GetComponent<TestVanDammeAnim>().inseminatorUnit != null )
            {
                if ( HitUnits( this.firedBy, this.playerNum, this.damageInternal, this.damageInternal, this.damageType, ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ), this.projectileSize / 2f, base.X - ( ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ) ) * (float)( (int)Mathf.Sign( this.xI ) ), base.Y, this.xI, this.yI, false, false, false, false, this.firedBy.GetComponent<TestVanDammeAnim>().inseminatorUnit ) )
                {
                    this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                    global::UnityEngine.Object.Destroy( base.gameObject );
                    this.hasHit = true;
                }
            }
            else if ( HitUnits( this.firedBy, this.playerNum, this.damageInternal, this.damageInternal, this.damageType, ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ), this.projectileSize / 2f, base.X - ( ( this.playerNum < 0 ) ? 0f : ( this.projectileSize * 0.5f ) ) * (float)( (int)Mathf.Sign( this.xI ) ), base.Y, this.xI, this.yI, false, false, false, false ) )
            {
                this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                global::UnityEngine.Object.Destroy( base.gameObject );
                this.hasHit = true;
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
