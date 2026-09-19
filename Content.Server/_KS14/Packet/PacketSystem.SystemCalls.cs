using System.Threading.Channels;
using System.Threading.Tasks;

namespace Content.Server._KS14.Packet;

/// <summary>
/// This handles execution of external system methods outside async functions.
/// </summary>
public sealed partial class PacketSystem
{
    /// <summary>
    /// Methods that are about to be called next tick
    /// </summary>
    private Queue<Action> _callQueue = new();

    /// <summary>
    /// Invoke all system calls in call queue
    /// </summary>
    private void UpdateSystemCalls()
    {
        while (_callQueue.Count > 0)
        {
            var action = _callQueue.Dequeue();
            action.Invoke();
        }
    }

    /// <summary>
    /// If function is running in main thread - executes the method and returns value.
    /// If function isn't running in main thread - Wait for system queue to execute it, then return the result
    /// </summary>
    /// <param name="func"></param>
    /// <param name="channel"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task<T> TryWrapSystemCall<T>(Func<T> func, Channel<object> channel)
    {
        if (Environment.CurrentManagedThreadId == _mainThreadId)
            return func.Invoke();

        WrapSystemCall(async void () =>
        {
            await channel.Writer.WriteAsync(func.Invoke()!);
        });

        return (T) await channel.Reader.ReadAsync();
    }

    public void TryWrapSystemCall(Action action)
    {
        if (Environment.CurrentManagedThreadId == _mainThreadId)
        {
            action.Invoke();
            return;
        }

        WrapSystemCall(action);
    }

    public void WrapSystemCall(Action action)
    {
        _callQueue.Enqueue(action);
    }
}
