using System;

namespace IMSAT.Road.Core.Standards
{
    public enum CriterionStatus { Pass, Fail, NotEvaluated }

    public sealed class CriterionResult
    {
        public string Name { get; set; }
        public double? Station { get; set; }
        public double? Actual { get; set; }
        public double? RequiredLimit { get; set; }
        public string SourceReference { get; set; }
        public CriterionStatus Status { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Limits are supplied by an audited standard table, never guessed.</summary>
    public static class DesignCriterion
    {
        public static CriterionResult CheckMin(
            string name, double? station, double actual, double? minimum, string sourceReference)
        {
            return Evaluate(name, station, actual, minimum, sourceReference, true);
        }

        public static CriterionResult CheckMax(
            string name, double? station, double actual, double? maximum, string sourceReference)
        {
            return Evaluate(name, station, actual, maximum, sourceReference, false);
        }

        private static CriterionResult Evaluate(string name, double? station, double actual,
            double? limit, string source, bool minimum)
        {
            if (double.IsNaN(actual) || double.IsInfinity(actual))
                throw new ArgumentOutOfRangeException(nameof(actual));
            bool verified = limit.HasValue && !double.IsNaN(limit.Value) &&
                            !double.IsInfinity(limit.Value) && !string.IsNullOrWhiteSpace(source);
            return new CriterionResult
            {
                Name = name,
                Station = station,
                Actual = actual,
                RequiredLimit = limit,
                SourceReference = source ?? "",
                Status = !verified ? CriterionStatus.NotEvaluated :
                    (minimum ? actual >= limit.Value : actual <= limit.Value)
                    ? CriterionStatus.Pass : CriterionStatus.Fail,
                Note = verified ? "" : "Chua co gioi han da doi chieu va dan nguon dieu khoan; khong duoc bao DAT."
            };
        }
    }
}
