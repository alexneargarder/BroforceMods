using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BroMakerLib;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using HarmonyLib;
using System.IO;

namespace Captain_Ameribro_Mod
{

    class Shield : Projectile
    {
        public AnimatedTexture texture;
		public float returnTime = 0.25f;
		public float holdAtApexDuration = -1f;
		protected bool hasReachedApex;
		protected float holdAtApexTime;
		public float returnLerpSpeed = 3f;
		protected float shieldSpeed;
		//public float rotationSpeed = 2f;
		public float rotationSpeed = 4.0f;
		protected float hitUnitsDelay;
		protected int hitUnitsCount;
		protected bool dropping;
		//public BoxCollider shieldCollider;
		public SphereCollider shieldCollider;
		public float shieldLoopPitchM = 1f;
		protected float collectDelayTime = 0.1f;
		protected float windCounter;
		protected int windCount;
		public float windRotationSpeedM = 1f;
		protected bool stuck;
		protected float xStart;
		protected float lastXI;
		public float bounceXM = 0.5f;
		public float bounceYM = 0.33f;
		public float frictionM = 0.4f;
		public float bounceVolumeM = 0.25f;
		public float heightOffGround = 8f;
		protected List<Unit> alreadyHit = new List<Unit>();
		//protected int hitCount;

		// Charging Variables
		public float shieldCharge = 0f;
		protected const float ChargeReturnScaler = 0.5f;
		public const float ChargeSpeedScaler = 100f;

		// Homing Variables
		protected float targetAngle;
		protected float targetX;
		protected float targetY;
		public float seekRange = 80f;
		protected bool foundMook = false;
		protected float originalSpeed;
		protected float seekSpeedCurrent;
		protected float speed;
		public float seekTurningSpeedLerpM = 4f;
		public float seekTurningSpeedM = 1f;
		protected float angle;
		protected float seekCounter;

		public static Material storedMat;

		protected override void Awake()
        {
            Boomerang boom = (HeroController.GetHeroPrefab(HeroType.BroMax) as BroMax).boomerang as Boomerang;

            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

			if ( storedMat == null )
            {
				storedMat = new Material(boom.GetComponent<MeshRenderer>().material);
				storedMat.mainTexture = ResourcesController.CreateTexture(".\\Mods\\Development - BroMaker\\Storage\\Bros\\Captain Ameribro", "captainAmeribroShield.png");
				//storedMat.mainTexture = ResourcesController.CreateTexture(Main.ExtractResource("Captain_Ameribro_Mod.Sprites.captainAmeribroShieldPlaceholder.png"));
				//BMLogger.Log("current dir: " + Directory.GetCurrentDirectory());
			}

			renderer.material = storedMat;

			SpriteSM sprite = this.gameObject.GetComponent<SpriteSM>();
			sprite.lowerLeftPixel = new Vector2(0, 16);
			sprite.pixelDimensions = new Vector2(13, 13);

			sprite.plane = SpriteBase.SPRITE_PLANE.XY;
			sprite.width = 16;
			sprite.height = 16;
			//sprite.width = 1;
			//sprite.height = 1;
			sprite.offset = new Vector3(0, 0, 0);
			//sprite.CalcEdges();
			//sprite.CalcSize();
			//sprite.offset = new Vector3(0, 0, 0);

			//texture.paused = false;

/*			SpriteSM boomSprite = boom.GetComponent<SpriteSM>();
			BMLogger.Log("lowerleft: " + boomSprite.lowerLeftPixel);
			BMLogger.Log("pixelDimensions: " + boomSprite.pixelDimensions);
			BMLogger.Log("plane: " + boomSprite.plane);
			BMLogger.Log("width: " + boomSprite.width);
			BMLogger.Log("height: " + boomSprite.height);
			BMLogger.Log("offset: " + boomSprite.offset);*/



			base.Awake();
        }

