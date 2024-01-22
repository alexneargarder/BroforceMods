using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using System.Reflection;
using System.IO;

namespace EctoBro
{
    class GhostTrap : Grenade
    {
		Material storedMat;
		protected int frame = 0;
		protected float counter = 0f;
		protected bool opened = false;
		protected override void Awake()
		{
			try
            {
				MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

				if (storedMat == null)
				{
					string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
					storedMat = ResourcesController.CreateMaterial(Path.Combine(directoryPath, "ghostTrap.png"), ResourcesController.Particle_AlphaBlend);
				}

				renderer.material = storedMat;

				this.sprite = this.gameObject.GetComponent<SpriteSM>();
				this.sprite.lowerLeftPixel = new Vector2(0, 48);
				this.sprite.pixelDimensions = new Vector2(48, 48);

				this.sprite.plane = SpriteBase.SPRITE_PLANE.XY;
				this.sprite.width = 48;
				this.sprite.height = 48;
				this.sprite.offset = new Vector3(0, 21, 0);

				base.Awake();

				// Setup Variables
				this.disabledAtStart = false;
				this.shrink = false;
				this.trailType = TrailType.ColorTrail;
				this.rotationSpeedMultiplier = 0f;
			}
			catch ( Exception ex )
            {
				BMLogger.Log("exception creating projectile: " + ex.ToString());
            }
		}


		// Called when picking up and throwing already thrown grenade
        public override void ThrowGrenade(float XI, float YI, float newX, float newY, int _playerNum)
        {
			base.ThrowGrenade(XI, YI, newX, newY, _playerNum);
		}

		// Called on first throw
        public override void Launch(float newX, float newY, float xI, float yI)
        {
            base.Launch(newX, newY, xI, yI);
			this.startLife = 10000f;
			this.life = 10000f;
		}

        protected override bool Update()
        {
            base.Update();

			if ( this.opened )
            {
				this.counter += this.t;
				if (this.counter > EctoBro.trapFramerate)
				{
					this.counter -= EctoBro.trapFramerate;
					++this.frame;
					if (this.frame > 7)
					{
						this.frame = 4;
					}
					this.sprite.SetLowerLeftPixel(frame * 48f, 48f);
				}
			}
			return true;
		}

        protected override void Bounce(bool bounceX, bool bounceY)
		{
			if (bounceY && this.yI <= 0f)
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

        protected void ActivateTrap()
        {
			this.opened = true;
        }

		public void CloseTrap()
        {

        }
	}
}
