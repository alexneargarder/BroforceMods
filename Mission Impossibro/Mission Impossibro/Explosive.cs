using UnityEngine;
using BroMakerLib.CustomObjects.Projectiles;

namespace Mission_Impossibro
{
    public class Explosive : CustomSachelPack
    {
        protected bool stickToDeadUnit;
        protected float previousSpeed;

        protected override void Awake()
        {
            if ( defaultSoundHolder == null )
            {
                this.defaultSoundHolder = HeroController.GetHeroPrefab( HeroType.McBrover ).projectile.soundHolder;
            }

            base.Awake();

            // Setup Variables
            this.life = 100f;
            this.range = 50f;
            this.blastForce = 50f;
            this.sticky = true;
            this.stickyToUnits = true;
            this.damage = 8;
            this.damageInternal = 8;
            this.fullDamage = 8;
            this.damageType = DamageType.Fire;
            this.canHitGrenades = false;
            this.soundVolume = 0.4f;
            this.bounceXM = 0.1f;
            this.bounceYM = 0.1f;
        }

        public override void PrefabSetup()
        {
            SachelPack other = ( HeroController.GetHeroPrefab( HeroType.McBrover ) as McBrover ).projectile as SachelPack;
            this.fire1 = other.fire1;
            this.fire2 = other.fire2;
            this.fire3 = other.fire3;
            this.smoke1 = other.smoke1;
            this.smoke2 = other.smoke2;
            this.explosion = other.explosion;
            this.explosionSmall = other.explosionSmall;
            this.shrapnel = other.shrapnel;
            this.shrapnelSpark = other.shrapnelSpark;
            this.flickPuff = other.flickPuff;
        }

        protected override void Update()
        {
            base.Update();
            if ( this.xI != 0 )
            {
                this.previousSpeed = this.xI;
            }
        }

        public override void TryStickToUnit( Unit unit, bool _stickToDeadUnit = false )
        {
            this.stickToDeadUnit = _stickToDeadUnit;
            if ( unit != null )
            {
                this.stuckToUnit = unit;
                this.PlayStuckSound( 0.7f );
                Vector3 arg = this.stuckToUnit.transform.InverseTransformPoint( base.transform.position );
                this.StickToUnit( this.stuckToUnit, arg );
            }
        }

