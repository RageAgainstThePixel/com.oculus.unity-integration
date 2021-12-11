/************************************************************************************
Copyright : Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Your use of this SDK or tool is subject to the Oculus SDK License Agreement, available at
https://developer.oculus.com/licenses/oculussdk/

Unless required by applicable law or agreed to in writing, the Utilities SDK distributed
under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
ANY KIND, either express or implied. See the License for the specific language governing
permissions and limitations under the License.
************************************************************************************/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Shows the Oculus plaform UI.
/// </summary>
public class OVRPlatformMenu : MonoBehaviour
{
	/// <summary>
	/// The key code.
	/// </summary>
	private OVRInput.RawButton inputCode = OVRInput.RawButton.Back;

	public enum eHandler
	{
		ShowConfirmQuit,
		RetreatOneLevel,
	};

	public eHandler shortPressHandler = eHandler.ShowConfirmQuit;

	/// <summary>
	/// Callback to handle short press. Returns true if ConfirmQuit menu should be shown.
	/// </summary>
	public System.Func<bool> OnShortPress;
	private static Stack<string> sceneStack = new Stack<string>();

    private enum eBackButtonAction
	{
		NONE,
		SHORT_PRESS
	};

    private eBackButtonAction HandleBackButtonState()
	{
		eBackButtonAction action = eBackButtonAction.NONE;

		if (OVRInput.GetDown(inputCode))
		{
			action = eBackButtonAction.SHORT_PRESS;
		}

		return action;
	}

	/// <summary>
	/// Instantiate the cursor timer
	/// </summary>
    private void Awake()
	{
		if (shortPressHandler == eHandler.RetreatOneLevel && OnShortPress == null)
        {
            OnShortPress = RetreatOneLevel;
        }

        if (!OVRManager.isHmdPresent)
		{
			enabled = false;
			return;
		}

		sceneStack.Push(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
	}

	/// <summary>
	/// Show the confirm quit menu
	/// </summary>
    private void ShowConfirmQuitMenu()
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		Debug.Log("[PlatformUI-ConfirmQuit] Showing @ " + Time.time);
		OVRManager.PlatformUIConfirmQuit();
#endif
	}

	/// <summary>
	/// Sample handler for short press which retreats to the previous scene that used OVRPlatformMenu.
	/// </summary>
	private static bool RetreatOneLevel()
	{
		if (sceneStack.Count > 1)
		{
			string parentScene = sceneStack.Pop();
			UnityEngine.SceneManagement.SceneManager.LoadSceneAsync (parentScene);
			return false;
		}

		return true;
	}

	/// <summary>
	/// Tests for long-press and activates global platform menu when detected.
	/// as per the Unity integration doc, the back button responds to "mouse 1" button down/up/etc
	/// </summary>
    private void Update()
	{
#if UNITY_ANDROID
		eBackButtonAction action = HandleBackButtonState();
		if (action == eBackButtonAction.SHORT_PRESS)
		{
			if (OnShortPress == null || OnShortPress())
			{
				ShowConfirmQuitMenu();
			}
		}
#endif
	}
}
