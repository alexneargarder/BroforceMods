using System;
using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using BroMakerLib.Loggers;
using Effects;
using UnityEngine;

namespace Brostbuster
{
    class GhostTrap : CustomGrenade
    {
        // Audio
        public AudioClip trapOpen;
        public AudioClip trapMain;
        public AudioClip trapClosing;
        public AudioClip trapClosed;
        protected AudioSource trapAudio;
        protected int currentClip = 0;
        protected float shutdownTime = 0f;

        // Visuals
        protected int frame = 0;
        protected float counter = 0f;
        protected const float trapWidth = 224f;
        protected const float trapHeight = 128f;
        protected float trapFramerate = 0.145f;
        // Starting from 0
        protected const int lastOpeningFrame = 9;
        protected const int lastFrame = 19;

        // State
        public enum TrapState
        {
            Thrown = 0,
            Opening = 1,
            Open = 2,
            Closing = 3,
            ClosingAnimation = 4,
            Closed = 5
        }
        public TrapState state = TrapState.Thrown;
        public float runTime = 0f;
        protected float attractCounter = 0f;
        public bool expediteClose = false;

        // Hit detection
        public float topLeftX, topLeftY, topRightX, topRightY, bottomX, bottomY;
        public float topFloatingY, leftFloatingX, rightFloatingX;
        public const float width = 120f;
        public const float height = 130f;
        public static Dictionary<Unit, FloatingObject> grabbedUnits = new Dictionary<Unit, FloatingObject>();
        public static Dictionary<BroforceObject, FloatingObject> grabbedObjects = new Dictionary<BroforceObject, FloatingObject>();
        public List<FloatingObject> floatingObjects = new List<FloatingObject>();
        public List<Unit> ignoredUnits = new List<Unit>();
        public int killedUnits = 0;
        public int thrownBy = -1;

        protected override void Awake()
        {
            try
            {
                if ( this.sprite == null )
                {
                    this.SpriteLowerLeftPixel = new Vector2( 0, trapHeight );
                    this.SpritePixelDimensions = new Vector2( trapWidth, trapHeight );
                    this.spriteWidth = trapWidth;
                    this.spriteHeight = trapHeight;
                    this.SpriteOffset = new Vector3( 9, 60, 0 );
                }

                this.DefaultSoundHolder = ( HeroController.GetHeroPrefab( HeroType.SnakeBroSkin ) as SnakeBroskin ).specialGrenade.soundHolder;

                base.Awake();

                // Setup Variables
                this.disabledAtStart = false;
                this.shrink = false;
                this.trailType = TrailType.ColorTrail;
                this.rotationSpeedMultiplier = 0f;
                this.lifeLossOnBounce = false;
                this.deathOnBounce = false;
                this.destroyInsideWalls = false;
                this.startLife = 10000f;
                this.life = 10000f;

                if ( this.trapAudio == null )
                {
                    if ( base.gameObject.GetComponent<AudioSource>() == null )
                    {
                        this.trapAudio = base.gameObject.AddComponent<AudioSource>();
                        this.trapAudio.rolloffMode = AudioRolloffMode.Logarithmic;
                        this.trapAudio.minDistance = 300f;
                        this.trapAudio.dopplerLevel = 0.1f;
                        this.trapAudio.maxDistance = 3000f;
                        this.trapAudio.spatialBlend = 1f;
                        this.trapAudio.volume = 0.33f;
                    }
                    else
                    {
                        this.trapAudio = this.GetComponent<AudioSource>();
                    }
                }

                // Needed for transparent sprites
                this.gameObject.layer = 28;
            }
            catch ( Exception ex )
            {
                BMLogger.Log( "Exception Creating Projectile: " + ex.ToString() );
            }
        }

        public override void PrefabSetup()
        {
            trapOpen = ResourcesController.GetAudioClip( SoundPath, "trapOpen.wav" );
            trapMain = ResourcesController.GetAudioClip( SoundPath, "trapMain.wav" );
            trapClosing = ResourcesController.GetAudioClip( SoundPath, "trapClosing.wav" );
            trapClosed = ResourcesController.GetAudioClip( SoundPath, "trapClosed.wav" );
        }

        // Called when picking up and throwing already thrown grenade
        public override void ThrowGrenade( float XI, float YI, float newX, float newY, int _playerNum )
        {
            //base.ThrowGrenade(XI, YI, newX, newY, _playerNum);
        }

