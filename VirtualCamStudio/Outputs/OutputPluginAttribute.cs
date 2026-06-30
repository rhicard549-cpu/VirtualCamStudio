namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Attribute to provide metadata about an output plugin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class OutputPluginAttribute : Attribute
    {
        /// <summary>
        /// Gets the display name of the plugin.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the description of what the plugin does.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the plugin category.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the plugin version.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Creates a new output plugin attribute.
        /// </summary>
        /// <param name="name">The display name of the plugin.</param>
        /// <param name="description">The description of what the plugin does.</param>
        /// <param name="category">The plugin category (e.g., "Display", "Recording", "Streaming").</param>
        /// <param name="version">The plugin version (e.g., "1.0.0").</param>
        public OutputPluginAttribute(string name, string description, string category = "General", string version = "1.0.0")
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Category = category;
            Version = version;
        }
    }
}
