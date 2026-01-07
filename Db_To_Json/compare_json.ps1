# AQ.json vs AQ-cn.json 比较脚本
# PowerShell 版本

param(
    [string]$File1 = "Output\AQ.json",
    [string]$File2 = "Output\AQ-cn.json"
)

Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host ("="*69) -ForegroundColor Cyan
Write-Host "  AQ.json vs AQ-cn.json 比较报告" -ForegroundColor Yellow
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host ("="*69) -ForegroundColor Cyan
Write-Host ""

# 加载 JSON 文件
Write-Host "📂 正在加载文件..." -ForegroundColor Gray

try {
    $data1 = Get-Content $File1 -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Host "   ✅ 加载成功: $File1" -ForegroundColor Green
} catch {
    Write-Host "   ❌ 加载失败: $File1" -ForegroundColor Red
    Write-Host "   错误: $_" -ForegroundColor Red
    exit 1
}

try {
    $data2 = Get-Content $File2 -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Host "   ✅ 加载成功: $File2" -ForegroundColor Green
} catch {
    Write-Host "   ❌ 加载失败: $File2" -ForegroundColor Red
    Write-Host "   错误: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 获取所有属性
$props1 = $data1.PSObject.Properties | Select-Object -ExpandProperty Name
$props2 = $data2.PSObject.Properties | Select-Object -ExpandProperty Name
$allProps = ($props1 + $props2) | Select-Object -Unique | Sort-Object

# 打印表头
$format = "{0,-30} {1,-20} {2,-20} {3,10}"
Write-Host ($format -f "类型", "AQ.json (英文)", "AQ-cn.json (中文)", "差异") -ForegroundColor Cyan
Write-Host ("-"*85) -ForegroundColor Gray

$totalDiff = 0

# 遍历所有属性
foreach ($prop in $allProps) {
    $count1 = 0
    $count2 = 0
    
    if ($data1.$prop) {
        if ($data1.$prop -is [Array]) {
            $count1 = $data1.$prop.Count
        } elseif ($data1.$prop -is [PSCustomObject]) {
            $count1 = ($data1.$prop.PSObject.Properties).Count
        } else {
            $count1 = 1
        }
    }
    
    if ($data2.$prop) {
        if ($data2.$prop -is [Array]) {
            $count2 = $data2.$prop.Count
        } elseif ($data2.$prop -is [PSCustomObject]) {
            $count2 = ($data2.$prop.PSObject.Properties).Count
        } else {
            $count2 = 1
        }
    }
    
    $diff = $count2 - $count1
    $totalDiff += [Math]::Abs($diff)
    
    $diffSymbol = if ($diff -eq 0) { "✅" } 
                  elseif ([Math]::Abs($diff) -lt 100) { "⚠️" } 
                  else { "❌" }
    
    $color = if ($diff -eq 0) { "Green" }
             elseif ([Math]::Abs($diff) -lt 100) { "Yellow" }
             else { "Red" }
    
    Write-Host ($format -f $prop, $count1, $count2, "$diff $diffSymbol") -ForegroundColor $color
}

Write-Host ("-"*85) -ForegroundColor Gray
Write-Host ""

# 总结
Write-Host "📊 总结:" -ForegroundColor Yellow
Write-Host "   - 总类型数: $($allProps.Count)" -ForegroundColor White
Write-Host "   - 总差异数: $totalDiff" -ForegroundColor White

if ($totalDiff -eq 0) {
    Write-Host "   ✅ 两个文件数据量完全一致！" -ForegroundColor Green
} elseif ($totalDiff -lt 100) {
    Write-Host "   ⚠️ 存在少量差异，可能是数据库版本差异" -ForegroundColor Yellow
} else {
    Write-Host "   ❌ 存在较大差异，需要检查数据库" -ForegroundColor Red
}

Write-Host ""
Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host ("="*69) -ForegroundColor Cyan
