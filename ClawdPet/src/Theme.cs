using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace ClawdPet
{
    /// <summary>Терминальная тема в духе Claude Code: тёмный фон, оранжевый акцент, моноширинный шрифт.</summary>
    public static class Theme
    {
        public static readonly FontFamily Font = new("Cascadia Mono, Consolas, Courier New");

        public static readonly Color Bg = Color.FromRgb(0x1A, 0x19, 0x17);
        public static readonly Color Panel = Color.FromRgb(0x20, 0x20, 0x1D);
        public static readonly Color Field = Color.FromRgb(0x26, 0x24, 0x1F);
        public static readonly Color Fg = Color.FromRgb(0xE7, 0xE3, 0xDB);
        public static readonly Color Dim = Color.FromRgb(0x8B, 0x85, 0x79);
        public static readonly Color Accent = Color.FromRgb(0xD9, 0x77, 0x57);
        public static readonly Color AccentSoft = Color.FromRgb(0x2B, 0x21, 0x1C);
        public static readonly Color Line = Color.FromRgb(0x3C, 0x39, 0x34);

        public static readonly SolidColorBrush BgBrush = Frozen(Bg);
        public static readonly SolidColorBrush PanelBrush = Frozen(Panel);
        public static readonly SolidColorBrush FieldBrush = Frozen(Field);
        public static readonly SolidColorBrush FgBrush = Frozen(Fg);
        public static readonly SolidColorBrush DimBrush = Frozen(Dim);
        public static readonly SolidColorBrush AccentBrush = Frozen(Accent);
        public static readonly SolidColorBrush AccentSoftBrush = Frozen(AccentSoft);
        public static readonly SolidColorBrush LineBrush = Frozen(Line);

        static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>Свежий набор стилей для окна или меню.</summary>
        public static ResourceDictionary Load() => (ResourceDictionary)XamlReader.Parse(Xaml);

        const string Xaml = @"<ResourceDictionary
        xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

  <SolidColorBrush x:Key=""Bg"" Color=""#1A1917""/>
  <SolidColorBrush x:Key=""Panel"" Color=""#20201D""/>
  <SolidColorBrush x:Key=""Field"" Color=""#26241F""/>
  <SolidColorBrush x:Key=""Fg"" Color=""#E7E3DB""/>
  <SolidColorBrush x:Key=""Dim"" Color=""#8B8579""/>
  <SolidColorBrush x:Key=""Accent"" Color=""#D97757""/>
  <SolidColorBrush x:Key=""AccentSoft"" Color=""#2B211C""/>
  <SolidColorBrush x:Key=""Line"" Color=""#3C3934""/>
  <FontFamily x:Key=""Mono"">Cascadia Mono, Consolas, Courier New</FontFamily>

  <Style TargetType=""TextBlock"">
    <Setter Property=""Foreground"" Value=""{StaticResource Fg}""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""TextOptions.TextFormattingMode"" Value=""Display""/>
  </Style>

  <Style TargetType=""CheckBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource Fg}""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Padding"" Value=""0,3""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""CheckBox"">
          <Border Background=""Transparent"" Padding=""{TemplateBinding Padding}"">
            <StackPanel Orientation=""Horizontal"">
              <TextBlock x:Name=""Mark"" Text=""[ ]"" Foreground=""{StaticResource Dim}"" Margin=""0,0,8,0""
                         FontFamily=""{StaticResource Mono}"" FontSize=""12.5""/>
              <ContentPresenter VerticalAlignment=""Center"" RecognizesAccessKey=""False""/>
            </StackPanel>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsChecked"" Value=""True"">
              <Setter TargetName=""Mark"" Property=""Text"" Value=""[x]""/>
              <Setter TargetName=""Mark"" Property=""Foreground"" Value=""{StaticResource Accent}""/>
            </Trigger>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter Property=""Foreground"" Value=""{StaticResource Accent}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""TextBox"">
    <Setter Property=""Background"" Value=""{StaticResource Field}""/>
    <Setter Property=""Foreground"" Value=""{StaticResource Fg}""/>
    <Setter Property=""BorderBrush"" Value=""{StaticResource Line}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""CaretBrush"" Value=""{StaticResource Accent}""/>
    <Setter Property=""SelectionBrush"" Value=""{StaticResource Accent}""/>
    <Setter Property=""SelectionOpacity"" Value=""0.35""/>
    <Setter Property=""Padding"" Value=""7,4""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TextBox"">
          <Border Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}"" Padding=""{TemplateBinding Padding}"" CornerRadius=""2"">
            <ScrollViewer x:Name=""PART_ContentHost"" Focusable=""False"" Margin=""0""
                          VerticalScrollBarVisibility=""{TemplateBinding VerticalScrollBarVisibility}""/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""PasswordBox"">
    <Setter Property=""Background"" Value=""{StaticResource Field}""/>
    <Setter Property=""Foreground"" Value=""{StaticResource Fg}""/>
    <Setter Property=""BorderBrush"" Value=""{StaticResource Line}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""CaretBrush"" Value=""{StaticResource Accent}""/>
    <Setter Property=""SelectionBrush"" Value=""{StaticResource Accent}""/>
    <Setter Property=""Padding"" Value=""7,4""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""PasswordBox"">
          <Border Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}"" Padding=""{TemplateBinding Padding}"" CornerRadius=""2"">
            <ScrollViewer x:Name=""PART_ContentHost"" Focusable=""False"" Margin=""0""/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key=""ComboToggle"" TargetType=""ToggleButton"">
    <Setter Property=""Focusable"" Value=""False""/>
    <Setter Property=""ClickMode"" Value=""Press""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ToggleButton"">
          <Border x:Name=""Bd"" Background=""{StaticResource Field}"" BorderBrush=""{StaticResource Line}"" BorderThickness=""1"" CornerRadius=""2"">
            <TextBlock Text=""▾"" HorizontalAlignment=""Right"" VerticalAlignment=""Center"" Margin=""0,0,9,0""
                       Foreground=""{StaticResource Accent}"" FontFamily=""{StaticResource Mono}"" FontSize=""11""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Bd"" Property=""BorderBrush"" Value=""{StaticResource Accent}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ComboBox"">
    <Setter Property=""Foreground"" Value=""{StaticResource Fg}""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""Height"" Value=""28""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ComboBox"">
          <Grid>
            <ToggleButton x:Name=""Toggle"" Style=""{StaticResource ComboToggle}""
                          IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}""/>
            <ContentPresenter Content=""{TemplateBinding SelectionBoxItem}""
                              ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}""
                              Margin=""9,0,26,0"" VerticalAlignment=""Center"" IsHitTestVisible=""False""/>
            <Popup x:Name=""PART_Popup"" Placement=""Bottom"" IsOpen=""{TemplateBinding IsDropDownOpen}""
                   AllowsTransparency=""True"" Focusable=""False"" PopupAnimation=""Slide"">
              <Border Background=""{StaticResource Panel}"" BorderBrush=""{StaticResource Accent}"" BorderThickness=""1""
                      MinWidth=""{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"">
                <ScrollViewer MaxHeight=""240"" VerticalScrollBarVisibility=""Auto"">
                  <ItemsPresenter/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ComboBoxItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource Fg}""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ComboBoxItem"">
          <Border x:Name=""Bd"" Background=""Transparent"" Padding=""9,5"">
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsHighlighted"" Value=""True"">
              <Setter TargetName=""Bd"" Property=""Background"" Value=""{StaticResource AccentSoft}""/>
            </Trigger>
            <Trigger Property=""IsSelected"" Value=""True"">
              <Setter Property=""Foreground"" Value=""{StaticResource Accent}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""Button"">
    <Setter Property=""Foreground"" Value=""{StaticResource Accent}""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""Bd"" Background=""Transparent"" BorderBrush=""{StaticResource Accent}"" BorderThickness=""1""
                  Padding=""{TemplateBinding Padding}"" CornerRadius=""3"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""Bd"" Property=""Background"" Value=""{StaticResource Accent}""/>
              <Setter Property=""Foreground"" Value=""{StaticResource Bg}""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""Bd"" Property=""Opacity"" Value=""0.7""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""TabControl"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TabControl"">
          <Grid>
            <Grid.RowDefinitions>
              <RowDefinition Height=""Auto""/>
              <RowDefinition Height=""*""/>
            </Grid.RowDefinitions>
            <StackPanel x:Name=""HeaderPanel"" Orientation=""Horizontal"" IsItemsHost=""True"" Margin=""0,0,0,10""/>
            <Grid Grid.Row=""1"">
              <Rectangle Stroke=""{StaticResource Accent}"" StrokeThickness=""1"" StrokeDashArray=""4 3""
                         RadiusX=""5"" RadiusY=""5"" Fill=""{StaticResource Bg}""/>
              <ContentPresenter x:Name=""PART_SelectedContentHost"" ContentSource=""SelectedContent"" Margin=""14,12""/>
            </Grid>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""TabItem"">
    <Setter Property=""Foreground"" Value=""{StaticResource Dim}""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TabItem"">
          <Border x:Name=""Bd"" Background=""Transparent"" BorderBrush=""Transparent"" BorderThickness=""0,0,0,1""
                  Padding=""10,3"" Margin=""0,0,14,0"">
            <ContentPresenter ContentSource=""Header"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsSelected"" Value=""True"">
              <Setter TargetName=""Bd"" Property=""BorderBrush"" Value=""{StaticResource Accent}""/>
              <Setter Property=""Foreground"" Value=""{StaticResource Accent}""/>
            </Trigger>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter Property=""Foreground"" Value=""{StaticResource Fg}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key=""SbThumb"" TargetType=""Thumb"">
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Thumb"">
          <Border Background=""#4A463E"" CornerRadius=""3"" Margin=""1""/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ScrollBar"">
    <Setter Property=""Width"" Value=""8""/>
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ScrollBar"">
          <Grid Background=""{TemplateBinding Background}"">
            <Track x:Name=""PART_Track"">
              <Track.Thumb>
                <Thumb Style=""{StaticResource SbThumb}""/>
              </Track.Thumb>
            </Track>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property=""Orientation"" Value=""Vertical"">
              <Setter TargetName=""PART_Track"" Property=""IsDirectionReversed"" Value=""True""/>
            </Trigger>
            <Trigger Property=""Orientation"" Value=""Horizontal"">
              <Setter Property=""Width"" Value=""Auto""/>
              <Setter Property=""Height"" Value=""8""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ToolTip"">
    <Setter Property=""Foreground"" Value=""{StaticResource Fg}""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""FontSize"" Value=""12""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ToolTip"">
          <Border Background=""{StaticResource Panel}"" BorderBrush=""{StaticResource Accent}"" BorderThickness=""1""
                  Padding=""8,4"" CornerRadius=""3"">
            <ContentPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ContextMenu"">
    <Setter Property=""HasDropShadow"" Value=""False""/>
    <Setter Property=""FontFamily"" Value=""{StaticResource Mono}""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ContextMenu"">
          <Grid Margin=""9"" Background=""Transparent"">
            <ItemsPresenter/>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key=""RawMenuItem"" TargetType=""MenuItem"">
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""MenuItem"">
          <ContentPresenter ContentSource=""Header""/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";
    }
}
