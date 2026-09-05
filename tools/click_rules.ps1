# Atomically: read window rect, click the rule-group button, capture result.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32Rule {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
[W32Rule]::SetProcessDPIAware() | Out-Null

$p = Get-Process BSGroupGenerator -ErrorAction Stop
$h = $p.MainWindowHandle
[W32Rule]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 500

$r = New-Object W32Rule+RECT
[W32Rule]::GetWindowRect($h, [ref]$r) | Out-Null
$bx = [int]($r.Left + 1122)
$by = [int]($r.Top + 408)
Write-Host "window $($r.Left),$($r.Top) -> click $bx,$by"

[W32Rule]::SetCursorPos($bx, $by) | Out-Null
Start-Sleep -Milliseconds 200
[W32Rule]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
[W32Rule]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 1800

$r2 = New-Object W32Rule+RECT
[W32Rule]::GetWindowRect($h, [ref]$r2) | Out-Null
$w = [int]($r2.Right - $r2.Left)
$ht = [int]($r2.Bottom - $r2.Top)
$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r2.Left, $r2.Top, 0, 0, $bmp.Size)
$Out = "D:\Documents\Zcode\BS Group Generator\tools\ui_check.png"
$bmp.Save($Out)
Write-Host "saved"
