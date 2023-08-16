using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib;
using BroMakerLib.Loggers;
using UnityEngine.Audio;

namespace Captain_Ameribro_Mod
{
    [HeroPreset("Captain Ameribro", HeroType.Nebro)]
    class CaptainAmeribro : SwordHero
    {
		protected Shield shield;
		public float shieldSpeed = 500f;

		public float specialAttackDashTime = 0f;
		public Material materialNormal, materialArmless;
		public float sliceVolume = 0.7f;
		public float wallHitVolume = 0.6f;
		protected int punchingIndex = 0;
		protected bool grabbingShield;
		protected int grabbingFrame;

		protected override void Awake()
        {
			materialNormal = this.material;
			materialArmless = (HeroController.GetHeroPrefab(HeroType.Nebro) as Nebro).materialArmless;
			//materialArmless.mainTexture = ResourcesController.CreateTexture(Main.ExtractResource("Captain Ameribro Mod.Sprites.neobro_armless_anim.png"));
			materialArmless.mainTexture = ResourcesController.CreateTexture("C:\\Users\\Alex\\Desktop\\Coding Things\\Github\\BroforceModsDev\\Bromaker - Captain Ameribro Mod\\Bromaker - Captain Ameribro Mod\\Sprites", "neobro_armless_anim.png");

			shield = new GameObject("CaptainAmeribroShield", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(AnimatedTexture), typeof(Shield), typeof(BoxCollider) } ).GetComponent<Shield>();
			shield.enabled = false;
			shield.shieldCollider = shield.gameObject.GetComponent<BoxCollider>();
			Boomerang boom = (HeroController.GetHeroPrefab(HeroType.BroMax) as BroMax).boomerang as Boomerang;
			//boom = UnityEngine.Object.Instantiate(boom, Vector3.zero, Quaternion.identity);
			//boom = Boomerang.Instantiate(boom);
			//shield = boom.gameObject.AddComponent<Shield>();
			shield.assignNullValues(boom);
			BMLogger.Log("collider null? " + (shield.shieldCollider == null));
			//UnityEngine.Object.DestroyImmediate(boom.gameObject.GetComponent<Boomerang>());
			//BMLogger.Log("collider null? " + (shield.shieldCollider == null));
			//shield.transform.position = this.transform.position;

			//boom.
			BMLogger.Log("created shield");
			base.Awake();
        }

        protected override void UseSpecial()
        {


			BMLogger.Log("in use special");
			//ProjectileController.SpawnProjectileOverNetwork(this.shield, this, base.X + base.transform.localScale.x * 6f, base.Y + 15f, base.transform.localScale.x * this.shieldSpeed, 0f, false, base.playerNum, false, false, 0f);
			BMLogger.Log("this.x: " + base.X + base.transform.localScale.x * 6f);
			BMLogger.Log("this.x: " + base.Y + 15f);
			BMLogger.Log("this.x: " + base.transform.localScale.x * this.shieldSpeed);
			Shield newShield = ProjectileController.SpawnProjectileLocally(this.shield, this, base.X + base.transform.localScale.x * 6f, base.Y + 15f, base.transform.localScale.x * this.shieldSpeed, 0f, false, base.playerNum, false, false, 0f) as Shield;
			//newShield.setup(this.shield);

			newShield.enabled = true;
			Main.checkAttached(newShield.gameObject);
			//ProjectileController.SpawnProjectileLocally( boom, this, base.X + base.transform.localScale.x * 6f, base.Y + 15f, base.transform.localScale.x * this.shieldSpeed, 0f, false, base.playerNum, false, false, 0f);
			BMLogger.Log("finished spawning projectile");
			//base.UseSpecial();
		}

