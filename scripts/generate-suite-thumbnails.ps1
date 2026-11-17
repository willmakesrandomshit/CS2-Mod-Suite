# Portfolio thumbnails: restrained type-led marks plus verified CSII captures.
# Each cover uses product-specific composition. No invented dashboard copy, metrics or versions.
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Brush([string]$hex) { [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($hex)) }
function Pen([string]$hex, [float]$width) {
    $p=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($hex),$width)
    $p.StartCap=[Drawing.Drawing2D.LineCap]::Round; $p.EndCap=[Drawing.Drawing2D.LineCap]::Round
    return $p
}
function Canvas([string]$hex) {
    $b=[Drawing.Bitmap]::new(512,512)
    $g=[Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([Drawing.ColorTranslator]::FromHtml($hex))
    return @{Bitmap=$b;Graphics=$g}
}
function Rect($g,[string]$hex,[float]$x,[float]$y,[float]$w,[float]$h) {
    $b=Brush $hex; $g.FillRectangle($b,$x,$y,$w,$h); $b.Dispose()
}
function Line($g,[string]$hex,[float]$w,[float]$x1,[float]$y1,[float]$x2,[float]$y2) {
    $p=Pen $hex $w; $g.DrawLine($p,$x1,$y1,$x2,$y2); $p.Dispose()
}
function Circle($g,[string]$hex,[float]$x,[float]$y,[float]$w,[float]$h) {
    $b=Brush $hex; $g.FillEllipse($b,$x,$y,$w,$h); $b.Dispose()
}
function EllipseOutline($g,[string]$hex,[float]$w,[float]$x,[float]$y,[float]$ew,[float]$eh) {
    $p=Pen $hex $w; $g.DrawEllipse($p,$x,$y,$ew,$eh); $p.Dispose()
}
function Poly($g,[string]$hex,[int[]]$xy) {
    $pts=[Drawing.Point[]]@()
    for($i=0;$i -lt $xy.Length;$i+=2){$pts+=,[Drawing.Point]::new($xy[$i],$xy[$i+1])}
    $b=Brush $hex; $g.FillPolygon($b,$pts); $b.Dispose()
}
function Label($g,[string]$text,[float]$x,[float]$y,[float]$w,[float]$h,[float]$size,[string]$hex,[string]$family='Bahnschrift',[string]$weight='Bold',[string]$align='Near') {
    $style=[Enum]::Parse([Drawing.FontStyle],$weight)
    $f=[Drawing.Font]::new($family,$size,$style,[Drawing.GraphicsUnit]::Pixel)
    $b=Brush $hex; $fmt=[Drawing.StringFormat]::new()
    $fmt.Alignment=[Enum]::Parse([Drawing.StringAlignment],$align); $fmt.LineAlignment=[Drawing.StringAlignment]::Center
    $g.DrawString($text,$f,$b,[Drawing.RectangleF]::new($x,$y,$w,$h),$fmt)
    $fmt.Dispose();$b.Dispose();$f.Dispose()
}
function Save($surface,[string]$mod) {
    $path=Join-Path $repoRoot "$mod\Properties\Thumbnail.png"
    $surface.Bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    $surface.Graphics.Dispose();$surface.Bitmap.Dispose()
}
function PhotoRect($mod,[string]$file,[int]$left,[int]$top,[int]$side,[string]$name,[string]$accent) {
    $img=[Drawing.Image]::FromFile((Join-Path $repoRoot "artwork\source-captures\$file"))
    $src=[Drawing.Rectangle]::new($left,$top,$side,$side)
    $s=Canvas '#17212a';$g=$s.Graphics
    $g.DrawImage($img,[Drawing.Rectangle]::new(0,0,512,512),$src,[Drawing.GraphicsUnit]::Pixel)
    $img.Dispose()
    $veil=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(203,13,20,26))
    $g.FillRectangle($veil,0,426,512,86);$veil.Dispose()
    Rect $g $accent 24 442 42 4
    Label $g $name 24 452 464 42 27 '#f5f2e9' 'Bahnschrift' 'Bold'
    Save $s $mod
}

# Authentic game views: crop the actual feature, then add only the product name.
PhotoRect 'ContourPlus' 'ContourPlus.jpg' 0 0 509 'CONTOUR+' '#eea64b'
PhotoRect 'FastTrack' 'FastTrack.jpg' 0 0 524 'FASTTRACK' '#39c3d2'
PhotoRect 'MagicMarking' 'MagicMarking.jpg' 0 0 701 'MAGIC MARKING' '#f2c849'
PhotoRect 'TrafficStressLab' 'TrafficStressLab.png' 0 0 615 'TRAFFIC STRESS LAB' '#4fc1cf'

