# Đóng gói bản host thử: API cho MonsterASP (IIS), script CSDL dự phòng, hai giao diện cho Cloudflare Pages.
# Chạy từ thư mục gốc repo:  powershell -ExecutionPolicy Bypass -File deploy\monsterasp\publish.ps1 [-FrameworkDependent]
#   -FrameworkDependent: host đã có sẵn ASP.NET Core 10 thì gói nhỏ (~40 MB); mặc định kèm .NET (~185 MB) để chạy ở mọi host.
# Kết quả ở artifacts\test-host (đã bỏ qua trong git). Không chứa bí mật: appsettings.Staging.json tự điền trên máy chủ.
param([switch]$FrameworkDependent)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$out = Join-Path $root 'artifacts\test-host'

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }
function Run($exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$exe thất bại (mã $LASTEXITCODE)" }
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
Step "API: đóng gói cho IIS (self-contained = $selfContained)"
# Khôi phục gói cho win-x64 ghi thêm vào packages.lock.json; giữ nguyên tệp khóa của repo để CI (--locked-mode) không lệch
$locks = Get-ChildItem "$root\src" -Recurse -Filter 'packages.lock.json' | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$saved = @{}
foreach ($lock in $locks) { $saved[$lock.FullName] = [System.IO.File]::ReadAllBytes($lock.FullName) }
try {
    Run 'dotnet' @('publish', "$root\src\ObentoTensho.Api", '-c', 'Release', '-r', 'win-x64', '--self-contained', $selfContained,
        '/p:EnvironmentName=Staging', '-o', "$out\api")
} finally {
    foreach ($path in $saved.Keys) { [System.IO.File]::WriteAllBytes($path, $saved[$path]) }
}
Copy-Item "$PSScriptRoot\appsettings.Staging.example.json" "$out\api\appsettings.Staging.example.json"
# Host không hiện log màn hình và bản thử chưa có SMS thật (mã OTP chỉ nằm trong log): ghi log ra logs\stdout*.log
$webConfig = "$out\api\web.config"
(Get-Content $webConfig -Raw) -replace 'stdoutLogEnabled="false"', 'stdoutLogEnabled="true"' | Set-Content $webConfig -Encoding UTF8
Compress-Archive -Path "$out\api\*" -DestinationPath "$out\api.zip"

Step 'CSDL: script SQL dự phòng (API đã tự chạy migration khi Database:MigrateOnStartup = true)'
$env:ConnectionStrings__Default = 'Server=.;Database=KhongDung;Trusted_Connection=True;TrustServerCertificate=True'
Run 'dotnet' @('ef', 'migrations', 'script', '--idempotent', '--project', "$root\src\ObentoTensho.Infrastructure",
    '--startup-project', "$root\src\ObentoTensho.Infrastructure", '-o', "$out\db.sql")
Remove-Item Env:\ConnectionStrings__Default

Step 'Giao diện khách (tệp tĩnh) và trang quản trị'
Push-Location "$root\web"
try {
    Run 'npx' @('ng', 'build', 'customer', '--configuration', 'production,pages')
    Run 'npx' @('ng', 'build', 'admin')
} finally { Pop-Location }

# Mỗi dự án Pages một thư mục: site\ là tệp tĩnh, functions\ là hàm chuyển tiếp /api /uploads /hubs
foreach ($app in 'customer', 'admin') {
    $target = Join-Path $out $app
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item "$root\web\dist\$app\browser" "$target\site" -Recurse
    Copy-Item "$root\deploy\cloudflare\functions" "$target\functions" -Recurse
}

Step 'Khóa ký JWT mới (chỉ cần cho lần host đầu; đã có khóa trên host thì giữ khóa cũ)'
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
Write-Host ([Convert]::ToBase64String($bytes)) -ForegroundColor Yellow

$size = [math]::Round((Get-Item "$out\api.zip").Length / 1MB)
Write-Host "`nXong: $out" -ForegroundColor Green
Write-Host "  api.zip ($size MB) -> tải lên wwwroot của site API rồi giải nén (xem quy trình host)"
Write-Host '  db.sql             -> chỉ dùng khi migration tự động lỗi: chạy trong Run T-SQL của CSDL'
Write-Host '  customer\          -> cd vào đây rồi: npx wrangler pages deploy site --project-name <ten-giao-dien-khach>'
Write-Host '  admin\             -> cd vào đây rồi: npx wrangler pages deploy site --project-name <ten-trang-quan-tri>'
