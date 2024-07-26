using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using RocketLib.Utils;

namespace Control_Enemies_Mod
{
    public enum GhostState
    {
        Idle = 0,
        Attacking = 1,
        Taunting = 2,
        Ressurecting = 3
    }

    public class GhostPlayer : MonoBehaviour
    {
        // General
        public int playerNum;
        public Player player;
        public float spawnDelay = 0.25f;
        public GhostState state = GhostState.Idle;
        public Vector3 overrideSpawnPoint = Vector3.zero;

        // Animation
        public SpriteSM sprite;
        public float currentTransparency = 0f;
        protected const float finalTransparency = 0.75f;
        public Color playerColor = new Color(1f, 1f, 1f);
        public int frame = 0;
        protected float frameCounter = 0f;
        protected const float idleFramerate = 0.11f;
        protected const float attackingFramerate = 0.11f;
        protected float dancingFramerate = 0.07f;
        protected float ressurectingFramerate = 0.15f;
        protected const float spriteWidth = 32f;
        protected const float spriteHeight = 32f;

        // Movement
        public bool up, left, down, right, fire, buttonJump, special, highfive, buttonGesture, sprint;
        protected float yI, xI;
        protected const float speed = 175f;
        protected const float accelerationFactor = 3.5f;
        protected const float decelerationFactor = 1.5f;

        // Possession
        TestVanDammeAnim characterToPossess = null;
        Vector3 frozenPosition = Vector3.zero;

        public void Setup()
        {
            try
            {
                MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

                string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                directoryPath = Path.Combine(directoryPath, "sprites");

                Material material = ResourcesController.GetMaterial(directoryPath, "ghostSprite.png");

                renderer.material = material;

                this.sprite = this.gameObject.GetComponent<SpriteSM>();
                sprite.SetTextureDefaults();
                sprite.SetSize(32, 32);
                sprite.lowerLeftPixel = new Vector2(0, 32);
                sprite.pixelDimensions = new Vector2(32, 32);
                sprite.plane = SpriteBase.SPRITE_PLANE.XY;
                sprite.width = 32;
                sprite.height = 32;
                sprite.color = new Color(playerColor.r, playerColor.g, playerColor.b, currentTransparency);
                sprite.CalcUVs();
                sprite.UpdateUVs();
                sprite.offset = new Vector3(0f, 0f, 0f);

                this.gameObject.layer = 28;
            }
            catch (Exception ex)
            {
                Main.Log("Exception creating ghost: " + ex.ToString());
            }
        }

        public virtual void Start()
        {
            this.player = HeroController.players[playerNum];
        }

        public void SetSpawn()
        {
            Vector3 start;
            // No spawn provided so calculate the default one
            if ( overrideSpawnPoint == Vector3.zero )
            {
                start = Map.FindStartLocation();
                start.y += 40;
                start.x = SortOfFollow.GetScreenMinX() + ((SortOfFollow.GetScreenMaxX() - SortOfFollow.GetScreenMinX() - 50) / 4) * (playerNum + 1);
            }
            // Use overidden spawn point
            else
            {
                start = overrideSpawnPoint;
                start.y += 16;
            }
            this.transform.position = start;
        }

        public virtual void Update()
        {
            if ( spawnDelay > 0f )
            {
                spawnDelay -= Time.deltaTime;
                if ( spawnDelay <= 0 )
                {
                    this.SetSpawn();
                }
                return;
            }

            this.HandleTransparency();

            this.HandleInput();

            this.ChangeFrame();
        }

        public virtual void LateUpdate()
        {
            if ( this.characterToPossess != null )
            {
                characterToPossess.X = frozenPosition.x;
                characterToPossess.Y = frozenPosition.y;
            }
        }

        public void HandleTransparency()
        {
            if (currentTransparency < finalTransparency)
            {
                currentTransparency += Time.deltaTime / 1.5f;

                if (currentTransparency > finalTransparency)
                {
                    currentTransparency = finalTransparency;
                }

                sprite.SetColor(new Color(playerColor.r, playerColor.g, playerColor.b, currentTransparency));
            }
        }

