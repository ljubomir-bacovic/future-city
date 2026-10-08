using Friflo.Engine.ECS;
using FutureCity.Sim.Components;
using FutureCity.Sim.Content;
using FutureCity.Sim.Emergence;

namespace FutureCity.Sim.Systems;

// Building construction sites and working at farms, workshops and shrines.
public sealed partial class OrderSystem
{
    private static void UpdateBuild(World world, Entity unit)
    {
        ref var order = ref unit.GetComponent<Order>();
        ref var citizen = ref unit.GetComponent<Citizen>();
        if (!TryGetOwnBuilding(world, unit, order.Target, out var site) || Buildings.IsComplete(site))
        {
            GiveUp(unit, ref order, citizen);
            return;
        }
        var missing = Buildings.MissingMaterials(world, site);
        if (Logistics(world, unit, site, missing)) return;

        if (citizen.Carried > 0)
        {
            order.Stage = missing[citizen.CarriedGood] > 0 ? OrderStage.Supply : OrderStage.Deliver;
            return;
        }
        if (missing.Any(m => m > 0))
        {
            if (TryFetchFirst(world, unit, ref order, missing)) return;
            // Nothing in store to bring: an organized village sends its automatic builders to gather it instead.
            if (order.Auto && Civics.HasAutoJobs(world, unit.GetComponent<Owner>().Player)) { order = default; return; }
            ApproachEntity(world, unit, site, reach: 1); // wait at the site until materials turn up
            return;
        }

        var arrived = ApproachEntity(world, unit, site, reach: 1);
        if (arrived == Progress.Failed) { GiveUp(unit, ref order, citizen); return; }
        if (arrived == Progress.Underway) return;
        order.Stage = OrderStage.Work;
        ref var construction = ref site.GetComponent<Construction>();
        construction.Work += Labor.Work(world, unit, WorkKind.Build);
        if (construction.Work >= Buildings.TypeOf(world, site).Def.BuildWork * Labor.PerTick)
            Complete(world, site);
    }

    private static void Complete(World world, Entity site)
    {
        site.RemoveComponent<Construction>();
        Array.Clear(site.GetComponent<Inventory>().Amounts); // the materials are now the building
        var pos = site.GetComponent<TilePosition>();
        world.Emit(SimEventKind.BuildingCompleted, site.GetComponent<Owner>().Player, site.Id, pos.X, pos.Y,
            site.GetComponent<Building>().Kind);
    }

    private static void UpdateWork(World world, Entity unit)
    {
        ref var order = ref unit.GetComponent<Order>();
        ref var citizen = ref unit.GetComponent<Citizen>();
        if (!TryGetOwnBuilding(world, unit, order.Target, out var building) || !Buildings.IsComplete(building)
            || !Buildings.TypeOf(world, building).IsWorkplace)
        {
            GiveUp(unit, ref order, citizen);
            return;
        }
        var type = Buildings.TypeOf(world, building);
        if (Logistics(world, unit, building, missing: null)) return;

        if (type.Def.Research > 0) UpdateResearch(world, unit, building, type);
        else if (type.Def.Recipe != null) UpdateWorkshop(world, unit, building, type);
        else if (type.Def.Field != null) UpdateFarm(world, unit, building, type);
        else if (type.Def.Mint != null) UpdateMint(world, unit, building, type);
        else if (type.Def.Market) UpdateMarketWork(world, unit, building);
    }

    private static void UpdateResearch(World world, Entity unit, Entity shrine, BuildingType type)
    {
        ref var order = ref unit.GetComponent<Order>();
        if (unit.GetComponent<Citizen>().Carried > 0) { order.Stage = OrderStage.Deliver; return; }
        int player = unit.GetComponent<Owner>().Player;
        if (Civics.HasAutoJobs(world, player))
        {
            var facts = Civics.FactsOf(world, player);
            if (!Enumerable.Range(0, world.Content.Techs.Count).Any(t => Civics.IsDiscoverable(world, player, t, facts)))
            {
                order = default; // nothing left to think about: find other work
                return;
            }
        }
        if (ApproachEntity(world, unit, shrine, reach: 1) != Progress.Arrived) return;
        order.Stage = OrderStage.Work;
        Civics.RecordWork(world, player, WorkKind.Research, type.Def.Research);
    }

