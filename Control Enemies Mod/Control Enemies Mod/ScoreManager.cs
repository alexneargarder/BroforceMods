using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using RocketLib.Utils;
using Rewired.ComponentControls.Data;
using System.Security.Policy;

namespace Control_Enemies_Mod
{
    public static class ScoreManager
    {
        public static SpriteSM scorePrefab = null;
        public static List<List<SpriteSM>> scoreSprites = new List<List<SpriteSM>>() { new List<SpriteSM>() { null, null, null, null } };
        public static bool[] spriteSetup = new bool[] { false, false, false, false };
        public static int[] currentScore = new int[] { 0, 0, 0, 0 };
        public const float spriteWidth = 32f;
        public const float spriteHeight = 32f;

        public static void LoadSprites()
        {
            scorePrefab = new GameObject("ControlEnemiesModScore", new Type[] { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(SpriteSM) }).GetComponent<SpriteSM>();
            scorePrefab.gameObject.SetActive(false);

            MeshRenderer renderer = scorePrefab.gameObject.GetComponent<MeshRenderer>();
            string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            directoryPath = Path.Combine(directoryPath, "sprites");
            Material material = ResourcesController.GetMaterial(directoryPath, "tallyMarks.png");
            renderer.material = material;

            scorePrefab.SetTextureDefaults();
            scorePrefab.SetSize(spriteWidth, spriteHeight);
            scorePrefab.lowerLeftPixel = new Vector2(0, spriteHeight);
            scorePrefab.pixelDimensions = new Vector2(spriteWidth, spriteHeight);
            scorePrefab.plane = SpriteBase.SPRITE_PLANE.XY;
            scorePrefab.width = spriteWidth;
            scorePrefab.height = spriteHeight;
            scorePrefab.CalcUVs();
            scorePrefab.UpdateUVs();
            scorePrefab.offset = new Vector3(0f, 0f, 0f);
            
            UnityEngine.Object.DontDestroyOnLoad(scorePrefab);
        }

        public static void SetupSprites(int playerNum)
        {
            if (!spriteSetup[playerNum])
            {
                int rows = (int)Mathf.Ceil(currentScore[playerNum] / 5);
                if ( rows == 0 )
                {
                    rows = 1;
                }
                for ( int i = 0; i < rows; ++i )
                {
                    SetupSprites(playerNum, i);
                }
            }
        }

        private static void SetupSprites(int playerNum, int row)
        {
            // Add another row
            if (scoreSprites.Count < row + 1)
            {
                scoreSprites.Add(new List<SpriteSM>() { null, null, null, null });
            }

            PlayerHUD hud = HeroController.players[playerNum].hud;

            SpriteSM sprite = UnityEngine.Object.Instantiate<SpriteSM>(scorePrefab, Vector3.zero, Quaternion.identity).GetComponent<SpriteSM>();
            scoreSprites[row][playerNum] = sprite;
            sprite.transform.parent = hud.transform;
            sprite.gameObject.layer = 17;

            // Even (left players)
            if (playerNum % 2 == 0)
            {
                sprite.transform.localPosition = new Vector3(70f + 12.5f * row, -8f, -2f);
                sprite.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }
            // Odd (right players)
            else
            {
                sprite.transform.localPosition = new Vector3(-(70f + 12.5f * row), -8f, -2f);
                sprite.transform.localScale = new Vector3(-0.5f, 0.5f, 0.5f);
            }

            int scoreForThisRow;
            if (currentScore[playerNum] >= 5 * (row + 1))
            {
                scoreForThisRow = 5;
            }
            else
            {
                scoreForThisRow = currentScore[playerNum] - 5 * row;
            }
            if (scoreForThisRow <= 0)
            {
                // Display 0 if on the first row
                if (row == 0)
                {
                    sprite.SetLowerLeftPixel(5 * spriteWidth, spriteHeight);
                }
            }
            else
            {
                sprite.SetLowerLeftPixel((scoreForThisRow - 1) * spriteWidth, spriteHeight);
            }

            sprite.gameObject.SetActive(true);
            spriteSetup[playerNum] = true;
        }

        public static void UpdateScore(int playerNum)
        {
            int rows = (int)Mathf.Ceil(currentScore[playerNum] / 5f);
            if (rows == 0)
            {
                rows = 1;
            }
            for ( int i = 0; i < rows; ++i )
            {
                // Need to add row or create sprite
                if ( scoreSprites.Count < i + 1 || scoreSprites[i][playerNum] == null)
                {
                    SetupSprites(playerNum, i);
                }
                else
                {
                    int scoreForThisRow;
                    if (currentScore[playerNum] >= 5 * (i + 1))
                    {
                        scoreForThisRow = 5;
                    }
                    else
                    {
                        scoreForThisRow = currentScore[playerNum] - 5 * i;
                    }
                    if (scoreForThisRow <= 0)
                    {
                        // Display 0 if on the first row
                        if (i == 0)
                        {
                            scoreSprites[i][playerNum].SetLowerLeftPixel(5 * spriteWidth, spriteHeight);
                        }
                    }
                    else
                    {
                        scoreSprites[i][playerNum].SetLowerLeftPixel((scoreForThisRow - 1) * spriteWidth, spriteHeight);
                    }
                }
            }
        }

        public static bool CanWin(int playerNum)
        {
            return currentScore[playerNum] >= Main.requiredScore[playerNum];
        }
    }
}
