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
		protected bool hasReachedApex;
		protected float shieldSpeed;
		public float rotationSpeed = 2.0f;
		protected float hitUnitsDelay;
		protected int hitUnitsCount;
		protected bool dropping;
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
		public float frictionM = 0.5f;
		public float bounceVolumeM = 0.25f;
		public float heightOffGround = 8f;
		protected List<Unit> alreadyHit = new List<Unit>();
		protected const float groundRotationSpeed = -10.0f;

		// Charging Variables
		public float shieldCharge = 0f;
		protected const float ChargeReturnScalar = 0.5f;
		protected const float ChargeDroppingScalar = 0.5f;
		public const float ChargeSpeedScalar = 100f;
		protected const int BaseDamage = 8;

		// Homing Variables
		protected float targetAngle;
		protected float targetX;
		protected float targetY;
		protected float originalAngle;
		public float seekRange = 200f;
		protected bool foundMook = false;
		protected float originalSpeed;
		protected float seekSpeedCurrent;
		protected float speed;
		public float seekTurningSpeedLerpM = 50f;
		public float seekTurningSpeedM = 30f;
		protected float angle;
		protected float seekCounter = 0.1f;
		protected float droppingDuration = 2f;
		protected float droppingTime;
		protected bool stopSeeking = false;
		protected int bounceCount = 5;
		protected bool startedSnap = false;
		protected float startingThrowX;
		protected float startingThrowY;
		protected Vector3 startingThrowVector;
		protected bool pathBlocked = false;
		protected LayerMask ladderLayer;

		public static Material storedMat;
		public SpriteSM storedSprite;
		public CaptainAmeribro throwingPlayer;

		public int frameCount = 0;

		protected override void Awake()
        {
            Boomerang boom = (HeroController.GetHeroPrefab(HeroType.BroMax) as BroMax).boomerang as Boomerang;

            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

			if ( storedMat == null )
            {
				storedMat = new Material(boom.GetComponent<MeshRenderer>().material);
				if ( !Main.DEBUGTEXTURES )
                {
					storedMat.mainTexture = ResourcesController.CreateTexture(Main.ExtractResource("Captain_Ameribro_Mod.Sprites.captainAmeribroShieldPlaceholder.png"));
				}
				else
                {
					storedMat.mainTexture = ResourcesController.CreateTexture(".\\Mods\\Development - BroMaker\\Storage\\Bros\\Captain Ameribro", "captainAmeribroShield.png");
				}
			}

			renderer.material = storedMat;

			SpriteSM sprite = this.gameObject.GetComponent<SpriteSM>();
			sprite.lowerLeftPixel = new Vector2(0, 16);
			sprite.pixelDimensions = new Vector2(13, 13);

			sprite.plane = SpriteBase.SPRITE_PLANE.XY;
			sprite.width = 16;
			sprite.height = 16;
			sprite.offset = new Vector3(0, 0, 0);

			this.storedSprite = sprite;

			try
            {
				this.texture = this.gameObject.GetComponent<AnimatedTexture>();

				this.texture.frames = 1;

				this.texture.frame = 0;
			}
			catch ( Exception ex )
            {
				BMLogger.Log("exception getting animated texture: " + ex);
            }

			this.ladderLayer = 1 << LayerMask.NameToLayer("Ladders");

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
			this.speed = Mathf.Abs(this.xI);
			this.bounceCount = 5;
		
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
			this.originalAngle = this.angle;

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

       public void AssignNullValues( Boomerang other )
        {
            Traverse boomTraverse = Traverse.Create(other);
            //this.shieldCollider = other.boomerangCollider;
            this.soundVolume = other.soundVolume;
            this.soundHolder = other.soundHolder;
        }
        public void Setup( Shield other, CaptainAmeribro player )
        {
			this.throwingPlayer = player;

			this.shieldCharge = this.throwingPlayer.currentSpecialCharge;

			this.damageType = DamageType.Knock;

			this.damage = BaseDamage + (int)System.Math.Round(BaseDamage * this.throwingPlayer.currentSpecialCharge);

			this.damageInternal = this.damage;
			this.fullDamage = this.damage;

			this.startingThrowX = this.throwingPlayer.X;
			this.startingThrowY = this.throwingPlayer.Y;
			this.startingThrowVector = this.throwingPlayer.transform.position;

			base.transform.eulerAngles = new Vector3(0f, 0f, 0f);

            this.enabled = true;

			this.returnTime += ChargeReturnScalar * this.shieldCharge;

			this.droppingDuration += ChargeReturnScalar * this.shieldCharge;
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
			base.RunProjectile(t); // Calls move projectile
			this.returnTime -= t;
			this.collectDelayTime -= t;

			if (this.frameCount == 120)
            {
				this.frameCount = 0;
            }
			++this.frameCount;

			if ( frameCount == 30 )
            {
				BMLogger.Log("pos: " + this.X + " " + this.Y);
            }
			
			if (this.returnTime <= 0f)
			{
				if ( this.Y < -50 )
                {
					this.ReturnShield();
                }
				if (!this.dropping)
				{
					if ( !this.hasReachedApex && this.speed < (0.5 * this.originalSpeed) ) // Shield has slowed down enough to be considered at Apex
                    {
						BMLogger.Log("--------------REACHED APEX, STARTING REVERSE----------------");	

						this.hasReachedApex = true;
						this.droppingTime = this.droppingDuration;

						this.seekTurningSpeedLerpM = 30f;
						this.seekTurningSpeedM = 10f;

						this.BeginReturnSeek();
					}
					else if ( this.hasReachedApex )
					{
						this.ReturnSeek();

						if (this.frameCount == 30)
						{
							BMLogger.Log("speed: " + this.speed);
							BMLogger.Log("seeking towards: " + this.throwingPlayer.X + " " + this.throwingPlayer.Y);
						}
					}

					
				}
				this.CheckReturnShield();
				if (!this.dropping && this.hasReachedApex) // Check if shield should start droppping
				{
					this.droppingTime -= t;
					if ( frameCount == 30 || frameCount == 90 )
                    {
						BMLogger.Log("distance to player: " + Vector3.Distance(this.transform.position, this.throwingPlayer.transform.position));
                    }
					if ( !startedSnap &&  Vector3.Distance( this.transform.position, this.throwingPlayer.transform.position ) < 100 )
                    {
						startedSnap = true;
						this.seekTurningSpeedLerpM = 60f;
						this.seekTurningSpeedM = 20f;
					}
					if ( this.droppingTime < 0 )
                    {
						this.StartDropping();
                    }
				}
			}
			if (!this.dropping)
			{
				float num = 140f + Mathf.Abs(this.xI) * 0.5f;
				if (this.speed != 0f && Time.timeScale > 0f)
				{
					float pitch = Mathf.Clamp(num / Mathf.Abs(this.speed) * 1.2f * this.shieldLoopPitchM, 0.5f * this.shieldLoopPitchM, 1f * this.shieldLoopPitchM) * Time.timeScale;
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
			if (Mathf.Sign(this.lastXI) != Mathf.Sign(this.xI) )
			{
				this.alreadyHit.Clear();
			}
			this.lastXI = this.xI;
		}

		// Handles figuring out where the Shield should return to
		protected void BeginReturnSeek()
        {
			this.foundMook = true;
			this.targetX = this.throwingPlayer.X;
			this.targetY = this.throwingPlayer.Y + throwingPlayer.height + 5;

			float y = this.targetX - base.X;
			float x = this.targetY - base.Y;
			this.targetAngle = global::Math.GetAngle(x, y);
			Vector3 currentPlayerPos = this.throwingPlayer.transform.position;
			currentPlayerPos.y += this.throwingPlayer.height + 2;
			Vector3 direction = currentPlayerPos - this.transform.position;

			// Check if anything is in the way between shield and player
			if ( Physics.Raycast(this.transform.position, direction, out this.raycastHit, Vector3.Distance(this.transform.position, currentPlayerPos), this.groundLayer) )
            {
				BMLogger.Log("ground found between");
				pathBlocked = true;
				CalculateSeek(this.startingThrowX, this.startingThrowY);
				DetermineReturnAngle(this.startingThrowVector);
			}
			else
            {
				BMLogger.Log("no ground between");
				pathBlocked = false;
				DetermineReturnAngle(this.throwingPlayer.transform.position);
            }
		}

		protected void ReturnSeek()
        {
			if ( pathBlocked )
            {	
				CalculateSeek(this.startingThrowX, this.startingThrowY);
				if (Vector3.Distance(this.transform.position, this.startingThrowVector) < 40)
				{
					this.StartDropping();
				}
			}
			else
            {
				CalculateSeek(this.throwingPlayer.X, this.throwingPlayer.Y);
			}
        }

		// Check whether there are blocks above / below to determine whether shield needs to return in a straight line or if it can circle around
		protected void DetermineReturnAngle(Vector3 playerPos)
		{
			bool above = false, below = false;
			float distanceBelow, distanceAbove;
			// Check below shield
			if ( Physics.Raycast(new Vector3(base.X, base.Y, 0f), Vector3.down, out this.raycastHit, 100f, this.groundLayer) )
			{
				distanceBelow = Vector3.Distance(this.transform.position, this.raycastHit.point);
				below = distanceBelow < 100;
				BMLogger.Log("distance to ground: " + distanceBelow);
			}
			// Check above shield
			if ( Physics.Raycast(new Vector3(base.X, base.Y, 0f), Vector3.up, out this.raycastHit, 100f, this.groundLayer) )
            {
				distanceAbove = Vector3.Distance(this.transform.position, this.raycastHit.point);
				above = distanceAbove < 100;
				BMLogger.Log("distance to ceiling: " + distanceAbove);
			}

			// We check the terrain around the player in case the shield entered an area with no ground above or below
			if ( Physics.Raycast(playerPos, Vector3.down, out this.raycastHit, 100f, this.groundLayer) )
			{
				distanceBelow = Vector3.Distance(playerPos, this.raycastHit.point);
				BMLogger.Log("distance to ground for player: " + distanceBelow);
				below = below || distanceBelow < 50;
			}
			if (Physics.Raycast(playerPos, Vector3.down, out this.raycastHit, 100f, this.ladderLayer))
            {
				distanceBelow = Vector3.Distance(playerPos, this.raycastHit.point);
				below = below || distanceBelow < 50;
			}

			if ( Physics.Raycast(playerPos, Vector3.up, out this.raycastHit, 100f, this.groundLayer) )
			{
				distanceAbove = Vector3.Distance(playerPos, this.raycastHit.point);
				BMLogger.Log("distance to ceiling for player: " + distanceAbove);
				if ( distanceAbove < 100 )
                {
					above = below = true;
                }
			}
			if (Physics.Raycast(playerPos, Vector3.up, out this.raycastHit, 100f, this.ladderLayer))
            {
				distanceAbove = Vector3.Distance(playerPos, this.raycastHit.point);
				if (distanceAbove < 100)
				{
					above = below = true;
				}
			}

			// Blocks above and below, or path to player is blocked, or shield is already heading towards player, so return shield straight
			if ( below && above || pathBlocked || (Mathf.Sign(xI) == Mathf.Sign(playerPos.x - this.X) ) ) 
            {
				BMLogger.Log("returning direct");
				this.angle = this.targetAngle;
            }
			else if ( below ) // Blocks below return shield up
            {
				Vector3 currentPlayerPos = this.throwingPlayer.transform.position;
				currentPlayerPos.y += this.throwingPlayer.height + 2;
				Vector3 direction = currentPlayerPos - this.transform.position;

				// Check if anything is in the way between area above shield and player
				if (Physics.Raycast(this.transform.position + new Vector3(0, 30, 0), direction, out this.raycastHit, Vector3.Distance(this.transform.position, currentPlayerPos), this.groundLayer))
				{
					BMLogger.Log("ground found above player and shield");
					BMLogger.Log("returning direct");
					this.angle = this.targetAngle;
				}
				else
                {
					BMLogger.Log("returning above");
					if (this.xI > 0) // Right
					{
						this.angle -= (Mathf.PI / 4);
					}
					else if (this.xI < 0) // Left
					{
						this.angle += (Mathf.PI / 4);
					}
				}	
            }
			else if ( above ) // Blocks above return shield down
            {
				Vector3 currentPlayerPos = this.throwingPlayer.transform.position;
				currentPlayerPos.y += this.throwingPlayer.height + 2;
				Vector3 direction = currentPlayerPos - this.transform.position;

				// Check if anything is in the way between area below shield and player
				if (Physics.Raycast(this.transform.position - new Vector3(0, 30, 0), direction, out this.raycastHit, Vector3.Distance(this.transform.position, currentPlayerPos), this.groundLayer))
				{
					BMLogger.Log("ground found above player and shield");
					BMLogger.Log("returning direct");
					this.angle = this.targetAngle;
				}
				else
                {
					BMLogger.Log("returning down");
					if (this.xI > 0) // Right
					{
						this.angle += (Mathf.PI / 4);
					}
					else if (this.xI < 0) // Left
					{
						this.angle -= (Mathf.PI / 4);
					}
				}
				
			}
		}

		// Called by RunProjectile Base method
		protected override void MoveProjectile()
		{
			if (!this.stuck)
			{
				if ( !this.stopSeeking )
                {
					this.RunSeeking();
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
					else if ((this.returnTime >= 0f || this.hasReachedApex) && !this.dropping) // If moving forward or towards player and hasn't reached return time
					{
						this.speed = Mathf.Lerp(this.speed, this.originalSpeed, this.t * 10f);
						if (this.frameCount == 30)
						{
							BMLogger.Log("Moving towards target");
						}
					}
					else
					{
						this.speed = Mathf.Lerp(this.speed, 0, this.t * 10f);
						if (this.frameCount == 30)
						{
							BMLogger.Log("Slowing Down");
						}
					}
					this.seekSpeedCurrent = Mathf.Lerp(this.seekSpeedCurrent, this.seekTurningSpeedM, this.seekTurningSpeedLerpM * this.t);
					this.angle = Mathf.Lerp(this.angle, this.targetAngle, this.t * this.seekSpeedCurrent);
					Vector2 vector = global::Math.Point2OnCircle(this.angle, this.speed);
					this.xI = vector.x;
					this.yI = vector.y;
				}

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

		protected void CalculateSeek(float manualTargetX, float manualTargetY)
		{
			this.foundMook = true;
			this.targetX = manualTargetX;
			this.targetY = manualTargetY + throwingPlayer.height + 2;

			float y = this.targetX - base.X;
			float x = this.targetY - base.Y;
			this.targetAngle = global::Math.GetAngle(x, y);
		}

		protected void CalculateSeek()
		{
			if (!this.foundMook)
			{
				this.seekRange = this.returnTime * Mathf.Abs(this.speed);
				BMLogger.Log("calculated range: " + this.seekRange);
				Unit nearestVisibleUnitDamagebleBy = Map.GetNearestVisibleUnitDamagebleBy(this.playerNum, (int)this.seekRange, base.X, base.Y, false);
				// Check that we found a unit, it hasn't already been hit, and it is in the direction the shield is traveling.
				if (nearestVisibleUnitDamagebleBy != null && nearestVisibleUnitDamagebleBy.gameObject.activeInHierarchy && !this.alreadyHit.Contains(nearestVisibleUnitDamagebleBy) && (Mathf.Sign(nearestVisibleUnitDamagebleBy.X - this.X) == Mathf.Sign(this.xI)) )
				{
					this.foundMook = true;
					this.targetX = nearestVisibleUnitDamagebleBy.X;
					this.targetY = nearestVisibleUnitDamagebleBy.Y + throwingPlayer.height + 4;
					Traverse trav = Traverse.Create((nearestVisibleUnitDamagebleBy as Mook));
					SpriteSM unitSprite = trav.Field("sprite").GetValue() as SpriteSM;
					unitSprite.SetColor(new Color(0, 255, 0, 1f));
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
					if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null) // Hit a block / wall
					{
						this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(1, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));

						HitBlock(raycastHit);
					}
					else if (!this.hasReachedApex) // Hit non-block non-unit thing (such as escape helicopter or saw blades)
					{
						
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
				if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null) // Hit a block / wall
				{
					this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(1, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));

					HitBlock(raycastHit);
				}
				else if (!this.hasReachedApex) // Hit non-block non-unit thing (such as escape helicopter or saw blades)
				{
					
				}
				this.PlayBounceSound();
			}
            if (this.dropping)
            {
                if (this.yI < 0f)
                {
                    if (Physics.Raycast(new Vector3(base.X, base.Y + 6f, 0f), Vector3.down, out this.raycastHit, 6f + this.heightOffGround - this.yI * this.t, this.groundLayer))
                    {
						this.stopSeeking = true;

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
                        this.rotationSpeed = groundRotationSpeed * this.xI;
                        this.PlayBounceSound();
                    }
                }
                else if (this.yI > 0f && Physics.Raycast(new Vector3(base.X, base.Y - 6f, 0f), Vector3.up, out this.raycastHit, 6f + this.heightOffGround + this.yI * this.t, this.groundLayer))
                {
					this.stopSeeking = true;
                    if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null)
                    {
                        this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(0, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));
                    }
                    this.yI *= -(this.bounceYM + 0.1f);
                    this.PlayBounceSound();
                    this.rotationSpeed = groundRotationSpeed * this.xI;
                }
            }
            return true;
		}

		protected void HitBlock( RaycastHit raycastHit )
        {
			// Calculate new angle
			Bounce(raycastHit);

			BMLogger.Log("bounced");

			if (this.bounceCount == 0)
			{
				this.StartDropping();
				BMLogger.Log("Out of bounces, dropping");
			}
			else if (this.returnTime > 0f) // Hasn't started returning
			{
				this.returnTime = 0f;
				BMLogger.Log("starting return");
			}
			else if (!this.dropping && this.droppingTime > 0 && this.droppingTime < (0.9) * this.droppingDuration)
			{
				this.StartDropping();
				BMLogger.Log("Returning for long enough to start dropping");
				BMLogger.Log("Return time: " + this.returnTime);
			}

			--this.bounceCount;
		}

		protected void StartDropping()
		{
			BMLogger.Log("----------STARTED DROPPING------------");

			this.dropping = true;
			this.collectDelayTime = 0f;
			this.rotationSpeed = groundRotationSpeed * this.xI;
			base.GetComponent<AudioSource>().Stop();
			this.shieldCollider.enabled = false;
			this.stopSeeking = true;
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

			Vector3 curAngle = new Vector3(Math.Cos(this.angle), Math.Sin(this.angle), 0);
			Vector3 bounceAngle = (curAngle - 2 * Vector3.Dot(curAngle, raycastHit.normal) * raycastHit.normal);
			this.angle = Mathf.Atan(bounceAngle.y / bounceAngle.x);
			this.targetAngle = this.angle;
			if (!this.dropping)
			{
				this.speed = 0.9f * this.speed;
			}
			else
            {
				this.speed = 0.7f * this.speed;
            }
			Vector2 vector = global::Math.Point2OnCircle(this.angle, this.speed);
			this.xI = vector.x;
			this.yI = vector.y;
			this.foundMook = false;
		}

		protected override void HitUnits()
		{
			if (this.hitUnitsDelay > 0f)
			{
				this.hitUnitsDelay -= this.t;
			}
			else if (!this.dropping)
			{
				if (this.reversing || this.hasReachedApex)
				{
					if (Map.HitLivingUnits(this, this.playerNum, this.damageInternal, this.damageType, this.projectileSize, this.projectileSize, base.X, base.Y, this.xI, this.yI, true, false, true, false))
					{	
						this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
						this.hitUnitsDelay = 0.0667f;
						
						this.hitUnitsCount++;
					}
				}
				else if (Map.HitUnits(this.firedBy, this.playerNum, this.damageInternal, 1, this.damageType, this.projectileSize, this.projectileSize * 1.3f, base.X, base.Y, this.xI, this.yI, true, false, true, this.alreadyHit, false, true))
				{
					this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
					this.hitUnitsDelay = 0.0667f;
					this.hitUnitsCount++;

					if ( !this.hasReachedApex )
                    {
						this.foundMook = false;

						this.angle = this.originalAngle;
						this.targetAngle = this.originalAngle;
					}
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
						/*PID targetOthers = PID.TargetOthers;
						bool immediate = false;
						bool ignoreSessionID = false;
						bool addExecutionDelay = true;
						if (Boomerang.<> f__mg$cache0 == null)
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
