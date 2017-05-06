..\..\..\bin\flatc-win.exe --csharp Vector3.fbs
..\..\..\bin\flatc-win.exe --csharp Quaternion.fbs
..\..\..\bin\flatc-win.exe --csharp HalcyonPrimitiveBaseShape.fbs
..\..\..\bin\flatc-win.exe --csharp HalcyonPrimitive.fbs
..\..\..\bin\flatc-win.exe --csharp HalcyonGroup.fbs

move /Y .\InWorldz\Arbiter\Serialization\* ..\Serialization
rmdir /S/Q .\InWorldz
