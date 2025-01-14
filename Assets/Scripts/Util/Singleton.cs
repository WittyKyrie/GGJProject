using UnityEngine;

namespace Util
{
    public abstract class Singleton<T> : MonoBehaviour where T : Component
    {
    
        private static T _instance;
    
        public static T Instance =>
            _instance;

        protected virtual void Awake ()
        {
            if ( _instance == null )
            {
                _instance = this as T;
                DontDestroyOnLoad ( gameObject );
            }
            else
            {
                Destroy ( gameObject );
            }
        }
    }
}
