using System;
using System.Collections.Generic;

namespace NovaMarket.Simulation.Erp
{
    /// <summary>Data quality problems of the ERP the player must find before computing (Atlas step p2).</summary>
    public enum DataIssueKind
    {
        /// <summary>A month billed as 0 that was a stock-out or a registration error.</summary>
        FalseZero,

        /// <summary>A one-off order of a single customer that will not repeat.</summary>
        OneOffOrder,

        /// <summary>Zeros before the product existed: they are not demand.</summary>
        PreLaunch,

        /// <summary>A SKU billed in boxes (CJ) while the rest of the query is in units.</summary>
        UnitsInBoxes
    }

    /// <summary>What the player does to a problem: replace the value, leave the month out, or convert units.</summary>
    public enum CorrectionAction
    {
        Impute,
        Exclude,
        Convert
    }

    /// <summary>
    /// One problem planted in (or found in) the ERP data: SKU, months, raw values and the clean values
    /// the game accepts. <see cref="AcceptedValues"/> holds one or more full answers (one value per
    /// month, null = month excluded); the player's series must match one of them.
    /// </summary>
    public sealed class DataIssue
    {
        public DataIssue(DataIssueKind kind, string code, IReadOnlyList<int> months, IReadOnlyList<int> rawValues,
            IReadOnlyList<double?[]> acceptedValues, bool isReal, string explanation, string atlasStep)
        {
            if (months == null || months.Count == 0) throw new ArgumentException("An issue needs months.", nameof(months));
            if (acceptedValues == null || acceptedValues.Count == 0) throw new ArgumentException("An issue needs an answer.");
            foreach (double?[] answer in acceptedValues)
                if (answer.Length != months.Count) throw new ArgumentException("Every answer needs one value per month.");
            Kind = kind;
            Code = code;
            Months = months;
            RawValues = rawValues;
            AcceptedValues = acceptedValues;
            IsReal = isReal;
            Explanation = explanation;
            AtlasStep = atlasStep;
        }

        public DataIssueKind Kind { get; }
        public string Code { get; }

        /// <summary>History indices affected (0 = ene-23).</summary>
        public IReadOnlyList<int> Months { get; }

        /// <summary>What the ERP shows for each month.</summary>
        public IReadOnlyList<int> RawValues { get; }

        /// <summary>Correct clean values; the first one is the model answer.</summary>
        public IReadOnlyList<double?[]> AcceptedValues { get; }

        /// <summary>True for problems that exist in the Atlas history itself (not planted by the game).</summary>
        public bool IsReal { get; }

        public string Explanation { get; }
        public string AtlasStep { get; }

        public CorrectionAction ExpectedAction => Kind switch
        {
            DataIssueKind.PreLaunch => CorrectionAction.Exclude,
            DataIssueKind.UnitsInBoxes => CorrectionAction.Convert,
            _ => CorrectionAction.Impute
        };

        public string KindName => Name(Kind);

        public static string Name(DataIssueKind kind) => kind switch
        {
            DataIssueKind.FalseZero => "Cero que no es real",
            DataIssueKind.OneOffOrder => "Pedido atípico de un cliente",
            DataIssueKind.PreLaunch => "Meses antes del lanzamiento",
            _ => "Unidades mezcladas con cajas"
        };

        public static string Name(CorrectionAction action) => action switch
        {
            CorrectionAction.Impute => "Imputar",
            CorrectionAction.Exclude => "Excluir",
            _ => "Convertir"
        };

        public bool Covers(string code, int month) => code == Code && Contains(month);

        private bool Contains(int month)
        {
            foreach (int m in Months) if (m == month) return true;
            return false;
        }
    }
}
