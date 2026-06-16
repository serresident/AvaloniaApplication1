# -*- coding: utf-8 -*-
import re

file_path = r"c:\Users\ess2\source\repos\AvaloniaApplication1\AvaloniaApplication1\Views\DashboardView.axaml"

with open(file_path, 'rb') as f:
    raw = f.read()

if raw.startswith(b'\xef\xbb\xbf'):
    content_bytes = raw[3:]
else:
    content_bytes = raw

content = content_bytes.decode('utf-8')

# Normalize line endings to \n to simplify regex matching
content = content.replace('\r\n', '\n').replace('\r', '\n')

# 1. Locate the Valve template substring
valve_template_pattern = re.compile(
    r'(<DataTemplate DataType="\{x:Type vm:ValveWidgetViewModel\}">.*?</DataTemplate>)',
    re.DOTALL
)

match = valve_template_pattern.search(content)
if not match:
    print("ERROR: Valve template not found!")
    exit(1)

valve_template = match.group(1)
print("Found Valve template block.")

# 2. Inside the Valve template, replace the layout Grid with DockPanel
# We search for the <Border Background="Transparent" ...> block up to the matching </Border>
# inside the template.
layout_pattern = re.compile(
    r'(<Border Background="Transparent" BorderThickness="0" Margin="0" ClipToBounds="False">\s*)'
    r'<Grid RowDefinitions="\*,Auto" ClipToBounds="False">\s*'
    r'(<!-- Valve Graphic -->\s*<Grid Grid\.Row="0" Margin="0" ClipToBounds="False">\s*<v:ValveControl\s+.*?\s*/>\s*</Grid>\s*)'
    r'(<!-- Title and Value -->\s*<StackPanel Grid\.Row="1" HorizontalAlignment="Center" Margin="0,0,0,5">\s*<TextBlock Text="\{Binding Title\}"[^>]*/>\s*<TextBlock Text="\{Binding DisplayValue\}"[^>]*/>\s*</StackPanel>\s*)'
    r'</Grid>\s*'
    r'</Border>',
    re.DOTALL
)

new_layout = (
    r'\1<DockPanel Tag="{Binding Rotation}" ClipToBounds="False">\n'
    r'                                            <!-- Title and Value -->\n'
    r'                                            <StackPanel Tag="{Binding Rotation}" DockPanel.Dock="Bottom" HorizontalAlignment="Center" Margin="0,0,0,5">\n'
    r'                                                <StackPanel.Styles>\n'
    r'                                                    <Style Selector="StackPanel[Tag=180]">\n'
    r'                                                        <Setter Property="DockPanel.Dock" Value="Top" />\n'
    r'                                                    </Style>\n'
    r'                                                </StackPanel.Styles>\n'
    r'                                                <TextBlock Text="{Binding Title}" FontSize="12" Foreground="Gray" HorizontalAlignment="Center" />\n'
    r'                                                <TextBlock Text="{Binding DisplayValue}" FontSize="12" FontWeight="Bold" Foreground="{Binding CurrentColor, Converter={StaticResource HexToBrush}}" HorizontalAlignment="Center" />\n'
    r'                                            </StackPanel>\n\n'
    r'                                            <!-- Valve Graphic -->\n'
    r'                                            <Grid ClipToBounds="False">\n'
    r'                                                <v:ValveControl \n'
    r'                                                    ValveType="{Binding ValveType}"\n'
    r'                                                    IsOpen="{Binding IsOpen}"\n'
    r'                                                    Setpoint="{Binding Setpoint}"\n'
    r'                                                    Feedback="{Binding Feedback}"\n'
    r'                                                    HasFeedbackSource="{Binding HasFeedbackSource}"\n'
    r'                                                    IsVertical="{Binding IsVertical}"\n'
    r'                                                    ActuatorType="{Binding ActuatorType}"\n'
    r'                                                    ActiveColor="{Binding ActiveColor}"\n'
    r'                                                    InactiveColor="{Binding InactiveColor}"\n'
    r'                                                    Rotation="{Binding Rotation}"\n'
    r'                                                    IsAlarmFlashing="{Binding IsAlarmFlashing}"\n'
    r'                                                    IsAlarmFlashState="{Binding IsAlarmFlashState}"\n'
    r'                                                    ShowStaticAlarmIcon="{Binding ShowStaticAlarmIcon}"\n'
    r'                                                    ShowFeedbackBar="True" />\n'
    r'                                            </Grid>\n'
    r'                                        </DockPanel>\n'
    r'                                    </Border>'
)

