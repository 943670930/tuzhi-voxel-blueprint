@echo off
cd /d "%~dp0"
echo Starting low-poly preview server...
python lowpoly_preview_server.py
if errorlevel 1 (
  echo.
  echo Failed. Try: pip install -r requirements-voxel-lowpoly.txt
  echo Also need: Tools\instant-meshes\Instant Meshes.exe
  pause
)
