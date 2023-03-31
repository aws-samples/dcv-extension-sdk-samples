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
#include "../generated/extensions.pb.h"

#include <stdio.h>
#include <windows.h>
#include "simplelogger.h"

#define LOG_FILE "C:\\Temp\\DcvExtensionVirtualChannelsCPP"

enum
{
    READ_BUFFER_SIZE = 4096
};

using namespace dcv::extensions;

int last_request_id = 1;

char log_file[sizeof LOG_FILE + 20];
const std::string CHANNEL_NAME = "echo";

void
WriteMessage(ExtensionMessage& msg);

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

        if (!ReadFile(handle, offset_buf, remaining_bytes, &curr_read, nullptr)) {
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

DcvMessage*
ReadNextMessage()
{
    uint32_t msg_sz = 0;

    HANDLE input_handle = GetStdHandle(STD_INPUT_HANDLE);

    if (input_handle == INVALID_HANDLE_VALUE) {
        log_f("Error getting stdin handle: 0x%X", GetLastError());
        return nullptr;
    }

    /*
     * Read size of message, 32 bits
     */
    if (!ReadFromHandle(input_handle, (uint8_t*)&msg_sz, sizeof msg_sz)) {
        return nullptr;
    }

    uint8_t* buf = static_cast<uint8_t*>(malloc(msg_sz));

    /*
     * Read message and unpack it
     */
    if (!ReadFromHandle(input_handle, buf, msg_sz)) {
        return nullptr;
    }

    DcvMessage* msg = new DcvMessage();
    if (!msg->ParseFromArray(buf, msg_sz)) {
        log_f("Could not unpack message from std input");
        delete msg;
        return nullptr;
    }

    return msg;
}

void
WriteRequest(Request* request)
{
    ExtensionMessage extension_msg;

    extension_msg.set_allocated_request(request);

    WriteMessage(extension_msg);
}

void
RequestVirtualChannel()
{
    auto request = new Request();
    auto msg = new SetupVirtualChannelRequest();

    msg->set_virtual_channel_name(CHANNEL_NAME);
    msg->set_relay_client_process_id(GetCurrentProcessId());

    request->set_allocated_setup_virtual_channel_request(msg);
    request->set_request_id(std::to_string(last_request_id++));

    WriteRequest(request);
}

void
CloseVirtualChannel()
{
    // TODO: actually call local closure

    auto request = new Request();
    auto msg = new CloseVirtualChannelRequest();

    msg->set_virtual_channel_name(CHANNEL_NAME);

    request->set_allocated_close_virtual_channel_request(msg);
    request->set_request_id(std::to_string(last_request_id++));

    WriteRequest(request);
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

        if (!WriteFile(handle, offset_buf, remaining_bytes, &curr_written, nullptr)) {
            log_f("Could not write to handle: 0x%X", GetLastError());
            return FALSE;
        }

        bytes_written += curr_written;
    }

    return TRUE;
}