    // Fetch inputs into the workshop, turn them into outputs, carry the outputs to a store.
    private static void UpdateWorkshop(World world, Entity unit, Entity workshop, BuildingType type)
    {
        ref var order = ref unit.GetComponent<Order>();
        ref var citizen = ref unit.GetComponent<Citizen>();
        int capacity = world.Content.Citizens.CarryCapacity;
        if (citizen.Carried > 0)
        {
            order.Stage = type.Inputs[citizen.CarriedGood] > 0 ? OrderStage.Supply : OrderStage.Deliver;
            return;
        }

        var stock = workshop.GetComponent<Inventory>().Amounts;
        bool hasInputs = true;
        int outputsHeld = 0, mostOutput = -1;
        for (int g = 0; g < stock.Length; g++)
        {
            if (stock[g] < type.Inputs[g]) hasInputs = false;
            if (type.Outputs[g] <= 0) continue;
            outputsHeld += stock[g];
            if (mostOutput < 0 || stock[g] > stock[mostOutput]) mostOutput = g;
        }

        if (outputsHeld >= capacity || (outputsHeld > 0 && !hasInputs))
        {
            if (ApproachEntity(world, unit, workshop, reach: 1) != Progress.Arrived) return;
            int take = Math.Min(capacity, stock[mostOutput]);
            stock[mostOutput] -= take;
            citizen.CarriedGood = mostOutput;
            citizen.Carried = take;
            order.Stage = OrderStage.Deliver;
            return;
        }
        if (!hasInputs)
        {
            var wanted = new int[stock.Length];
            for (int g = 0; g < stock.Length; g++) wanted[g] = Math.Max(0, type.Inputs[g] - stock[g]);
            if (TryFetchFirst(world, unit, ref order, wanted)) return;
            // An organized village sends idle workers elsewhere; a family crafter waits for inputs from the market.
            if (Civics.HasAutoJobs(world, unit.GetComponent<Owner>().Player) && !Households.TryGetEmployer(world, unit, out _))
            {
                order = default;
                return;
            }
            ApproachEntity(world, unit, workshop, reach: 1);
            return;
        }

        if (ApproachEntity(world, unit, workshop, reach: 1) != Progress.Arrived) return;
        order.Stage = OrderStage.Work;
        order.Timer += Labor.Work(world, unit, WorkKind.Craft);
        if (order.Timer < type.Def.Recipe!.WorkTicks * Labor.PerTick) return;
        order.Timer = 0;
        int player = unit.GetComponent<Owner>().Player;
        // Guild methods: a share more output per batch, paid out in whole units as it adds up.
        ref var credit = ref workshop.GetComponent<Building>().Credit;
        int bonus = Economy.HasGuilds(world, player) ? world.Content.Economy.Guilds.OutputBonusPercent : 0;
        for (int g = 0; g < stock.Length; g++)
        {
            int made = type.Outputs[g];
            if (made > 0 && bonus > 0)
            {
                credit += made * bonus;
                made += credit / 100;
                credit %= 100;
            }
            stock[g] += made - type.Inputs[g];
            if (made > 0) Civics.RecordProduced(world, player, g, made);
        }
    }

