using System.Collections.Generic;
using UnityEngine;

namespace Utility_Mod
{
    public class TestDummy : Mook
    {
        public TestDummyMode mode = TestDummyMode.Locked;

        public Vector3 spawnPosition;
        public bool showDPSOverlay = true;

        private readonly List<DamageEvent> damageHistory = new List<DamageEvent>();
        private const float dpsWindowSeconds = 5f;

        public float currentDPS { get; private set; }
        public float averageDPS { get; private set; }
        public int totalDamage { get; private set; }
        public int hitCount { get; private set; }
        public int lastHitDamage { get; private set; }
        public int minHitDamage { get; private set; }
        public int maxHitDamage { get; private set; }
        public float firstHitTime { get; private set; }
        public float lastHitTime { get; private set; }
        public float engagementTime { get; private set; }

        private bool needsReset;
        private int baseDamageDepth;
        private bool statsDirty;
        private float lastActivityTime;
        private bool wasPersistent;
        private bool prefabCanTumble;
        private bool prefabCanBackSomersault;

        public void Setup( Vector3 position )
        {
            this.prefabCanTumble = this.canTumble;
            this.prefabCanBackSomersault = this.canBackSomersault;

            this.showDPSOverlay = Main.settings.showTestDummyDPS ?? true;
            this.mode = Main.settings.testDummyDefaultMode;

            this.health = 1000;
            this.maxHealth = 1000;
            this.playerNum = -1;
            this.invulnerable = false;
            this.gameObject.layer = LayerMask.NameToLayer( "Units" );
            this.name = "TestDummy";

            if ( this.enemyAI != null )
            {
                this.enemyAI.enabled = false;
            }

            // DWOC runs in Mook.Awake and may have already disabled us if spawned off-camera.
            this.enabled = true;
            DisableWhenOffCamera dwoc = this.GetComponent<DisableWhenOffCamera>();
            if ( dwoc != null )
            {
                // Destroy is deferred to end of frame; a surviving Update would re-enable the AI.
                dwoc.enabled = false;
                UnityEngine.Object.Destroy( dwoc );
            }

            this.exitingDoor = false;
            this.originDoor = null;

            this.canBeAssasinated = false;
            this.canBeRevived = false;
            this.canBeCoveredInAcid = false;
            ApplyTumbleFlags();
            // Mook.Damage's Fire branch sets burnTime only when PanicAI returns true, which ours never does.
            this.willPanicWhenOnFire = false;
            // Prefab ships this at 2f, already past the quicksand drown threshold.
            this.quicksandChokeCounter = 0f;

            spawnPosition = position;
            lastActivityTime = Time.time;
            ResetStats();

            if ( this.gameObject.GetComponent<TestDummyDisplay>() == null )
            {
                this.gameObject.AddComponent<TestDummyDisplay>();
            }
        }

        private void ApplyTumbleFlags()
        {
            bool free = this.mode == TestDummyMode.Free;
            this.canTumble = free && this.prefabCanTumble;
            this.canBackSomersault = free && this.prefabCanBackSomersault;
        }

        public override void RegisterUnit()
        {
            // The base override also registers with statistics, inflating the level mook total.
            Map.RegisterUnit( this, false );
        }

