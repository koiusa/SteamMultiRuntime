using Unity.Mathematics;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal struct NpcControllerInputCommand
    {
        public Vector2 MoveInput;
        public Vector3 MoveDirection;
        public bool JumpRequested;
        public bool WireHeld;
        public bool WireFireRequested;
        public float ReelInput;
        public Vector3 WireTarget;
    }

    internal struct NpcCrowdAgentData
    {
        public float3 Position;
        public float3 Velocity;
        public float3 GoalVelocity;
        public float3 UpAxis;
        public float Radius;
        public float TimeHorizon;
        public float GoalWeight;
        public float AvoidanceWeight;
        public float SeparationExponent;
        public float MinApproachSpeed;
        public float ForwardDotMin;
        public int MaxNeighbors;
        public int Mode;
        public int UseForwardFilter;
    }

    internal struct NpcCrowdCommand
    {
        public Vector3 DesiredVelocity;
        public bool JumpRequested;
        public ActorTraversalState TraversalState;
        public Vector3 WireAnchor;
        public float WireRopeLength;
    }

    [System.Serializable]
    public struct NpcCrowdContactSettings
    {
        public bool EnablePlayerContacts;
        public bool EnableNetworkPhysicsObjectContacts;
        [Min(0f)] public float BroadphasePadding;
        [Range(0f, 1f)] public float PenetrationResolution;
        public bool ApplyImpulse;

        public static NpcCrowdContactSettings CreateDefault() => new()
        {
            EnablePlayerContacts = true,
            EnableNetworkPhysicsObjectContacts = true,
            BroadphasePadding = 0.5f,
            PenetrationResolution = 1f,
            ApplyImpulse = true
        };
    }

    internal struct NpcCrowdMovementData
    {
        public float3 Position;
        public float3 Velocity;
        public float3 DesiredPlanarVelocity;
        public float3 UpAxis;
        public float3 GroundDisplacement;
        public float3 GroundVelocity;
        public float GroundCoordinate;
        public int HasGroundSurface;
        public float MoveSpeed;
        public float Acceleration;
        public float RotationSpeed;
        public float JumpSpeed;
        public float Gravity;
        public int Grounded;
        public int JumpRequested;
        public int AirborneFromJump;
        public int TraversalMode;
        public float3 WireAnchor;
        public float WireRopeLength;
        public float3 WallNormal;
        public float WallDistance;
        public int HasWall;
    }

    internal struct NpcCrowdMovementResult
    {
        public float3 Position;
        public float3 Velocity;
        public int Grounded;
        public int AirborneFromJump;
    }
}
