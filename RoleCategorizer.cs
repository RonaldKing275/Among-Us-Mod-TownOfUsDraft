using System.Collections.Generic;

namespace TownOfUsDraft
{
    public enum RoleCategory
    {
        RandomImp,
        CrewInvestigative,
        CrewKilling,
        CrewProtective,
        NeutralEvil,
        NeutralKilling,
        NeutralBenign,
        CrewSupport,
        CrewPower,
        Unknown
    }

    public static class RoleCategorizer
    {
        private static Dictionary<string, RoleCategory> RoleMap = new Dictionary<string, RoleCategory>()
        {
            // --- IMPOSTORS ---
            { "ImpostorRole", RoleCategory.RandomImp },
            { "AssassinRole", RoleCategory.RandomImp },
            { "NinjaRole", RoleCategory.RandomImp },
            { "PoisonerRole", RoleCategory.RandomImp },
            { "ChameleonRole", RoleCategory.RandomImp },
            { "GrenadierRole", RoleCategory.RandomImp },
            { "MorphlingRole", RoleCategory.RandomImp },
            { "CamouflagerRole", RoleCategory.RandomImp },
            { "SwooperRole", RoleCategory.RandomImp },
            { "UndertakerRole", RoleCategory.RandomImp },
            { "JanitorRole", RoleCategory.RandomImp },
            { "MinerRole", RoleCategory.RandomImp },
            { "ConsigliereRole", RoleCategory.RandomImp },
            { "BlackmailerRole", RoleCategory.RandomImp },
            { "WarlockRole", RoleCategory.RandomImp },
            { "VampireRole", RoleCategory.RandomImp }, 
            // { "TraitorRole", RoleCategory.RandomImp }, // USUNIĘTO - Rola wtórna

            // --- CREW INVESTIGATIVE ---
            { "SeerRole", RoleCategory.CrewInvestigative },
            { "SpyRole", RoleCategory.CrewInvestigative },
            { "SnitchRole", RoleCategory.CrewInvestigative },
            { "TrackerRole", RoleCategory.CrewInvestigative },
            { "DetectiveRole", RoleCategory.CrewInvestigative },
            { "CoronerRole", RoleCategory.CrewInvestigative },
            { "LookoutRole", RoleCategory.CrewInvestigative },
            { "InvestigatorRole", RoleCategory.CrewInvestigative },
            { "PsychicRole", RoleCategory.CrewInvestigative },
            { "MorticianRole", RoleCategory.CrewInvestigative },

            // --- CREW KILLING ---
            { "SheriffRole", RoleCategory.CrewKilling },
            { "VeteranRole", RoleCategory.CrewKilling },
            { "VigilanteRole", RoleCategory.CrewKilling },
            { "HunterRole", RoleCategory.CrewKilling },

            // --- CREW PROTECTIVE ---
            { "MedicRole", RoleCategory.CrewProtective },
            { "WardenRole", RoleCategory.CrewProtective },
            { "GuardianAngelRole", RoleCategory.CrewProtective },

            // --- CREW SUPPORT ---
            { "EngineerRole", RoleCategory.CrewSupport },
            { "TransporterRole", RoleCategory.CrewSupport },
            { "PlumberRole", RoleCategory.CrewSupport },
            { "AltruistRole", RoleCategory.CrewSupport },
            { "MayorRole", RoleCategory.CrewSupport }, 
            { "MechanicRole", RoleCategory.CrewSupport },
            { "TimeMasterRole", RoleCategory.CrewSupport },

             // --- CREW POWER ---
            { "PoliticianRole", RoleCategory.CrewPower },
            { "LocksmithRole", RoleCategory.CrewPower },
            { "DictatorRole", RoleCategory.CrewPower },

            // --- NEUTRAL EVIL ---
            { "JesterRole", RoleCategory.NeutralEvil },
            { "ExecutionerRole", RoleCategory.NeutralEvil },
            { "DoomsayerRole", RoleCategory.NeutralEvil },

            // --- NEUTRAL KILLING ---
            { "ArsonistRole", RoleCategory.NeutralKilling },
            { "PlaguebearerRole", RoleCategory.NeutralKilling },
            { "PestilenceRole", RoleCategory.NeutralKilling },
            { "WerewolfRole", RoleCategory.NeutralKilling },
            { "TheGlitchRole", RoleCategory.NeutralKilling },
            { "JuggernautRole", RoleCategory.NeutralKilling },
            { "SerialKillerRole", RoleCategory.NeutralKilling },

            // --- NEUTRAL BENIGN ---
            { "AmnesiacRole", RoleCategory.NeutralBenign },
            { "SurvivorRole", RoleCategory.NeutralBenign },
            { "LawyerRole", RoleCategory.NeutralBenign },
            { "PigeonRole", RoleCategory.NeutralBenign },
        };

        public static RoleCategory GetCategory(string roleName)
        {
            if (RoleMap.ContainsKey(roleName)) return RoleMap[roleName];
            return RoleCategory.Unknown;
        }

        public static List<string> GetRolesInCategory(RoleCategory category, List<string> availableRoles)
        {
            List<string> result = new List<string>();
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