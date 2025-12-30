@echo off
chcp 65001 > nul
echo Kopiowanie TownOfUsDraft.dll do wszystkich instalacji...
echo.

copy /Y "D:\Users\RafKing\Desktop\TownOfUsDraft\bin\Debug\net6.0\TownOfUsDraft.dll" "D:\Steam\steamapps\common\Among Us\BepInEx\plugins\TownOfUsDraft.dll"
if %errorlevel% == 0 (echo ✓ Instalacja 1) else (echo ✗ Błąd instalacja 1)

copy /Y "D:\Users\RafKing\Desktop\TownOfUsDraft\bin\Debug\net6.0\TownOfUsDraft.dll" "D:\Steam\steamapps\common\Among Us — kopia\BepInEx\plugins\TownOfUsDraft.dll"
if %errorlevel% == 0 (echo ✓ Instalacja 2) else (echo ✗ Błąd instalacja 2)

copy /Y "D:\Users\RafKing\Desktop\TownOfUsDraft\bin\Debug\net6.0\TownOfUsDraft.dll" "D:\Steam\steamapps\common\Among Us — kopia (2)\BepInEx\plugins\TownOfUsDraft.dll"
if %errorlevel% == 0 (echo ✓ Instalacja 3) else (echo ✗ Błąd instalacja 3)

copy /Y "D:\Users\RafKing\Desktop\TownOfUsDraft\bin\Debug\net6.0\TownOfUsDraft.dll" "D:\Steam\steamapps\common\Among Us — kopia (3)\BepInEx\plugins\TownOfUsDraft.dll"
if %errorlevel% == 0 (echo ✓ Instalacja 4) else (echo ✗ Błąd instalacja 4)

echo.
echo Gotowe!
pause

