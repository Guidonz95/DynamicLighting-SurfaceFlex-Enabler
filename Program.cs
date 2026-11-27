using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Windows.Devices.Sensors;

class Program {
    [StructLayout(LayoutKind.Sequential)] struct IFD { public uint cb; public Guid g; public uint f; public IntPtr r; }
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr p, IntPtr min, IntPtr max);
    [DllImport("setupapi.dll", CharSet=CharSet.Auto)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr p, uint f);
    [DllImport("setupapi.dll", CharSet=CharSet.Auto)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref Guid g, uint i, ref IFD ifd);
    [DllImport("setupapi.dll", CharSet=CharSet.Auto)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr s, ref IFD ifd, IntPtr det, uint sz, out uint req, IntPtr d);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);
    [DllImport("kernel32.dll", CharSet=CharSet.Auto)] static extern SafeFileHandle CreateFile(string p, uint a, uint s, IntPtr sa, uint cd, uint f, IntPtr t);
    [DllImport("hid.dll")] static extern bool HidD_SetOutputReport(SafeFileHandle h, IntPtr b, int len);
    [DllImport("hid.dll")] static extern bool HidD_GetFeature(SafeFileHandle h, IntPtr b, int len);

    static double maxLux = 250.0, maxDark = 1.0, minRoom = 0.0, hystRatio = 0.18;
    static bool invert = true;
    static int pollMs = 120;

    static void LoadCfg() {
        try {
            string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (!File.Exists(p)) return;
            string j = File.ReadAllText(p);
            var m1 = Regex.Match(j, "\"MaxLuxCutoff\"\\s*:\\s*([0-9.]+)");
            if (m1.Success) maxLux = double.Parse(m1.Groups.Value, CultureInfo.InvariantCulture);
            var m2 = Regex.Match(j, "\"MaxBrightnessDarkPercent\"\\s*:\\s*([0-9.]+)");
            if (m2.Success) maxDark = Math.Max(0.0, Math.Min(1.0, double.Parse(m2.Groups.Value, CultureInfo.InvariantCulture) / 100.0));
            var m3 = Regex.Match(j, "\"MinBrightnessRoomPercent\"\\s*:\\s*([0-9.]+)");
            if (m3.Success) minRoom = Math.Max(0.0, Math.Min(1.0, double.Parse(m3.Groups.Value, CultureInfo.InvariantCulture) / 100.0));
            var m4 = Regex.Match(j, "\"InvertCurve\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (m4.Success) invert = bool.Parse(m4.Groups.Value);
            var m5 = Regex.Match(j, "\"PollIntervalMs\"\\s*:\\s*([0-9]+)");
            if (m5.Success) pollMs = Math.Max(50, Math.Min(1000, int.Parse(m5.Groups.Value)));
            var m6 = Regex.Match(j, "\"HysteresisPercent\"\\s*:\\s*([0-9.]+)");
            if (m6.Success) hystRatio = Math.Max(0.0, Math.Min(1.0, double.Parse(m6.Groups.Value, CultureInfo.InvariantCulture) / 100.0));
        } catch {}
    }

    static int ReadHwLevel(IntPtr featBuf) {
        int val = -1;
        Guid g = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
        IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (set == IntPtr.Zero) return -1;
        uint i = 0;
        IFD ifd = new IFD { cb = (uint)Marshal.SizeOf(typeof(IFD)) };
        while (SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i++, ref ifd)) {
            uint req = 0;
            SetupDiGetDeviceInterfaceDetail(set, ref ifd, IntPtr.Zero, 0, out req, IntPtr.Zero);
            if (req == 0 || req > 1024) continue;
            IntPtr b = Marshal.AllocHGlobal((int)req);
            Marshal.WriteInt32(b, (IntPtr.Size == 8) ? 8 : 6);
            if (SetupDiGetDeviceInterfaceDetail(set, ref ifd, b, req, out req, IntPtr.Zero)) {
                string p = Marshal.PtrToStringAuto(IntPtr.Add(b, 4));
                if (p != null && p.IndexOf("col06", StringComparison.OrdinalIgnoreCase) >= 0 && p.IndexOf("VHF", StringComparison.OrdinalIgnoreCase) < 0) {
                    using (var h = CreateFile(p, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero)) {
                        if (!h.IsInvalid) {
                            Marshal.WriteInt64(featBuf, 0, 0);
                            Marshal.WriteByte(featBuf, 0, 0xD6);
                            if (HidD_GetFeature(h, featBuf, 5)) {
                                byte v = Marshal.ReadByte(featBuf, 1);
                                if (v <= 12) val = v;
                            }
                        }
                    }
                }
            }
            Marshal.FreeHGlobal(b);
        }
        SetupDiDestroyDeviceInfoList(set);
        return val;
    }

    static void WriteHwLevel(int lvl, IntPtr pkt) {
        Marshal.WriteByte(pkt, 0, 0xD5);
        Marshal.WriteByte(pkt, 1, (byte)lvl);
        Guid g = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
        IntPtr set = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (set == IntPtr.Zero) return;
        uint i = 0;
        IFD ifd = new IFD { cb = (uint)Marshal.SizeOf(typeof(IFD)) };
        while (SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i++, ref ifd)) {
            uint req = 0;
            SetupDiGetDeviceInterfaceDetail(set, ref ifd, IntPtr.Zero, 0, out req, IntPtr.Zero);
            if (req == 0 || req > 1024) continue;
            IntPtr b = Marshal.AllocHGlobal((int)req);
            Marshal.WriteInt32(b, (IntPtr.Size == 8) ? 8 : 6);
            if (SetupDiGetDeviceInterfaceDetail(set, ref ifd, b, req, out req, IntPtr.Zero)) {
                string p = Marshal.PtrToStringAuto(IntPtr.Add(b, 4));
                if (p != null && p.IndexOf("col06", StringComparison.OrdinalIgnoreCase) >= 0 && p.IndexOf("VHF", StringComparison.OrdinalIgnoreCase) < 0) {
                    using (var h = CreateFile(p, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero)) {
                        if (!h.IsInvalid) HidD_SetOutputReport(h, pkt, 2);
                    }
                }
            }
            Marshal.FreeHGlobal(b);
        }
        SetupDiDestroyDeviceInfoList(set);
    }
    static void WriteRegistryBrightness(int pct) {
        try {
            using (var root = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Lighting")) {
                if (root != null) {
                    root.SetValue("Brightness", pct, RegistryValueKind.DWord);
                    if (pct > 0) root.SetValue("AmbientLightingEnabled", 1, RegistryValueKind.DWord);
                    using (var devs = root.OpenSubKey("Devices", true)) {
                        if (devs != null) {
                            foreach (string s in devs.GetSubKeyNames()) {
                                if (s.IndexOf("VHF", StringComparison.OrdinalIgnoreCase) >= 0) {
                                    using (var sk = devs.OpenSubKey(s, true)) {
                                        if (sk != null) {
                                            sk.SetValue("Brightness", pct, RegistryValueKind.DWord);
                                            if (pct > 0) sk.SetValue("AmbientLightingEnabled", 1, RegistryValueKind.DWord);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        } catch {}
    }

    static void Main() {
        LoadCfg();
        LightSensor sensor = null;
        try { sensor = LightSensor.GetDefault(); } catch {}
        IntPtr pkt = Marshal.AllocHGlobal(2);
        IntPtr featBuf = Marshal.AllocHGlobal(8);
        double phase = 0.0, anchorLux = -1.0;
        int lastLvl = -1, tick = 0, prevSlider = -1, manualLvl = -1;

        while (true) {
            try {
                if (tick % 40 == 0) LoadCfg();
                if (sensor == null && tick % 20 == 0) { try { sensor = LightSensor.GetDefault(); } catch {} }

                double currentLux = -1.0;
                if (sensor != null) {
                    var rd = sensor.GetCurrentReading();
                    if (rd != null) currentLux = Math.Max(0.0, rd.IlluminanceInLux);
                }

                int hwNow = ReadHwLevel(featBuf);
                if (lastLvl >= 0 && hwNow >= 0 && hwNow != lastLvl) {
                    Thread.Sleep(350);
                    int settled = ReadHwLevel(featBuf);
                    if (settled >= 0) hwNow = settled;
                    int newPct = (int)Math.Round((hwNow / 12.0) * 100.0);
                    WriteRegistryBrightness(newPct);
                    prevSlider = newPct;
                    manualLvl = hwNow;
                    anchorLux = currentLux;
                    lastLvl = hwNow;
                    Thread.Sleep(pollMs);
                    continue;
                }

                int gEn = 1, gBr = 100, dEn = 1, dBr = 100, eff = 0, spd = 5;
                bool hasDevBr = false;
                using (var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Lighting")) {
                    if (root != null) {
                        gEn = Convert.ToInt32(root.GetValue("AmbientLightingEnabled", 1));
                        gBr = Convert.ToInt32(root.GetValue("Brightness", 100));
                        eff = Convert.ToInt32(root.GetValue("EffectType", 0));
                        spd = Convert.ToInt32(root.GetValue("Speed", 5));
                        using (var devs = root.OpenSubKey("Devices")) {
                            if (devs != null) {
                                foreach (string s in devs.GetSubKeyNames()) {
                                    if (s.IndexOf("VHF", StringComparison.OrdinalIgnoreCase) >= 0) {
                                        using (var sk = devs.OpenSubKey(s)) {
                                            if (sk != null) {
                                                dEn = Convert.ToInt32(sk.GetValue("AmbientLightingEnabled", 1));
                                                object dbObj = sk.GetValue("Brightness", null);
                                                if (dbObj != null) { dBr = Convert.ToInt32(dbObj); hasDevBr = true; }
                                                eff = Convert.ToInt32(sk.GetValue("EffectType", eff));
                                                spd = Convert.ToInt32(sk.GetValue("Speed", spd));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                int effectiveSlider = (gEn == 0 || dEn == 0) ? 0 : (hasDevBr ? dBr : gBr);
                if (prevSlider != -1 && effectiveSlider != prevSlider) {
                    manualLvl = (int)Math.Round((effectiveSlider / 100.0) * 12.0);
                    anchorLux = currentLux;
                }
                prevSlider = effectiveSlider;

                if (manualLvl >= 0 && currentLux >= 0.0 && anchorLux >= 0.0) {
                    double threshold = Math.Max(3.0, anchorLux * hystRatio);
                    if (Math.Abs(currentLux - anchorLux) > threshold) {
                        manualLvl = -1;
                    }
                }

                int target = 0;
                if (gEn == 0 || dEn == 0) {
                    target = 0;
                } else if (manualLvl >= 0) {
                    target = manualLvl;
                } else {
                    double sensorRatio = maxDark;
                    if (currentLux >= 0.0) {
                        if (invert) {
                            sensorRatio = (currentLux >= maxLux) ? minRoom : (minRoom + (maxDark - minRoom) * (1.0 - (Math.Log10(currentLux + 1.0) / Math.Log10(maxLux + 1.0))));
                        } else {
                            sensorRatio = Math.Min(1.0, currentLux / maxLux);
                        }
                    }
                    double effectMult = 1.0;
                    if (eff != 0 && sensorRatio > 0.0) {
                        phase += ((eff == 2) ? 0.14 : 0.07) * Math.Max(1, spd) / 5.0;
                        effectMult = 0.15 + 0.85 * (0.5 + 0.5 * Math.Sin(phase));
                    }
                    target = (int)Math.Round(Math.Max(0.0, Math.Min(12.0, sensorRatio * effectMult * 12.0)));
                }

                if (target != lastLvl) {
                    WriteHwLevel(target, pkt);
                    lastLvl = target;
                }
            } catch {}

            if (++tick % 50 == 0) SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            Thread.Sleep(pollMs);
        }
    }
}
