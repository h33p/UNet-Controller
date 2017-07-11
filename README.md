# A CharacterController based controller for use in Unity's new Networking system.

###### Features
- Client-side prediction.
- Client-side reconciliation.
- Interpolation.
- Snapping to grid in order to suppress floating point nature.
- Inputs and results storing to combat big latency changes.
- Relatively low overhead.
- Quake-like strafing. But most likely very incomparable.
- Crouching.
- Animations.
- Toleration values, because the floating point errors.
- Network data analyzer to compare the results between the server and the client.
- First person and third person camera.
- Sliding of steep surfaces.
- Ability to set AI target (basic, moves straight towards the target).
- Recording and playing back gameplay.
- Foot IK.
- Ragdoll.

###### Performance (On 2015 MacBook Pro with quad-core i7)
- Up to 1 millisecond on server every time a network update happens.
- Up to 1 millisecond on client every time a network update happens. Sometimes it jumps a bit higher if there are a lot of inputs to replay during reconciliation. That is due to high latency.
- Very small overhead on non local clients moving.
- GC allocations 80 bytes per player. This is due to CharacterController.

###### How does it run fast
- It moves character only when network update occurs and then interpolate until the next network update in LateUpdate.

###### Current issues
- Update Once and Lerp mode does not work well with the update rates over 50hz. Use lower send rate or a higher one with Update Once mode.

##### TODO
- Extend gameplay recording to support regular game objects.
- Implement physics and other APIs with lag compensation (currently work in progress).
- Add jumping between updates so jumping would feel good even on small update periods.
- Fix issues.
