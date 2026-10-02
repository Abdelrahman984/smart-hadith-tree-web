namespace SmartHadithTree.Domain.Enums;

/// <summary>When a student heard from a mukhtalit narrator, relative to the ikhtilat.</summary>
public enum HearingTiming
{
    Unknown = 0,
    /// <summary>سمع منه قبل الاختلاط — accepted.</summary>
    Before = 1,
    /// <summary>سمع منه بعد الاختلاط — rejected.</summary>
    After = 2
}
