using FutureCity.Sim.Components;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Commands;

/// <summary>Marks out a construction site. Builders then bring the materials and build it.</summary>
/// <param name="Building">Building type id.</param>
/// <param name="X">Column of the footprint's top corner.</param>
/// <param name="Y">Row of the footprint's top corner.</param>
public sealed record PlaceBuilding(string Building, int X, int Y) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        int kind = world.Content.BuildingIndex(Building ?? "");
        if (kind < 0 || Player == Players.Nature || Buildings.CanPlace(world, Player, kind, X, Y) != Placement.Ok) return;
        Spawn.Building(world, Player, kind, X, Y);
    }
}

/// <summary>Removes one of the player's construction sites; materials already delivered go back to the nearest store.</summary>
/// <param name="Target">Id of the construction site.</param>
public sealed record CancelBuilding(int Target) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!UnitOrders.TryGetOwnBuilding(world, Player, Target, out var site) || Buildings.IsComplete(site)) return;
        var pos = site.GetComponent<TilePosition>();
        var delivered = site.GetComponent<Inventory>().Amounts;
        if (Stores.TryFindNearest(world, Player, pos.X, pos.Y, out var store) || Bands.TryGetCamp(world, Player, out store))
        {
            for (int g = 0; g < delivered.Length; g++) Stores.Put(store, g, delivered[g]);
        }
        site.DeleteEntity();
    }
}

/// <summary>Chooses which technology the player's shrines research ("" for none: research goes to the most advanced one).</summary>
/// <param name="Tech">Technology id, or "".</param>
public sealed record SetResearchFocus(string Tech) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        if (!Civics.TryGet(world, Player, out var civ)) return;
        int tech = string.IsNullOrEmpty(Tech) ? -1 : world.Content.TechIndex(Tech);
        if (tech < 0 && !string.IsNullOrEmpty(Tech)) return;
        civ.GetComponent<Civilization>().ResearchFocus = tech;
    }
}

/// <summary>Establishes an institution whose preconditions hold, paying its cost in food.</summary>
/// <param name="Institution">Institution id.</param>
public sealed record EstablishInstitution(string Institution) : Command
{
    /// <inheritdoc />
    public override void Execute(World world)
    {
        int index = world.Content.InstitutionIndex(Institution ?? "");
        if (index < 0 || !Civics.TryGet(world, Player, out var civ)) return;
        if (!Civics.CanEstablish(world, Player, index, Civics.FactsOf(world, Player))) return;
        Stores.TakeFood(world, Player, world.Content.Institutions[index].Def.FoodCost * 100);
        civ.GetComponent<Civilization>().Institutions[index] = 1;
        Bands.TryGetCamp(world, Player, out var camp);
        var at = camp.IsNull ? default : camp.GetComponent<TilePosition>();
        world.Emit(SimEventKind.InstitutionEstablished, Player, civ.Id, at.X, at.Y, index);
    }
}