        // Called on first throw
        public override void Launch( float newX, float newY, float xI, float yI )
        {
            base.Launch( newX, newY, xI, yI );
            this.startLife = 10000f;
            this.life = 10000f;
        }

        // Don't register grenade
        protected override void RegisterGrenade()
        {
        }

        // Don't bounce off door infinitely
        protected override void HitFragile()
        {
            Vector3 vector = new Vector3( this.xI, this.yI, 0f );
            Vector3 normalized = vector.normalized;
            Collider[] array = Physics.OverlapSphere( new Vector3( base.X + normalized.x * 2f, base.Y + normalized.y * 2f, 0f ), 2f, this.fragileLayer );
            for ( int i = 0; i < array.Length; i++ )
            {
                EffectsController.CreateProjectilePuff( base.X, base.Y );
                if ( array[i].GetComponent<DoorDoodad>() != null && this.state != TrapState.Closed )
                {
                    this.Bounce( true, false );
                }
                else
                {
                    array[i].gameObject.SendMessage( "Damage", new DamageObject( 1, this.damageType, this.xI, this.yI, base.X, base.Y, this ), SendMessageOptions.DontRequireReceiver );
                }
            }
        }

        protected override bool Update()
        {
            base.Update();

            this.runTime += this.t;

            // Opening or Open
            switch ( this.state )
            {
                case TrapState.Opening:
                case TrapState.Open:
                    {
                        this.counter += this.t;
                        if ( this.counter > trapFramerate )
                        {
                            this.counter -= trapFramerate;
                            ++this.frame;
                            if ( this.state != TrapState.Open && this.frame == lastOpeningFrame + 1 )
                            {
                                this.trapFramerate = 0.08f;
                                this.state = TrapState.Open;
                            }
                            else if ( this.frame > lastFrame )
                            {
                                this.frame = lastOpeningFrame + 1;
                            }
                            this.sprite.SetLowerLeftPixel( frame * trapWidth, trapHeight );
                        }

                        this.attractCounter += this.t;
                        if ( this.runTime < 6f && this.attractCounter >= 0.0334f )
                        {
                            this.attractCounter -= 0.0334f;
                            Map.AttractMooks( base.X, base.Y, 200f, 100f );
                        }


                        if ( this.state > TrapState.Opening )
                        {
                            // Don't start grabbing units until trap is open
                            this.FindObjects();
                        }


                        // Play next clip
                        if ( ( this.trapAudio.clip.length - this.trapAudio.time ) <= 0.02f )
                        {
                            // Start playing main audio clip
                            if ( this.currentClip == 0 )
                            {
                                this.trapAudio.clip = trapMain;
                                this.trapAudio.Play();
                                ++this.currentClip;
                            }
                            // Play closing clip
                            else
                            {
                                this.trapAudio.clip = trapClosing;
                                this.trapAudio.Play();
                                this.currentClip = 2;
                            }
                        }

                        // Start closing trap after closing audio clip has played for a second
                        if ( this.runTime > 8f )
                        {
                            this.trapFramerate = 0.08f;
                            this.runTime = 10f;
                            this.state = TrapState.Closing;
                        }

                        break;
                    }
                case TrapState.Closing:
                    {
                        this.counter += this.t;
                        if ( this.counter > trapFramerate )
                        {
                            this.counter -= trapFramerate;
                            --this.frame;
                            if ( this.frame < lastOpeningFrame + 1 )
                            {
                                this.frame = lastFrame;
                            }
                            this.sprite.SetLowerLeftPixel( frame * trapWidth, trapHeight );
                        }

                        if ( this.runTime < 14f )
                        {
                            this.FindObjects();
                        }

                        if ( currentClip == 2 && ( ( this.trapAudio.clip.length - this.trapAudio.time ) <= 0.02f ) )
                        {
                            this.trapAudio.clip = trapClosed;
                            this.trapAudio.Play();
                            ++this.currentClip;
                            this.shutdownTime = trapClosed.length + 0.1f;
                        }

                        // Speed up trap closing by moving to final clip
                        if ( this.expediteClose && this.runTime > 12f && this.floatingObjects.Count == 0 && currentClip == 2 )
                        {
                            this.trapAudio.clip = trapClosed;
                            this.trapAudio.Play();
                            ++this.currentClip;
                            this.runTime = 15.5f;
                            this.shutdownTime = trapClosed.length + 0.1f;
                        }

                        if ( this.runTime > 16f )
                        {
                            this.CloseTrap();
                        }
                        break;
                    }
                case TrapState.ClosingAnimation:
                    {
                        // Animate closing
                        this.counter += this.t;
                        if ( this.counter > trapFramerate )
                        {
                            this.counter -= trapFramerate;
                            --this.frame;
                            // Finished closing
                            if ( this.frame < 0 )
                            {
                                this.frame = 0;
                                this.xI = 0;
                                this.yI = 0;
                                this.state = TrapState.Closed;
                            }
                            this.sprite.SetLowerLeftPixel( frame * trapWidth, trapHeight );
                        }
                        break;
                    }
                case TrapState.Closed:
                    {
                        CheckReturnTrap();
                        this.sprite.SetLowerLeftPixel( 0, trapHeight );
                        if ( this.shutdownTime > 0f )
                        {
                            this.shutdownTime -= this.t;
                            if ( this.shutdownTime <= 0 )
                            {
                                this.trapAudio.enabled = false;
                            }
                        }
                        break;
                    }
                default:
                    break;
            }

            return true;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            this.MoveUnits();
        }

