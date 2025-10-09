using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WileyWidget.ViewModels
{
    /// <summary>
    /// Represents a conversation mode for the AI assistant
    /// </summary>
    public class ConversationMode
    {
        /// <summary>
        /// Display name of the conversation mode
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this mode does
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Icon emoji for the mode
        /// </summary>
        public string Icon { get; set; } = string.Empty;
    }
}