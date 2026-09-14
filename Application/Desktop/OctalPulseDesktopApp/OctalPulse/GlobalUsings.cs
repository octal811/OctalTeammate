// Global using aliases to resolve WPF/WinForms namespace conflicts
// caused by <UseWindowsForms>true</UseWindowsForms>.
// All unqualified names resolve to WPF/WinUI types throughout the project.
global using Application    = System.Windows.Application;
global using Binding        = System.Windows.Data.Binding;
global using Brush          = System.Windows.Media.Brush;
global using Color          = System.Windows.Media.Color;
global using FontStyle      = System.Windows.FontStyle;
global using MessageBox     = System.Windows.MessageBox;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using Point          = System.Windows.Point;
global using SolidColorBrush = System.Windows.Media.SolidColorBrush;
global using UserControl    = System.Windows.Controls.UserControl;