		protected override void AnimateSpecial()
		{
			if (this.grabbingShield)
			{
				this.grabbingFrame--;
				this.SetSpriteOffset(0f, 0f);
				this.DeactivateGun();
				this.frameRate = 0.045f;
				int num = 17 + Mathf.Clamp(this.grabbingFrame, 0, 7);
				this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)(this.spritePixelHeight * 5));
				if (this.grabbingFrame <= 0)
				{
					base.frame = 0;
					this.usingSpecial = false;
					this.grabbingShield = false;
				}
			}
			else
			{
				if (!this.useNewThrowingFrames)
				{
					this.SetSpriteOffset(0f, 0f);
					this.DeactivateGun();
					this.frameRate = 0.0334f;
					int num = 16 + Mathf.Clamp(base.frame, 0, 4);
					this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)this.spritePixelHeight);
					if (base.frame == 2)
					{
						this.UseSpecial();
					}
					if (base.frame >= 4)
					{
						base.frame = 0;
						this.ActivateGun();
						this.usingSpecial = (this.usingPockettedSpecial = false);
					}
				}
				else
				{
					this.SetSpriteOffset(0f, 0f);
					this.DeactivateGun();
					this.frameRate = 0.0334f;
					int num2 = 17 + Mathf.Clamp(base.frame, 0, 7);
					this.sprite.SetLowerLeftPixel((float)(num2 * this.spritePixelWidth), (float)(this.spritePixelHeight * 5));
					if (base.frame == 4)
					{
						this.UseSpecial();
					}
					if (base.frame >= 7)
					{
						base.frame = 0;
						this.usingSpecial = (this.usingPockettedSpecial = false);
						this.ActivateGun();
						this.ChangeFrame();
					}
				}
			}
		}

		public void PlaySliceSound()
		{
			if (this.sound == null)
			{
				this.sound = Sound.GetInstance();
			}
			if (this.sound != null)
			{
				this.sound.PlaySoundEffectAt(this.soundHolder.special2Sounds, this.sliceVolume, base.transform.position, 1f, true, false, false, 0f);
			}
		}

		public void PlayWallSound()
		{
			if (this.sound == null)
			{
				this.sound = Sound.GetInstance();
			}
			if (this.sound != null)
			{
				this.sound.PlaySoundEffectAt(this.soundHolder.defendSounds, this.wallHitVolume, base.transform.position, 1f, true, false, false, 0f);
			}
		}

		protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
		{
			if (this.attachedToZipline != null)
			{
				this.attachedToZipline.DetachUnit(this);
				if (base.transform.localScale.x > 0f)
				{
					this.AirDashRight();
				}
				else
				{
					this.AirDashLeft();
				}
				return;
			}
			Map.HurtWildLife(x + base.transform.localScale.x * 13f, y + 5f, 12f);
			this.gunFrame = 1;
			this.punchingIndex++;
			this.gunCounter = 0f;
			this.SetGunFrame();
			float num = base.transform.localScale.x * 12f;
			this.ConstrainToFragileBarriers(ref num, 16f);
			if (Physics.Raycast(new Vector3(x - Mathf.Sign(base.transform.localScale.x) * 12f, y + 5.5f, 0f), new Vector3(base.transform.localScale.x, 0f, 0f), out this.raycastHit, 19f, this.groundLayer | 1 << LayerMask.NameToLayer("FLUI")) || Physics.Raycast(new Vector3(x - Mathf.Sign(base.transform.localScale.x) * 12f, y + 10.5f, 0f), new Vector3(base.transform.localScale.x, 0f, 0f), out this.raycastHit, 19f, this.groundLayer | 1 << LayerMask.NameToLayer("FLUI")))
			{
				this.MakeEffects(this.raycastHit.point.x + base.transform.localScale.x * 4f, this.raycastHit.point.y);
				MapController.Damage_Local(this, this.raycastHit.collider.gameObject, 9, DamageType.Bullet, this.xI + base.transform.localScale.x * 200f, 0f, x, y);
				this.hasHitWithWall = true;
				if (Map.HitUnits(this, base.playerNum, 5, DamageType.Melee, 6f, x, y, base.transform.localScale.x * 520f, 460f, false, true, false, this.alreadyHit, false, false))
				{
					this.hasHitWithSlice = true;
				}
				else
				{
					this.hasHitWithSlice = false;
				}
				Map.DisturbWildLife(x, y, 80f, base.playerNum);
			}
			else
			{
				this.hasHitWithWall = false;
				if (Map.HitUnits(this, this, base.playerNum, 5, DamageType.Melee, 12f, 9f, x, y, base.transform.localScale.x * 520f, 460f, false, true, false, true))
				{
					this.hasHitWithSlice = true;
				}
				else
				{
					this.hasHitWithSlice = false;
				}
			}
		}

		protected override void RunGun()
        {
            if (this.specialAttackDashTime > 0f)
			{
				this.gunFrame = 11;
				this.SetGunFrame();
			}
			else if (!this.WallDrag)
			{
				if (this.gunFrame > 0)
				{
					if (!this.hasBeenCoverInAcid)
					{
						base.GetComponent<Renderer>().material = this.materialArmless;
					}
					this.gunCounter += this.t;
					if (this.gunCounter > 0.0334f)
					{
						this.gunCounter -= 0.0334f;
						this.gunFrame++;
						if (this.gunFrame >= 6)
						{
							this.gunFrame = 0;
						}
						this.SetGunFrame();
						if (this.gunFrame == 2)
						{
							if (this.hasHitWithSlice)
							{
								this.PlaySliceSound();
							}
							else if (this.hasHitWithWall)
							{
								this.PlayWallSound();
							}
						}
					}
				}
	/*			else if (this.currentZone != null && this.currentZone.PoolIndex != -1)
				{
					this.gunSprite.SetLowerLeftPixel(0f, 128f);
				}*/
			}
			if ((!this.gunSprite.gameObject.activeSelf || this.gunFrame == 0) && !this.hasBeenCoverInAcid)
			{
				base.GetComponent<Renderer>().material = this.materialNormal;
			}
        }

        protected void SetGunFrame()
        {
			if (!this.ducking)
			{
				int num = this.punchingIndex % 2;
				if (num != 0)
				{
					if (num == 1)
					{
						this.gunSprite.SetLowerLeftPixel((float)(32 * (5 + this.gunFrame)), 32f);
					}
				}
				else
				{
					this.gunSprite.SetLowerLeftPixel((float)(32 * this.gunFrame), 32f);
				}
			}
			else
			{
				int num2 = this.punchingIndex % 2;
				if (num2 != 0)
				{
					if (num2 == 1)
					{
						this.gunSprite.SetLowerLeftPixel((float)(32 * (15 + this.gunFrame)), 32f);
					}
				}
				else
				{
					this.gunSprite.SetLowerLeftPixel((float)(32 * (10 + this.gunFrame)), 32f);
				}
			}
		}

        protected override void SetGunPosition(float xOffset, float yOffset)
        {
            // Fixes arms being offset from body
            this.gunSprite.transform.localPosition = new Vector3(xOffset + 4f, yOffset, -1f);
        }

		public void ReturnShield(Shield shield)
		{
			this.SpecialAmmo++;
			if (!this.usingSpecial)
			{
				this.usingSpecial = true;
				this.grabbingFrame = 4;
				this.grabbingShield = true;
				this.ChangeFrame();
			}
		}
	}
}
