# 🔨 Build Instructions - Town of Us Draft Mode

Instrukcje kompilacji projektu dla Windows, Linux i macOS.

---

## 📋 Wymagania

### Oprogramowanie
- **.NET SDK 6.0+** - [Pobierz tutaj](https://dotnet.microsoft.com/download/dotnet/6.0)
- **Git** (opcjonalnie) - dla klonowania repo
- **Visual Studio 2022** lub **Rider** (opcjonalnie) - dla komfortowego dev

### Biblioteki (w folderze `libs/`)
✅ Już zawarte w projekcie:
- `0Harmony.dll`
- `Assembly-CSharp.dll` (Among Us)
- `BepInEx.Core.dll`
- `BepInEx.Unity.IL2CPP.dll`
- `Hazel.dll` (networking)
- `Il2CppInterop.Runtime.dll`
- `Il2Cppmscorlib.dll`
- `MiraAPI.dll`
- `TownOfUsMira.dll`
- `UnityEngine.*.dll` (Core, UI, Image, IMGUI, TextRendering)

---

## 🏗️ Kompilacja (Command Line)

### Windows (PowerShell)

```powershell
# 1. Przejdź do folderu projektu
cd C:\Users\RafKing\Desktop\TownOfUsDraft

# 2. Upewnij się, że masz .NET 6.0
dotnet --version  # Powinno pokazać 6.0.x lub wyższe

# 3. Restore dependencies (jeśli są)
dotnet restore TownOfUsDraft.csproj

# 4. Build (Release)
dotnet build TownOfUsDraft.csproj -c Release

# 5. Opcjonalnie: Clean przed buildem
dotnet clean TownOfUsDraft.csproj
dotnet build TownOfUsDraft.csproj -c Release

# Output: bin\Release\net6.0\TownOfUsDraft.dll
```

### Linux / macOS (Bash)

```bash
# 1. Przejdź do folderu projektu
cd ~/TownOfUsDraft

# 2. Sprawdź .NET
dotnet --version

# 3. Restore
dotnet restore TownOfUsDraft.csproj

# 4. Build
dotnet build TownOfUsDraft.csproj -c Release

# Output: bin/Release/net6.0/TownOfUsDraft.dll
```

---

## 🎨 Kompilacja (Visual Studio 2022)

1. **Otwórz projekt**
   - `File → Open → Project/Solution`
   - Wybierz `TownOfUsDraft.sln`

2. **Konfiguracja**
   - W górnym pasku: `Debug` → zmień na `Release`
   - Platform: `Any CPU`

3. **Build**
   - `Build → Build Solution` (Ctrl+Shift+B)
   - Lub prawy klick na projekt → `Build`

4. **Output**
   - `bin\Release\net6.0\TownOfUsDraft.dll`

---

## 🚀 Kompilacja (Rider)

1. **Otwórz projekt**
   - `File → Open`
   - Wybierz folder `TownOfUsDraft` lub plik `.sln`

2. **Konfiguracja**
   - W górnym pasku: Wybierz `Release`

3. **Build**
   - `Build → Build Solution` (Ctrl+Shift+B)
   - Lub ikona młotka w górnym pasku

4. **Output**
   - `bin/Release/net6.0/TownOfUsDraft.dll`

---

## 📦 Deployment

### Automatyczne kopiowanie do Among Us

Dodaj do `TownOfUsDraft.csproj` (po `</PropertyGroup>`):

```xml
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Copy SourceFiles="$(TargetPath)" DestinationFolder="D:\Steam\steamapps\common\Among Us\BepInEx\plugins\" />
  <Message Text="✅ DLL skopiowana do Among Us!" Importance="high" />
</Target>
```

**Zmień ścieżkę** na swoją instalację Among Us!

Teraz każdy build automatycznie skopiuje DLL do gry.

---

## 🧪 Debug Build

Dla debugowania (symbole + logi):

```bash
dotnet build TownOfUsDraft.csproj -c Debug
```

**Różnice Debug vs Release:**

| Feature           | Debug | Release |
|-------------------|-------|---------|
| Optymalizacje     | ❌    | ✅      |
| Symbole debug     | ✅    | ❌      |
| Rozmiar DLL       | Większy | Mniejszy |
| Performance       | Wolniejsze | Szybsze |

**Uwaga:** Do testowania w grze używaj **Release**! Debug może być zbyt wolny.

---

## 🐛 Troubleshooting

### Problem: "SDK not found"
**Rozwiązanie:**
```bash
# Sprawdź zainstalowane SDK
dotnet --list-sdks

# Jeśli brak 6.0.x - zainstaluj:
# Windows: winget install Microsoft.DotNet.SDK.6
# Linux: sudo apt install dotnet-sdk-6.0
# macOS: brew install dotnet@6
```

### Problem: "Reference 'Assembly-CSharp' could not be found"
**Rozwiązanie:**
- Upewnij się, że folder `libs/` zawiera wszystkie DLL
- Sprawdź czy `TownOfUsDraft.csproj` ma linię:
  ```xml
  <Reference Include="libs/*.dll" />
  ```

### Problem: "CS0246: The type or namespace 'X' could not be found"
**Rozwiązanie:**
- Brakuje DLL w `libs/`
- Sprawdź listę wymaganych bibliotek powyżej

### Problem: "CS0012: The type 'X' is defined in an assembly that is not referenced"
**Rozwiązanie:**
- Dodaj brakujący DLL do `libs/`
- Przykład dla `UnityEngine.UI`:
  ```xml
  <Reference Include="libs/UnityEngine.UI.dll" />
  ```

### Problem: Build sukces, ale mod nie działa w grze
**Rozwiązanie:**
1. Sprawdź logi: `Among Us/BepInEx/LogOutput.log`
2. Upewnij się, że masz:
   - BepInEx IL2CPP
   - Town of Us Mira
   - MiraAPI
3. Sprawdź kompatybilność wersji Among Us (obecnie: 2024.x.x)

---

## 📁 Struktura Output

Po build'zie:

```
bin/
└── Release/
    └── net6.0/
        ├── TownOfUsDraft.dll          ← Main mod file
        ├── TownOfUsDraft.deps.json
        ├── TownOfUsDraft.pdb          ← Debug symbols (tylko Debug build)
        └── [wszystkie libs/*.dll]     ← Skopiowane dependencies
```

**Do instalacji w grze potrzebny jest tylko:**
- `TownOfUsDraft.dll`

Reszta plików jest ignorowana (Among Us ma już te biblioteki z BepInEx/TOU).

---

## 🔄 Automatyczny Build na commit (GitHub Actions)

Opcjonalnie: Dodaj `.github/workflows/build.yml`:

```yaml
name: Build

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 6.0.x
    
    - name: Restore dependencies
      run: dotnet restore TownOfUsDraft.csproj
    
    - name: Build
      run: dotnet build TownOfUsDraft.csproj -c Release --no-restore
    
    - name: Upload artifact
      uses: actions/upload-artifact@v3
      with:
        name: TownOfUsDraft-dll
        path: bin/Release/net6.0/TownOfUsDraft.dll
```

Każdy commit automatycznie builduje DLL!

---

## 🚢 Release Checklist

Przed wypuszczeniem nowej wersji:

- [ ] Zmień wersję w `DraftPlugin.cs`:
  ```csharp
  [BepInPlugin("TownOfUsDraft", "Town Of Us Draft Mode", "1.0.0")]
  ```
- [ ] Zaktualizuj `CHANGELOG.md`
- [ ] Build w trybie Release
- [ ] Przetestuj w grze (minimum 2 gry)
- [ ] Sprawdź logi pod kątem błędów
- [ ] Stwórz release na GitHub z:
  - `TownOfUsDraft.dll`
  - `README.md`
  - `CHANGELOG.md`
- [ ] Opcjonalnie: ZIP z `TownOfUsDraft.dll` + instrukcja instalacji

---

## 📊 Build Metrics

Typowe czasy kompilacji:

| Konfiguracja          | Czas  | Rozmiar DLL |
|-----------------------|-------|-------------|
| Debug (Clean)         | ~5s   | ~120 KB     |
| Debug (Incremental)   | ~1s   | ~120 KB     |
| Release (Clean)       | ~6s   | ~100 KB     |
| Release (Incremental) | ~1.5s | ~100 KB     |

*Czasy dla Ryzen 5 5600X, SSD NVMe*

---

## 🤝 Contributing Build Setup

Jeśli chcesz kontrybuować:

1. **Fork repo**
2. **Clone lokalnie**
   ```bash
   git clone https://github.com/YOUR_USERNAME/TownOfUsDraft.git
   ```
3. **Setup libs**
   - Skopiuj wszystkie DLL z `Among Us/BepInEx/` do `libs/`
4. **Build & Test**
   ```bash
   dotnet build -c Debug
   # Testuj w grze
   ```
5. **Commit changes**
   ```bash
   git commit -m "Fixed XYZ"
   ```
6. **Push & Pull Request**

---

## 📞 Pomoc

Problemy z kompilacją?
- GitHub Issues: [link]
- Discord: [link]

---

**Happy Building! 🎉**