        protected override void RunMovement()
        {
            if ( this.state == TrapState.Thrown || this.state == TrapState.Closed )
            {
                base.RunMovement();
                if ( this.Y < 0 )
                {
                    this.DestroyGrenade();
                }
            }
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            if ( this.state != TrapState.Closed && bounceY && this.yI <= 0f )
            {
                this.ActivateTrap();
                this.yI = 0f;
                this.xI = 0f;
            }
            else
            {
                base.Bounce( bounceX, bounceY );
            }
        }

        protected override void RunWarnings()
        {
        }

        public override void Death()
        {
            if ( this.state == TrapState.Closed && this.runTime > 100f )
            {
                this.DestroyGrenade();
            }
        }

        protected override void DestroyGrenade()
        {
            UnityEngine.Object.Destroy( base.gameObject );
        }

        protected override void OnDestroy()
        {
            this.ReleaseUnits();

            base.OnDestroy();
        }

        protected void ActivateTrap()
        {
            this.runTime = 0.5f;
            this.state = TrapState.Opening;

            this.trapAudio.clip = trapOpen;
            this.trapAudio.loop = false;
            this.trapAudio.Play();

            // Set triangle bounds
            bottomX = base.X;
            bottomY = base.Y - 10f;
            topRightX = bottomX + width;
            topRightY = bottomY + height;
            topLeftX = bottomX - width;
            topLeftY = topRightY;

            // Controls where the enemies are allowed to be
            topFloatingY = base.Y + 110f;
            leftFloatingX = base.X - 90f;
            rightFloatingX = base.X + 90f;
        }

        public void StartClosingTrap()
        {
            this.expediteClose = true;
            if ( this.state == TrapState.Thrown )
            {
                this.ActivateTrap();
            }
            else if ( this.state < TrapState.Closing )
            {
                this.trapFramerate = 0.08f;
                this.runTime = 10f;
                this.state = TrapState.Closing;
                this.trapAudio.clip = trapClosing;
                this.trapAudio.Play();
                this.currentClip = 2;
            }
        }

        public void CloseTrap()
        {
            this.frame = lastOpeningFrame;
            this.counter = 0;
            this.trapFramerate = 0.11f;
            this.state = TrapState.ClosingAnimation;
            this.sprite.SetLowerLeftPixel( frame * trapWidth, trapHeight );
            this.ReleaseUnits();
        }

