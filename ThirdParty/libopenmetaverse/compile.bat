@echo off 
C:\WINDOWS\Microsoft.NET\Framework\v3.5\msbuild OpenMetaverse.sln 
:SUCCESS 
echo Build Successful! 
exit /B 0 
:FAIL 
echo Build Failed, check log for reason 
exit /B 1 
