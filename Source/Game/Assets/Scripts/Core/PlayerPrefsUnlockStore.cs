using System;
using Boki.Routes;
using UnityEngine;

namespace Boki.Core
{
    public sealed class PlayerPrefsUnlockStore : IUnlockStore
    {
        private static string Key(RouteLevel level)
        {
            switch (level)
            {
                case RouteLevel.Forest: return "boki.unlock.forestC";
                case RouteLevel.City: return "boki.unlock.cityC";
                case RouteLevel.Feri: return "boki.unlock.feriC";
                default: throw new ArgumentOutOfRangeException(nameof(level));
            }
        }
        public bool Read(RouteLevel level) => PlayerPrefs.GetInt(Key(level), 0) == 1;
        public void Write(RouteLevel level) => PlayerPrefs.SetInt(Key(level), 1);
        public void Save() => PlayerPrefs.Save();
    }
}
