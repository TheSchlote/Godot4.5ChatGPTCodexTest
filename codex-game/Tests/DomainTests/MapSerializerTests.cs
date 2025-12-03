using CodexGame.Domain.Maps;
using NUnit.Framework;

namespace CodexGame.Tests.DomainTests;

[TestFixture]
public class MapSerializerTests
{
    [Test]
    public void RoundTrip_SerializesAndDeserializes()
    {
        var map = new MapData
        {
            Width = 2,
            Height = 2,
            Cells = { new MapCell(0, 0, 1, "Earth") },
            Spawns = { new SpawnPoint(1, 0, 1) }
        };

        var json = MapSerializer.ToJson(map);
        var loaded = MapSerializer.FromJson(json);

        Assert.That(loaded.Width, Is.EqualTo(2));
        Assert.That(loaded.Spawns, Has.Count.EqualTo(1));
        Assert.That(loaded.Cells[0].Type, Is.EqualTo("Earth"));
    }
}
