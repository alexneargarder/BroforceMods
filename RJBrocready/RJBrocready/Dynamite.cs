using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using System.Reflection;
using System.IO;

namespace RJBrocready
{
    public class Dynamite : Grenade
    {
		public static Material storedMat;
		protected float flashCounter = 0f;
		protected float flashRate = 0.03f;
		protected int flashFrame = 0;
		protected bool flashReversing = false;
		Rigidbody rigidbody;

		protected override void Awake()
		{
			MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

			if (storedMat == null)
			{
				string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				storedMat = ResourcesController.GetMaterial(directoryPath, "dynamite.png");
			}

			renderer.material = storedMat;

			this.sprite = this.gameObject.GetComponent<SpriteSM>();
			sprite.lowerLeftPixel = new Vector2(0, 16);
			sprite.pixelDimensions = new Vector2(16, 16);

			sprite.plane = SpriteBase.SPRITE_PLANE.XY;
			sprite.width = 18;
			sprite.height = 22;

			base.Awake();

			// Setup Variables
			this.bounceM = 0.3f;
			this.disabledAtStart = false;
			this.shrink = false;
			this.trailType = TrailType.None;
			this.lifeLossOnBounce = false;
			this.deathOnBounce = false;
			this.destroyInsideWalls = false;
			this.rotateAtRightAngles = false;
			this.fades = false;
			this.fadeUVs = false;
			this.useAngularFriction = true;
			this.shrapnelControlsMotion = false;
		}

		public override void ThrowGrenade(float XI, float YI, float newX, float newY, int _playerNum)
		{
			base.enabled = true;
			base.transform.parent = null;
			this.SetXY(newX, newY);
			this.xI = XI;
			this.yI = YI;
			this.playerNum = _playerNum;
			rigidbody.position = new Vector3(newX, newY);
			if (Mathf.Abs(xI) > 100)
			{
				this.rI = -Mathf.Sign(xI) * UnityEngine.Random.Range(20, 25);
			}
			else
			{
				this.rI = -Mathf.Sign(xI) * UnityEngine.Random.Range(10, 15);
			}
			rigidbody.AddForce(new Vector3(xI, yI, 0f), ForceMode.VelocityChange);
			rigidbody.AddTorque(new Vector3(0f, 0f, this.rI), ForceMode.VelocityChange);
			this.SetMinLife(0.7f);
		}

