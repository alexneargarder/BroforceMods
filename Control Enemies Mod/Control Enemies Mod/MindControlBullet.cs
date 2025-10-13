using UnityEngine;

namespace Control_Enemies_Mod
{
    public class MindControlBullet : PlasmaBullet
    {
        public void Setup( EllenRipbro ellenRipbro )
        {
            // Assign values from plasma bullet
            PlasmaBullet otherProjectile = ellenRipbro.projectile as PlasmaBullet;
            this.soundHolder = otherProjectile.soundHolder;
            this.sparkWhite1 = otherProjectile.sparkWhite1;
            this.blastForce = otherProjectile.blastForce;
            this.fire1 = otherProjectile.fire1;
            this.fire2 = otherProjectile.fire2;
            this.fire3 = otherProjectile.fire3;
            this.smoke1 = otherProjectile.smoke1;
            this.smoke2 = otherProjectile.smoke2;
            this.explosion = otherProjectile.explosion;
            this.range = otherProjectile.range;
            this.sparkBlue1 = otherProjectile.sparkBlue1;
            this.sparkBlue2 = otherProjectile.sparkBlue2;
            this.beamPuff = otherProjectile.beamPuff;
            this.ballPuff = otherProjectile.ballPuff;
            this.wobblePuff = otherProjectile.wobblePuff;
            this.puffLife = 0.16f;
            this.z = otherProjectile.z;
            this.shrapnel = otherProjectile.shrapnel;
            this.shrapnelSpark = otherProjectile.shrapnelSpark;
            this.flickPuff = otherProjectile.flickPuff;
            this.life = otherProjectile.life;
            this.projectileSize = otherProjectile.projectileSize;
            this.fullLife = 1;
            this.fadeDamage = otherProjectile.fadeDamage;
            this.damageType = otherProjectile.damageType;
            this.playerNum = otherProjectile.playerNum;
            this.soundHolder = otherProjectile.soundHolder;
            this.canHitGrenades = otherProjectile.canHitGrenades;
            this.affectScenery = otherProjectile.affectScenery;
            this.soundVolume = otherProjectile.soundVolume;
            this.firedBy = otherProjectile.firedBy;
            this.seed = otherProjectile.seed;
            this.random = new Randomf( UnityEngine.Random.Range( 0, 10000 ) );
            this.sparkCount = otherProjectile.sparkCount;
            this.isDamageable = otherProjectile.isDamageable;
            this.horizontalProjectile = otherProjectile.horizontalProjectile;
            this.isWideProjectile = otherProjectile.isWideProjectile;
            this.zOffset = ( 1f - UnityEngine.Random.value * 2f ) * 0.04f;
            this.canReflect = otherProjectile.canReflect;
            this.startProjectileSpeed = 400f;
            this.heldDelay = 0;
            this.canMakeEffectsMoreThanOnce = false;
            this.whitePopEffect = otherProjectile.whitePopEffect;
            this.doubleSpeed = otherProjectile.doubleSpeed;
            this.giveDeflectAchievementOnMookKill = otherProjectile.giveDeflectAchievementOnMookKill;
            this.health = 3;
            this.maxHealth = -1;
        }

        public override void Fire( float x, float y, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            this.gameObject.SetActive( true );
            base.Fire( x, y, xI, yI, _zOffset, playerNum, FiredBy );
        }

