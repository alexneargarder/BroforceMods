using BroMakerLib.Loggers;
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
        public float diveCountdown = 1f;
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
            this.attachedUnit.playerNum = playerNum;
            this.attachedUnit.SetFriendlyExplosion();
            this.firedBy = FiredBy;
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
            }
        }

        public void StartDiving()
        {
            Vector3 target = Vector3.zero;

            // Find nearest enemy unit in the direction the skull is flying that has direct line of sight to the skull
            Unit nearestVisibleUnitDamagebleBy = Map.GetNearestVisibleUnitDamagebleBy( this.playerNum, 30, this.attachedUnit.X + this.attachedUnit.transform.localScale.x * 20, this.attachedUnit.Y, false );
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
                BMLogger.Log( "chosen angle: " + randomAngle );
                float distance = UnityEngine.Random.Range(150f, 300f);
                
                float targetX = this.attachedUnit.X + Mathf.Cos(randomAngle) * distance;
                float targetY = this.attachedUnit.Y + Mathf.Sin(randomAngle) * distance;
                
                target = new Vector3(targetX, targetY, 0f);

                BMLogger.Log( "targetting: " + targetX +  " " + targetY );
            }

            this.attachedUnit.StartDiving( target, -1, 0 );
            if ( this.attachedUnit.enemyAI.mentalState != MentalState.Alerted )
            {
                this.attachedUnit.enemyAI.FullyAlert( target.x, target.y, -1 );
            }
        }
    }
}
 