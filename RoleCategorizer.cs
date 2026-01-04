using System.Collections.Generic;

namespace TownOfUsDraft
{
    public enum RoleCategory
    {
        // UWAGA: Kolejność musi odpowiadać indeksom w TOU-Mira (0-24)
        CommonCrew = 0,         // Index 0: Common Crew
        RandomCrew = 1,         // Index 1: Random Crew
        CrewInvestigative = 2,  // Index 2: Crew Investigative
        CrewKilling = 3,        // Index 3: Crew Killing
        CrewProtective = 4,     // Index 4: Crew Protective
        CrewPower = 5,          // Index 5: Crew Power
        CrewSupport = 6,        // Index 6: Crew Support
        SpecialCrew = 7,        // Index 7: Special Crew
        NonImp = 8,             // Index 8: Non-Imp
        CommonNeutral = 9,      // Index 9: Common Neutral
        SpecialNeutral = 10,    // Index 10: Special Neutral
        WildcardNeutral = 11,   // Index 11: Wildcard Neutral
        RandomNeutral = 12,     // Index 12: Random Neutral
        NeutralBenign = 13,     // Index 13: Neutral Benign
        NeutralEvil = 14,       // Index 14: Neutral Evil
        NeutralKilling = 15,    // Index 15: Neutral Killing
        NeutralOutlier = 16,    // Index 16: Neutral Outlier
        CommonImp = 17,         // Index 17: Common Imp
        RandomImp = 18,         // Index 18: Random Imp
        ImpConcealing = 19,     // Index 19: Imp Concealing
        ImpKilling = 20,        // Index 20: Imp Killing
        ImpPower = 21,          // Index 21: Imp Power
        ImpSupport = 22,        // Index 22: Imp Support
        SpecialImp = 23,        // Index 23: Special Imp
        Any = 24,               // Index 24: Any
        Unknown = 99            // Fallback
    }

    public static class RoleCategorizer
    {
        // ⚠️ TODO (v2.0): Zastąpić hardcoded mapę dynamicznym odczytem z TOU-Mira!
        // Zamiast ręcznego mapowania, użyć:
        //   - MiscUtils.AllRegisteredRoles → iteracja po wszystkich rolach
        //   - role.Group.Name → odczyt kategorii z property .Group
        //   - Automatyczne tworzenie mapy przy starcie gry
        // To rozwiąże problem z nowymi rolami w przyszłych wersjach TOU-Mira.
        
