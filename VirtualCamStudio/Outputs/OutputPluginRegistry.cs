using System.Reflection;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Information about an output plugin.
    /// </summary>
    public class OutputPluginInfo
    {
        /// <summary>
        /// Gets the plugin type.
        /// </summary>
        public Type PluginType { get; }

        /// <summary>
        /// Gets the plugin display name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the plugin description.
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
        /// Creates a new output plugin info.
        /// </summary>
        public OutputPluginInfo(Type pluginType, string name, string description, string category, string version)
        {
            PluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Category = category;
            Version = version;
        }

        /// <summary>
        /// Creates an instance of this plugin.
        /// </summary>
        /// <param name="constructorArgs">Optional constructor arguments.</param>
        /// <returns>A new instance of the plugin.</returns>
        public IOutputTarget CreateInstance(params object[] constructorArgs)
        {
            return (IOutputTarget)Activator.CreateInstance(PluginType, constructorArgs)!;
        }
    }

    /// <summary>
    /// Utility for discovering and managing output plugins.
    /// </summary>
    public static class OutputPluginRegistry
    {
        /// <summary>
        /// Discovers all output plugins in the current assembly.
        /// </summary>
        /// <returns>A list of discovered plugin information.</returns>
        public static List<OutputPluginInfo> DiscoverPlugins()
        {
            var plugins = new List<OutputPluginInfo>();
            var assembly = Assembly.GetExecutingAssembly();

            // Find all types that implement IOutputTarget
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IOutputTarget).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in pluginTypes)
            {
                // Get plugin metadata from attribute
                var attribute = type.GetCustomAttribute<OutputPluginAttribute>();

                if (attribute != null)
                {
                    var info = new OutputPluginInfo(
                        type,
                        attribute.Name,
                        attribute.Description,
                        attribute.Category,
                        attribute.Version
                    );

                    plugins.Add(info);
                }
                else
                {
                    // Plugin without attribute - use type name as fallback
                    var info = new OutputPluginInfo(
                        type,
                        type.Name,
                        $"Output plugin: {type.Name}",
                        "General",
                        "1.0.0"
                    );

                    plugins.Add(info);
                }
            }

            return plugins.OrderBy(p => p.Category).ThenBy(p => p.Name).ToList();
        }

        /// <summary>
        /// Gets plugin information for a specific plugin type.
        /// </summary>
        /// <typeparam name="T">The plugin type.</typeparam>
        /// <returns>Plugin information, or null if not found.</returns>
        public static OutputPluginInfo? GetPluginInfo<T>() where T : IOutputTarget
        {
            var plugins = DiscoverPlugins();
            return plugins.FirstOrDefault(p => p.PluginType == typeof(T));
        }

        /// <summary>
        /// Gets all plugins in a specific category.
        /// </summary>
        /// <param name="category">The category name.</param>
        /// <returns>A list of plugins in the specified category.</returns>
        public static List<OutputPluginInfo> GetPluginsByCategory(string category)
        {
            var plugins = DiscoverPlugins();
            return plugins.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}
