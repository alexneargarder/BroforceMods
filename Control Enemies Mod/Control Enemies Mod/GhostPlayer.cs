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
        Resurrecting = 3,
        Reviving = 4,
    }

    public class GhostPlayer : MonoBehaviour
    {
        // General
        public int playerNum;
        public Player player;
        public float spawnDelay = 0.25f;
        public GhostState state = GhostState.Idle;
        public Vector3 overrideSpawnPoint = Vector3.zero;
        protected bool ableToRevive = false;
        protected float forceReviveTime = 0f;
        protected float startReviveFlashTime = 0f;
        public const float ghostSpawnOffset = 16f;

        // Animation
        public SpriteSM sprite;
        public float currentTransparency = 0f;
        protected const float finalTransparency = 0.75f;
        public Color playerColor = new Color(1f, 1f, 1f);
        public int frame = 0;
        protected float frameCounter = 0f;
        protected const float idleFramerate = 0.11f;
        protected float attackingFramerate = 0.08f;
        protected float dancingFramerate = 0.11f;
        protected float resurrectingFramerate = 0.11f;
        protected float reviveFramerate = 0.11f;
        protected const float spriteWidth = 32f;
        protected const float spriteHeight = 32f;
        protected Color reviveColor = new Color(1f, 0.8431f, 0f);
        //protected Color[] playerColors = new Color[] { new Color(0.15f, 0.47f, 0.92f), new Color(1f, 0.17f, 0.17f), new Color(1f, 0.64f, 0f), new Color(0.56f, 0f, 1f) };
        protected Color[] playerColors = new Color[] { new Color(0f, 0.5f, 1f), new Color(1f, 0f, 0f), new Color(1f, 0.45f, 0f), new Color(0.55f, 0f, 1f) };

        // Movement
        public bool up, left, down, right, fire, buttonJump, special, highfive, buttonGesture, sprint;
        protected float yI, xI;
        protected const float normalSpeed = 130f;
        protected const float sprintSpeed = 180f;
        protected const float accelerationFactor = 3.5f;
        protected const float decelerationFactor = 1.5f;
        protected float screenMinX, screenMaxX, screenMinY, screenMaxY;
        protected bool usingController = false;
        protected int controllerNum = 0;

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
            this.playerColor = playerColors[playerNum];

            if ( this.player.controllerNum > 3 )
            {
                this.usingController = true;
                controllerNum = this.player.controllerNum - 4;
            }
        }

        public void SetSpawn()
        {
            Vector3 start;
            // No spawn provided so calculate the default one
            if ( overrideSpawnPoint == Vector3.zero )
            {
                start = Map.FindStartLocation();
                // Spawning near start
                if ( Mathf.Abs(SortOfFollow.GetScreenMinX() - start.x) < 100 )
                {
                    start.y += 40;
                    start.x = SortOfFollow.GetScreenMinX() + 25 + ((SortOfFollow.GetScreenMaxX() - SortOfFollow.GetScreenMinX() - 50) / 4) * (playerNum + 1);
                }
                // Spawning away from start
                else
                {
                    start.y = SortOfFollow.GetScreenMinY() + 60 + (SortOfFollow.GetScreenMaxY() - SortOfFollow.GetScreenMinY()) / 2f;
                    start.x = SortOfFollow.GetScreenMinX() + 25 + ((SortOfFollow.GetScreenMaxX() - SortOfFollow.GetScreenMinX() - 50) / 4) * (playerNum + 1);
                }
                
            }
            // Use overidden spawn point
            else
            {
                start = overrideSpawnPoint;
                start.y += ghostSpawnOffset;
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

            this.ConstrainToScreen();

            this.ChangeFrame();

            // Countdown to player being forced to revive
            if ( this.ableToRevive && this.state != GhostState.Reviving )
            {
                this.forceReviveTime -= Time.deltaTime;
                if ( this.forceReviveTime <= 0 )
                {
                    this.StartReviving();
                }
                // Flash player
                else
                {
                    float num = 0.5f + Mathf.Sin((Time.time - startReviveFlashTime) * 15f) * 0.23f;
                    Color color = new Color(num, num, num, 1f);
                    this.sprite.GetComponent<Renderer>().material.SetColor("_TintColor", color);
                }
            }

            // Make sure enemy we are attacking doesn't die
            if ( this.state == GhostState.Attacking )
            {
                if ( characterToPossess == null || !characterToPossess.IsAlive() || characterToPossess.health <= 0 )
                {
                    if (this.characterToPossess != null)
                    {
                        this.characterToPossess.name = "enemy";
                    }
                    this.characterToPossess = null;
                    this.state = GhostState.Idle;
                    this.frame = 0;
                    SetFrame();
                }
            }
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

            if ( state == GhostState.Idle || this.state == GhostState.Reviving )
            {
                // Check sprint manually since it's not detected by GetInput
                this.sprint = InputReader.GetDashStart(player.controllerNum);

                // Use actual axes rather than just cardinal directions
                if ( this.usingController )
                {
                    Rewired.Player player = Rewired.ReInput.players.GetPlayer(this.controllerNum);

                    float upAmount = player.GetAxis("Up") - player.GetAxis("Down");
                    float rightAmount = player.GetAxis("Right") - player.GetAxis("Left");

                    float speed = this.sprint ? sprintSpeed : normalSpeed;

                    if ( Mathf.Abs(yI) < Mathf.Abs(upAmount * speed))
                    {
                        this.yI += upAmount * speed * Time.deltaTime * accelerationFactor;
                    }

                    if ( this.yI > speed)
                    {
                        this.yI = speed;
                    }
                    else if ( this.yI < -speed)
                    {
                        this.yI = -speed;
                    }

                    if ( Mathf.Abs(xI) < Mathf.Abs(rightAmount * speed) )
                    {
                        this.xI += rightAmount * speed * Time.deltaTime * accelerationFactor;
                    }

                    if (this.xI > speed)
                    {
                        this.xI = speed;
                    }
                    else if (this.xI < -speed)
                    {
                        this.xI = -speed;
                    }

                    // Slow vertical momentum
                    if ( !(this.up || this.down) )
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
                    }
                    // Go left
                    else if (this.left)
                    {
                        if (this.transform.localScale.x != -1)
                        {
                            this.transform.localScale = new Vector3(-1f, 1f, 1f);
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
                }
                else
                {
                    float speed = this.sprint ? sprintSpeed : normalSpeed;

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
                }

                Vector3 position = this.transform.position;
                position.x += (xI * Time.deltaTime);
                position.y += (yI * Time.deltaTime);

                this.transform.position = position;

                // Keep player next to ghost to make camera work
                if ( this.state == GhostState.Reviving || this.ableToRevive )
                {
                    player.character.SetXY(position.x, position.y);
                }

                // Don't check inputs if we're reviving
                if ( this.state != GhostState.Reviving )
                {
                    // Try to find enemy to attack
                    if (fire)
                    {
                        if (!this.ableToRevive)
                        {
                            this.TryToAttack();
                            HeroController.SetAvatarAngry(playerNum, true);
                        }
                        // Start reviving
                        else
                        {
                            this.StartReviving();
                        }
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

                float speed = this.sprint ? sprintSpeed : normalSpeed;

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

        public void ConstrainToScreen()
        {
            SetResolutionCamera.GetScreenExtents(ref this.screenMinX, ref this.screenMaxX, ref this.screenMinY, ref this.screenMaxY);

            Vector3 position = this.transform.position;

            if ( position.x < this.screenMinX + 5f )
            {
                position.x = this.screenMinX + 5f;
            }
            else if ( position.x > this.screenMaxX - 5f )
            {
                position.x = this.screenMaxX - 5f;
            }

            if ( position.y < screenMinY + 10f )
            {
                position.y = this.screenMinY + 10f;
            }
            else if ( position.y > screenMaxY - 10f )
            {
                position.y = this.screenMaxY - 10f;
            }

            this.transform.position = position;
        }

        public void ChangeFrame()
        {
            frameCounter += Time.deltaTime;

            switch (state)
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
                    if (frameCounter > dancingFramerate)
                    {
                        frameCounter -= dancingFramerate;

                        ++frame;

                        if (frame > 21)
                        {
                            frame = 4;
                        }

                        this.sprite.SetLowerLeftPixel(frame * spriteWidth, 3 * spriteHeight);
                    }
                    break;
                case GhostState.Resurrecting:
                    if (frameCounter > resurrectingFramerate)
                    {
                        frameCounter -= resurrectingFramerate;

                        ++frame;

                        if (frame > 15)
                        {
                            frame = 0;
                            this.state = GhostState.Idle;
                            this.sprite.SetLowerLeftPixel(frame * spriteWidth, spriteHeight);
                            return;
                        }

                        this.sprite.SetLowerLeftPixel(frame * spriteWidth, 4 * spriteHeight);
                    }
                    break;
                case GhostState.Reviving:
                    if (frameCounter > reviveFramerate)
                    {
                        frameCounter -= reviveFramerate;

                        ++frame;

                        if (frame > 14)
                        {
                            this.ReviveCharacter();
                            frame = 0;
                        }

                        this.sprite.SetLowerLeftPixel(frame * spriteWidth, 5* spriteHeight);
                    }
                    break;
            }
        }

        public void SetFrame()
        {
            switch (state)
            {
                case GhostState.Idle:
                    this.sprite.SetLowerLeftPixel(frame * spriteWidth, spriteHeight);
                    break;
                case GhostState.Attacking:
                    this.sprite.SetLowerLeftPixel(frame * spriteWidth, 2 * spriteHeight);
                    break;
                case GhostState.Taunting:
                    this.sprite.SetLowerLeftPixel(frame * spriteWidth, 3 * spriteHeight);
                    break;
                case GhostState.Resurrecting:
                    this.sprite.SetLowerLeftPixel(frame * spriteWidth, 4 * spriteHeight);
                    break;
                case GhostState.Reviving:
                    this.sprite.SetLowerLeftPixel(frame * spriteWidth, spriteHeight);
                    break;
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

        public void TryToAttack()
        {
            if (player.Lives <= 0)
            {
                // Disable attacking
                return;
            }
            TestVanDammeAnim character = GetNextClosestUnit(playerNum, 15f, 10f, this.transform.position.x, this.transform.position.y);
            if ( character != null )
            {
                characterToPossess = character;
                characterToPossess.name = "p";
                this.transform.localScale = new Vector3( Mathf.Sign(characterToPossess.X - this.transform.position.x), 1f, 1f);
                this.frozenPosition = characterToPossess.transform.position;
                this.transform.position = new Vector3(characterToPossess.X - base.transform.localScale.x * 11f, characterToPossess.Y + characterToPossess.height + 3f, 0f);
                this.state = GhostState.Attacking;
                this.frame = 0;
                this.xI = 0;
                this.yI = 0;
            }
        }

        public void FinishAttack()
        {
            // Make sure character is alive before finish possession
            if (characterToPossess != null && characterToPossess.IsAlive() && characterToPossess.health > 0)
            {
                Main.StartControllingUnit(playerNum, characterToPossess, false, true, false);
                characterToPossess = null;
                this.state = GhostState.Idle;
                this.gameObject.SetActive(false);
            }
            else
            {
                if ( this.characterToPossess != null )
                {
                    this.characterToPossess.name = "enemy";
                }
                this.characterToPossess = null;
                this.state = GhostState.Idle;
                this.frame = 0;
                SetFrame();
            }
        }

        public void SetCanReviveCharacter()
        {
            this.state = GhostState.Idle;
            if ( this.characterToPossess != null )
            {
                this.characterToPossess.name = "enemy";
                this.characterToPossess = null;
            }
            ableToRevive = true;
            forceReviveTime = 1.5f;
            startReviveFlashTime = Time.time;
            sprite.SetColor(new Color(reviveColor.r, reviveColor.g, reviveColor.b, currentTransparency));
        }

        public void StartReviving()
        {
            this.ableToRevive = false;
            this.state = GhostState.Reviving;
            this.frame = 0;
            // Restore original tint
            this.sprite.GetComponent<Renderer>().material.SetColor("_TintColor", Color.gray);
        }

        public void ReviveCharacter()
        {
            this.player.character.transform.position = this.transform.position;
            this.player.character.SetXY(this.transform.position.x, this.transform.position.y - 15f);
            this.player.character.transform.localScale = this.transform.localScale;
            this.gameObject.SetActive(false);
            this.player.character.xI = this.xI;
            this.player.character.yI = this.yI;
            this.player.character.gameObject.SetActive(true);
            Main.SwitchToHeroAvatar(playerNum);
            this.state = GhostState.Idle;
            this.currentTransparency = finalTransparency;
            this.sprite.SetColor(new Color(playerColor.r, playerColor.g, playerColor.b, currentTransparency));
        }

        public void StartResurrecting()
        {
            this.frame = 0;
            this.state = GhostState.Resurrecting;
            this.SetFrame();
        }

        public void ReActivate()
        {
            if (HeroController.players[playerNum].Lives <= 0 )
            {
                this.playerColor = new Color(1f, 1f, 1f, 1f);
                this.sprite.SetColor(new Color(playerColor.r, playerColor.g, playerColor.b, currentTransparency));
            }
            this.gameObject.SetActive(true);
        }
    }
}
