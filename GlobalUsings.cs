// Explicit global usings instead of ImplicitUsings.
//
// Why: the project uses System.Windows.Forms.NotifyIcon (the tray icon), and
// along with it the SDK pulls System.Drawing and System.Windows.Forms into the
// global namespace. That makes Color, Brush, Rectangle, Application and
// KeyEventArgs ambiguous in every WPF file. So we import exactly what is
// needed and resolve the collisions with aliases.

global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows;
global using System.Windows.Controls;
global using System.Windows.Input;
global using System.Windows.Interop;
global using System.Windows.Media;
global using System.Windows.Media.Effects;
global using System.Windows.Media.Imaging;
global using System.Windows.Shapes;

// Aliases resolving the collision with System.Drawing / System.Windows.Forms
global using Application = System.Windows.Application;
global using Brush = System.Windows.Media.Brush;
global using Color = System.Windows.Media.Color;
global using Cursor = System.Windows.Input.Cursor;
global using Cursors = System.Windows.Input.Cursors;
global using Key = System.Windows.Input.Key;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
global using Rectangle = System.Windows.Shapes.Rectangle;
// System.Windows.Shapes.Path exists too - spell out that the file one is wanted
global using Path = System.IO.Path;