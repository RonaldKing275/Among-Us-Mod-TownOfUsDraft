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

            try {
                var type = System.Type.GetType("TownOfUs.Options.RoleOptions, TownOfUsMira");
                if (type != null) _roleOptionsInstance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            } catch {}

            var allRoles = CustomRoleManager.AllRoles; 

            foreach (var roleObj in allRoles)
            {
                if (roleObj is ICustomRole iRole)
                {
                    string roleName = iRole.ToString();
                    if (!IsRoleEnabledInConfig(roleName)) continue;

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
            if (_cachedRoles[DraftCategory.Crewmate].Count == 0) _cachedRoles[DraftCategory.Crewmate].Add("Crewmate");
            if (_cachedRoles[DraftCategory.Impostor].Count == 0) _cachedRoles[DraftCategory.Impostor].Add("Impostor");
        }
        
        public static bool HasRoles(DraftCategory cat)
        {
             return _cachedRoles != null && _cachedRoles.ContainsKey(cat) && _cachedRoles[cat].Count > 0;
        }

        public static DraftCategory GetRandomCrewmateCategory()
        {
            var validCats = new List<DraftCategory> { 
                DraftCategory.Support, DraftCategory.Investigative, 
                DraftCategory.Killing, DraftCategory.Power, DraftCategory.Protective 
            };
            return validCats[Random.Range(0, validCats.Count)];
        }

        private static bool IsRoleEnabledInConfig(string roleName)
        {
            if (_roleOptionsInstance == null) return true;
            try 
            {
                string sanitizedName = roleName.Replace(" ", "");
                var prop = _roleOptionsInstance.GetType().GetProperty(sanitizedName);
                object optObj = null;
                if (prop != null) optObj = prop.GetValue(_roleOptionsInstance);
                else {
                    var field = _roleOptionsInstance.GetType().GetField(sanitizedName + "Options");
                    if (field != null) optObj = field.GetValue(_roleOptionsInstance);
                }

                if (optObj != null)
                {
                    var chanceProp = optObj.GetType().GetProperty("SpawnChance") ?? optObj.GetType().GetProperty("Value");
                    if (chanceProp != null)
                    {
                        float val = System.Convert.ToSingle(chanceProp.GetValue(optObj));
                        if (val <= 0) return false; 
                    }
                }
            } catch {}
            return true;
        }

        public static List<string> GetRandomRoles(DraftCategory category, int count)
        {
            if (_cachedRoles == null) InitializeRoles();
            if (!HasRoles(category)) 
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