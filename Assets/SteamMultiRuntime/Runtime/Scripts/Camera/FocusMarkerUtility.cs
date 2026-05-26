using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// CinemachineCamera の TrackingTarget / Follow を Reflection 経由で取得・設定する共通ユーティリティ。
    /// </summary>
    internal static class FocusMarkerUtility
    {
        public static Transform GetTrackingTarget(CinemachineCamera camera)
        {
            if (camera == null)
            {
                return null;
            }

            if (TryGetMemberValue(camera, "TrackingTarget", out var trackingTarget))
            {
                return trackingTarget as Transform;
            }

            if (TryGetMemberValue(camera, "Follow", out var followTarget))
            {
                return followTarget as Transform;
            }

            if (TryGetNestedMemberValue(camera, "Target", "TrackingTarget", out var nestedTrackingTarget))
            {
                return nestedTrackingTarget as Transform;
            }

            return null;
        }

        public static void SetTrackingTarget(CinemachineCamera camera, Transform target)
        {
            if (camera == null)
            {
                return;
            }

            if (TrySetMemberValue(camera, "TrackingTarget", target))
            {
                return;
            }

            if (TrySetMemberValue(camera, "Follow", target))
            {
                return;
            }

            TrySetNestedMemberValue(camera, "Target", "TrackingTarget", target);
        }

        private static bool TryGetMemberValue(object instance, string memberName, out object value)
        {
            value = null;
            if (instance == null)
            {
                return false;
            }

            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanRead)
            {
                value = property.GetValue(instance);
                return true;
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                value = field.GetValue(instance);
                return true;
            }

            return false;
        }

        private static bool TrySetMemberValue(object instance, string memberName, object value)
        {
            if (instance == null)
            {
                return false;
            }

            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value);
                return true;
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(instance, value);
                return true;
            }

            return false;
        }

        private static bool TryGetNestedMemberValue(object instance, string memberName, string nestedMemberName, out object value)
        {
            value = null;
            if (!TryGetMemberValue(instance, memberName, out var nestedInstance) || nestedInstance == null)
            {
                return false;
            }

            return TryGetMemberValue(nestedInstance, nestedMemberName, out value);
        }

        private static bool TrySetNestedMemberValue(object instance, string memberName, string nestedMemberName, object value)
        {
            if (!TryGetMemberValue(instance, memberName, out var nestedInstance) || nestedInstance == null)
            {
                return false;
            }

            return TrySetMemberValue(nestedInstance, nestedMemberName, value);
        }
    }
}
