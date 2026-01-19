using System.Reflection;
using HarmonyLib;
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
        protected Unit targettedUnit;

        public static SkullProjectile CreatePrefab()
        {
            if ( cachedPrefab == null )
            {
                HellLostSoul attachedUnitPrefab = Instantiate( InstantiationController.GetPrefabFromResourceName( "networkobjects:ZHellLostSoul" ) ).GetComponent<HellLostSoul>();
                attachedUnitPrefab.gameObject.SetActive( false );
                cachedPrefab = attachedUnitPrefab.gameObject.AddComponent<SkullProjectile>();
                cachedPrefab.attachedUnit = attachedUnitPrefab;
                DontDestroyOnLoad( cachedPrefab );
            }

            return cachedPrefab;
        }

        public SkullProjectile SpawnProjectileLocally( MonoBehaviour FiredBy, float x, float y, float xI, float yI, int playerNum, float _zOffset = 0f )
        {
            SkullProjectile projectile = Instantiate<SkullProjectile>( this, new Vector3( x, y, 0f ), Quaternion.identity );
            projectile.gameObject.SetActive( true );
            projectile.Fire( x, y, xI, yI, _zOffset, playerNum, FiredBy );
            return projectile;
        }

        public virtual void Fire( float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            attachedUnitTraverse = Traverse.Create( attachedUnit );
            attachedUnit.X = newX;
            attachedUnit.Y = newY;
            attachedUnit.xI = xI;
            attachedUnit.yI = yI;
            attachedUnit.zOffset = _zOffset;
            attachedUnit.playerNum = 5;
            this.playerNum = playerNum;
            attachedUnit.SetFriendlyExplosion();
            firedBy = FiredBy;

            // Delay fire animation
            attachedUnitTraverse.Field( "fireAnimDelay" ).SetValue( 0.3f );

            // Fix exclamation mark bubble appearing on top of enemy
            for ( int i = 0; i < attachedUnit.transform.childCount; ++i )
            {
                if ( attachedUnit.transform.GetChild( i ).name == "ExclamationMark" )
                {
                    ReactionBubble reaction = attachedUnit.transform.GetChild( i ).GetComponent<ReactionBubble>();
                    reaction.SetPosition( reaction.transform.localPosition );
                    Traverse reactionTrav = Traverse.Create( reaction );
                    reactionTrav.Field( "yStart" ).SetValue( reaction.transform.localPosition.y );
                    break;
                }
            }
        }

        public void Update()
        {
            if ( diveCountdown > 0 )
            {
                diveCountdown -= Time.deltaTime;
                if ( diveCountdown <= 0 )
                {
                    StartDiving();
                }
                // Check for enemies along path
                else if ( Map.HitUnits( attachedUnit, attachedUnit, playerNum, 4, DamageType.Melee, 3f, attachedUnit.X, attachedUnit.Y, attachedUnit.xI, attachedUnit.yI, false, false ) )
                {
                    typeof( HellLostSoul ).GetMethod( "MakeEffects", BindingFlags.NonPublic | BindingFlags.Instance ).Invoke( attachedUnit, new object[] { } );
                    attachedUnit.CheckDestroyed();
                }
            }
        }

        public void StartDiving()
        {
            Vector3 target;

            // Find nearest enemy unit in the direction the skull is flying that has direct line of sight to the skull
            attachedUnit.playerNum = playerNum;
            Unit nearestVisibleUnitDamagebleBy = Map.GetNearestVisibleUnitDamagebleBy( playerNum, 40, attachedUnit.X + attachedUnit.transform.localScale.x * 20, attachedUnit.Y, false );
            attachedUnit.playerNum = 5;
            // Check that we found a unit, it hasn't already been hit, and it is in the direction the shield is traveling.
            if ( nearestVisibleUnitDamagebleBy != null && nearestVisibleUnitDamagebleBy.gameObject.activeInHierarchy && ( Mathf.Sign( nearestVisibleUnitDamagebleBy.X - attachedUnit.X ) == Mathf.Sign( attachedUnit.transform.localScale.x ) ) )
            {
                targettedUnit = nearestVisibleUnitDamagebleBy;
                target = nearestVisibleUnitDamagebleBy.transform.position;
            }
            // If no unit is found, fly in a random direction towards the ground
            else
            {
                float randomAngle = Random.Range( -65f, -15f ) * Mathf.Deg2Rad;
                float distance = 500;

                // Adjust angle based on facing direction
                float targetX = attachedUnit.X + Mathf.Cos( randomAngle ) * distance * attachedUnit.transform.localScale.x;
                float targetY = attachedUnit.Y + Mathf.Sin( randomAngle ) * distance;

                target = new Vector3( targetX, targetY, 0f );
            }

            if ( attachedUnit.enemyAI.mentalState != MentalState.Alerted )
            {
                attachedUnit.enemyAI.FullyAlert( target.x, target.y, -1 );
            }
        }
    }
}
