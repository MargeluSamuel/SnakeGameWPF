using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SnakeGameWPF
{
    public class SnakePart
    {
        public UIElement _UIElement { get; set; }
        public Point _position { get; set; }

        public bool _isHead {  get; set; }
    }
}
