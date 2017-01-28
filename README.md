# A CharacterController based controller for use in Unity's new Networking system.

###### Features
- Client-side prediction.
- Client-side reconciliation.
- Interpolation.
- Snapping to grid in order to suppress floating point nature.
- Inputs and results storing to combat rapid latency changes.
- Relatively low overhead.
- Quake-like strafing. But most likely very incomparable.

###### Performance (On 2015 MacBook Pro with quad-core i7)
- Up to 1 millisecond on server every time a network update happens.
- Up to 1 millisecond on client every time a network update happens. Sometimes it jumps a bit higher if there are a lot of inputs to replay during reconciliation. That is due to high latency.
- Very small overhead on non local clients moving.

###### How does it run fast
- It moves character only when network update occurs and then interpolate until the next network update in LateUpdate.

###### Current issues
- Quite big GC allocations happen on RPCs internaly (up to 500 bytes). That might be because Unity needs to parse custom classes. A byte serializer will be written to combat this and optimize bandwidth.

##### TODO
- Add some sort of forgiving mode to allow client send the position it thinks is right and if it is close enough, then make the server move towards client set position.
- Implement options for different kinds of interpolation (curve based, slerp based and more).
- Add third person movement.
- Eliminate GC allocations.
- Add jumping between updates so jumping would feel good even on small update periods.
- Serialize data into bytes and send over network to minimize network bandwidth.
