# Hazırlık ekranındaki "güçlendirme seçilmedi" görseli: eski UISpritesheet içindeki "NO ITEM" karesinin Türkçe karşılığı.
# Aynı stil: saydam zemin, beyaz Luckiest Guy harfleri, sağ-alta gri gölge. Eski sprite sayfası değiştirilmez.
# Kullanım: powershell -File Tools\dil\guc_yok_uret.ps1 [-Yenile]   (-Yenile olmadan var olan dosyanın üstüne yazmaz)
param([switch]$Yenile)

Add-Type -AssemblyName System.Drawing
$proje = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$fontYolu = Join-Path $proje 'Assets\UnityTechnologies\Endless Runner\Art\Fonts\LuckiestGuy.ttf'
$cikti = Join-Path $proje 'Assets\_TabanlariYagla\Art\UI\guclendirme_yok.png'

if ((Test-Path $cikti) -and -not $Yenile) { Write-Host "Zaten var, atlandı: $cikti (yeniden üretmek için -Yenile)"; exit 0 }
New-Item -ItemType Directory -Force (Split-Path $cikti) | Out-Null

$boyut = 256
$satirlar = @(('G' + [char]0x00DC + [char]0x00C7), 'YOK')   # "GÜÇ" / "YOK" (kaynak dosya kodlamasından bağımsız)

$pfc = [System.Drawing.Text.PrivateFontCollection]::new()
$pfc.AddFontFile($fontYolu)
$aile = $pfc.Families[0]

$bmp = [System.Drawing.Bitmap]::new($boyut, $boyut, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# Her satır kareye sığacak en büyük boyutta (iki satır, kenarlarda pay bırakarak).
$em = 100.0
$fmt = [System.Drawing.StringFormat]::GenericTypographic
$olcuFont = [System.Drawing.Font]::new($aile, $em, [System.Drawing.GraphicsUnit]::Pixel)
$enGenis = ($satirlar | ForEach-Object { $g.MeasureString($_, $olcuFont, 10000, $fmt).Width } | Measure-Object -Maximum).Maximum
$olcuFont.Dispose()
$em = [Math]::Min($em * ($boyut * 0.86) / $enGenis, $boyut * 0.40)

$satirAraligi = $em * 1.12
$ustBosluk = ($boyut - $satirAraligi * $satirlar.Count) / 2 + $em * 0.10
$golge = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 150, 150, 160))
$beyaz = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
$kayma = [Math]::Round($em * 0.07)

for ($i = 0; $i -lt $satirlar.Count; $i++) {
    $yol = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $yol.AddString($satirlar[$i], $aile, 0, $em, [System.Drawing.PointF]::new(0, 0), $fmt)
    $sinir = $yol.GetBounds()
    $x = ($boyut - $sinir.Width) / 2 - $sinir.X
    $y = $ustBosluk + $i * $satirAraligi
    $m = [System.Drawing.Drawing2D.Matrix]::new()
    $m.Translate($x + $kayma, $y + $kayma)
    $golgeYol = $yol.Clone(); $golgeYol.Transform($m); $g.FillPath($golge, $golgeYol)
    $m.Reset(); $m.Translate($x, $y)
    $yol.Transform($m); $g.FillPath($beyaz, $yol)
    $golgeYol.Dispose(); $yol.Dispose(); $m.Dispose()
}

$g.Dispose()
$bmp.Save($cikti, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); $golge.Dispose(); $beyaz.Dispose(); $pfc.Dispose()
Write-Host "Üretildi: $cikti"
