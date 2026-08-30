# Focus the app, open Help > About via Alt+H,A, capture to tools/ui_about.png
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32Ab {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
[W32Ab]::SetProcessDPIAware() | Out-Null

$p = Get-Process BSGroupGenerator -ErrorAction Stop
$h = $p.MainWindowHandle
[W32Ab]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 600
[System.Windows.Forms.SendKeys]::SendWait("%h")
Start-Sleep -Milliseconds 1200
[System.Windows.Forms.SendKeys]::SendWait("a")
Start-Sleep -Milliseconds 2500

$r = New-Object W32Ab+RECT
[W32Ab]::GetWindowRect($h, [ref]$r) | Out-Null
$w = [int]($r.Right - $r.Left)
$ht = [int]($r.Bottom - $r.Top)

$bmp = New-Object System.Drawing.Bitmap($w, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$Out = "D:\Documents\Zcode\BS Group Generator\tools\ui_about.png"
$bmp.Save($Out)
Write-Host "saved"
