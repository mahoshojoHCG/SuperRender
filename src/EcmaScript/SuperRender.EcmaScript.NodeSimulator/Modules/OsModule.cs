using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Node.js `os` module. Values come from <see cref="System.Environment"/> where available.
/// </summary>
public static class OsModule
{
    private static PropertyDescriptor MethodDesc(string name, Func<JsValue, JsValue[], JsValue> impl, int length) =>
        PropertyDescriptor.Data(JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true);

    public static JsDynamicObject Create()
    {
        var o = new JsDynamicObject();
        o.DefineOwnProperty("EOL", PropertyDescriptor.Data(new JsString(System.Environment.NewLine)));
        o.DefineOwnProperty("platform", MethodDesc("platform", (_, _) => new JsString(GetPlatform()), 0));
        o.DefineOwnProperty("arch", MethodDesc("arch", (_, _) => new JsString(GetArch()), 0));
        o.DefineOwnProperty("type", MethodDesc("type", (_, _) => new JsString(GetType_()), 0));
        o.DefineOwnProperty("release", MethodDesc("release", (_, _) => new JsString(System.Environment.OSVersion.Version.ToString()), 0));
        o.DefineOwnProperty("version", MethodDesc("version", (_, _) => new JsString(System.Environment.OSVersion.VersionString), 0));
        o.DefineOwnProperty("hostname", MethodDesc("hostname", (_, _) => new JsString(System.Environment.MachineName), 0));
        o.DefineOwnProperty("homedir", MethodDesc("homedir", (_, _) => new JsString(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)), 0));
        o.DefineOwnProperty("tmpdir", MethodDesc("tmpdir", (_, _) => new JsString(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)), 0));
        o.DefineOwnProperty("endianness", MethodDesc("endianness", (_, _) => new JsString(BitConverter.IsLittleEndian ? "LE" : "BE"), 0));
        o.DefineOwnProperty("totalmem", MethodDesc("totalmem", (_, _) => JsNumber.Create(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes), 0));
        o.DefineOwnProperty("freemem", MethodDesc("freemem", (_, _) =>
            JsNumber.Create(Math.Max(0, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes - GC.GetTotalMemory(forceFullCollection: false))), 0));
        o.DefineOwnProperty("uptime", MethodDesc("uptime", (_, _) => JsNumber.Create(System.Environment.TickCount64 / 1000.0), 0));
        o.DefineOwnProperty("loadavg", MethodDesc("loadavg", (_, _) =>
        {
            var (a, b, c) = GetLoadAverage();
            var arr = new JsArray();
            arr.Push(JsNumber.Create(a));
            arr.Push(JsNumber.Create(b));
            arr.Push(JsNumber.Create(c));
            return arr;
        }, 0));
        o.DefineOwnProperty("cpus", MethodDesc("cpus", (_, _) => BuildCpusArray(), 0));
        o.DefineOwnProperty("userInfo", MethodDesc("userInfo", (_, _) => BuildUserInfo(), 1));
        o.DefineOwnProperty("networkInterfaces", MethodDesc("networkInterfaces", (_, _) => BuildNetworkInterfaces(), 0));
        return o;
    }

    public static string GetPlatform()
    {
        if (OperatingSystem.IsWindows()) return "win32";
        if (OperatingSystem.IsMacOS()) return "darwin";
        if (OperatingSystem.IsLinux()) return "linux";
        if (OperatingSystem.IsFreeBSD()) return "freebsd";
        return "linux";
    }

    public static string GetArch() => RuntimeInformation.OSArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.X86 => "ia32",
        Architecture.Arm => "arm",
        Architecture.Arm64 => "arm64",
        _ => "unknown",
    };

    private static string GetType_()
    {
        if (OperatingSystem.IsWindows()) return "Windows_NT";
        if (OperatingSystem.IsMacOS()) return "Darwin";
        return "Linux";
    }

    private static (double, double, double) GetLoadAverage()
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var text = File.ReadAllText("/proc/loadavg");
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3
                    && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
                    && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var c))
                {
                    return (a, b, c);
                }
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            {
                // getloadavg(3) — link with libc / libSystem
                Span<double> buf = stackalloc double[3];
                unsafe
                {
                    fixed (double* p = buf)
                    {
                        if (NativeGetLoadAvg(p, 3) == 3)
                            return (buf[0], buf[1], buf[2]);
                    }
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return (0, 0, 0);
    }

    [DllImport("libc", EntryPoint = "getloadavg")]
    private static extern unsafe int NativeGetLoadAvg(double* loadavg, int nelem);

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern int NativeGetUid();

    [DllImport("libc", EntryPoint = "getgid")]
    private static extern int NativeGetGid();

    [DllImport("libc", EntryPoint = "sysctlbyname", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern unsafe int NativeSysctlByName(string name, void* oldp, nuint* oldlenp, void* newp, nuint newlen);

    private const int RRF_RT_REG_SZ = 0x00000002;
    private const int RRF_RT_REG_DWORD = 0x00000010;
    private const long HKEY_LOCAL_MACHINE = unchecked((long)0x80000002u);

    [DllImport("advapi32.dll", EntryPoint = "RegGetValueW", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern unsafe int NativeRegGetValue(IntPtr hkey, string subKey, string valueName, int flags, out int type, void* data, ref int dataLen);

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string? RegReadString(string subKey, string value)
    {
        try
        {
            unsafe
            {
                int len = 0;
                var hkey = new IntPtr(HKEY_LOCAL_MACHINE);
                if (NativeRegGetValue(hkey, subKey, value, RRF_RT_REG_SZ, out _, null, ref len) != 0 || len == 0) return null;
                Span<byte> buf = stackalloc byte[Math.Min(len, 1024)];
                if (len > buf.Length) buf = new byte[len];
                fixed (byte* p = buf)
                {
                    if (NativeRegGetValue(hkey, subKey, value, RRF_RT_REG_SZ, out _, p, ref len) != 0) return null;
                }
                var chars = (len / 2) - 1;
                if (chars <= 0) return string.Empty;
                return System.Text.Encoding.Unicode.GetString(buf[..(chars * 2)]);
            }
        }
        catch (DllNotFoundException) { return null; }
        catch (EntryPointNotFoundException) { return null; }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static long? RegReadDword(string subKey, string value)
    {
        try
        {
            unsafe
            {
                int data = 0;
                int len = sizeof(int);
                var hkey = new IntPtr(HKEY_LOCAL_MACHINE);
                if (NativeRegGetValue(hkey, subKey, value, RRF_RT_REG_DWORD, out _, &data, ref len) != 0) return null;
                return data;
            }
        }
        catch (DllNotFoundException) { return null; }
        catch (EntryPointNotFoundException) { return null; }
    }

    private static string? SysctlString(string name)
    {
        if (!(OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())) return null;
        try
        {
            unsafe
            {
                nuint len = 0;
                if (NativeSysctlByName(name, null, &len, null, 0) != 0 || len == 0) return null;
                var buf = new byte[(int)len];
                fixed (byte* p = buf)
                {
                    if (NativeSysctlByName(name, p, &len, null, 0) != 0) return null;
                }
                var s = System.Text.Encoding.UTF8.GetString(buf, 0, (int)len);
                return s.TrimEnd('\0');
            }
        }
        catch (DllNotFoundException) { return null; }
        catch (EntryPointNotFoundException) { return null; }
    }

    private static long SysctlInt64(string name)
    {
        if (!(OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())) return 0;
        try
        {
            unsafe
            {
                long value = 0;
                nuint len = (nuint)sizeof(long);
                if (NativeSysctlByName(name, &value, &len, null, 0) != 0) return 0;
                return value;
            }
        }
        catch (DllNotFoundException) { return 0; }
        catch (EntryPointNotFoundException) { return 0; }
    }

    private static JsArray BuildCpusArray()
    {
        var cpuCount = System.Environment.ProcessorCount;
        var arr = new JsArray();

        string[] models = new string[cpuCount];
        long[] speeds = new long[cpuCount];
        Array.Fill(models, "unknown");

        try
        {
            if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
            {
                int idx = 0;
                string model = "unknown";
                long speed = 0;
                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (line.Length == 0)
                    {
                        if (idx < cpuCount) { models[idx] = model; speeds[idx] = speed; }
                        idx++;
                        model = "unknown"; speed = 0;
                        continue;
                    }
                    var colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    var key = line[..colon].Trim();
                    var val = line[(colon + 1)..].Trim();
                    if (key == "model name" || key == "Processor") model = val;
                    else if (key == "cpu MHz" && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz))
                        speed = (long)mhz;
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                var brand = SysctlString("machdep.cpu.brand_string") ?? "unknown";
                var hz = SysctlInt64("hw.cpufrequency");
                if (hz == 0) hz = SysctlInt64("hw.cpufrequency_max");
                long mhz = hz > 0 ? hz / 1_000_000 : 0;
                for (int i = 0; i < cpuCount; i++) { models[i] = brand; speeds[i] = mhz; }
            }
            else if (OperatingSystem.IsWindows())
            {
                for (int i = 0; i < cpuCount; i++)
                {
                    var sub = $"HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\{i.ToString(CultureInfo.InvariantCulture)}";
                    var name = RegReadString(sub, "ProcessorNameString");
                    var mhz = RegReadDword(sub, "~MHz");
                    if (!string.IsNullOrEmpty(name)) models[i] = name!;
                    if (mhz.HasValue) speeds[i] = mhz.Value;
                }
                if (models[0] == "unknown")
                {
                    var ident = System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
                    if (!string.IsNullOrEmpty(ident)) Array.Fill(models, ident);
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        for (int i = 0; i < cpuCount; i++)
        {
            var cpu = new JsDynamicObject();
            cpu.DefineOwnProperty("model", PropertyDescriptor.Data(new JsString(models[i])));
            cpu.DefineOwnProperty("speed", PropertyDescriptor.Data(JsNumber.Create(speeds[i])));
            var times = new JsDynamicObject();
            times.DefineOwnProperty("user", PropertyDescriptor.Data(JsNumber.Create(0)));
            times.DefineOwnProperty("nice", PropertyDescriptor.Data(JsNumber.Create(0)));
            times.DefineOwnProperty("sys", PropertyDescriptor.Data(JsNumber.Create(0)));
            times.DefineOwnProperty("idle", PropertyDescriptor.Data(JsNumber.Create(0)));
            times.DefineOwnProperty("irq", PropertyDescriptor.Data(JsNumber.Create(0)));
            cpu.DefineOwnProperty("times", PropertyDescriptor.Data(times));
            arr.Push(cpu);
        }
        return arr;
    }

    private static JsDynamicObject BuildUserInfo()
    {
        var u = new JsDynamicObject();
        u.DefineOwnProperty("username", PropertyDescriptor.Data(new JsString(System.Environment.UserName)));

        int uid = -1, gid = -1;
        if (!OperatingSystem.IsWindows())
        {
            try { uid = NativeGetUid(); gid = NativeGetGid(); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        u.DefineOwnProperty("uid", PropertyDescriptor.Data(JsNumber.Create(uid)));
        u.DefineOwnProperty("gid", PropertyDescriptor.Data(JsNumber.Create(gid)));

        JsValue shell = JsValue.Null;
        if (OperatingSystem.IsWindows())
        {
            var s = System.Environment.GetEnvironmentVariable("ComSpec");
            if (!string.IsNullOrEmpty(s)) shell = new JsString(s);
        }
        else
        {
            var s = System.Environment.GetEnvironmentVariable("SHELL");
            if (!string.IsNullOrEmpty(s)) shell = new JsString(s);
        }
        u.DefineOwnProperty("shell", PropertyDescriptor.Data(shell));
        u.DefineOwnProperty("homedir", PropertyDescriptor.Data(new JsString(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile))));
        return u;
    }

    private static JsDynamicObject BuildNetworkInterfaces()
    {
        var result = new JsDynamicObject();
        NetworkInterface[] interfaces;
        try { interfaces = NetworkInterface.GetAllNetworkInterfaces(); }
        catch (NetworkInformationException) { return result; }
        catch (PlatformNotSupportedException) { return result; }

        foreach (var ni in interfaces)
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            var props = ni.GetIPProperties();
            var mac = FormatMac(ni.GetPhysicalAddress().GetAddressBytes());
            var isInternal = ni.NetworkInterfaceType == NetworkInterfaceType.Loopback;

            var arr = new JsArray();
            foreach (var addr in props.UnicastAddresses)
            {
                var family = addr.Address.AddressFamily;
                if (family != AddressFamily.InterNetwork && family != AddressFamily.InterNetworkV6) continue;

                var entry = new JsDynamicObject();
                var addrStr = addr.Address.ToString();
                var scopeIdx = addrStr.IndexOf('%');
                if (scopeIdx >= 0) addrStr = addrStr[..scopeIdx];

                entry.DefineOwnProperty("address", PropertyDescriptor.Data(new JsString(addrStr)));
                entry.DefineOwnProperty("netmask", PropertyDescriptor.Data(new JsString(GetNetmask(addr))));
                entry.DefineOwnProperty("family", PropertyDescriptor.Data(new JsString(family == AddressFamily.InterNetwork ? "IPv4" : "IPv6")));
                entry.DefineOwnProperty("mac", PropertyDescriptor.Data(new JsString(mac)));
                entry.DefineOwnProperty("internal", PropertyDescriptor.Data(isInternal ? JsValue.True : JsValue.False));
                entry.DefineOwnProperty("cidr", PropertyDescriptor.Data(new JsString($"{addrStr}/{addr.PrefixLength}")));
                if (family == AddressFamily.InterNetworkV6)
                {
                    entry.DefineOwnProperty("scopeid", PropertyDescriptor.Data(JsNumber.Create(addr.Address.ScopeId)));
                }
                arr.Push(entry);
            }

            if (arr.DenseLength == 0) continue;
            result.DefineOwnProperty(ni.Name, PropertyDescriptor.Data(arr, writable: true, enumerable: true, configurable: true));
        }
        return result;
    }

    private static string GetNetmask(UnicastIPAddressInformation addr)
    {
        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
        {
            try { return addr.IPv4Mask.ToString(); }
            catch (PlatformNotSupportedException) { return PrefixToMaskV4(addr.PrefixLength); }
            catch (NotImplementedException) { return PrefixToMaskV4(addr.PrefixLength); }
        }
        return PrefixToMaskV6(addr.PrefixLength);
    }

    private static string PrefixToMaskV4(int prefix)
    {
        if (prefix <= 0) return "0.0.0.0";
        if (prefix >= 32) return "255.255.255.255";
        uint m = prefix == 0 ? 0 : 0xFFFFFFFFu << (32 - prefix);
        return $"{(m >> 24) & 0xFF}.{(m >> 16) & 0xFF}.{(m >> 8) & 0xFF}.{m & 0xFF}";
    }

    private static string PrefixToMaskV6(int prefix)
    {
        var bytes = new byte[16];
        for (int i = 0; i < 16 && prefix > 0; i++)
        {
            int bits = Math.Min(8, prefix);
            bytes[i] = (byte)(0xFF << (8 - bits));
            prefix -= bits;
        }
        return new IPAddress(bytes).ToString();
    }

    private static string FormatMac(byte[] bytes)
    {
        if (bytes.Length == 0) return "00:00:00:00:00:00";
        var sb = new System.Text.StringBuilder(bytes.Length * 3);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(':');
            sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
