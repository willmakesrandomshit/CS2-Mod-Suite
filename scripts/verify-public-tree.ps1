$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$forbiddenDirectories = '[\\/](bin|obj|node_modules|dist|Temp|Library|logs?|backups?|Screenshots)[\\/]'
$forbiddenExtensions = @('.dll','.exe','.pdb','.so','.bundle','.zip','.7z','.dmp','.cok','.save')

$badFiles = @(Get-ChildItem -LiteralPath $repo -Recurse -Force -File | Where-Object {
    $_.FullName -match $forbiddenDirectories -or $forbiddenExtensions -contains $_.Extension.ToLowerInvariant()
})
if ($badFiles.Count) { throw "Generated/private files found:`n$($badFiles.FullName -join "`n")" }

$pathMatches = @(rg -n --hidden --glob '!**/.git/**' --glob '!scripts/verify-public-tree.ps1' '(C:\\Users\\|C:/Users/|/Users/)' $repo)
if ($LASTEXITCODE -eq 0) { throw "Personal user paths found:`n$($pathMatches -join "`n")" }
if ($LASTEXITCODE -ne 1) { throw 'ripgrep path scan failed.' }

$credentialMatches = @(rg -n -i --hidden --glob '!**/.git/**' --glob '!scripts/verify-public-tree.ps1' '(api[_-]?key|client[_-]?secret|github.{0,20}token|paradox.{0,20}(credential|password|token)|bearer\s+[A-Za-z0-9._~+/=-]{12,}|BEGIN [A-Z ]*PRIVATE KEY|webhooks?/[A-Za-z0-9._/-]{20,})' $repo)
if ($LASTEXITCODE -eq 0) { throw "Potential credentials found; review before publishing:`n$($credentialMatches -join "`n")" }
if ($LASTEXITCODE -ne 1) { throw 'ripgrep credential scan failed.' }

Write-Output 'Public-tree checks passed. Run gitleaks as the second independent secret scanner.'
