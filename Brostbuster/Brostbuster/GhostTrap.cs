using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using System.Reflection;
using System.IO;
using HarmonyLib;

namespace Brostbuster
{
    class GhostTrap : Grenade
    {
		// Audio
		public AudioClip trapOpen;
		public AudioClip trapMain;
		public AudioClip trapClosing;
		public AudioClip trapClosed;
		protected AudioSource trapAudio;
		protected int currentClip = 0;
		protected float shutdownTime = 0f;

		// Visuals
		Material storedMat;
		protected int frame = 0;
		protected float counter = 0f;
		protected const float trapWidth = 224f;
		protected const float trapHeight = 128f;
		protected float trapFramerate = 0.3f;
		protected const int lastOpeningFrame = 4;
		protected const int lastFrame = 14;

		// State
		public enum TrapState
        {
			Thrown = 0,
			Opening = 1,
			Open = 2,
			Closing = 3,
			ClosingAnimation = 4,
			Closed = 5
        }
		public TrapState state = TrapState.Thrown;
		public float runTime = 0f;
		protected float attractCounter = 0f;
		public bool expediteClose = false;

		// Hit detection
		public float topLeftX, topLeftY, topRightX, topRightY, bottomX, bottomY;
		public float topFloatingY, leftFloatingX, rightFloatingX;
		public const float width = 120f;
		public const float height = 130f;
		public static Dictionary<Unit, FloatingUnit> grabbedUnits = new Dictionary<Unit, FloatingUnit>();
		public List<FloatingUnit> floatingUnits = new List<FloatingUnit>();
		public List<Unit> ignoredUnits = new List<Unit>();
		public int killedUnits = 0;

		protected override void Awake()
		{
			try
            {
				MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

				string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				if (storedMat == null)
				{
					storedMat = ResourcesController.GetMaterial(directoryPath, "ghostTrap.png");
				}

				renderer.material = storedMat;

				this.sprite = this.gameObject.GetComponent<SpriteSM>();
				this.sprite.lowerLeftPixel = new Vector2(0, trapHeight);
				this.sprite.pixelDimensions = new Vector2(trapWidth, trapHeight);

				this.sprite.plane = SpriteBase.SPRITE_PLANE.XY;
				this.sprite.width = trapWidth;
				this.sprite.height = trapHeight;
				this.sprite.offset = new Vector3(9, 60, 0);

				base.Awake();

				// Setup Variables
				this.disabledAtStart = false;
				this.shrink = false;
				this.trailType = TrailType.ColorTrail;
				this.rotationSpeedMultiplier = 0f;
				this.lifeLossOnBounce = false;
				this.deathOnBounce = false;
				this.destroyInsideWalls = false;
				this.startLife = 10000f;
				this.life = 10000f;

				if (this.trapAudio == null)
				{
					if (base.gameObject.GetComponent<AudioSource>() == null)
					{
						this.trapAudio = base.gameObject.AddComponent<AudioSource>();
						this.trapAudio.rolloffMode = AudioRolloffMode.Logarithmic;
						this.trapAudio.minDistance = 300f;
						this.trapAudio.dopplerLevel = 0.1f;
						this.trapAudio.maxDistance = 3000f;
						this.trapAudio.spatialBlend = 1f;
						this.trapAudio.volume = 0.33f;
					}
					else
					{
						this.trapAudio = this.GetComponent<AudioSource>();
					}
				}

				// Audio Clips need to be reloaded every time because each ghost trap needs its own clips
				trapOpen = ResourcesController.CreateAudioClip( Path.Combine(directoryPath, "sounds"), "trapOpen.wav" );
				trapMain = ResourcesController.CreateAudioClip( Path.Combine(directoryPath, "sounds"), "trapMain.wav" );
				trapClosing = ResourcesController.CreateAudioClip( Path.Combine(directoryPath, "sounds"), "trapClosing.wav" );
				trapClosed = ResourcesController.CreateAudioClip( Path.Combine(directoryPath, "sounds"), "trapClosed.wav" );
			}
			catch ( Exception ex )
            {
				BMLogger.Log("Exception Creating Projectile: " + ex.ToString());
            }
		}


