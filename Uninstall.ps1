Stop-Process -Name "SurfaceFlexControl" -Force -EA SilentlyContinue
Unregister-ScheduledTask -TaskName "SurfaceFlexAdaptiveLighting" -Confirm:$false -EA SilentlyContinue
Remove-Item "C:\Program Files\DynamicLighting-SurfaceFlex" -Recurse -Force -EA SilentlyContinue
Remove-Item "C:\Program Files\SurfaceFlexLighting" -Recurse -Force -EA SilentlyContinue
Remove-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Lighting\Backlight" -Name "EnableBucketedBacklightAutobrightness" -EA SilentlyContinue
Write-Host "Uninstallation complete." -ForegroundColor Yellow
