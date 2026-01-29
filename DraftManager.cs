using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AmongUs.GameOptions;
using MiraAPI.Roles; 
using MiraAPI.GameOptions; 
using MiraAPI.GameOptions.OptionTypes; 
using BepInEx.Unity.IL2CPP; 
using BepInEx.Unity.IL2CPP.Utils.Collections; 
using System.Reflection;
using System.Collections; 
using Hazel; 

namespace TownOfUsDraft
{
    public static class DraftManager
    {
        public static Queue<byte> TurnQueue = new Queue<byte>();
        public static Dictionary<byte, RoleCategory> HostDraftAssignments = new Dictionary<byte, RoleCategory>();
        private static HashSet<string> _globalUsedRoles = new HashSet<string>();
        public static Dictionary<byte, RoleBehaviour> PendingRoles = new Dictionary<byte, RoleBehaviour>();

        private static readonly HashSet<string> VanillaBannedRoles = new HashSet<string>
        {
            "Engineer", "Scientist", "Shapeshifter", "Guardian Angel", "GuardianAngel",
            "Noisemaker", "Tracker", "Phantom", "Impostor", "Crewmate", "Ghost"
        };

        private static readonly HashSet<string> TransformationRoles = new HashSet<string>
        {
            // Role które są transformacjami innych ról - NIE DO DRAFTOWANIA!
            "Pestilence", "PestilenceRole",  // Transformacja Plaguebearer
            "Lovers", "Lover",  // Modifier, nie role
            "Phantom", "PhantomRole",  // Vanilla transformacja
        };
        
        // Cache dla mapy ról
        private static bool _roleCountCacheInitialized = false;
        private static Dictionary<string, int> _roleCountCache = new Dictionary<string, int>();

        // Zmienne stanu Draftu
        public static bool IsDraftRunning = false;
        private static List<byte> _draftOrder = new List<byte>();
        private static Dictionary<byte, List<string>> _draftOptions = new Dictionary<byte, List<string>>();

        public static void ResetState()
        {
            DraftPlugin.Instance.Log.LogError("[DraftManager] ResetState() - Czyszczenie stanu Draftu...");
            
            TurnQueue.Clear();
            HostDraftAssignments.Clear();
            _globalUsedRoles.Clear();
            PendingRoles.Clear();
            
            _rolesApplied = false;
            IsDraftRunning = false;
            _draftOrder.Clear();
            _draftOptions.Clear();
            
            DraftPlugin.Instance.Log.LogError("[DraftManager] Stan wyczyszczony.");
        }

        public static void StartDraft()
        {

            // Logi debugowania zostały usunięte - było zbyt dużo spamu

            PendingRoles.Clear();
            _globalUsedRoles.Clear();
            TurnQueue.Clear();
            HostDraftAssignments.Clear();
            _rolesApplied = false; // Reset flagi aplikacji ról

            if (!AmongUsClient.Instance.AmHost) return;

            int seed = AmongUsClient.Instance.GameId; 
            System.Random rng = new System.Random(seed);
            
            var players = PlayerControl.AllPlayerControls.ToArray()
                .Where(p => p != null && !p.Data.Disconnected)
                .OrderBy(p => rng.Next())
                .ToList();

            DraftPlugin.Instance.Log.LogError($"╔═══════════════════════════════════════════════════════╗");
            DraftPlugin.Instance.Log.LogError($"║  StartDraft() - Znaleziono {players.Count} graczy                 ║");
            DraftPlugin.Instance.Log.LogError($"╚═══════════════════════════════════════════════════════╝");
            
            bool isHost = AmongUsClient.Instance.AmHost;
            byte localPlayerId = PlayerControl.LocalPlayer?.PlayerId ?? 255;
            DraftPlugin.Instance.Log.LogError($"IsHost: {isHost}, LocalPlayerID: {localPlayerId}");
            
            for (int i = 0; i < players.Count; i++)
            {
                bool isLocalPlayer = players[i].PlayerId == localPlayerId;
                string marker = isLocalPlayer ? " ← LOCAL/HOST" : "";
                DraftPlugin.Instance.Log.LogError($"  [{i}] {players[i].Data.PlayerName} (ID:{players[i].PlayerId}){marker}");
            }

            // NOWE: Pobierz kategorie ze Slot1-15 zamiast BuildDraftPool()
            List<RoleCategory> draftPool = GetSlotCategoriesFromConfig(players.Count);
            
            // Jeśli nie udało się odczytać z configu, użyj fallback
            if (draftPool == null || draftPool.Count == 0)
            {
                DraftPlugin.Instance.Log.LogWarning("[StartDraft] Nie udało się odczytać Slotów z configu! Używam fallback.");
                draftPool = BuildDraftPool(players.Count, rng);
            }

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                RoleCategory cat = (i < draftPool.Count) ? draftPool[i] : RoleCategory.CrewSupport;
                HostDraftAssignments[p.PlayerId] = cat;
                TurnQueue.Enqueue(p.PlayerId);
                
                DraftPlugin.Instance.Log.LogError($"  [QUEUE] {p.Data.PlayerName} (ID:{p.PlayerId}) → {cat}");
            }

            DraftPlugin.Instance.Log.LogError($"TurnQueue.Count = {TurnQueue.Count}, PendingRoles.Count = {PendingRoles.Count}");
            ProcessNextTurn(rng);
        }

        public static void ProcessNextTurn(System.Random rng = null)
        {
            if (rng == null) rng = new System.Random();

            DraftPlugin.Instance.Log.LogError($"[ProcessNextTurn] TurnQueue.Count = {TurnQueue.Count}");

            while (TurnQueue.Count > 0)
            {
                byte nextPlayerId = TurnQueue.Peek();
                var player = GetPlayerById(nextPlayerId);
                
                DraftPlugin.Instance.Log.LogError($"[ProcessNextTurn] Sprawdzam gracza ID:{nextPlayerId}...");

                if (player == null || player.Data == null || player.Data.Disconnected)
                {
                    DraftPlugin.Instance.Log.LogWarning($"[Draft] ✗ Gracz {nextPlayerId} rozłączony lub null, pomijam i auto-przypisuję Crewmate.");
                    TurnQueue.Dequeue(); 
                    
                    // Auto-przypisanie domyślnej roli dla rozłączonego gracza
                    if (!PendingRoles.ContainsKey(nextPlayerId))
                    {
                        // Znajdź CrewmateRole
                        foreach (var r in RoleManager.Instance.AllRoles) 
                        {
                            var uObj = r as UnityEngine.Object;
                            if (uObj != null && uObj.name == "CrewmateRole") 
                            { 
                                PendingRoles[nextPlayerId] = r as RoleBehaviour; 
                                DraftPlugin.Instance.Log.LogWarning($"[Draft]   → Auto-przypisano CrewmateRole dla ID:{nextPlayerId}");
                                break; 
                            }
                        }
                    }
                    continue; 
                }
                
                DraftPlugin.Instance.Log.LogError($"[ProcessNextTurn] ✓ Gracz {player.Data.PlayerName} (ID:{nextPlayerId}) jest OK!");

                TurnQueue.Dequeue(); 
                RoleCategory cat = HostDraftAssignments.ContainsKey(nextPlayerId) ? HostDraftAssignments[nextPlayerId] : RoleCategory.CrewSupport;
                
                DraftPlugin.Instance.Log.LogError($"[ProcessNextTurn] Kategoria dla ID:{nextPlayerId}: {cat}");
                
                List<string> options = GenerateUniqueOptions(cat, rng);
                
                DraftPlugin.Instance.Log.LogError($"[ProcessNextTurn] Wygenerowano {options.Count} opcji dla ID:{nextPlayerId}");
                
                // NAPRAWIONE: Dodajemy do _globalUsedRoles DOPIERO gdy gracz wybierze
                // (przeniesione do OnPlayerSelectedRole)
                
                while (options.Count < 3) options.Add("NO_OPTION");

                DraftHud.TurnWatchdogTimer = 0f; 
                DraftHud.CurrentTurnPlayerId = nextPlayerId;
                DraftHud.CurrentTurnOptions = options; 

                DraftPlugin.Instance.Log.LogError($"[ProcessNextTurn] → Wywołuję OnTurnStarted() i SendStartTurnRpc() dla ID:{nextPlayerId}");

                OnTurnStarted(nextPlayerId, FormatCategoryName(cat), options);
                SendStartTurnRpc(nextPlayerId, FormatCategoryName(cat), options);
                
                DraftPlugin.Instance.Log.LogError($"[ProcessNextTurn] → RETURN! (czekam na wybór gracza ID:{nextPlayerId})");
                return;
            }

            DraftPlugin.Instance.Log.LogError($"[ProcessNextTurn] TurnQueue.Count = 0! Wszystkie tury zakończone.");
            DraftPlugin.Instance.Log.LogError($"[ProcessNextTurn] → Wywołuję OnTurnStarted(255) i SendStartTurnRpc(255) - FINALIZACJA!");
            
            OnTurnStarted(255, "", new List<string>()); 
            SendStartTurnRpc(255, "", new List<string>{"","",""});
        }

        public static void ForceSkipTurn()
        {
            byte pid = DraftHud.CurrentTurnPlayerId;
            DraftPlugin.Instance.Log.LogWarning($"[Draft Watchdog] Timeout dla gracza {pid}. Auto-pick.");

            string autoRole = "CrewmateRole"; // Fallback role
            if (DraftHud.CurrentTurnOptions != null && DraftHud.CurrentTurnOptions.Count > 0)
            {
                // Filtrujemy Crewmate i NO_OPTION
                var valid = DraftHud.CurrentTurnOptions.Where(r => r != "Crewmate" && r != "NO_OPTION").ToList();
                
                if (valid.Count > 0) 
                {
                    // Wybierz pierwszą valid rolę
                    autoRole = valid[0];
                }
                else 
                {
                    // Jeśli nie ma valid ról, weź cokolwiek co nie jest NO_OPTION
                    var any = DraftHud.CurrentTurnOptions.FirstOrDefault(r => r != "NO_OPTION");
                    if (any != null) autoRole = any;
                }
            }
            OnPlayerSelectedRole(autoRole, pid);
        }

