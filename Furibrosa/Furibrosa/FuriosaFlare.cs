using BroMakerLib.Loggers;
using UnityEngine;

namespace Furibrosa
{
    public class FuriosaFlare : Flare
    {
        public void Setup(Flare otherFlare)
        {
            // Copied values
            this.blastForce = otherFlare.blastForce; ;
            this.hitUnitsSize = otherFlare.hitUnitsSize;
            this.fire1 = otherFlare.fire1; 
            this.fire2 = otherFlare.fire2;
            this.fire3 = otherFlare.fire3;
            this.smoke1 = otherFlare.smoke1;
            this.smoke2 = otherFlare.smoke2;
            this.smoke3 = otherFlare.smoke3;
            this.explosion = otherFlare.explosion;
            this.trailType = otherFlare.trailType;
            this.z = otherFlare.z;
            this.shrapnel = otherFlare.shrapnel;
            this.shrapnelSpark = otherFlare.shrapnelSpark;
            this.flickPuff = otherFlare.flickPuff;
            this.life = otherFlare.life;
            this.projectileSize = otherFlare.projectileSize;
            this.fullLife = 1;
            this.fadeDamage = otherFlare.fadeDamage;
            this.damageType = otherFlare.damageType;
            this.playerNum = otherFlare.playerNum;
            this.soundHolder = otherFlare.soundHolder;
            this.canHitGrenades = otherFlare.canHitGrenades;
            this.affectScenery = otherFlare.affectScenery;
            this.soundVolume = otherFlare.soundVolume;
            this.firedBy = otherFlare.firedBy;
            this.seed = otherFlare.seed;
            this.random = new Randomf(UnityEngine.Random.Range(0, 10000));
            this.sparkCount = otherFlare.sparkCount;
            this.isDamageable = otherFlare.isDamageable;
            this.horizontalProjectile = otherFlare.horizontalProjectile;
            this.isWideProjectile = otherFlare.isWideProjectile;
            this.zOffset = (1f - UnityEngine.Random.value * 2f) * 0.04f;
            this.canReflect = otherFlare.canReflect;
            this.startProjectileSpeed = 400f;
            this.heldDelay = 0;
            this.canMakeEffectsMoreThanOnce = false;
            this.whitePopEffect = otherFlare.whitePopEffect;
            this.doubleSpeed = otherFlare.doubleSpeed;
            this.giveDeflectAchievementOnMookKill = otherFlare.giveDeflectAchievementOnMookKill;
            this.health = 3;
            this.maxHealth = -1;

            UnityEngine.Object.Destroy(otherFlare);

            this.damage = this.damageInternal = this.fullDamage =  Furibrosa.flaregunDamage;
            this.range = 9f;
        }

        public override void Fire(float x, float y, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy)
        {
            this.gameObject.SetActive(true);
            base.Fire(x, y, xI, yI, _zOffset, playerNum, FiredBy);
        }
    }
}
