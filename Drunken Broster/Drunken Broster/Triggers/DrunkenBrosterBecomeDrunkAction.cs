using RocketLib.CustomTriggers;

namespace Drunken_Broster.Triggers
{
    public class DrunkenBrosterBecomeDrunkAction : CustomTriggerAction<DrunkenBrosterBecomeDrunkActionInfo>
    {
        public override void Start()
        {
            base.Start();

            if ( info.becomeSober )
            {
                CustomTriggerStateManager.SetDuringLevel<bool>( "DrunkenBroster_InfiniteDrunk", false );
                DrunkenBroster.ForceAllSoberUp();
            }
            else
            {
                CustomTriggerStateManager.SetDuringLevel( "DrunkenBroster_InfiniteDrunk", true );
                DrunkenBroster.ForceAllDrunk();
            }

            this.state = TriggerActionState.Done;
        }

        public override void Update() { }
    }
}
