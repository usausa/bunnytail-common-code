namespace BunnyTail.CommonCode;

public interface IDeepCloneable<out T>
{
    T DeepClone();
}
