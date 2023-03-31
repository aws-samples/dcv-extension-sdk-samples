@echo off

set CWD=%cd%

cd %~dp0
git clone https://github.com/microsoft/vcpkg --depth 1

cd vcpkg
call bootstrap-vcpkg.bat
vcpkg install protobuf-c --x-install-root=..\protobuf-c

cd %CWD%
