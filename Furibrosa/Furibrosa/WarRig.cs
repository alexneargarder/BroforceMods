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
        protected float frontHeadHeight;
        protected float distanceToFront;
        protected float distanceToBack;
        public BoxCollider platform;

        protected int crushDamage = 5;
        protected float crushXRange = 40f;
        protected float crushYRange = 50f;
        protected float crushXOffset = 53f;
        protected float crushYOffset = 30f;
        protected float unitXRange = 50f;
        protected float unitYRange = 20f;
        protected float crushDamageCooldown = 0f;

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
            sprite.offset = new Vector3(0f, 31f, 0f);

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
            // Prevent tank from standing on platforms and ladders
            this.platformLayer = this.groundLayer;
            this.ladderLayer = this.groundLayer;
            this.speed = 200f;
            this.originalSpeed = 200f;
            this.waistHeight = 10f;
            this.deadWaistHeight = 10f;
            this.height = 52f;
            this.headHeight = this.height;
            this.standingHeadHeight = this.height;
            this.deadHeadHeight = this.height;
            this.frontHeadHeight = 32f;
            this.halfWidth = 63f;
            this.feetWidth = 58f;
            this.width = 62f;
            this.distanceToFront = 32f;
            this.distanceToBack = 49f;
            this.doRollOnLand = false;
            this.canChimneyFlip = false;
            this.canWallClimb = false;
            this.canTumble = false;
            this.canDuck = false;
            this.canLedgeGrapple = false;
            this.jumpForce = 360;
            this.DeactivateGun();

            GameObject platformObject = this.gameObject.FindChildOfName("Platform");
            if ( platformObject != null )
            {
                this.platform = platformObject.GetComponent<BoxCollider>();
                this.platform.center = new Vector3(-9f, 44f, -4.5f);
                this.platform.size = new Vector3(80f, 12f, 64f);
            }
        }

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

            return;
            if ( crushDamageCooldown <= 0f )
            {
                if( this.CrushGroundWhileMoving((int)Mathf.Max(Mathf.Round(Mathf.Abs(this.xI / 200f) * crushDamage), 1f), 25, crushXRange, crushYRange, unitXRange, unitYRange, crushXOffset, crushYOffset) )
                {
                    if ( Mathf.Abs(xI) < 150f )
                    {
                        crushDamageCooldown = 0.1f;
                    }
                }
            }
            else
            {
                crushDamageCooldown -= this.t;
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

        protected override void CheckRescues()
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
                if ( base.transform.localScale.x > 0 )
                {
                    // Check top middle of vehicle left to right
                    Vector3 topLeft = new Vector3(base.X - distanceToBack, base.Y + this.headHeight, 0f);
                    Vector3 topRight = new Vector3(base.X + distanceToFront, base.Y + this.headHeight, 0f);
                    //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                    if ( Physics.Raycast(topLeft, Vector3.right, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT )
                    {
                        result = true;
                        this.HitCeiling(this.raycastHit);
                    }

                    if ( !result )
                    {
                        // Check top middle of vehicle right to left
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                        if (Physics.Raycast(topRight, Vector3.left, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit);
                        }
                    }
                    
                    // Check front of vehicle left to right
                    if ( !result )
                    {
                        topLeft = new Vector3(base.X + distanceToFront, base.Y + frontHeadHeight, 0f);
                        topRight = new Vector3(base.X + halfWidth - 1f, base.Y + frontHeadHeight, 0f);
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if (Physics.Raycast(topLeft, Vector3.right, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.frontHeadHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit, frontHeadHeight);
                        }
                    }

                    // Check front of vehicle right to left
                    if (!result)
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if (Physics.Raycast(topRight, Vector3.left, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.frontHeadHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit, frontHeadHeight);
                        }
                    }
                }
                else
                {
                    // Check top middle of vehicle left to right
                    Vector3 topLeft = new Vector3(base.X - distanceToFront, base.Y + this.headHeight, 0f);
                    Vector3 topRight = new Vector3(base.X + distanceToBack, base.Y + this.headHeight, 0f);
                    //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                    if (Physics.Raycast(topLeft, Vector3.right, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                    {
                        result = true;
                        this.HitCeiling(this.raycastHit);
                    }

                    if (!result)
                    {
                        // Check top middle of vehicle right to left
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling", topLeft, topRight, Color.red);
                        if (Physics.Raycast(topRight, Vector3.left, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.headHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit);
                        }
                    }

                    // Check front of vehicle left to right
                    if (!result)
                    {
                        topLeft = new Vector3(base.X - halfWidth + 1f, base.Y + frontHeadHeight, 0f);
                        topRight = new Vector3(base.X - distanceToFront, base.Y + frontHeadHeight, 0f);
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if (Physics.Raycast(topLeft, Vector3.right, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.frontHeadHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit, frontHeadHeight);
                        }
                    }

                    // Check front of vehicle right to left
                    if (!result)
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("ceiling3", topLeft, topRight, Color.red);

                        if (Physics.Raycast(topRight, Vector3.left, out this.raycastHit, Mathf.Abs(topRight.x - topLeft.x), this.groundLayer) && this.raycastHit.point.y < base.Y + this.frontHeadHeight + yIT)
                        {
                            result = true;
                            this.HitCeiling(this.raycastHit, frontHeadHeight);
                        }
                    }
                }
            }
            return result;
        }

        protected void HitCeiling(RaycastHit ceilingHit, float customHeight)
        {
            if (this.up || this.buttonJump)
            {
                ceilingHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
            }
            this.yIT = ceilingHit.point.y - customHeight - base.Y;
            if (!this.chimneyFlip && this.yI > 100f && ceilingHit.collider != null)
            {
                this.currentFootStepGroundType = ceilingHit.collider.tag;
                this.PlayFootStepSound(0.2f, 0.6f);
            }
            this.yI = 0f;
            this.jumpTime = 0f;
            if ((this.canCeilingHang && this.CanCheckClimbAlongCeiling() && (this.up || this.buttonJump)) || this.hangGrace > 0f)
            {
                this.StartHanging();
            }

            //RocketLib.Utils.DrawDebug.DrawLine("ceillingHit", ceilingHit.point, ceilingHit.point + new Vector3(5f, 0f, 0f), Color.green);
        }

        protected override void HitCeiling(RaycastHit ceilingHit)
        {
            base.HitCeiling(ceilingHit);

            //RocketLib.Utils.DrawDebug.DrawLine("ceillingHit", ceilingHit.point, ceilingHit.point + new Vector3(3f, 0f, 0f), Color.green);
        }

        // Overridden to change distance the raycasts are using to collision detect, the default distance didn't cover the size of the vehicle, which caused teleporting issues
        protected override bool ConstrainToWalls(ref float yIT, ref float xIT)
        {
            if (!this.dashing || (this.left && this.xIBlast > 0f) || (this.right && this.xIBlast < 0f) || (!this.left && !this.right && Mathf.Abs(this.xIBlast) > 0f))
            {
                this.xIBlast *= 1f - this.t * 4f;
            }
            this.pushingTime -= this.t;
            this.canTouchRightWalls = false;
            this.canTouchLeftWalls = false;
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

            if (base.transform.localScale.x > 0)
            {
                // Check front of vehicle
                Vector3 bottomRight = new Vector3(base.X + this.halfWidth, base.Y, 0);
                Vector3 topRight = new Vector3(base.X + this.halfWidth, base.Y + this.frontHeadHeight, 0);
                //RocketLib.Utils.DrawDebug.DrawLine("wall", bottomRight, topRight, Color.red);
                if (Physics.Raycast(bottomRight, Vector3.up, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x - 2f, raycastHitWalls.point.y, 0f), Vector3.right, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.green);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X + this.halfWidth);
                        return true;
                    }
                }

                if (Physics.Raycast(topRight, Vector3.down, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y) - 0.5f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x - 2f, raycastHitWalls.point.y, 0f), Vector3.right, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.halfWidth + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.green);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X + this.halfWidth);
                        return true;
                    }
                }

                // Check top front of vehicle
                bottomRight = new Vector3(base.X + this.distanceToFront, base.Y + this.headHeight, 0);
                topRight = new Vector3(base.X + this.distanceToFront, base.Y + this.frontHeadHeight, 0);
                //RocketLib.Utils.DrawDebug.DrawLine("wall2", bottomRight, topRight, Color.red);
                if (Physics.Raycast(bottomRight, Vector3.up, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.distanceToFront + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x - 2f, raycastHitWalls.point.y, 0f), Vector3.right, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.distanceToFront + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.green);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X + (this.halfWidth - distanceToFront));
                        return true;
                    }
                }

                if (Physics.Raycast(topRight, Vector3.down, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.distanceToFront + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x - 2f, raycastHitWalls.point.y, 0f), Vector3.right, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x < base.X + this.distanceToFront + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.green);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X + (this.halfWidth - distanceToFront));
                        return true;
                    }
                }
            }
            else
            {
                // Check front of vehicle
                Vector3 bottomRight = new Vector3(base.X - this.halfWidth, base.Y, 0);
                Vector3 topRight = new Vector3(base.X - this.halfWidth, base.Y + this.frontHeadHeight, 0);
                //RocketLib.Utils.DrawDebug.DrawLine("wall", bottomRight, topRight, Color.red);
                if (Physics.Raycast(bottomRight, Vector3.up, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x + 2f, raycastHitWalls.point.y, 0f), Vector3.left, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.green);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X - this.halfWidth);
                        return true;
                    }
                }

                if (Physics.Raycast(topRight, Vector3.down, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y) - 0.5f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x + 2f, raycastHitWalls.point.y, 0f), Vector3.left, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.halfWidth + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.green);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X - this.halfWidth);
                        return true;
                    }
                }

                // Check top front of vehicle
                bottomRight = new Vector3(base.X - this.distanceToFront, base.Y + this.headHeight, 0);
                topRight = new Vector3(base.X - this.distanceToFront, base.Y + this.frontHeadHeight, 0);
                //RocketLib.Utils.DrawDebug.DrawLine("wall2", bottomRight, topRight, Color.red);
                if (Physics.Raycast(bottomRight, Vector3.up, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.distanceToFront + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x + 2f, raycastHitWalls.point.y, 0f), Vector3.left, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.distanceToFront + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.green);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X - (this.halfWidth - distanceToFront));
                        return true;
                    }
                }

                if (Physics.Raycast(topRight, Vector3.down, out this.raycastHitWalls, Mathf.Abs(topRight.y - bottomRight.y), this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.distanceToFront + xIT)
                {
                    if ((Physics.Raycast(new Vector3(bottomRight.x + 2f, raycastHitWalls.point.y, 0f), Vector3.left, out this.raycastHitWalls, 10f, this.groundLayer) && this.raycastHitWalls.point.x > base.X - this.distanceToFront + xIT))
                    {
                        //RocketLib.Utils.DrawDebug.DrawLine("wallhit", raycastHitWalls.point, raycastHitWalls.point + new Vector3(3f, 0f, 0f), Color.green);
                        this.xI = 0f;
                        xIT = this.raycastHitWalls.point.x - (base.X - (this.halfWidth - distanceToFront));
                        return true;
                    }
                }
            }


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

        protected override bool CanJumpOffGround()
        {
            return this.CanTouchGround((float)((!this.right || this.canTouchLeftWalls || Physics.Raycast(new Vector3(base.X, base.Y + 5f, 0f), Vector3.left, out this.raycastHitWalls, 13.5f, this.groundLayer)) ? 0 : -13) + (float)((!this.left || this.canTouchRightWalls || Physics.Raycast(new Vector3(base.X, base.Y + 5f, 0f), Vector3.right, out this.raycastHitWalls, 13.5f, this.groundLayer)) ? 0 : 13) * ((!this.isInQuicksand) ? 1f : 0.4f));
        }

        protected new bool CanTouchGround(float xOffset)
        {
            LayerMask mask = this.GetGroundLayer();
            RaycastHit raycastHit;
            if (Physics.Raycast(new Vector3(base.X, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, mask))
            {
                this.SetCurrentFootstepSound(raycastHit.collider);
                return true;
            }
            if (Physics.Raycast(new Vector3(base.X - feetWidth, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, mask))
            {
                this.SetCurrentFootstepSound(raycastHit.collider);
                return true;
            }
            if (Physics.Raycast(new Vector3(base.X + feetWidth, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, mask))
            {
                this.SetCurrentFootstepSound(raycastHit.collider);
                return true;
            }
            if (xOffset != 0f && Physics.Raycast(new Vector3(base.X + xOffset, base.Y + 12f, 0f), Vector3.down, out raycastHit, 15f, mask))
            {
                this.SetCurrentFootstepSound(raycastHit.collider);
                return true;
            }
            if (!Map.IsBlockLadder(base.X, base.Y) && !this.down)
            {
                if (Physics.Raycast(new Vector3(base.X, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, this.ladderLayer))
                {
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    return true;
                }
                if (!Map.IsBlockLadder(base.X - 3f, base.Y) && !this.down && Physics.Raycast(new Vector3(base.X - 3f, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, this.ladderLayer))
                {
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    return true;
                }
                if (!Map.IsBlockLadder(base.X, base.Y) && !this.down && Physics.Raycast(new Vector3(base.X - 3f, base.Y + 14f, 0f), Vector3.down, out raycastHit, 16f, this.ladderLayer))
                {
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    return true;
                }
                if (xOffset != 0f && !Map.IsBlockLadder(base.X + xOffset, base.Y) && !this.down && Physics.Raycast(new Vector3(base.X + xOffset, base.Y + 12f, 0f), Vector3.down, out raycastHit, 15f, this.ladderLayer))
                {
                    this.SetCurrentFootstepSound(raycastHit.collider);
                    return true;
                }
            }
            return false;
        }

        protected override void Land()
        {
            if ((!this.isHero && this.yI < this.fallDamageHurtSpeed) || (this.isHero && this.yI < this.fallDamageHurtSpeedHero))
            {
                if ((!this.isHero && this.yI < this.fallDamageDeathSpeed) || (this.isHero && this.yI < this.fallDamageDeathSpeedHero))
                {
                    this.crushingGroundLayers = 2;
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
                    SortOfFollow.Shake(0.3f);
                    EffectsController.CreateGroundWave(base.X, base.Y, 80f);
                    Map.ShakeTrees(base.X, base.Y, 144f, 64f, 128f);
                }
            }
            else if (this.crushingGroundLayers > 0)
            {
                this.crushingGroundLayers--;
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
            this.jumpingMelee = false;
            this.timesKickedByVanDammeSinceLanding = 0;
            if (this.health > 0 && base.playerNum >= 0 && this.yI < -150f)
            {
                Map.BotherNearbyMooks(base.X, base.Y, 24f, 16f, base.playerNum);
            }
            this.FallDamage(this.yI);
            this.StopAirDashing();
            this.lastLandTime = Time.realtimeSinceStartup;
            if (this.yI < 0f && this.health > 0 && this.groundHeight > base.Y - 2f + this.yIT && this.yI < -70f)
            {
                EffectsController.CreateLandPoofEffect(base.X, this.groundHeight, (Mathf.Abs(this.xI) >= 30f) ? (-(int)base.transform.localScale.x) : 0, this.GetFootPoofColor());
            }
            if (this.health > 0)
            {
                if ((this.left || this.right) && (!this.left || !this.right))
                {
                    if (this.xI > 0f)
                    {
                        this.xI += 100f;
                    }
                    if (this.xI < 0f)
                    {
                        this.xI -= 100f;
                    }
                    base.actionState = ActionState.Running;
                    if (this.delayedDashing || (this.dashing && Time.time - this.leftTapTime > this.minDashTapTime && Time.time - this.rightTapTime > this.minDashTapTime))
                    {
                        this.StartDashing();
                    }
                    this.hasDashedInAir = false;
                    if (this.useNewFrames)
                    {
                        if (this.CanDoRollOnLand())
                        {
                            this.RollOnLand();
                        }
                        this.counter = 0f;
                        this.AnimateRunning();
                        if (!FluidController.IsSubmerged(this) && this.groundHeight > base.Y - 8f)
                        {
                            EffectsController.CreateFootPoofEffect(base.X, this.groundHeight + 1f, 0f, Vector3.up * 1f, BloodColor.None);
                        }
                    }
                }
                else
                {
                    this.StopRolling();
                    this.SetActionstateToIdle();
                }
            }
            if (this.yI < -50f)
            {
                if (Physics.Raycast(new Vector3(base.X, base.Y + 5f, 0f), Vector3.down, out this.raycastHit, 12f, this.groundLayer | Map.platformLayer))
                {
                    this.raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                }
                if (Physics.Raycast(new Vector3(base.X + 6f, base.Y + 5f, 0f), Vector3.down, out this.raycastHit, 12f, this.groundLayer | Map.platformLayer))
                {
                    this.raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                }
                if (Physics.Raycast(new Vector3(base.X - 6f, base.Y + 5f, 0f), Vector3.down, out this.raycastHit, 12f, this.groundLayer | Map.platformLayer))
                {
                    this.raycastHit.collider.SendMessage("StepOn", this, SendMessageOptions.DontRequireReceiver);
                }
            }
            bool flag = false;
            if (base.playerNum >= 0 && this.yI < -100f)
            {
                flag = this.PushGrassAway();
            }
            if (this.health > 0 && !flag && this.yI < -100f)
            {
                this.PlayLandSound();
            }
            if (this.bossBlockPieceCurrentlyStandingOn != null)
            {
                this.bossBlockPieceCurrentlyStandingOn.LandOn(this.yI);
            }
            if (this.blockCurrentlyStandingOn != null)
            {
                this.blockCurrentlyStandingOn.LandOn(this.yI);
            }
            this.yI = 0f;
            if (this.groundTransform != null)
            {
                this.lastParentedToTransform = this.groundTransform;
            }
            if (this.IsParachuteActive)
            {
                this.IsParachuteActive = false;
            }
        }

        protected override void FallDamage(float yI)
        {
            if ((!this.isHero && yI < this.fallDamageHurtSpeed) || (this.isHero && yI < this.fallDamageHurtSpeedHero))
            {
                if (this.health > 0)
                {
                    this.crushingGroundLayers = Mathf.Max(this.crushingGroundLayers, 2);
                }
                if ((!this.isHero && yI < this.fallDamageDeathSpeed) || (this.isHero && yI < this.fallDamageDeathSpeedHero))
                {
                    Map.KnockAndDamageUnit(SingletonMono<MapController>.Instance, this, this.health + 40, DamageType.Crush, -1f, 450f, 0, false);
                    Map.ExplodeUnits(this, 25, DamageType.Crush, 64f, 25f, base.X, base.Y, 200f, 170f, base.playerNum, false, false, true);
                }
                else
                {
                    Map.KnockAndDamageUnit(SingletonMono<MapController>.Instance, this, this.health - 10, DamageType.Crush, -1f, 450f, 0, false);
                    Map.ExplodeUnits(this, 10, DamageType.Crush, 48f, 20f, base.X, base.Y, 150f, 120f, base.playerNum, false, false, true);
                }
            }
        }

        protected virtual bool CrushGroundWhileMoving(int damageGroundAmount, int damageUnitsAmount, float xRange, float yRange, float unitsXRange, float unitsYRange, float xOffset, float yOffset)
        {
            bool hitGround = false;
            if (this.left)
            {
                if (Map.HitLivingUnits(this, playerNum, damageUnitsAmount, DamageType.Crush, unitsXRange, unitsYRange, base.X - xOffset, base.Y + yOffset, this.xI, 40f, true, true, false, true))
                {
                    this.PlaySpecial2Sound(0.33f);
                }

                hitGround = MapController.DamageGround(this, damageGroundAmount, DamageType.Crush, xRange, yRange, base.X - xOffset, base.Y + yOffset, true);
                if (Physics.Raycast(new Vector3(base.X - xOffset, base.Y + yOffset, 0f), Vector3.left, out this.raycastHit, xRange, this.fragileLayer) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null)
                {
                    this.raycastHit.collider.gameObject.SendMessage("Damage", new DamageObject(1, DamageType.Crush, this.xI, this.yI, this.raycastHit.point.x, this.raycastHit.point.y, this));
                    hitGround = true;
                }
            }
            else if (this.right || (this.boostingForward && base.transform.localScale.x > 0))
            {
                if (Map.HitLivingUnits(this, playerNum, damageUnitsAmount, DamageType.Crush, unitsXRange, unitsYRange, base.X + xOffset, base.Y + yOffset, this.xI, 40f, true, true, false, true))
                {
                    this.PlaySpecial2Sound(0.33f);
                }

                hitGround = MapController.DamageGround(this, damageGroundAmount, DamageType.Crush, xRange, yRange, base.X + xOffset, base.Y + yOffset, true);
                if (Physics.Raycast(new Vector3(base.X + xOffset, base.Y + yOffset, 0f), Vector3.right, out this.raycastHit, xRange, this.fragileLayer) && this.raycastHit.collider.gameObject.GetComponent<Parachute>() == null)
                {
                    this.raycastHit.collider.gameObject.SendMessage("Damage", new DamageObject(1, DamageType.Crush, this.xI, this.yI, this.raycastHit.point.x, this.raycastHit.point.y, this));
                    hitGround = true;
                }
            }
            // DEBUG
            //RocketLib.Utils.DrawDebug.DrawRectangle("ground", new Vector3(base.X + xOffset - xRange / 2f, base.Y + yOffset - yRange / 2f, 0f), new Vector3(base.X + xOffset + xRange / 2f, base.Y + yOffset + yRange / 2f, 0f), Color.red);

            return hitGround;
        }
        #endregion

        #region Primary
        protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
        {
        }
        #endregion

        #region Special
        protected override void PressSpecial()
        {
        }
        #endregion
    }
}
