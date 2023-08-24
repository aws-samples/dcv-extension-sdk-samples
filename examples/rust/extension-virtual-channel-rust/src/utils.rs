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
//! This file shows how to make requests to interact with DCV, handle their respective
//! responses and create a local relay(unix socket) for Virtual Channel Communication

use crate::proto::*;
use crate::reader::{read_event, read_message, read_response};
use crate::writer::write_request;
use std::io::Error;
use tokio::io::{self, AsyncRead, AsyncReadExt, AsyncWrite, AsyncWriteExt};

#[cfg(not(windows))]
use std::os::unix::prelude::{FromRawFd, IntoRawFd};
#[cfg(not(windows))]
use tokio::net::UnixStream;
#[cfg(not(windows))]
use uds::{UnixSocketAddr, UnixStreamExt};

const READ_BUFFER_SIZE: usize = 4096;

// Setup and connect an Unix Socket for virtual channel communication
#[cfg(not(windows))]
pub async fn setup_and_connect_virtual_channel_ipc(relay_path: &str) -> io::Result<UnixStream> {
    #[cfg(target_os = "macos")]
    let socket_addr = UnixSocketAddr::from_path(relay_path)?;
    #[cfg(target_os = "linux")]
    let socket_addr = UnixSocketAddr::from_abstract(relay_path)?;

    let stream = mio::net::UnixStream::connect_to_unix_addr(&socket_addr)?;
    let stream = UnixStream::from_std(unsafe {
        std::os::unix::net::UnixStream::from_raw_fd(stream.into_raw_fd())
    })?;

    log::info!(
        "SetupAndConnectUnixSocket - Created unix socket: {:?}",
        stream.peer_addr()
    );

    Ok(stream)
}

#[cfg(windows)]
pub async fn setup_and_connect_virtual_channel_ipc(
    relay_path: &str,
) -> io::Result<tokio::net::windows::named_pipe::NamedPipeClient> {
    loop {
        match tokio::net::windows::named_pipe::ClientOptions::new().open(relay_path) {
            Ok(client) => break Ok(client),
            Err(e)
                if e.raw_os_error() == Some(winapi::shared::winerror::ERROR_PIPE_BUSY as i32) => {}
            Err(e) => return Err(e),
        }

        log::info!("Pipe is busy, waiting...");

        tokio::time::sleep(std::time::Duration::from_millis(50)).await;
    }
}

pub async fn write_auth_token_over_virtual_channel_ipc<T: AsyncRead + AsyncWrite + Unpin>(
    stream: &mut T,
    auth_token: Vec<u8>,
) -> io::Result<()> {
    log::debug!("Starting Virtual Channel auth token transfer over Named Pipe/Unix Socket");

    stream.write_all(&auth_token).await?;
    stream.flush().await?;

    log::debug!("Ending Virtual Channel auth token transfer over Named Pipe/Unix Socket");

    Ok(())
}

pub async fn read_write_over_virtual_channel_ipc<T: AsyncRead + AsyncWrite + Unpin>(
    stream: &mut T,
) -> io::Result<()> {
    let message = b"hello world";

    log::info!("Starting Virtual Channel data transfer over Unix Socket");

    for msg_num in 0..5 {
        // write binary message data to unix socket
        stream.write_all(message).await?;
        stream.flush().await?;
        log::info!("Wrote data {} - bytes: {:?}", msg_num, message);

        // read binary message data from socket
        let mut read_buffer = [0; READ_BUFFER_SIZE];
        let read_bytes = stream.read(&mut read_buffer).await?;
        log::info!(
            "Received data {} - num of bytes: {}, bytes: {:?}",
            msg_num,
            read_bytes,
            read_buffer[..read_bytes].to_vec(),
        );
    }

    log::info!("Ending Virtual Channel data transfer over Unix Socket");

    Ok(())
}

/* Handle Requests */

// Sends a request to DCV to get the filepath of the Manifest.json file
pub async fn request_manifest_path(request_id: u32) -> io::Result<()> {
    let manifest_request = request::Request::GetManifestRequest(GetManifestRequest {});

    let request = Request {
        request_id: request_id.to_string(),
        request: Some(manifest_request),
    };

    log::info!(
        "GetManifestPath- Sending message request with id: '{}' to get Manifest.json path",
        request_id,
    );

    write_request(request).await
}

// Sends a request to DCV to create a Virtual Channel
pub async fn request_setup_virtual_channel(
    request_id: u32,
    vc_name: &str,
    relay_client_pid: i64,
) -> io::Result<()> {
    let setup_vc_request =
        request::Request::SetupVirtualChannelRequest(SetupVirtualChannelRequest {
            virtual_channel_name: vc_name.to_string(),
            relay_client_process_id: relay_client_pid,
        });

    let request = Request {
        request_id: request_id.to_string(),
        request: Some(setup_vc_request),
    };
    log::info!(
        "SetupVirtualChannel - Sending message request with id: '{}' and virtual channel name: '{}'",
        request_id,
        vc_name
    );

    write_request(request).await
}

