using System.IO;

namespace Custom_Triggers_Mod
{
    public abstract class CustomTriggerActionInfo : TriggerActionInfo
    {
        public override void Serialize( BinaryWriter bw )
        {
            Main.Log( "serializing custom action" );
            base.Serialize( bw );
        }
    }
}
