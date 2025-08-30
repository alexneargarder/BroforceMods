using System;
using BroMakerLib.CustomObjects.Projectiles;
using BroMakerLib.Loggers;
using HarmonyLib;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class AlienEggProjectile : CustomProjectile
    {
        protected Shrapnel[] shrapnelPrefabs = new Shrapnel[3];
        protected int frame = 0;
        protected float counter = 0;
        protected float frameRate = 0.06f;
        protected int cycle = 0;
        protected bool exploded = false;
        protected float r = 90f;
        protected float rI = 0f;
        protected float rotationSpeedMultiplier = 1.5f;
        protected BloodColor bloodColor = BloodColor.Green;
        protected float explodeRange = 40f;
        public GibHolder openedEggGibHolder;
        protected AlienClimber faceHugger;
        protected float spawnFacehuggerTimer = 0.2f;

        protected override void Awake()
        {
            if ( this.sprite == null )
            {
                this.spriteLowerLeftPixel = new Vector2( 0, 32 );
                this.spritePixelDimensions = new Vector2( 32, 32 );
                this.spriteWidth = 22;
                this.spriteHeight = 22;
                this.spriteOffset = new Vector3( 0, 5, 0 );
            }

            this.projectileSize = 8f;

            this.damage = 5;
            this.damageInternal = this.damage;
            this.fullDamage = this.damage;

            base.Awake();
        }

        public override void PrefabSetup()
        {
            base.PrefabSetup();

            AlienEggBlock prefab = Map.Instance.activeTheme.blockAlienEgg as AlienEggBlock;
            this.openedEggGibHolder = prefab.openedEggGibHolder;
        }

        public override void Fire( float newX, float newY, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy )
        {
            this.rI = -Mathf.Sign( xI ) * ( 200f + UnityEngine.Random.value * 200f ) * this.rotationSpeedMultiplier;

            base.Fire( newX, newY, xI, yI, _zOffset, playerNum, FiredBy );

            this.faceHugger = Map.SpawnFaceHugger( newX, newY, 0f, 0f );
            this.faceHugger.RegisterUnit();
            Registry.RegisterDeterminsiticGameObject( this.faceHugger.gameObject );
            this.faceHugger.transform.parent = base.transform;
            this.faceHugger.ForceStart();
            this.faceHugger.gameObject.SetActive( false );
            this.faceHugger.playerNum = 5;
            this.faceHugger.firingPlayerNum = 5;
        }

        protected override void SetRotation()
        {
            // Don't rotate based on momentum
            if ( this.xI > 0f )
            {
                base.transform.localScale = new Vector3( -1f, 1f, 1f );
            }
            else
            {
                base.transform.localScale = new Vector3( 1f, 1f, 1f );
            }

            base.transform.eulerAngles = new Vector3( 0f, 0f, this.r );
        }

        protected override void Update()
        {
            this.ApplyGravity();

            // Change frames
            this.counter += this.t;
            if ( this.counter > this.frameRate )
            {
                this.counter -= this.frameRate;
                ++this.frame;
                this.ChangeFrame();
            }

            this.r += this.rI * this.t;

            this.SetRotation();

            base.Update();

            if ( this.spawnFacehuggerTimer > 0 )
            {
                this.spawnFacehuggerTimer -= this.t;
                if ( this.spawnFacehuggerTimer <= 0 )
                {
                    try
                    {
                        this.MakeEffects( false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point );
                    }
                    catch ( Exception ex )
                    {
                        BMLogger.ExceptionLog( ex );
                    }
                    this.Death();
                }
            }
        }

        protected virtual void ApplyGravity()
        {
            this.yI -= 500f * this.t;
        }

        protected void ChangeFrame()
        {
            if ( this.frame >= 5 )
            {
                this.frame = 0;
                ++this.cycle;
            }
            this.sprite.SetLowerLeftPixel_X( 32f * this.frame );
        }

        protected void LaunchFaceHugger()
        {
            this.faceHugger.SetXY( base.X, base.Y );
            this.faceHugger.transform.parent = null;
            this.faceHugger.gameObject.SetActive( true );
            this.faceHugger.transform.rotation = Quaternion.identity;
            this.faceHugger.playerNum = 5;
            this.faceHugger.firingPlayerNum = 5;
            Traverse trav = Traverse.Create( this.faceHugger );
            trav.Field( "usingSpecial" ).SetValue( true );
            trav.Method( "UseSpecial" ).GetValue();
            this.faceHugger.xI = this.xI;
            this.faceHugger.yI = this.yI + 50;
            EffectsController.CreateSlimeParticles( BloodColor.Green, base.X, base.Y, 45, 8f, 8f, 140f, this.faceHugger.xI * 0.5f, this.faceHugger.yI * 0.5f );
        }

        protected override void MakeEffects( bool particles, float x, float y, bool useRayCast, Vector3 hitNormal, Vector3 hitPoint )
        {
            if ( this.exploded )
            {
                return;
            }
            this.exploded = true;
            this.LaunchFaceHugger();
            EffectsController.CreateGibs( this.openedEggGibHolder, base.X, base.Y, 10f, 10f, 0f, 0f );
            Block.MakeAlienBlockCollapseEffects( base.X, base.Y, xI, yI, true );
        }

    }
}
