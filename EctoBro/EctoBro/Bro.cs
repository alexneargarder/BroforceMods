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
        LineRenderer protonLine2;
        protected Vector3 protonLineHitpoint;
        protected float currentOffset = 0f;
        protected float offsetSpeed = 5f;
        protected float targetSway = 0f;
        protected float curSway = 0f;
        protected float swaySpeed = 5f;
        protected float swaySpeedCurrent = 0.5f;
        protected float swaySpeedLerpM = 1f;

        // DEBUG
        public static string scaleStr = "0.035";
        public static float scale = 0.035f;
        public static string scaleStr2 = "1";
        public static float scale2 = 1f;
        public static string offsetStr = "1";
        public static float offset = 0f;
        public static bool tile = false;

        protected override void Awake()
        {
            base.Awake();

            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            protonLine1 = new GameObject("ProtonLine1", new Type[] { typeof(Transform), typeof(LineRenderer) }).GetComponent<LineRenderer>();
            protonLine1.transform.parent = this.transform;
            protonLine1.material = ResourcesController.GetMaterial(directoryPath, "protonLine1.png");

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
            makeTextBox("scale", ref scaleStr, ref scale);
            makeTextBox("scale2", ref scaleStr2, ref scale2);
            makeTextBox("offset", ref offsetStr, ref offset);

            tile = GUILayout.Toggle(tile, "tile");
        }

        // Proton Gun methods
        protected override void StartFiring()
        {
            base.StartFiring();
            this.currentOffset = 0;
            this.curSway = 0;
            this.targetSway = UnityEngine.Random.Range(-10, 10);
            this.StartProtonGun();
        }

        protected override void RunFiring()
        {
            if ( this.fire )
            {
                this.currentOffset += this.t * offsetSpeed;
                UpdateProtonGun();
            }
        }

        protected override void StopFiring()
        {
            base.StopFiring();
            this.StopProtonGun();
        }

        protected void DrawProtonLine()
        {
            Vector3 startPoint = new Vector3(base.transform.position.x + base.transform.localScale.x * 12f, base.transform.position.y + 6.5f, 0);
            Vector3 endPoint;
            Vector3 midPoint;

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

            if ( targetSway < 0 )
            {
                curSway -= this.t * swaySpeedCurrent;
                //curSway = Mathf.Lerp(curSway, targetSway, swaySpeedCurrent * this.t);
                if ( curSway - 0.1 <= targetSway )
                {
                    targetSway = UnityEngine.Random.Range(-10, 10);
                    swaySpeedCurrent = 1f;
                }
            }
            else
            {
                curSway += this.t * swaySpeedCurrent;
                //curSway = Mathf.Lerp(curSway, targetSway, swaySpeedCurrent * this.t);
                if (curSway + 0.1 >= targetSway)
                {
                    targetSway = UnityEngine.Random.Range(-10, 10);
                    swaySpeedCurrent = 1f;
                }
            }

            endPoint.y += curSway;

            //midPoint = new Vector3(Mathf.Abs(startPoint.x - endPoint.x) / 2 + startPoint.x, endPoint.y);

            this.protonLine1.SetPosition(0, startPoint);
            this.protonLine1.SetPosition(1, endPoint);
            //this.protonLine1.SetPosition(1, midPoint);
            //this.protonLine1.SetPosition(2, endPoint);
            float magnitude = (endPoint - startPoint).magnitude;
            this.protonLine1.textureMode = LineTextureMode.RepeatPerSegment;
            //this.protonLine1.textureMode = LineTextureMode.Tile;
            //this.protonLine1.material.SetTextureScale("_MainTex", new Vector2(scale, scale2));
            this.protonLine1.material.SetTextureScale("_MainTex", new Vector2(magnitude * scale, scale2));
            this.protonLine1.material.SetTextureOffset("_MainTex", new Vector2(-currentOffset, 0));

            this.protonLine2.SetPosition(0, startPoint);
            this.protonLine2.SetPosition(1, endPoint);
            this.protonLine2.textureMode = LineTextureMode.RepeatPerSegment;
            this.protonLine2.material.SetTextureScale("_MainTex", new Vector2(magnitude * scale, scale2));
            this.protonLine2.material.SetTextureOffset("_MainTex", new Vector2(-currentOffset, 0f));
        }

        protected void StartProtonGun()
        {
            this.protonLine1.enabled = true;
            this.protonLine2.enabled = true;
            this.protonLine1.startWidth = 8f;
            this.protonLine1.endWidth = 10f;
            this.protonLine2.startWidth = 13f;
            this.protonLine2.endWidth = 15f;
            DrawProtonLine();
        }

        protected void UpdateProtonGun()
        {
            DrawProtonLine();
        }

        protected void StopProtonGun()
        {
            this.protonLine1.enabled = false;
            this.protonLine2.enabled = false;
        }
    }
}
