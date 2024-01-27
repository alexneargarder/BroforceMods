using System;
using System.Collections.Generic;
using BroMakerLib;
using BroMakerLib.CustomObjects.Bros;
using BroMakerLib.Loggers;
using System.IO;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace EctoBro
{
    [HeroPreset("EctoBro", HeroType.Rambro)]
    public class EctoBro : CustomHero
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

        // Ghost Trap
        GhostTrap trapPrefab, currentTrap;

        // Misc
        public static bool patched = false;

        // DEBUG
        Transform testTransform;
        MeshFilter testFilter;
        GameObject test;
        //public static string scaleStr = "0.035";
        //public static float scale = 0.035f;
        //public static string scaleStr2 = "1";
        //public static float scale2 = 1f;
        //public static bool tile = false;
        //public static string scaleStr = "1";
        //public static float scale = 1f;
        //public static string offsetStr = "1";
        //public static float offset = 0f;
        //public static string proton2SpeedStr = "1.9";
        //public static float proton2Speed = 1.9f;
        //public static string proton2FramerateStr = "0.1";
        //public static float proton2Framerate = 0.1f;
        //public static string trapFramerateStr = "0.08";
        //public static float trapFramerate = 0.08f;
        //public static string rotPointStr = "-3";
        //public static float rotPoint = -3f;
        //public static int currentGhostTrapFrame = 0;
        public static bool debugLines = false;

        // DEBUG
        public static void checkAttached(GameObject gameObject)
        {
            BMLogger.Log("\n\n");
            Component[] allComponents;
            allComponents = gameObject.GetComponents(typeof(Component));
            foreach (Component comp in allComponents)
            {
                BMLogger.Log("attached: " + comp.name + " also " + comp.GetType());
            }
            BMLogger.Log("\n\n");
        }

        protected override void Awake()
        {
            base.Awake();

            if (!patched)
            {
                try
                {
                    var harmony = new Harmony("EctoBro");
                    var assembly = Assembly.GetExecutingAssembly();
                    harmony.PatchAll(assembly);
                    patched = true;
                }
                catch (Exception ex)
                {
                    BMLogger.Log(ex.ToString());
                }
            }

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

            this.unitsLayer = 1 << LayerMask.NameToLayer("Units");

            trapPrefab = new GameObject("GhostTrap", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM), typeof(GhostTrap) }).GetComponent<GhostTrap>();
            trapPrefab.enabled = false;
            trapPrefab.soundHolder = (HeroController.GetHeroPrefab(HeroType.SnakeBroSkin) as SnakeBroskin).specialGrenade.soundHolder;
            trapPrefab.gameObject.layer = 28;
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
            //makeTextBox("speed", ref proton2SpeedStr, ref proton2Speed);
            //makeTextBox("framerate", ref proton2FramerateStr, ref proton2Framerate);
            //makeTextBox("rot", ref rotPointStr, ref rotPoint);
            //makeTextBox("trapframerate", ref trapFramerateStr, ref trapFramerate);

/*            GUILayout.Label("Current frame: " + currentGhostTrapFrame);
            if ( GUILayout.Button("previous frame") )
            {
                --currentGhostTrapFrame;
            }
            if (GUILayout.Button("Next frame"))
            {
                ++currentGhostTrapFrame;
            }*/

            debugLines = GUILayout.Toggle(debugLines, "debug lines");
        }

        // Proton Gun methods
        #region ProtonGun
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
                this.currentOffset2 += this.t * offsetSpeed * 1.9f;
                this.protonDamageCooldown -= this.t;
                this.effectCooldown -= this.t;
                this.fireKnockbackCooldown -= this.t;
                UpdateProtonGun();

/*                if ( this.fireKnockbackCooldown <= 0 )
                {
                    this.xIBlast -= base.transform.localScale.x * 3f * this.pushBackForceM;
                    if (base.Y > this.groundHeight)
                    {
                        this.yI += Mathf.Clamp(3f * this.pushBackForceM, 3f, 6f);
                    }

                    this.pushBackForceM = Mathf.Clamp(this.pushBackForceM + this.t * 6f, 1f, 6f);
                    this.fireKnockbackCooldown = 0.015f;
                }*/
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
            Vector3 startPoint = new Vector3(base.X + base.transform.localScale.x * 10f, base.Y + 7f, 0);
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
            Vector3 hitDetectionStart = new Vector3(base.X, base.Y + 7f, 0);
            ProtonLineHitDetection(hitDetectionStart, ref endPoint);

            // Check if hit point is too close to have both segments of proton line 1
            float DistanceToEnd = Vector3.Distance(hitDetectionStart, endPoint);

            //endPoint.y += curSway;

            // Calculate sway of proton beams
