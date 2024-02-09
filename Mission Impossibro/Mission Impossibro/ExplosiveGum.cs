using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using System.Reflection;
using System.IO;

namespace Mission_Impossibro
{
    public class ExplosiveGum : SachelPack
    {
		public static Material storedMat;
		public SpriteSM storedSprite;
		protected bool stickToDeadUnit;
		protected float previousSpeed;

		protected override void Awake()
		{
			MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

			if (storedMat == null)
			{
				string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				storedMat = ResourcesController.GetMaterial(directoryPath, "explosiveGum.png");
			}

			renderer.material = storedMat;

			SpriteSM sprite = this.gameObject.GetComponent<SpriteSM>();
			sprite.lowerLeftPixel = new Vector2(0, 16);
			sprite.pixelDimensions = new Vector2(16, 16);

			sprite.plane = SpriteBase.SPRITE_PLANE.XY;
			sprite.width = 16;
			sprite.height = 16;
			sprite.offset = new Vector3(0, 0, 0);

			storedSprite = sprite;

			base.Awake();

			// Setup Variables
			this.life = 1f;
			this.fullLife = 1f;
			this.range = 20f;
			this.blastForce = 8f;
			this.sticky = true;
			this.stickyToUnits = true;
			this.damage = 25;
			this.damageInternal = 1;
			this.fullDamage = 1;
			this.damageType = DamageType.Fire;
			this.canHitGrenades = false;
			this.projectileSize = 8;
			this.soundVolume = 0.6f;
		}

		public void AssignNullValues(SachelPack other)
		{
			this.soundHolder = other.soundHolder;
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
