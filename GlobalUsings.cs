// Явные глобальные using'и вместо ImplicitUsings.
//
// Причина: проект использует System.Windows.Forms.NotifyIcon (иконка в трее),
// а вместе с ним SDK добавляет в глобальную область System.Drawing и
// System.Windows.Forms. Из-за этого в каждом WPF-файле типы Color, Brush,
// Rectangle, Application, KeyEventArgs становятся неоднозначными.
// Здесь импортируем ровно то, что нужно, и разрешаем конфликты алиасами.

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

// Алиасы, снимающие конфликт с System.Drawing / System.Windows.Forms
global using Application = System.Windows.Application;
global using Brush = System.Windows.Media.Brush;
global using Color = System.Windows.Media.Color;
global using Cursor = System.Windows.Input.Cursor;
global using Cursors = System.Windows.Input.Cursors;
global using Key = System.Windows.Input.Key;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
global using Rectangle = System.Windows.Shapes.Rectangle;
// System.Windows.Shapes.Path тоже существует — уточняем, что нужен файловый
global using Path = System.IO.Path;
