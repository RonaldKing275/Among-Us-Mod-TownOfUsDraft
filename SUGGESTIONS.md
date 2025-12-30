# 💡 Sugestie ulepszeń - Draft Mode v2.0

## 🎨 Ulepszenia UI/UX

### 1. **Unity UI zamiast OnGUI**
**Priorytet: 🔴 Wysoki**

**Problem:** OnGUI jest przestarzałe, wolne i brzydkie.

**Rozwiązanie:**
```csharp
// Stwórz prefab w Unity:
// DraftCanvas
//   ├─ Background (Image - czarny, alpha 0.95)
//   ├─ TitleText (TextMeshProUGUI)
//   ├─ TimerText (TextMeshProUGUI)
//   ├─ RoleButton1 (Button + Image + Text)
//   ├─ RoleButton2 (Button + Image + Text)
//   ├─ RoleButton3 (Button + Image + Text)
//   └─ RandomButton (Button + Text)

// W DraftHud.cs:
private GameObject _canvasPrefab;
private Canvas _activeCanvas;

void ShowDraftUI() {
    _activeCanvas = Instantiate(_canvasPrefab).GetComponent<Canvas>();
    // ... setup buttons
}
```

**Zalety:**
- Lepsze performance (batching)
- Responsywne skalowanie
- Animacje (DOTween)
- Ładniejsze czcionki

---

### 2. **Animacje przejść**
**Priorytet: 🟡 Średni**

```csharp
// Fade in przy starcie tury
IEnumerator FadeInEffect() {
    CanvasGroup group = _canvas.GetComponent<CanvasGroup>();
    group.alpha = 0f;
    float elapsed = 0f;
    
    while (elapsed < 0.5f) {
        elapsed += Time.deltaTime;
        group.alpha = Mathf.Lerp(0f, 1f, elapsed / 0.5f);
        yield return null;
    }
}

// Pulsujący timer gdy < 5s
void PulseTimerWarning() {
    if (timeLeft < 5f) {
        float scale = 1f + Mathf.Sin(Time.time * 10f) * 0.2f;
        timerText.transform.localScale = Vector3.one * scale;
        timerText.color = Color.Lerp(Color.white, Color.red, Mathf.PingPong(Time.time * 2f, 1f));
    }
}
```

---

### 3. **Ikony ról**
**Priorytet: 🟡 Średni**

```csharp
// Dodaj sprite'y dla każdej roli
Dictionary<string, Sprite> _roleIcons = new Dictionary<string, Sprite>();

void LoadRoleIcons() {
    // Opcja 1: Z assetów TOU
    var touSprites = Resources.FindObjectsOfTypeAll<Sprite>()
        .Where(s => s.name.Contains("Role"));
    
    // Opcja 2: Z własnego AssetBundle
    var bundle = AssetBundle.LoadFromFile("draftmode_assets");
    var icons = bundle.LoadAllAssets<Sprite>();
}

void ShowRoleOption(string roleName, Button btn) {
    btn.GetComponent<Image>().sprite = _roleIcons[roleName];
    btn.GetComponentInChildren<Text>().text = roleName;
}
```

---

## 🎮 Gameplay Features

### 4. **Ban Phase**
**Priorytet: 🔴 Wysoki**

```csharp
// Przed draftem - każdy gracz banuje 1 rolę
public static HashSet<string> BannedRoles = new HashSet<string>();

IEnumerator BanPhaseRoutine() {
    foreach (var player in players) {
        // Pokaż wszystkie role
        ShowAllRolesForBan();
        
        // Czekaj na wybór
        while (!playerHasBanned) {
            yield return null;
        }
        
        BannedRoles.Add(selectedRole);
        BroadcastBanRpc(selectedRole);
    }
    
    // Po ban phase -> draft
    StartDraft();
}

// W GenerateUniqueOptions:
var available = categoryRoles
    .Where(r => !_globalUsedRoles.Contains(r) && !BannedRoles.Contains(r))
    .ToList();
```

