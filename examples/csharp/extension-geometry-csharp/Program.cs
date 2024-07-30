// /*
//  * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
//  * SPDX-License-Identifier: MIT-0
//  *
//  * Permission is hereby granted, free of charge, to any person obtaining a copy of this
//  * software and associated documentation files (the "Software"), to deal in the Software
//  * without restriction, including without limitation the rights to use, copy, modify,
//  * merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
//  * permit persons to whom the Software is furnished to do so.
//  *
//  * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
//  * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
//  * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
//  * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//  * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//  * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//  */

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dcv.Extensions;
using DcvExtensionGeometryCS.DcvExtensions;
using Point = System.Drawing.Point;

namespace DcvExtensionGeometryCS
{
    /*
     * Std input is used to read messages from DCV
     * Std output is used to send messages to DCV
     */

    internal class Program
    {
        private const int DataChunk = 1024;

        private static readonly string
            LogPath = $@"C:\Temp\DcvExtensionGeometryCS_{Process.GetCurrentProcess().Id}.log";

        private static readonly SimpleLogger Logger = new SimpleLogger(LogPath);
        private static StreamingViews _views;

        public static void Main()
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            try
            {
                Logger.Log("DCV Extension Geometry C#");

                // Processor packs/sends messages to DCV and receives/decodes messages from DCV 
                var processor = new Processor(Console.OpenStandardInput(), Console.OpenStandardOutput(), Logger);
                processor.StreamingViewsChanged += ProcessorStreamingViewsChanged;

                // Get path to the manifest
                Logger.Log("Requesting manifest path");
                var manifestPath = await processor.GetManifestAsync();
                Logger.Log($"Received manifest path: {manifestPath}");

                // Get list of streaming areas
                Logger.Log("Requesting list of streaming views");
                _views = await processor.GetStreamingViewsAsync();
                Logger.Log($"Received list with {_views.StreamingView.Count} views");

                foreach (var view in _views.StreamingView)
                    Logger.Log(
                        $"viewId={view.ViewId} area={view.LocalArea.Width}x{view.LocalArea.Height}@[{view.LocalArea.X},{view.LocalArea.Y}] zoom={view.ZoomFactor} remote offset={view.RemoteOffset.X},{view.RemoteOffset.Y}, view has focus={view.HasFocus}, client owns focus={_views.HasFocus}, handle=0x{view.Handle:X}");
                
                // Discover if mouse pointer is over a streaming area or not
                var mouseTask = Task.Run(async () =>
                {
                    for (var i = 0; i < 1000; ++i)
                    {
                        NativeMethods.GetCursorPos(out var point);

                        // Use Geometry API
                        var viewId = await processor.IsPointInsideStreamingViewsAsync(new Dcv.Extensions.Point
                            { X = point.X, Y = point.Y });
                        Logger.Log($"API view_id={viewId} for point X={point.X} Y={point.Y}");

                        // Use view's handle
                        viewId = IsPointInsideStreamingViews(point);
                        Logger.Log($"EXT view_id={viewId} for point X={point.X} Y={point.Y}");

                        // Do something if point is inside a streaming area
                        if (viewId >= 0)
                            await processor.SetCursorPointAsync(new Dcv.Extensions.Point { X = point.X, Y = point.Y });

                        await Task.Delay(2000);
                    }
                });

                // Wait until one of the tasks completes
                await Task.WhenAny(mouseTask);

                // Close the processor
                processor.Close();
            }
            catch (Exception ex)
            {
                Logger.Log($"Uncaught Exception: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                Logger.Log("Exiting");
            }
        }

        private static void ProcessorStreamingViewsChanged(StreamingViews streamingViews)
        {
            _views = streamingViews;
            Logger.Log($"Received updated streaming views list with {_views.StreamingView.Count} views");

            foreach (var view in _views.StreamingView)
                Logger.Log(
                    $"viewId={view.ViewId} area={view.LocalArea.Width}x{view.LocalArea.Height}@[{view.LocalArea.X},{view.LocalArea.Y}] zoom={view.ZoomFactor} remote offset={view.RemoteOffset.X},{view.RemoteOffset.Y}, view has focus={view.HasFocus}, client owns focus={_views.HasFocus}, handle=0x{view.Handle:X}");
        }
 
        private static int IsPointInsideStreamingViews(Point point)
        {
            var streamingViews = _views?.StreamingView;

            if (streamingViews == null)
            {
                return -1;
            }

            foreach (var view in streamingViews)
            {
                if (point.X < view.LocalArea.X || point.X >= view.LocalArea.X + view.LocalArea.Width ||
                    point.Y < view.LocalArea.Y || point.Y >= view.LocalArea.Y + view.LocalArea.Height)
                {
                    continue;
                }

                UIntPtr hWnd = NativeMethods.WindowFromPoint(point);
                if (hWnd.ToUInt64() == view.Handle)
                {
                    // Point is on a software rendered streaming area
                    return view.ViewId;
                }

                do
                {
                    Point clientPoint = point;
                    NativeMethods.ScreenToClient(hWnd, ref clientPoint);

                    UIntPtr childHWnd = NativeMethods.RealChildWindowFromPoint(hWnd, clientPoint);
                    if (childHWnd == hWnd)
                    {
                        // Point is on a context menu or DCV menu or overlapped window that hides the underlying streaming area
                        return -1;
                    }

                    if (childHWnd.ToUInt64() == view.Handle)
                    {
                        // Point is on a hardware rendered streaming area
                        return view.ViewId;
                    }

                    hWnd = childHWnd;
                } while (hWnd != null);
            }

            return -1;
        }
    }

    internal class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        internal static extern UIntPtr RealChildWindowFromPoint(UIntPtr hwndParent, Point ptParentClientCoords);

        [DllImport("user32.dll")]
        internal static extern bool ScreenToClient(UIntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        internal static extern UIntPtr WindowFromPoint(Point Point);
    }
}