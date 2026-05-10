using UnityEngine;

public class RunnerTrackSegment : MonoBehaviour
{
    public Transform entry;
    public Transform exit;

    public Vector3 EntryLocalPosition
    {
        get
        {
            if (entry == null)
            {
                return Vector3.zero;
            }
            return transform.InverseTransformPoint(entry.position);
        }
    }

    public Quaternion EntryLocalRotation
    {
        get
        {
            if (entry == null)
            {
                return Quaternion.identity;
            }
            return Quaternion.Inverse(transform.rotation) * entry.rotation;
        }
    }

    public Vector3 ExitLocalPosition
    {
        get
        {
            if (exit == null)
            {
                return Vector3.forward * 10f;
            }
            return transform.InverseTransformPoint(exit.position);
        }
    }

    public Quaternion ExitLocalRotation
    {
        get
        {
            if (exit == null)
            {
                return Quaternion.identity;
            }
            return Quaternion.Inverse(transform.rotation) * exit.rotation;
        }
    }

    public float Length
    {
        get
        {
            return Vector3.Distance(EntryLocalPosition, ExitLocalPosition);
        }
    }

    private void Reset()
    {
        AutoAssignMarkers();
    }

    public void AutoAssignMarkers()
    {
        entry = FindChildRecursive(transform, "Entry");
        exit = FindChildRecursive(transform, "Exit");
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
