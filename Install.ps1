$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dst = "C:\Program Files\DynamicLighting-SurfaceFlex"

New-Item -Path "HKLM:\SOFTWARE\Microsoft\Lighting\Backlight" -Force | Out-Null
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Lighting\Backlight" -Name "EnableBucketedBacklightAutobrightness" -Value 0 -Type DWord -Force

$cer = Get-ChildItem "$root\driver" -Filter "*.cer" -EA SilentlyContinue | Select-Object -First 1
if ($cer) { certutil -addstore -f Root $cer.FullName | Out-Null; certutil -addstore -f TrustedPublisher $cer.FullName | Out-Null }
$inf = Get-ChildItem "$root\driver" -Filter "*.inf" -EA SilentlyContinue | Select-Object -First 1
if ($inf) { pnputil /add-driver $inf.FullName /install | Out-Null }

Stop-Process -Name "SurfaceFlexControl" -Force -EA SilentlyContinue
New-Item -ItemType Directory -Path $dst -Force | Out-Null
Copy-Item "$root\config.json" "$dst\config.json" -Force
Copy-Item "$root\src\Bridge\Program.cs" "$dst\Program.cs" -Force

$fx = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$wm = "C:\Windows\System32\WinMetadata"
& "$fx\csc.exe" /nologo /platform:x64 /target:winexe /optimize+ /out:"$dst\SurfaceFlexControl.exe" /r:"$fx\System.Runtime.dll" /r:"$fx\System.Runtime.WindowsRuntime.dll" /r:"$wm\Windows.Foundation.winmd" /r:"$wm\Windows.Devices.winmd" "$dst\Program.cs"

Unregister-ScheduledTask -TaskName "SurfaceFlexAdaptiveLighting" -Confirm:$false -EA SilentlyContinue
$act = New-ScheduledTaskAction -Execute "$dst\SurfaceFlexControl.exe"
$trg = New-ScheduledTaskTrigger -AtLogOn
$prn = New-ScheduledTaskPrincipal -GroupId "BUILTIN\Users" -RunLevel Highest
$set = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 0) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName "SurfaceFlexAdaptiveLighting" -Action $act -Trigger $trg -Principal $prn -Settings $set -Force | Out-Null
Start-ScheduledTask -TaskName "SurfaceFlexAdaptiveLighting"
Write-Host "Installation complete!" -ForegroundColor Green
