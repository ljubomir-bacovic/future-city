using Friflo.Engine.ECS;

namespace FutureCity.Sim.Components;

/// <summary>
/// Goods held by a store, a construction site (materials delivered) or a workshop (inputs and outputs),
/// indexed by good (see <see cref="Content.ContentDatabase.Goods"/>).
/// </summary>
[ComponentKey("inventory")]
public struct Inventory : IComponent
{
    /// <summary>Units of each good.</summary>
    public int[] Amounts;

    /// <summary>An empty inventory for <paramref name="goods"/> kinds of goods.</summary>
    public static Inventory Empty(int goods) => new() { Amounts = new int[goods] };
}

/// <summary>A building. Its <see cref="TilePosition"/> is the top corner of its square footprint.</summary>
[ComponentKey("building")]
public struct Building : IComponent
{
    /// <summary>Index into <see cref="Content.ContentDatabase.Buildings"/>.</summary>
    public int Kind;
    /// <summary>A workshop's extra output under guilds still to come, in hundredths of a unit.</summary>
    public int Credit;
    /// <summary>Damage taken; at the type's hit points the building is destroyed. Builders repair it.</summary>
    public int Damage;
    /// <summary>For a tower: tick of its next possible shot.</summary>
    public long ReadyTick;
}

/// <summary>Present while a building is still being built; removed when it is complete.</summary>
[ComponentKey("construction")]
public struct Construction : IComponent
{
    /// <summary>Work done, in hundredths of a tick of plain work.</summary>
    public int Work;
}

/// <summary>Growth stage of a farm field.</summary>
public enum FieldStage
{
    /// <summary>Unsown; the soil recovers.</summary>
    Fallow,
    /// <summary>Sown and growing.</summary>
    Growing,
    /// <summary>Ripe and waiting for the harvest.</summary>
    Ripe,
}

/// <summary>A farm field: its crop and its soil.</summary>
[ComponentKey("field")]
public struct Field : IComponent
{
    /// <summary>Growth stage.</summary>
    public FieldStage Stage;
    /// <summary>Sowing work while fallow, or growth while growing, in hundredths of a tick.</summary>
    public int Progress;
    /// <summary>Crop left to harvest while ripe.</summary>
    public int Remaining;
    /// <summary>Current soil fertility in percent; harvests lower it.</summary>
    public int Fertility;
    /// <summary>The soil's natural fertility; a fallow field recovers up to this.</summary>
    public int NaturalFertility;
}