**UI:**
- Ekran z wszystkimi dostępnymi rolami
- Kliknięcie = ban
- Zbanowane role są przekreślone dla wszystkich

---

### 5. **Role Trading**
**Priorytet: 🟢 Niski**

```csharp
// Po drafcie, przed finalizacją - 30s na wymianę
public class TradeOffer {
    public byte FromPlayer;
    public byte ToPlayer;
    public RoleTypes OfferedRole;
    public RoleTypes RequestedRole;
    public bool Accepted;
}

void SendTradeOffer(byte targetId) {
    var offer = new TradeOffer {
        FromPlayer = PlayerControl.LocalPlayer.PlayerId,
        ToPlayer = targetId,
        OfferedRole = PendingRoles[PlayerControl.LocalPlayer.PlayerId],
        RequestedRole = PendingRoles[targetId]
    };
    
    SendTradeOfferRpc(offer);
}

void AcceptTrade(TradeOffer offer) {
    // Swap ról
    var temp = PendingRoles[offer.FromPlayer];
    PendingRoles[offer.FromPlayer] = PendingRoles[offer.ToPlayer];
    PendingRoles[offer.ToPlayer] = temp;
}
```

**UI:**
- Lista graczy z ich kategoriami (nie dokładne role!)
- Przycisk "Propose Trade"
- Popup: "Player X wants to trade [Category] for your [Category]. Accept?"

---

### 6. **Draft Stats & History**
**Priorytet: 🟡 Średni**

```csharp
[Serializable]
public class DraftHistory {
    public List<DraftSession> Sessions = new List<DraftSession>();
}

[Serializable]
public class DraftSession {
    public DateTime Date;
    public Dictionary<string, int> RolePickCount; // Ile razy każda rola została wybrana
    public Dictionary<string, float> RoleWinRate; // Winrate każdej roli
    public int TotalGames;
}

void SaveDraftSession() {
    var session = new DraftSession {
        Date = DateTime.Now,
        RolePickCount = new Dictionary<string, int>()
    };
    
    foreach (var kvp in PendingRoles) {
        string roleName = kvp.Value.ToString();
        if (!session.RolePickCount.ContainsKey(roleName))
            session.RolePickCount[roleName] = 0;
        session.RolePickCount[roleName]++;
    }
    
    string json = JsonUtility.ToJson(_history);
    File.WriteAllText("BepInEx/config/DraftHistory.json", json);
}

// UI: Ekran statystyk po grze
void ShowStatsScreen() {
    // Top 5 najpopularniejszych ról
    // Twoja winrate z każdą rolą
    // Średni czas wyboru
}
```

---

## 🔧 Technical Improvements

### 7. **Custom Options Integration**
**Priorytet: 🔴 Wysoki**

```csharp
// Zamiast hardcodowanego configa, integracja z MiraAPI CustomOptions

public class DraftOptions {
    public static CustomToggleOption EnableDraft;
    public static CustomNumberOption DraftTimeout;
    public static CustomNumberOption CrewInvestigative;
    // ... itd

    public static void RegisterOptions() {
        EnableDraft = CustomOption.AddToggle(
            "Enable Draft Mode",
            true,
            new CustomOptionData {
                Category = "Draft Mode",
                Color = Color.cyan
            }
        );
        
        DraftTimeout = CustomOption.AddNumber(
            "Draft Timeout",
            20f,
            10f,
            60f,
            5f,
            new CustomOptionData {
                Category = "Draft Mode",
                Format = "{0}s"
            }
        );
        
        // ... wszystkie opcje ról
    }
}

// W DraftPlugin.Load():
DraftOptions.RegisterOptions();
```

**Zaleta:** Ustawienia widoczne w lobby przed grą!

---

### 8. **Rollback System**
**Priorytet: 🟢 Niski**