        public void HandleInput()
        {
            player.GetInput(ref up, ref down, ref left, ref right, ref fire, ref buttonJump, ref special, ref highfive, ref buttonGesture, ref sprint);

            if ( state == GhostState.Idle )
            {
                // Go up
                if (this.up)
                {
                    this.yI += speed * Time.deltaTime * accelerationFactor;
                    if (this.yI > speed)
                    {
                        this.yI = speed;
                    }
                }
                // Go down
                else if (this.down)
                {
                    this.yI -= speed * Time.deltaTime * accelerationFactor;
                    if (this.yI < -speed)
                    {
                        this.yI = -speed;
                    }
                }
                // Slow vertical momentum
                else
                {
                    if (this.yI > 0)
                    {
                        this.yI -= speed * Time.deltaTime * decelerationFactor;
                        if (this.yI < 0)
                        {
                            this.yI = 0;
                        }
                    }
                    else if (this.yI < 0)
                    {
                        this.yI += speed * Time.deltaTime * decelerationFactor;
                        if (this.yI > 0)
                        {
                            this.yI = 0;
                        }
                    }
                }

                // Go right
                if (this.right)
                {
                    if (this.transform.localScale.x != 1)
                    {
                        this.transform.localScale = new Vector3(1f, 1f, 1f);
                    }
                    this.xI += speed * Time.deltaTime * accelerationFactor;
                    if (this.xI > speed)
                    {
                        this.xI = speed;
                    }
                }
                // Go left
                else if (this.left)
                {
                    if (this.transform.localScale.x != -1)
                    {
                        this.transform.localScale = new Vector3(-1f, 1f, 1f);
                    }
                    this.xI -= speed * Time.deltaTime * accelerationFactor;
                    if (this.xI < -speed)
                    {
                        this.xI = -speed;
                    }
                }
                // Slow horizontal momentum
                else
                {
                    if (this.xI > 0)
                    {
                        this.xI -= speed * Time.deltaTime * decelerationFactor;
                        if (this.xI < 0)
                        {
                            this.xI = 0;
                        }
                    }
                    else if (this.xI < 0)
                    {
                        this.xI += speed * Time.deltaTime * decelerationFactor;
                        if (this.xI > 0)
                        {
                            this.xI = 0;
                        }
                    }
                }

                Vector3 position = this.transform.position;
                position.x += (xI * Time.deltaTime);
                position.y += (yI * Time.deltaTime);

                this.transform.position = position;

                // Try to find enemy to attack
                if (fire)
                {
                    this.TryToAttack();
                    HeroController.SetAvatarAngry(playerNum, true);
                }
                else
                {
                    HeroController.SetAvatarCalm(playerNum, true);
                }

                // Start dancing
                if (buttonGesture && this.state != GhostState.Attacking)
                {
                    this.frame = 0;
                    this.state = GhostState.Taunting;
                }
            }
            else if ( state == GhostState.Taunting )
            {
                // Check facing
                if (this.right)
                {
                    if (this.transform.localScale.x != 1)
                    {
                        this.transform.localScale = new Vector3(1f, 1f, 1f);
                    }
                }
                else if (this.left)
                {
                    if (this.transform.localScale.x != -1)
                    {
                        this.transform.localScale = new Vector3(-1f, 1f, 1f);
                    }
                }

                // Slow Down
                if (this.yI > 0)
                {
                    this.yI -= speed * Time.deltaTime * decelerationFactor;
                    if (this.yI < 0)
                    {
                        this.yI = 0;
                    }
                }
                else if (this.yI < 0)
                {
                    this.yI += speed * Time.deltaTime * decelerationFactor;
                    if (this.yI > 0)
                    {
                        this.yI = 0;
                    }
                }
                if (this.xI > 0)
                {
                    this.xI -= speed * Time.deltaTime * decelerationFactor;
                    if (this.xI < 0)
                    {
                        this.xI = 0;
                    }
                }
                else if (this.xI < 0)
                {
                    this.xI += speed * Time.deltaTime * decelerationFactor;
                    if (this.xI > 0)
                    {
                        this.xI = 0;
                    }
                }

                if ( !buttonGesture )
                {
                    this.frame = 0;
                    this.state = GhostState.Idle;
                }
            }
        }

