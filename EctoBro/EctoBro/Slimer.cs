using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using HarmonyLib;
using System.Reflection;
using System.IO;

namespace EctoBro
{
    class Slimer : Projectile
    {
		public static Material storedMat;
		public SpriteSM storedSprite;
		BloodColor bloodColor = BloodColor.Green;
		public static int overrideBloodColor = 0;
		public static AudioClip[] freezeSounds;
		public List<Unit> frozenUnits = new List<Unit>();
		protected const float frameRate = 0.1f;
		protected int frame = 0;
		protected float counter = 0f;
		protected float startingY;
		protected float startingX;

		protected override void Awake()
		{
			MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

			string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			if (storedMat == null)
			{
				storedMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "slimer.png"), ResourcesController.Unlit_DepthCutout);
			}

			renderer.material = storedMat;

			SpriteSM sprite = this.gameObject.GetComponent<SpriteSM>();
			sprite.lowerLeftPixel = new Vector2(0, 32);
			sprite.pixelDimensions = new Vector2(32, 32);

			sprite.plane = SpriteBase.SPRITE_PLANE.XY;
			sprite.width = 32;
			sprite.height = 32;
			sprite.offset = new Vector3(0, 0, 0);

			sprite.color = new Color(1f, 1f, 1f, 0.75f);

			storedSprite = sprite;

			// Load sounds
			if (freezeSounds == null)
			{
				freezeSounds = new AudioClip[7];
				freezeSounds[0] = ResourcesController.GetAudioClip(Path.Combine(directoryPath, "sounds"), "freeze1.wav");
				freezeSounds[1] = ResourcesController.GetAudioClip(Path.Combine(directoryPath, "sounds"), "freeze2.wav");
				freezeSounds[2] = ResourcesController.GetAudioClip(Path.Combine(directoryPath, "sounds"), "freeze3.wav");
				freezeSounds[3] = ResourcesController.GetAudioClip(Path.Combine(directoryPath, "sounds"), "freeze4.wav");
				freezeSounds[4] = ResourcesController.GetAudioClip(Path.Combine(directoryPath, "sounds"), "freeze5.wav");
				freezeSounds[5] = ResourcesController.GetAudioClip(Path.Combine(directoryPath, "sounds"), "freeze6.wav");
				freezeSounds[6] = ResourcesController.GetAudioClip(Path.Combine(directoryPath, "sounds"), "freeze7.wav");
				freezeSounds[7] = ResourcesController.GetAudioClip(Path.Combine(directoryPath, "sounds"), "freeze8.wav");
			}

			base.Awake();

			this.damageType = DamageType.Freeze;

			this.damage = 0;

			this.damageInternal = this.damage;
			this.fullDamage = this.damage;

			this.life = 2f;
		}

        public override void Fire(float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy)
        {
            base.Fire(newX, newY, xI, yI, _zOffset, playerNum, FiredBy);

			base.transform.localScale = new Vector3(base.transform.localScale.x * -1f, base.transform.localScale.y, base.transform.localScale.z);
			startingY = base.Y;
			startingX = base.X;
        }

        protected override void Update()
        {
            base.Update();

			counter += this.t;
			if ( counter > frameRate )
            {
				counter -= frameRate;
				++frame;
				if ( frame > 7 )
                {
					frame = 0;
                }
				this.storedSprite.SetLowerLeftPixel(frame * 32, 32);
            }

			base.Y = startingY + 25.0f * Math.Sin( Mathf.Abs(base.X - startingX) / 25.0f);
        }

        protected override void TryHitUnitsAtSpawn()
        {
            //base.TryHitUnitsAtSpawn();
        }

        protected override bool HitWalls()
        {
			++overrideBloodColor;

			int column = (int)((base.X + 8f) / 16f);
			int row = (int)((base.Y + 8f) / 16f);

			for ( int i = 0; i < 2; ++i )
            {
				if (Map.IsBlockSolid(column, row + 1))
				{
					Block block = Map.GetBlock(column, row + 1);
					if (block != null)
					{
						block.Bloody(DirectionEnum.Down, this.bloodColor, true);
					}
				}
				if (Map.IsBlockSolid(column - 1, row))
				{
					Block block2 = Map.GetBlock(column - 1, row);
					if (block2 != null)
					{
						block2.Bloody(DirectionEnum.Right, this.bloodColor, true);
					}
				}
				if (Map.IsBlockSolid(column + 1, row))
				{
					Block block3 = Map.GetBlock(column + 1, row);
					if (block3 != null)
					{
						block3.Bloody(DirectionEnum.Left, this.bloodColor, true);
					}
				}
				if (Map.IsBlockSolid(column, row - 1))
				{
					Block block4 = Map.GetBlock(column, row - 1);
					if (block4 != null)
					{
						block4.Bloody(DirectionEnum.Up, this.bloodColor, true);
					}
				}
				++column;
			}

			--overrideBloodColor;
			return base.HitWalls();
        }

		public bool FreezeUnits(MonoBehaviour firedBy, int playerNum, float x, float y, float range, float freezeTime)
		{
			if (Map.units == null)
			{
				return false;
			}
			bool result = false;
			for (int i = Map.units.Count - 1; i >= 0; i--)
			{
				Unit unit = Map.units[i];
				if (unit != null && !unit.invulnerable && GameModeController.DoesPlayerNumDamage(playerNum, unit.playerNum))
				{
					float num = unit.X - x;
					if (Mathf.Abs(num) - range < unit.width && (unit.Y != y || num != 0f))
					{
						float f = unit.Y + unit.height / 2f + 3f - y;
						if (Mathf.Abs(f) - range < unit.height && !frozenUnits.Contains(unit))
						{
							if (unit.CanFreeze())
							{
								unit.Freeze(freezeTime);
								Sound.GetInstance().PlaySoundEffectAt(freezeSounds, 0.6f, unit.transform.position, 1f, true, true, false, 0f);
							}
							else
							{
								unit.Damage(15, DamageType.Freeze, 0f, 10f, 0, firedBy, x, y);
							}
							frozenUnits.Add(unit);
							result = true;
						}
					}
				}
			}
			return result;
		}

		protected override void HitUnits()
        {
			FreezeUnits(this.firedBy, this.playerNum, base.X, base.Y - 3f, 18f, 3f);
		}

        protected override void HitHorizontalWalls()
        {
        }

        protected override void HitFragile()
        {
        }
    }
}
