param(
    [string]$BaseUrl = "http://santuario-nerd.localhost:5080",
    [string]$Email = "admin@santuario-nerd.local",
    [string]$Password = $env:LOAD_TEST_PASSWORD,
    [string]$Endpoint = "/api/analytics/dashboard",
    [ValidateRange(1, 100)]
    [int]$Concurrency = 4,
    [ValidateRange(1, 1000)]
    [int]$Iterations = 3,
    [switch]$Compressed
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

if ([string]::IsNullOrWhiteSpace($Password)) {
    throw "Defina LOAD_TEST_PASSWORD somente no processo local antes de executar."
}

$handler = New-Object System.Net.Http.HttpClientHandler
$handler.UseCookies = $true
$handler.CookieContainer = New-Object System.Net.CookieContainer
$client = New-Object System.Net.Http.HttpClient($handler)
$client.BaseAddress = [Uri]$BaseUrl
$client.Timeout = [TimeSpan]::FromSeconds(60)

try {
    $loginJson = @{ email = $Email; password = $Password } | ConvertTo-Json -Compress
    $loginBody = New-Object System.Net.Http.StringContent(
        $loginJson,
        [Text.Encoding]::UTF8,
        "application/json"
    )
    $loginResponse = $client.PostAsync("/api/auth/login", $loginBody).GetAwaiter().GetResult()
    if (-not $loginResponse.IsSuccessStatusCode) {
        throw "Login falhou com HTTP $([int]$loginResponse.StatusCode)."
    }

    if ($Compressed) {
        $client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip")
    }

    $statusCounts = @{}
    $totalBytes = 0L
    $totalRequests = $Concurrency * $Iterations
    $timer = [Diagnostics.Stopwatch]::StartNew()

    for ($round = 1; $round -le $Iterations; $round++) {
        $tasks = @()
        for ($worker = 1; $worker -le $Concurrency; $worker++) {
            $tasks += $client.GetAsync($Endpoint)
        }

        [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
        foreach ($task in $tasks) {
            $response = $task.Result
            $status = [int]$response.StatusCode
            if (-not $statusCounts.ContainsKey($status)) {
                $statusCounts[$status] = 0
            }
            $statusCounts[$status]++
            $body = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            $totalBytes += $body.LongLength
            $response.Dispose()
        }
    }

    $timer.Stop()
    [pscustomobject]@{
        endpoint = $Endpoint
        concurrency = $Concurrency
        requests = $totalRequests
        elapsedSeconds = [Math]::Round($timer.Elapsed.TotalSeconds, 3)
        requestsPerSecond = [Math]::Round($totalRequests / $timer.Elapsed.TotalSeconds, 2)
        averageResponseBytes = [Math]::Round($totalBytes / [double]$totalRequests, 0)
        statuses = ($statusCounts.GetEnumerator() | Sort-Object Name | ForEach-Object {
            "$($_.Name)=$($_.Value)"
        }) -join ","
    } | ConvertTo-Json -Compress
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
