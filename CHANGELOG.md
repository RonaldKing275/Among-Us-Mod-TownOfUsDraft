# 📝 Changelog - Town of Us Draft Mode

Wszystkie istotne zmiany w projekcie są dokumentowane w tym pliku.

---

## [1.0.0] - 2025-12-28

### ✨ Nowe funkcje
- ✅ **Kompletny system Draft Mode** - Interaktywny wybór ról w turach
- ✅ **Watchdog Timer** - Automatyczny wybór po 20s dla AFK graczy
- ✅ **Pity Shield System** - Ochrona dla graczy ginących wcześnie
- ✅ **Synchronizacja sieciowa** - Pełne wsparcie multiplayer z RPC
- ✅ **System konfiguracji** - BepInEx config dla wszystkich ustawień
- ✅ **9 kategorii ról** - Impostor, 5x Crew, 3x Neutral
- ✅ **60+ mapowanych ról** - Wsparcie dla wszystkich popularnych ról TOU

### 🔧 Poprawki w przeglądzie kodu

#### RoleGenerationPatch.cs
- ❌ Usunięto zbędny import `TownOfUs.Roles`
- ✅ Dodano warunkową blokadę tylko gdy `BlockGeneration = true`
- ✅ Pozwala na normalną grę gdy Draft Mode jest wyłączony

#### DraftManager.cs
- ✅ Lepsze logowanie podczas finalizacji draftu
- ✅ Timeout zwiększony do 5s (50 * 0.1s)
- ✅ Dodano licznik sukcesu aplikacji ról
- ✅ Czyszczenie `PendingRoles` po finalizacji
- ✅ Obsługa rozłączonych graczy (auto-assign Crewmate)
- 🔄 Przeniesienie dodawania ról do `_globalUsedRoles` do `OnPlayerSelectedRole`
- ✅ Fallback na wartości z configa gdy MiraAPI nie zwraca opcji
- ✅ Lepsze logowanie puli ról

#### DraftHud.cs
- 🎨 Cache GUIStyle - brak alokacji co klatkę
- ✅ Timer widoczny dla wszystkich graczy (nie tylko host)
- ✅ Wyświetlanie kategorii podczas oczekiwania
- ⏱️ Synchronizacja timera co 1s przez RPC (ID: 252)
- ⚙️ Timeout pobierany z konfiguracji

#### DraftNetworkPatch.cs
- ✅ Dodano RPC_TIMER_SYNC (ID: 252)
- ✅ Obsługa synchronizacji timera dla wszystkich graczy

#### DeathTracker.cs
- ✅ Lepsze logowanie dla Pity Shield
- ✅ Wyświetlanie nazw graczy zamiast tylko ID
- ✅ Dodano helper `GetPlayerById`
- ✅ Log gdy brak graczy z tarczą

#### TouConfigAdapter.cs
- 🔄 Przepisany z hardcoded wartości na BepInEx ConfigFile
- ✅ Dodano ConfigEntry dla wszystkich opcji
- ✅ Inicjalizacja przez `DraftPlugin`
- ✅ Switch statement dla case-insensitive pobierania wartości

#### DraftPlugin.cs
- ✅ Inicjalizacja konfiguracji przy Load()
- ✅ Ładne logowanie przy starcie z statusem Draft Mode

#### ForceDraftPatch.cs
- ✅ Sprawdzanie `EnableDraftMode` przed uruchomieniem draftu
- ✅ Log gdy Draft Mode jest wyłączony

### 📚 Dokumentacja
- ✅ Kompletny README.md z instrukcjami
- ✅ SUGGESTIONS.md z 20 pomysłami na v2.0
- ✅ CHANGELOG.md (ten plik)

### 🏗️ Architektura
```
Core Files:
├── DraftPlugin.cs          - Entry point (BepInEx)
├── DraftManager.cs         - Draft logic + RPC handlers
├── DraftHud.cs             - UI rendering + Timer management
├── RoleCategorizer.cs      - Role → Category mapping
└── TouConfigAdapter.cs     - BepInEx configuration

Patches:
├── ForceDraftPatch.cs         - IntroCutscene hooks
├── BlockTouGenerationPatch.cs - Blocks TOU role generator
├── RoleGenerationPatch.cs     - Blocks vanilla SelectRoles
├── DraftNetworkPatch.cs       - RPC protocol (249, 251, 252)
├── DeathTracker.cs            - Pity Shield implementation
└── HudPatch.cs                - DraftHud initialization
```

### 🔌 RPC Protocol
| ID  | Name           | Data Format                                      |
|-----|----------------|--------------------------------------------------|
| 249 | ROLE_SELECTED  | `[byte playerId, int roleTypeId]`               |
| 251 | START_TURN     | `[byte playerId, string cat, string[3] options]`|
| 252 | TIMER_SYNC     | `[float currentTime]`                           |

### ⚙️ Konfiguracja (BepInEx/config/TownOfUsDraft.cfg)
```ini
[General]
EnableDraftMode = true
DraftTimeout = 20.0

[Roles]
CrewSupport = 2
CrewProtective = 1
CrewInvestigative = 2
CrewKilling = 1
CrewPower = 0
NeutralKilling = 1
NeutralEvil = 1
NeutralBenign = 0
RandomNeutral = 0
```

---

## [Planowane] - v1.1.0

### 🎯 Planowane funkcje
- [ ] Custom Options integration (zamiast BepInEx config)
- [ ] Reconnect handling (restore draft state)
- [ ] Unity UI upgrade (zamiana OnGUI → Canvas)

### 🐛 Znane problemy
- Timer synchronizuje się z 1s opóźnieniem (zamierzone)
- OnGUI alokuje GC co klatkę mimo cache (wymaga Unity UI)
- Brak obsługi reconnect podczas draftu

---

## [Planowane] - v1.5.0

### 🎯 Planowane funkcje
- [ ] Ban Phase (każdy gracz banuje 1 rolę)
- [ ] Draft Statistics (historia wyborów, winrate)
- [ ] Role Weighting (OP role mają mniejszą szansę)
- [ ] Dynamic Role Pool (dostosowanie do liczby graczy)

---

## [Planowane] - v2.0.0

### 🎯 Planowane funkcje
- [ ] Role Trading (wymiana ról między graczami)
- [ ] Voice Lines & Sound Effects
- [ ] Mobile Touch Controls
- [ ] Multi-Language Support (EN, PL, ES, FR, DE)
- [ ] Animations & Transitions
- [ ] Role Icons (sprite'y dla każdej roli)
- [ ] Spectator Mode (kategorie map dla czekających)

---

## Notacja wersji

Format: `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking changes (np. przepisanie całego systemu)
- **MINOR**: Nowe funkcje (backward compatible)
- **PATCH**: Bugfixy i małe poprawki

---

## Kontakt

Znalazłeś bug? Masz pomysł na funkcję?
- GitHub Issues: [link]
- Discord: [link]

---

**Dziękujemy za korzystanie z Draft Mode! 🎉**

