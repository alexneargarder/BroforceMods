using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.CustomObjects.Projectiles;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Brostbuster
{
    [HeroPreset( "Brostbuster", HeroType.Rambro )]
    public class Brostbuster : CustomHero
    {
        // Proton beam
        LineRenderer protonLine1;
        LineRenderer protonLine1Cap;
        LineRenderer protonLine2;
        Material[] protonLine2Mats;
        protected Vector3 protonLineHitpoint;
        protected const float protonLineRange = 500;
        protected const float offsetSpeed = 4f;
        protected const float swaySpeedLerpM = 1f;
        protected const int protonUnitDamage = 1;
        protected const int protonWallDamage = 1;
        protected float currentOffset = 0f;
        protected float currentOffset2 = 0f;
        protected float sparkCooldown = 0;
        protected float flameCooldown = 0;
        protected float muzzleFlashCooldown = 0f;
        protected int protonLine2Frame = 0;
        protected float protonLine2FrameCounter = 0f;
        protected float protonDamageCooldown = 0f;
        protected float fireKnockbackCooldown = 0f;
        protected float effectCooldown = 0f;
        public static System.Random rnd = new System.Random();
        protected LayerMask fragileGroundLayer;
        public AudioClip protonStartup;
        public AudioClip protonBeamStartup;
        public AudioClip protonLoop;
        public AudioClip protonEnd;
        public AudioSource protonAudio;
        protected bool playedBeamStartup = false;
        protected float startupTime = 0f;
        protected float shutdownTime = 0f;
        public static HashSet<Brostbuster> currentBros = new HashSet<Brostbuster>();
        protected float currentGunPosition = 0f;

        // Ghost Trap
        GhostTrap trapPrefab, currentTrap;

        // Melee
        protected int SlimerTraps = 0;
        protected bool usingSlimerMelee = false;
        protected bool playedSlimerAudio = false;
        protected bool alreadySpawnedSlimer = false;
        Slimer slimerPrefab, currentSlimer;
        public static Color SlimerColor = new Color( 0.058824f, 1f, 0f );
        protected bool throwingMook = false;
        public AudioClip slimerTrapOpen;
        public AudioSource slimerPortalSource;

        // Misc
        protected bool acceptedDeath = false;

        #region General
        public override void PreloadAssets()
        {
            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            CustomHero.PreloadSprites( directoryPath, new List<string> { "protonLine1.png", "protonLine1End.png", "protonLine21.png", "protonLine22.png", "protonLine23.png", "protonLine24.png" } );
            CustomHero.PreloadSprites( Path.Combine( directoryPath, "projectiles" ), new List<string> { "ghostTrap.png", "slimer.png" } );
            CustomHero.PreloadSounds( Path.Combine( directoryPath, "sounds" ), new List<string> { "protonStartup.wav", "protonStartup2.wav", "protonLoop.wav", "protonEnd.wav", "slimerTrapOpen.wav", "freeze1.wav", "freeze2.wav", "freeze3.wav", "freeze4.wav", "freeze5.wav", "freeze6.wav", "freeze7.wav", "freeze8.wav", "slimer1.wav", "slimer2.wav", "slimer3.wav", "slimer4.wav", "trapOpen.wav", "trapMain.wav", "trapClosing.wav", "trapClosed.wav" } );
        }

        public override void HarmonyPatches( Harmony harmony )
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony.PatchAll( assembly );
        }

        public override int GetVariant()
        {
            // Don't duplicate brostbuster variants
            if ( Brostbuster.currentBros.Count > 0 )
            {
                List<int> available = new List<int> { 0, 1, 2, 3 };
                foreach ( Brostbuster bro in currentBros )
                {
                    available.Remove( bro.CurrentVariant );
                }

                Brostbuster.currentBros.Add( this );
                return available[UnityEngine.Random.Range( 0, available.Count )];
            }
            else
            {
                Brostbuster.currentBros.Add( this );
                return base.GetVariant();
            }
        }

        public override void BeforePrefabSetup()
        {
            this.SoundHolderVoiceType = SoundHolderVoiceTypes.MaleLight;
        }

        public override void AfterPrefabSetup()
        {
            string soundPath = Path.Combine( directoryPath, "sounds" );

            protonStartup = ResourcesController.GetAudioClip( soundPath, "protonStartup.wav" );
            protonBeamStartup = ResourcesController.GetAudioClip( soundPath, "protonStartup2.wav" );
            protonLoop = ResourcesController.GetAudioClip( soundPath, "protonLoop.wav" );
            protonEnd = ResourcesController.GetAudioClip( soundPath, "protonEnd.wav" );
            slimerTrapOpen = ResourcesController.GetAudioClip( soundPath, "slimerTrapOpen.wav" );

            this.protonAudio = base.gameObject.AddComponent<AudioSource>();
            this.protonAudio.rolloffMode = AudioRolloffMode.Linear;
            this.protonAudio.minDistance = 200f;
            this.protonAudio.dopplerLevel = 0.1f;
            this.protonAudio.maxDistance = 500f;
            this.protonAudio.spatialBlend = 1f;
            this.protonAudio.volume = 0.33f;
        }

        protected override void Awake()
        {
            base.Awake();

            protonLine1 = new GameObject( "ProtonLine1", new Type[] { typeof( LineRenderer ) } ).GetComponent<LineRenderer>();
            protonLine1.transform.parent = this.transform;
            protonLine1.material = ResourcesController.GetMaterial( directoryPath, "protonLine1.png" );
            protonLine1.material.mainTexture.wrapMode = TextureWrapMode.Repeat;

            protonLine1Cap = new GameObject( "ProtonLine1End", new Type[] { typeof( LineRenderer ) } ).GetComponent<LineRenderer>();
            protonLine1Cap.transform.parent = this.transform;
            protonLine1Cap.material = ResourcesController.GetMaterial( directoryPath, "protonLine1End.png" );
            protonLine1Cap.material.mainTexture.wrapMode = TextureWrapMode.Repeat;

            protonLine2 = new GameObject( "ProtonLine2", new Type[] { typeof( LineRenderer ) } ).GetComponent<LineRenderer>();
            protonLine2.transform.parent = this.transform;
            protonLine2Mats = new Material[4];
            for ( int i = 0; i < 4; ++i )
            {
                protonLine2Mats[i] = ResourcesController.GetMaterial( directoryPath, "protonLine2" + ( i + 1 ) + ".png" );
                protonLine2Mats[i].mainTexture.wrapMode = TextureWrapMode.Repeat;
            }
            protonLine2.material = protonLine2Mats[0];

            this.fragileGroundLayer = this.fragileLayer | this.groundLayer;

            this.trapPrefab = CustomGrenade.CreatePrefab<GhostTrap>();

            this.slimerPrefab = CustomProjectile.CreatePrefab<Slimer>();

            this.currentMeleeType = BroBase.MeleeType.Disembowel;
            this.meleeType = BroBase.MeleeType.Disembowel;
        }

        protected override void Start()
        {
            base.Start();
            Brostbuster.currentBros.Add( this );
        }

        protected override void Update()
        {
            base.Update();
            if ( this.acceptedDeath )
            {
                if ( this.health <= 0 && !this.WillReviveAlready )
                {
                    return;
                }
                // Revived
                else
                {
                    Brostbuster.currentBros.Add( this );
                    this.acceptedDeath = false;
                }
            }

            // Stop proton gun when getting on helicopter
            if ( this.isOnHelicopter )
            {
                this.StopProtonGun();
                this.protonAudio.enabled = false;
            }

            // Handle death
            if ( base.actionState == ActionState.Dead && !this.acceptedDeath )
            {
                this.StopProtonGun();
                this.protonAudio.enabled = false;
                
                if ( !this.WillReviveAlready )
                {
                    Brostbuster.currentBros.Remove( this );
                    this.acceptedDeath = true;
                }
            }
        }
        #endregion

        // Proton Gun methods
        #region ProtonGun
        protected override void StartFiring()
        {
            base.StartFiring();
            this.fireKnockbackCooldown = 0f;
            this.protonAudio.enabled = true;
            this.protonAudio.clip = protonStartup;
            this.protonAudio.loop = false;
            this.protonAudio.volume = 0.33f;
            this.protonAudio.Play();
            // Shorten beam startup if shutdown hasn't fully finished and beam was previously active
            if ( this.shutdownTime - 0.2f > 0f && this.playedBeamStartup )
            {
                this.startupTime = this.shutdownTime / 2f;
                this.protonAudio.time = this.startupTime / 2f;
            }
            else
            {
                this.startupTime = 0f;
                this.protonAudio.time = 0f;
            }
            this.playedBeamStartup = false;
        }

        protected override void RunFiring()
        {
            this.protonDamageCooldown -= this.t;
            this.effectCooldown -= this.t;
            this.flameCooldown -= this.t;
            this.fireKnockbackCooldown -= this.t;
            if ( this.fire )
            {
                if ( !this.playedBeamStartup )
                {
                    this.startupTime += this.t;
                    // Play beam startup, start beam
                    if ( this.startupTime > 1.1f || !this.protonAudio.isPlaying )
                    {
                        this.protonAudio.clip = protonBeamStartup;
                        this.protonAudio.time = 0f;
                        this.protonAudio.Play();
                        this.StartProtonGun();
                        this.playedBeamStartup = true;
                    }
                }
                else
                {
                    // Start proton loop
                    if ( !this.protonAudio.isPlaying )
                    {
                        this.protonAudio.clip = protonLoop;
                        this.protonAudio.loop = true;
                        this.protonAudio.Play();
                    }
                    this.currentOffset += this.t * offsetSpeed;
                    this.currentOffset2 += this.t * offsetSpeed * 1.9f;
                    UpdateProtonGun();
                    this.StopRolling();
                    this.FireFlashAvatar();
                    if ( this.currentGesture != GestureElement.Gestures.None )
                    {
                        SetGestureAnimation( GestureElement.Gestures.None );
                    }
                }
            }
            else
            {
                if ( this.shutdownTime > 0f )
                {
                    this.shutdownTime -= this.t;
                    if ( this.shutdownTime <= 0f )
                    {
                        this.protonAudio.enabled = false;
                        this.playedBeamStartup = false;
                    }
                }
            }
        }

        protected override void AddSpeedLeft()
        {
            if ( this.usingSlimerMelee )
            {
                return;
            }
            base.AddSpeedLeft();
            if ( this.xIBlast > this.speed * 1.6f )
            {
                this.xIBlast = this.speed * 1.6f;
            }
        }

        protected override void AddSpeedRight()
        {
            if ( this.usingSlimerMelee )
            {
                return;
            }
            base.AddSpeedRight();
            if ( this.xIBlast < this.speed * -1.6f )
            {
                this.xIBlast = this.speed * -1.6f;
            }
        }

        protected override void RunGun()
        {
            if ( !this.wallDrag && this.fire && this.playedBeamStartup )
            {
                this.gunCounter += this.t;
                if ( this.gunCounter > 0.07f )
                {
                    this.gunCounter -= 0.07f;
                    this.gunFrame--;
                    if ( this.gunFrame < 1 )
                    {
                        this.gunFrame = 3;
                    }
                    this.SetGunSprite( this.gunFrame, 0 );
                }
            }
            else if ( this.gunFrame > 0 )
            {
                this.gunCounter += this.t;
                if ( this.gunCounter > 0.0334f )
                {
                    this.gunCounter -= 0.0334f;
                    this.gunFrame--;
                    this.SetGunSprite( this.gunFrame, 0 );
                }
            }
            else
            {
                this.SetGunSprite( this.gunFrame, 0 );
            }
        }

        protected override void SetGunPosition( float xOffset, float yOffset )
        {
            currentGunPosition = yOffset;
            base.SetGunPosition( xOffset, yOffset );
        }

        protected override void StopFiring()
        {
            base.StopFiring();
            this.StopProtonGun();
            this.protonAudio.clip = protonEnd;
            this.protonAudio.loop = false;
            this.protonAudio.volume = 0.08f;
            this.protonAudio.Play();
            this.shutdownTime = protonEnd.length + 0.2f;
        }

        protected void StartProtonGun()
        {
            this.currentOffset = 0;
            this.currentOffset2 = 0;
            this.protonLine2Frame = 0;
            this.protonLine2FrameCounter = 0f;
            this.protonDamageCooldown = 0f;
            this.effectCooldown = 0f;

            this.protonLine1.enabled = true;
            this.protonLine1.startWidth = 8f;
            this.protonLine1.endWidth = 13f;
            this.protonLine1.textureMode = LineTextureMode.RepeatPerSegment;

            this.protonLine1Cap.enabled = true;
            this.protonLine1Cap.startWidth = 3f;
            this.protonLine1Cap.textureMode = LineTextureMode.RepeatPerSegment;
            this.protonLine1Cap.numCornerVertices = 10;
            this.protonLine1Cap.positionCount = 3;

            this.protonLine2.enabled = true;
            this.protonLine2.startWidth = 13f;
            this.protonLine2.endWidth = 18f;
            this.protonLine2.textureMode = LineTextureMode.RepeatPerSegment;

            DrawProtonLine();
        }

        protected void UpdateProtonGun()
        {
            DrawProtonLine();
        }

        protected void StopProtonGun()
        {
            this.protonLine1.enabled = false;
            this.protonLine1Cap.enabled = false;
            this.protonLine2.enabled = false;
        }

        protected void DrawProtonLine()
        {
            Vector3 startPoint = new Vector3( base.X + base.transform.localScale.x * 10f, base.Y + 7f + currentGunPosition, 0 );
            Vector3 endPoint = Vector3.zero;
            Vector3 startPointCap = startPoint;
            startPoint.x += base.transform.localScale.x * 10f;
            Vector3 startPointCapEnd = startPoint;
            startPointCapEnd.x += base.transform.localScale.x * 0.5f;
            float capOffset = base.transform.localScale.x * ( 1.9f * Math.Sin( Math.PI2 * currentOffset ) );
            startPointCapEnd.y += capOffset;
            Vector3 startPointCapMid = new Vector3( base.transform.localScale.x * Mathf.Abs( startPointCap.x - startPointCapEnd.x ) / 2 + startPointCap.x, startPointCap.y + 0.75f * capOffset );

            // Create sparks at tip of gun
            if ( sparkCooldown <= 0 )
            {
                int particleCount = rnd.Next( 3, 5 );
                for ( int i = 0; i < particleCount; ++i )
                {
                    EffectsController.CreateSparkParticles( EffectsController.instance.sparkParticleShower, startPointCap.x, startPoint.y, 1, 0, 30f + UnityEngine.Random.value * 20f, UnityEngine.Random.value * 50f, UnityEngine.Random.value * 50f, 0.5f, 0.2f + UnityEngine.Random.value * 0.2f );
                }
                sparkCooldown = UnityEngine.Random.Range( 0.25f, 0.5f );
            }
            sparkCooldown -= this.t;

            // Run hit detection
            Vector3 hitDetectionStart = new Vector3( base.X, base.Y + 7f, 0 );
            ProtonLineHitDetection( hitDetectionStart, ref endPoint );

            // Check if hit point is too close to have both segments of proton line 1
            float DistanceToEnd = Vector3.Distance( hitDetectionStart, endPoint );

            // Update proton line 2 material
            this.protonLine2.material = this.protonLine2Mats[protonLine2Frame];
            this.protonLine2FrameCounter += this.t;
            if ( this.protonLine2FrameCounter >= 0.1f )
            {
                this.protonLine2FrameCounter -= 0.1f;
                ++this.protonLine2Frame;
                if ( this.protonLine2Frame > 3 )
                {
                    this.protonLine2Frame = 0;
                }
            }

            float magnitude = ( endPoint - startPoint ).magnitude;

            if ( DistanceToEnd > 30f )
            {
                this.protonLine1.SetPosition( 0, startPoint );
                this.protonLine1.SetPosition( 1, endPoint );
                this.protonLine1.material.SetTextureScale( "_MainTex", new Vector2( magnitude * 0.035f, 1f ) );
                this.protonLine1.material.SetTextureOffset( "_MainTex", new Vector2( -currentOffset, 0 ) );

                this.protonLine1Cap.SetPosition( 0, startPointCap );
                this.protonLine1Cap.SetPosition( 1, startPointCapMid );
                this.protonLine1Cap.SetPosition( 2, startPointCapEnd );

                startPointCap.z = -10f;
                endPoint.z = -10f;
                this.protonLine2.SetPosition( 0, startPointCap );
                this.protonLine2.SetPosition( 1, endPoint );
                this.protonLine2.material.SetTextureScale( "_MainTex", new Vector2( magnitude * 0.035f, 1f ) );
                this.protonLine2.material.SetTextureOffset( "_MainTex", new Vector2( -currentOffset2, 0f ) );
            }
            else if ( DistanceToEnd > 15f )
            {
                this.protonLine1.SetPosition( 0, startPoint );
                this.protonLine1.SetPosition( 1, startPoint );

                this.protonLine1Cap.SetPosition( 0, startPointCap );
                this.protonLine1Cap.SetPosition( 1, startPointCapMid );
                this.protonLine1Cap.SetPosition( 2, endPoint );

                startPointCap.z = -10f;
                endPoint.z = -10f;
                this.protonLine2.SetPosition( 0, startPointCap );
                this.protonLine2.SetPosition( 1, endPoint );
                this.protonLine2.material.SetTextureScale( "_MainTex", new Vector2( magnitude * 0.035f, 1f ) );
                this.protonLine2.material.SetTextureOffset( "_MainTex", new Vector2( -currentOffset2, 0f ) );
            }
            else
            {
                this.protonLine1.SetPosition( 0, startPoint );
                this.protonLine1.SetPosition( 1, startPoint );

                this.protonLine1Cap.SetPosition( 0, startPoint );
                this.protonLine1Cap.SetPosition( 1, startPoint );
                this.protonLine1Cap.SetPosition( 2, startPoint );

                this.protonLine2.SetPosition( 0, startPoint );
                this.protonLine2.SetPosition( 1, startPoint );
            }
        }

        public Unit HitClosestUnit( MonoBehaviour damageSender, int playerNum, float xRange, float yRange, float x, float y, int direction, Vector3 startPoint, bool haveHitGround, Vector3 groundVector )
        {
            if ( Map.units == null )
            {
                return null;
            }
            int num = 999999;
            float num2 = Mathf.Max( xRange, yRange );
            Unit unit = null;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                Unit unit3 = Map.units[i];
                if ( unit3 != null && !unit3.invulnerable && unit3.health <= num && GameModeController.DoesPlayerNumDamage( playerNum, unit3.playerNum ) )
                {
                    float f = unit3.X - x;
                    if ( Mathf.Abs( f ) - xRange < unit3.width && Mathf.Sign( f ) == direction )
                    {
                        float f2 = unit3.Y + unit3.height / 2f + 3f - y;
                        if ( Mathf.Abs( f2 ) - yRange < unit3.height )
                        {
                            float num4 = Mathf.Abs( f ) + Mathf.Abs( f2 );
                            if ( num4 < num2 )
                            {
                                if ( unit3.health > 0 )
                                {
                                    unit = unit3;
                                    num2 = num4;
                                }
                            }
                        }
                    }
                }
            }

            if ( unit != null && ( !haveHitGround || Mathf.Abs( unit.X - x ) < Mathf.Abs( groundVector.x - x ) ) )
            {
                DamageUnit( unit, startPoint );
                return unit;
            }
            return null;
        }

        public bool HitProjectiles( int playerNum, int damage, DamageType damageType, float xRange, float yRange, float x, float y, float xI, float yI, int direction )
        {
            bool result = false;
            for ( int i = Map.damageableProjectiles.Count - 1; i >= 0; i-- )
            {
                Projectile projectile = Map.damageableProjectiles[i];
                if ( projectile != null && GameModeController.DoesPlayerNumDamage( playerNum, projectile.playerNum ) )
                {
                    float f = projectile.X - x;
                    if ( Mathf.Abs( f ) - xRange < projectile.projectileSize && Mathf.Sign( f ) == direction )
                    {
                        float f2 = projectile.Y - y;
                        if ( Mathf.Abs( f2 ) - yRange < projectile.projectileSize )
                        {
                            Map.DamageProjectile( projectile, damage, damageType, xI, yI, 0f, playerNum );
                            result = true;
                        }
                    }
                }
            }
            return result;
        }

        protected void ProtonLineHitDetection( Vector3 startPoint, ref Vector3 endPoint )
        {
            RaycastHit groundHit = this.raycastHit;
            bool haveHitGround = false;
            bool haveCrossedStreams = false;
            float currentRange = protonLineRange;

            // Hit ground
            if ( Physics.Raycast( startPoint, ( base.transform.localScale.x > 0 ? Vector3.right : Vector3.left ), out raycastHit, currentRange, this.fragileGroundLayer ) )
            {
                groundHit = this.raycastHit;
                // Shorten the range we check for raycast hits, we don't care about hitting anything past the current terrain.
                currentRange = this.raycastHit.distance;
                haveHitGround = true;
            }

            // Check for streams crossing
            if ( currentBros.Count > 1 )
            {
                try
                {
                    foreach ( Brostbuster bro in currentBros )
                    {
                        // Check both are firing and are around the same y level
                        if ( bro.fire && bro.playedBeamStartup && Tools.FastAbsWithinRange( base.Y - bro.Y, 10 ) )
                        {
                            float distance = base.X - bro.X;
                            // Check they are firing towards each other
                            if ( Tools.FastAbsWithinRange( distance, currentRange ) && -1 * Mathf.Sign( distance ) == base.transform.localScale.x && Mathf.Sign( distance ) == bro.transform.localScale.x )
                            {
                                currentRange = Mathf.Abs( distance / 2.0f );
                                haveCrossedStreams = true;
                                haveHitGround = false;
                            }
                        }
                    }
                }
                catch { }
            }

            Unit unit;
            if ( ( unit = HitClosestUnit( this, this.playerNum, currentRange, 6, startPoint.x, startPoint.y, (int)base.transform.localScale.x, startPoint, haveHitGround, groundHit.point ) ) != null )
            {
                endPoint = new Vector3( unit.X, startPoint.y, 0 );
            }
            // Damage ground since no unit hit
            else if ( haveHitGround )
            {
                DamageCollider( groundHit );
                endPoint = new Vector3( groundHit.point.x, groundHit.point.y, 0 );
            }
            // Crossed streams with another Brostbuster
            else if ( haveCrossedStreams )
            {
                endPoint = new Vector3( startPoint.x + base.transform.localScale.x * currentRange, startPoint.y, 0 );
                // Create explosion
                this.CreateExplosion( endPoint );
            }
            // Nothing hit
            else
            {
                endPoint = new Vector3( startPoint.x + base.transform.localScale.x * protonLineRange, startPoint.y, 0 );
            }

            HitProjectiles( this.playerNum, protonUnitDamage, DamageType.Fire, Mathf.Abs( endPoint.x - startPoint.x ), 6f, startPoint.x, startPoint.y, base.transform.localScale.x * 30, 20, (int)base.transform.localScale.x );
        }

        protected void DamageUnit( Unit unit, Vector3 startPoint )
        {
            if ( this.protonDamageCooldown > 0 )
            {
                return;
            }

            // Only damage visible objects
            if ( unit != null && SortOfFollow.IsItSortOfVisible( unit.transform.position, 24, 24f ) )
            {
                unit.Damage( protonUnitDamage, DamageType.Fire, base.transform.localScale.x, 0, (int)base.transform.localScale.x, this, unit.X, unit.Y );
                unit.Knock( DamageType.Fire, base.transform.localScale.x * 30, 20, false );

                // Deal extra damage to bosses and vehicles
                if ( BroMakerUtilities.IsBoss( unit ) || unit is Tank )
                {
                    protonDamageCooldown = 0.055f;
                }
                else
                {
                    protonDamageCooldown = 0.08f;
                }
            }

            MakeHitEffect( new Vector3( unit.X + base.transform.localScale.x * 4, startPoint.y ) );
        }

        protected void DamageCollider( RaycastHit hit )
        {
            if ( this.protonDamageCooldown > 0 )
            {
                return;
            }

            // Only damage visible objects
            if ( SortOfFollow.IsItSortOfVisible( hit.point, 24, 24f ) )
            {
                Unit unit = hit.collider.GetComponent<Unit>();
                // Damage unit
                if ( unit != null )
                {
                    // Add damage if we're hitting a boss
                    unit.Damage( protonUnitDamage + ( unit.health > 30 ? 1 : 0 ), DamageType.Fire, base.transform.localScale.x, 0, (int)base.transform.localScale.x, this, hit.point.x, hit.point.y );
                    unit.Knock( DamageType.Fire, base.transform.localScale.x * 30, 20, false );
                }
                // Damage other
                else
                {
                    hit.collider.SendMessage( "Damage", new DamageObject( protonWallDamage, DamageType.Bullet, 0f, 0f, hit.point.x, hit.point.y, this ) );
                }

                protonDamageCooldown = 0.05f;
            }

            MakeHitEffect( new Vector3( hit.point.x + base.transform.localScale.x * 4, hit.point.y ) );
        }

        protected void MakeHitEffect(Vector3 hitPoint)
        {
            if ( this.effectCooldown <= 0 )
            {
                EffectsController.CreateEffect( EffectsController.instance.whiteFlashPopSmallPrefab, hitPoint.x, hitPoint.y + UnityEngine.Random.Range( -3, 3 ), 0f, 0f, Vector3.zero, null );

                this.effectCooldown = 0.15f;
            }

            if ( this.flameCooldown <= 0 )
            {
                EffectsController.CreateFire( hitPoint.x, hitPoint.y + UnityEngine.Random.Range( -1f, 1f ) - 2f, 0f, 0f, new Vector3( base.transform.localScale.x * UnityEngine.Random.Range( 0, 20 ), UnityEngine.Random.Range( 0, 20 ), 0f ) );

                this.flameCooldown = 0.1f;
            }
        }

        protected void CreateExplosion( Vector3 position )
        {
            if ( this.effectCooldown > 0 )
            {
                return;
            }
            EffectsController.CreatePlumes( position.x, position.y, 5, 10f, 360f, 0f, 0f );
            if ( base.IsMine )
            {
                float explodeRange = 90f;
                int explodeDamage = 30;
                MapController.DamageGround( this, explodeDamage, DamageType.Explosion, explodeRange, position.x, position.y, null, false );
                Map.ExplodeUnits( this, explodeDamage, DamageType.Explosion, explodeRange, explodeRange * 0.8f, position.x, position.y - 32f, 200f, 200f, base.playerNum, true, true, true );
                MapController.BurnUnitsAround_NotNetworked( this, base.playerNum, 5, explodeRange * 1f, position.x, position.y, true, true );
                Map.HitProjectiles( base.playerNum, explodeDamage, DamageType.Explosion, explodeRange, position.x, position.y, 0f, 0f, 0.25f );
                Map.ShakeTrees( position.x, position.y, 320f, 64f, 160f );
            }
            SortOfFollow.Shake( 1f );
            EffectsController.CreateHugeExplosion( position.x, position.y, 20f, 20f, 80f, 1f, 32f, 0.7f, 0.9f, 8, 20, 110f, 160f, 0f, 0.9f );
            this.effectCooldown = 0.3f;
        }

        protected override void OnDestroy()
        {
            if ( this.actionState != ActionState.Dead )
            {
                Brostbuster.currentBros.Remove( this );
            }
            base.OnDestroy();
        }
        #endregion

        // Special Methods
        #region Special
        protected override void UseSpecial()
        {
            if ( this.currentTrap != null && this.currentTrap.state != GhostTrap.TrapState.Closed )
            {
                // Close Trap
                this.currentTrap.StartClosingTrap();
            }
            else if ( this.SpecialAmmo > 0 )
            {
                this.PlayThrowLightSound( 0.4f );
                this.SpecialAmmo--;
                if ( base.IsMine )
                {
                    Grenade grenade;
                    if ( this.down && this.IsOnGround() && this.ducking )
                    {
                        grenade = ProjectileController.SpawnGrenadeLocally( this.trapPrefab, this, base.X + Mathf.Sign( base.transform.localScale.x ) * 6f, base.Y + 3f, 0.001f, 0.011f, Mathf.Sign( base.transform.localScale.x ) * 30f, 70f, base.playerNum, 0 );
                    }
                    else
                    {
                        grenade = ProjectileController.SpawnGrenadeLocally( this.trapPrefab, this, base.X + Mathf.Sign( base.transform.localScale.x ) * 8f, base.Y + 8f, 0.001f, 0.011f, Mathf.Sign( base.transform.localScale.x ) * 200f, 150f, base.playerNum, 0 );
                    }
                    this.currentTrap = grenade.GetComponent<GhostTrap>();
                    this.currentTrap.thrownBy = this.playerNum;
                    this.currentTrap.enabled = true;
                }
            }
            else
            {
                HeroController.FlashSpecialAmmo( base.playerNum );
                this.ActivateGun();
            }
            this.pressSpecialFacingDirection = 0;
        }

        public void ReturnTrap()
        {
            ++this.SlimerTraps;
        }
        #endregion

        // Melee methods
        #region Melee
        // Performs melee attack
        protected void MeleeAttack( bool shouldTryHitTerrain, bool playMissSound )
        {
            bool flag;
            Map.DamageDoodads( 3, DamageType.Knock, base.X + (float)( base.Direction * 4 ), base.Y, 0f, 0f, 6f, base.playerNum, out flag, null );
            this.KickDoors( 24f );
            if ( Map.HitClosestUnit( this, base.playerNum, 4, DamageType.Knock, 14f, 24f, base.X + base.transform.localScale.x * 8f, base.Y + 8f, base.transform.localScale.x * 200f, 500f, true, false, base.IsMine, false, true ) )
            {
                this.sound.PlaySoundEffectAt( this.soundHolder.alternateMeleeHitSound, 1f, base.transform.position, 1f, true, false, false, 0f );
                this.meleeHasHit = true;
            }
            else if ( playMissSound )
            {
                this.sound.PlaySoundEffectAt( this.soundHolder.missSounds, 0.3f, base.transform.position, 1f, true, false, false, 0f );
            }
            this.meleeChosenUnit = null;
            if ( shouldTryHitTerrain && this.TryMeleeTerrain( 0, 2 ) )
            {
                this.meleeHasHit = true;
            }
            this.TriggerBroMeleeEvent();
        }

        protected void SpawnSlimer()
        {
            currentSlimer = slimerPrefab.SpawnProjectileLocally( this, base.X, base.Y + 6f, base.transform.localScale.x * 175f, 0f, base.playerNum ) as Slimer;
            --this.SlimerTraps;
            this.alreadySpawnedSlimer = true;
        }

        // Sets up melee attack
        protected override void StartCustomMelee()
        {
            if ( this.usingSlimerMelee )
            {
                return;
            }
            if ( this.CanStartNewMelee() )
            {
                this.alreadySpawnedSlimer = false;
                this.playedSlimerAudio = false;
                base.frame = 1;
                base.counter = -0.05f;
                this.throwingMook = ( this.nearbyMook != null && this.nearbyMook.CanBeThrown() );
                this.usingSlimerMelee = ( this.SlimerTraps > 0 ) && !this.throwingMook;
                this.AnimateMelee();
            }
            else if ( this.CanStartMeleeFollowUp() )
            {
                this.meleeFollowUp = true;
                this.alreadySpawnedSlimer = false;
                this.playedSlimerAudio = false;
                this.usingSlimerMelee = ( this.SlimerTraps > 0 );
            }
            if ( !this.jumpingMelee )
            {
                this.dashingMelee = true;
                if ( !this.usingSlimerMelee )
                {
                    this.xI = (float)base.Direction * this.speed;
                }
            }
            this.StartMeleeCommon();
        }

        // Calls MeleeAttack
        protected override void AnimateCustomMelee()
        {
            this.AnimateMeleeCommon();
            // Release Slimer
            if ( this.usingSlimerMelee )
            {
                base.frameRate = 0.06f;
                int num = 11 + base.frame;
                this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( 9 * this.spritePixelHeight ) );
                if ( base.frame == 0 && !this.playedSlimerAudio )
                {
                    this.slimerPortalSource = this.sound.PlaySoundEffectAt( this.slimerTrapOpen, 0.4f, base.transform.position, 1f, true, false, true, 0f );
                    this.playedSlimerAudio = true;
                }
                else if ( base.frame == 8 && !this.alreadySpawnedSlimer )
                {
                    base.counter -= 0.066f;
                    SpawnSlimer();
                }
                else if ( base.frame >= 9 )
                {
                    base.frame = 0;
                    this.CancelMelee();
                }
            }
            // Proton Bash
            else
            {
                if ( !this.throwingMook )
                {
                    base.frameRate = 0.04f;
                }
                int num = 25 + Mathf.Clamp( base.frame, 0, 6 );
                int num2 = 1;
                if ( !this.standingMelee )
                {
                    if ( this.jumpingMelee )
                    {
                        num = 17 + Mathf.Clamp( base.frame, 0, 6 );
                        num2 = 6;
                    }
                    else if ( this.dashingMelee )
                    {
                        num = 17 + Mathf.Clamp( base.frame, 0, 6 );
                        num2 = 6;
                        if ( base.frame == 4 )
                        {
                            base.counter -= 0.0334f;
                        }
                        else if ( base.frame == 5 )
                        {
                            base.counter -= 0.0334f;
                        }
                    }
                }
                this.sprite.SetLowerLeftPixel( (float)( num * this.spritePixelWidth ), (float)( num2 * this.spritePixelHeight ) );
                if ( base.frame == 3 )
                {
                    base.counter -= 0.066f;
                    this.MeleeAttack( true, true );
                }
                else if ( base.frame > 3 && !this.meleeHasHit )
                {
                    this.MeleeAttack( false, false );
                }
                if ( base.frame >= 6 )
                {
                    base.frame = 0;
                    this.CancelMelee();
                }
            }
        }

        protected override void RunCustomMeleeMovement()
        {
            if ( this.usingSlimerMelee )
            {
                if ( !this.jumpingMelee )
                {
                    if ( this.frame < 9 )
                    {
                        this.xI = Mathf.Lerp( this.xI, 0, this.t * 5 );
                    }
                }

                if ( base.actionState == ActionState.Jumping )
                {
                    if ( this.isInQuicksand )
                    {
                        this.xI *= 1f - this.t * 20f;
                        this.xI = Mathf.Clamp( this.xI, -16f, 16f );
                        this.xIBlast *= 1f - this.t * 20f;
                    }
                    if ( this.jumpTime > 0f )
                    {
                        this.jumpTime -= this.t;
                        if ( !this.buttonJump )
                        {
                            this.jumpTime = 0f;
                        }
                    }
                    if ( !( this.impaledByTransform != null ) )
                    {
                        if ( this.wallClimbing )
                        {
                            this.ApplyWallClimbingGravity();
                        }
                        else if ( this.yI > 40f )
                        {
                            this.ApplyFallingGravity();
                        }
                        else
                        {
                            this.ApplyFallingGravity();
                        }
                    }
                    if ( this.yI < this.maxFallSpeed )
                    {
                        this.yI = this.maxFallSpeed;
                    }
                    if ( this.yI < -50f )
                    {
                        this.RunFalling();
                    }
                    if ( this.canCeilingHang && this.hangGrace > 0f )
                    {
                        this.RunCheckHanging();
                    }
                }

                // Have to cancel slimer melee if interacting with walls because the animation gets glitched out with how long it takes
                if ( this.chimneyFlip || this.wallClimbing || this.wallDragTime > 0f )
                {
                    this.CancelMelee();
                }
            }
            else
            {
                if ( this.jumpingMelee )
                {
                    this.ApplyFallingGravity();
                    if ( this.yI < this.maxFallSpeed )
                    {
                        this.yI = this.maxFallSpeed;
                    }
                }
                else if ( this.dashingMelee )
                {
                    if ( base.frame <= 1 )
                    {
                        this.xI = 0f;
                        this.yI = 0f;
                    }
                    else if ( base.frame <= 3 )
                    {
                        if ( this.meleeChosenUnit == null )
                        {
                            if ( !this.isInQuicksand )
                            {
                                this.xI = this.speed * 1f * base.transform.localScale.x;
                            }
                            this.yI = 0f;
                        }
                        else if ( !this.isInQuicksand )
                        {
                            this.xI = this.speed * 0.5f * base.transform.localScale.x + ( this.meleeChosenUnit.X - base.X ) * 6f;
                        }
                    }
                    else if ( base.frame <= 5 )
                    {
                        if ( !this.isInQuicksand )
                        {
                            this.xI = this.speed * 0.3f * base.transform.localScale.x;
                        }
                        this.ApplyFallingGravity();
                    }
                    else
                    {
                        this.ApplyFallingGravity();
                    }
                }
                else if ( base.Y > this.groundHeight + 1f )
                {
                    this.CancelMelee();
                }
            }
        }

        protected override void CancelMelee()
        {
            // Stop portal sound if melee was cancelled before slimer spawned
            if ( this.slimerPortalSource != null && !this.alreadySpawnedSlimer && this.usingSlimerMelee )
            {
                this.slimerPortalSource.Stop();
            }
            base.CancelMelee();
            this.usingSlimerMelee = false;
        }

        protected override void AnimateWallAnticipation()
        {
            // Have to cancel slimer melee if interacting with walls because the animation gets glitched out with how long it takes
            if ( this.usingSlimerMelee )
            {
                this.CancelMelee();
            }
            base.AnimateWallAnticipation();
        }
        #endregion
    }
}
