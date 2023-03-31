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
using System.IO.Pipes;
using System.Threading.Tasks;

namespace DCVExtensionGeometryGuiCS.DcvExtensions
{
    internal class VirtualChannel
    {
        private readonly NamedPipeClientStream namedPipe;

        private VirtualChannel(NamedPipeClientStream stream)
        {
            namedPipe = stream;
        }

        public static async Task<VirtualChannel> ConnectAsync(string relayPath)
        {
            if (string.IsNullOrEmpty(relayPath))
                throw new ArgumentException("relayPath (named pipe name) is null or empty");

            if (relayPath.Length < 17)
                throw new ArgumentException($"relayPath (named pipe name) {relayPath} is not valid");

            // For Windows the relayPath format is: "\\.\pipe\<random>"
            // where server name is always ".", random is an 8 digit hexadecimal number added by DCV
            var namedPipeName = relayPath.Substring(9);

            // Create the named pipe, the pipe has to support asynchronous operations
            var namedPipe = new NamedPipeClientStream(".", namedPipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);

            // Connect the named pipe
            await namedPipe.ConnectAsync();

            return new VirtualChannel(namedPipe);
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            // Read a buffer from the named pipe
            return namedPipe.ReadAsync(buffer, offset, count);
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count)
        {
            // Write the buffer to the named pipe
            await namedPipe.WriteAsync(buffer, offset, count);
            await namedPipe.FlushAsync();
        }

        public void Close()
        {
            // Close the named pipe
            namedPipe.Close();
        }
    }
}