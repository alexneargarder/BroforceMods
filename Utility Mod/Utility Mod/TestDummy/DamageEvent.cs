using UnityEngine;

namespace Utility_Mod
{
    public class DamageEvent
    {
        public float timestamp;
        public int damage;
        public DamageType damageType;
        public Vector3 position;
        public MonoBehaviour damageSender;
    }
}