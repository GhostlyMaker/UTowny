using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenMod.API.Eventing;
using OpenMod.API.Plugins;
using UTowny.Caching;
using UTowny.Configuration;
using UTowny.Domain.Common;
using UTowny.Persistence.Database;
using UTowny.Persistence.Repositories;
using UTowny.Services;

internal static class CreationRegression
{
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "utowny-creation-" + Guid.NewGuid());
        try
        {
            var options = Options.Create(new UTownyOptions());
            options.Value.Economy.StartingBalance = 0;
            var db = new DatabaseConnectionFactory(directory, options);
            await new SchemaMigrator(db, NullLogger<SchemaMigrator>.Instance).MigrateAsync();
            await new ExtendedSchema(db, options).InitializeAsync();
            var cache = new WorldStateCache(new WorldRepository(db));
            await cache.RebuildAsync();
            // Only event delivery is isolated; service, gate, repositories, cache
            // and migrations are the production code and operate on real SQLite.
            var events = new DomainEventPublisher(DispatchProxy.Create<IEventBus, NoopEvents>(), NullLogger<DomainEventPublisher>.Instance);
            events.Initialize(DispatchProxy.Create<IOpenModPlugin, NoopEvents>());
            var gate = new MutationGate(cache, events);
            var players = new PlayerRepository(db);
            var playtime = new PlaytimeService(players, options, gate);
            var towns = new TownService(db, cache, playtime, players, options, NullLogger<TownService>.Instance, gate);
            var ordinary = new PlayerId(1);
            var admin = new PlayerId(2);
            var restricted = await towns.CreateAsync(ordinary, "Ordinary");
            if (restricted.Code != "minimum_playtime") throw new Exception("Regular playtime restriction lost");
            var created = await towns.CreateAsAdminAsync(admin, "AdminTown");
            if (!created.Success || (await players.GetAsync(admin))?.Balance != 0) throw new Exception("Admin creation failed or charged money");
            if ((await towns.CreateAsAdminAsync(admin, "SecondTown")).Code != "already_in_town") throw new Exception("Duplicate membership allowed");
            if ((await towns.CreateAsAdminAsync(new PlayerId(3), "admintown")).Code != "town_name_taken") throw new Exception("Duplicate name allowed");
            if ((await towns.CreateAsAdminAsync(new PlayerId(4), "!")).Code != "invalid_town_name") throw new Exception("Invalid name allowed");
            options.Value.Towns.MinimumPlaytimeHours = 0;
            if ((await towns.CreateAsync(ordinary, "Ordinary")).Code != "insufficient_balance") throw new Exception("Regular balance restriction lost");
            await players.EnsureAsync(new PlayerId(5), 30000);
            if (!(await towns.CreateAsync(new PlayerId(5), "PaidTown")).Success || (await players.GetAsync(new PlayerId(5)))?.Balance != 5000)
                throw new Exception("Regular creation fee was not charged exactly once");
            options.Value.Towns.CreationPrice = 100;
            options.Value.Towns.MinimumBalance = 5000;
            await players.EnsureAsync(new PlayerId(6), 1000);
            if ((await towns.CreateAsync(new PlayerId(6), "BelowMinimum")).Code != "insufficient_balance" || (await players.GetAsync(new PlayerId(6)))?.Balance != 1000)
                throw new Exception("Minimum balance check failed or debited a rejected creation");
            await cache.RebuildAsync();
            if (towns.GetTown(admin)?.Name != "AdminTown") throw new Exception("Admin town was not persisted");
            var economy=new EconomyService(db,players,options,gate);
            await players.EnsureAsync(new PlayerId(7),1000);await players.EnsureAsync(new PlayerId(8),0);
            if(!(await economy.TransferAsync(new(7),new(8),250)).Success || await economy.GetBalanceAsync(new(7))!=750 || await economy.GetBalanceAsync(new(8))!=250)throw new Exception("Payment did not transfer funds");
            if((await economy.TransferAsync(new(7),new(8),751)).Success || await economy.GetBalanceAsync(new(7))!=750 || await economy.GetBalanceAsync(new(8))!=250)throw new Exception("Overdraft payment altered balances");
            if((await economy.TransferAsync(new(7),new(7),10)).Success || (await economy.TransferAsync(new(7),new(8),0)).Success || (await economy.TransferAsync(new(7),new(8),-1)).Success)throw new Exception("Invalid payment accepted");
            await players.EnsureAsync(new PlayerId(9),long.MaxValue);
            if((await economy.TransferAsync(new(7),new(9),1)).Success || await economy.GetBalanceAsync(new(7))!=750)throw new Exception("Receiver overflow did not roll back sender debit");
            var simultaneous=await Task.WhenAll(economy.TransferAsync(new(7),new(8),500),economy.TransferAsync(new(7),new(8),500));
            if(simultaneous.Count(x=>x.Success)!=1 || await economy.GetBalanceAsync(new(7))!=250 || await economy.GetBalanceAsync(new(8))!=750)throw new Exception("Concurrent transfers overspent funds");
            if(UTowny.Utilities.UTownyChat.Color.R!=0 || UTowny.Utilities.UTownyChat.Color.G!=174 || UTowny.Utilities.UTownyChat.Color.B!=98)throw new Exception("Chat color incorrect");
            Console.WriteLine("PASS: payments preserve funds, prevent overdrafts/self/invalid amounts, roll back overflow, and serialize concurrent transfers; chat color is #00AE62.");
            Console.WriteLine("PASS: admin creates with zero funds/playtime without charge; regular playtime, balance and fee checks remain; names, membership and persistence verified against SQLite.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}

public class NoopEvents : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.ReturnType == typeof(Task)) return Task.CompletedTask;
        throw new NotSupportedException(method?.Name);
    }
}
