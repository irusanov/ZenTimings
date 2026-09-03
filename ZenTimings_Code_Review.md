# ZenTimings (WPF) — Code Review

**Scope:** ~8,600 lines of first‑party C#/XAML across `App`, `MainWindow`, `ViewModels`, `Windows/*`, `Controls/*`, `Helpers/*`, `Settings/*`, `Encryption/*`, `Updater.cs`. The bundled `Decompressor/7zip/*` (public‑domain LZMA SDK port) was skimmed but not reviewed line‑by‑line — it's vendored third‑party code, not yours.

**Overall impression:** this is a mature, working hardware‑utility app, not a toy project. The update pipeline in particular (RSA signature + SHA‑256 checksum + anti‑downgrade check) is noticeably more careful than most hobby updaters. The main issues are architectural (God-object `MainWindow`, blocking calls on the UI thread, inconsistent MVVM) rather than outright bugs, plus a handful of concrete latent bugs and one real dead-code security foot-gun.

---

## 1. High-priority findings

### 1.1 `XmlUtils.DeserializeFromXml` silently expects a *file path*, not XML text — and `AesEncryption` violates that contract
`Utils/XmlUtils.cs`:
```csharp
public static T DeserializeFromXml<T>(string xml)
{
    XmlSerializer serializer = new XmlSerializer(typeof(T));
    using (StreamReader reader = new StreamReader(xml))   // <-- opens "xml" as a FILE PATH
    {
        return (T)serializer.Deserialize(reader);
    }
}
```
`StreamReader(string)` opens a file at that path; it does not wrap a string as content. Every current caller (`AppSettings.Load`, `SensorSettings.Load`) happens to pass a file path, so today this works. But `Encryption/AesEncryption.cs` calls it the *other* way:
```csharp
public T DecryptXmlInMemory<T>(string inputFile)
{
    byte[] encryptedData = File.ReadAllBytes(inputFile);
    string decryptedXml = DecryptString(encryptedData);
    return XmlUtils.DeserializeFromXml<T>(decryptedXml);   // decryptedXml is XML *content*, not a path
}
```
This is currently dead code (nothing calls `EncryptXmlFile`/`DecryptXmlInMemory`/`DecryptStringInMemory` anywhere in the project), so it hasn't bitten anyone yet. But it's a live landmine: the moment someone wires encrypted settings in, this will throw `FileNotFoundException`, or — worse — if the decrypted XML content happens to coincide with a real path on disk, it will silently deserialize the *wrong file* instead of the decrypted payload. Recommend either deleting the unused `AesEncryption`/`EncryptionKeys` classes, or fixing `XmlUtils` to have two clearly-named methods (`DeserializeFromXmlFile(path)` vs `DeserializeFromXmlString(xml)`) so this class of mistake can't happen again.

### 1.2 `EncryptionKeys` generates and stores the AES key/IV next to the app, defeating the point of encryption
```csharp
private readonly string KeyFilePath = "key.bin";
private readonly string IVFilePath = "iv.bin";
...
if (File.Exists(KeyFilePath) && File.Exists(IVFilePath)) { ... }
else { aes.GenerateKey(); aes.GenerateIV(); File.WriteAllBytes(...); }
```
Also dead code today (see 1.1), but worth flagging before it's reactivated: relative paths (`"key.bin"`) resolve against the process's current working directory, which is not guaranteed to be the app folder — and even if it were, storing the key beside the ciphertext it protects means anyone with file access can decrypt everything. If this is meant to protect local settings from casual tampering, that's a reasonable goal, but it should use `ProtectedData`/DPAPI (per-user/per-machine protection) instead of a self-managed AES key sitting on disk. If it's meant to protect secrets from the machine's own user, on-disk AES with a co-located key can't achieve that regardless of implementation.

### 1.3 `MainWindow`'s constructor does blocking, unbounded I/O on the UI thread
`WaitForPowerTable()` (called directly from the `MainWindow` constructor) busy-waits with `Thread.Sleep(200)` for **up to 100 seconds**:
```csharp
int timeout = 100000;
...
do {
    status = cpu.RefreshPowerTable();
    if (status != SMU.Status.OK) Thread.Sleep(200);
} while (status != SMU.Status.OK && timer.Elapsed.TotalMilliseconds < timeout);
```
`WaitForInpoutDriverLoad()` does the same with a 5s cap. Both run synchronously during window construction, on the UI thread, with no way for the user to cancel other than force-closing the app. In the failure case (SMU not responding), the app is unresponsive for up to a minute and forty seconds before it even shows the main window. This should be moved off the UI thread (`Task.Run` + `await`, with a real cancel button on the splash screen), especially since `SplashWindow` already exists and could host a "Cancel" action.

