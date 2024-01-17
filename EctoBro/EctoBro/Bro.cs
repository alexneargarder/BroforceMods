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
        protected Vector3 protonLineHitpoint;
        protected float currentOffset = 0f;
        protected float currentOffset2 = 0f;
        protected float offsetSpeed = 4f;
        protected float targetSway = 0f;
        protected float curSway = 0f;
        protected float swaySpeed = 5f;
        protected float swaySpeedCurrent = 0.5f;
        protected float swaySpeedLerpM = 1f;
        protected float sparkCooldown = 0;
        protected float muzzleFlashCooldown = 0f;
        public static System.Random rnd = new System.Random();

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
            protonLine2.material = ResourcesController.GetMaterial(directoryPath, "protonLine2.png");
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
        }

        // Proton Gun methods
        protected override void StartFiring()
        {
            base.StartFiring();
            this.StartProtonGun();
        }

        protected override void RunFiring()
        {
            if ( this.fire )
            {
                this.currentOffset += this.t * offsetSpeed;
                this.currentOffset2 += this.t * offsetSpeed * 2;
                //this.currentOffset += this.t;
                UpdateProtonGun();
            }
        }

        protected override void RunGun()
        {
            if (!this.wallDrag && this.fire)
            {
                this.gunCounter += this.t;
                if (this.gunCounter > 0.06f)
                {
                    this.gunCounter -= 0.06f;
                    this.gunFrame--;
                    if (this.gunFrame < 0)
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

        protected void DrawProtonLine()
        {
            Vector3 startPoint = new Vector3(base.transform.position.x + base.transform.localScale.x * 10f, base.transform.position.y + 7f, 0);
            Vector3 endPoint;
            Vector3 startPointCap = startPoint;
            startPoint.x += base.transform.localScale.x * 10f;
            Vector3 startPointCapEnd = startPoint;
            startPointCapEnd.x += base.transform.localScale.x * 0.5f;
            float capOffset = base.transform.localScale.x * (1.9f * Math.Sin(Math.PI2 * currentOffset));
            startPointCapEnd.y += capOffset;
            Vector3 startPointCapMid = new Vector3(base.transform.localScale.x * Mathf.Abs(startPointCap.x - startPointCapEnd.x) / 2 + startPointCap.x, startPointCap.y + 0.75f * capOffset);

            //EffectsController.CreateMuzzleFlashEffect(startPoint.x, startPoint.y, -25f, 0f, UnityEngine.Random.Range(-20, 20), base.transform);
            //EffectsController.CreateEffect(EffectsController.instance.muzzleFlashGlowPrefab, startPoint.x, startPoint.y, -25f, 0f);
            //GameObject gameObject = EffectsController.InstantiateEffect(EffectsController.instance.lightObject, startPoint, Quaternion.identity) as GameObject;
            //gameObject.transform.parent = base.transform;

            /*            if ( muzzleFlashCooldown <= 0 )
                        {
                            //Puff puff = EffectsController.CreateEffect(EffectsController.instance.muzzleFlashPrefab, startPoint.x, startPoint.y, -25f, 0f, new Vector3(0f, -100f, 0f), null);
                            //puff.transform.parent = base.transform;
                            //puff.SetColor(new Color(1f, 0.439f, 0.188f, 1f));

                            //muzzleFlashCooldown = 0.25f;
                        }*/

            //muzzleFlashCooldown -= this.t;

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

            // Hit ground
            if (Physics.Raycast(startPoint, (base.transform.localScale.x > 0 ? Vector3.right : Vector3.left), out raycastHit, 2000f, this.groundLayer))
            {
                endPoint = new Vector3(raycastHit.point.x, raycastHit.point.y, 0);
            }
            else
            {
                endPoint = new Vector3(startPoint.x + base.transform.localScale.x * 2000f, startPoint.y, 0);
            }

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

            endPoint.y += curSway;

            //midPoint = new Vector3(Mathf.Abs(startPoint.x - endPoint.x) / 2 + startPoint.x, endPoint.y);

            float magnitude = (endPoint - startPoint).magnitude;

            this.protonLine1.SetPosition(0, startPoint);
            this.protonLine1.SetPosition(1, endPoint);
            this.protonLine1.material.SetTextureScale("_MainTex", new Vector2(magnitude * 0.035f, 1f));
            this.protonLine1.material.SetTextureOffset("_MainTex", new Vector2(-currentOffset, 0));

            this.protonLine1Cap.SetPosition(0, startPointCap);
            this.protonLine1Cap.SetPosition(1, startPointCapMid);
            this.protonLine1Cap.SetPosition(2, startPointCapEnd);

            this.protonLine2.SetPosition(0, startPointCap);
            this.protonLine2.SetPosition(1, endPoint);
            this.protonLine2.material.SetTextureScale("_MainTex", new Vector2(magnitude * 0.035f, 1f));
            this.protonLine2.material.SetTextureOffset("_MainTex", new Vector2(-currentOffset2, 0f));
        }

        protected void StartProtonGun()
        {
            this.currentOffset = 0;
            this.currentOffset2 = 0;
            this.curSway = 0;
            this.targetSway = UnityEngine.Random.Range(0, 10);

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
    }
}
