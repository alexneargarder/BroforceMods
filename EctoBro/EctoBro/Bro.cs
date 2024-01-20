using System;
using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace EctoBro
{
    [HeroPreset("EctoBro", HeroType.Rambro)]
    public class Bro : CustomHero
    {
        // Proton beam
        LineRenderer protonLine1;
        LineRenderer protonLine1Cap;
        LineRenderer protonLine2;
        Material[] protonLine2Mats;
        protected Vector3 protonLineHitpoint;
        protected const float protonLineRange = 1000f;
        protected const float offsetSpeed = 4f;
        protected const float swaySpeedLerpM = 1f;
        protected const int protonUnitDamage = 1;
        protected const int protonWallDamage = 1;

        protected float currentOffset = 0f;
        protected float currentOffset2 = 0f;
        protected float targetSway = 0f;
        protected float curSway = 0f;
        protected float swaySpeed = 5f;
        protected float swaySpeedCurrent = 0.5f;
        protected float sparkCooldown = 0;
        protected float muzzleFlashCooldown = 0f;
        protected int protonLine2Frame = 0;
        protected float protonLine2FrameCounter = 0f;
        protected float protonDamageCooldown = 0f;
        protected float fireKnockbackCooldown = 0f;
        protected float effectCooldown = 0f;
        protected float pushBackForceM = 1f;
        public static System.Random rnd = new System.Random();
        protected LayerMask unitsLayer;

        // DEBUG
        //public static string scaleStr = "0.035";
        //public static float scale = 0.035f;
        //public static string scaleStr2 = "1";
        //public static float scale2 = 1f;
        //public static bool tile = false;
        //public static string scaleStr = "1";
        //public static float scale = 1f;
        //public static string offsetStr = "1";
        //public static float offset = 0f;
        public static string proton2SpeedStr = "1.9";
        public static float proton2Speed = 1.9f;
        public static string proton2FramerateStr = "0.1";
        public static float proton2Framerate = 0.1f;

        protected override void Awake()
        {
            base.Awake();

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            protonLine1 = new GameObject("ProtonLine1", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
            protonLine1.transform.parent = this.transform;
            protonLine1.material = ResourcesController.GetMaterial(directoryPath, "protonLine1.png");

            protonLine1Cap = new GameObject("ProtonLine1End", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
            protonLine1Cap.transform.parent = this.transform;
            protonLine1Cap.material = ResourcesController.GetMaterial(directoryPath, "protonLine1End.png");

            protonLine2 = new GameObject("ProtonLine2", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
            protonLine2.transform.parent = this.transform;
            protonLine2Mats = new Material[4];
            for ( int i = 0; i < 4; ++i )
            {
                protonLine2Mats[i] = ResourcesController.GetMaterial(directoryPath, "protonLine2" + (i + 1) + ".png");
            }
            protonLine2.material = protonLine2Mats[0];
            //protonLine2.material = ResourcesController.GetMaterial(directoryPath, "protonLine2.png");

            this.unitsLayer = 1 << LayerMask.NameToLayer("Units");
        }

        protected override void Update()
        {
            base.Update();
        }

        public void makeTextBox(string label, ref string text, ref float val)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            text = GUILayout.TextField(text);
            GUILayout.EndHorizontal();

            float temp;
            if (float.TryParse(text, out temp))
            {
                val = temp;
            }
        }

        public override void UIOptions()
        {
            //makeTextBox("red", ref redStr, ref red);
            //makeTextBox("scale", ref scaleStr, ref scale);
            //makeTextBox("offset", ref offsetStr, ref offset);
            makeTextBox("speed", ref proton2SpeedStr, ref proton2Speed);
            makeTextBox("framerate", ref proton2FramerateStr, ref proton2Framerate);
        }

        // Proton Gun methods
        protected override void StartFiring()
        {
            base.StartFiring();
            this.StartProtonGun();
            this.fireKnockbackCooldown = 0f;
    }

        protected override void RunFiring()
        {
            if ( this.fire )
            {
                this.currentOffset += this.t * offsetSpeed;
                this.currentOffset2 += this.t * offsetSpeed * proton2Speed;
                this.protonDamageCooldown -= this.t;
                this.effectCooldown -= this.t;
                this.fireKnockbackCooldown -= this.t;
                UpdateProtonGun();

                if ( this.fireKnockbackCooldown <= 0 )
                {
                    this.xIBlast -= base.transform.localScale.x * 4f * this.pushBackForceM;
                    if (base.Y > this.groundHeight)
                    {
                        this.yI += Mathf.Clamp(3f * this.pushBackForceM, 3f, 16f);
                    }

                    this.pushBackForceM = Mathf.Clamp(this.pushBackForceM + this.t * 6f, 1f, 12f);
                    this.fireKnockbackCooldown = 0.015f;
                }
            }
            else
            {
                this.pushBackForceM = 1f;
            }
        }

        protected override void AddSpeedLeft()
        {
            base.AddSpeedLeft();
            if (this.xIBlast > this.speed * 1.6f)
            {
                this.xIBlast = this.speed * 1.6f;
            }
        }

        protected override void AddSpeedRight()
        {
            base.AddSpeedRight();
            if (this.xIBlast < this.speed * -1.6f)
            {
                this.xIBlast = this.speed * -1.6f;
            }
        }

        protected override void RunGun()
        {
            if (!this.wallDrag && this.fire)
            {
                this.gunCounter += this.t;
                if (this.gunCounter > 0.07f)
                {
                    this.gunCounter -= 0.07f;
                    this.gunFrame--;
                    if (this.gunFrame < 1)
                    {
                        this.gunFrame = 3;
                    }
                    this.SetGunSprite(this.gunFrame, 0);
                }
            }
            else if ( this.gunFrame > 0 )
            {
                this.gunCounter += this.t;
                if (this.gunCounter > 0.0334f)
                {
                    this.gunCounter -= 0.0334f;
                    this.gunFrame--;
                    this.SetGunSprite(this.gunFrame, 0);
                }
            }
            else
            {
                this.SetGunSprite(this.gunFrame, 0);
            }
        }

        protected override void StopFiring()
        {
            base.StopFiring();
            this.StopProtonGun();
        }

        protected void StartProtonGun()
        {
            this.currentOffset = 0;
            this.currentOffset2 = 0;
            this.curSway = 0;
            this.targetSway = UnityEngine.Random.Range(0, 10);
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
            Vector3 startPoint = new Vector3(base.transform.position.x + base.transform.localScale.x * 10f, base.transform.position.y + 7f, 0);
            Vector3 endPoint = Vector3.zero;
            Vector3 startPointCap = startPoint;
            startPoint.x += base.transform.localScale.x * 10f;
            Vector3 startPointCapEnd = startPoint;
            startPointCapEnd.x += base.transform.localScale.x * 0.5f;
            float capOffset = base.transform.localScale.x * (1.9f * Math.Sin(Math.PI2 * currentOffset));
            startPointCapEnd.y += capOffset;
            Vector3 startPointCapMid = new Vector3(base.transform.localScale.x * Mathf.Abs(startPointCap.x - startPointCapEnd.x) / 2 + startPointCap.x, startPointCap.y + 0.75f * capOffset);

            // Create sparks at tip of gun
            if ( sparkCooldown <= 0 )
            {
                int particleCount = rnd.Next(3, 5);
                for (int i = 0; i < particleCount; ++i)
                {
                    EffectsController.CreateSparkParticles(EffectsController.instance.sparkParticleShower, startPointCap.x, startPoint.y, 1, 0, 30f + UnityEngine.Random.value * 20f, UnityEngine.Random.value * 50f, UnityEngine.Random.value * 50f, 0.5f, 0.2f + UnityEngine.Random.value * 0.2f);
                }
                sparkCooldown = UnityEngine.Random.Range(0.25f, 0.5f);
            }
            sparkCooldown -= this.t;

            // Run hit detection
            ProtonLineHitDetection(ref startPoint, ref endPoint);

            endPoint.y += curSway;

            // Calculate sway of proton beams
            swaySpeedCurrent = Mathf.Lerp(swaySpeedCurrent, swaySpeed, swaySpeedLerpM * this.t);
            if ( curSway > targetSway )
            {
                curSway -= this.t * swaySpeedCurrent;
                //curSway = Mathf.Lerp(curSway, targetSway, swaySpeedCurrent * this.t);
                if ( curSway - 0.1 <= targetSway )
                {
                    targetSway = UnityEngine.Random.Range(0, 10);
                    swaySpeedCurrent = 1f;
                }
            }
            else
            {
                curSway += this.t * swaySpeedCurrent;
                //curSway = Mathf.Lerp(curSway, targetSway, swaySpeedCurrent * this.t);
                if (curSway + 0.1 >= targetSway)
                {
                    targetSway = UnityEngine.Random.Range(0, 10);
                    swaySpeedCurrent = 1f;
                }
            }

            // Update proton line 2 material
            this.protonLine2.material = this.protonLine2Mats[protonLine2Frame];
            this.protonLine2FrameCounter += this.t;
            if ( this.protonLine2FrameCounter >= proton2Framerate )
            {
                this.protonLine2FrameCounter -= proton2Framerate;
                ++this.protonLine2Frame;
                if ( this.protonLine2Frame > 3 )
                {
                    this.protonLine2Frame = 0;
                }
            }

            float magnitude = (endPoint - startPoint).magnitude;

            this.protonLine1.SetPosition(0, startPoint);
            this.protonLine1.SetPosition(1, endPoint);
            this.protonLine1.material.SetTextureScale("_MainTex", new Vector2(magnitude * 0.035f, 1f));
            this.protonLine1.material.SetTextureOffset("_MainTex", new Vector2(-currentOffset, 0));

            this.protonLine1Cap.SetPosition(0, startPointCap);
            this.protonLine1Cap.SetPosition(1, startPointCapMid);
            this.protonLine1Cap.SetPosition(2, startPointCapEnd);

            startPointCap.z = -10f;
            endPoint.z = -10f;
            this.protonLine2.SetPosition(0, startPointCap);
            this.protonLine2.SetPosition(1, endPoint);
            this.protonLine2.material.SetTextureScale("_MainTex", new Vector2(magnitude * 0.035f, 1f));
            this.protonLine2.material.SetTextureOffset("_MainTex", new Vector2(-currentOffset2, 0f));
        }

        protected void ProtonLineHitDetection(ref Vector3 startPoint, ref Vector3 endPoint)
        {
            RaycastHit groundHit = this.raycastHit;
            bool haveHitGround = false;
            float currentRange = protonLineRange;

            // Hit ground
            if (Physics.Raycast(startPoint, (base.transform.localScale.x > 0 ? Vector3.right : Vector3.left), out raycastHit, currentRange, this.groundLayer))
            {
                groundHit = this.raycastHit;
                // Shorten the range we check for raycast hits, we don't care about hitting anything past the current terrain.
                currentRange = this.raycastHit.distance;
                haveHitGround = true;
            }

            // Hit Unit, which must be closer than wall since we're checking with a shortened range
            if (Physics.Raycast(startPoint, (base.transform.localScale.x > 0 ? Vector3.right : Vector3.left), out raycastHit, currentRange, this.unitsLayer))
            {
                DamageCollider(this.raycastHit);
                endPoint = new Vector3(raycastHit.point.x, raycastHit.point.y, 0);
            }
            // Damage ground since no unit hit
            else if ( haveHitGround )
            {
                DamageCollider(groundHit);
                endPoint = new Vector3(groundHit.point.x, groundHit.point.y, 0);
            }
            // Nothing hit
            else
            {
                endPoint = new Vector3(startPoint.x + base.transform.localScale.x * protonLineRange, startPoint.y, 0);
            }
        }

        protected void DamageCollider(RaycastHit hit)
        {
            if ( this.protonDamageCooldown > 0 )
            {
                return;
            }
            Unit unit = hit.collider.GetComponent<Unit>();
            // Damage unit
            if (unit != null)
            {
                unit.Damage( protonUnitDamage, DamageType.Fire, base.transform.localScale.x, 0, (int)base.transform.localScale.x, this, hit.point.x, hit.point.y);
                unit.Knock(DamageType.Fire, base.transform.localScale.x * 30, 20, false);
            }
            // Damage other
            else
            {
                hit.collider.SendMessage("Damage", new DamageObject(protonWallDamage, DamageType.Bullet, 0f, 0f, hit.point.x, hit.point.y, this));
            }

            //EffectsController.CreateLaserParticle(hit.point.x, hit.point.y, hit.collider.gameObject);
            if ( this.effectCooldown <= 0 )
            {
                Puff puff = EffectsController.CreateEffect(EffectsController.instance.whiteFlashPopSmallPrefab, hit.point.x + base.transform.localScale.x * 4, hit.point.y + UnityEngine.Random.Range(-3, 3) + this.curSway, 0f, 0f, Vector3.zero, null);
                //puff.transform.localScale /= 2;
                this.effectCooldown = 0.15f;
            }
            
            protonDamageCooldown = 0.05f;
        }
    }
}
