# Capture the BSGroupGenerator main window (DPI aware). ASCII-only for PowerShell 5.1.
$Out = "D:\Documents\Zcode\BS Group Generator\tools\ui_check.png"

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32Shot {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
[W32Shot]::SetProcessDPIAware() | Out-Null

$p = Get-Process BSGroupGenerator -ErrorAction Stop
$h = $p.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { throw "no main window" }
[W32Shot]::ShowWindow($h, 9) | Out-Null
[W32Shot]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 800

$r = New-Object W32Shot+RECT
[W32Shot]::GetWindowRect($h, [ref]$r) | Out-Null
$w = [int]($r.Right - $r.Left)
$ht = [int]($r.Bottom - $r.Top)
Write-Host "rect: $($r.Left),$($r.Top) ${w}x$ht"

$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$bmp.Save($Out)
Write-Host "saved"
