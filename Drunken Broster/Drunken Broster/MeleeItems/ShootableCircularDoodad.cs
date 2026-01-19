using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class ShootableCircularDoodad : Doodad
    {
        public float radius;
        public CircularProjectile owner;

        protected override void Awake()
        {
        }

        protected override void Start()
        {
            // Absorb bullets
            isImpenetrable = true;
            Map.RegisterDestroyableDoodad( this );
        }

        protected override void Update()
        {
            X = transform.position.x;
            Y = transform.position.y;
        }

        protected override void LateUpdate()
        {
        }

        public new bool IsPointInRange( float x, float y, float range )
        {
            return Vector2.Distance( new Vector2( x, y ), transform.position ) < ( range + radius );
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
