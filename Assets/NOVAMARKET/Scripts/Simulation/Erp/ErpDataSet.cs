using System;
using System.Collections.Generic;
using NovaMarket.Simulation.Portfolio;

namespace NovaMarket.Simulation.Erp
{
    /// <summary>A large order of one customer that will not repeat (shown in the billing detail).</summary>
    public sealed class OneOffOrder
    {
        public OneOffOrder(string code, int month, string customer, int units, string note)
        {
            Code = code;
            Month = month;
            Customer = customer;
            Units = units;
            Note = note;
        }

        public string Code { get; }
        public int Month { get; }
        public string Customer { get; }
        public int Units { get; }
        public string Note { get; }
    }

    /// <summary>
    /// What NOVA ERP holds: the sector portfolio plus the billed quantities as the ERP registered
    /// them, with the data problems (and their correct answers) of the level.
    /// </summary>
    public sealed class ErpDataSet
    {
        public const string Units = "UN";
        public const string Boxes = "CJ";

        private readonly Dictionary<string, int[]> billed;
        private readonly Dictionary<string, string> salesUnits;
        private readonly List<DataIssue> issues;
        private readonly List<OneOffOrder> orders;

        public ErpDataSet(Portfolio.Portfolio portfolio, int seed, Dictionary<string, int[]> billed = null,
            Dictionary<string, string> salesUnits = null, List<DataIssue> issues = null, List<OneOffOrder> orders = null)
        {
            Portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
            Seed = seed;
            this.billed = billed ?? new Dictionary<string, int[]>();
            this.salesUnits = salesUnits ?? new Dictionary<string, string>();
            this.issues = issues ?? new List<DataIssue>();
            this.orders = orders ?? new List<OneOffOrder>();
        }

        public Portfolio.Portfolio Portfolio { get; }
        public int Seed { get; }
        public IReadOnlyList<DataIssue> Issues => issues;
        public IReadOnlyList<OneOffOrder> Orders => orders;

        /// <summary>Monthly quantities as billed in the ERP (in <see cref="SalesUnit"/>).</summary>
        public IReadOnlyList<int> Billed(string code) =>
            billed.TryGetValue(code, out int[] values) ? values : Portfolio.Find(code)?.History;

        /// <summary>"UN" (units) or "CJ" (boxes).</summary>
        public string SalesUnit(string code) => salesUnits.TryGetValue(code, out string unit) ? unit : Units;

        public OneOffOrder OrderAt(string code, int month) => orders.Find(o => o.Code == code && o.Month == month);

        public DataIssue IssueAt(string code, int month) => issues.Find(i => i.Covers(code, month));
    }
}
