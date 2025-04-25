using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Management;
using static System.Collections.Specialized.BitVector32;
using System.Collections.Generic;
using System.Security.Principal;
using System.Data;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices;

namespace BitlockMove
{
    static class SessionEnum
    {
        [DllImport("winsta.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr WinStationOpenServerW(string serverName);

        [DllImport("winsta.dll", SetLastError = true)]
        public static extern bool WinStationCloseServer(IntPtr hServer);

        [DllImport("winsta.dll", SetLastError = true)]
        public static extern bool WinStationEnumerateW(IntPtr hServer, out IntPtr ppSessionIds, out uint count);

        [DllImport("winsta.dll", SetLastError = true)]
        public static extern bool WinStationQueryInformationW(IntPtr hServer, uint sessionId, int infoClass, IntPtr pInfo, uint infoSize, out uint returnLength);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SessionIdW
        {
            public uint SessionId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string WinStationName;
            public int State;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROTOCOLCOUNTERS
        {
            public int WdBytes;
            public int WdFrames;
            public int WaitForOutBuf;
            public int Frames;
            public int Bytes;
            public int CompressedBytes;
            public int CompressFlushes;
            public int Errors;
            public int Timeouts;
            public int AsyncFramingError;
            public int AsyncOverrunError;
            public int AsyncOverflowError;
            public int AsyncParityError;
            public int TdErrors;
            public short ProtocolType;
            public short Length;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
            public int[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CACHE_STATISTICS
        {
            private readonly short ProtocolType;
            private readonly short Length;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            private readonly int[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROTOCOLSTATUS
        {
            public PROTOCOLCOUNTERS Output;
            public PROTOCOLCOUNTERS Input;
            public CACHE_STATISTICS Statistics;
            public int AsyncSignal;
            public int AsyncSignalMask;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WINSTATIONINFORMATIONW
        {
            public ConnectionState State;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string WinStationName;

            public int SessionId;
            public int Unknown;
            public FILETIME ConnectTime;
            public FILETIME DisconnectTime;
            public FILETIME LastInputTime;
            public FILETIME LoginTime;
            public PROTOCOLSTATUS ProtocolStatus;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 18)]
            public string Domain;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 24)]
            public string UserName;

            public FILETIME CurrentTime;
        }

        public static void enumerate(string serverName)
        {

            IntPtr hServer = WinStationOpenServerW(serverName);
            if (hServer == IntPtr.Zero)
            {
                Console.WriteLine("[-] Failed to open server.");
                return;
            }
            //Console.WriteLine($"Server Handle: 0x{hServer.ToInt64():X}");

            IntPtr pSessionIds;
            uint count;
            if (WinStationEnumerateW(hServer, out pSessionIds, out count))
            {
                Console.WriteLine($"[*] Number of sessions: {count}");
                int structSize = Marshal.SizeOf(typeof(SessionIdW));
                for (uint i = 0; i < count; i++)
                {
                    IntPtr currentPtr = new IntPtr(pSessionIds.ToInt64() + (i * structSize));
                    SessionIdW session = Marshal.PtrToStructure<SessionIdW>(currentPtr);
                    Console.WriteLine("\r\n");
                    Console.WriteLine($"SessionID: {session.SessionId}");
                    Console.WriteLine($"State: {session.State}");
                    Console.WriteLine($"SessionName: {session.WinStationName}");

                    WINSTATIONINFORMATIONW wsInfo = new WINSTATIONINFORMATIONW();
                    IntPtr pInfo = Marshal.AllocHGlobal(Marshal.SizeOf(wsInfo));
                    uint returnLength;
                    if (WinStationQueryInformationW(hServer, session.SessionId, 8, pInfo, (uint)Marshal.SizeOf(wsInfo), out returnLength))
                    {
                        wsInfo = Marshal.PtrToStructure<WINSTATIONINFORMATIONW>(pInfo);
                        string userName = wsInfo.Domain + "\\" + wsInfo.UserName;
                        Console.WriteLine($"UserName: {userName}");
                    }
                    else
                    {
                        Console.WriteLine($"[-] Failed to query session info for SessionName: {session.WinStationName}");
                    }
                    Marshal.FreeHGlobal(pInfo);
                }
                Marshal.FreeHGlobal(pSessionIds);
            }
            else
            {
                Console.WriteLine("[-] Failed to enumerate sessions.");
            }

            if (!WinStationCloseServer(hServer))
            {
                Console.WriteLine("[-] Failed to close server handle.");
            }
        }
    }
}
