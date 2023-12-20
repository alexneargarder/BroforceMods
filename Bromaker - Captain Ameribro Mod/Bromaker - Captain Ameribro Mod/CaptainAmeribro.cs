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
		//public float shieldSpeed = 500f;
		public float shieldSpeed = 450f;

		public Material materialNormal, materialNormalShield, materialNormalNoShield, materialArmless;
		public Material gunMaterialNormal, gunMaterialNoShield;

		public float specialAttackDashTime = 0f;
		public float sliceVolume = 0.7f;
		public float wallHitVolume = 0.6f;
		protected int punchingIndex = 0;
		protected bool grabbingShield;
		protected int grabbingFrame;
		protected float specialAttackDashCounter;
		protected float airdashFadeCounter;
		public float airdashFadeRate = 0.1f;
		protected const int normalAttackDamage = 5;
		protected const int meleeAttackDamage = 7;

		protected bool isHoldingSpecial = false;
		protected float maxSpecialCharge = 1f;
		public float currentSpecialCharge = 0f;

		public int frameCount = 0;

		protected override void Awake()
        {
			shield = new GameObject("CaptainAmeribroShield", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(AnimatedTexture), typeof(Shield), typeof(SphereCollider) } ).GetComponent<Shield>();
			shield.enabled = false;
			shield.shieldCollider = shield.gameObject.GetComponent<SphereCollider>();

			Boomerang boom = (HeroController.GetHeroPrefab(HeroType.BroMax) as BroMax).boomerang as Boomerang;
			shield.AssignNullValues(boom);

			this.currentMeleeType = BroBase.MeleeType.Disembowel;

			base.Awake();
        }

        protected override void Start()
        {
            base.Start();
			
			materialNormal = this.material;
			materialNormalShield = materialNormal;

			materialNormalNoShield = new Material(this.material);

			if ( !Main.DEBUGTEXTURES )
            {
				materialNormalNoShield.mainTexture = ResourcesController.CreateTexture(Main.ExtractResource("Captain_Ameribro_Mod.Sprites.captainAmeribroMainNoShield.png"));
			}
			else
            {
				materialNormalNoShield.mainTexture = ResourcesController.CreateTexture(".\\Mods\\Development - BroMaker\\Storage\\Bros\\Captain Ameribro", "captainAmeribroMainNoShield.png");
			}

			materialArmless = (HeroController.GetHeroPrefab(HeroType.Nebro) as Nebro).materialArmless;
            if (!Main.DEBUGTEXTURES)
            {
				materialArmless.mainTexture = ResourcesController.CreateTexture(Main.ExtractResource("Captain_Ameribro_Mod.Sprites.captainAmeribroArmless.png"));
			}
			else
            {
				materialArmless.mainTexture = ResourcesController.CreateTexture(".\\Mods\\Development - BroMaker\\Storage\\Bros\\Captain Ameribro", "captainAmeribroArmless.png");
			}

			gunMaterialNormal = this.gunMaterial;

			gunMaterialNoShield = new Material(gunMaterial);
			if ( !Main.DEBUGTEXTURES )
            {
				gunMaterialNoShield.mainTexture = ResourcesController.CreateTexture(Main.ExtractResource("Captain_Ameribro_Mod.Sprites.captainAmeribroGunNoShield.png"));
			}
			else
            {
				gunMaterialNoShield.mainTexture = ResourcesController.CreateTexture(".\\Mods\\Development - BroMaker\\Storage\\Bros\\Captain Ameribro", "captainAmeribroGunNoShield.png");
			}
		}

		protected override void Update()
		{
			base.Update();
			if (isHoldingSpecial)
			{
				currentSpecialCharge += this.t;
			}

			if (this.frameCount == 120)
			{
				this.frameCount = 0;
			}
			++this.frameCount;

		}

        protected override void UseSpecial()
        {
			BMLogger.Log("--------------THROWING SHIELD----------------");
			if ( this.SpecialAmmo > 0 && !isHoldingSpecial )
            {
				if ( this.currentSpecialCharge > this.maxSpecialCharge )
                {
					this.currentSpecialCharge = this.maxSpecialCharge;
                }

				this.materialNormal = this.materialNormalNoShield;
				base.GetComponent<Renderer>().material = this.materialNormalNoShield;
				this.gunSprite.meshRender.material = this.gunMaterialNoShield;

				//ProjectileController.SpawnProjectileOverNetwork(this.shield, this, base.X + base.transform.localScale.x * 6f, base.Y + 15f, base.transform.localScale.x * this.shieldSpeed, 0f, false, base.playerNum, false, false, 0f);
				float chargedShieldSpeed = this.shieldSpeed + Shield.ChargeSpeedScalar * this.currentSpecialCharge;

				BMLogger.Log("shield charge: " + this.currentSpecialCharge);

				Shield newShield = ProjectileController.SpawnProjectileLocally(this.shield, this, base.X + base.transform.localScale.x * 6f, base.Y + 15f, base.transform.localScale.x * chargedShieldSpeed, 0f, false, base.playerNum, false, false, 0f) as Shield;

				//newShield.shieldCharge = this.currentSpecialCharge;
				newShield.Setup(this.shield, this);

				this.currentSpecialCharge = 0;

				this.SpecialAmmo--;
			}
			else
            {
				base.UseSpecial();
            }
		}

		public void ReturnShield(Shield shield)
		{
			this.SpecialAmmo++;
			this.materialNormal = this.materialNormalShield;
			base.GetComponent<Renderer>().material = this.materialNormalShield;
			this.gunSprite.meshRender.material = this.gunMaterialNormal;
			if (!this.usingSpecial)
			{
				this.usingSpecial = true;
				this.grabbingFrame = 4;
				this.grabbingShield = true;
				this.ChangeFrame();
			}
		}

		protected override void PressSpecial()
        {
			isHoldingSpecial = true;
			base.PressSpecial();
        }

		protected override void ReleaseSpecial()
        {
			isHoldingSpecial = false;
			base.ReleaseSpecial();
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
				if ( isHoldingSpecial && this.SpecialAmmo > 0 )
                {
					this.SetSpriteOffset(0f, 0f);
					this.DeactivateGun();
					//this.frameRate = 0.0334f;
					int num = 16 + Mathf.Clamp(base.frame, 0, 4);
					this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)this.spritePixelHeight);
					if ( base.frame >= 0 )
                    {
						base.frame = 0;
					}
					
				}
				else if (!this.useNewThrowingFrames)
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

        protected override void UseFire()
        {
			if ( !this.isHoldingSpecial)
            {
				base.UseFire();
				this.fireDelay = 0.15f;
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
				MapController.Damage_Local(this, this.raycastHit.collider.gameObject, normalAttackDamage + 1, DamageType.Bullet, this.xI + base.transform.localScale.x * 200f, 0f, x, y);
				this.hasHitWithWall = true;
				if (Map.HitUnits(this, base.playerNum, normalAttackDamage, DamageType.Melee, 6f, x, y, base.transform.localScale.x * 520f, 460f, false, true, false, this.alreadyHit, false, false))
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
				if (Map.HitUnits(this, this, base.playerNum, normalAttackDamage, DamageType.Melee, 12f, 9f, x, y, base.transform.localScale.x * 520f, 460f, false, true, false, true))
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

		protected override void PressHighFiveMelee(bool forceHighFive = false)
		{
			if (this.right && this.CanAirDash(DirectionEnum.Right))
			{
				if (!this.wasHighFive)
				{
					this.Airdash(true);
				}
			}
			else if (this.left && this.CanAirDash(DirectionEnum.Left))
			{
				if (!this.wasHighFive)
				{
					this.Airdash(true);
				}
			}
			else if (this.airdashTime <= 0f)
			{
				base.PressHighFiveMelee(false);
			}
		}

		protected override void RunLeftAirDash()
		{
			base.RunLeftAirDash();
			this.specialAttackDashCounter += this.t;
			if (this.specialAttackDashCounter > 0f)
			{
				this.specialAttackDashCounter -= 0.0333f;
				Map.HitUnits(this, this, base.playerNum, 1, DamageType.Crush, 9f, base.X, base.Y, this.xI * 0.3f, 500f + UnityEngine.Random.value * 200f, true, true);
			}
			if (this.airDashDelay <= 0f)
			{
				this.airdashFadeCounter += Time.deltaTime;
				if (this.airdashFadeCounter > this.airdashFadeRate)
				{
					this.airdashFadeCounter -= this.airdashFadeRate;
					this.CreateFaderTrailInstance();
				}
			}
		}

		protected override void RunRightAirDash()
		{
			base.RunRightAirDash();
			this.specialAttackDashCounter += this.t;
			if (this.specialAttackDashCounter > 0f)
			{
				this.specialAttackDashCounter -= 0.0333f;
				Map.HitUnits(this, this, base.playerNum, 1, DamageType.Crush, 9f, base.X, base.Y, this.xI * 0.3f, 500f + UnityEngine.Random.value * 200f, true, true);
			}
			if (this.airDashDelay <= 0f)
			{
				this.airdashFadeCounter += Time.deltaTime;
				if (this.airdashFadeCounter > this.airdashFadeRate)
				{
					this.airdashFadeCounter -= this.airdashFadeRate;
					this.CreateFaderTrailInstance();
				}
			}
		}

		protected void MeleeAttack(bool shouldTryHitTerrain, bool playMissSound)
        {
			bool flag;
			Map.DamageDoodads(meleeAttackDamage - 2, DamageType.Knock, base.X + (float)(base.Direction * 4), base.Y, 0f, 0f, 6f, base.playerNum, out flag, null);
			this.KickDoors(24f);
			if (Map.HitClosestUnit(this, base.playerNum, meleeAttackDamage, DamageType.Knock, 14f, 24f, base.X + base.transform.localScale.x * 8f, base.Y + 8f, base.transform.localScale.x * 200f, 500f, true, false, base.IsMine, false, true))
			{
				this.sound.PlaySoundEffectAt(this.soundHolder.meleeHitSound, 1f, base.transform.position, 1f, true, false, false, 0f);
				this.meleeHasHit = true;
			}
			else if (playMissSound)
			{
				this.sound.PlaySoundEffectAt(this.soundHolder.missSounds, 0.3f, base.transform.position, 1f, true, false, false, 0f);
			}
			this.meleeChosenUnit = null;
			if (shouldTryHitTerrain && this.TryMeleeTerrain(0, meleeAttackDamage - 2))
			{
				this.meleeHasHit = true;
			}
			this.TriggerBroMeleeEvent();
		}

        protected override void StartCustomMelee()
        {
			if (this.CanStartNewMelee())
			{
				base.frame = 1;
				base.counter = -0.05f;
				this.AnimateMelee();
			}
			else if (this.CanStartMeleeFollowUp())
			{
				this.meleeFollowUp = true;
			}
			if (!this.jumpingMelee)
			{
				this.dashingMelee = true;
				this.xI = (float)base.Direction * this.speed;
			}
			this.StartMeleeCommon();
		}

        protected override void AnimateCustomMelee()
        {
			this.AnimateMeleeCommon();
			int num = 25 + Mathf.Clamp(base.frame, 0, 6);
			int num2 = 1;
			if (!this.standingMelee)
			{
				if (this.jumpingMelee)
				{
					num = 17 + Mathf.Clamp(base.frame, 0, 6);
					num2 = 6;
				}
				else if (this.dashingMelee)
				{
					num = 17 + Mathf.Clamp(base.frame, 0, 6);
					num2 = 6;
					if (base.frame == 4)
					{
						base.counter -= 0.0334f;
					}
					else if (base.frame == 5)
					{
						base.counter -= 0.0334f;
					}
				}
			}
			this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)(num2 * this.spritePixelHeight));
			if (base.frame == 3)
			{
				base.counter -= 0.066f;
				this.MeleeAttack(true, true);
			}
			else if (base.frame > 3 && !this.meleeHasHit)
			{
				this.MeleeAttack(false, false);
			}
			if (base.frame >= 6)
			{
				base.frame = 0;
				this.CancelMelee();
			}
		}

        protected override void RunCustomMeleeMovement()
        {
			if (!this.useNewKnifingFrames)
			{
				if (base.Y > this.groundHeight + 1f)
				{
					this.ApplyFallingGravity();
				}
			}
			else if (this.jumpingMelee)
			{
				this.ApplyFallingGravity();
				if (this.yI < this.maxFallSpeed)
				{
					this.yI = this.maxFallSpeed;
				}
			}
			else if (this.dashingMelee)
			{
				if (base.frame <= 1)
				{
					this.xI = 0f;
					this.yI = 0f;
				}
				else if (base.frame <= 3)
				{
					if (this.meleeChosenUnit == null)
					{
						if (!this.isInQuicksand)
						{
							this.xI = this.speed * 1f * base.transform.localScale.x;
						}
						this.yI = 0f;
					}
					else if (!this.isInQuicksand)
					{
						this.xI = this.speed * 0.5f * base.transform.localScale.x + (this.meleeChosenUnit.X - base.X) * 6f;
					}
				}
				else if (base.frame <= 5)
				{
					if (!this.isInQuicksand)
					{
						this.xI = this.speed * 0.3f * base.transform.localScale.x;
					}
					this.ApplyFallingGravity();
				}
				else
				{
					this.ApplyFallingGravity();
				}
			}
			else if (base.Y > this.groundHeight + 1f)
			{
				this.CancelMelee();
			}
		}
    }
}
