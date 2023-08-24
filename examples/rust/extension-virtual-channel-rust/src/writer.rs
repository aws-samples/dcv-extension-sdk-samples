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
//! This file shows how to write protobuf messages and requests to send to DCV.

use crate::proto::*;
use prost::Message;
use tokio::io::{self, AsyncWriteExt};

// Write a message to send to DCV
async fn write_message(msg: ExtensionMessage) -> io::Result<()> {
    // Use stdout to send messages to DCV
    let mut stdout = io::stdout();

    // Encode the msg size into a 32-bit header in little-endian format
    let header_msg_size = msg.encoded_len();
    stdout
        .write_u32_le(
            header_msg_size
                .try_into()
                .expect("Header(msg size) does not fit into 4 bytes"),
        )
        .await?;
    log::info!(
        "WriteMessage - wrote header(msg_size): {} in stdout handle",
        header_msg_size
    );

    // Encode the message into a buffer
    let mut msg_buffer = vec![];
    msg.encode(&mut msg_buffer).unwrap();
    stdout.write_all(&msg_buffer).await?;
    log::info!("WriteMessage - wrote msg in stdout handle");

    stdout.flush().await?;

    Ok(())
}

// Create a request to send to DCV
pub async fn write_request(request: Request) -> io::Result<()> {
    let ext_msg_request = extension_message::Msg::Request(request);

    let extension_msg = ExtensionMessage {
        msg: Some(ext_msg_request),
    };

    // Send the request as a message to DCV
    let res = write_message(extension_msg).await;
    match &res {
        Ok(()) => log::info!("WriteRequest - Request sent successfully"),
        Err(e) => log::info!("WriteRequest - Request failed. Error: {}", e),
    }

    res
}
