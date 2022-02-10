using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Networking;
using UnityEngine;
using Captain_Ameribro_Mod;

// Token: 0x02000504 RID: 1284
public class Shield : Projectile
{
    public void Setup(BoxCollider attachBoomerangCollider, Transform attachTransform, SoundHolder attachSoundHolder, float attachRotationSpeed, bool startRunning)
    {
        this.boomerangCollider = attachBoomerangCollider;
        base.transform.parent = attachTransform;
        this.soundHolder = attachSoundHolder;
        //this.rotationSpeed = attachRotationSpeed;
        this.returnTime = 2f;
        this.rotationSpeed = 0f;
        this.damage = 3;
        this.texture = this.gameObject.GetComponent<AnimatedTexture>();

        base.transform.localScale = new Vector3(1f, 1f, 1f);
        
        base.transform.eulerAngles = new Vector3(0f, 0f, 0f);

        this.enabled = startRunning;

        //texture.paused = true;
        //Main.Log("request frame: " + Main.requestedFrame);
        this.texture.frames = 1;
        //this.texture.SetFrame(Main.requestedFrame);

        if (this.gameObject == null)
        {
            Main.Log("gameobject null");
        }
        if (this.boomerangCollider == null)
        {
            Main.Log("collider null");
        }

    }

    public void Display()
    {
        Main.Log("\n\n");
        Main.Log("damage: " + base.damage);
        Main.Log("x: " + this.X + "  y: " + this.Y + "  xI: " + this.xI + "  yI: " + this.yI);
        //Main.Log("hold at apex time: " + holdAtApexTime + "    hold at apex duration: " + holdAtApexDuration);
        Main.Log("return time: " + returnTime);
        Main.Log(base.transform.localScale.ToString());
        Main.Log(base.transform.eulerAngles.ToString());
    }

    protected override void SetRotation()
    {
        return;
    }

