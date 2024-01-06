using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using HarmonyLib;
using System.Reflection;

namespace Mission_Impossibro
{
    class TranqDart : Projectile
    {
		public static Material storedMat;
		public AnimatedTexture texture;
		public SpriteSM storedSprite;
		//public static List<Mook> sleepingUnits = new List<Mook>();
		protected override void Awake()
		{
			MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

			if (storedMat == null)
			{
				storedMat = ResourcesController.GetMaterial(".\\Mods\\Development - BroMaker\\Storage\\Bros\\Mission Impossibro\\TranqDarts1.png");
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

			BMLogger.Log("in tranq awake");
			base.Awake();
		}

        public override void Fire(float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy)
        {
            base.Fire(newX, newY, xI, yI, _zOffset, playerNum, FiredBy);
        }

        protected override void HitUnits()
        {
			bool hitUnits = false;
			float xRange = this.projectileSize;
			float yRange = this.projectileSize / 2;
			MonoBehaviour damageSender = this.firedBy;
			MonoBehaviour avoidID = this.firedBy;
			if (Map.units != null)
			{
				for (int i = Map.units.Count - 1; i >= 0; i--)
				{
					Unit unit = Map.units[i];
					if (unit != null && (GameModeController.DoesPlayerNumDamage(playerNum, unit.playerNum) || (unit.playerNum < 0 && unit.CatchFriendlyBullets())) && !unit.invulnerable && unit.health > 0 && unit.actionState != ActionState.Fallen )
					{
						float num2 = unit.X - X;
						if (Mathf.Abs(num2) - xRange < unit.width)
						{
							float num3 = unit.Y + unit.height / 2f + 4f - Y;
							if (Mathf.Abs(num3) - yRange < unit.height && (Mathf.Sqrt(num2 * num2 + num3 * num3) <= xRange + unit.width) && (avoidID == null || avoidID != unit || unit.CatchFriendlyBullets()))
							{
								if ( unit is Mook )
                                {
									Mook mook = unit as Mook;
									BMLogger.Log("mooktype: " + mook.mookType);
									BMLogger.Log("attempting to fall on face");
									Traverse trav = Traverse.Create(mook);
									trav.Method("FallOnFace").GetValue();
									trav.Field("fallenTime").SetValue(10f);
									//MethodInfo methodInfo = typeof(CaravanEnterMapUtility).GetMethod("FindNearEdgeCell", BindingFlags.NonPublic | BindingFlags.Instance);
									//var parameters = new object[] { map, extraCellValidator };
									//__result = (IntVec3)methodInfo.Invoke(null, parameters);
									hitUnits = true;
								}
							}
						}
					}
				}
			}

			if ( hitUnits )
            {
				this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
				UnityEngine.Object.Destroy(base.gameObject);
				this.hasHit = true;
			}

			/*if (this.reversing)
			{
				if (Map.HitLivingUnits(this.firedBy ?? this, this.playerNum, this.damageInternal, this.damageType, this.projectileSize, this.projectileSize / 2f, base.X, base.Y, this.xI, this.yI, false, false, true, false))
				{
					this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
					UnityEngine.Object.Destroy(base.gameObject);
					this.hasHit = true;
				}
			}
			else if (Map.HitUnits(this.firedBy, this.firedBy, this.playerNum, this.damageInternal, this.damageType, this.projectileSize, this.projectileSize / 2f, base.X, base.Y, this.xI, this.yI, false, false, true, true))
			{
				this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
				UnityEngine.Object.Destroy(base.gameObject);
				this.hasHit = true;
			}*/
		}

        public void Setup()
		{
			this.damageType = DamageType.Normal;

			this.damage = 3;

			this.damageInternal = this.damage;
			this.fullDamage = this.damage;

			this.enabled = true;
		}
	}
}
