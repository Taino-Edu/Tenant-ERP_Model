param(
    [string]$BaseUrl = "http://exemplosvisual.localhost:3000",
    [string[]]$Paths = @("/", "/robots.txt", "/sitemap.xml"),
    [ValidateRange(1, 20)]
    [int]$Concurrency = 4,
    [ValidateRange(1, 50)]
    [int]$Iterations = 3,
    [switch]$AllowRemote
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
if (-not ('Octus.Performance.HttpLoadProbe' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'HttpLoadProbe.cs')
}

$baseUri = [Uri]$BaseUrl
$isLocal = $baseUri.Host -eq "localhost" `
    -or $baseUri.Host -eq "127.0.0.1" `
    -or $baseUri.Host -eq "::1" `
    -or $baseUri.Host.EndsWith(".localhost", [StringComparison]::OrdinalIgnoreCase)

if (-not $isLocal -and -not $AllowRemote) {
    throw "Destino remoto bloqueado. Use -AllowRemote somente com autorização e janela de teste."
}
if ($baseUri.Scheme -notin @('http', 'https')) { throw "Use HTTP ou HTTPS." }
foreach ($path in $Paths) {
    $targetUri = [Uri]::new($baseUri, $path)
    if (-not $path.StartsWith('/') -or $path.StartsWith('//') -or $path.Contains('\') `
        -or $targetUri.Authority -ne $baseUri.Authority -or $targetUri.Scheme -ne $baseUri.Scheme) {
        throw "Caminho fora da origem autorizada: $path"
    }
}

$handler = [Net.Http.HttpClientHandler]::new()
# Não seguir um redirecionamento local para produção ou serviço externo.
$handler.AllowAutoRedirect = $false
$handler.AutomaticDecompression = [Net.DecompressionMethods]::GZip `
    -bor [Net.DecompressionMethods]::Deflate `
    -bor [Net.DecompressionMethods]::Brotli
$client = [Net.Http.HttpClient]::new($handler)
$client.BaseAddress = $baseUri
$client.Timeout = [TimeSpan]::FromSeconds(30)
$client.DefaultRequestHeaders.UserAgent.ParseAdd("Octus-Authorized-Load-Test/1.0")

try {
    foreach ($path in $Paths) {
        $statusCounts = @{}
        $cacheControl = $null
        $contentType = $null
        $totalBytes = 0L
        $durations = [Collections.Generic.List[double]]::new()
        $errors = 0
        $totalRequests = $Concurrency * $Iterations
        $timer = [Diagnostics.Stopwatch]::StartNew()

        for ($round = 1; $round -le $Iterations; $round++) {
            $tasks = 1..$Concurrency | ForEach-Object { [Octus.Performance.HttpLoadProbe]::GetAsync($client, $path) }
            [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
            foreach ($task in $tasks) {
                $sample = $task.Result
                $status = if ($sample.Error) { $sample.Error } else { [string]$sample.Status }
                $statusCounts[$status] = 1 + ($statusCounts[$status] ?? 0)
                $cacheControl ??= $sample.CacheControl
                $contentType ??= $sample.ContentType
                $totalBytes += $sample.Bytes
                $durations.Add($sample.Milliseconds)
                if ($sample.Error -or $sample.Status -ge 400) { $errors++ }
            }
        }

        $timer.Stop()
        $ordered = @($durations | Sort-Object)
        [pscustomobject]@{
            path = $path
            concurrency = $Concurrency
            requests = $totalRequests
            elapsedSeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3)
            requestsPerSecond = [Math]::Round($totalRequests / $timer.Elapsed.TotalSeconds, 2)
            p50Ms = [Math]::Round($ordered[[Math]::Ceiling($ordered.Count * 0.50) - 1], 2)
            p95Ms = [Math]::Round($ordered[[Math]::Ceiling($ordered.Count * 0.95) - 1], 2)
            p99Ms = [Math]::Round($ordered[[Math]::Ceiling($ordered.Count * 0.99) - 1], 2)
            errors = $errors
            averageResponseBytes = [Math]::Round($totalBytes / [double]$totalRequests, 0)
            statuses = ($statusCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ","
            cacheControl = $cacheControl
            contentType = $contentType
        }
    }
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
