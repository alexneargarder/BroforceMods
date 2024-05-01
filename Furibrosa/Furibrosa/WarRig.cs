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
using RocketLib.Collections;

namespace Furibrosa
{
    public class WarRig : Mook
    {
        protected float originalSpeed = 200f;
        public Unit pilotUnit = null;
        protected bool boostingForward = false;
        protected MobileSwitch pilotSwitch;
        protected bool hasResetDamage;
        protected int shieldDamage;
        public bool hasBeenPiloted;
        protected int fireAmount;
        protected int knockCount;
        public bool alwaysKnockOnExplosions = false;
        protected int crushingGroundLayers;
        protected float fallDamageHurtSpeed = -450;
        protected float fallDamageHurtSpeedHero = -550;
        protected float fallDamageDeathSpeed = -600;
        protected float fallDamageDeathSpeedHero = -750;
        protected float pilotUnitDelay;
        protected bool fixedBubbles = false;

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
            sprite.offset = new Vector3(0f, 30f, 0f);

            this.gameObject.SetActive(false);
        }

        void FixPlayerBubble(ReactionBubble bubble)
        {
            bubble.transform.localPosition = new Vector3(0f, 53f, 0f);
            bubble.SetPosition(bubble.transform.localPosition);
            Traverse bubbleTrav = Traverse.Create(bubble);
            bubble.RestartBubble();
            bubbleTrav.Field("yStart").SetValue(bubble.transform.localPosition.y + 5);
        }

        protected override void Start()
        {
            base.Start();
            this.speed = 200f;
            this.originalSpeed = 200f;
            this.waistHeight = 10f;
            this.height = 50f;
            this.headHeight = 50f;
            this.standingHeadHeight = 50f;
            this.halfWidth = 63f;
            this.feetWidth = 58f;
            this.width = 62f;
            this.doRollOnLand = false;
            this.canChimneyFlip = false;
            this.canWallClimb = false;
            this.canTumble = false;
            this.canDuck = false;
            this.isHero = true;
            this.DeactivateGun();
        }

        protected float crushXRange = 40f;
        protected float crushYRange = 30f;
        protected float crushXOffset = 50f;
        protected float crushYOffset = 20f;
        protected float unitXRange = 50f;
        protected float unitYRange = 20f;

        protected override void Update()
        {
            base.Update();
            if (this.pilotUnitDelay > 0f)
            {
                this.pilotUnitDelay -= this.t;
            }
            if (this.pilotUnit != null)
            {
                // Controls where camera is
                this.pilotUnit.SetXY(base.X, base.Y + 25f);
                this.pilotUnit.row = this.row;
                this.pilotUnit.collumn = this.collumn;
                this.pilotUnit.transform.position = new Vector3(this.pilotUnit.X, this.pilotUnit.Y, 10f);
                if (this.pilotUnit.playerNum < 0)
                {
                    this.pilotUnit.gameObject.SetActive(false);
                }
            }
            if (this.pilotSwitch == null)
            {
                this.pilotSwitch = SwitchesController.CreatePilotMookSwitch(this, new Vector3(0f, 40f, 0f));
            }

            //this.CrushGroundWhileMoving(15, 25, crushXRange, crushYRange, unitXRange, unitYRange, crushXOffset, crushYOffset);

            if (this.constrainedLeft || this.constrainedRight)
            {
                BMLogger.Log("hit wall");
            }
        }

        protected virtual void ResetDamageAmounts()
        {
            this.health = this.maxHealth;
            if (!this.hasResetDamage)
            {
                this.hasResetDamage = true;
                this.burnDamage = 0;
                this.shieldDamage = 0;
            }
        }
        public override void Damage(int damage, DamageType damageType, float xI, float yI, int direction, MonoBehaviour damageSender, float hitX, float hitY)
        {
            if (damageType == DamageType.Acid)
            {
                damageType = DamageType.Fire;
            }
            if (damageType == DamageType.Fire)
            {
                this.fireAmount += damage;
            }
            else if (damageType != DamageType.SelfEsteem)
            {
                this.shieldDamage += damage;
            }
            if ((damageType == DamageType.Crush || base.actionState == ActionState.Panicking || this.fireAmount > 35 || this.shieldDamage > 60) && this.health > 0)
            {
                base.Damage(damage, damageType, xI, yI, direction, damageSender, hitX, hitY);
            }
            else if (this.health <= 0)
            {
                this.knockCount++;
                if (this.knockCount % 4 == 0 || (this.alwaysKnockOnExplosions && damageType == DamageType.Explosion))
                {
                    yI = Mathf.Min(yI + 20f, 20f);
                    base.Y += 2f;
                    this.Knock(damageType, xI, 0f, false);
                }
                else
                {
                    this.Knock(damageType, xI, 0f, false);
                }
            }
            else if (damageType != DamageType.SelfEsteem)
            {
                this.PlayDefendSound();
            }
            if (damageType == DamageType.SelfEsteem && damage >= this.health && this.health > 0)
            {
                this.Death(0f, 0f, new DamageObject(damage, damageType, 0f, 0f, base.X, base.Y, this));
            }
        }