// Sends a request to DCV to close the Virtual Channel
pub async fn request_close_virtual_channel(request_id: u32, vc_name: &str) -> io::Result<()> {
    let close_vc_request =
        request::Request::CloseVirtualChannelRequest(CloseVirtualChannelRequest {
            virtual_channel_name: vc_name.to_string(),
        });

    let request = Request {
        request_id: request_id.to_string(),
        request: Some(close_vc_request),
    };

    log::info!(
        "CloseVirtualChannel - Sending message request with id: '{}' and virtual channel name: '{}'",
        request_id,
        vc_name
    );

    write_request(request).await
}

/* Validate and handle the Responses */

pub async fn wait_manifest_path_response() -> io::Result<GetManifestResponse> {
    let response = read_response(read_message().await?);

    match response {
        Ok(response::Response::GetManifestResponse(res)) => {
            log::info!(
                "GetManifestPath - Received manifest path: '{}' in response",
                res.manifest_path
            );

            Ok(res)
        }
        Ok(res) => {
            log::error!(
                "GetManifestPath - Was expecting 'GetManifestResponse'. Received: {:?}",
                res
            );

            Err(Error::new(
                io::ErrorKind::InvalidData,
                "Response type mismatch".to_string(),
            ))
        }
        Err(e) => {
            log::error!("GetManifestPath - Failed to get response. {}", e);

            Err(e)
        }
    }
}

pub async fn wait_setup_virtual_channel_response() -> io::Result<SetupVirtualChannelResponse> {
    let response = read_response(read_message().await?);

    match response {
        Ok(response::Response::SetupVirtualChannelResponse(res)) => {
            log::info!("SetupVirtualChannel - Received virtual channel name: '{}' and relay path: '{}' in response",
            res.virtual_channel_name,
            res.relay_path // TODO: remove after relay_path is removed from the response
        );

            Ok(res)
        }
        Ok(res) => {
            log::error!(
                "SetupVirtualChannel - Was expecting 'SetupVirtualChannelResponse'. Received: {:?}",
                res
            );

            Err(Error::new(
                io::ErrorKind::InvalidData,
                "Response type mismatch".to_string(),
            ))
        }
        Err(e) => {
            log::error!("SetupVirtualChannel - Failed to get response. {}", e);

            Err(e)
        }
    }
}

pub async fn wait_close_virtual_channel_response() -> io::Result<CloseVirtualChannelResponse> {
    let response = read_response(read_message().await?);

    match response {
        Ok(response::Response::CloseVirtualChannelResponse(res)) => {
            log::info!(
                "CloseVirtualChannel - Received closed virtual channel name: '{}' in response",
                res.virtual_channel_name
            );

            Ok(res)
        }
        Ok(res) => {
            log::error!(
                "CloseVirtualChannel - Was expecting 'CloseVirtualChannelResponse'. Received: {:?}",
                res
            );

            Err(Error::new(
                io::ErrorKind::InvalidData,
                "Response type mismatch".to_string(),
            ))
        }
        Err(e) => {
            log::error!("CloseVirtualChannel - Failed to get response. {}", e);

            Err(e)
        }
    }
}

/* Validate and handle the Events */

pub async fn wait_for_event(expected_event: &str) -> io::Result<event::Event> {
    loop {
        let event = read_event(read_message().await?);

        match event {
            Ok(ev) => {
                let validate = validate_event(expected_event, &ev);
                if !validate.0 {
                    log::error!(
                        "WaitForEvent - Expected {}. Received: {}",
                        expected_event,
                        validate.1
                    );
                } else {
                    log::info!("WaitForEvent - {}", validate.1);

                    break Ok(ev);
                }
            }
            Err(e) => log::error!("WaitForEvent - Failed to get event. Error: {:?}", e),
        }
    }
}

fn validate_event(expected: &str, received: &event::Event) -> (bool, String) {
    match (expected, received) {
        (
            "VirtualChannelReadyEvent",
            event::Event::VirtualChannelReadyEvent(VirtualChannelReadyEvent {
                virtual_channel_name,
            }),
        ) => (
            true,
            format!(
                "Received 'VirtualChannelReadyEvent' for virtual channel '{}'",
                virtual_channel_name
            ),
        ),
        (
            "VirtualChannelClosedEvent",
            event::Event::VirtualChannelClosedEvent(VirtualChannelClosedEvent {
                virtual_channel_name,
            }),
        ) => (
            true,
            format!(
                "Received 'VirtualChannelClosedEvent' for virtual channel '{}'",
                virtual_channel_name
            ),
        ),
        (
            "StreamingViewsChangedEvent",
            event::Event::StreamingViewsChangedEvent(StreamingViewsChangedEvent {
                streaming_views,
            }),
        ) => (
            true,
            format!(
                "Received 'StreamingViewsChangedEvent' for virtual channel: {:?}",
                streaming_views
            ),
        ),
        (_, _) => (
            false,
            format!(
                "Received unexpected event. Expected: '{}', Received: {:?}",
                expected, received
            ),
        ),
    }
}
