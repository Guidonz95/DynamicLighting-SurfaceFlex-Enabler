@echo off
title Surface Pro Flex Keyboard - Dynamic Lighting Enabler
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Please run Install.bat as Administrator.
    pause
    exit /b 1
)

echo === Installing Surface Pro Flex Keyboard Dynamic Lighting ===
set "INSTALL_DIR=C:\Program Files\SurfaceFlexLighting"
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

copy /Y "%~dp0config.json" "%INSTALL_DIR%\config.json" >nul
copy /Y "%~dp0src\Bridge\Program.cs" "%INSTALL_DIR%\Program.cs" >nul

echo [1/4] Compiling C# Adaptive Lighting Bridge (built-in .NET)...
set "FX=C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
set "WM=C:\Windows\System32\WinMetadata"
"%FX%\csc.exe" /nologo /platform:x64 /target:winexe /optimize+ /out:"%INSTALL_DIR%\SurfaceFlexControl.exe" /r:"%FX%\System.Runtime.dll" /r:"$FX%\System.Runtime.WindowsRuntime.dll" /r:"%WM%\Windows.Foundation.winmd" /r:"%WM%\Windows.Devices.winmd" "%INSTALL_DIR%\Program.cs"

echo [2/4] Installing Virtual LampArray KMDF Driver (zero external tools)...
certutil -addstore -f "Root" "%~dp0driver\SurfaceVirtualLampArray.cer" >nul
certutil -addstore -f "TrustedPublisher" "%~dp0driver\SurfaceVirtualLampArray.cer" >nul
pnputil /add-driver "%~dp0driver\VirtualLampArray.inf" /install >nul

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$inf = '%~dp0driver\VirtualLampArray.inf'; $hw = 'Root\VirtualSurfaceLampArray';" ^
  "if (-not (Get-PnpDevice -EA SilentlyContinue | Where-Object { $_.InstanceId -match 'VirtualSurface' -or $_.FriendlyName -match 'Surface Pro Flex Keyboard LampArray' })) {" ^
  "  $sig = '[DllImport(\"setupapi.dll\",CharSet=CharSet.Auto,SetLastError=true)]public static extern IntPtr SetupDiCreateDeviceInfoList(ref Guid g,IntPtr h);[DllImport(\"setupapi.dll\",CharSet=CharSet.Auto,SetLastError=true)]public static extern bool SetupDiCreateDeviceInfo(IntPtr s,string n,ref Guid g,string d,IntPtr h,int f,byte[] dev);[DllImport(\"setupapi.dll\",CharSet=CharSet.Auto,SetLastError=true)]public static extern bool SetupDiSetDeviceRegistryProperty(IntPtr s,byte[] dev,uint p,byte[] b,uint sz);[DllImport(\"setupapi.dll\",SetLastError=true)]public static extern bool SetupDiCallClassInstaller(uint f,IntPtr s,byte[] dev);[DllImport(\"newdev.dll\",CharSet=CharSet.Auto,SetLastError=true)]public static extern bool UpdateDriverForPlugAndPlayDevices(IntPtr h,string hw,string inf,uint f,out bool rb);';" ^
  "  $t = Add-Type -MemberDefinition $sig -Name DevInst -Namespace Win32 -PassThru;" ^
  "  $g = [Guid]'4d36e97d-e325-11ce-bfc1-08002be10318'; $s = $t::SetupDiCreateDeviceInfoList([ref]$g, [IntPtr]::Zero);" ^
  "  $dev = New-Object byte[] 32; [BitConverter]::GetBytes(32).CopyTo($dev, 0);" ^
  "  if ($t::SetupDiCreateDeviceInfo($s, 'System', [ref]$g, 'Surface Pro Flex Keyboard LampArray', [IntPtr]::Zero, 1, $dev)) {" ^
  "    $b = [Text.Encoding]::Unicode.GetBytes($hw + \"`0`0\");" ^
  "    [void]$t::SetupDiSetDeviceRegistryProperty($s, $dev, 1, $b, $b.Length);" ^
  "    [void]$t::SetupDiCallClassInstaller(0x19, $s, $dev);" ^
  "    $rb = $false; [void]$t::UpdateDriverForPlugAndPlayDevices([IntPtr]::Zero, $hw, $inf, 1, [ref]$rb);" ^
  "  }" ^
  "}"

echo [3/4] Disabling Windows 11 conflicting bell-curve ALR & setting autostart...
reg add "HKLM\SOFTWARE\Microsoft\Lighting\Backlight" /v EnableBucketedBacklightAutobrightness /t REG_DWORD /d 0 /f >nul
schtasks /create /tn "SurfaceFlexDynamicLighting" /tr "\"%INSTALL_DIR%\SurfaceFlexControl.exe\"" /sc onlogon /rl highest /f >nul

echo [4/4] Starting Bridge...
taskkill /F /IM SurfaceFlexControl.exe >nul 2>&1
start "" "%INSTALL_DIR%\SurfaceFlexControl.exe"

echo.
echo [DONE] Open Settings - Personalization - Dynamic Lighting!
echo Note: Windows requires Test Signing (bcdedit /set testsigning on) for VHF kernel drivers.
pause
