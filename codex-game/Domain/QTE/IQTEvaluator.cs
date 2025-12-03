namespace CodexGame.Domain.QTE;

/// <summary>
/// Pure evaluator that converts player input data into a QTE score.
/// </summary>
public interface IQTEvaluator<in TInput>
{
    QTEResult Evaluate(TInput input, QTEProfile profile);
}
