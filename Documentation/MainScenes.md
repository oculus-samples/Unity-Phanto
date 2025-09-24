# Main Scenes

After opening the project, you will find three main scenes:

## LobbyScene

![RescenScene](../Media/LobbyScene1.PNG 'Rescen Scene') ![StartGame](../Media/LobbyScene2.PNG 'Start Game')

[LobbyScene.unity](../Assets/Phanto/Scenes/LobbyScene.unity): This self-contained scene includes the introduction, shows the current mesh, and allows the player to start the game. The player can view, scan, and change their scene. If no scene information is provided, the player is guided to the **Space Setup** flow. Options include:

1. **TutorialScene**: starts the tutorial.
2. **GameScene**: starts the game.
3. **Trigger Space Setup**: using the left trigger, the player can restart the **Space Setup** process to rescan the environment.

## TutorialScene

![Tutorial0](../Media/Tutorial0.PNG 'Tutorial0') ![Tutorial1](../Media/Tutorial1.PNG 'Tutorial1') ![Tutorial2](../Media/Tutorial2.PNG 'Tutorial2') ![Tutorial3](../Media/Tutorial3.PNG 'Tutorial3') ![Tutorial4](../Media/Tutorial4.PNG 'Tutorial4') ![Tutorial5](../Media/Tutorial5.PNG 'Tutorial5')

[TutorialScene.unity](../Assets/Phanto/Scenes/TutorialScene.unity): Contains the tutorial, presenting controls and game dynamics. The tutorial introduces game mechanics, including:

- Using the Polterblast 3000
- Fighting Phanto
- Placing the Ecto Blaster
- Shooting and interacting with Phantoms

When running the app for the first time, the tutorial is mandatory. Afterward, players can repeat the tutorial or jump into the game.

## GameScene

![Game Controls](../Media/GameControls.png 'Controls')

![Gameplay_Blaster](../Media/GameplayBlaster.gif 'Gameplay_Blaster') ![Gameplay_Navigation](../Media/GameplayNavigation.gif 'Gameplay_Navigation')

[GameScene.unity](../Assets/Phanto/Scenes/GameScene.unity): This self-contained scene includes assets for gameplay, such as Phanto, Phantoms, and other components. Prefabs for main game components include:

- Phanto
- Phantoms
- Polterblast 3000
- Ecto Blaster

You can use the scene in standalone mode and run it using Oculus Link or by building and deploying it to your device. Restarting the game is as simple as reloading the scene.
