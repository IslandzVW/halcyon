@echo off
del VersionInfo.cs >nul
svn update VersionInfo.cs
svnversion -n >>VersionInfo.temp
copy VersionInfo.part1+VersionInfo.temp+VersionInfo.part2 VersionInfo.cs
del VersionInfo.temp >nul
