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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dcv.Extensions;
using static Dcv.Extensions.Response.Types;

namespace DCVExtensionGeometryGuiCS.DcvExtensions
{
    public class Processor
    {
        private readonly SimpleLogger logger;
        private readonly Reader reader;
        private readonly Writer writer;
        private int lastRequestId = 1;

        private readonly Task readTask;
        private readonly CancellationTokenSource source;
        private TaskCompletionSource<bool> tcsClose;
        private TaskCompletionSource<StreamingViews> tcsGetStreamingViews;
        private TaskCompletionSource<int> tcsIsPointInside;

        private TaskCompletionSource<string> tcsManifest;
        private TaskCompletionSource<bool> tcsReady;
        private TaskCompletionSource<bool> tcsSetCursorPoint;
        private TaskCompletionSource<string> tcsSetup;

        public Processor(Stream input, Stream output, SimpleLogger logger)
        {
            this.logger = logger;
            source = new CancellationTokenSource();
            reader = new Reader(input);
            writer = new Writer(output);

            // Run a task that reads the messages from stdin
            readTask = Task.Run(async () =>
            {
                while (!source.Token.IsCancellationRequested)
                {
                    var message = await reader.ReceiveMessageAsync(source.Token);
                    ProcessDcvMessage(message);
                }
            });
        }

        public event Action<StreamingViews> StreamingViewsChanged;

        public async void Close()
        {
            // Stop the read task
            source.Cancel();

            await readTask;
        }

        public Task<string> GetManifestAsync()
        {
            var requestId = (lastRequestId++).ToString();

            logger.Log($"Sending 'GetManifestRequest' with id '{requestId}' to DCV client");

            // Creates a TaskCompletionSource that will be completed when the response is received
            tcsManifest = new TaskCompletionSource<string>();

            // Send a GetManifestRequest message to the DCV client
            writer.SendGetManifestRequest(requestId);

            return tcsManifest.Task;
        }

        public Task<string> SetupVirtualChannelAsync(long pid, string virtualChannelName)
        {
            var requestId = (lastRequestId++).ToString();

            logger.Log(
                $"Sending 'SetupVirtualChannelRequest' with id '{requestId}' to DCV client for channel '{virtualChannelName}'");

            // Creates a TaskCompletionSource that will be completed when the channel is set up
            tcsSetup = new TaskCompletionSource<string>();
            tcsReady = new TaskCompletionSource<bool>();

            // Send a SetupVirtualChannelRequest message to the DCV client
            writer.SendSetupVirtualChannelRequest(requestId, pid, virtualChannelName);

            return tcsSetup.Task;
        }

        public Task<bool> WaitVirtualChannelReadyEventAsync()
        {
            return tcsReady.Task;
        }

        public Task<bool> CloseVirtualChannelAsync(string virtualChannelName)
        {
            var requestId = (lastRequestId++).ToString();

            logger.Log(
                $"Sending 'CloseVirtualChannelRequest' with id '{requestId}' to DCV client for channel '{virtualChannelName}'");

            // Creates a TaskCompletionSource that will be completed when the channel is closed
            tcsClose = new TaskCompletionSource<bool>();

            // Send a CloseVirtualChannelRequest message to the DCV client
            writer.SendCloseVirtualChannelRequest(requestId, virtualChannelName);

            return tcsClose.Task;
        }

        public Task SetCursorPointAsync(Point point)
        {
            var requestId = (lastRequestId++).ToString();

            logger.Log(
                $"Sending 'SetCursorPointRequest' with id '{requestId}' to DCV client for point X={point.X} Y={point.Y}");

            // Creates a TaskCompletionSource that will be completed when the response is received
            tcsSetCursorPoint = new TaskCompletionSource<bool>();

            // Send a SetCursorPointRequest message to the DCV client
            writer.SendSetCursorPointRequest(requestId, point);

            return tcsSetCursorPoint.Task;
        }

        public Task<StreamingViews> GetStreamingViewsAsync()
        {
            var requestId = (lastRequestId++).ToString();

            logger.Log($"Sending 'GetStreamingViewsRequest' with id '{requestId}' to DCV client");

            // Creates a TaskCompletionSource that will be completed when the response is received
            tcsGetStreamingViews = new TaskCompletionSource<StreamingViews>();

            // Send a GetStreamingViewsRequest message to the DCV client
            writer.SendGetStreamingViewsRequest(requestId);

            return tcsGetStreamingViews.Task;
        }