void
WriteMessage(ExtensionMessage& msg)
{
    int msg_sz;
    HANDLE output_handle = GetStdHandle(STD_OUTPUT_HANDLE);

    if (output_handle == INVALID_HANDLE_VALUE) {
        log_f("Error getting stdout handle: 0x%X", GetLastError());
        return;
    }

    msg_sz = static_cast<int>(msg.ByteSizeLong());
    uint8_t* buf = static_cast<uint8_t*>(malloc(msg_sz));
    msg.SerializeToArray(buf, msg_sz);

    /*
     * Write size of message, 32 bits
     */
    if (!WriteToHandle(output_handle, reinterpret_cast<uint8_t*>(&msg_sz), sizeof msg_sz)) {
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
SetupAndConnectNamedPipe(const std::string& relay_path)
{
    HANDLE named_pipe_handle;

    while (TRUE) {
        named_pipe_handle = CreateFileA(
            relay_path.c_str(),
            GENERIC_READ | GENERIC_WRITE,
            0,
            nullptr,
            OPEN_EXISTING,
            0,
            nullptr);

        if (named_pipe_handle != INVALID_HANDLE_VALUE) {
            break;
        }

        BOOL res = GetLastError();
        if (res != ERROR_PIPE_BUSY) {
            log_f("Failed to open pipe with error: 0x%x", res);

            break;
        }

        if (!WaitNamedPipeA(relay_path.c_str(), 10000)) {
            log_f("Failed to open pipe, timeout reached");

            break;
        }
    }

    return named_pipe_handle;
}

int
main()
{
    DWORD written_bytes;

    sprintf_s(log_file, "%s_%i.log", LOG_FILE, GetCurrentProcessId());
    log_init(log_file);

    log_f("RequestVirtualChannel");

    RequestVirtualChannel();

    DcvMessage* msg = ReadNextMessage();
    if (msg == nullptr) {
        log_f("Could not get messages from stdin");
        return -1;
    }

    log_f("Expecting a response");

    // Expecting a response
    if (!msg->has_response()) {
        log_f("Unexpected message case %u", msg->msg_case());
        delete msg;
        return -1;
    }

    if (msg->response().status() != Response_Status_SUCCESS) {
        log_f("Error in response for setup request %u", msg->response().status());
        delete msg;
        return -1;
    }

    log_f("Connect to named pipe");

    // Connect to named pipe
    HANDLE named_pipe_handle = SetupAndConnectNamedPipe(msg->response().setup_virtual_channel_response().relay_path());
    if (named_pipe_handle == INVALID_HANDLE_VALUE) {
        log_f("Failed to create and setup named pipe");
        delete msg;
        return -1;
    }

    log_f("Writing auth token on named pipe");

    BOOL res = WriteFile(named_pipe_handle,
                         msg->response().setup_virtual_channel_response().virtual_channel_auth_token().c_str(),
                         msg->
                         response().setup_virtual_channel_response().virtual_channel_auth_token().
                         length(),
                         &written_bytes,
                         nullptr);

    delete msg;

    if (!res) {
        log_f("WriteFile failed with error 0x%x", GetLastError());

        return -1;
    }

    log_f("Wait for the event");

    // Wait for the event
    msg = ReadNextMessage();
    if (msg == nullptr) {
        log_f("Could not get messages from stdin");
        return -1;
    }

    // Expecting an event
    if (!msg->has_event()) {
        log_f("Unexpected message case %u", msg->msg_case());
        delete msg;
        return -1;
    }

    // Expecting a setup event
    if (msg->event().event_case() != Event::kVirtualChannelReadyEvent) {
        log_f("Unexpected event case %u", msg->event().event_case());
        delete msg;
        return -1;
    }

    log_f("Write to / Read from named pipe");

    // Write to / Read from named pipe
    for (int msg_number = 0; msg_number < 100; ++msg_number) {
        DWORD read_bytes;
        char read_buffer[READ_BUFFER_SIZE];
        std::string message = "C++ Test " + std::to_string(msg_number);

        log_f("Write: '%s'", message);

        if (!WriteFile(named_pipe_handle, message.c_str(), message.length() + 1, &written_bytes,
                       nullptr)) {
            log_f("WriteFile failed with error 0x%x", GetLastError());

            break;
        }

        if (!ReadFile(named_pipe_handle, read_buffer, READ_BUFFER_SIZE - 1, &read_bytes, nullptr)) {
            log_f("ReadFile failed with error 0x%x", GetLastError());

            break;
        }

        read_buffer[read_bytes] = '\0';
        log_f("Read: %s", read_buffer);
        memset(read_buffer, 0, READ_BUFFER_SIZE);

        Sleep(1000);
    }

    CloseHandle(named_pipe_handle);

    delete msg;

    CloseVirtualChannel();

    // Wait for response
    msg = ReadNextMessage();
    if (msg == nullptr) {
        log_f("Could not get messages from stdin");
        return -1;
    }

    // Expecting close response
    if (!msg->has_response()) {
        log_f("Unexpected message case %u", msg->msg_case());
        delete msg;
        return -1;
    }

    if (msg->response().status() != Response_Status_SUCCESS) {
        log_f("Error in response for close request %u", msg->response().status());
        delete msg;
        return -1;
    }

    delete msg;

    // We closed!
    return 0;
}
