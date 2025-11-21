using System.Collections.Generic;
using UnityModManagerNet;

namespace Swap_Bros_Mod
{
    public enum SortingMethod
    {
        UnlockOrder = 0,
        AlphabeticalAZ = 1,
        AlphabeticalZA = 2
    }

    public class Settings : UnityModManagerNet.UnityModManager.ModSettings
    {
        public bool alwaysChosen = false;
        public bool ignoreCurrentUnlocked = false;
        public bool includeExpendabros = false;
        public bool includeUnfinishedCharacters = false;
        public bool clickingEnabled = true;
        public bool enableBromaker = false;
        public bool enableBromakerDefault = true;
        public float swapCoolDown = 0.5f;

        public int[] selGridInt = { 0, 0, 0, 0 };
        public bool[] showSettings = { true, false, false, false };
        public KeyBind[] swapLeftKeys = { new KeyBind(), new KeyBind(), new KeyBind(), new KeyBind() };
        public KeyBind[] swapRightKeys = { new KeyBind(), new KeyBind(), new KeyBind(), new KeyBind() };

        public List<string> enabledBros = new List<string>();
        public bool filterBros = false;
        public bool useVanillaBroSelection = true;
        public bool ignoreForcedBros = false;

        public SortingMethod sorting = SortingMethod.UnlockOrder;
        public string sortingMethodName = "Sorting Method: Unlock Order";

        public override void Save( UnityModManager.ModEntry modEntry )
        {
            Save( this, modEntry );
        }
    }
}
