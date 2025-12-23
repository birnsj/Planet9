@echo off
REM Helper script to build all MonoGame content files
echo Building MonoGame content...
cd ..
dotnet mgcb Content\Content.mgcb /rebuild
pause

