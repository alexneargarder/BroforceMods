using UnityModManagerNet;

namespace IronBro_Multiplayer_Mod
{
    public class Settings : UnityModManager.ModSettings
    {
        public bool helicopterWait;

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save( this, modEntry );
        }

    }
}
