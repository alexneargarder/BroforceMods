using BroMakerLib.CustomObjects.Projectiles;
using Rogueforce;
using UnityEngine;
using Utility;

namespace Drunken_Broster.MeleeItems
{
    public class CrateProjectile : CustomProjectile
    {
        public Shrapnel[] shrapnelPrefabs;
        protected bool madeEffects;

        protected override void Awake()
        {
            if ( !RanSetup )
            {
                SpriteWidth = 12f;
                SpriteHeight = 12f;
            }

            projectileSize = 12f;
            damage = 16;
            damageInternal = damage;
            fullDamage = damage;

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
            soundHolder.deathSounds = crateBlock.soundHolder.deathSounds;
        }

        protected override void SetRotation()
        {
            // Don't rotate based on momentum
            if ( xI > 0f )
            {
                transform.localScale = new Vector3( -1f, 1f, 1f );
            }
            else
            {
                transform.localScale = new Vector3( 1f, 1f, 1f );
            }

            transform.eulerAngles = new Vector3( 0f, 0f, 0f );
        }

        protected override void Update()
        {
            ApplyGravity();
            base.Update();
        }

        protected virtual void ApplyGravity()
        {
            yI -= 500f * t;
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
                                Map.KnockAndDamageUnit( damageSender, unit, 0, damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y );
                            }
                            else if ( unit.health <= 0 )
                            {
                                Map.KnockAndDamageUnit( damageSender, unit, ValueOrchestrator.GetModifiedDamage( corpseDamage, playerNum ), damageType, xI, 1.25f * yI, (int)Mathf.Sign( xI ), knock, x, y );
                            }
                            else
                            {
                                damage = ValueOrchestrator.GetModifiedDamage( damage, playerNum );
                                // Don't allow instagibs
                                if ( damage > unit.health )
                                {
                                    damage = unit.health;
                                }
                                Map.KnockAndDamageUnit( damageSender, unit, damage, damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y );
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
            if ( reversing )
            {
                if ( HitUnits( firedBy ?? this, playerNum, damageInternal, damageInternal, damageType, projectileSize - 4f, projectileSize / 2f, X, Y, xI, 50f, false, false, false, false ) )
                {
                    MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                    Destroy( gameObject );
                    hasHit = true;
                    if ( giveDeflectAchievementOnMookKill )
                    {
                        AchievementManager.AwardAchievement( Achievement.bronald_bradman, playerNum );
                    }
                }
            }
            else if ( HitUnits( firedBy, playerNum, damageInternal, damageInternal, damageType, projectileSize - 4f, projectileSize / 2f, X, Y, xI, 50f, false, false, false, true ) )
            {
                MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                Destroy( gameObject );
                hasHit = true;
                if ( giveDeflectAchievementOnMookKill )
                {
                    AchievementManager.AwardAchievement( Achievement.bronald_bradman, playerNum );
                }
            }
        }

        // Don't apply double damage to units unlike base implementation
        protected override void TryHitUnitsAtSpawn()
        {
            if ( firedBy != null && firedBy.GetComponent<TestVanDammeAnim>() != null && firedBy.GetComponent<TestVanDammeAnim>().inseminatorUnit != null )
            {
                if ( HitUnits( firedBy, playerNum, damageInternal, damageInternal, damageType, ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ), projectileSize / 2f, X - ( ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ) ) * (float)( (int)Mathf.Sign( xI ) ), Y, xI, yI, false, false, false, false, firedBy.GetComponent<TestVanDammeAnim>().inseminatorUnit ) )
                {
                    MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                    Destroy( gameObject );
                    hasHit = true;
                }
            }
            else if ( HitUnits( firedBy, playerNum, damageInternal, damageInternal, damageType, ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ), projectileSize / 2f, X - ( ( playerNum < 0 ) ? 0f : ( projectileSize * 0.5f ) ) * (float)( (int)Mathf.Sign( xI ) ), Y, xI, yI, false, false, false, false ) )
            {
                MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                Destroy( gameObject );
                hasHit = true;
            }
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( madeEffects )
            {
                return;
            }
            madeEffects = true;

            PlayDeathSound();
            EffectsController.CreateShrapnel( shrapnelPrefabs[0], X, Y, 12f, 150f, 7f, 0f, 120f );
            EffectsController.CreateShrapnel( shrapnelPrefabs[1], X, Y, 12f, 150f, 8f, 0f, 120f );
            EffectsController.CreateShrapnel( shrapnelPrefabs[2], X, Y, 12f, 150f, 8f, 0f, 120f );
        }


    }
}