        protected override bool HitWalls()
        {
            int layerMask = this.groundLayer | 1 << LayerMask.NameToLayer( "DirtyHippie" );
            if ( this.stuckToUnit == null && ( this.stuckUp || this.stuckLeft || this.stuckRight ) )
            {
                if ( this.stuckUp )
                {
                    if ( Physics.Raycast( new Vector3( base.X, base.Y - 6f, 0f ), Vector3.up, out this.raycastHit, 14f + this.heightOffGround, layerMask ) )
                    {
                        base.Y = this.raycastHit.point.y - this.heightOffGround;
                        this.xI = ( this.yI = 0f );
                    }
                    else
                    {
                        this.stuckUp = false;
                    }
                }
                if ( this.stuckLeft )
                {
                    if ( Physics.Raycast( new Vector3( base.X + 6f, base.Y, 0f ), Vector3.left, out this.raycastHit, 10f + this.heightOffGround, layerMask ) )
                    {
                        base.X = this.raycastHit.point.x + this.heightOffGround;
                        this.xI = ( this.yI = 0f );
                    }
                    else
                    {
                        this.stuckLeft = false;
                    }
                }
                if ( this.stuckRight )
                {
                    if ( Physics.Raycast( new Vector3( base.X - 6f, base.Y, 0f ), Vector3.right, out this.raycastHit, 10f + this.heightOffGround, layerMask ) )
                    {
                        base.X = this.raycastHit.point.x - this.heightOffGround;
                        this.xI = ( this.yI = 0f );
                    }
                    else
                    {
                        this.stuckRight = false;
                    }
                }
            }
            else
            {
                if ( this.xI < 0f )
                {
                    if ( Physics.Raycast( new Vector3( base.X + 4f, base.Y, 0f ), Vector3.left, out this.raycastHit, 6f + this.heightOffGround, layerMask ) )
                    {
                        if ( this.raycastHit.collider.GetComponent<SawBlade>() != null )
                        {
                            this.Death();
                        }
                        if ( this.raycastHit.collider.GetComponent<Unit>() != null )
                        {
                            this.TryStickToUnit( this.raycastHit.collider.GetComponent<Unit>() );
                            this.xI = ( this.yI = 0f );
                            this.PlayStuckSound( 0.7f );
                        }
                        else if ( this.raycastHit.collider.GetComponent<DamageRelay>() != null )
                        {
                            this.stuckLeft = true;
                            this.xI = ( this.yI = 0f );
                            this.PlayStuckSound( 0.7f );
                        }
                        else if ( this.raycastHit.collider.GetComponent<BossBlockPiece>() != null || this.raycastHit.collider.GetComponent<BossBlockWeapon>() != null )
                        {
                            this.stuckLeft = true;
                            this.xI = ( this.yI = 0f );
                            this.PlayStuckSound( 0.7f );
                        }
                        else if ( this.raycastHit.collider.GetComponent<Block>() != null )
                        {
                            this.stuckLeft = true;
                            this.xI = ( this.yI = 0f );
                            this.PlayStuckSound( 0.7f );
                        }
                        else
                        {
                            this.xI *= -this.bounceXM;
                            this.MakeBounceEffect( DirectionEnum.Left, this.raycastHit.point );
                            this.PlayBounceSound( this.xI );
                        }
                        this.stickyToUnits = false;
                    }
                }
                else if ( this.xI > 0f && Physics.Raycast( new Vector3( base.X - 4f, base.Y, 0f ), Vector3.right, out this.raycastHit, 4f + this.heightOffGround, layerMask ) )
                {
                    if ( this.raycastHit.collider.GetComponent<SawBlade>() != null )
                    {
                        this.Death();
                    }
                    if ( this.raycastHit.collider.GetComponent<Unit>() != null )
                    {
                        this.TryStickToUnit( this.raycastHit.collider.GetComponent<Unit>() );
                        this.xI = ( this.yI = 0f );
                        this.PlayStuckSound( 0.7f );
                    }
                    else if ( this.raycastHit.collider.GetComponent<DamageRelay>() != null )
                    {
                        this.stuckRight = true;
                        this.xI = ( this.yI = 0f );
                        this.PlayStuckSound( 0.7f );
                    }
                    else if ( this.raycastHit.collider.GetComponent<BossBlockPiece>() != null || this.raycastHit.collider.GetComponent<BossBlockWeapon>() != null )
                    {
                        this.stuckRight = true;
                        this.xI = ( this.yI = 0f );
                        this.PlayStuckSound( 0.7f );
                    }
                    else if ( this.raycastHit.collider.GetComponent<Block>() != null )
                    {
                        this.stuckRight = true;
                        this.xI = ( this.yI = 0f );
                        this.PlayStuckSound( 0.7f );
                    }
                    else
                    {
                        this.stickyToUnits = false;
                        this.xI *= -this.bounceXM;
                        this.MakeBounceEffect( DirectionEnum.Right, this.raycastHit.point );
                        this.PlayBounceSound( this.xI );
                    }
                }
                if ( this.yI < 0f )
                {
                    if ( Physics.Raycast( new Vector3( base.X, base.Y + 6f, 0f ), Vector3.down, out this.raycastHit, 6f + this.heightOffGround - this.yI * this.t, layerMask ) )
                    {
                        if ( this.raycastHit.collider.GetComponent<SawBlade>() != null )
                        {
                            this.Death();
                        }
                        this.stickyToUnits = false;
                        this.xI *= this.frictionM;
                        if ( this.yI < -40f )
                        {
                            this.yI *= -this.bounceYM;
                            this.MakeBounceEffect( DirectionEnum.Down, this.raycastHit.point );
                        }
                        else
                        {
                            this.yI = 0f;
                            base.Y = this.raycastHit.point.y + this.heightOffGround;
                        }
                        this.PlayBounceSound( this.yI );
                    }
                }
                else if ( this.yI > 0f && Physics.Raycast( new Vector3( base.X, base.Y - 6f, 0f ), Vector3.up, out this.raycastHit, 6f + this.heightOffGround + this.yI * this.t, layerMask ) )
                {
                    if ( this.raycastHit.collider.GetComponent<SawBlade>() != null )
                    {
                        this.Death();
                    }
                    if ( this.raycastHit.collider.GetComponent<Unit>() != null )
                    {
                        this.TryStickToUnit( this.raycastHit.collider.GetComponent<Unit>() );
                        this.xI = ( this.yI = 0f );
                        this.PlayStuckSound( 0.7f );
                    }
                    else if ( this.raycastHit.collider.GetComponent<DamageRelay>() != null )
                    {
                        this.stuckUp = true;
                        this.xI = ( this.yI = 0f );
                        this.PlayStuckSound( 0.7f );
                    }
                    else if ( this.raycastHit.collider.GetComponent<BossBlockPiece>() != null || this.raycastHit.collider.GetComponent<BossBlockWeapon>() != null )
                    {
                        this.stuckUp = true;
                        this.xI = ( this.yI = 0f );
                        this.PlayStuckSound( 0.7f );
                    }
                    else if ( this.raycastHit.collider.GetComponent<Block>() != null )
                    {
                        this.stuckUp = true;
                        this.xI = ( this.yI = 0f );
                        this.PlayStuckSound( 0.7f );
                    }
                    else
                    {
                        this.stickyToUnits = false;
                        this.yI *= -( this.bounceYM + 0.1f );
                        this.MakeBounceEffect( DirectionEnum.Up, this.raycastHit.point );
                        this.PlayBounceSound( this.yI );
                    }
                }
            }
            if ( this.stuckToUnit != null )
            {
                Vector3 vector = this.stuckToUnit.transform.TransformPoint( this.stuckTolocalPos );
                this.SetXY( vector.x, vector.y );
                this.xI = this.stuckToUnit.xI;
                this.yI = this.stuckToUnit.yI;
                if ( this.stuckToUnit.health <= 0 && !this.stickToDeadUnit && Mathf.Abs( this.stuckToUnit.xI ) + Mathf.Abs( this.stuckToUnit.yI ) < 100f )
                {
                    this.DetachFromUnit();
                }
                return false;
            }
            return true;
        }

