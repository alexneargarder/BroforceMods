using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityModManagerNet;

namespace Control_Enemies_Mod
{
    public class Settings : UnityModManager.ModSettings
    {
        public int count = 0;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

    }
}