		public override void Launch(float newX, float newY, float xI, float yI)
        {
			if (this == null)
			{
				return;
			}
			this.SetXY(newX, newY);
			this.xI = xI;
			this.yI = yI;
			this.r = 0;
			this.life = 3f;
			this.startLife = this.life;
			if (this.sprite != null)
			{
				this.spriteWidth = this.sprite.width;
				this.spriteHeight = this.sprite.height;
			}
			this.spriteWidthI = -this.spriteWidth / this.life * 1f;
			this.spriteHeightI = -this.spriteHeight / this.life * 1f;
			if ( Mathf.Abs(xI) > 100 )
            {
				this.rI = -Mathf.Sign(xI) * UnityEngine.Random.Range(20, 25);
			}
			else
            {
				this.rI = -Mathf.Sign(xI) * UnityEngine.Random.Range(10, 15);
			}
			this.SetPosition();
			if (!this.shrapnelControlsMotion && base.GetComponent<Rigidbody>() == null)
			{
				BoxCollider boxCollider = base.gameObject.GetComponent<BoxCollider>();
				if (boxCollider == null)
				{
					boxCollider = base.gameObject.AddComponent<BoxCollider>();
				}
				boxCollider.size = new Vector3(20, 6, 6f);
				rigidbody = base.gameObject.AddComponent<Rigidbody>();
				rigidbody.AddForce(new Vector3(xI, yI, 0f), ForceMode.VelocityChange);
				rigidbody.constraints = (RigidbodyConstraints)56;
				rigidbody.maxAngularVelocity = float.MaxValue;
				rigidbody.AddTorque(new Vector3(0f, 0f, this.rI), ForceMode.VelocityChange);
				rigidbody.drag = 0.8f;
				rigidbody.angularDrag = 0.1f;
				rigidbody.mass = 200;
				Quaternion rotation = rigidbody.rotation;
				rotation.eulerAngles = new Vector3(0f, 0f, 90f);
				rigidbody.rotation = rotation;
			}
			base.enabled = true;
			this.lastTrailX = newX;
			this.lastTrailY = newY;
			if (Map.InsideWall(newX, newY, this.size, false))
			{
				if (xI > 0f && !Map.InsideWall(newX - 8f, newY, this.size, false))
				{
					float num = 8f;
					float num2 = 0f;
					bool flag = false;
					bool flag2 = false;
					if (Map.ConstrainToBlocks(this, newX - 8f, newY, this.size, ref num, ref num2, ref flag, ref flag2, false))
					{
						newX = newX - 8f + num;
						newY += num2;
					}
					xI = -xI * 0.6f;
				}
				else if (xI > 0f)
				{
					float num3 = xI * 0.03f;
					float num4 = yI * 0.03f;
					bool bounceX = false;
					bool bounceY = false;
					if (Map.ConstrainToBlocks(this, newX, newY, this.size, ref num3, ref num4, ref bounceX, ref bounceY, false))
					{
						this.Bounce(bounceX, bounceY);
					}
					newX += num3;
					newY += num4;
					xI = -xI * 0.6f;
				}
				if (xI < 0f && !Map.InsideWall(newX + 8f, newY, this.size, false))
				{
					float num5 = -8f;
					float num6 = 0f;
					bool flag3 = false;
					bool flag4 = false;
					if (Map.ConstrainToBlocks(this, newX + 8f, newY, this.size, ref num5, ref num6, ref flag3, ref flag4, false))
					{
						newX = newX + 8f + num5;
						newY += num6;
					}
					xI = -xI * 0.6f;
				}
				else if (xI < 0f)
				{
					float num7 = xI * 0.03f;
					float num8 = yI * 0.03f;
					bool bounceX2 = false;
					bool bounceY2 = false;
					if (Map.ConstrainToBlocks(this, newX, newY, this.size, ref num7, ref num8, ref bounceX2, ref bounceY2, false))
					{
						this.Bounce(bounceX2, bounceY2);
					}
					newX += num7;
					newY += num8;
					xI = -xI * 0.6f;
				}
			}
			this.SetPosition();
			sprite.offset = new Vector3(0f, -0.5f, 0f);
		}

        protected override bool Update()
        {
			bool retVal = base.Update();

			this.flashCounter += this.t;
			if (this.flashCounter > this.flashRate)
			{
				this.flashCounter -= this.flashRate;
				if (!this.flashReversing)
				{
					++flashFrame;
				}
				else
				{
					--flashFrame;
				}

				if (flashFrame > 9)
				{
					flashFrame = 8;
					this.flashReversing = true;
				}
				else if (flashFrame < 0)
				{
					flashFrame = 1;
					this.flashReversing = false;
				}
				this.sprite.SetLowerLeftPixel(flashFrame * 16, 16);
			}

			base.X = rigidbody.position.x;
			base.Y = rigidbody.position.y;
			return retVal;
		}

        public override void Death()
        {
			this.MakeEffects();
			this.DestroyGrenade();
		}

        protected override void RunWarnings()
        {
        }

		protected override void MakeEffects()
        {
			EffectsController.CreateNuclearExplosion(X, Y - 6, 0f);
			MapController.DamageGround(this, 100, DamageType.Explosion, 128f, X, Y, null, false);
			Map.ExplodeUnits(this, 12, DamageType.Explosion, 144f, 96f, X, Y - 10f, 360f, 400f, base.playerNum, true, true, true);
			Map.ExplodeUnits(this, 32, DamageType.Crush, 96f, 96f, X, Y - 10f, 360f, 400f, base.playerNum, true, true, true);
			MapController.BurnUnitsAround_NotNetworked(this, -15, 5, 160f, X, Y, true, true);
			Map.HitProjectiles(base.playerNum, 15, DamageType.Explosion, 80f, X, Y, 0f, 0f, 0.25f);
			Map.ShakeTrees(X, Y, 320f, 64f, 160f);
		}
    }
}
