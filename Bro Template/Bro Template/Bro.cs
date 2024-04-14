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
    public class Bro : CustomHero
    {
        // General

        // Primary

        // Melee

        // Special

        protected bool acceptedDeath = false;
        bool wasInvulnerable = false;

        #region General
        protected override void Update()
        {
            base.Update();
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
            }
        }
        #endregion

        #region Primary
        #endregion

        #region Melee
        #endregion

        #region Special
        #endregion
    }
}
