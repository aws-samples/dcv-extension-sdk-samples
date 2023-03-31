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
using System.IO;
using System.Threading.Tasks;
using Dcv.Extensions;
using Google.Protobuf;

namespace DcvExtensionGeometryCS.DcvExtensions
{
    internal class Writer
    {
        private readonly Stream outputStream = Console.OpenStandardOutput();

        public Writer(Stream outputStream)
        {
            this.outputStream = outputStream;
        }

        public Task SendGetManifestRequest(string requestId)
        {
            return SendRequest(new Request
            {
                RequestId = requestId,
                GetManifestRequest = new GetManifestRequest()
            });
        }

        public Task SendSetupVirtualChannelRequest(string requestId, long pId, string channelName)
        {
            return SendRequest(new Request
            {
                RequestId = requestId,
                SetupVirtualChannelRequest = new SetupVirtualChannelRequest
                {
                    RelayClientProcessId = pId,
                    VirtualChannelName = channelName
                }
            });
        }

        public Task SendCloseVirtualChannelRequest(string requestId, string channelName)
        {
            return SendRequest(new Request
            {
                RequestId = requestId,
                CloseVirtualChannelRequest = new CloseVirtualChannelRequest
                {
                    VirtualChannelName = channelName
                }
            });
        }

        public Task SendSetCursorPointRequest(string requestId, Point point)
        {
            return SendRequest(new Request
            {
                RequestId = requestId,
                SetCursorPointRequest = new SetCursorPointRequest
                {
                    Point = point
                }
            });
        }

        public Task SendGetStreamingViewsRequest(string requestId)
        {
            return SendRequest(new Request
            {
                RequestId = requestId,
                GetStreamingViewsRequest = new GetStreamingViewsRequest()
            });
        }

        public Task SendIsPointInsideStreamingViewsRequest(string requestId, Point point)
        {
            return SendRequest(new Request
            {
                RequestId = requestId,
                IsPointInsideStreamingViewsRequest = new IsPointInsideStreamingViewsRequest
                {
                    Point = point
                }
            });
        }

        private Task SendRequest(Request request)
        {
            return SendMessage(new ExtensionMessage { Request = request });
        }

        private async Task SendMessage<T>(T message) where T : IMessage
        {
            var messageSize = message.CalculateSize();

            var ms = new MemoryStream(messageSize + sizeof(int));
            var bw = new BinaryWriter(ms);

            // Write the size of the serialized protobuf message to the temporary buffer
            bw.Write(messageSize);
            // Write the serialized protobuf message to the temporary buffer
            message.WriteTo(ms);

            var data = ms.ToArray();

            // Send the message (size + serialized protobuf message) to the DCV client
            await outputStream.WriteAsync(data, 0, data.Length);
            await outputStream.FlushAsync();
        }
    }
}