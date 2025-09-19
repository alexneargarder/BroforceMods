using System;
using System.IO;
using UnityEngine;
using System.Collections;

namespace Unity_Inspector_Mod
{
    public static class ScreenshotCapture
    {
        private static readonly string screenshotDirectory;
        private static bool isCapturing = false;

        static ScreenshotCapture()
        {
            var tempPath = Path.Combine(
                Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ),
                "Temp"
            );
            screenshotDirectory = Path.Combine( tempPath, "unity-inspector" );

            if ( !Directory.Exists( screenshotDirectory ) )
            {
                Directory.CreateDirectory( screenshotDirectory );
            }
        }

        public static string TakeScreenshot()
        {
            try
            {
                // Generate random filename
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var randomId = Guid.NewGuid().ToString("N").Substring(0, 8);
                var filename = $"screenshot_{timestamp}_{randomId}.png";

                ManageScreenshotHistory();

                var path = Path.Combine( screenshotDirectory, filename );
                
                // Use Unity's built-in screenshot method
                UnityEngine.ScreenCapture.CaptureScreenshot( path );
                
                // Wait a moment for the file to be written
                System.Threading.Thread.Sleep( 100 );
                
                if ( !File.Exists( path ) )
                {
                    Main.Log( "Screenshot file not found after capture - it may be saving asynchronously" );
                }

                var wslPath = path.Replace( '\\', '/' ).Replace( "C:", "/mnt/c" );
                return wslPath;
            }
            catch ( Exception ex )
            {
                Main.Log( $"Failed to take screenshot: {ex.Message}" );
                throw;
            }
        }

        private static void ManageScreenshotHistory()
        {
            try
            {
                var latestPath = Path.Combine( screenshotDirectory, "latest.png" );

                if ( File.Exists( latestPath ) )
                {
                    for ( int i = 4; i >= 1; i-- )
                    {
                        var oldPath = Path.Combine( screenshotDirectory, $"latest-{i}.png" );
                        var newPath = Path.Combine( screenshotDirectory, $"latest-{i + 1}.png" );

                        if ( File.Exists( newPath ) )
                        {
                            File.Delete( newPath );
                        }

                        if ( File.Exists( oldPath ) )
                        {
                            File.Move( oldPath, newPath );
                        }
                    }

                    File.Move( latestPath, Path.Combine( screenshotDirectory, "latest-1.png" ) );
                }

                var oldestPath = Path.Combine( screenshotDirectory, "latest-6.png" );
                if ( File.Exists( oldestPath ) )
                {
                    File.Delete( oldestPath );
                }
            }
            catch ( Exception ex )
            {
                Main.Log( $"Error managing screenshot history: {ex.Message}" );
            }
        }
    }
}