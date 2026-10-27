using UnityEngine;

namespace KaniTactics.Game
{
    /// 初回アクセス時に自動生成され、シーンをまたいで生存するシングルトンの土台。
    /// シーンに何も置かなくても Instance から使える(SoundManager / SceneLoader)。
    public abstract class PersistentSingleton<T> : MonoBehaviour where T : PersistentSingleton<T>
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(typeof(T).Name);
                    _instance = go.AddComponent<T>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
    }
}