```csharp
// Cofnięcie wyboru (tylko host, tylko ostatnia tura)
public static Stack<RoleAssignment> DraftHistory = new Stack<RoleAssignment>();

public class RoleAssignment {
    public byte PlayerId;
    public RoleTypes Role;
    public List<string> Options;
    public float Timestamp;
}

void UndoLastPick() {
    if (!AmongUsClient.Instance.AmHost) return;
    if (DraftHistory.Count == 0) return;
    
    var last = DraftHistory.Pop();
    
    // Przywróć gracza do kolejki
    TurnQueue.Enqueue(last.PlayerId);
    
    // Usuń rolę z używanych
    _globalUsedRoles.Remove(last.Role.ToString());
    
    // Cofnij czas
    ProcessNextTurn();
}
```

**UI:** Przycisk "Undo" widoczny tylko dla hosta (mały, w rogu)

---

### 9. **Spectator Mode podczas draftu**
**Priorytet: 🟡 Średni**

```csharp
// Gracze, którzy czekają, widzą stream wyborów (bez spoilerów)

void ShowSpectatorView() {
    // Zamiast "Waiting for Player X..."
    // Pokaż:
    // - Kategorie już rozdane (bez szczegółów)
    // - Progres: "5/10 picks completed"
    // - Minimap z kolorami kategorii
    
    DrawCategoryMap();
}

void DrawCategoryMap() {
    // Każdy gracz = kropka na mapie
    // Kolor kropki = kolor kategorii (nie konkretna rola!)
    // Czerwony = Impostor category
    // Niebieski = Crew category
    // Zielony = Neutral category
}
```

---

### 10. **Voice Lines & Sound Effects**
**Priorytet: 🟢 Niski**

```csharp
Dictionary<string, AudioClip> _draftSounds = new Dictionary<string, AudioClip>();

void LoadSounds() {
    _draftSounds["draft_start"] = LoadAudioClip("draft_start.ogg");
    _draftSounds["your_turn"] = LoadAudioClip("your_turn.ogg");
    _draftSounds["role_selected"] = LoadAudioClip("role_selected.ogg");
    _draftSounds["timeout_warning"] = LoadAudioClip("timeout_warning.ogg"); // Gdy < 5s
    _draftSounds["draft_complete"] = LoadAudioClip("draft_complete.ogg");
}

void OnTurnStarted(...) {
    if (isMyTurn) {
        PlaySound("your_turn");
    }
}

void PlaySound(string key) {
    if (_draftSounds.ContainsKey(key)) {
        var source = Camera.main.GetComponent<AudioSource>();
        source.PlayOneShot(_draftSounds[key]);
    }
}
```

**Dźwięki:**
- Gong przy starcie draftu
- "Beep" gdy twoja tura
- "Click" przy wyborze roli
- "Tick tock" gdy zostało < 5s
- Fanfara po zakończeniu

---

## 🌐 Network & Performance

### 11. **Delta Compression dla RPC**
**Priorytet: 🟡 Średni**

```csharp
// Zamiast wysyłać pełne dane co sekundę:

// PRZED:
SendTimerSyncRpc(TurnWatchdogTimer); // 4 bajty

// PO:
byte delta = (byte)((TurnWatchdogTimer - _lastSentTime) * 10f); // 1 bajt
SendTimerDeltaRpc(delta);

// Na kliencie:
TurnWatchdogTimer += (delta / 10f);
```

**Zaleta:** Mniejsze zużycie bandwidth (ważne dla hostów z wolnym internetem)

---

### 12. **Object Pooling dla UI**
**Priorytet: 🟢 Niski**

```csharp
// Zamiast tworzyć nowe GUIStyle co klatkę:

public class UIPool {
    private static Queue<Button> _buttonPool = new Queue<Button>();
    
    public static Button GetButton() {
        if (_buttonPool.Count > 0) {
            var btn = _buttonPool.Dequeue();
            btn.gameObject.SetActive(true);
            return btn;
        }
        return Instantiate(_buttonPrefab);
    }
    
    public static void ReturnButton(Button btn) {
        btn.gameObject.SetActive(false);
        _buttonPool.Enqueue(btn);
    }
}
```

