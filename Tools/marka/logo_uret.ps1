# Tabanları Yağla marka görsellerini üretir (Windows PowerShell 5.1, System.Drawing / GDI+).
# Yazı tipi: Luckiest Guy (Apache 2.0, projede: Assets/UnityTechnologies/Endless Runner/Art/Fonts).
# Simge kaynağı: kaynak/ikon_samet_kaynak.png (Godot sürümünün kendi 3B sahnesinden alınmış, proje sahibi).
# Mevcut dosyaların üzerine yazmaz: hedef dosya varsa -Yenile verilmedikçe atlar.
# Türkçe harfler betik kodlamasından bağımsız olsun diye karakter kodlarıyla yazılır.
param([switch]$Yenile)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$proje = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$fontYolu = Join-Path $proje 'Assets\UnityTechnologies\Endless Runner\Art\Fonts\LuckiestGuy.ttf'
$simgeKaynak = Join-Path $PSScriptRoot 'kaynak\ikon_samet_kaynak.png'
$markaKlasor = Join-Path $proje 'Assets\_TabanlariYagla\Art\Brand'
$sablonVeri = Join-Path $proje 'Assets\WebGLTemplates\TabanlariYagla\TemplateData'
$simgeKlasor = Join-Path $sablonVeri 'icons'

# "TABANLARI" (Türkçe büyük harf: noktasız I) ve "YAĞLA" (Ğ = U+011E)
$satir1 = 'TABANLARI'
$satir2 = 'YA' + [char]0x011E + 'LA'

$lacivert = [System.Drawing.Color]::FromArgb(255, 27, 37, 80)     # dış çizgi
$golge    = [System.Drawing.Color]::FromArgb(140, 10, 14, 40)     # gölge
$arkaUst  = [System.Drawing.Color]::FromArgb(255, 27, 37, 80)     # açılış/yükleme arka planı (tema rengi #1B2550)
$arkaAlt  = [System.Drawing.Color]::FromArgb(255, 46, 63, 143)