        public static void OnTurnStarted(byte activePlayerId, string catTitle, List<string> options)
        {
            DraftPlugin.Instance.Log.LogError($"[OnTurnStarted] activePlayerId={activePlayerId}, catTitle={catTitle}, options.Count={options.Count}");
            
            if (activePlayerId == 255)
            {
                DraftPlugin.Instance.Log.LogError($"[OnTurnStarted] activePlayerId=255 → FINALIZACJA DRAFTU!");
                DraftHud.IsDraftActive = false;
                if (DraftHud.Instance != null)
                {
                    DraftPlugin.Instance.Log.LogError($"[OnTurnStarted] → Wywołuję FinalizeDraftRoutine()...");
                    DraftHud.Instance.StartCoroutine(FinalizeDraftRoutine().WrapToIl2Cpp());
                }
                else
                {
                    DraftPlugin.Instance.Log.LogError($"[OnTurnStarted] ✗ DraftHud.Instance jest NULL!");
                }
                return;
            }

            DraftPlugin.Instance.Log.LogError($"[OnTurnStarted] → Ustawiam turę dla gracza ID:{activePlayerId}");
            DraftHud.ActiveTurnPlayerId = activePlayerId;
            DraftHud.CategoryTitle = catTitle;
            DraftHud.MyOptions = options;
            DraftHud.IsDraftActive = true;
            
            // Reset timera dla wszystkich klientów (synchronizacja)
            DraftHud.TurnWatchdogTimer = 0f;
        }

        private static IEnumerator FinalizeDraftRoutine()
        {
            DraftPlugin.Instance.Log.LogError("╔═══════════════════════════════════════════════════════╗");
            DraftPlugin.Instance.Log.LogError("║           FINALIZACJA DRAFTU - ROZPOCZĘCIE           ║");
            DraftPlugin.Instance.Log.LogError("╚═══════════════════════════════════════════════════════╝");
            
            try
            {
                DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] Time.timeScale PRZED: {Time.timeScale}");
            Time.timeScale = 1f;
                DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] Time.timeScale PO: {Time.timeScale}");
                
