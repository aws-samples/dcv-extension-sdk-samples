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

#define _CRT_SECURE_NO_WARNINGS
#include <stdio.h>
#include <Windows.h>
#include "simplelogger.h"
#include "extensions.pb-c.h"

#define LOG_FILE "C:\\Temp\\DcvExtensionVirtualChannelsC"
#define READ_BUFFER_SIZE 4096

char log_file[sizeof(LOG_FILE) + 20];
char* CHANNEL_NAME = "echo";
char read_buffer[READ_BUFFER_SIZE];

void
WriteMessage(Dcv__Extensions__ExtensionMessage* msg);

BOOL
ReadFromHandle(HANDLE handle,
               uint8_t* buffer,
               DWORD size)
{
    DWORD bytes_read = 0;

    while (bytes_read < size) {
        DWORD curr_read;
        uint8_t* offset_buf = buffer + bytes_read;
        DWORD remaining_bytes = size - bytes_read;

        if (!ReadFile(handle, offset_buf, remaining_bytes, &curr_read, NULL)) {
            log_f("Could not read from handle: 0x%X", GetLastError());
            return FALSE;
        }

        if (curr_read == 0) {
            log_f("Read 0 bytes, stopping read");
            return FALSE;
        }

        bytes_read += curr_read;
    }

    return TRUE;
}

Dcv__Extensions__DcvMessage*
ReadNextMessage()
{
    uint8_t* buf;
    uint32_t msg_sz = 0;
    Dcv__Extensions__DcvMessage* msg;
    HANDLE input_handle = GetStdHandle(STD_INPUT_HANDLE);

    if (input_handle == INVALID_HANDLE_VALUE) {
        log_f("Error getting stdin handle: 0x%X", GetLastError());
        return NULL;
    }

    /*
     * Read size of message, 32 bits
     */
    if (!ReadFromHandle(input_handle, (uint8_t*)&msg_sz, sizeof(msg_sz))) {
        log_f("Error reading message size: 0x%X", GetLastError());
        return NULL;
    }

    buf = calloc(1, msg_sz);
    if (buf == NULL) {
        log_f("Could not allocate buffer to read message: 0x%X", GetLastError());
        return NULL;
    }

    /*
     * Read message and unpack it
     */
    if (!ReadFromHandle(input_handle, buf, msg_sz)) {
        log_f("Error reading message body: 0x%X", GetLastError());
        free(buf);
        return NULL;
    }

    log_f("Received message, %i bytes", msg_sz);

    msg = dcv__extensions__dcv_message__unpack(NULL, msg_sz, buf);
    free(buf);
    if (msg == NULL) {
        log_f("Could not unpack message from std input");
    }

    return msg;
}

void
WriteRequest(Dcv__Extensions__Request* request)
{
    Dcv__Extensions__ExtensionMessage extension_msg = DCV__EXTENSIONS__EXTENSION_MESSAGE__INIT;

    extension_msg.msg_case = DCV__EXTENSIONS__EXTENSION_MESSAGE__MSG_REQUEST;
    extension_msg.request = request;

    WriteMessage(&extension_msg);
}

void
RequestVirtualChannel()
{
    UUID req_id;

    Dcv__Extensions__Request request = DCV__EXTENSIONS__REQUEST__INIT;
    Dcv__Extensions__SetupVirtualChannelRequest msg = DCV__EXTENSIONS__SETUP_VIRTUAL_CHANNEL_REQUEST__INIT;

    msg.virtual_channel_name = CHANNEL_NAME;
    msg.relay_client_process_id = GetCurrentProcessId();

    log_f("About to send SetupVirtualChannelRequest with virtual_channel_name = '%s', relay_client_process_id = %lld",
          msg.virtual_channel_name,
          msg.relay_client_process_id);

    request.request_case = DCV__EXTENSIONS__REQUEST__REQUEST_SETUP_VIRTUAL_CHANNEL_REQUEST;
    request.setup_virtual_channel_request = &msg;
    UuidCreate(&req_id);
    UuidToStringA(&req_id, &request.request_id);
    // TODO: handle errors from uuid API

    WriteRequest(&request);

    RpcStringFreeA(&request.request_id);
}

