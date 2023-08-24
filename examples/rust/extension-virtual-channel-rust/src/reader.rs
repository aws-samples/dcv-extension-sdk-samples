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
//! This file shows how to read protobuf messages sent from DCV and handle the
//! Responses and Events.

use std::io::Error;

use crate::proto::*;
use prost::Message;
use std::io::ErrorKind;
use tokio::io::{self, AsyncReadExt};

const SUCCESS: i32 = 1;

// Receive a message from DCV
pub async fn read_message() -> io::Result<DcvMessage> {
    // Use stdin to read messages from DCV
    let mut stdin = io::stdin();

    // Read the header(msg size) 32 bits in little-endian format
    let header = stdin.read_u32_le().await?;
    log::info!("ReadMessage - received message size(header): {}", header);

    // Read the message into a buffer
    let msg_size = header.try_into().expect("Header didnt fit into usize");
    let mut msg_buffer: Vec<u8> = vec![0; msg_size];
    let num_bytes = stdin.read(&mut msg_buffer).await?;
    log::info!("ReadMessage - received {} bytes in msg body", num_bytes);

    // Decode the serialized protobuf message using prost and return it
    let decoded =
        DcvMessage::decode(&msg_buffer[..]).map_err(|e| Error::new(ErrorKind::Other, e))?;

    Ok(decoded)
}

// Read the response from DCV
pub fn read_response(msg: DcvMessage) -> io::Result<response::Response> {
    if msg.msg.is_none() {
        let err = format!("Received invalid message {:?}", msg.msg);
        return Err(Error::new(ErrorKind::InvalidData, err));
    }

    let msg = msg.msg.expect("Message should not be 'None'");
    if let dcv_message::Msg::Response(res) = msg {
        if res.response.is_none() {
            let err = format!(
                "Received invalid response {:?} for request ID '{}'",
                res.response, res.request_id
            );

            return Err(Error::new(ErrorKind::InvalidData, err));
        } else if res.status != SUCCESS {
            let err = format!(
                "Response errored with status code '{}' for request ID '{}'",
                res.status, res.request_id
            );

            return Err(Error::new(ErrorKind::Other, err));
        }

        log::info!(
            "ReadResponse - Successfully received response for request ID: '{}'",
            res.request_id,
        );

        Ok(res.response.expect("Response should not be 'None'"))
    } else {
        let err = format!("Received invalid message case: {:?}", msg);
        Err(Error::new(ErrorKind::InvalidData, err))
    }
}

// Read the event from DCV
pub fn read_event(msg: DcvMessage) -> io::Result<event::Event> {
    if msg.msg.is_none() {
        let err = format!("Received invalid message: {:?}", msg.msg);

        return Err(Error::new(ErrorKind::InvalidData, err));
    }

    let event_msg = msg.msg.expect("Message should not be 'None'");
    if let dcv_message::Msg::Event(ev) = event_msg {
        if ev.event.is_none() {
            let err = format!("Received invalid event: {:?}", ev.event);

            return Err(Error::new(ErrorKind::InvalidData, err));
        }

        Ok(ev.event.expect("Event should not be 'None'"))
    } else {
        let err = format!("Received invalid message case: {:?}", event_msg);

        Err(Error::new(ErrorKind::InvalidData, err))
    }
}
