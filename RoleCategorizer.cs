using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MiraAPI.Roles;
using TownOfUs.Roles;
using System.Reflection;

namespace TownOfUsDraft
{
    public enum DraftCategory { Crewmate, Support, Investigative, Killing, Power, Protective, Impostor, NeutralBenign, NeutralEvil, NeutralKilling, Unknown }

    public static class RoleCategorizer
    {
        private static Dictionary<DraftCategory, List<string>> _cachedRoles;
        private static object _roleOptionsInstance;

        public static void InitializeRoles()
        {
            _cachedRoles = new Dictionary<DraftCategory, List<string>>();
            foreach (DraftCategory cat in System.Enum.GetValues(typeof(DraftCategory))) _cachedRoles[cat] = new List<string>();

            Debug.Log("[Draft] Inicjalizacja puli ról (Wczytywanie Configu)...");

            // Przygotowanie refleksji do opcji (aby sprawdzić czy rola jest włączona)
            try {
                var type = System.Type.GetType("TownOfUs.Options.RoleOptions, TownOfUsMira");
                if (type != null) _roleOptionsInstance = type.GetProperty("Instance")?.GetValue(null);
            } catch {}

            var allRoles = CustomRoleManager.AllRoles; 

            foreach (var roleObj in allRoles)
            {
                if (roleObj is ICustomRole iRole)
                {
                    string roleName = iRole.ToString(); // Fix CS1061

                    // --- FILTROWANIE PO CONFIGU ---
                    // Sprawdzamy czy rola jest włączona (Probability > 0)
                    if (!IsRoleEnabledInConfig(roleName)) 
                    {
                        // Debug.Log($"[Draft] Rola {roleName} jest wyłączona w configu. Pomijam.");
                        continue;
                    }

                    if (roleObj is ITownOfUsRole touRole)
                    {
                        DraftCategory cat = MapTouAlignment(touRole.RoleAlignment);
                        _cachedRoles[cat].Add(roleName);
                    }
                    else
                    {
                        if (roleName.Contains("Impostor")) _cachedRoles[DraftCategory.Impostor].Add(roleName);
                        else _cachedRoles[DraftCategory.Crewmate].Add(roleName);
                    }
                }
            }
            
            // Fallbacki żeby pule nie były puste
            if (_cachedRoles[DraftCategory.Crewmate].Count == 0) _cachedRoles[DraftCategory.Crewmate].Add("Crewmate");
            if (_cachedRoles[DraftCategory.Impostor].Count == 0) _cachedRoles[DraftCategory.Impostor].Add("Impostor");
        }

        // Metoda sprawdzająca czy opcja dla danej roli (np. JesterOptions) jest > 0
        private static bool IsRoleEnabledInConfig(string roleName)
        {
            if (_roleOptionsInstance == null) return true; 
            
            try 
            {
                // Nazwy opcji to zazwyczaj "NazwaRoli" + "Options" (np. JesterOptions)
                string sanitizedName = roleName.Replace(" ", "");
                string optionFieldName = sanitizedName + "Options"; 

                // Szukamy pola w RoleOptions
                var optionsField = _roleOptionsInstance.GetType().GetField(optionFieldName);
                if (optionsField == null) optionsField = _roleOptionsInstance.GetType().GetProperty(optionFieldName)?.GetGetMethod()?.DeclaringType.GetField(optionFieldName);

                if (optionsField != null)
                {
                    var roleOptionsObj = optionsField.GetValue(_roleOptionsInstance);
                    if (roleOptionsObj != null)
                    {
                        // Szukamy pola określającego szansę (SpawnChance, Value, Probability)
                        var chanceProp = roleOptionsObj.GetType().GetProperty("SpawnChance");
                        if (chanceProp == null) chanceProp = roleOptionsObj.GetType().GetProperty("Value");
                        if (chanceProp == null) chanceProp = roleOptionsObj.GetType().GetProperty("Probability");

                        if (chanceProp != null)
                        {
                            float val = System.Convert.ToSingle(chanceProp.GetValue(roleOptionsObj));
                            // Jeśli wartość jest 0 lub mniej, rola jest wyłączona
                            if (val <= 0) return false;
                        }
                    }
                }
            }
            catch {}
            
            return true; // Domyślnie włączona jeśli nie znaleziono opcji (dla bezpieczeństwa)
        }

        public static List<string> GetRandomRoles(DraftCategory category, int count)
        {
            if (_cachedRoles == null) InitializeRoles();
            if (!_cachedRoles.ContainsKey(category) || _cachedRoles[category].Count == 0) 
            {
                if(category != DraftCategory.Impostor) return GetRandomRoles(DraftCategory.Crewmate, count);
                return new List<string>{"Impostor"};
            }

            List<string> pool = new List<string>(_cachedRoles[category]);
            List<string> picked = new List<string>();

            for (int i = 0; i < count; i++)
            {
                if (pool.Count == 0) pool = new List<string>(_cachedRoles[category]); 
                int idx = Random.Range(0, pool.Count);
                picked.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return picked;
        }
        
        public static DraftCategory GetRandomCrewmateCategory()
        {
            // Losowanie fallback
            var validCats = new List<DraftCategory> { 
                DraftCategory.Support, DraftCategory.Investigative, DraftCategory.Killing, 
                DraftCategory.Power, DraftCategory.Protective 
            };
            return validCats[Random.Range(0, validCats.Count)];
        }

        private static DraftCategory MapTouAlignment(RoleAlignment alignment)
        {
            switch (alignment)
            {
                case RoleAlignment.CrewmateSupport: return DraftCategory.Support;
                case RoleAlignment.CrewmateInvestigative: return DraftCategory.Investigative;
                case RoleAlignment.CrewmateKilling: return DraftCategory.Killing;
                case RoleAlignment.CrewmatePower: return DraftCategory.Power;
                case RoleAlignment.CrewmateProtective: return DraftCategory.Protective;
                case RoleAlignment.ImpostorKilling: return DraftCategory.Impostor;
                case RoleAlignment.ImpostorConcealing: return DraftCategory.Impostor;
                case RoleAlignment.ImpostorSupport: return DraftCategory.Impostor;
                case RoleAlignment.ImpostorPower: return DraftCategory.Impostor;
                case RoleAlignment.NeutralBenign: return DraftCategory.NeutralBenign;
                case RoleAlignment.NeutralEvil: return DraftCategory.NeutralEvil;
                case RoleAlignment.NeutralKilling: return DraftCategory.NeutralKilling;
                default: return DraftCategory.Crewmate;
            }
        }
    }
}