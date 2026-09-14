using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace English.Models
{
    public static class VisualElementExtensions
    {
        public static T FindParent<T>(this Element element) where T : class
        {
            var parent = element.Parent;
            while (parent != null)
            {
                if (parent is T result) return result;
                parent = parent.Parent;
            }
            return null;
        }
    }
}
