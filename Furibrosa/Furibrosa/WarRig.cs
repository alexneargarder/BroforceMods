using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using BroMakerLib.CustomObjects.Bros;
using RocketLib;
using HarmonyLib;
using BroMakerLib;
using System.IO;
using System.Reflection;
using BroMakerLib.Loggers;

namespace Furibrosa
{
    public class WarRig : MookArmouredGuy
    {
        protected bool boostingForward = false;

        #region General
        public void Setup()
        {
            this.gameObject.name = "WarRig";
            UnityEngine.Object.Destroy(this.gameObject.FindChildOfName("ZMook"));
            UnityEngine.Object.Destroy(this.GetComponent<BigGuyAI>());
            MookArmouredGuy mookArmouredGuy = this.gameObject.GetComponent<MookArmouredGuy>();
            Traverse trav = Traverse.Create(mookArmouredGuy);

            // Assign null values
            this.sprite = this.GetComponent<SpriteSM>();
            this.gunSprite = mookArmouredGuy.gunSprite;
            this.soundHolder = mookArmouredGuy.soundHolder;
            this.soundHolderFootSteps = mookArmouredGuy.soundHolderFootSteps;
            this.gibs = mookArmouredGuy.gibs;
            this.player1Bubble = mookArmouredGuy.player1Bubble;
            this.player2Bubble = mookArmouredGuy.player2Bubble;
            this.player3Bubble = mookArmouredGuy.player3Bubble;
            this.player4Bubble = mookArmouredGuy.player4Bubble;
            this.blood = mookArmouredGuy.blood;
            this.heroTrailPrefab = mookArmouredGuy.heroTrailPrefab;
            this.high5Bubble = mookArmouredGuy.high5Bubble;
            this.projectile = mookArmouredGuy.projectile;
            this.specialGrenade = mookArmouredGuy.specialGrenade;
            if ( this.specialGrenade != null )
            {
                this.specialGrenade.playerNum = mookArmouredGuy.specialGrenade.playerNum;
            }
            this.heroType = mookArmouredGuy.heroType;
            this.wallDragAudio = trav.GetFieldValue("wallDragAudio") as AudioSource;
            this.SetOwner(mookArmouredGuy.Owner);
            this.pilotUnit = null;

            UnityEngine.Object.Destroy(mookArmouredGuy);

            // Load sprite
            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();
            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Material material = ResourcesController.GetMaterial(directoryPath, "vehicleSprite.png");
            renderer.material = material;

            this.sprite = this.gameObject.GetComponent<SpriteSM>();
            sprite.lowerLeftPixel = new Vector2(0, 64);
            sprite.pixelDimensions = new Vector2(128, 64);
            sprite.plane = SpriteBase.SPRITE_PLANE.XY;
            sprite.width = 128;
            sprite.height = 64;
            this.spritePixelWidth = 128;
            this.spritePixelHeight = 64;

            this.gameObject.SetActive(false);
        }

        protected override void Start()
        {
            base.Start();
            this.isTank = true;
            this.speed = 200;
            this.originalSpeed = 200;
        }

        public override void PilotUnitRPC(Unit newPilotUnit)
        {
            this.pilotUnitDelay = 0.2f;
            if (this.pilotUnit != null && this.pilotUnit != newPilotUnit)
            {
                this.DisChargePilot(150f, true, newPilotUnit);
            }
            this.ActuallyPilot(newPilotUnit);
        }

        protected override void ActuallyPilot(Unit PilotUnit)
        {
            if (base.IsFrozen)
            {
                base.UnFreeze();
            }
            this.pilotUnit = PilotUnit;
            base.playerNum = this.pilotUnit.playerNum;
            this.health = this.maxHealth;
            this.deathNotificationSent = false;
            this.isHero = true;
            this.firingPlayerNum = PilotUnit.playerNum;
            this.pilotUnit.StartPilotingUnit(this);
            if (this.pilotSwitch != null)
            {
            }
            this.RestartBubble();
            this.blindTime = 0f;
            this.stunTime = 0f;
            this.burnTime = 0f;
            this.ResetDamageAmounts();
            this.ActivateGun();
            base.GetComponent<Collider>().enabled = true;
            base.GetComponent<Collider>().gameObject.layer = LayerMask.NameToLayer("FriendlyBarriers");
            base.SetOwner(PilotUnit.Owner);
        }

        protected override void DisChargePilot(float disChargeYI, bool stunPilot, Unit dischargedBy)
        {
            DisChargePilotRPC(disChargeYI, stunPilot, dischargedBy);
        }
        #endregion

        #region Animation
        protected override void ChangeFrame()
        {
            this.sprite.SetLowerLeftPixel(0, this.spritePixelHeight);
            this.DeactivateGun();
        }

        protected override void AnimateRunning()
        {
            
        }
        #endregion

