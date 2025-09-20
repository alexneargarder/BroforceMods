using UnityEngine;

namespace Custom_Triggers_Mod
{
    public class TestCustomTriggerAction : TriggerAction
    {
        public override TriggerActionInfo Info
        {
            get
            {
                return this.info;
            }
            set
            {
                this.info = (TriggerActionInfo)value;
            }
        }

        public override void Update()
        {
            //Main.Log( "testing 123" );
        }

        public override void Start()
        {
            base.Start();
            Main.Log( "in start" );
            this.state = TriggerActionState.Done;

            HeroController.players[0].character.GetComponent<SpriteSM>().SetColor( Color.blue );

        }

        public TriggerActionInfo info;
    }
}
