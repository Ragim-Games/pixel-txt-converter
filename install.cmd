@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1

echo Pixel TXT Converter - CMD setup
echo.
if exist ".venv\Scripts\python.exe" goto check_python

python -c "import sys; sys.exit(sys.version_info < (3, 11))" >nul 2>&1
if not errorlevel 1 goto create_with_python
py -3 -c "import sys; sys.exit(sys.version_info < (3, 11))" >nul 2>&1
if not errorlevel 1 goto create_with_launcher
echo ERROR: Python 3.11 or later was not found.
echo Install Python with Tcl/Tk and pip from https://www.python.org/downloads/windows/
echo Enable Add Python to PATH, then reopen CMD and run install.cmd again.
goto failed

:create_with_python
python -m venv .venv
if errorlevel 1 goto failed
goto check_python

:create_with_launcher
py -3 -m venv .venv
if errorlevel 1 goto failed

:check_python
".venv\Scripts\python.exe" -c "import sys, tkinter; sys.exit(sys.version_info < (3, 11))"
if errorlevel 1 goto failed
echo Installing Python dependencies...
".venv\Scripts\python.exe" -m pip install --disable-pip-version-check -r requirements.txt
if errorlevel 1 goto failed
echo Registering Explorer commands and starting automatic conversion...
".venv\Scripts\python.exe" main.py --install
if errorlevel 1 goto failed
echo.
echo Keep this folder and its .venv folder in place while the app is installed.
echo Use uninstall.cmd to remove automatic conversion and Explorer integration.
popd
exit /b 0

:failed
echo.
echo ERROR: Setup did not complete. Review the error above, then run install.cmd again.
popd
exit /b 1