        public static TestVanDammeAnim GetNextClosestUnit(int playerNum, float xRange, float yRange, float x, float y)
        {
            if (Map.units == null)
            {
                return null;
            }
            float num = xRange;
            TestVanDammeAnim unit = null;
            for (int i = Map.units.Count - 1; i >= 0; i--)
            {
                TestVanDammeAnim unit2 = Map.units[i] as TestVanDammeAnim;
                if (unit2 != null && Main.AvailableToPossess(unit2))
                {
                    float num2 = unit2.Y + unit2.height / 2f + 3f - y;
                    if (Mathf.Abs(num2) - yRange < unit2.height)
                    {
                        float num3 = unit2.X - x;
                        if (Mathf.Abs(num3) - num < unit2.width)
                        {
                            unit = unit2;
                            num = Mathf.Abs(num2);
                        }
                    }
                }
            }
            if (unit != null)
            {
                return unit;
            }
            return null;
        }

        public static float ghostoffsetx = 11f;
        public static float ghostoffsety = 3f;

        public void TryToAttack()
        {
            TestVanDammeAnim character = GetNextClosestUnit(playerNum, 15f, 10f, this.transform.position.x, this.transform.position.y);
            if ( character != null )
            {
                characterToPossess = character;
                characterToPossess.name = "p";
                this.transform.localScale = new Vector3( Mathf.Sign(characterToPossess.X - this.transform.position.x), 1f, 1f);
                this.frozenPosition = characterToPossess.transform.position;
                this.transform.position = new Vector3(characterToPossess.X - base.transform.localScale.x * ghostoffsetx, characterToPossess.Y + characterToPossess.height + ghostoffsety, 0f);
                this.state = GhostState.Attacking;
                this.frame = 0;
                this.xI = 0;
                this.yI = 0;
            }
        }

        public void FinishAttack()
        {
            Main.StartControllingUnit(playerNum, characterToPossess, false, true, false);
            characterToPossess = null;
            this.state = GhostState.Idle;
            this.gameObject.SetActive(false);
        }

        public void ChangeFrame()
        {
            frameCounter += Time.deltaTime;

            switch ( state )
            {
                case GhostState.Idle:
                    if (frameCounter > idleFramerate)
                    {
                        frameCounter -= idleFramerate;

                        ++frame;

                        if (frame > 7)
                        {
                            frame = 0;
                        }

                        this.sprite.SetLowerLeftPixel(frame * spriteWidth, spriteHeight);
                    }
                    break;
                case GhostState.Attacking:
                    if (frameCounter > attackingFramerate)
                    {
                        frameCounter -= attackingFramerate;

                        ++frame;

                        if (frame > 13)
                        {
                            frame = 0;
                            this.FinishAttack();
                        }

                        this.sprite.SetLowerLeftPixel(frame * spriteWidth, 2 * spriteHeight);
                    }
                    break;
                case GhostState.Taunting:
                    if (frameCounter > attackingFramerate)
                    {
                        frameCounter -= attackingFramerate;

                        ++frame;

                        if (frame > 21)
                        {
                            frame = 4;
                        }

                        this.sprite.SetLowerLeftPixel(frame * spriteWidth, 3 * spriteHeight);
                    }
                    break;
                case GhostState.Ressurecting:
                    if (frameCounter > ressurectingFramerate)
                    {
                        frameCounter -= ressurectingFramerate;

                        ++frame;

                        if (frame > 14)
                        {
                            frame = 0;
                            this.state = GhostState.Idle;
                            this.sprite.SetLowerLeftPixel(frame * spriteWidth, spriteHeight);
                            return;
                        }

                        this.sprite.SetLowerLeftPixel(frame * spriteWidth, 4 * spriteHeight);
                    }
                    break;
            }
        }
    }
}
