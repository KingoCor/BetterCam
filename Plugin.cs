using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using CircuitSuperstars;

namespace BetterCam;

public enum CameraType {
    Default,
    Follow,
	FirstPerson
}

class PatchCamera {
	
	internal static Vector3 lastPosition;
	private static bool initialized = false;

	static void CameraFollow(CircuitSuperstars.PlayerVehicleCamera __instance, float deltaTime) {
		Rigidbody car = __instance.GetTarget();

		Vector3 translation = __instance.transform.position-car.transform.position;
		float height = translation.y*Plugin.height.Value;
		translation.y = 0;
		float distance = translation.magnitude*Plugin.distance.Value;
		
		Vector3 carForward = car.transform.forward;
		Vector3 desiredPosition = car.transform.position - carForward * distance;
		desiredPosition.y += height;

		__instance.transform.position = Vector3.Lerp(lastPosition,desiredPosition,Plugin.lerpSpeed.Value*deltaTime);
		__instance.transform.rotation = Quaternion.LookRotation(car.transform.position-__instance.transform.position);
	}

	static void CameraFirstPeson(CircuitSuperstars.PlayerVehicleCamera __instance, float deltaTime) {
		Rigidbody car = __instance.GetTarget();

		Vector3 desiredPosition = car.transform.position;
		desiredPosition += car.transform.forward*Plugin.distance.Value;
		desiredPosition += car.transform.up*Plugin.height.Value;

		__instance.transform.position = desiredPosition;
		__instance.transform.rotation = Quaternion.LookRotation(car.transform.forward);
	}


	[HarmonyPatch(typeof(CircuitSuperstars.PlayerVehicleCamera), nameof(CircuitSuperstars.PlayerVehicleCamera.PositionCamera))]
	[HarmonyPostfix]
	static void PositionCameraPostfix(CircuitSuperstars.PlayerVehicleCamera __instance, float deltaTime) {
		Rigidbody car = __instance.GetTarget();
		if (car == null) {
			initialized = false;
			return;
		}
		if (!initialized) {
			lastPosition = __instance.transform.position;
			initialized = true;
		}

		__instance.virtualCamera.m_Lens.NearClipPlane = Plugin.nearClipPlane.Value;

		switch(Plugin.cameraType.Value) {
			case CameraType.Default: return;
			case CameraType.Follow: 
				CameraFollow(__instance, deltaTime);
				break;
			case CameraType.FirstPerson:
				CameraFirstPeson(__instance, deltaTime);
				break;
		}

		lastPosition = __instance.transform.position;
	}
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    internal static new ManualLogSource Logger;

	internal static ConfigEntry<CameraType> cameraType;
	internal static ConfigEntry<float> lerpSpeed;
	internal static ConfigEntry<float> height;
	internal static ConfigEntry<float> distance;
	internal static ConfigEntry<float> nearClipPlane;

	private void BindConfig() {
		cameraType = Config.Bind<CameraType>(
			"General",
			"cameraType",
			CameraType.Follow,
			"Тип камеры"
		);

		lerpSpeed = Config.Bind<float>(
			"General",
			"camSpeed",
			2.0f,
			new ConfigDescription(
				"Camera speed",
				new AcceptableValueRange<float>(0,100)
			)
		);

		height = Config.Bind<float>(
			"General",
			"height",
			1.00f,
			new ConfigDescription(
				"Camera height as a percentage of the standard",
				new AcceptableValueRange<float>(-10,10)
			)
		);

		distance = Config.Bind<float>(
			"General",
			"distance",
			1.00f,
			new ConfigDescription(
				"Horizontal distance from the machine to the camera as a percentage of the standard", 
				new AcceptableValueRange<float>(-10,10)
			)
		);

		nearClipPlane = Config.Bind<float>(
			"General",
			"nearClipPlane",
			1,
			new ConfigDescription("Near clip plane", new AcceptableValueRange<float>(0,20))
		);
	}
        
    private void Awake() {
        Logger = base.Logger;

		BindConfig();

		var instance = new Harmony(MyPluginInfo.PLUGIN_GUID);
		instance.PatchAll(typeof(PatchCamera));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
}
