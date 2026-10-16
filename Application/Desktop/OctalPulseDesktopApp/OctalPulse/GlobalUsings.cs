// Global using aliases to resolve WPF/WinForms namespace conflicts
// caused by <UseWindowsForms>true</UseWindowsForms>.
// All unqualified names resolve to WPF/WinUI types throughout the project.
global using Application    = System.Windows.Application;
global using Binding        = System.Windows.Data.Binding;
global using Brush          = System.Windows.Media.Brush;
global using Brushes        = System.Windows.Media.Brushes;
global using Color          = System.Windows.Media.Color;
global using FontFamily     = System.Windows.Media.FontFamily;
global using FontStyle      = System.Windows.FontStyle;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using Image          = System.Windows.Controls.Image;
global using KeyEventArgs   = System.Windows.Input.KeyEventArgs;
global using MessageBox     = System.Windows.MessageBox;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using Point          = System.Windows.Point;
global using SolidColorBrush = System.Windows.Media.SolidColorBrush;
global using Stretch        = System.Windows.Media.Stretch;
global using UserControl    = System.Windows.Controls.UserControl;

