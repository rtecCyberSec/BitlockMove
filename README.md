# BitlockMove

Lateral Movement via Bitlocker DCOM & COM Hijacking.

This Proof of Concept (PoC) for Lateral Movement abuses the fact, that some COM Classes configured as `INTERACTIVE USER` will spawn a process in the context of the currently logged on users session.

If those processes are also vulnerable to COM Hijacking, we can configure a COM Hijack via the remote registry, drop a malicious DLL via SMB and trigger loading/execution of this DLL via DCOM.

This technique removes the need to takeover the system plus afterward:
1) Impersonate the target user
2) Steal the target users credentials from LSASS or somewhere else
3) or use alternative techniques to take over the account

Because our code is already getting executed in the context of the logged in user, we can do whatever we want in that context and create less IoCs for alternative techniques.

In this PoC, the CLSID `ab93b6f1-be76-4185-a488-a9001b105b94` - BDEUILauncher Class is used with the IID `IBDEUILauncher`. This function allows us to spawn four different processes, whereas the `BaaUpdate.exe` process is vulnerable to COM Hijacking when being started with any input parameters:

<br>
<div align="center">
    <img src="https://github.com/rtecCyberSec/BitlockMove/blob/main/images/BaaUpdate.png?raw=true" width="500">
</div>
<br>


The CLSID `A7A63E5C-3877-4840-8727-C1EA9D7A4D50` is trying to be loaded, which we can hijack from remote:

<br>
<div align="center">
    <img src="https://github.com/rtecCyberSec/BitlockMove/blob/main/images/BAAClsid.png?raw=true" width="500">
</div>
<br>

As this CLSID is related to Bitlocker, it can only be found on Client systems. Therefore, this PoC only allows Lateral Movement on Client systems, not on Servers.

# Enum Mode

To find out, which users are active on a remote client you can use the enum mode like this:

```bash
BitlockMove.exe mode=enum target=<targetHost>
```

<br>
<div align="center">
    <img src="https://github.com/rtecCyberSec/BitlockMove/blob/main/images/BitlockMoveEnum.png?raw=true" width="500">
</div>
<br>

# Attack mode

To actually execute code on the remote system, you need to specify the target username, the DLL drop path as well as the command to execute:

```bash
BitlockMove.exe mode=attack target=<targetHost> dllpath=C:\windows\temp\pwned.dll targetuser=local\domadm command="cmd.exe /C calc.exe"
```

<br>
<div align="center">
    <img src="https://github.com/rtecCyberSec/BitlockMove/blob/main/images/BitlockMovePoC.png?raw=true" width="500">
</div>
<br>

# OpSec considerations / Detection

The PoC uses a hardcoded DLL, which will always look the same and which will get dropped on the target. It's super easy to build detections on this DLL, so using a self written DLL will less likely get you detected.
With a custom DLL you will also live in a trusted signed process instead of spawning a new one, that's usually what attackers prefer.

Behavior based detection of this technique can be done by checking for
1) Remote COM Hijack of the mentioned CLSID followed by
2) `BaaUpdate.exe` loading a newly dropped DLL from the hijack location
3) `BaaUpdate.exe` spawning suspicious sub-processes