---

### 13. **Reconnect Handling**
**Priorytet: 🔴 Wysoki**

```csharp
// Obecnie: Jeśli gracz reconnectuje podczas draftu, jest pomijany

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
public static void OnPlayerReconnect(PlayerControl player) {
    if (!DraftHud.IsDraftActive) return;
    
    // Wyślij mu aktualny stan draftu
    SendDraftStateRpc(player.PlayerId, new DraftState {
        CurrentTurnPlayer = DraftHud.ActiveTurnPlayerId,
        CompletedPicks = PendingRoles.Keys.ToList(),
        RemainingQueue = TurnQueue.ToList()
    });
}

void RestoreDraftState(DraftState state) {
    DraftHud.ActiveTurnPlayerId = state.CurrentTurnPlayer;
    // ... restore
}
```

---

## 🎯 Balance & Meta

### 14. **Role Weighting System**
**Priorytet: 🟡 Średni**

```csharp
// Niektóre role są OP - daj im mniejszą szansę na pojawienie się

Dictionary<string, float> _roleWeights = new Dictionary<string, float> {
    { "SheriffRole", 1.0f },      // Normalna szansa
    { "MayorRole", 0.5f },        // Połowa szansy
    { "JuggernautRole", 0.3f },   // Bardzo rzadka
};

List<string> GenerateWeightedOptions(RoleCategory category, System.Random rng) {
    var categoryRoles = GetRolesInCategory(category);
    
    // Losuj 3 z wagami
    List<string> selected = new List<string>();
    for (int i = 0; i < 3; i++) {
        float totalWeight = categoryRoles.Sum(r => _roleWeights[r]);
        float roll = (float)rng.NextDouble() * totalWeight;
        
        float cumulative = 0f;
        foreach (var role in categoryRoles) {
            cumulative += _roleWeights[role];
            if (roll < cumulative) {
                selected.Add(role);
                categoryRoles.Remove(role); // Nie powtarzaj
                break;
            }
        }
    }
    
    return selected;
}
```

---

### 15. **Dynamic Role Pool**
**Priorytet: 🟡 Średni**

```csharp
// Zamiast sztywnej puli, dostosuj do liczby graczy

List<RoleCategory> BuildDynamicPool(int playerCount) {
    if (playerCount <= 5) {
        // Małe lobby: Więcej crew, mniej neutrali
        return SmallLobbyPool();
    } else if (playerCount <= 10) {
        // Średnie lobby: Standard
        return StandardPool();
    } else {
        // Duże lobby: Więcej chaos (więcej neutrali/killerów)
        return LargeLobbyPool();
    }
}
```

---

## 🧪 Testing & Debug

### 16. **Draft Replay System**
**Priorytet: 🟢 Niski**

```csharp
// Zapisuj każdy draft do pliku, żeby debug'ować problemy

[Serializable]
public class DraftReplay {
    public List<DraftEvent> Events = new List<DraftEvent>();
}

[Serializable]
public class DraftEvent {
    public float Timestamp;
    public string Type; // "TurnStart", "RoleSelected", "Timeout"
    public Dictionary<string, object> Data;
}

void RecordEvent(string type, Dictionary<string, object> data) {
    _currentReplay.Events.Add(new DraftEvent {
        Timestamp = Time.time,
        Type = type,
        Data = data
    });
}

void SaveReplay() {
    string json = JsonUtility.ToJson(_currentReplay);
    File.WriteAllText($"BepInEx/logs/draft_replay_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json", json);
}

// Można potem odtworzyć draft w trybie "playback"
```

---

### 17. **Console Commands**
**Priorytet: 🟢 Niski**

