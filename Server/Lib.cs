using SpacetimeDB;

public static partial class Module
{
    [Table(Name = "spawn_food_timer", Scheduled = nameof(SpawnFood), ScheduledAt = nameof(scheduled_at))]
    public partial struct SpawnFoodTimer
    {
        [PrimaryKey, AutoInc]
        public ulong scheduled_id;
        public ScheduleAt scheduled_at;
    }

    // We're using this table as a singleton, so in this table
    // there only be one element where the `id` is 0.
    [Table(Name = "config", Public = true)]
    public partial struct Config
    {
        [PrimaryKey]
        public uint id;
        public ulong world_size;
    }

    [Type]
    public partial struct DbVector2(float x, float y)
    {
        public float x = x;
        public float y = y;
    }

    [Table(Name = "entity", Public = true)]
    public partial struct Entity
    {
        [PrimaryKey, AutoInc]
        public uint entity_id;
        public DbVector2 position;
        public uint mass;
    }

    [Table(Name = "circle", Public = true)]
    public partial struct Circle
    {
        [PrimaryKey]
        public uint entity_id;
        [SpacetimeDB.Index.BTree]
        public uint player_id;
        public DbVector2 direction;
        public float speed;
        public ulong last_split_time;
    }

    [Table(Name = "food", Public = true)]
    public partial struct Food
    {
        [PrimaryKey]
        public uint entity_id;
    }

    [Table(Name = "player", Public = true)]
    [Table(Name = "logged_out_player")]
    public partial struct Player
    {
        [PrimaryKey]
        public Identity identity;
        [Unique, AutoInc]
        public uint player_id;
        public string name;
    }

    [Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        Log.Info($"Initializing...");
        ctx.Db.config.Insert(new Config { world_size = 1000 });
        ctx.Db.spawn_food_timer.Insert(new SpawnFoodTimer
        {
            scheduled_at = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(500))
        });
    }

    [Reducer(ReducerKind.ClientConnected)]
    public static void Connect(ReducerContext ctx)
    {
        var player = ctx.Db.logged_out_player.identity.Find(ctx.Sender);
        if (player != null)
        {
            ctx.Db.player.Insert(player.Value);
            ctx.Db.logged_out_player.identity.Delete(player.Value.identity);
        }
        else
        {
            ctx.Db.player.Insert(new Player
            {
                identity = ctx.Sender,
                name = "",
            });
        }
    }

    [Reducer(ReducerKind.ClientDisconnected)]
    public static void Disconnect(ReducerContext ctx)
    {
        var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
        // Remove any circles from the arena
        foreach (var circle in ctx.Db.circle.player_id.Filter(player.player_id))
        {
            var entity = ctx.Db.entity.entity_id.Find(circle.entity_id) ?? throw new Exception("Could not find circle");
            ctx.Db.entity.entity_id.Delete(entity.entity_id);
            ctx.Db.circle.entity_id.Delete(entity.entity_id);
        }
        ctx.Db.logged_out_player.Insert(player);
        ctx.Db.player.identity.Delete(player.identity);
    }

    private const uint FOOD_MASS_MIN = 2;
    private const uint FOOD_MASS_MAX = 4;
    private const uint TARGET_FOOD_COUNT = 600;

    public static float MassToRadius(uint mass) => MathF.Sqrt(mass);

    [Reducer]
    public static void SpawnFood(ReducerContext ctx, SpawnFoodTimer _timer)
    {
        if (ctx.Db.player.Count == 0)
        {
            return;
        }

        var world_size = (ctx.Db.config.id.Find(0) ?? throw new Exception("Config not found")).world_size;
        var rng = ctx.Rng;
        var food_count = ctx.Db.food.Count;
        while (food_count < TARGET_FOOD_COUNT)
        {
            var food_mass = rng.Range(FOOD_MASS_MIN, FOOD_MASS_MAX);
            var food_radius = MassToRadius(food_mass);
            var x = rng.Range(food_radius, world_size - food_radius);
            var y = rng.Range(food_radius, world_size - food_radius);
            var entity = ctx.Db.entity.Insert(new Entity()
            {
                position = new DbVector2(x, y),
                mass = food_mass,
            });
            ctx.Db.food.Insert(new Food
            {
                entity_id = entity.entity_id,
            });
            food_count++;
            Log.Info($"Spawned food! {entity.entity_id}");
        }
    }

    public static float Range(this Random rng, float min, float max) => rng.NextSingle() * (max - min) + min;

    public static uint Range(this Random rng, uint min, uint max) => (uint)rng.NextInt64(min, max);

    private const uint START_PLAYER_MASS = 15;

    [Reducer]
    public static void EnterGame(ReducerContext ctx, string name)
    {
        Log.Info($"Creating player with name {name}");
        var player = ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found");
        player.name = name;
        ctx.Db.player.identity.Update(player);
        SpawnPlayerInitialCircle(ctx, player.player_id);
    }

    public static Entity SpawnPlayerInitialCircle(ReducerContext ctx, uint player_id)
    {
        var rng = ctx.Rng;
        var world_size = (ctx.Db.config.id.Find(0) ?? throw new Exception("Config not found")).world_size;
        var player_start_radius = MassToRadius(START_PLAYER_MASS);
        var x = rng.Range(player_start_radius, world_size - player_start_radius);
        var y = rng.Range(player_start_radius, world_size - player_start_radius);
        return SpawnCircleAt(
            ctx,
            player_id,
            START_PLAYER_MASS,
            new DbVector2(x, y),
            ctx.Timestamp
        );
    }

    public static Entity SpawnCircleAt(ReducerContext ctx, uint player_id, uint mass, DbVector2 position, DateTimeOffset timestamp)
    {
        var entity = ctx.Db.entity.Insert(new Entity
        {
            position = position,
            mass = mass,
        });
        ctx.Db.circle.Insert(new Circle
        {
            entity_id = entity.entity_id,
            player_id = player_id,
            direction = new DbVector2(0, 1),
            speed = 0f,
            last_split_time = (ulong)timestamp.ToUnixTimeMilliseconds(),
        });
        return entity;
    }
}
