using CodexGame.Domain.QTE;
using NUnit.Framework;

namespace CodexGame.Tests.DomainTests;

[TestFixture]
public class TimingBarEvaluatorTests
{
    [TestCase(1.0f, 1.01f, QTEScore.Critical)]
    [TestCase(1.0f, 1.05f, QTEScore.Great)]
    [TestCase(1.0f, 1.07f, QTEScore.Good)]
    [TestCase(1.0f, 1.30f, QTEScore.Miss)]
    public void Evaluate_ReturnsExpectedScore(float target, float press, QTEScore expected)
    {
        var profile = new QTEProfile(QTEType.TimingBar, difficulty: 1f, critWindow: 0.02f);
        var evaluator = new TimingBarEvaluator();

        var result = evaluator.Evaluate(new TimingBarInput(target, press), profile);

        Assert.That(result.Score, Is.EqualTo(expected));
    }
}
