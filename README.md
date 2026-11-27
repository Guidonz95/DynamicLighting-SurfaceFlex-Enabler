# DynamicLighting-SurfaceFlex-Enabler

Native **Windows 11 Dynamic Lighting** (`LampArray`) and **Inverse Adaptive Backlight** enabler for the **Microsoft Surface Pro Flex Keyboard** (attached via Surface Pro 11 / 10 / 9 pogo-pin connector).

## Why This Project Exists
Out of the box on Windows 11, the Surface Pro Flex Keyboard exposes its backlight controller over HID (`Col06`, Reports `0xD5` / `0xD6`) without a standard HID `LampArray` (`0x59`) usage page, leaving it missing from **Settings -> Personalization -> Dynamic Lighting**. Furthermore, Windows' built-in Ambient Light Response (ALR) dims the keys in the dark and turns them up in bright rooms.

This project solves both problems:
1. **Virtual HID LampArray Kernel Driver (`VirtualLampArray.sys`)**: Uses the Windows Virtual HID Framework (`vhf.sys`) to expose the Surface Pro Flex Keyboard as a native `LampArray` device in Windows 11 Dynamic Lighting Settings (brightness slider + native effects).
2. **Adaptive Lighting & Hardware Sync Bridge (`SurfaceFlexControl.exe`)**:
   - Reads the Surface Ambient Light Sensor (`Windows.Devices.Sensors.LightSensor`) and applies an **inverse logarithmic curve** (brightest `12/12` in the dark, `0/12` in bright daylight).
   - Includes **18% lux hysteresis** so manual changes via the **physical keyboard backlight key** (`0xD6`) or **Windows Dynamic Lighting slider** stay locked until ambient lighting changes significantly.
   - Disables Windows' conflicting bell-curve ALR (`EnableBucketedBacklightAutobrightness = 0`).

## Requirements & Installation
Because Windows 11 requires kernel-mode (`KMDF`) callers for `VhfCreate` (`vhf.sys`), **Test Signing** must be enabled:

1. Open **PowerShell as Administrator** and enable Test Signing (suspending BitLocker for 1 reboot if active):
   ```powershell
   Suspend-BitLocker -MountPoint "C:" -RebootCount 1
   bcdedit /set testsigning on
   ```
2. Reboot your Surface.
3. Right-click **`Install.bat`** -> **Run as administrator**.
4. Open **Settings -> Personalization -> Dynamic Lighting** to control your Surface Pro Flex Keyboard.

## Configuration (`config.json`)
Edit `C:\Program Files\SurfaceFlexLighting\config.json` (reloaded live every ~5 seconds without restarting):
- `MaxLuxCutoff`: Ambient lux above which backlight turns off (`250.0` default).
- `MaxBrightnessDarkPercent`: Maximum brightness in complete darkness (`100` default = hardware level `12/12`).
- `MinBrightnessRoomPercent`: Minimum brightness at or above `MaxLuxCutoff` (`0` default).
- `HysteresisPercent`: Ambient lux change threshold required before auto-brightness overrides a manual key/slider adjustment (`18.0` default).
- `InvertCurve`: `true` for inverse curve (brighter in dark), `false` for linear curve.
