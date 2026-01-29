# Town of Us - Draft Mode Addon

**Wersja:** 1.3.0  
**Autor:** RonaldKing  
**Kompatybilność:** Town of Us Mira + MiraAPI

Addon do moda **Town of Us Mira**, wprowadzający tryb **Draftu** (wyboru ról) na początku rozgrywki.

## 📦 Instalacja

1. **Wymagania wstępne:**
   - [BepInEx IL2CPP](https://github.com/BepInEx/BepInEx) (wersja 6.0+)
   - [Town of Us Mira](https://github.com/TownOfUs-Mira/TownOfUs-Mira)
   - [MiraAPI](https://github.com/TownOfUs-Mira/MiraAPI)

2. **Instalacja:**
   - Skopiuj plik `TownOfUsDraft.dll` do folderu:
     `Among Us/BepInEx/plugins/`

3. **Konfiguracja:**
   - Uruchom grę raz, aby wygenerować plik konfiguracyjny.
   - Plik znajdziesz w: `Among Us/BepInEx/config/TownOfUsDraft.cfg`

---

## ⚙️ Architektura techniczna

### Główne komponenty
| Plik | Opis |
| :--- | :--- |
| `DraftPlugin.cs` | Entry point (BepInEx), inicjalizacja Harmony |
| `DraftManager.cs` | Główna logika draftu, obsługa stanów i RPC |
| `DraftHud.cs` | Obsługa UI (OnGUI), przyciski i Timer |
| `RoleCategorizer.cs` | Dynamiczne mapowanie ról z TOU na kategorie draftu |
| `TouConfigAdapter.cs` | System konfiguracji i integracja z ustawieniami |

### Struktura Patchy (`Patches/`)
```text
Patches/
├── ForceDraftPatch.cs         → Hook do intro cutscene (start draftu)
├── BlockTouGenerationPatch.cs → Blokowanie domyślnego generatora TOU
├── RoleGenerationPatch.cs     → Blokowanie generatora Vanilla (Among Us)
├── DraftNetworkPatch.cs       → Obsługa i routing pakietów RPC
├── DeathTracker.cs            → System "Pity Shield" (ochrona pechowców)
└── HudPatch.cs                → Inicjalizacja i wstrzykiwanie DraftHud