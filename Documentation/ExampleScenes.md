# Example Scenes

To simplify development, there are example scenes showcasing best practices with each Presence Platform component:

## ContentPlacement

![Content Placement](../Media/ContentPlacement.gif 'Content Placement')

[ContentPlacement.unity](../Assets/Phanto/Scenes/Features/ContentPlacement.unity) uses the mesh for content placement. The example takes the Blaster from the game and shows how to use the mesh to place it anywhere in the room.

## MeshCollisions

![Mesh Collisions](../Media/MeshCollisions.gif 'Mesh Collisions')

[PhantomCollisions.unity](../Assets/Phanto/Scenes/Features/MeshCollisions.unity) demonstrates using the mesh for physics. Using fast collisions, the Ectoplasma bounces off the mesh, creating a realistic experience.

## AirNavigation

![Air Navigation](../Media/AirNavigation.gif 'Air Navigation')

[AirNavigation.unity](../Assets/Phanto/Scenes/Features/AirNavigation.unity) shows how to use the scanned mesh as a sensor for an air-navigated character (Phanto).

## MeshNavigation

![Mesh Navigation](../Media/MeshNavigation.gif 'Mesh Navigation')

[MeshNavigation.unity](../Assets/Phanto/Scenes/Features/MeshNavigation.unity) shows how to use the mesh for ground navigation, with and without additional bounding box information on furniture (acquired using manual capture of room elements).

## SceneVisualization

![Scene Visualization](../Media/SceneVisualization.gif 'Scene Visualization')

[SceneVisualization.unity](../Assets/Phanto/Scenes/Features/SceneVisualization.unity) is a debug scene that presents the mesh and furniture bounding box, if available.

## SemanticSceneQuery

![Semantic Scene Query](../Media/SemanticSceneQuery.gif 'Semantic Scene Query')

[SemanticSceneQuery.unity](../Assets/Phanto/Scenes/Features/SemanticSceneQuery.unity) demonstrates how to use automatically discovered furniture in the scene. Phantoms use the Scene Mesh for spawning, targeting, navigating, and attacking crystals. The phantoms' thought bubble enhances immersion, allowing advanced path planning based on detected furniture.

## DebugDrawingScene

![Debug Scene](../Media/DebugDrawScene.gif 'Debug Scene')

[DebugDrawingScene.unity](../Assets/Phanto/Scenes/Features/DebugDrawingScene.unity) is a debug scene showcasing developer debug tools.

## UserInBounds

![User In Bounds](../Media/UserInBounds.gif 'User In Bounds')

[UserInBounds.unity](../Assets/Phanto/Scenes/Features/UserInBounds.unity) demonstrates best practices for handling cases when the user is outside the scene. When leaving the scene bounds, the user is notified and presented with an option to rescan the space. [InsideSceneChecker.cs](../Assets/Phanto/Samples/Scripts/InsideSceneChecker.cs) is attached to the camera prefab and notifies the app when the user's head or hands are inside/outside the bounds.

## DepthOcclusion

![Depth Occlusion](../Media/DepthOcclusions.gif 'Depth Occlusion')

[DepthOcclusion.unity](../Assets/Phanto/Scenes/Features/DepthOcclusion.unity) demonstrates best practices for dynamic occlusion using the [Depth API](https://developers.meta.com/horizon/documentation/unity/unity-depthapi-overview/), which uses real-time depth estimation for occlusions. To mitigate performance impact, a mixture of soft and hard occlusions were selected for each element in the game. Visit the [Depth API open-source repository](https://github.com/oculus-samples/Unity-DepthAPI) to learn more and try the new SDK.

## HapticsDemo

![Haptics Demo](../Media/HapticsDemo.gif 'Haptics Demo')

[HapticsDemo.unity](../Assets/Phanto/Scenes/Features/HapticsDemo.unity) showcases the integration of haptics with dynamic modulation tied to controller interactions and virtual objects: Phanto floats in the middle of the room and triggers a synchronized audio-haptic effect when "poked". Pulling the right controller trigger increases the effect's amplitude, while moving the thumbstick modulates the frequency.

The haptic assets used in this project were designed with [Haptics Studio](https://developers.meta.com/horizon/documentation/unity/haptics-studio/) and integrated using the [Haptics SDK for Unity](https://developers.meta.com/horizon/documentation/unity/unity-haptics-sdk/) following our [Haptic Design Guidelines](https://developers.meta.com/horizon/design/haptics-overview/).

To learn more about the Haptics SDK for Unity and how dynamically modulated haptics were implemented, check out [HapticsDemoController.cs](../Assets/Phanto/Samples/Scripts/HapticsDemoController.cs) for the demo scene or [PolterblastTrigger.cs](../Assets/Phanto/Polterblast/Scripts/PolterblastTrigger.cs) for the Polterblast haptics featured in the main game loop.
