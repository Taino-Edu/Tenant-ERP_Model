[CmdletBinding()]
param(
    [ValidateSet('all', 'financeiro', 'fiscal')]
    [string[]]$Module = @('all'),
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'output\packages'
}
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)

if ($Module -contains 'all') {
    $Module = @('financeiro', 'fiscal')
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$candidateFiles = & git -C $repoRoot ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel listar os arquivos do repositorio.' }
$candidateFiles = $candidateFiles |
    ForEach-Object { $_.Replace('\', '/') } |
    Where-Object { Test-Path -LiteralPath (Join-Path $repoRoot $_) }

foreach ($moduleName in $Module) {
    $moduleRoot = Join-Path $PSScriptRoot $moduleName
    $manifestPath = Join-Path $moduleRoot 'module.json'
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

    $selected = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)

    foreach ($path in $candidateFiles) {
        foreach ($pattern in $manifest.includePatterns) {
            if ($path -match $pattern) {
                [void]$selected.Add($path)
                break
            }
        }
    }

    foreach ($entrypoint in $manifest.requiredEntrypoints) {
        if (-not $selected.Contains($entrypoint)) {
            throw "O pacote $moduleName perdeu o arquivo obrigatorio: $entrypoint"
        }
    }

    $stage = Join-Path $outputRoot ".stage-$moduleName"
    $resolvedStage = [System.IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($outputRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Diretorio temporario fora da saida permitida: $resolvedStage"
    }
    if (Test-Path -LiteralPath $resolvedStage) {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resolvedStage | Out-Null

    foreach ($relativePath in ($selected | Sort-Object)) {
        $source = Join-Path $repoRoot $relativePath
        $destination = Join-Path $resolvedStage $relativePath
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }

    Copy-Item -LiteralPath (Join-Path $moduleRoot 'README.md') -Destination $resolvedStage
    Copy-Item -LiteralPath $manifestPath -Destination $resolvedStage

    $hashLines = Get-ChildItem -LiteralPath $resolvedStage -Recurse -File |
        Sort-Object FullName |
        ForEach-Object {
            $relative = $_.FullName.Substring($resolvedStage.Length + 1).Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $relative"
        }
    Set-Content -LiteralPath (Join-Path $resolvedStage 'PACKAGE-CONTENTS.sha256') -Value $hashLines -Encoding utf8

    $zipPath = Join-Path $outputRoot ("TenantERP-{0}-source.zip" -f $moduleName)
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $resolvedStage '*') -DestinationPath $zipPath -CompressionLevel Optimal
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force

    $zip = Get-Item -LiteralPath $zipPath
    Write-Host ("{0}: {1} arquivos, {2:N1} MB -> {3}" -f $moduleName, $selected.Count, ($zip.Length / 1MB), $zip.FullName)
}