void
CloseVirtualChannel()
{
    // TODO: actually call local closure
    UUID req_id;

    Dcv__Extensions__Request request = DCV__EXTENSIONS__REQUEST__INIT;
    Dcv__Extensions__CloseVirtualChannelRequest msg = DCV__EXTENSIONS__CLOSE_VIRTUAL_CHANNEL_REQUEST__INIT;

    msg.virtual_channel_name = CHANNEL_NAME;

    request.request_case = DCV__EXTENSIONS__REQUEST__REQUEST_CLOSE_VIRTUAL_CHANNEL_REQUEST;
    request.close_virtual_channel_request = &msg;
    UuidCreate(&req_id);
    UuidToStringA(&req_id, &request.request_id);
    // TODO: handle errors from uuid API

    WriteRequest(&request);

    RpcStringFreeA(&request.request_id);
}

BOOL
WriteToHandle(HANDLE handle,
              uint8_t* buffer,
              DWORD size)
{
    DWORD bytes_written = 0;

    while (bytes_written < size) {
        DWORD curr_written;
        uint8_t* offset_buf = buffer + bytes_written;
        DWORD remaining_bytes = size - bytes_written;

        if (!WriteFile(handle, offset_buf, remaining_bytes, &curr_written, NULL)) {
            log_f("Could not write to handle: 0x%X", GetLastError());
            return FALSE;
        }

        bytes_written += curr_written;
    }

    return TRUE;
}

void
WriteMessage(Dcv__Extensions__ExtensionMessage* msg)
{
    uint32_t msg_sz;
    uint8_t* buf;
    HANDLE output_handle = GetStdHandle(STD_OUTPUT_HANDLE);

    if (output_handle == INVALID_HANDLE_VALUE) {
        log_f("Error getting stdout handle: 0x%X", GetLastError());
        return;
    }

    msg_sz = (uint32_t)dcv__extensions__extension_message__get_packed_size(msg);
    if (msg_sz == 0) {
        log_f("Could not pack message into buffer");
        return;
    }

    buf = calloc(1, msg_sz);
    if (buf == NULL) {
        log_f("Could not allocate buffer to write message: 0x%X", GetLastError());
        return;
    }

    dcv__extensions__extension_message__pack(msg, buf);

    /*
     * Write size of message, 32 bits
     */
    if (!WriteToHandle(output_handle, (uint8_t*)&msg_sz, sizeof(msg_sz))) {
        free(buf);
        return;
    }

    /*
     * Write message
     */
    WriteToHandle(output_handle, buf, msg_sz);
    FlushFileBuffers(output_handle);

    free(buf);
}

HANDLE
SetupAndConnectNamedPipe(const char* relay_path)
{
    HANDLE named_pipe_handle;

    while (TRUE) {
        BOOL res;

        named_pipe_handle = CreateFileA(
            relay_path,
            GENERIC_READ | GENERIC_WRITE,
            0,
            NULL,
            OPEN_EXISTING,
            0,
            NULL);

        if (named_pipe_handle != INVALID_HANDLE_VALUE) {
            break;
        }

        res = GetLastError();
        if (res != ERROR_PIPE_BUSY) {
            log_f("Failed to open pipe with error: 0x%x", res);

            break;
        }

        if (!WaitNamedPipeA(relay_path, 10000)) {
            log_f("Failed to open pipe, timeout reached");

            break;
        }
    }

    return named_pipe_handle;
}

