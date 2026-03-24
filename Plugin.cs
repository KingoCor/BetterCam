using System.Collections.Generic;

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
	Chase,
	FirstPerson
}

class PatchCamera {
	
	internal static Vector3 lastPosition;
	private static bool initialized = false;

	static void CameraFollow(CircuitSuperstars.PlayerVehicleCamera __instance, float deltaTime) {
		Rigidbody car = __instance.GetTarget();

		Vector3 translation = __instance.transform.position-car.transform.position;
		float height = translation.y*Plugin.followConfig.height.Value;
		translation.y = 0;
		float distance = translation.magnitude*Plugin.followConfig.distance.Value;
		
		Vector3 desiredPosition = car.transform.position-car.transform.forward*distance;
		desiredPosition.y += height;

		__instance.transform.position = Vector3.Lerp(lastPosition,desiredPosition,Plugin.followConfig.speed.Value*deltaTime);
		__instance.transform.rotation = Quaternion.LookRotation(car.transform.position-__instance.transform.position);
	}

	static void CameraFirstPeson(CircuitSuperstars.PlayerVehicleCamera __instance, float deltaTime) {
		Rigidbody car = __instance.GetTarget();

		Vector3 desiredPosition = car.transform.position;
		desiredPosition += car.transform.forward*Plugin.firstPersonConfig.distance.Value;
		desiredPosition += car.transform.up*Plugin.firstPersonConfig.height.Value;

		VehicleDescriptorComponent vdc = car.GetComponentInParent<VehicleDescriptorComponent>();
        if (vdc!=null) {
			string vehicleId = vdc.VehicleDescriptor.name;

			desiredPosition += car.transform.forward*Plugin.carsConfigs[vehicleId].distance.Value;
			desiredPosition += car.transform.up*Plugin.carsConfigs[vehicleId].height.Value;
			desiredPosition += car.transform.right*Plugin.carsConfigs[vehicleId].sideOffset.Value;
		}

		__instance.transform.position = Vector3.Lerp(lastPosition,desiredPosition,Plugin.firstPersonConfig.speed.Value*deltaTime);//desiredPosition;
		__instance.transform.rotation = Quaternion.LookRotation(car.transform.forward);
	}

	static void CameraChase(CircuitSuperstars.PlayerVehicleCamera __instance, float deltaTime) {
		Rigidbody car = __instance.GetTarget();

		Vector3 desiredPosition = car.transform.position;
		desiredPosition -= car.transform.forward*Plugin.chaseConfig.distance.Value;
		desiredPosition += car.transform.up*Plugin.chaseConfig.height.Value;

		__instance.transform.position = Vector3.Lerp(lastPosition,desiredPosition,Plugin.chaseConfig.speed.Value*deltaTime);
		
		Vector3 lookAt = car.transform.position-__instance.transform.position;
		lookAt.y = 0;

		__instance.transform.rotation = Quaternion.LookRotation(lookAt);
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
			case CameraType.Chase:
				CameraChase(__instance, deltaTime);
				break;
			case CameraType.FirstPerson:
				CameraFirstPeson(__instance, deltaTime);
				break;
		}

		lastPosition = __instance.transform.position;
	}
}

class CameraTypeConfig {
	public ConfigEntry<float> height;
	public ConfigEntry<float> distance;
	public ConfigEntry<float> speed;

	public CameraTypeConfig(ConfigFile config, string name, float defaultHeight, float defaultDistance, float defaultSpeed) {
		height = config.Bind<float>(
			name,
			"height",
			defaultHeight,
			new ConfigDescription(
				"Camera height as a percentage of the standard",
				new AcceptableValueRange<float>(-10,10)
			)
		);

		distance = config.Bind<float>(
			name,
			"distance",
			defaultDistance,
			new ConfigDescription(
				"Horizontal distance from the machine to the camera as a percentage of the standard", 
				new AcceptableValueRange<float>(-10,10)
			)
		);

		speed = config.Bind<float>(
			name,
			"speed",
			defaultSpeed,
			new ConfigDescription(
				"Camera speed",
				new AcceptableValueRange<float>(0,100)
			)
		);
	}
}

class CarTypeConfig {
	public ConfigEntry<float> height;
	public ConfigEntry<float> distance;
	public ConfigEntry<float> sideOffset;

	public CarTypeConfig(ConfigFile config, string name) {
		height = config.Bind<float>(
			name,
			"height",
			0.0f,
			new ConfigDescription(
				"Adds to First Person height",
				new AcceptableValueRange<float>(-2,2)
			)
		);

		distance = config.Bind<float>(
			name,
			"distance",
			0.0f,
			new ConfigDescription(
				"Adds to First Person distance", 
				new AcceptableValueRange<float>(-2,2)
			)
		);

		sideOffset = config.Bind<float>(
			name,
			"sideOffset",
			0.0f,
			new ConfigDescription(
				"Offset to left or right from car center", 
				new AcceptableValueRange<float>(-2,2)
			)
		);

	}
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    internal static new ManualLogSource Logger;

	internal static ConfigEntry<CameraType> cameraType;
	internal static ConfigEntry<float> nearClipPlane;

	internal static CameraTypeConfig followConfig;
	internal static CameraTypeConfig chaseConfig;
	internal static CameraTypeConfig firstPersonConfig;

	internal static Dictionary<string, CarTypeConfig> carsConfigs;

	private void BindConfig() {
		cameraType = Config.Bind<CameraType>(
			"General",
			"cameraType",
			CameraType.Follow,
			"Camera type"
		);

		nearClipPlane = Config.Bind<float>(
			"General",
			"nearClipPlane",
			1,
			new ConfigDescription("Near clip plane", new AcceptableValueRange<float>(0,20))
		);

		followConfig = new CameraTypeConfig(Config, "Follow", 1.0f, 1.0f, 2.0f);
		chaseConfig = new CameraTypeConfig(Config, "Chase", 2.0f, 8.0f, 10.0f);
		firstPersonConfig = new CameraTypeConfig(Config, "First Person", 0.0f, 0.0f, 100.0f);

		carsConfigs = new Dictionary<string, CarTypeConfig>();

		VehicleDescriptor[] allVehicles = VehicleDescriptors.All;
        if (allVehicles == null) return;

        foreach (var vehicle in allVehicles) {
            string vehicleId = vehicle.name;
            carsConfigs[vehicleId] = new CarTypeConfig(Config, vehicleId);
        }
	}
        
    private void Awake() {
        Logger = base.Logger;

		BindConfig();

		var instance = new Harmony(MyPluginInfo.PLUGIN_GUID);
		instance.PatchAll(typeof(PatchCamera));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
}
