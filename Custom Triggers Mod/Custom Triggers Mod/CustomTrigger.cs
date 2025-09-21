using System;

namespace Custom_Triggers_Mod
{
    public class CustomTrigger
    {
        public Type CustomTriggerActionType;
        public Type CustomTriggerActionInfoType;
        public string ActionName;

        public CustomTrigger( Type customTriggerActionType, Type customTriggerActionInfoType, string actionName )
        {
            CustomTriggerActionType = customTriggerActionType;
            CustomTriggerActionInfoType = customTriggerActionInfoType;
            ActionName = actionName;
        }
    }
}
