using System.Text;

namespace Hollowwork.Core.Market;

/// <summary>
/// Friendly names for the metadata paths returned by the public currency
/// exchange. The API only exposes internal paths, so common currencies get
/// an explicit name and everything else falls back to a readable leaf.
/// </summary>
public static class CurrencyNames
{
    private static readonly IReadOnlyDictionary<string, string> Known =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Metadata/Items/Currency/CurrencyRerollRare"] = "Chaos Orb",
            ["Metadata/Items/Currency/CurrencyModValues"] = "Divine Orb",
            ["Metadata/Items/Currency/CurrencyAddModToRare"] = "Exalted Orb",
            ["Metadata/Items/Currency/CurrencyUpgradeToRare"] = "Orb of Alchemy",
            ["Metadata/Items/Currency/CurrencyRerollMagic"] = "Orb of Alteration",
            ["Metadata/Items/Currency/CurrencyUpgradeToMagic"] = "Orb of Transmutation",
            ["Metadata/Items/Currency/CurrencyRerollSocketLinks"] = "Orb of Fusing",
            ["Metadata/Items/Currency/CurrencyRerollSocketNumbers"] = "Jeweller's Orb",
            ["Metadata/Items/Currency/CurrencyRerollSocketColours"] = "Chromatic Orb",
            ["Metadata/Items/Currency/CurrencyIdentification"] = "Scroll of Wisdom",
            ["Metadata/Items/Currency/CurrencyPortal"] = "Portal Scroll",
            ["Metadata/Items/Currency/CurrencyCorrupt"] = "Vaal Orb",
            ["Metadata/Items/Currency/CurrencyPassiveRefund"] = "Orb of Regret",
            ["Metadata/Items/Currency/CurrencyConvertToNormal"] = "Orb of Scouring",
            ["Metadata/Items/Currency/CurrencyGemQuality"] = "Gemcutter's Prism",
            ["Metadata/Items/Currency/CurrencyArmourQuality"] = "Armourer's Scrap",
            ["Metadata/Items/Currency/CurrencyWeaponQuality"] = "Blacksmith's Whetstone",
            ["Metadata/Items/Currency/CurrencyFlaskQuality"] = "Glassblower's Bauble",
            ["Metadata/Items/Currency/CurrencyMapQuality"] = "Cartographer's Chisel",
            ["Metadata/Items/Currency/CurrencyDuplicate"] = "Mirror of Kalandra",
            ["Metadata/Items/Currency/CurrencyInstillingOrb"] = "Instilling Orb",
            ["Metadata/Items/Currency/CurrencyUpgradeRandomly"] = "Orb of Chance",
            ["Metadata/Items/Heist/HeistCoin"] = "Rogue's Marker",
            ["Metadata/Items/DivinationCards/DivinationCardDeck"] = "Stacked Deck",
            ["Metadata/Items/Currency/HarvestSeedGreen"] = "Vivid Lifeforce",
            ["Metadata/Items/Currency/HarvestSeedBlue"] = "Primal Lifeforce",
            ["Metadata/Items/Currency/HarvestSeedRed"] = "Sacred Lifeforce",
        };

    private static readonly IReadOnlyDictionary<string, string> Poe2Known =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Metadata/Items/Currency/CurrencyUpgradeToRare"] = "Regal Orb",
            ["Metadata/Items/Currency/CurrencyUpgradeToRare2"] = "Greater Regal Orb",
            ["Metadata/Items/Currency/CurrencyUpgradeToRare3"] = "Perfect Regal Orb",
            ["Metadata/Items/Currency/CurrencyRerollRare2"] = "Greater Chaos Orb",
            ["Metadata/Items/Currency/CurrencyRerollRare3"] = "Perfect Chaos Orb",
            ["Metadata/Items/Currency/CurrencyAddModToRare2"] = "Greater Exalted Orb",
            ["Metadata/Items/Currency/CurrencyAddModToRare3"] = "Perfect Exalted Orb",
            ["Metadata/Items/Currency/CurrencyRerollMagic2"] = "Greater Orb of Alteration",
            ["Metadata/Items/Currency/CurrencyRerollMagic3"] = "Perfect Orb of Alteration",
            ["Metadata/Items/Currency/CurrencyUpgradeToMagic2"] = "Greater Orb of Transmutation",
            ["Metadata/Items/Currency/CurrencyUpgradeToMagic3"] = "Perfect Orb of Transmutation",
            ["Metadata/Items/Currency/CurrencyRemoveMod"] = "Orb of Annulment",
        };

    public static string Resolve(string metadataPath, bool poe2 = false)
    {
        if (poe2 && Poe2Known.TryGetValue(metadataPath, out var poe2Name))
        {
            return poe2Name;
        }

        if (Known.TryGetValue(metadataPath, out var known))
        {
            return known;
        }

        var slash = metadataPath.LastIndexOf('/');
        var leaf = slash >= 0 ? metadataPath[(slash + 1)..] : metadataPath;
        if (leaf.StartsWith("Currency", StringComparison.Ordinal))
        {
            leaf = leaf["Currency".Length..];
        }

        if (leaf.Length == 0)
        {
            return metadataPath;
        }

        var builder = new StringBuilder(leaf.Length + 8);
        for (var i = 0; i < leaf.Length; i++)
        {
            var c = leaf[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(leaf[i - 1]) &&
                builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    public static string Short(string name)
    {
        if (name.EndsWith(" Orb", StringComparison.Ordinal))
        {
            return name[..^4];
        }

        if (name.StartsWith("Orb of ", StringComparison.Ordinal))
        {
            return name["Orb of ".Length..];
        }

        if (name.StartsWith("Scroll of ", StringComparison.Ordinal))
        {
            return name["Scroll of ".Length..];
        }

        return name;
    }
}
