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

namespace DcvExtensionVirtualChannelsCS.DcvExtensions
{
    internal class Reader
    {
        private readonly Stream inputStream;

        public Reader(Stream inputStream)
        {
            this.inputStream = inputStream;
        }

        private async Task ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var bytesRead = 0;
            var offset = 0;

            while (bytesRead < buffer.Length)
            {
                var read = await inputStream.ReadAsync(buffer, offset, buffer.Length - bytesRead, cancellationToken);
                if (read == 0)
                    // TODO: perform a clean shutdown
                    // Dcv terminated, die.
                    Process.GetCurrentProcess().Kill();
                bytesRead += read;
            }
        }

        // ReceiveMessageAsync returns promptly to the caller and will call OnNewMessage when complete messages arrive 
        public async Task<DcvMessage> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4];
            await ReadExactAsync(buffer, cancellationToken);

            // Read the size of the serialized protobuf message. It is a 32 bit Little Endian value
            var size = BitConverter.ToInt32(buffer, 0);

            buffer = new byte[size];
            // Read the serialized protobuf message
            await ReadExactAsync(buffer, cancellationToken);

            // Parse the serialized protobuf message
            return DcvMessage.Parser.ParseFrom(buffer);
        }
    }
}