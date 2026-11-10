using BikiniWipeout.Core;
using UnityEngine;

namespace BikiniWipeout
{
    /// <summary><see cref="IKeyValueStore"/> backed by Unity's PlayerPrefs.</summary>
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public float GetFloat(string key, float defaultValue)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        public void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
