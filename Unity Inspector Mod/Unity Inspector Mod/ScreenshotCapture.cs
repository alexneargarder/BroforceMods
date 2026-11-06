using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using RocketLib.Utils;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public sealed class ScreenshotResult
    {
        public string Path;
        public object Highlighted;
        public string HighlightError;
    }

    public sealed class ScreenshotBusyException : Exception
    {
        public ScreenshotBusyException( string message ) : base( message ) { }
    }

    public static class ScreenshotCapture
    {
        private const int KeptScreenshots = 50;
        private const int LockWaitMs = 2000;
        private const int CaptureWaitMs = 3000;
        private const int PollIntervalMs = 25;

        private static readonly string screenshotDirectory;
        private static readonly bool isLinux;
        private static readonly object captureLock = new object();
        private static readonly byte[] PngTrailer = { 0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

        static ScreenshotCapture()
        {
            isLinux = Application.platform == RuntimePlatform.LinuxPlayer;
            screenshotDirectory = Path.Combine( Main.mod.Path, "Screenshots" );
        }

        public static ScreenshotResult TakeScreenshot( JToken highlight )
        {
            if ( !System.Threading.Monitor.TryEnter( captureLock, LockWaitMs ) )
            {
                throw new ScreenshotBusyException( $"Another take_screenshot was still capturing after {LockWaitMs}ms - try again" );
            }

            try
            {
                return TakeScreenshotLocked( highlight );
            }
            finally
            {
                System.Threading.Monitor.Exit( captureLock );
            }
        }

        private static ScreenshotResult TakeScreenshotLocked( JToken highlight )
        {
            var result = new ScreenshotResult();

            bool wanted = highlight != null && highlight.Type != JTokenType.Null;
            // Minted before the draw is dispatched because the finally enqueues the removal whatever
            // the draw ended as, and it needs the id to do that. Both actions go through the one FIFO
            // queue, so the removal is always drained after the draw that still holds the line.
            string highlightId = wanted ? "UnityInspectorHighlight_" + Guid.NewGuid().ToString( "N" ) : null;

            try
            {
                if ( wanted )
                {
                    DrawHighlight( highlightId, highlight, result );
                }

                result.Path = Capture();
                return result;
            }
            finally
            {
                if ( highlightId != null )
                {
                    RemoveHighlight( highlightId );
                }
            }
        }

        // Resolving the target and building the LineRenderer are both Unity calls, so the whole of it
        // goes to the main thread; the capture that follows is what the caller's thread waits out.
        private static void DrawHighlight( string id, JToken highlight, ScreenshotResult result )
        {
            // A draw the wait gave up on is abandoned rather than run late, so the action writes the
            // result itself: nothing can land here after the capture and claim an outline the frame
            // never had.
            try
            {
                MainThreadDispatcher.EnqueueAndWait( () =>
                {
                    Bounds bounds;
                    string error = ResolveHighlightBounds( highlight, out bounds );
                    if ( error != null )
                    {
                        result.HighlightError = error;
                        return;
                    }

                    float lineWidth = Mathf.Clamp( Mathf.Min( bounds.size.x, bounds.size.y ) * 0.05f, 0.3f, 2f );
                    DrawDebug.DrawRectangle(
                        id,
                        new Vector3( bounds.min.x, bounds.min.y, bounds.center.z ),
                        new Vector3( bounds.max.x, bounds.max.y, bounds.center.z ),
                        Color.green,
                        lineWidth );

                    result.Highlighted = new
                    {
                        x = bounds.min.x,
                        y = bounds.min.y,
                        width = bounds.size.x,
                        height = bounds.size.y
                    };
                }, 3000 );
            }
            catch ( Exception ex )
            {
                result.HighlightError = $"Could not draw the highlight: {ex.Message}";
            }
        }

        // DrawDebug has no removal call of its own; its ID dictionary is the only handle on the line.
        // Not waited on: the file is already written by now, and waiting would spend the caller's
        // remaining budget on a line that comes down on the next frame either way.
        private static void RemoveHighlight( string id )
        {
            MainThreadDispatcher.Enqueue( () =>
            {
                LineRenderer line;
                if ( !DrawDebug.lines.TryGetValue( id, out line ) )
                {
                    return;
                }

                DrawDebug.lines.Remove( id );
                if ( line != null )
                {
                    UnityEngine.Object.Destroy( line.gameObject );
                }
            } );
        }

        // Runs on the main thread, which is where the camera can be asked what it is showing.
        private static string ResolveHighlightBounds( JToken highlight, out Bounds bounds )
        {
            bounds = new Bounds();
            string subject;

            if ( highlight.Type == JTokenType.String )
            {
                string path = highlight.Value<string>();
                if ( string.IsNullOrEmpty( path ) )
                {
                    return "highlight is an empty GameObject path";
                }

                return HighlightObject( path, null, out bounds, out subject );
            }

            var rectangle = highlight as JObject;
            if ( rectangle == null )
            {
                return "highlight must be a GameObject path, an object with an instanceId, or a rectangle with x, y, width and height";
            }

            int? instanceId;
            try
            {
                instanceId = MessageHandler.ReadInt( rectangle, "instanceId", "highlight instanceId" );
            }
            catch ( CallerErrorException ex )
            {
                return ex.Message;
            }
            if ( instanceId.HasValue )
            {
                return HighlightObject( null, instanceId, out bounds, out subject );
            }

            foreach ( var key in new[] { "x", "y", "width", "height" } )
            {
                var part = rectangle[key];
                if ( part == null || part.Type == JTokenType.Null )
                {
                    return $"highlight rectangle has no '{key}' - it needs x, y, width and height in world units";
                }
                if ( ( part.Type != JTokenType.Integer && part.Type != JTokenType.Float )
                     || float.IsNaN( part.Value<float>() ) || float.IsInfinity( part.Value<float>() ) )
                {
                    return $"highlight rectangle '{key}' must be a finite number in world units, got {part.ToString( Newtonsoft.Json.Formatting.None )}";
                }
            }

            float rx = rectangle["x"].Value<float>();
            float ry = rectangle["y"].Value<float>();
            float rw = rectangle["width"].Value<float>();
            float rh = rectangle["height"].Value<float>();

            bounds = new Bounds(
                new Vector3( rx + rw / 2f, ry + rh / 2f, 0f ),
                new Vector3( Mathf.Abs( rw ), Mathf.Abs( rh ), 0f ) );
            return OnScreenError( bounds, "the highlight rectangle" );
        }

        private static string OnScreenError( Bounds bounds, string subject )
        {
            if ( !IsOnScreen( bounds ) )
            {
                return $"{subject} is off-screen, so no outline would be visible in the capture";
            }

            return null;
        }

        private static string HighlightObject( string path, int? instanceId, out Bounds bounds, out string subject )
        {
            bounds = new Bounds();
            subject = null;

            GameObject go;
            try
            {
                go = GameObjectInspector.Resolve( path, instanceId ).GameObject;
            }
            catch ( ArgumentException ex )
            {
                return ex.Message;
            }

            if ( GameObjectInspector.IsAsset( go ) )
            {
                return $"'{go.name}' is a prefab asset, not an object in a scene, so it is not in the captured frame";
            }

            if ( !go.activeInHierarchy )
            {
                return $"'{go.name}' is inactive, so it is not in the captured frame";
            }

            string error = BoundsOf( go, out bounds );
            if ( error != null )
            {
                return error;
            }

            subject = $"'{go.name}'";
            return OnScreenError( bounds, subject );
        }

        // The camera the game renders the level with; any overlap with its view counts, so an object
        // running off the edge of the screen is still outlined.
        private static bool IsOnScreen( Bounds bounds )
        {
            // Unity's null check is an operator overload, so ?? would take a destroyed camera for a live one.
            Camera camera = CameraController.MainCam;
            if ( camera == null )
            {
                camera = Camera.main;
            }

            if ( camera == null )
            {
                return true;
            }

            Vector3 min = camera.WorldToViewportPoint( bounds.min );
            Vector3 max = camera.WorldToViewportPoint( bounds.max );

            return Mathf.Max( min.x, max.x ) >= 0f
                   && Mathf.Min( min.x, max.x ) <= 1f
                   && Mathf.Max( min.y, max.y ) >= 0f
                   && Mathf.Min( min.y, max.y ) <= 1f;
        }

        private static string BoundsOf( GameObject go, out Bounds bounds )
        {
            bounds = new Bounds();

            // A disabled renderer draws nothing, so its bounds would outline empty space, and an
            // enabled one with nothing to render - a SpriteRenderer holding no sprite, an empty
            // LineRenderer - reports no extent at all; either way the next fallback stands in for it.
            Renderer renderer = go.GetComponent<Renderer>();
            if ( renderer != null && renderer.enabled && HasExtent( renderer.bounds ) )
            {
                bounds = renderer.bounds;
                return null;
            }

            Collider collider = go.GetComponent<Collider>();
            if ( collider != null && collider.enabled && HasExtent( collider.bounds ) )
            {
                bounds = collider.bounds;
                return null;
            }

            // An empty parent holding its sprites on children is the common shape here, so the
            // subtree's renderers are what stands in when the object itself draws nothing.
            Renderer[] childRenderers = go.GetComponentsInChildren<Renderer>();
            bool haveChildBounds = false;
            for ( int i = 0; i < childRenderers.Length; i++ )
            {
                if ( !childRenderers[i].enabled || !HasExtent( childRenderers[i].bounds ) )
                {
                    continue;
                }

                if ( haveChildBounds )
                {
                    bounds.Encapsulate( childRenderers[i].bounds );
                }
                else
                {
                    bounds = childRenderers[i].bounds;
                    haveChildBounds = true;
                }
            }

            if ( haveChildBounds )
            {
                return null;
            }

            return $"'{go.name}' has no enabled renderer with bounds, no enabled collider and no enabled child renderer with bounds to outline";
        }

        // Bounds with no width and no height would outline a point, so the candidate they came from
        // counts as nothing to draw and the next one is tried instead.
        private static bool HasExtent( Bounds candidate )
        {
            return candidate.extents.x > 0f || candidate.extents.y > 0f;
        }

        private static string Capture()
        {
            try
            {
                Directory.CreateDirectory( screenshotDirectory );

                var timestamp = DateTime.Now.ToString( "yyyyMMdd_HHmmss" );
                var randomId = Guid.NewGuid().ToString( "N" ).Substring( 0, 8 );
                var path = Path.Combine( screenshotDirectory, $"screenshot_{timestamp}_{randomId}.png" );

                MainThreadDispatcher.EnqueueAndWait( () => ScreenCapture.CaptureScreenshot( path ), CaptureWaitMs );

                int elapsed = 0;
                while ( !IsCompletePng( path ) && elapsed < CaptureWaitMs )
                {
                    System.Threading.Thread.Sleep( PollIntervalMs );
                    elapsed += PollIntervalMs;
                }

                if ( !IsCompletePng( path ) )
                {
                    throw new IOException( $"No complete screenshot appeared at {path} within {CaptureWaitMs}ms - the game's log says why" );
                }

                PruneScreenshots( path );

                return isLinux ? path : path.Replace( '\\', '/' );
            }
            catch ( Exception ex )
            {
                Main.Log( $"Failed to take screenshot: {ex.Message}" );
                throw;
            }
        }

        // The file appears when Unity opens it, before the image is written.
        private static bool IsCompletePng( string path )
        {
            try
            {
                using ( var stream = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete ) )
                {
                    if ( stream.Length < PngTrailer.Length + 8 )
                    {
                        return false;
                    }

                    var tail = new byte[PngTrailer.Length];
                    stream.Seek( -PngTrailer.Length, SeekOrigin.End );
                    int read = 0;
                    while ( read < tail.Length )
                    {
                        int got = stream.Read( tail, read, tail.Length - read );
                        if ( got <= 0 )
                        {
                            return false;
                        }
                        read += got;
                    }

                    for ( int i = 0; i < tail.Length; i++ )
                    {
                        if ( tail[i] != PngTrailer[i] )
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
            catch ( IOException )
            {
                return false;
            }
            catch ( UnauthorizedAccessException )
            {
                return false;
            }
        }

        private static void PruneScreenshots( string keep )
        {
            try
            {
                var old = new DirectoryInfo( screenshotDirectory ).GetFiles( "screenshot_*.png" )
                    .Where( file => !string.Equals( file.FullName, Path.GetFullPath( keep ), StringComparison.Ordinal ) )
                    .OrderByDescending( file => file.LastWriteTimeUtc )
                    .Skip( KeptScreenshots - 1 );

                foreach ( var file in old )
                {
                    file.Delete();
                }
            }
            catch ( Exception ex )
            {
                Main.Log( $"Could not prune old screenshots: {ex.Message}" );
            }
        }
    }
}