```csharp
// Komendy do testowania (tylko host)

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
public static bool Prefix(ChatController __instance) {
    string text = __instance.TextArea.text;
    
    if (text.StartsWith("/draft")) {
        string[] args = text.Split(' ');
        
        switch (args[1]) {
            case "skip":
                DraftManager.ForceSkipTurn();
                return false;
            
            case "undo":
                DraftManager.UndoLastPick();
                return false;
            
            case "restart":
                DraftManager.StartDraft();
                return false;
            
            case "timer":
                if (args.Length > 2) {
                    DraftHud.TurnWatchdogTimer = float.Parse(args[2]);
                }
                return false;
        }
    }
    
    return true; // Normal chat
}
```

**Komendy:**
```
/draft skip      - Skipuje aktualną turę
/draft undo      - Cofa ostatni wybór
/draft restart   - Restartuje draft
/draft timer 15  - Ustawia timer na 15s
```

---

## 📱 Platform Support

### 18. **Mobile Touch Controls**
**Priorytet: 🟡 Średni**

```csharp
void Update() {
    #if UNITY_ANDROID || UNITY_IOS
    // Touch handling
    if (Input.touchCount > 0) {
        Touch touch = Input.GetTouch(0);
        
        if (touch.phase == TouchPhase.Ended) {
            Ray ray = Camera.main.ScreenPointToRay(touch.position);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit)) {
                Button btn = hit.collider.GetComponent<Button>();
                if (btn != null) {
                    btn.onClick.Invoke();
                }
            }
        }
    }
    #endif
}
```

**UI Adjustments:**
- Większe przyciski (min. 100x100px)
- Większe odstępy między przyciskami
- Scroll view zamiast fixed layout

---

## 🌍 Localization

### 19. **Multi-Language Support**
**Priorytet: 🟢 Niski**

```csharp
public enum Language { English, Polish, Spanish, French, German }

Dictionary<Language, Dictionary<string, string>> _translations = new Dictionary<Language, Dictionary<string, string>> {
    { Language.English, new Dictionary<string, string> {
        { "your_turn", "YOUR TURN" },
        { "waiting", "Waiting for player" },
        { "finalizing", "FINALIZING DRAFT..." }
    }},
    { Language.Polish, new Dictionary<string, string> {
        { "your_turn", "TWOJA TURA" },
        { "waiting", "Czekam na gracza" },
        { "finalizing", "FINALIZACJA DRAFTU..." }
    }}
    // ... more languages
};

string Translate(string key) {
    Language current = GetCurrentLanguage();
    return _translations[current][key];
}
```

---

## 🎉 Fun Extras

### 20. **Role Roulette Mode**
**Priorytet: 🟢 Niski**

```csharp
// Opcjonalny tryb: Roles są na "kole fortuny"

IEnumerator SpinRoulette(List<string> options) {
    float spinDuration = 3f;
    float spinSpeed = 20f;
    
    int index = 0;
    float elapsed = 0f;
    
    while (elapsed < spinDuration) {
        elapsed += Time.deltaTime;
        
        // Zwalniaj stopniowo
        float t = elapsed / spinDuration;
        float currentSpeed = Mathf.Lerp(spinSpeed, 0f, t);
        
        index = (index + 1) % options.Count;
        HighlightRole(options[index]);
        
        yield return new WaitForSeconds(1f / currentSpeed);
    }
    
    // Wybierz ostatnio podświetloną rolę
    OnPlayerSelectedRole(options[index]);
}
```

---

## Priorytetyzacja

### Must-Have (v1.1):
1. ✅ Custom Options Integration (#7)
2. ✅ Reconnect Handling (#13)
3. ✅ Unity UI Upgrade (#1)

### Should-Have (v1.5):
4. ⭕ Ban Phase (#4)
5. ⭕ Draft Stats (#6)
6. ⭕ Role Weighting (#14)

### Nice-to-Have (v2.0):
7. ⬜ Role Trading (#5)
8. ⬜ Voice Lines (#10)
9. ⬜ Mobile Support (#18)
10. ⬜ Localization (#19)

---

**Powodzenia w rozwoju! 🚀**

