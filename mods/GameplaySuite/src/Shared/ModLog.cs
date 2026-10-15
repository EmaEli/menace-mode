using System;

namespace MenaceModSuite.Shared;

// Host-neutral logger; SuiteSystem wraps Jiangyu's IModLog in it.
public sealed class ModLog
{
    private readonly Action<string> _msg;
    private readonly Action<string> _warning;
    private readonly Action<string> _error;

    public ModLog(Action<string> msg, Action<string> warning, Action<string> error)
    {
        _msg = msg ?? (_ => { });
        _warning = warning ?? (_ => { });
        _error = error ?? (_ => { });
    }

    public void Msg(string message) => _msg(message);
    public void Warning(string message) => _warning(message);
    public void Error(string message) => _error(message);
}