function Kaydet([System.Drawing.Bitmap]$bmp, [string]$yol) {
    if ((Test-Path $yol) -and -not $Yenile) { Write-Host "ATLANDI (var): $yol"; return }
    $bmp.Save($yol, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host ("YAZILDI {0}x{1}: {2}" -f $bmp.Width, $bmp.Height, $yol)
}

function YaziYolu([System.Drawing.FontFamily]$aile, [string]$metin, [System.Drawing.RectangleF]$kutu) {
    $yol = New-Object System.Drawing.Drawing2D.GraphicsPath
    $yol.AddString($metin, $aile, [int][System.Drawing.FontStyle]::Regular, 400, ([System.Drawing.PointF]::new(0, 0)), [System.Drawing.StringFormat]::GenericTypographic)
    $b = $yol.GetBounds()
    $olcek = [Math]::Min($kutu.Width / $b.Width, $kutu.Height / $b.Height)
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate($kutu.X + ($kutu.Width - $b.Width * $olcek) / 2, $kutu.Y + ($kutu.Height - $b.Height * $olcek) / 2)
    $m.Scale($olcek, $olcek)
    $m.Translate(-$b.X, -$b.Y)
    $yol.Transform($m)
    return $yol
}

function YaziKatmani([System.Drawing.Graphics]$g, [System.Drawing.Drawing2D.GraphicsPath]$yol, [System.Drawing.Color]$ust, [System.Drawing.Color]$alt, [float]$cizgi) {
    # gölge
    $gYol = $yol.Clone(); $gm = New-Object System.Drawing.Drawing2D.Matrix; $gm.Translate(0, $cizgi * 0.45); $gYol.Transform($gm)
    $gKalem = [System.Drawing.Pen]::new($golge, $cizgi * 2); $gKalem.LineJoin = 'Round'
    $g.DrawPath($gKalem, $gYol); $g.FillPath(([System.Drawing.SolidBrush]::new($golge)), $gYol)
    # kalın lacivert dış çizgi
    $kalem = [System.Drawing.Pen]::new($lacivert, $cizgi * 2); $kalem.LineJoin = 'Round'
    $g.DrawPath($kalem, $yol)
    # dikey renk geçişli dolgu
    $b = $yol.GetBounds()
    $firca = [System.Drawing.Drawing2D.LinearGradientBrush]::new(([System.Drawing.RectangleF]::new($b.X, $b.Y - 1, $b.Width, $b.Height + 2)), $ust, $alt, [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($firca, $yol)
}

function LogoCiz([int]$en, [int]$boy) {
    $pfc = New-Object System.Drawing.Text.PrivateFontCollection
    $pfc.AddFontFile($fontYolu)
    $aile = $pfc.Families[0]
    $bmp = [System.Drawing.Bitmap]::new($en, $boy, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $kenar = $en * 0.06; $cizgi = $en * 0.017
    $ust = [System.Drawing.RectangleF]::new($kenar, $boy * 0.07, $en - 2 * $kenar, $boy * 0.40)
    $alt = [System.Drawing.RectangleF]::new($kenar + $en * 0.08, $boy * 0.52, $en - 2 * $kenar - $en * 0.16, $boy * 0.40)
    YaziKatmani $g (YaziYolu $aile $satir1 $ust) ([System.Drawing.Color]::FromArgb(255, 255, 230, 92)) ([System.Drawing.Color]::FromArgb(255, 255, 179, 0)) $cizgi
    YaziKatmani $g (YaziYolu $aile $satir2 $alt) ([System.Drawing.Color]::FromArgb(255, 255, 176, 64)) ([System.Drawing.Color]::FromArgb(255, 255, 106, 0)) $cizgi
    $g.Dispose(); $pfc.Dispose()
    return $bmp
}

function Olcekle([System.Drawing.Image]$kaynak, [System.Drawing.Rectangle]$alan, [int]$en, [int]$boy) {
    $bmp = [System.Drawing.Bitmap]::new($en, $boy, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.SmoothingMode = 'AntiAlias'
    $g.DrawImage($kaynak, ([System.Drawing.Rectangle]::new(0, 0, $en, $boy)), $alan, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose(); return $bmp
}

# --- Logo ---
$logo = LogoCiz 2048 1152
Kaydet $logo (Join-Path $markaKlasor 'logo_tabanlari_yagla.png')
Kaydet (Olcekle $logo ([System.Drawing.Rectangle]::new(0, 0, 2048, 1152)) 1024 576) (Join-Path $markaKlasor 'logo_tabanlari_yagla_1024.png')
Kaydet (Olcekle $logo ([System.Drawing.Rectangle]::new(0, 0, 2048, 1152)) 640 360) (Join-Path $sablonVeri 'brand-logo.png')

# --- Simgeler: yazısız Samet portresi + turuncu ışınlar (kaynağın "YAĞLA!" şeridinin üstünden kare kırpım) ---
$kaynak = [System.Drawing.Image]::FromFile($simgeKaynak)
$kirp = [System.Drawing.Rectangle]::new(91, 30, 330, 330)
foreach ($s in @(@(180, 'apple-touch-icon-180.png'), @(192, 'icon-192.png'), @(512, 'icon-512.png'), @(512, 'icon-maskable-512.png'), @(32, 'favicon-32.png'))) {
    Kaydet (Olcekle $kaynak $kirp $s[0] $s[0]) (Join-Path $simgeKlasor $s[1])
}
Kaydet (Olcekle $kaynak $kirp 1024 1024) (Join-Path $markaKlasor 'uygulama_simgesi_1024.png')
$kaynak.Dispose()

# --- iPhone 13 açılış görseli (1170x2532): tema rengi üzerinde logo ---
$acilis = [System.Drawing.Bitmap]::new(1170, 2532, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($acilis)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
$g.FillRectangle(([System.Drawing.Drawing2D.LinearGradientBrush]::new(([System.Drawing.Rectangle]::new(0, 0, 1170, 2532)), $arkaUst, $arkaAlt, [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)), 0, 0, 1170, 2532)
$g.DrawImage($logo, ([System.Drawing.Rectangle]::new(85, 940, 1000, 562)))
$g.Dispose()
Kaydet $acilis (Join-Path $sablonVeri 'acilis-iphone13-1170x2532.png')
$logo.Dispose(); $acilis.Dispose()
