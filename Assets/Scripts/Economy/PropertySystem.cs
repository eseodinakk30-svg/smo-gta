using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.World;

namespace SanMonica.Economy
{
    /// <summary>
    /// Buying and using property: apartments and villas to save and change
    /// clothes in, garages to store cars, and businesses that pay out daily.
    /// </summary>
    public class PropertySystem : MonoBehaviour
    {
        public readonly HashSet<string> Owned = new HashSet<string>();
        public string LastUsedSafehouse { get; private set; }

        private CityLayout _layout;

        public void Initialize(CityLayout layout)
        {
            _layout = layout;
        }

        public bool IsOwned(PropertyInstance property) => property != null && Owned.Contains(property.Definition.id);

        public bool TryBuy(PropertyInstance property)
        {
            if (property == null) return false;
            if (IsOwned(property))
            {
                GameEvents.Notify("You already own " + property.Definition.displayName, 2.5f);
                return false;
            }
            var economy = Services.Economy;
            if (economy == null) return false;
            if (!economy.TrySpend(property.Definition.price, "Property: " + property.Definition.displayName)) return false;

            Owned.Add(property.Definition.id);
            property.Owned = true;
            GameEvents.Notify("Purchased " + property.Definition.displayName, 4f);
            Services.Save?.AutoSave();
            return true;
        }

        public long DailyIncome()
        {
            if (_layout == null) return 0;
            long total = 0;
            foreach (var property in _layout.Properties)
                if (Owned.Contains(property.Definition.id)) total += property.Definition.dailyIncome;
            return total;
        }

        public PropertyInstance FindOwnedNearest(Vector3 position)
        {
            if (_layout == null) return null;
            PropertyInstance best = null;
            float bestDistance = float.MaxValue;
            foreach (var property in _layout.Properties)
            {
                if (!Owned.Contains(property.Definition.id)) continue;
                float d = (property.Definition.position - position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = property; }
            }
            return best;
        }

        public void UseSafehouse(PropertyInstance property)
        {
            if (property == null) return;
            LastUsedSafehouse = property.Definition.id;
            var player = Services.Player;
            if (player != null && player.Health != null)
            {
                player.Health.Heal(player.Health.MaxHealth);
                GameEvents.Notify("Rested at " + property.Definition.displayName, 3f);
            }
            Services.Clock?.SkipHours(8f);
            Services.Save?.AutoSave();
        }

        public IEnumerable<PropertyInstance> OwnedProperties()
        {
            if (_layout == null) yield break;
            foreach (var property in _layout.Properties)
                if (Owned.Contains(property.Definition.id)) yield return property;
        }

        public List<string> CaptureState() => new List<string>(Owned);

        public void RestoreState(List<string> owned)
        {
            Owned.Clear();
            if (owned == null || _layout == null) return;
            foreach (var id in owned) Owned.Add(id);
            foreach (var property in _layout.Properties)
                property.Owned = Owned.Contains(property.Definition.id);
        }
    }
}