        public override void Damage( int damage, DamageType damageType, float xI, float yI,
                                   int direction, MonoBehaviour damageSender, float hitX, float hitY )
        {
            // GibArea deals OutOfBounds every frame with no sender, while mines send it with themselves as the sender.
            if ( mode != TestDummyMode.Free && damageType == DamageType.OutOfBounds
                 && ( damageSender == null || (object)damageSender == this ) )
            {
                RestoreState();
                needsReset = false;
                return;
            }

            // Self-sent hits are the game re-sending damage it already dealt (landings, wall slams, deferred lumps), except the burn lump.
            bool selfSent = (object)damageSender == this;
            if ( selfSent && mode != TestDummyMode.Free
                 && ( damageType == DamageType.Fall || damageType == DamageType.Knock ) )
            {
                return;
            }
            bool record = damage > 0 && ( !selfSent || damageType == DamageType.Fizzle );
            // Acid never reaches a mook's health.
            if ( damageType == DamageType.Acid ) record = false;
            // In Free the game defers a Fire hit into the burn lump under this condition, so the lump is the recorded hit.
            if ( mode == TestDummyMode.Free && damageType == DamageType.Fire && !selfSent
                 && this.canBeSetOnFire && this.health > 0 && this.burnDamage <= this.health * 5 ) record = false;

            if ( mode == TestDummyMode.Free )
            {
                base.Damage( damage, damageType, xI, yI, direction, damageSender, hitX, hitY );
            }
            else
            {
                // Mook.Damage skins a Knifed unit that fails IsAlive() after the hit.
                int realHealth = this.health;
                this.health = damageType == DamageType.Knifed ? damage + 1 : 0;
                try
                {
                    baseDamageDepth++;
                    base.Damage( damage, damageType, xI, yI, direction, damageSender, hitX, hitY );
                }
                finally
                {
                    baseDamageDepth--;
                    this.health = record ? Mathf.Max( 1, realHealth - damage ) : realHealth;
                }

                // Mook.Damage's Fire branch is gated on health > 0, which the pinned HP skips.
                if ( damageType == DamageType.Fire && this.canBeSetOnFire )
                {
                    this.burnTime = Mathf.Clamp( this.burnTime + 0.1f, 1f, 2f );
                }
            }

            // Vanilla never clears burnDamage after StopBurning, so the next lump would re-send it.
            if ( selfSent && damageType == DamageType.Fizzle ) this.burnDamage = 0;

            if ( !record ) return;

            float now = Time.time;
            if ( hitCount == 0 )
            {
                firstHitTime = now;
                minHitDamage = damage;
                maxHitDamage = damage;
            }
            else
            {
                if ( damage < minHitDamage ) minHitDamage = damage;
                if ( damage > maxHitDamage ) maxHitDamage = damage;
            }

            damageHistory.Add( new DamageEvent
            {
                timestamp = now,
                damage = damage,
                damageType = damageType,
                position = new Vector3( hitX, hitY, 0 ),
                damageSender = damageSender
            } );

            totalDamage += damage;
            hitCount++;
            lastHitDamage = damage;
            lastHitTime = now;
            statsDirty = true;

            // Map.HitUnits passes knock=false for bullets, so nothing else moves the dummy.
            if ( mode == TestDummyMode.AutoReset && this.health > 0 && (object)damageSender != this )
            {
                if ( Mathf.Abs( this.xI ) < 50f && Mathf.Abs( xI ) > 1f )
                {
                    this.xI = Mathf.Clamp( this.xI + xI * 0.5f, -200f, 200f );
                }
                if ( Mathf.Abs( this.yI ) < 50f && Mathf.Abs( yI ) > 1f )
                {
                    this.yI = Mathf.Clamp( this.yI + yI * 0.5f, -20000f, 400f );
                }
            }

            needsReset = true;
            lastActivityTime = now;
        }

        public override void Death( float xI, float yI, DamageObject damage )
        {
            // Mook.Damage keeps running after Death returns.
            if ( mode == TestDummyMode.Free )
            {
                base.Death( xI, yI, damage );
            }
        }

        protected override void Gib( DamageType damageType, float xI, float yI )
        {
            if ( mode == TestDummyMode.Free )
            {
                base.Gib( damageType, xI, yI );
                return;
            }

            // Kill planes Gib instead of dealing damage.
            if ( damageType == DamageType.OutOfBounds && baseDamageDepth == 0 )
            {
                RestoreState();
                needsReset = false;
            }
        }

        public override void GibNow( DamageType damageType, float xI, float yI )
        {
            if ( mode == TestDummyMode.Free )
            {
                base.GibNow( damageType, xI, yI );
            }
        }

        public override void Reenable()
        {
            base.Reenable();
            // TestVanDammeAnim's throw path calls Reenable, and Mook.Reenable turns the AI back on.
            if ( this.enemyAI != null )
            {
                this.enemyAI.enabled = false;
            }
        }

        public override bool CanBeThrown()
        {
            return mode != TestDummyMode.Locked && base.CanBeThrown();
        }

        public override void Knock( DamageType damageType, float xI, float yI, bool forceTumble )
        {
            if ( mode == TestDummyMode.Locked ) return;
            base.Knock( damageType, xI, yI, forceTumble );
        }

        protected override void Update()
        {
            base.Update();
            UpdateStats();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            if ( HasTransientActivity() )
            {
                lastActivityTime = Time.time;
                needsReset = true;
            }

            bool persistent = HasPersistentState();
            if ( persistent )
            {
                if ( !wasPersistent )
                {
                    lastActivityTime = Time.time;
                }
                needsReset = true;
            }
            wasPersistent = persistent;

            switch ( mode )
            {
                case TestDummyMode.Locked:
                    RestoreState();
                    needsReset = false;
                    break;

                case TestDummyMode.AutoReset:
                    if ( needsReset && Time.time - lastActivityTime >= Main.settings.testDummyAutoResetDelay )
                    {
                        RestoreState();
                        needsReset = false;
                    }
                    break;

                case TestDummyMode.Free:
                    break;
            }
        }

