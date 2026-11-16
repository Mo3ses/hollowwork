using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hollowwork.Automation;

public sealed record EntityOffsets(
    [property: JsonPropertyName("detailsOffset")] int DetailsOffset,
    [property: JsonPropertyName("componentsOffset")] int ComponentsOffset,
    [property: JsonPropertyName("lookupOffset")] int LookupOffset,
    [property: JsonPropertyName("componentMapOffset")] int ComponentMapOffset);

public sealed record LifeOffsets(
    [property: JsonPropertyName("curHp")] int CurHp,
    [property: JsonPropertyName("maxHp")] int MaxHp,
    [property: JsonPropertyName("curMp")] int CurMp,
    [property: JsonPropertyName("maxMp")] int MaxMp,
    [property: JsonPropertyName("curEs")] int CurEs,
    [property: JsonPropertyName("maxEs")] int MaxEs);

public sealed record AutomationProfile(
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("processName")] string ProcessName,
    [property: JsonPropertyName("windowTitle")] string WindowTitle,
    [property: JsonPropertyName("windowClass")] string WindowClass,
    [property: JsonPropertyName("gameRootPattern")] string GameRootPattern,
    [property: JsonPropertyName("gameRootPointerOffset")] int GameRootPointerOffset,
    [property: JsonPropertyName("gameRootAdjust")] int GameRootAdjust,
    [property: JsonPropertyName("inGameStateOffset")] int InGameStateOffset,
    [property: JsonPropertyName("dataOffset")] int DataOffset,
    [property: JsonPropertyName("playerOffset")] int PlayerOffset,
    [property: JsonPropertyName("entity")] EntityOffsets Entity,
    [property: JsonPropertyName("life")] LifeOffsets Life)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AutomationProfile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var profile = JsonSerializer.Deserialize<AutomationProfile>(
            File.ReadAllText(path),
            Options) ?? throw new InvalidDataException("Automation profile is empty.");

        profile.Validate();
        return profile;
    }

    public byte[] PatternBytes()
    {
        var text = GameRootPattern
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        if (text.Length % 2 != 0)
        {
            throw new InvalidDataException("gameRootPattern must contain whole bytes.");
        }

        return Convert.FromHexString(text);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Game) ||
            string.IsNullOrWhiteSpace(ProcessName) ||
            string.IsNullOrWhiteSpace(GameRootPattern))
        {
            throw new InvalidDataException("Automation profile is missing game, processName or gameRootPattern.");
        }

        if (PatternBytes().Length < 4)
        {
            throw new InvalidDataException("gameRootPattern is too short.");
        }

        if (Entity is null || Life is null)
        {
            throw new InvalidDataException("Automation profile is missing entity or life offsets.");
        }
    }
}