        public Task<int> IsPointInsideStreamingViewsAsync(Point point)
        {
            var requestId = (lastRequestId++).ToString();

            logger.Log(
                $"Sending 'IsPointInsideStreamingViewsRequest' with id '{requestId}' to DCV client for point X={point.X} Y={point.Y}");

            // Creates a TaskCompletionSource that will be completed when the response is received
            tcsIsPointInside = new TaskCompletionSource<int>();

            // Send a IsPointInsideStreamingViewsRequest message to the DCV client
            writer.SendIsPointInsideStreamingViewsRequest(requestId, point);

            return tcsIsPointInside.Task;
        }

        #region Incoming messages processing

        private void ProcessDcvMessage(DcvMessage message)
        {
            switch (message.MsgCase)
            {
                case DcvMessage.MsgOneofCase.Event:
                    switch (message.Event.EventCase)
                    {
                        case Event.EventOneofCase.VirtualChannelReadyEvent:
                            logger.Log(
                                $"Received '{message.Event.EventCase}' from DCV client for channel '{message.Event.VirtualChannelReadyEvent.VirtualChannelName}'");
                            ProcessVirtualChannelReadyEvent(message.Event.VirtualChannelReadyEvent);
                            break;
                        case Event.EventOneofCase.VirtualChannelClosedEvent:
                            logger.Log(
                                $"Received '{message.Event.EventCase}' from DCV client for channel '{message.Event.VirtualChannelClosedEvent.VirtualChannelName}'");
                            break;
                        case Event.EventOneofCase.StreamingViewsChangedEvent:
                            logger.Log($"Received '{message.Event.EventCase}' from DCV client");
                            ProcessStreamingViewsChangedEvent(message.Event.StreamingViewsChangedEvent);
                            break;
                        default:
                        case Event.EventOneofCase.None:
                            // case not supported in this version of extensions.proto, not an error
                            logger.Log($"Received '{message.Event.EventCase}' from DCV client");
                            break;
                    }

                    break;
                case DcvMessage.MsgOneofCase.Response:
                    switch (message.Response.ResponseCase)
                    {
                        case Response.ResponseOneofCase.GetManifestResponse:
                            logger.Log(
                                $"Received '{message.Response.ResponseCase}' with id '{message.Response.RequestId}' from DCV client");
                            ProcessGetManifestResponse(message.Response.GetManifestResponse, message.Response.Status);
                            break;
                        case Response.ResponseOneofCase.SetupVirtualChannelResponse:
                            logger.Log(
                                $"Received '{message.Response.ResponseCase}' with id '{message.Response.RequestId}' from DCV client for channel '{message.Response.SetupVirtualChannelResponse.VirtualChannelName}'");
                            ProcessSetupVirtualChannelResponse(message.Response.SetupVirtualChannelResponse,
                                message.Response.Status);
                            break;
                        case Response.ResponseOneofCase.CloseVirtualChannelResponse:
                            logger.Log(
                                $"Received '{message.Response.ResponseCase}' with id '{message.Response.RequestId}' from DCV client for channel '{message.Response.CloseVirtualChannelResponse.VirtualChannelName}'");
                            ProcessCloseVirtualChannelResponse(message.Response.CloseVirtualChannelResponse,
                                message.Response.Status);
                            break;
                        case Response.ResponseOneofCase.SetCursorPointResponse:
                            logger.Log(
                                $"Received '{message.Response.ResponseCase}' with id '{message.Response.RequestId}' from DCV client'");
                            ProcessSetCursorPointResponse(message.Response.SetCursorPointResponse,
                                message.Response.Status);
                            break;
                        case Response.ResponseOneofCase.GetStreamingViewsResponse:
                            logger.Log(
                                $"Received '{message.Response.ResponseCase}' with id '{message.Response.RequestId}' from DCV client'");
                            ProcessGetStreamingViewsResponse(message.Response.GetStreamingViewsResponse,
                                message.Response.Status);
                            break;
                        case Response.ResponseOneofCase.IsPointInsideStreamingViewsResponse:
                            logger.Log(
                                $"Received '{message.Response.ResponseCase}' with id '{message.Response.RequestId}' from DCV client'");
                            ProcessIsPointInsideStreamingViewsResponse(
                                message.Response.IsPointInsideStreamingViewsResponse, message.Response.Status);
                            break;
                        default:
                        case Response.ResponseOneofCase.None:
                            // case not supported in this version of extensions.proto, not an error
                            logger.Log(
                                $"Received unsupported '{message.Response.ResponseCase}' with id '{message.Response.RequestId}' from DCV client");
                            break;
                    }

                    break;
                default:
                case DcvMessage.MsgOneofCase.None:
                    // case not supported in this version of extensions.proto, not an error
                    break;
            }
        }

