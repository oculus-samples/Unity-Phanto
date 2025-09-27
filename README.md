![Phanto Banner](./Media/PhantoTitle.png 'Phanto')

# Project Phanto - Presence Platform Reference App

Project Phanto is a Mixed Reality (MR) reference app that highlights the latest Presence Platform features, such as scene mesh, Scene Model, and Scene API objects.

This codebase serves as a reference and template for MR projects. You can test the experience by building and deploying it to your Meta Quest device.

## Project Description

This project showcases scene mesh functionality, Scene Model integration, Scene API objects, haptic feedback, and mixed reality navigation systems.

Built using the [Unity engine](https://unity.com/) with Unity 6 or higher, it includes haptic assets designed with [Haptics Studio](https://developers.meta.com/horizon/documentation/unity/haptics-studio/) and integrated using the [Haptics SDK for Unity](https://developers.meta.com/horizon/documentation/unity/unity-haptics-sdk/).

## How to Run the Project in Unity

1. Use *Unity 6* or higher.
2. Load a [Main Scene](./Documentation/MainScenes.md) or [Example Scene](./Documentation/ExampleScenes.md).
3. Choose one of three running options:
    - **Quest 3**: Build, deploy, and run the game on your headset.
    - **Oculus Link** (Windows only):
        <details>
          <summary><b>Oculus Link</b></summary>

        - Open the Oculus app and run Oculus Link from the headset.
        - Select **Scene Api** as the **Scene Data Source** in SceneDataLoaderSettings.asset.
        - With the headset on, navigate to Unity and press "Play".
        - **Note**: Scene mesh and room elements will show up in Link. You can only trigger room scan from within the headset.
        </details>

    - **Static Mesh**: Select **Static Mesh Data** as the **Scene Data Source** for development without Scene Model on headset.

## Dependencies

This project uses the following plugins and software:

- [Unity](https://unity.com/download) 6 or higher
- [Meta XR Interaction SDK OVR Integration v67](https://developers.meta.com/horizon/downloads/package/meta-xr-interaction-sdk-ovr-integration/67.0)
- [Haptics Studio](https://developers.meta.com/horizon/documentation/unity/haptics-studio/)
- [Haptics SDK for Unity](https://developers.meta.com/horizon/documentation/unity/unity-haptics-sdk/)
- [XRGizmos](https://github.com/darktable/XRGizmos.git)
- [Graphy](https://github.com/Tayx94/graphy.git)

To test this project within Unity, you need:

- [The Meta Quest App](https://www.meta.com/quest/setup/)
- Mac or Windows

## Getting the Code

First, ensure you have Git LFS installed by running:

```sh
git lfs install
```

Then, clone this repository using the "Code" button above or this command:

```sh
git clone https://github.com/oculus-samples/Unity-Phanto
```

## Documentation

More information is available in the sections below:

- [Health & Safety Guidelines](./Documentation/HealthAndSafetyGuidelines.md)
- [Design Flow](./Documentation/DesignFlow.md)
- [Device Compatibility](#device-compatibility)
- [Key Components](./Documentation/KeyComponents.md)
- [Main Scenes](./Documentation/MainScenes.md)
- [Example Scenes](./Documentation/ExampleScenes.md)

## Device Compatibility

| Device    | Scene API | Color Passthrough | High res color | Scene Mesh | Haptics<sup>[1](#HapticsQuality)</sup> |
| :-------- | :-------: | :---------------: | :------------: | :--------: | :------------------------------------: |
| Quest 3   |    ✔️     |        ✔️         |       ✔️       |     ✔️     |                   ✔️                   |
| Quest Pro |    ✔️     |        ✔️         |       ❌       |     ❌     |                   ✔️                   |
| Quest 2   |    ✔️     |        ❌         |       ❌       |     ❌     |                   ✔️                   |

<a name="HapticsQuality">1</a>: There have been significant improvements in the haptics capability of Quest Pro and Quest 3 controllers over Quest 2: Quest Pro and Quest 3 introduce TruTouch haptics, enabling a new level of immersion in your applications. For more information, visit our [Haptic Design Guidelines](https://developers.meta.com/horizon/design/haptics-overview/#meta-quest-platform-and-haptic-hardware-considerations).

## License

This codebase is available as both a reference and a template for mixed reality projects.

Most of Phanto is licensed under the [MIT License](./LICENSE.txt); however, files from [Text Mesh Pro](https://unity.com/legal/licenses/unity-companion-license) are licensed under their respective terms.

### Dependencies Licenses

[XRGizmos](https://github.com/darktable/XRGizmos.git) is sourced from https://github.com/darktable/XRGizmos.git. The License for XRGizmos can be found [here](https://github.com/darktable/XRGizmos/blob/main/LICENSE.txt).

[Graphy](https://github.com/Tayx94/graphy.git) is sourced from https://github.com/Tayx94/graphy.git. The License for Graphy can be found [here](https://github.com/Tayx94/graphy/blob/master/LICENSE).

## Contribution

See the [CONTRIBUTING](./CONTRIBUTING.md) file for information on how to contribute.
