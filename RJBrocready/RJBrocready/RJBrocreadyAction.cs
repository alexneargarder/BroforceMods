using RocketLib.CustomTriggers;
using UnityEngine;

namespace RJBrocready
{
    class RJBrocreadyActionInfo : CustomTriggerActionInfo
    {
        public bool enableBecomingThing = true;
        public override void ShowGUI( LevelEditorGUI gui )
        {
            enableBecomingThing = GUILayout.Toggle( enableBecomingThing, "Enable transforming into the thing on death." );
            enableBecomingThing = !GUILayout.Toggle( !enableBecomingThing, "Disable transforming into the thing on death." );
        }
    }

    public class RJBrocreadyAction : CustomTriggerAction
    {
        RJBrocreadyActionInfo info;

        public override TriggerActionInfo Info
        {
            get
            {
                return this.info;
            }
            set
            {
                this.info = (RJBrocreadyActionInfo)value;
            }
        }

        public override void Start()
        {
            base.Start();

            for ( int i = 0; i < 4; ++i )
            {
                if ( HeroController.PlayerIsAlive( i ) )
                {
                    try
                    {
                        if ( HeroController.players[i].character is RJBrocready brocready )
                        {
                            brocready.SetCanBecomeThing( this.info.enableBecomingThing );
                        }
                    }
                    catch { }
                }
            }

            this.state = TriggerActionState.Done;
        }

        public override void Update()
        {
        }
    }
}