### 1.4 `MainWindow.xaml.cs` is a 1,340-line God object
It owns: hardware polling, timer-driven auto-refresh, tray icon logic, window chrome, plugin loading, driver checks, update checks, screenshot saving, and menu handlers for six external URLs. `SensorsWindow.xaml.cs` (1,358 lines) and `DebugDialog.xaml.cs` (712 lines) have the same shape. This isn't "wrong" for a WPF utility of this size, but it makes the class hard to unit-test (nothing in the project currently is unit-tested — there's no test project at all) and increases the blast radius of any change. Given `MainViewModel` already exists and is reasonably clean, a good next step would be migrating more of `MainWindow`'s state (auto-refresh, sensor values, plugin results) into it and leaving the code-behind mostly for view wiring.

### 1.5 Inconsistent MVVM / change-notification pattern
`Common/ObservableObject.cs` provides a nice `SetProperty<T>` helper:
```csharp
protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
{
    if (Equals(storage, value)) return false;
    storage = value;
    PropertyChanged?.Invoke(...);
    return true;
}
```
But `MainViewModel` doesn't inherit from it — it hand-rolls `INotifyPropertyChanged` and repeats the same 5-line get/set boilerplate ~15 times, several of which pass a magic string instead of `nameof(...)` (`OnPropertyChanged("ApobData")`, `OnPropertyChanged("SwaAdcV")`, etc. — easy to typo and rename-unsafe). Also worth noting `MainViewModel.OnPropertyChanged` unconditionally does `Application.Current.Dispatcher.Invoke(...)` for every property set, even when already on the UI thread — that's a synchronous re-entrant dispatcher call on the hot auto-refresh path (every `AutoRefreshInterval` ms), which is more overhead than necessary. `Dispatcher.CheckAccess()` to skip the marshal when already on the UI thread, or `Invoke` only where genuinely needed, would help.

