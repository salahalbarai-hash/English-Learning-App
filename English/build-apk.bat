@echo off
echo Cleaning project...
dotnet clean

echo.
echo Building APK...
dotnet publish -f net9.0-android -c Release -p:AndroidPackageFormat=apk

echo.
echo Opening output folder...
start "" "bin\Release\net9.0-android\publish\"

echo DONE ✅
pause