﻿using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using HarmonyLib;
using System.Reflection;
using System.IO;

namespace Mission_Impossibro
{
    class TranqDart : Projectile
    {
		public static Material storedMat;
		public SpriteSM storedSprite;
		protected override void Awake()
		{
			MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

			if (storedMat == null)
			{
				string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				storedMat = ResourcesController.GetMaterial(directoryPath, "TranqDarts1.png");
			}

			renderer.material = storedMat;

			SpriteSM sprite = this.gameObject.GetComponent<SpriteSM>();
			sprite.lowerLeftPixel = new Vector2(0, 16);
			sprite.pixelDimensions = new Vector2(16, 16);

			sprite.plane = SpriteBase.SPRITE_PLANE.XY;
			sprite.width = 10;
			sprite.height = 10;
			sprite.offset = new Vector3(0, 0, 0);

			storedSprite = sprite;

			base.Awake();

			this.damageType = DamageType.Normal;

			this.damage = 3;

			this.damageInternal = this.damage;
			this.fullDamage = this.damage;

			this.life = 0.30f;
		}

        public override void Fire(float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy)
        {
            base.Fire(newX, newY, xI, yI, _zOffset, playerNum, FiredBy);
        }

		bool CanFallOnFace( Mook mook )
        {
			MookType mookType = mook.mookType;
			switch( mookType )
            {
				case MookType.Trooper:
					if (mook is MookGeneral)
						return false;
					return true;
				case MookType.Suicide:
				case MookType.Scout:
				case MookType.RiotShield:
				case MookType.Grenadier:
				case MookType.Bazooka:
				case MookType.UndeadTrooper:
				case MookType.Warlock:
					return true;
				default:
					return false;
            }
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
					if (unit != null && (GameModeController.DoesPlayerNumDamage(playerNum, unit.playerNum) || (unit.playerNum < 0 && unit.CatchFriendlyBullets())) && !unit.invulnerable && unit.health > 0  )
					{
						float num2 = unit.X - X;
						if (Mathf.Abs(num2) - xRange < unit.width)
						{
							float num3 = unit.Y + unit.height / 2f + 4f - Y;
							if (Mathf.Abs(num3) - yRange < unit.height && (Mathf.Sqrt(num2 * num2 + num3 * num3) <= xRange + unit.width) && (avoidID == null || avoidID != unit || unit.CatchFriendlyBullets()))
							{
								float stunTime = 20.0f / unit.health;
								stunTime = (stunTime < 2 ? 2 : stunTime);
								// Hit boss
								if ( unit.CompareTag("Metal") || unit.CompareTag("Boss") || unit is DolphLundrenSoldier || unit is SatanMiniboss )
                                {
									// Stun boss after 8 hits
									if ( Bro.bossHitCounter.ContainsKey(unit) )
                                    {
										int hits = Bro.bossHitCounter[unit];
										if ( hits > 8 )
                                        {
											unit.Stun(stunTime);
											Bro.bossHitCounter[unit] = -6;
										}
										else
                                        {
											++Bro.bossHitCounter[unit];
                                        }
                                    }
									else
                                    {
										Bro.bossHitCounter.Add(unit, 1);
                                    }

									Map.KnockAndDamageUnit(damageSender, unit, damage, damageType, xI, yI, (int)Mathf.Sign(xI), false, X, Y, false);
									hitUnits = true;
									break;
								}
								// Don't hit mooks other non boss enemies if they are already stunned
								else if (unit.actionState != ActionState.Fallen && !unit.IsIncapacitated())
                                {
									if (unit is Mook)
									{
										Mook mook = unit as Mook;

										if (CanFallOnFace(mook))
										{
											if (!mook.IsOnGround())
											{
												if (mook is MookJetpack)
												{
													Traverse trav = Traverse.Create(mook as MookJetpack);
													trav.Method("StartSpiralling").GetValue();
												}
												else
												{
													mook.IsParachuteActive = false;
													Traverse trav = Traverse.Create(mook);
													trav.Method("FallOnFace").GetValue();
													trav.Field("fallenTime").SetValue(stunTime);
												}
												Map.KnockAndDamageUnit(damageSender, unit, 2, damageType, xI, yI, (int)Mathf.Sign(xI), false, X, Y, false);
											}
											else
											{
												Traverse trav = Traverse.Create(mook);
												trav.Method("FallOnFace").GetValue();
												trav.Field("fallenTime").SetValue(stunTime);
												Map.KnockAndDamageUnit(damageSender, unit, 2, damageType, xI, yI, (int)Mathf.Sign(xI), false, X, Y, false);
											}
										}
										else
										{
											unit.Stun(stunTime);
											Map.KnockAndDamageUnit(damageSender, unit, 2, damageType, xI, yI, (int)Mathf.Sign(xI), false, X, Y, false);
										}
									}
									else
									{
										unit.Stun(stunTime);
										Map.KnockAndDamageUnit(damageSender, unit, damage, damageType, xI, yI, (int)Mathf.Sign(xI), false, X, Y, false);
									}
									hitUnits = true;
									break;
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
		}

        protected override void TryHitUnitsAtSpawn()
        {
			this.HitUnits();
        }

        public void Setup()
		{
			this.enabled = true;
		}
	}
}
