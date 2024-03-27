using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RebindingMenuManager : MonoBehaviour
{
    // Start is called before the first frame update
    public InputActionReference MoveRef, JumpRef, CrouchRef, SprintRef, UseRef, InventoryRef, InteractRef, CameraRotateRef, ChangeCameraRef, ZoomCameraRef, DashRef, AttackRef, RollRef, Ability1Ref, Ability2Ref, Ability3Ref, Ability4Ref, Ability5Ref, Ability6Ref, Ability7Ref; 

    private void OnEnable()
    {
        MoveRef.action.Disable();
        CrouchRef.action.Disable();
        SprintRef.action.Disable();
        JumpRef.action.Disable();
        Ability1Ref.action.Disable();
        Ability2Ref.action.Disable();
        Ability3Ref.action.Disable();
        Ability4Ref.action.Disable();
        Ability5Ref.action.Disable();
        Ability6Ref.action.Disable();
        Ability7Ref.action.Disable();
    }
    private void OnDisable()
    {
        MoveRef.action.Enable();
        CrouchRef.action.Enable();
        SprintRef.action.Enable();
        JumpRef.action.Enable();
        Ability1Ref.action.Enable();
        Ability2Ref.action.Enable();
        Ability3Ref.action.Enable();
        Ability4Ref.action.Enable();
        Ability5Ref.action.Enable();
        Ability6Ref.action.Enable();
        Ability7Ref.action.Enable();
    }
    // Update is called once per frame
}
