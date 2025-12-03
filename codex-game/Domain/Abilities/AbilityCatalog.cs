using System.Collections.Generic;

namespace CodexGame.Domain.Abilities;

public sealed class AbilityCatalog
{
    private readonly Dictionary<string, Ability> _abilities = new();

    public void Register(Ability ability) => _abilities[ability.Id] = ability;

    public Ability Get(string id) => _abilities[id];

    public bool TryGet(string id, out Ability ability) => _abilities.TryGetValue(id, out ability!);

    public IEnumerable<string> GetAllIds() => _abilities.Keys;
}
