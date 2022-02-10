using System;
using System.Collections.Generic;
using UnityEngine;
using Captain_Ameribro_Mod;

public class CaptainAmeribro : BroBase
{
    protected override void UseSpecial()
    {
        if (this.SpecialAmmo > 0)
        {
            this.PlayThrowLightSound(0.4f);
            this.SpecialAmmo--;
            gunSprite.GetComponent<Renderer>().material.mainTexture = gunTextureNoShield;
            if (base.IsMine)
            {
                //Main.Log("spawning");
                //ProjectileController.SpawnProjectileOverNetwork(this.boomerang, this, base.X + base.transform.localScale.x * 6f, base.Y + 15f, base.transform.localScale.x * this.boomerangSpeed, 0f, false, base.playerNum, false, false, 0f);
                Shield newProj = ProjectileController.SpawnProjectileLocally(this.shield, this, base.X + base.transform.localScale.x * 6f, base.Y + 8, base.transform.localScale.x * this.boomerangSpeed, 0f, false, base.playerNum, false, false, 0f) as Shield;
                //newProj.Display();
                //Main.Log("arguments: " + (base.X + base.transform.localScale.x * 6f) + "   " + (base.Y + 8) + "   " + (base.transform.localScale.x * this.boomerangSpeed) + "   " + 0f);
                //shield.gameObject.SetActive(true);
                //shield.Fire(base.X + base.transform.localScale.x * 6f, base.Y + 8, base.transform.localScale.x * this.boomerangSpeed, 0f, 0f, base.playerNum, this);
                
                //shield.gameObject.SetActive
                BoxCollider tempAttachBoxCollider = BoxCollider.Instantiate(attachBoxCollider);
                //Transform tempShieldTransform = Transform.Instantiate(shieldTransform);
                Transform tempShieldTransform = newProj.transform;
                SoundHolder tempSoundHolder = SoundHolder.Instantiate(boomerangSoundHolder);

                newProj.Setup(tempAttachBoxCollider, tempShieldTransform, tempSoundHolder, rotationSpeed, true);
                newProj.activeProjectile = true;
                //Main.Log("spawned");
            }
        }
        else
        {
            HeroController.FlashSpecialAmmo(base.playerNum);
            this.ActivateGun();
        }
        this.pressSpecialFacingDirection = 0;
    }

    public void ReturnShield(Shield shield)
    {
        //Main.Log("return boomerang");
        this.SpecialAmmo++;
        if (!this.usingSpecial)
        {
            this.usingSpecial = true;
            this.grabbingFrame = 4;
            this.grabbingBoomerang = true;
            this.ChangeFrame();
        }
        gunSprite.GetComponent<Renderer>().material.mainTexture = gunTextureWithShield;
    }

    protected override void PressSpecial()
    {
        if (this.SpecialAmmo > 0)
        {
            this.grabbingBoomerang = false;
        }
        base.PressSpecial();
    }

    protected override void AnimateSpecial()
    {
        if (this.grabbingBoomerang)
        {
            this.grabbingFrame--;
            this.SetSpriteOffset(0f, 0f);
            this.DeactivateGun();
            this.frameRate = 0.045f;
            int num = 17 + Mathf.Clamp(this.grabbingFrame, 0, 7);
            this.sprite.SetLowerLeftPixel((float)(num * this.spritePixelWidth), (float)(this.spritePixelHeight * 5));
            if (this.grabbingFrame <= 0)
            {
                base.frame = 0;
                this.usingSpecial = false;
                this.grabbingBoomerang = false;
            }
        }
        else
        {
            base.AnimateSpecial();
        }
    }

    public void Setup(SpriteSM attachSprite, Player attachPlayer, int attachplayerNum, Shield attachShield,
        SoundHolder attachSoundHolder, float attachFireRate)
    {
        sprite = attachSprite;
        player = attachPlayer;
        playerNum = attachplayerNum;
        shield = attachShield;
        soundHolder = attachSoundHolder;
        this.SpecialAmmo = 1;
        this.originalSpecialAmmo = 1;
        this.fireRate = attachFireRate;
        this.health = 1;
        this.canChimneyFlip = true;
        this.doRollOnLand = true;
        this.useNewFrames = true;
    }


