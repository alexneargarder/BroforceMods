using System;
using System.IO;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public static class ScreenshotCapture
    {
        private static readonly string screenshotDirectory;
        private static readonly bool isLinux;

        static ScreenshotCapture()
        {
            isLinux = Application.platform == RuntimePlatform.LinuxPlayer;
            screenshotDirectory = Path.Combine( Main.mod.Path, "Screenshots" );

            if ( !Directory.Exists( screenshotDirectory ) )
            {
                Directory.CreateDirectory( screenshotDirectory );
            }
        }

        public static string TakeScreenshot()
        {
            try
            {
                var timestamp = DateTime.Now.ToString( "yyyyMMdd_HHmmss" );
                var randomId = Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
                var filename = $"screenshot_{timestamp}_{randomId}.png";

                ManageScreenshotHistory();

                var path = Path.Combine( screenshotDirectory, filename );

                UnityEngine.ScreenCapture.CaptureScreenshot( path );

                System.Threading.Thread.Sleep( 100 );

                if ( !File.Exists( path ) )
                {
                    Main.Log( "Screenshot file not found after capture - it may be saving asynchronously" );
                }

                if ( isLinux )
                {
                    return path;
                }

                return path.Replace( '\\', '/' );
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