### 1.6 Target framework is .NET Framework 4.5
```xml
<TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
```
.NET Framework 4.5 has been out of support for years (Microsoft's support baseline is 4.6.2+). Nothing in the reviewed code obviously requires 4.5 specifically — retargeting to 4.7.2 or 4.8 (both still in support, both drop-in compatible for almost all WPF/Framework code) would cost little and remove a class of "why doesn't this run on a fresh Windows install" support questions. A longer-term move to `net8.0-windows` would also unlock nullable reference types, `System.Text.Json`, and better async tooling, though that's a bigger lift given the WinForms/WPF interop (`Forms.NotifyIcon`) and native driver interop already present.

---

## 2. Medium-priority findings

- **Swallowed exceptions.** ~9 empty `catch { }` blocks (`Updater.cs:246,497`, `AppSettings.cs:139`, `SpdInfoWindow.xaml.cs` x3, `LegacyDDR5*TimingsPanel.xaml.cs` x2, `OHWMPlugin.cs:81`) plus ~37 `catch (Exception ex)` blocks that only `Debug.WriteLine` the message. `Debug.WriteLine` output is invisible in a Release build outside a debugger, so in the field these failures are completely silent. At minimum, route these through a real logging sink (even a rolling text file next to `settings.xml`) so bug reports can include actual failure context instead of "it just doesn't show the AGESA version."
- **`SpdInfoWindow.xaml.cs` catch-and-continue loops** (`catch { idx++; continue; }`, `catch { continue; }` x2) — likely intentional "skip this SPD slot/byte on read failure," but worth a one-line comment confirming that, since a bare catch here could also be masking a real driver communication failure that should abort the whole scan rather than silently produce partial/misleading SPD data.
- **`DriverHelper.ExtractPawnIO()`** writes the installer to `Directory.GetCurrentDirectory()` rather than a per-run temp path (`Path.GetTempFileName()`/`Path.GetTempPath()`), and there's no cleanup if `Process.Start` throws between extraction and the `File.Delete` at the end of the calling method — a crash mid-install leaves `PawnIO_setup.exe` sitting in the app folder. Also note `InstallPawnIO()` (sync) doesn't set `UseShellExecute = true` explicitly on the two `ProcessStartInfo`s (relies on the .NET Framework default), while `InstallPawnIOAsync()` does — harmless today, but a maintenance trap if this is ever ported to .NET Core/5+, where the default flips to `false` and both installer launches would silently fail to elevate/associate.
- **`Updater.DownloadAndApplyUpdateCore`** pumps the WPF message loop manually while waiting on a `ManualResetEvent`:
  ```csharp
  while (!downloadComplete.WaitOne(50))
      Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
  ```
  This works, but it's a somewhat fragile way to keep the UI responsive during an async-style download; `await Task.Run(...)`/`async`-`await` throughout `Updater` would read more clearly and avoid hand-rolled pumping. Not urgent — the rest of `Updater.cs` is genuinely solid (TLS 1.2 pinned, RSA signature verification on both the update manifest and the release zip, SHA‑256 checksum, and an explicit anti-downgrade version check before applying).
- **Magic sensor-name string arrays** in `MainViewModel` (`ApuVddioSensorNames`, `VsocSensorNames`, `VmiscSensorNames`) hardcode vendor-specific sensor labels inline. If these need to grow per-motherboard-vendor over time (likely, given how many BIOS vendors are already special-cased in `VendorUtils.cs`), consider moving them to a small config/resource so they can be extended without a recompile, and so a future contributor adding "one more weird ASUS sensor name" doesn't need to touch `MainViewModel.cs` directly.
- **Hardcoded external URLs scattered through `MainWindow.xaml.cs`** (PayPal, Revolut, Discord, Google Sheets, Google Drive links via six near-identical `MenuItem_Click_N` handlers). Functionally fine, but consider one `OpenUrl(string url)` helper plus a `Dictionary<string,string>` or resource lookup — mostly for readability, since `MenuItem_Click`, `MenuItem_Click_1`, `MenuItem_Click_2`... (WPF's auto-generated names) make it hard to tell at a glance which menu item does what without cross-referencing the XAML.

## 3. Low-priority / style

- No test project anywhere in the solution. Given how much of this app is "parse hardware state, compute derived timing values, decide what to show," a good amount of that logic (e.g., `VendorUtils`, the SPD decoding paths, `AgesaHelper`) is pure-ish and unit-testable even without mocking the driver layer.
- Several `private readonly string[] X = { ... }` and enum groupings would read slightly better as `static readonly` at the type level with `IReadOnlyList<string>`, but this is cosmetic.
- `NumericTextBox.cs` uses tabs while the rest of the codebase uses spaces — pick one (an `.editorconfig` would enforce this automatically and is an easy one-time add).
- `AppSettings.Save()` returns early and silently no-ops if `DriverHelper.IsPawnIoInstalled` is false:
  ```csharp
  if (!DriverHelper.IsPawnIoInstalled) return;
  ```
  That's a surprising side effect to bury inside a generic-sounding `Save()` — anyone changing a setting while the driver isn't installed will have their change silently discarded on next load, with no user-facing feedback. Worth at least a comment explaining why settings persistence is gated on driver presence, if not a rethink (settings and driver state seem like they should be independent).
- `AboutDialog`, `Changelog`, and the six `MenuItem_Click_N` handlers in `MainWindow` all call `Process.Start("https://...")` directly with no try/catch — if the OS has no default browser association (rare, but possible on stripped-down/enterprise images), this throws `Win32Exception` unhandled.

## 4. What's already good

- **Update pipeline security** (`Updater.cs`, `Encryption/UpdaterEncryption.cs`): RSA-2048/SHA-256 signature check on the update manifest *before* trusting any version info in it, a second signature check on the downloaded release zip, a SHA-256 checksum as a fast fail-early check, `TLS 1.2` explicitly pinned, and an explicit anti-downgrade guard comparing the extracted binary's `FileVersion` against the installed version before applying. This is meaningfully better than the "just download and run" pattern common in small updater implementations.
- **Single-instance handling** (`App.xaml.cs`) via a named mutex plus a `WM_SHOWME` broadcast message to reactivate the existing window — the right pattern, correctly implemented (mutex kept alive via `GC.KeepAlive`, race between "already running" and driver cleanup handled via a second dedicated mutex in `CleanupDriverIfLastInstance`).
- **Resource cleanup on shutdown** is handled properly where it matters: `_notifyIcon?.Dispose()`, `AsusWmi?.Dispose()`, `cpu?.Dispose()`, `BMC?.Dispose()` are all called from a central `Cleanup()`/closing path, not left to the GC.
- **Sensor lookup caching** in `MainViewModel.DetectSensors()`/`RefreshSensors()` — sensors are located by name once and cached, rather than re-running a `SelectMany`+`FirstOrDefault` string search on every refresh tick.
- **Theming architecture** (`Themes/*.xaml` + `ThemedAdonisWindow.cs`) — ten resource-dictionary-based themes with a shared `Base.xaml`/`SharedAssets.xaml`, applied via `ResourceLocator.SetColorScheme`, is a clean way to support this many themes without per-theme code branches.

---

## Suggested priority order
1. Delete or fix the dead `AesEncryption`/`EncryptionKeys` path (1.1/1.2) before anyone builds on top of it.
2. Move `WaitForPowerTable`/`WaitForInpoutDriverLoad` off the UI thread with real cancellation (1.3).
3. Add an `.editorconfig` + a lightweight file-based logger to replace the silent `Debug.WriteLine`/empty-catch pattern (2.1) — cheap win for future bug reports.
4. Longer-term: retarget to net472/net48 (1.6), and gradually push `MainWindow`/`SensorsWindow` state into their view models (1.4/1.5).
