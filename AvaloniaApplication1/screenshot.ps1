Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Find the Avalonia Application window process
$proc = Get-Process AvaloniaApplication1 -ErrorAction SilentlyContinue
if (-not $proc) {
    Write-Host "Process not found"
    exit
}

# Capture screen
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$graphics.Dispose()

# Save
$bitmap.Save("c:\Users\adm\projects\AvaloniaApplication1\AvaloniaApplication1\screenshot.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Write-Host "Screenshot saved"