# Access Studio — site plan geometry and a clear path to the frontage.
$s=Canvas '#172536';$g=$s.Graphics
Rect $g '#243b50' 0 0 512 512
for($i=28;$i -le 484;$i+=32){Line $g '#2d465a' 1 $i 28 $i 484;Line $g '#2d465a' 1 28 $i 484 $i}
Poly $g '#ded8c9' @(214,88,421,119,393,336,186,305)
Poly $g '#86959a' @(236,113,394,137,372,302,214,278)
Poly $g '#536c76' @(259,142,358,157,343,258,244,243)
Line $g '#f0ece2' 4 52 389 459 389
Line $g '#55c6cc' 9 92 388 92 322
Line $g '#55c6cc' 9 92 322 215 322
Line $g '#55c6cc' 9 215 322 215 272
Circle $g '#e9a46e' 195 253 40 40
Label $g 'ACCESS' 30 32 430 59 43 '#f3f1e9'
Label $g 'STUDIO' 32 90 275 40 22 '#93aab4' 'Bahnschrift' 'Regular'
Save $s 'AccessStudio'

# City Pulse — a single signal across a quiet skyline, without simulated readings.
$s=Canvas '#eeeae0';$g=$s.Graphics
$build=Brush '#9eaca5'
$cityShapes=@(
    @(21,452,21,404,40,404,40,383,57,383,57,394,77,394,77,452),
    @(92,452,92,374,115,374,115,349,134,349,134,365,151,365,151,452),
    @(171,452,171,413,193,413,193,387,209,387,209,452),
    @(224,452,224,364,246,364,246,327,264,327,264,351,286,351,286,452),
    @(307,452,307,392,329,392,329,376,347,376,347,452),
    @(373,452,373,371,396,371,396,341,418,341,418,361,442,361,442,452),
    @(463,452,463,399,483,399,483,384,500,384,500,452)
)
foreach($shape in $cityShapes){Poly $g '#9eaca5' $shape};$build.Dispose()
Line $g '#d9d5ca' 3 0 452 512 452
Label $g 'CITY' 29 37 340 66 54 '#293c3b' 'Georgia' 'Bold'
Label $g 'PULSE' 30 97 380 70 55 '#293c3b' 'Georgia' 'Bold'
Line $g '#bf5d4a' 8 24 225 115 225;Line $g '#bf5d4a' 8 115 225 142 188
Line $g '#bf5d4a' 8 142 188 170 267;Line $g '#bf5d4a' 8 170 267 198 225
Line $g '#bf5d4a' 8 198 225 488 225
Save $s 'CityPulse'

# CrashLens — diagnostic focus, with no invented log rows or error counts.
$s=Canvas '#17191d';$g=$s.Graphics
Rect $g '#c75a48' 0 0 12 512
Label $g 'CRASH' 34 54 435 82 61 '#eeeae2' 'Bahnschrift' 'Bold'
Label $g 'LENS' 34 128 320 82 61 '#eeeae2' 'Bahnschrift' 'Bold'
EllipseOutline $g '#c75a48' 12 300 251 124 124
Line $g '#c75a48' 13 397 350 469 422
Label $g '!' 321 264 82 92 67 '#e8e4dc' 'Bahnschrift' 'Bold' 'Center'
Save $s 'CrashLens'

# Discord RPC — presence sync mark, not a copied Discord logo.
$s=Canvas '#4d568c';$g=$s.Graphics
Label $g 'DR' 24 90 464 237 190 '#f2f0e9' 'Bahnschrift' 'Bold' 'Center'
Line $g '#9ca5dc' 5 102 344 410 344
Circle $g '#ef9c7c' 403 326 36 36
Label $g 'DISCORD RPC' 28 381 456 57 30 '#f2f0e9' 'Bahnschrift' 'Bold' 'Center'
Save $s 'DiscordRPC'

# Event Engine — typographic marquee and restrained stage lighting.
$s=Canvas '#39222e';$g=$s.Graphics
Rect $g '#b14c4b' 0 0 512 18;Rect $g '#b14c4b' 0 494 512 18
for($x=50;$x -le 460;$x+=82){Line $g '#cb8668' 2 $x 40 ($x-42) 210}
Circle $g '#dfa968' 334 48 128 128
Poly $g '#e9bd7c' @(398,145,279,358,472,358)
Rect $g '#201a20' 0 323 512 171
Label $g 'EVENT' 30 341 452 66 51 '#f0e8dd' 'Georgia' 'Bold' 'Center'
Label $g 'ENGINE' 30 401 452 62 44 '#e0b47b' 'Georgia' 'Bold' 'Center'
Save $s 'EventEngine'

