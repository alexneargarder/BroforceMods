using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Drunken_Broster
{
    public static class DrunkenCameraManager
    {
        private static readonly List<DrunkenBroster> drunkBros = new List<DrunkenBroster>();

        private static float cameraTiltTime;
        private static float currentTilt;
        private static float targetTilt;
        private static float currentMaxTilt = 5f;
        private static float currentTiltSpeed = 1.5f;
        private static bool isSoberingUp;

        private static Vector3 originalCameraRotation;
        private static bool hasSavedRotation;

        private static float lastUpdateTime = -1f;

        private const float BASE_MAX_TILT = 5f;
        private const float BASE_TILT_SPEED = 1.5f;
        private const float ATTACK_TILT_MULTIPLIER = 1.8f;
        private const float ATTACK_SPEED_MULTIPLIER = 1.3f;
        private const float MULTIPLAYER_BOOST_PER_BRO = 0.3f;

        public static void RegisterDrunk( DrunkenBroster bro )
        {
            if ( !drunkBros.Contains( bro ) )
            {
                drunkBros.Add( bro );

                // Save original camera rotation on first drunk bro
                if ( !hasSavedRotation && CameraController.MainCam )
                {
                    originalCameraRotation = CameraController.MainCam.transform.eulerAngles;
                    hasSavedRotation = true;
                }

                // If this is the first drunk bro, reset tilt state
                if ( drunkBros.Count == 1 )
                {
                    cameraTiltTime = 0f;
                    currentTilt = 0f;
                    currentMaxTilt = BASE_MAX_TILT;
                    currentTiltSpeed = BASE_TILT_SPEED;
                    isSoberingUp = false;
                }
            }
        }

        public static void UnregisterDrunk( DrunkenBroster bro, bool immediateReset = false )
        {
            drunkBros.Remove( bro );

            // If no drunk bros remain, start sobering animation
            if ( drunkBros.Count == 0 )
            {
                // If immediate reset is true then it indicates we should immediately reset the camera tilt if no drunken bros are left
                if ( immediateReset )
                {
                    ResetCamera();
                }
                else
                {
                    isSoberingUp = true;
                }
            }
        }

        public static void UpdateCameraTilt()
        {
            // Only update once per frame
            if ( Time.time == lastUpdateTime ) return;
            lastUpdateTime = Time.time;

            // Check if we should be tilting
            if ( !DrunkenBroster.enableCameraTilt || CameraController.MainCam == null )
            {
                if ( hasSavedRotation && CameraController.MainCam != null )
                {
                    CameraController.MainCam.transform.eulerAngles = originalCameraRotation;
                }
                return;
            }

            // If no drunk bros and not sobering, nothing to do
            if ( drunkBros.Count == 0 && !isSoberingUp )
            {
                return;
            }

            // Clean up any null references (destroyed bros)
            drunkBros.RemoveAll( b => b == null );

            // Check if level is finished or any drunk bro is on helicopter, if so reset camera
            if ( GameModeController.LevelFinished || drunkBros.Any( b => b.isOnHelicopter ) )
            {
                ResetCamera();
                return;
            }

            float deltaTime = Time.deltaTime;

            // Calculate intensity based on settings
            float intensityModifier = DrunkenBroster.lowIntensityMode ? 0.5f : 1f;

            // Calculate multiplayer boost (disabled in low intensity mode)
            float multiplayerBoost = 1f;
            if ( !DrunkenBroster.lowIntensityMode && drunkBros.Count > 1 )
            {
                multiplayerBoost = 1f + ( drunkBros.Count - 1 ) * MULTIPLAYER_BOOST_PER_BRO;
            }

            // Check if any drunk bro is attacking (disabled in low intensity mode)
            bool anyAttacking = false;
            if ( !DrunkenBroster.lowIntensityMode )
            {
                anyAttacking = drunkBros.Any( b => b.IsDoingMelee || b.attackForwards || b.attackUpwards || b.attackDownwards );
            }

            // Smoothly adjust tilt parameters
            if ( anyAttacking && drunkBros.Count > 0 )
            {
                currentMaxTilt = Mathf.Lerp( currentMaxTilt, BASE_MAX_TILT * ATTACK_TILT_MULTIPLIER * multiplayerBoost, deltaTime * 5f );
                currentTiltSpeed = Mathf.Lerp( currentTiltSpeed, BASE_TILT_SPEED * ATTACK_SPEED_MULTIPLIER * multiplayerBoost, deltaTime * 5f );
            }
            else
            {
                float targetMaxTilt = isSoberingUp ? 0f : BASE_MAX_TILT * intensityModifier * multiplayerBoost;
                float targetSpeed = isSoberingUp ? 0.5f : BASE_TILT_SPEED * intensityModifier * multiplayerBoost;

                currentMaxTilt = Mathf.Lerp( currentMaxTilt, targetMaxTilt, deltaTime * 3f );
                currentTiltSpeed = Mathf.Lerp( currentTiltSpeed, targetSpeed, deltaTime * 3f );
            }

            // Calculate target tilt
            cameraTiltTime += deltaTime * currentTiltSpeed;
            targetTilt = Mathf.Sin( cameraTiltTime ) * currentMaxTilt;

            // Smoothly interpolate to target
            currentTilt = Mathf.Lerp( currentTilt, targetTilt, deltaTime * 4f );

            // Apply the tilt to camera
            if ( CameraController.MainCam != null )
            {
                Vector3 currentRotation = CameraController.MainCam.transform.eulerAngles;
                CameraController.MainCam.transform.eulerAngles = new Vector3( currentRotation.x, currentRotation.y, currentTilt );
            }

            // Check if sobering is complete
            if ( isSoberingUp && Mathf.Abs( currentTilt ) < 0.1f )
            {
                isSoberingUp = false;
                currentTilt = 0f;
                if ( CameraController.MainCam != null && hasSavedRotation )
                {
                    CameraController.MainCam.transform.eulerAngles = originalCameraRotation;
                    hasSavedRotation = false;
                }
            }
        }

        public static void ResetCamera()
        {
            if ( CameraController.MainCam != null && hasSavedRotation )
            {
                CameraController.MainCam.transform.eulerAngles = originalCameraRotation;
            }

            drunkBros.Clear();
            cameraTiltTime = 0f;
            currentTilt = 0f;
            targetTilt = 0f;
            isSoberingUp = false;
            hasSavedRotation = false;
        }

        public static void OnSettingsChanged()
        {
            if ( !DrunkenBroster.enableCameraTilt )
            {
                ResetCamera();
            }
        }
    }
}