        protected void CheckReturnTrap()
        {
            if ( this.state == TrapState.Closed )
            {
                // Only return to owner if still living
                if ( this.firedBy != null && ( this.firedBy as TestVanDammeAnim ).health > 0 )
                {
                    float f = this.firedBy.transform.position.x - base.X;
                    float f2 = this.firedBy.transform.position.y + 10f - base.Y;
                    if ( Mathf.Abs( f ) < 9f && Mathf.Abs( f2 ) < 14f )
                    {
                        Brostbuster bro = this.firedBy as Brostbuster;
                        if ( bro && killedUnits > 0 )
                        {
                            bro.ReturnTrap();
                        }
                        Sound.GetInstance().PlaySoundEffectAt( this.soundHolder.powerUp, 0.7f, base.transform.position, 0.95f + UnityEngine.Random.value * 0.1f, true, false, false, 0f );
                        EffectsController.CreatePuffDisappearEffect( base.X, base.Y + 3f, 0f, 0f );
                        this.DestroyGrenade();
                    }
                }
                // Return to any brostbuster if original owner is dead
                else
                {
                    for ( int i = 0; i < HeroController.players.Length; ++i )
                    {
                        if ( HeroController.players[i] != null && HeroController.players[i].IsAliveAndSpawnedHero() )
                        {
                            if ( HeroController.players[i].character is Brostbuster bro )
                            {
                                float f = bro.transform.position.x - base.X;
                                float f2 = bro.transform.position.y + 10f - base.Y;
                                if ( Mathf.Abs( f ) < 9f && Mathf.Abs( f2 ) < 14f )
                                {
                                    if ( killedUnits > 0 )
                                    {
                                        bro.ReturnTrap();
                                    }
                                    Sound.GetInstance().PlaySoundEffectAt( this.soundHolder.powerUp, 0.7f, base.transform.position, 0.95f + UnityEngine.Random.value * 0.1f, true, false, false, 0f );
                                    EffectsController.CreatePuffDisappearEffect( base.X, base.Y + 3f, 0f, 0f );
                                    this.DestroyGrenade();
                                }
                            }
                        }
                    }
                }
            }
        }

        float Sign( float p1X, float p1Y, float p2X, float p2Y, float p3X, float p3Y )
        {
            return ( p1X - p3X ) * ( p2Y - p3Y ) - ( p2X - p3X ) * ( p1Y - p3Y );
        }

        bool ShouldGrabUnit( float x, float y )
        {
            float d1, d2, d3;
            bool has_neg, has_pos;

            d1 = Sign( x, y, topLeftX, topLeftY, topRightX, topRightY );
            d2 = Sign( x, y, topRightX, topRightY, bottomX, bottomY );
            d3 = Sign( x, y, bottomX, bottomY, topLeftX, topLeftY );

            has_neg = ( d1 < 0 ) || ( d2 < 0 ) || ( d3 < 0 );
            has_pos = ( d1 > 0 ) || ( d2 > 0 ) || ( d3 > 0 );

            return !( has_neg && has_pos );
        }

        protected void AddUnit( Unit unit )
        {
            FloatingObject floatUnit = new FloatingObject( unit, this );
            floatingObjects.Add( floatUnit );
            grabbedUnits.Add( unit, floatUnit );
            unit.Panic( 1000f, true );
            if ( unit is Mook )
            {
                ( unit as Mook ).SetInvulnerable( float.MaxValue );
            }
            Map.units.Remove( unit );
        }

        protected void AddObject( BroforceObject grabbedObject )
        {
            if ( grabbedObject is Projectile projectile )
            {
                Map.projectiles.Remove( projectile );
            }
            else if ( grabbedObject is Grenade grenade )
            {
                Map.grenades.Remove( grenade );
                grenade.GetFieldValue<ProjectileTrail>( "createdTrail" )?.EffectDie();
            }
            grabbedObject.enabled = false;
            FloatingObject floatObject = new FloatingObject( grabbedObject, this );
            floatingObjects.Add( floatObject );
            grabbedObjects.Add( grabbedObject, floatObject );
        }

