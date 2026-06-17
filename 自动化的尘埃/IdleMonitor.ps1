# BetterGI Idle Auto Trigger (v3)
param(
    [int]$IdleThresholdSeconds = 900,
    [int]$MinGapSeconds = 72000,
    [int]$FastCheckInterval = 60,
    [int]$SlowCheckInterval = 3600,
    [string]$MarkerFile = (Join-Path $PSScriptRoot ".bettergi_last_run"),
    [string]$LogFile = (Join-Path $PSScriptRoot "Execution-Log.md"),
    [string]$StatusFile = (Join-Path $PSScriptRoot "status.txt"),
    [string]$ShortcutPath = "C:\Users\32683\Desktop\原神牛逼\RunBetterGI.bat - 快捷方式.lnk",
    [string]$ProcessName = "BetterGI"
)
$script:lastStatus = $null
$csharp = @"
using System;
using System.Runtime.InteropServices;
public static class UserIdle {
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO p);
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    public static uint GetSeconds() {
        LASTINPUTINFO i = new LASTINPUTINFO();
        i.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        if (!GetLastInputInfo(ref i)) return 0;
        return ((uint)Environment.TickCount - i.dwTime) / 1000;
    }
}
"@
Add-Type -TypeDefinition $csharp -ErrorAction Stop
$wd = Split-Path -Parent $MarkerFile
if (-not (Test-Path $wd)) { New-Item -ItemType Directory -Path $wd -Force | Out-Null }
$div = "=" * 50
function Write-Status($gapSec, $idleSec, $state) {
    if ($state -eq $script:lastStatus) { return }
    $script:lastStatus = $state
    $gapH = [math]::Floor($gapSec / 3600); $gapM = [math]::Floor(($gapSec % 3600) / 60)
    $idleM = [math]::Floor($idleSec / 60)
    if ($gapSec -ge 99999999) { $gapH = "N/A"; $gapM = "N/A" }
    $body = "Monitor: ALIVE | Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | Gap: $gapH h $gapM m / 20h | Idle: $idleM min / 15min | State: $state"
    "# $body" | Out-File -FilePath $StatusFile -Encoding utf8 -Force
}
function Write-Log($tag, $msg) {
    $t = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    Add-Content -Path $LogFile -Value "`n$div`n`n[$tag]  $t`n$msg`n" -Encoding UTF8
}
function Format-Gap($s) { if ($s -ge 3600) { $h=[math]::Floor($s/3600); $m=[math]::Floor(($s%3600)/60); return "$h h $m m" }; return "$([math]::Floor($s/60)) m" }
Write-Log "启动" "监控脚本已启动，开始后台轮询"
$now = Get-Date; $last = $null
if (Test-Path $MarkerFile) { try { $raw = (Get-Content $MarkerFile -Encoding UTF8 -EA SilentlyContinue).Trim(); $last = [DateTime]::Parse($raw) } catch { $last = $null } }
$gap = if ($last) { [math]::Floor(($now-$last).TotalSeconds) } else { [int]::MaxValue }
if ($last) {
    $gt = Format-Gap $gap
    if ($gap -lt $MinGapSeconds) {
        Write-Log "状态" "距上次执行： $gt (不足 20 小时）`n进入低频轮询模式（每 1 小时检查一次）"
    } else {
        $idle = [UserIdle]::GetSeconds()
        Write-Log "状态" "距上次执行： $gt (已满 20 小时）`n进入高频监控模式（每 1 分钟检查一次）"
    }
    Write-Status $gap $idle "WARMUP"
} else {
    Write-Log "状态" "未找到历史执行记录，视为首次运行`n进入高频监控模式（每 1 分钟检查一次）"
    Write-Status 99999999 0 "FIRST_RUN"
}
while ($true) {
    $bg = Get-Process -Name $ProcessName -EA SilentlyContinue
    if ($bg) {
        $ns = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss"); $ns | Out-File -FilePath $MarkerFile -Encoding utf8 -Force
        Write-Log "跳过" "检测到 BetterGI 已在运行（进程 PID $($bg.Id)）`n判定为手动启动，写入时间戳，监控退出。"
        "# MONITOR STOPPED - BetterGI already running" | Out-File -FilePath $StatusFile -Encoding utf8 -Force
        exit 0
    }
    $now = Get-Date; $gap = if ($last) { [math]::Floor(($now-$last).TotalSeconds) } else { [int]::MaxValue }
    if ($gap -ge $MinGapSeconds) {
        $idle = [UserIdle]::GetSeconds()
        if ($idle -ge $IdleThresholdSeconds) {
            $ns = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss"); $ns | Out-File -FilePath $MarkerFile -Encoding utf8 -Force
            $gt = Format-Gap $gap
            $idleM = [math]::Floor($idle/60)
            Write-Log "DONE" "系统空闲 $idleM 分钟，距上次执行 $gt`n已启动 BetterGI 一条龙，监控脚本退出。"
            Write-Status $gap $idle "TRIGGERED"
            Start-Process -FilePath $ShortcutPath 
            exit 0
        }
        Write-Status $gap $idle "WAITING_IDLE"
        Start-Sleep -Seconds $FastCheckInterval
    } else {
        Write-Status $gap 0 "WAITING_GAP"
        $rem = $MinGapSeconds - $gap; $sl = [math]::Min($rem, $SlowCheckInterval)
        Start-Sleep -Seconds $sl
    }
}