		// Called when picking up and throwing already thrown grenade
        public override void ThrowGrenade(float XI, float YI, float newX, float newY, int _playerNum)
        {
			//base.ThrowGrenade(XI, YI, newX, newY, _playerNum);
		}

		// Called on first throw
        public override void Launch(float newX, float newY, float xI, float yI)
        {
            base.Launch(newX, newY, xI, yI);
			this.startLife = 10000f;
			this.life = 10000f;
		}

		// Don't register grenade
        protected override void RegisterGrenade()
        {
        }

		// Don't bounce off door infinitely
        protected override void HitFragile()
        {
			Vector3 vector = new Vector3(this.xI, this.yI, 0f);
			Vector3 normalized = vector.normalized;
			Collider[] array = Physics.OverlapSphere(new Vector3(base.X + normalized.x * 2f, base.Y + normalized.y * 2f, 0f), 2f, this.fragileLayer);
			for (int i = 0; i < array.Length; i++)
			{
				EffectsController.CreateProjectilePuff(base.X, base.Y);
				if (array[i].GetComponent<DoorDoodad>() != null && this.state != TrapState.Closed)
				{
					this.Bounce(true, false);
				}
				else
				{
					array[i].gameObject.SendMessage("Damage", new DamageObject(1, this.damageType, this.xI, this.yI, base.X, base.Y, this), SendMessageOptions.DontRequireReceiver);
				}
			}
		}

        protected override bool Update()
        {
            base.Update();

			this.runTime += this.t;

			// Opening or Open
			switch ( this.state )
            {
				case TrapState.Opening:
				case TrapState.Open:
					{
						this.counter += this.t;
						if (this.counter > trapFramerate)
						{
							this.counter -= trapFramerate;
							++this.frame;
							if (this.state != TrapState.Open && this.frame == lastOpeningFrame + 1)
							{
								this.trapFramerate = 0.08f;
								this.state = TrapState.Open;
							}
							else if (this.frame > lastFrame)
							{
								this.frame = lastOpeningFrame + 1;
							}
							this.sprite.SetLowerLeftPixel(frame * trapWidth, trapHeight);
						}

						this.attractCounter += this.t;
						if (this.runTime < 6f && this.attractCounter >= 0.0334f)
						{
							this.attractCounter -= 0.0334f;
							Map.AttractMooks(base.X, base.Y, 200f, 100f);
						}


						if ( this.state > TrapState.Opening )
                        {
							// Don't start grabbing units until trap is open
							this.FindUnits();
						}
						

						// Play next clip
						if ((this.trapAudio.clip.length - this.trapAudio.time) <= 0.02f)
						{
							// Start playing main audio clip
							if (this.currentClip == 0)
							{
								this.trapAudio.clip = trapMain;
								this.trapAudio.Play();
								++this.currentClip;
							}
							// Play closing clip
							else
							{
								this.trapAudio.clip = trapClosing;
								this.trapAudio.Play();
								this.currentClip = 2;
							}
						}

						// Start closing trap after closing audio clip has played for a second
						if (this.runTime > 8f)
						{
							this.trapFramerate = 0.08f;
							this.runTime = 10f;
							this.state = TrapState.Closing;
						}

						break;
					}
				case TrapState.Closing:
					{
						this.counter += this.t;
						if (this.counter > trapFramerate)
						{
							this.counter -= trapFramerate;
							--this.frame;
							if (this.frame < lastOpeningFrame + 1)
							{
								this.frame = lastFrame;
							}
							this.sprite.SetLowerLeftPixel(frame * trapWidth, trapHeight);
						}

						if ( this.runTime < 14f )
                        {
							this.FindUnits();
						}

						if ( currentClip == 2 && ((this.trapAudio.clip.length - this.trapAudio.time) <= 0.02f) )
						{
							this.trapAudio.clip = trapClosed;
							this.trapAudio.Play();
							++this.currentClip;
							this.shutdownTime = trapClosed.length + 0.1f;
						}

						// Speed up trap closing by moving to final clip
						if ( this.expediteClose && this.runTime > 12f && this.floatingUnits.Count == 0 && currentClip == 2 )
                        {
							this.trapAudio.clip = trapClosed;
							this.trapAudio.Play();
							++this.currentClip;
							this.runTime = 15.5f;
							this.shutdownTime = trapClosed.length + 0.1f;
						}

						if (this.runTime > 16f)
						{
							this.CloseTrap();
						}
						break;
					}
				case TrapState.ClosingAnimation:
					{
						// Animate closing
						this.counter += this.t;
						if (this.counter > trapFramerate)
						{
							this.counter -= trapFramerate;
							--this.frame;
							// Finished closing
							if (this.frame < 0)
							{
								this.frame = 0;
								this.xI = 0;
								this.yI = 0;
								this.state = TrapState.Closed;
							}
							this.sprite.SetLowerLeftPixel(frame * trapWidth, trapHeight);
						}
						break;
					}
				case TrapState.Closed:
                    {
						CheckReturnTrap();
						this.sprite.SetLowerLeftPixel(0, trapHeight);
						if ( this.shutdownTime > 0f )
                        {
							this.shutdownTime -= this.t;
							if ( this.shutdownTime <= 0 )
                            {
								this.trapAudio.enabled = false;
                            }
                        }
						break;
					}
				default:
					break;					
			}

			return true;
		}

