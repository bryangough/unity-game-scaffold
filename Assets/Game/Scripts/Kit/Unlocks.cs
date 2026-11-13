using System;
using System.Collections.Generic;

namespace GameScaffold
{
    /// <summary>One collectable (a skin, a backdrop, a music track), earned when a lifetime count reaches <see cref="Needed"/>.</summary>
    public sealed class Unlock
    {
        public readonly string Id, Name, Kind;
        public readonly int Needed;
        public Unlock(string id, string name, string kind, int needed) { Id = id; Name = name; Kind = kind; Needed = needed; }
        /// <summary>Open from the start: never news, and what a kind falls back to.</summary>
        public bool Free => Needed <= 0;
    }

    /// <summary>
    /// Cosmetic unlocks behind a lifetime count: total stars (<see cref="Progress.TotalStars"/>), missions done
    /// (<see cref="MissionBook{TReport}.Stars"/>), runs played; the game picks the count and the table, the menus draw
    /// it. Plain C#, tested by <c>UnlocksTests</c>. Unlocks are for showing off, never between the player and the next
    /// level. Both the jelly and the stealth game built this separately; the first of each kind is free, the first
    /// earned one comes within a game or two and the last wants a good share of everything there is.
    /// <para>The "new" badge: an earned unlock the player has not looked at yet. Pass <see cref="Progress.Seen"/> and
    /// call <see cref="Progress.MarkSeen"/> when the collection screen opens. The chosen one per kind is
    /// <see cref="Progress.Chosen"/>; always go through <see cref="Resolve"/>, so a choice that is no longer earned
    /// (a cleared save, a changed table) falls back to the free one. With Unlock Everything on, pass
    /// <c>int.MaxValue</c> as the count.</para>
    /// </summary>
    public sealed class UnlockTable
    {
        /// <summary>Every unlock, in the order they are earned.</summary>
        public readonly Unlock[] All;

        public UnlockTable(params Unlock[] all)
        {
            var ids = new HashSet<string>();
            var kinds = new HashSet<string>();
            var free = new HashSet<string>();
            foreach (Unlock u in all)
            {
                if (!ids.Add(u.Id)) throw new ArgumentException($"[UnlockTable] id '{u.Id}' twice");
                kinds.Add(u.Kind);
                if (u.Free) free.Add(u.Kind);
            }
            foreach (string k in kinds)
                if (!free.Contains(k)) throw new ArgumentException($"[UnlockTable] kind '{k}' has no free unlock to fall back to");
            All = (Unlock[])all.Clone();
            Array.Sort(All, (a, b) => a.Needed != b.Needed ? a.Needed.CompareTo(b.Needed) : Array.IndexOf(all, a).CompareTo(Array.IndexOf(all, b)));
        }

        public Unlock Find(string id)
        {
            foreach (Unlock u in All) if (u.Id == id) return u;
            return null;
        }

        public bool IsOpen(string id, int count) { Unlock u = Find(id); return u != null && count >= u.Needed; }

        /// <summary>The unlocks of one kind, in order.</summary>
        public List<Unlock> OfKind(string kind) => Filter(u => u.Kind == kind);

        /// <summary>The next unlock still to earn, or null when everything is open.</summary>
        public Unlock Next(int count)
        {
            foreach (Unlock u in All) if (count < u.Needed) return u;
            return null;
        }

        /// <summary>How far to the next unlock ("3 more stars for Sunset"); 0 when everything is open.</summary>
        public int ToNext(int count) { Unlock n = Next(count); return n == null ? 0 : n.Needed - count; }

        /// <summary>What opened going from one count to another: for "Unlocked: Sunset!" on the result card.</summary>
        public List<Unlock> NewlyOpen(int before, int after) => Filter(u => before < u.Needed && after >= u.Needed);

        /// <summary>The id to use for a kind: the chosen one if it is that kind and earned, otherwise the free one.</summary>
        public string Resolve(string kind, string chosenId, int count)
        {
            Unlock c = Find(chosenId);
            if (c != null && c.Kind == kind && count >= c.Needed) return c.Id;
            foreach (Unlock u in All) if (u.Kind == kind && u.Free) return u.Id;
            return null;
        }

        /// <summary>Earned (not free) and not yet seen: the dot on the collection button.</summary>
        public bool IsNew(string id, int count, Func<string, bool> seen) { Unlock u = Find(id); return u != null && !u.Free && count >= u.Needed && !seen(u.Id); }

        public int NewCount(int count, Func<string, bool> seen)
        {
            int n = 0;
            foreach (Unlock u in All) if (!u.Free && count >= u.Needed && !seen(u.Id)) n++;
            return n;
        }

        /// <summary>The ids that are open at this count (to mark seen when the collection opens).</summary>
        public List<string> OpenIds(int count)
        {
            var ids = new List<string>();
            foreach (Unlock u in All) if (count >= u.Needed) ids.Add(u.Id);
            return ids;
        }

        private List<Unlock> Filter(Func<Unlock, bool> keep)
        {
            var list = new List<Unlock>();
            foreach (Unlock u in All) if (keep(u)) list.Add(u);
            return list;
        }
    }
}
