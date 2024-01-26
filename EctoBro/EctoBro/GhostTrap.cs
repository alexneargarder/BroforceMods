using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using System.Reflection;
using System.IO;
using HarmonyLib;

namespace EctoBro
{
    class GhostTrap : Grenade
    {
		// Visuals
		Material storedMat;
		protected int frame = 0;
		protected float counter = 0f;
		protected const float trapWidth = 224f;
		protected const float trapHeight = 128f;
		protected float trapFramerate = 0.2f;

		// State
		public enum TrapState
        {
			Thrown = 0,
			Opening = 1,
			Open = 2,
			Closing = 3,
			Closed = 4
        }
		public TrapState state = TrapState.Thrown;
		public float runTime = 0f;

		// Hit detection
		public float topLeftX, topLeftY, topRightX, topRightY, bottomX, bottomY;
		public float topFloatingY, leftFloatingX, rightFloatingX;
		public const float width = 120f;
		public const float height = 130f;
		public static List<Unit> grabbedUnits = new List<Unit>();
		public List<FloatingUnit> floatingUnits = new List<FloatingUnit>();

		// DEBUG
		LineRenderer line1, line2, line3;

		protected override void Awake()
		{
			try
            {
				MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

				string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				if (storedMat == null)
				{
					storedMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "ghostTrap2.0.png"), ResourcesController.Particle_AlphaBlend);
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


				line1 = new GameObject("Line1", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
				line1.transform.parent = this.transform;
				line1.material = ResourcesController.GetMaterial(directoryPath, "protonLine1End.png");

				line2 = new GameObject("Line1", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
				line2.transform.parent = this.transform;
				line2.material = ResourcesController.GetMaterial(directoryPath, "protonLine1End.png");

				line3 = new GameObject("Line1", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
				line3.transform.parent = this.transform;
				line3.material = ResourcesController.GetMaterial(directoryPath, "protonLine1End.png");
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

        protected override bool Update()
        {
            base.Update();

			this.runTime += this.t;

			// Opening or Open
			if ( this.state > TrapState.Thrown && this.state < TrapState.Closing )
            {
				this.counter += this.t;
				if (this.counter > trapFramerate)
				{
					this.counter -= trapFramerate;
					++this.frame;
					if (this.frame == 3)
					{
						this.trapFramerate = 0.08f;
						this.state = TrapState.Open;
					}
					else if (this.frame > 12)
					{
						this.frame = 3;
					}
					this.sprite.SetLowerLeftPixel(frame * trapWidth, trapHeight);
				}

				// Don't start grabbing units until trap is open
				if ( this.state > TrapState.Opening )
                {
					this.FindUnits();
				}

				if ( this.runTime > 10f )
                {
					this.StartClosingTrap();
                }
			}
			// Closing
			else if ( this.state == TrapState.Closing )
            {
				// Animate closing
				this.counter += this.t;
				if (this.counter > trapFramerate)
				{
					this.counter -= trapFramerate;
					++this.frame;
					if (this.frame > 12)
					{
						this.frame = 3;
					}
					this.sprite.SetLowerLeftPixel(frame * trapWidth, trapHeight);
				}

				this.FindUnits();

				if ( this.runTime > 13f && this.floatingUnits.Count == 0 )
                {
					this.CloseTrap();
                }
			}
			// Closed
			else if ( this.state == TrapState.Closed )
            {
				this.sprite.SetLowerLeftPixel(0, trapHeight);
			}


			if (EctoBro.debugLines)
			{
				this.line1.enabled = true;
				this.line2.enabled = true;
				this.line3.enabled = true;


				this.line1.SetPosition(0, new Vector3(bottomX, bottomY));
				this.line1.SetPosition(1, new Vector3(topRightX, topRightY));

				this.line2.SetPosition(0, new Vector3(topRightX, topRightY));
				this.line2.SetPosition(1, new Vector3(topLeftX, topLeftY));

				this.line3.SetPosition(0, new Vector3(topLeftX, topLeftY));
				this.line3.SetPosition(1, new Vector3(bottomX, bottomY));
			}
			else
			{
				this.line1.enabled = false;
				this.line2.enabled = false;
				this.line3.enabled = false;
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
			this.DestroyGrenade();
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
			this.state = TrapState.Opening;

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

			// DEBUG
			this.line1.enabled = true;
			this.line1.startWidth = 3f;

			this.line2.enabled = true;
			this.line2.startWidth = 3f;

			this.line3.enabled = true;
			this.line3.startWidth = 3f;
		}

		public void StartClosingTrap()
        {
			if ( this.state != TrapState.Closing )
            {
				this.runTime = 10f;
				this.state = TrapState.Closing;
			}
        }

		public void CloseTrap()
        {
			this.xI = 0f;
			this.yI = 0f;
			this.state = TrapState.Closed;
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

		protected void FindUnits()
        {
			for ( int i = 0; i < Map.units.Count; ++i )
            {
				Unit unit = Map.units[i];
				// Check that unit is not null, is not a player, is not dead, and is within the possible horizontal range of the trap
				if (unit != null && unit.playerNum < 0 && unit.health > 0 && !grabbedUnits.Contains(unit) && Tools.FastAbsWithinRange(unit.X - bottomX, width) )
                {
					float num = unit.Y - bottomY;
					// Check that unit is within the possible vertical range of the trap, is above the trap, and is inside the trap triangle
					if ( Tools.FastAbsWithinRange(num, height) && (Mathf.Sign(num) == 1f) && ShouldGrabUnit(unit.X, unit.Y) )
                    {
						grabbedUnits.Add(unit);
						floatingUnits.Add(new FloatingUnit(unit, this));
						unit.Panic(1000f, true);
						if ( unit is Mook )
                        {
							(unit as Mook).SetInvulnerable(float.MaxValue);
                        }
						Map.units.Remove(unit);
                    }
                }
            }
        }

		protected void MoveUnits()
        {
			if ( this.state == TrapState.Open)
            {
				for (int i = 0; i < floatingUnits.Count; ++i)
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