        protected override void LateUpdate()
        {
            base.LateUpdate();

			this.MoveUnits();
		}

        protected override void RunMovement()
        {
			if ( this.state == TrapState.Thrown || this.state == TrapState.Closed )
            {
				base.RunMovement();
				if ( this.Y < 0 )
                {
					this.DestroyGrenade();
                }
			}
        }

        protected override void Bounce(bool bounceX, bool bounceY)
		{
			if (this.state != TrapState.Closed && bounceY && this.yI <= 0f)
			{
				this.ActivateTrap();
				this.yI = 0f;
				this.xI = 0f;
			}
			else
			{
				base.Bounce(bounceX, bounceY);
			}
		}

        protected override void RunWarnings()
        {
        }

		public override void Death()
        {
			if ( this.state == TrapState.Closed && this.runTime > 100f )
            {
				this.DestroyGrenade();
			}
        }

        protected override void DestroyGrenade()
        {
			UnityEngine.Object.Destroy(base.gameObject);
		}

        protected override void OnDestroy()
        {
			this.ReleaseUnits();

            base.OnDestroy();
        }

        protected void ActivateTrap()
        {
			this.runTime = 0.5f;
			this.state = TrapState.Opening;

			this.trapAudio.clip = trapOpen;
			this.trapAudio.loop = false;
			this.trapAudio.Play();

			// Set triangle bounds
			bottomX = base.X;
			bottomY = base.Y - 10f;
			topRightX = bottomX + width;
			topRightY = bottomY + height;
			topLeftX = bottomX - width;
			topLeftY = topRightY;

			// Controls where the enemies are allowed to be
			topFloatingY = base.Y + 110f;
			leftFloatingX = base.X - 90f;
			rightFloatingX = base.X + 90f;
		}

		public void StartClosingTrap()
        {
			this.expediteClose = true;
			if ( this.state == TrapState.Thrown )
            {
				this.ActivateTrap();
            }
			else if ( this.state < TrapState.Closing )
            {
				this.trapFramerate = 0.08f;
				this.runTime = 10f;
				this.state = TrapState.Closing;
				this.trapAudio.clip = trapClosing;
				this.trapAudio.Play();
				this.currentClip = 2;
			}
        }

		public void CloseTrap()
        {
			this.frame = lastOpeningFrame;
			this.counter = 0;
			this.trapFramerate = 0.2f;
			this.state = TrapState.ClosingAnimation;
			this.sprite.SetLowerLeftPixel(frame * trapWidth, trapHeight);
			this.ReleaseUnits();
		}