    // DEBUG

    /*protected override void Land()
    {
        Main.Log("do roll on land: " + this.doRollOnLand);
        Main.Log("use new frames: " + this.useNewFrames);
        base.Land();
    }*/

    /*protected override bool ConstrainToCeiling(ref float yIT)
    {
        //Main.Log("captain ameribro constrain to ceiling called");
        Main.Log("can chimney flip: " + this.canChimneyFlip);
        Main.Log("yI: " + this.yI + " walldrag: " + this.WallDrag);
        bool result = base.ConstrainToCeiling(ref yIT);
        //Main.Log("after constrain ameribro");
        return result;
    }

    protected override void AnimateChimneyFlip()
    {
        Main.Log("CHIMNEY FLIP CALLED IN AMERIBRO");
        base.AnimateChimneyFlip();
    }

    protected override void RunMovement()
    {
        //Main.Log("running movement in ameribro");
        base.RunMovement();
        //Main.Log("after movement ameribro");
    }*/
    public void Display()
    {
        /*Main.Log("canDash: " + this.canDash);
        Main.Log("dashing: " + this.dashing);
        Main.Log("dashSpeedM: " + this.dashSpeedM);
        Main.Log("wasDashing: " + this.wasDashing);
        Main.Log("dashbutton: " + this.dashButton);
        Main.Log("wasdashbutton: " + this.wasdashButton);*/
    }

    // Token: 0x06002772 RID: 10098 RVA: 0x00130F96 File Offset: 0x0012F396
    /*protected override void Awake()
    {
        Main.Log("awake");
        this.isHero = true;
        base.Awake();
    }*/

/*    protected override void Start()
    {
        Main.Log("started");
        base.Start();
        Main.Log("after start");
    }*/

    protected override void Update()
    {
        //Main.Log("spritesm: " + sprite.name + " null? " + (sprite == null));
        /*if (this.fire)
        {
            //Main.Log("fire");
        }*/
        if (this.player == null)
        {
            Main.Log("player null");
        }
        if (this.gameObject == null)
        {
            Main.Log("game object null");
        }
        if (this.sprite == null)
        {
            Main.Log("sprite null");
        }
        if (this.gunSprite == null)
        {
            Main.Log("gun sprite null");
        }
        if (this.playerNum != 0)
        {
            Main.Log("wrong player num: " + playerNum);
        }
        if (this.transform == null)
        {
            Main.Log("transform null");
        }
        if (this.shield == null)
        {
            Main.Log("boomerang null");
        }
        if (this.sound ==  null)
        {
            Main.Log("sound null");
        }
        if (this.soundHolder == null)
        {
            Main.Log("sound null");
        }
        if (!base.IsMine)
        {
            Main.Log("base not mine");
        }
        if (this.shield.gameObject == null)
        {
            Main.Log("projectile gameobject null");
        }
        if (this.gunSprite == null)
        {
            Main.Log("gun sprite null");
        }

        if (this.ducking && this.SpecialAmmo > 0)
        {
            Map.DeflectProjectiles(this, base.playerNum, 2f, base.X + Mathf.Sign(base.transform.localScale.x) * 6f, base.Y + 6f, Mathf.Sign(base.transform.localScale.x) * 200f, true);

        }

        /*if (this.sprint || this.wasSprint)
        {
            Main.Log("this.sprint: " + sprint + " was sprint " + wasSprint);
            Main.Log("can dash");
        }*/


        //UseFire();
        base.Update();
    }

    /*protected override void StartDashing()
    {

        //Main.Log("before");

        base.StartDashing();

        //Main.Log("AFTER dashing: " + base.dashing);
        //Main.Log("AFTER delayed dashing: " + base.delayedDashing);
    }*/

