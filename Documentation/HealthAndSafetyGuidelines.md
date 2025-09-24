# Health & Safety Guidelines

When building mixed reality experiences, evaluate your content from a health and safety perspective to offer users a comfortable and safe experience. Please read the [Mixed Reality H&S Guidelines](https://developer.oculus.com/resources/mr-health-safety-guideline/) before designing and developing your app using this sample project or any of our Presence Platform features.

## Avoiding Improper Occlusion

Avoid improper occlusion, which occurs when virtual content does not respect the physicality of the user's environment. Improper occlusion can result in a misperception of actionable space.

- See [Occlusions with Virtual Content](https://developers.meta.com/horizon/resources/mr-health-depth#occlusion-with-virtual-content)
- Ensure users have completed Space Setup and granted [Spatial Data permission](https://developers.meta.com/horizon/documentation/unity/unity-spatial-data-perm/) to allow proper occlusion in content placement, mesh collisions, and air navigation.

## Using Semi-Transparent Content

Using semi-transparent content lets users better view their physical space and reduces the occlusion of objects or people not part of the scanned mesh.

- Spatial data won't incorporate dynamic elements of a user's living space (e.g., a chair moved after capture or a moving person/pet in the space).
- Uncaptured dynamic elements may be occluded by virtual content, making it more difficult for users to safely avoid such hazards while engaged in the mixed reality experience.

## Personal Space Guidelines

Respect the user's personal space. Avoid having virtual content pass through their body or loom close to their face. When content crosses into a user's personal space, they may experience psychological or visual discomfort or take actions to avoid the virtual content that may increase the risk of injury or damage.

- [PersonalBubble.cs](../Assets/Phanto/Player/PersonalBubble.cs) is an example of how to implement a "personal bubble" as part of the nav mesh. Add this script to the player camera rig to prevent Phanto from getting too close to it.
- The circumference of the 'personal bubble' may be altered to provide more space. The faster that virtual content approaches a user, the larger the circumference may need to be tuned.
