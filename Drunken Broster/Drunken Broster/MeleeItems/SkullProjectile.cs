using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class SkullProjectile : MonoBehaviour
    {
        public static SkullProjectile cachedPrefab;
        public HellLostSoul attachedUnit;
        public Traverse attachedUnitTraverse;
        public int playerNum;
        public MonoBehaviour firedBy;
        public float diveCountdown = 0.4f;
        protected Unit targettedUnit = null;

        public static SkullProjectile CreatePrefab()
        {
            if ( cachedPrefab == null)
            {
                HellLostSoul attachedUnitPrefab = UnityEngine.Object.Instantiate( InstantiationController.GetPrefabFromResourceName( "networkobjects:ZHellLostSoul" ) ).GetComponent<HellLostSoul>();
                attachedUnitPrefab.gameObject.SetActive( false );
                cachedPrefab = attachedUnitPrefab.gameObject.AddComponent<SkullProjectile>();
                cachedPrefab.attachedUnit = attachedUnitPrefab;
                UnityEngine.Object.DontDestroyOnLoad( cachedPrefab );
            }

            return cachedPrefab;
        }

        public SkullProjectile SpawnProjectileLocally( MonoBehaviour FiredBy, float x, float y, float xI, float yI, int playerNum, float _zOffset = 0f )
        {
            SkullProjectile projectile = UnityEngine.Object.Instantiate<SkullProjectile>( this, new Vector3( x, y, 0f ), Quaternion.identity );
            projectile.gameObject.SetActive( true );
            projectile.Fire( x, y, xI, yI, _zOffset, playerNum, FiredBy );
            return projectile;
        }

        public virtual void Fire( float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            this.attachedUnitTraverse = Traverse.Create( attachedUnit );
            this.attachedUnit.X = newX;
            this.attachedUnit.Y = newY;
            this.attachedUnit.xI = xI;
            this.attachedUnit.yI = yI;
            this.attachedUnit.zOffset = _zOffset;
            this.attachedUnit.playerNum = 5;
            this.playerNum = playerNum;
            this.attachedUnit.SetFriendlyExplosion();
            this.firedBy = FiredBy;
            
            // Delay fire animation
            this.attachedUnitTraverse.Field( "fireAnimDelay" ).SetValue( 0.3f );

            // Fix exclamation mark bubble appearing on top of enemy
            for ( int i = 0; i < this.attachedUnit.transform.childCount; ++i )
            {
                if ( this.attachedUnit.transform.GetChild(i).name == "ExclamationMark" )
                {
                    ReactionBubble reaction = this.attachedUnit.transform.GetChild( i ).GetComponent<ReactionBubble>();
                    reaction.SetPosition( reaction.transform.localPosition );
                    Traverse reactionTrav = Traverse.Create( reaction );
                    reactionTrav.Field( "yStart" ).SetValue( reaction.transform.localPosition.y );
                    break;
                }
            }
        }

        public void Update()
        {
            if( diveCountdown > 0 )
            {
                diveCountdown -= Time.deltaTime;
                if ( diveCountdown <= 0 )
                {
                    this.StartDiving();
                }
                // Check for enemies along path
                else if ( Map.HitUnits( this.attachedUnit, this.attachedUnit, this.playerNum, 4, DamageType.Melee, 3f, this.attachedUnit.X, this.attachedUnit.Y, this.attachedUnit.xI, this.attachedUnit.yI, false, false ) )
                {
                    typeof( HellLostSoul ).GetMethod( "MakeEffects", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( attachedUnit, new object[] { } );
                    this.attachedUnit.CheckDestroyed();
                }
            }
        }

        public void StartDiving()
        {
            Vector3 target = Vector3.zero;

            // Find nearest enemy unit in the direction the skull is flying that has direct line of sight to the skull
            this.attachedUnit.playerNum = this.playerNum;
            Unit nearestVisibleUnitDamagebleBy = Map.GetNearestVisibleUnitDamagebleBy( this.playerNum, 40, this.attachedUnit.X + this.attachedUnit.transform.localScale.x * 20, this.attachedUnit.Y, false );
            this.attachedUnit.playerNum = 5;
            // Check that we found a unit, it hasn't already been hit, and it is in the direction the shield is traveling.
            if ( nearestVisibleUnitDamagebleBy != null && nearestVisibleUnitDamagebleBy.gameObject.activeInHierarchy && ( Mathf.Sign( nearestVisibleUnitDamagebleBy.X - this.attachedUnit.X ) == Mathf.Sign( this.attachedUnit.transform.localScale.x ) ) )
            {
                this.targettedUnit = nearestVisibleUnitDamagebleBy;
                target = nearestVisibleUnitDamagebleBy.transform.position;
            }
            // If no unit is found, fly in a random direction towards the ground
            else
            {
                float randomAngle = UnityEngine.Random.Range(-65f, -15f) * Mathf.Deg2Rad;
                float distance = 500;
                
                // Adjust angle based on facing direction
                float targetX = this.attachedUnit.X + Mathf.Cos(randomAngle) * distance * this.attachedUnit.transform.localScale.x;
                float targetY = this.attachedUnit.Y + Mathf.Sin(randomAngle) * distance;
                
                target = new Vector3(targetX, targetY, 0f);
            }

            if ( this.attachedUnit.enemyAI.mentalState != MentalState.Alerted )
            {
                this.attachedUnit.enemyAI.FullyAlert( target.x, target.y, -1 );
            }
        }
    }
}
 