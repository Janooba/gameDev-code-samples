using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleCameraRig : MonoBehaviour
{
    public ExampleCharacterController controller;
    public new Camera camera;
    public float FollowSharpness = 1000f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Update()
    {
        transform.position = Vector3.Lerp(transform.position, controller.head.position, FollowSharpness * Time.deltaTime);
        transform.rotation = controller.head.rotation;
    }
}