                if (PlayerControl.LocalPlayer != null) 
                {
                    PlayerControl.LocalPlayer.moveable = true;
                    DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] LocalPlayer.moveable = true");
                }
                else
                {
                    DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] ✗ LocalPlayer jest NULL!");
                }
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] ✗ EXCEPTION podczas ustawiania Time.timeScale: {ex.Message}");
            }

            // Czekanie na synchronizację - Unity potrzebuje czasu na przygotowanie
            DraftPlugin.Instance.Log.LogError("[FinalizeDraftRoutine] Czekam 1.0s na synchronizację...");
            yield return new WaitForSeconds(1.0f);
            
            DraftPlugin.Instance.Log.LogError("[FinalizeDraftRoutine] ✓ 1.0s minęło! Sprawdzam AmongUsClient...");
            
            try
            {
                if (AmongUsClient.Instance == null)
                {
                    DraftPlugin.Instance.Log.LogError("[FinalizeDraftRoutine] ✗ AmongUsClient.Instance jest NULL!");
                    yield break;
                }
                
                DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] AmHost: {AmongUsClient.Instance.AmHost}");

                // ⚠️ KRYTYCZNE: OnDraftCompleted() TYLKO NA HOŚCIE!
                // Klienty będą otrzymywać role przez RPC od hosta
                if (AmongUsClient.Instance.AmHost)
                {
                    DraftPlugin.Instance.Log.LogError("[FinalizeDraftRoutine] HOST: Wywołuję ForceDraftPatch.OnDraftCompleted()...");
                    
                    try
                    {
                        TownOfUsDraft.Patches.ForceDraftPatch.OnDraftCompleted();
                        DraftPlugin.Instance.Log.LogError("[FinalizeDraftRoutine] HOST: ✓ OnDraftCompleted() zakończone!");
                    }
                    catch (System.Exception ex)
                    {
                        DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] HOST: ✗ BŁĄD w OnDraftCompleted(): {ex.Message}");
                        DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] HOST: StackTrace: {ex.StackTrace}");
                    }
                }
                else
                {
                    DraftPlugin.Instance.Log.LogError("[FinalizeDraftRoutine] KLIENT: Czekam na rozpoczęcie gry od hosta...");
                    DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] KLIENT: Time.timeScale = {Time.timeScale}");
                    DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] KLIENT: GameState = {AmongUsClient.Instance?.GameState}");
                    DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] KLIENT: LocalPlayer.moveable = {PlayerControl.LocalPlayer?.moveable}");
                }
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] ✗ EXCEPTION: {ex.Message}");
                DraftPlugin.Instance.Log.LogError($"[FinalizeDraftRoutine] StackTrace: {ex.StackTrace}");
            }
        }

        public static bool _rolesApplied = false; // Flaga żeby nie aplikować wiele razy (publiczna dla ForceDraftPatch)

        // NAJLEPSZA METODA: Aplikuj role W IntroCutscene.Start (DOKŁADNIE przed ShowRole!)
        public static void ApplyDraftRolesInIntroStart()
        {
            if (_rolesApplied)
            {
                return;
            }

            
            // Aplikuj role NATYCHMIAST
            int successCount = 0;
            foreach (var kvp in PendingRoles)
            {
                var player = GetPlayerById(kvp.Key);
                if (player != null && !player.Data.Disconnected && !player.Data.IsDead)
                {
                    try 
                    { 
                        var roleBehaviour = kvp.Value;
                        var roleName = roleBehaviour.GetType().Name.Replace("Role", "");
                        var roleId = (int)roleBehaviour.Role;
                        
                        
                        // KLUCZOWE: Ustaw rolę przez RpcSetRole
                        // MiraAPI/TOU automatycznie zainicjalizuje komponenty
                        // Ponieważ jesteśmy W Start (przed ShowRole), role będą świeże!
                        player.RpcSetRole(roleBehaviour.Role);
                        
                        successCount++;
                    } 
                    catch (System.Exception e) 
                    { 
                        DraftPlugin.Instance.Log.LogError($"      ✗ BŁĄD dla {player.Data.PlayerName}: {e.Message}"); 
                    }
                }
                else
                {
                    DraftPlugin.Instance.Log.LogWarning($"      ⚠ Gracz {kvp.Key} nie znaleziony lub disconnected/dead");
                }
            }

            
            // KRYTYCZNE: Przywróć flagę TOU żeby mogło obsłużyć ShowRole!
            TownOfUsDraft.Patches.DraftRoleOverridePatch.SetTouReplaceRoleManagerFlag(false);
            
            PendingRoles.Clear();
            _rolesApplied = true;
        }

        // --- Odczyt opcji z GameOptionsManager ---
        private static int GetMiraOption(string keyword)
        {
            try 
            {
                // Użyj GameOptionsManager.Instance - zawiera opcje z TOU-Mira
                if (GameOptionsManager.Instance == null || GameOptionsManager.Instance.CurrentGameOptions == null)
                {
                    DraftPlugin.Instance.Log.LogWarning($"[GetMiraOption] GameOptionsManager nie jest dostępny!");
                    return 0;
                }
                
                var gameOptions = GameOptionsManager.Instance.CurrentGameOptions;
                var optionsType = gameOptions.GetType();
                
                // Przeszukaj WSZYSTKIE pola (publiczne i prywatne)
                var allFields = optionsType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                foreach (var field in allFields)
                {
                    string fieldName = field.Name.ToLower();
                    string keywordLower = keyword.ToLower();
                    
                    // Sprawdź czy nazwa pola pasuje do keyword
                    if (fieldName.Contains(keywordLower) || 
                        fieldName.Replace("_", "").Contains(keywordLower.Replace(" ", "")))
                    {
                        var value = field.GetValue(gameOptions);
                        if (value == null) continue;
                        
                        // Spróbuj skonwertować na int
                        int result = 0;
                        if (value is int intVal) result = intVal;
                        else if (value is byte byteVal) result = (int)byteVal;
                        else if (value is float floatVal) result = (int)floatVal;
                        else if (value is uint uintVal) result = (int)uintVal;
                        else continue;
                        
                        return result;
                    }
                }
                
                DraftPlugin.Instance.Log.LogWarning($"[GetMiraOption] Nie znaleziono opcji dla '{keyword}'");
            } 
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[GetMiraOption] Exception: {ex.Message}\n{ex.StackTrace}");
            }
            return 0;
        }

        // NOWA FUNKCJA: Odczyt Slot1-15 z TOU-Mira RoleOptions
        private static List<RoleCategory> GetSlotCategoriesFromConfig(int playerCount)
        {
            try
            {
                // Znajdź MiraAPI assembly
                var miraApiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "MiraAPI");
                    
                if (miraApiAssembly == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[GetSlotCategories] MiraAPI assembly nie znalezione!");
                    return null;
                }

                // Znajdź TownOfUsMira assembly
                var touAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                
                if (touAssembly == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[GetSlotCategories] TownOfUsMira assembly nie znalezione!");
                    return null;
                }

                // Znajdź OptionGroupSingleton<T>
                var optionGroupSingletonType = miraApiAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "OptionGroupSingleton`1");
                    
                if (optionGroupSingletonType == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[GetSlotCategories] OptionGroupSingleton<> nie znaleziony!");
                    return null;
                }

                // Znajdź RoleOptions
                var roleOptionsType = touAssembly.GetType("TownOfUs.Options.RoleOptions");
                if (roleOptionsType == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[GetSlotCategories] RoleOptions nie znaleziony!");
                    return null;
                }

                // Utwórz OptionGroupSingleton<RoleOptions>
                var concreteType = optionGroupSingletonType.MakeGenericType(roleOptionsType);
                var instanceProp = concreteType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                
                if (instanceProp == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[GetSlotCategories] Instance property nie znalezione!");
                    return null;
                }

                // Pobierz instancję RoleOptions
                var roleOptionsInstance = instanceProp.GetValue(null);
                if (roleOptionsInstance == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[GetSlotCategories] RoleOptions Instance jest null!");
                    return null;
                }


                List<RoleCategory> categories = new List<RoleCategory>();

                DraftPlugin.Instance.Log.LogError($"[GetSlotCategories] Odczytuję sloty dla {playerCount} graczy...");

                // Odczytaj Slot1 do SlotN (N = liczba graczy)
                for (int slotNum = 1; slotNum <= playerCount && slotNum <= 15; slotNum++)
                {
                    string slotName = $"Slot{slotNum}";
                    var slotProp = roleOptionsType.GetProperty(slotName, BindingFlags.Public | BindingFlags.Instance);
                    
                    if (slotProp == null)
                    {
                        DraftPlugin.Instance.Log.LogWarning($"[GetSlotCategories] {slotName} nie znaleziony!");
                        categories.Add(RoleCategory.CommonCrew); // Fallback
                        continue;
                    }

                    var slotObject = slotProp.GetValue(roleOptionsInstance);
                    if (slotObject == null)
                    {
                        DraftPlugin.Instance.Log.LogWarning($"[GetSlotCategories] {slotName} jest null!");
                        categories.Add(RoleCategory.CommonCrew);
                        continue;
                    }

                    // Pobierz .Value (index kategorii 0-24)
                    var valueProp = slotObject.GetType().GetProperty("Value");
                    if (valueProp == null)
                    {
                        DraftPlugin.Instance.Log.LogWarning($"[GetSlotCategories] {slotName}.Value nie znaleziony!");
                        categories.Add(RoleCategory.CommonCrew);
                        continue;
                    }

                    int categoryIndex = (int)valueProp.GetValue(slotObject);
                    RoleCategory category = RoleCategorizer.IndexToCategory(categoryIndex);
                    
                    categories.Add(category);
                    DraftPlugin.Instance.Log.LogError($"  [{slotNum}] {slotName} = {categoryIndex} → {category}");
                }

                DraftPlugin.Instance.Log.LogError($"[GetSlotCategories] ✓ Zwracam {categories.Count} kategorii");
                return categories;
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[GetSlotCategories] Exception: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        // STARA FUNKCJA (fallback) - używana gdy nie udało się odczytać ze Slotów
        private static List<RoleCategory> BuildDraftPool(int playerCount, System.Random rng)
        {
            List<RoleCategory> pool = new List<RoleCategory>();
            
            int imp = (GameOptionsManager.Instance?.CurrentGameOptions?.NumImpostors) ?? 1;
            for(int i=0; i<imp; i++) pool.Add(RoleCategory.RandomImp);

            // Pobieranie wartości z konfiguracji lub z MiraAPI jako fallback
            int nk = GetMiraOption("Neutral Killing") + GetMiraOption("Neutral Killer");
            int ne = GetMiraOption("Neutral Evil");
            int nb = GetMiraOption("Neutral Benign");
            int no = GetMiraOption("Neutral Outlier") + GetMiraOption("Neutral Chaos");
            int rndN = GetMiraOption("Random Neutral");

            // Fallback na wartości z configa jeśli MiraAPI nie zwraca nic
            if (nk == 0) nk = TouConfigAdapter.GetRoleCount("NeutralKilling", 0);
            if (ne == 0) ne = TouConfigAdapter.GetRoleCount("NeutralEvil", 0);
            if (nb == 0) nb = TouConfigAdapter.GetRoleCount("NeutralBenign", 0);
            if (rndN == 0) rndN = TouConfigAdapter.GetRoleCount("RandomNeutral", 0);

            if (nk==0 && ne==0 && nb==0 && playerCount >= 4) {
                 DraftPlugin.Instance.Log.LogWarning("[Config] Nie znaleziono Neutrali. Ustawiam 1 NK (Fallback).");
                 nk = 1; 
            }

            int cInv = GetMiraOption("Investigative");
            int cPro = GetMiraOption("Protective"); 
            int cSup = GetMiraOption("Support");       
            int cPow = GetMiraOption("Power");
            int cKil = GetMiraOption("Killing");

            // Fallback na wartości z configa
            if (cInv == 0) cInv = TouConfigAdapter.GetRoleCount("Investigative", 2);
            if (cPro == 0) cPro = TouConfigAdapter.GetRoleCount("Protective", 1);
            if (cSup == 0) cSup = TouConfigAdapter.GetRoleCount("Support", 2);
            if (cPow == 0) cPow = TouConfigAdapter.GetRoleCount("Power", 0);
            if (cKil == 0) cKil = TouConfigAdapter.GetRoleCount("Killing", 1);


            for(int i=0; i<nk; i++) pool.Add(RoleCategory.NeutralKilling);
            for(int i=0; i<ne; i++) pool.Add(RoleCategory.NeutralEvil);
            for(int i=0; i<nb; i++) pool.Add(RoleCategory.NeutralBenign);
            for(int i=0; i<rndN; i++) pool.Add(GetRandomNeutralCategory(rng));

            for(int i=0; i<cInv; i++) pool.Add(RoleCategory.CrewInvestigative);
            for(int i=0; i<cPro; i++) pool.Add(RoleCategory.CrewProtective);
            for(int i=0; i<cKil; i++) pool.Add(RoleCategory.CrewKilling);
            for(int i=0; i<cSup; i++) pool.Add(RoleCategory.CrewSupport);
            for(int i=0; i<cPow; i++) pool.Add(RoleCategory.CrewPower);

            int remaining = playerCount - pool.Count;
            if (remaining < 0) 
            {
                var impTicket = pool[0]; pool.RemoveAt(0); 
                pool = pool.OrderBy(x => rng.Next()).Take(playerCount - 1).ToList();
                pool.Insert(0, impTicket);
            }
            else 
            {
                for(int i=0; i<remaining; i++) pool.Add(GetWeightedCrewCategory(rng));
            }

            return pool.OrderBy(x => rng.Next()).ToList();
        }

        // --- Helpery ---
        public static void OnPlayerSelectedRole(string roleName, byte forcedPlayerId = 255)
        {
            byte targetId = (forcedPlayerId == 255) ? PlayerControl.LocalPlayer.PlayerId : forcedPlayerId;
            var player = GetPlayerById(targetId);
            if (player != null)
            {
                DraftPlugin.Instance.Log.LogError($"╔═══════════════════════════════════════════════════════╗");
                DraftPlugin.Instance.Log.LogError($"║ [WYBÓR] Gracz {player.Data.PlayerName} (ID:{targetId}) wybrał: {roleName}");
                DraftPlugin.Instance.Log.LogError($"╚═══════════════════════════════════════════════════════╝");
                
                // Dodajemy wybraną rolę do używanych
                if (!_globalUsedRoles.Contains(roleName))
                {
                    _globalUsedRoles.Add(roleName);
                }

                RoleBehaviour roleToAssign = null;
                
                // Znajdź RoleBehaviour po nazwie
                foreach (var r in RoleManager.Instance.AllRoles) 
                {
                    var uObj = r as UnityEngine.Object;
                    if (uObj != null && uObj.name == roleName) 
                    { 
                        roleToAssign = r as RoleBehaviour;
                        DraftPlugin.Instance.Log.LogError($"[WYBÓR] ✓ Znaleziono RoleBehaviour: {uObj.name}");
                        DraftPlugin.Instance.Log.LogError($"[WYBÓR]   ├─ Type: {roleToAssign.GetType().FullName}");
                        DraftPlugin.Instance.Log.LogError($"[WYBÓR]   └─ RoleTypes ID: {(int)roleToAssign.Role} = {roleToAssign.Role}");
                        break; 
                    }
                }
                
                // Fallback jeśli nie znaleziono
                if (roleToAssign == null) 
                {
                    DraftPlugin.Instance.Log.LogWarning($"[OnPlayerSelectedRole] Nie znaleziono roli '{roleName}', przypisuję Crewmate");
                    foreach (var r in RoleManager.Instance.AllRoles) 
                    {
                        var uObj = r as UnityEngine.Object;
                        if (uObj != null && uObj.name == "CrewmateRole") 
                        { 
                            roleToAssign = r as RoleBehaviour; 
                            break; 
                        }
                     }
                }

                if (AmongUsClient.Instance.AmHost || targetId == PlayerControl.LocalPlayer.PlayerId)
                    SendRoleSelectedRpc(targetId, roleName);

                // USUNIĘTO: if (targetId == PlayerControl.LocalPlayer.PlayerId) DraftHud.ActiveTurnPlayerId = 255;
                // To powodowało natychmiastowe pokazanie "FINALIZACJA DRAFTU" w GUI zamiast czekać na następną turę!
                // ActiveTurnPlayerId jest teraz ustawiany TYLKO w OnTurnStarted()
                
                if (!PendingRoles.ContainsKey(targetId)) PendingRoles.Add(targetId, roleToAssign);
                else PendingRoles[targetId] = roleToAssign;
                
                if (roleToAssign != null)
                {
                    var finalRoleName = (roleToAssign as UnityEngine.Object)?.name ?? "Unknown";
                    DraftPlugin.Instance.Log.LogError($"[WYBÓR] → Zapisuję do PendingRoles[{targetId}]: {finalRoleName}");
                    DraftPlugin.Instance.Log.LogError($"[WYBÓR] → PendingRoles.Count = {PendingRoles.Count}");
                }
                
                // KRYTYCZNE: Host musi wywołać ProcessNextTurn() żeby przejść do kolejnej tury lub zakończyć draft!
                if (AmongUsClient.Instance.AmHost) {
                    // USUNIĘTO: DraftHud.HostTimerActive = true; 
                    // To powodowało automatyczne wywołanie ProcessNextTurn() po 0.5s w DraftHud.Update()!
                    DraftHud.TurnWatchdogTimer = 0f;
                    
                    DraftPlugin.Instance.Log.LogError($"[WYBÓR] → HOST wywołuje ProcessNextTurn()...");
                    ProcessNextTurn();
                }
            }
        }
        
        public static void ApplyRoleFromRpc(PlayerControl player, string roleName) 
        {
            DraftPlugin.Instance.Log.LogError($"[ApplyRoleFromRpc] Gracz {player.Data.PlayerName} (ID:{player.PlayerId}) wybrał: {roleName}");
            
            RoleBehaviour roleToAssign = null;
            
            // Znajdź RoleBehaviour po nazwie
            foreach (var r in RoleManager.Instance.AllRoles) 
            {
                var uObj = r as UnityEngine.Object;
                if (uObj != null && uObj.name == roleName) 
                { 
                    roleToAssign = r as RoleBehaviour;
                    break; 
                }
            }
            
            if (roleToAssign != null)
            {
                if (!PendingRoles.ContainsKey(player.PlayerId)) PendingRoles.Add(player.PlayerId, roleToAssign);
                else PendingRoles[player.PlayerId] = roleToAssign;
                
                DraftPlugin.Instance.Log.LogError($"[ApplyRoleFromRpc] → Dodano do PendingRoles[{player.PlayerId}], Count={PendingRoles.Count}");
                
                // KRYTYCZNE: Host musi wywołać ProcessNextTurn() po odebraniu wyboru od klienta!
                if (AmongUsClient.Instance.AmHost) 
                {
                DraftHud.TurnWatchdogTimer = 0f;
                    
                    DraftPlugin.Instance.Log.LogError($"[ApplyRoleFromRpc] → HOST wywołuje ProcessNextTurn()...");
                    ProcessNextTurn();
                }
            }
            else
            {
                DraftPlugin.Instance.Log.LogError($"[ApplyRoleFromRpc] ✗ Nie znaleziono RoleBehaviour dla: {roleName}");
            }
        }

        private static RoleCategory GetRandomNeutralCategory(System.Random r) {
            int roll = r.Next(0, 3);
            if (roll == 0) return RoleCategory.NeutralKilling;
            if (roll == 1) return RoleCategory.NeutralBenign;
            return RoleCategory.NeutralEvil;
        }
        private static RoleCategory GetWeightedCrewCategory(System.Random r) {
            int roll = r.Next(0, 100);
            if (roll < 20) return RoleCategory.CrewInvestigative;
            if (roll < 40) return RoleCategory.CrewKilling;
            if (roll < 60) return RoleCategory.CrewProtective;
            if (roll < 80) return RoleCategory.CrewSupport;
            return RoleCategory.CrewPower;
        } 
        private static List<string> GenerateUniqueOptions(RoleCategory category, System.Random rng) 
        {
            List<string> allRoles = GetAllAvailableRoleNames();
            List<string> categoryRoles = RoleCategorizer.GetRolesInCategory(category, allRoles);
            
            // Jeśli nie znaleziono ról w kategorii z RoleCategorizer, spróbuj dynamicznie
            if (categoryRoles.Count == 0)
            {
                DraftPlugin.Instance.Log.LogWarning($"[GenerateOptions] Brak ról w kategorii {category}, próbuję dynamicznie...");
                categoryRoles = GetRolesByDynamicCategory(category, allRoles);
            }
            
            var available = categoryRoles.Where(r => !_globalUsedRoles.Contains(r)).ToList();
            if (available.Count < 3) available = categoryRoles; 
            if (available.Count == 0) 
            {
                DraftPlugin.Instance.Log.LogWarning($"[GenerateOptions] Brak ról dla {category}, fallback na wszystkie crew role.");
                available = allRoles.Where(r => !r.Contains("Impostor")).ToList();
            }
            
            return available.OrderBy(x => rng.Next()).Take(3).ToList();
        }
        
        // Dynamiczne kategoryzowanie ról na podstawie ich właściwości w MiraAPI
        private static List<string> GetRolesByDynamicCategory(RoleCategory category, List<string> allRoles)
        {
            List<string> result = new List<string>();
            
            foreach (var roleName in allRoles)
            {
                RoleBehaviour roleObj = null;
                
                // Znajdź rolę w RoleManager.Instance.AllRoles
                foreach (var r in RoleManager.Instance.AllRoles)
                {
                    var uObj = r as UnityEngine.Object;
                    if (uObj != null && uObj.name == roleName)
                    {
                        roleObj = r;
                        break;
                    }
                }
                
                if (roleObj == null) continue;
                
                var roleBehaviour = roleObj.TryCast<RoleBehaviour>();
                if (roleBehaviour == null) continue;
                
                // Sprawdź typ C# roli
                string fullTypeName = roleBehaviour.GetType().FullName;
                
                // Kategoryzuj na podstawie namespace/typu
                bool matches = false;
                
                switch (category)
                {
                    case RoleCategory.RandomImp:
                        matches = fullTypeName.Contains("Impostor") && !fullTypeName.Contains("Hns");
                        break;
                    case RoleCategory.CrewInvestigative:
                        matches = fullTypeName.Contains("Crewmate") && 
                                 (roleName.Contains("Seer") || roleName.Contains("Investigator") || 
                                  roleName.Contains("Spy") || roleName.Contains("Snitch") || 
                                  roleName.Contains("Lookout") || roleName.Contains("Mystic") ||
                                  roleName.Contains("Oracle") || roleName.Contains("Forensic") ||
                                  roleName.Contains("Aurial") || roleName.Contains("Sonar"));
                        break;
                    case RoleCategory.CrewKilling:
                        matches = fullTypeName.Contains("Crewmate") && 
                                 (roleName.Contains("Sheriff") || roleName.Contains("Veteran") || 
                                  roleName.Contains("Vigilante") || roleName.Contains("Hunter"));
                        break;
                    case RoleCategory.CrewProtective:
                        matches = fullTypeName.Contains("Crewmate") && 
                                 (roleName.Contains("Medic") || roleName.Contains("Warden") || 
                                  roleName.Contains("Cleric") || roleName.Contains("Trapper"));
                        break;
                    case RoleCategory.CrewSupport:
                        matches = fullTypeName.Contains("Crewmate") && 
                                 (roleName.Contains("Engineer") || roleName.Contains("Transporter") || 
                                  roleName.Contains("Plumber") || roleName.Contains("Altruist") ||
                                  roleName.Contains("Mayor") || roleName.Contains("Imitator") ||
                                  roleName.Contains("Swapper") || roleName.Contains("Medium"));
                        break;
                    case RoleCategory.CrewPower:
                        matches = fullTypeName.Contains("Crewmate") && 
                                 (roleName.Contains("Politician") || roleName.Contains("Prosecutor") || 
                                  roleName.Contains("Jailor") || roleName.Contains("Mirrorcaster"));
                        break;
                    case RoleCategory.NeutralKilling:
                        matches = fullTypeName.Contains("Neutral") && 
                                 (roleName.Contains("Arsonist") || roleName.Contains("Plaguebearer") || 
                                  roleName.Contains("Werewolf") || roleName.Contains("Glitch") ||
                                  roleName.Contains("Juggernaut") || roleName.Contains("Vampire"));
                        break;
                    case RoleCategory.NeutralEvil:
                        matches = fullTypeName.Contains("Neutral") && 
                                 (roleName.Contains("Jester") || roleName.Contains("Executioner") || 
                                  roleName.Contains("Doomsayer"));
                        break;
                    case RoleCategory.NeutralBenign:
                        matches = fullTypeName.Contains("Neutral") && 
                                 (roleName.Contains("Amnesiac") || roleName.Contains("Survivor") || 
                                  roleName.Contains("Fairy") || roleName.Contains("Mercenary") ||
                                  roleName.Contains("SoulCollector") || roleName.Contains("Chef") ||
                                  roleName.Contains("Inquisitor") || roleName.Contains("Spectre"));
                        break;
                }
                
                if (matches)
                {
                    result.Add(roleName);
                }
            }
            
            return result;
        }
        private static List<string> GetAllAvailableRoleNames() 
        {
            List<string> list = new List<string>();
            
            foreach (var r in RoleManager.Instance.AllRoles) 
            { 
                var unityObj = r as UnityEngine.Object;
                if (unityObj == null) continue;
                
                string roleName = unityObj.name;
                
                // Filtruj vanilla Among Us roles
                if (VanillaBannedRoles.Contains(roleName)) continue;
                
                // Filtruj role transformacyjne
                if (TransformationRoles.Contains(roleName)) 
                {
                    continue;
                }
                
                // Filtruj inne niepożądane
                if (roleName.Contains("Vanilla") || roleName == "Unknown") continue;
                
                // Sprawdź czy to TOU-Mira role (opcjonalnie)
                var roleBehaviour = r.TryCast<RoleBehaviour>();
                if (roleBehaviour != null)
                {
                    string roleType = roleBehaviour.GetType().FullName;
                    bool isTouRole = roleType.StartsWith("TownOfUs.Roles.");
                    
                    if (isTouRole)
                    {
                        // ✅ NOWE: Sprawdź czy rola jest włączona w configu
                        if (IsRoleEnabled(roleName))
                        {
                            list.Add(roleName);
                        }
                    }
                }
                else
                {
                    list.Add(roleName);
                }
            } 
            
            return list;
        }
        
        // Sprawdza czy rola jest włączona w configu (Count > 0)
        private static bool IsRoleEnabled(string roleName)
        {
            try
            {
                // Inicjalizuj cache ról jeśli jeszcze nie zrobiono
                if (!_roleCountCacheInitialized)
                {
                    _roleCountCacheInitialized = true;
                    BuildRoleCountCache();
                }
                
                // Sprawdź w cache
                if (_roleCountCache.ContainsKey(roleName))
                {
                    return _roleCountCache[roleName] > 0;
                }
                
                // Jeśli nie ma w cache, zakładamy że jest włączona (bezpieczniejsze)
                return true;
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[IsRoleEnabled] Exception dla '{roleName}': {ex.Message}");
                return true;
            }
        }
        
        // Buduje cache Count dla wszystkich ról TOU-Mira
        private static void BuildRoleCountCache()
        {
            _roleCountCache.Clear();
            
            try
            {
                // Pobierz TOU-Mira assembly
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                
                if (touAssembly == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[BuildRoleCountCache] TownOfUsMira assembly nie znaleziony!");
                    return;
                }
                
                // Pobierz MiscUtils.AllRegisteredRoles i GetAssignData
                var miscUtilsType = touAssembly.GetType("TownOfUs.Utilities.MiscUtils");
                if (miscUtilsType == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[BuildRoleCountCache] MiscUtils nie znaleziony!");
                    return;
                }
                
                var allRegisteredRolesProp = miscUtilsType.GetProperty("AllRegisteredRoles", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (allRegisteredRolesProp == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[BuildRoleCountCache] AllRegisteredRoles nie znaleziony!");
                    return;
                }
                
                var allRoles = allRegisteredRolesProp.GetValue(null);
                if (allRoles == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[BuildRoleCountCache] AllRegisteredRoles zwrócił null!");
                    return;
                }
                
                var rolesEnumerable = allRoles as System.Collections.IEnumerable;
                if (rolesEnumerable == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[BuildRoleCountCache] AllRegisteredRoles nie jest IEnumerable!");
                    return;
                }
                
                var getAssignDataMethod = miscUtilsType.GetMethod("GetAssignData", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (getAssignDataMethod == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[BuildRoleCountCache] GetAssignData nie znaleziony!");
                    return;
                }
                
                // Iteruj po wszystkich rolach (DOKŁADNIE jak w debug kodzie!)
                foreach (var roleObj in rolesEnumerable)
                {
                    if (roleObj == null) continue;
                    
                    try
                    {
                        // Pobierz RoleTypes (enum używany przez GetAssignData)
                        var roleProp = roleObj.GetType().GetProperty("Role");
                        var roleEnum = roleProp?.GetValue(roleObj);
                        
                        if (roleEnum == null) continue;
                        
                        // Pobierz nazwę roli (Unity object name)
                        var unityObj = roleObj as UnityEngine.Object;
                        if (unityObj == null) continue;
                        
                        string unityName = unityObj.name; // Np. "Sheriff"
                        
                        // Wywołaj GetAssignData(roleEnum)
                        var assignData = getAssignDataMethod.Invoke(null, new object[] { roleEnum });
                        
                        if (assignData == null) continue;
                        
                        // Odczytaj Count z assignData
                        var assignDataType = assignData.GetType();
                        int count = 0;
                        
                        var countField = assignDataType.GetField("Count");
                        var countProp = assignDataType.GetProperty("Count");
                        
                        if (countField != null)
                            count = Convert.ToInt32(countField.GetValue(assignData));
                        else if (countProp != null)
                            count = Convert.ToInt32(countProp.GetValue(assignData));
                        
                        // Zapisz do cache
                        _roleCountCache[unityName] = count;
                    }
                    catch (System.Exception ex)
                    {
                        DraftPlugin.Instance.Log.LogWarning($"[BuildRoleCountCache] Błąd przetwarzania roli: {ex.Message}");
                    }
                }
                
                DraftPlugin.Instance.Log.LogInfo($"[BuildRoleCountCache] Zbudowano cache dla {_roleCountCache.Count} ról");
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[BuildRoleCountCache] Exception: {ex.Message}");
            }
        }
        // NOWA FUNKCJA: Pełna inicjalizacja roli przez TOU-Mira API
        public static PlayerControl GetPlayerById(byte id) { foreach (var p in PlayerControl.AllPlayerControls) if (p.PlayerId == id) return p; return null; }
        private static string FormatCategoryName(RoleCategory c) => c.ToString().Replace("Random","").Replace("Crew","Crewmate ");
        private static void SendStartTurnRpc(byte playerId, string cat, List<string> opts) {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)251, SendOption.Reliable, -1);
            writer.Write(playerId); writer.Write(cat); 
            writer.Write(opts.Count > 0 ? opts[0] : "NO_OPTION"); 
            writer.Write(opts.Count > 1 ? opts[1] : "NO_OPTION"); 
            writer.Write(opts.Count > 2 ? opts[2] : "NO_OPTION");
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        
        // Synchronizacja timera dla wszystkich klientów
        public static void SendTimerSyncRpc(float timerValue)
        {
            if (!AmongUsClient.Instance.AmHost) return; // Tylko host wysyła
            
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId, (byte)254, SendOption.Reliable, -1);
            writer.Write(timerValue);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        
        private static void SendRoleSelectedRpc(byte playerId, string roleName) 
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)249, SendOption.Reliable, -1);
            writer.Write(playerId); 
            writer.Write(roleName);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void OnRandomRoleSelected() { if (DraftHud.MyOptions.Count > 0) OnPlayerSelectedRole(DraftHud.MyOptions[new System.Random().Next(DraftHud.MyOptions.Count)]); }
        
        private static void LogAllDetectedOptions()
        {
            
            // KROK 1: Przeszukaj GameOptionsManager
            if (GameOptionsManager.Instance != null)
            {
                var managerType = GameOptionsManager.Instance.GetType();
                
                // Wszystkie właściwości i pola GameOptionsManager
                var allMembers = managerType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                
                foreach (var member in allMembers)
                {
                    if (member.Name.ToLower().Contains("option") || 
                        member.Name.ToLower().Contains("config") ||
                        member.Name.ToLower().Contains("setting"))
                    {
                        
                        // Jeśli to property, spróbuj odczytać wartość
                        if (member is PropertyInfo prop)
                        {
                            try
                            {
                                var value = prop.GetValue(GameOptionsManager.Instance);
                                if (value != null)
                                {
                                    
                                    // Jeśli to nie jest prymityw, przeszukaj jego członków
                                    if (!value.GetType().IsPrimitive && value.GetType() != typeof(string))
                                    {
                                        DumpObjectMembers(value, "       ");
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                DraftPlugin.Instance.Log.LogWarning($"       Błąd: {ex.Message}");
                            }
                        }
                    }
                }
                
                // CurrentGameOptions
                if (GameOptionsManager.Instance.CurrentGameOptions != null)
                {
                    var gameOptions = GameOptionsManager.Instance.CurrentGameOptions;
                    
                    DumpAllFields(gameOptions, "    ");
                    
                    // *** NOWE: PRZESZUKAJ RoleOptions! ***
                    try
                    {
                        var roleOptionsProp = gameOptions.GetType().GetProperty("RoleOptions");
                        if (roleOptionsProp != null)
                        {
                            var roleOptions = roleOptionsProp.GetValue(gameOptions);
                            if (roleOptions != null)
                            {
                                
                                // GŁĘBOKI DUMP - użyj DumpAllFields aby zobaczyć WSZYSTKIE pola
                                DumpAllFields(roleOptions, "    ");
                                
                                // KROK 1: Spróbuj odczytać pola bezpośrednio z obiektu RoleOptions
                                try
                                {
                                    var roleOptionsType = roleOptions.GetType();
                                    
                                    // Odczytaj WSZYSTKIE pola (publiczne i prywatne)
                                    var allFields = roleOptionsType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                    
                                    foreach (var field in allFields)
                                    {
                                        try
                                        {
                                            var fieldValue = field.GetValue(roleOptions);
                                            
                                            // Jeśli to lista/tablica - wyświetl zawartość
                                            if (fieldValue != null)
                                            {
                                                var fieldType = fieldValue.GetType();
                                                
                                                // Sprawdź czy to tablica
                                                if (fieldType.IsArray)
                                                {
                                                    var array = (System.Array)fieldValue;
                                                    int maxItems = array.Length < 15 ? array.Length : 15;
                                                    for (int i = 0; i < maxItems; i++)
                                                    {
                                                        var item = array.GetValue(i);
                                                        if (item != null)
                                                        {
                                                            // Jeśli item ma więcej właściwości - wyświetl je
                                                            var itemType = item.GetType();
                                                            if (itemType.IsEnum || itemType.IsPrimitive || itemType == typeof(string))
                                                            {
                                                            }
                                                            else
                                                            {
                                                                
                                                                // Wyświetl publiczne właściwości obiektu
                                                                var props = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Take(5);
                                                                foreach (var prop in props)
                                                                {
                                                                    try
                                                                    {
                                                                        var propValue = prop.GetValue(item);
                                                                    }
                                                                    catch { }
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                        }
                                                    }
                                                }
                                                // Sprawdź czy to Il2CppSystem.Collections
                                                else if (fieldType.FullName != null && fieldType.FullName.Contains("Il2CppSystem.Collections"))
                                                {
                                                    
                                                    // Spróbuj odczytać Count
                                                    var countProp = fieldType.GetProperty("Count");
                                                    if (countProp != null)
                                                    {
                                                        try
                                                        {
                                                            var count = countProp.GetValue(fieldValue);
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }
                                        }
                                        catch (System.Exception ex)
                                        {
                                            DraftPlugin.Instance.Log.LogWarning($"    ✗ Błąd odczytu {field.Name}: {ex.Message}");
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    DraftPlugin.Instance.Log.LogError($"[3.CAST] Exception: {ex.Message}\n{ex.StackTrace}");
                                }
                                
                                // DODATKOWE SPRAWDZENIA - spróbuj rzutować i odczytać konkretne typy
                                try
                                {
                                    // Spróbuj rzutować na RoleOptionsCollectionV10
                                    var roleOptionsType = roleOptions.GetType();
                                    
                                    // Sprawdź wszystkie metody
                                    var methods = roleOptionsType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                                        .Take(20);
                                    
                                    foreach (var method in methods)
                                    {
                                        var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                                    }
                                    
                                    // Sprawdź wszystkie właściwości szczegółowo
                                    var properties = roleOptionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                                    
                                    foreach (var prop in properties)
                                    {
                                        try
                                        {
                                            var value = prop.GetValue(roleOptions);
                                            
                                            // Jeśli to kolekcja, lista lub tablica - pokaż zawartość
                                            if (value != null)
                                            {
                                                var valueType = value.GetType();
                                                
                                                // Sprawdź czy to Il2CppSystem.Collections.Generic
                                                if (valueType.FullName != null && 
                                                    (valueType.FullName.Contains("List") || 
                                                     valueType.FullName.Contains("Array") ||
                                                     valueType.FullName.Contains("Dictionary") ||
                                                     valueType.FullName.Contains("Collection")))
                                                {
                                                    
                                                    // Spróbuj odczytać Count
                                                    var countProp = valueType.GetProperty("Count");
                                                    if (countProp != null)
                                                    {
                                                        var count = countProp.GetValue(value);
                                                    }
                                                    
                                                    // Spróbuj iterować
                                                    if (value is System.Collections.IEnumerable enumerable)
                                                    {
                                                        int idx = 0;
                                                        foreach (var item in enumerable)
                                                        {
                                                            if (idx < 5) // Pokaż tylko pierwsze 5
                                                            {
                                                            }
                                                            idx++;
                                                        }
                                                        if (idx > 5)
                                                        {
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (System.Exception ex)
                                        {
                                            DraftPlugin.Instance.Log.LogWarning($"    -> {prop.Name} (błąd odczytu: {ex.Message})");
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    DraftPlugin.Instance.Log.LogError($"[3] Błąd podczas szczegółowej analizy RoleOptions: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DraftPlugin.Instance.Log.LogError($"[DEBUG] Błąd czytania RoleOptions: {ex.Message}");
                    }
                }
            }
            
            
            try
            {
                // 1. Znajdź MiraAPI assembly
                var miraAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "MiraAPI");
                
                if (miraAssembly == null)
                {
                    DraftPlugin.Instance.Log.LogWarning("[DEBUG] MiraAPI assembly nie znalezione!");
                    return;
                }
                
                
                // 2. Wyświetl WSZYSTKIE typy w namespace MiraAPI.GameOptions
                var gameOptionsTypes = miraAssembly.GetTypes()
                    .Where(t => t.Namespace != null && t.Namespace.StartsWith("MiraAPI.GameOptions"))
                    .ToList();
                
                foreach (var type in gameOptionsTypes)
                {
                }
                
                // 3. Wyświetl WSZYSTKIE typy w namespace MiraAPI.Roles
                var roleTypes = miraAssembly.GetTypes()
                    .Where(t => t.Namespace != null && t.Namespace.StartsWith("MiraAPI.Roles"))
                    .Take(15) // Tylko pierwsze 15
                    .ToList();
                
                foreach (var type in roleTypes)
                {
                }
                
                // 4. Sprawdź RoleOptionsGroup
                var roleOptionsGroupType = miraAssembly.GetType("MiraAPI.Roles.RoleOptionsGroup");
                if (roleOptionsGroupType != null)
                {
                    
                    // Sprawdź właściwości statyczne i instancyjne
                    var allProps = roleOptionsGroupType.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                    foreach (var prop in allProps.Take(15))
                    {
                        try
                        {
                        }
                        catch (System.Exception)
                        {
                        }
                    }
                    
                    // Sprawdź metody
                    var methods = roleOptionsGroupType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                        .Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_") && !m.Name.StartsWith("add_") && !m.Name.StartsWith("remove_"))
                        .Take(10);
                    
                    foreach (var method in methods)
                    {
                        var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                    }
                }
                else
                {
                    DraftPlugin.Instance.Log.LogWarning("[DEBUG] RoleOptionsGroup nie znaleziony!");
                }
                
                // 5. Spróbuj znaleźć RoleManager
                var roleManagerType = miraAssembly.GetType("MiraAPI.Roles.RoleManager");
                if (roleManagerType != null)
                {
                    
                    // Pokaż właściwości statyczne
                    var properties = roleManagerType.GetProperties(BindingFlags.Public | BindingFlags.Static);
                    
                    foreach (var prop in properties)
                    {
                        
                        // Jeśli to kolekcja ról, spróbuj ją odczytać
                        if (prop.Name.Contains("Role") || prop.Name.Contains("All") || prop.Name.Contains("Register"))
                        {
                            try
                            {
                                var value = prop.GetValue(null);
                                if (value != null)
                                {
                                    
                                    // Jeśli to kolekcja, pokaż ile elementów
                                    if (value is System.Collections.IEnumerable enumerable)
                                    {
                                        int count = 0;
                                        foreach (var item in enumerable)
                                        {
                                            count++;
                                            if (count <= 5) // Pokaż pierwsze 5
                                            {
                                            }
                                        }
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                DraftPlugin.Instance.Log.LogWarning($"    └─> Nie można odczytać: {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    DraftPlugin.Instance.Log.LogWarning("[DEBUG] RoleManager nie znaleziony!");
                }
                
                // 5. Sprawdź TownOfUsMira assembly
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                
                if (touAssembly != null)
                {
                    
                    // Znajdź wszystkie typy ról w TOU (tylko CustomRole)
                    var touRoleTypes = touAssembly.GetTypes()
                        .Where(t => t.Name.EndsWith("Role") && 
                                   t.Namespace != null && 
                                   t.Namespace.StartsWith("TownOfUs.Roles") &&
                                   !t.IsAbstract)
                        .ToList();
                    
                    
                    // Pokaż przykładowe role z każdej kategorii
                    var crewRoles = touRoleTypes.Where(r => r.Namespace.Contains("Crewmate")).Take(5).ToList();
                    var impRoles = touRoleTypes.Where(r => r.Namespace.Contains("Impostor")).Take(5).ToList();
                    var neutralRoles = touRoleTypes.Where(r => r.Namespace.Contains("Neutral")).Take(5).ToList();
                    
                    foreach (var role in crewRoles)
                    {
                    }
                    
                    foreach (var role in impRoles)
                    {
                    }
                    
                    foreach (var role in neutralRoles)
                    {
                    }
                    
                    // Sprawdź typy związane z Options w TOU-Mira
                    var optionsTypes = touAssembly.GetTypes()
                        .Where(t => t.Name.Contains("Option") || t.Name.Contains("Config") || t.Name.Contains("Setting"))
                        .Take(20)
                        .ToList();
                    
                    foreach (var type in optionsTypes)
                    {
                        
                        // Jeśli typ ma statyczne właściwości/pola, pokaż je
                        var staticMembers = type.GetMembers(BindingFlags.Public | BindingFlags.Static)
                            .Where(m => m is PropertyInfo || m is FieldInfo)
                            .Take(5);
                        
                        foreach (var member in staticMembers)
                        {
                            try
                            {
                                if (member is PropertyInfo prop)
                                {
                                    var value = prop.GetValue(null);
                                }
                                else if (member is FieldInfo field)
                                {
                                    var value = field.GetValue(null);
                                }
                            }
                            catch { }
                        }
                    }
                    
                    // KLUCZOWE: Szczegółowy odczyt TownOfUs.Options.RoleOptions i Slot1-15
                    
                    // Krok 1: Znajdź OptionGroupSingleton<T> w MiraAPI
                    var miraApiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "MiraAPI");
                    
                    if (miraApiAssembly == null)
                    {
                        DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ MiraAPI assembly nie znalezione!");
                    }
                    else
                    {
                        var optionGroupSingletonType = miraApiAssembly.GetType("MiraAPI.GameOptions.OptionGroupSingleton`1");
                        var roleOptionsType = touAssembly.GetType("TownOfUs.Options.RoleOptions");
                        
                        if (optionGroupSingletonType == null)
                        {
                            DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ OptionGroupSingleton<> nie znaleziony!");
                        }
                        else if (roleOptionsType == null)
                        {
                            DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ RoleOptions nie znaleziony!");
                        }
                        else
                        {
                            
                            // Krok 2: Utwórz OptionGroupSingleton<RoleOptions>
                            var singletonType = optionGroupSingletonType.MakeGenericType(roleOptionsType);
                            
                            // Krok 3: Pobierz właściwość Instance
                            var instanceProp = singletonType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                            if (instanceProp == null)
                            {
                                DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ Właściwość 'Instance' nie znaleziona!");
                            }
                            else
                            {
                                try
                                {
                                    var roleOptionsInstance = instanceProp.GetValue(null);
                                    if (roleOptionsInstance == null)
                                    {
                                        DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ Instance jest NULL!");
                                    }
                                    else
                                    {
                                        
                                        // Krok 4: Odczytaj RoleAssignmentType
                                        var assignmentTypeProp = roleOptionsType.GetProperty("RoleAssignmentType", BindingFlags.Public | BindingFlags.Instance);
                                        if (assignmentTypeProp != null)
                                        {
                                            try
                                            {
                                                var assignmentTypeObject = assignmentTypeProp.GetValue(roleOptionsInstance);
                                                if (assignmentTypeObject != null)
                                                {
                                                    // RoleAssignmentType to ModdedEnumOption, musimy odczytać .Value
                                                    var valueProp = assignmentTypeObject.GetType().GetProperty("Value");
                                                    if (valueProp != null)
                                                    {
                                                        var assignmentTypeValue = valueProp.GetValue(assignmentTypeObject);
                                                    }
                                                    else
                                                    {
                                                    }
                                                }
                                            }
                                            catch (System.Exception ex)
                                            {
                                                DraftPlugin.Instance.Log.LogWarning($"[DEBUG] ✗ Błąd odczytu RoleAssignmentType: {ex.Message}");
                                            }
                                        }
                        
                                        // Krok 5: Odczytaj Slot1 do Slot15 (konfiguracja dla każdego gracza)
                                        for (int slotNum = 1; slotNum <= 15; slotNum++)
                                        {
                                            var slotName = $"Slot{slotNum}";
                                            
                                            // Szukaj właściwości Slot{X} na instancji RoleOptions
                                            var slotProp = roleOptionsType.GetProperty(slotName, BindingFlags.Public | BindingFlags.Instance);
                                            
                                            if (slotProp != null)
                                            {
                                                try
                                                {
                                                    // Pobierz obiekt slotu z instancji (to będzie ModdedEnumOption)
                                                    var slotObject = slotProp.GetValue(roleOptionsInstance);
                                                    
                                                    if (slotObject != null)
                                                    {
                                                        var slotTypeName = slotObject.GetType().Name;
                                                        
                                        // Odczytaj .Value z ModdedEnumOption (faktyczna wartość kategorii - to jest INDEX)
                                        var valueProp = slotObject.GetType().GetProperty("Value");
                                        
                                        if (valueProp != null)
                                        {
                                            int selectedIndex = (int)valueProp.GetValue(slotObject);
                                            
                                            // Próba odczytania nazw kategorii dynamicznie
                                            string readableName = "UNKNOWN";
                                            string[] categoryNames = null;
                                            
                                            // Metoda 1: Spróbuj property "Selections"
                                            var selectionsProp = slotObject.GetType().GetProperty("Selections");
                                            if (selectionsProp != null)
                                            {
                                                try
                                                {
                                                    categoryNames = (string[])selectionsProp.GetValue(slotObject);
                                                }
                                                catch { }
                                            }
                                            
                                            // Metoda 2: Spróbuj property "Values" (widziane w logach)
                                            if (categoryNames == null)
                                            {
                                                var valuesProp = slotObject.GetType().GetProperty("Values");
                                                if (valuesProp != null)
                                                {
                                                    try
                                                    {
                                                        categoryNames = (string[])valuesProp.GetValue(slotObject);
                                                    }
                                                    catch { }
                                                }
                                            }
                                            
                                            // Metoda 3: Fallback - użyj zahardkodowanej tabeli
                                            if (categoryNames == null)
                                            {
                                                DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ Nie udało się pobrać kategorii dynamicznie, używam zahardkodowanej tabeli");
                                                categoryNames = new string[]
                                                {
                                                    "CommonCrew", "RandomCrew", "CrewInvestigative", "CrewKilling", 
                                                    "CrewProtective", "CrewPower", "CrewSupport", "SpecialCrew",
                                                    "NonImp", "CommonNeutral", "SpecialNeutral", "WildcardNeutral",
                                                    "RandomNeutral", "NeutralBenign", "NeutralEvil", "NeutralKilling",
                                                    "NeutralOutlier", "CommonImp", "RandomImp", "ImpConcealing",
                                                    "ImpKilling", "ImpPower", "ImpSupport", "SpecialImp", "Any"
                                                };
                                            }
                                            
                                            // Zmapuj index na nazwę
                                            if (categoryNames != null && selectedIndex >= 0 && selectedIndex < categoryNames.Length)
                                            {
                                                readableName = categoryNames[selectedIndex];
                                            }
                                            
                                            
                                            // Jeśli to pierwszy slot, wypisz pełną listę kategorii
                                            if (slotNum == 1 && categoryNames != null)
                                            {
                                                for (int i = 0; i < categoryNames.Length; i++)
                                                {
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // Jeśli nie ma .Value, wypisz cały obiekt
                                        }
                                                    }
                                                    else
                                                    {
                                                        DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ {slotName} = NULL");
                                                    }
                                                }
                                                catch (System.Exception ex)
                                                {
                                                    DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ Błąd odczytu {slotName}: {ex.Message}");
                                                    DraftPlugin.Instance.Log.LogWarning($"[DEBUG]      Stack: {ex.StackTrace}");
                                                }
                                            }
                                            else
                                            {
                                                DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ Właściwość {slotName} nie znaleziona!");
                                            }
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    DraftPlugin.Instance.Log.LogWarning($"[DEBUG] ✗ Błąd podczas pobierania Instance: {ex.Message}");
                                    DraftPlugin.Instance.Log.LogWarning($"[DEBUG]    Stack: {ex.StackTrace}");
                                }
                            }
                        }
                    }
                    
                    
                    // KLUCZOWE: Sprawdzenie włączonych ról używając MiscUtils
                    
                    try
                    {
                        // Pobierz typ MiscUtils z TOU-Mira
                        var miscUtilsType = touAssembly.GetType("TownOfUs.Utilities.MiscUtils");
                        
                        if (miscUtilsType == null)
                        {
                            DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ TownOfUs.Utilities.MiscUtils nie znaleziony!");
                        }
                        else
                        {
                            
                            // Pobierz AllRegisteredRoles (statyczna właściwość)
                            var allRegisteredRolesProp = miscUtilsType.GetProperty("AllRegisteredRoles", 
                                BindingFlags.Public | BindingFlags.Static);
                            
                            if (allRegisteredRolesProp == null)
                            {
                                DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ AllRegisteredRoles nie znaleziony!");
                            }
                            else
                            {
                                var allRoles = allRegisteredRolesProp.GetValue(null);
                                
                                if (allRoles == null)
                                {
                                    DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ AllRegisteredRoles zwrócił null!");
                                }
                                else
                                {
                                    // To powinno być IEnumerable<RoleBehaviour>
                                    var rolesEnumerable = allRoles as System.Collections.IEnumerable;
                                    
                                    if (rolesEnumerable == null)
                                    {
                                        DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ AllRegisteredRoles nie jest IEnumerable!");
                                    }
                                    else
                                    {
                                        
                                        // Pobierz metodę GetAssignData
                                        var getAssignDataMethod = miscUtilsType.GetMethod("GetAssignData", 
                                            BindingFlags.Public | BindingFlags.Static);
                                        
                                        if (getAssignDataMethod == null)
                                        {
                                            DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ GetAssignData nie znaleziony!");
                                        }
                                        else
                                        {
                                            
                                            int enabledCount = 0;
                                            int disabledCount = 0;
                                            int totalCount = 0;
                                            
                                            // Iteruj po wszystkich rolach
                                            foreach (var roleObj in rolesEnumerable)
                                            {
                                                if (roleObj == null) continue;
                                                
                                                try
                                                {
                                                    totalCount++;
                                                    
                                                    // Pobierz RoleTypes (enum używany przez GetAssignData)
                                                    var roleProp = roleObj.GetType().GetProperty("Role");
                                                    var roleEnum = roleProp?.GetValue(roleObj);
                                                    
                                                    if (roleEnum == null)
                                                    {
                                                        DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ {roleObj.GetType().Name}: brak Role property");
                                                        continue;
                                                    }
                                                    
                                                    // Pobierz techniczną nazwę roli - NAJPIERW spróbuj z klasy (np. "SheriffRole")
                                                    string roleName = roleObj.GetType().Name; // Np. "SheriffRole"
                                                    
                                                    // Usuń suffix "Role" jeśli istnieje
                                                    if (roleName.EndsWith("Role"))
                                                    {
                                                        roleName = roleName.Substring(0, roleName.Length - 4); // "Sheriff"
                                                    }
                                                    
                                                    // Alternatywnie: spróbuj pobrać nazwę z pola "Name" obiektu roli
                                                    try
                                                    {
                                                        var nameField = roleObj.GetType().GetField("RoleName", BindingFlags.Public | BindingFlags.Instance);
                                                        if (nameField != null)
                                                        {
                                                            var nameValue = nameField.GetValue(roleObj);
                                                            if (nameValue != null && !string.IsNullOrEmpty(nameValue.ToString()))
                                                            {
                                                                roleName = nameValue.ToString();
                                                            }
                                                        }
                                                    }
                                                    catch { }
                                                    
                                                    // Wywołaj GetAssignData(roleEnum)
                                                    var assignData = getAssignDataMethod.Invoke(null, new object[] { roleEnum });
                                                    
                                                    if (assignData == null)
                                                    {
                                                        DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ {roleName}: GetAssignData zwrócił null");
                                                        continue;
                                                    }
                                                    
                                                    // Odczytaj Count i Chance z assignData
                                                    var assignDataType = assignData.GetType();
                                                    
                                                    int count = 0;
                                                    int chance = 0;
                                                    
                                                    // Spróbuj odczytać Count
                                                    var countField = assignDataType.GetField("Count");
                                                    var countProp = assignDataType.GetProperty("Count");
                                                    
                                                    if (countField != null)
                                                        count = Convert.ToInt32(countField.GetValue(assignData));
                                                    else if (countProp != null)
                                                        count = Convert.ToInt32(countProp.GetValue(assignData));
                                                    
                                                    // Spróbuj odczytać Chance
                                                    var chanceField = assignDataType.GetField("Chance");
                                                    var chanceProp = assignDataType.GetProperty("Chance");
                                                    
                                                    if (chanceField != null)
                                                        chance = Convert.ToInt32(chanceField.GetValue(assignData));
                                                    else if (chanceProp != null)
                                                        chance = Convert.ToInt32(chanceProp.GetValue(assignData));
                                                    
                                                    // Sprawdź czy rola jest włączona
                                                    if (count > 0 && chance >= 10)
                                                    {
                                                        enabledCount++;
                                                    }
                                                    else
                                                    {
                                                        disabledCount++;
                                                        // Opcjonalnie: zakomentuj żeby nie spamować logów
                                                        // DraftPlugin.Instance.Log.LogInfo($"[DEBUG]   ✗ {roleName}: Count={count}, Chance={chance}% [WYŁĄCZONA]");
                                                    }
                                                }
                                                catch (System.Exception ex)
                                                {
                                                    DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ Błąd przetwarzania roli: {ex.Message}");
                                                }
                                            }
                                            
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DraftPlugin.Instance.Log.LogError($"[DEBUG] Błąd sprawdzania włączonych ról: {ex.Message}");
                        DraftPlugin.Instance.Log.LogError($"[DEBUG] Stack: {ex.StackTrace}");
                    }
                    
                    // KLUCZOWE: Mapowanie ról do kategorii poprzez właściwość .Group
                    
                    try
                    {
                        var miscUtilsType2 = touAssembly.GetType("TownOfUs.Utilities.MiscUtils");
                        if (miscUtilsType2 == null)
                        {
                            DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ MiscUtils nie znaleziony (mapa kategorii)!");
                        }
                        else
                        {
                            var allRegisteredRolesProp2 = miscUtilsType2.GetProperty("AllRegisteredRoles", BindingFlags.Public | BindingFlags.Static);
                            
                            if (allRegisteredRolesProp2 != null)
                            {
                                var allRoles2 = allRegisteredRolesProp2.GetValue(null) as System.Collections.IEnumerable;
                                
                                if (allRoles2 != null)
                                {
                                    
                                    // Grupuj role według kategorii
                                    var categoryMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
                                    
                                    foreach (var roleObj in allRoles2)
                                    {
                                        if (roleObj == null) continue;
                                        
                                        try
                                        {
                                            // DEBUG: Sprawdź typ obiektu
                                            var roleType = roleObj.GetType();
                                            
                                            // 1. Nazwa roli
                                            string roleName = roleType.Name;
                                            if (roleName.EndsWith("Role"))
                                            {
                                                roleName = roleName.Substring(0, roleName.Length - 4);
                                            }
                                            
                                            
                                            // 2. Pobierz obiekt grupy (.Group property)
                                            var groupProp = roleType.GetProperty("Group");
                                            
                                            string groupName = "Brak Grupy";
                                            
                                            if (groupProp != null)
                                            {
                                                var groupObj = groupProp.GetValue(roleObj);
                                                if (groupObj != null)
                                                {
                                                    // Pobierz nazwę grupy z obiektu RoleOptionsGroup
                                                    var nameField = groupObj.GetType().GetField("Name");
                                                    var nameProp = groupObj.GetType().GetProperty("Name");
                                                    
                                                    if (nameField != null)
                                                        groupName = (string)nameField.GetValue(groupObj);
                                                    else if (nameProp != null)
                                                        groupName = (string)nameProp.GetValue(groupObj);
                                                }
                                            }
                                            
                                            // Dodaj do mapy
                                            if (!categoryMap.ContainsKey(groupName))
                                            {
                                                categoryMap[groupName] = new System.Collections.Generic.List<string>();
                                            }
                                            categoryMap[groupName].Add(roleName);
                                            
                                            // Wypisz mapowanie (tylko pierwsze 5 z każdej kategorii dla czytelności)
                                            if (categoryMap[groupName].Count <= 5)
                                            {
                                            }
                                        }
                                        catch (System.Exception ex)
                                        {
                                            DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ Błąd mapowania roli: {ex.Message}");
                                        }
                                    }
                                    
                                    // Wypisz podsumowanie kategorii
                                    foreach (var kvp in categoryMap.OrderBy(x => x.Key))
                                    {
                                        string rolesList = string.Join(", ", kvp.Value.Take(10));
                                        if (kvp.Value.Count > 10)
                                        {
                                            rolesList += $" ... (+{kvp.Value.Count - 10} więcej)";
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DraftPlugin.Instance.Log.LogError($"[DEBUG] Błąd mapowania ról do kategorii: {ex.Message}");
                        DraftPlugin.Instance.Log.LogError($"[DEBUG] Stack: {ex.StackTrace}");
                    }
                    
                    
                    // KLUCZOWE: Odczyt kategorii ról z TouRoleGroups
                    
                    try
                    {
                        var touRoleGroupsType = touAssembly.GetType("TownOfUs.Roles.TouRoleGroups");
                        
                        if (touRoleGroupsType == null)
                        {
                            DraftPlugin.Instance.Log.LogWarning("[DEBUG] ✗ TouRoleGroups nie znaleziony!");
                        }
                        else
                        {
                            
                            // Najpierw przeskanuj WSZYSTKIE dostępne pola/properties
                            
                            var allFields = touRoleGroupsType.GetFields(BindingFlags.Public | BindingFlags.Static);
                            var allProps = touRoleGroupsType.GetProperties(BindingFlags.Public | BindingFlags.Static);
                            
                            
                            // Wypisz wszystkie pola
                            foreach (var field in allFields.Take(30)) // Ogranicz do 30
                            {
                                try
                                {
                                    var value = field.GetValue(null);
                                    var typeName = field.FieldType.Name;
                                    
                                    // Sprawdź czy to lista/enumerable ról
                                    if (value is System.Collections.IEnumerable enumerable && !(value is string))
                                    {
                                        var rolesList = new System.Collections.Generic.List<string>();
                                        
                                        foreach (var item in enumerable)
                                        {
                                            if (item != null)
                                            {
                                                // Spróbuj pobrać nazwę z Enum.GetName()
                                                string itemName = item.ToString();
                                                try
                                                {
                                                    var enumType = item.GetType();
                                                    var enumName = System.Enum.GetName(enumType, item);
                                                    if (!string.IsNullOrEmpty(enumName))
                                                    {
                                                        itemName = enumName;
                                                    }
                                                }
                                                catch { }
                                                
                                                rolesList.Add(itemName);
                                            }
                                        }
                                        
                                        if (rolesList.Count > 0)
                                        {
                                        }
                                        else
                                        {
                                        }
                                    }
                                    else
                                    {
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ Błąd odczytu pola {field.Name}: {ex.Message}");
                                }
                            }
                            
                            // Wypisz wszystkie properties i ZAGŁĘB SIĘ w RoleOptionsGroup
                            foreach (var prop in allProps.Take(30)) // Ogranicz do 30
                            {
                                try
                                {
                                    var value = prop.GetValue(null);
                                    var typeName = prop.PropertyType.Name;
                                    
                                    // Jeśli to RoleOptionsGroup, spróbuj odczytać jego zawartość
                                    if (typeName == "RoleOptionsGroup")
                                    {
                                        
                                        // Spróbuj znaleźć listę ról w tym obiekcie
                                        if (value != null)
                                        {
                                            var groupType = value.GetType();
                                            
                                            // Szukaj pól/properties zawierających listy ról
                                            foreach (var subField in groupType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                                            {
                                                try
                                                {
                                                    var subValue = subField.GetValue(value);
                                                    if (subValue is System.Collections.IEnumerable enumerable && !(subValue is string))
                                                    {
                                                        var rolesList = new System.Collections.Generic.List<string>();
                                                        
                                                        foreach (var item in enumerable)
                                                        {
                                                            if (item != null)
                                                            {
                                                                string itemName = item.ToString();
                                                                try
                                                                {
                                                                    var enumType = item.GetType();
                                                                    var enumName = System.Enum.GetName(enumType, item);
                                                                    if (!string.IsNullOrEmpty(enumName))
                                                                    {
                                                                        itemName = enumName;
                                                                    }
                                                                }
                                                                catch { }
                                                                
                                                                rolesList.Add(itemName);
                                                            }
                                                        }
                                                        
                                                        if (rolesList.Count > 0)
                                                        {
                                                        }
                                                    }
                                                }
                                                catch { }
                                            }
                                            
                                            // Sprawdź też properties
                                            foreach (var subProp in groupType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                            {
                                                try
                                                {
                                                    var subValue = subProp.GetValue(value);
                                                    if (subValue is System.Collections.IEnumerable enumerable && !(subValue is string))
                                                    {
                                                        var rolesList = new System.Collections.Generic.List<string>();
                                                        
                                                        foreach (var item in enumerable)
                                                        {
                                                            if (item != null)
                                                            {
                                                                string itemName = item.ToString();
                                                                try
                                                                {
                                                                    var enumType = item.GetType();
                                                                    var enumName = System.Enum.GetName(enumType, item);
                                                                    if (!string.IsNullOrEmpty(enumName))
                                                                    {
                                                                        itemName = enumName;
                                                                    }
                                                                }
                                                                catch { }
                                                                
                                                                rolesList.Add(itemName);
                                                            }
                                                        }
                                                        
                                                        if (rolesList.Count > 0)
                                                        {
                                                        }
                                                    }
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                    // Sprawdź czy to lista/enumerable ról
                                    else if (value is System.Collections.IEnumerable enumerable && !(value is string))
                                    {
                                        var rolesList = new System.Collections.Generic.List<string>();
                                        
                                        foreach (var item in enumerable)
                                        {
                                            if (item != null)
                                            {
                                                // Spróbuj pobrać nazwę z Enum.GetName()
                                                string itemName = item.ToString();
                                                try
                                                {
                                                    var enumType = item.GetType();
                                                    var enumName = System.Enum.GetName(enumType, item);
                                                    if (!string.IsNullOrEmpty(enumName))
                                                    {
                                                        itemName = enumName;
                                                    }
                                                }
                                                catch { }
                                                
                                                rolesList.Add(itemName);
                                            }
                                        }
                                        
                                        if (rolesList.Count > 0)
                                        {
                                        }
                                        else
                                        {
                                        }
                                    }
                                    else
                                    {
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ Błąd odczytu property {prop.Name}: {ex.Message}");
                                }
                            }
                            
                            
                            // NOWE: Skanowanie RoleOptionsGroup dla list ról
                            
                            try
                            {
                                var categoryRoleMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
                                
                                // Iteruj po WSZYSTKICH RoleOptionsGroup properties
                                foreach (var prop in allProps)
                                {
                                    try
                                    {
                                        var groupObj = prop.GetValue(null);
                                        if (groupObj == null) continue;
                                        
                                        var groupType = groupObj.GetType();
                                        if (groupType.Name != "RoleOptionsGroup") continue;
                                        
                                        string categoryName = prop.Name; // Np. "CrewInvest", "NeutralKiller"
                                        
                                        // Pobierz ładniejszą nazwę z obiektu grupy
                                        try
                                        {
                                            var nameField = groupType.GetField("Name");
                                            var nameProp = groupType.GetProperty("Name");
                                            if (nameField != null)
                                                categoryName = (string)nameField.GetValue(groupObj) ?? categoryName;
                                            else if (nameProp != null)
                                                categoryName = (string)nameProp.GetValue(groupObj) ?? categoryName;
                                        }
                                        catch { }
                                        
                                        if (!categoryRoleMap.ContainsKey(categoryName))
                                        {
                                            categoryRoleMap[categoryName] = new System.Collections.Generic.List<string>();
                                        }
                                        
                                        
                                        // Szukaj kolekcji ról w tym obiekcie grupy
                                        foreach (var subProp in groupType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                                        {
                                            try
                                            {
                                                var collectionObj = subProp.GetValue(groupObj);
                                                if (collectionObj is System.Collections.IEnumerable collection && !(collectionObj is string))
                                                {
                                                    foreach (var roleItem in collection)
                                                    {
                                                        if (roleItem == null) continue;
                                                        
                                                        // Pobierz nazwę z typu (np. "SheriffOption" -> "Sheriff")
                                                        string roleName = roleItem.GetType().Name
                                                            .Replace("Option", "")
                                                            .Replace("Role", "")
                                                            .Replace("_", "");
                                                        
                                                        if (!string.IsNullOrEmpty(roleName) && !categoryRoleMap[categoryName].Contains(roleName))
                                                        {
                                                            categoryRoleMap[categoryName].Add(roleName);
                                                        }
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                    catch (System.Exception ex)
                                    {
                                        DraftPlugin.Instance.Log.LogWarning($"[DEBUG]   ✗ Błąd skanowania {prop.Name}: {ex.Message}");
                                    }
                                }
                                
                                // Podsumowanie
                                foreach (var kvp in categoryRoleMap.OrderBy(x => x.Key))
                                {
                                    if (kvp.Value.Count > 0)
                                    {
                                        string rolesList = string.Join(", ", kvp.Value.Take(10));
                                        if (kvp.Value.Count > 10)
                                            rolesList += $" ... (+{kvp.Value.Count - 10} więcej)";
                                        
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                DraftPlugin.Instance.Log.LogError($"[DEBUG] Błąd nowego mapowania: {ex.Message}");
                            }
                            
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DraftPlugin.Instance.Log.LogError($"[DEBUG] Błąd odczytu TouRoleGroups: {ex.Message}");
                        DraftPlugin.Instance.Log.LogError($"[DEBUG] Stack: {ex.StackTrace}");
                    }
                }
                else
                {
                    DraftPlugin.Instance.Log.LogWarning("[DEBUG] TownOfUsMira assembly nie znalezione!");
                }
                
            }
            catch (System.Exception ex)
            {
                DraftPlugin.Instance.Log.LogError($"[DEBUG] Exception: {ex.Message}\n{ex.StackTrace}");
            }
            
        }
        
        // --- Funkcje pomocnicze do debugowania ---
        private static void DumpObjectMembers(object obj, string indent)
        {
            if (obj == null) return;
            
            var objType = obj.GetType();
            var members = objType.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m is PropertyInfo || m is FieldInfo)
                .Take(30); // Ogranicz do 30 aby nie zapełnić logów
            
            int count = 0;
            foreach (var member in members)
            {
                count++;
                try
                {
                    object value = null;
                    string typeName = "";
                    
                    if (member is PropertyInfo prop)
                    {
                        value = prop.GetValue(obj);
                        typeName = prop.PropertyType.Name;
                    }
                    else if (member is FieldInfo field)
                    {
                        value = field.GetValue(obj);
                        typeName = field.FieldType.Name;
                    }
                    
                }
                catch
                {
                }
            }
            
            if (count >= 30)
            {
            }
        }
        
        private static void DumpAllFields(object obj, string indent)
        {
            if (obj == null) return;
            
            var objType = obj.GetType();
            var allFields = objType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            
            // Najpierw pokaż WSZYSTKIE nazwy pól
            foreach (var field in allFields.Take(100)) // Limit 100 aby nie zapełnić logów
            {
                try
                {
                    var value = field.GetValue(obj);
                    string displayValue = value?.ToString() ?? "null";
                    
                    // Ogranicz długość wyświetlanej wartości
                    if (displayValue.Length > 50)
                        displayValue = displayValue.Substring(0, 47) + "...";
                    
                }
                catch (System.Exception)
                {
                }
            }
            
            if (allFields.Length > 100)
            {
            }
        }
    }
}
