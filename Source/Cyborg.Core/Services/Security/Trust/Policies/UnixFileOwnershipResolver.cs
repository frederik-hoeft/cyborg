using System.Runtime.InteropServices;

namespace Cyborg.Core.Services.Security.Trust.Policies;

internal static partial class UnixFileOwnershipResolver
{
    private const int ERANGE = 34;
    private const int INITIAL_ACCOUNT_LOOKUP_BUFFER_SIZE = 1024;
    private const int MAX_ACCOUNT_LOOKUP_BUFFER_SIZE = 1024 * 64;

    public static bool TryGetOwnerAndGroup(string path, [NotNullWhen(true)] out string? userName, [NotNullWhen(true)] out string? groupName)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!OperatingSystem.IsLinux() || RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            userName = null;
            groupName = null;
            return false;
        }

        int status = NativeMethods.LStat(path, out GlibcX64Stat statBuffer);
        if (status != 0)
        {
            userName = null;
            groupName = null;
            return false;
        }
        if (!TryResolveUserName(statBuffer.StUid, out userName) || !TryResolveGroupName(statBuffer.StGid, out groupName))
        {
            userName = null;
            groupName = null;
            return false;
        }
        return true;
    }

    private static bool TryResolveUserName(uint userId, [NotNullWhen(true)] out string? userName)
    {
        int bufferSize = INITIAL_ACCOUNT_LOOKUP_BUFFER_SIZE;
        while (bufferSize <= MAX_ACCOUNT_LOOKUP_BUFFER_SIZE)
        {
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                int resultCode = NativeMethods.GetPasswdByUid(userId, out GlibcX64Passwd passwd, buffer, checked((nuint)bufferSize), out IntPtr result);
                if (resultCode == 0)
                {
                    if (result == IntPtr.Zero)
                    {
                        userName = null;
                        return false;
                    }

                    userName = Marshal.PtrToStringUTF8(passwd.PwName);
                    return !string.IsNullOrEmpty(userName);
                }
                if (resultCode != ERANGE)
                {
                    userName = null;
                    return false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            bufferSize *= 2;
        }

        userName = null;
        return false;
    }

    private static bool TryResolveGroupName(uint groupId, [NotNullWhen(true)] out string? groupName)
    {
        int bufferSize = INITIAL_ACCOUNT_LOOKUP_BUFFER_SIZE;
        while (bufferSize <= MAX_ACCOUNT_LOOKUP_BUFFER_SIZE)
        {
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                int resultCode = NativeMethods.GetGroupById(groupId, out GlibcX64Group group, buffer, checked((nuint)bufferSize), out IntPtr result);
                if (resultCode == 0)
                {
                    if (result == IntPtr.Zero)
                    {
                        groupName = null;
                        return false;
                    }

                    groupName = Marshal.PtrToStringUTF8(group.GrName);
                    return !string.IsNullOrEmpty(groupName);
                }
                if (resultCode != ERANGE)
                {
                    groupName = null;
                    return false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            bufferSize *= 2;
        }

        groupName = null;
        return false;
    }

    private static partial class NativeMethods
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)] // search OS libraries only (unfortunately named "System32" on Linux)
        [LibraryImport("libc", EntryPoint = "lstat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        public static partial int LStat(string path, out GlibcX64Stat statBuffer);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)] // search OS libraries only (unfortunately named "System32" on Linux)
        [LibraryImport("libc", EntryPoint = "getpwuid_r")]
        public static partial int GetPasswdByUid(uint userId, out GlibcX64Passwd passwd, IntPtr buffer, nuint bufferLength, out IntPtr result);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)] // search OS libraries only (unfortunately named "System32" on Linux)
        [LibraryImport("libc", EntryPoint = "getgrgid_r")]
        public static partial int GetGroupById(uint groupId, out GlibcX64Group group, IntPtr buffer, nuint bufferLength, out IntPtr result);
    }
}

// Matched against glibc 2.42 amd64 <sys/stat.h> -> <bits/struct_stat.h>
// from libc6-dev_2.42-14_amd64.
[StructLayout(LayoutKind.Sequential)]
internal struct GlibcX64Stat
{
    public ulong StDev;
    public ulong StIno;
    public ulong StNLink;
    public uint StMode;
    public uint StUid;
    public uint StGid;
    public int Padding0;
    public ulong StRDev;
    public long StSize;
    public long StBlkSize;
    public long StBlocks;
    public GlibcTimespec StAtim;
    public GlibcTimespec StMtim;
    public GlibcTimespec StCtim;
    public long GlibcReserved0;
    public long GlibcReserved1;
    public long GlibcReserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GlibcTimespec
{
    public long TvSec;
    public long TvNsec;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GlibcX64Passwd
{
    public IntPtr PwName;
    public IntPtr PwPasswd;
    public uint PwUid;
    public uint PwGid;
    public IntPtr PwGecos;
    public IntPtr PwDir;
    public IntPtr PwShell;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GlibcX64Group
{
    public IntPtr GrName;
    public IntPtr GrPasswd;
    public uint GrGid;
    public IntPtr GrMem; // char**
}
