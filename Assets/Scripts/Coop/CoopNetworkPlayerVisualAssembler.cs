using UnityEngine;
using UnityEngine.Animations.Rigging;

public static class CoopNetworkPlayerVisualAssembler
{
    private const string NetworkAimTargetName = "Aim_Target";

    public static GameObject CreateVisualTemplate(Player sourcePlayer, Transform parent)
    {
        if (sourcePlayer == null)
            return null;

        GameObject template = Object.Instantiate(sourcePlayer.gameObject, parent);
        template.name = "NetworkPlayerVisualTemplate";
        template.SetActive(false);
        StripSinglePlayerRuntime(template);
        ReplaceAnimationEvents(template);
        return template;
    }

    public static void PrepareAssignedVisualSource(GameObject visualSource)
    {
        if (visualSource == null)
            return;

        StripSinglePlayerRuntime(visualSource);
        ReplaceAnimationEvents(visualSource);
    }

    public static void AttachVisual(GameObject template, NetworkPlayer networkPlayer)
    {
        if (template == null || networkPlayer == null)
            return;

        GameObject playerBody = Object.Instantiate(template, networkPlayer.transform);
        playerBody.name = "PlayerBody";
        playerBody.SetActive(true);
        playerBody.transform.localPosition = Vector3.zero;
        playerBody.transform.localRotation = Quaternion.identity;
        playerBody.transform.localScale = Vector3.one;

        StripAimAndCameraTargetColliders(playerBody);
        WireReferences(networkPlayer, playerBody);
    }

    private static void WireReferences(NetworkPlayer networkPlayer, GameObject playerBody)
    {
        Transform aimTarget = FindChildByName(playerBody.transform, "Aim_Target")
            ?? FindChildByName(playerBody.transform, "AimTarget");

        if (aimTarget == null)
            aimTarget = CreateNetworkAimTarget(networkPlayer.transform);

        RewireAimConstraints(playerBody, aimTarget);
        RebuildRig(playerBody);

        Transform cameraTarget = FindChildByName(playerBody.transform, "CameraFollow_Target")
            ?? FindChildByName(playerBody.transform, "cameraTarget")
            ?? FindChildByName(playerBody.transform, "CameraTarget");

        WeaponModel firstWeaponModel = playerBody.GetComponentInChildren<WeaponModel>(true);
        Transform gunPoint = firstWeaponModel != null && firstWeaponModel.gunPoint != null
            ? firstWeaponModel.gunPoint
            : FindChildByName(playerBody.transform, "GunPoint");

        networkPlayer.ConfigureVisualReferences(aimTarget, gunPoint, cameraTarget);

        CoopPlayerPresentation presentation = networkPlayer.GetComponent<CoopPlayerPresentation>();
        if (presentation == null)
            return;

        presentation.ConfigureFromPlayerBody(
            playerBody.transform,
            playerBody.GetComponentInChildren<Animator>(true),
            aimTarget,
            playerBody.GetComponentInChildren<LineRenderer>(true),
            playerBody.GetComponentInChildren<Player_SoundFX>(true),
            playerBody.GetComponentInChildren<Rig>(true),
            playerBody.GetComponentInChildren<TwoBoneIKConstraint>(true),
            FindChildByName(playerBody.transform, "LeftHandIK_Target"));
    }

    private static Transform CreateNetworkAimTarget(Transform parent)
    {
        GameObject aimTargetObject = new GameObject(NetworkAimTargetName);
        Transform aimTarget = aimTargetObject.transform;
        aimTarget.SetParent(parent, false);
        aimTarget.localPosition = Vector3.forward * 8f + Vector3.up;
        aimTarget.localRotation = Quaternion.identity;
        aimTarget.localScale = Vector3.one;
        return aimTarget;
    }

    private static void RewireAimConstraints(GameObject playerBody, Transform aimTarget)
    {
        MultiAimConstraint[] constraints = playerBody.GetComponentsInChildren<MultiAimConstraint>(true);

        foreach (MultiAimConstraint constraint in constraints)
        {
            WeightedTransformArray sources = new WeightedTransformArray(0);
            sources.Add(new WeightedTransform(aimTarget, 1f));
            constraint.data.sourceObjects = sources;
        }
    }

    private static void RebuildRig(GameObject playerBody)
    {
        RigBuilder rigBuilder = playerBody.GetComponentInChildren<RigBuilder>(true);

        if (rigBuilder == null || Application.isPlaying == false)
            return;

        rigBuilder.Build();
    }

    private static void StripSinglePlayerRuntime(GameObject root)
    {
        RemoveComponents<Player>(root);
        RemoveComponents<Player_Movement>(root);
        RemoveComponents<Player_AimController>(root);
        RemoveComponents<Player_WeaponController>(root);
        RemoveComponents<Player_WeaponVisuals>(root);
        RemoveComponents<Player_Interaction>(root);
        RemoveComponents<Player_Health>(root);
        RemoveComponents<Player_Hitbox>(root);
        RemoveComponents<CharacterController>(root);
        RemoveComponents<Ragdoll>(root);
        RemoveComponents<CharacterJoint>(root);
        RemoveComponents<ConfigurableJoint>(root);
        RemoveComponents<HingeJoint>(root);
        RemoveComponents<FixedJoint>(root);
        RemoveComponents<SpringJoint>(root);
        RemoveComponents<Rigidbody>(root);
        StripAimAndCameraTargetColliders(root);
        RemoveComponents<Collider>(root);
    }

    private static void StripAimAndCameraTargetColliders(GameObject root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (IsAimOrCameraTargetName(child.name) == false)
                continue;

            Collider[] colliders = child.GetComponents<Collider>();
            foreach (Collider collider in colliders)
                Object.DestroyImmediate(collider);
        }
    }

    private static bool IsAimOrCameraTargetName(string objectName)
    {
        return objectName == "Aim_Target"
            || objectName == "AimTarget"
            || objectName == "CameraFollow_Target"
            || objectName == "CameraTarget"
            || objectName == "cameraTarget";
    }

    private static void ReplaceAnimationEvents(GameObject root)
    {
        Player_AnimationEvents[] animationEvents = root.GetComponentsInChildren<Player_AnimationEvents>(true);
        foreach (Player_AnimationEvents animationEvent in animationEvents)
        {
            GameObject target = animationEvent.gameObject;
            Object.DestroyImmediate(animationEvent);
            target.AddComponent<CoopPlayerAnimationEvents>();
        }
    }

    private static void RemoveComponents<T>(GameObject root) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);
        foreach (T component in components)
            Object.DestroyImmediate(component);
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