/*            swaySpeedCurrent = Mathf.Lerp(swaySpeedCurrent, swaySpeed, swaySpeedLerpM * this.t);
            if ( curSway > targetSway )
            {
                curSway -= this.t * swaySpeedCurrent;
                //curSway = Mathf.Lerp(curSway, targetSway, swaySpeedCurrent * this.t);
                if ( curSway - 0.1 <= targetSway )
                {
                    targetSway = UnityEngine.Random.Range(0, 7);
                    swaySpeedCurrent = 1f;
                }
            }
            else
            {
                curSway += this.t * swaySpeedCurrent;
                //curSway = Mathf.Lerp(curSway, targetSway, swaySpeedCurrent * this.t);
                if (curSway + 0.1 >= targetSway)
                {
                    targetSway = UnityEngine.Random.Range(0, 7);
                    swaySpeedCurrent = 1f;
                }
            }*/

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

            float magnitude = (endPoint - startPoint).magnitude;

            if ( DistanceToEnd > 30f)
            {
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
            else if ( DistanceToEnd > 15f )
            {
                this.protonLine1.SetPosition(0, startPoint);
                this.protonLine1.SetPosition(1, startPoint);

                this.protonLine1Cap.SetPosition(0, startPointCap);
                this.protonLine1Cap.SetPosition(1, startPointCapMid);
                this.protonLine1Cap.SetPosition(2, endPoint);

                startPointCap.z = -10f;
                endPoint.z = -10f;
                this.protonLine2.SetPosition(0, startPointCap);
                this.protonLine2.SetPosition(1, endPoint);
                this.protonLine2.material.SetTextureScale("_MainTex", new Vector2(magnitude * 0.035f, 1f));
                this.protonLine2.material.SetTextureOffset("_MainTex", new Vector2(-currentOffset2, 0f));
            }
            else
            {
                this.protonLine1.SetPosition(0, startPoint);
                this.protonLine1.SetPosition(1, startPoint);

                this.protonLine1Cap.SetPosition(0, startPoint);
                this.protonLine1Cap.SetPosition(1, startPoint);
                this.protonLine1Cap.SetPosition(2, startPoint);

                this.protonLine2.SetPosition(0, startPoint);
                this.protonLine2.SetPosition(1, startPoint);
            }
        }

        protected void ProtonLineHitDetection(Vector3 startPoint, ref Vector3 endPoint)
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
            if (Physics.Raycast(startPoint, (base.transform.localScale.x > 0 ? Vector3.right : Vector3.left), out raycastHit, currentRange, this.unitsLayer) 
                && (this.raycastHit.collider.GetComponent<Unit>() == null || !this.raycastHit.collider.GetComponent<Unit>().invulnerable) )
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

            // Only damage visible objects
            if (SortOfFollow.IsItSortOfVisible(hit.point, 24, 24f))
            {
                Unit unit = hit.collider.GetComponent<Unit>();
                // Damage unit
                if (unit != null)
                {
                    unit.Damage(protonUnitDamage, DamageType.Fire, base.transform.localScale.x, 0, (int)base.transform.localScale.x, this, hit.point.x, hit.point.y);
                    unit.Knock(DamageType.Fire, base.transform.localScale.x * 30, 20, false);
                }
                // Damage other
                else
                {
                    hit.collider.SendMessage("Damage", new DamageObject(protonWallDamage, DamageType.Bullet, 0f, 0f, hit.point.x, hit.point.y, this));
                }
            }

            //EffectsController.CreateLaserParticle(hit.point.x, hit.point.y, hit.collider.gameObject);
            if ( this.effectCooldown <= 0 )
            {
                Puff puff = EffectsController.CreateEffect(EffectsController.instance.whiteFlashPopSmallPrefab, hit.point.x + base.transform.localScale.x * 4, hit.point.y + UnityEngine.Random.Range(-3, 3), 0f, 0f, Vector3.zero, null);
                this.effectCooldown = 0.15f;
            }
            
            protonDamageCooldown = 0.05f;
        }
        #endregion

        // Special Methods
        #region Special
        protected override void UseSpecial()
        {
            if (this.currentTrap != null && this.currentTrap.state != GhostTrap.TrapState.Closed )
            {
                // Close Trap
                this.currentTrap.StartClosingTrap();
            }
            else if (this.SpecialAmmo > 0)
            {
                this.PlayThrowLightSound(0.4f);
                this.SpecialAmmo--;
                if (base.IsMine)
                {
                    Grenade grenade;
                    if (this.down && this.IsOnGround() && this.ducking)
                    {
                        grenade = ProjectileController.SpawnGrenadeLocally(this.trapPrefab, this, base.X + Mathf.Sign(base.transform.localScale.x) * 6f, base.Y + 3f, 0.001f, 0.011f, Mathf.Sign(base.transform.localScale.x) * 30f, 70f, base.playerNum, 0);
                    }
                    else
                    {
                        grenade = ProjectileController.SpawnGrenadeLocally(this.trapPrefab, this, base.X + Mathf.Sign(base.transform.localScale.x) * 8f, base.Y + 8f, 0.001f, 0.011f, Mathf.Sign(base.transform.localScale.x) * 200f, 150f, base.playerNum, 0);
                    }
                    this.currentTrap = grenade.GetComponent<GhostTrap>();
                    this.currentTrap.enabled = true;
                }
            }
            else
            {
                HeroController.FlashSpecialAmmo(base.playerNum);
                this.ActivateGun();
            }
            this.pressSpecialFacingDirection = 0;
        }
        #endregion
    }
}
