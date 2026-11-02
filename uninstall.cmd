@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1
if not exist ".venv\Scripts\python.exe" (
    echo ERROR: The installation environment is missing. Run install.cmd first.
    popd
    exit /b 1
)
".venv\Scripts\python.exe" main.py --uninstall
if errorlevel 1 (
    echo ERROR: Uninstall did not complete. Review the error above and try again.
    popd
    exit /b 1
)
popd
exit /b 0
