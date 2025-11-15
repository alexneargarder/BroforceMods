using System.Collections.Generic;
using RocketLib.CustomTriggers;

namespace Drunken_Broster.Triggers
{
    public class DrunkenBrosterMeleePoolAction : LevelStartTriggerAction<DrunkenBrosterMeleePoolActionInfo>
    {
        protected override void ExecuteAction( bool isLevelStart )
        {
            if ( info.resetToDefault )
            {
                CustomTriggerStateManager.SetDuringLevel<List<DrunkenBroster.MeleeItem>>( "DrunkenBroster_MeleePool", null );
                return;
            }

            List<DrunkenBroster.MeleeItem> itemPool = new List<DrunkenBroster.MeleeItem>();

            if ( info.enableTire ) itemPool.Add( DrunkenBroster.MeleeItem.Tire );
            if ( info.enableAcidEgg ) itemPool.Add( DrunkenBroster.MeleeItem.AcidEgg );
            if ( info.enableBeehive ) itemPool.Add( DrunkenBroster.MeleeItem.Beehive );
            if ( info.enableBottle ) itemPool.Add( DrunkenBroster.MeleeItem.Bottle );
            if ( info.enableCrate ) itemPool.Add( DrunkenBroster.MeleeItem.Crate );
            if ( info.enableCoconut ) itemPool.Add( DrunkenBroster.MeleeItem.Coconut );
            if ( info.enableExplosiveBarrel ) itemPool.Add( DrunkenBroster.MeleeItem.ExplosiveBarrel );
            if ( info.enableSoccerBall ) itemPool.Add( DrunkenBroster.MeleeItem.SoccerBall );
            if ( info.enableAlienEgg ) itemPool.Add( DrunkenBroster.MeleeItem.AlienEgg );
            if ( info.enableSkull ) itemPool.Add( DrunkenBroster.MeleeItem.Skull );

            if ( itemPool.Count > 0 )
            {
                if ( isLevelStart )
                {
                    CustomTriggerStateManager.SetForLevelStart( "DrunkenBroster_MeleePool", itemPool );
                }
                else
                {
                    CustomTriggerStateManager.SetDuringLevel( "DrunkenBroster_MeleePool", itemPool );
                }
            }
        }
    }
}