        public override void Fire(float x, float y, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy)
        {
            
            this.shieldSpeed = xI;
            if (xI > 0f)
            {
                this.rotationSpeed *= -1f;
            }
			this.shieldCollider.transform.parent = base.transform.parent;

			//Calling projectile base fire
			//base.Fire( x, y, X, yI, _zOffset, playerNum, FiredBy);
			this.t = Time.deltaTime;
            this.damageInternal = this.damage;
            this.fullLife = this.life;
            this.fullDamage = this.damage;
            this.SetXY(x, y);
            this.xI = xI;
            this.yI = yI;

			// Init targetting variables
			this.speed = this.xI;
			this.originalSpeed = this.speed;
			if (xI > 0f)
			{
				this.angle = 1.57079637f;
			}
			else
			{
				this.angle = -1.57079637f;
			}
			this.targetAngle = this.angle;

			this.playerNum = playerNum;
            this.SetPosition();
            this.SetRotation();
            this.firedBy = FiredBy;
            Vector3 vector = new Vector3(xI, yI, 0f);
            this.startProjectileSpeed = vector.magnitude;
            if (playerNum >= 0 && playerNum <= 3)
            {
                ScaleProjectileWithPerks component = base.GetComponent<ScaleProjectileWithPerks>();
                if (component != null)
                {
                    component.Setup(this);
                }
            }
            this.CheckSpawnPoint();
            this.zOffset = _zOffset;
            this.CheckFriendlyFireMaterial();
            // End projectile base fire

            base.gameObject.AddComponent<AudioSource>();
            base.GetComponent<AudioSource>().playOnAwake = false;
            base.GetComponent<AudioSource>().rolloffMode = AudioRolloffMode.Linear;
            base.GetComponent<AudioSource>().minDistance = 100f;
            base.GetComponent<AudioSource>().dopplerLevel = 0.02f;
            base.GetComponent<AudioSource>().maxDistance = 220f;
            base.GetComponent<AudioSource>().spatialBlend = 1f;
            base.GetComponent<AudioSource>().volume = this.soundVolume;
            base.GetComponent<AudioSource>().loop = true;
            base.GetComponent<AudioSource>().clip = this.soundHolder.specialSounds[UnityEngine.Random.Range(0, this.soundHolder.specialSounds.Length)];
            base.GetComponent<AudioSource>().Play();
            Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.effortSounds, 0.4f, base.transform.position, 1.15f + UnityEngine.Random.value * 0.1f, true, false, false, 0f);
            this.xStart = x - Mathf.Sign(xI) * 48f;
            this.lastXI = xI;
        }

       public void assignNullValues( Boomerang other )
        {
            Traverse boomTraverse = Traverse.Create(other);
            //this.shieldCollider = other.boomerangCollider;
            this.soundVolume = other.soundVolume;
            this.soundHolder = other.soundHolder;
        }
        public void setup( Shield other )
        {
            //this.shieldCollider = BoxCollider.Instantiate(other.shieldCollider);
            //this.shieldCollider.transform = other.transform;
            //base.transform.parent = other.transform;
            //this.soundHolder = SoundHolder.Instantiate(other.soundHolder);

            this.texture = this.gameObject.GetComponent<AnimatedTexture>();

			//base.transform.localScale = new Vector3(-1f, 3f, 3f);

			//shieldCollider.transform.localScale = new Vector3(5, 5, 5);

            base.transform.eulerAngles = new Vector3(0f, 0f, 0f);

            this.enabled = true;

			this.texture.frames = 1;

			this.texture.frame = 0;

			this.returnTime += ChargeReturnScaler * this.shieldCharge;
        }