        protected void FindObjects()
        {
            // Find nearby units
            for ( int i = 0; i < Map.units.Count; ++i )
            {
                Unit unit = Map.units[i];
                // Check that unit is not null, is not a player, is not dead, and is not already grabbed by this trap or another
                if ( unit != null && unit.gameObject.activeSelf && GameModeController.DoesPlayerNumDamage( thrownBy, unit.playerNum ) && unit.health > 0 && !grabbedUnits.ContainsKey( unit ) && !ignoredUnits.Contains( unit ) )
                {
                    // Ignore all vehicles and bosses
                    if ( unit.CompareTag( "Boss" ) || unit.CompareTag( "Metal" ) || unit is SatanMiniboss || unit is DolphLundrenSoldier || unit is Tank || unit is MookVehicleDigger )
                    {
                        ignoredUnits.Add( unit );
                        continue;
                    }
                    // Check unit is in rectangle around trap
                    if ( Tools.FastAbsWithinRange( unit.X - bottomX, 50f ) && Tools.FastAbsWithinRange( unit.Y - bottomY, 20f ) )
                    {
                        AddUnit( unit );
                    }
                    // Check unit is in trap triangle
                    else if ( Tools.FastAbsWithinRange( unit.X - bottomX, width ) )
                    {
                        float num = unit.Y - bottomY;
                        // Check that unit is within the possible vertical range of the trap, is above the trap, and is inside the trap triangle
                        if ( Tools.FastAbsWithinRange( num, height ) && ( Mathf.Sign( num ) == 1f ) && ShouldGrabUnit( unit.X, unit.Y ) )
                        {
                            AddUnit( unit );
                        }
                    }
                }
            }

            // Don't catch projectiles when closing
            if ( this.state < TrapState.Closing )
            {
                // Find nearby projectile
                for ( int i = 0; i < Map.projectiles.Count; ++i )
                {
                    Projectile projectile = Map.projectiles[i];
                    // Check that projectile is not null, damages the player, and is not already grabbed by this trap or another
                    if ( projectile != null && projectile.gameObject.activeSelf && GameModeController.DoesPlayerNumDamage( projectile.playerNum, thrownBy ) && !grabbedObjects.ContainsKey( projectile ) )
                    {
                        // Check projectile is in rectangle around trap
                        if ( Tools.FastAbsWithinRange( projectile.X - bottomX, 50f ) && Tools.FastAbsWithinRange( projectile.Y - bottomY, 20f ) )
                        {
                            AddObject( projectile );
                        }
                        // Check projectile is in trap triangle
                        else if ( Tools.FastAbsWithinRange( projectile.X - bottomX, width ) )
                        {
                            float num = projectile.Y - bottomY;
                            // Check that projectile is within the possible vertical range of the trap, is above the trap, and is inside the trap triangle
                            if ( Tools.FastAbsWithinRange( num, height ) && ( Mathf.Sign( num ) == 1f ) && ShouldGrabUnit( projectile.X, projectile.Y ) )
                            {
                                AddObject( projectile );
                            }
                        }
                    }
                }

                // Find nearby grenades
                for ( int i = 0; i < Map.grenades.Count; ++i )
                {
                    Grenade grenade = Map.grenades[i];
                    // Check that projectile is not null, damages the player, and is not already grabbed by this trap or another
                    if ( grenade != null && grenade.gameObject.activeSelf && GameModeController.DoesPlayerNumDamage( grenade.playerNum, thrownBy ) && !grabbedObjects.ContainsKey( grenade ) )
                    {
                        // Check projectile is in rectangle around trap
                        if ( Tools.FastAbsWithinRange( grenade.X - bottomX, 50f ) && Tools.FastAbsWithinRange( grenade.Y - bottomY, 20f ) )
                        {
                            AddObject( grenade );
                        }
                        // Check projectile is in trap triangle
                        else if ( Tools.FastAbsWithinRange( grenade.X - bottomX, width ) )
                        {
                            float num = grenade.Y - bottomY;
                            // Check that projectile is within the possible vertical range of the trap, is above the trap, and is inside the trap triangle
                            if ( Tools.FastAbsWithinRange( num, height ) && ( Mathf.Sign( num ) == 1f ) && ShouldGrabUnit( grenade.X, grenade.Y ) )
                            {
                                AddObject( grenade );
                            }
                        }
                    }
                }
            }
        }

        protected void MoveUnits()
        {
            if ( this.state == TrapState.Open )
            {
                // Iterate backwards since units may be destroyed during this loop
                for ( int i = floatingObjects.Count - 1; i >= 0; --i )
                {
                    floatingObjects[i].MoveObject( this.t );
                }
            }
            else
            {
                // Iterate backwards since units may be destroyed during this loop
                for ( int i = floatingObjects.Count - 1; i >= 0; --i )
                {
                    floatingObjects[i].MoveUnitToCenter( this.t );
                }
            }
        }

        protected void ReleaseUnits()
        {
            for ( int i = 0; i < floatingObjects.Count; ++i )
            {
                floatingObjects[i].ReleaseObject();
                if ( floatingObjects[i].trappedObject is Unit trappedUnit )
                {
                    grabbedUnits.Remove( trappedUnit );
                    Map.units.Add( trappedUnit );
                }
                else if ( floatingObjects[i].trappedObject is Projectile trappedProjectile )
                {
                    grabbedObjects.Remove( trappedProjectile );
                    Map.projectiles.Add( trappedProjectile );
                }
                else if ( floatingObjects[i].trappedObject is Grenade trappedGrenade )
                {
                    grabbedObjects.Remove( trappedGrenade );
                    Map.grenades.Add( trappedGrenade );
                }
            }
        }
    }
}
