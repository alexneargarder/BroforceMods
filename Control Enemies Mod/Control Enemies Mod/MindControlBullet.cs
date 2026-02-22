using UnityEngine;

namespace Control_Enemies_Mod
{
    public class MindControlBullet : PlasmaBullet
    {
        public void Setup( EllenRipbro ellenRipbro )
        {
            // Assign values from plasma bullet
            PlasmaBullet otherProjectile = ellenRipbro.projectile as PlasmaBullet;
            soundHolder = otherProjectile.soundHolder;
            sparkWhite1 = otherProjectile.sparkWhite1;
            blastForce = otherProjectile.blastForce;
            fire1 = otherProjectile.fire1;
            fire2 = otherProjectile.fire2;
            fire3 = otherProjectile.fire3;
            smoke1 = otherProjectile.smoke1;
            smoke2 = otherProjectile.smoke2;
            explosion = otherProjectile.explosion;
            range = otherProjectile.range;
            sparkBlue1 = otherProjectile.sparkBlue1;
            sparkBlue2 = otherProjectile.sparkBlue2;
            beamPuff = otherProjectile.beamPuff;
            ballPuff = otherProjectile.ballPuff;
            wobblePuff = otherProjectile.wobblePuff;
            puffLife = 0.16f;
            z = otherProjectile.z;
            shrapnel = otherProjectile.shrapnel;
            shrapnelSpark = otherProjectile.shrapnelSpark;
            flickPuff = otherProjectile.flickPuff;
            life = otherProjectile.life;
            projectileSize = otherProjectile.projectileSize;
            fullLife = 1;
            fadeDamage = otherProjectile.fadeDamage;
            damageType = otherProjectile.damageType;
            playerNum = otherProjectile.playerNum;
            soundHolder = otherProjectile.soundHolder;
            canHitGrenades = otherProjectile.canHitGrenades;
            affectScenery = otherProjectile.affectScenery;
            soundVolume = otherProjectile.soundVolume;
            firedBy = otherProjectile.firedBy;
            seed = otherProjectile.seed;
            random = new Randomf( Random.Range( 0, 10000 ) );
            sparkCount = otherProjectile.sparkCount;
            isDamageable = otherProjectile.isDamageable;
            horizontalProjectile = otherProjectile.horizontalProjectile;
            isWideProjectile = otherProjectile.isWideProjectile;
            zOffset = ( 1f - Random.value * 2f ) * 0.04f;
            canReflect = otherProjectile.canReflect;
            startProjectileSpeed = 400f;
            heldDelay = 0;
            canMakeEffectsMoreThanOnce = false;
            whitePopEffect = otherProjectile.whitePopEffect;
            doubleSpeed = otherProjectile.doubleSpeed;
            giveDeflectAchievementOnMookKill = otherProjectile.giveDeflectAchievementOnMookKill;
            health = 3;
            maxHealth = -1;
        }

        public override void Fire( float x, float y, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            gameObject.SetActive( true );
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
            Unit unit = null;
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
                                    if ( facehugger != null && ( bool )facehugger.GetFieldValue( "connectedToFace" ) && facehugger.inseminatedUnit == firedBy )
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
                Map.KnockAndDamageUnit( damageSender, unit, damage, damageType, xI, yI, ( int )Mathf.Sign( xI ), knock, x, y );
                return unit;
            }

            return null;
        }

        protected override void TryHitUnitsAtSpawn()
        {
            Unit hitUnit = HitClosestUnit( this, playerNum, 0, damageType, projectileSize, projectileSize / 2f, X, Y, xI, yI, false, false, true, false );
            if ( hitUnit != null )
            {
                hasHit = true;
                hasHitUnit = true;
                MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                if ( hitUnit is TestVanDammeAnim anim )
                {
                    Main.StartControllingUnit( playerNum, anim );
                }

                Destroy( gameObject );
            }
        }

        protected override void HitUnits()
        {
            float xI = this.xI;
            this.xI *= 0.3333334f;
            Unit hitUnit = HitClosestUnit( this, playerNum, 0, damageType, projectileSize, projectileSize / 2f, X, Y, this.xI, yI, false, false, true, false );
            //if (Map.HitLivingUnits(this, this.playerNum, 0, this.damageType, this.projectileSize, this.projectileSize / 2f, base.X, base.Y, this.xI, this.yI, false, false, true, false))
            if ( hitUnit != null )
            {
                hasHitUnit = true;
                MakeEffects( false, X, Y, false, raycastHit.normal, raycastHit.point );
                Destroy( gameObject );
                Sound.GetInstance().PlaySoundEffectAt( soundHolder.hitSounds, 0.5f, transform.position, 0.95f + Random.value * 0.15f );
                if ( hitUnit is TestVanDammeAnim anim )
                {
                    Main.StartControllingUnit( playerNum, anim );
                }
            }

            this.xI = xI;
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 point )
        {
            if ( !hasHitUnit )
            {
                Unit hitUnit = HitClosestUnit( this, playerNum, 0, damageType, 16f, 16f, X, Y, 0f, 0f, false, false, true, false );
                if ( hitUnit != null && hitUnit is TestVanDammeAnim anim )
                {
                    Main.StartControllingUnit( playerNum, anim );
                }
            }

            EffectsController.CreateShrapnel( sparkWhite1, x, y, 2f, 130f, 8f, xI * 0.2f, 50f );
            hasHitUnit = true;
            EffectsController.CreateWhiteFlashPopSmall( x, y );
        }
    }
}