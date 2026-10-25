using System;

namespace Boki.Routes
{
    public interface IUnlockStore
    {
        bool Read(RouteLevel level);
        void Write(RouteLevel level);
        void Save();
    }

    // Only permanent flags are stored. Run snapshots are independent values.
    public sealed class PersistentUnlocks
    {
        private readonly IUnlockStore store;
        public PersistentUnlocks(IUnlockStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));
        public UnlockSnapshot Read() => new UnlockSnapshot
        {
            ForestC = store.Read(RouteLevel.Forest),
            CityC = store.Read(RouteLevel.City),
            FeriC = store.Read(RouteLevel.Feri)
        };

        public bool Earn(RunState run, RouteLevel level)
        {
            if (run == null || !RunState.ValidLevel(level) || !run.Health.IsAlive
                || run.ActiveLevel != level || run.Choice(level) == RouteChoice.None || store.Read(level)) return false;
            store.Write(level);
            store.Save();
            return true;
        }
    }
}
