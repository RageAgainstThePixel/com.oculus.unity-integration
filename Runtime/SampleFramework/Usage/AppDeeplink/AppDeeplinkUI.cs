using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AppDeeplinkUI : MonoBehaviour
{
    // these are just for illustration, you'll need to modify them to match your own app ids
    private const ulong UNITY_COMPANION_APP_ID = 3535750239844224;
    private const ulong UNREAL_COMPANION_APP_ID = 4055411724486843;

    private RectTransform deeplinkAppId;
    private RectTransform deeplinkMessage;

    private RectTransform uiLaunchType;
    private RectTransform uiLaunchSource;
    private RectTransform uiDeepLinkMessage;

    private bool inMenu = true;

    // Start is called before the first frame update
    private void Start()
    {
        DebugUIBuilder ui = DebugUIBuilder.instance;
        uiLaunchType = ui.AddLabel("UnityDeeplinkSample");
        ui.AddDivider();
        ui.AddButton("launch OtherApp", LaunchOtherApp);
        ui.AddButton("launch UnrealDeeplinkSample", LaunchUnrealDeeplinkSample);
        deeplinkAppId = CustomDebugUI.instance.AddTextField(UNITY_COMPANION_APP_ID.ToString(), 0);
        deeplinkMessage = CustomDebugUI.instance.AddTextField("MSG_UNITY_SAMPLE", 0);

        ui.AddButton("LaunchSelf", LaunchSelf);

        if (Application.platform == RuntimePlatform.Android)
        {
            // init ovr platform
            if (!Oculus.Platform.Core.IsInitialized())
            {
                Oculus.Platform.Core.Initialize();
            }
        }

        uiLaunchType = ui.AddLabel("LaunchType: ");
        uiLaunchSource = ui.AddLabel("LaunchSource: ");
        uiDeepLinkMessage = ui.AddLabel("DeeplinkMessage: ");

        ui.ToggleLaserPointer(true);

        ui.Show();
    }

    // Update is called once per frame
    private void Update()
    {
        DebugUIBuilder ui = DebugUIBuilder.instance;
        if (Application.platform == RuntimePlatform.Android)
        {
            // retrieve + update launch details
            Oculus.Platform.Models.LaunchDetails launchDetails = Oculus.Platform.ApplicationLifecycle.GetLaunchDetails();
            uiLaunchType.GetComponentInChildren<Text>().text = "LaunchType: " + launchDetails.LaunchType;
            uiLaunchSource.GetComponentInChildren<Text>().text = "LaunchSource: " + launchDetails.LaunchSource;
            uiDeepLinkMessage.GetComponentInChildren<Text>().text = "DeeplinkMessage: " + launchDetails.DeeplinkMessage;
        }

        if (OVRInput.GetDown(OVRInput.Button.Two) || OVRInput.GetDown(OVRInput.Button.Start))
        {
            if (inMenu)
            {
                DebugUIBuilder.instance.Hide();
            }
            else
            {
                DebugUIBuilder.instance.Show();
            }
            inMenu = !inMenu;
        }
    }

    private void LaunchUnrealDeeplinkSample()
    {
        Debug.Log(string.Format("LaunchOtherApp({0})", UNREAL_COMPANION_APP_ID));
        var options = new Oculus.Platform.ApplicationOptions();
        options.SetDeeplinkMessage(deeplinkMessage.GetComponentInChildren<Text>().text);
        Oculus.Platform.Application.LaunchOtherApp(UNREAL_COMPANION_APP_ID, options);
    }

    private void LaunchSelf()
    {
        // launch self, assumes android platform
        ulong appId;
        if (ulong.TryParse(Oculus.Platform.PlatformSettings.MobileAppID, out appId))
        {
            Debug.Log(string.Format("LaunchSelf({0})", appId));
            var options = new Oculus.Platform.ApplicationOptions();
            options.SetDeeplinkMessage(deeplinkMessage.GetComponentInChildren<Text>().text);
            Oculus.Platform.Application.LaunchOtherApp(appId, options);
        }
    }

    private void LaunchOtherApp()
    {
        ulong appId;
        if(ulong.TryParse(deeplinkAppId.GetComponentInChildren<Text>().text, out appId))
        {
            Debug.Log(string.Format("LaunchOtherApp({0})", appId));
            var options = new Oculus.Platform.ApplicationOptions();
            options.SetDeeplinkMessage(deeplinkMessage.GetComponentInChildren<Text>().text);
            Oculus.Platform.Application.LaunchOtherApp(appId, options);
        }
    }
}
