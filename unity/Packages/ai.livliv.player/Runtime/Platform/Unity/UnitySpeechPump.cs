using LivLiv.Runtime;
using UnityEngine;

namespace LivLiv.Unity
{
    // Runs independently of the character's render/visibility/timeScale state.
    [AddComponentMenu("")]
    public sealed class UnitySpeechPump : MonoBehaviour
    {
        private SpeechPlayer speech;
        internal void Initialize(SpeechPlayer value)
        {
            speech = value;
            AudioSettings.OnAudioConfigurationChanged += ConfigurationChanged;
        }
        private void Update() { speech?.Tick(); }
        private void OnApplicationPause(bool paused) { if (paused) speech?.Stop(); }
        private void ConfigurationChanged(bool deviceChanged) { speech?.Stop(); }
        private void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= ConfigurationChanged;
            var owned = speech; speech = null; owned?.Dispose();
        }
    }
}