# Junction Studio — the turning path is the subject; no fictitious controls.
$s=Canvas '#e7e1d3';$g=$s.Graphics
Rect $g '#e7e1d3' 0 0 512 512
Rect $g '#29363b' 0 173 512 166;Rect $g '#29363b' 173 0 166 512
Line $g '#e7e1d3' 4 256 0 256 169;Line $g '#e7e1d3' 4 256 342 256 512
Line $g '#e7e1d3' 4 0 256 169 256;Line $g '#e7e1d3' 4 342 256 512 256
for($y=35;$y -lt 154;$y+=34){Line $g '#eee7d7' 3 243 $y 269 $y}
for($y=365;$y -lt 490;$y+=34){Line $g '#eee7d7' 3 243 $y 269 $y}
Line $g '#e7b150' 10 294 474 294 275;Line $g '#e7b150' 10 294 275 220 275;Line $g '#e7b150' 10 220 275 220 208
Poly $g '#e7b150' @(206,218,220,190,234,218)
Label $g 'JUNCTION' 25 31 462 48 33 '#29363b' 'Bahnschrift' 'Bold' 'Center'
Label $g 'STUDIO' 26 434 460 46 30 '#29363b' 'Bahnschrift' 'Bold' 'Center'
Save $s 'JunctionStudio'

# Network Studio — three deliberate routes join one street corridor.
$s=Canvas '#273b40';$g=$s.Graphics
Line $g '#415a5b' 50 0 430 170 302;Line $g '#415a5b' 50 170 302 302 335;Line $g '#415a5b' 50 302 335 512 178
Line $g '#d9d7c9' 2 0 405 170 277;Line $g '#d9d7c9' 2 0 455 170 327
Line $g '#d9d7c9' 2 170 277 302 310;Line $g '#d9d7c9' 2 170 327 302 360
Line $g '#d9d7c9' 2 302 310 512 153;Line $g '#d9d7c9' 2 302 360 512 203
foreach($p in @(@(108,353),@(252,322),@(390,245))){Circle $g '#e47a57' ($p[0]-9) ($p[1]-9) 18 18}
Label $g 'NETWORK' 26 32 460 56 39 '#f1eee4'
Label $g 'STUDIO' 27 83 440 42 24 '#a9baba'
Save $s 'NetworkStudio'

# Parkify — a quieter park mark with an organic path and layered planting.
$s=Canvas '#dbe3cc';$g=$s.Graphics
Poly $g '#6d8d6e' @(0,260,104,207,207,228,274,173,385,194,512,145,512,512,0,512)
Poly $g '#9fb58c' @(0,342,98,295,180,318,251,272,343,305,428,270,512,288,512,512,0,512)
$p=Pen '#efe3c4' 30
$g.DrawCurve($p,[Drawing.Point[]]@([Drawing.Point]::new(-8,462),[Drawing.Point]::new(83,382),[Drawing.Point]::new(176,396),[Drawing.Point]::new(244,337),[Drawing.Point]::new(315,354),[Drawing.Point]::new(391,294),[Drawing.Point]::new(522,312)),0.15);$p.Dispose()
Label $g 'parkify' 29 28 360 60 47 '#234533' 'Georgia' 'Bold'
Save $s 'Parkify'

# Road Rules — roadway paint as the graphic, with a clean readable title.
$s=Canvas '#262d31';$g=$s.Graphics
Rect $g '#41494a' 0 158 512 354
Line $g '#b9c0b8' 3 0 170 512 170;Line $g '#b9c0b8' 3 0 500 512 500
Line $g '#e2c554' 6 0 322 512 322
for($x=28;$x -lt 490;$x+=76){Line $g '#f0eee4' 6 $x 350 ($x+38) 350}
Label $g 'ROAD' 28 34 456 57 47 '#f0eee4'
Label $g 'RULES' 28 91 456 58 47 '#e2c554'
Save $s 'RoadRules'

# SaveGuard — one clear shield silhouette, no fake file stack or health claims.
$s=Canvas '#20342f';$g=$s.Graphics
Poly $g '#e5ece3' @(256,61,411,115,394,292,341,376,256,432,171,376,118,292,101,115)
Poly $g '#437968' @(256,88,382,131,368,281,324,348,256,392,188,348,144,281,130,131)
Label $g 'SG' 163 140 186 88 64 '#f0f1e9' 'Bahnschrift' 'Bold' 'Center'
Line $g '#f0f1e9' 17 184 251 235 302;Line $g '#f0f1e9' 17 235 302 335 190
Label $g 'SAVEGUARD' 26 442 460 42 28 '#f0f1e9' 'Bahnschrift' 'Bold' 'Center'
Save $s 'SaveGuard'

Write-Output 'Generated 14 reviewed 512x512 thumbnails; screenshot-based covers use bundled CSII captures.'

