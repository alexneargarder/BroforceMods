using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;

namespace Brostbuster
{
    class Slimer : CustomProjectile
    {
		BloodColor bloodColor = BloodColor.Green;
		public static int overrideBloodColor = 0;
		public List<Unit> frozenUnits = new List<Unit>();
		protected const float frameRate = 0.1f;
		protected int frame = 0;
		protected float counter = 0f;
		protected float startingY;
		protected float startingX;
		public AudioClip[] freezeSounds;
		public AudioClip[] slimerGroans;
		public AudioSource slimerAudio;

		protected override void Awake()
		{
			if ( this.sprite == null )
			{
                this.spriteLowerLeftPixel = new Vector2( 0, 32 );
                this.spritePixelDimensions = new Vector2( 32, 32 );
                this.spriteWidth = 32;
                this.spriteHeight = 32;
                this.spriteColor = new Color( 1f, 1f, 1f, 0.75f );
            }

            base.Awake();

            this.damageType = DamageType.Freeze;

			this.damage = 0;

			this.damageInternal = this.damage;
			this.fullDamage = this.damage;

			this.life = 4f;

			if (this.slimerAudio == null)
			{
				if (base.gameObject.GetComponent<AudioSource>() == null)
				{
					this.slimerAudio = base.gameObject.AddComponent<AudioSource>();
					this.slimerAudio.rolloffMode = AudioRolloffMode.Logarithmic;
					this.slimerAudio.minDistance = 150f;
					this.slimerAudio.maxDistance = 2000f;
					this.slimerAudio.dopplerLevel = 0.1f;
					this.slimerAudio.spatialBlend = 1f;
					this.slimerAudio.volume = 0.6f;
				}
				else
				{
					this.slimerAudio = this.GetComponent<AudioSource>();
				}
			}

            this.gameObject.layer = 28;
        }

        public override void PrefabSetup()
        {
            // Load sounds
            freezeSounds = new AudioClip[8];
            freezeSounds[0] = ResourcesController.GetAudioClip( soundPath, "freeze1.wav" );
            freezeSounds[1] = ResourcesController.GetAudioClip( soundPath, "freeze2.wav" );
            freezeSounds[2] = ResourcesController.GetAudioClip( soundPath, "freeze3.wav" );
            freezeSounds[3] = ResourcesController.GetAudioClip( soundPath, "freeze4.wav" );
            freezeSounds[4] = ResourcesController.GetAudioClip( soundPath, "freeze5.wav" );
            freezeSounds[5] = ResourcesController.GetAudioClip( soundPath, "freeze6.wav" );
            freezeSounds[6] = ResourcesController.GetAudioClip( soundPath, "freeze7.wav" );
            freezeSounds[7] = ResourcesController.GetAudioClip( soundPath, "freeze8.wav" );

            slimerGroans = new AudioClip[4];
            slimerGroans[0] = ResourcesController.GetAudioClip( soundPath, "slimer1.wav" );
            slimerGroans[1] = ResourcesController.GetAudioClip( soundPath, "slimer2.wav" );
            slimerGroans[2] = ResourcesController.GetAudioClip( soundPath, "slimer3.wav" );
            slimerGroans[3] = ResourcesController.GetAudioClip( soundPath, "slimer4.wav" );
        }

        public override void Fire(float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy)
        {
            base.Fire(newX, newY, xI, yI, _zOffset, playerNum, FiredBy);

			base.transform.localScale = new Vector3(base.transform.localScale.x * -1f, base.transform.localScale.y, base.transform.localScale.z);
			startingY = base.Y;
			startingX = base.X;

			this.slimerAudio.clip = slimerGroans[UnityEngine.Random.Range(0, slimerGroans.Length)];
			this.slimerAudio.loop = false;
			this.slimerAudio.Play();
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
				this.sprite.SetLowerLeftPixel(frame * 32, 32);
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

			float num = Mathf.Abs(this.xI) * this.t;
			if (Physics.Raycast(new Vector3(base.X - Mathf.Sign(this.xI) * this.projectileSize, base.Y + this.projectileSize * 0.5f, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize * 2f + num, this.groundLayer) && this.raycastHit.distance < this.projectileSize + num)
			{
				if ( this.raycastHit.collider.gameObject.GetComponent<Block>() == null )
                {
					this.ProjectileApplyDamageToBlock(raycastHit.collider.gameObject, 15, this.damageType, this.xI, this.yI);
					this.MakeEffects(true, raycastHit.point.x + raycastHit.normal.x * 3f, raycastHit.point.y + raycastHit.normal.y * 3f, true, raycastHit.normal, raycastHit.point);
				}
				
			}


			// Don't perform hit detection when out of bounds 
			if ( base.X < 0 || base.Y < 0 )
			{
				return false;
			}

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

        protected override void HitGrenades()
        {
        }

        protected override void HitProjectiles()
        {
        }
    }
}
