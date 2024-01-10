using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using System.IO;
using System.Reflection;

namespace Captain_Ameribro_Mod
{
    [HeroPreset("Captain Ameribro", HeroType.Nebro)]
    class CaptainAmeribro : SwordHero
    {
		// Sprite variables
		public Material materialNormal, materialNormalShield, materialNormalNoShield, materialArmless;
		public Material gunMaterialNormal, gunMaterialNoShield;
		protected bool wasInvulnerable = false;

		// Audio variables
		public static AudioClip[] shieldUnitBounce;
		public static AudioClip shieldChargeShing;
		public static AudioClip[] shieldMeleeSwing;
		public static AudioClip[] shieldMeleeHit;
		public static AudioClip[] shieldMeleeTerrain;
		public static AudioClip airDashSound;
		public static AudioClip[] effortSounds;
		public static AudioClip[] ricochetSounds;
		public static AudioClip[] pistolSounds;
		public int currentMeleeSound = 0;
		public float sliceVolume = 0.7f;
		public float wallHitVolume = 0.6f;

		// Special variables
		protected Shield shield;
		protected Shield thrownShield;
		public const float shieldSpeed = 400f;
		protected bool grabbingShield;
		protected int grabbingFrame;
		protected int specialFrame = 0;
		protected float specialFrameRate = 0.0334f;
		protected float specialFrameCounter = 0f;
		protected bool animateSpecial = false;
		protected float specialAttackDashCounter;
		protected bool isHoldingSpecial = false;
		protected float maxSpecialCharge = 1f;
		public float currentSpecialCharge = 0f;
		public bool playedShingNoise = false;

		// Default attack variables
		protected int punchingIndex = 0;
		protected const int normalAttackDamage = 6;
		protected bool heldGunFrame = false;

		// Melee variables
		protected const int meleeAttackDamage = 7;
		public float specialAttackDashTime = 0f;
		protected float airdashFadeCounter;
		public float airdashFadeRate = 0.1f;
		protected bool usingShieldMelee = false;
		protected Projectile pistolBullet;
		protected float airDashCooldown = 0f;

		// Misc Variables
		protected List<Unit> currentlyHitting;
		protected const int defaultSpeed = 130;

		// DEBUG variables
		public const bool DEBUGTEXTURES = true;
		public int frameCount = 0;

		public void makeTextBox(string label, ref string text, ref float val)
        {
			GUILayout.BeginHorizontal();
			GUILayout.Label(label);
			text = GUILayout.TextField(text);
			GUILayout.EndHorizontal();

			float.TryParse(text, out val);
        }

		public void makeTextBox(string label, ref string text, ref int val)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label);
			text = GUILayout.TextField(text);
			GUILayout.EndHorizontal();

