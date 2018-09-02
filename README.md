# An authoritative CharacterController based controller for use in Unity's UNet.

###### Features
- Client-side prediction.
- Client-side reconciliation.
- Interpolation.
- Snapping to grid in order to suppress floating point nature.
- Relatively low overhead.
- Quake-like strafing. But most likely very incomparable.
- Crouching.
- Animations.
- Network data analyzer to compare the results between the server and the client.
- First person and third person camera.
- Sliding of steep surfaces.
- Ability to set AI target (basic, moves straight towards the target).
- Recording and playing back gameplay.
- Foot IK.
- Ragdoll.
- Lag compensation (WIP).

###### Performance (On 2015 MacBook Pro with quad-core i7)
- Up to 1 millisecond on server every time a network update happens.
- Up to 1 millisecond on client every time a network update happens. Sometimes it jumps a bit higher if there are a lot of inputs to replay during reconciliation. That is due to high latency.
- Very small overhead on non local clients moving.
- GC allocations 80 bytes per player. This is due to CharacterController.
- Way higher GC allocations on the server side (and while recording gameplay for that matter), due to the need of storing history of the objects for lag compensation.

###### How does it run fast
- It moves character only when network update occurs and then interpolate until the next network update in LateUpdate.
- Efficient data reusing with as least locals as possible (as they cause GC allocations).

###### Current issues
- Update Once and Lerp mode does not work well with the update rates over 50hz. Use lower send rate or a higher one with Update Once mode.

##### TODO
- Add jumping between updates so jumping would feel good even on small update periods.
- Complete lag compensation.
- Fix issues.
