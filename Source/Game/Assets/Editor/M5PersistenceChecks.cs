using System;
using Boki.Core;
using Boki.Routes;
using UnityEditor;
using UnityEngine;

namespace Boki.Editor
{
    // Run Write and Read in separate Editor processes to verify disk persistence.
    public static class M5PersistenceChecks
    {
        public static void Write()
        {
            Guard();
            PlayerPrefs.SetInt("boki.m5.unrelated", 42);
            foreach (var key in new[] { "boki.unlock.forestC", "boki.unlock.cityC", "boki.unlock.feriC" }) PlayerPrefs.DeleteKey(key);
            var progress = new PersistentUnlocks(new PlayerPrefsUnlockStore());
            var run = new RunState(progress.Read(), 0);
            if (run.AvailableUnlocks.ForestC || run.AvailableUnlocks.CityC || run.AvailableUnlocks.FeriC)
                throw new InvalidOperationException("Absent keys must default to false.");
            run.TrySelect(RouteLevel.Forest, RouteChoice.B);
            if (!progress.Earn(run, RouteLevel.Forest)) throw new InvalidOperationException("Forest milestone not earned.");
            Debug.Log("M5_PERSISTENCE_WRITE_PASSED");
        }
        public static void Read()
        {
            Guard();
            var flags = new PersistentUnlocks(new PlayerPrefsUnlockStore()).Read();
            if (!flags.ForestC || flags.CityC || flags.FeriC || PlayerPrefs.GetInt("boki.m5.unrelated") != 42)
                throw new InvalidOperationException("Permanent progress failed process restart or touched unrelated data.");
            PlayerPrefs.SetInt("boki.unlock.cityC", 2);
            if (new PlayerPrefsUnlockStore().Read(RouteLevel.City)) throw new InvalidOperationException("Invalid saved value must stay locked.");
            PlayerPrefs.DeleteKey("boki.unlock.forestC");
            PlayerPrefs.DeleteKey("boki.unlock.cityC");
            PlayerPrefs.DeleteKey("boki.m5.unrelated");
            PlayerPrefs.Save();
            Debug.Log("M5_PERSISTENCE_RESTART_PASSED");
        }
        private static void Guard()
        {
            if (Application.productName != "Boki-M5-Validation")
                throw new InvalidOperationException("Run only in the isolated validation project.");
        }
    }
}
