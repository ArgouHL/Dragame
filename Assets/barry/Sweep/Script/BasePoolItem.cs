using UnityEngine;

public abstract class BasePoolItem : MonoBehaviour
{
    public Vector3 initialScale;
    public virtual void ResetState()
    {
        {
            transform.localScale = initialScale;
        }
    }

}