    // Sow in the sowing seasons, wait while the crop grows, and harvest when ripe. The harvest is stacked at the
    // field (safe from rot) and hauled to a store whenever there is no field work to do.
    private static void UpdateFarm(World world, Entity unit, Entity farm, BuildingType type)
    {
        ref var order = ref unit.GetComponent<Order>();
        ref var citizen = ref unit.GetComponent<Citizen>();
        ref var field = ref farm.GetComponent<Field>();
        var def = type.Def.Field!;
        int capacity = world.Content.Citizens.CarryCapacity;
        int player = unit.GetComponent<Owner>().Player;

        if (citizen.Carried > 0)
        {
            order.Stage = OrderStage.Deliver;
            return;
        }
        bool harvesting = field.Stage == FieldStage.Ripe;
        bool sowing = field.Stage == FieldStage.Fallow && Calendar.Season(world).Sowing;
        var stacked = farm.GetComponent<Inventory>().Amounts;
        if (!sowing && !harvesting && stacked[type.FieldGood] > 0)
        {
            if (ApproachEntity(world, unit, farm, reach: 1) != Progress.Arrived) return;
            int take = Math.Min(capacity, stacked[type.FieldGood]);
            stacked[type.FieldGood] -= take;
            citizen.CarriedGood = type.FieldGood;
            citizen.Carried = take;
            order.Stage = OrderStage.Deliver;
            return;
        }
        if (!sowing && !harvesting)
        {
            // Nothing to do on the field this season: an organized village sends people elsewhere meanwhile.
            if (Civics.HasAutoJobs(world, player)) { order = default; return; }
            ApproachEntity(world, unit, farm, reach: 1);
            return;
        }

        if (ApproachEntity(world, unit, farm, reach: 0) != Progress.Arrived) return;
        order.Stage = OrderStage.Work;
        if (sowing)
        {
            field.Progress += Labor.Work(world, unit, WorkKind.Farm);
            if (field.Progress < def.SowWork * Labor.PerTick) return;
            field.Stage = FieldStage.Growing;
            field.Progress = 0;
            return;
        }

        order.Timer += Labor.Work(world, unit, WorkKind.Farm);
        int perUnit = def.HarvestTicksPerUnit * Labor.PerTick;
        while (order.Timer >= perUnit && field.Remaining > 0)
        {
            order.Timer -= perUnit;
            field.Remaining--;
            stacked[type.FieldGood]++;
            Civics.RecordProduced(world, player, type.FieldGood, 1);
        }
        if (field.Remaining == 0)
        {
            field.Stage = FieldStage.Fallow;
            field.Progress = 0;
            field.Fertility = Math.Max(0, field.Fertility - def.FertilityPerHarvest);
        }
    }

    // Shared stages of building and work orders: taking goods to a store, fetching from one, supplying the building.
    // Returns true if the unit is busy with one of them this tick.
    private static bool Logistics(World world, Entity unit, Entity building, int[]? missing)
    {
        ref var order = ref unit.GetComponent<Order>();
        ref var citizen = ref unit.GetComponent<Citizen>();
        switch (order.Stage)
        {
            case OrderStage.Deliver:
            {
                var result = Deliver(world, unit);
                if (result == Progress.Underway) return true;
                if (result == Progress.Failed) { GiveUp(unit, ref order, citizen); return true; }
                order.Stage = OrderStage.Travel;
                return true;
            }
            case OrderStage.Fetch:
            {
                int wanted = missing?[order.Good] ?? world.Content.Citizens.CarryCapacity;
                var result = wanted > 0 ? Fetch(world, unit, wanted) : Progress.Failed;
                if (result == Progress.Underway) return true;
                order.Stage = result == Progress.Arrived ? OrderStage.Supply : OrderStage.Travel;
                return true;
            }
            case OrderStage.Supply:
            {
                if (citizen.Carried == 0) { order.Stage = OrderStage.Travel; return true; }
                var result = ApproachEntity(world, unit, building, reach: 1);
                if (result == Progress.Underway) return true;
                if (result == Progress.Failed) { order.Stage = OrderStage.Deliver; return true; }
                int put = missing == null ? citizen.Carried : Math.Min(citizen.Carried, missing[citizen.CarriedGood]);
                Stores.Put(building, citizen.CarriedGood, put);
                citizen.Carried -= put;
                order.Stage = citizen.Carried > 0 ? OrderStage.Deliver : OrderStage.Travel;
                return true;
            }
            default:
                return false;
        }
    }

    // Starts fetching the first wanted good that the worker can get (from home, or from a reachable public store).
    private static bool TryFetchFirst(World world, Entity unit, ref Order order, int[] wanted)
    {
        for (int g = 0; g < wanted.Length; g++)
        {
            if (wanted[g] <= 0 || !TryFindSupply(world, unit, g, out _)) continue;
            order.Good = g;
            order.Stage = OrderStage.Fetch;
            return true;
        }
        return false;
    }

    private static bool TryGetOwnBuilding(World world, Entity unit, int id, out Entity building) =>
        world.TryGetEntity(id, out building) && building.HasComponent<Building>()
        && building.GetComponent<Owner>().Player == unit.GetComponent<Owner>().Player;
}
