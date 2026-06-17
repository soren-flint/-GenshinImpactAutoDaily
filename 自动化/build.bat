@echo off
chcp 65001 >nul
echo ============================================
echo   AutoDaily v2 - Build Script
echo ============================================
echo.

:: 检查 dotnet
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 未找到 .NET 8 SDK，请先安装：
    echo   https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo [1/2] 编译项目（单文件自包含，约 70MB）...
dotnet publish -c Release -o .\publish
if %errorlevel% neq 0 (
    echo [错误] 编译失败！请检查错误信息。
    pause
    exit /b 1
)

echo.
echo ============================================
echo   编译完成！
echo   输出目录：%~dp0publish\
echo   主程序：AutoDaily.exe
echo ============================================
echo.
echo 使用方式：
echo   1. 将 publish\AutoDaily.exe 复制到任意位置
echo   2. 双击运行，首次自动弹出设置向导
echo   3. 关闭窗口 → 最小化到系统托盘
echo.
echo 分发给其他 BetterGI 用户：只需这一个 EXE 文件。
echo ============================================
pause
