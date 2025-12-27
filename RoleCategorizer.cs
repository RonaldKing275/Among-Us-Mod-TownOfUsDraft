using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MiraAPI.Roles;
using TownOfUs.Roles;

namespace TownOfUsDraft
{
    public enum DraftCategory { Crewmate, Support, Investigative, Killing, Power, Protective, Impostor, NeutralBenign, NeutralEvil, NeutralKilling, Unknown }

    public static class RoleCategorizer
    {
        private static Dictionary<DraftCategory, List<string>> _cachedRoles;

        public static List<string> GetRandomRoles(DraftCategory category, int count)
        {
            if (_cachedRoles == null) InitializeRoles();
            if (!_cachedRoles.ContainsKey(category)) return new List<string>();

            List<string> available = new List<string>(_cachedRoles[category]);
            List<string> picked = new List<string>();

            for (int i = 0; i < count; i++)
            {
                if (available.Count == 0) break;
                int idx = Random.Range(0, available.Count);
                picked.Add(available[idx]);
            }
            return picked;
        }

        private static void InitializeRoles()
        {
            _cachedRoles = new Dictionary<DraftCategory, List<string>>();
            foreach (DraftCategory cat in System.Enum.GetValues(typeof(DraftCategory))) _cachedRoles[cat] = new List<string>();

            Debug.Log("[Draft] Inicjalizacja kategoryzacji...");

            // AllRoles jest statyczne w CustomRoleManager
            var allRoles = CustomRoleManager.AllRoles; 

            foreach (var roleObj in allRoles)
            {
                if (roleObj is ICustomRole iRole)
                {
                    // NAPRAWA: Używamy ToString(), interfejs ICustomRole może nie mieć Name
                    string roleName = iRole.ToString();

                    if (roleObj is ITownOfUsRole touRole)
                    {
                        DraftCategory cat = MapTouAlignment(touRole.RoleAlignment);
                        _cachedRoles[cat].Add(roleName);
                    }
                    else
                    {
                        // Fallback
                        if (roleName.Contains("Impostor")) 
                            _cachedRoles[DraftCategory.Impostor].Add(roleName);
                        else 
                            _cachedRoles[DraftCategory.Crewmate].Add(roleName);
                    }
                }
            }
            
            // Fallbacki
            if (!_cachedRoles[DraftCategory.Crewmate].Contains("Crewmate")) _cachedRoles[DraftCategory.Crewmate].Add("Crewmate");
            if (!_cachedRoles[DraftCategory.Impostor].Contains("Impostor")) _cachedRoles[DraftCategory.Impostor].Add("Impostor");
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