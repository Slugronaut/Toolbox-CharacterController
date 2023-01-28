# Toolbox-CharacterController
A physics-based first-person character controller.

This character controller is based off of Unity's built-in Rigidbody component which allows it to easily interact with other physical events and objects in the world. However, it remains snappy and responsive and provides compensation factors to still allow for that 'arcade' feel that most games require.

It is setup for first-person use here but with slight amounts of tweaking it could easily be adapted for third-person behind the shoulder, top-down, or side-scrolling use as well.

Dependencies:  
com.unity.inputsystem  
[com.postegames.gcci](https://github.com/Slugronaut/Toolbox-GCCI)  
(GCCI is not a super hard dependecy and can easily have the interface section commented out. I'll probably add conditional compilation for it later)