int
main()
{
    Dcv__Extensions__DcvMessage* msg;
    HANDLE named_pipe_handle;
    int num_message = 0;
    DWORD written_bytes;

    sprintf(log_file, "%s_%i.log", LOG_FILE, GetCurrentProcessId());
    log_init(log_file);

    log_f("Sending request to setup virtual channel");

    RequestVirtualChannel();

    log_f("Reading response");

    msg = ReadNextMessage();
    if (msg == NULL) {
        log_f("Could not get messages from stdin");
        return -1;
    }

    // Expecting a response
    if (msg->msg_case != DCV__EXTENSIONS__DCV_MESSAGE__MSG_RESPONSE) {
        log_f("Unexpected message case %u", msg->msg_case);
        dcv__extensions__dcv_message__free_unpacked(msg, NULL);
        return -1;
    }

    if (msg->response->response_case != DCV__EXTENSIONS__RESPONSE__RESPONSE_SETUP_VIRTUAL_CHANNEL_RESPONSE) {
        log_f("Unexpected response case %u", msg->response->response_case);
        dcv__extensions__dcv_message__free_unpacked(msg, NULL);
        return -1;
    }

    if (msg->response->status != DCV__EXTENSIONS__RESPONSE__STATUS__SUCCESS) {
        log_f("Error in response for setup request %u", msg->response->status);
        dcv__extensions__dcv_message__free_unpacked(msg, NULL);
        return -1;
    }

    log_f("Response successful, connecting to named pipe: %s",
          msg->response->setup_virtual_channel_response->relay_path);

    // Connect to named pipe
    named_pipe_handle = SetupAndConnectNamedPipe(msg->response->setup_virtual_channel_response->relay_path);
    if (named_pipe_handle == INVALID_HANDLE_VALUE) {
        log_f("Failed to create and setup named pipe");
        dcv__extensions__dcv_message__free_unpacked(msg, NULL);
        return -1;
    }

    // Send auth token on pipe
    if (!WriteFile(named_pipe_handle,
                   msg->response->setup_virtual_channel_response->virtual_channel_auth_token.data,
                   msg->response->setup_virtual_channel_response->virtual_channel_auth_token.len,
                   &written_bytes,
                   NULL)) {
        log_f("WriteFile of auth token failed with error 0x%x", GetLastError());

        return -1;
    }

    if (FlushFileBuffers(named_pipe_handle) == 0) {
        log_f("FlushFileBuffers of vc pipe failed with error 0x%x", GetLastError());

        return -1;
    }

    dcv__extensions__dcv_message__free_unpacked(msg, NULL);

    log_f("Waiting for pipe ready event");

    // Wait for the event
    msg = ReadNextMessage();
    if (msg == NULL) {
        log_f("Could not get messages from stdin");
        return -1;
    }

    // Expecting an event
    if (msg->msg_case != DCV__EXTENSIONS__DCV_MESSAGE__MSG_EVENT) {
        log_f("Unexpected message case %u", msg->msg_case);
        dcv__extensions__dcv_message__free_unpacked(msg, NULL);
        return -1;
    }

    // Expecting a setup event
    if (msg->event->event_case != DCV__EXTENSIONS__EVENT__EVENT_VIRTUAL_CHANNEL_READY_EVENT) {
        log_f("Unexpected event case %u", msg->event->event_case);
        dcv__extensions__dcv_message__free_unpacked(msg, NULL);
        return -1;
    }

    log_f("Beginning write and read loop");

    // Write to / Read from named pipe
    for (num_message = 0; num_message < 100; ++num_message) {
        char message[100];
        DWORD read_bytes;

        sprintf(message, "Echo Test %i", num_message);

        log_f("Write: %s", message);

        if (!WriteFile(named_pipe_handle, message, sizeof(message) + 1, &written_bytes, NULL)) {
            log_f("WriteFile failed with error 0x%x", GetLastError());

            break;
        }

        if (!ReadFile(named_pipe_handle, read_buffer, READ_BUFFER_SIZE - 1, &read_bytes, NULL)) {
            log_f("ReadFile failed with error 0x%x", GetLastError());

            break;
        }

        read_buffer[read_bytes] = '\0';
        log_f("Read: %s", read_buffer);
        memset(read_buffer, 0, READ_BUFFER_SIZE);

        Sleep(1000);
    }

    log_f("Closing named pipe");

    CloseHandle(named_pipe_handle);

    dcv__extensions__dcv_message__free_unpacked(msg, NULL);

    log_f("Sending close virtual channel request");

    CloseVirtualChannel();

    log_f("Waiting for response");

    // Wait for response
    msg = ReadNextMessage();
    if (msg == NULL) {
        log_f("Could not get messages from stdin");
        return -1;
    }

    // Expecting close response
    if (msg->msg_case != DCV__EXTENSIONS__DCV_MESSAGE__MSG_RESPONSE) {
        log_f("Unexpected message case %u", msg->msg_case);
        dcv__extensions__dcv_message__free_unpacked(msg, NULL);
        return -1;
    }

    if (msg->response->status != DCV__EXTENSIONS__RESPONSE__STATUS__SUCCESS) {
        log_f("Error in response for setup request %u", msg->response->status);
        dcv__extensions__dcv_message__free_unpacked(msg, NULL);
        return -1;
    }

    dcv__extensions__dcv_message__free_unpacked(msg, NULL);

    log_f("Exiting");

    // We closed!
    return 0;
}