        protected virtual void PlayDefendSound()
        {
            Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.defendSounds, 0.7f + UnityEngine.Random.value * 0.4f, base.transform.position, 0.8f + 0.34f * UnityEngine.Random.value, true, false, false, 0f);
        }

        public override void Knock(DamageType damageType, float xI, float yI, bool forceTumble)
        {
            if (this.health > 0)
            {
                this.knockCount++;
                if (this.knockCount % 8 == 0 || (this.alwaysKnockOnExplosions && damageType == DamageType.Explosion))
                {
                    this.KnockSimple(new DamageObject(0, DamageType.Bullet, xI * 0.5f, yI * 0.3f, base.X, base.Y, null));
                }
            }
            else
            {
                base.Knock(damageType, xI, yI, forceTumble);
            }
        }

        public override bool CanPilotUnit(int newPlayerNum)
        {
            return this.health <= 0 || this.pilotUnit != null;
        }

        public override void PilotUnitRPC(Unit newPilotUnit)
        {
            if ( !this.fixedBubbles )
            {
                FixPlayerBubble(this.player1Bubble);
                FixPlayerBubble(this.player2Bubble);
                FixPlayerBubble(this.player3Bubble);
                FixPlayerBubble(this.player4Bubble);
                this.fixedBubbles = true;
            }
            this.pilotUnitDelay = 0.2f;
            if (this.pilotUnit != null && this.pilotUnit != newPilotUnit)
            {
                this.DisChargePilot(150f, true, newPilotUnit);
            }
            this.ActuallyPilot(newPilotUnit);
        }

        protected virtual void ActuallyPilot(Unit PilotUnit)
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
            base.GetComponent<Collider>().enabled = true;
            base.GetComponent<Collider>().gameObject.layer = LayerMask.NameToLayer("FriendlyBarriers");
            base.SetOwner(PilotUnit.Owner);
        }

        protected virtual void DisChargePilot(float disChargeYI, bool stunPilot, Unit dischargedBy)
        {
            DisChargePilotRPC(disChargeYI, stunPilot, dischargedBy);
        }

        protected virtual void DisChargePilotRPC(float disChargeYI, bool stunPilot, Unit dischargedBy)
        {
            if (this.pilotUnit != dischargedBy)
            {
                this.pilotUnit.GetComponent<Renderer>().enabled = true;
                this.pilotUnit.DischargePilotingUnit(base.X, Mathf.Clamp(base.Y + 32f, -6f, 100000f), this.xI + ((!stunPilot) ? 0f : ((float)(UnityEngine.Random.Range(0, 2) * 2 - 1) * disChargeYI * 0.3f)), disChargeYI + 100f + ((this.pilotUnit.playerNum >= 0) ? 0f : (disChargeYI * 0.5f)), stunPilot);
                base.StopPlayerBubbles();
                this.pilotUnit = null;
                base.playerNum = -1;
                this.isHero = false;
                this.firingPlayerNum = -1;
                this.fire = false;
                this.hasBeenPiloted = true;
                this.DeactivateGun();
                if (this.health > 0)
                {
                    this.Damage(this.health + 1, DamageType.SelfEsteem, 0f, 0f, 0, this, base.X, base.Y);
                }
                base.SetSyncingInternal(false);
            }
        }

        protected override void PressHighFiveMelee(bool forceHighFive = false)
        {
            if (this.pilotUnitDelay <= 0f && this.pilotUnit && this.pilotUnit.IsMine)
            {
                this.DisChargePilot(130f, false, null);
            }
        }

        public override void Death(float xI, float yI, DamageObject damage)
        {
            if (damage == null || damage.damageType != DamageType.SelfEsteem)
            {
            }
            if (base.GetComponent<Collider>() != null)
            {
                base.GetComponent<Collider>().enabled = false;
            }
            if (this.enemyAI != null)
            {
                this.enemyAI.HideSpeachBubbles();
                this.OnlyDestroyScriptOnSync = true;
                UnityEngine.Object.Destroy(this.enemyAI);
            }
            this.DeactivateGun();
            base.Death(xI, yI, damage);
            if (this.pilotUnit)
            {
                this.DisChargePilot(150f, false, null);
            }
        }

        public override bool CanBeThrown()
        {
            return false;
        }

        protected override bool CanBeAffectedByWind()
        {
            return false;
        }

        protected override void CheckForTraps(ref float yIT)
        {
        }
        #endregion

        #region Animation
        protected override void ChangeFrame()
        {
            // Animate moving
            if ( Mathf.Abs(this.xI) > 5f )
            {
                AnimateRunning();
            }
            else
            {
                this.sprite.SetLowerLeftPixel(0, this.spritePixelHeight);
            }
        }

        protected override void AnimateRunning()
        {
            this.sprite.SetLowerLeftPixel(1 * this.spritePixelWidth, this.spritePixelHeight);
        }

        protected override void AnimateWallAnticipation()
        {
        }

        protected override void AnimateImpaled()
        {
        }

        protected override void RunAvatarRunning()
        {
        }

        protected override void ActivateGun()
        {
        }
        #endregion

        #region Movement
        // Overridden to change distance the raycasts are using to collision detect, the default distance didn't cover the size of the vehicle, which caused teleporting issues
        protected override bool ConstrainToCeiling(ref float yIT)
        {
            if (base.actionState == ActionState.Dead)
            {
                this.headHeight = this.deadHeadHeight;
                this.waistHeight = this.deadWaistHeight;
            }
            bool result = false;
            this.chimneyFlipConstrained = false;
            if (this.yI >= 0f || this.WallDrag)
            {
                if (((!this.right && base.transform.localScale.x < 0f && Physics.Raycast(new Vector3(base.X + 4f, base.Y + 1f, 0f), Vector3.up, out this.raycastHit, this.headHeight + 15f, this.groundLayer)) || (!this.left && base.transform.localScale.x > 0f && Physics.Raycast(new Vector3(base.X + -4f, base.Y + 1f, 0f), Vector3.up, out this.raycastHit, this.headHeight + 15f, this.groundLayer))) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                {
                    result = true;
                    this.lastKnifeClimbStabY -= this.t * 16f;
                    if ((this.up || this.buttonJump) && this.chimneyFlip)
                    {
                        this.yI = 100f;
                        this.jumpTime = 0.03f;
                        yIT = this.raycastHit.point.y - this.headHeight - base.Y;
                        this.chimneyFlipConstrained = true;
                        if (this.chimneyFlipFrames > 8 && !Physics.Raycast(new Vector3(base.X + (float)this.chimneyFlipDirection, base.Y + 3f, 0f), Vector3.up, out this.newRaycastHit, this.headHeight + 16f + yIT, this.groundLayer))
                        {
                            this.xI = (float)(this.chimneyFlipDirection * 0);
                        }
                        else
                        {
                            this.xI = (float)(this.chimneyFlipDirection * 100);
                        }
                    }
                }
                if (Physics.Raycast(new Vector3(base.X + (halfWidth - (base.transform.localScale.x > 0 ? 20f : 10f)), base.Y + 1f, 0f), Vector3.up, out this.raycastHit, this.headHeight + 15f, this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                {
                    result = true;
                    this.lastKnifeClimbStabY -= this.t * 16f;
                    if (this.chimneyFlip)
                    {
                        this.chimneyFlipConstrained = true;
                    }
                    this.HitCeiling(this.raycastHit);
                }
                if (Physics.Raycast(new Vector3(base.X - (halfWidth - (base.transform.localScale.x < 0 ? 20f : 10f)), base.Y + 1f, 0f), Vector3.up, out this.raycastHit, this.headHeight + 15f, this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                {
                    result = true;
                    this.lastKnifeClimbStabY -= this.t * 16f;
                    if (this.chimneyFlip)
                    {
                        this.chimneyFlipConstrained = true;
                    }
                    this.HitCeiling(this.raycastHit);
                }
                if ( !result )
                {
                    if (Physics.Raycast(new Vector3(base.X + (halfWidth), base.Y + 1f, 0f), Vector3.up, out this.raycastHit, this.headHeight - 10f, this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                    {
                        result = true;
                        this.lastKnifeClimbStabY -= this.t * 16f;
                        if (this.chimneyFlip)
                        {
                            this.chimneyFlipConstrained = true;
                        }
                        this.HitCeiling(this.raycastHit);
                    }
                    if (Physics.Raycast(new Vector3(base.X - (halfWidth), base.Y + 1f, 0f), Vector3.up, out this.raycastHit, this.headHeight - 10f, this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                    {
                        result = true;
                        this.lastKnifeClimbStabY -= this.t * 16f;
                        if (this.chimneyFlip)
                        {
                            this.chimneyFlipConstrained = true;
                        }
                        this.HitCeiling(this.raycastHit);
                    }
                }
            }
            if ((!this.chimneyFlipConstrained || this.up || this.buttonJump) && this.chimneyFlip && this.chimneyFlipFrames > 5)
            {
                this.xI = (float)(this.chimneyFlipDirection * 100);
                if (this.up || this.buttonJump)
                {
                    this.jumpTime = this.t + 0.001f;
                    this.yI = 100f;
                }
            }
            return result;
        }

        protected override void HitCeiling(RaycastHit ceilingHit)
        {
            base.HitCeiling(ceilingHit);
            BMLogger.Log("hit ceiling");
        }

        // Overridden to change distance the raycasts are using to collision detect, the default distance didn't cover the size of the vehicle, which caused teleporting issues
        protected override bool ConstrainToWalls(ref float yIT, ref float xIT)
        {
            if (!this.dashing || (this.left && this.xIBlast > 0f) || (this.right && this.xIBlast < 0f) || (!this.left && !this.right && Mathf.Abs(this.xIBlast) > 0f))
            {
                this.xIBlast *= 1f - this.t * 4f;
            }
            this.pushingTime -= this.t;
            this.wasWallDragging = this.wallDrag;
            bool flag = false;
            this.canTouchRightWalls = false;
            this.canTouchLeftWalls = false;
            this.wallDragTime -= this.t;
            this.wasConstrainedLeft = this.constrainedLeft;
            this.wasConstrainedRight = this.constrainedRight;
            this.constrainedLeft = false;
            this.constrainedRight = false;
            this.ConstrainToFragileBarriers(ref xIT, this.halfWidth);
            this.ConstrainToMookBarriers(ref xIT, this.halfWidth);
            this.row = (int)((base.Y + 16f) / 16f);
            this.collumn = (int)((base.X + 8f) / 16f);
            this.wasLedgeGrapple = this.ledgeGrapple;
            this.ledgeGrapple = false;
            float collisionDistance = 68f;
            if (Physics.Raycast(new Vector3(base.X + 2f, base.Y + this.waistHeight, 0f), Vector3.left, out this.raycastHitWalls, collisionDistance, this.groundLayer))
            {
                if (this.raycastHitWalls.point.x > base.X - this.halfWidth - 4f + xIT)
                {
                    this.canTouchLeftWalls = true;
                }
                if (this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT)
                {
                    this.constrainedLeft = true;
                    this.HitLeftWall();
                    if (this.airdashDirection == DirectionEnum.Left)
                    {
                        this.StopAirDashing();
                    }
                    if (base.actionState == ActionState.Jumping && this.left && !Physics.Raycast(new Vector3(base.X + 2f, base.Y + this.headHeight - 6f, 0f), Vector3.left, 31f, this.groundLayer))
                    {
                        if (!this.down && this.canLedgeGrapple)
                        {
                            this.LedgeGrapple(this.left, this.right, this.halfWidth, this.headHeight);
                        }
                        if (Map.IsBlockSolid(this.collumn - 1, this.row - 1) && Map.IsBlockSolid(this.collumn - 1, this.row + 1))
                        {
                            this.StartDucking();
                        }
                    }
                    else if (base.actionState == ActionState.Jumping && this.left && (this.yI <= this.maxWallClimbYI || this.wallClimbing) && this.canWallClimb && base.Y - this.groundHeight > 6f)
                    {
                        this.AssignWallTransform(this.raycastHitWalls.collider.transform);
                        flag = true;
                        this.SetAirdashAvailable();
                        if (!this.wasWallDragging)
                        {
                            if (this.yI < 0f)
                            {
                                this.yI = 0f;
                                yIT = 0f;
                            }
                            this.raycastHitWalls.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                            this.SetCurrentFootstepSound(this.raycastHitWalls.collider);
                            if (!this.wallClimbAnticipation && !this.usingSpecial)
                            {
                                base.frame = 0;
                            }
                            if (!this.useNewKnifeClimbingFrames)
                            {
                                this.knifeHand++;
                            }
                            this.ChangeFrame();
                        }
                        this.DeactivateGun();
                        if (!this.wasWallDragging || this.up || this.buttonJump)
                        {
                            this.wallDragTime = 0.2f;
                        }
                        this.StopRolling();
                        this.doubleJumpsLeft = 0;
                        this.ClampWallDragYI(ref yIT);
                    }
                    if (this.canPushBlocks && (base.actionState == ActionState.Running || base.actionState == ActionState.ClimbingLadder))
                    {
                        if (base.IsMine && Map.PushBlock(base.X, base.Y + this.waistHeight, Mathf.Sign(this.xI), this.width + 1f))
                        {
                            this.PlayPushBlockSound();
                        }
                        this.AssignPushingTime();
                    }
                    this.xI = 0f;
                    if (!this.left)
                    {
                        this.xIBlast = Mathf.Abs(this.xIBlast * 0.4f);
                    }
                    else
                    {
                        this.xIBlast = 0f;
                    }
                    xIT = this.raycastHitWalls.point.x - (base.X - this.halfWidth);
                    this.WallDrag = flag;
                    return true;
                }
            }
            if (Physics.Raycast(new Vector3(base.X + 2f, base.Y + this.toeHeight, 0f), Vector3.left, out this.raycastHitWalls, collisionDistance, this.groundLayer))
            {
                if (this.raycastHitWalls.point.x > base.X - this.halfWidth - 4f + xIT)
                {
                    this.canTouchLeftWalls = true;
                }
                if (this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT)
                {
                    this.constrainedLeft = true;
                    this.HitLeftWall();
                    if (this.airdashDirection == DirectionEnum.Left)
                    {
                        this.StopAirDashing();
                    }
                    xIT = this.raycastHitWalls.point.x - (base.X - this.halfWidth);
                    if (base.actionState == ActionState.Jumping && this.left)
                    {
                        if (!this.down && this.canLedgeGrapple)
                        {
                            this.LedgeGrapple(this.left, this.right, this.halfWidth, this.headHeight);
                        }
                        if (Map.IsBlockSolid(this.collumn - 1, this.row - 1) && Map.IsBlockSolid(this.collumn - 1, this.row + 1))
                        {
                            this.StartDucking();
                        }
                    }
                    else if (this.canDuck && base.actionState == ActionState.Running && this.left && !Map.IsBlockSolid(this.collumn - 1, this.row) && Map.IsBlockSolid(this.collumn - 1, this.row - 1) && Map.IsBlockSolid(this.collumn - 1, this.row + 1))
                    {
                        this.StartDucking();
                    }
                    else if (base.actionState == ActionState.Jumping && this.left && (this.yI <= this.maxWallClimbYI || this.wallClimbing) && this.canWallClimb && base.Y - this.groundHeight > 6f)
                    {
                        this.AssignWallTransform(this.raycastHitWalls.collider.transform);
                        flag = true;
                        this.SetAirdashAvailable();
                        if (!this.wasWallDragging)
                        {
                            if (this.yI < 0f)
                            {
                                this.yI = 0f;
                                yIT = 0f;
                            }
                            this.raycastHitWalls.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                            this.SetCurrentFootstepSound(this.raycastHitWalls.collider);
                            if (!this.wallClimbAnticipation && !this.usingSpecial)
                            {
                                base.frame = 0;
                            }
                            if (!this.useNewKnifeClimbingFrames)
                            {
                                this.knifeHand++;
                            }
                            this.ChangeFrame();
                        }
                        this.DeactivateGun();
                        if (!this.wasWallDragging || this.up || this.buttonJump)
                        {
                            this.wallDragTime = 0.2f;
                        }
                        this.StopRolling();
                        this.doubleJumpsLeft = 0;
                        this.ClampWallDragYI(ref yIT);
                    }
                    if (this.canPushBlocks && (base.actionState == ActionState.Running || base.actionState == ActionState.ClimbingLadder))
                    {
                        if (base.IsMine && Map.PushBlock(base.X, base.Y + this.waistHeight, Mathf.Sign(this.xI), this.width + 1f))
                        {
                            this.PlayPushBlockSound();
                        }
                        this.AssignPushingTime();
                    }
                    this.xI = 0f;
                    if (!this.left)
                    {
                        this.xIBlast = Mathf.Abs(this.xIBlast * 0.4f);
                    }
                    else
                    {
                        this.xIBlast = 0f;
                    }
                    xIT = this.raycastHitWalls.point.x - (base.X - this.halfWidth);
                    this.WallDrag = flag;
                    return true;
                }
            }
            if (Physics.Raycast(new Vector3(base.X + 2f, base.Y + this.headHeight - 3f, 0f), Vector3.left, out this.raycastHitWalls, collisionDistance, this.groundLayer) && this.raycastHitWalls.point.x > base.X - (this.halfWidth) + xIT)
            {
                this.constrainedLeft = true;
                if (this.canDuck && Map.IsBlockSolid(this.collumn - 1, this.row - 1) && Map.IsBlockSolid(this.collumn - 1, this.row + 1))
                {
                    this.StartDucking();
                }
                if (this.canPushBlocks && (base.actionState == ActionState.Running || base.actionState == ActionState.ClimbingLadder) && base.IsMine && Map.PushBlock(base.X, base.Y + this.waistHeight, Mathf.Sign(this.xI), this.width + 1f))
                {
                    this.PlayPushBlockSound();
                }
                this.xI = 0f;
                if (!this.left)
                {
                    this.xIBlast = Mathf.Abs(this.xIBlast * 0.4f);
                }
                else
                {
                    this.xIBlast = 0f;
                }
                xIT = this.raycastHitWalls.point.x - (base.X - (this.halfWidth));
                this.WallDrag = flag;
                return true;
            }
            if (Physics.Raycast(new Vector3(base.X - 2f, base.Y + this.waistHeight, 0f), Vector3.right, out this.raycastHitWalls, collisionDistance, this.groundLayer))
            {
                if (this.raycastHitWalls.point.x < base.X + this.halfWidth + 4f + xIT)
                {
                    this.canTouchRightWalls = true;
                }
                if (this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT)
                {
                    this.constrainedRight = true;
                    this.HitRightWall();
                    if (this.airdashDirection == DirectionEnum.Right)
                    {
                        this.StopAirDashing();
                    }
                    if (base.actionState == ActionState.Jumping && this.right && !Physics.Raycast(new Vector3(base.X - 2f, base.Y + this.headHeight - 6f, 0f), Vector3.right, 23f, this.groundLayer))
                    {
                        if (!this.down && this.canLedgeGrapple)
                        {
                            this.LedgeGrapple(this.left, this.right, this.halfWidth, this.headHeight);
                        }
                        if (Map.IsBlockSolid(this.collumn + 1, this.row - 1) && Map.IsBlockSolid(this.collumn + 1, this.row + 1))
                        {
                            this.StartDucking();
                        }
                    }
                    else if (base.actionState == ActionState.Jumping && (this.yI <= this.maxWallClimbYI || this.wallClimbing) && this.right && this.canWallClimb && base.Y - this.groundHeight > 6f)
                    {
                        this.AssignWallTransform(this.raycastHitWalls.collider.transform);
                        flag = true;
                        this.SetAirdashAvailable();
                        if (!this.wasWallDragging)
                        {
                            if (this.yI < 0f)
                            {
                                this.yI = 0f;
                                yIT = 0f;
                            }
                            this.raycastHitWalls.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                            this.SetCurrentFootstepSound(this.raycastHitWalls.collider);
                            if (!this.wallClimbAnticipation && !this.usingSpecial)
                            {
                                base.frame = 0;
                            }
                            if (!this.useNewKnifeClimbingFrames)
                            {
                                this.knifeHand++;
                            }
                            this.ChangeFrame();
                        }
                        this.DeactivateGun();
                        if (!this.wasWallDragging || this.up || this.buttonJump)
                        {
                            this.wallDragTime = 0.2f;
                        }
                        this.StopRolling();
                        this.doubleJumpsLeft = 0;
                        this.ClampWallDragYI(ref yIT);
                    }
                    if (this.canPushBlocks && (base.actionState == ActionState.Running || base.actionState == ActionState.ClimbingLadder))
                    {
                        if (base.IsMine && Map.PushBlock(base.X, base.Y + this.waistHeight, Mathf.Sign(this.xI), this.width + 1f))
                        {
                            this.PlayPushBlockSound();
                        }
                        this.AssignPushingTime();
                    }
                    this.xI = 0f;
                    if (!this.right)
                    {
                        this.xIBlast = -Mathf.Abs(this.xIBlast * 0.4f);
                    }
                    else
                    {
                        this.xIBlast = 0f;
                    }
                    xIT = this.raycastHitWalls.point.x - (base.X + this.halfWidth);
                    this.WallDrag = flag;
                    return true;
                }
            }
            if (Physics.Raycast(new Vector3(base.X - 2f, base.Y + this.toeHeight, 0f), Vector3.right, out this.raycastHitWalls, collisionDistance, this.groundLayer))
            {
                if (this.raycastHitWalls.point.x < base.X + this.halfWidth + 4f + xIT)
                {
                    this.canTouchRightWalls = true;
                }
                if (this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT)
                {
                    this.constrainedRight = true;
                    this.HitRightWall();
                    if (this.airdashDirection == DirectionEnum.Right)
                    {
                        this.StopAirDashing();
                    }
                    if (base.actionState == ActionState.Jumping && this.right)
                    {
                        if (!this.down && this.canLedgeGrapple)
                        {
                            this.LedgeGrapple(this.left, this.right, this.halfWidth, this.headHeight);
                        }
                        if (Map.IsBlockSolid(this.collumn + 1, this.row - 1) && Map.IsBlockSolid(this.collumn + 1, this.row + 1))
                        {
                            this.StartDucking();
                        }
                    }
                    else if (this.canDuck && base.actionState == ActionState.Running && this.right && !Map.IsBlockSolid(this.collumn + 1, this.row) && Map.IsBlockSolid(this.collumn + 1, this.row - 1) && Map.IsBlockSolid(this.collumn + 1, this.row + 1))
                    {
                        this.StartDucking();
                    }
                    else if (base.actionState == ActionState.Jumping && this.right && (this.yI <= this.maxWallClimbYI || this.wallClimbing) && this.canWallClimb && base.Y - this.groundHeight > 6f)
                    {
                        this.AssignWallTransform(this.raycastHitWalls.collider.transform);
                        flag = true;
                        this.SetAirdashAvailable();
                        if (!this.wasWallDragging)
                        {
                            if (this.yI < 0f)
                            {
                                this.yI = 0f;
                                yIT = 0f;
                            }
                            this.raycastHitWalls.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                            this.SetCurrentFootstepSound(this.raycastHitWalls.collider);
                            if (!this.wallClimbAnticipation && !this.usingSpecial)
                            {
                                base.frame = 0;
                            }
                            if (!this.useNewKnifeClimbingFrames)
                            {
                                this.knifeHand++;
                            }
                            this.ChangeFrame();
                        }
                        this.DeactivateGun();
                        if (!this.wasWallDragging || this.up || this.buttonJump)
                        {
                            this.wallDragTime = 0.2f;
                        }
                        this.StopRolling();
                        this.doubleJumpsLeft = 0;
                        this.ClampWallDragYI(ref yIT);
                    }
                    xIT = this.raycastHitWalls.point.x - (base.X + this.halfWidth);
                    if (this.canPushBlocks && (base.actionState == ActionState.Running || base.actionState == ActionState.ClimbingLadder))
                    {
                        if (base.IsMine && Map.PushBlock(base.X, base.Y + this.waistHeight, Mathf.Sign(this.xI), this.width + 1f))
                        {
                            this.PlayPushBlockSound();
                        }
                        this.AssignPushingTime();
                    }
                    this.xI = 0f;
                    if (!this.right)
                    {
                        this.xIBlast = -Mathf.Abs(this.xIBlast * 0.4f);
                    }
                    else
                    {
                        this.xIBlast = 0f;
                    }
                    this.WallDrag = flag;
                    return true;
                }
            }
            if (Physics.Raycast(new Vector3(base.X - 2f, base.Y + this.headHeight - 3f, 0f), Vector3.right, out this.raycastHitWalls, collisionDistance, this.groundLayer) && this.raycastHitWalls.point.x < base.X + (this.halfWidth) + xIT)
            {
                this.constrainedRight = true;
                if (this.canDuck && Map.IsBlockSolid(this.collumn + 1, this.row - 1) && Map.IsBlockSolid(this.collumn + 1, this.row + 1))
                {
                    this.StartDucking();
                }
                if (this.canPushBlocks && (base.actionState == ActionState.Running || base.actionState == ActionState.ClimbingLadder))
                {
                    if (base.IsMine && Map.PushBlock(base.X, base.Y + this.waistHeight, Mathf.Sign(this.xI), this.width + 1f))
                    {
                        this.PlayPushBlockSound();
                    }
                    this.AssignPushingTime();
                }
                this.xI = 0f;
                if (!this.right)
                {
                    this.xIBlast = -Mathf.Abs(this.xIBlast * 0.4f);
                }
                else
                {
                    this.xIBlast = 0f;
                }
                xIT = this.raycastHitWalls.point.x - (base.X + (this.halfWidth));
                this.WallDrag = flag;
                return true;
            }
            this.WallDrag = flag;
            return false;
        }

        protected override void AddSpeedLeft()
        {
            if (this.holdStillTime <= 0f)
            {
                if (this.xI > -25f)
                {
                    this.xI = -25f;
                }
                this.xI -= this.speed * (this.dashing ? 1f : 0.5f) * this.t;
            }
            if (this.xI < -((!this.dashing || this.ducking) ? this.GetSpeed : (this.GetSpeed * this.dashSpeedM)))
            {
                this.xI = -((!this.dashing || this.ducking) ? this.GetSpeed : (this.GetSpeed * this.dashSpeedM));
            }
            else if (this.xI > -50f && this.holdStillTime <= 0f)
            {
                this.xI -= this.speed * 2.6f * this.t * ((!this.IsParachuteActive) ? 1f : 0.5f);
            }
        }

        protected override void AddSpeedRight()
        {
            if (this.holdStillTime <= 0f)
            {
                if (this.xI < 25f)
                {
                    this.xI = 25f;
                }
                this.xI += this.speed * (this.dashing ? 1f : 0.5f) * this.t;
            }
            if (this.xI > ((!this.dashing || this.ducking) ? this.GetSpeed : (this.GetSpeed * this.dashSpeedM)))
            {
                this.xI = ((!this.dashing || this.ducking) ? this.GetSpeed : (this.GetSpeed * this.dashSpeedM));
            }
            else if (this.xI < 50f && this.holdStillTime <= 0f)
            {
                this.xI += this.speed * 2.6f * this.t * ((!this.IsParachuteActive) ? 1f : 0.5f);
            }
        }

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

        protected override void Land()
        {
            if ((!this.isHero && this.yI < this.fallDamageHurtSpeed) || (this.isHero && this.yI < this.fallDamageHurtSpeedHero))
            {
                if ((!this.isHero && this.yI < this.fallDamageDeathSpeed) || (this.isHero && this.yI < this.fallDamageDeathSpeedHero))
                {
                    this.crushingGroundLayers = 2;
                    //MapController.DamageGround(this, 10, DamageType.Crush, 32f, base.X, base.Y + 8f, null, false);
                    SortOfFollow.Shake(0.3f);
                    EffectsController.CreateGroundWave(base.X, base.Y, 96f);
                    Map.ShakeTrees(base.X, base.Y, 144f, 64f, 128f);
                }
                else
                {
                    if (this.isHero)
                    {
                        if (this.yI <= this.fallDamageDeathSpeedHero)
                        {
                            this.crushingGroundLayers = 2;
                        }
                        else if (this.yI < this.fallDamageHurtSpeedHero * 0.3f + this.fallDamageDeathSpeedHero * 0.7f)
                        {
                            this.crushingGroundLayers = 1;
                        }
                        else if (this.yI < (this.fallDamageHurtSpeedHero + this.fallDamageDeathSpeedHero) / 2f)
                        {
                            this.crushingGroundLayers = 0;
                        }
                    }
                    else if (this.yI < (this.fallDamageHurtSpeed + this.fallDamageDeathSpeed) / 2f)
                    {
                        this.crushingGroundLayers = 2;
                    }
                    //MapController.DamageGround(this, 10, DamageType.Crush, 32f, base.X, base.Y + 8f, null, false);
                    SortOfFollow.Shake(0.3f);
                    EffectsController.CreateGroundWave(base.X, base.Y, 80f);
                    Map.ShakeTrees(base.X, base.Y, 144f, 64f, 128f);
                }
            }
            else if (this.crushingGroundLayers > 0)
            {
                this.crushingGroundLayers--;
                //MapController.DamageGround(this, 10, DamageType.Crush, 32f, base.X, base.Y + 8f, null, false);
                SortOfFollow.Shake(0.3f);
                Map.ShakeTrees(base.X, base.Y, 80f, 48f, 100f);
            }
            else if (this.yI < -60f && this.health > 0)
            {
                this.PlayFootStepSound(this.soundHolderFootSteps.landMetalSounds, 0.55f, 0.9f);
                this.gunFrame = 0;
                EffectsController.CreateGroundWave(base.X, base.Y + 10f, 64f);
                SortOfFollow.Shake(0.2f);
            }
            else if (this.health > 0)
            {
                this.PlayFootStepSound(this.soundHolderFootSteps.landMetalSounds, 0.35f, 0.9f);
                SortOfFollow.Shake(0.1f);
                this.gunFrame = 0;
            }
            base.Land();
        }

        protected virtual void CrushGroundWhileMoving(int damageGroundAmount, int damageUnitsAmount, float xRange, float yRange, float unitsXRange, float unitsYRange, float xOffset, float yOffset)
        {
            if (this.left)
            {
                if (Map.HitLivingUnits(this, playerNum, damageUnitsAmount, DamageType.Crush, unitsXRange, unitsYRange, base.X - xOffset, base.Y + yOffset, this.xI, 40f, true, true, false, true))
                {
                    this.PlaySpecial2Sound(0.33f);
                }
                
                MapController.DamageGround(this, damageGroundAmount, DamageType.Crush, xRange, yRange, base.X - xOffset, base.Y + yOffset, false);
                if (Physics.Raycast(new Vector3(base.X - xOffset, base.Y + yOffset, 0f), Vector3.left, out this.raycastHit, xRange, this.fragileLayer) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null)
                {
                    this.raycastHit.collider.gameObject.SendMessage("Damage", new DamageObject(1, DamageType.Crush, this.xI, this.yI, this.raycastHit.point.x, this.raycastHit.point.y, this));
                }
            }
            else if (this.right || (this.boostingForward && base.transform.localScale.x > 0))
            {
                if (Map.HitLivingUnits(this, playerNum, damageUnitsAmount, DamageType.Crush, unitsXRange, unitsYRange, base.X + xOffset, base.Y + yOffset, this.xI, 40f, true, true, false, true))
                {
                    this.PlaySpecial2Sound(0.33f);
                }

                MapController.DamageGround(this, damageGroundAmount, DamageType.Crush, xRange, yRange, base.X + xOffset, base.Y + yOffset, false);
                if (Physics.Raycast(new Vector3(base.X + xOffset, base.Y + yOffset, 0f), Vector3.right, out this.raycastHit, xRange, this.fragileLayer) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null)
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
