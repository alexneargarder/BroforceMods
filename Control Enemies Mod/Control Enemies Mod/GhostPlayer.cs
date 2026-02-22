using System;
using System.IO;
using System.Reflection;
using Rewired;
using RocketLib.Utils;
using UnityEngine;

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
        protected bool ableToRevive;
        protected float forceReviveTime;
        protected float startReviveFlashTime;
        public const float ghostSpawnOffset = 16f;

        // Animation
        public SpriteSM sprite;
        public float currentTransparency;
        protected const float finalTransparency = 0.75f;
        public Color playerColor = new Color( 1f, 1f, 1f );
        public int frame;
        protected float frameCounter;
        protected const float idleFramerate = 0.11f;
        protected float attackingFramerate = 0.08f;
        protected float dancingFramerate = 0.11f;
        protected float resurrectingFramerate = 0.11f;
        protected float reviveFramerate = 0.11f;
        protected const float spriteWidth = 32f;
        protected const float spriteHeight = 32f;

        protected Color reviveColor = new Color( 1f, 0.8431f, 0f );

        //protected Color[] playerColors = new Color[] { new Color(0.15f, 0.47f, 0.92f), new Color(1f, 0.17f, 0.17f), new Color(1f, 0.64f, 0f), new Color(0.56f, 0f, 1f) };
        public static Color[] playerColors = { new Color( 0f, 0.5f, 1f ), new Color( 1f, 0f, 0f ), new Color( 1f, 0.45f, 0f ), new Color( 0.55f, 0f, 1f ) };
        public static Color burningColor = new Color( 0.53f, 0.05f, 0.15f );

        // Movement
        public bool up, left, down, right, fire, buttonJump, special, highfive, buttonGesture, sprint;
        protected float yI, xI;
        protected const float normalSpeed = 130f;
        protected const float sprintSpeed = 180f;
        protected const float accelerationFactor = 3.5f;
        protected const float decelerationFactor = 1.5f;
        protected float screenMinX, screenMaxX, screenMinY, screenMaxY;
        protected bool usingController;
        protected int controllerNum;

        // Possession
        TestVanDammeAnim characterToPossess;
        Vector3 frozenPosition = Vector3.zero;
        float frozenXI, frozenYI;

        public void Setup()
        {
            try
            {
                MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

                string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
                directoryPath = Path.Combine( directoryPath, "sprites" );

                Material material = ResourcesController.GetMaterial( directoryPath, "ghostSprite.png" );

                renderer.material = material;

                sprite = gameObject.GetComponent<SpriteSM>();
                sprite.SetTextureDefaults();
                sprite.SetSize( 32, 32 );
                sprite.lowerLeftPixel = new Vector2( 0, 32 );
                sprite.pixelDimensions = new Vector2( 32, 32 );
                sprite.plane = SpriteBase.SPRITE_PLANE.XY;
                sprite.width = 32;
                sprite.height = 32;
                sprite.color = new Color( playerColor.r, playerColor.g, playerColor.b, currentTransparency );
                sprite.CalcUVs();
                sprite.UpdateUVs();
                sprite.offset = new Vector3( 0f, 0f, 0f );

                gameObject.layer = 28;
            }
            catch ( Exception ex )
            {
                Main.Log( "Exception creating ghost: " + ex.ToString() );
            }
        }

        public virtual void Start()
        {
            player = HeroController.players[playerNum];
            playerColor = playerColors[playerNum];

            // Change red player on burning levels to an easier to see color
            if ( Main.isBurning && playerNum == 1 )
            {
                playerColor = burningColor;
            }

            if ( player.controllerNum > 3 )
            {
                usingController = true;
                controllerNum = player.controllerNum - 4;
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
                if ( Mathf.Abs( SortOfFollow.GetScreenMinX() - start.x ) < 100 )
                {
                    start.y += 40;
                    start.x = SortOfFollow.GetScreenMinX() + 25 + ( ( SortOfFollow.GetScreenMaxX() - SortOfFollow.GetScreenMinX() - 50 ) / 4 ) * ( playerNum + 1 );
                }
                // Spawning away from start
                else
                {
                    start.y = SortOfFollow.GetScreenMinY() + 60 + ( SortOfFollow.GetScreenMaxY() - SortOfFollow.GetScreenMinY() ) / 2f;
                    start.x = SortOfFollow.GetScreenMinX() + 25 + ( ( SortOfFollow.GetScreenMaxX() - SortOfFollow.GetScreenMinX() - 50 ) / 4 ) * ( playerNum + 1 );
                }
            }
            // Use overidden spawn point
            else
            {
                start = overrideSpawnPoint;
                start.y += ghostSpawnOffset;
            }

            transform.position = start;
        }

        public virtual void Update()
        {
            if ( spawnDelay > 0f )
            {
                spawnDelay -= Time.deltaTime;
                if ( spawnDelay <= 0 )
                {
                    SetSpawn();
                }

                return;
            }

            HandleTransparency();

            HandleInput();

            ConstrainToScreen();

            ChangeFrame();

            // Countdown to player being forced to revive
            if ( ableToRevive && state != GhostState.Reviving )
            {
                forceReviveTime -= Time.deltaTime;
                if ( forceReviveTime <= 0 )
                {
                    StartReviving();
                }
                // Flash player
                else
                {
                    float num = 0.5f + Mathf.Sin( ( Time.time - startReviveFlashTime ) * 15f ) * 0.23f;
                    Color color = new Color( num, num, num, 1f );
                    sprite.GetComponent<Renderer>().material.SetColor( "_TintColor", color );
                }
            }

            // Make sure enemy we are attacking doesn't die
            if ( state == GhostState.Attacking )
            {
                if ( characterToPossess == null || !characterToPossess.IsAlive() || characterToPossess.health <= 0 )
                {
                    if ( characterToPossess != null )
                    {
                        characterToPossess.name = "enemy";
                    }

                    characterToPossess = null;
                    state = GhostState.Idle;
                    frame = 0;
                    SetFrame();
                }
            }
        }

        public virtual void LateUpdate()
        {
            if ( characterToPossess != null )
            {
                characterToPossess.X = frozenPosition.x;
                characterToPossess.Y = frozenPosition.y;
                characterToPossess.xI = frozenXI;
                characterToPossess.yI = frozenYI;
            }
        }

        public void HandleTransparency()
        {
            if ( currentTransparency < finalTransparency )
            {
                currentTransparency += Time.deltaTime / 1.5f;

                if ( currentTransparency > finalTransparency )
                {
                    currentTransparency = finalTransparency;
                }

                sprite.SetColor( new Color( playerColor.r, playerColor.g, playerColor.b, currentTransparency ) );
            }
        }

        public void HandleInput()
        {
            player.GetInput( ref up, ref down, ref left, ref right, ref fire, ref buttonJump, ref special, ref highfive, ref buttonGesture, ref sprint );

            if ( state == GhostState.Idle || state == GhostState.Reviving )
            {
                // Check sprint manually since it's not detected by GetInput
                sprint = InputReader.GetDashStart( player.controllerNum );

                // Use actual axes rather than just cardinal directions
                if ( usingController )
                {
                    Rewired.Player player = ReInput.players.GetPlayer( controllerNum );

                    float upAmount = player.GetAxis( "Up" ) - player.GetAxis( "Down" );
                    float rightAmount = player.GetAxis( "Right" ) - player.GetAxis( "Left" );

                    float speed = sprint ? sprintSpeed : normalSpeed;

                    if ( Mathf.Abs( yI ) < Mathf.Abs( upAmount * speed ) )
                    {
                        yI += upAmount * speed * Time.deltaTime * accelerationFactor;
                    }

                    if ( yI > speed )
                    {
                        yI = speed;
                    }
                    else if ( yI < -speed )
                    {
                        yI = -speed;
                    }

                    if ( Mathf.Abs( xI ) < Mathf.Abs( rightAmount * speed ) )
                    {
                        xI += rightAmount * speed * Time.deltaTime * accelerationFactor;
                    }

                    if ( xI > speed )
                    {
                        xI = speed;
                    }
                    else if ( xI < -speed )
                    {
                        xI = -speed;
                    }

                    // Slow vertical momentum
                    if ( !( up || down ) )
                    {
                        if ( yI > 0 )
                        {
                            yI -= speed * Time.deltaTime * decelerationFactor;
                            if ( yI < 0 )
                            {
                                yI = 0;
                            }
                        }
                        else if ( yI < 0 )
                        {
                            yI += speed * Time.deltaTime * decelerationFactor;
                            if ( yI > 0 )
                            {
                                yI = 0;
                            }
                        }
                    }

                    // Go right
                    if ( right )
                    {
                        if ( transform.localScale.x != 1 )
                        {
                            transform.localScale = new Vector3( 1f, 1f, 1f );
                        }
                    }
                    // Go left
                    else if ( left )
                    {
                        if ( transform.localScale.x != -1 )
                        {
                            transform.localScale = new Vector3( -1f, 1f, 1f );
                        }
                    }
                    // Slow horizontal momentum
                    else
                    {
                        if ( xI > 0 )
                        {
                            xI -= speed * Time.deltaTime * decelerationFactor;
                            if ( xI < 0 )
                            {
                                xI = 0;
                            }
                        }
                        else if ( xI < 0 )
                        {
                            xI += speed * Time.deltaTime * decelerationFactor;
                            if ( xI > 0 )
                            {
                                xI = 0;
                            }
                        }
                    }
                }
                else
                {
                    float speed = sprint ? sprintSpeed : normalSpeed;

                    // Go up
                    if ( up )
                    {
                        yI += speed * Time.deltaTime * accelerationFactor;
                        if ( yI > speed )
                        {
                            yI = speed;
                        }
                    }
                    // Go down
                    else if ( down )
                    {
                        yI -= speed * Time.deltaTime * accelerationFactor;
                        if ( yI < -speed )
                        {
                            yI = -speed;
                        }
                    }
                    // Slow vertical momentum
                    else
                    {
                        if ( yI > 0 )
                        {
                            yI -= speed * Time.deltaTime * decelerationFactor;
                            if ( yI < 0 )
                            {
                                yI = 0;
                            }
                        }
                        else if ( yI < 0 )
                        {
                            yI += speed * Time.deltaTime * decelerationFactor;
                            if ( yI > 0 )
                            {
                                yI = 0;
                            }
                        }
                    }

                    // Go right
                    if ( right )
                    {
                        if ( transform.localScale.x != 1 )
                        {
                            transform.localScale = new Vector3( 1f, 1f, 1f );
                        }

                        xI += speed * Time.deltaTime * accelerationFactor;
                        if ( xI > speed )
                        {
                            xI = speed;
                        }
                    }
                    // Go left
                    else if ( left )
                    {
                        if ( transform.localScale.x != -1 )
                        {
                            transform.localScale = new Vector3( -1f, 1f, 1f );
                        }

                        xI -= speed * Time.deltaTime * accelerationFactor;
                        if ( xI < -speed )
                        {
                            xI = -speed;
                        }
                    }
                    // Slow horizontal momentum
                    else
                    {
                        if ( xI > 0 )
                        {
                            xI -= speed * Time.deltaTime * decelerationFactor;
                            if ( xI < 0 )
                            {
                                xI = 0;
                            }
                        }
                        else if ( xI < 0 )
                        {
                            xI += speed * Time.deltaTime * decelerationFactor;
                            if ( xI > 0 )
                            {
                                xI = 0;
                            }
                        }
                    }
                }

                Vector3 position = transform.position;
                position.x += ( xI * Time.deltaTime );
                position.y += ( yI * Time.deltaTime );

                transform.position = position;

                // Keep player next to ghost to make camera work
                if ( state == GhostState.Reviving || ableToRevive )
                {
                    player.character.SetXY( position.x, position.y );
                }

                // Don't check inputs if we're reviving
                if ( state != GhostState.Reviving )
                {
                    // Try to find enemy to attack
                    if ( fire )
                    {
                        if ( !ableToRevive )
                        {
                            TryToAttack();
                            HeroController.SetAvatarAngry( playerNum, true );
                        }
                        // Start reviving
                        else
                        {
                            StartReviving();
                        }
                    }
                    else
                    {
                        HeroController.SetAvatarCalm( playerNum, true );
                    }

                    // Start dancing
                    if ( buttonGesture && state != GhostState.Attacking )
                    {
                        frame = 0;
                        state = GhostState.Taunting;
                    }
                }
            }
            else if ( state == GhostState.Taunting )
            {
                // Check facing
                if ( right )
                {
                    if ( transform.localScale.x != 1 )
                    {
                        transform.localScale = new Vector3( 1f, 1f, 1f );
                    }
                }
                else if ( left )
                {
                    if ( transform.localScale.x != -1 )
                    {
                        transform.localScale = new Vector3( -1f, 1f, 1f );
                    }
                }

                float speed = sprint ? sprintSpeed : normalSpeed;

                // Slow Down
                if ( yI > 0 )
                {
                    yI -= speed * Time.deltaTime * decelerationFactor;
                    if ( yI < 0 )
                    {
                        yI = 0;
                    }
                }
                else if ( yI < 0 )
                {
                    yI += speed * Time.deltaTime * decelerationFactor;
                    if ( yI > 0 )
                    {
                        yI = 0;
                    }
                }

                if ( xI > 0 )
                {
                    xI -= speed * Time.deltaTime * decelerationFactor;
                    if ( xI < 0 )
                    {
                        xI = 0;
                    }
                }
                else if ( xI < 0 )
                {
                    xI += speed * Time.deltaTime * decelerationFactor;
                    if ( xI > 0 )
                    {
                        xI = 0;
                    }
                }

                if ( !buttonGesture )
                {
                    frame = 0;
                    state = GhostState.Idle;
                }
            }
        }

        public void ConstrainToScreen()
        {
            SetResolutionCamera.GetScreenExtents( ref screenMinX, ref screenMaxX, ref screenMinY, ref screenMaxY );

            Vector3 position = transform.position;

            if ( position.x < screenMinX + 5f )
            {
                position.x = screenMinX + 5f;
            }
            else if ( position.x > screenMaxX - 5f )
            {
                position.x = screenMaxX - 5f;
            }

            if ( position.y < screenMinY + 10f )
            {
                position.y = screenMinY + 10f;
            }
            else if ( position.y > screenMaxY - 10f )
            {
                position.y = screenMaxY - 10f;
            }

            transform.position = position;
        }

        public void ChangeFrame()
        {
            frameCounter += Time.deltaTime;

            switch ( state )
            {
                case GhostState.Idle:
                    if ( frameCounter > idleFramerate )
                    {
                        frameCounter -= idleFramerate;

                        ++frame;

                        if ( frame > 7 )
                        {
                            frame = 0;
                        }

                        sprite.SetLowerLeftPixel( frame * spriteWidth, spriteHeight );
                    }

                    break;
                case GhostState.Attacking:
                    if ( frameCounter > attackingFramerate )
                    {
                        frameCounter -= attackingFramerate;

                        ++frame;

                        if ( frame > 13 )
                        {
                            frame = 0;
                            FinishAttack();
                        }

                        sprite.SetLowerLeftPixel( frame * spriteWidth, 2 * spriteHeight );
                    }

                    break;
                case GhostState.Taunting:
                    if ( frameCounter > dancingFramerate )
                    {
                        frameCounter -= dancingFramerate;

                        ++frame;

                        if ( frame > 21 )
                        {
                            frame = 4;
                        }

                        sprite.SetLowerLeftPixel( frame * spriteWidth, 3 * spriteHeight );
                    }

                    break;
                case GhostState.Resurrecting:
                    if ( frameCounter > resurrectingFramerate )
                    {
                        frameCounter -= resurrectingFramerate;

                        ++frame;

                        if ( frame > 15 )
                        {
                            frame = 0;
                            state = GhostState.Idle;
                            sprite.SetLowerLeftPixel( frame * spriteWidth, spriteHeight );
                            return;
                        }

                        sprite.SetLowerLeftPixel( frame * spriteWidth, 4 * spriteHeight );
                    }

                    break;
                case GhostState.Reviving:
                    if ( frameCounter > reviveFramerate )
                    {
                        frameCounter -= reviveFramerate;

                        ++frame;

                        if ( frame > 14 )
                        {
                            ReviveCharacter();
                            frame = 0;
                        }

                        sprite.SetLowerLeftPixel( frame * spriteWidth, 5 * spriteHeight );
                    }

                    break;
            }
        }

        public void SetFrame()
        {
            switch ( state )
            {
                case GhostState.Idle:
                    sprite.SetLowerLeftPixel( frame * spriteWidth, spriteHeight );
                    break;
                case GhostState.Attacking:
                    sprite.SetLowerLeftPixel( frame * spriteWidth, 2 * spriteHeight );
                    break;
                case GhostState.Taunting:
                    sprite.SetLowerLeftPixel( frame * spriteWidth, 3 * spriteHeight );
                    break;
                case GhostState.Resurrecting:
                    sprite.SetLowerLeftPixel( frame * spriteWidth, 4 * spriteHeight );
                    break;
                case GhostState.Reviving:
                    sprite.SetLowerLeftPixel( frame * spriteWidth, spriteHeight );
                    break;
            }
        }

        public static TestVanDammeAnim GetNextClosestUnit( int playerNum, float xRange, float yRange, float x, float y )
        {
            if ( Map.units == null )
            {
                return null;
            }

            float num = xRange;
            TestVanDammeAnim unit = null;
            for ( int i = Map.units.Count - 1; i >= 0; i-- )
            {
                TestVanDammeAnim unit2 = Map.units[i] as TestVanDammeAnim;
                if ( unit2 != null && Main.AvailableToPossess( unit2 ) )
                {
                    float num2 = unit2.Y + unit2.height / 2f + 3f - y;
                    if ( Mathf.Abs( num2 ) - yRange < unit2.height )
                    {
                        float num3 = unit2.X - x;
                        if ( Mathf.Abs( num3 ) - num < unit2.width )
                        {
                            unit = unit2;
                            num = Mathf.Abs( num2 );
                        }
                    }
                }
            }

            if ( unit != null )
            {
                return unit;
            }

            return null;
        }

        public void TryToAttack()
        {
            if ( player.Lives <= 0 )
            {
                // Disable attacking
                return;
            }

            TestVanDammeAnim character = GetNextClosestUnit( playerNum, 15f, 10f, transform.position.x, transform.position.y );
            if ( character != null )
            {
                characterToPossess = character;
                characterToPossess.name = "p";
                transform.localScale = new Vector3( Mathf.Sign( characterToPossess.X - transform.position.x ), 1f, 1f );
                frozenPosition = characterToPossess.transform.position;
                frozenXI = characterToPossess.xI;
                frozenYI = characterToPossess.yI;
                transform.position = new Vector3( characterToPossess.X - transform.localScale.x * 11f, characterToPossess.Y + characterToPossess.height + 3f, 0f );
                state = GhostState.Attacking;
                frame = 0;
                xI = 0;
                yI = 0;
            }
        }

        public void FinishAttack()
        {
            // Make sure character is alive before finish possession
            if ( characterToPossess != null && characterToPossess.IsAlive() && characterToPossess.health > 0 )
            {
                Main.StartControllingUnit( playerNum, characterToPossess );
                characterToPossess = null;
                state = GhostState.Idle;
                gameObject.SetActive( false );
            }
            else
            {
                if ( characterToPossess != null )
                {
                    characterToPossess.name = "enemy";
                }

                characterToPossess = null;
                state = GhostState.Idle;
                frame = 0;
                SetFrame();
            }
        }

        public void SetCanReviveCharacter()
        {
            state = GhostState.Idle;
            if ( characterToPossess != null )
            {
                characterToPossess.name = "enemy";
                characterToPossess = null;
            }

            ableToRevive = true;
            forceReviveTime = 1.5f;
            startReviveFlashTime = Time.time;
            sprite.SetColor( new Color( reviveColor.r, reviveColor.g, reviveColor.b, currentTransparency ) );
        }

        public void StartReviving()
        {
            ableToRevive = false;
            state = GhostState.Reviving;
            frame = 0;
            // Restore original tint
            sprite.GetComponent<Renderer>().material.SetColor( "_TintColor", Color.gray );
        }

        public void ReviveCharacter()
        {
            player.character.transform.position = transform.position;
            player.character.SetXY( transform.position.x, transform.position.y - 15f );
            player.character.transform.localScale = transform.localScale;
            gameObject.SetActive( false );
            player.character.xI = xI;
            player.character.yI = yI;
            player.character.gameObject.SetActive( true );
            Main.SwitchToHeroAvatar( playerNum );
            state = GhostState.Idle;
            currentTransparency = finalTransparency;
            sprite.SetColor( new Color( playerColor.r, playerColor.g, playerColor.b, currentTransparency ) );
        }

        public void StartResurrecting()
        {
            frame = 0;
            state = GhostState.Resurrecting;
            SetFrame();
        }

        public void ReActivate()
        {
            if ( HeroController.players[playerNum].Lives <= 0 )
            {
                playerColor = new Color( 1f, 1f, 1f, 1f );
                sprite.SetColor( new Color( playerColor.r, playerColor.g, playerColor.b, currentTransparency ) );
            }

            gameObject.SetActive( true );
        }
    }
}