    // Token: 0x0600351B RID: 13595 RVA: 0x001989D8 File Offset: 0x00196DD8
    public override void Fire(float x, float y, float xI, float yI, float _zOffset, int playerNum, MonoBehaviour FiredBy)
    {
        //Main.Log("fire called on shield");
        //xI = 10;
        /*if (true)
        {
            //this.xI = Main.requestedXI;
            xI = Main.requestedXI;
            Main.changeXI = false;
        }*/
        //Main.Log("called fire");
        this.boomerangSpeed = xI;
        if (xI > 0f)
        {
            this.rotationSpeed *= -1f;
        }
        //Main.Log("after rotation speed");
        /*if (boomerangCollider.transform == null)
        {
            Main.Log("null transform");
        }
        if (base.transform.parent == null)
        {
            Main.Log("null parent");
        }*/
        //Main.Log("after ifs");
        this.boomerangCollider.transform.parent = base.transform.parent;
        //Main.Log("after transform parent");
        base.Fire(x, y, xI, yI, _zOffset, playerNum, FiredBy);
        //Main.Log("after base.fire");
        base.gameObject.AddComponent<AudioSource>();
        //Main.Log("after add component");
        base.GetComponent<AudioSource>().playOnAwake = false;
        base.GetComponent<AudioSource>().rolloffMode = AudioRolloffMode.Linear;
        base.GetComponent<AudioSource>().minDistance = 100f;
        base.GetComponent<AudioSource>().dopplerLevel = 0.02f;
        base.GetComponent<AudioSource>().maxDistance = 220f;
        base.GetComponent<AudioSource>().spatialBlend = 1f;
        base.GetComponent<AudioSource>().volume = this.soundVolume;
        base.GetComponent<AudioSource>().loop = true;
        base.GetComponent<AudioSource>().clip = this.soundHolder.specialSounds[UnityEngine.Random.Range(0, this.soundHolder.specialSounds.Length)];
        base.GetComponent<AudioSource>().Play();
        //Main.Log("after play audio source");
        Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.effortSounds, 0.4f, base.transform.position, 1.15f + UnityEngine.Random.value * 0.1f, true, false, false, 0f);
        this.xStart = x - Mathf.Sign(xI) * 48f;
        this.lastXI = xI;
        //Main.Log("exited fire");
    }

    protected override void CheckSpawnPoint()
    {
        Collider[] array = Physics.OverlapSphere(new Vector3(base.X, base.Y, 0f), 5f, this.groundLayer);
        if (array.Length > 0)
        {
            this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
            for (int i = 0; i < array.Length; i++)
            {
                this.ProjectileApplyDamageToBlock(array[i].gameObject, this.damageInternal, this.damageType, this.xI, this.yI);
            }
            this.returnTime = 0f;
            this.xI = 0f;
        }
        this.RegisterProjectile();
        this.CheckReturnZones();
        if ((this.canReflect && this.playerNum >= 0 && this.horizontalProjectile && Physics.Raycast(new Vector3(base.X - Mathf.Sign(this.xI) * this.projectileSize * 2f, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize * 3f, this.barrierLayer)) || (!this.horizontalProjectile && Physics.Raycast(new Vector3(base.X, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize + this.startProjectileSpeed * this.t, this.barrierLayer)))
        {
            this.ReflectProjectile(this.raycastHit);
        }
        else if ((this.canReflect && this.playerNum < 0 && this.horizontalProjectile && Physics.Raycast(new Vector3(base.X - Mathf.Sign(this.xI) * this.projectileSize * 2f, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize * 3f, this.friendlyBarrierLayer)) || (!this.horizontalProjectile && Physics.Raycast(new Vector3(base.X, base.Y, 0f), new Vector3(this.xI, this.yI, 0f), out this.raycastHit, this.projectileSize + this.startProjectileSpeed * this.t, this.friendlyBarrierLayer)))
        {
            this.ReflectProjectile(this.raycastHit);
        }
        this.CheckSpawnPointFragile();
    }

    protected override void RunProjectile(float t)
    {
        //Main.Log("rotation speed:    " + this.rotationSpeed);
        //Main.Log("called run");
        base.RunProjectile(t);
        this.returnTime -= t;
        this.collectDelayTime -= t;
        this.ricochetCooldown -= t;

        if (this.holdAtApexTime > 0f)
        {
            this.holdAtApexTime -= t;
            this.CheckReturnShield();
            if (this.holdAtApexTime <= 0f)
            {
                //Main.Log("first applying reverse force");
                this.ApplyReverseForce(t);
            }
        }
        else if (this.returnTime <= 0f)
        {
            if (!this.dropping)
            {
                if (forward)
                {
                    this.ApplyForwardForce(t);
                }
                else
                {
                    this.ApplyReverseForce(t);
                    /*if (Mathf.Sign(this.boomerangSpeed) == Mathf.Sign(this.xI))
                    {
                        //Main.Log("second applying reverse force");
                        this.ApplyReverseForce(t);
                        if (!this.hasReachedApex && Mathf.Sign(this.boomerangSpeed) != Mathf.Sign(this.xI))
                        {
                            this.hasReachedApex = true;
                            this.holdAtApexTime = this.holdAtApexDuration;
                            this.xI = 0f;
                        }
                    }
                    else
                    {
                        //Main.Log("third applying reverse force");
                        this.ApplyReverseForce(t);
                    }*/
                }
            }
            this.CheckReturnShield();
            if (!this.dropping)
            {
                /*float f = this.xStart - base.X;
                if (Mathf.Sign(this.boomerangSpeed) == Mathf.Sign(f))
                {
                    this.dropping = true;
                    this.collectDelayTime = 0f;
                    base.GetComponent<AudioSource>().Stop();
                    this.xI *= 0.66f;
                    this.boomerangCollider.enabled = false;
                }*/
            }
        }
        if (!this.dropping)
        {
            float num = 140f + Mathf.Abs(this.xI) * 0.5f;
            if (this.boomerangSpeed != 0f && Time.timeScale > 0f)
            {
                float pitch = Mathf.Clamp(num / Mathf.Abs(this.boomerangSpeed) * 1.2f * this.boomerangLoopPitchM, 0.5f * this.boomerangLoopPitchM, 1f * this.boomerangLoopPitchM) * Time.timeScale;
                base.GetComponent<AudioSource>().pitch = pitch;
            }
            base.transform.Rotate(0f, 0f, num * this.rotationSpeed * t, Space.Self);
            this.windCounter += t;
            if (this.windCounter > 0.0667f)
            {
                this.windCount++;
                this.windCounter -= 0.0667f;
                EffectsController.CreateBoomerangWindEffect(base.X, base.Y, 5f, 0f, 0f, base.transform, 0f, (float)(this.windCount * 27) * this.rotationSpeed);
            }
        }
        else
        {
            base.transform.Rotate(0f, 0f, this.rotationSpeed * t, Space.Self);
        }
        if (Mathf.Sign(this.lastXI) != Mathf.Sign(this.xI) || this.holdAtApexTime > 0f)
        {
            this.alreadyHit.Clear();
        }
        this.lastXI = this.xI;
        //Main.Log("AFTER run");
    }

    private void ApplyReverseForce(float t)
    {
        //Main.Log("applying return force to shield");
        this.xI -= this.boomerangSpeed * t * this.returnLerpSpeed;
        this.xI = Mathf.Clamp(this.xI, -Mathf.Abs(this.boomerangSpeed), Mathf.Abs(this.boomerangSpeed));
    }

    private void ApplyForwardForce(float t)
    {
        //Main.Log("applying return force to shield");
        this.xI += this.boomerangSpeed * t * this.returnLerpSpeed;
        this.xI = Mathf.Clamp(this.xI, -Mathf.Abs(this.boomerangSpeed), Mathf.Abs(this.boomerangSpeed));
    }

    // Token: 0x0600351F RID: 13599 RVA: 0x0019916C File Offset: 0x0019756C
    protected override void HitProjectiles()
    {
        if (Map.HitProjectiles(this.playerNum, this.damageInternal, this.damageType, this.projectileSize, base.X, base.Y, this.xI, this.yI, 0.1f))
        {
            this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
        }
    }

    // Token: 0x06003520 RID: 13600 RVA: 0x001991E2 File Offset: 0x001975E2
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (this.boomerangCollider != null && this.boomerangCollider.gameObject != null)
        {
            UnityEngine.Object.Destroy(this.boomerangCollider.gameObject);
        }
    }

    // Token: 0x06003521 RID: 13601 RVA: 0x00199221 File Offset: 0x00197621
    protected override void MoveProjectile()
    {
        //Main.Log("called move");
        if (!this.stuck)
        {
            //Main.Log("not stuck");
            //this.texture = this.gameObject.GetComponent<AnimatedTexture>();
            

            if (Main.requestDisplay && activeProjectile)
            {
                Display();
                Main.requestDisplay = false;
            }

            /*if (!Main.settings.animateShield)
            {
                texture.paused = true;
                this.texture.SetFrame(Main.requestedFrame);
            }
            else
            {
                texture.paused = false;
            }*/
            /*if (!Main.settings.moveShield)
            {
                return;
            }*/

            //Main.Log("before move");
            base.MoveProjectile();
            //Main.Log("after move");
            this.boomerangCollider.transform.position = base.transform.position;
            //Main.Log("after collider thing");
            /*if (this.dropping)
            {
                this.ApplyGravity();
            }*/
        }
    }

    // Token: 0x06003522 RID: 13602 RVA: 0x00199260 File Offset: 0x00197660
    protected override bool HitWalls()
    {
        //Main.Log("hit walls");
        if (this.xI < 0f)
        {
            if (Physics.Raycast(new Vector3(base.X + 4f, base.Y + 4f, 0f), Vector3.left, out this.raycastHit, 6f + this.heightOffGround, this.groundLayer) || Physics.Raycast(new Vector3(base.X + 4f, base.Y - 4f, 0f), Vector3.left, out this.raycastHit, 6f + this.heightOffGround, this.groundLayer))
            {
                this.collectDelayTime = 0f;
                if (Mathf.Abs(this.xI) > Mathf.Abs(this.boomerangSpeed) * 0.33f)
                {
                    EffectsController.CreateSuddenSparkShower(this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f, this.sparkCount / 2, 2f, 40f + UnityEngine.Random.value * 80f, this.raycastHit.normal.x * (100f + UnityEngine.Random.value * 210f), this.raycastHit.normal.y * 120f + (-60f + UnityEngine.Random.value * 350f), 0.2f);
                }
                this.xI *= -this.bounceXM;
                if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null)
                {
                    this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(1, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));
                    if (this.returnTime > 0f)
                    {
                        Main.Log("return time set to 0");
                        this.returnTime = 0f;
                    }
                    if (this.ricochetCooldown < 0)
                    {
                        Main.Log("ricochet first");
                        this.forward = !this.forward;
                        this.ricochetCooldown = 0.2f;
                    }
                    /*else if (!this.dropping && this.boomerangSpeed > 0f)
                    {
                        this.StartDropping();
                        this.yI += 80f;
                    }*/
                }
                /*else if (!this.hasReachedApex)
                {
                    this.xI = 0f;
                    this.hasReachedApex = true;
                    this.holdAtApexTime = this.holdAtApexDuration;
                }*/
                this.PlayBounceSound();
            }
        }
        else if (this.xI > 0f && (Physics.Raycast(new Vector3(base.X - 4f, base.Y + 4f, 0f), Vector3.right, out this.raycastHit, 4f + this.heightOffGround, this.groundLayer) || Physics.Raycast(new Vector3(base.X - 4f, base.Y - 4f, 0f), Vector3.right, out this.raycastHit, 4f + this.heightOffGround, this.groundLayer)))
        {
            this.collectDelayTime = 0f;
            if (Mathf.Abs(this.xI) > Mathf.Abs(this.boomerangSpeed) * 0.33f)
            {
                EffectsController.CreateSuddenSparkShower(this.raycastHit.point.x + this.raycastHit.normal.x * 3f, this.raycastHit.point.y + this.raycastHit.normal.y * 3f, this.sparkCount / 2, 2f, 40f + UnityEngine.Random.value * 80f, this.raycastHit.normal.x * (100f + UnityEngine.Random.value * 210f), this.raycastHit.normal.y * 120f + (-60f + UnityEngine.Random.value * 350f), 0.2f);
            }
            this.xI *= -this.bounceXM;
            if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null)
            {
                this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(1, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));
                if (this.returnTime > 0f)
                {
                    Main.Log("return time set to 0");
                    this.returnTime = 0f;
                }
                if (this.ricochetCooldown < 0)
                {
                    Main.Log("ricochet second");
                    this.forward = !this.forward;
                    this.ricochetCooldown = 0.2f;
                }
                /*else if (!this.dropping && this.boomerangSpeed < 0f)
                {
                    this.StartDropping();
                    this.yI += 80f;
                }*/
            }
            /*else if (!this.hasReachedApex)
            {
                this.xI = 0f;
                this.hasReachedApex = true;
                this.holdAtApexTime = this.holdAtApexDuration;
            }*/
            this.PlayBounceSound();
        }
        if (this.dropping)
        {
            if (this.yI < 0f)
            {
                if (Physics.Raycast(new Vector3(base.X, base.Y + 6f, 0f), Vector3.down, out this.raycastHit, 6f + this.heightOffGround - this.yI * this.t, this.groundLayer))
                {
                    if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null && this.yI < -30f)
                    {
                        this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(0, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));
                    }
                    this.xI *= this.frictionM;
                    if (this.yI < -40f)
                    {
                        this.yI *= -this.bounceYM;
                    }
                    else
                    {
                        this.yI = 0f;
                        base.Y = this.raycastHit.point.y + this.heightOffGround;
                    }
                    this.rotationSpeed = -25f * this.xI;
                    this.PlayBounceSound();
                }
            }
            else if (this.yI > 0f && Physics.Raycast(new Vector3(base.X, base.Y - 6f, 0f), Vector3.up, out this.raycastHit, 6f + this.heightOffGround + this.yI * this.t, this.groundLayer))
            {
                if (this.raycastHit.collider.gameObject.GetComponent<Block>() != null)
                {
                    this.raycastHit.collider.gameObject.GetComponent<Block>().Damage(new DamageObject(0, DamageType.Knock, this.xI, this.yI, base.X, base.Y, this));
                }
                this.yI *= -(this.bounceYM + 0.1f);
                this.PlayBounceSound();
                this.rotationSpeed = -25f * this.xI;
            }
        }
        //Main.Log("after hit walls");
        return true;
    }

    // Token: 0x06003523 RID: 13603 RVA: 0x00199A0F File Offset: 0x00197E0F
    protected void StartDropping()
    {
        this.dropping = true;
        this.collectDelayTime = 0f;
        this.rotationSpeed = -25f * this.xI;
        base.GetComponent<AudioSource>().Stop();
        this.boomerangCollider.enabled = false;
    }

    // Token: 0x06003524 RID: 13604 RVA: 0x00199A4C File Offset: 0x00197E4C
    protected void PlayBounceSound()
    {
        float num = Mathf.Abs(this.xI) + Mathf.Abs(this.yI);
        if (num > 33f)
        {
            float num2 = num / 210f;
            float num3 = 0.05f + Mathf.Clamp(num2 * num2, 0f, 0.25f);
            Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.hitSounds, num3 * this.bounceVolumeM, base.transform.position, 1f, true, false, false, 0f);
        }
    }

    // Token: 0x06003525 RID: 13605 RVA: 0x00199AD3 File Offset: 0x00197ED3
    protected override void RunLife()
    {
    }

    // Token: 0x06003526 RID: 13606 RVA: 0x00199AD5 File Offset: 0x00197ED5
    protected override void Bounce(RaycastHit raycastHit)
    {
        Main.Log("bouncing");
        if (this.returnTime > 0f)
        {
            this.xI = 0f;
            this.returnTime = 0f;
        }
        /*else if (!this.dropping)
        {
            this.StartDropping();
        }*/
    }

    // Token: 0x06003527 RID: 13607 RVA: 0x00199B14 File Offset: 0x00197F14
    protected override void HitUnits()
    {
        if (this.hitUnitsDelay > 0f)
        {
            this.hitUnitsDelay -= this.t;
        }
        else if (!this.dropping)
        {
            /*if (this.hasReachedApex && this.holdAtApexTime > 0f && MapController.DamageGround(this, 1, DamageType.Normal, 6f, base.X, base.Y, null, false))
            {
                Main.Log("reached apex slowing down?");
                EffectsController.CreateSparkParticles(base.X, base.Y, 1f, 3, 2f, 10f, 0f, 0f, UnityEngine.Random.value, 1f);
                this.holdAtApexTime = Mathf.Clamp(this.holdAtApexTime -= this.t * 3f, this.t, this.holdAtApexTime);
            }*/
            if (this.reversing)
            {
                if (Map.HitLivingUnits(this, this.playerNum, this.damageInternal, this.damageType, this.projectileSize, this.projectileSize, base.X, base.Y, this.xI, this.yI, true, false, true, false))
                {
                    this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
                    this.hitUnitsDelay = 0.0667f;
                    if (this.returnTime > 0f)
                    {
                        this.returnTime = 0f;
                    }
                    /*if (Mathf.Sign(this.xI) == Mathf.Sign(this.boomerangSpeed))
                    {
                        this.xI *= 0.66f;
                    }*/
                    /*if (this.holdAtApexTime > 0f)
                    {
                        this.holdAtApexTime -= 0.2f;
                    }*/
                    this.hitUnitsCount++;
                }
            }
            else if (Map.HitUnits(this.firedBy, this.playerNum, this.damageInternal, 1, this.damageType, this.projectileSize, this.projectileSize * 1.3f, base.X, base.Y, this.xI, this.yI, true, false, true, this.alreadyHit, false, true))
            {
                this.MakeEffects(false, base.X, base.Y, false, this.raycastHit.normal, this.raycastHit.point);
                this.hitUnitsDelay = 0.0667f;
                if (this.returnTime > 0f)
                {

                    //this.returnTime = 0f;
                }
                /*if (Mathf.Sign(this.xI) == Mathf.Sign(this.boomerangSpeed))
                {
                    this.xI *= 0.66f;
                }*/
                /*if (this.holdAtApexTime > 0f)
                {
                    this.holdAtApexTime -= 0.2f;
                }*/
                this.hitUnitsCount++;
            }
        }
    }

    // Token: 0x06003528 RID: 13608 RVA: 0x00199DFE File Offset: 0x001981FE
    protected override void HitWildLife()
    {
    }

    // Token: 0x06003529 RID: 13609 RVA: 0x00199E00 File Offset: 0x00198200
    protected void CheckReturnShield()
    {
        
        if (this.firedBy != null && (this.collectDelayTime <= 0f || this.hitUnitsCount > 2))
        {
            float f = this.firedBy.transform.position.x - base.X;
            float f2 = this.firedBy.transform.position.y + 10f - base.Y;
            if (Mathf.Abs(f) < 9f && Mathf.Abs(f2) < 14f)
            {
                //Main.Log("trying return");
                Shield.TryReturnRPC(this);
                /*if (base.IsMine)
                {
                    PID targetOthers = PID.TargetOthers;
                    bool immediate = false;
                    bool ignoreSessionID = false;
                    bool addExecutionDelay = true;
                    if (Shield.rpcSig == null)
					{
                        Shield.rpcSig = new RpcSignature<Shield>(Shield.TryReturnRPC);
                    }
                    Networking.Networking.RPC<Shield>(targetOthers, immediate, ignoreSessionID, addExecutionDelay, Shield.rpcSig, this);
                }*/
            }
        }
    }

    // Token: 0x0600352A RID: 13610 RVA: 0x00199EDC File Offset: 0x001982DC
    private static void TryReturnRPC(Shield boomerang)
    {
        if (boomerang != null)
        {
            boomerang.ReturnShield();
        }
    }

    // Token: 0x0600352B RID: 13611 RVA: 0x00199EF0 File Offset: 0x001982F0
    private void ReturnShield()
    {
        //Main.Log("returning shield");
        CaptainAmeribro captainAmeribro = this.firedBy as CaptainAmeribro;
        if (captainAmeribro)
        {
            captainAmeribro.ReturnShield(this);
        }
        //Main.Log("after return shield");
        Sound.GetInstance().PlaySoundEffectAt(this.soundHolder.powerUp, 0.7f, base.transform.position, 0.95f + UnityEngine.Random.value * 0.1f, true, false, false, 0f);
        //Main.Log("after play sound effect");
        this.DeregisterProjectile();
        //Main.Log("after deregister");
        this.activeProjectile = false;
        UnityEngine.Object.Destroy(base.gameObject);
        //Main.Log("destroyed");
    }

    // Token: 0x0600352C RID: 13612 RVA: 0x00199F6B File Offset: 0x0019836B
    protected virtual void ApplyGravity()
    {
        this.yI -= 600f * this.t;
    }

    // Token: 0x0600352D RID: 13613 RVA: 0x00199F86 File Offset: 0x00198386
    public override void Death()
    {
    }

    public bool forward = true;

    public float ricochetCooldown = 0f;

    public bool activeProjectile = false;

    public AnimatedTexture texture;

    public float returnTime = 2f;

    public float holdAtApexDuration = 4f;

    protected bool hasReachedApex;

    protected float holdAtApexTime;

    // Token: 0x040031CE RID: 12750
    public float returnLerpSpeed = 3f;

    // Token: 0x040031CF RID: 12751
    protected float boomerangSpeed;

    // Token: 0x040031D0 RID: 12752
    public float rotationSpeed = 400f;

    // Token: 0x040031D1 RID: 12753
    protected float hitUnitsDelay;

    // Token: 0x040031D2 RID: 12754
    protected int hitUnitsCount;

    // Token: 0x040031D3 RID: 12755
    protected bool dropping;

    // Token: 0x040031D4 RID: 12756
    public BoxCollider boomerangCollider;

    // Token: 0x040031D5 RID: 12757
    public float boomerangLoopPitchM = 1f;

    // Token: 0x040031D6 RID: 12758
    protected float collectDelayTime = 1f;

    // Token: 0x040031D7 RID: 12759
    protected float windCounter;

    // Token: 0x040031D8 RID: 12760
    protected int windCount;

    // Token: 0x040031D9 RID: 12761
    public float windRotationSpeedM = 1f;

    // Token: 0x040031DA RID: 12762
    protected bool stuck;

    // Token: 0x040031DB RID: 12763
    protected float xStart;

    // Token: 0x040031DC RID: 12764
    protected float lastXI;

    // Token: 0x040031DD RID: 12765
    public float bounceXM = 0.5f;

    // Token: 0x040031DE RID: 12766
    public float bounceYM = 0.33f;

    // Token: 0x040031DF RID: 12767
    public float frictionM = 0.4f;

    // Token: 0x040031E0 RID: 12768
    public float bounceVolumeM = 0.25f;

    // Token: 0x040031E1 RID: 12769
    public float heightOffGround = 4f;

    // Token: 0x040031E2 RID: 12770
    protected List<Unit> alreadyHit = new List<Unit>();

    // Token: 0x040031E3 RID: 12771
    protected int hitCount;

    // Token: 0x040031E4 RID: 12772
    [CompilerGenerated]
    private static RpcSignature<Shield> rpcSig;
}
