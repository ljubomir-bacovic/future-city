using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;

namespace FutureCity.Sim.Emergence;

/// <summary>Reading and recording a civilization's state: facts for conditions, knowledge, institutions and activity.</summary>
public static class Civics
{
    /// <summary>Finds the player's civilization entity.</summary>
    public static bool TryGet(World world, int player, out Entity civilization)
    {
        if (world.CivilizationIds.TryGetValue(player, out int id) && world.TryGetEntity(id, out civilization)
            && civilization.HasComponent<Civilization>())
            return true;
        foreach (var entity in World.InIdOrder(world.Store.Query<Civilization, Owner>()))
        {
            if (entity.GetComponent<Owner>().Player != player) continue;
            world.CivilizationIds[player] = entity.Id;
            civilization = entity;
            return true;
        }
        civilization = default;
        return false;
    }

    /// <summary>A snapshot of everything conditions can ask about the player.</summary>
    public static Facts FactsOf(World world, int player)
    {
        var census = Bands.CensusOf(world, player);
        var store = Economy.Holdings(world, player);
        var content = world.Content;
        bool has = TryGet(world, player, out var entity);
        var civ = has ? entity.GetComponent<Civilization>() : default;
        return new Facts
        {
            Population = census.Total,
            Adults = census.Adults,
            Children = census.Children,
            Food = Stores.MealsIn(world, store),
            Shelter = Buildings.ShelterOf(world, player),
            Era = civ.Era,
            Store = store,
            Gathered = civ.Gathered ?? new int[content.Goods.Count],
            Produced = civ.Produced ?? new int[content.Goods.Count],
            Buildings = Buildings.CountCompleted(world, player),
            Techs = civ.Techs ?? new int[content.Techs.Count],
            Institutions = civ.Institutions ?? new int[content.Institutions.Count],
            Coins = Economy.MoneySupply(world, player),
            Trades = civ.Trades,
            Happiness = Society.AverageHappiness(world, player),
            Classes = Society.Count(world, player),
            Soldiers = Military.Count(world, player),
        };
    }

    /// <summary>The "what is missing" list for a condition, with readable labels.</summary>
    public static IReadOnlyList<ConditionPart> Explain(World world, Condition condition, Facts facts) =>
        condition.Explain(facts, world.Content.FactLabel);

    /// <summary>Whether the player knows a technology.</summary>
    public static bool Knows(World world, int player, int tech) =>
        TryGet(world, player, out var civ) && civ.GetComponent<Civilization>().Techs[tech] != 0;

    /// <summary>Whether the player has established an institution.</summary>
    public static bool Has(World world, int player, int institution) =>
        TryGet(world, player, out var civ) && civ.GetComponent<Civilization>().Institutions[institution] != 0;

    /// <summary>Whether a technology is unknown but its preconditions hold, so work can lead to its discovery.</summary>
    public static bool IsDiscoverable(World world, int player, int tech, Facts facts) =>
        !Knows(world, player, tech) && world.Content.Techs[tech].Preconditions.IsMet(facts);

    /// <summary>Whether the player could establish an institution now (preconditions and food cost).</summary>
    public static bool CanEstablish(World world, int player, int institution, Facts facts)
    {
        var type = world.Content.Institutions[institution];
        return !Has(world, player, institution) && type.Preconditions.IsMet(facts) && facts.Food >= type.Def.FoodCost;
    }

    /// <summary>Whether an established institution lets the player's idle people find work on their own.</summary>
    public static bool HasAutoJobs(World world, int player)
    {
        if (!TryGet(world, player, out var civ)) return false;
        var established = civ.GetComponent<Civilization>().Institutions;
        for (int i = 0; i < established.Length; i++)
        {
            if (established[i] != 0 && world.Content.Institutions[i].Def.AutoJobs) return true;
        }
        return false;
    }

    /// <summary>The civilization's current era.</summary>
    public static EraType EraOf(World world, int player) =>
        world.Content.Eras[TryGet(world, player, out var civ) ? civ.GetComponent<Civilization>().Era : 0];

    internal static void RecordWork(World world, int player, WorkKind kind, int ticks = 1)
    {
        if (TryGet(world, player, out var civ)) civ.GetComponent<Civilization>().Work[(int)kind] += ticks;
    }

    internal static void RecordGathered(World world, int player, int good, int amount)
    {
        if (TryGet(world, player, out var civ)) civ.GetComponent<Civilization>().Gathered[good] += amount;
    }

    internal static void RecordProduced(World world, int player, int good, int amount)
    {
        if (TryGet(world, player, out var civ)) civ.GetComponent<Civilization>().Produced[good] += amount;
    }

    /// <summary>A fresh civilization component sized for the content.</summary>
    internal static Civilization NewCivilization(ContentDatabase content) => new()
    {
        Era = 0,
        ResearchFocus = -1,
        Techs = new int[content.Techs.Count],
        TechProgress = new int[content.Techs.Count],
        Institutions = new int[content.Institutions.Count],
        Gathered = new int[content.Goods.Count],
        Produced = new int[content.Goods.Count],
        Work = new int[Enum.GetValues<WorkKind>().Length],
        LastWork = new int[Enum.GetValues<WorkKind>().Length],
        CoinQuality = 100,
        TributePercent = content.Economy.Taxes.Tribute.Default,
        MarketTaxPercent = content.Economy.Taxes.MarketTax.Default,
        TariffPercent = content.Economy.Taxes.Tariff.Default,
        Ledger = new int[Enum.GetValues<LedgerEntry>().Length],
        LastLedger = new int[Enum.GetValues<LedgerEntry>().Length],
        GuildCap = new int[content.Buildings.Count],
    };
}
