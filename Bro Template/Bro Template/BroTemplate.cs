using System;
using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Bro_Template
{
    [HeroPreset("Bro Template", HeroType.Rambro)]
    public class BroTemplate : CustomHero
    {
        // General
        protected bool acceptedDeath = false;
        bool wasInvulnerable = false;

        // Primary

        // Melee

        // Special


        #region General
        protected override void Start()
        {
            base.Start();

            // Needed to have custom melee functions called, actual type is irrelevant
            this.meleeType = MeleeType.Disembowel;
        }

        protected override void Update()
        {
            base.Update();
            // Don't run any code past this point if the character is dead
            if (this.acceptedDeath)
            {
                if (this.health <= 0 && !this.WillReviveAlready)
                {
                    return;
                }
                // Revived
                else
                {
                    this.acceptedDeath = false;
                }
            }

            if (this.invulnerable)
            {
                this.wasInvulnerable = true;
            }

            // Check if invulnerability ran out
            if (this.wasInvulnerable && !this.invulnerable)
            {
                // Fix any not currently displayed textures
                this.wasInvulnerable = false;
            }

            // Check if character has died
            if (base.actionState == ActionState.Dead && !this.acceptedDeath)
            {
                if (!this.WillReviveAlready)
                {
                    this.acceptedDeath = true;
                }
            }
        }
        #endregion

        #region Primary
        #endregion

        #region Melee
        protected override void StartCustomMelee()
        {
            if (this.CanStartNewMelee())
            {
                base.frame = 1;
                base.counter = -0.05f;
                this.AnimateMelee();
            }
            else if (this.CanStartMeleeFollowUp())
            {
                this.meleeFollowUp = true;
            }
            if (!this.jumpingMelee)
            {
                this.dashingMelee = true;
                this.xI = (float)base.Direction * this.speed;
            }
            this.StartMeleeCommon();
        }

        protected override void AnimateCustomMelee()
        {
            this.AnimateMeleeCommon();
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
                this.PerformKnifeMeleeAttack(true, true);
            }
            else if (base.frame > 3 && !this.meleeHasHit)
            {
                this.PerformKnifeMeleeAttack(false, false);
            }
            if (base.frame >= 6)
            {
                base.frame = 0;
                this.CancelMelee();
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
                        if (!this.isInQuicksand)
                        {
                            this.xI = this.speed * 1f * base.transform.localScale.x;
                        }
                        this.yI = 0f;
                    }
                    else if (!this.isInQuicksand)
                    {
                        this.xI = this.speed * 0.5f * base.transform.localScale.x + (this.meleeChosenUnit.X - base.X) * 6f;
                    }
                }
                else if (base.frame <= 5)
                {
                    if (!this.isInQuicksand)
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
        #endregion

        #region Special
        #endregion
    }
}
