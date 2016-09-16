# A CharacterController based controller for use in Unity's new Networking system.

###### Features
- Client-side prediction.
- Client-side reconciliation.
- Interpolation.
- Realtively low overhead.

###### Performance
- Around 1 millisecond on server every time a network update happens.
- Around 1-2 milliseconds on client every time a network update happens. Sometimes it jumps to 5 milliseconds when there is more to reconciliate or more to sort (GC Alloc happens on both).
- Very small overhead on non local clients moving.

###### How does it run fast
- It moves character only when network update occurs and then interpolate until the next network update in LateUpdate.
- Quite big GC allocations happen (150 bytes constantly and up to 500 bytes on client)

###### Current issues
- Timing is not right. There are some issues if client or server freezes for a moment, client begins getting a lot of prediction errors. The same happens when the latency is changing which currently makes it not ideal in real games.

##### TODO
- Fix timing issues.
- Add some sort of forgiving mode to allow client send the position it thinks is right and if it is close enough, then make the server move towards client set position.
- Implement options for different kinds of interpolation (curve based, slerp based and more).
- Add third person movement.
- Eliminate GC allocations.
- Serialize data into bytes and send over network to minimize network bandwidth.