        protected override void SetRotation()
        {
            if ( this.stuckToUnit != null )
            {
                base.transform.localScale = new Vector3( 1f, 1f, 1f );
                base.transform.eulerAngles = new Vector3( 0f, 0f, 0f );
            }
            else if ( this.xI > 0f )
            {
                base.transform.localScale = new Vector3( -1f, 1f, 1f );
                base.transform.eulerAngles = new Vector3( 0f, 0f, global::Math.GetAngle( this.yI, -this.xI ) * 180f / 3.14159274f );
            }
            else if ( this.xI < 0f || this.stuckLeft || this.stuckRight )
            {
                base.transform.localScale = new Vector3( 1f, 1f, 1f );
                base.transform.eulerAngles = new Vector3( 0f, 0f, global::Math.GetAngle( this.yI, -this.xI ) * 180f / 3.14159274f );
            }
            else if ( previousSpeed > 0 )
            {
                base.transform.localScale = new Vector3( -1f, 1f, 1f );
                base.transform.eulerAngles = new Vector3( 0f, 0f, -( Mathf.PI / 2 ) * 180f / 3.14159274f );
            }
            else
            {
                base.transform.localScale = new Vector3( 1f, 1f, 1f );
                base.transform.eulerAngles = new Vector3( 0f, 0f, ( Mathf.PI / 2 ) * 180f / 3.14159274f );
            }
        }
    }
}