		protected override void CheckSpawnPoint()
		{
			Collider[] array = Physics.OverlapSphere(new Vector3(base.X, base.Y, 0f), 5f, this.groundLayer);
			if (array.Length > 0)
			{
				this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
				for (int i = 0; i < array.Length; i++)
				{
					this.ProjectileApplyDamageToBlock(array[i].gameObject, this.damageInternal, this.damageType, this.xI, this.yI);
				}
				this.returnTime = 0f;
				this.xI = 0f;
			}
			this.RegisterProjectile();
			this.CheckReturnZones();
			if ((this.canReflect && this.playerNum >= 0 && this.horizontalProjectile && Physics.Raycast(new Vector3(base.X - Mathf.Sign(this.xI) * this.projectileSize * 2f, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize * 3f, this.barrierLayer)) || (!this.horizontalProjectile && Physics.Raycast(new Vector3(base.X, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize + this.startProjectileSpeed * this.t, this.barrierLayer)))
			{
				this.ReflectProjectile(this.raycastHit);
			}
			else if ((this.canReflect && this.playerNum < 0 && this.horizontalProjectile && Physics.Raycast(new Vector3(base.X - Mathf.Sign(this.xI) * this.projectileSize * 2f, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize * 3f, this.friendlyBarrierLayer)) || (!this.horizontalProjectile && Physics.Raycast(new Vector3(base.X, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize + this.startProjectileSpeed * this.t, this.friendlyBarrierLayer)))
			{
				this.ReflectProjectile(this.raycastHit);
			}
			this.CheckSpawnPointFragile();
		}

		protected override void RunProjectile(float t)
		{
			base.RunProjectile(t);
			this.returnTime -= t;
			this.collectDelayTime -= t;
/*			if (this.holdAtApexTime > 0f)
			{
				this.holdAtApexTime -= t;
				this.CheckReturnShield();
				if (this.holdAtApexTime <= 0f)
				{
					this.ApplyReverseForce(t);
				}
			}*/
			if (this.returnTime <= 0f)
			{
				if (!this.dropping)
				{
					if (Mathf.Sign(this.shieldSpeed) == Mathf.Sign(this.xI))
					{
						this.ApplyReverseForce(t);
						if (!this.hasReachedApex && Mathf.Sign(this.shieldSpeed) != Mathf.Sign(this.xI))
						{
							this.hasReachedApex = true;
							this.holdAtApexTime = this.holdAtApexDuration;
							this.xI = 0f;
						}
					}
					else
					{
						this.ApplyReverseForce(t);
					}
				}
				this.CheckReturnShield();
				if (!this.dropping)
				{
					float f = this.xStart - base.X;
					if (Mathf.Sign(this.shieldSpeed) == Mathf.Sign(f))
					{
						this.dropping = true;
						this.collectDelayTime = 0f;
						base.GetComponent<AudioSource>().Stop();
						this.xI *= 0.66f;
						this.shieldCollider.enabled = false;
					}
				}
			}
			if (!this.dropping)
			{
				float num = 140f + Mathf.Abs(this.xI) * 0.5f;
				if (this.shieldSpeed != 0f && Time.timeScale > 0f)
				{
					float pitch = Mathf.Clamp(num / Mathf.Abs(this.shieldSpeed) * 1.2f * this.shieldLoopPitchM, 0.5f * this.shieldLoopPitchM, 1f * this.shieldLoopPitchM) * Time.timeScale;
					base.GetComponent<AudioSource>().pitch = pitch;
				}
				base.transform.Rotate(0f, 0f, num * this.rotationSpeed * t, Space.Self);
				this.windCounter += t;
				if (this.windCounter > 0.0667f)
				{
					this.windCount++;
					this.windCounter -= 0.0667f;
					EffectsController.CreateBoomerangWindEffect(base.X, base.Y, 5f, 0f, 0f, base.transform, 0f, (float)(this.windCount * 27) * this.rotationSpeed);
				}
			}
			else
			{
				base.transform.Rotate(0f, 0f, this.rotationSpeed * t, Space.Self);
			}
			if (Mathf.Sign(this.lastXI) != Mathf.Sign(this.xI) || this.holdAtApexTime > 0f)
			{
				this.alreadyHit.Clear();
			}
			this.lastXI = this.xI;
		}

		private void ApplyReverseForce(float t)
		{
			this.xI -= this.shieldSpeed * t * this.returnLerpSpeed;
			this.xI = Mathf.Clamp(this.xI, -Mathf.Abs(this.shieldSpeed), Mathf.Abs(this.shieldSpeed));
		}

		protected override void HitProjectiles()
		{
			if (Map.HitProjectiles(this.playerNum, this.damageInternal, this.damageType, this.projectileSize, base.X, base.Y, this.xI, this.yI, 0.1f))
			{
				this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			if (this.shieldCollider != null && this.shieldCollider.gameObject != null)
			{
				UnityEngine.Object.Destroy(this.shieldCollider.gameObject);
			}
		}

		protected override void MoveProjectile()
		{
			if (!this.stuck)
			{
				/*this.RunSeeking();
				if (this.targetAngle > this.angle + 3.14159274f)
				{
					this.angle += 6.28318548f;
				}
				else if (this.targetAngle < this.angle - 3.14159274f)
				{
					this.angle -= 6.28318548f;
				}
				if (this.reversing)
				{
					if (this.IsHeldByZone())
					{
						this.speed *= 1f - this.t * 8f;
					}
					else
					{
						this.speed = Mathf.Lerp(this.speed, this.originalSpeed, this.t * 8f);
					}
				}
				else
				{
					this.speed = Mathf.Lerp(this.speed, this.originalSpeed, this.t * 10f);
				}
				this.seekSpeedCurrent = Mathf.Lerp(this.seekSpeedCurrent, this.seekTurningSpeedM, this.seekTurningSpeedLerpM * this.t);
				this.angle = Mathf.Lerp(this.angle, this.targetAngle, this.t * this.seekSpeedCurrent);
				Vector2 vector = global::Math.Point2OnCircle(this.angle, this.speed);
				this.xI = vector.x;
				this.yI = vector.y;
				this.SetRotation();*/

				base.MoveProjectile();
				this.shieldCollider.transform.position = base.transform.position;
				if (this.dropping)
				{
					this.ApplyGravity();
				}
			}
		}

		protected void RunSeeking()
		{
			if (!this.IsHeldByZone())
			{
				this.seekCounter += this.t;
				if (this.seekCounter > 0.1f)
				{
					this.seekCounter -= 0.03f;
					this.CalculateSeek();
				}
			}
		}

		protected void CalculateSeek()
		{
			if (!this.foundMook)
			{
				Unit nearestVisibleUnitDamagebleBy = Map.GetNearestVisibleUnitDamagebleBy(this.playerNum, (int)this.seekRange, base.X, base.Y, false);
				if (nearestVisibleUnitDamagebleBy != null && nearestVisibleUnitDamagebleBy.gameObject.activeInHierarchy)
				{
					this.foundMook = true;
					this.targetX = nearestVisibleUnitDamagebleBy.X;
					this.targetY = nearestVisibleUnitDamagebleBy.Y + nearestVisibleUnitDamagebleBy.height / 2f;
				}
				else
				{
					this.targetX = base.X + this.xI;
					this.targetY = base.Y + this.yI;
				}
			}
			float y = this.targetX - base.X;
			float x = this.targetY - base.Y;
			this.targetAngle = global::Math.GetAngle(x, y);
		}

		protected override bool HitWalls()
		{
			if (this.xI < 0f)
			{
				if (Physics.Raycast(new Vector3(base.X + 4f, base.Y + 4f, 0f), Vector3.left, out this.raycastHit, 6f + this.heightOffGround, this.groundLayer) || Physics.Raycast(new Vector3(base.X + 4f, base.Y - 4f, 0f), Vector3.left, out this.raycastHit, 6f + this.heightOffGround, this.groundLayer))
				{
					this.collectDelayTime = 0f;
					if (Mathf.Abs(this.xI) > Mathf.Abs(this.shieldSpeed) * 0.33f)
					{
						EffectsController.CreateSuddenSparkShower(this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f, this.sparkCount / 2, 2f, 40f + UnityEngine.Random.value * 80f, this.raycastHit.normal.x * (100f + UnityEngine.Random.value * 210f), this.raycastHit.normal.y * 120f + (-60f + UnityEngine.Random.value * 350f), 0.2f);
					}
					this.xI *= -this.bounceXM;
					if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null)
					{
						this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(1, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));
						if (this.returnTime > 0f)
						{
							this.returnTime = 0f;
						}
						else if (!this.dropping && this.shieldSpeed > 0f)
						{
							this.StartDropping();
							this.yI += 80f;
						}
					}
					else if (!this.hasReachedApex)
					{
						this.xI = 0f;
						this.hasReachedApex = true;
						this.holdAtApexTime = this.holdAtApexDuration;
					}
					this.PlayBounceSound();
				}
			}
			else if (this.xI > 0f && (Physics.Raycast(new Vector3(base.X - 4f, base.Y + 4f, 0f), Vector3.right, out this.raycastHit, 4f + this.heightOffGround, this.groundLayer) || Physics.Raycast(new Vector3(base.X - 4f, base.Y - 4f, 0f), Vector3.right, out this.raycastHit, 4f + this.heightOffGround, this.groundLayer)))
			{
				this.collectDelayTime = 0f;
				if (Mathf.Abs(this.xI) > Mathf.Abs(this.shieldSpeed) * 0.33f)
				{
					EffectsController.CreateSuddenSparkShower(this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f, this.sparkCount / 2, 2f, 40f + UnityEngine.Random.value * 80f, this.raycastHit.normal.x * (100f + UnityEngine.Random.value * 210f), this.raycastHit.normal.y * 120f + (-60f + UnityEngine.Random.value * 350f), 0.2f);
				}
				this.xI *= -this.bounceXM;
				if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null)
				{
					this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(1, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));
					if (this.returnTime > 0f)
					{
						this.returnTime = 0f;
					}
					else if (!this.dropping && this.shieldSpeed < 0f)
					{
						this.StartDropping();
						this.yI += 80f;
					}
				}
				else if (!this.hasReachedApex)
				{
					this.xI = 0f;
					this.hasReachedApex = true;
					this.holdAtApexTime = this.holdAtApexDuration;
				}
				this.PlayBounceSound();
			}
			if (this.dropping)
			{
				if (this.yI < 0f)
				{
					if (Physics.Raycast(new Vector3(base.X, base.Y + 6f, 0f), Vector3.down, out this.raycastHit, 6f + this.heightOffGround - this.yI * this.t, this.groundLayer))
					{
						if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null && this.yI < -30f)
						{
							this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(0, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));
						}
						this.xI *= this.frictionM;
						if (this.yI < -40f)
						{
							this.yI *= -this.bounceYM;
						}
						else
						{
							this.yI = 0f;
							base.Y = this.raycastHit.point.y + this.heightOffGround;
						}
						this.rotationSpeed = -25f * this.xI;
						this.PlayBounceSound();
					}
				}
				else if (this.yI > 0f && Physics.Raycast(new Vector3(base.X, base.Y - 6f, 0f), Vector3.up, out this.raycastHit, 6f + this.heightOffGround + this.yI * this.t, this.groundLayer))
				{
					if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null)
					{
						this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(0, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));
					}
					this.yI *= -(this.bounceYM + 0.1f);
					this.PlayBounceSound();
					this.rotationSpeed = -25f * this.xI;
				}
			}
			return true;
		}

		protected void StartDropping()
		{
			this.dropping = true;
			this.collectDelayTime = 0f;
			this.rotationSpeed = -25f * this.xI;
			base.GetComponent<AudioSource>().Stop();
			this.shieldCollider.enabled = false;
		}

		protected void PlayBounceSound()
		{
			float num = Mathf.Abs(this.xI) + Mathf.Abs(this.yI);
			if (num > 33f)
			{
				float num2 = num / 210f;
				float num3 = 0.05f + Mathf.Clamp(num2 * num2, 0f, 0.25f);
				Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.hitSounds, num3 * this.bounceVolumeM, base.transform.position, 1f, true, false, false, 0f);
			}
		}

		protected override void RunLife()
		{
		}

		protected override void Bounce(RaycastHit raycastHit)
		{
			if (this.returnTime > 0f)
			{
				this.xI = 0f;
				this.returnTime = 0f;
			}
			else if (!this.dropping)
			{
				this.StartDropping();
			}
		}

		protected override void HitUnits()
		{
			if (this.hitUnitsDelay > 0f)
			{
				this.hitUnitsDelay -= this.t;
			}
			else if (!this.dropping)
			{
				if (this.hasReachedApex && this.holdAtApexTime > 0f && MapController.DamageGround(this, 1, DamageType.Normal, 6f, base.X, base.Y, null, false))
				{
					EffectsController.CreateSparkParticles(base.X, base.Y, 1f, 3, 2f, 10f, 0f, 0f, UnityEngine.Random.value, 1f);
					this.holdAtApexTime = Mathf.Clamp(this.holdAtApexTime -= this.t * 3f, this.t, this.holdAtApexTime);
				}
				if (this.reversing)
				{
					if (Map.HitLivingUnits(this, this.playerNum, this.damageInternal, this.damageType, this.projectileSize, this.projectileSize, base.X, base.Y, this.xI, this.yI, true, false, true, false))
					{
						this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
						this.hitUnitsDelay = 0.0667f;
						if (this.returnTime > 0f)
						{
							this.returnTime = 0f;
						}
						if (Mathf.Sign(this.xI) == Mathf.Sign(this.shieldSpeed))
						{
							this.xI *= 0.66f;
						}
						if (this.holdAtApexTime > 0f)
						{
							this.holdAtApexTime -= 0.2f;
						}
						this.hitUnitsCount++;
					}
				}
				else if (Map.HitUnits(this.firedBy, this.playerNum, this.damageInternal, 1, this.damageType, this.projectileSize, this.projectileSize * 1.3f, base.X, base.Y, this.xI, this.yI, true, false, true, this.alreadyHit, false, true))
				{
					this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
					this.hitUnitsDelay = 0.0667f;
					if (this.returnTime > 0f)
					{
						this.returnTime = 0f;
					}
					if (Mathf.Sign(this.xI) == Mathf.Sign(this.shieldSpeed))
					{
						this.xI *= 0.66f;
					}
					if (this.holdAtApexTime > 0f)
					{
						this.holdAtApexTime -= 0.2f;
					}
					this.hitUnitsCount++;
				}
			}
		}
		protected override void HitWildLife()
		{
		}
		protected void CheckReturnShield()
		{
			if (this.firedBy != null && (this.collectDelayTime <= 0f || this.hitUnitsCount > 2))
			{
				float f = this.firedBy.transform.position.x - base.X;
				float f2 = this.firedBy.transform.position.y + 10f - base.Y;
				if (Mathf.Abs(f) < 9f && Mathf.Abs(f2) < 14f)
				{
					Shield.TryReturnRPC(this);
					if (base.IsMine)
					{
						PID targetOthers = PID.TargetOthers;
						bool immediate = false;
						bool ignoreSessionID = false;
						bool addExecutionDelay = true;
/*						if (Boomerang.<> f__mg$cache0 == null)
					{
							Boomerang.<> f__mg$cache0 = new RpcSignature<Boomerang>(Boomerang.TryReturnRPC);
						}
						Networking.RPC<Boomerang>(targetOthers, immediate, ignoreSessionID, addExecutionDelay, Boomerang.<> f__mg$cache0, this);*/
					}
				}
			}
		}
		private static void TryReturnRPC(Shield shield)
		{
			if (shield != null)
			{
				shield.ReturnShield();
			}
		}
		private void ReturnShield()
		{
			CaptainAmeribro captainAmeribro = this.firedBy as CaptainAmeribro;
			if (captainAmeribro)
			{
				captainAmeribro.ReturnShield(this);
			}
			Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.powerUp, 0.7f, base.transform.position, 0.95f + UnityEngine.Random.value * 0.1f, true, false, false, 0f);
			this.DeregisterProjectile();
			UnityEngine.Object.Destroy(base.gameObject);
		}

		protected virtual void ApplyGravity()
		{
			this.yI -= 600f * this.t;
		}

		public override void Death()
		{
		}
	}
}
