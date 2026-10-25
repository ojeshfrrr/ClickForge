param([string]$OutputPath = 'src/ClickForge/Assets/ClickForge.ico')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$xml = [xml](Get-Content -LiteralPath src/ClickForge/Themes/Controls.xaml -Raw -Encoding UTF8)
$namespaces = New-Object Xml.XmlNamespaceManager($xml.NameTable)
$namespaces.AddNamespace('w','http://schemas.microsoft.com/winfx/2006/xaml/presentation')
$namespaces.AddNamespace('x','http://schemas.microsoft.com/winfx/2006/xaml')
$node = $xml.SelectSingleNode('//w:DrawingImage[@x:Key="ForgeIcon"]',$namespaces)
if (-not $node) { throw 'The actual ForgeIcon resource is missing' }
$node.RemoveAttribute('Key','http://schemas.microsoft.com/winfx/2006/xaml')
$image = [Windows.Markup.XamlReader]::Parse($node.OuterXml)
$frames = @()
foreach ($size in @(16,32,48,64,128,256)) {
    $visual = New-Object Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $width = $size * $image.Width / $image.Height
    $context.DrawImage($image,[Windows.Rect]::new(($size-$width)/2,0,$width,$size)); $context.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32); $bitmap.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder; $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object IO.MemoryStream; $encoder.Save($stream)
    $frames += [pscustomobject]@{Size=$size;Bytes=$stream.ToArray()}; $stream.Dispose()
}
$full = [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputPath)); New-Item -ItemType Directory -Force (Split-Path $full -Parent) | Out-Null
$stream = [IO.File]::Create($full); $writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16*$frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset); $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $stream.Dispose() }
Write-Output "Application icon: $full"
