# Realtime Fracture

<video src="https://i.imgur.com/WlymY0V.mp4" width="320" height="200"></video>

An in-progress realtime destruction system for use with the Unity game engine, this project aims to allow for more dynamic procedural destruction than is possible using precomputation methods. This fracturing allows for the fragmentation of objects into many pieces in real time.
![mesh example 1](https://i.imgur.com/gELhGgu.png)

## Mesh Splitting
The current version of this system is designed for use with manifold, convex meshes using a single material.
The single material limitation is simply due to an assumption by the current implementation, and is easily remidied.
The additional treatment of concave meshes is possible, but would require tracking and checking for connected components with each fracture (to ensure disconnected pieces of geometry are seperated into independent objects, and identifying independent faces along the cut plane). Such a feature has not yet been implemented.
The manifold mesh requirement should remain, as user-expected behaviour for non-manifold meshes would not be universal.
![mesh example 2](https://i.imgur.com/49Avcsa.png)
### How it Works
Meshes in Unity are defined using an array of indices, representing triangles, which reference vertices in a vertex array. UV, normal, tangent, and other datasets map to the vertex array 1:1. For the sake of mesh splitting, a new collection is created, much like the triangle array, but instead defining ngons; vertices are still indexed, with sequences of indices defining an ngon, and these sequences seperated by values of -1. Mesh Splits are performed on this ngon data, and the new mesh data is generated using these results. This provides one major advantage over working directly with triangle data: the resulting mesh does not contain redundant data, while performing such an operation on triangle data can lead to a runaway increase in geometry complexity (an issue found in earlier tests).

## The Physics Fracture Component
There are a number of approximations involved in the physics fracturing procedure, most due to practial limitations. As fracture mechanics relies on physical information that is entirely unavailable in a realtime rigidbody simulation, in which not even peak force of collisions is available, a new approach was taken.
* For instantaneous collisions, the total energy loss due to the collision is used to calculate the amount of energy available to fracture a breakable object. While this works well for inelastic collisions and glancing collisions, it does not reflect the reality of elastic collisions very well. Instead, it may be better to use the total energy of the collision, account for energy losses due to fractures and then use the resulting energy loss to alter the objects' momenta.
* Two surface energies are attributed to a breakable object; a **Fracture Energy** which is analogous to chemical activation energy, and a **Surface Energy** which is analogous to activation energy minus the enthalpy of formation. The naming of these variables is obviously not ideal, and subject to change.
* The surface area of a cut, used to calculate the total energy neccesary to perform a bisection, is approximately proportional to the cross section of an ellipse which fully encompases the bounding box, though this needs to be verified and is also subject to change. The intertia tensor could be used to provide a more accurate measure of this cross section, or it could be checked explicitly after performing mesh splitting calculations, though this would be significantly more expensive for low energy collisions, as it requires performing mesh split calculations even when no split takes place.
* The position and orientation of bifurcation planes is chosen based on a few criteria. The position of the plane is determined based on an impact point of the collision, with a random offset determined by the **Mean Fault Distance**, with a distribution based on the expected distance to the nearest point of a 3D Poisson point process. The normal of the plane is simply weighted by the size vector of the objects' bounding box.
* **Fracture Min Mass** is a variables used to prevent the fracturing of objects into unreasonably small fragments. Fragments with mass below the given minimum limit will no longer fracture (note that mass is divided between fragments after fractures based on bounding box size).
* Collisions with insuffucient energy to cause fractures over circular area with radius **Min Fracture Radius** are ignored, for performance consideration.
* Continuous collisions, e.g. force applied by a press, are not currently handled, as the 'activation energy' model above is unuseable for such situations. A new model would need to be created to handle these collisions, if the need arises.
