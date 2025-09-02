using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Utility_Mod
{
    public class TestDummy : Mook
    {
        private List<DamageEvent> damageHistory = new List<DamageEvent>();
        private readonly float dpsWindowSeconds = 5f;

        public bool showDPSOverlay = true;

        public float currentDPS { get; private set; }
        public int totalDamage { get; private set; }
        public int hitCount { get; private set; }

        public void Setup()
        {
            this.showDPSOverlay = Main.settings.showTestDummyDPS ?? true;

            Mook originalMook = this.GetComponent<Mook>();

            if ( originalMook != null && originalMook != this )
            {
                this.health = 1000;
                this.maxHealth = 1000;

                UnityEngine.Object.Destroy( originalMook );
            }
            else
            {
                this.health = 1000;
                this.maxHealth = 1000;
            }

            if ( this.enemyAI != null )
            {
                UnityEngine.Object.Destroy( this.enemyAI );
                this.enemyAI = null;
            }

            DisableWhenOffCamera disabler = this.GetComponent<DisableWhenOffCamera>();
            if ( disabler != null )
            {
                UnityEngine.Object.Destroy( disabler );
            }

            Rigidbody rb = this.GetComponent<Rigidbody>();
            if ( rb != null )
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            this.name = "TestDummy";

            // Set proper layer for collision detection
            this.gameObject.layer = LayerMask.NameToLayer( "Enemies" );

            this.playerNum = -1;

            this.invulnerable = false;

            this.width = 8f;  // Standard mook width
            this.height = 11f; // Standard mook height

            damageHistory = new List<DamageEvent>();

            TestDummyDisplay display = this.gameObject.AddComponent<TestDummyDisplay>();
        }

        public override void Damage( int damage, DamageType damageType, float xI, float yI,
                                   int direction, MonoBehaviour damageSender, float hitX, float hitY )
        {
            damageHistory.Add( new DamageEvent
            {
                timestamp = Time.time,
                damage = damage,
                damageType = damageType,
                position = new Vector3( hitX, hitY, 0 ),
                damageSender = damageSender
            } );

            totalDamage += damage;
            hitCount++;

            CleanOldDamageHistory();

            CalculateDPS();

            int originalHealth = this.health;
            base.Damage( damage, damageType, 0f, 0f, direction, damageSender, hitX, hitY );

            if ( this.health <= 0 )
            {
                this.health = this.maxHealth;
            }

            Rigidbody rb = this.GetComponent<Rigidbody>();
            if ( rb != null )
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            this.xI = 0f;
            this.yI = 0f;
        }

        protected override void PlayHitSound( float v = 0.4f )
        {
            // Override to prevent null reference exception
            // TestDummy doesn't need hit sounds
        }

        protected override void PlayDeathSound()
        {
            // Override to prevent null reference exception
            // TestDummy doesn't die
        }

        public override void Death( float xI, float yI, DamageObject damage )
        {
            this.health = this.maxHealth;
        }

        protected override void Land()
        {
            this.yI = 0f;
            this.xI = 0f;
        }

        protected override void Update()
        {
            this.xI = 0f;
            this.yI = 0f;
        }

        protected override void RunMovement()
        {
            this.xI = 0f;
            this.yI = 0f;
        }

        protected override void GetEnemyMovement()
        {
            this.xI = 0f;
            this.yI = 0f;
        }

        protected override void ApplyFallingGravity()
        {
            this.yI = 0f;
        }

        public override void Knock( DamageType damageType, float xI, float yI, bool forceTumble )
        {
            this.xI = 0f;
            this.yI = 0f;
        }

        public override void Launch( float xI, float yI )
        {
            this.xI = 0f;
            this.yI = 0f;
        }

        public override void SetVelocity( DamageType damageType, float xI, float xIBlast, float yIBlast )
        {
            this.xI = 0f;
            this.yI = 0f;
        }

        protected override void Jump( bool wallJump )
        {
            this.yI = 0f;
        }

        protected override void FixedUpdate()
        {
        }

        private void CalculateDPS()
        {
            if ( damageHistory.Count == 0 )
            {
                currentDPS = 0f;
                return;
            }

            float cutoffTime = Time.time - dpsWindowSeconds;
            var recentDamage = damageHistory.Where( d => d.timestamp >= cutoffTime ).ToList();

            if ( recentDamage.Count == 0 )
            {
                currentDPS = 0f;
                return;
            }

            float totalRecentDamage = recentDamage.Sum( d => d.damage );
            float timeSpan = Time.time - recentDamage.First().timestamp;

            currentDPS = timeSpan > 0 ? totalRecentDamage / timeSpan : 0f;
        }

        private void CleanOldDamageHistory()
        {
            float cutoffTime = Time.time - dpsWindowSeconds;
            damageHistory.RemoveAll( d => d.timestamp < cutoffTime );
        }

        public void ResetStats()
        {
            damageHistory.Clear();
            currentDPS = 0f;
            totalDamage = 0;
            hitCount = 0;
            this.health = this.maxHealth;
        }

        public override bool CanBeThrown()
        {
            return false;
        }
    }
}