using UnityEngine;
using BroMakerLib.CustomObjects.Projectiles;
using System;
using BroMakerLib.Loggers;

namespace Mission_Impossibro
{
    public class ExplosiveGum : CustomSachelPack
    {
		protected bool stickToDeadUnit;
		protected float previousSpeed;

		protected override void Awake()
		{
            if ( this.DefaultSoundHolder == null )
            {
                this.DefaultSoundHolder = ( HeroController.GetHeroPrefab( HeroType.BroGummer ) as BroGummer ).sachelPackProjectile.soundHolder;
            }

            base.Awake();

			// Setup Variables
			this.life = 1f;
			this.fullLife = 1f;
			this.range = 20f;
			this.blastForce = 8f;
			this.sticky = true;
			this.stickyToUnits = true;
			this.damage = 20;
			this.damageInternal = 1;
			this.fullDamage = 1;
			this.damageType = DamageType.Fire;
			this.canHitGrenades = false;
			this.projectileSize = 8;
			this.soundVolume = 0.6f;
		}

        public override void PrefabSetup()
        {
			SachelPack other = ( HeroController.GetHeroPrefab( HeroType.BroGummer ) as BroGummer ).sachelPackProjectile as SachelPack;
            this.fire1 = other.fire1;
            this.fire2 = other.fire2;
            this.fire3 = other.fire3;
            this.smoke1 = other.smoke1;
            this.smoke2 = other.smoke2;
            this.explosion = other.explosion;
            this.explosionSmall = other.explosionSmall;
            this.shrapnel = other.shrapnel;
            this.shrapnelSpark = other.shrapnelSpark;
            this.flickPuff = other.flickPuff;
            this.heightOffGround = other.heightOffGround;
            this.bounceYM = other.bounceYM;
            this.bounceXM = other.bounceXM;
            this.frictionM = other.frictionM;
            this.bounceVolumeM = other.bounceVolumeM;
            this.soundVolume = other.soundVolume;
            this.sparkCount = other.sparkCount;
            this.horizontalProjectile = other.horizontalProjectile;
        }

		public override void TryStickToUnit(Unit unit, bool _stickToDeadUnit = false)
		{
			this.stickToDeadUnit = _stickToDeadUnit;
			if (unit != null)
			{
				this.stuckToUnit = unit;
				this.PlayStuckSound(0.7f);
				Vector3 arg = this.stuckToUnit.transform.InverseTransformPoint(base.transform.position);
				this.StickToUnit(this.stuckToUnit, arg);
			}
		}
	}
}
