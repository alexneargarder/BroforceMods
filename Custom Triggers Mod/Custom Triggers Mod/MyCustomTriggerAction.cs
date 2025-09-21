using UnityEngine;

namespace Custom_Triggers_Mod
{
    public class MyCustomTriggerAction : CustomTriggerAction
    {
        public override TriggerActionInfo Info
        {
            get
            {
                return this.info;
            }
            set
            {
                this.info = (MyCustomTriggerActionInfo)value;
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

        public MyCustomTriggerActionInfo info;
    }
}
