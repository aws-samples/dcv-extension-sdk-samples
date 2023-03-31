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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dcv.Extensions;
using DCVExtensionGeometryGuiCS.DcvExtensions;
using Point = Dcv.Extensions.Point;

namespace DCVExtensionGeometryGuiCS
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int DataChunk = 1024;

        private static readonly string LogPath =
            $@"C:\Temp\DcvExtensionGeometryGuiCS_{Process.GetCurrentProcess().Id}.log";

        private bool insideMove;

        private readonly SimpleLogger logger = new SimpleLogger(LogPath);
        private Task mainTask;
        private Processor processor;
        private StreamingViews views = new StreamingViews();

        public MainWindow()
        {
            InitializeComponent();

            logger.Log("DCV Extension Geometry GUI C#");

            // Processor packs/sends messages to DCV and receives/decodes messages from DCV 
            processor = new Processor(Console.OpenStandardInput(), Console.OpenStandardOutput(), logger);
            processor.StreamingViewsChanged += ProcessorStreamingViewsChanged;

            mainTask = Task.Run(async () =>
            {
                // Get list of streaming areas
                logger.Log("Requesting list of streaming views");
                views = await processor.GetStreamingViewsAsync();
                logger.Log($"Received list with {views.StreamingView.Count} views");

                foreach (var view in views.StreamingView)
                    logger.Log(
                        $"viewId={view.ViewId} area={view.LocalArea.Width}x{view.LocalArea.Height}@[{view.LocalArea.X},{view.LocalArea.Y}] zoom={view.ZoomFactor} remote offset={view.RemoteOffset.X},{view.RemoteOffset.Y}");
            });
        }

        private void ProcessorStreamingViewsChanged(StreamingViews streamingViews)
        {
            views = streamingViews;
            logger.Log($"Received updated streaming views list with {views.StreamingView.Count} views");

            foreach (var view in views.StreamingView)
                logger.Log(
                    $"viewId={view.ViewId} area={view.LocalArea.Width}x{view.LocalArea.Height}@[{view.LocalArea.X},{view.LocalArea.Y}] zoom={view.ZoomFactor} remote offset={view.RemoteOffset.X},{view.RemoteOffset.Y}");
        }

        private async void Pad_MouseMove(object sender, MouseEventArgs e)
        {
            if (processor == null) return;

            if (insideMove) return;

            insideMove = true;

            var padPoint = e.GetPosition(null);
            var screenPoint = new Point
            {
                X = (int)(padPoint.X * 10),
                Y = (int)(padPoint.Y * 10)
            };

            var viewId = await processor.IsPointInsideStreamingViewsAsync(screenPoint);
            logger.Log($"view_id={viewId} for point X={padPoint.X} Y={padPoint.Y}");

            if (viewId >= 0)
            {
                var view = views.StreamingView.First(v => v.ViewId == viewId);
                // use view to compute server side coordinates if you need them

                await processor.SetCursorPointAsync(screenPoint);
            }

            insideMove = false;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            processor.StreamingViewsChanged -= ProcessorStreamingViewsChanged;
            processor.Close();
            processor = null;
        }
    }
}