using System;

namespace CodexGame.Domain.QTE;

public readonly record struct TimingBarInput(float TargetTime, float PressTime);

public sealed class TimingBarEvaluator : IQTEvaluator<TimingBarInput>
{
    /// <summary>
    /// Evaluates based on absolute delta between target and press time.
    /// Lower delta -> better score. Crit window uses profile.CritWindow as seconds.
    /// </summary>
    public QTEResult Evaluate(TimingBarInput input, QTEProfile profile)
    {
        if (profile.Type != QTEType.TimingBar)
            throw new InvalidOperationException("TimingBarEvaluator requires TimingBar profile.");

        var delta = Math.Abs(input.PressTime - input.TargetTime);
        var difficulty = profile.Difficulty <= 0 ? 1f : profile.Difficulty;
        var critWindow = profile.CritWindow / difficulty;
        var greatWindow = critWindow * 2.5f;
        var goodWindow = critWindow * 4f;

        return delta switch
        {
            _ when delta <= critWindow => new QTEResult(QTEScore.Critical, 1.5f, true),
            _ when delta <= greatWindow => new QTEResult(QTEScore.Great, 1.2f, false),
            _ when delta <= goodWindow => new QTEResult(QTEScore.Good, 1.0f, false),
            _ => new QTEResult(QTEScore.Miss, 0.0f, false)
        };
    }
}