        #region Movement
        protected override void Jump(bool wallJump)
        {
            if (!this.wasButtonJump || this.pressedJumpInAirSoJumpIfTouchGroundGrace > 0f)
            {
            }
            if (this.canAirdash && (this.canTouchLeftWalls || this.canTouchRightWalls || !wallJump))
            {
                this.SetAirdashAvailable();
            }
            this.lastJumpTime = Time.time;
            base.actionState = ActionState.Jumping;
            if (this.blockCurrentlyStandingOn != null && this.blockCurrentlyStandingOn.IsBouncy)
            {
                this.blockCurrentlyStandingOn.BounceOn();
            }
            if (this.blockCurrentlyStandingOn != null && this.IsSurroundedByBarbedWire())
            {
                this.yI = this.jumpForce * 0.8f;
                EffectsController.CreateBloodParticles(this.bloodColor, base.X, base.Y + 10f, -5f, 3, 4f, 4f, 50f, this.xI * 0.8f, this.yI);
                this.barbedWireWithin.PlayCutSound();
            }
            else if (Physics.Raycast(new Vector3(base.X, base.Y + 2f, 0f), Vector3.down, out this.raycastHit, 4f, Map.groundLayer) && this.raycastHit.collider.GetComponent<BossBlockPiece>() != null)
            {
                BossBlockPiece component = this.raycastHit.collider.GetComponent<BossBlockPiece>();
                if (component.isBouncy)
                {
                    this.yI = this.jumpForce * 1.9f;
                    component.BounceOn();
                }
                else
                {
                    this.yI = this.jumpForce;
                }
            }
            else
            {
                this.yI = this.jumpForce;
            }
            this.xIBlast += this.parentedDiff.x / this.t;
            float value = this.parentedDiff.y / this.t;
            this.yI += Mathf.Clamp(value, -100f, 400f);
            this.doubleJumpsLeft = 0;
            this.wallClimbAnticipation = false;
            if (wallJump)
            {
                this.jumpTime = 0f;
                this.xI = 0f;
                if (this.useNewKnifeClimbingFrames)
                {
                    base.frame = 0;
                    this.lastKnifeClimbStabY = base.Y + this.knifeClimbStabHeight;
                }
                else
                {
                    this.knifeHand++;
                }
                RaycastHit raycastHit;
                if (this.left && Physics.Raycast(new Vector3(base.X, base.Y + this.headHeight, 0f), Vector3.left, out raycastHit, 10f, this.groundLayer))
                {
                    raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    if (this.useNewKnifeClimbingFrames)
                    {
                        this.AnimateWallAnticipation();
                    }
                }
                else if (this.right && Physics.Raycast(new Vector3(base.X, base.Y + this.headHeight, 0f), Vector3.right, out raycastHit, 10f, this.groundLayer))
                {
                    raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    if (this.useNewKnifeClimbingFrames)
                    {
                        this.AnimateWallAnticipation();
                    }
                }
                this.PlayClimbSound();
            }
            else
            {
                if (!this.IsSurroundedByBarbedWire())
                {
                    this.jumpTime = this.JUMP_TIME;
                }
                this.ChangeFrame();
                this.PlayJumpSound();
            }
            if (!wallJump && this.groundHeight - base.Y > -2f)
            {
                EffectsController.CreateJumpPoofEffect(base.X, base.Y, (Mathf.Abs(this.xI) >= 30f) ? (-(int)base.transform.localScale.x) : 0, this.GetFootPoofColor());
            }
            this.airDashJumpGrace = 0f;
        }

        protected override void RunJetPack() {}

        private void CrushGroundWhileMoving(int damageGroundAmount, int damageUnitsAmount, float unitsRange, float offset)
        {
            if (this.left || (this.boostingForward && base.transform.localScale.x < 0))
            {
                if (Map.HitLivingUnits(this, base.playerNum, damageUnitsAmount, DamageType.Crush, unitsRange, base.X - offset, base.Y + 2f, this.xI, 40f, true, true, false, true))
                {
                    this.PlaySpecial2Sound(0.33f);
                }
                if (!this.boostingForward)
                {
                    MapController.DamageGround(this, 1, DamageType.Crush, 11f, base.X - (offset + 2f), base.Y + 21f, null, true);
                }
                MapController.DamageGround(this, damageGroundAmount, DamageType.Crush, (float)(11 + ((!this.boostingForward) ? 0 : 8)), base.X - (offset + 2f), base.Y + 12f + (float)((!this.boostingForward) ? 0 : 8), null, true);
                if (Physics.Raycast(new Vector3(base.X - 4f, base.Y + this.waistHeight, 0f), Vector3.left, out this.raycastHit, 5f + offset + Mathf.Abs(this.xIT), this.fragileLayer) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null)
                {
                    this.raycastHit.collider.gameObject.SendMessage("Damage", new DamageObject(1, DamageType.Crush, this.xI, this.yI, this.raycastHit.point.x, this.raycastHit.point.y, this));
                }
            }
            else if (this.right || (this.boostingForward && base.transform.localScale.x > 0))
            {
                if (Map.HitLivingUnits(this, base.playerNum, damageUnitsAmount, DamageType.Crush, unitsRange, base.X + offset, base.Y + 2f, this.xI, 40f, true, true, false, true))
                {
                    this.PlaySpecial2Sound(0.33f);
                }
                if (!this.boostingForward)
                {
                    MapController.DamageGround(this, 1, DamageType.Crush, 11f, base.X + (offset + 2f), base.Y + 21f, null, true);
                }
                MapController.DamageGround(this, damageGroundAmount, DamageType.Crush, (float)(11 + ((!this.boostingForward) ? 0 : 8)), base.X + (offset + 2f), base.Y + 12f + (float)((!this.boostingForward) ? 0 : 8), null, true);
                if (Physics.Raycast(new Vector3(base.X + 4f, base.Y + this.waistHeight, 0f), Vector3.right, out this.raycastHit, 5f + offset + Mathf.Abs(this.xIT), this.fragileLayer) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null)
                {
                    this.raycastHit.collider.gameObject.SendMessage("Damage", new DamageObject(1, DamageType.Crush, this.xI, this.yI, this.raycastHit.point.x, this.raycastHit.point.y, this));
                }
            }
        }
        #endregion

        #region Primary
        #endregion

        #region Special
        protected override void PressSpecial()
        {
        }
        #endregion
    }
}