		protected void CheckReturnTrap()
		{
			if (this.firedBy != null && this.state == TrapState.Closed)
			{
				float f = this.firedBy.transform.position.x - base.X;
				float f2 = this.firedBy.transform.position.y + 10f - base.Y;
				if (Mathf.Abs(f) < 9f && Mathf.Abs(f2) < 14f)
				{
					Brostbuster bro = this.firedBy as Brostbuster;
					if (bro && killedUnits > 0)
					{
						bro.ReturnTrap();
					}
					Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.powerUp, 0.7f, base.transform.position, 0.95f + UnityEngine.Random.value * 0.1f, true, false, false, 0f);
					this.DestroyGrenade();
				}
			}
		}

		float sign(float p1X, float p1Y, float p2X, float p2Y, float p3X, float p3Y)
		{
			return (p1X - p3X) * (p2Y - p3Y) - (p2X - p3X) * (p1Y - p3Y);
		}

		bool ShouldGrabUnit(float x, float y)
		{
			float d1, d2, d3;
			bool has_neg, has_pos;

			d1 = sign(x, y, topLeftX, topLeftY, topRightX, topRightY);
			d2 = sign(x, y, topRightX, topRightY, bottomX, bottomY);
			d3 = sign(x, y, bottomX, bottomY, topLeftX, topLeftY);

			has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
			has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

			return !(has_neg && has_pos);
		}

		protected void AddUnit(Unit unit)
        {
			FloatingUnit floatUnit = new FloatingUnit(unit, this);
			floatingUnits.Add(floatUnit);
			grabbedUnits.Add(unit, floatUnit);
			unit.Panic(1000f, true);
			if (unit is Mook)
			{
				(unit as Mook).SetInvulnerable(float.MaxValue);
			}
			Map.units.Remove(unit);
		}

		protected void FindUnits()
        {
			for ( int i = 0; i < Map.units.Count; ++i )
            {
				Unit unit = Map.units[i];
				// Check that unit is not null, is not a player, is not dead, and is not already grabbed by this trap or another
				if (unit != null && unit.playerNum < 0 && unit.health > 0 && !grabbedUnits.ContainsKey(unit) && !ignoredUnits.Contains(unit) && !unit.IsHero )
                {
					// Ignore all vehicles and bosses
					if (unit.CompareTag("Boss") || unit.CompareTag("Metal") || unit is SatanMiniboss || unit is DolphLundrenSoldier || unit is Tank || unit is MookVehicleDigger)
                    {
						ignoredUnits.Add(unit);
						continue;
                    }
					// Check unit is in rectangle around trap
					if ( Tools.FastAbsWithinRange(unit.X - bottomX, 50f) && Tools.FastAbsWithinRange(unit.Y - bottomY, 20f) )
                    {
						AddUnit(unit);
					}
					// Check unit is in trap triangle
					else if ( Tools.FastAbsWithinRange(unit.X - bottomX, width) )
                    {
						float num = unit.Y - bottomY;
						// Check that unit is within the possible vertical range of the trap, is above the trap, and is inside the trap triangle
						if (Tools.FastAbsWithinRange(num, height) && (Mathf.Sign(num) == 1f) && ShouldGrabUnit(unit.X, unit.Y))
						{
							AddUnit(unit);
						}
					}
                }
            }
        }

		protected void MoveUnits()
        {
			if ( this.state == TrapState.Open)
            {
				// Iterate backwards since units may be destroyed during this loop
				for (int i = floatingUnits.Count - 1; i >= 0; --i)
				{
					floatingUnits[i].MoveUnit(this.t);
				}
			}				
			else
            {
				// Iterate backwards since units may be destroyed during this loop
				for (int i = floatingUnits.Count - 1; i >= 0; --i)
				{
					floatingUnits[i].MoveUnitToCenter(this.t);
				}
			}
		}

		protected void ReleaseUnits()
        {
			for (int i = 0; i < floatingUnits.Count; ++i)
			{
				floatingUnits[i].ReleaseUnit();
				grabbedUnits.Remove(floatingUnits[i].unit);
				Map.units.Add(floatingUnits[i].unit);
			}
		}
	}
}
