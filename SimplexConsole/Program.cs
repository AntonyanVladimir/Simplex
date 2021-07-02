using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SimplexConsole
{
    class Program
    {
        private static int anzahlVonVariablen;
        static void Main(string[] args)
        {
            //die Zielfunktion entgegennehmen
            Console.WriteLine("Bitte geben Sie die Zielfunktion ein:  z.B. 300x_1+250x_2 ...");
            var zielfunktionAusDerKonsole = Console.ReadLine();

            //x_1 x_2 entfernen
            var zielFunk = Regex.Replace(zielfunktionAusDerKonsole, "x_[1-9]+", "");
            List<double> zielFunktionsWerte = zielFunk.Split("+").Select(m => (-1)*double.Parse(m)).ToList();

            //Restriktionen entgegennehmen
            Console.WriteLine("Nichtnegativitätsbedingungen wurden automatisch gesetzt.");
            Console.WriteLine($"Bitte geben Sie die weiteren Restriktionen mit Semikolon getrennt ein: " +
                                $" \n z.B. 0.1x_1 + 0.5x_2 <= 30;10x_1+20x_2<=1500;1500x_1+500x_2<=150000");

            var restriktFromConsole = Console.ReadLine();

            string[] geleseneRestriktionen = restriktFromConsole.Split(";");
            var anzahlVonRestriktionen = geleseneRestriktionen.Length;

            List<List<double>> tabelle = new List<List<double>>();
            for (var index = 0; index < geleseneRestriktionen.Length; index++)
            {
                string[] valuesFromRest = Regex.Replace(geleseneRestriktionen[index], "x_[1-9]+", " ")
                    .Replace("<", " ")
                    .Replace("=", "")
                    .Replace("+", " ")
                    .Split(" ");
                var currentRestriktion = new List<double>();
                foreach (var value in valuesFromRest)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        currentRestriktion.Add(double.Parse(value, CultureInfo.InvariantCulture));
                }
                //entsprechend die Schlupfvariablen hinzufügen (abhängig von der Anzahl der Restriktionen)
                var lastElement = currentRestriktion.Last();
                currentRestriktion.Remove(lastElement);

                if (anzahlVonVariablen < currentRestriktion.Count)
                    anzahlVonVariablen = currentRestriktion.Count;
                for (var j = 0; j < anzahlVonRestriktionen; j++)
                {
                    if (j == index)
                        currentRestriktion.Add(1);
                    else
                        currentRestriktion.Add(0);
                }
                currentRestriktion.Add(lastElement);
                tabelle.Add(currentRestriktion);
            }
            //Add Schlupfvariablen in Zielfunktion
            for (var j = 0; j < anzahlVonRestriktionen; j++)
            {
                zielFunktionsWerte.Add(0);
            }
            zielFunktionsWerte.Add(0);
            tabelle.Add(zielFunktionsWerte);

            var optimized = Optimize(tabelle);
            var optimizedValues = GetOptimizedValues(optimized, 2);
            LösungAusgeben(optimizedValues);
        }
        private static List<List<double>> Optimize(List<List<double>> liste)
        {
            var b = new List<double>();
            foreach (var zeile in liste)
                b.Add(zeile.Last());
            b.Remove(b.Last());

            var pivotspaltenIndex = FindPivotspaltenIndex(liste);
            var pivotSpalte = GetPivotspalte(liste, pivotspaltenIndex);

            var pivotzeilenIndex = FindPivotzeilenIndex(pivotSpalte, b);

            NormalizePivotZeile(liste, pivotzeilenIndex, pivotspaltenIndex);
            //Alle Pivotspaltenelemente außer dem Pivotelement auf null bringen
            GaussAnwenden(liste, pivotzeilenIndex, pivotspaltenIndex);

            var zielfunktionszeile = liste.Last();
            while (zielfunktionszeile.Any(m => m < 0))
            {
                Optimize(liste);
            }
            return liste;
        }
        private static Dictionary<string, double> GetOptimizedValues(List<List<double>> liste, int countOfVariables)
        {
            var lastSpaltenIndex = liste[0].Count - 1;
            var loesungsSpalte = GetSpalteAt(liste, lastSpaltenIndex);
            Dictionary<string, double> optValues = new Dictionary<string, double>();
            var optimaleLoesung = Math.Abs(liste.Last().ToList().Last());

            optValues.Add("Optimale Lösung", optimaleLoesung);
            for (int i = 0; i < countOfVariables; i++)
            {
                var currentSpalte = GetSpalteAt(liste, i);
                var indexOfSolution = currentSpalte.IndexOf(1);

                var currentOptValue = Math.Round(loesungsSpalte.ElementAt(indexOfSolution));
                optValues.Add($"x{i + 1}", currentOptValue);
            }

            return optValues;
        }
        private static List<double> GetSpalteAt(List<List<double>> liste, int index)
        {
            var spalte = new List<double>();
            foreach (var zeile in liste)
                spalte.Add(zeile.ElementAt(index));
            return spalte;
        }
        private static List<double> GetPivotspalte(List<List<double>> list, int pivotspaltenIndex)
        {
            var spalte = new List<double>();
            foreach (var zeile in list)
            {
                spalte.Add(zeile.ElementAt(pivotspaltenIndex));
            }
            return spalte;
        }

        private static int FindPivotspaltenIndex(List<List<double>> list)
        {
            var zielfunktionsZeile = list.Last();

            var pivotspaltenEl = list.Last().Where(m => m < 0).Min();

            var pivotspaltenIndex = zielfunktionsZeile.IndexOf(pivotspaltenEl);

            return pivotspaltenIndex;
        }
        private static int FindPivotzeilenIndex(List<double> pivotspalte, List<double> b)
        {
            int minIndex = 0;
            for (var index = 0; index < b.Count - 1; index++)
            {
                minIndex = index;
                //if (pivotspalte[index] != 0)
                //{
                var currentItem = b[index] / pivotspalte[index];
                var nextItem = b[index + 1] / pivotspalte[index + 1];
                if (nextItem < currentItem)
                    minIndex = index + 1;
                // }
            }

            var pivotzeilenEl = pivotspalte.ElementAt(minIndex);
            var pivotzeilElIndex = pivotspalte.IndexOf(pivotzeilenEl);
            return pivotzeilElIndex;
        }
        private static void NormalizePivotZeile(List<List<double>> liste, int pivotzeilenIndex, int pivotspaltenIndex)
        {
            var pivotelement = liste[pivotzeilenIndex].ElementAt(pivotspaltenIndex);

            liste[pivotzeilenIndex] = liste[pivotzeilenIndex].Select(m => m / pivotelement).ToList();
        }
        //Alle Pivotspaltenelemente außer dem Pivotelement auf null bringen
        private static void GaussAnwenden(List<List<double>> liste, int pivotzeilenIndex, int pivotspaltenIndex)
        {
            var pivotzeile = liste[pivotzeilenIndex];

            for (var index = 0; index < liste.Count; index++)
            {
                //für alle Zeile außer dem Pivotzeile Pivotspaltenelement auf null bringen
                if (index != pivotzeilenIndex)
                {
                    var currentZeile = liste[index];
                    var currrentPivotspaltenElement = currentZeile.ElementAt(pivotspaltenIndex);
                    for (var j = 0; j < pivotzeile.Count; j++)
                    {
                        currentZeile[j] = currentZeile[j] - (currrentPivotspaltenElement * pivotzeile[j]);
                    }
                }
            }

        }
        private static void LösungAusgeben(Dictionary<string, double> optimaleLoesung)
        {
            foreach(var keyValue in optimaleLoesung)
            {
                Console.WriteLine($"{keyValue.Key} : {keyValue.Value}");
            }
        }
    }
}