        // HARDCODED MAPA: Rola → Kategoria (zgodna z TOU-Mira)
        // Na podstawie dokumentacji TOU-Mira i logów z MiscUtils.AllRegisteredRoles
        public static Dictionary<string, RoleCategory> RoleMap = new Dictionary<string, RoleCategory>()
        {
            // ===== IMPOSTOR ROLES =====

            // Imp Concealing (19) ZATWIERDZONE!
            { "EclipsalRole", RoleCategory.ImpConcealing },
            { "EscapistRole", RoleCategory.ImpConcealing },
            { "GrenadierRole", RoleCategory.ImpConcealing },
            { "MorphlingRole", RoleCategory.ImpConcealing },
            { "SwooperRole", RoleCategory.ImpConcealing },
            { "VenererRole", RoleCategory.ImpConcealing },

            // Imp Killing (20) ZATWIERDZONE!
            { "AmbusherRole", RoleCategory.ImpKilling },
            { "BomberRole", RoleCategory.ImpKilling },
            { "ScavengerRole", RoleCategory.ImpSupport },
            { "WarlockRole", RoleCategory.ImpKilling },
            
            // Imp Support (22) ZATWIERDZONE!
            { "BlackmailerRole", RoleCategory.ImpSupport },
            { "HypnotistRole", RoleCategory.ImpSupport },
            { "JanitorRole", RoleCategory.ImpSupport },
            { "MinerRole", RoleCategory.ImpPower },
            { "UndertakerRole", RoleCategory.ImpSupport },
            
            // Imp Power (21) ZATWIERDZONE!
            { "AmbassadorRole", RoleCategory.ImpPower },
            { "SpellbinderRole", RoleCategory.ImpPower },
            //{ "TraitorRole", RoleCategory.ImpPower }, // Traitor jest specjalny
            

            // ===== CREWMATE ROLES =====

            // Crew Investigative (2) ZATWIERDZONE!
            { "AurialRole", RoleCategory.CrewInvestigative },
            { "ForensicRole", RoleCategory.CrewInvestigative },
            { "HaunterRole", RoleCategory.CrewInvestigative },
            { "InvestigatorRole", RoleCategory.CrewInvestigative },
            { "LookoutRole", RoleCategory.CrewInvestigative },
            { "MysticRole", RoleCategory.CrewInvestigative },
            { "SeerRole", RoleCategory.CrewInvestigative },
            { "SnitchRole", RoleCategory.CrewInvestigative },
            { "SpyRole", RoleCategory.CrewInvestigative },
            { "SonarRole", RoleCategory.CrewInvestigative },
            { "TrapperRole", RoleCategory.CrewInvestigative },
            // { "HaunterRole", RoleCategory.CrewKilling }, // Duch

            // Crew Killing (3) ZATWIERDZONE!
            { "SheriffRole", RoleCategory.CrewKilling },
            { "VeteranRole", RoleCategory.CrewKilling },
            { "VigilanteRole", RoleCategory.CrewKilling },
            { "HunterRole", RoleCategory.CrewKilling },
            { "DeputyRole", RoleCategory.CrewKilling },

            // Crew Protective (4) ZATWIERDZONE!
            { "AltruistRole", RoleCategory.CrewProtective },
            { "ClericRole", RoleCategory.CrewProtective },
            { "MedicRole", RoleCategory.CrewProtective },
            { "MirrorcasterRole", RoleCategory.CrewProtective },
            { "OracleRole", RoleCategory.CrewProtective },
            { "WardenRole", RoleCategory.CrewProtective },
            
            // Crew Power (5) ZATWIERDZONE!
            { "JailorRole", RoleCategory.CrewPower },
            { "MayorRole", RoleCategory.CrewPower },
            { "PoliticianRole", RoleCategory.CrewPower },
            { "ProsecutorRole", RoleCategory.CrewPower },
            { "SwapperRole", RoleCategory.CrewPower },
            
            // Crew Support (6) ZATWIERDZONE!
            { "EngineerTouRole", RoleCategory.CrewSupport },
            { "ImitatorRole", RoleCategory.CrewSupport },
            { "MediumRole", RoleCategory.CrewSupport },
            { "PlumberRole", RoleCategory.CrewSupport },
            { "TransporterRole", RoleCategory.CrewSupport },


            // ===== NEUTRAL ROLES =====

            // Neutral Benign (13) ZATWIERDZONE!
            { "AmnesiacRole", RoleCategory.NeutralBenign },
            { "FairyRole", RoleCategory.NeutralBenign },
            { "MercenaryRole", RoleCategory.NeutralBenign },
            { "SurvivorRole", RoleCategory.NeutralBenign },
            
            // Neutral Evil (14) ZATWIERDZONE!
            { "JesterRole", RoleCategory.NeutralEvil },
            { "ExecutionerRole", RoleCategory.NeutralEvil },
            { "DoomsayerRole", RoleCategory.NeutralEvil },
            //{ "SpectreRole", RoleCategory.NeutralEvil }, // Duch
            
            // Neutral Killing (15) ZATWIERDZONE!
            { "ArsonistRole", RoleCategory.NeutralKilling },
            { "GlitchRole", RoleCategory.NeutralKilling },
            { "JuggernautRole", RoleCategory.NeutralKilling },
            { "PlaguebearerRole", RoleCategory.NeutralKilling },
            //{ "PestilenceRole", RoleCategory.NeutralKilling }, // Pestilence jest specjalny
            { "SoulCollectorRole", RoleCategory.NeutralOutlier },
            { "VampireRole", RoleCategory.NeutralKilling },
            { "WerewolfRole", RoleCategory.NeutralKilling },
            
            // Neutral Outlier (16) ZATWIERDZONE!
            { "ChefRole", RoleCategory.NeutralOutlier },
            { "InquisitorRole", RoleCategory.NeutralOutlier },
            
        };

        // 🚀 PRZYSZŁOŚĆ (v2.0): Dynamiczne ładowanie mapy z TOU-Mira
        // Ta funkcja będzie używana zamiast hardcoded RoleMap
        /*
        public static Dictionary<string, RoleCategory> BuildDynamicRoleMap()
        {
            var map = new Dictionary<string, RoleCategory>();
            
            try
            {
                // 1. Znajdź TOU-Mira assembly
                var touAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "TownOfUsMira");
                
                if (touAssembly == null) return map;
                
                // 2. Pobierz MiscUtils.AllRegisteredRoles
                var miscUtilsType = touAssembly.GetType("TownOfUs.Utilities.MiscUtils");
                var allRolesProp = miscUtilsType?.GetProperty("AllRegisteredRoles", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                
                if (allRolesProp == null) return map;
                
                var allRoles = allRolesProp.GetValue(null) as System.Collections.IEnumerable;
                if (allRoles == null) return map;
                
                // 3. Iteruj po rolach i odczytaj .Group.Name
                foreach (var roleObj in allRoles)
                {
                    if (roleObj == null) continue;
                    
                    try
                    {
                        var roleType = roleObj.GetType();
                        
                        // Nazwa roli (np. "SheriffRole" → "Sheriff")
                        string roleName = roleType.Name;
                        if (roleName.EndsWith("Role"))
                            roleName = roleName.Substring(0, roleName.Length - 4);
                        
                        // Pobierz .Group
                        var groupProp = roleType.GetProperty("Group");
                        if (groupProp == null) continue;
                        
                        var groupObj = groupProp.GetValue(roleObj);
                        if (groupObj == null) continue;
                        
                        // Pobierz .Group.Name
                        var groupNameProp = groupObj.GetType().GetProperty("Name");
                        var groupNameField = groupObj.GetType().GetField("Name");
                        
                        string groupName = null;
                        if (groupNameProp != null)
                            groupName = (string)groupNameProp.GetValue(groupObj);
                        else if (groupNameField != null)
                            groupName = (string)groupNameField.GetValue(groupObj);
                        
                        if (string.IsNullOrEmpty(groupName)) continue;
                        
                        // Mapuj nazwę grupy na RoleCategory
                        RoleCategory category = MapGroupNameToCategory(groupName);
                        
                        if (category != RoleCategory.Unknown)
                        {
                            map[roleName] = category;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            
            return map;
        }
        
        private static RoleCategory MapGroupNameToCategory(string groupName)
        {
            // Mapowanie nazwy grupy (np. "Crew Investigative") na enum RoleCategory
            switch (groupName?.ToLower().Replace(" ", ""))
            {
                case "crewinvestigative": return RoleCategory.CrewInvestigative;
                case "crewkilling": return RoleCategory.CrewKilling;
                case "crewprotective": return RoleCategory.CrewProtective;
                case "crewpower": return RoleCategory.CrewPower;
                case "crewsupport": return RoleCategory.CrewSupport;
                case "neutralbenign": return RoleCategory.NeutralBenign;
                case "neutralevil": return RoleCategory.NeutralEvil;
                case "neutralkilling": return RoleCategory.NeutralKilling;
                case "neutraloutlier": return RoleCategory.NeutralOutlier;
                case "impconcealing": return RoleCategory.ImpConcealing;
                case "impkilling": return RoleCategory.ImpKilling;
                case "imppower": return RoleCategory.ImpPower;
                case "impsupport": return RoleCategory.ImpSupport;
                default: return RoleCategory.Unknown;
            }
        }
        */