    protected override void SetGunSprite(int spriteFrame, int spriteRow)
    {
        if (base.actionState == ActionState.ClimbingLadder && this.hangingOneArmed)
        {
            this.gunSprite.SetLowerLeftPixel((float)(this.gunSpritePixelWidth * (11 + spriteFrame)), (float)(this.gunSpritePixelHeight * (1 + spriteRow)));
        }
        else if (this.attachedToZipline != null && base.actionState == ActionState.Jumping)
        {
            this.gunSprite.SetLowerLeftPixel((float)(this.gunSpritePixelWidth * 11), (float)(this.gunSpritePixelHeight * 2));
        }
        else
        {
            base.SetGunSprite(spriteFrame, spriteRow);
        }
    }

    protected override void RunGun()
    {
        /*if (this.specialAttackDashTime > 0f)
        {
            this.gunFrame = 11;
            this.SetGunFrame();
        }*/
        if (!this.WallDrag)
        {
            if (this.gunFrame > 0)
            {
                if (!this.hasBeenCoverInAcid)
                {
                    base.GetComponent<Renderer>().material = this.materialArmless;
                }
                this.gunCounter += this.t;
                if (this.gunCounter > 0.0334f)
                {
                    this.gunCounter -= 0.0334f;
                    this.gunFrame++;
                    if (this.gunFrame >= 6)
                    {
                        this.gunFrame = 0;
                    }
                    this.SetGunFrame();
                    if (this.gunFrame == 2)
                    {
                        if (this.hasHitWithSlice)
                        {
                            PlaySliceSound();
                        }
                        else if (this.hasHitWithWall)
                        {
                            PlayWallSound();
                        }
                    }
                }
            }
           /* else if (this.currentZone != null && this.currentZone.PoolIndex != -1)
            {
                this.gunSprite.SetLowerLeftPixel(0f, 128f);
            }*/
        }
        if ((!this.gunSprite.gameObject.activeSelf || this.gunFrame == 0) && !this.hasBeenCoverInAcid)
        {
            base.GetComponent<Renderer>().material = this.materialNormal;
        }
    }
    public void PlaySliceSound()
    {
        if (this.sound == null)
        {
            this.sound = Sound.GetInstance();
        }
        if (this.sound != null)
        {
            this.sound.PlaySoundEffectAt(this.soundHolder.special2Sounds, 0.7f, base.transform.position, 1f, true, false, false, 0f);
        }
    }

    public void PlayWallSound()
    {
        if (this.sound == null)
        {
            this.sound = Sound.GetInstance();
        }
        if (this.sound != null)
        {
            this.sound.PlaySoundEffectAt(this.soundHolder.defendSounds, 0.6f, base.transform.position, 1f, true, false, false, 0f);
        }
    }

    protected override void UseFire()
    {
        //Main.Log("usefire ameribro");
        this.alreadyHit.Clear();
        this.hasHitWithWall = false;
        this.hasHitWithSlice = false;
        this.gunFrame = 6;
        this.hasPlayedAttackHitSound = false;
        this.FireWeapon(base.X + base.transform.localScale.x * 10f, base.Y + 6.5f, base.transform.localScale.x * (float)(250 + ((!Demonstration.bulletsAreFast) ? 0 : 150)), (float)(UnityEngine.Random.Range(0, 40) - 20) * ((!Demonstration.bulletsAreFast) ? 0.2f : 1f));
        this.PlayAttackSound();
        Map.DisturbWildLife(base.X, base.Y, 60f, base.playerNum);
        fireDelay = 0.1f;
    }

