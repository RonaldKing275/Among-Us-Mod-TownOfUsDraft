using System.Collections.Generic;

namespace TownOfUsDraft
{
    public enum RoleCategory
    {
        // Kolejność indeksów w TOU-Mira (0-24)
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
            { "ParasiteRole", RoleCategory.ImpKilling },
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
            { "PuppeteerRole", RoleCategory.ImpPower },
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
            { "MonarchRole", RoleCategory.CrewPower },
            //{ "MayorRole", RoleCategory.CrewPower }, // rola po Politician
            { "PoliticianRole", RoleCategory.CrewPower },
            { "ProsecutorRole", RoleCategory.CrewPower },
            { "SwapperRole", RoleCategory.CrewPower },
            { "TimeLordRole", RoleCategory.CrewPower },
            
            // Crew Support (6) ZATWIERDZONE!
            { "EngineerTouRole", RoleCategory.CrewSupport },
            { "ImitatorRole", RoleCategory.CrewSupport },
            { "MediumRole", RoleCategory.CrewSupport },
            { "PlumberRole", RoleCategory.CrewSupport },
            { "SentryRole", RoleCategory.CrewSupport },
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

        public static RoleCategory GetCategory(string roleName)
        {
            if (RoleMap.ContainsKey(roleName)) return RoleMap[roleName];
            return RoleCategory.Unknown;
        }

        // Konwersja indeksu ze Slotu (RoleListOption z TOU-Mira) na RoleCategory
        public static RoleCategory IndexToCategory(int slotIndex)
        {
            // Mapowanie zgodne z TOU-Mira's RoleListOption enum (TownOfUs/Options/RoleOptions.cs line 303-330)
            switch (slotIndex)
            {
                case 0: return RoleCategory.CommonCrew;      // CrewCommon
                case 1: return RoleCategory.RandomCrew;      // CrewRandom
                case 2: return RoleCategory.CrewInvestigative; // CrewInvest
                case 3: return RoleCategory.CrewKilling;     // CrewKilling
                case 4: return RoleCategory.CrewProtective;  // CrewProtective
                case 5: return RoleCategory.CrewPower;       // CrewPower
                case 6: return RoleCategory.CrewSupport;     // CrewSupport
                case 7: return RoleCategory.SpecialCrew;     // CrewSpecial
                case 8: return RoleCategory.NonImp;          // NonImp (Crew lub Neutral)
                case 9: return RoleCategory.CommonNeutral;   // NeutCommon
                case 10: return RoleCategory.SpecialNeutral; // NeutSpecial
                case 11: return RoleCategory.WildcardNeutral; // NeutWildcard
                case 12: return RoleCategory.RandomNeutral;  // NeutRandom
                case 13: return RoleCategory.NeutralBenign;  // NeutBenign
                case 14: return RoleCategory.NeutralEvil;    // NeutEvil
                case 15: return RoleCategory.NeutralKilling; // NeutKilling
                case 16: return RoleCategory.NeutralOutlier; // NeutOutlier
                case 17: return RoleCategory.CommonImp;      // ImpCommon
                case 18: return RoleCategory.RandomImp;      // ImpRandom
                case 19: return RoleCategory.ImpConcealing;  // ImpConceal
                case 20: return RoleCategory.ImpKilling;     // ImpKilling
                case 21: return RoleCategory.ImpPower;       // ImpPower
                case 22: return RoleCategory.ImpSupport;     // ImpSupport
                case 23: return RoleCategory.SpecialImp;     // ImpSpecial
                case 24: return RoleCategory.Any;            // Any
                default: return RoleCategory.Unknown;
            }
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