using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class ShootableCircularDoodad : Doodad
    {
        public float radius = 0f;
        public CircularProjectile owner;

        protected override void Awake()
        {
        }

        protected override void Start()
        {
            // Absorb bullets
            this.isImpenetrable = true;
            Map.RegisterDestroyableDoodad( this );
        }

        protected override void Update()
        {
            base.X = base.transform.position.x;
            base.Y = base.transform.position.y;
        }

        protected override void LateUpdate()
        {
        }

        public new bool IsPointInRange( float x, float y, float range )
        {
            return Vector2.Distance( new Vector2( x, y ), base.transform.position ) < ( range + radius );
        }

        public override bool Damage( DamageObject damageObject )
        {
            return owner.Damage( damageObject );
        }

        public override bool DamageOptional( DamageObject damageObject, ref bool showBulletHit )
        {
            showBulletHit = true;
            return Damage( damageObject );
        }
    }
}