        public static RoleCategory GetCategory(string roleName)
        {
            if (RoleMap.ContainsKey(roleName)) return RoleMap[roleName];
            return RoleCategory.Unknown;
        }

        // Konwersja indeksu ze Slotu (0-24) na RoleCategory
        public static RoleCategory IndexToCategory(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex <= 24)
            {
                return (RoleCategory)slotIndex;
            }
            return RoleCategory.Unknown;
        }

        public static List<string> GetRolesInCategory(RoleCategory category, List<string> availableRoles)
        {
            List<string> result = new List<string>();
            
            // Jeśli kategoria to "Any", zwróć wszystkie role
            if (category == RoleCategory.Any)
            {
                return new List<string>(availableRoles);
            }
            
            // Obsługa "Random" i "Common" kategorii - mapujemy na bardziej szczegółowe
            if (category == RoleCategory.RandomImp || category == RoleCategory.CommonImp)
            {
                // Zwróć wszystkie Impostor role
                foreach (var role in availableRoles)
                {
                    var cat = GetCategory(role);
                    if (cat == RoleCategory.ImpConcealing || cat == RoleCategory.ImpKilling || 
                        cat == RoleCategory.ImpSupport || cat == RoleCategory.ImpPower || 
                        cat == RoleCategory.SpecialImp)
                    {
                        result.Add(role);
                    }
                }
                return result;
            }
            
            if (category == RoleCategory.RandomCrew || category == RoleCategory.CommonCrew)
            {
                // Zwróć wszystkie Crewmate role
                foreach (var role in availableRoles)
                {
                    var cat = GetCategory(role);
                    if (cat == RoleCategory.CrewInvestigative || cat == RoleCategory.CrewKilling || 
                        cat == RoleCategory.CrewProtective || cat == RoleCategory.CrewSupport || 
                        cat == RoleCategory.CrewPower || cat == RoleCategory.SpecialCrew)
                    {
                        result.Add(role);
                    }
                }
                return result;
            }
            
            if (category == RoleCategory.RandomNeutral || category == RoleCategory.CommonNeutral)
            {
                // Zwróć wszystkie Neutral role
                foreach (var role in availableRoles)
                {
                    var cat = GetCategory(role);
                    if (cat == RoleCategory.NeutralBenign || cat == RoleCategory.NeutralEvil || 
                        cat == RoleCategory.NeutralKilling || cat == RoleCategory.NeutralOutlier || 
                        cat == RoleCategory.SpecialNeutral || cat == RoleCategory.WildcardNeutral)
                    {
                        result.Add(role);
                    }
                }
                return result;
            }
            
            if (category == RoleCategory.NonImp)
            {
                // Zwróć wszystkie role NIE-Impostor (Crew + Neutral)
                foreach (var role in availableRoles)
                {
                    var cat = GetCategory(role);
                    if (cat != RoleCategory.ImpConcealing && cat != RoleCategory.ImpKilling && 
                        cat != RoleCategory.ImpSupport && cat != RoleCategory.ImpPower && 
                        cat != RoleCategory.SpecialImp && cat != RoleCategory.RandomImp && 
                        cat != RoleCategory.CommonImp)
                    {
                        result.Add(role);
                    }
                }
                return result;
            }
            
            // Dla konkretnych kategorii
            foreach (var role in availableRoles)
            {
                if (GetCategory(role) == category)
                {
                    result.Add(role);
                }
            }
            return result;
        }
    }
}