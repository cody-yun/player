using System;
using System.Threading;
using System.Threading.Tasks;

namespace LivLiv.Platform
{
    // All calls and continuations belong to the host's main thread. An output owns the audio
    // context; each returned piece owns its scheduled source/buffer until Dispose.
    public interface ISpeechOutput : IDisposable
    {
        bool Running { get; }
        double CurrentTime { get; }
        double HeardTime { get; }
        double WallTime { get; }
        void Unlock();
        void SetVolume(double volume);
        ISpeechPiece Schedule(float[] samples, int sampleRate, double at);
    }

    public interface ISpeechPiece : IDisposable
    {
        bool Ended { get; }
    }

    // Optional event boundary for outputs with more states than Running (e.g. Web Audio
    // suspended -> interrupted). Consumed on the host main thread; not a busy/clip event.
    public interface ISpeechStateOutput
    {
        int ConsumeStateChanges();
        bool HasOpenContext { get; }
    }

    // Open starts downloading immediately, including while an earlier sentence is loading.
    // NextAsync joins whatever has arrived, or returns null once the stream has ended. The
    // first channel is supplied at its own sample rate. Cancellation must unblock NextAsync.
    public interface ISpeechLoader
    {
        ISpeechIncoming Open(string url, CancellationToken cancellationToken);
    }
    public interface ISpeechBytesLoader : ISpeechLoader
    {
        ISpeechIncoming Open(byte[] encodedAudio, CancellationToken cancellationToken);
    }

    public interface ISpeechIncoming : IDisposable
    {
        int SampleRate { get; }
        Task<float[]> NextAsync();
    }
}