        private bool HasTransientActivity()
        {
            if ( IsHeldByHero() ) return true;
            if ( tumbling || backSomersaulting || frontSomersaulting || backTumbling ) return true;
            // Strung-up and impaled units are parked in actionState Jumping with nothing to clear it.
            if ( actionState != ActionState.Idle && !HasPersistentState() ) return true;
            if ( Mathf.Abs( xI ) > 0.5f || Mathf.Abs( yI ) > 0.5f ) return true;
            if ( burnTime > 0f || stunTime > 0f || blindTime > 0f || scaredTime > 0f ) return true;
            return false;
        }

        private bool IsHeldByHero()
        {
            // TestVanDammeAnim.ThrowBackMook carries a mook by parenting it to the hero.
            return transform.parent != null && transform.parent.GetComponent<TestVanDammeAnim>() != null;
        }

        private bool HasPersistentState()
        {
            if ( transform.parent != null && !IsHeldByHero() ) return true;
            if ( IsFrozen ) return true;
            if ( impaledByTransform != null || strungUpBy != null ) return true;
            // Vanilla never clears isBeingThrown.
            if ( assasinated || disemboweled || decapitated || isBeingThrown ) return true;
            // Unit.Death parks actionState at Dead and nothing clears it.
            if ( actionState == ActionState.Dead || health <= 0 ) return true;
            return false;
        }

