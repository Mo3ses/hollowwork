namespace Hollowwork.Automation;

public readonly record struct PlayerVitals(
    int Hp,
    int MaxHp,
    int Es,
    int MaxEs,
    int Mp,
    int MaxMp)
{
    public double HpPercent => MaxHp > 0 ? Hp * 100d / MaxHp : 0;

    public double EsPercent => MaxEs > 0 ? Es * 100d / MaxEs : 0;

    public double MpPercent => MaxMp > 0 ? Mp * 100d / MaxMp : 0;

    /// <summary>
    /// Slider percentage plus the pool it maps to, e.g. <c>5% · 42</c>. With no known maximum the
    /// percentage stands alone.
    /// </summary>
    public static string DescribeThreshold(double percent, int max)
    {
        var rounded = Math.Round(percent, 1);
        return max > 0
            ? $"{rounded:0.#}% · {Math.Round(max * percent / 100d):0}"
            : $"{rounded:0.#}%";
    }

    public bool IsSane =>
        MaxHp > 0 &&
        Hp >= 0 &&
        Hp <= MaxHp * 2 &&
        MaxMp >= 0 &&
        Mp >= 0 &&
        Mp <= MaxMp * 2 &&
        MaxEs >= 0 &&
        Es >= 0 &&
        Es <= MaxEs * 2;
}