valve_template_new, count = layout_pattern.subn(new_layout, valve_template)
if count > 0:
    print("Valve layout replaced successfully inside template.")
else:
    print("WARNING: Valve layout pattern not matched inside template! Trying fallback match.")
    # Fallback to a broader match
    fallback_layout_pattern = re.compile(
        r'<Border Background="Transparent" BorderThickness="0" Margin="0" ClipToBounds="False">.*?</Border>',
        re.DOTALL
    )
    # Let's see if we can substitute the entire Border block
    new_dockpanel_border = """<Border Background="Transparent" BorderThickness="0" Margin="0" ClipToBounds="False">
                                        <DockPanel Tag="{Binding Rotation}" ClipToBounds="False">
                                            <!-- Title and Value -->
                                            <StackPanel Tag="{Binding Rotation}" DockPanel.Dock="Bottom" HorizontalAlignment="Center" Margin="0,0,0,5">
                                                <StackPanel.Styles>
                                                    <Style Selector="StackPanel[Tag=180]">
                                                        <Setter Property="DockPanel.Dock" Value="Top" />
                                                    </Style>
                                                </StackPanel.Styles>
                                                <TextBlock Text="{Binding Title}" FontSize="12" Foreground="Gray" HorizontalAlignment="Center" />
                                                <TextBlock Text="{Binding DisplayValue}" FontSize="12" FontWeight="Bold" Foreground="{Binding CurrentColor, Converter={StaticResource HexToBrush}}" HorizontalAlignment="Center" />
                                            </StackPanel>

                                            <!-- Valve Graphic -->
                                            <Grid ClipToBounds="False">
                                                <v:ValveControl 
                                                    ValveType="{Binding ValveType}"
                                                    IsOpen="{Binding IsOpen}"
                                                    Setpoint="{Binding Setpoint}"
                                                    Feedback="{Binding Feedback}"
                                                    HasFeedbackSource="{Binding HasFeedbackSource}"
                                                    IsVertical="{Binding IsVertical}"
                                                    ActuatorType="{Binding ActuatorType}"
                                                    ActiveColor="{Binding ActiveColor}"
                                                    InactiveColor="{Binding InactiveColor}"
                                                    Rotation="{Binding Rotation}"
                                                    IsAlarmFlashing="{Binding IsAlarmFlashing}"
                                                    IsAlarmFlashState="{Binding IsAlarmFlashState}"
                                                    ShowStaticAlarmIcon="{Binding ShowStaticAlarmIcon}"
                                                    ShowFeedbackBar="True" />
                                            </Grid>
                                        </DockPanel>
                                    </Border>"""
    valve_template_new, count = fallback_layout_pattern.subn(new_dockpanel_border, valve_template, count=1)
    if count > 0:
        print("Valve layout replaced successfully via fallback match.")
    else:
        print("ERROR: Fallback layout match failed!")

# 3. Inside the Valve template, add "Rotate" to the context menu
# Match: <MenuItem Header="На задний план" ... /> \s* <Separator /> \s* <MenuItem Header="Свойства"
menu_pattern = re.compile(
    r'(<MenuItem Header="На задний план"[^>]*/>\s*<Separator />\s*)(<MenuItem Header="Свойства")',
    re.DOTALL
)

valve_template_new, m_count = menu_pattern.subn(
    r'\1<MenuItem Header="Повернуть" Command="{Binding RotateCommand}" />\n                                            <Separator />\n                                            \2',
    valve_template_new
)

if m_count > 0:
    print("Valve context menu Rotate item added successfully.")
else:
    print("WARNING: Context menu insertion pattern not matched!")

# 4. Replace the old valve template with the new one in the main file content
content = content.replace(valve_template, valve_template_new)

# 5. Restore carriage returns to match Windows standard
out = content.replace('\n', '\r\n')

# Check BOM
if raw.startswith(b'\xef\xbb\xbf'):
    out_bytes = b'\xef\xbb\xbf' + out.encode('utf-8')
else:
    out_bytes = out.encode('utf-8')

with open(file_path, 'wb') as f:
    f.write(out_bytes)

print("Replacement script run completed.")