        public void RestoreState()
        {
            if ( transform.parent != null )
            {
                transform.SetParent( null, true );
            }
            this.lastParentedToTransform = null;
            this.onBackOf = null;

            // Map.HitUnits reads unit.X/Y, not transform.
            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;
            this.SetXY( spawnPosition.x, spawnPosition.y );

            this.actionState = ActionState.Idle;
            this.xI = 0f;
            this.yI = 0f;
            this.xIBlast = 0f;
            this.yIBlast = 0f;

            this.tumbling = false;
            this.backSomersaulting = false;
            this.frontSomersaulting = false;
            this.backTumbling = false;
            this.hasTumbled = false;
            this.fallenTime = 0f;
            this.backSomersaultFrames = 0;

            this.holdUpTime = 0f;
            this.holdLeftTime = 0f;
            this.holdRightTime = 0f;
            this.holdDownTime = 0f;
            this.holdStillTime = 0f;

            this.UnFreeze();

            this.stunTime = 0f;
            this.stunVocalDelay = 0f;
            this.blindTime = 0f;
            this.scaredTime = 0f;
            this.tearGasChoking = false;
            this.dancingTime = 0f;

            // Clearing burnTime alone makes StopBurning fire with the accumulated burnDamage.
            this.burnTime = 0f;
            this.burnDamage = 0;
            this.burnCounter = 0f;
            this.fireSpreadCounter = 0f;

            this.quicksandChokeCounter = 0f;

            // Plasma / shock tick in Mook.Update and self-damage if not zeroed.
            this.plasmaCounter = 0f;
            this.plasmaDamage = 0;
            this.shockCounter = 0f;
            this.shockDamage = 0;

            this.isBeingThrown = false;
            this.catchFriendlyBullets = false;
            this.temporarilyCatchFriendBullets = false;
            this.disableCatchFriendBulletsCounter = 0f;

            if ( this.impalementCollider != null )
            {
                UnityEngine.Object.Destroy( this.impalementCollider );
                this.impalementCollider = null;
            }
            this.impaledByTransform = null;
            this.impaledBy = null;
            this.impaledDirection = 0;
            this.useImpaledFrames = false;
            this.impaledCounter = 0f;
            this.bloodTime = 0f;
            this.bloodTimeOut = 0f;

            this.strungUpBy = null;
            this.strungUpX = 0f;

            this.assasinated = false;
            this.assasinatedFrame = 0;
            this.disemboweled = false;
            this.disemboweledFrame = 0;
            this.disembowelHoldFrames = 0;
            this.decapitated = false;
            this.decapitationCounter = 0f;
            this.hasDied = false;
            this.deathNotificationSent = false;
            this.hasReportedKill = false;
            this.hasNotifiedDeathType = false;
            this.deathType = DeathType.Unassigned;
            this.deathTime = 0f;

            this.animatingRevealPosition = false;

            this.somersaultScreamDelay = 0f;
            if ( this.fallingSoundSource != null )
            {
                ReusableOneShotSound pooledSound = this.fallingSoundSource.GetComponent<ReusableOneShotSound>();
                if ( pooledSound != null )
                {
                    pooledSound.Repool();
                }
                else
                {
                    UnityEngine.Object.Destroy( this.fallingSoundSource.gameObject );
                }
                this.fallingSoundSource = null;
            }

            Renderer r = this.GetComponent<Renderer>();
            if ( r != null && this.defaultMaterial != null )
            {
                r.material = this.defaultMaterial;
            }

            // Mook.Death leaves invulnerable set and TestVanDammeAnim.Death disables the collider and gun sprite, and none of it self-clears.
            this.invulnerable = false;
            this.invulnerableTime = 0f;
            Collider deathCollider = this.GetComponent<Collider>();
            if ( deathCollider != null )
            {
                deathCollider.enabled = true;
            }
            this.ActivateGun();

            this.health = this.maxHealth;
            Rigidbody rb = this.GetComponent<Rigidbody>();
            if ( rb != null )
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        protected override void GetEnemyMovement()
        {
            // Mook calls this directly, so disabling enemyAI does not stop it.
        }

        protected override void RunFiring()
        {
        }

        protected override void UseFire()
        {
        }

        protected override void UseSpecial()
        {
        }

        protected override void PlayHitSound( float v = 0.4f )
        {
            if ( mode != TestDummyMode.Locked )
            {
                base.PlayHitSound( v );
            }
        }

        protected override void PlayDeathSound()
        {
            if ( mode != TestDummyMode.Locked )
            {
                base.PlayDeathSound();
            }
        }

        protected override void BurnOthers()
        {
        }

        public override void BurnInternal( int damage, int direction )
        {
            // Mook.BurnInternal starts the burn only when PanicAI succeeds, which ours never does.
            this.burnDamage += damage;
            if ( this.health > 0 && this.burnTime <= 0f )
            {
                this.burnTime = Mathf.Clamp( this.burnTime + 0.4f, 1f, 2f );
            }
        }

        protected override bool PanicAI( bool forgetPlayer )
        {
            return false;
        }

        protected override bool PanicAI( int direction, bool forgetPlayer )
        {
            return false;
        }

        public override void Panic( bool forgetPlayer )
        {
        }

        public override void Panic( float time, bool forgetPlayer )
        {
        }

        public override void Panic( int direction, float time, bool forgetPlayer )
        {
        }

        public override void Attract( float xTarget, float yTarget )
        {
        }

        public override void Alert( float alertX, float alertY )
        {
        }

        public override void FullyAlert( float x, float y, int playerNum )
        {
        }

        public override void HearSound( float alertX, float alertY )
        {
        }

        protected override void CheckForQuicksand()
        {
        }

        protected override void AnimateFallen()
        {
            if ( mode != TestDummyMode.Locked )
            {
                base.AnimateFallen();
            }
        }

        protected override void Land()
        {
            if ( mode == TestDummyMode.Locked )
            {
                this.tumbling = false;
                this.backSomersaulting = false;
                this.frontSomersaulting = false;
                this.backTumbling = false;
                this.yI = 0f;
                this.xI = 0f;
            }
            else
            {
                base.Land();
            }
        }

        public override void CheckDestroyed()
        {
            if ( mode == TestDummyMode.Free )
            {
                base.CheckDestroyed();
            }
        }

        private void UpdateStats()
        {
            float now = Time.time;
            float cutoff = now - dpsWindowSeconds;

            damageHistory.RemoveAll( d => d.timestamp < cutoff );

            int recent = 0;
            for ( int i = 0; i < damageHistory.Count; i++ )
            {
                recent += damageHistory[i].damage;
            }
            currentDPS = recent / dpsWindowSeconds;

            if ( hitCount == 0 )
            {
                engagementTime = 0f;
                averageDPS = 0f;
            }
            else
            {
                engagementTime = Mathf.Max( 0f, lastHitTime - firstHitTime );
                averageDPS = engagementTime > 0f ? totalDamage / engagementTime : 0f;
            }

            if ( statsDirty && hitCount > 0 && now - lastHitTime > Main.settings.testDummyStatsAutoClearDelay )
            {
                ResetStats();
            }
        }

        public void ResetStats()
        {
            damageHistory.Clear();
            currentDPS = 0f;
            averageDPS = 0f;
            totalDamage = 0;
            hitCount = 0;
            lastHitDamage = 0;
            minHitDamage = 0;
            maxHitDamage = 0;
            firstHitTime = 0f;
            lastHitTime = 0f;
            engagementTime = 0f;
            statsDirty = false;
        }

        public void SetMode( TestDummyMode newMode )
        {
            mode = newMode;
            ApplyTumbleFlags();
            // TestVanDammeAnim.Damage stamps deathType whenever HP reads 0, which Locked/AutoReset hits always do.
            this.deathType = DeathType.Unassigned;
        }

        public void CycleMode()
        {
            switch ( mode )
            {
                case TestDummyMode.Locked: SetMode( TestDummyMode.AutoReset ); break;
                case TestDummyMode.AutoReset: SetMode( TestDummyMode.Free ); break;
                default: SetMode( TestDummyMode.Locked ); break;
            }
        }

        public string GetModeShortLabel()
        {
            switch ( mode )
            {
                case TestDummyMode.Locked: return "Lock";
                case TestDummyMode.Free: return "Free";
                default: return "Auto";
            }
        }
    }
}
