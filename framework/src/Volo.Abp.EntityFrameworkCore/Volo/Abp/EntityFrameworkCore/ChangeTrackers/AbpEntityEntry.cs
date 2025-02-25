using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Volo.Abp.EntityFrameworkCore.ChangeTrackers;

public class AbpEntityEntry
{
    public string Id { get; set; }

    public EntityEntry EntityEntry { get; set; }

    public List<AbpNavigationEntry> NavigationEntries { get; set; }

    private bool _isModified;
    public bool IsModified
    {
        get
        {
            return _isModified || EntityEntry.State == EntityState.Modified || NavigationEntries.Any(n => n.IsModified);
        }
        set
        {
            _isModified = value;
        }
    }

    public AbpEntityEntry(string id, EntityEntry entityEntry)
    {
        Id = id;
        EntityEntry = entityEntry;
        NavigationEntries = EntityEntry.Navigations.Select(x => new AbpNavigationEntry(x, x.Metadata.Name)).ToList();
    }

    public void UpdateNavigationEntries()
    {
        foreach (var navigationEntry in NavigationEntries)
        {
            if (IsModified ||
                EntityEntry.State == EntityState.Modified ||
                navigationEntry.IsModified ||
                navigationEntry.NavigationEntry.IsModified)
            {
                continue;
            }

            var navigation = EntityEntry.Navigations.FirstOrDefault(n => n.Metadata.Name == navigationEntry.Name);

            var currentValue = AbpNavigationEntry.GetOriginalValue(navigation?.CurrentValue);
            if (currentValue == null)
            {
                continue;
            }

            switch (navigationEntry.OriginalValue)
            {
                case null:
                    navigationEntry.OriginalValue = currentValue;
                    break;
                case IEnumerable originalValueCollection when currentValue is IEnumerable currentValueCollection:
                {
                    var existingList = originalValueCollection.Cast<object?>().ToList();
                    var newList = currentValueCollection.Cast<object?>().ToList();
                    if (newList.Count > existingList.Count)
                    {
                        navigationEntry.OriginalValue = currentValue;
                    }

                    break;
                }
                default:
                    navigationEntry.OriginalValue = currentValue;
                    break;
            }
        }
    }
}

public class AbpNavigationEntry
{
    public NavigationEntry NavigationEntry { get; set; }

    public string Name { get; set; }

    public bool IsModified { get; set; }

    public List<object>? OriginalValue { get; set; }

    public object? CurrentValue => NavigationEntry.CurrentValue;

    public AbpNavigationEntry(NavigationEntry navigationEntry, string name)
    {
        NavigationEntry = navigationEntry;
        Name = name;
        OriginalValue = GetOriginalValue(navigationEntry.CurrentValue);
    }

    public static List<object>? GetOriginalValue(object? currentValue)
    {
        if (currentValue is null)
        {
            return null;
        }

        if (currentValue is IEnumerable enumerable)
        {
            return enumerable.Cast<object>().ToList();
        }

        return new List<object> { currentValue };
    }
}
