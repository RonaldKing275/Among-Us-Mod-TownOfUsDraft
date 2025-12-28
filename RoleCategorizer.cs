using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MiraAPI.Roles;
// Nie dodajemy using TownOfUs.Roles, żeby uniknąć błędów typów. Działamy na ogólnym ICustomRole.

namespace TownOfUsDraft
{
    public enum RoleCategory 
    { 
        Crewmate, 
        CrewSupport, 
        CrewInvestigative, 
        CrewKilling, 
        CrewPower, 
        CrewProtective, 
        
        Impostor, 
        RandomImp, 
        
        NeutralBenign, 
        NeutralEvil, 
        NeutralKilling, 
        
        Unknown 
    }

    public static class RoleCategorizer
    {
        private static Dictionary<RoleCategory, List<ICustomRole>> _cachedRoles;
        private static List<ICustomRole> _allValidRoles = new List<ICustomRole>();

        public static void InitializeRoles()
        {
            _cachedRoles = new Dictionary<RoleCategory, List<ICustomRole>>();
            _allValidRoles.Clear();

            foreach (RoleCategory cat in System.Enum.GetValues(typeof(RoleCategory))) 
            {
                if (!_cachedRoles.ContainsKey(cat))
                    _cachedRoles[cat] = new List<ICustomRole>();
            }

            // Dostęp statyczny do listy ról (wg Twoich logów Instance nie istnieje)
            var allRoles = CustomRoleManager.AllRoles; 

            if (allRoles == null) return;

            foreach (var roleBehaviour in allRoles)
            {
                // Rzutowanie RoleBehaviour -> ICustomRole
                ICustomRole customRole = roleBehaviour as ICustomRole;
                
                // Jeśli to nie jest CustomRole, pomijamy (chyba że chcesz obsługiwać Vanilla)
                if (customRole == null) continue;

                // --- DETEKCJA PO NAZWIE KLASY ---
                string fullType = customRole.ToString(); // np. "TownOfUs.Roles.Crewmate.MedicRole"
                
                // Wykrywanie kategorii z tekstu
                RoleCategory cat = DetectCategoryFromText(fullType);

                if (cat != RoleCategory.Unknown)
                {
                    if (!_cachedRoles[cat].Contains(customRole)) _cachedRoles[cat].Add(customRole);
                    
                    // Dodatkowo wrzucamy do worka Impostorów dla "RandomImp"
                    if (cat == RoleCategory.Impostor)
                    {
                        if (!_cachedRoles[RoleCategory.RandomImp].Contains(customRole)) 
                            _cachedRoles[RoleCategory.RandomImp].Add(customRole);
                    }
                    
                    _allValidRoles.Add(customRole);
                }
            }
            
            if (DraftPlugin.Instance != null)
                DraftPlugin.Instance.Log.LogInfo($"[Draft] Załadowano {_allValidRoles.Count} ról.");
        }

        public static List<string> GetRandomRoleNames(RoleCategory category, int count)
        {
            if (_cachedRoles == null || _cachedRoles.Count == 0) InitializeRoles();
            
            if (!_cachedRoles.ContainsKey(category)) return new List<string>();

            var pool = _cachedRoles[category];
            if (pool.Count == 0) return new List<string>();

            return pool.OrderBy(x => Random.Range(0f, 1f))
                       .Take(count)
                       .Select(r => GetPrettyName(r)) // Zamiana obiektu na ładny string
                       .ToList();
        }

        public static ICustomRole GetRoleByName(string roleName)
        {
            if (_allValidRoles.Count == 0) InitializeRoles();
            
            // 1. Szukamy w naszym cache
            foreach (var role in _allValidRoles)
            {
                if (GetPrettyName(role) == roleName)
                    return role;
            }
            
            // 2. Jeśli nie ma w cache, szukamy ręcznie w CustomRoleManager (bo GetRole(string) nie istnieje)
            var allRoles = CustomRoleManager.AllRoles;
            if (allRoles != null)
            {
                foreach (var rb in allRoles)
                {
                    var r = rb as ICustomRole;
                    if (r != null && GetPrettyName(r) == roleName)
                        return r;
                }
            }
                
            return null;
        }

        // Metoda czyszcząca nazwę (np. "TownOfUs.Roles.MedicRole" -> "Medic")
        // Używamy tego zamiast .Name, którego brakuje
        public static string GetPrettyName(ICustomRole role)
        {
            if (role == null) return "Unknown";
            
            string raw = role.ToString();
            
            // Bierzemy ostatni człon po kropce
            if (raw.Contains("."))
            {
                var parts = raw.Split('.');
                raw = parts[parts.Length - 1];
            }
            
            // Usuwamy słowo "Role" z końca, jeśli jest
            if (raw.EndsWith("Role"))
            {
                raw = raw.Substring(0, raw.Length - 4);
            }
            
            return raw;
        }

        private static RoleCategory DetectCategoryFromText(string typeName)
        {
            // Analiza stringa np. "TownOfUs.Roles.Crewmate.Support.MedicRole"
            
            if (typeName.Contains("Crewmate"))
            {
                if (typeName.Contains("Support")) return RoleCategory.CrewSupport;
                if (typeName.Contains("Invest")) return RoleCategory.CrewInvestigative;
                if (typeName.Contains("Killing") || typeName.Contains("Military")) return RoleCategory.CrewKilling;
                if (typeName.Contains("Power")) return RoleCategory.CrewPower;
                if (typeName.Contains("Protect") || typeName.Contains("Medic")) return RoleCategory.CrewProtective;
                
                return RoleCategory.Crewmate; 
            }

            if (typeName.Contains("Impostor"))
            {
                return RoleCategory.Impostor; 
            }

            if (typeName.Contains("Neutral"))
            {
                if (typeName.Contains("Benign")) return RoleCategory.NeutralBenign;
                if (typeName.Contains("Evil")) return RoleCategory.NeutralEvil;
                if (typeName.Contains("Killing")) return RoleCategory.NeutralKilling;
                return RoleCategory.NeutralBenign; 
            }

            // Obsługa Vanilli (jeśli MiraAPI je zwraca pod takimi nazwami)
            if (typeName.Contains("Engineer") || typeName.Contains("Scientist")) return RoleCategory.CrewSupport;
            if (typeName.Contains("Sheriff")) return RoleCategory.CrewKilling;
            if (typeName.Contains("Shapeshifter")) return RoleCategory.Impostor;

            return RoleCategory.Unknown;
        }
    }
}