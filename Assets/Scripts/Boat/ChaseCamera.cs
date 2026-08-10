using UnityEngine;

namespace MaritimeScenario.Boat
{
    /// <summary>
    /// Simple "chase cam": follows a target (the vessel) keeping a smoothed relative
    /// offset. It only acts in Play (LateUpdate does not run in edit mode), so it does
    /// not disturb the static framing of the scene.
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0f, 2.2f, -6f);
        public Vector3 LookAtHeight = new Vector3(0f, 1f, 0f);
        public float SmoothSpeed = 4f;

        /// <summary>
        /// Smoothly moves the camera toward the target's offset position and makes it
        /// look at the target. Runs after all movement so it never lags a frame behind.
        /// </summary>
        void LateUpdate()
        {
            if (Target == null) return;

            Vector3 desiredPosition = Target.TransformPoint(Offset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, SmoothSpeed * Time.deltaTime);
            transform.LookAt(Target.position + LookAtHeight);
        }
    }
}
