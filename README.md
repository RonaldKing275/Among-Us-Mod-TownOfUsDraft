# 🎮 Town of Us - Draft Mode

**Wersja:** 1.1.0  
**Autor:** RonaldKing  
**Kompatybilność:** Town of Us Mira + MiraAPI

---

## 📋 Opis

Draft Mode to dodatek do Town of Us Mira, który wprowadza system draftu ról. Zamiast losowego przydzielania ról, gracze wybierają swoje role w zorganizowanych turach.

### ✨ Kluczowe funkcje

- **🎯 Interaktywny Draft**: Każdy gracz wybiera rolę z 3 losowych opcji w swojej kategorii
- **⏱️ Mechanizm Timeout**: Automatyczny wybór po 20s dla AFK graczy
- **🛡️ Pity Shield System**: Ochrona dla 2 graczy, którzy zginęli wcześnie w poprzedniej grze
- **🔄 Synchronizacja sieciowa**: Pełne wsparcie dla multiplayer z RPC
- **⚙️ Pełna konfiguracja**: BepInEx config dla dostosowania do preferencji

---

## 🚀 Instalacja

1. Upewnij się, że masz zainstalowane:
   - **BepInEx IL2CPP** (wersja 6.0+)
   - **Town of Us Mira**
   - **MiraAPI**

2. Skopiuj `TownOfUsDraft.dll` do folderu:
   ```
   Among Us/BepInEx/plugins/
   ```

3. Uruchom grę - konfiguracja zostanie automatycznie wygenerowana w:
   ```
   Among Us/BepInEx/config/TownOfUsDraft.cfg
   ```

---

## ⚙️ Konfiguracja

Plik konfiguracyjny znajduje się w `BepInEx/config/TownOfUsDraft.cfg`:

```ini
[General]
EnableDraftMode = true          # Włącz/wyłącz Draft Mode
DraftTimeout = 20.0             # Czas (w sekundach) na wybór roli

[Roles]
CrewSupport = 2                 # Liczba ról Crew Support
CrewProtective = 1              # Liczba ról Crew Protective
CrewInvestigative = 2           # Liczba ról Crew Investigative
CrewKilling = 1                 # Liczba ról Crew Killing
CrewPower = 0                   # Liczba ról Crew Power
NeutralKilling = 1              # Liczba ról Neutral Killing
NeutralEvil = 1                 # Liczba ról Neutral Evil
NeutralBenign = 0               # Liczba ról Neutral Benign
RandomNeutral = 0               # Liczba losowych ról Neutral
```

### 📝 Uwagi dotyczące konfiguracji:

- Wartości z konfiguracji działają jako **fallback** jeśli MiraAPI nie wykryje ustawień TOU
- Liczba Impostorów jest pobierana automatycznie z ustawień lobby
- Jeśli suma ról < liczba graczy, pozostałe sloty to losowe role Crew
- Jeśli suma ról > liczba graczy, system automatycznie redukuje pulę (zachowując Impostorów)

---

## 🎮 Jak działa Draft?

### 1️⃣ **Start gry**
Po rozpoczęciu gry (intro cutscene), Draft Mode automatycznie się uruchamia:
- Gra zostaje zatrzymana (freeze)
- Ekran przyciemnia się
- Pojawia się interfejs draftu

### 2️⃣ **Kolejność**
- Host losuje kolejność graczy (seed = Game ID, więc jest deterministyczna)
- Każdy gracz dostaje przypisaną kategorię na podstawie ustawień
- Kolejność jest wyświetlana na ekranie

### 3️⃣ **Wybór roli**
Gdy nadejdzie Twoja tura:
- Zobaczysz **3 losowe role** ze swojej kategorii
- Masz **20 sekund** na wybór (timer widoczny u góry)
- Możesz kliknąć jedną z opcji lub użyć przycisku "LOSUJ"
- Jeśli nie wybierzesz w czasie, system automatycznie wybierze za Ciebie

### 4️⃣ **Finalizacja**
Po zakończeniu wszystkich tur:
- Draft się kończy
- Gra wznawia się automatycznie
- Role są przypisywane zgodnie z wyborami

---

## 🛡️ Pity Shield System

### Jak działa?

**W poprzedniej grze:**
- System śledzi, którzy gracze zginęli **przed pierwszym meetingiem**
- Top 2 pechowców są zapisywani

**W następnej grze:**
- Ci gracze otrzymują **niewidzialną tarczę**
- **Pierwsze zabójstwo** na nich jest **blokowane**
- Killer dostaje normalny cooldown (bez kary)
- Tarcza znika po pierwszym meetingu

### Przykład:
```
Gra 1: Alice i Bob giną jako pierwsi przed spotkaniem
Gra 2: Alice i Bob mają tarczę - pierwsze zabójstwo na nich nie zadziała
```

---

## 🔧 Architektura techniczna

### Główne komponenty:

```
DraftPlugin.cs          → Entry point (BepInEx)
DraftManager.cs         → Logika draftu + RPC
DraftHud.cs             → UI (OnGUI) + Timer
RoleCategorizer.cs      → Mapowanie ról na kategorie
TouConfigAdapter.cs     → System konfiguracji

Patches/
├── ForceDraftPatch.cs         → Hook do intro cutscene
├── BlockTouGenerationPatch.cs → Blokowanie generatora TOU
├── RoleGenerationPatch.cs     → Blokowanie vanilla generator
├── DraftNetworkPatch.cs       → Obsługa RPC
├── DeathTracker.cs            → Pity Shield system
└── HudPatch.cs                → Inicjalizacja DraftHud
```

### Patche Harmony:

| Patch | Priority | Funkcja |
|-------|----------|---------|
| `BlockTouGenerationPatch` | Default | Blokuje generator ról TOU podczas draftu |
| `RoleGenerationPatch` | First | Blokuje vanilla `SelectRoles` |
| `ForceDraftPatch` | Default | Uruchamia draft po intro |
| `DraftNetworkPatch` | Default | Obsługuje RPC (ID: 249, 251, 252) |
| `DeathTracker` | Default | Śledzi zgony i implementuje Pity Shield |

### RPC Protocol:

```csharp
// RPC ID 249: Wybór roli
Data: [byte playerId, int roleTypeId]

// RPC ID 251: Start tury
Data: [byte activePlayerId, string category, string opt1, string opt2, string opt3]

// RPC ID 252: Synchronizacja timera
Data: [float currentTime]
```

---

## 📄 Licencja

Ten projekt jest hobbystycznym dodatkiem do Among Us i Town of Us.  
**Nie jest powiązany z Innersloth ani oficjalnym zespołem Town of Us.**

---

**Miłej zabawy! 🎉**

