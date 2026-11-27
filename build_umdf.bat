@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
set "REPO=%~dp0..\.."
set "KIT_INC=C:\Program Files (x86)\Windows Kits\10\Include"
set "KIT_LIB=C:\Program Files (x86)\Windows Kits\10\Lib"

cl.exe /nologo /Tp "%REPO%\src\Driver\VirtualLampArrayUmdf.c" /c /Fo"%REPO%\src\Driver\VirtualLampArrayUmdf.obj" /Zp8 /Gy /W3 /D_WIN64 /D_AMD64_ /DAMD64 /DUNICODE /D_UNICODE /DUMDF_VERSION_MAJOR=2 /DUMDF_VERSION_MINOR=35 /I"%KIT_INC%\wdf\umdf\2.35" /I"%KIT_INC%\10.0.26100.0\um" /I"%KIT_INC%\10.0.26100.0\shared"
if errorlevel 1 exit /b 1

link.exe /nologo /DLL /SUBSYSTEM:WINDOWS /DEF:"%REPO%\src\Driver\VirtualLampArrayUmdf.def" /OUT:"%REPO%\driver\VirtualLampArrayUmdf.dll" "%REPO%\src\Driver\VirtualLampArrayUmdf.obj" "%KIT_LIB%\wdf\umdf\x64\2.35\WdfDriverStubUm.lib" "%KIT_LIB%\10.0.26100.0\um\x64\vhfum.lib" "%KIT_LIB%\10.0.26100.0\um\x64\kernel32.lib" "%KIT_LIB%\10.0.26100.0\um\x64\ntdll.lib"
