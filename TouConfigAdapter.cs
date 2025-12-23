using AmongUs.GameOptions;
using System.Reflection;
using UnityEngine;
using MiraAPI.GameOptions; // Sprawdź czy to jest odpowiedni namespace dla opcji

namespace TownOfUsDraft
{
    public static class TouConfigAdapter
    {
        // Ta metoda przeszukuje opcje gry, żeby znaleźć ustawione limity
        public static int GetRoleCount(string optionName, int defaultValue = 0)
        {
            // UWAGA: To jest heurystyka. Przeszukujemy listę wszystkich opcji.
            // W MiraAPI opcje są często w GameOptionsManager lub CustomOption.AllOptions
            
            // Przykład szukania w MiraAPI (zależnie od implementacji):
            // Musisz sprawdzić w logach jakie są dostępne opcje, jeśli to nie zadziała.
            
            /* PONIŻEJ KOD TYMCZASOWY - ZAKŁADA STAŁE WARTOŚCI DO TESTÓW */
            /* DOCELOWO: Tu musi być kod czytający z API TOU */
            
            // Na potrzeby testów DRAFTU ustawiamy "sztywne" wartości, które normalnie byłyby w configu
            // Możesz to zmienić na czytanie z pliku konfiguracyjnego BepInEx
            
            if (optionName == "Support") return 2;
            if (optionName == "Protective") return 1;
            if (optionName == "Investigative") return 2;
            if (optionName == "Killing") return 1;
            if (optionName == "NeutralKilling") return 1;
            if (optionName == "NeutralEvil") return 1;
            
            return defaultValue;
        }
    }
}