        public Unit HitClosestUnit( MonoBehaviour damageSender, int playerNum, int damage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, bool knock, bool canGib, bool firedLocally, bool checkIfUnitIsLocallyOwned )
        {
            if ( Map.units == null )
            {
                return null;
            }
            int num = 999999;
            float num2 = Mathf.Max( xRange, yRange );
            float num3 = Mathf.Max( xRange, yRange );
            Unit unit = null;
            Unit unit2 = null;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                Unit unit3 = Map.units[i];
                if ( unit3 != null && !unit3.invulnerable && unit3.health <= num && GameModeController.DoesPlayerNumDamage( playerNum, unit3.playerNum ) && unit3 is TestVanDammeAnim )
                {
                    float f = unit3.X - x;
                    if ( Mathf.Abs( f ) - xRange < unit3.width )
                    {
                        float f2 = unit3.Y + unit3.height / 2f + 3f - y;
                        if ( Mathf.Abs( f2 ) - yRange < unit3.height )
                        {
                            float num4 = Mathf.Abs( f ) + Mathf.Abs( f2 );
                            if ( num4 < num2 )
                            {
                                if ( unit3.health > 0 )
                                {
                                    // Check if unit is a facehugger that is facehugging our current unit
                                    AlienFaceHugger facehugger = unit3 as AlienFaceHugger;
                                    if ( facehugger != null && (bool)facehugger.GetFieldValue( "connectedToFace" ) && facehugger.inseminatedUnit == this.firedBy )
                                    {
                                        // Skip this facehugger since they're facehugging us
                                        continue;
                                    }
                                    unit = unit3;
                                    num2 = num4;
                                }
                            }
                        }
                    }
                }
            }
            Vector3 vector = new Vector3( x, y + 5f, 0f );
            if ( unit != null && !Physics.Raycast( vector, unit.transform.position - vector, ( unit.transform.position - vector ).magnitude * 0.8f, Map.groundLayer ) )
            {
                Map.KnockAndDamageUnit( damageSender, unit, damage, damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                return unit;
            }
            if ( unit2 != null && !Physics.Raycast( vector, unit2.transform.position - vector, ( unit2.transform.position - vector ).magnitude * 0.8f, Map.groundLayer ) )
            {
                if ( !canGib )
                {
                    damage = 0;
                }
                Map.KnockAndDamageUnit( damageSender, unit2, damage, damageType, xI, yI, (int)Mathf.Sign( xI ), knock, x, y, false );
                return unit2;
            }
            return null;
        }

        protected override void TryHitUnitsAtSpawn()
        {
            Unit hitUnit = HitClosestUnit( this, this.playerNum, 0, this.damageType, this.projectileSize, this.projectileSize / 2f, base.X, base.Y, this.xI, this.yI, false, false, true, false );
            if ( hitUnit != null )
            {
                this.hasHit = true;
                this.hasHitUnit = true;
                this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                if ( hitUnit is TestVanDammeAnim )
                {
                    Main.StartControllingUnit( playerNum, hitUnit as TestVanDammeAnim );
                }
                UnityEngine.Object.Destroy( base.gameObject );
            }
        }

        protected override void HitUnits()
        {
            float xI = this.xI;
            this.xI *= 0.3333334f;
            Unit hitUnit = HitClosestUnit( this, this.playerNum, 0, this.damageType, this.projectileSize, this.projectileSize / 2f, base.X, base.Y, this.xI, this.yI, false, false, true, false );
            //if (Map.HitLivingUnits(this, this.playerNum, 0, this.damageType, this.projectileSize, this.projectileSize / 2f, base.X, base.Y, this.xI, this.yI, false, false, true, false))
            if ( hitUnit != null )
            {
                this.hasHitUnit = true;
                this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                UnityEngine.Object.Destroy( base.gameObject );
                Sound.GetInstance().PlaySoundEffectAt( this.soundHolder.hitSounds, 0.5f, base.transform.position, 0.95f + UnityEngine.Random.value * 0.15f, true, false, false, 0f );
                if ( hitUnit is TestVanDammeAnim )
                {
                    Main.StartControllingUnit( playerNum, hitUnit as TestVanDammeAnim );
                }
            }
            this.xI = xI;
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 point )
        {
            if ( !this.hasHitUnit )
            {
                Unit hitUnit = HitClosestUnit( this, this.playerNum, 0, this.damageType, 16f, 16f, base.X, base.Y, 0f, 0f, false, false, true, false );
                if ( hitUnit != null && hitUnit is TestVanDammeAnim )
                {
                    Main.StartControllingUnit( playerNum, hitUnit as TestVanDammeAnim );
                }
            }
            EffectsController.CreateShrapnel( this.sparkWhite1, x, y, 2f, 130f, 8f, this.xI * 0.2f, 50f );
            this.hasHitUnit = true;
            EffectsController.CreateWhiteFlashPopSmall( x, y );
        }

    }
}
