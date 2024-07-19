using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            this.random = new Randomf(UnityEngine.Random.Range(0, 10000));
            this.sparkCount = otherProjectile.sparkCount;
            this.isDamageable = otherProjectile.isDamageable;
            this.horizontalProjectile = otherProjectile.horizontalProjectile;
            this.isWideProjectile = otherProjectile.isWideProjectile;
            this.zOffset = (1f - UnityEngine.Random.value * 2f) * 0.04f;
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

        public override void Fire(float x, float y, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy)
        {
            this.gameObject.SetActive(true);
            base.Fire(x, y, xI, yI, _zOffset, playerNum, FiredBy);
        }

        protected override void HitUnits()
        {
            float xI = this.xI;
            this.xI *= 0.3333334f;
            Unit hitUnit = Map.HitClosestUnit(this, this.playerNum, 0, this.damageType, this.projectileSize, this.projectileSize / 2f, base.X, base.Y, this.xI, this.yI, false, false, true, false, false);
            //if (Map.HitLivingUnits(this, this.playerNum, 0, this.damageType, this.projectileSize, this.projectileSize / 2f, base.X, base.Y, this.xI, this.yI, false, false, true, false))
            if ( hitUnit != null )
            {
                this.hasHitUnit = true;
                this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
                UnityEngine.Object.Destroy(base.gameObject);
                Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.hitSounds, 0.5f, base.transform.position, 0.95f + UnityEngine.Random.value * 0.15f, true, false, false, 0f);
                Main.StartControllingUnit(playerNum, hitUnit);
            }
            this.xI = xI;
        }

    }
}
