## NICE DCV Extension SDK Samples

NICE DCV is a high-performance remote display protocol. It lets you securely deliver remote desktops and application streaming from any cloud or data center to any device, over varying network conditions. By using NICE DCV, you can run graphics-intensive applications remotely. You can then stream the results to more modest client machines, which eliminates the need for expensive dedicated workstations.

With the DCV Extension SDK, developers can integrate DCV protocol with their applications. The following are typical use cases:
- Provide high-level device redirection for custom hardware devices in remote sessions.
- Establish virtual channels between DCV Server and DCV Client to enhance remote application usability.
- Describe the DCV client and DCV server runtime components and allow applications to interact with them.
A DCV extension may communicate with either a DCV client or a DCV server, depending on where it is installed. In addition, the DCV extension could request a virtual channel via the DCV protocol and then use this virtual channel to send arbitrary data.


This repository contains sample code that could help with getting started with DCV Extension SDK

### C# example (Virtual Channels)

Project DcvExtensionVirtualChannelsCS.
This example shows the following:

* Getting protobuf as nuget dependency
* Compiling extensions.proto into a C# set of classes
* Establishing communication channels with DCV over standard input and standard output using asynchronous IO
* Setting up a virtual channel
* Connecting to the named pipe of the virtual channel created by DCV and sending/receiving data using asynchronous IO

### C example (Virtual Channels)

Project dcvextension-c.
This example shows the following:

* Using win32 APIs for the communication over standard stream and named pipes
* Simple approach using synchronous IO

This example requires an additional tool, protobuf-c, to compile extensions.proto as C headers and functions.
Protobuf-c is available here:
https://github.com/protobuf-c/protobuf-c
The protobuf C compiler and the link time library must be build form the protobuf-c sources.

### C++ example (Virtual Channels)

Project dcvextension-cpp.
This example shows the following:

* Using win32 APIs for the communication over standard stream and named pipes
* Simple approach using synchronous IO

This example requires an additional library to be compiled. You need the google protobuf compiler and runtime.
You can execute the setup_protobuf.bat script in the example folder to download and build protobuf. To do that it requires to have installed git and Visual Studio 2017 or newer (please note that if you have multiple versions of Visual Studio installed on your machine, protobuf will be built using the newest one and then you will have to also build the example using the same version)

The setup_protobuf.bat script is just an utility that performs what described at  https://github.com/protocolbuffers/protobuf/tree/main/src#c-protobuf---windows and https://github.com/microsoft/vcpkg#quick-start-windows

### Rust example (Virtual Channels)

Project dcvextension-rs.
This example shows the following:

* Build an extension for macOS, Linux, and Windows
* Compiling extensions.proto into a Rust set of definitions
* Setting up and exchange data over a virtual channel

### C# example (Geometry)

Project DcvExtensionGeometryCS
This example shows the following:

* Getting protobuf as nuget dependency
* Compiling extensions.proto into a C# set of classes
* Requesting the server layout and local streaming views and receiving the response synchronously
* Receiving the streaming views asynchronously
* Discovering if a screen point (current position of the mouse in the example) is over a visible pixel of a streaming area
* Display the pointer cursor on a given position of a streaming area 

### C# example with GUI (Geometry)

Project DcvExtensionGeometryGuiCS
This example shows the following:

* Getting protobuf as nuget dependency
* Compiling extensions.proto into a C# set of classes
* Requesting the server layout and local streaming views and receiving the response synchronously
* Receiving the streaming views asynchronously
* Discovering if a screen point (current position of the mouse in the example) is over a visible pixel of a streaming area
* Display the pointer cursor on a given position of a streaming area



## Support/Contact Us

If you have any questions regarding this beta program, please reach out to <aws-dcv-extensions-support@amazon.com>. 

Please note that this support is not subject to the standard AWS Support SLAs. We will provide support on a best effort basis and respond to your questions as soon as possible.   


To provide feedback, please file an issue.

## License

This library is licensed under the MIT-0 License. See the LICENSE file.
