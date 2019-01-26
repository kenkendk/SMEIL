namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// The interface for an instantiated item
    /// </summary>
    public interface IInstance
    {
        /// <summary>
        /// The name of the item, or null for anonymous instances
        /// </summary>
        string Name { get; }
    }
}