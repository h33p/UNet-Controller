# A CharacterController based controller for use in Unity's new Networking system.

###### Features
- Client-side prediction.
- Client-side reconciliation.
- Interpolation.
- Snapping to grid in order to suppress floating point nature.
- Inputs and results storing to combat rapid latency changes.
- Relatively low overhead.

###### Performance
- Around 1 millisecond on server every time a network update happens.
- Around 1-2 milliseconds on client every time a network update happens. Sometimes it jumps to 5 milliseconds when there is more to reconciliate or more to sort (GC Alloc happens on both).
- Very small overhead on non local clients moving.

###### How does it run fast
- It moves character only when network update occurs and then interpolate until the next network update in LateUpdate.

###### Current issues
- Quite big GC allocations happen (150 bytes constantly and up to 500 bytes on client)

##### TODO
- Add some sort of forgiving mode to allow client send the position it thinks is right and if it is close enough, then make the server move towards client set position.
- Implement options for different kinds of interpolation (curve based, slerp based and more).
- Add third person movement.
- Eliminate GC allocations.
- Add jumping between updates so jumping would feel good even on small update periods.
- Clean the code even more, with possible custom defineable functions to be called during movement in order to allow customizing behavior easily.
- Serialize data into bytes and send over network to minimize network bandwidth.
