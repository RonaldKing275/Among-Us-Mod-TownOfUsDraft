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
            // --- IMPOSTORS (tylko TOU-Mira, BEZ vanilla Among Us) ---
            { "GrenadierRole", RoleCategory.RandomImp },
            { "MorphlingRole", RoleCategory.RandomImp },
            { "SwooperRole", RoleCategory.RandomImp },
            { "UndertakerRole", RoleCategory.RandomImp },
            { "JanitorRole", RoleCategory.RandomImp },
            { "MinerRole", RoleCategory.RandomImp },
            { "BlackmailerRole", RoleCategory.RandomImp },
            { "WarlockRole", RoleCategory.RandomImp },
            { "BomberRole", RoleCategory.RandomImp },
            { "EscapistRole", RoleCategory.RandomImp },
            { "VenererRole", RoleCategory.RandomImp },
            { "AmbusherRole", RoleCategory.RandomImp },
            { "HypnotistRole", RoleCategory.RandomImp },
            { "SpellslingerRole", RoleCategory.RandomImp },
            { "AmbassadorRole", RoleCategory.RandomImp },
            { "ScavengerRole", RoleCategory.RandomImp },
            { "EclipsalRole", RoleCategory.RandomImp },
            // USUNIĘTO vanilla Among Us roles: ImpostorRole, NinjaRole, PoisonerRole, ChameleonRole, CamouflagerRole
            // USUNIĘTO role z innych modów: AssassinRole, ConsigliereRole, VampireRole
            // USUNIĘTO rola wtórna: TraitorRole

            // --- CREW INVESTIGATIVE (tylko TOU-Mira, BEZ vanilla Among Us) ---
            { "SeerRole", RoleCategory.CrewInvestigative },
            { "SpyRole", RoleCategory.CrewInvestigative },
            { "SnitchRole", RoleCategory.CrewInvestigative },
            { "LookoutRole", RoleCategory.CrewInvestigative },
            { "InvestigatorRole", RoleCategory.CrewInvestigative },
            { "MysticRole", RoleCategory.CrewInvestigative },
            { "OracleRole", RoleCategory.CrewInvestigative },
            { "ForensicRole", RoleCategory.CrewInvestigative },
            { "AurialRole", RoleCategory.CrewInvestigative },
            { "SonarRole", RoleCategory.CrewInvestigative },
            // USUNIĘTO vanilla Among Us roles: DetectiveRole, TrackerRole, CoronerRole, PsychicRole, MorticianRole

            // --- CREW KILLING ---
            { "SheriffRole", RoleCategory.CrewKilling },
            { "VeteranRole", RoleCategory.CrewKilling },
            { "VigilanteRole", RoleCategory.CrewKilling },
            { "HunterRole", RoleCategory.CrewKilling },

            // --- CREW PROTECTIVE (tylko TOU-Mira, BEZ vanilla Among Us) ---
            { "MedicRole", RoleCategory.CrewProtective },
            { "WardenRole", RoleCategory.CrewProtective },
            { "ClericRole", RoleCategory.CrewProtective },
            { "TrapperRole", RoleCategory.CrewProtective },
            // USUNIĘTO vanilla Among Us roles: GuardianAngelRole

            // --- CREW SUPPORT (tylko TOU-Mira, BEZ vanilla Among Us) ---
            { "EngineerTouRole", RoleCategory.CrewSupport }, // TOU Engineer (nie vanilla!)
            { "TransporterRole", RoleCategory.CrewSupport },
            { "PlumberRole", RoleCategory.CrewSupport },
            { "AltruistRole", RoleCategory.CrewSupport },
            { "MayorRole", RoleCategory.CrewSupport }, 
            { "ImitatorRole", RoleCategory.CrewSupport },
            { "SwapperRole", RoleCategory.CrewSupport },
            { "MediumRole", RoleCategory.CrewSupport },
            // USUNIĘTO vanilla Among Us roles: EngineerRole, MechanicRole, TimeMasterRole

             // --- CREW POWER (tylko TOU-Mira) ---
            { "PoliticianRole", RoleCategory.CrewPower },
            { "ProsecutorRole", RoleCategory.CrewPower },
            { "JailorRole", RoleCategory.CrewPower },
            { "MirrorcasterRole", RoleCategory.CrewPower },
            // USUNIĘTO role które nie istnieją w TOU-Mira: LocksmithRole, DictatorRole

            // --- NEUTRAL EVIL ---
            { "JesterRole", RoleCategory.NeutralEvil },
            { "ExecutionerRole", RoleCategory.NeutralEvil },
            { "DoomsayerRole", RoleCategory.NeutralEvil },

            // --- NEUTRAL KILLING (tylko TOU-Mira) ---
            { "ArsonistRole", RoleCategory.NeutralKilling },
            { "PlaguebearerRole", RoleCategory.NeutralKilling },
            // USUNIĘTO: PestilenceRole - to transformacja Plaguebearer, nie osobna rola do draftu!
            { "WerewolfRole", RoleCategory.NeutralKilling },
            { "GlitchRole", RoleCategory.NeutralKilling },
            { "JuggernautRole", RoleCategory.NeutralKilling },
            { "VampireRole", RoleCategory.NeutralKilling },
            // USUNIĘTO role z innych modów: SerialKillerRole

            // --- NEUTRAL BENIGN (tylko TOU-Mira) ---
            { "AmnesiacRole", RoleCategory.NeutralBenign },
            { "SurvivorRole", RoleCategory.NeutralBenign },
            { "FairyRole", RoleCategory.NeutralBenign },
            { "MercenaryRole", RoleCategory.NeutralBenign },
            { "SoulCollectorRole", RoleCategory.NeutralBenign },
            { "ChefRole", RoleCategory.NeutralBenign },
            { "InquisitorRole", RoleCategory.NeutralBenign },
            { "SpectreRole", RoleCategory.NeutralBenign },
            // USUNIĘTO role z innych modów: LawyerRole, PigeonRole
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