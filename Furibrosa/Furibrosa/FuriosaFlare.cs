using UnityEngine;

namespace Furibrosa
{
    public class FuriosaFlare : Flare
    {
        public void Setup( Flare otherFlare )
        {
            // Copied values
            blastForce = otherFlare.blastForce;
            ;
            hitUnitsSize = otherFlare.hitUnitsSize;
            fire1 = otherFlare.fire1;
            fire2 = otherFlare.fire2;
            fire3 = otherFlare.fire3;
            smoke1 = otherFlare.smoke1;
            smoke2 = otherFlare.smoke2;
            smoke3 = otherFlare.smoke3;
            explosion = otherFlare.explosion;
            trailType = otherFlare.trailType;
            z = otherFlare.z;
            shrapnel = otherFlare.shrapnel;
            shrapnelSpark = otherFlare.shrapnelSpark;
            flickPuff = otherFlare.flickPuff;
            life = otherFlare.life;
            projectileSize = otherFlare.projectileSize;
            fullLife = 1;
            fadeDamage = otherFlare.fadeDamage;
            damageType = otherFlare.damageType;
            playerNum = otherFlare.playerNum;
            soundHolder = otherFlare.soundHolder;
            canHitGrenades = otherFlare.canHitGrenades;
            affectScenery = otherFlare.affectScenery;
            soundVolume = otherFlare.soundVolume;
            firedBy = otherFlare.firedBy;
            seed = otherFlare.seed;
            random = new Randomf( Random.Range( 0, 10000 ) );
            sparkCount = otherFlare.sparkCount;
            isDamageable = otherFlare.isDamageable;
            horizontalProjectile = otherFlare.horizontalProjectile;
            isWideProjectile = otherFlare.isWideProjectile;
            zOffset = ( 1f - Random.value * 2f ) * 0.04f;
            canReflect = otherFlare.canReflect;
            startProjectileSpeed = 400f;
            heldDelay = 0;
            canMakeEffectsMoreThanOnce = false;
            whitePopEffect = otherFlare.whitePopEffect;
            doubleSpeed = otherFlare.doubleSpeed;
            giveDeflectAchievementOnMookKill = otherFlare.giveDeflectAchievementOnMookKill;
            health = 3;
            maxHealth = -1;

            Destroy( otherFlare );

            damage = damageInternal = fullDamage = Furibrosa.flaregunDamage;
            range = 9f;
        }

        public override void Fire( float x, float y, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            gameObject.SetActive( true );
            base.Fire( x, y, xI, yI, _zOffset, playerNum, FiredBy );
        }
    }
}