    protected override void FireWeapon(float x, float y, float xSpeed, float ySpeed)
    {
        if (this.attachedToZipline != null)
        {
            this.attachedToZipline.DetachUnit(this);
            if (base.transform.localScale.x > 0f)
            {
                this.AirDashRight();
            }
            else
            {
                this.AirDashLeft();
            }
            return;
        }
        Map.HurtWildLife(x + base.transform.localScale.x * 13f, y + 5f, 12f);
        this.gunFrame = 1;
        this.punchingIndex++;
        this.gunCounter = 0f;
        this.SetGunFrame();
        float num = base.transform.localScale.x * 12f;
        this.ConstrainToFragileBarriers(ref num, 16f);
        if (Physics.Raycast(new Vector3(x - Mathf.Sign(base.transform.localScale.x) * 12f, y + 5.5f, 0f), new Vector3(base.transform.localScale.x, 0f, 0f), out this.raycastHit, 19f, this.groundLayer) || Physics.Raycast(new Vector3(x - Mathf.Sign(base.transform.localScale.x) * 12f, y + 10.5f, 0f), new Vector3(base.transform.localScale.x, 0f, 0f), out this.raycastHit, 19f, this.groundLayer))
        {
            this.MakeEffects(this.raycastHit.point.x + base.transform.localScale.x * 4f, this.raycastHit.point.y);
            MapController.Damage_Local(this, this.raycastHit.collider.gameObject, 9, DamageType.Bullet, this.xI + base.transform.localScale.x * 200f, 0f, x, y);
            this.hasHitWithWall = true;
            if (Map.HitUnits(this, base.playerNum, 5, DamageType.Melee, 6f, x, y, base.transform.localScale.x * 520f, 460f, false, true, false, this.alreadyHit, false, false))
            {
                this.hasHitWithSlice = true;
            }
            else
            {
                this.hasHitWithSlice = false;
            }
            Map.DisturbWildLife(x, y, 80f, base.playerNum);
        }
        else
        {
            this.hasHitWithWall = false;
            if (Map.HitUnits(this, this, base.playerNum, 5, DamageType.Melee, 12f, 9f, x, y, base.transform.localScale.x * 520f, 460f, false, true, false, true))
            {
                this.hasHitWithSlice = true;
            }
            else
            {
                this.hasHitWithSlice = false;
            }
        }
    }

    protected void SetGunFrame()
    {
        if (!this.ducking)
        {
            int num = this.punchingIndex % 2;
            if (num != 0)
            {
                if (num == 1)
                {
                    this.gunSprite.SetLowerLeftPixel((float)(32 * (5 + this.gunFrame)), 32f);
                }
            }
            else
            {
                this.gunSprite.SetLowerLeftPixel((float)(32 * this.gunFrame), 32f);
            }
        }
        else
        {
            int num2 = this.punchingIndex % 2;
            if (num2 != 0)
            {
                if (num2 == 1)
                {
                    this.gunSprite.SetLowerLeftPixel((float)(32 * (15 + this.gunFrame)), 32f);
                }
            }
            else
            {
                this.gunSprite.SetLowerLeftPixel((float)(32 * (10 + this.gunFrame)), 32f);
            }
        }
    }

    protected override void SetGunPosition(float xOffset, float yOffset)
    {
        this.gunSprite.transform.localPosition = new Vector3(xOffset + 4f, yOffset, -1f);
    }

    protected void MakeEffects(float x, float y)
    {
        EffectsController.CreateWhiteFlashPopSmall(x, y);
    }

    public BoxCollider attachBoxCollider;
    public SoundHolder boomerangSoundHolder;
    public float rotationSpeed;
    public Transform shieldTransform;

    public Texture2D gunTextureWithShield;
    public Texture2D gunTextureNoShield;

    protected bool hasPlayedAttackHitSound;

    protected List<Unit> alreadyHit = new List<Unit>();

    protected int punchingIndex;

    protected bool hasHitWithSlice;

    protected bool hasHitWithWall;

    //public Projectile[] flameProjectiles;

    public Material materialArmless;

    public Material materialNormal;

    public Shield shield;

    public float boomerangSpeed = 300f;

    protected bool grabbingBoomerang;

    protected int grabbingFrame;
}