			int.TryParse(text, out val);
		}

		public override void UIOptions()
		{
		}

		protected override void Awake()
		{
			shield = new GameObject("CaptainAmeribroShield", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(AnimatedTexture), typeof(Shield), typeof(SphereCollider) }).GetComponent<Shield>();
			shield.enabled = false;
			shield.shieldCollider = shield.gameObject.GetComponent<SphereCollider>();

			Boomerang boom = (HeroController.GetHeroPrefab(HeroType.BroMax) as BroMax).boomerang as Boomerang;
			shield.AssignNullValues(boom);

			this.currentMeleeType = BroBase.MeleeType.Disembowel;
			this.meleeType = BroBase.MeleeType.Disembowel;

			pistolBullet = (HeroController.GetHeroPrefab(HeroType.DoubleBroSeven) as DoubleBroSeven).projectile;

			base.Awake();
		}

		protected override void Start()
        {
            base.Start();

			materialNormal = this.material;
			materialNormalShield = materialNormal;

			materialNormalNoShield = new Material(this.material);

			string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

			if ( !DEBUGTEXTURES )
            {
				materialNormalNoShield.mainTexture = ResourcesController.CreateTexture(Main.ExtractResource("Captain_Ameribro_Mod.Sprites.captainAmeribroMainNoShield.png"));
			}
			else
            {
				materialNormalNoShield.mainTexture = ResourcesController.CreateTexture(directoryPath, "captainAmeribroMainNoShield.png");
			}

			materialArmless = new Material((HeroController.GetHeroPrefab(HeroType.Nebro) as Nebro).materialArmless);
            if (!DEBUGTEXTURES)
            {
				materialArmless.mainTexture = ResourcesController.CreateTexture(Main.ExtractResource("Captain_Ameribro_Mod.Sprites.captainAmeribroArmless.png"));
			}
			else
            {
				materialArmless.mainTexture = ResourcesController.CreateTexture(directoryPath, "captainAmeribroArmless.png");
			}

			gunMaterialNormal = this.gunMaterial;

			gunMaterialNoShield = new Material(gunMaterial);
			if ( !DEBUGTEXTURES )
            {
				gunMaterialNoShield.mainTexture = ResourcesController.CreateTexture(Main.ExtractResource("Captain_Ameribro_Mod.Sprites.captainAmeribroGunNoShield.png"));
			}
			else
            {
				gunMaterialNoShield.mainTexture = ResourcesController.CreateTexture(directoryPath, "captainAmeribroGunNoShield.png");
			}

			if ( shieldUnitBounce == null )
            {
				shieldUnitBounce = new AudioClip[3];
				shieldUnitBounce[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "special1.wav");
				shieldUnitBounce[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "special2.wav");
				shieldUnitBounce[2] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "special3.wav");
			}
			
			if ( shieldChargeShing == null )
            {
				shieldChargeShing = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "ShieldShing.wav");
			}

			if ( shieldMeleeSwing == null )
            {
				shieldMeleeSwing = new AudioClip[2];
				shieldMeleeSwing[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "melee1part1.wav");
				shieldMeleeSwing[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "melee3part1.wav");
			}

			if ( shieldMeleeHit == null )
            {
				shieldMeleeHit = new AudioClip[2];
				shieldMeleeHit[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "melee1part2.wav");
				shieldMeleeHit[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "melee3part2.wav");
			}
		
			if ( shieldMeleeTerrain == null )
            {
				shieldMeleeTerrain = new AudioClip[2];
				shieldMeleeTerrain[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "meleeterrainhit1.wav");
				shieldMeleeTerrain[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "meleeterrainhit2.wav");
			}
			
			if ( airDashSound ==  null )
            {
				airDashSound = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "swish.wav");
			}

			if (effortSounds == null)
            {
				effortSounds = new AudioClip[5];
				effortSounds[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "grunt1.wav");
				effortSounds[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "grunt2.wav");
				effortSounds[2] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "grunt3.wav");
				effortSounds[3] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "grunt4.wav");
				effortSounds[4] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "grunt5.wav");
			}

			if (ricochetSounds == null)
            {
				ricochetSounds = new AudioClip[4];
				ricochetSounds[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "ricochet1.wav");
				ricochetSounds[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "ricochet2.wav");
				ricochetSounds[2] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "ricochet3.wav");
				ricochetSounds[3] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "ricochet4.wav");
			}

			if (pistolSounds == null)
            {
				pistolSounds = new AudioClip[4];
				pistolSounds[0] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "pistol1.wav");
				pistolSounds[1] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "pistol2.wav");
				pistolSounds[2] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "pistol3.wav");
				pistolSounds[3] = ResourcesController.CreateAudioClip(Path.Combine(directoryPath, "sounds"), "pistol4.wav");
            }
		}

		protected override void Update()
		{
			if ( this.invulnerable )
            {
				this.wasInvulnerable = true;
            }
			base.Update();
			// Check if invulnerability ran out
			if ( this.wasInvulnerable && !this.invulnerable )
            {
				materialNormalShield.SetColor("_TintColor", Color.gray);
				materialNormalNoShield.SetColor("_TintColor", Color.gray);
				materialArmless.SetColor("_TintColor", Color.gray);
				gunMaterialNormal.SetColor("_TintColor", Color.gray);
				gunMaterialNoShield.SetColor("_TintColor", Color.gray);
			}

			// Charge special
			if (isHoldingSpecial)
			{
				currentSpecialCharge += this.t;
			}

			// Keep track of special frames
			this.specialCounter += this.t;
			if (this.specialCounter > this.specialFrameRate)
			{
				if (this.animateSpecial)
				{
					this.AnimateSpecial();
				}
				this.specialCounter -= this.specialFrameRate;
				++this.specialFrame;
			}

			// Make shield drop on death
			if (base.actionState == ActionState.Dead && thrownShield != null && !thrownShield.dropping)
			{
				thrownShield.StartDropping();
			}

			// Reflect projectiles if using melee
			if ( this.doingMelee && this.usingShieldMelee && base.frame > 1 )
            {
				if (Map.DeflectProjectiles(this, this.playerNum, 10, this.X, this.Y, this.transform.localScale.x * 300, true))
				{
					Sound.GetInstance().PlaySoundEffectAt(ricochetSounds, 0.6f, base.transform.position, 1f, true, false, false, 0f);
				}
			}

			if ( airDashCooldown > 0 )
            {
				airDashCooldown -= this.t;
            }

			// DEBUG
			if (this.frameCount == 120)
			{
				this.frameCount = 0;
			}
			++this.frameCount;
		}

        protected override void ChangeFrame()
        {
			// Cancel holding special if doing any animation that won't work for the armless sprite
			if ( this.animateSpecial && (this.chimneyFlip || this.wallClimbing || this.WallDrag || (base.actionState == ActionState.Dead) || 
				(this.inseminatorUnit != null) || (this.impaledByTransform != null && this.useImpaledFrames) || this.attachedToZipline != null ) )
            {
				this.CancelSpecial();
            }
            base.ChangeFrame();
			if ( this.doingMelee && !this.usingShieldMelee )
            {
				base.frameRate = 0.05f;
			}
		}

		protected override void AnimateWallAnticipation()
        {
			// Cancel holding special if we are about to grab a wall
			if ( this.animateSpecial )
            {
				this.CancelSpecial();
            }
            base.AnimateWallAnticipation();
        }

        protected override void UseSpecial()
        {
			if ( this.SpecialAmmo > 0 && !isHoldingSpecial )
            {
				if ( this.currentSpecialCharge > this.maxSpecialCharge )
                {
					this.currentSpecialCharge = this.maxSpecialCharge;
                }

				this.materialNormal = this.materialNormalNoShield;
				this.gunSprite.meshRender.material = this.gunMaterialNoShield;

				float chargedShieldSpeed = shieldSpeed + Shield.ChargeSpeedScalar * this.currentSpecialCharge;

				if ( Physics.Raycast(this.transform.position, Vector3.up, out this.raycastHit, 22, this.groundLayer) )
                {
					thrownShield = ProjectileController.SpawnProjectileLocally(this.shield, this, base.X + base.transform.localScale.x * 6f, base.Y + 10f, base.transform.localScale.x * chargedShieldSpeed, 0f, false, base.playerNum, false, false, 0f) as Shield;
				}
				else
                {
					thrownShield = ProjectileController.SpawnProjectileLocally(this.shield, this, base.X + base.transform.localScale.x * 6f, base.Y + 15f, base.transform.localScale.x * chargedShieldSpeed, 0f, false, base.playerNum, false, false, 0f) as Shield;
				}

				thrownShield.Setup(this.shield, this);

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
			if ( this.doingMelee )
            {
				this.doingMelee = false;
				this.jumpingMelee = false;
				this.dashingMelee = false;
				this.standingMelee = false;
				this.meleeFollowUp = false;
				this.meleeChosenUnit = null;
				this.counter = 0f;
				this.hasPlayedMissSound = false;
				if (base.actionState != ActionState.ClimbingLadder)
				{
					if (base.Y > this.groundHeight)
					{
						base.actionState = ActionState.Jumping;
					}
					else if (this.left || this.right)
					{
						base.actionState = ActionState.Running;
					}
					else
					{
						this.SetActionstateToIdle();
					}
				}
			}
			if (!this.usingSpecial)
			{
				this.usingSpecial = true;
				this.grabbingFrame = 0;
				this.grabbingShield = true;
				this.ChangeFrame();
			}
		}

        // Make shield drop if character is destroyed
        protected override void OnDestroy()
        {
			if (thrownShield != null && !thrownShield.dropping)
			{
				thrownShield.StartDropping();
			}
			base.OnDestroy();
        }

        protected override void PressSpecial()
        {
			// Don't start holding special unless we actually have a shield to prevent shield from charging
			if ( this.SpecialAmmo > 0 && !(this.wallClimbing || this.wallDrag || this.attachedToZipline != null || this.IsGesturing() || this.frontSomersaulting) )
            {
				if (!this.hasBeenCoverInAcid && !this.doingMelee)
				{
					this.speed = 80;
					this.isHoldingSpecial = true;
					this.specialFrame = 0;
					this.specialFrameRate = 0.05f;
					this.specialCounter = this.specialFrameRate;
					this.animateSpecial = true;
					this.playedShingNoise = false;
					base.frame = 0;
					this.pressSpecialFacingDirection = (int)base.transform.localScale.x;
				}
			}
		}

		protected override void ReleaseSpecial()
        {
			this.isHoldingSpecial = false;
			base.ReleaseSpecial();
        }

		protected void CancelSpecial()
        {
			this.isHoldingSpecial = false;
			this.speed = defaultSpeed;
			this.animateSpecial = false;
			this.currentSpecialCharge = 0f;
			this.gunFrame = 0;
			if (!this.hasBeenCoverInAcid)
			{
				base.GetComponent<Renderer>().material = this.materialNormal;
			}
		}

        public override void AttachToZipline(ZipLine zipLine)
        {
			if ( this.animateSpecial)
            {
                this.CancelSpecial();
            }
            base.AttachToZipline(zipLine);
        }

        public override void SetGestureAnimation(GestureElement.Gestures gesture)
        {
			if ( this.animateSpecial && gesture != GestureElement.Gestures.None )
            {
				this.CancelSpecial();
            }
            base.SetGestureAnimation(gesture);
        }

        protected override void AnimateSpecial()
		{
			if (this.grabbingShield)
			{
				this.SetSpriteOffset(0f, 0f);
				this.DeactivateGun();
				this.frameRate = 0.05f;
				this.sprite.SetLowerLeftPixel((float)((26 + this.grabbingFrame) * this.spritePixelWidth), (float)(this.spritePixelHeight * 5));
				++this.grabbingFrame;
				if (this.grabbingFrame > 3)
				{
					base.frame = 0;
					this.usingSpecial = false;
					this.grabbingShield = false;
				}
			}
			else
			{
				if (!this.hasBeenCoverInAcid)
				{
					base.GetComponent<Renderer>().material = this.materialArmless;
				}
				if ( isHoldingSpecial && this.SpecialAmmo > 0 )
                {
					if (this.specialFrame > 2 && this.currentSpecialCharge < 0.25f)
					{
						this.specialFrameRate = 0.05f;
						this.specialFrame = 2;
					}
					else if ( this.specialFrame > 5 && this.currentSpecialCharge < 1f )
                    {
						this.specialFrame = 3;
                    }
					else if ( this.specialFrame == 5 && this.currentSpecialCharge > 1f )
                    {
						this.specialFrame = 6;
                    }
					else if ( this.specialFrame > 5 )
                    {
						this.specialFrameRate = 0.0333f;
						this.specialFrame = 3;
					}

					if ( !this.playedShingNoise && this.currentSpecialCharge > 1f )
                    {
						Sound.GetInstance().PlaySoundEffectAt(CaptainAmeribro.shieldChargeShing, 0.3f, base.transform.position, 1f, true, false, false, 0f);
						this.playedShingNoise = true;
                    }

					this.gunSprite.SetLowerLeftPixel((float)(32 * (1 + this.specialFrame)), 128f);
				}
				else
				{
					if ( this.specialFrame > 2 && this.specialFrame < 7 )
                    {
						this.specialFrame = 7;
					}
					else if (this.specialFrame == 9)
					{
						this.UseSpecial();
						this.speed = defaultSpeed;
					}

					if (this.specialFrame >= 11)
					{
						base.frame = 0;
						this.animateSpecial = (this.usingPockettedSpecial = false);
						if (!this.hasBeenCoverInAcid)
						{
							base.GetComponent<Renderer>().material = this.materialNormal;
						}
						this.ChangeFrame();
					}
					else
                    {
						this.specialFrameRate = 0.05f;
						this.gunSprite.SetLowerLeftPixel((float)(32 * (1 + this.specialFrame)), 128f);
					}
				}
			}
		}

		// Copie from Neo
		protected override void AnimateInseminationFrames()
		{
			int num = 24 + base.CalculateInseminationFrame();
			this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)(this.spritePixelHeight * 8));
		}

		// Copied from Neo
		protected override void SetGunSprite(int spriteFrame, int spriteRow)
        {
			if ( !this.animateSpecial )
            {
				if (base.actionState == ActionState.ClimbingLadder && this.hangingOneArmed)
				{
					this.gunSprite.SetLowerLeftPixel((float)(this.gunSpritePixelWidth * (11 + spriteFrame)), (float)(this.gunSpritePixelHeight * (1 + spriteRow)));
				}
				else if (this.attachedToZipline != null && base.actionState == ActionState.Jumping)
				{
					this.gunSprite.SetLowerLeftPixel((float)(this.gunSpritePixelWidth * 11), (float)(this.gunSpritePixelHeight * 2));
				}
				else
				{
					base.SetGunSprite(spriteFrame, spriteRow);
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
			if ( !this.animateSpecial)
            {
				base.UseFire();
				this.fireDelay = 0.25f;
			}
        }

        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
		{
			if (this.attachedToZipline != null)
			{
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
			currentlyHitting = new List<Unit>();
			float num = base.transform.localScale.x * 12f;
			this.ConstrainToFragileBarriers(ref num, 16f);
			if (Physics.Raycast(new Vector3(x - Mathf.Sign(base.transform.localScale.x) * 12f, y + 5.5f, 0f), new Vector3(base.transform.localScale.x, 0f, 0f), out this.raycastHit, 18f, this.groundLayer | 1 << LayerMask.NameToLayer("FLUI")) || Physics.Raycast(new Vector3(x - Mathf.Sign(base.transform.localScale.x) * 12f, y + 10.5f, 0f), new Vector3(base.transform.localScale.x, 0f, 0f), out this.raycastHit, 19f, this.groundLayer | 1 << LayerMask.NameToLayer("FLUI")))
			{
				this.MakeEffects(this.raycastHit.point.x + base.transform.localScale.x * 4f, this.raycastHit.point.y);
				MapController.Damage_Local(this, this.raycastHit.collider.gameObject, normalAttackDamage, DamageType.Bullet, this.xI + base.transform.localScale.x * 200f, 0f, x, y);
				this.hasHitWithWall = true;
				if (Map.HitUnits(this, base.playerNum, normalAttackDamage, DamageType.Melee, 12f, x, y, base.transform.localScale.x * 250, 100f, false, true, false, this.alreadyHit, false, false))
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
				if (Map.HitUnits(this, base.playerNum, normalAttackDamage, DamageType.Melee, 10, x + base.transform.localScale.x * 0, y, base.transform.localScale.x * 250, 100f, false, true, false, this.alreadyHit, false, false))
				{
					this.hasHitWithSlice = true;
				}
				else
				{
					this.hasHitWithSlice = false;
				}
			}
		}

		protected void NormalAttackDamage(float x, float y, float xSpeed, float ySpeed)
        {
			float num = base.transform.localScale.x * 12f;
			this.ConstrainToFragileBarriers(ref num, 16f);
			if (Physics.Raycast(new Vector3(x - Mathf.Sign(base.transform.localScale.x) * 12f, y + 5.5f, 0f), new Vector3(base.transform.localScale.x, 0f, 0f), out this.raycastHit, 18f, this.groundLayer | 1 << LayerMask.NameToLayer("FLUI")) || Physics.Raycast(new Vector3(x - Mathf.Sign(base.transform.localScale.x) * 12f, y + 10.5f, 0f), new Vector3(base.transform.localScale.x, 0f, 0f), out this.raycastHit, 19f, this.groundLayer | 1 << LayerMask.NameToLayer("FLUI")))
			{
			}
			else
			{
				this.hasHitWithWall = false;
				if (Map.HitUnits(this, base.playerNum, normalAttackDamage, DamageType.Melee, 10, x + base.transform.localScale.x * 0, y, base.transform.localScale.x * 250, 100f, false, true, false, this.alreadyHit, false, false))
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
					if (this.gunCounter > 0.05f)
					{
						this.gunCounter -= 0.05f;
						this.gunFrame++;
						if (this.gunFrame > 4 && !heldGunFrame )
						{
							this.NormalAttackDamage(base.X + base.transform.localScale.x * 10f, base.Y + 6.5f, base.transform.localScale.x * 400f, (float)UnityEngine.Random.Range(-20, 20));
							if (this.hasHitWithSlice)
							{
								this.PlaySliceSound();
								this.hasHitWithSlice = false;
							}
							else if (this.hasHitWithWall)
							{
								this.PlayWallSound();
								this.hasHitWithWall = false;
							}
							this.gunFrame = 4;
							this.heldGunFrame = true;
						}
						if (this.gunFrame >= 6 && heldGunFrame)
						{
							this.gunFrame = 0;
						}
						if ( this.gunFrame == 0 && this.punchingIndex % 2 == 1 )
                        {
							this.gunSprite.SetLowerLeftPixel((float)(32 * this.gunFrame), 32f);
						}
						else
                        {
							this.SetGunFrame();
						}
						if (this.gunFrame == 2)
						{
							if (this.hasHitWithSlice)
							{
								this.PlaySliceSound();
								this.hasHitWithSlice = false;
							}
							else if (this.hasHitWithWall)
							{
								this.PlayWallSound();
								this.hasHitWithWall = false;
							}
						}
					}
				}
	/*			else if (this.currentZone != null && this.currentZone.PoolIndex != -1)
				{
					this.gunSprite.SetLowerLeftPixel(0f, 128f);
				}*/
			}
			if ( !this.animateSpecial && (!this.gunSprite.gameObject.activeSelf || this.gunFrame == 0) && !this.hasBeenCoverInAcid)
			{
				base.GetComponent<Renderer>().material = this.materialNormal;
				this.heldGunFrame = false;
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

		// Copied from Neo
        protected override void SetGunPosition(float xOffset, float yOffset)
        {
            // Fixes arms being offset from body
            this.gunSprite.transform.localPosition = new Vector3(xOffset + 4f, yOffset, -1f);
        }

		protected override void PressHighFiveMelee(bool forceHighFive = false)
		{
			if (this.right && this.CanAirDash(DirectionEnum.Right) && this.SpecialAmmo > 0 )
			{
				if (!this.wasHighFive)
				{
					this.Airdash(true);
				}
			}
			else if (this.left && this.CanAirDash(DirectionEnum.Left) && this.SpecialAmmo > 0)
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

        protected override void AirDashLeft()
        {
			if ( !this.animateSpecial && this.SpecialAmmo > 0 && this.airDashCooldown <= 0 )
            {
				if (this.attachedToZipline != null)
				{
					this.attachedToZipline.DetachUnit(this);
					this.ActivateGun();
					this.gunFrame = 0;
					this.SetGunSprite(0, 0);
				}
				this.currentlyHitting = new List<Unit>();
				base.AirDashLeft();
				this.airDashCooldown = this.airdashTime + 0.2f;
			}
        }

        protected override void AirDashRight()
        {
			if ( !this.animateSpecial && this.SpecialAmmo > 0 && this.airDashCooldown <= 0 )
			{
				if (this.attachedToZipline != null)
				{
					this.attachedToZipline.DetachUnit(this);
					this.ActivateGun();
					this.gunFrame = 0;
					this.SetGunSprite(0, 0);
				}
				this.currentlyHitting = new List<Unit>();
				base.AirDashRight();
				this.airDashCooldown = this.airdashTime + 0.2f;
			}
        }

        protected override void RunLeftAirDash()
		{
			if (this.airDashDelay > 0f)
			{
				this.airDashDelay -= this.t;
				this.yI = 0f;
				this.xI = 50f;
				base.transform.localScale = new Vector3(-1f, this.yScale, 1f);
				if (this.airDashDelay <= 0f)
				{
					this.ChangeFrame();
					this.PlayAidDashSound();
				}
			}
			else
			{
				this.SetAirDashLeftSpeed();
			}
			this.specialAttackDashCounter += this.t;
			if (this.specialAttackDashCounter > 0f)
			{
				this.specialAttackDashCounter -= 0.0333f;
				if ( Map.HitUnits(this, base.playerNum, 3, DamageType.Crush, 9f, base.X, base.Y, base.transform.localScale.x * (200 + UnityEngine.Random.value * 50f), 350, true, true, false, currentlyHitting, true, false) )
                {
					this.sound.PlaySoundEffectAt(shieldMeleeHit, 0.3f, base.transform.position, 1f, true, false, false, 0f);
				}
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
			if (this.airDashDelay > 0f)
			{
				this.airDashDelay -= this.t;
				this.yI = 0f;
				this.xI = -50f;
				base.transform.localScale = new Vector3(1f, this.yScale, 1f);
				if (this.airDashDelay <= 0f)
				{
					this.ChangeFrame();
					this.PlayAidDashSound();
				}
			}
			else
			{
				this.SetAirDashRightSpeed();
			}
			this.specialAttackDashCounter += this.t;
			if (this.specialAttackDashCounter > 0f)
			{
				this.specialAttackDashCounter -= 0.0333f;
				if (Map.HitUnits(this, base.playerNum, 3, DamageType.Crush, 9f, base.X, base.Y, base.transform.localScale.x * (200 + UnityEngine.Random.value * 50f), 350, true, true, false, currentlyHitting, true, false))
				{
					this.sound.PlaySoundEffectAt(shieldMeleeHit, 0.3f, base.transform.position, 1f, true, false, false, 0f);
				}
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

        protected override void PlayAidDashSound()
        {
			this.sound.PlaySoundEffectAt(effortSounds, 0.25f, base.transform.position, 1f, true, false, false, 0f);
			this.sound.PlaySoundEffectAt(airDashSound, 0.75f, base.transform.position, 1f, true, false, false, 0f);
		}

        // Performs melee attack
        protected void MeleeAttack(bool shouldTryHitTerrain, bool playMissSound)
        {
			bool flag;
			Map.DamageDoodads(meleeAttackDamage - 2, DamageType.Knock, base.X + (float)(base.Direction * 4), base.Y, 0f, 0f, 6f, base.playerNum, out flag, null);
			this.KickDoors(24f);
			if (Map.HitClosestUnit(this, base.playerNum, meleeAttackDamage, DamageType.Knock, 14f, 24f, base.X + base.transform.localScale.x * 8f, base.Y + 8f, base.transform.localScale.x * 300f, 600f, true, false, base.IsMine, false, true))
			{
				this.sound.PlaySoundEffectAt(shieldMeleeHit[this.currentMeleeSound], 0.5f, base.transform.position, 1f, true, false, false, 0f);
				this.meleeHasHit = true;
			}
			else if (playMissSound)
			{
				//this.sound.PlaySoundEffectAt(this.soundHolder.missSounds, 0.3f, base.transform.position, 1f, true, false, false, 0f);
			}
			this.meleeChosenUnit = null;
			if (shouldTryHitTerrain && this.TryMeleeTerrain(0, meleeAttackDamage - 2))
			{
				this.meleeHasHit = true;
				this.sound.PlaySoundEffectAt(shieldMeleeTerrain, 0.5f, base.transform.position, 1f, true, false, false, 0f);
			}
			this.TriggerBroMeleeEvent();
		}

		// Sets up melee attack
        protected override void StartCustomMelee()
        {
			if (this.animateSpecial)
			{
				return;
			}
			if (this.CanStartNewMelee())
			{
				this.usingShieldMelee = this.SpecialAmmo > 0;
				if (!(this.nearbyMook != null && this.nearbyMook.CanBeThrown()) && this.usingShieldMelee)
                {
					this.currentMeleeSound = UnityEngine.Random.Range(0, shieldMeleeSwing.Length);
					this.sound.PlaySoundEffectAt(shieldMeleeSwing[this.currentMeleeSound], 0.6f, base.transform.position, 1f, true, false, false, 0f);
				}
				base.frame = 1;
				base.counter = -0.05f;
				
				this.AnimateMelee();
			}
			else if (this.CanStartMeleeFollowUp())
			{
				this.meleeFollowUp = true;
			}
			if (!this.jumpingMelee && this.usingShieldMelee)
			{
				this.dashingMelee = true;
				this.xI = (float)base.Direction * this.speed;
			}
			this.StartMeleeCommon();
		}

		// Calls MeleeAttack
        protected override void AnimateCustomMelee()
        {
			this.AnimateMeleeCommon();
			// Shield bash
			if ( this.usingShieldMelee )
            {
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
			// Fire gun
			else
			{
				int num = 25 + Mathf.Clamp(base.frame, 0, 6);
				int num2 = 1;
				this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)(num2 * this.spritePixelHeight));
				if (base.frame == 3)
                {
					this.sound.PlaySoundEffectAt(pistolSounds, 0.6f, base.transform.position, 1f, true, false, false, 0f);
                    Projectile bullet = ProjectileController.SpawnProjectileLocally(this.pistolBullet, this, this.X + (this.transform.localScale.x * 12), this.Y + 13.5f, this.transform.localScale.x * 250, 0, base.playerNum);
                    EffectsController.CreateMuzzleFlashEffect(this.X + (this.transform.localScale.x * 14), this.Y + 13.5f, -25f, this.transform.localScale.x * 100, 0, base.transform);
				}
				if (base.frame >= 6)
				{
					base.frame = 0;
					this.CancelMelee();
				}
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
						if (!this.isInQuicksand && this.usingShieldMelee)
						{
							this.xI = this.speed * 1f * base.transform.localScale.x;
						}
						this.yI = 0f;
					}
					else if (!this.isInQuicksand && this.usingShieldMelee)
					{
						this.xI = this.speed * 0.5f * base.transform.localScale.x + (this.meleeChosenUnit.X - base.X) * 6f;
					}
				}
				else if (base.frame <= 5)
				{
					if (!this.isInQuicksand && this.usingShieldMelee)
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