        private void ProcessVirtualChannelReadyEvent(VirtualChannelReadyEvent message)
        {
            logger.Log($"DCV has set up virtual channel '{message.VirtualChannelName}'");

            Trace.Assert(tcsReady != null);

            // Set the result of pipe ready task
            tcsReady.SetResult(true);
            tcsReady = null;
        }

        private void ProcessStreamingViewsChangedEvent(StreamingViewsChangedEvent message)
        {
            logger.Log("DCV has notified that the geometry of the Streaming Views has changed");
            StreamingViewsChanged?.Invoke(message.StreamingViews);
        }

        private void ProcessGetManifestResponse(GetManifestResponse message, Status status)
        {
            Trace.Assert(tcsManifest != null);

            if (status != Status.Success)
            {
                logger.Log($"DCV returned an error while getting manifest: {status}");

                // Set the result of manifest task
                tcsManifest.SetException(new Exception($"Failed to get manifest {status}"));
                tcsManifest = null;

                return;
            }

            // Set the result of setup task
            tcsManifest.SetResult(message.ManifestPath);
            tcsManifest = null;
        }

        private void ProcessSetupVirtualChannelResponse(SetupVirtualChannelResponse message, Status status)
        {
            Trace.Assert(tcsSetup != null);

            if (status != Status.Success)
            {
                logger.Log(
                    $"DCV returned an error while setting up virtual channel '{message.VirtualChannelName}': {status}");

                // Set the result of setup task
                tcsSetup.SetException(new Exception($"Failed to setup virtual channel {status}"));
                tcsSetup = null;

                return;
            }

            logger.Log(
                $"DCV is setting up the virtual channel '{message.VirtualChannelName}' and the named pipe {message.RelayPath} is available for connection");

            // Set the result of setup task
            tcsSetup.SetResult(message.RelayPath);
            tcsSetup = null;
        }

        private void ProcessCloseVirtualChannelResponse(CloseVirtualChannelResponse message, Status status)
        {
            logger.Log($"DCV closed the virtual channel '{message.VirtualChannelName}': {status}");

            Trace.Assert(tcsClose != null);

            // Set the result of close task
            tcsClose.SetResult(true);
            tcsClose = null;
        }

        private void ProcessSetCursorPointResponse(SetCursorPointResponse message, Status status)
        {
            logger.Log($"DCV SetCursorPointResponse status: {status}");

            Trace.Assert(tcsSetCursorPoint != null);

            // Set the result of the task
            tcsSetCursorPoint.SetResult(status == Status.Success);
            tcsSetCursorPoint = null;
        }

        private void ProcessGetStreamingViewsResponse(GetStreamingViewsResponse message, Status status)
        {
            logger.Log($"DCV ProcessGetStreamingViewsResponse status: {status}");

            Trace.Assert(tcsGetStreamingViews != null);

            // Set the result of the task
            tcsGetStreamingViews.SetResult(message.StreamingViews);
            tcsGetStreamingViews = null;
        }

        private void ProcessIsPointInsideStreamingViewsResponse(IsPointInsideStreamingViewsResponse message,
            Status status)
        {
            logger.Log($"DCV IsPointInsideStreamingViewsResponse status: {status}");

            Trace.Assert(tcsIsPointInside != null);

            // Set the result of the task
            tcsIsPointInside.SetResult(message.ViewId);
            tcsIsPointInside = null;
        }

        #endregion
    }
}