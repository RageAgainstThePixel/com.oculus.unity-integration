using System.Runtime.InteropServices;
using UnityEngine;

namespace Oculus.Platform
{
  public class CallbackRunner : MonoBehaviour
  {
    [DllImport(CAPI.DLL_NAME)]
    private static extern void ovr_UnityResetTestPlatform();

    public bool IsPersistantBetweenSceneLoads = true;

    private void Awake()
    {
      var existingCallbackRunner = FindObjectOfType<CallbackRunner>();
      if (existingCallbackRunner != this)
      {
        Debug.LogWarning("You only need one instance of CallbackRunner");
      }
      if (IsPersistantBetweenSceneLoads)
      {
        DontDestroyOnLoad(gameObject);
      }
    }

    private void Update()
    {
      Request.RunCallbacks();
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
      ovr_UnityResetTestPlatform();
#endif
    }

    private void OnApplicationQuit()
    {
      Callback.OnApplicationQuit();
    }
  }
}
