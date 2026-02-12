public abstract class BaseObstacle : BasePoolItem
{


    public ObstacleType obstacleType;


    protected virtual void Deactivate()
    {
        // ÂkÁÙ¦Û¤v
        ObstaclePool.Instance.ReturnObstacle(this);
    }
}