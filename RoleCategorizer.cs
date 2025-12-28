using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MiraAPI.Roles;
using TownOfUs.Roles; // Wymagane: Dodaj referencję do TownOfUsMira.dll

namespace TownOfUsDraft
{
    // Definicja kategorii
    public enum DraftCategory { Crewmate, Support, Investigative, Killing, Power, Protective, Impostor, NeutralBenign, NeutralEvil, NeutralKilling, Unknown }

    public static class RoleCategorizer
    {
        // ZMIANA: Przechowujemy obiekty ICustomRole, a nie stringi!
        private static Dictionary<DraftCategory, List<ICustomRole>> _cachedRoles;
        private static List<ICustomRole> _allValidRoles = new List<ICustomRole>();

        public static void InitializeRoles()
        {
            _cachedRoles = new Dictionary<DraftCategory, List<ICustomRole>>();
            _allValidRoles.Clear();

            foreach (DraftCategory cat in System.Enum.GetValues(typeof(DraftCategory))) 
                _cachedRoles[cat] = new List<ICustomRole>();

            // Pobieramy prawdziwe obiekty ról z MiraAPI
            foreach (var roleObj in CustomRoleManager.Instance.AllRoles)
            {
                if (roleObj.IsHidden) continue;

                if (roleObj is ITownOfUsRole touRole)
                {
                    // Mapujemy kategorię z Town Of Us na nasze enumy
                    DraftCategory cat = MapTouAlignment(touRole.Alignment);
                    
                    if (!_cachedRoles[cat].Contains(roleObj))
                    {
                        _cachedRoles[cat].Add(roleObj);
                        _allValidRoles.Add(roleObj);
                    }
                }
            }
            
            Debug.Log($"[Draft] Załadowano {_allValidRoles.Count} ról.");
        }

        public static ICustomRole GetRandomRole(DraftCategory category)
        {
            if (_cachedRoles == null) InitializeRoles();
            if (!_cachedRoles.ContainsKey(category)) return null;
            var pool = _cachedRoles[category];
            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        // Metoda do losowania dowolnej roli (dla przycisku Random)
        public static ICustomRole GetRandomRoleAny()
        {
            if (_allValidRoles == null || _allValidRoles.Count == 0) InitializeRoles();
            if (_allValidRoles.Count == 0) return null;
            return _allValidRoles[Random.Range(0, _allValidRoles.Count)];
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