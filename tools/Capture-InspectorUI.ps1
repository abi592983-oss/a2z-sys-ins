param(
    [string]$AssemblyPath = "src/A2ZSysIns/bin/x64/Release/net48/A2Z System Inspector.exe",
    [string]$OutputDirectory = "ui-artifacts"
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase
$resolved = (Resolve-Path $AssemblyPath).Path
$assembly = [System.Reflection.Assembly]::LoadFrom($resolved)

# Build the WPF object tree without Application.Run or any diagnostic collection.
$appType = $assembly.GetType("A2ZSysIns.App", $true)
$app = [Activator]::CreateInstance($appType)
$appType.GetMethod("InitializeComponent").Invoke($app, @()) | Out-Null
$windowType = $assembly.GetType("A2ZSysIns.MainWindow", $true)
$window = [Activator]::CreateInstance($windowType)
$root = [System.Windows.FrameworkElement]$window.Content
$tabs = [System.Windows.Controls.TabControl]$window.FindName("Tabs")
if ($null -eq $tabs) { throw "Inspector tabs were not found." }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$states = @("Job-Details", "Health-Check", "Results-Export")
foreach ($size in @(@(1366,768), @(1024,768))) {
    $w = [int]$size[0]; $h = [int]$size[1]
    $window.MinWidth = 0; $window.MinHeight = 0
    $window.MaxWidth = $w; $window.MaxHeight = $h
    $window.Width = $w; $window.Height = $h
    for ($i = 0; $i -lt $states.Length; $i++) {
        $tabs.SelectedIndex = $i
        $root.Measure([System.Windows.Size]::new($w, $h))
        $root.Arrange([System.Windows.Rect]::new(0, 0, $w, $h))
        $root.UpdateLayout()
        $bmp = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
            $w, $h, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $bmp.Render($root)
        $png = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
        $png.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
        $out = Join-Path $OutputDirectory ("{0}-{1}x{2}.png" -f $states[$i],$w,$h)
        $stream = [System.IO.File]::Create((Join-Path (Get-Location).Path $out))
        try { $png.Save($stream) } finally { $stream.Close() }
        if ((Get-Item $out).Length -lt 1000) { throw "UI capture suspiciously empty: $out" }
        Write-Host "Captured $out"
    }
}
$window.Close()
"CI visual snapshots only; test placeholders are not real diagnostic results." |
    Set-Content -Path (Join-Path $OutputDirectory "README.txt") -Encoding UTF8
