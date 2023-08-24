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
//! DCV Rust Extension
//!
//! This Rust extension is an example of how the DCV Extensions SDK can be used
//! to develop extensions for DCV servers (Linux and Windows) and clients
//! (Linux, macOS and Windows). This code will generate the extension
//! executable which DCV will execute when linked in the Manifest.json file.

use log::*;
use simplelog::{Config, WriteLogger};

use dcvextension_rs::utils::*;

#[cfg(not(windows))]
const LOG_FILE: &str = "/tmp/DcvExtensionVirtualChannelsRust"; // Extension log path for linux and macOS
#[cfg(windows)]
const LOG_FILE: &str = r"c:\temp\DcvExtensionVirtualChannelsRust"; // Extension log path for Windows

#[tokio::main]
async fn main() -> std::io::Result<()> {
    // Initialize the Logger
    WriteLogger::init(
        LevelFilter::Info,
        Config::default(),
        std::fs::File::create(format!("{}_{}.log", LOG_FILE, std::process::id())).unwrap(),
    )
    .expect("WriteLogger was not able to initialize.");

    let mut last_request_id: u32 = 1;

    log::info!("Starting Rust Extension...");

    // Get the path to the Manifest.json file
    request_manifest_path(last_request_id).await?;

    wait_manifest_path_response().await?;

    // Setup a Virtual Channel
    let virtual_channel_name = "echo";
    let relay_client_pid: i64 = std::process::id().into();
    last_request_id += 1;
    request_setup_virtual_channel(last_request_id, virtual_channel_name, relay_client_pid).await?;

    let setup_vc_resp = wait_setup_virtual_channel_response().await?;

    // Setup a Unix Socket for the Virtual Channel and connect to it via the relay path
    let relay_path = setup_vc_resp.relay_path;
    let mut socket_stream = setup_and_connect_virtual_channel_ipc(&relay_path).await?;

    // Send the auth token through the virtual channel to authenticate with DCV
    let auth_token = setup_vc_resp.virtual_channel_auth_token;
    write_auth_token_over_virtual_channel_ipc(&mut socket_stream, auth_token).await?;

    // Wait for 'VirtualChannelReadyEvent' which confirms that the Virtual Channel is ready
    // for communication
    wait_for_event("VirtualChannelReadyEvent").await?;

    // Read and Write binary data over unix socket for virtual channel communication
    read_write_over_virtual_channel_ipc(&mut socket_stream).await?;

    // Close the Virtual Channel
    last_request_id += 1;
    request_close_virtual_channel(last_request_id, virtual_channel_name).await?;

    // Wait for 'CloseVirtualChannelResponse' which confirms the Virtual channel has been closed
    wait_close_virtual_channel_response().await?;

    log::info!("Exiting Rust Extension...");

    Ok(())
}
