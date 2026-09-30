using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteRP.Network;

namespace AbsoluteRP.Helpers
{
    // Every profile link this account is part of, kept in step with the server.
    public static class RelationshipManager
    {
        public static readonly List<RelationshipBond> All = new();
        public static event Action OnChanged;

        public static IEnumerable<RelationshipBond> Accepted => All.Where(b => b.Status == (int)RelationshipStatus.Accepted);
        public static IEnumerable<RelationshipBond> IncomingPending => All.Where(b => b.Status == (int)RelationshipStatus.Pending && !b.RequestedByUs);
        public static IEnumerable<RelationshipBond> OutgoingPending => All.Where(b => b.Status == (int)RelationshipStatus.Pending && b.RequestedByUs);

        private static long _lastFetch;
        // Ask the server for the list, at most once every 20 seconds.
        public static void EnsureFetched(bool force = false)
        {
            if (Plugin.character == null) return;
            var now = Environment.TickCount64;
            if (!force && _lastFetch != 0 && now - _lastFetch < 20000) return;
            _lastFetch = now;
            Relationships_DS.FetchRelationships(Plugin.character);
        }

        public static void ReplaceAll(IEnumerable<RelationshipBond> bonds)
        {
            All.Clear();
            if (bonds != null) All.AddRange(bonds);
            OnChanged?.Invoke();
        }

        public static void Upsert(RelationshipBond bond)
        {
            if (bond == null) return;
            All.RemoveAll(b => b.BondID == bond.BondID);
            All.Add(bond);
            OnChanged?.Invoke();
        }

        public static void Remove(int bondID)
        {
            if (All.RemoveAll(b => b.BondID == bondID) > 0) OnChanged?.Invoke();
        }

        public static RelationshipBond ByID(int bondID) => All.FirstOrDefault(b => b.BondID == bondID);

        public static void ClearAll() { All.Clear(); _lastFetch = 0; OnChanged?.Invoke(); }
    }
}
